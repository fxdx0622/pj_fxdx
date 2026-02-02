using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

/// <summary>
/// 落ち影の個別要素
/// </summary>
[System.Serializable]
public class BuildingShadowElement
{
	[SerializeField]
	private string label;

	[SerializeField]
	private SpriteRenderer target;

	[Header("落ち影の有効化")][SerializeField] private bool enableShadow = true;

	[Header("個別調整設定の有効化")][SerializeField] private bool useLocalSettings = false;

	[Header("落ち影のオフセット（グローバル設定の値に加算されます）")][SerializeField] private Vector2 localOffset = Vector2.zero;

	[Header("落ち影の角度（グローバル設定の値に加算されます）")][SerializeField] private float localShearX = 0f;

	[Header("落ち影の高さ（グローバル設定とは関係なしにローカル設定になります）")]
	[SerializeField, Range(0f, 1f)] private float localHeightScale = 1.0f;

	[Header("落ち影の先端の太さ（グローバル設定とは関係なしにローカル設定になります）")]
	[SerializeField, Range(0f, 1f)] private float localExpandFar = 0f;

	public string Label => string.IsNullOrEmpty(label) && target != null ? target.name : label;
	public SpriteRenderer Target => target;
	public bool EnableShadow => enableShadow;
	public bool UseLocalSettings => useLocalSettings;
	public Vector2 LocalOffset => localOffset;
	public float LocalShearX => localShearX;
	public float LocalHeightScale => localHeightScale;
	public float LocalExpandFar => localExpandFar;

	public void InitializeFromRenderer(SpriteRenderer renderer)
	{
		target = renderer;
		if (renderer == null) return;

		if (string.IsNullOrEmpty(label))
			label = renderer.name;

		enableShadow = true;
		useLocalSettings = false;
		localOffset = Vector2.zero;
		localShearX = 0f;
		localHeightScale = 1f;
		localExpandFar = 0f;
	}
}

/// <summary>
/// 建物の落ち影を管理するクラス
/// </summary>
[ExecuteAlways]
public class BuildingShadowSetController : MonoBehaviour
{
	[SerializeField] private Material shadowMaterial;
	[SerializeField] private bool previewInEditMode = true;

	[Header("グローバル落ち影設定")]

	[Header("落ち影のカラー")]
	[SerializeField] private Color globalShadowColor = new Color(0, 0, 0, 0);

	[Header("落ち影の濃さ")]
	[SerializeField, Range(0f, 1f)] private float shadowStrength = 0.6f;

	[Header("落ち影の傾き")]
	[SerializeField] private float globalShearX = -0.3f;

	[Header("落ち影の高さ")]
	[SerializeField, Range(0f, 1f)] private float globalHeightScale = 1.0f;

	[Header("落ち影の先端の太さ")]
	[SerializeField, Range(0f, 1f)] private float globalExpandFar = 0f;

	[Header("落ち影のオフセット")]
	[SerializeField] private Vector2 globalShadowOffset = new Vector2(0f, -0.1f);

	[Header("Roots（落ち影の元となるSpriteRenderer）")]
	[SerializeField] private GameObject[] roots;

	[Header("Elements（落ち影）")]
	[SerializeField] private BuildingShadowElement[] elements;

	class ShadowRuntime
	{
		public BuildingShadowElement element;
		public SpriteRenderer source;
		public SpriteRenderer shadow;
		public Vector3 baseLocalPosition;
	}

	readonly List<ShadowRuntime> runtimes = new();
	MaterialPropertyBlock mpb;

	static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
	static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
	static readonly int ShearXId = Shader.PropertyToID("_ShearX");
	static readonly int HeightScaleId = Shader.PropertyToID("_HeightScale");
	static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");
	static readonly int ExpandFarId = Shader.PropertyToID("_ExpandFar");

	void OnEnable()
	{
		mpb ??= new MaterialPropertyBlock();
		BuildRuntimesFromElements();
	}

	void OnDisable()
	{
		foreach (var r in runtimes)
			if (r?.shadow != null)
				r.shadow.enabled = false;
	}

	void OnDestroy()
	{
		foreach (var r in runtimes)
		{
			if (r?.shadow == null) continue;
#if UNITY_EDITOR
			if (!Application.isPlaying)
				DestroyImmediate(r.shadow.gameObject);
			else
#endif
				Destroy(r.shadow.gameObject);
		}
		runtimes.Clear();
	}

	void LateUpdate()
	{
		if (runtimes.Count == 0) return;

		bool playing = Application.isPlaying;

		foreach (var r in runtimes)
		{
			var e = r.element;
			var src = r.source;
			var sh = r.shadow;
			if (e == null || src == null || sh == null) continue;

			bool visible = e.EnableShadow && src.enabled && (playing || previewInEditMode);
			sh.enabled = visible;
			if (!visible) continue;

			Vector2 offset = globalShadowOffset;
			float shear = globalShearX;
			float heightScale;
			float expandFar;

			if (e.UseLocalSettings)
			{
				offset += e.LocalOffset;
				shear += e.LocalShearX;
				heightScale = e.LocalHeightScale;
				expandFar = e.LocalExpandFar;
			}
			else
			{
				offset = globalShadowOffset;
				shear = globalShearX;
				heightScale = globalHeightScale;
				expandFar = globalExpandFar;
			}

			float height01 = Mathf.Lerp(0.99f, 1.0f, heightScale);
			float signedHeight = height01 * -1f;

			var t = sh.transform;
			t.localPosition = r.baseLocalPosition + new Vector3(offset.x, offset.y, 0);
			t.localRotation = Quaternion.identity;
			t.localScale = Vector3.one;

			sh.sprite = src.sprite;
			sh.flipX = src.flipX;
			sh.flipY = src.flipY;

			sh.GetPropertyBlock(mpb);
			mpb.SetFloat(ShearXId, shear);
			mpb.SetFloat(HeightScaleId, signedHeight);
			mpb.SetColor(ShadowColorId, globalShadowColor);
			mpb.SetFloat(ShadowStrengthId, shadowStrength);
			mpb.SetFloat(ExpandFarId, expandFar);
			sh.SetPropertyBlock(mpb);
		}
	}

	public void RebuildElementsFromRoots()
	{
		var renderers = CollectRenderers();
		var list = new List<BuildingShadowElement>();

		foreach (var r in renderers)
		{
			// 既存要素を検索
			var existing = System.Array.Find(elements, e => e != null && e.Target == r);

			if (existing != null)
			{
				// 既存設定を維持
				list.Add(existing);
			}
			else
			{
				// 新規だけ初期化
				var e = new BuildingShadowElement();
				e.InitializeFromRenderer(r);
				list.Add(e);
			}
		}

		elements = list.ToArray();
		BuildRuntimesFromElements();
	}

	public void RemoveMissingElements()
	{
		var list = new List<BuildingShadowElement>();
		foreach (var e in elements)
			if (e != null && e.Target != null)
				list.Add(e);

		elements = list.ToArray();
		BuildRuntimesFromElements();
	}

	void BuildRuntimesFromElements()
	{
		runtimes.Clear();
		if (elements == null || shadowMaterial == null) return;

		foreach (var e in elements)
		{
			if (e?.Target == null) continue;
			if (IsShadowRenderer(e.Target)) continue;

			runtimes.Add(CreateShadow(e, e.Target));
		}
	}

	ShadowRuntime CreateShadow(BuildingShadowElement e, SpriteRenderer src)
	{
		string name = e.Label + "_Shadow";

		SpriteRenderer sh = null;
		foreach (Transform child in src.transform)
		{
			if (child.name.EndsWith("_Shadow"))
			{
				sh = child.GetComponent<SpriteRenderer>();
				if (sh != null) break;
			}
		}

		// 無ければ新規生成
		if (sh == null)
		{
			var go = new GameObject(name);
			go.transform.SetParent(src.transform, false);
			sh = go.AddComponent<SpriteRenderer>();
		}
		else
		{
			// 既存影の名前だけ更新
			if (sh.gameObject.name != name)
				sh.gameObject.name = name;
		}

		sh.sharedMaterial = shadowMaterial;
		sh.sprite = src.sprite;

		Vector3 basePos = Vector3.zero;

		if (src.sprite != null)
		{
			float parentPivotY = src.sprite.pivot.y / src.sprite.rect.height;
			float spriteHeight = src.sprite.rect.height / src.sprite.pixelsPerUnit;
			float offsetY = -(1f - parentPivotY) * spriteHeight - 0.5f * spriteHeight;

			basePos = new Vector3(0f, offsetY, 0f);
			sh.transform.localPosition = basePos;
		}
		else
		{
			sh.transform.localPosition = Vector3.zero;
		}

		return new ShadowRuntime
		{
			element = e,
			source = src,
			shadow = sh,
			baseLocalPosition = basePos
		};
	}

	List<SpriteRenderer> CollectRenderers()
	{
		var list = new List<SpriteRenderer>();
		if (roots == null) return list;

		foreach (var root in roots)
		{
			if (root == null) continue;
			foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
				if (!IsShadowRenderer(r))
					list.Add(r);
		}
		return list;
	}

	bool IsShadowRenderer(SpriteRenderer r)
	{
		return r != null && r.gameObject.name.EndsWith("_Shadow");
	}
}

#if UNITY_EDITOR
[CustomEditor(typeof(BuildingShadowSetController))]
[CanEditMultipleObjects]
public class BuildingShadowSetControllerEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		EditorGUILayout.LabelField("落ち影生成", EditorStyles.boldLabel);

		var ctrls = new List<BuildingShadowSetController>();
		foreach (var t in targets)
			ctrls.Add((BuildingShadowSetController)t);

		using (new EditorGUILayout.HorizontalScope())
		{
			if (GUILayout.Button("Rootから落ち影を生成（既存の影は維持したまま）"))
			{
				foreach (var c in ctrls)
				{
					Undo.RecordObject(c, "Rebuild Shadow Elements");
					c.RebuildElementsFromRoots();
					EditorUtility.SetDirty(c);
				}
			}
		}

		if (GUILayout.Button("Missing 要素を削除"))
		{
			foreach (var c in ctrls)
			{
				Undo.RecordObject(c, "Remove Missing Shadow Elements");
				c.RemoveMissingElements();
				EditorUtility.SetDirty(c);
			}
		}
	}
}
#endif