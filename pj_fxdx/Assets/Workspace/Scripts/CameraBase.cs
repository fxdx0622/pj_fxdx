using UnityEngine;

public class CameraBase : MonoBehaviour
{
	[SerializeField, Header("プレイヤー")]
	private Transform target;

	[SerializeField, Header("カメラ位置オフセット")]
	private Vector3 offset = new Vector3(0, 0, -10f);

	private void LateUpdate()
	{
		if (target == null) return;

		transform.position = target.position + offset;
	}
}
