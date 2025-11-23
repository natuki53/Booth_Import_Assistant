using UnityEditor;
using UnityEngine;

namespace BoothImportAssistant.UI
{
    /// <summary>
    /// タブUI部品（購入/ギフト切り替え）
    /// 元ファイル: BoothLibraryWindow.cs の DrawTabs()
    /// </summary>
    public class TabView
    {
        private int selectedTab = 0; // 0: 購入した商品, 1: ギフト
        private string[] tabNames = new string[] { "購入した商品", "ギフト" };

        public int SelectedTab => selectedTab;

        public int DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            
            int newSelectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(30));
            
            // タブが変更されたら記録
            if (newSelectedTab != selectedTab)
            {
                selectedTab = newSelectedTab;
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            
            return selectedTab;
        }
    }
}

