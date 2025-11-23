using UnityEditor;
using UnityEngine;

namespace BoothImportAssistant.UI
{
    /// <summary>
    /// ページネーションコントロールUI部品
    /// 元ファイル: BoothLibraryWindow.cs の DrawPaginationControls()
    /// </summary>
    public class PaginationView
    {
        public bool DrawPaginationControls(int totalAssets, int totalPages, int currentPage, int itemsPerPage, out int newPage)
        {
            newPage = currentPage;
            
            if (totalPages <= 1)
            {
                return false; // ページが1つ以下の場合は表示しない
            }
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // 前のページボタン
            GUI.enabled = currentPage > 0;
            if (GUILayout.Button("◀ 前へ", GUILayout.Width(80), GUILayout.Height(25)))
            {
                newPage = currentPage - 1;
            }
            GUI.enabled = true;
            
            GUILayout.Space(10);
            
            // ページ情報表示
            int startItem = currentPage * itemsPerPage + 1;
            int endItem = Mathf.Min((currentPage + 1) * itemsPerPage, totalAssets);
            GUIStyle pageInfoStyle = new GUIStyle(GUI.skin.label);
            pageInfoStyle.alignment = TextAnchor.MiddleCenter;
            pageInfoStyle.fontStyle = FontStyle.Bold;
            GUILayout.Label($"ページ {currentPage + 1} / {totalPages}  ({startItem}-{endItem} / {totalAssets}件)", pageInfoStyle, GUILayout.Width(200));
            
            GUILayout.Space(10);
            
            // 次のページボタン
            GUI.enabled = currentPage < totalPages - 1;
            if (GUILayout.Button("次へ ▶", GUILayout.Width(80), GUILayout.Height(25)))
            {
                newPage = currentPage + 1;
            }
            GUI.enabled = true;
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            return newPage != currentPage;
        }
    }
}

