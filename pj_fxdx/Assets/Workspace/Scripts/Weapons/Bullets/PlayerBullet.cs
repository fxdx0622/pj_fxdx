using UnityEngine;

public class PlayerBullet : BulletBase
{
	public override void Start()
	{
		Destroy(gameObject, lifeTime);
	}

	public override void OnTriggerEnter2D(Collider2D collision)
	{
		if (collision.CompareTag("Enemy") || collision.CompareTag("Cube"))
		{
			Destroy(gameObject);
		}
	}

	[SerializeField] private float lifeTime = 3f;
}
