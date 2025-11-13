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
            
            // 既存のプロセスが存在する場合、確実に停止してから新しいプロセスを起動
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                StopBridge();
            }
            
            // ポート49729を使用しているプロセスを検出して終了
            KillProcessUsingPort(49729);
            System.Threading.Thread.Sleep(500); // プロセス終了を待機

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
            if (!string.IsNullOrEmpty(bridgeFolder))
            {
                bridgeFolder = bridgeFolder.Replace('\\', '/');
            }
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
                
                // エラーのみ出力
                bridgeProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Debug.LogError(args.Data);
                    }
                };

                bridgeProcess.Start();
                bridgeProcess.BeginErrorReadLine();

                System.Threading.Thread.Sleep(500);

                if (bridgeProcess.HasExited)
                {
                    Debug.LogError("[BoothBridge] Bridge起動失敗");
                    KillProcessUsingPort(49729);
                    EditorUtility.DisplayDialog("エラー", 
                        "Bridgeの起動に失敗しました。", 
                        "OK");
                    return false;
                }
                // 起動成功時はキャッシュを更新
                cachedBridgeRunningStatus = true;
                lastPortCheckTime = EditorApplication.timeSinceStartup;
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
            // キャッシュをクリア（停止時に状態が変わるため）
            cachedBridgeRunningStatus = null;
            
            bool stopped = false;
            
            // プロセス参照がある場合、それを停止
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                try
                {
                    bridgeProcess.Kill();
                    
                    if (!bridgeProcess.WaitForExit(5000))
                    {
                        if (bridgeProcess.HasExited)
                        {
                            stopped = true;
                        }
                    }
                    else
                    {
                        stopped = true;
                    }
                    
                    bridgeProcess.Dispose();
                    bridgeProcess = null;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[BoothBridge] Bridge終了エラー: " + ex.Message);
                    try
                    {
                        bridgeProcess?.Dispose();
                    }
                    catch { }
                    bridgeProcess = null;
                }
            }
            
            // ポート49729を使用しているプロセスを停止
            if (!stopped)
            {
                KillProcessUsingPort(49729);
            }
        }

        /// <summary>
        /// Bridgeが起動中か
        /// </summary>
        public static bool IsBridgeRunning()
        {
            // プロセス参照がある場合、それをチェック（これは軽量なので常にチェック）
            if (bridgeProcess != null && !bridgeProcess.HasExited)
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
            bool isRunning = IsPortInUse(49729);
            cachedBridgeRunningStatus = isRunning;
            lastPortCheckTime = currentTime;
            return isRunning;
        }
        
        /// <summary>
        /// 指定されたポートを使用しているプロセスが存在するかチェック
        /// </summary>
        private static bool IsPortInUse(int port)
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // Windows: netstatでポートを使用しているプロセスIDを取得
                    Process netstatProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/c netstat -ano | findstr :" + port,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    netstatProcess.Start();
                    string output = netstatProcess.StandardOutput.ReadToEnd();
                    netstatProcess.WaitForExit();
                    
                    // 出力からプロセスIDを抽出
                    string[] lines = output.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        if (line.Contains(":" + port) && line.Contains("LISTENING"))
                        {
                            string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int pid))
                            {
                                try
                                {
                                    Process process = Process.GetProcessById(pid);
                                    if (process.ProcessName.ToLower().Contains("node"))
                                    {
                                        return true;
                                    }
                                }
                                catch
                                {
                                    // プロセスが存在しない場合は無視
                                }
                            }
                        }
                    }
                }
                else if (Application.platform == RuntimePlatform.OSXEditor || 
                         Application.platform == RuntimePlatform.LinuxEditor)
                {
                    // Mac/Linux: lsofでポートを使用しているプロセスIDを取得
                    Process lsofProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "lsof",
                            Arguments = "-ti:" + port,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    lsofProcess.Start();
                    string output = lsofProcess.StandardOutput.ReadToEnd();
                    lsofProcess.WaitForExit();
                    
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        string[] pids = output.Trim().Split('\n');
                        foreach (string pidStr in pids)
                        {
                            if (int.TryParse(pidStr.Trim(), out int pid))
                            {
                                try
                                {
                                    Process process = Process.GetProcessById(pid);
                                    if (process.ProcessName.ToLower().Contains("node"))
                                    {
                                        return true;
                                    }
                                }
                                catch
                                {
                                    // プロセスが存在しない場合は無視
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // エラーが発生した場合はfalseを返す
            }
            
            return false;
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
            if (!string.IsNullOrEmpty(bridgeFolder))
            {
                bridgeFolder = bridgeFolder.Replace('\\', '/');
            }
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
                    string fullPath = null;
                    if (bridgeManagerPath.StartsWith("Assets/"))
                    {
                        fullPath = Path.GetFullPath(bridgeManagerPath.Replace("Assets/", Application.dataPath + "/"));
                    }
                    else if (bridgeManagerPath.StartsWith("Packages/"))
                    {
                        // Packages/はプロジェクトルートからの相対パス
                        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                        if (!string.IsNullOrEmpty(projectRoot))
                        {
                            projectRoot = projectRoot.Replace('\\', '/');
                        }
                        fullPath = Path.GetFullPath(Path.Combine(projectRoot, bridgeManagerPath));
                    }
                    else
                    {
                        fullPath = Path.GetFullPath(bridgeManagerPath);
                    }
                    
                    // fullPathが取得できなかった場合はスキップ
                    if (string.IsNullOrEmpty(fullPath))
                    {
                        continue;
                    }
                    
                    // パスを正規化
                    fullPath = fullPath.Replace('\\', '/');
                    string editorFolder = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(editorFolder))
                    {
                        editorFolder = editorFolder.Replace('\\', '/');
                    }
                    string boothImportAssistantFolder = Directory.GetParent(editorFolder).FullName;
                    if (!string.IsNullOrEmpty(boothImportAssistantFolder))
                    {
                        boothImportAssistantFolder = boothImportAssistantFolder.Replace('\\', '/');
                    }
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
                    if (!string.IsNullOrEmpty(editorFolder))
                    {
                        editorFolder = editorFolder.Replace('\\', '/');
                    }
                    string boothImportAssistantFolder = Directory.GetParent(editorFolder).FullName;
                    if (!string.IsNullOrEmpty(boothImportAssistantFolder))
                    {
                        boothImportAssistantFolder = boothImportAssistantFolder.Replace('\\', '/');
                    }
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
        /// システムのnpmパスを検索
        /// </summary>
        private static string FindSystemNpmPath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                // Windows: 環境変数PATHからnpm.cmdを検索
                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    string[] paths = pathEnv.Split(';');
                    foreach (string path in paths)
                    {
                        if (string.IsNullOrEmpty(path)) continue;
                        string npmPath = Path.Combine(path, "npm.cmd");
                        if (File.Exists(npmPath))
                        {
                            return npmPath;
                        }
                    }
                }
                
                // 一般的なインストール場所
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (!string.IsNullOrEmpty(programFiles))
                {
                    string npmPath1 = Path.Combine(programFiles, "nodejs", "npm.cmd");
                    if (File.Exists(npmPath1)) return npmPath1;
                }
                
                return "npm.cmd";
            }
            else if (Application.platform == RuntimePlatform.OSXEditor || 
                     Application.platform == RuntimePlatform.LinuxEditor)
            {
                // Mac/Linux: 一般的なインストール場所を確認
                string[] commonPaths = new string[]
                {
                    "/usr/local/bin/npm",           // Homebrew (Intel Mac)
                    "/opt/homebrew/bin/npm",        // Homebrew (Apple Silicon)
                    "/usr/bin/npm",                 // システムインストール
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".nvm/versions/node/*/bin/npm")
                };
                
                foreach (string npmPath in commonPaths)
                {
                    // ワイルドカード対応（nvm用）
                    if (npmPath.Contains("*"))
                    {
                        string baseDir = Path.GetDirectoryName(Path.GetDirectoryName(npmPath));
                        if (Directory.Exists(baseDir))
                        {
                            var dirs = Directory.GetDirectories(baseDir);
                            foreach (var dir in dirs)
                            {
                                string fullPath = Path.Combine(dir, "bin", "npm");
                                if (File.Exists(fullPath))
                                {
                                    return fullPath;
                                }
                            }
                        }
                    }
                    else if (File.Exists(npmPath))
                    {
                        return npmPath;
                    }
                }
                
                // 環境変数PATHから検索
                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    string[] paths = pathEnv.Split(':');
                    foreach (string path in paths)
                    {
                        if (string.IsNullOrEmpty(path)) continue;
                        string npmPath = Path.Combine(path, "npm");
                        if (File.Exists(npmPath))
                        {
                            return npmPath;
                        }
                    }
                }
                
                return "npm";
            }
            
            return "npm";
        }

        /// <summary>
        /// node_modulesをインストール
        /// </summary>
        private static bool InstallNodeModules(string nodePath, string workingDirectory)
        {
            try
            {
                // バンドルされたNode.jsを使用しているかどうかを判定
                bool isBundledNode = !string.IsNullOrEmpty(nodePath) && 
                                     nodePath.Replace('\\', '/').Contains("node-runtime");
                
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
                        npmPath = FindSystemNpmPath();
                    }
                }
                else
                {
                    // Mac/Linux: nodeと同じディレクトリのnpm、またはシステムのnpm
                    npmPath = Path.Combine(nodeDir, "npm");
                    if (!File.Exists(npmPath))
                    {
                        // バンドルされたNode.jsを使用している場合、システムのnpmのフルパスを検索
                        if (isBundledNode)
                        {
                            npmPath = FindSystemNpmPath();
                        }
                        else
                        {
                            // システムのnpmを使用
                            npmPath = "npm";
                        }
                    }
                }

                // バンドルされたNode.jsを使用している場合、シェル経由で実行
                // これにより環境変数PATHが正しく設定される
                if (isBundledNode)
                {
                    string nodePathForNpm = null;
                    
                    if (Application.platform == RuntimePlatform.WindowsEditor)
                    {
                        // Windows: npmと同じディレクトリにnode.exeがある可能性が高い
                        if (!string.IsNullOrEmpty(npmPath) && File.Exists(npmPath))
                        {
                            string npmDir = Path.GetDirectoryName(npmPath);
                            if (File.Exists(Path.Combine(npmDir, "node.exe")))
                            {
                                nodePathForNpm = npmDir;
                            }
                        }
                        
                        // 一般的なnodeのインストール場所を確認
                        if (string.IsNullOrEmpty(nodePathForNpm))
                        {
                            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                            if (!string.IsNullOrEmpty(programFiles))
                            {
                                string nodePath1 = Path.Combine(programFiles, "nodejs", "node.exe");
                                if (File.Exists(nodePath1))
                                {
                                    nodePathForNpm = Path.Combine(programFiles, "nodejs");
                                }
                            }
                        }
                        
                        // cmd経由でnpmを実行（nodeのパスをPATHに追加）
                        string pathPrefix = !string.IsNullOrEmpty(nodePathForNpm) 
                            ? $"set PATH={nodePathForNpm};%PATH% && " 
                            : "";
                        
                        string cmdCommand = $"{pathPrefix}cd /d \"{workingDirectory}\" && \"{npmPath}\" install";
                        
                        ProcessStartInfo installInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c \"{cmdCommand}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8
                        };
                        
                        using (Process installProcess = Process.Start(installInfo))
                        {
                            if (installProcess == null)
                            {
                                return false;
                            }

                            string output = installProcess.StandardOutput.ReadToEnd();
                            string error = installProcess.StandardError.ReadToEnd();
                            
                            installProcess.WaitForExit();

                            if (installProcess.ExitCode == 0)
                            {
                                return true;
                            }
                            else
                            {
                                Debug.LogError($"[BoothBridge] npm install失敗 (終了コード: {installProcess.ExitCode})");
                                if (!string.IsNullOrEmpty(error))
                                {
                                    Debug.LogError($"[BoothBridge] エラー: {error}");
                                }
                                if (!string.IsNullOrEmpty(output))
                                {
                                    Debug.LogError($"[BoothBridge] 出力: {output}");
                                }
                                return false;
                            }
                        }
                    }
                    else if (Application.platform == RuntimePlatform.OSXEditor || 
                             Application.platform == RuntimePlatform.LinuxEditor)
                    {
                        // Mac/Linux: npmと同じディレクトリにnodeがある可能性が高い
                        if (!string.IsNullOrEmpty(npmPath) && File.Exists(npmPath))
                        {
                            string npmDir = Path.GetDirectoryName(npmPath);
                            if (File.Exists(Path.Combine(npmDir, "node")))
                            {
                                nodePathForNpm = npmDir;
                            }
                        }
                        
                        // 一般的なnodeのインストール場所を確認
                        if (string.IsNullOrEmpty(nodePathForNpm))
                        {
                            string[] commonNodePaths = new string[]
                            {
                                "/opt/homebrew/bin/node",        // Homebrew (Apple Silicon)
                                "/usr/local/bin/node",           // Homebrew (Intel Mac)
                                "/usr/bin/node"                  // システムインストール
                            };
                            
                            foreach (string commonNodePath in commonNodePaths)
                            {
                                if (File.Exists(commonNodePath))
                                {
                                    nodePathForNpm = Path.GetDirectoryName(commonNodePath);
                                    break;
                                }
                            }
                        }
                        
                        // シェルコマンドを構築
                        string pathPrefix = !string.IsNullOrEmpty(nodePathForNpm) 
                            ? $"export PATH='{nodePathForNpm}:$PATH' && " 
                            : "";
                        
                        string escapedWorkingDir = workingDirectory.Replace("'", "'\"'\"'");
                        string escapedNpmPath = (npmPath != "npm" && File.Exists(npmPath))
                            ? npmPath.Replace("'", "'\"'\"'")
                            : "npm";
                        
                        string shellCommand = $"{pathPrefix}cd '{escapedWorkingDir}' && '{escapedNpmPath}' install";
                        
                        ProcessStartInfo installInfo = new ProcessStartInfo
                        {
                            FileName = "/bin/sh",
                            Arguments = $"-c \"{shellCommand}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8
                        };
                        
                        using (Process installProcess = Process.Start(installInfo))
                        {
                            if (installProcess == null)
                            {
                                return false;
                            }

                            string output = installProcess.StandardOutput.ReadToEnd();
                            string error = installProcess.StandardError.ReadToEnd();
                            
                            installProcess.WaitForExit();

                            if (installProcess.ExitCode == 0)
                            {
                                return true;
                            }
                            else
                            {
                                Debug.LogError($"[BoothBridge] npm install失敗 (終了コード: {installProcess.ExitCode})");
                                if (!string.IsNullOrEmpty(error))
                                {
                                    Debug.LogError($"[BoothBridge] エラー: {error}");
                                }
                                if (!string.IsNullOrEmpty(output))
                                {
                                    Debug.LogError($"[BoothBridge] 出力: {output}");
                                }
                                return false;
                            }
                        }
                    }
                }
                
                // 通常の方法でnpmを実行（バンドルされていないNode.jsを使用している場合）
                {
                    // 通常の方法でnpmを実行
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
                    
                    using (Process installProcess = Process.Start(installInfo))
                    {
                        if (installProcess == null)
                        {
                            return false;
                        }

                        string output = installProcess.StandardOutput.ReadToEnd();
                        string error = installProcess.StandardError.ReadToEnd();
                        
                        installProcess.WaitForExit();

                        if (installProcess.ExitCode == 0)
                        {
                            return true;
                        }
                        else
                        {
                            Debug.LogError($"[BoothBridge] npm install失敗 (終了コード: {installProcess.ExitCode})");
                            if (!string.IsNullOrEmpty(error))
                            {
                                Debug.LogError($"[BoothBridge] エラー: {error}");
                            }
                            if (!string.IsNullOrEmpty(output))
                            {
                                Debug.LogError($"[BoothBridge] 出力: {output}");
                            }
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoothBridge] npm install実行エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 指定されたポートを使用しているプロセスを検出して終了
        /// </summary>
        private static void KillProcessUsingPort(int port)
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // Windows: netstatでポートを使用しているプロセスIDを取得
                    // パイプを使うため、cmd経由で実行
                    Process netstatProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/c netstat -ano | findstr :" + port,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    netstatProcess.Start();
                    string output = netstatProcess.StandardOutput.ReadToEnd();
                    netstatProcess.WaitForExit();
                    
                    // 出力からプロセスIDを抽出
                    string[] lines = output.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        if (line.Contains(":" + port) && line.Contains("LISTENING"))
                        {
                            string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int pid))
                            {
                                try
                                {
                                    Process process = Process.GetProcessById(pid);
                                    if (process.ProcessName.ToLower().Contains("node"))
                                    {
                                        // Node.jsプロセスを終了
                                        process.Kill();
                                        process.WaitForExit(3000);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"[BoothBridge] プロセス終了失敗: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                else if (Application.platform == RuntimePlatform.OSXEditor || 
                         Application.platform == RuntimePlatform.LinuxEditor)
                {
                    // Mac/Linux: lsofでポートを使用しているプロセスIDを取得
                    Process lsofProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "lsof",
                            Arguments = "-ti:" + port,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    lsofProcess.Start();
                    string output = lsofProcess.StandardOutput.ReadToEnd();
                    lsofProcess.WaitForExit();
                    
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        string[] pids = output.Trim().Split('\n');
                        foreach (string pidStr in pids)
                        {
                            if (int.TryParse(pidStr.Trim(), out int pid))
                            {
                                try
                                {
                                    Process process = Process.GetProcessById(pid);
                                    if (process.ProcessName.ToLower().Contains("node"))
                                    {
                                        // Node.jsプロセスを終了
                                        process.Kill();
                                        process.WaitForExit(3000);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"[BoothBridge] プロセス終了失敗: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoothBridge] ポート検出失敗: {ex.Message}");
            }
        }
    }
}

