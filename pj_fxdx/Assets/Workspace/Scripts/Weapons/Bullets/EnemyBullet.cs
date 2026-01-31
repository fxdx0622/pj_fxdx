using Unity.VisualScripting;
using UnityEngine;

public class EnemyBullet : BulletBase
{
	public override void Start() { }

	public override void OnTriggerEnter2D(Collider2D collision)
	{
		if (collision.CompareTag("Player") || collision.CompareTag("Cube"))
		{
			Destroy(gameObject);
		}
	}
}
