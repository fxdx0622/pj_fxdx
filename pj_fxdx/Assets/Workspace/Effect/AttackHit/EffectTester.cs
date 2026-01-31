using UnityEngine;

public class EffectTester : MonoBehaviour
{
	[SerializeField] private GameObject effectPrefab;

	void Update()
	{
		// ゲームパッドX
		if (Input.GetKeyDown(KeyCode.JoystickButton2))
		{
			Vector3 spawnPos = Camera.main.transform.position + Camera.main.transform.forward * 2f;
			GameObject effect = Instantiate(effectPrefab, spawnPos, Quaternion.identity);

			Destroy(effect, 1f);
		}
	}
}
