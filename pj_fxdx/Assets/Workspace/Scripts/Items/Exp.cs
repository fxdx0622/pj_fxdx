using UnityEngine;

/// <summary>
/// 経験値
/// </summary>
public class Exp : ItemBase
{
	public void SetExpAmount(int amount)
	{
		expAmount = amount;
	}

	protected override void OnCollected(PlayerBase player)
	{
		base.OnCollected(player);
		if (player == null) return;

		Debug.Log($"経験値 +{expAmount}");
	}

	[SerializeField, Header("経験値量")]
	private int expAmount = 10;
}
