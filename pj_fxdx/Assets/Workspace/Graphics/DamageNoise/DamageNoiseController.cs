using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ダメージノイズ制御クラス
/// </summary>
public class DamageNoiseController : MonoBehaviour
{
	public void PlayDamageNoise()
	{
		StopAllCoroutines();
		StartCoroutine(DamageNoiseRoutine());
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
		if (isDamageNoise)
		{
			foreach (var renderer in renderers)
			{
				renderer.GetPropertyBlock(mpb);
				mpb.SetColor("_Color", color);
				mpb.SetFloat("_Bias", bias);
				mpb.SetFloat("_ScanningFrequency", scanningFrequency);
				mpb.SetFloat("_ScanningSpeed", scanningSpeed);
				mpb.SetFloat("_GlitchFrameRate", glitchFrameRate);
				mpb.SetFloat("_GlitchFrequency", glitchFrequency);
				mpb.SetFloat("_GlitchScale", glitchScale);
				renderer.SetPropertyBlock(mpb);
			}
		}
	}

	IEnumerator DamageNoiseRoutine()
	{
		isDamageNoise = true;
		ApplyDamageNoise();

		yield return new WaitForSeconds(damageNoiseDuration);

		isDamageNoise = false;
		RestoreOriginal();
	}

	void ApplyDamageNoise()
	{
		foreach (var r in renderers)
		{
			var originalMats = originalMaterials[r];
			var newMats = new Material[originalMats.Length];

			for (int i = 0; i < originalMats.Length; i++)
			{
				Material newMat;
				if (damageNoiseMaterial != null)
				{
					newMat = new Material(damageNoiseMaterial);
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
			mpb.SetColor("_Color", color);
			mpb.SetFloat("_Bias", bias);
			mpb.SetFloat("_ScanningFrequency", scanningFrequency);
			mpb.SetFloat("_ScanningSpeed", scanningSpeed);
			mpb.SetFloat("_GlitchFrameRate", glitchFrameRate);
			mpb.SetFloat("_GlitchFrequency", glitchFrequency);
			mpb.SetFloat("_GlitchScale", glitchScale);
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

	bool isDamageNoise = false;

	[SerializeField, Header("ダメージ演出マテリアル")]
	Material damageNoiseMaterial;

	[SerializeField, Header("ダメージ演出時間")]
	float damageNoiseDuration = 100.0f;

	List<Renderer> renderers = new List<Renderer>();

	Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

	MaterialPropertyBlock mpb;

	[SerializeField, Header("ダメージ演出カラー")]
	Color color = new Color(0.0f, 0.5f, 1.0f);

	[SerializeField, Header("ダメージ演出Bias")]
	float bias = 0.5f;

	[SerializeField, Header("スキャン線密度")]
	float scanningFrequency = 2.0f;

	[SerializeField, Header("スキャン線移動速度")]
	float scanningSpeed = 1.0f;

	[SerializeField, Header("グリッチフレーム更新周期")]
	float glitchFrameRate = 2.0f;

	[SerializeField, Header("グリッチ密度")]
	float glitchFrequency = 1.0f;

	[SerializeField, Header("グリッチ強度")]
	float glitchScale = 1.0f;

	#endregion
}

[CustomEditor(typeof(DamageNoiseController))]
public class DamageNoiseControllerEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		if (Application.isPlaying)
		{
			if (GUILayout.Button("▶ Play DamageNoise"))
			{
				((DamageNoiseController)target).PlayDamageNoise();
			}
		}
		else
		{
			EditorGUILayout.HelpBox("Play Mode でテスト動作可能", MessageType.Info);
		}
	}
}

