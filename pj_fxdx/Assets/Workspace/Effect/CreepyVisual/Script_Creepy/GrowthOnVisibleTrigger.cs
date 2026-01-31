using UnityEngine;

/// <summary>
/// 指定カメラ（既定: MainCamera）の視錐台内に入ったら Growth を発火。
/// ・Sceneビュー影響なし
/// ・screenEdgePaddingで“画面枠”を±拡縮（+で広め、-で狭め）
/// </summary>
[RequireComponent(typeof(Renderer))]
public class GrowthOnVisibleTrigger : MonoBehaviour
{
	[Tooltip("未指定なら同じGameObjectから GrowthDriver を取得")]
	public GrowthDriver target;

	[Header("Camera")]
	[Tooltip("明示指定があれば優先。空なら毎回 Camera.main を参照")]
	public Camera targetCamera;

	[Header("Trigger")]
	[Tooltip("一度だけ発火（ON）／視界に入るたびに発火（OFF）")]
	public bool once = true;
	[Tooltip("Awake時に Growth を 0 に初期化（事前のにじみ防止）")]
	public bool forceZeroAtAwake = true;
	[Tooltip("開始値（0..1）。負値なら 0 から")]
	[Range(-1f, 1f)] public float startFrom = -1f;
	[Tooltip("ONなら即座に最終状態まで進める（アニメをスキップ）")]
	public bool instantComplete = false;

	[Header("Performance & Padding")]
	[Tooltip("判定間隔（秒）。小さいほど応答は速いが負荷増")]
	[Range(0.01f, 0.5f)] public float checkInterval = 0.05f;
	[Tooltip("オブジェクトのバウンズを相対で膨らませる（チラつき防止）")]
	[Range(0f, 1f)] public float boundsPadding = 0.05f;
	[Tooltip("画面枠のパディング（ビューポート）。+で広め（画面外でも発火）、-で狭め")]
	[Range(-0.5f, 0.5f)] public float screenEdgePadding = 0.0f;

	Renderer _renderer;
	float _timer;
	bool _fired;

	void Awake()
	{
		_renderer = GetComponent<Renderer>();
		if (!target) target = GetComponent<GrowthDriver>();
		if (target)
		{
			target.playOnEnable = false;
			if (forceZeroAtAwake) target.ResetGrowth(0f);
		}
	}

	void OnEnable()
	{
		_timer = 0f;
		_fired = false;
	}

	void Update()
	{
		if (once && _fired) return;

		_timer -= Time.deltaTime;
		if (_timer > 0f) return;
		_timer = checkInterval;

		var cam = targetCamera ? targetCamera : Camera.main;
		if (!cam || !_renderer) return;

		if (IsInViewWithPadding(cam, _renderer, boundsPadding, screenEdgePadding))
		{
			Fire();
		}
	}

	static bool IsInViewWithPadding(Camera cam, Renderer rend, float boundsPad, float screenPad)
	{
		// バウンズを少し膨らませる
		var b = rend.bounds;
		var e = b.extents;
		b.Expand(new Vector3(e.x * boundsPad, e.y * boundsPad, e.z * boundsPad) * 2f);

		// 8頂点をビューポートに投影し、矩形交差で判定（z>0の点が最低1つ必要）
		Vector3 c = b.center;
		Vector3 ex = b.extents;
		Vector3[] corners = new Vector3[8] {
			c + new Vector3( ex.x,  ex.y,  ex.z),
			c + new Vector3( ex.x,  ex.y, -ex.z),
			c + new Vector3( ex.x, -ex.y,  ex.z),
			c + new Vector3( ex.x, -ex.y, -ex.z),
			c + new Vector3(-ex.x,  ex.y,  ex.z),
			c + new Vector3(-ex.x,  ex.y, -ex.z),
			c + new Vector3(-ex.x, -ex.y,  ex.z),
			c + new Vector3(-ex.x, -ex.y, -ex.z),
		};

		float minX = float.PositiveInfinity;
		float minY = float.PositiveInfinity;
		float maxX = -float.PositiveInfinity;
		float maxY = -float.PositiveInfinity;
		bool anyInFront = false;

		for (int i = 0; i < 8; i++)
		{
			var v = cam.WorldToViewportPoint(corners[i]);
			if (v.z > 0f) anyInFront = true; // カメラ前方に1点以上
			minX = Mathf.Min(minX, v.x);
			minY = Mathf.Min(minY, v.y);
			maxX = Mathf.Max(maxX, v.x);
			maxY = Mathf.Max(maxY, v.y);
		}

		if (!anyInFront) return false;

		float minAllowed = 0f - screenPad;
		float maxAllowed = 1f + screenPad;

		// ビューポート矩形の交差判定（少しでも重なれば可視とみなす）
		bool overlapX = (maxX >= minAllowed) && (minX <= maxAllowed);
		bool overlapY = (maxY >= minAllowed) && (minY <= maxAllowed);
		return overlapX && overlapY;
	}

	void Fire()
	{
		if (!target) return;

		if (instantComplete)
		{
			target.ResetGrowth(1f);
		}
		else
		{
			float from = (startFrom >= 0f) ? Mathf.Clamp01(startFrom) : 0f;
			target.Play(from);
		}

		_fired = true;
	}
}
