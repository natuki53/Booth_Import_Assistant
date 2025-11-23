using UnityEditor;
using UnityEngine;

namespace BoothImportAssistant.UI
{
    /// <summary>
    /// 通知メッセージUI部品
    /// 元ファイル: BoothLibraryWindow.cs の通知表示部分
    /// </summary>
    public class NotificationView
    {
        private bool showUpdateNotification = false;
        private double notificationEndTime = 0;
        
        private bool showReloadNotification = false;
        private string reloadNotificationMessage = "";
        private MessageType reloadNotificationMessageType = MessageType.Info;
        private double reloadNotificationEndTime = 0;

        public void ShowUpdateNotification()
        {
            showUpdateNotification = true;
            notificationEndTime = EditorApplication.timeSinceStartup + 5.0;
        }

        public void ShowReloadNotification(bool success)
        {
            showReloadNotification = true;
            if (success)
            {
                reloadNotificationMessage = "BOOTHデータを再読み込みをしました。";
                reloadNotificationMessageType = MessageType.Info;
            }
            else
            {
                reloadNotificationMessage = "BOOTHデータが見つかりません。同期を行ってください。";
                reloadNotificationMessageType = MessageType.Warning;
            }
            reloadNotificationEndTime = EditorApplication.timeSinceStartup + 5.0;
        }

        public void DrawNotifications()
        {
            // 更新通知
            if (showUpdateNotification && EditorApplication.timeSinceStartup < notificationEndTime)
            {
                EditorGUILayout.HelpBox("BOOTHデータを更新しました。", MessageType.Info);
            }
            else if (showUpdateNotification)
            {
                showUpdateNotification = false;
            }
            
            // 再読み込み通知
            if (showReloadNotification && EditorApplication.timeSinceStartup < reloadNotificationEndTime)
            {
                EditorGUILayout.HelpBox(reloadNotificationMessage, reloadNotificationMessageType);
            }
            else if (showReloadNotification)
            {
                showReloadNotification = false;
            }
        }
    }
}

