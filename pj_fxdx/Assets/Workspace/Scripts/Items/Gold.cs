using UnityEngine;

/// <summary>
/// ゴールド
/// </summary>
public class Gold : ItemBase
{
	public void SetGoldAmount(int amount)
	{
		goldAmount = amount;
	}

	protected override void OnCollected(PlayerBase player)
	{
		base.OnCollected(player);
		if (player == null) return;

		Debug.Log($"ゴールド +{goldAmount}");
	}

	[SerializeField, Header("ゴールド量")]
	private int goldAmount = 10;
}
