using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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

        static BridgeManager()
        {
            EditorApplication.quitting += OnEditorQuitting;
        }

        /// <summary>
        /// Bridgeを起動
        /// </summary>
        public static bool StartBridge()
        {
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                return true;
            }

            projectPath = GetProjectPath();
            if (string.IsNullOrEmpty(projectPath))
            {
                Debug.LogError("[BoothBridge] プロジェクトパスを取得できません");
                EditorUtility.DisplayDialog("エラー", 
                    "プロジェクトが保存されていません。\nプロジェクトを保存してから再試行してください。", 
                    "OK");
                return false;
            }

            string nodePath = FindNodePath();
            if (string.IsNullOrEmpty(nodePath))
            {
                Debug.LogError("[BoothBridge] Node.js (v18+) が見つかりません - https://nodejs.org/");
                EditorUtility.DisplayDialog("エラー", 
                    "Node.js (v18以上) がインストールされていません。\n\nhttps://nodejs.org/ からインストールしてください。", 
                    "OK");
                return false;
            }

            string bridgeScriptPath = GetBridgeScriptPath();
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
            string nodeModulesPath = Path.Combine(bridgeFolder, "node_modules");
            string packageJsonPath = Path.Combine(bridgeFolder, "package.json");
            
            if (File.Exists(packageJsonPath) && !Directory.Exists(nodeModulesPath))
            {
                Debug.Log("[BoothBridge] node_modulesが見つかりません。npm installを実行します...");
                if (!InstallNodeModules(nodePath, bridgeFolder))
                {
                    Debug.LogError("[BoothBridge] npm installに失敗しました");
                    EditorUtility.DisplayDialog("エラー", 
                        "Node.jsの依存関係のインストールに失敗しました。\n\n手動で以下のコマンドを実行してください：\n\ncd \"" + bridgeFolder + "\"\nnpm install", 
                        "OK");
                    return false;
                }
                Debug.Log("[BoothBridge] npm installが完了しました");
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = nodePath,
                    Arguments = $"\"{bridgeScriptPath}\" --projectPath \"{projectPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                bridgeProcess = new Process { StartInfo = startInfo };
                
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

                System.Threading.Thread.Sleep(500);

                if (bridgeProcess.HasExited)
                {
                    Debug.LogError("[BoothBridge] Bridge起動失敗 - Node.js v18+が必要、またはポート4823が使用中");
                    EditorUtility.DisplayDialog("エラー", 
                        "Bridgeの起動に失敗しました。\n\nUnityコンソールで詳細を確認してください。", 
                        "OK");
                    return false;
                }

                Debug.Log("[BoothBridge] Bridge起動成功 (PID: " + bridgeProcess.Id + ")");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[BoothBridge] Bridge起動エラー: " + ex.Message);
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
            // 1. バンドルされたNode.jsを優先
            string bundledNode = GetBundledNodePath();
            if (!string.IsNullOrEmpty(bundledNode) && File.Exists(bundledNode))
            {
                return bundledNode;
            }
            
            // 2. システムのNode.jsをフォールバック
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

        private static string GetBundledNodePath()
        {
            string bridgeScriptPath = GetBridgeScriptPath();
            if (string.IsNullOrEmpty(bridgeScriptPath))
            {
                return null;
            }
            
            string bridgeFolder = Path.GetDirectoryName(bridgeScriptPath);
            string runtimeFolder = Path.Combine(bridgeFolder, "node-runtime");
            
            #if UNITY_EDITOR_WIN
                return Path.Combine(runtimeFolder, "win-x64", "node.exe");
            #elif UNITY_EDITOR_OSX
                string nodePath = Path.Combine(runtimeFolder, "osx-x64", "node");
                if (File.Exists(nodePath))
                {
                    try
                    {
                        var process = new Process();
                        process.StartInfo.FileName = "chmod";
                        process.StartInfo.Arguments = "+x \"" + nodePath + "\"";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.Start();
                        process.WaitForExit();
                    }
                    catch { }
                }
                return nodePath;
            #elif UNITY_EDITOR_LINUX
                string nodePath = Path.Combine(runtimeFolder, "linux-x64", "node");
                if (File.Exists(nodePath))
                {
                    try
                    {
                        var process = new Process();
                        process.StartInfo.FileName = "chmod";
                        process.StartInfo.Arguments = "+x \"" + nodePath + "\"";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.Start();
                        process.WaitForExit();
                    }
                    catch { }
                }
                return nodePath;
            #else
                return null;
            #endif
        }

        private static string GetBridgeScriptPath()
        {
            // AssetDatabaseを使用してパスを取得（Unityのアセットパスは常にスラッシュを使用）
            // VPM経由でインストールされた場合、Packages/から始まる可能性がある
            string[] possiblePaths = new string[]
            {
                "Assets/BoothImportAssistant/Editor/BridgeManager.cs",
                "Packages/com.natuki.booth-import-assistant/Editor/BridgeManager.cs"
            };
            
            foreach (string assetPath in possiblePaths)
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    string bridgeManagerPath = AssetDatabase.GUIDToAssetPath(guid);
                    // パスを正規化（バックスラッシュをスラッシュに変換）
                    bridgeManagerPath = bridgeManagerPath.Replace('\\', '/');
                    
                    // アセットパスをファイルシステムパスに変換
                    string fullPath;
                    if (bridgeManagerPath.StartsWith("Assets/"))
                    {
                        fullPath = Path.GetFullPath(bridgeManagerPath.Replace("Assets/", Application.dataPath + "/"));
                    }
                    else if (bridgeManagerPath.StartsWith("Packages/"))
                    {
                        // Packages/はプロジェクトルートからの相対パス
                        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                        fullPath = Path.GetFullPath(Path.Combine(projectRoot, bridgeManagerPath));
                    }
                    else
                    {
                        fullPath = Path.GetFullPath(bridgeManagerPath);
                    }
                    
                    // パスを正規化
                    fullPath = fullPath.Replace('\\', '/');
                    string editorFolder = Path.GetDirectoryName(fullPath);
                    string boothImportAssistantFolder = Directory.GetParent(editorFolder).FullName;
                    string bridgePath = Path.Combine(boothImportAssistantFolder, "Bridge", "bridge.js");
                    
                    // パスを正規化（バックスラッシュをスラッシュに変換）
                    return bridgePath.Replace('\\', '/');
                }
            }
            
            // フォールバック: StackTraceを使用
            try
            {
                string scriptPath = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName();
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    // パスを正規化（バックスラッシュをスラッシュに変換）
                    scriptPath = scriptPath.Replace('\\', '/');
                    string editorFolder = Path.GetDirectoryName(scriptPath);
                    string boothImportAssistantFolder = Directory.GetParent(editorFolder).FullName;
                    string bridgePath = Path.Combine(boothImportAssistantFolder, "Bridge", "bridge.js");
                    
                    // パスを正規化
                    return bridgePath.Replace('\\', '/');
                }
            }
            catch
            {
                // StackTrace取得に失敗した場合の処理
            }
            
            // 最後のフォールバック: 相対パスから構築
            string dataPath = Application.dataPath;
            string fallbackPath = Path.Combine(dataPath, "BoothImportAssistant", "Bridge", "bridge.js");
            return fallbackPath.Replace('\\', '/');
        }

        /// <summary>
        /// node_modulesをインストール
        /// </summary>
        private static bool InstallNodeModules(string nodePath, string workingDirectory)
        {
            try
            {
                // npmコマンドのパスを取得
                string npmPath;
                string nodeDir = Path.GetDirectoryName(nodePath);
                
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // Windows: まずnode.exeと同じディレクトリのnpm.cmdを確認
                    npmPath = Path.Combine(nodeDir, "npm.cmd");
                    if (!File.Exists(npmPath))
                    {
                        // npm.cmdが見つからない場合、システムのnpmを使用
                        npmPath = "npm.cmd";
                    }
                }
                else
                {
                    // Mac/Linux: nodeと同じディレクトリのnpm、またはシステムのnpm
                    npmPath = Path.Combine(nodeDir, "npm");
                    if (!File.Exists(npmPath))
                    {
                        // システムのnpmを使用
                        npmPath = "npm";
                    }
                }

                ProcessStartInfo installInfo = new ProcessStartInfo
                {
                    FileName = npmPath,
                    Arguments = "install",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                Debug.Log($"[BoothBridge] npm install実行中: {workingDirectory}");
                
                using (Process installProcess = Process.Start(installInfo))
                {
                    if (installProcess == null)
                    {
                        return false;
                    }

                    string output = installProcess.StandardOutput.ReadToEnd();
                    string error = installProcess.StandardError.ReadToEnd();
                    
                    installProcess.WaitForExit();

                    if (!string.IsNullOrEmpty(output))
                    {
                        Debug.Log($"[BoothBridge] npm install出力:\n{output}");
                    }
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogWarning($"[BoothBridge] npm install警告:\n{error}");
                    }

                    if (installProcess.ExitCode == 0)
                    {
                        return true;
                    }
                    else
                    {
                        Debug.LogError($"[BoothBridge] npm install失敗 (終了コード: {installProcess.ExitCode})");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoothBridge] npm install実行エラー: {ex.Message}");
                return false;
            }
        }
    }
}

