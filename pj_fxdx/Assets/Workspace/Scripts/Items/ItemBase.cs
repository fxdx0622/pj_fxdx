using UnityEngine;

/// <summary>
/// 全てのアイテム（ゴールド・経験値など）のベースクラス
/// </summary>
public class ItemBase : MonoBehaviour
{
	protected virtual void Start()
	{
		itemRigidbody2D = GetComponent<Rigidbody2D>();
		player = CharacterManager.Instance.GetCurrentPlayer();

		itemDropCoolTimer = lifeTime;
		itemDropBlinkInterval = DEFAULT_ITEM_DROP_BLINK_INTERVAL;
	}

	protected virtual void Update()
	{
		if (isCollected)
			return;

		itemDropCoolTimer -= Time.deltaTime;
		if (itemDropCoolTimer <= 0.0f)
		{
			// 寿命切れで削除
			Destroy(gameObject);
			return;
		}

		// 残り寿命が一定以下なら点滅処理
		if (itemDropCoolTimer <= blinkStartTime)
		{
			itemDropBlinkInterval -= Time.deltaTime;

			if (itemDropBlinkInterval <= 0.0f)
			{
				itemSpriteRenderer.enabled = !itemSpriteRenderer.enabled;
				itemDropBlinkInterval = DEFAULT_ITEM_DROP_BLINK_INTERVAL;
			}
		}
		else
		{
			itemSpriteRenderer.enabled = true;
		}

		if (player == null) return;

		// 吸引距離内に入ったらプレイヤーへ移動
		float distance = Vector2.Distance(transform.position, player.transform.position);
		if (distance <= magnetRange)
		{
			Vector2 dir = (player.transform.position - transform.position).normalized;
			itemRigidbody2D.linearVelocity = dir * moveSpeed;
		}
	}

	protected virtual void OnTriggerEnter2D(Collider2D collision)
	{
		if (isCollected) return;

		if (collision.CompareTag("Player"))
		{
			OnCollected(collision.GetComponent<PlayerBase>());
		}
	}

	/// <summary>
	/// アイテムが取得されたときの処理
	/// </summary>
	protected virtual void OnCollected(PlayerBase player)
	{
		isCollected = true;
		Destroy(gameObject);
	}

	[SerializeField, Header("自然消滅までの時間")]
	protected float lifeTime = 10.0f;

	[SerializeField, Header("寿命が近づいたときに点滅を開始するまでの残り時間")]
	protected float blinkStartTime = 3.0f;

	[SerializeField, Header("プレイヤー吸引距離")]
	protected float magnetRange = 2.5f;

	[SerializeField, Header("吸引速度")]
	protected float moveSpeed = 5.0f;

	[SerializeField, Header("アイテムのスプライト")]
	protected SpriteRenderer itemSpriteRenderer;

	/// <summary>
	///	状態管理タイマー
	/// </summary>
	protected float itemDropBlinkInterval = 0.0f;
	protected float itemDropCoolTimer = 0.0f;
	protected static readonly float DEFAULT_ITEM_DROP_BLINK_INTERVAL = 0.08f;

	protected Rigidbody2D itemRigidbody2D;
	protected bool isCollected = false;
	protected PlayerBase player;
}
