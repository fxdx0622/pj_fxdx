using System.Collections.Generic;
using UnityEngine;
using UApp = UnityEngine.Application;
using UDbg = UnityEngine.Debug;
using UObj = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 指定 SortingLayer の SpriteRenderer に、未アタッチなら
/// GrowthDriver / GrowthOnVisibleTrigger を付与し、
/// 必要なら Growth 対応マテリアルも自動設定するユーティリティ。
/// 「フィルタで自動収集」⇔「手動リストのみ」を切替可能。
/// </summary>
[ExecuteAlways]
public class GrowthAutoAttacher : MonoBehaviour
{
	// ===== Mode =====
	[Header("Mode")]
	[Tooltip("ON: 下の明示リストだけに適用 / OFF: フィルタで自動収集した対象に適用")]
	public bool affectOnlyExplicitList = false;

	[Tooltip("手動で管理する対象（SpriteRenderer）。このリストにのみ適用したい場合は上のスイッチをONにします。")]
	public List<SpriteRenderer> explicitTargets = new List<SpriteRenderer>();

	[Header("構造/Seed の一括上書き（任意）")]
	public bool overrideCentersFromProfile = false;      // centers系（配列/個数/モーション）
	public bool overrideSeedAndRandomFromProfile = false; // Seed/Random系

	// ===== Filter (used when affectOnlyExplicitList = false) =====
	[Header("Filter (自動収集モード時に使用)")]
	[Tooltip("ここに列挙した Sorting Layer 名の SpriteRenderer が対象。空配列なら全レイヤー。")]
	public string[] sortingLayerNames = new string[] { "Building" };

	[Tooltip("非アクティブのオブジェクトも対象に含める")]
	public bool includeInactive = false;

	// ===== What to attach =====
	[Header("What to attach (Components)")]
	[Tooltip("GrowthDriver を未アタッチのものにだけ付与")]
	public bool attachGrowthDriver = true;

	[Tooltip("GrowthOnVisibleTrigger を未アタッチのものにだけ付与")]
	public bool attachVisibleTrigger = true;

	// ===== Material =====
	[Header("Material (任意)")]
	[Tooltip("true: 現在のマテリアルが Growth 対応でない場合にだけ割当")]
	public bool assignGrowthMaterialIfNeeded = true;

	[Tooltip("割り当てに使う既製マテリアル（推奨）。未設定なら growthShader から新規生成")]
	public Material defaultGrowthMaterial;

	[Tooltip("defaultGrowthMaterial が未設定のとき使用するシェーダ。ここから新規 Material を生成して割当")]
	public Shader growthShader;

	[Tooltip("true: オブジェクトごとに個別インスタンス（sr.material）。false: 共有（sr.sharedMaterial）")]
	public bool perObjectMaterialInstance = true;

	[Tooltip("既にGrowth対応マテリアル(_Growth有り)なら上書きしない（安全）")]
	public bool skipIfAlreadyGrowthReady = true;

	// ===== Run / Log =====
	[Header("Run")]
	[Tooltip("有効化時に自動実行（エディタ/再生問わず）")]
	public bool runOnEnable = false;

	[Tooltip("処理結果をログ出力")]
	public bool logSummary = true;

	void OnEnable()
	{
		if (runOnEnable) AttachNow();
	}

	[ContextMenu("Attach Growth Components + Material (One-shot)")]
	public void AttachNow()
	{
		var targets = EnumerateTargets();
		int scanned = 0, addedDrv = 0, addedTrig = 0, matAssigned = 0, matSkipped = 0;

		foreach (var sr in targets)
		{
			if (!sr) continue;
			scanned++;

			var go = sr.gameObject;

			// --- Components ---
			if (attachGrowthDriver && !go.GetComponent<GrowthDriver>())
			{
#if UNITY_EDITOR
                if (!UApp.isPlaying) Undo.AddComponent<GrowthDriver>(go);
                else go.AddComponent<GrowthDriver>();
#else
				go.AddComponent<GrowthDriver>();
#endif
				addedDrv++;
			}

			if (attachVisibleTrigger && !go.GetComponent<GrowthOnVisibleTrigger>())
			{
#if UNITY_EDITOR
                if (!UApp.isPlaying) Undo.AddComponent<GrowthOnVisibleTrigger>(go);
                else go.AddComponent<GrowthOnVisibleTrigger>();
#else
				go.AddComponent<GrowthOnVisibleTrigger>();
#endif
				addedTrig++;
			}

			// --- Material ---
			if (assignGrowthMaterialIfNeeded)
			{
				var current = sr.sharedMaterial; // まず共有参照で判定
				bool alreadyGrowth = IsGrowthReady(current); // _Growth を持つか

				if (skipIfAlreadyGrowthReady && alreadyGrowth)
				{
					matSkipped++;
					continue;
				}

				Material src = defaultGrowthMaterial;
				if (src == null && growthShader != null)
					src = new Material(growthShader) { name = "Mat(GrowthAutoAttacher)" };

				if (src == null) continue; // 割当不可

#if UNITY_EDITOR
    if (!UApp.isPlaying) Undo.RecordObject(sr, "Assign Growth Material");
#endif
				if (perObjectMaterialInstance)
				{
					var inst = new Material(src);
					sr.material = inst; // 個別インスタンス
				}
				else
				{
					sr.sharedMaterial = src; // 共有
				}
				matAssigned++;
			}
		}

		if (logSummary)
		{
			string mode = affectOnlyExplicitList ? "ExplicitList" : "Filter";
			UDbg.Log("[GrowthAutoAttacher] Mode=" + mode +
					 ", Scanned=" + scanned +
					 ", Added Driver=" + addedDrv +
					 ", Added Trigger=" + addedTrig +
					 ", Mat Assigned=" + matAssigned +
					 ", Mat Skipped=" + matSkipped);
		}
	}

	// === 対象列挙 ===
	IEnumerable<SpriteRenderer> EnumerateTargets()
	{
		if (affectOnlyExplicitList)
		{
			// 手動リストから（null を取り除きつつ）
			for (int i = explicitTargets.Count - 1; i >= 0; --i)
			{
				var sr = explicitTargets[i];
				if (!sr) { explicitTargets.RemoveAt(i); continue; }
				yield return sr;
			}
			yield break;
		}

		// フィルタ収集
		var list = ScanByFilter();
		foreach (var sr in list) yield return sr;
	}

	// フィルタでスキャン → リスト返却
	List<SpriteRenderer> ScanByFilter()
	{
		var result = new List<SpriteRenderer>();

		// レイヤー名集合
		var layerNameSet = new HashSet<string>();
		if (sortingLayerNames != null)
		{
			foreach (var raw in sortingLayerNames)
			{
				if (string.IsNullOrWhiteSpace(raw)) continue;
				layerNameSet.Add(raw.Trim());
			}
		}
		bool filterByLayerName = layerNameSet.Count > 0;

		// モードを決める
		FindObjectsInactive inactiveMode;
		if (includeInactive)inactiveMode = FindObjectsInactive.Include;
		else inactiveMode = FindObjectsInactive.Exclude;
		SpriteRenderer[] srs = UObj.FindObjectsByType<SpriteRenderer>(inactiveMode, FindObjectsSortMode.None);

		if (srs == null) return result;

		foreach (var sr in srs)
		{
			if (!sr) continue;
			if (filterByLayerName && !layerNameSet.Contains(sr.sortingLayerName))
				continue;

			result.Add(sr);
		}
		return result;
	}

	// Growth対応シェーダ判定：_Growth プロパティを持っているか
	static bool IsGrowthReady(Material m) => (m != null && m.HasProperty("_Growth"));

	// === GrowthAutoAttacher クラス内に追記 ===
	[Header("見た目の一括適用（明示リスト対象）")]
	[Tooltip("ONで、明示リスト(explicitTargets)に入っている SpriteRenderer のみに見た目を一括適用します")]
	public bool enableLookApply = false;

	[Tooltip("見た目のプリセット（色/ブレンド/ノイズ等）")]
	public GrowthDriverProfile lookProfile;

	[Tooltip("必要なら、対象の SpriteRenderer に Growth 用マテリアルを割り当てます")]
	public bool alsoAssignMaterial = false;

	[Tooltip("割り当てるマテリアル（SH_GrowthSimple2D 等のシェーダが入ったもの）")]
	public Material growthMaterial;

	[Tooltip("true=sharedMaterial 変更（インスタンス増加なし）。false=material 変更（個別インスタンスになる）")]
	public bool assignSharedMaterial = true;

	[Tooltip("対象に GrowthDriver が付いている場合、同じ見た目値を GrowthDriver 側にもコピーして即反映します")]
	public bool syncGrowthDriverIfPresent = true;


	// --- Shader Property IDs（SH_GrowthSimple2D と一致させてください） ---
	static readonly int ID_BlendMode = Shader.PropertyToID("_BlendMode");
	static readonly int ID_BloodMix = Shader.PropertyToID("_BloodMix");
	static readonly int ID_InsideAlpha = Shader.PropertyToID("_InsideAlpha");
	static readonly int ID_IgnoreBase = Shader.PropertyToID("_IgnoreBaseAlpha");
	static readonly int ID_EdgeWidth = Shader.PropertyToID("_EdgeWidth");
	static readonly int ID_RimBoost = Shader.PropertyToID("_RimBoost");
	static readonly int ID_RimColor = Shader.PropertyToID("_RimColor");
	static readonly int ID_BloodColor = Shader.PropertyToID("_BloodColor");
	static readonly int ID_NoiseScale = Shader.PropertyToID("_NoiseScale");
	static readonly int ID_NoiseAmp = Shader.PropertyToID("_NoiseAmp");
	static readonly int ID_EdgeNoiseScroll = Shader.PropertyToID("_EdgeNoiseScroll");
	static readonly int ID_EdgeFlow = Shader.PropertyToID("_EdgeFlowStrength");
	static readonly int ID_RimWobbleAmp = Shader.PropertyToID("_RimWobbleAmp");
	static readonly int ID_RimWobbleFreq = Shader.PropertyToID("_RimWobbleFreq");

	static MaterialPropertyBlock s_mpb;  // 使い回し

	[ContextMenu("見た目を適用（明示リスト）")]
	public void ApplyLookToExplicitList()
	{
		if (!enableLookApply || lookProfile == null) return;
		if (s_mpb == null) s_mpb = new MaterialPropertyBlock();

		int applied = 0;
		foreach (var sr in explicitTargets)
		{
			if (!sr) continue;

			var gd = sr.GetComponent<GrowthDriver>();
			bool hasGD = gd != null;
			bool previewLock = hasGD && gd.IsEditorPreviewActive;

			// プレビュー中は、マテリアルやMPBを触らない＝値だけ GrowthDriver に流す
			if (previewLock)
			{
				if (syncGrowthDriverIfPresent)
				{
					// 見た目の値をgdにコピー（あなたの既存コピー処理をそのまま使用）
					gd.applyLookOverrides = true;
					gd.blendMode = (GrowthDriver.BlendMode)lookProfile.blendMode;
					gd.bloodMix = lookProfile.bloodMix;
					gd.insideAlpha = lookProfile.insideAlpha;
					gd.ignoreBaseAlpha = lookProfile.ignoreBaseAlpha;
					gd.edgeWidth = lookProfile.edgeWidth;
					gd.rimBoost = lookProfile.rimBoost;
					gd.rimColor = lookProfile.rimColor;
					gd.bloodColor = lookProfile.bloodColor;
					gd.noiseScale = lookProfile.noiseScale;
					gd.noiseAmp = lookProfile.noiseAmp;
					gd.edgeNoiseScroll = lookProfile.edgeNoiseScroll;
					gd.edgeFlowStrength = lookProfile.edgeFlowStrength;
					gd.rimWobbleAmp = lookProfile.rimWobbleAmp;
					gd.rimWobbleFreq = lookProfile.rimWobbleFreq;

					gd.ForceApplyFromManager(); // 最終反映（中でプレビューロックが効く）
				}
				continue; // ここで次へ。MPB直書き/マテリアル変更はしない
			}

			//  GrowthDriver がいる場合：MPB直書きはしない。値をgdにコピーしてApply。
			if (hasGD && syncGrowthDriverIfPresent)
			{
				if (alsoAssignMaterial && growthMaterial)
				{
					if (assignSharedMaterial) sr.sharedMaterial = growthMaterial;
					else sr.material = growthMaterial;
				}

				gd.applyLookOverrides = true;
				gd.blendMode = (GrowthDriver.BlendMode)lookProfile.blendMode;
				gd.bloodMix = lookProfile.bloodMix;
				gd.insideAlpha = lookProfile.insideAlpha;
				gd.ignoreBaseAlpha = lookProfile.ignoreBaseAlpha;
				gd.edgeWidth = lookProfile.edgeWidth;
				gd.rimBoost = lookProfile.rimBoost;
				gd.rimColor = lookProfile.rimColor;
				gd.bloodColor = lookProfile.bloodColor;
				gd.noiseScale = lookProfile.noiseScale;
				gd.noiseAmp = lookProfile.noiseAmp;
				gd.edgeNoiseScroll = lookProfile.edgeNoiseScroll;
				gd.edgeFlowStrength = lookProfile.edgeFlowStrength;
				gd.rimWobbleAmp = lookProfile.rimWobbleAmp;
				gd.rimWobbleFreq = lookProfile.rimWobbleFreq;

				gd.ForceApplyFromManager();
				continue; // MPB直書きはしない
			}

			// 3) GrowthDriver が居ない場合のみ、従来通り MPB直書きで見た目適用
			if (s_mpb == null) s_mpb = new MaterialPropertyBlock();
			if (alsoAssignMaterial && growthMaterial)
			{
				if (assignSharedMaterial) sr.sharedMaterial = growthMaterial;
				else sr.material = growthMaterial;
			}
			s_mpb.Clear();
			s_mpb.SetFloat(ID_BlendMode, (float)lookProfile.blendMode);
			s_mpb.SetFloat(ID_BloodMix, lookProfile.bloodMix);
			s_mpb.SetFloat(ID_InsideAlpha, lookProfile.insideAlpha);
			s_mpb.SetFloat(ID_IgnoreBase, lookProfile.ignoreBaseAlpha ? 1f : 0f);
			s_mpb.SetFloat(ID_EdgeWidth, lookProfile.edgeWidth);
			s_mpb.SetFloat(ID_RimBoost, lookProfile.rimBoost);
			s_mpb.SetColor(ID_RimColor, lookProfile.rimColor);
			s_mpb.SetColor(ID_BloodColor, lookProfile.bloodColor);
			s_mpb.SetFloat(ID_NoiseScale, lookProfile.noiseScale);
			s_mpb.SetFloat(ID_NoiseAmp, lookProfile.noiseAmp);
			s_mpb.SetVector(ID_EdgeNoiseScroll, new Vector4(lookProfile.edgeNoiseScroll.x, lookProfile.edgeNoiseScroll.y, 0f, 0f));
			s_mpb.SetFloat(ID_EdgeFlow, lookProfile.edgeFlowStrength);
			s_mpb.SetFloat(ID_RimWobbleAmp, lookProfile.rimWobbleAmp);
			s_mpb.SetFloat(ID_RimWobbleFreq, lookProfile.rimWobbleFreq);
			sr.SetPropertyBlock(s_mpb);
		}


		Debug.Log($"[GrowthAutoAttacher] 見た目を適用（明示リスト）: {applied} 件");
	}

	// ===== Editor helpers =====
#if UNITY_EDITOR
    [ContextMenu("List: Replace From Filter Scan")]
    public void ReplaceExplicitListFromFilter()
    {
        var list = ScanByFilter();
        Undo.RecordObject(this, "Replace Explicit Targets From Filter");
        explicitTargets = list;
        EditorUtility.SetDirty(this);
    }

    [ContextMenu("List: Add Current Selection")]
    public void AddSelectionToExplicitList()
    {
        var gos = Selection.gameObjects;
        if (gos == null || gos.Length == 0) return;

        Undo.RecordObject(this, "Add Selection To Explicit Targets");
        var set = new HashSet<SpriteRenderer>(explicitTargets);
        foreach (var go in gos)
        {
            var sr = go ? go.GetComponent<SpriteRenderer>() : null;
            if (sr && !set.Contains(sr)) { explicitTargets.Add(sr); set.Add(sr); }
        }
        EditorUtility.SetDirty(this);
    }

    [ContextMenu("List: Remove Missing/Null")]
    public void CleanExplicitList()
    {
        Undo.RecordObject(this, "Clean Explicit Targets");
        explicitTargets.RemoveAll(x => x == null);
        EditorUtility.SetDirty(this);
    }
#endif
}
