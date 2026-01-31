using UnityEngine;

/// <summary>
/// 常駐型シングルトン基底クラス
/// </summary>
[DefaultExecutionOrder(-5)]
public class GameSingleton<T> : MonoBehaviour where T : Object
{
	public static T Instance { get; protected set; }

	protected virtual void Awake()
	{
		if (Instance == null)
		{
			Instance = this as T;   // GameSingleton<T>を継承したTクラスへダウンキャスト

			// シーンを跨いでも破棄されないようにする
			DontDestroyOnLoad(this.gameObject);

			// 1回目のみFirstAwakeを呼び出す
			FirstAwake();
		}
		else
		{
			// 二重で起動されないようにする
			Destroy(this.gameObject);
		}
	}

	/// <summary>
	/// 初回のみ呼ばれるAwake
	/// </summary>
	protected virtual void FirstAwake(){}

	public static void DestroyInstance()
	{
		if (Instance != null)
		{
			MonoBehaviour b = Instance as MonoBehaviour;
			GameObject.Destroy(b.gameObject);
			Instance = null;
		}
	}
}
