using UnityEditor;
using UnityEngine;

namespace BoothImportAssistant.UI
{
    /// <summary>
    /// 空状態表示UI部品
    /// 元ファイル: BoothLibraryWindow.cs の DrawEmptyState()
    /// </summary>
    public class EmptyStateView
    {
        public void Draw()
        {
            EditorGUILayout.Space(50);
            
            GUIStyle centeredStyle = new GUIStyle(GUI.skin.label);
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            centeredStyle.wordWrap = true;
            
            GUILayout.Label("まだBOOTHの同期が行われていません", centeredStyle);
            EditorGUILayout.Space(10);
            GUILayout.Label("上の「同期」ボタンを押して、BOOTH購入リストを取得してください", centeredStyle);
        }
    }
}

