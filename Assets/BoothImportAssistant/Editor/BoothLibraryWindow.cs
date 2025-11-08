using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BoothImportAssistant.Models;
using BoothImportAssistant.Presenters;

namespace BoothImportAssistant
{
    /// <summary>
    /// BOOTH Library表示ウィンドウ（UI描画のみ）
    /// </summary>
    public class BoothLibraryWindow : EditorWindow
    {
        private BoothLibraryPresenter presenter;
        private Vector2 scrollPosition;
        private Texture2D placeholderIcon;
        
        private bool showUpdateNotification = false;
        private double notificationEndTime = 0;
        private double lastRepaintTime = 0;

        [MenuItem("Tools/BOOTH Library")]
        public static void ShowWindow()
        {
            var window = GetWindow<BoothLibraryWindow>("BOOTH Library");
            window.minSize = new Vector2(500, 300);
            window.Show();
        }

        private void OnEnable()
        {
            // プロジェクトパス取得
            string projectPath = GetProjectPath();
            if (string.IsNullOrEmpty(projectPath))
            {
                Debug.LogWarning("[BoothBridge] プロジェクトパスを取得できません");
                return;
            }

            // プレースホルダーアイコン
            placeholderIcon = EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;

            // Presenterを初期化
            presenter = new BoothLibraryPresenter(projectPath);
            presenter.OnDataChanged += Repaint;
            presenter.OnShowUpdateNotification += ShowUpdateNotificationUI;

            // エディタ更新ハンドラーを追加
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            // エディタ更新ハンドラーを削除
            EditorApplication.update -= OnEditorUpdate;
            
            // Presenterを破棄
            presenter?.Dispose();
        }

        private void OnEditorUpdate()
        {
            // Presenterの更新処理
            presenter?.Update();

            // Bridgeステータスをリアルタイムで更新（1秒ごと）
            if (EditorApplication.timeSinceStartup - lastRepaintTime > 1.0)
            {
                lastRepaintTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void ShowUpdateNotificationUI()
        {
            showUpdateNotification = true;
            notificationEndTime = EditorApplication.timeSinceStartup + 2.0;
        }

        private void OnGUI()
        {
            if (presenter == null)
            {
                EditorGUILayout.HelpBox("Presenterが初期化されていません", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginVertical();

            // ヘッダー
            DrawHeader();

            // 更新通知
            if (showUpdateNotification && EditorApplication.timeSinceStartup < notificationEndTime)
            {
                EditorGUILayout.HelpBox("✅ BOOTHデータが更新されました！", MessageType.Info);
            }
            else if (showUpdateNotification)
            {
                showUpdateNotification = false;
            }

            // 進捗バー表示
            var currentProgress = presenter.CurrentProgress;
            if (currentProgress != null && currentProgress.active)
            {
                EditorGUILayout.Space(5);
                Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(progressRect, currentProgress.progress / 100f, currentProgress.message);
                EditorGUILayout.Space(5);
            }

            // アセットリスト
            if (presenter.Assets.Count == 0)
            {
                DrawEmptyState();
            }
            else
            {
                DrawAssetList();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            
            GUILayout.Label("BOOTH Import Assistant", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            // 同期ボタン
            if (GUILayout.Button("同期", GUILayout.Height(30), GUILayout.Width(100)))
            {
                presenter.SyncWithBooth();
            }
            
            // 再読み込みボタン
            if (GUILayout.Button("再読み込み", GUILayout.Height(30), GUILayout.Width(100)))
            {
                presenter.ReloadAssets();
                Repaint();
            }
            
            // Bridge停止ボタン
            bool isBridgeRunning = presenter.IsBridgeRunning();
            GUI.enabled = isBridgeRunning; // Bridgeが起動中のみ有効
            if (GUILayout.Button("Bridge停止", GUILayout.Height(30), GUILayout.Width(100)))
            {
                presenter.StopBridge();
                Repaint();
            }
            GUI.enabled = true; // GUI.enabledをリセット
            
            GUILayout.FlexibleSpace();
            
            // Bridgeステータス
            bool isRunning = presenter.IsBridgeRunning();
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.normal.textColor = isRunning ? Color.green : Color.gray;
            GUILayout.Label(isRunning ? "● Bridge起動中" : "○ Bridge停止中", statusStyle);
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.Space(50);
            
            GUIStyle centeredStyle = new GUIStyle(GUI.skin.label);
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            centeredStyle.wordWrap = true;
            
            GUILayout.Label("まだBOOTHの同期が行われていません", centeredStyle);
            EditorGUILayout.Space(10);
            GUILayout.Label("上の「同期」ボタンを押して、BOOTH購入リストを取得してください", centeredStyle);
        }

        private void DrawAssetList()
        {
            // 縦スクロールバーのみ表示（横スクロールバーは非表示）
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var asset in presenter.Assets)
            {
                DrawAssetItem(asset);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetItem(BoothAsset asset)
        {
            // 外側のボックス全体を幅いっぱいに
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginHorizontal();
            
            // ===== 左：サムネイル（固定幅） =====
            Texture2D thumbnail = presenter.GetThumbnail(asset);
            if (thumbnail != null)
            {
                GUILayout.Label(thumbnail, GUILayout.Width(120), GUILayout.Height(120));
            }
            else
            {
                GUILayout.Label(placeholderIcon, GUILayout.Width(120), GUILayout.Height(120));
            }
            
            GUILayout.Space(10);
            
            // ===== 中央：情報（余った幅を使用、長いテキストは改行） =====
            EditorGUILayout.BeginVertical();
            
            // タイトル（改行対応）
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.wordWrap = true;
            GUILayout.Label(asset.title, titleStyle);
            
            // 作者（改行対応）
            GUIStyle authorStyle = new GUIStyle(EditorStyles.miniLabel);
            authorStyle.wordWrap = true;
            GUILayout.Label("作者: " + asset.author, authorStyle);
            
            // 購入日
            GUILayout.Label("購入日: " + asset.purchaseDate, EditorStyles.miniLabel);
            
            if (asset.installed)
            {
                GUILayout.Label("✅ インポート済み", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // ===== 右：ボタン（固定幅） =====
            DrawDownloadButtons(asset);
            
            EditorGUILayout.EndHorizontal(); // 横並び終了
            EditorGUILayout.EndVertical(); // ボックス終了
            
            EditorGUILayout.Space(5);
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
                if (avatarIndices.Count > 0)
                {
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
                        // ドロップダウン用のラベル配列を作成
                        string[] options = new string[avatarIndices.Count];
                        for (int i = 0; i < avatarIndices.Count; i++)
                        {
                            string label = asset.downloadUrls[avatarIndices[i]].label;
                            if (label.Length > 25)
                            {
                                label = label.Substring(0, 22) + "...";
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
                
                // マテリアルのダウンロード
                if (materialIndices.Count > 0)
                {
                    int materialCount = 1;
                    foreach (int index in materialIndices)
                    {
                        // マテリアルボタン（統一ラベル）
                        string buttonLabel = materialIndices.Count > 1 ? $"マテリアル インポート {materialCount}" : "マテリアルインポート";
                        if (GUILayout.Button(buttonLabel, GUILayout.Height(24)))
                        {
                            presenter.DownloadAsset(asset, index);
                        }
                        materialCount++;
                    }
                }
            }
            else
            {
                // ダウンロードリンクがない場合
                if (GUILayout.Button("商品ページ", GUILayout.Height(26)))
                {
                    Application.OpenURL(asset.productUrl);
                }
            }

            EditorGUILayout.EndVertical(); // ボタンエリア終了
        }

        private string GetProjectPath()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
            {
                return null;
            }
            
            return Directory.GetParent(dataPath).FullName;
        }
    }
}
