using UnityEngine;

/// <summary>
/// Enemy_001：地上キャラ スライム(仮)
/// </summary>
public class Enemy_001 : EnemyBase
{
	#region SECRET

	public override void Start()
	{
		base.Start();
		rigidbodyComponent = GetComponent<Rigidbody2D>();
		characterAnimator.Play(characterAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash, 0, Random.Range(0f, 1f));
		ChangeState((int)EEnemy001_State.Idle);
		AlphaTimer = defaultAlphaTime;
	}

	public override void Update()
	{
		//base.Update();

		if (isActionPause == true)
		{
			// イベントなどによりアクション停止
			return;
		}
		if (IsHitStop() == true)
		{
			UpdateHitStop();
			return;
		}

		UpdateKnockBack();

		CheckDetectedPlayer();

		// 全体のタイマー関連の更新
		if (attackCooldownTimer >= 0.0f)
		{
			attackCooldownTimer -= Time.deltaTime;
		}

		// ステート挙動の処理
		EEnemy001_State currentState = (EEnemy001_State)GetCurrentState();

		switch (currentState)
		{
			case EEnemy001_State.Idle:
				{
					UpdateStateIdle();
					break;
				}

			case EEnemy001_State.Move:
				{
					UpdateStateApproach();
					break;
				}

			case EEnemy001_State.Attack:
				{
					UpdateStateAttack();
					break;
				}

			case EEnemy001_State.Damage:
				{
					UpdateStateDamage();
					break;
				}

			case EEnemy001_State.Dead:
				{
					UpdateStateDead();
					break;
				}

			default:
				break;
		}

		// アクション結果の反映と設定
		UpdateCharacterDirection();
		UpdateCharacterAnimation();
	}

	public override void LateUpdate(){}

	public override void FixedUpdate()
	{
		// 物理挙動関連はFixedUpdateに設定
		if (isActionPause == true)
		{
			// イベントなどによりアクション停止
			rigidbodyComponent.linearVelocity = Vector2.zero;
			return;
		}

		if (IsHitStop() == true)
		{
			return;
		}

		// ステート挙動の処理
		EEnemy001_State currentState = (EEnemy001_State)GetCurrentState();

		switch (currentState)
		{
			case EEnemy001_State.Move:
				{
					Vector2 moveDirection = (characterDirectionRight == true) ? Vector2.right : Vector2.left;
					CharacterAction_Move(moveDirection);
					break;
				}

			case EEnemy001_State.Attack:
				{
					Vector2 moveDirection = (characterDirectionRight == true) ? Vector2.right : Vector2.left;
					CharacterAction_Attack(moveDirection);
					break;
				}

			default:
				{
					rigidbodyComponent.linearVelocity = Vector2.zero;
					break;
				}
		}
	}

	private void CheckDetectedPlayer()
	{
		PlayerBase playerObject = CharacterManager.Instance.GetCurrentPlayer();
		if (playerObject == null)
		{
			return;
		}

		// 自分の位置とプレイヤーの位置をチェック
		Vector2 currentPosition = (Vector2)transform.position + contactDamageOffset * transform.localScale;
		Vector2 playerPosition = playerObject.GetPlayerCenterPosition();
		float distanceX = currentPosition.x - playerPosition.x;
		float distanceY = currentPosition.y - playerPosition.y;

		distanceToPlayer = Vector2.Distance(currentPosition, playerPosition);

		if ((Mathf.Abs(distanceX) <= sightRange.x)
			&& (Mathf.Abs(distanceY) <= sightRange.y))
		{
			// プレイヤーに向けてRayCastして間に壁がないかどうか調べる
			Vector2 direction = playerPosition - currentPosition;
			direction.Normalize();

			LayerMask mapColliderMask = LayerMask.GetMask("MapCollider");
			RaycastHit2D hit = Physics2D.Raycast(currentPosition, direction, distanceToPlayer, mapColliderMask);
			if (hit.collider == null)
			{
				// デバッグ用に視界を描画
				Debug.DrawRay(currentPosition, direction * distanceToPlayer, Color.red);
			}
		}

		characterDirectionRight = (distanceX < 0.0f);
	}

	private void UpdateStateIdle()
	{
		ChangeState((int)EEnemy001_State.Move);
	}

	private void UpdateStateApproach()
	{
		if (GetStateProgress() == 0)
		{
			// アクションをMoveに切り替え
			isMove = true;

			// Progressを進める
			AddStateProgress(1);
		}

		// プレイヤーが攻撃実行範囲に入ったら攻撃へ移行
		if (distanceToPlayer <= attackRange.x)
		{
			isMove = false;
			ChangeState((int)EEnemy001_State.Attack);
			return;
		}
	}

	private void UpdateStateAttack()
	{
		if (GetStateProgress() == 0)
		{
			// アクションを攻撃に切り替え
			isAttack = true;


			PlayerBase playerObject = CharacterManager.Instance.GetCurrentPlayer();
			if (playerObject == null)
			{
				return;
			}
			Vector2 currentPosition = (Vector2)transform.position + contactDamageOffset * transform.localScale;
			Vector2 playerPosition = playerObject.GetPlayerCenterPosition();
			Vector3 direction = (playerPosition - currentPosition) * distanceToPlayer;
		
			Vector2 dir = direction.normalized;
			GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

			// 弾回転
			float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
			bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

			Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
			if (bulletRb != null)
			{
				bulletRb.isKinematic = false;
				bulletRb.gravityScale = 0;
				bulletRb.linearVelocity = dir * 5.0f;
			}

			// 攻撃持続時間タイマーをセット
			attackTimer = defaultAttackTime;

			// Progressを進める
			AddStateProgress(1);
		}

		if (attackTimer >= 0.0f)
		{
			attackTimer -= Time.deltaTime;
			return;
		}

		// 攻撃を終了する
		isAttack = false;

		// 攻撃のクールタイムをセット
		attackCooldownTimer = defaultAttackCooldownTime;

		// 待機状態へ移行
		ChangeState((int)EEnemy001_State.Idle);
	}

	private void UpdateStateDamage()
	{
		if (knockbackTimer >= 0.0f)
		{
			knockbackTimer -= Time.deltaTime;
			return;
		}

		// 終わったらIdleへ戻す
		isKnockBack = false;
		ChangeState((int)EEnemy001_State.Idle);
	}

	private void UpdateStateDead()
	{
		if (GetStateProgress() == 0)
		{
			if (deadActionTimer >= 0.0f)
			{
				deadActionTimer -= Time.deltaTime;
				return;
			}

			// Progressを進める
			AddStateProgress(1);
		}
		else if (GetStateProgress() == 1)
		{
			deadBlinkTimer -= Time.deltaTime;
			deadBlinkInterval -= Time.deltaTime;

			if (deadBlinkTimer <= 0.0f)
			{
				// Progressを進める
				AddStateProgress(1);
				return;
			}
			else if (deadBlinkInterval <= 0.0f)
			{
				// 描画のONOFF切り替えで点滅っぽくする
				characterSpriteRenderer.enabled = !characterSpriteRenderer.enabled;
				deadBlinkInterval = DEFAULT_DEAD_BLINK_INTERVAL;
			}
		}
		else if (GetStateProgress() == 2)
		{
			// 死亡演出完了後自分を削除
			Destroy(gameObject);
		}
	}

	private void UpdateCharacterDirection()
	{
		// 回転によりキャラクターの向きを設定
		characterSpriteRenderer.flipX = (characterDirectionRight != false);
	}

	private void UpdateCharacterAnimation()
	{
		// 移動関連アニメーションの判定
		characterAnimator.SetBool("isMove", isMove);

		// 攻撃関連アニメーションの判定
		characterAnimator.SetBool("isAttack", isAttack);

		// ダメージ関連アニメーションの判定
		characterAnimator.SetBool("isDead", isDead);
	}

	protected override void CharacterAction_Move(Vector2 moveDirection)
	{
		Vector2 currentVelocity = rigidbodyComponent.linearVelocity;
		float moveDirX = (moveDirection.x > 0.0f) ? 1.0f : -1.0f;

		Vector2 workVelocity = moveDirection * 2.0f;
		rigidbodyComponent.linearVelocity = workVelocity;
	}

	private void CharacterAction_Attack(Vector2 moveDirection)
	{
		rigidbodyComponent.linearVelocity = Vector2.zero;
	}

	protected override void CharacterAction_Dead()
	{
		// 死亡状態へ移行
		isDead = true;
		ChangeState((int)EEnemy001_State.Dead);

		// 点滅から消滅までの時間設定
		deadActionTimer = defaultDeadActionTime;
		deadBlinkTimer = DEFAULT_DEAD_BLINK_TIMER;
		deadBlinkInterval = DEFAULT_DEAD_BLINK_INTERVAL;

		// 死亡時にアイテムをドロップ
		if (!hasDroppedItems)
		{
			DropItems();
			hasDroppedItems = true;
		}
	}

	private void OnTriggerEnter2D(Collider2D collision)
	{
		if (collision.CompareTag("Bullet"))
		{
			currentHP -= 100;
			Debug.Log("エネミー体力 : " + currentHP);

			// 死亡判定
			if (currentHP <= 0)
			{
				isDead = true;
				Debug.Log("エネミー死亡通知 : " + name);

				CharacterAction_Dead();
			}
			else
			{
				SetHitStop(0.1f, 0.1f, false);
			}
		}
	}

	private enum EEnemy001_State
	{
		Idle = 0,       // 待機中
		Move,           // 移動
		Attack,         // 攻撃
		Damage,         // 被ダメージ中
		Dead,           // 死亡
	}

	/// <summary>
	///	キャラクターの状態
	/// </summary>
	private bool isMove = false;

	/// <summary>
	///	パラメーター
	/// </summary>
	[SerializeField, Header("視界範囲")]
	private Vector2 sightRange;

	[SerializeField, Header("攻撃範囲")]
	private Vector2 attackRange;

	[SerializeField, Header("プレイヤーに近づく移動速度")]
	private float approachSpeed = 5.0f;

	/// <summary>
	///	アクション管理周り
	/// </summary>
	private Rigidbody2D rigidbodyComponent;
	private float distanceToPlayer = 0.0f;
	private bool characterDirectionRight = false;

	/// <summary>
	///	状態管理タイマー
	/// </summary>
	[SerializeField, Header("攻撃持続時間")]
	private float defaultAttackTime = 2.0f;
	private float attackTimer = 0.0f;

	private bool hasDroppedItems = false;

	[SerializeField, Header("攻撃クールダウン時間")]
	private float defaultAttackCooldownTime = 5.0f;
	private float attackCooldownTimer = 0.0f;

	[SerializeField, Header("被ダメージのけぞり時間")]
	private float defaultKnockbackTime = 0.2f;
	private float knockbackTimer = 0.0f;

	[SerializeField, Header("死亡演出時間")]
	private float defaultDeadActionTime = 1.0f;
	private float deadActionTimer = 0.0f;

	[SerializeField, Header("半透明時間")]
	private float defaultAlphaTime = 1.0f;
	private float AlphaTimer = 0.0f;

	[SerializeField, Header("弾プレハブ")]
	private GameObject bulletPrefab;

	[SerializeField, Header("発射位置")]
	private Transform firePoint;

	#endregion
}
