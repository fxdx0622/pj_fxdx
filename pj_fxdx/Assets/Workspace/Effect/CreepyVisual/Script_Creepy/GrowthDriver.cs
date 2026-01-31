using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Growth エフェクトのドライバ（最適化＋Human-friendly Seed版）
/// ・MPB一本化（軽量）
/// ・Seedは auto/manual＋文字列ラベルで決定論を担保（FNV-1a）
/// ・マルチシード＝「手動配置＋内部ジッター」方式（Inspectorは壊さない）
/// ・Capは None / Fixed のみ
/// ・プレビューON＋非再生は毎回リロール（見た目確認しやすい）
/// ・単一点運用もマルチ配列に統一（useMultiCenters=OFF でも CenterCount=1 を送る）
/// </summary>
[RequireComponent(typeof(Renderer))]
public class GrowthDriver : MonoBehaviour
{
	// ====== Editor ======
#if UNITY_EDITOR
    [Header("Editor")]
    [Tooltip("インスペクタ変更を即反映（再生中も）")]
    public bool liveApplyInPlayMode = true;

    [Tooltip("プレビュー用に Growth を一時上書き（OFFで解除）")]
    public bool previewMode = false;

    [Range(0f, 1f), Tooltip("プレビュー時の Growth 値（0..1）")]
    public float previewGrowth = 1f;

    [ContextMenu("Random/Rebuild (Editor)")]
    void EditorRebuild()
    {
        ApplyRandomization();                 // 開始/遅延/時間/位相
        RebuildCentersJitterWithSeed(BaseSeed());
        Apply();
        EditorUtility.SetDirty(this);
    }
#endif

	// ====== Const ======
	const int C_MAX_SEEDS = 8;

	// ====== Enums ======
	public enum BlendMode { Normal = 0, Multiply = 1, Overlay = 2, Add = 3 }
	public enum CapMode { None, Fixed }

	// ====== Human-friendly Salts ======
	// FNV-1a 32bit で「名前→int」へ（同一文字列は同一値）
	static int Salt(string key)
	{
		unchecked
		{
			uint h = 2166136261u;     // FNV offset basis
			for (int i = 0; i < key.Length; i++)
			{
				h ^= key[i];
				h *= 16777619u;       // FNV prime
			}
			return (int)h;
		}
	}
	static readonly int SALT_BASE = Salt("seed:base");
	static readonly int SALT_RAND = Salt("seed:randomization");
	static readonly int SALT_CENTER = Salt("seed:centers");

	// ====== Seed ======
	[Header("Seed")]
	[Tooltip("ON=自動Seed(InstanceID由来)、OFF=manualSeed固定")]
	public bool autoSeed = true;

	[Tooltip("手動Seed。値が同じなら同じ見た目")]
	public int manualSeed = 12345;

	[Tooltip("任意ラベルで見た目固定（空なら無効）")]
	public string seedLabel = "";

	// ====== Centers（多点起点・手動ベース） ======
	[Header("Centers")]
	[Tooltip("ONで多点から侵食（Voronoi風“和集合”）")]
	public bool useMultiCenters = false;

	[Range(1, 8), Tooltip("使用する起点数（手動値）")]
	public int centerCount = 2;

	[Tooltip("各起点のUV（0..1）※手動ベース、内部ジッターは別管理")]
	public Vector2[] centers = new Vector2[8]
	{
		new Vector2(0.35f, 0.6f),
		new Vector2(0.65f, 0.4f),
		new Vector2(0.5f,  0.8f),
		new Vector2(0.2f,  0.2f),
		new Vector2(0.8f,  0.2f),
		new Vector2(0.2f,  0.8f),
		new Vector2(0.8f,  0.8f),
		new Vector2(0.5f,  0.5f),
	};

	// ====== Centers > Random ======
	[Header("Centers > Random")]
	[Tooltip("各起点へ半径 centersJitterRadius の位置ジッター（Inspectorの centers[] は保持）")]
	public bool varyCentersJitter = true;

	[Range(0f, 0.5f), Tooltip("ジッター半径（UV距離）")]
	public float centersJitterRadius = 0.06f;

	[Tooltip("起点『個数』をランダム化（Inspectorの centerCount は変更しない）")]
	public bool varyCenterCount = false;

	[Range(1, 8), Tooltip("ランダム化時の起点数の下限")]
	public int centerCountMin = 2;

	[Range(1, 8), Tooltip("ランダム化時の起点数の上限")]
	public int centerCountMax = 4;

	// ====== Playback ======
	[Header("Playback")]
	[Tooltip("0→最終到達値に達する秒数（基準）")]
	public float duration = 1.5f;

	[Tooltip("有効化時に自動再生")]
	public bool playOnEnable = true;

	// ====== Stop/Cap ======
	[Header("Stop/Cap")]
	[Tooltip("成長の最終到達モード（None/Fixed）")]
	public CapMode capMode = CapMode.None;

	[Range(0f, 1f), Tooltip("Fixed時の最終 Growth")]
	public float capFixed = 1.0f;

	float _capEff = 1f; // 実効Cap

	// ====== Dynamics ======
	[Header("Dynamics")]
	[Tooltip("到達順マップ（白=先に侵食）を使う")]
	public bool useGrowthMap = false;

	[Tooltip("（単一点相当を使う場合の便宜。centers[0] へ転記して使います）")]
	public Vector2 centerUV = new Vector2(0.5f, 0.5f);

	[Range(0f, 1f), Tooltip("開始半径 R0（放射相当）")]
	public float startRadius = 0.0f;

	[Range(0f, 2f), Tooltip("終了半径 R1（放射相当）")]
	public float endRadius = 1.2f;

	// ====== Look ======
	[Header("Look")]
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

	[Header("Color & Noise")]
	[Tooltip("塗り（血/汚染）カラー")]
	public Color bloodColor = new Color(0.40f, 0.02f, 0.02f, 1f);

	[Tooltip("縁ノイズのスケール")]
	public float noiseScale = 3.0f;

	[Range(0f, 1f), Tooltip("縁ノイズの強さ")]
	public float noiseAmp = 0.2f;

	// ====== Edge Animation (Rim) ======
	[Header("Edge Animation (Rim)")]
	[Tooltip("縁ノイズを流す速度（U,V / 秒）")]
	public Vector2 edgeNoiseScroll = Vector2.zero; // (U,V)/sec

	[Range(0f, 0.2f), Tooltip("縁ノイズによるリムの揺らぎ量")]
	public float edgeFlowStrength = 0.00f;

	[Range(0f, 1f), Tooltip("リム幅のゆらぎ振幅（0..1）")]
	public float rimWobbleAmp = 0.00f;

	[Tooltip("リム幅のゆらぎ周波数（Hz）")]
	public float rimWobbleFreq = 2.0f;

	[Header("Centers > Motion")]
	[Tooltip("各シードを小さく周回/揺らし、エッジに生命感を出す")]
	public bool animateCenters = false;

	[Range(0f, 0.2f), Tooltip("1シードの揺れ半径（UV）")]
	public float centersOrbitAmp = 0.02f;

	[Tooltip("周回の基準周波数(Hz)")]
	public float centersOrbitFreqBase = 0.6f;

	[Range(0f, 1f), Tooltip("周波数の個体差（0=同一, 1=±100%）")]
	public float centersOrbitFreqJitter = 0.30f;

	// Growth > Oscillation
	[Header("Growth > Oscillation")]
	[Tooltip("Growth を一定範囲で往復させる（停止中のみ/常時）")]
	public bool oscillateGrowth = false;

	[Tooltip("停止中のみ往復（ON）/ 常時（OFF）")]
	public bool oscillateOnlyWhenStopped = true;

	public enum OscWave { Sine, Triangle }
	[Tooltip("往復波形：サイン or 三角波")]
	public OscWave oscillateWave = OscWave.Sine;

	[Range(0f, 1f), Tooltip("往復の下限")]
	public float oscillateMin = 0.2f;

	[Range(0f, 1f), Tooltip("往復の上限")]
	public float oscillateMax = 0.6f;

	[Range(0.01f, 2f), Tooltip("往復の周波数（Hz）")]
	public float oscillateFreq = 0.3f;

	// ====== Random（開始/遅延/時間/ノイズ） ======
	[Header("Random")]
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


	// ====== 非シリアライズ内部 ======
	[System.NonSerialized] Vector2[] _centersJitter = new Vector2[C_MAX_SEEDS];
	[System.NonSerialized] int _appliedSeed = int.MinValue;
	[System.NonSerialized] int _currentCenterCount = 0;
	[System.NonSerialized] float[] _seedPhaseX = new float[C_MAX_SEEDS];
	[System.NonSerialized] float[] _seedPhaseY = new float[C_MAX_SEEDS];
	[System.NonSerialized] float[] _seedFreq = new float[C_MAX_SEEDS];
	[System.NonSerialized] float _oscPhase0 = 0f;
	Renderer _r;
	MaterialPropertyBlock _mpb;
	float _t;
	bool _playing;

	float _initGrowth = 0f;
	float _actualDuration = 1.5f;
	float _delayLeft = 0f;
	Vector2 _noiseOffset = Vector2.zero;
	Vector4[] _centersBuf; // 再利用バッファ

	// Shader property IDs
	static readonly int ID_Growth = Shader.PropertyToID("_Growth");
	static readonly int ID_UseMap = Shader.PropertyToID("_UseMap");
	static readonly int ID_R0 = Shader.PropertyToID("_R0");
	static readonly int ID_R1 = Shader.PropertyToID("_R1");
	static readonly int ID_BlendMode = Shader.PropertyToID("_BlendMode");
	static readonly int ID_BloodMix = Shader.PropertyToID("_BloodMix");
	static readonly int ID_InsideAlpha = Shader.PropertyToID("_InsideAlpha");
	static readonly int ID_Ignore = Shader.PropertyToID("_IgnoreBaseAlpha");
	static readonly int ID_EdgeWidth = Shader.PropertyToID("_EdgeWidth");
	static readonly int ID_RimBoost = Shader.PropertyToID("_RimBoost");
	static readonly int ID_RimColor = Shader.PropertyToID("_RimColor");
	static readonly int ID_BloodColor = Shader.PropertyToID("_BloodColor");
	static readonly int ID_NoiseScale = Shader.PropertyToID("_NoiseScale");
	static readonly int ID_NoiseAmp = Shader.PropertyToID("_NoiseAmp");
	static readonly int ID_NoiseOffset = Shader.PropertyToID("_NoiseOffset");
	static readonly int ID_UseMultiCenter = Shader.PropertyToID("_UseMultiCenter");
	static readonly int ID_CenterCount = Shader.PropertyToID("_CenterCount");
	static readonly int ID_Centers = Shader.PropertyToID("_Centers");
	static readonly int ID_EdgeNoiseScroll = Shader.PropertyToID("_EdgeNoiseScroll");
	static readonly int ID_EdgeFlowStrength = Shader.PropertyToID("_EdgeFlowStrength");
	static readonly int ID_RimWobbleAmp = Shader.PropertyToID("_RimWobbleAmp");
	static readonly int ID_RimWobbleFreq = Shader.PropertyToID("_RimWobbleFreq");
	static readonly int SALT_OSC = Salt("seed:osc");

	void Reset()
	{
		_r = GetComponent<Renderer>();
	}

	void Awake()
	{
		_r = GetComponent<Renderer>();
		_mpb = new MaterialPropertyBlock();
	}

	void OnEnable()
	{
		if (randomizeOnEnable) ApplyRandomization();
		RebuildCentersJitterWithSeed(BaseSeed());

		if (playOnEnable) Play(_initGrowth);
		else { _t = _initGrowth; Apply(); }
	}

	// ====== Seed helpers ======
	static int Combine(int a, int b)
	{
		unchecked
		{
			uint x = (uint)a;
			uint y = (uint)b;
			uint h = x + 0x9E3779B9u + (y << 6) + (y >> 2);
			h ^= (h << 13);
			h ^= (h >> 17);
			h ^= (h << 5);
			return (int)h;
		}
	}

	int BaseSeed()
	{
		int s = autoSeed ? Combine(gameObject.GetInstanceID(), SALT_BASE) : manualSeed;
		if (!string.IsNullOrEmpty(seedLabel))
			s = Combine(s, Salt("label:" + seedLabel)); // ラベルで安定分岐
		return s;
	}

	// ====== Randomize (start/delay/duration/noise) ======
	void ApplyRandomization()
	{
		var rng = new System.Random(Combine(BaseSeed(), SALT_RAND));

		float Next01() => (float)rng.NextDouble();
		float Lerp01(float a, float b) => Mathf.Lerp(a, b, Next01());

		_initGrowth = varyStartGrowth ? Mathf.Clamp01(Lerp01(startGrowthMin, startGrowthMax)) : 0f;
		_delayLeft = varyStartDelay ? Mathf.Max(0f, Lerp01(delayMin, delayMax)) : 0f;

		float mul = varyDuration ? Mathf.Max(0.0001f, Lerp01(durationMulMin, durationMulMax)) : 1f;
		_actualDuration = Mathf.Max(0.0001f, duration * mul);

		_noiseOffset = varyNoiseOffset
			? new Vector2(Lerp01(-noiseOffsetRange, noiseOffsetRange),
						  Lerp01(-noiseOffsetRange, noiseOffsetRange))
			: Vector2.zero;

		_capEff = (capMode == CapMode.Fixed) ? Mathf.Clamp01(capFixed) : 1f;
		if (_initGrowth > _capEff) _initGrowth = _capEff;
	}

	// ====== Centers jitter & count ======
	void RebuildCentersJitterWithSeed(int seed)
	{
		var rng = new System.Random(Combine(seed, SALT_CENTER));

		// centers の実長も上限に含める
		int centersLen = (centers != null) ? centers.Length : 0;
		int hardMax = Mathf.Min(C_MAX_SEEDS, Mathf.Max(centersLen, 1)); // 少なくとも1

		// 実効カウント
		if (useMultiCenters)
		{
			if (varyCenterCount)
			{
				int minC = Mathf.Clamp(centerCountMin, 1, hardMax);
				int maxC = Mathf.Clamp(centerCountMax, 1, hardMax);
				if (maxC < minC) maxC = minC;
				_currentCenterCount = rng.Next(minC, maxC + 1);
			}
			else
			{
				_currentCenterCount = Mathf.Clamp(centerCount, 1, hardMax);
			}
		}
		else
		{
			_currentCenterCount = 1; // 単一点
		}

		// 事前計算（可読性＆微最適化）
		const float TAU = 6.28318530718f; // 2π
		bool doJitter = varyCentersJitter && (_currentCenterCount > 0);
		float jitterRadius = centersJitterRadius;
		float freqBase = centersOrbitFreqBase;
		float freqJitterRange = Mathf.Clamp01(centersOrbitFreqJitter);

		// ループを1本化：ジッターと(位相/周波数)を同時に設定
		for (int i = 0; i < C_MAX_SEEDS; i++)
		{
			// --- 位置ジッター（使うシードだけ） ---
			if (doJitter && i < _currentCenterCount)
			{
				// 面積一様サンプリング（半径 = sqrt(u)）
				float ang = (float)rng.NextDouble() * TAU;
				float rad = Mathf.Sqrt((float)rng.NextDouble()) * jitterRadius;
				_centersJitter[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
			}
			else
			{
				_centersJitter[i] = Vector2.zero;
			}

			// --- 初期位相（X/Y独立） ---
			_seedPhaseX[i] = (float)rng.NextDouble() * TAU;
			_seedPhaseY[i] = (float)rng.NextDouble() * TAU;

			// --- 周波数（個体差 ±range）---
			float r = (float)rng.NextDouble();                // 0..1
			float jitter = 1f + (2f * r - 1f) * freqJitterRange; // 1±range
			_seedFreq[i] = Mathf.Max(0.01f, freqBase * jitter);  // 元コード同等：乗算後に下限を保証
		}

		_appliedSeed = seed;
		_oscPhase0 = ((Combine(seed, SALT_OSC) & 1023) * (6.28318530718f / 1024f));
	}


	void EnsureSeedApplied()
	{
		int s = BaseSeed();

		int centersLen = (centers != null) ? centers.Length : 0;
		int hardMax = Mathf.Min(C_MAX_SEEDS, Mathf.Max(centersLen, 1));
		int manualCnt = Mathf.Clamp(centerCount, 1, hardMax);

		bool needCountUpdate =
			(!useMultiCenters && _currentCenterCount != 1) ||
			(useMultiCenters && !varyCenterCount && _currentCenterCount != manualCnt);

		if (s != _appliedSeed || needCountUpdate)
			RebuildCentersJitterWithSeed(s);
	}


	Vector4[] BuildCentersBuffer()
	{
		// 常に再利用バッファを確保
		if (_centersBuf == null || _centersBuf.Length != C_MAX_SEEDS)
			_centersBuf = new Vector4[C_MAX_SEEDS];

		// 単一点運用時は centerUV → centers[0] を毎回同期
		if (!useMultiCenters)
		{
			if (centers == null || centers.Length < 1)
				centers = new Vector2[C_MAX_SEEDS];

			centers[0] = new Vector2(
				Mathf.Clamp01(centerUV.x),
				Mathf.Clamp01(centerUV.y)
			);
			_currentCenterCount = 1; // 念押し
		}

		// 時間（停止中でも動かしたいので realtimeSinceStartup を使用）
		float timeNow = Time.realtimeSinceStartup;

		for (int i = 0; i < C_MAX_SEEDS; i++)
		{
			Vector2 baseUV = (i < (centers?.Length ?? 0)) ? centers[i] : new Vector2(0.5f, 0.5f);

			// ジッター（静的）＋ モーション（動的）
			Vector2 uv = baseUV + _centersJitter[i];

			if (animateCenters && i < _currentCenterCount && centersOrbitAmp > 0f)
			{
				// 独立した周回（位相X/Yを別にして軽い“ゆら周回”）
				float ox = Mathf.Sin(_seedPhaseX[i] + _seedFreq[i] * timeNow);
				float oy = Mathf.Cos(_seedPhaseY[i] + _seedFreq[i] * timeNow);
				uv += new Vector2(ox, oy) * centersOrbitAmp;
			}

			_centersBuf[i] = new Vector4(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y), 0f, 0f);
		}
		return _centersBuf;
	}

	// --- Editorプレビューが有効か？（非再生 または liveApplyInPlayMode中）---
	public bool IsEditorPreviewActive
	{
#if UNITY_EDITOR
    get { return previewMode && (!Application.isPlaying || liveApplyInPlayMode); }
#else
		get { return false; }
#endif
	}

	// プレビュー中は外部からの操作を無視
	bool EditorPreviewLock()
	{
#if UNITY_EDITOR
    return IsEditorPreviewActive;
#else
		return false;
#endif
	}

	// ====== Control ======
	public void Play(float from = 0f)
	{
		if (EditorPreviewLock()) { Apply(); return; }
		_t = Mathf.Clamp01(from);
		_playing = true;
		Apply();
	}

	public void ResetGrowth(float value = 0f)
	{
		if (EditorPreviewLock()) { Apply(); return; }
		_t = Mathf.Clamp01(value);
		_playing = false;
		_delayLeft = 0f;
		Apply();
	}

	void Update()
	{
		// 停止中でも “見た目が動く” 系は毎フレ適用
		if (!_playing)
		{
			if (animateCenters || edgeFlowStrength > 0f || rimWobbleAmp > 0f || oscillateGrowth)
				Apply();
			return;
		}

		// スタート遅延
		if (_delayLeft > 0f)
		{
			_delayLeft -= Time.deltaTime;
			Apply();
			return;
		}

		if (_actualDuration <= 0f) _actualDuration = Mathf.Max(0.0001f, duration);

		float target = (capMode == CapMode.Fixed) ? Mathf.Clamp01(capFixed) : 1f;
		_t = Mathf.MoveTowards(_t, target, Time.deltaTime / _actualDuration);
		Apply();

		if (Mathf.Approximately(_t, target)) _playing = false;
	}

	// ====== Apply (MPB) ======
	void Apply()
	{
		if (_r == null) return;

		EnsureSeedApplied();

		float tForDraw = _t;
#if UNITY_EDITOR
        if (previewMode)
        {
            if (!Application.isPlaying || liveApplyInPlayMode)
                tForDraw = Mathf.Clamp01(previewGrowth);
        }
#endif
		// ここから：Growth 往復オプション
		if (oscillateGrowth && (!oscillateOnlyWhenStopped || !_playing))
		{
			// 基本時間は realtime（停止中でも動かす）
			float t = Time.realtimeSinceStartup * oscillateFreq;

			// 0..1 の往復波
			float w01;
			if (oscillateWave == OscWave.Sine)
			{
				w01 = 0.5f * (1f + Mathf.Sin(_oscPhase0 + t * 6.28318530718f));
			}
			else // Triangle
			{
				w01 = Mathf.PingPong(t * 2f, 1f);
			}

			float lo = Mathf.Min(oscillateMin, oscillateMax);
			float hi = Mathf.Max(oscillateMin, oscillateMax);

			float cap = (capMode == CapMode.Fixed) ? Mathf.Clamp01(capFixed) : 1f;
			// 両端ともcap内に丸めた上で順序を保証
			lo = Mathf.Min(lo, cap);
			hi = Mathf.Min(hi, cap);
			if (lo > hi) { var tmp = lo; lo = hi; hi = tmp; }

			tForDraw = Mathf.Lerp(lo, hi, w01);
		}

		if (_mpb == null) _mpb = new MaterialPropertyBlock();
		_mpb.Clear();

		// 動的
		_mpb.SetFloat(ID_Growth, tForDraw);
		_mpb.SetFloat(ID_UseMap, useGrowthMap ? 1f : 0f);
		_mpb.SetFloat(ID_R0, startRadius);
		_mpb.SetFloat(ID_R1, endRadius);
		_mpb.SetVector(ID_NoiseOffset, new Vector4(_noiseOffset.x, _noiseOffset.y, 0f, 0f));

		// マルチシード（実効:1）
		var arr = BuildCentersBuffer();
		_mpb.SetFloat(ID_UseMultiCenter, useMultiCenters ? 1f : 0f);
		_mpb.SetFloat(ID_CenterCount, _currentCenterCount);
		_mpb.SetVectorArray(ID_Centers, arr);

		// 見た目
		if (applyLookOverrides)
		{
			_mpb.SetFloat(ID_BlendMode, (float)blendMode);
			_mpb.SetFloat(ID_BloodMix, bloodMix);
			_mpb.SetFloat(ID_InsideAlpha, insideAlpha);
			_mpb.SetFloat(ID_Ignore, ignoreBaseAlpha ? 1f : 0f);
			_mpb.SetFloat(ID_EdgeWidth, edgeWidth);
			_mpb.SetFloat(ID_RimBoost, rimBoost);
			_mpb.SetColor(ID_RimColor, rimColor);
			_mpb.SetColor(ID_BloodColor, bloodColor);
			_mpb.SetFloat(ID_NoiseScale, noiseScale);
			_mpb.SetFloat(ID_NoiseAmp, noiseAmp);
			_mpb.SetVector(ID_EdgeNoiseScroll, new Vector4(edgeNoiseScroll.x, edgeNoiseScroll.y, 0f, 0f));
			_mpb.SetFloat(ID_EdgeFlowStrength, edgeFlowStrength);
			_mpb.SetFloat(ID_RimWobbleAmp, rimWobbleAmp);
			_mpb.SetFloat(ID_RimWobbleFreq, rimWobbleFreq);
		}

		_r.SetPropertyBlock(_mpb);
	}

	void OnValidate()
	{
		if (_r == null) _r = GetComponent<Renderer>();
		if (_r == null) return;
		if (_mpb == null) _mpb = new MaterialPropertyBlock();

#if UNITY_EDITOR
    // --- Editor only ---
	if (Application.isPlaying && liveApplyInPlayMode)
{
    RebuildCentersJitterWithSeed(BaseSeed());
    Apply();
}
    // 再生中は配列矯正を行わない（Editor再生時の想定外変更を防ぐ）
    if (!Application.isPlaying)
    {
        // 配列サイズを常に8へ矯正（不足は(0.5,0.5)で埋め）
        if (centers == null || centers.Length != C_MAX_SEEDS)
        {
            // Undo対応（取り消し可能に）
            Undo.RecordObject(this, "Resize Centers to 8");

            var newArr = new Vector2[C_MAX_SEEDS];
            int copy = (centers != null) ? Mathf.Min(centers.Length, C_MAX_SEEDS) : 0;
            for (int i = 0; i < copy; i++) newArr[i] = centers[i];
            for (int i = copy; i < C_MAX_SEEDS; i++) newArr[i] = new Vector2(0.5f, 0.5f);
            centers = newArr;

            EditorUtility.SetDirty(this);
            // Prefab化している場合の変更記録（任意）
            // PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }

        // カウント系の即時クランプ（配列長と上限を常に一致）
        int hardMax = Mathf.Min(C_MAX_SEEDS, Mathf.Max(centers?.Length ?? 0, 1));
        centerCount     = Mathf.Clamp(centerCount,     1, hardMax);
        centerCountMin  = Mathf.Clamp(centerCountMin,  1, hardMax);
        centerCountMax  = Mathf.Clamp(centerCountMax,  1, hardMax);
        if (centerCountMax < centerCountMin) centerCountMax = centerCountMin;
    }

    // プレビューONかつ非再生時のみ、見た目確認のためにリロール
    if (!Application.isPlaying && previewMode)
    {
        ApplyRandomization();
        RebuildCentersJitterWithSeed(BaseSeed());
    }
#endif

		Apply();
	}

	public float EffectiveGrowth
	{
		get
		{
			float t = _t;
#if UNITY_EDITOR
            if (previewMode && (!Application.isPlaying || liveApplyInPlayMode))
                t = Mathf.Clamp01(previewGrowth);
#endif
			return t;
		}
	}

	public Vector2 CurrentNoiseOffset => _noiseOffset;

	public void ApplyExternalDynamics(float growth, Vector2 center, float r0, float r1, Vector2 noiseOffset)
	{
		if (EditorPreviewLock()) { Apply(); return; }
		_t = Mathf.Clamp01(growth);

		if (centers == null || centers.Length < 1)
			centers = new Vector2[8];
		centers[0] = new Vector2(Mathf.Clamp01(center.x), Mathf.Clamp01(center.y));

		startRadius = r0;
		endRadius = r1;
		_noiseOffset = noiseOffset;

		_delayLeft = 0f;
		_playing = false;
		Apply();
	}

	public void ForceApplyFromManager()
	{
		// 即時MPB反映が必要な時に呼ぶ
		var mi = typeof(GrowthDriver).GetMethod("Apply", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		mi?.Invoke(this, null);
	}
	[ContextMenu("Random/Rebuild (Manager)")]
	public void ReRandomizeAndRebuild()
	{
		// ランダム初期化→センター再構築→適用
		var miRand = typeof(GrowthDriver).GetMethod("ApplyRandomization", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		var miRebd = typeof(GrowthDriver).GetMethod("RebuildCentersJitterWithSeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		var miApply = typeof(GrowthDriver).GetMethod("Apply", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		miRand?.Invoke(this, null);
		miRebd?.Invoke(this, new object[] { BaseSeed() });
		miApply?.Invoke(this, null);
	}

}
