using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BoothImportAssistant.Bridge
{
    /// <summary>
    /// npm installの実行
    /// 元ファイル: BridgeManager.cs の InstallNodeModules()
    /// </summary>
    public static class NpmInstaller
    {
        public static bool InstallNodeModules(string nodePath, string workingDirectory)
        {
            try
            {
                // バンドルされたNode.jsを使用しているかどうかを判定
                bool isBundledNode = !string.IsNullOrEmpty(nodePath) && 
                                     nodePath.Replace('\\', '/').Contains("node-runtime");
                
                // npmコマンドのパスを取得
                string npmPath = GetNpmPath(nodePath);
                
                // バンドルされたNode.jsを使用している場合、シェル経由で実行
                if (isBundledNode)
                {
                    return InstallViaShell(npmPath, workingDirectory);
                }
                
                // 通常の方法でnpmを実行
                return InstallDirect(npmPath, workingDirectory);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoothBridge] npm install実行エラー: {ex.Message}");
                return false;
            }
        }

        private static string GetNpmPath(string nodePath)
        {
            string nodeDir = Path.GetDirectoryName(nodePath);
            nodeDir = nodeDir?.Replace('\\', '/') ?? nodeDir;
            
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                // Windows: まずnode.exeと同じディレクトリのnpm.cmdを確認
                string npmPath = Path.Combine(nodeDir, "npm.cmd");
                npmPath = npmPath.Replace('\\', '/');
                if (!File.Exists(npmPath))
                {
                    // npm.cmdが見つからない場合、システムのnpmを使用
                    npmPath = NodePathResolver.FindNpmPath();
                }
                return npmPath;
            }
            else
            {
                // Mac/Linux: nodeと同じディレクトリのnpm、またはシステムのnpm
                string npmPath = Path.Combine(nodeDir, "npm");
                npmPath = npmPath.Replace('\\', '/');
                if (!File.Exists(npmPath))
                {
                    npmPath = NodePathResolver.FindNpmPath();
                }
                return npmPath;
            }
        }

        private static bool InstallViaShell(string npmPath, string workingDirectory)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return InstallViaShellWindows(npmPath, workingDirectory);
            }
            else if (Application.platform == RuntimePlatform.OSXEditor || 
                     Application.platform == RuntimePlatform.LinuxEditor)
            {
                return InstallViaShellUnix(npmPath, workingDirectory);
            }
            
            return false;
        }

        private static bool InstallViaShellWindows(string npmPath, string workingDirectory)
        {
            string nodePathForNpm = FindNodeDirectory(npmPath);
            
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

        private static bool InstallViaShellUnix(string npmPath, string workingDirectory)
        {
            string nodePathForNpm = FindNodeDirectory(npmPath);
            
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

        private static bool InstallDirect(string npmPath, string workingDirectory)
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

        private static string FindNodeDirectory(string npmPath)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindNodeDirectoryWindows(npmPath);
            }
            else if (Application.platform == RuntimePlatform.OSXEditor || 
                     Application.platform == RuntimePlatform.LinuxEditor)
            {
                return FindNodeDirectoryUnix(npmPath);
            }
            
            return null;
        }

        private static string FindNodeDirectoryWindows(string npmPath)
        {
            // npmと同じディレクトリにnode.exeがある可能性が高い
            if (!string.IsNullOrEmpty(npmPath) && File.Exists(npmPath))
            {
                string npmDir = Path.GetDirectoryName(npmPath);
                npmDir = npmDir?.Replace('\\', '/') ?? npmDir;
                string nodeExePath = Path.Combine(npmDir, "node.exe");
                nodeExePath = nodeExePath.Replace('\\', '/');
                if (File.Exists(nodeExePath))
                {
                    return npmDir;
                }
            }
            
            // 一般的なnodeのインストール場所を確認
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                string nodePath1 = Path.Combine(programFiles, "nodejs", "node.exe");
                nodePath1 = nodePath1.Replace('\\', '/');
                if (File.Exists(nodePath1))
                {
                    string nodeDir = Path.Combine(programFiles, "nodejs");
                    return nodeDir.Replace('\\', '/');
                }
            }
            
            return null;
        }

        private static string FindNodeDirectoryUnix(string npmPath)
        {
            // npmと同じディレクトリにnodeがある可能性が高い
            if (!string.IsNullOrEmpty(npmPath) && File.Exists(npmPath))
            {
                string npmDir = Path.GetDirectoryName(npmPath);
                npmDir = npmDir?.Replace('\\', '/') ?? npmDir;
                string nodePathCheck = Path.Combine(npmDir, "node");
                nodePathCheck = nodePathCheck.Replace('\\', '/');
                if (File.Exists(nodePathCheck))
                {
                    return npmDir;
                }
            }
            
            // 一般的なnodeのインストール場所を確認
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
                    string nodeDir = Path.GetDirectoryName(commonNodePath);
                    return nodeDir?.Replace('\\', '/') ?? nodeDir;
                }
            }
            
            return null;
        }
    }
}

