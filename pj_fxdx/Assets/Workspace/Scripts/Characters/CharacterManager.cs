using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor.Overlays;
#endif

/// <summary>
/// キャラクターに関するマネージャークラス
/// </summary>
public class CharacterManager : GameSingleton<CharacterManager>
{
	/// <summary>
	/// 対象マップのプレイヤーとして登録する
	/// </summary>
	public void RegisterCurrentPlayer(PlayerBase registerCharacter)
	{
		if (m_currentPlayer != null)
		{
			// 上書きしようとした場合は警告する
			Debug.Assert(false, "CharacterManager : マップ中に複数のPlayerが存在する可能性があります。");
		}

		m_currentPlayer = registerCharacter;
	}

	public void UnregisterCurrentPlayer(PlayerBase targetCharacter)
	{
		if (m_currentPlayer == targetCharacter)
		{
			m_currentPlayer = null;
		}
	}

	/// <summary>
	/// 現在操作中のプレイヤーキャラクターを取得する
	/// </summary>
	public PlayerBase GetCurrentPlayer()
	{
		return m_currentPlayer;
	}

	private PlayerBase m_currentPlayer
	{
		get; set;
	}

	protected override void FirstAwake() { }

	void Start() { }

	void Update() { }
}
