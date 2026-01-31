using UnityEngine;

/// <summary>
/// 地面の種類を表す列挙型や、地面の特性を持つフィールドをまとめたクラス
/// </summary>
public class GroundType : MonoBehaviour
{
	public EGroundMaterialType GetGroundMaterialType()
	{
		return groundMaterialType;
	}
	public void SetGroundMaterialType(EGroundMaterialType type)
	{
		groundMaterialType = type;
	}
	public EGroundState GetGroundState()
	{
		return groundState;
	}
	public void SetGroundState(EGroundState state)
	{
		groundState = state;
	}
	public EGroundEffect GetGroundEffect()
	{
		return groundEffect;
	}
	public void SetGroundEffect(EGroundEffect effect)
	{
		groundEffect = effect;
	}
	public EGroundType GetGroundType()
	{
		return groundType;
	}
	public void SetGroundType(EGroundType type)
	{
		groundType = type;
	}

	public enum EGroundMaterialType
	{
		Fire,
		Ice,
		Thunder,
		Wind,
		Dark,
		Water,
		Grass,
	}

	public enum EGroundState
	{
		Normal,
		Slippery,
		Sticky,
	}

	public enum EGroundEffect
	{
		None,
		Slow,
		Boost,
		Damage,
		Heal,
	}
	public enum EGroundType
	{
		Ground,
		Wall,
		Celling,
		PassThroughPlatform,
	}

	#region SECRET

	[SerializeField, Header("地面の属性")]
	private EGroundMaterialType groundMaterialType;
	[SerializeField, Header("地面の状態")]
	private EGroundState groundState;
	[SerializeField, Header("地面の効果")]
	private EGroundEffect groundEffect;
	[SerializeField, Header("地面の種類")]
	private EGroundType groundType;

	#endregion
}
