using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.VirtualTexturing;
using UnityEngine.Tilemaps;
using static UnityEngine.UI.Image;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerBase: CharacterBase
{
	public void OnMove(InputAction.CallbackContext context)
	{
		if (!canControl) return;
		moveInput = context.ReadValue<Vector2>();
	}

	public void OnLook(InputAction.CallbackContext context)
	{
		if (!canControl) return;
		lookInput = context.ReadValue<Vector2>();
	}

	public void OnHover(InputAction.CallbackContext context)
	{
		if (!canControl) return;
		if (context.started || context.performed)
		{
			isHoverInput = true;
			isFall = false;
		}
		else if (context.canceled)
		{
			isHoverInput = false;
		}
	}

	public void OnJumpHover(InputAction.CallbackContext context)
	{
		if (!canControl) return;

		// 押した瞬間 → ジャンプ予約
		if (context.started)
		{
			// 地上にいるときだけジャンプ
			if (isGrounded)
			{
				jumpRequested = true;
				isFall = false;
			}

			// 空中ではホバーON
			if (!isGrounded)
			{
				isHoverInput = true;
				isFall = false;
			}
		}
		// 長押し中
		else if (context.performed)
		{
			if (!isGrounded)
				isHoverInput = true;
		}
		// 離したらホバー解除
		else if (context.canceled)
		{
			isHoverInput = false;
		}
	}

	public void OnWarp(InputAction.CallbackContext context)
	{
		if (!canControl) return;

		if (playerInRange)
		{
			if (context.performed)
			{
				fader.FadeOutThenIn();
			}
		}
	}

	public void ResetToInitialPosition()
	{
		transform.position = initialPosition;
	}

	/// <summary>
	///	視線判定などプレイヤーの中心位置を設定する
	/// </summary>
	public Vector2 GetPlayerCenterPosition()
	{
		return ((Vector2)transform.position + collisionCollider.offset * transform.localScale);
	}

	public override void SetCharacterHP(int setValue)
	{
		base.SetCharacterHP(setValue);

		if ((isDead == true)
			&& (currentHP > 0))
		{
			isDead = false;
		}
	}

	/// <summary>
	///	プレイヤーの無敵状態を取得
	/// </summary>
	public bool IsPlayerInvincible()
	{
		return isInvincible;
	}

	#region SECRET

	public override void Start()
	{
		// CharacterManagerへ登録
		CharacterManager.Instance.RegisterCurrentPlayer(this);

		rb = GetComponent<Rigidbody2D>();
		rb.freezeRotation = true;

		// 初期位置を記録
		initialPosition = transform.position;

		currentHP = 1000;

		if (hoverEffect != null)
			hoverEffect.SetActive(false);
	}

	public override void Update()
	{
		if (isDead == true)
		{
			return;
		}

		// キャラスケール追従
		if (characterTransform != null)
			characterTransform.localScale = transform.localScale;

		// 右スティックの横入力がある場合のみ向きを更新
		if (Mathf.Abs(lookInput.x) > 0.1f)
		{
			float sign = lookInput.x > 0 ? -1f : 1f;

			Vector3 scale = transform.localScale;
			scale.x = Mathf.Abs(scale.x) * sign;
			transform.localScale = scale;
		}

		// ホバー判定：空中でLT押してる間
		isHover = isHoverInput;

		UpdateGunRotation();
		HandleShooting();

		// 被ダメージクールダウン
		if (recieveDamageCoolTimer > 0.0f)
		{
			isInvincible = true;

			recieveDamageCoolTimer -= Time.deltaTime;
			recieveDamageBlinkInterval -= Time.deltaTime;

			if (recieveDamageCoolTimer <= 0.0f)
			{
				// 被ダメージ処理終了
				characterSpriteRenderer.enabled = true;
			}
			else if (recieveDamageBlinkInterval <= 0.0f)
			{
				// 描画のONOFF切り替えで点滅っぽくする
				characterSpriteRenderer.enabled = !characterSpriteRenderer.enabled;
				recieveDamageBlinkInterval = DEFAULT_RECIEVE_DAMAGE_BLINK_INTERVAL;
			}
		}
		else
		{
			isInvincible = false;
		}
	}

	public override void FixedUpdate()
	{
		if (!canControl)
		{
			rb.linearVelocity = Vector2.zero;
			animator.SetBool("isMoving", false);
			return;
		}

		if (isDead != true)
		{
			// 横移動
			float vx = moveInput.x * moveSpeed;
			rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);


			// 横向き反転
			if (Mathf.Abs(moveInput.x) > 0.01f)
			{
				transform.localScale = new Vector3(
					-Mathf.Sign(moveInput.x) * Mathf.Abs(transform.localScale.x),
					transform.localScale.y,
					transform.localScale.z
				);
			}

			// ジャンプ
			if (jumpRequested)
			{
				rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
				rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
				jumpRequested = false;
				isJump = true;
				isGrounded = false;
			}

			// ホバー（空中のみ）
			if (isHover && !isGrounded)
			{
				float hover = 1000f * hoverForce * Time.deltaTime;
				rb.AddForce(Vector2.up * hover, ForceMode2D.Force);
				isFall = false;
			}
		}

		UpdateCharacterAnimation();
		UpdateGroundCheck();
		UpdateHoverEffect();
	}

	private void UpdateCharacterAnimation()
	{
		// 移動関連アニメーションの判定
		Vector2 currentVelocity = rb.linearVelocity;
		bool isMoving = (currentVelocity.x != 0.0f) ? true : false;
		animator.SetBool("isMoving", isMoving);

		animator.SetBool("isHover", !isGrounded && isHover);

		animator.SetBool("isJump", isJump);

		animator.SetBool("isFall", isFall);

		animator.SetBool("isDead", isDead);
	}

	private void UpdateGroundCheck()
	{
		RaycastHit2D hit = Physics2D.BoxCast(
			rb.GetComponent<Collider2D>().bounds.center,   
			rb.GetComponent<Collider2D>().bounds.size,    
			0f,                                          
			Vector2.down,                                  
			groundCheckDistance,                           
			groundLayer                                    
		);

		if (hit.collider != null)
		{
			if (!isGrounded)
			{
				isJump = false;
			}

			isGrounded = true;
			isFall = true;
		}
		else
		{
			isGrounded = false;
		}
	}

	private void UpdateHoverEffect()
	{
		bool shouldPlay =
			isHover &&
			!isGrounded;

		if (hoverEffect == null) return;

		if (shouldPlay && !isHoverEffectPlaying)
		{
			hoverEffect.SetActive(true);
			isHoverEffectPlaying = true;
		}
		else if (!shouldPlay && isHoverEffectPlaying)
		{
			hoverEffect.SetActive(false);
			isHoverEffectPlaying = false;
		}
	}

	private void UpdateGunRotation()
	{
		if (gunTransform == null) return;

		float angle;

		if (lookInput.sqrMagnitude > 0.01f)
		{
			// 右スティック入力で自由回転
			lastLookInput = lookInput.normalized;
			angle = Mathf.Atan2(lookInput.y, lookInput.x) * Mathf.Rad2Deg;
		}
		else
		{
			// 入力がゼロのときは水平補正
			angle = transform.localScale.x >= 0 ? 0f : 180f;
		}

		// 親キャラが左向きなら回転を180°反転して逆さま防止
		if (transform.localScale.x < 0)
			angle += 180f;

		// 回転反映
		gunTransform.rotation = Quaternion.RotateTowards(
			gunTransform.rotation,
			Quaternion.Euler(0, 0, angle),
			360f * Time.deltaTime
		);

		// スケール固定
		//gunTransform.localScale = new Vector3(0.03f, 0.03f, 1f);
	}

	private void HandleShooting()
	{
		if (lookInput.magnitude >= stickFireThreshold && Time.time >= nextFireTime)
		{
			nextFireTime = Time.time + fireRate;

			Vector2 dir = lookInput.normalized;
			GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

			// 弾回転
			float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
			bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

			Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
			if (bulletRb != null)
			{
				bulletRb.isKinematic = false;
				bulletRb.gravityScale = 0;
				bulletRb.linearVelocity = dir * bulletSpeed;
			}

			Destroy(bullet, 3f);
		}
	}

	private void OnTriggerEnter2D(Collider2D collision)
	{
		if (collision.CompareTag("EnemyBullet"))
		{
			// 無敵状態によりダメージをカット
			if (isInvincible == true) return;
	
			currentHP -= 100;
			Debug.Log("プレイヤー体力 : " + currentHP);

			// 死亡判定
			if (currentHP <= 0)
			{
				isDead = true;
				Debug.Log("プレイヤー死亡通知 : " + name);

				CharacterAction_Dead();
			}
			else
			{
				// ダメージ後の無敵時間を設定
				recieveDamageCoolTimer = defaultRecieveDamageCoolTime;
				recieveDamageBlinkInterval = DEFAULT_RECIEVE_DAMAGE_BLINK_INTERVAL;

				//SetHitStop(0.1f, 0.1f, false);
			}
		}
	}

	[SerializeField, Header("プレイヤーのAnimator")]
	protected Animator animator;

	[SerializeField, Header("プレイヤーのTransform")]
	private Transform characterTransform;

	[SerializeField, Header("移動速度")]
	private float moveSpeed = 5f;

	[SerializeField, Header("ホバー力")]
	private float hoverForce = 1f;

	[SerializeField, Header("ジャンプ力")]
	private float jumpForce = 5;

	[SerializeField, Header("地面レイヤー")]
    private LayerMask groundLayer;

	[SerializeField, Header("壁レイヤー")]
	private LayerMask wallLayer;

	[SerializeField, Header("衝突判定コライダー")]
	private BoxCollider2D collisionCollider;

	[SerializeField, Header("地接地判定の距離")]
	private float groundCheckDistance = 0.1f;

	[SerializeField, Header("銃のTransform")]
	private Transform gunTransform;

	[SerializeField, Header("弾プレハブ")]
	private GameObject bulletPrefab;

	[SerializeField, Header("発射位置")]
	private Transform firePoint;

	[SerializeField, Header("弾のスピード")]
	private float bulletSpeed = 10f;

	[SerializeField, Header("ホバーエフェクト")]
	private GameObject hoverEffect;

	private bool isHoverEffectPlaying = false;

	[SerializeField, Header("連射間隔")]
	private float fireRate = 0.2f;

	[SerializeField, Header("右スティック発射のしきい値")]
	private float stickFireThreshold = 0.9f;

	[SerializeField, Header("スクリーンフェーダー")]
	private ScreenFader fader;

	private Vector3 initialPosition;

	private Rigidbody2D rb;
	private Vector2 moveInput;
	private Vector2 lookInput;
	private bool isGrounded;

	/// <summary>
	///	キャラクターの状態
	/// </summary>
	private bool isInvincible = false;

	/// <summary>
	///	状態管理タイマー
	/// </summary>
	private float defaultRecieveDamageCoolTime = 2.0f;
	private float recieveDamageCoolTimer = 0.0f;
	private float recieveDamageBlinkInterval = 0.1f;
	static private readonly float DEFAULT_RECIEVE_DAMAGE_BLINK_INTERVAL = 0.04f;

	protected bool isHoverInput = false; 
	protected bool isHover = false;
	protected bool isFall = false;
	private bool isJump = false;
	private bool jumpRequested = false;

	private float nextFireTime = 0f;

	private Vector2 lastLookInput = Vector2.right;

	public bool canControl = true;
	public bool playerInRange = false;

	#endregion
}
