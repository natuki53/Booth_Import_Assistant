using System.Collections.Generic;
using System.Linq;
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

            EditorGUILayout.Space(4);
            Rect dividerRect = EditorGUILayout.GetControlRect(false, 1);
            GUI.Box(dividerRect, GUIContent.none, GUI.skin.horizontalSlider);
            EditorGUILayout.Space(4);

            DrawDownloadStatus(asset);
            
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
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            
            if (asset.downloadUrls != null && asset.downloadUrls.Length > 0)
            {
                List<int> avatarIndices = GetAvatarIndices(asset);
                List<int> materialIndices = GetMaterialIndices(asset);

                if (avatarIndices.Count > 0)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label("パッケージ", EditorStyles.boldLabel);
                    EditorGUILayout.Space(2);
                    DrawAvatarDownloadButtons(asset, avatarIndices);
                    EditorGUILayout.EndVertical();
                }

                if (materialIndices.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label("マテリアル", EditorStyles.boldLabel);
                    EditorGUILayout.Space(2);
                    DrawMaterialDownloadButtons(asset, materialIndices);
                    EditorGUILayout.EndVertical();
                }
            }
            else
            {
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

            int popupIndex = Mathf.Clamp(presenter.GetSelectedDownloadIndex(asset.id), 0, Mathf.Max(avatarIndices.Count - 1, 0));
            int actualIndex = avatarIndices[popupIndex];

            if (avatarIndices.Count > 1)
            {
                string[] options = avatarIndices
                    .Select(i =>
                    {
                        string label = asset.downloadUrls[i].label ?? $"オプション {i + 1}";
                        return label.Length > 35 ? label.Substring(0, 32) + "..." : label;
                    })
                    .ToArray();

                int newIndex = EditorGUILayout.Popup(popupIndex, options, GUILayout.Width(210));
                presenter.SetSelectedDownloadIndex(asset.id, Mathf.Clamp(newIndex, 0, avatarIndices.Count - 1));
                actualIndex = avatarIndices[presenter.GetSelectedDownloadIndex(asset.id)];
            }

            bool hasSavedFile = presenter.HasSavedFile(asset, actualIndex);
            DownloadUrl downloadInfo = (asset.downloadUrls != null && actualIndex >= 0 && actualIndex < asset.downloadUrls.Length)
                ? asset.downloadUrls[actualIndex]
                : null;
            bool canOverwrite = downloadInfo != null && !string.IsNullOrEmpty(downloadInfo.url);

            EditorGUILayout.BeginHorizontal();
            if (!hasSavedFile)
            {
                if (GUILayout.Button("ダウンロードのみ", GUILayout.Height(24)))
                {
                    presenter.DownloadOnly(asset, actualIndex);
                }
                if (GUILayout.Button("DL & インポート", GUILayout.Height(24)))
                {
                    presenter.DownloadAsset(asset, actualIndex);
                }
            }
            else
            {
                if (GUILayout.Button("インポート", GUILayout.Height(24)))
                {
                    presenter.ImportFromSavedFile(asset, actualIndex);
                }
            }

            DrawMoreOptionsMenu(asset, actualIndex, canOverwrite);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMaterialDownloadButtons(BoothAsset asset, List<int> materialIndices)
        {
            if (materialIndices.Count == 0) return;

            int materialCount = 1;
            foreach (int index in materialIndices)
            {
                bool hasSavedFile = presenter.HasSavedFile(asset, index);
                DownloadUrl downloadInfo = (asset.downloadUrls != null && index >= 0 && index < asset.downloadUrls.Length)
                    ? asset.downloadUrls[index]
                    : null;
                bool canOverwrite = downloadInfo != null && !string.IsNullOrEmpty(downloadInfo.url);

                EditorGUILayout.BeginHorizontal();
                if (!hasSavedFile)
                {
                    if (GUILayout.Button("ダウンロードのみ", GUILayout.Height(24)))
                    {
                        presenter.DownloadOnly(asset, index);
                    }
                    if (GUILayout.Button("DL & インポート", GUILayout.Height(24)))
                    {
                        presenter.DownloadAsset(asset, index);
                    }
                }
                else
                {
                    if (GUILayout.Button("インポート", GUILayout.Height(24)))
                    {
                        presenter.ImportFromSavedFile(asset, index);
                    }
                }

                DrawMoreOptionsMenu(asset, index, canOverwrite);
                EditorGUILayout.EndHorizontal();

                if (materialCount < materialIndices.Count)
                {
                    EditorGUILayout.Space(4);
                }

                materialCount++;
            }
        }

        private void DrawMoreOptionsMenu(BoothAsset asset, int downloadIndex, bool canOverwrite)
        {
            Rect buttonRect = GUILayoutUtility.GetRect(new GUIContent("・・・"), GUI.skin.button, GUILayout.Width(32), GUILayout.Height(24));
            if (GUI.Button(buttonRect, "・・・"))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("保存先ディレクトリを開く"), false, () => presenter.OpenDownloadFolder(asset));
                if (canOverwrite)
                {
                    menu.AddItem(new GUIContent("上書きダウンロード"), false, () => presenter.OverwriteDownload(asset, downloadIndex));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("上書きダウンロード"));
                }
                menu.DropDown(buttonRect);
            }
        }

        private void DrawDownloadStatus(BoothAsset asset)
        {
            var status = presenter.GetDownloadStatus(asset);
            string statusText;

            if (status.total <= 0)
            {
                statusText = "ダウンロード状態：未ダウンロード";
            }
            else if (status.total == 1 && status.downloaded == 1)
            {
                statusText = "ダウンロード状態：ダウンロード済み";
            }
            else if (status.downloaded <= 0)
            {
                statusText = "ダウンロード状態：未ダウンロード";
            }
            else if (status.downloaded >= status.total)
            {
                statusText = $"ダウンロード状態：ダウンロード済み({status.downloaded}/{status.total})";
            }
            else
            {
                statusText = $"ダウンロード状態：ダウンロード済み({status.downloaded}/{status.total})";
            }

            GUILayout.Label(statusText, EditorStyles.miniLabel);

            List<int> avatarIndices = GetAvatarIndices(asset);
            if (avatarIndices.Count > 1)
            {
                int popupIndex = Mathf.Clamp(presenter.GetSelectedDownloadIndex(asset.id), 0, avatarIndices.Count - 1);
                int actualIndex = avatarIndices[popupIndex];
                bool isDownloaded = presenter.IsSelectedFileDownloaded(asset, actualIndex);
                string selectedText = $"選択中のファイル：{(isDownloaded ? "ダウンロード済み" : "未ダウンロード")}";
                GUILayout.Label(selectedText, EditorStyles.miniLabel);
            }

            var materialStatus = presenter.GetMaterialDownloadStatus(asset);
            if (materialStatus.Count > 0)
            {
                if (materialStatus.Count == 1)
                {
                    string materialText = $"マテリアル：{(materialStatus[0] ? "ダウンロード済み" : "未ダウンロード")}";
                    GUILayout.Label(materialText, EditorStyles.miniLabel);
                }
                else
                {
                    List<string> parts = new List<string>();
                    for (int i = 0; i < materialStatus.Count; i++)
                    {
                        string label = materialStatus[i] ? "ダウンロード済み" : "未ダウンロード";
                        parts.Add($"{label}({i + 1})");
                    }
                    GUILayout.Label("マテリアル：" + string.Join(" ", parts), EditorStyles.miniLabel);
                }
            }
        }

        private static List<int> GetAvatarIndices(BoothAsset asset)
        {
            List<int> indices = new List<int>();
            if (asset?.downloadUrls == null) return indices;

            for (int i = 0; i < asset.downloadUrls.Length; i++)
            {
                if (!asset.downloadUrls[i].isMaterial)
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        private static List<int> GetMaterialIndices(BoothAsset asset)
        {
            List<int> indices = new List<int>();
            if (asset?.downloadUrls == null) return indices;

            for (int i = 0; i < asset.downloadUrls.Length; i++)
            {
                if (asset.downloadUrls[i].isMaterial)
                {
                    indices.Add(i);
                }
            }

            return indices;
        }
    }
}

