using System.Linq;
using UnityEditor;
using UnityEngine;
using BoothImportAssistant.Presenters;

namespace BoothImportAssistant.UI
{
    /// <summary>
    /// アセットリスト表示UI部品（スクロール、フィルタリング、ページネーション）
    /// 元ファイル: BoothLibraryWindow.cs の DrawAssetList()
    /// </summary>
    public class AssetListView
    {
        private readonly BoothLibraryPresenter presenter;
        private readonly AssetItemView assetItemView;
        private readonly PaginationView paginationView;
        
        private Vector2 scrollPosition;
        private int currentPage = 0;
        private const int itemsPerPage = 20;

        public AssetListView(BoothLibraryPresenter presenter)
        {
            this.presenter = presenter;
            this.assetItemView = new AssetItemView(presenter);
            this.paginationView = new PaginationView();
        }

        public void DrawAssetList(int selectedTab)
        {
            // 選択されたタブに応じてアセットをフィルタリング
            string filterSource = selectedTab == 0 ? "purchased" : "gift";
            var filteredAssets = presenter.Assets
                .Where(asset => 
                {
                    // sourceフィールドがない古いデータは購入として扱う
                    if (string.IsNullOrEmpty(asset.source))
                    {
                        return filterSource == "purchased";
                    }
                    return asset.source == filterSource;
                })
                .ToList();
            
            // ページネーション計算
            int totalAssets = filteredAssets.Count;
            int totalPages = Mathf.CeilToInt((float)totalAssets / itemsPerPage);
            
            // ページ範囲の補正
            if (currentPage >= totalPages && totalPages > 0)
            {
                currentPage = totalPages - 1;
            }
            if (currentPage < 0)
            {
                currentPage = 0;
            }
            
            // 現在のページのアセットを取得
            int startIndex = currentPage * itemsPerPage;
            int endIndex = Mathf.Min(startIndex + itemsPerPage, totalAssets);
            var currentPageAssets = filteredAssets.Skip(startIndex).Take(endIndex - startIndex).ToList();
            
            // ページネーションコントロール（上部）
            if (paginationView.DrawPaginationControls(totalAssets, totalPages, currentPage, itemsPerPage, out int newPage))
            {
                if (newPage != currentPage)
                {
                    currentPage = newPage;
                    scrollPosition = Vector2.zero;
                }
            }
            
            EditorGUILayout.Space(5);
            
            // 縦スクロールバーのみ表示（横スクロールバーは非表示）
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var asset in currentPageAssets)
            {
                assetItemView.DrawAssetItem(asset);
            }

            EditorGUILayout.EndScrollView();
        }

        public void ResetPage()
        {
            currentPage = 0;
            scrollPosition = Vector2.zero;
        }
    }
}

