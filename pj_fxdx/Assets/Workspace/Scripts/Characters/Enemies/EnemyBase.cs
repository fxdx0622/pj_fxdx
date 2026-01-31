using UnityEngine;
using static CharacterBase;

/// <summary>
/// エネミーのベースクラス
/// </summary>
public class EnemyBase : CharacterBase
{
	public void SetSpawnPosition(Vector3 pos)
	{
		spawnPos = pos;
	}
	public SpriteRenderer GetSpriteRenderer()
	{
		return characterSpriteRenderer;
	}

	public void DropItems()
	{
		// 経験値は1つドロップ
		if (expPrefab != null)
		{
			Vector2 dropPos = (Vector2)transform.position + new Vector2(
				Random.Range(-0.2f, 0.2f),
				Random.Range(0.3f, 0.6f)
			);
			GameObject expObj = Instantiate(expPrefab, dropPos, Quaternion.identity);

			Rigidbody2D rbExp = expObj.GetComponent<Rigidbody2D>();
			if (rbExp != null)
			{
				Vector2 force = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(2f, 3f));
				rbExp.AddForce(force, ForceMode2D.Impulse);
			}
		}

		// ゴールドはランダム個数
		if (goldPrefab == null) return;

		int goldCount = Random.Range(goldDropMinCount, goldDropMaxCount + 1);

		for (int i = 0; i < goldCount; i++)
		{
			Vector2 dropPos = (Vector2)transform.position + new Vector2(
				Random.Range(-0.5f, 0.5f),
				Random.Range(0.3f, 0.8f)
			);
			GameObject goldObj = Instantiate(goldPrefab, dropPos, Quaternion.identity);

			Rigidbody2D rb = goldObj.GetComponent<Rigidbody2D>();
			if (rb != null)
			{
				Vector2 force = new Vector2(Random.Range(-1f, 1f), Random.Range(2f, 4f));
				rb.AddForce(force, ForceMode2D.Impulse);
			}
		}
	}

	public virtual void Awake(){}

	public override void Start()
	{
		// ステータス情報をセット
		characterStatus = defaultEnemyStatus;
		currentHP = characterStatus.HPMax;

		SetTeamType(ETeamType.Enemy);
	}

	public virtual void OnDestroy(){}

	public override void Update()
	{
		if (isDead == true)
		{
			deadBlinkTimer -= Time.deltaTime;
			deadBlinkInterval -= Time.deltaTime;

			if (deadBlinkTimer <= 0.0f)
			{
				// 死亡演出完了後自分を削除
				Destroy(gameObject);
				return;
			}
			else if (deadBlinkInterval <= 0.0f)
			{
				// 描画のONOFF切り替えで点滅っぽくする
				characterSpriteRenderer.enabled = !characterSpriteRenderer.enabled;
				deadBlinkInterval = DEFAULT_DEAD_BLINK_INTERVAL;
			}
		}
	}

	public virtual void LateUpdate(){}

	public override void FixedUpdate(){}

	protected int GetCurrentState()
	{
		return currentState;
	}

	protected int GetPrevState()
	{
		return prevState;
	}

	protected void ChangeState(int state)
	{
		prevState = currentState;
		currentState = state;

		// ステート切り替え時にProgressを初期化
		stateProgress = 0;
	}

	protected int GetStateProgress()
	{
		return stateProgress;
	}

	protected void AddStateProgress(int value)
	{
		stateProgress += value;
	}

	protected override void CharacterAction_Dead()
	{
		transform.rotation = Quaternion.Euler(0.0f, 0.0f, -90.0f);
		deadBlinkTimer = DEFAULT_DEAD_BLINK_TIMER;
		deadBlinkInterval = DEFAULT_DEAD_BLINK_INTERVAL * 3.0f;
	}

	protected virtual void Reset(){}

	public enum EEnemyType
	{
		None,
		Normal,
		Boss_001,
	}
	[field: SerializeField, Header("種別")]
	public EEnemyType enemyType { set; get; }

	[SerializeField, Header("エネミーの使用するステータス")]
	protected CharacterStatus defaultEnemyStatus;

	[SerializeField, Header("接触当たり判定のオフセット")]
	protected Vector2 contactDamageOffset;

	[SerializeField, Header("接触当たり判定のサイズ")]
	protected Vector2 contactDamageSize;

	[SerializeField, Header("ゴールドオブジェクト")]
	protected GameObject goldPrefab;

	[SerializeField, Header("経験値オブジェクト")]
	protected GameObject expPrefab;

	[SerializeField, Header("ゴールドドロップ数（最小値）")]
	private int goldDropMinCount = 1;

	[SerializeField, Header("ゴールドドロップ数（最大値）")]
	private int goldDropMaxCount = 3;

	protected float deadBlinkTimer = 0.0f;
	protected float deadBlinkInterval = 0.1f;
	static protected readonly float DEFAULT_DEAD_BLINK_TIMER = 0.8f;
	static protected readonly float DEFAULT_DEAD_BLINK_INTERVAL = 0.04f;

	/// <summary>
	///	リセット判定用パラメータ
	/// </summary>
	protected Vector3 spawnPos;

	private int currentState = 0;
	private int prevState = 0;
	private int stateProgress = 0;
}
