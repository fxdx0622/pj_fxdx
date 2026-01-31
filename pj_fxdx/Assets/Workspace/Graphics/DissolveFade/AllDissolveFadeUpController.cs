using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 一括ディゾルブフェード制御クラス（開幕演出用）
/// </summary>
public class AllDissolveFadeUpController : MonoBehaviour
{
	public void PlayDissolveFade()
	{
		StopAllCoroutines();
		StartCoroutine(CutoffHeightCoroutine(13f, -13f, dissolveFadeSpeed));
	}

	#region SECRET
	void Awake()
	{
		renderers.AddRange(GetComponentsInChildren<Renderer>());
		mpb = new MaterialPropertyBlock();

		foreach (var r in renderers)
		{
			originalMaterials[r] = r.sharedMaterials;
		}
	}

	void Update()
	{
		if (isDissolveFade)
		{
			foreach (var renderer in renderers)
			{
				renderer.GetPropertyBlock(mpb);
				mpb.SetFloat(CutoffHeightID, cutoffHeightValue);
				renderer.SetPropertyBlock(mpb);
			}
		}
	}

	private IEnumerator CutoffHeightCoroutine(float from, float to, float duration)
	{
		isDissolveFade = true;
		ApplyDissolveFade();

		float time = 0f;

		while (time < duration)
		{
			time += Time.deltaTime;
			cutoffHeightValue = Mathf.Lerp(from, to, time / duration);
			yield return null;
		}

		cutoffHeightValue = to;



		yield return new WaitForSeconds(dissolveFadeSpeed);

		isDissolveFade = false;
		RestoreOriginal();
	}

	void ApplyDissolveFade()
	{
		foreach (var r in renderers)
		{
			var originalMats = originalMaterials[r];
			var newMats = new Material[originalMats.Length];

			for (int i = 0; i < originalMats.Length; i++)
			{
				Material newMat;
				if (dissolveFadeMaterial != null)
				{
					newMat = new Material(dissolveFadeMaterial);
					// 元のテクスチャとUV情報をコピー
					var tex = originalMats[i].GetTexture("_MainTex");
					if (tex != null)
					{
						newMat.SetTexture("_MainTex", tex);
						newMat.SetTextureOffset("_MainTex", originalMats[i].GetTextureOffset("_MainTex"));
						newMat.SetTextureScale("_MainTex", originalMats[i].GetTextureScale("_MainTex"));
					}
				}
				else
				{
					newMat = new Material(originalMats[i]);
				}

				newMats[i] = newMat;
			}

			// sharedMaterials ではなく materials を使う
			r.materials = newMats;

			// PropertyBlock を初期設定
			r.GetPropertyBlock(mpb);
			mpb.Clear();
			mpb.SetFloat(CutoffHeightID, cutoffHeightValue);
			r.SetPropertyBlock(mpb);
		}
	}

	void RestoreOriginal()
	{
		foreach (var r in renderers)
		{
			r.sharedMaterials = originalMaterials[r];

			mpb.Clear();
			r.SetPropertyBlock(mpb);
		}
	}

	bool isDissolveFade = false;

	[SerializeField, Header("ディゾルブフェードマテリアル")]
	Material dissolveFadeMaterial;

	[SerializeField, Header("ディゾルブのスピード")]
	float dissolveFadeSpeed = 1.0f;

	List<Renderer> renderers = new List<Renderer>();

	Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

	MaterialPropertyBlock mpb;


	private static readonly int CutoffHeightID = Shader.PropertyToID("_CutoffHeight");

	[SerializeField, Header("Renderer")]
	float cutoffHeightValue;

	#endregion
}

[CustomEditor(typeof(AllDissolveFadeUpController))]
public class DissolveFadeControllerUpEditor_v2 : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		if (Application.isPlaying)
		{
			if (GUILayout.Button("▶ Play DamageNoise"))
			{
				((AllDissolveFadeUpController)target).PlayDissolveFade();
			}
		}
		else
		{
			EditorGUILayout.HelpBox("Play Mode でテスト動作可能", MessageType.Info);
		}
	}
}

