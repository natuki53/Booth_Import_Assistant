using System;
using UnityEditor;
using UnityEngine;
using BoothImportAssistant.Presenters;
using BoothImportAssistant.UI;
using BoothImportAssistant.Utils;

namespace BoothImportAssistant
{
    /// <summary>
    /// BOOTH Library表示ウィンドウ（メイン）
    /// 元ファイル: BoothLibraryWindow.cs をUI部品ごとに分割してリファクタリング
    /// </summary>
    public class BoothLibraryWindow : EditorWindow
    {
        private BoothLibraryPresenter presenter;
        
        // UI部品
        private HeaderView headerView;
        private TabView tabView;
        private NotificationView notificationView;
        private AssetListView assetListView;
        private EmptyStateView emptyStateView;
        
        // 更新制御
        private double lastRepaintTime = 0;

        [MenuItem("Tools/BOOTH Library")]
        public static void ShowWindow()
        {
            var window = GetWindow<BoothLibraryWindow>("BOOTH Import Assistant");
            window.minSize = new Vector2(500, 300);
            window.Show();
        }

        private void OnEnable()
        {
            // プロジェクトパス取得
            string projectPath = ProjectPathUtility.GetProjectPath();
            if (string.IsNullOrEmpty(projectPath))
            {
                Debug.LogWarning("[BoothBridge] プロジェクトパスを取得できません");
                return;
            }

            // Presenterを初期化
            presenter = new BoothLibraryPresenter(projectPath);
            presenter.OnDataChanged += Repaint;

            // UI部品を初期化
            notificationView = new NotificationView();
            headerView = new HeaderView(presenter, notificationView);
            tabView = new TabView();
            assetListView = new AssetListView(presenter);
            emptyStateView = new EmptyStateView();

            // 通知イベント接続
            presenter.OnShowUpdateNotification += () => notificationView?.ShowUpdateNotification();

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
            double currentTime = EditorApplication.timeSinceStartup;
            
            // Presenterの更新処理
            if (presenter != null)
            {
                presenter.Update();
            }

            // Bridgeステータスをリアルタイムで更新（1秒ごと）
            if (currentTime - lastRepaintTime > 1.0)
            {
                lastRepaintTime = currentTime;
                Repaint();
            }
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
            headerView?.DrawHeader();
            
            // 通知
            notificationView?.DrawNotifications();
            
            // タブUI
            int newSelectedTab = tabView?.DrawTabs() ?? 0;
            
            // 進捗バー表示
            DrawProgressBar();

            // アセットリストまたは空状態
            if (presenter.Assets.Count == 0)
            {
                emptyStateView?.Draw();
            }
            else
            {
                assetListView?.DrawAssetList(tabView.SelectedTab);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProgressBar()
        {
            var currentProgress = presenter.CurrentProgress;
            if (currentProgress != null && currentProgress.active)
            {
                EditorGUILayout.Space(5);
                Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(progressRect, currentProgress.progress / 100f, currentProgress.message);
                EditorGUILayout.Space(5);
            }
        }
    }
}

