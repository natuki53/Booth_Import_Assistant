using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BoothImportAssistant
{
    /// <summary>
    /// Bridge（Node.jsサーバー）の起動・終了を管理
    /// </summary>
    [InitializeOnLoad]
    public static class BridgeManager
    {
        private static Process bridgeProcess;
        private static string projectPath;
        private static bool isInitialized = false;

        static BridgeManager()
        {
            EditorApplication.quitting += OnEditorQuitting;
            isInitialized = true;
            
            Debug.Log("[BoothBridge] BridgeManager初期化完了");
        }

        /// <summary>
        /// Bridgeを起動
        /// </summary>
        public static bool StartBridge()
        {
            // 既に起動中の場合
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                Debug.Log("[BoothBridge] Bridgeは既に起動しています");
                return true;
            }

            // プロジェクトパス取得
            projectPath = GetProjectPath();
            if (string.IsNullOrEmpty(projectPath))
            {
                Debug.LogWarning("[BoothBridge][ERROR] プロジェクトパスを取得できません。プロジェクトを保存してください。");
                EditorUtility.DisplayDialog("エラー", 
                    "プロジェクトが保存されていません。\nプロジェクトを保存してから再試行してください。", 
                    "OK");
                return false;
            }

            // Node.js確認
            string nodePath = FindNodePath();
            if (string.IsNullOrEmpty(nodePath))
            {
                Debug.LogWarning("[BoothBridge][ERROR] Node.jsが見つかりません。");
                EditorUtility.DisplayDialog("エラー", 
                    "Node.js (v18以上) がインストールされていません。\n\nhttps://nodejs.org/ からインストールしてください。", 
                    "OK");
                return false;
            }

            // bridge.jsのパス（UnityExtensionフォルダの外にあるBridge）
            string bridgeScriptPath = GetBridgeScriptPath();
            if (!File.Exists(bridgeScriptPath))
            {
                Debug.LogWarning("[BoothBridge][ERROR] bridge.jsが見つかりません: " + bridgeScriptPath);
                EditorUtility.DisplayDialog("エラー", 
                    "bridge.js が見つかりません。\n\nBridgeフォルダが正しく配置されているか確認してください。", 
                    "OK");
                return false;
            }

            try
            {
                // プロセス起動
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = nodePath,
                    Arguments = $"\"{bridgeScriptPath}\" --projectPath \"{projectPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                bridgeProcess = new Process { StartInfo = startInfo };
                
                // 出力をUnityコンソールにリダイレクト
                bridgeProcess.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Debug.Log(args.Data);
                    }
                };
                
                bridgeProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Debug.LogError(args.Data);
                    }
                };

                bridgeProcess.Start();
                bridgeProcess.BeginOutputReadLine();
                bridgeProcess.BeginErrorReadLine();

                // 起動確認（500ms待機）
                System.Threading.Thread.Sleep(500);

                if (bridgeProcess.HasExited)
                {
                    Debug.LogError("[BoothBridge][ERROR] Bridgeの起動に失敗しました");
                    return false;
                }

                Debug.Log("[BoothBridge] Bridge起動に成功しました");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[BoothBridge][ERROR] Bridge起動エラー: " + ex.Message);
                EditorUtility.DisplayDialog("エラー", 
                    "Bridgeの起動に失敗しました。\n\n" + ex.Message, 
                    "OK");
                return false;
            }
        }

        /// <summary>
        /// Bridgeを終了
        /// </summary>
        public static void StopBridge()
        {
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                try
                {
                    bridgeProcess.Kill();
                    bridgeProcess.Dispose();
                    bridgeProcess = null;
                    Debug.Log("[BoothBridge] Bridge終了");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[BoothBridge] Bridge終了エラー: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Bridgeが起動中か
        /// </summary>
        public static bool IsBridgeRunning()
        {
            return bridgeProcess != null && !bridgeProcess.HasExited;
        }

        private static void OnEditorQuitting()
        {
            StopBridge();
        }

        private static string GetProjectPath()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
            {
                return null;
            }
            
            // Assets/ の親ディレクトリがプロジェクトルート
            return Directory.GetParent(dataPath).FullName;
        }

        private static string FindNodePath()
        {
            // Windows
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                // 環境変数PATHからnode.exeを検索
                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    string[] paths = pathEnv.Split(';');
                    foreach (string path in paths)
                    {
                        string nodePath = Path.Combine(path, "node.exe");
                        if (File.Exists(nodePath))
                        {
                            return nodePath;
                        }
                    }
                }
                
                // 一般的なインストール場所
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string nodePath1 = Path.Combine(programFiles, "nodejs", "node.exe");
                if (File.Exists(nodePath1)) return nodePath1;
                
                return null;
            }
            
            // Mac/Linux
            if (Application.platform == RuntimePlatform.OSXEditor || 
                Application.platform == RuntimePlatform.LinuxEditor)
            {
                // 一般的なインストール場所を確認
                string[] commonPaths = new string[]
                {
                    "/usr/local/bin/node",           // Homebrew (Intel Mac)
                    "/opt/homebrew/bin/node",        // Homebrew (Apple Silicon)
                    "/usr/bin/node",                 // システムインストール
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".nvm/versions/node/*/bin/node")
                };
                
                foreach (string nodePath in commonPaths)
                {
                    // ワイルドカード対応（nvm用）
                    if (nodePath.Contains("*"))
                    {
                        string baseDir = Path.GetDirectoryName(Path.GetDirectoryName(nodePath));
                        if (Directory.Exists(baseDir))
                        {
                            var dirs = Directory.GetDirectories(baseDir);
                            foreach (var dir in dirs)
                            {
                                string fullPath = Path.Combine(dir, "bin", "node");
                                if (File.Exists(fullPath))
                                {
                                    return fullPath;
                                }
                            }
                        }
                    }
                    else if (File.Exists(nodePath))
                    {
                        return nodePath;
                    }
                }
                
                // 環境変数PATHから検索
                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    string[] paths = pathEnv.Split(':');
                    foreach (string path in paths)
                    {
                        string nodePath = Path.Combine(path, "node");
                        if (File.Exists(nodePath))
                        {
                            return nodePath;
                        }
                    }
                }
                
                // 最後の手段：シェルコマンドとして"node"を返す
                return "node";
            }
            
            return null;
        }

        private static string GetBridgeScriptPath()
        {
            // UnityExtension/Editor/BridgeManager.cs から
            // Bridge/bridge.js を探す
            string scriptPath = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName();
            string editorFolder = Path.GetDirectoryName(scriptPath);
            string unityExtensionFolder = Directory.GetParent(editorFolder).FullName;
            string rootFolder = Directory.GetParent(unityExtensionFolder).FullName;
            string bridgePath = Path.Combine(rootFolder, "Bridge", "bridge.js");
            
            return bridgePath;
        }
    }
}

