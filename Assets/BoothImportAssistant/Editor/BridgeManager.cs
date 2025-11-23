using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using BoothImportAssistant.Bridge;
using BoothImportAssistant.Utils;

namespace BoothImportAssistant
{
    /// <summary>
    /// Bridge（Node.jsサーバー）の起動・終了を管理（リファクタリング版）
    /// 元ファイル: BridgeManager.cs を部品・機能ごとに分割
    /// </summary>
    [InitializeOnLoad]
    public static class BridgeManager
    {
        private static NodeProcessManager processManager = new NodeProcessManager();
        
        // ポートチェックのキャッシュ（パフォーマンス向上のため）
        private static bool? cachedBridgeRunningStatus = null;
        private static double lastPortCheckTime = 0;
        private const double PORT_CHECK_CACHE_DURATION = 10.0; // 10秒間キャッシュ（起動/停止時は即座に更新）

        static BridgeManager()
        {
            EditorApplication.quitting += OnEditorQuitting;
        }

        /// <summary>
        /// Bridgeを起動
        /// </summary>
        public static bool StartBridge()
        {
            // キャッシュをクリア（起動時に状態が変わるため）
            cachedBridgeRunningStatus = null;

            string projectPath = ProjectPathUtility.GetProjectPath();
            if (string.IsNullOrEmpty(projectPath))
            {
                Debug.LogError("[BoothBridge] プロジェクトパスを取得できません");
                EditorUtility.DisplayDialog("エラー", 
                    "プロジェクトが保存されていません。\nプロジェクトを保存してから再試行してください。", 
                    "OK");
                return false;
            }

            string nodePath = NodePathResolver.FindNodePath();
            if (string.IsNullOrEmpty(nodePath))
            {
                Debug.LogError("[BoothBridge] Node.js (v18+) が見つかりません - https://nodejs.org/");
                EditorUtility.DisplayDialog("エラー", 
                    "Node.js (v18以上) がインストールされていません。\n\nhttps://nodejs.org/ からインストールしてください。", 
                    "OK");
                return false;
            }

            string bridgeScriptPath = BridgePathResolver.GetBridgeScriptPath();
            if (!File.Exists(bridgeScriptPath))
            {
                Debug.LogError("[BoothBridge] bridge.jsが見つかりません: " + bridgeScriptPath);
                EditorUtility.DisplayDialog("エラー", 
                    "bridge.js が見つかりません。\n\n期待されるパス:\n" + bridgeScriptPath, 
                    "OK");
                return false;
            }

            // node_modulesが存在しない場合、npm installを実行
            string bridgeFolder = Path.GetDirectoryName(bridgeScriptPath);
            if (!string.IsNullOrEmpty(bridgeFolder))
            {
                bridgeFolder = bridgeFolder.Replace('\\', '/');
            }
            string nodeModulesPath = Path.Combine(bridgeFolder, "node_modules");
            nodeModulesPath = nodeModulesPath.Replace('\\', '/');
            string packageJsonPath = Path.Combine(bridgeFolder, "package.json");
            packageJsonPath = packageJsonPath.Replace('\\', '/');
            
            if (File.Exists(packageJsonPath) && !Directory.Exists(nodeModulesPath))
            {
                Debug.Log("[BoothBridge] node_modulesが見つかりません。npm installを実行します...");
                if (!NpmInstaller.InstallNodeModules(nodePath, bridgeFolder))
                {
                    Debug.LogError("[BoothBridge] npm installに失敗しました");
                    EditorUtility.DisplayDialog("エラー", 
                        "Node.jsの依存関係のインストールに失敗しました。\n\n手動で以下のコマンドを実行してください：\n\ncd \"" + bridgeFolder + "\"\nnpm install", 
                        "OK");
                    return false;
                }
                Debug.Log("[BoothBridge] npm installが完了しました");
            }

            // プロセスを起動
            bool started = processManager.StartNodeProcess(nodePath, bridgeScriptPath, projectPath);
            if (started)
            {
                // 起動成功時はキャッシュを更新
                cachedBridgeRunningStatus = true;
                lastPortCheckTime = EditorApplication.timeSinceStartup;
            }
            
            return started;
        }

        /// <summary>
        /// Bridgeを終了
        /// </summary>
        public static void StopBridge()
        {
            // キャッシュをクリア（停止時に状態が変わるため）
            cachedBridgeRunningStatus = null;
            
            processManager.StopNodeProcess();
        }

        /// <summary>
        /// Bridgeが起動中か
        /// </summary>
        public static bool IsBridgeRunning()
        {
            // プロセス参照がある場合、それをチェック（これは軽量なのでいつでもチェック）
            if (processManager.IsProcessRunning())
            {
                // プロセス参照がある場合はキャッシュを更新
                cachedBridgeRunningStatus = true;
                lastPortCheckTime = EditorApplication.timeSinceStartup;
                return true;
            }
            
            // プロセス参照がない場合、ポートチェックが必要
            // キャッシュが有効な場合はキャッシュを返す
            double currentTime = EditorApplication.timeSinceStartup;
            if (cachedBridgeRunningStatus.HasValue && 
                (currentTime - lastPortCheckTime) < PORT_CHECK_CACHE_DURATION)
            {
                return cachedBridgeRunningStatus.Value;
            }
            
            // キャッシュが無効または存在しない場合、ポートチェックを実行
            bool isRunning = PortManager.IsPortInUse(49729);
            cachedBridgeRunningStatus = isRunning;
            lastPortCheckTime = currentTime;
            return isRunning;
        }

        private static void OnEditorQuitting()
        {
            StopBridge();
        }
    }
}

