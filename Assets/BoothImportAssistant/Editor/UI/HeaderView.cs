using UnityEditor;
using UnityEngine;
using BoothImportAssistant.Presenters;

namespace BoothImportAssistant.UI
{
    /// <summary>
    /// ヘッダーUI部品（ボタン、ステータス、最終更新日時）
    /// 元ファイル: BoothLibraryWindow.cs の DrawHeader()
    /// </summary>
    public class HeaderView
    {
        private readonly BoothLibraryPresenter presenter;
        private readonly NotificationView notificationView;
        
        // Bridgeステータスのキャッシュ（OnGUIでの重いチェックを避けるため）
        private bool cachedIsBridgeRunning = false;
        private double lastBridgeStatusCheckTime = 0;
        private const double BRIDGE_STATUS_CHECK_INTERVAL = 10.0;

        public HeaderView(BoothLibraryPresenter presenter, NotificationView notificationView)
        {
            this.presenter = presenter;
            this.notificationView = notificationView;
            
            // 初期化時にBridgeステータスを即座にチェック
            cachedIsBridgeRunning = presenter.IsBridgeRunning();
            lastBridgeStatusCheckTime = EditorApplication.timeSinceStartup;
        }

        public void DrawHeader()
        {
            EditorGUILayout.Space(10);
            
            GUILayout.Label("BOOTH Library", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            // 同期ボタン
            if (GUILayout.Button("同期", GUILayout.Height(30), GUILayout.Width(100)))
            {
                bool bridgeStarted = presenter.SyncWithBooth();
                // 実際にbridgeが起動した場合のみキャッシュを更新
                if (bridgeStarted)
                {
                    cachedIsBridgeRunning = true;
                    lastBridgeStatusCheckTime = EditorApplication.timeSinceStartup;
                }
                else
                {
                    // 起動しなかった場合は実際の状態を確認
                    cachedIsBridgeRunning = presenter.IsBridgeRunning();
                    lastBridgeStatusCheckTime = EditorApplication.timeSinceStartup;
                }
            }
            
            // 再読み込みボタン
            if (GUILayout.Button("再読み込み", GUILayout.Height(30), GUILayout.Width(100)))
            {
                bool success = presenter.ReloadAssets();
                notificationView?.ShowReloadNotification(success);
            }
            
            // Bridge停止ボタン（キャッシュされた値を使用）
            UpdateBridgeStatusCache();
            GUI.enabled = cachedIsBridgeRunning;
            if (GUILayout.Button("Bridge停止", GUILayout.Height(30), GUILayout.Width(100)))
            {
                presenter.StopBridge();
                cachedIsBridgeRunning = false;
                lastBridgeStatusCheckTime = EditorApplication.timeSinceStartup;
            }
            GUI.enabled = true;
            
            GUILayout.FlexibleSpace();
            
            // Bridgeステータス表示
            DrawBridgeStatus();
            
            EditorGUILayout.EndHorizontal();
            
            // 最終更新日時の表示
            DrawLastUpdated();
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);
        }

        private void UpdateBridgeStatusCache()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            if ((currentTime - lastBridgeStatusCheckTime) >= BRIDGE_STATUS_CHECK_INTERVAL)
            {
                cachedIsBridgeRunning = presenter.IsBridgeRunning();
                lastBridgeStatusCheckTime = currentTime;
            }
        }

        private void DrawBridgeStatus()
        {
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.normal.textColor = cachedIsBridgeRunning ? Color.green : Color.gray;
            GUILayout.Label(cachedIsBridgeRunning ? "● Bridge起動中" : "○ Bridge停止中", statusStyle);
        }

        private void DrawLastUpdated()
        {
            if (presenter.LastUpdated.HasValue)
            {
                EditorGUILayout.Space(3);
                GUIStyle dateStyle = new GUIStyle(EditorStyles.miniLabel);
                dateStyle.normal.textColor = Color.gray;
                string dateText = $"最終更新: {presenter.LastUpdated.Value:yyyy/MM/dd HH:mm:ss}";
                EditorGUILayout.LabelField(dateText, dateStyle);
            }
        }
    }
}

