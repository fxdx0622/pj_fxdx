using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shader "Custom/SH_FlexibleEffectSprite" 用コントローラ。
/// 既にシーンに配置済みの SpriteRenderer の「色」に対して、
/// _BlendMode / _BlendOpacity / _Color を使ってフィルタをかける。
/// スケール変更やスプライト差し替えは一切行わない。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class FlexibleEffectSpriteController : MonoBehaviour
{
	[Header("ターゲットマテリアル (SH_FlexibleEffectSprite)")]
	[Tooltip("Shader \"Custom/SH_FlexibleEffectSprite\" を割り当てたマテリアルを指定してください。")]
	[SerializeField] private Material effectMaterial;

	[Header("ブレンド設定")]
	[SerializeField] private BlendMode blendMode = BlendMode.Overlay;

	[Tooltip("画面効果の強さ(0〜1)。")]
	[Range(0f, 1f)]
	[SerializeField] private float effectStrength = 1f;

	[Header("マスク色 (白=ニュートラル寄り)")]
	[SerializeField] private Color maskColor = Color.white;

	private SpriteRenderer spriteRenderer;
	private MaterialPropertyBlock mpb;

	// シェーダープロパティID
	private static readonly int ID_BlendMode = Shader.PropertyToID("_BlendMode");
	private static readonly int ID_BlendOpacity = Shader.PropertyToID("_BlendOpacity");
	private static readonly int ID_Color = Shader.PropertyToID("_Color");

	// モードごとの「実際にシェーダへ渡す _BlendOpacity の上限」
	// 背景フィルタと同じ考え方で、安全側に少し絞っておく。
	private static readonly float[] MaxOpacityByMode =
	{
		0.45f, // 0: Screen
        1.00f, // 1: Multiply
        1.00f, // 2: Overlay
        1.00f, // 3: Negative
        0.85f, // 4: SoftLight
        0.60f, // 5: HardLight
        0.70f, // 6: VividLight
        1.00f, // 7: Grayscale
    };

	//==================================================
	// Unity ライフサイクル
	//==================================================

	private void Awake()
	{
		Init();
		Apply();
	}

	private void OnEnable()
	{
		Init();
		Apply();
	}

	private void OnValidate()
	{
		Init();
		Apply();
	}

	private void Init()
	{
		if (spriteRenderer == null)
			spriteRenderer = GetComponent<SpriteRenderer>();

		if (mpb == null)
			mpb = new MaterialPropertyBlock();

		// マテリアル自動割り当て（既存の spriteRenderer を壊さない範囲）
		if (spriteRenderer != null && effectMaterial != null)
		{
			if (spriteRenderer.sharedMaterial != effectMaterial)
			{
				spriteRenderer.sharedMaterial = effectMaterial;
			}
		}
	}

	private void Apply()
	{
		if (spriteRenderer == null || mpb == null) return;

		spriteRenderer.GetPropertyBlock(mpb);

		int modeIndex = Mathf.Clamp((int)blendMode, 0, MaxOpacityByMode.Length - 1);

		// ユーザーの 0〜1 を、モードごとの上限にマップ
		float maxOpacity = MaxOpacityByMode[modeIndex];
		float blendOpacity = Mathf.Clamp01(effectStrength) * maxOpacity;

		// Screen のときだけ「完全な真っ白」を少しだけ弱める
		Color finalColor = maskColor;
		if (blendMode == BlendMode.Screen)
		{
			float lum = 0.299f * maskColor.r + 0.587f * maskColor.g + 0.114f * maskColor.b;
			float mul = Mathf.Lerp(1.0f, 0.85f, lum); // 明るいほど 0.85 倍寄り
			finalColor = new Color(maskColor.r * mul, maskColor.g * mul, maskColor.b * mul, maskColor.a);
		}

		mpb.SetFloat(ID_BlendMode, modeIndex);
		mpb.SetFloat(ID_BlendOpacity, blendOpacity);
		mpb.SetColor(ID_Color, finalColor);

		spriteRenderer.SetPropertyBlock(mpb);
	}

	//==================================================
	// ランタイムから変更したいとき用 API
	//==================================================

	public void SetMode(BlendMode mode)
	{
		blendMode = mode;
		Apply();
	}

	public void SetEffectStrength(float value01)
	{
		effectStrength = Mathf.Clamp01(value01);
		Apply();
	}

	public void SetMaskColor(Color color)
	{
		maskColor = color;
		Apply();
	}

#if UNITY_EDITOR
	/// <summary>モードごとの簡易説明（インスペクター表示用）</summary>
	public static string GetModeDescription(BlendMode mode)
	{
		switch (mode)
		{
			case BlendMode.Screen:
				return "Screen: 明るさを持ち上げる合成。色はあまり意識せず、明るさ用として使用。\nおすすめ: Strength 0〜0.2 / Color 白〜グレー（明度だけ変えたいとき）。";

			case BlendMode.Multiply:
				return "Multiply: 暗くしつつ色味を足す合成。白〜黒は減光、有彩色で全体に色を追加。\nおすすめ: Strength ≒0.5 / Color はお好みの色。";

			case BlendMode.Overlay:
				return "Overlay: コントラストを強める合成。明部はより明るく、暗部はより暗く。色も足せる。\nおすすめ: Strength 0.7〜1.0 / Color お好み（赤×1.0などで強い印象）。";

			case BlendMode.Negative:
				return "Negative: スプライトの色を反転。暗部は明るく、明部は暗くなります。\nおすすめ: Strength 0.7〜1.0 / Color は範囲指定のみで色は意味なし。";

			case BlendMode.SoftLight:
				return "SoftLight: 柔らかいコントラスト調整。Overlay よりマイルド。\nおすすめ: Strength 0.3〜0.8 / Color 白〜淡い色でふんわりトーン調整。";

			case BlendMode.HardLight:
				return "HardLight: コントラスト強調寄り。明暗のメリハリがメインで、色変化は控えめ。\nおすすめ: Strength 0.3〜0.7 / Color やや暗めの色。";

			case BlendMode.VividLight:
				return "VividLight: 強いコントラスト＋はっきりした色変化。色付きのライトを強く乗せたいときに。\nおすすめ: Strength 0.3〜0.6 / Color ビビッドな有彩色（赤・シアン・マゼンタなど）。";

			case BlendMode.Grayscale:
				return "Grayscale: スプライトをモノクロ化。回想・停止・UI フォーカス演出に。\nおすすめ: Strength 0.4〜1.0 / Color は範囲のみで色は意味なし。";

			default:
				return "";
		}
	}
#endif
}

#if UNITY_EDITOR
/// <summary>
/// FlexibleEffectSpriteController 用カスタムインスペクタ。
/// 選択中モードの説明を HelpBox として表示する。
/// </summary>
[CustomEditor(typeof(FlexibleEffectSpriteController))]
public class FlexibleEffectSpriteControllerEditor : Editor
{
	private SerializedProperty effectMaterialProp;
	private SerializedProperty blendModeProp;
	private SerializedProperty effectStrengthProp;
	private SerializedProperty maskColorProp;

	private void OnEnable()
	{
		effectMaterialProp = serializedObject.FindProperty("effectMaterial");
		blendModeProp = serializedObject.FindProperty("blendMode");
		effectStrengthProp = serializedObject.FindProperty("effectStrength");
		maskColorProp = serializedObject.FindProperty("maskColor");
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.PropertyField(effectMaterialProp);
		EditorGUILayout.PropertyField(blendModeProp);
		EditorGUILayout.PropertyField(effectStrengthProp);
		EditorGUILayout.PropertyField(maskColorProp);

		EditorGUILayout.Space();

		var mode = (BlendMode)blendModeProp.enumValueIndex;
		string desc = FlexibleEffectSpriteController.GetModeDescription(mode);
		if (!string.IsNullOrEmpty(desc))
		{
			EditorGUILayout.HelpBox(desc, MessageType.Info);
		}

		EditorGUILayout.HelpBox(
			"共通: スプライトの形状・アルファはそのままに、色だけを変化させます。\nMask Color で色を付け、Effect Strength で強さを調整してください。",
			MessageType.None);

		serializedObject.ApplyModifiedProperties();
	}
}
#endif
