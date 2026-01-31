using UnityEngine;

#if UNITY_EDITOR
using UnityEditorInternal;
#endif

/// <summary>
/// キャラクターのパラメーター管理構造体
/// </summary>
[System.Serializable]
public struct CharacterStatus
{
	public int Level;               // レベル
	public int HPMax;               // 体力
	public int Speed;               // スピード
	public int Experience;          // 現在の経験値
	public int NextExperience;      // 次のレベルまでの経験値
};

/// <summary>
///  ゲーム中にキャラクターとして存在するオブジェクトのベースクラス
/// </summary>
public class CharacterBase : MonoBehaviour
{
		/// <summary>
	///	キャラクターのステータス関連
	/// </summary>
	public void GetCharacterHP(out int currentHP, out int HPMax)
	{
		currentHP = this.currentHP;
		HPMax = characterStatus.HPMax;
	}

	public virtual void SetCharacterHP(int setValue)
	{
		currentHP = setValue;

		if (currentHP > characterStatus.HPMax)
		{
			// 最大値でクリップ
			currentHP = characterStatus.HPMax;
		}
		else if (currentHP < 0)
		{
			currentHP = 0;
		}
	}

	public void AddCharacterHP(int addValue)
	{
		int resultHP = currentHP + addValue;
		SetCharacterHP(resultHP);
	}

	/// <summary>
	///	キャラクターの状態取得
	/// </summary>
	public bool IsDead()
	{
		return isDead;
	}

	/// <summary>
	/// キャラクターがアクションポーズ中かどうか
	/// </summary>
	/// <returns></returns>
	public bool IsActionPaused()
	{
		return isActionPause;
	}

	/// <summary>
	///	キャラクターのステータス取得
	/// </summary>
	public int GetCharacterLevel()
	{
		return characterStatus.Level;
	}

	/// <summary>
	///	キャラクターのステータス設定
	/// </summary>
	public virtual void SetCharacterActionPause(bool enable)
	{
		isActionPause = enable;

		var animators = GetComponentsInChildren<Animator>();
		foreach (var a in animators)
		{
			// Animatorをポーズ
			if (enable) a.speed = 0.0f;
			else a.speed = 1.0f;
		}
	}
	public bool CheckWallType(WallType wallType, WallType.EEnemyType enemyType = WallType.EEnemyType.None)
	{
		if (wallType == null)
		{
			return false;
		}


		switch (teamType)
		{
			case ETeamType.Player:
				if (wallType.GetWallType() == WallType.EWallType.Wall)
				{
					return true;
				}
				if (wallType.GetWallType() == WallType.EWallType.PlayerOnly)
				{
					return true;
				}
				if (wallType.GetWallType() == WallType.EWallType.EnemyOnly)
				{
					return false;
				}
				break;

			case ETeamType.Enemy:
				if (wallType.GetWallType() == WallType.EWallType.EnemyOnly)
				{
					if (wallType.IsHitEnemyType(enemyType) == true)
					{
						return true;
					}
				}
				if (wallType.GetWallType() == WallType.EWallType.Wall)
				{
					return true;
				}
				if (wallType.GetWallType() == WallType.EWallType.PlayerOnly)
				{
					return false;
				}
				break;
		}


		return false;
	}

	public GroundType GetGroundType()
	{
		return groundType;
	}
	public void SetGroundType(GroundType groundType)
	{
		this.groundType = groundType;
	}

	public ETeamType GetTeamType()
	{
		return teamType;
	}

	public void SetTeamType(ETeamType teamType)
	{
		this.teamType = teamType;
	}

	public bool IsHitStop()
	{
		return isHitStop;
	}

	public SpriteRenderer GetCharacterSpriteRenderer()
	{
		return characterSpriteRenderer;
	}

	public Animator GetCharacterAnimator()
	{
		return characterAnimator;
	}


	public void UpdateHitStop()
	{
		if (isHitStop == false)
		{
			return;
		}
		hitStopTimer -= Time.deltaTime;
		if (hitStopTimer <= 0.0f)
		{
			hitStopTimer = 0.0f;
			var animators = GetComponentsInChildren<Animator>();
			foreach (var animatior in animators)
			{
				animatior.speed = 1.0f;
			}
			characterSpriteRenderer.gameObject.transform.localPosition = offsetHitStopPos;
			isHitStop = false;
			// ヒットストップ終了後にRigidbody2Dの速度を復元
			characterRigidbody2D.linearVelocity = knockBackSaveRigidVel;
		}
		else
		{
			// ヒットストップ中は微妙に揺らす
			Vector3 random = Vector3.zero;
			random.x = Random.Range(-hitStopShakeValue, hitStopShakeValue);
			if (!isGround)
			{
				random.y = Random.Range(-hitStopShakeValue, hitStopShakeValue);
			}
			characterSpriteRenderer.gameObject.transform.localPosition = offsetHitStopPos + random;
		}
	}
	public void SetHitStop(float time, float shakeValue, bool isHitter, bool isDead = false)
	{
		isHitStop = true;
		hitStopTimer = time;
		offsetHitStopPos = characterSpriteRenderer.gameObject.transform.localPosition;
		hitStopShakeValue = shakeValue;
		knockBackSaveRigidVel = characterRigidbody2D.linearVelocity;
		characterRigidbody2D.linearVelocity = Vector2.zero;
		var animators = GetComponentsInChildren<Animator>();
		foreach (var animatior in animators)
		{
			if (isHitter)
			{
				// 攻撃側は少しだけ動かす
				animatior.speed = 0.1f;
			}
			else
			{
				animatior.speed = 0.0f;
				// 死亡時は少しだけ動かす
				if (isDead)
				{
					animatior.speed = 0.2f;
				}
			}
		}
	}

	public void SetKnockBack(float power, float damping, float threshold, Vector3 hitpos)
	{
		isKnockBackAction = true;
		knockBackValue = power - (power * knockBackResistance);
		knockBackDamping = damping;
		knockBackThreshold = threshold;
		knockBackHitPos = hitpos;
		knockBackDirection = (transform.position - hitpos).normalized;
	}

	public virtual void Start(){}

	public virtual void Update(){}

	public virtual void FixedUpdate(){}

	#region SECRET

	/// <summary>
	///	キャラクターのアクション関連
	/// </summary>
	protected virtual void CharacterAction_Move(Vector2 moveDirection)
	{

	}

	protected virtual void CharacterAction_Attack()
	{

	}

	protected virtual void CharacterAction_Hover()
	{

	}

	protected virtual void CharacterAction_Crouch()
	{

	}

	protected virtual void CharacterAction_FinishCrouch()
	{

	}

	protected virtual void CharacterAction_Damage()
	{

	}

	protected virtual void CharacterAction_Dead()
	{

	}

	protected virtual void UpdateKnockBack()
	{
		if (isKnockBackAction == false)
		{
			return;
		}
		if (knockBackValue <= knockBackThreshold)
		{
			isKnockBackAction = false;
			knockBackValue = 0.0f;
			return;
		}
		Vector2 moveDirection = knockBackDirection;
		moveDirection.y = 0.0f; // 水平方向のみノックバック
		KnockBackMove(moveDirection, knockBackValue);
		knockBackValue -= knockBackDamping * Time.deltaTime;
	}

	protected virtual void KnockBackMove(Vector2 moveDirection, float power)
	{
		Vector2 Direction = moveDirection.normalized * power * Time.deltaTime;
		Vector2 ColVec = (Vector2)transform.position + moveDirection.normalized * knockBackColSize;

		// スロープや壁際、崖際に到達したら攻撃へ移行
		Vector2 currentPosition = ColVec;
		currentPosition.y += 0.1f; // 少し上からRayを飛ばす
		LayerMask mapColliderMask = LayerMask.GetMask("MapCollider");
		RaycastHit2D hit = Physics2D.Raycast(currentPosition, moveDirection.normalized, power * Time.deltaTime, mapColliderMask);
		if (hit.collider != null)
		{
			// 壁にぶつかった
			isKnockBackAction = false;
			knockBackValue = 0.0f;
			return;
		}

		// 前方足元チェック
		currentPosition = ColVec + Direction;
		currentPosition.y += 0.2f;
		Vector2 direction = Vector2.down;
		float distance = 1f;
		hit = Physics2D.Raycast(currentPosition, direction, distance, mapColliderMask);
		if (hit.collider == null)
		{
			// 前方足元に床がない
			isKnockBackAction = false;
			knockBackValue = 0.0f;
			return;
		}
		else
		{
			// 前方足元地面の法線ベクトルを取得して角度をチェック
			Vector2 groundNormal = hit.normal;
			float angle = Vector2.Angle(groundNormal, Vector2.up);
			Debug.Log("Enemy00スロープ判定 : " + angle + " : " + groundNormal + " : " + hit.collider.gameObject.name);
			if ((angle > 1.0f)
				&& angle < KNOCKBACK_SLOPE_ANGLE_MAX)
			{
				// スロープには侵入しない
				isKnockBackAction = false;
				knockBackValue = 0.0f;
				return;
			}
		}

		transform.position += (Vector3)Direction;
	}

	/// <summary>
	///	キャラクターのチームタイプ
	/// </summary>
	public enum ETeamType
	{
		None = 0,
		Player = 1,
		Enemy = 2,
	}

	/// <summary>
	///	キャラクターの状態
	/// </summary>
	protected bool isGround = false;        // 接地している
	protected bool isHover = false;         // ホバー中
	protected bool isFall = false;          // 落下中
	protected bool isAttack = false;        // 攻撃中

	protected bool isDead = false;          // 死亡
	protected bool isKnockBack = false;     // 被ダメージ中
	protected bool isActionPause = false;   // キャラクターのアクション全体に作用するポーズフラグ

	protected GroundType groundType;        // 接地している地面のタイプ

	[SerializeField, Header("キャラクターのチームタイプ")]
	protected ETeamType teamType = ETeamType.None;

	[SerializeField, Header("キャラのスプライト")]
	protected SpriteRenderer characterSpriteRenderer;
	[SerializeField, Header("キャラのAnimator")]
	protected Animator characterAnimator;
	[SerializeField, Header("キャラのRigidbody2D")]
	protected Rigidbody2D characterRigidbody2D;

	/// <summary>
	///	キャラクターのステータス関連
	/// </summary>
	protected CharacterStatus characterStatus;
	protected int currentHP;

	protected bool isHitStop = false; // ヒットストップ中かどうか
	protected float hitStopTimer = 0.0f;
	protected float hitStopShakeValue = 0.0f;
	protected Vector3 offsetHitStopPos = Vector3.zero;

	[SerializeField, Header("ノックバック抵抗値:0から１( １はノックバックしない)")]
	protected float knockBackResistance = 0.0f; // ノックバック抵抗値（大きいほどノックバックしにくい）
	[SerializeField, Header("ノックバック時のコライダーサイズ(半径)")]
	protected float knockBackColSize = 0.4f;
	protected bool isKnockBackAction = false; // ノックバック中
	protected float knockBackValue = 0.0f;
	protected float knockBackDamping = 0.9f;
	protected float knockBackThreshold = 0.1f;
	protected Vector3 knockBackHitPos = Vector3.zero;
	protected Vector3 knockBackDirection = Vector3.zero;
	protected Vector3 knockBackSaveRigidVel = Vector3.zero;
	protected static readonly float KNOCKBACK_SLOPE_ANGLE_MAX = 50.0f;

	#endregion
}
