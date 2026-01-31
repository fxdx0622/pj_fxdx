using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 壁の種類をまとめたクラス
/// </summary>
public class WallType : MonoBehaviour
{
	public bool IsHitEnemyType(EEnemyType type)
	{
		return m_enemyType.Contains(type);
	}
	public EWallType GetWallType()
	{
		return m_wallType;
	}

	public enum EWallType
	{
		Wall,
		PlayerOnly,
		EnemyOnly,
	}

	public enum EEnemyType
	{
		None = -1,
		Enemy_000 = 0,
		Enemy_001 = 1,
		Boss_000 = 100,
	}

	#region SECRET

	[SerializeField, Header("壁のタイプ")]
	private EWallType m_wallType = EWallType.Wall;

	[SerializeField, Header("判定に当たる敵種別")]
	private List<EEnemyType> m_enemyType = new List<EEnemyType>();

    #endregion
}