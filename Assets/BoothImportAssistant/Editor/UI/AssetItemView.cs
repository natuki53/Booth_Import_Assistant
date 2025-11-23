using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BoothImportAssistant.Models;
using BoothImportAssistant.Presenters;

namespace BoothImportAssistant.UI
{
    /// <summary>
    /// 個別アセットアイテム表示UI部品
    /// 元ファイル: BoothLibraryWindow.cs の DrawAssetItem(), DrawDownloadButtons(), DrawClickableTitle()
    /// </summary>
    public class AssetItemView
    {
        private readonly BoothLibraryPresenter presenter;
        private Texture2D placeholderIcon;

        public AssetItemView(BoothLibraryPresenter presenter)
        {
            this.presenter = presenter;
            placeholderIcon = EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;
        }

        public void DrawAssetItem(BoothAsset asset)
        {
            // 外側のボックス全体を幅いっぱいに
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginHorizontal();
            
            // ===== 左：サムネイル（固定幅） =====
            DrawThumbnail(asset);
            
            GUILayout.Space(10);
            
            // ===== 中央：情報（余った幅を使用、長いテキストは改行） =====
            DrawAssetInfo(asset);
            
            GUILayout.Space(10);
            
            // ===== 右：ボタン（固定幅） =====
            DrawDownloadButtons(asset);
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
        }

        private void DrawThumbnail(BoothAsset asset)
        {
            Texture2D thumbnail = presenter.GetThumbnail(asset);
            if (thumbnail != null)
            {
                GUILayout.Label(thumbnail, GUILayout.Width(120), GUILayout.Height(120));
            }
            else
            {
                GUILayout.Label(placeholderIcon, GUILayout.Width(120), GUILayout.Height(120));
            }
        }

        private void DrawAssetInfo(BoothAsset asset)
        {
            EditorGUILayout.BeginVertical();
            
            // タイトル（クリック可能、ホバー時に青く表示）
            DrawClickableTitle(asset);
            
            // 作者（改行対応）
            GUIStyle authorStyle = new GUIStyle(EditorStyles.miniLabel);
            authorStyle.wordWrap = true;
            GUILayout.Label("作者: " + asset.author, authorStyle);
            
            if (asset.installed)
            {
                GUILayout.Label("✅ インポート済み", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawClickableTitle(BoothAsset asset)
        {
            // タイトルスタイルを作成
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.wordWrap = true;
            
            // タイトルの高さを計算
            GUIContent titleContent = new GUIContent(asset.title);
            float availableWidth = EditorGUIUtility.currentViewWidth - 370;
            float titleHeight = titleStyle.CalcHeight(titleContent, availableWidth);
            
            // 最小高さを確保
            titleHeight = Mathf.Max(titleHeight, EditorGUIUtility.singleLineHeight);
            
            // タイトル領域を取得
            Rect titleRect = EditorGUILayout.GetControlRect(false, titleHeight);
            
            // イベント処理
            Event currentEvent = Event.current;
            
            // マウスホバー判定
            bool isHovered = titleRect.Contains(currentEvent.mousePosition);
            
            // Repaint時にホバースタイルを適用
            if (currentEvent.type == EventType.Repaint)
            {
                if (isHovered)
                {
                    // ホバー時は青色で描画
                    GUIStyle hoveredStyle = new GUIStyle(titleStyle);
                    hoveredStyle.normal.textColor = new Color(0.3f, 0.5f, 0.9f);
                    hoveredStyle.Draw(titleRect, titleContent, false, false, false, false);
                }
                else
                {
                    // 通常時は通常色で描画
                    titleStyle.Draw(titleRect, titleContent, false, false, false, false);
                }
            }
            
            // カーソル変更
            if (isHovered)
            {
                EditorGUIUtility.AddCursorRect(titleRect, MouseCursor.Link);
            }
            
            // クリック処理
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && isHovered)
            {
                Application.OpenURL(asset.productUrl);
                currentEvent.Use();
            }
        }

        private void DrawDownloadButtons(BoothAsset asset)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            
            // ダウンロードボタン領域
            if (asset.downloadUrls != null && asset.downloadUrls.Length > 0)
            {
                // アバター別とマテリアルを分類
                List<int> avatarIndices = new List<int>();
                List<int> materialIndices = new List<int>();
                
                for (int i = 0; i < asset.downloadUrls.Length; i++)
                {
                    if (asset.downloadUrls[i].isMaterial)
                    {
                        materialIndices.Add(i);
                    }
                    else
                    {
                        avatarIndices.Add(i);
                    }
                }
                
                // アバター別のダウンロード
                DrawAvatarDownloadButtons(asset, avatarIndices);
                
                // マテリアルのダウンロード
                DrawMaterialDownloadButtons(asset, materialIndices);
            }
            else
            {
                // ダウンロードリンクがない場合
                if (GUILayout.Button("商品ページ", GUILayout.Height(26)))
                {
                    Application.OpenURL(asset.productUrl);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAvatarDownloadButtons(BoothAsset asset, List<int> avatarIndices)
        {
            if (avatarIndices.Count == 0) return;

            if (avatarIndices.Count == 1)
            {
                // 単一アバター
                if (GUILayout.Button("ダウンロード & インポート", GUILayout.Height(26)))
                {
                    presenter.DownloadAsset(asset, avatarIndices[0]);
                }
            }
            else
            {
                // 複数アバター：プルダウンメニュー
                string[] options = new string[avatarIndices.Count];
                for (int i = 0; i < avatarIndices.Count; i++)
                {
                    string label = asset.downloadUrls[avatarIndices[i]].label;
                    if (label.Length > 35)
                    {
                        label = label.Substring(0, 32) + "...";
                    }
                    options[i] = label;
                }
                
                // ドロップダウンで選択
                int selectedIndex = presenter.GetSelectedDownloadIndex(asset.id);
                selectedIndex = EditorGUILayout.Popup(
                    selectedIndex, 
                    options,
                    GUILayout.Width(180)
                );
                
                // 範囲チェック
                if (selectedIndex >= 0 && selectedIndex < avatarIndices.Count)
                {
                    presenter.SetSelectedDownloadIndex(asset.id, selectedIndex);
                }
                else
                {
                    presenter.SetSelectedDownloadIndex(asset.id, 0);
                }
                
                // 選択したアバターをダウンロード
                if (GUILayout.Button("ダウンロード & インポート", GUILayout.Height(24)))
                {
                    int actualIndex = avatarIndices[presenter.GetSelectedDownloadIndex(asset.id)];
                    presenter.DownloadAsset(asset, actualIndex);
                }
            }
        }

        private void DrawMaterialDownloadButtons(BoothAsset asset, List<int> materialIndices)
        {
            if (materialIndices.Count == 0) return;

            int materialCount = 1;
            foreach (int index in materialIndices)
            {
                // マテリアルボタン（統一ラベル）
                string buttonLabel = materialIndices.Count > 1 ? $"マテリアル インポート {materialCount}" : "マテリアル インポート";
                if (GUILayout.Button(buttonLabel, GUILayout.Height(24)))
                {
                    presenter.DownloadAsset(asset, index);
                }
                materialCount++;
            }
        }
    }
}

