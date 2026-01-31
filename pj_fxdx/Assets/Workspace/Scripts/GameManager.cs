using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;

/// <summary>
/// ゲームを制御するクラス
/// </summary>
public class GameManager : GameSingleton<GameManager>
{
	private void Start(){}

	private void Update(){}

	protected override void FirstAwake()
	{
		//フレームレート設定
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 60;

		if (CharacterManager.Instance == null)
		{
			Instantiate(m_characterManagerPrefab);
		}
	}

	[SerializeField, Tooltip("Character全般を管理するマネージャー")]
	private CharacterManager m_characterManagerPrefab;
}
