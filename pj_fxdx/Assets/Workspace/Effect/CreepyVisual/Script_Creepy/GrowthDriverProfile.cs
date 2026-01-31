using UnityEngine;

[CreateAssetMenu(fileName = "GrowthDriverProfile", menuName = "CreepyAura/GrowthDriverProfile (Full)")]
public class GrowthDriverProfile : ScriptableObject
{
	// === GrowthDriver と数値互換の簡易Enum ===
	public enum BlendMode { Normal = 0, Multiply = 1, Overlay = 2, Add = 3 }
	public enum CapMode { None, Fixed }
	public enum OscWave { Sine, Triangle }

	// =========================
	// Seed / Playback / Cap
	// =========================
	[Header("Seed")]
	[Tooltip("ON=自動Seed(InstanceID由来)、OFF=manualSeed固定")]
	public bool autoSeed = true;
	[Tooltip("手動Seed。値が同じなら同じ見た目")]
	public int manualSeed = 12345;
	[Tooltip("任意ラベルで見た目固定（空なら無効）")]
	public string seedLabel = "";

	[Header("Playback / Cap")]
	[Tooltip("0→最終到達値に達する秒数（基準）")]
	public float duration = 1.5f;
	[Tooltip("成長の最終到達モード（None/Fixed）")]
	public CapMode capMode = CapMode.None;
	[Range(0f, 1f), Tooltip("Fixed時の最終 Growth")]
	public float capFixed = 1.0f;

	// =========================
	// Dynamics
	// =========================
	[Header("Dynamics")]
	[Tooltip("到達順マップ（白=先に侵食）を使う")]
	public bool useGrowthMap = false;
	[Tooltip("（単一点相当のセンターUV）")]
	public Vector2 centerUV = new Vector2(0.5f, 0.5f);
	[Range(0f, 1f), Tooltip("開始半径 R0（放射相当）")]
	public float startRadius = 0.0f;
	[Range(0f, 2f), Tooltip("終了半径 R1（放射相当）")]
	public float endRadius = 1.2f;

	// =========================
	// Centers（多点）
	// =========================
	[Header("Centers")]
	[Tooltip("ONで多点から侵食（Voronoi風“和集合”）")]
	public bool useMultiCenters = false;
	[Range(1, 8), Tooltip("使用する起点数（手動値）")]
	public int centerCount = 2;

	[Tooltip("各起点へ半径 centersJitterRadius の位置ジッターを与える")]
	public bool varyCentersJitter = true;
	[Range(0f, 0.5f), Tooltip("ジッター半径（UV距離）")]
	public float centersJitterRadius = 0.06f;

	[Tooltip("起点『個数』をランダム化（Slider下限/上限を使用）")]
	public bool varyCenterCount = false;
	[Range(1, 8), Tooltip("ランダム化時の起点数の下限")]
	public int centerCountMin = 2;
	[Range(1, 8), Tooltip("ランダム化時の起点数の上限")]
	public int centerCountMax = 4;

	// （必要なら手動の基準座標も保持できるように—任意）
	[Tooltip("任意：手動の基準センター配列（長さ8推奨、未使用なら空でOK）")]
	public Vector2[] centers = new Vector2[0];

	// =========================
	// Centers > Motion
	// =========================
	[Header("Centers > Motion")]
	[Tooltip("各シードを小さく周回/揺らし、エッジに生命感を出す")]
	public bool animateCenters = false;
	[Range(0f, 0.2f), Tooltip("1シードの揺れ半径（UV）")]
	public float centersOrbitAmp = 0.02f;
	[Tooltip("周回の基準周波数(Hz)")]
	public float centersOrbitFreqBase = 0.6f;
	[Range(0f, 1f), Tooltip("周波数の個体差（0=同一, 1=±100%）")]
	public float centersOrbitFreqJitter = 0.30f;

	// =========================
	// Growth Oscillation
	// =========================
	[Header("Growth Oscillation")]
	[Tooltip("Growth を一定範囲で往復させる（停止中のみ/常時）")]
	public bool oscillateGrowth = false;
	[Tooltip("停止中のみ往復（ON）/ 常時（OFF）")]
	public bool oscillateOnlyWhenStopped = true;
	[Tooltip("往復波形：サイン or 三角波")]
	public OscWave oscillateWave = OscWave.Sine;
	[Range(0f, 1f), Tooltip("往復の下限")]
	public float oscillateMin = 0.2f;
	[Range(0f, 1f), Tooltip("往復の上限")]
	public float oscillateMax = 0.6f;
	[Range(0.01f, 2f), Tooltip("往復の周波数（Hz）")]
	public float oscillateFreq = 0.3f;

	// =========================
	// Randomization
	// =========================
	[Header("Randomization")]
	[Tooltip("有効化時に乱数バラつきを適用")]
	public bool randomizeOnEnable = true;

	[Tooltip("開始 Growth をランダム化")]
	public bool varyStartGrowth = true;
	[Range(0f, 1f), Tooltip("開始Growthの最小値")]
	public float startGrowthMin = 0.0f;
	[Range(0f, 1f), Tooltip("開始Growthの最大値")]
	public float startGrowthMax = 0.2f;

	[Tooltip("スタート遅延をランダム化")]
	public bool varyStartDelay = true;
	[Range(0f, 5f), Tooltip("開始遅延の最小秒数")]
	public float delayMin = 0.0f;
	[Range(0f, 5f), Tooltip("開始遅延の最大秒数")]
	public float delayMax = 0.5f;

	[Tooltip("再生時間倍率をランダム化")]
	public bool varyDuration = true;
	[Range(0.1f, 10f), Tooltip("再生時間倍率の最小値")]
	public float durationMulMin = 0.8f;
	[Range(0.1f, 10f), Tooltip("再生時間倍率の最大値")]
	public float durationMulMax = 1.2f;

	[Tooltip("ノイズ位相オフセットをランダム化")]
	public bool varyNoiseOffset = true;
	[Range(0f, 20f), Tooltip("ノイズ位相オフセット範囲（±この値）")]
	public float noiseOffsetRange = 10f;

	// =========================
	// Look (Apply Overrides)
	// =========================
	[Header("Look (Apply Overrides)")]
	[Tooltip("見た目オーバーライドを適用（OFF=ベースMaterial値を尊重）")]
	public bool applyLookOverrides = true;
	[Tooltip("仕上げブレンドモード")]
	public BlendMode blendMode = BlendMode.Multiply;
	[Range(0f, 1f), Tooltip("血色の混ぜ具合")]
	public float bloodMix = 0.5f;
	[Range(0f, 1f), Tooltip("内部の見かけ透過（塗り部）")]
	public float insideAlpha = 1.0f;
	[Tooltip("ベースのアルファを無視（常に1.0扱い）")]
	public bool ignoreBaseAlpha = false;
	[Range(0f, 0.2f), Tooltip("縁の幅")]
	public float edgeWidth = 0.05f;
	[Range(0f, 3f), Tooltip("縁の強さスケール")]
	public float rimBoost = 1.0f;
	[Tooltip("縁の色")]
	public Color rimColor = new Color(0.2f, 0.05f, 0.05f, 1f);
	[Tooltip("塗り（血/汚染）カラー")]
	public Color bloodColor = new Color(0.40f, 0.02f, 0.02f, 1f);
	[Tooltip("縁ノイズのスケール")]
	public float noiseScale = 3.0f;
	[Range(0f, 1f), Tooltip("縁ノイズの強さ")]
	public float noiseAmp = 0.2f;

	// =========================
	// Edge Animation
	// =========================
	[Header("Edge Animation")]
	[Tooltip("縁ノイズを流す速度（U,V / 秒）")]
	public Vector2 edgeNoiseScroll = Vector2.zero;
	[Range(0f, 0.2f), Tooltip("縁ノイズによるリムの揺らぎ量")]
	public float edgeFlowStrength = 0.00f;
	[Range(0f, 1f), Tooltip("リム幅のゆらぎ振幅（0..1）")]
	public float rimWobbleAmp = 0.00f;
	[Tooltip("リム幅のゆらぎ周波数（Hz）")]
	public float rimWobbleFreq = 2.0f;

	/// <summary>
	/// GrowthDriver へ一括コピー（不足分も含むフル適用）
	/// </summary>
	public void ApplyTo(GrowthDriver gd, bool includeSeedAndTiming = true, bool includeCentersAndMotion = true, bool includeLook = true)
	{
		if (!gd) return;

		if (includeSeedAndTiming)
		{
			gd.autoSeed = autoSeed;
			gd.manualSeed = manualSeed;
			gd.seedLabel = seedLabel;

			gd.duration = duration;
			gd.capMode = (GrowthDriver.CapMode)capMode;
			gd.capFixed = capFixed;

			gd.useGrowthMap = useGrowthMap;
			gd.centerUV = centerUV;
			gd.startRadius = startRadius;
			gd.endRadius = endRadius;

			// Randomization
			gd.randomizeOnEnable = randomizeOnEnable;
			gd.varyStartGrowth = varyStartGrowth;
			gd.startGrowthMin = startGrowthMin;
			gd.startGrowthMax = startGrowthMax;
			gd.varyStartDelay = varyStartDelay;
			gd.delayMin = delayMin;
			gd.delayMax = delayMax;
			gd.varyDuration = varyDuration;
			gd.durationMulMin = durationMulMin;
			gd.durationMulMax = durationMulMax;
			gd.varyNoiseOffset = varyNoiseOffset;
			gd.noiseOffsetRange = noiseOffsetRange;
		}

		if (includeCentersAndMotion)
		{
			gd.useMultiCenters = useMultiCenters;
			gd.centerCount = centerCount;
			gd.varyCentersJitter = varyCentersJitter;
			gd.centersJitterRadius = centersJitterRadius;
			gd.varyCenterCount = varyCenterCount;
			gd.centerCountMin = centerCountMin;
			gd.centerCountMax = centerCountMax;

			// 任意の基準センター座標があれば転記（長さ不足は無視）
			if (centers != null && centers.Length > 0)
			{
				// GrowthDriver 側が8固定配列を内部で維持している前提
				int copy = Mathf.Min(centers.Length, (gd.centers != null ? gd.centers.Length : 0));
				for (int i = 0; i < copy; i++) gd.centers[i] = centers[i];
			}

			// Motion / Oscillation
			gd.animateCenters = animateCenters;
			gd.centersOrbitAmp = centersOrbitAmp;
			gd.centersOrbitFreqBase = centersOrbitFreqBase;
			gd.centersOrbitFreqJitter = centersOrbitFreqJitter;

			gd.oscillateGrowth = oscillateGrowth;
			gd.oscillateOnlyWhenStopped = oscillateOnlyWhenStopped;
			gd.oscillateWave = (GrowthDriver.OscWave)oscillateWave;
			gd.oscillateMin = oscillateMin;
			gd.oscillateMax = oscillateMax;
			gd.oscillateFreq = oscillateFreq;
		}

		if (includeLook)
		{
			gd.applyLookOverrides = applyLookOverrides;
			gd.blendMode = (GrowthDriver.BlendMode)blendMode;
			gd.bloodMix = bloodMix;
			gd.insideAlpha = insideAlpha;
			gd.ignoreBaseAlpha = ignoreBaseAlpha;
			gd.edgeWidth = edgeWidth;
			gd.rimBoost = rimBoost;
			gd.rimColor = rimColor;
			gd.bloodColor = bloodColor;
			gd.noiseScale = noiseScale;
			gd.noiseAmp = noiseAmp;
			gd.edgeNoiseScroll = edgeNoiseScroll;
			gd.edgeFlowStrength = edgeFlowStrength;
			gd.rimWobbleAmp = rimWobbleAmp;
			gd.rimWobbleFreq = rimWobbleFreq;

			// 即時反映（あなたの GrowthDriver に用意済みのヘルパー）
			gd.ForceApplyFromManager();
		}
	}

	// --- Shader Property IDs（SH_GrowthSimple2D と一致） ---
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

	/// <summary>
	/// MPB へ見た目だけ適用（GrowthDriver なしでも反映可）
	/// </summary>
	public void ApplyLookToMPB(MaterialPropertyBlock mpb)
	{
		if (mpb == null) return;
		mpb.SetFloat(ID_BlendMode, (float)blendMode);
		mpb.SetFloat(ID_BloodMix, bloodMix);
		mpb.SetFloat(ID_InsideAlpha, insideAlpha);
		mpb.SetFloat(ID_IgnoreBase, ignoreBaseAlpha ? 1f : 0f);
		mpb.SetFloat(ID_EdgeWidth, edgeWidth);
		mpb.SetFloat(ID_RimBoost, rimBoost);
		mpb.SetColor(ID_RimColor, rimColor);
		mpb.SetColor(ID_BloodColor, bloodColor);
		mpb.SetFloat(ID_NoiseScale, noiseScale);
		mpb.SetFloat(ID_NoiseAmp, noiseAmp);
		mpb.SetVector(ID_EdgeNoiseScroll, new Vector4(edgeNoiseScroll.x, edgeNoiseScroll.y, 0f, 0f));
		mpb.SetFloat(ID_EdgeFlow, edgeFlowStrength);
		mpb.SetFloat(ID_RimWobbleAmp, rimWobbleAmp);
		mpb.SetFloat(ID_RimWobbleFreq, rimWobbleFreq);
	}
}
#if UNITY_EDITOR
public static class GrowthDriverProfileTools
{
    // 選択中の GrowthDriver から Profile を作る（見本：最低限）
    [UnityEditor.MenuItem("CreepyAura/Profile/Create From Selected GrowthDriver")]
    public static void CreateProfileFromSelectedGD()
    {
        var go = UnityEditor.Selection.activeGameObject;
        var gd = go ? go.GetComponent<GrowthDriver>() : null;
        if (!gd)
        {
            UnityEngine.Debug.LogWarning("選択中のオブジェクトに GrowthDriver がありません。");
            return;
        }

        var path = UnityEditor.EditorUtility.SaveFilePanelInProject(
            "Create GrowthDriverProfile",
            "NewGrowthDriverProfile",
            "asset",
            "保存先を選んでください");
        if (string.IsNullOrEmpty(path)) return;

        var prof = UnityEngine.ScriptableObject.CreateInstance<GrowthDriverProfile>();

        // --- 必要に応じて gd → prof へ値をコピー（例：見た目系のみ最低限） ---
        prof.blendMode       = (GrowthDriverProfile.BlendMode)gd.blendMode;
        prof.bloodMix        = gd.bloodMix;
        prof.insideAlpha     = gd.insideAlpha;
        prof.ignoreBaseAlpha = gd.ignoreBaseAlpha;
        prof.edgeWidth       = gd.edgeWidth;
        prof.rimBoost        = gd.rimBoost;
        prof.rimColor        = gd.rimColor;
        prof.bloodColor      = gd.bloodColor;
        prof.noiseScale      = gd.noiseScale;
        prof.noiseAmp        = gd.noiseAmp;
        prof.edgeNoiseScroll = gd.edgeNoiseScroll;
        prof.edgeFlowStrength= gd.edgeFlowStrength;
        prof.rimWobbleAmp    = gd.rimWobbleAmp;
        prof.rimWobbleFreq   = gd.rimWobbleFreq;

        UnityEditor.AssetDatabase.CreateAsset(prof, path);
        UnityEditor.EditorUtility.SetDirty(prof);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.Selection.activeObject = prof;

        UnityEngine.Debug.Log("GrowthDriver から GrowthDriverProfile を作成しました: " + path);
    }
}
#endif

