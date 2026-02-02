
#if UNITY_EDITOR
using System.IO;
using UnityEngine;


using UnityEditor;


/// <summary>
/// シーン上の SpriteRenderer 2つを、配置関係込みで 1枚のPNG(Sprite)にマージするツール。
/// - SpriteRenderer.bounds からワールドAABBを取り、
///   その範囲をピクセル化して、各ピクセルを元スプライトからサンプリングする。
/// - 新スプライトの pivot は RendererA の transform.position に対応させる。
/// </summary>
public class SpriteMergeTool : EditorWindow
{
	[Header("マージ対象 (Scene)")]
	[SerializeField] private SpriteRenderer rendererA;
	[SerializeField] private SpriteRenderer rendererB;

	[Header("出力設定")]
	[SerializeField] private string outputFolder = "Assets/MergedSprites";
	[SerializeField] private string outputFileName = "MergedFromScene.png";

	[MenuItem("Tools/Sprite Merge From Scene")]
	public static void Open()
	{
		var window = GetWindow<SpriteMergeTool>("Sprite Merge From Scene");
		window.Show();
	}

	private void OnGUI()
	{
		EditorGUILayout.LabelField("Sprite Merge From Scene", EditorStyles.boldLabel);
		EditorGUILayout.Space();

		rendererA = (SpriteRenderer)EditorGUILayout.ObjectField("Renderer A", rendererA, typeof(SpriteRenderer), true);
		rendererB = (SpriteRenderer)EditorGUILayout.ObjectField("Renderer B", rendererB, typeof(SpriteRenderer), true);

		EditorGUILayout.Space();

		outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
		outputFileName = EditorGUILayout.TextField("Output File Name", outputFileName);

		EditorGUILayout.Space();

		if (GUILayout.Button("Merge (Scene Transform Aware)"))
		{
			MergeFromScene();
		}
	}

	//==============================
	// Read/Write 一時変更用ヘルパー
	//==============================

	private struct TextureReadWriteBackup
	{
		public string assetPath;
		public bool originalIsReadable;
		public bool changed;
	}

	/// <summary>
	/// 指定 Sprite のテクスチャを読み取り可能にする（必要なら一時的に isReadable = true にして再インポート）。
	/// </summary>
	private void EnsureReadable(Sprite sprite, ref TextureReadWriteBackup backup)
	{
		backup.assetPath = null;
		backup.originalIsReadable = false;
		backup.changed = false;

		if (sprite == null) return;

		Texture2D tex = sprite.texture;
		if (tex == null) return;

		string path = AssetDatabase.GetAssetPath(tex);
		if (string.IsNullOrEmpty(path)) return;

		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null) return;

		backup.assetPath = path;
		backup.originalIsReadable = importer.isReadable;

		if (!importer.isReadable)
		{
			importer.isReadable = true;
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			backup.changed = true;
		}
	}

	/// <summary>
	/// EnsureReadable で変更した isReadable を元に戻す。
	/// </summary>
	private void RestoreReadWrite(ref TextureReadWriteBackup backup)
	{
		if (!backup.changed) return;
		if (string.IsNullOrEmpty(backup.assetPath)) return;

		var importer = AssetImporter.GetAtPath(backup.assetPath) as TextureImporter;
		if (importer == null) return;

		importer.isReadable = backup.originalIsReadable;
		AssetDatabase.ImportAsset(backup.assetPath, ImportAssetOptions.ForceUpdate);
	}

	//==============================
	// 本体
	//==============================

	private void MergeFromScene()
	{
		if (rendererA == null || rendererB == null)
		{
			Debug.LogError("Renderer A / Renderer B が設定されていません。");
			return;
		}

		Sprite spriteA = rendererA.sprite;
		Sprite spriteB = rendererB.sprite;
		if (spriteA == null || spriteB == null)
		{
			Debug.LogError("Renderer の sprite が設定されていません。");
			return;
		}

		// Read/Write 一時 ON
		TextureReadWriteBackup backupA = default;
		TextureReadWriteBackup backupB = default;

		try
		{
			EnsureReadable(spriteA, ref backupA);
			EnsureReadable(spriteB, ref backupB);

			Texture2D texA = spriteA.texture;
			Texture2D texB = spriteB.texture;
			if (texA == null || texB == null)
			{
				Debug.LogError("Sprite から Texture を取得できませんでした。");
				return;
			}

			// Pixels Per Unit は揃っている前提
			if (Mathf.Abs(spriteA.pixelsPerUnit - spriteB.pixelsPerUnit) > 0.001f)
			{
				Debug.LogError("2つの Sprite の PixelsPerUnit が異なります。一致させてから実行してください。");
				return;
			}
			float ppu = spriteA.pixelsPerUnit;

			// --- 1) シーン上のバウンディングボックス（ワールドAABB）を取得 ---
			Bounds bounds = rendererA.bounds;
			bounds.Encapsulate(rendererB.bounds);

			Vector3 min = bounds.min;
			Vector3 size = bounds.size;

			float worldWidth = size.x;
			float worldHeight = size.y;

			int widthPx = Mathf.CeilToInt(worldWidth * ppu);
			int heightPx = Mathf.CeilToInt(worldHeight * ppu);

			if (widthPx <= 0 || heightPx <= 0)
			{
				Debug.LogError("計算された画像サイズが不正です。bounds / ppu を確認してください。");
				return;
			}

			// --- 2) 出力テクスチャを用意 ---
			Texture2D mergedTex = new Texture2D(widthPx, heightPx, TextureFormat.RGBA32, false);
			Color32[] pixels = new Color32[widthPx * heightPx];

			Color32 clear = new Color32(0, 0, 0, 0);
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i] = clear;
			}

			// --- 3) 変換行列や Sprite 情報を用意 ---
			Matrix4x4 worldToLocalA = rendererA.transform.worldToLocalMatrix;
			Matrix4x4 worldToLocalB = rendererB.transform.worldToLocalMatrix;

			Rect rectA = spriteA.rect;
			Rect rectB = spriteB.rect;
			Vector2 pivotA = spriteA.pivot;
			Vector2 pivotB = spriteB.pivot;

			// αブレンド: src over dst
			System.Func<Color32, Color32, Color32> alphaBlend = (dst, src) =>
			{
				float sa = src.a / 255.0f;
				float da = dst.a / 255.0f;
				float outA = sa + da * (1.0f - sa);

				if (outA <= 0.0f)
				{
					return new Color32(0, 0, 0, 0);
				}

				float sr = src.r / 255.0f;
				float sg = src.g / 255.0f;
				float sb = src.b / 255.0f;

				float dr = dst.r / 255.0f;
				float dg = dst.g / 255.0f;
				float db = dst.b / 255.0f;

				float outR = (sr * sa + dr * da * (1.0f - sa)) / outA;
				float outG = (sg * sa + dg * da * (1.0f - sa)) / outA;
				float outB = (sb * sa + db * da * (1.0f - sa)) / outA;

				byte br = (byte)Mathf.Clamp(Mathf.RoundToInt(outR * 255.0f), 0, 255);
				byte bg = (byte)Mathf.Clamp(Mathf.RoundToInt(outG * 255.0f), 0, 255);
				byte bb = (byte)Mathf.Clamp(Mathf.RoundToInt(outB * 255.0f), 0, 255);
				byte ba = (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255.0f), 0, 255);

				return new Color32(br, bg, bb, ba);
			};

			// --- 4) 各ピクセルに対して、ワールド座標から逆算してサンプリング ---
			for (int y = 0; y < heightPx; y++)
			{
				float wy = min.y + (y + 0.5f) / ppu;

				for (int x = 0; x < widthPx; x++)
				{
					float wx = min.x + (x + 0.5f) / ppu;
					Vector3 worldPos = new Vector3(wx, wy, rendererA.transform.position.z);

					Color32 col = clear;

					SampleSpriteAtWorld(spriteA, texA, rectA, pivotA, worldToLocalA, worldPos, ref col, alphaBlend);
					SampleSpriteAtWorld(spriteB, texB, rectB, pivotB, worldToLocalB, worldPos, ref col, alphaBlend);

					pixels[y * widthPx + x] = col;
				}
			}

			mergedTex.SetPixels32(pixels);
			mergedTex.Apply();

			// --- 5) PNG として保存 ---
			if (string.IsNullOrEmpty(outputFolder))
			{
				outputFolder = "Assets";
			}
			if (!outputFolder.StartsWith("Assets"))
			{
				Debug.LogError("Output Folder はプロジェクト内(Assets配下)で指定してください。");
				return;
			}

			// ファイル名の拡張子を必ず .png にする
			if (string.IsNullOrEmpty(outputFileName))
			{
				outputFileName = "MergedFromScene";
			}
			string fileBase = Path.GetFileNameWithoutExtension(outputFileName);
			if (string.IsNullOrEmpty(fileBase))
			{
				fileBase = "MergedFromScene";
			}
			outputFileName = fileBase + ".png";

			string dir = outputFolder.TrimEnd('/');
			if (!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			string path = Path.Combine(dir, outputFileName);
			byte[] pngData = mergedTex.EncodeToPNG();
			File.WriteAllBytes(path, pngData);
			Debug.Log("Scene 配置を反映して Sprite をマージしました: " + path);

			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

			// --- 6) Sprite としてのインポート設定 ---
			TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer != null)
			{
				importer.textureType = TextureImporterType.Sprite;
				importer.spriteImportMode = SpriteImportMode.Single;
				importer.mipmapEnabled = false;
				importer.alphaIsTransparency = true;

				// pivot を RendererA の transform.position に対応させる
				Vector3 pivotWorld = rendererA.transform.position;
				float pivotPixelX = (pivotWorld.x - min.x) * ppu;
				float pivotPixelY = (pivotWorld.y - min.y) * ppu;

				Vector2 pivotNormalized = new Vector2(
					pivotPixelX / widthPx,
					pivotPixelY / heightPx
				);

				importer.spritePivot = pivotNormalized;
				importer.spritePixelsPerUnit = ppu;

				AssetDatabase.WriteImportSettingsIfDirty(path);
				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			}

			AssetDatabase.Refresh();

			Sprite mergedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
			if (mergedSprite != null)
			{
				Debug.Log("Merged Sprite (Sceneベース) を生成しました: " + path, mergedSprite);
			}
			else
			{
				Debug.LogWarning("PNGは生成されましたが、Spriteとしてのロードに失敗しました。インポート設定を確認してください。");
			}
		}
		finally
		{
			// Read/Write を元に戻す
			RestoreReadWrite(ref backupA);
			RestoreReadWrite(ref backupB);
		}
	}

	/// <summary>
	/// ワールド座標 worldPos に対応する Sprite のピクセルをサンプリングして、αブレンドする。
	/// </summary>
	private static void SampleSpriteAtWorld(
		Sprite sprite,
		Texture2D tex,
		Rect rect,
		Vector2 pivot,
		Matrix4x4 worldToLocal,
		Vector3 worldPos,
		ref Color32 dst,
		System.Func<Color32, Color32, Color32> alphaBlend)
	{
		Vector3 local = worldToLocal.MultiplyPoint3x4(worldPos); // SpriteRenderer ローカル座標（pivot = 原点）
		float ppu = sprite.pixelsPerUnit;

		// pivot を基準に、ローカル座標 → ピクセル座標へ
		float px = local.x * ppu + pivot.x; // 0〜rect.width
		float py = local.y * ppu + pivot.y; // 0〜rect.height

		if (px < 0.0f || px >= rect.width || py < 0.0f || py >= rect.height)
		{
			return; // スプライト外
		}

		int sx = (int)(rect.x + px);
		int sy = (int)(rect.y + py);

		Color32 src = tex.GetPixel(sx, sy);
		if (src.a == 0)
		{
			return; // 透過なら無視
		}

		dst = alphaBlend(dst, src);
	}
}
#endif