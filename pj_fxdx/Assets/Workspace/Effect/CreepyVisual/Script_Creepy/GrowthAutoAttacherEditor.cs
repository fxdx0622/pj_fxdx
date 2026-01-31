#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrowthAutoAttacher))]
public class GrowthAutoAttacherEditor : Editor
{
    SerializedProperty affectOnlyExplicitList;
    SerializedProperty explicitTargets;

    void OnEnable()
    {
        affectOnlyExplicitList = serializedObject.FindProperty("affectOnlyExplicitList");
        explicitTargets        = serializedObject.FindProperty("explicitTargets");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ★ explicitTargets を除外して既定UIを描画
        DrawPropertiesExcluding(serializedObject, "explicitTargets");

        var comp = (GrowthAutoAttacher)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("明示ターゲット操作", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("モード", GUILayout.Width(60));
            EditorGUILayout.LabelField(comp.affectOnlyExplicitList
                ? "明示リストのみに適用"
                : "フィルタで検索して適用");
        }

        // ★ 明示ターゲットのリストはここで1回だけ描画
        EditorGUILayout.PropertyField(explicitTargets, includeChildren: true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("選択を追加"))        comp.AddSelectionToExplicitList();
            if (GUILayout.Button("フィルタ結果で置換"))  comp.ReplaceExplicitListFromFilter();
            if (GUILayout.Button("Null参照を削除"))     comp.CleanExplicitList();
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("実行", EditorStyles.boldLabel);
            if (GUILayout.Button("コンポーネントをアタッチ（実行）"))
                comp.AttachNow();
            if (GUILayout.Button("見た目を適用（明示リスト）"))
                comp.ApplyLookToExplicitList();
        }

        EditorGUILayout.HelpBox(
            "・「明示リストのみに適用」をONにすると、上のリストにある SpriteRenderer にだけ処理を行います。\n" +
            "・OFFの場合は、インスペクター上部のフィルタ設定でシーンを検索して適用します。\n" +
            "・上の3ボタンはエディタ補助用、下の「実行」ボタンで実際の処理を走らせます。",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
