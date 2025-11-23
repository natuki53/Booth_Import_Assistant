using System;
using System.IO;
using UnityEngine;

namespace BoothImportAssistant.Bridge
{
    /// <summary>
    /// Node.jsとnpmのパス検出
    /// 元ファイル: BridgeManager.cs の FindNodePath(), FindSystemNpmPath()
    /// </summary>
    public static class NodePathResolver
    {
        public static string FindNodePath()
        {
            // 1. バンドルされたNode.jsを優先
            string bundledNode = BridgePathResolver.GetBundledNodePath();
            if (!string.IsNullOrEmpty(bundledNode) && File.Exists(bundledNode))
            {
                return bundledNode;
            }
            
            // 2. システムのNode.jsをフォールバック
            // Windows
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindNodePathWindows();
            }
            
            // Mac/Linux
            if (Application.platform == RuntimePlatform.OSXEditor || 
                Application.platform == RuntimePlatform.LinuxEditor)
            {
                return FindNodePathUnix();
            }
            
            return null;
        }

        private static string FindNodePathWindows()
        {
            // 環境変数PATHからnode.exeを検索
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                string[] paths = pathEnv.Split(';');
                foreach (string path in paths)
                {
                    string nodePath = Path.Combine(path, "node.exe");
                    nodePath = nodePath.Replace('\\', '/');
                    if (File.Exists(nodePath))
                    {
                        return nodePath;
                    }
                }
            }
            
            // 一般的なインストール場所
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string nodePath1 = Path.Combine(programFiles, "nodejs", "node.exe");
            nodePath1 = nodePath1.Replace('\\', '/');
            if (File.Exists(nodePath1)) return nodePath1;
            
            return null;
        }

        private static string FindNodePathUnix()
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
                    string resolvedPath = ResolveNvmNodePath();
                    if (!string.IsNullOrEmpty(resolvedPath))
                    {
                        return resolvedPath;
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
                    nodePath = nodePath.Replace('\\', '/');
                    if (File.Exists(nodePath))
                    {
                        return nodePath;
                    }
                }
            }
            
            // 最後の手段：シェルコマンドとして"node"を返す
            return "node";
        }

        private static string ResolveNvmNodePath()
        {
            string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".nvm/versions/node");
            baseDir = baseDir?.Replace('\\', '/');
            if (Directory.Exists(baseDir))
            {
                var dirs = Directory.GetDirectories(baseDir);
                foreach (var dir in dirs)
                {
                    string normalizedDir = dir.Replace('\\', '/');
                    string fullPath = Path.Combine(normalizedDir, "bin", "node");
                    fullPath = fullPath.Replace('\\', '/');
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }
            return null;
        }

        public static string FindNpmPath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindNpmPathWindows();
            }
            else if (Application.platform == RuntimePlatform.OSXEditor || 
                     Application.platform == RuntimePlatform.LinuxEditor)
            {
                return FindNpmPathUnix();
            }
            
            return "npm";
        }

        private static string FindNpmPathWindows()
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
                    npmPath = npmPath.Replace('\\', '/');
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
                npmPath1 = npmPath1.Replace('\\', '/');
                if (File.Exists(npmPath1)) return npmPath1;
            }
            
            return "npm.cmd";
        }

        private static string FindNpmPathUnix()
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
                    string resolvedPath = ResolveNvmNpmPath();
                    if (!string.IsNullOrEmpty(resolvedPath))
                    {
                        return resolvedPath;
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
                    npmPath = npmPath.Replace('\\', '/');
                    if (File.Exists(npmPath))
                    {
                        return npmPath;
                    }
                }
            }
            
            return "npm";
        }

        private static string ResolveNvmNpmPath()
        {
            string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".nvm/versions/node");
            baseDir = baseDir?.Replace('\\', '/');
            if (Directory.Exists(baseDir))
            {
                var dirs = Directory.GetDirectories(baseDir);
                foreach (var dir in dirs)
                {
                    string normalizedDir = dir.Replace('\\', '/');
                    string fullPath = Path.Combine(normalizedDir, "bin", "npm");
                    fullPath = fullPath.Replace('\\', '/');
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }
            return null;
        }
    }
}

