using UnityEngine;

/// <summary>
/// GodRays2Dの制御を行うコンポーネント
/// 自動セットアップとDustの形状同期に対応
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class GodRays2DController : MonoBehaviour
{
	// レンダラー関連

	[Header("レンダラー設定")]
	[Tooltip("Custom/SH_GodRays2Dを割り当てたマテリアルを指定します。")]
	public Material godRaysMaterial;

	// スプライト未設定時に使う共通スプライト
	// 256x256 RGBA32 Clamp Bilinear
	// 下端アルファ1 上端アルファ0の縦グラデーション
	private static Sprite s_defaultQuadSprite;

	// GodRay本体パラメータ

	[Header("色設定")]
	[ColorUsage(true, true)]
	public Color rayColor = new Color(1f, 0.95f, 0.8f, 1f);

	[Header("角度設定")]
	[Tooltip("右側の角度0〜180度この値を動かすと右側の角度が有効になります。")]
	[Range(0f, 180f)]
	public float anglePositive = 180f;

	[Tooltip("左側の角度0〜180度この値を動かすと左側の角度が有効になります。")]
	[Range(0f, 180f)]
	public float angleNegative = 0f;

	// どちら側の角度を採用するか判定
	[SerializeField, HideInInspector] private bool usePositiveSide = true;
	[SerializeField, HideInInspector] private float _lastAnglePositive = 180f;
	[SerializeField, HideInInspector] private float _lastAngleNegative = 0f;

	[Header("中心位置")]
	[Tooltip("レイの基準XスプライトUV0〜1窓の左右位置に合わせます。")]
	[Range(0f, 1f)]
	public float centerX = 0.5f;

	[Header("形状")]
	[Range(0f, 1f)] public float spread = 0.02f;
	[Range(-1f, 1f)] public float cutoff = -0.2f;
	[Range(0f, 1f)] public float falloff = 1.0f;
	[Range(0f, 1f)] public float edgeFade = 0.5f;

	[Range(0f, 0.2f)]
	[Tooltip("フォールオフ境界の揺らぎ量0で直線値を上げるほど境界が揺らぎます。")]
	public float falloffJitter = 0.1f;

	[Header("アニメーション")]
	[Range(0f, 10f)] public float speed = 3.0f;
	[Range(1f, 40f)] public float ray1Density = 20.0f;
	[Range(1f, 80f)] public float ray2Density = 60.0f;
	[Range(0f, 1f)] public float ray2Intensity = 0.8f;

	[Header("明るさ")]
	[Range(0f, 3f)] public float intensity = 2.0f;

	[Header("更新設定")]
	[Tooltip("再生中に毎フレームパラメータを反映するかどうか")]
	public bool updateEveryFrameInPlayMode = false;

	private SpriteRenderer spriteRenderer;
	private MaterialPropertyBlock mpb;
	private ParticleSystemRenderer dustRenderer;
	private MaterialPropertyBlock dustMpb;

	// GodRayシェーダ用プロパティID
	private static readonly int ColorId = Shader.PropertyToID("_Color");
	private static readonly int AngleId = Shader.PropertyToID("_Angle");
	private static readonly int CenterXId = Shader.PropertyToID("_CenterX");
	private static readonly int SpreadId = Shader.PropertyToID("_Spread");
	private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
	private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
	private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
	private static readonly int SpeedId = Shader.PropertyToID("_Speed");
	private static readonly int Ray1DensityId = Shader.PropertyToID("_Ray1Density");
	private static readonly int Ray2DensityId = Shader.PropertyToID("_Ray2Density");
	private static readonly int Ray2IntensityId = Shader.PropertyToID("_Ray2Intensity");
	private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
	private static readonly int FalloffJitterId = Shader.PropertyToID("_FalloffJitter");

	// Dustマスク用プロパティID
	private static readonly int MaskCenterWSId = Shader.PropertyToID("_MaskCenterWS");
	private static readonly int MaskRightWSId = Shader.PropertyToID("_MaskRightWS");
	private static readonly int MaskUpWSId = Shader.PropertyToID("_MaskUpWS");
	private static readonly int MaskHalfSizeId = Shader.PropertyToID("_MaskHalfSize");
	private static readonly int MaskFlipYId = Shader.PropertyToID("_MaskFlipY");

	// Dust用パラメータ

	[Header("Dust設定")]
	[Tooltip("GodRayに合わせてマスクされるDust用パーティクル")]
	public ParticleSystem dustParticles;

	[Tooltip("Custom/SH_GodRayDust2Dを割り当てたマテリアル")]
	public Material dustMaterial;


	/// <summary>
	/// インスペクタのReset時
	/// </summary>
	private void Reset()
	{
		spriteRenderer = GetComponent<SpriteRenderer>();
		EnsureRendererSetup();
	}

	/// <summary>
	/// 有効化時に初期化とパラメータ適用
	/// </summary>
	private void OnEnable()
	{
		Init();
		EnsureRendererSetup();

		_lastAnglePositive = anglePositive;
		_lastAngleNegative = angleNegative;
		usePositiveSide = true;

		ApplyToRenderer();
	}

	/// <summary>
	/// インスペクタ変更時
	/// </summary>
	private void OnValidate()
	{
		Init();
		EnsureRendererSetup();
		DetectLastChangedSide();
		ApplyToRenderer();
	}

	/// <summary>
	/// 再生中に毎フレーム更新が必要な場合
	/// </summary>
	private void Update()
	{
		if (!Application.isPlaying) return;
		if (!updateEveryFrameInPlayMode) return;

		ApplyToRenderer();
	}

	/// <summary>
	/// コンポーネントとMaterialPropertyBlockを確保
	/// </summary>
	private void Init()
	{
		if (spriteRenderer == null)
			spriteRenderer = GetComponent<SpriteRenderer>();
		if (spriteRenderer == null)
			spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

		if (mpb == null)
			mpb = new MaterialPropertyBlock();
	}

	/// <summary>
	/// SpriteRendererとスプライトとマテリアルを自動設定
	/// </summary>
	private void EnsureRendererSetup()
	{
		if (spriteRenderer == null)
		{
			spriteRenderer = GetComponent<SpriteRenderer>();
			if (spriteRenderer == null)
				spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
		}

		if (spriteRenderer.sprite == null)
		{
			spriteRenderer.sprite = GetOrCreateDefaultSprite();
		}

		if (godRaysMaterial != null && spriteRenderer.sharedMaterial != godRaysMaterial)
		{
			spriteRenderer.sharedMaterial = godRaysMaterial;
		}
	}

	/// <summary>
	/// デフォルトマスクスプライトを生成または取得
	/// </summary>
	private static Sprite GetOrCreateDefaultSprite()
	{
		if (s_defaultQuadSprite != null) return s_defaultQuadSprite;

		const int size = 256;

		var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
		{
			name = "GodRays2D_DefaultMask_Square",
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};

		var pixels = new Color32[size * size];

		for (int y = 0; y < size; y++)
		{
			float v = (float)y / (size - 1);
			float aFloat = 1f - v;
			aFloat = Mathf.SmoothStep(0f, 1f, aFloat);
			byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(aFloat * 255f), 0, 255);

			var col = new Color32(255, 255, 255, a);

			int rowIndex = y * size;
			for (int x = 0; x < size; x++)
			{
				pixels[rowIndex + x] = col;
			}
		}

		tex.SetPixels32(pixels);
		tex.Apply();

		var rect = new Rect(0, 0, size, size);
		var pivot = new Vector2(0.5f, 0.5f);

		s_defaultQuadSprite = Sprite.Create(tex, rect, pivot, size);
		s_defaultQuadSprite.name = "GodRays2D_DefaultMaskSprite_Square";

		return s_defaultQuadSprite;
	}

	// パラメータ更新

	/// <summary>
	/// どちらの角度スライダーが最後に変更されたかを判定
	/// </summary>
	private void DetectLastChangedSide()
	{
		const float eps = 0.0001f;

		bool changedPos = Mathf.Abs(anglePositive - _lastAnglePositive) > eps;
		bool changedNeg = Mathf.Abs(angleNegative - _lastAngleNegative) > eps;

		if (changedPos && !changedNeg)
		{
			usePositiveSide = true;
			_lastAnglePositive = anglePositive;
		}
		else if (!changedPos && changedNeg)
		{
			usePositiveSide = false;
			_lastAngleNegative = angleNegative;
		}
		else if (changedPos && changedNeg)
		{
			float deltaPos = Mathf.Abs(anglePositive - _lastAnglePositive);
			float deltaNeg = Mathf.Abs(angleNegative - _lastAngleNegative);

			usePositiveSide = (deltaPos >= deltaNeg);
			_lastAnglePositive = anglePositive;
			_lastAngleNegative = angleNegative;
		}
	}

	/// <summary>
	/// GodRayとDustにパラメータを反映
	/// </summary>
	private void ApplyToRenderer()
	{
		if (spriteRenderer == null || mpb == null) return;

		spriteRenderer.GetPropertyBlock(mpb);

		float sideDeg = usePositiveSide ? anglePositive : -angleNegative;
		float angleRad = sideDeg * Mathf.Deg2Rad;

		mpb.SetColor(ColorId, rayColor);
		mpb.SetFloat(AngleId, angleRad);
		mpb.SetFloat(CenterXId, centerX);

		mpb.SetFloat(SpreadId, spread);
		mpb.SetFloat(CutoffId, cutoff);
		mpb.SetFloat(FalloffId, falloff);
		mpb.SetFloat(EdgeFadeId, edgeFade);
		mpb.SetFloat(FalloffJitterId, falloffJitter);

		mpb.SetFloat(SpeedId, speed);
		mpb.SetFloat(Ray1DensityId, ray1Density);
		mpb.SetFloat(Ray2DensityId, ray2Density);
		mpb.SetFloat(Ray2IntensityId, ray2Intensity);

		mpb.SetFloat(IntensityId, intensity);

		spriteRenderer.SetPropertyBlock(mpb);

		ApplyParticleMaskParams();
	}

	/// <summary>
	/// Dust用マテリアルにマスク情報とGodRay形状を同期
	/// </summary>
	private void ApplyParticleMaskParams()
	{
		if (dustParticles == null || dustMaterial == null) return;
		if (spriteRenderer == null || spriteRenderer.sprite == null) return;

		// Renderer と MPB を確保
		if (dustRenderer == null)
		{
			dustRenderer = dustParticles.GetComponent<ParticleSystemRenderer>();
			if (dustRenderer == null) return;
			if (dustRenderer.sharedMaterial != dustMaterial) dustRenderer.sharedMaterial = dustMaterial;
		}

		if (dustMpb == null) dustMpb = new MaterialPropertyBlock();

		var bounds = spriteRenderer.bounds;
		Vector3 center = bounds.center;
		Vector3 size = bounds.size;

		Vector2 halfSize = new Vector2(size.x * 0.5f, size.y * 0.5f);

		Vector3 right = transform.right;
		Vector3 up = transform.up;

		Vector3 rightVec = right * halfSize.x;
		Vector3 upVec = up * halfSize.y;

		dustRenderer.GetPropertyBlock(dustMpb);

		dustMpb.SetVector(MaskCenterWSId, new Vector4(center.x, center.y, center.z, 1f));
		dustMpb.SetVector(MaskRightWSId, new Vector4(rightVec.x, rightVec.y, rightVec.z, 0f));
		dustMpb.SetVector(MaskUpWSId, new Vector4(upVec.x, upVec.y, upVec.z, 0f));
		dustMpb.SetVector(MaskHalfSizeId, new Vector4(halfSize.x, halfSize.y, 0f, 0f));

		float maskFlipY = (spriteRenderer != null && spriteRenderer.flipY) ? 1f : 0f;
		dustMaterial.SetFloat(MaskFlipYId, maskFlipY);

		float sideDeg = usePositiveSide ? anglePositive : -angleNegative;
		float angleRad = sideDeg * Mathf.Deg2Rad;

		dustMpb.SetFloat("_Angle", angleRad);
		dustMpb.SetFloat("_CenterX", centerX);
		dustMpb.SetFloat("_Spread", spread);
		dustMpb.SetFloat("_Cutoff", cutoff);
		dustMpb.SetFloat("_FalloffJitter", falloffJitter);

		dustMpb.SetFloat("_Speed", speed);
		dustMpb.SetFloat("_Ray1Density", ray1Density);
		dustMpb.SetFloat("_Ray2Density", ray2Density);
		dustMpb.SetFloat("_Ray2Intensity", ray2Intensity);
	}
}
