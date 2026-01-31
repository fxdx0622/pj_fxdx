using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DissolveFadeController : MonoBehaviour
{
	[Header("Renderer")]
	[SerializeField] private Renderer targetRenderer;

	private MaterialPropertyBlock _mpb;

	private static readonly int CutoffHeightID = Shader.PropertyToID("_CutoffHeight");

	[SerializeField, Header("Renderer")]
	float cutoffHeightValue;

	[SerializeField, Header("ディゾルブのスピード")]
	float dissolveFadeSpeed = 1.0f;

	private void Awake()
	{
		_mpb = new MaterialPropertyBlock();

		PlayDissolve();
	}

	private void Update()
	{
		targetRenderer.GetPropertyBlock(_mpb);
		_mpb.SetFloat(CutoffHeightID, cutoffHeightValue);
		targetRenderer.SetPropertyBlock(_mpb);
	}

	public void PlayDissolve()
	{
		StartCoroutine(CutoffHeightCoroutine(0f, 22f, dissolveFadeSpeed));
	}

	private IEnumerator CutoffHeightCoroutine(float from, float to, float duration)
	{
		float time = 0f;

		while (time < duration)
		{
			time += Time.deltaTime;
			cutoffHeightValue = Mathf.Lerp(from, to, time / duration);
			yield return null;
		}

		cutoffHeightValue = to;
	}
}

#if UNITY_EDITOR
[CustomEditor(typeof(DissolveFadeController))]
public class DissolveFadeControllerEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		DissolveFadeController script = (DissolveFadeController)target;

		if (GUILayout.Button("Play Dissolve"))
		{
			script.PlayDissolve();
		}
	}
}
#endif