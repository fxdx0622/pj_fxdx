using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class ScreenFader : MonoBehaviour
{
	public void FadeOutThenIn()
	{
		StartCoroutine(FadeOutInRoutine());
	}

	private IEnumerator FadeOutInRoutine()
	{
		player.canControl = false;

		// フェードアウト
		yield return StartCoroutine(Fade(0f, 1f));

		// 待機
		yield return new WaitForSeconds(waitTime);

		player.ResetToInitialPosition();

		// フェードイン
		yield return StartCoroutine(Fade(1f, 0f));

		player.canControl = true;
	}

	private IEnumerator Fade(float startAlpha, float endAlpha)
	{
		float timer = 0f;
		Color color = fadeImage.color;

		while (timer < fadeDuration)
		{
			timer += Time.deltaTime;
			color.a = Mathf.Lerp(startAlpha, endAlpha, timer / fadeDuration);
			fadeImage.color = color;
			yield return null;
		}

		// 完全に終わった時に Alpha を正確に設定
		color.a = endAlpha;
		fadeImage.color = color;
	}

	[SerializeField, Header("Image")]
	private Image fadeImage;

	[SerializeField, Header("フェード時間")]
	private float fadeDuration = 1f;

	[SerializeField, Header("フェードしたまま待機する時間")]
	private float waitTime = 0.5f;

	[SerializeField, Header("プレイヤー")]
	private PlayerBase player;

	private bool isFading = false;
}
