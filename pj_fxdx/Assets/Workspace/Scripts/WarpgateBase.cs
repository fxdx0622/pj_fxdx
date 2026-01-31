using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class WarpgateBase : MonoBehaviour
{
	private void Awake()
	{
		if (label != null)
			label.gameObject.SetActive(false);
	}

	private void OnTriggerEnter2D(Collider2D collision)
	{
		if (collision.CompareTag("Player"))
		{
			player.playerInRange = true;

			if (label != null)
				label.gameObject.SetActive(true);
		}
	}

	private void OnTriggerExit2D(Collider2D collision)
	{
		if (collision.CompareTag("Player"))
		{
			player.playerInRange = false;

			if (label != null)
				label.gameObject.SetActive(false);
		}
	}

	[SerializeField, Header("ワープラベル")]
	private TextMeshPro label;

	[SerializeField, Header("スクリーンフェーダー")]
	private ScreenFader fader;

	[SerializeField, Header("プレイヤー")]
	private PlayerBase player;
}
