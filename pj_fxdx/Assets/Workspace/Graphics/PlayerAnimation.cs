using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
	public float moveSpeed = 3f;

	Animator animator;
	float inputX;

	void Awake()
	{
		animator = GetComponent<Animator>();
	}

	void Update()
	{
		// 左右入力のみ
		inputX = Input.GetAxisRaw("Horizontal");

		// 移動
		if (inputX != 0)
		{
			transform.position += Vector3.right * inputX * moveSpeed * Time.deltaTime;
		}

		// アニメ切り替え
		animator.SetBool("IsMoving", inputX != 0);

		// 向き反転
		if (inputX != 0)
		{
			Vector3 scale = transform.localScale;
			scale.x = Mathf.Sign(inputX) * Mathf.Abs(scale.x);
			transform.localScale = scale;
		}
	}
}