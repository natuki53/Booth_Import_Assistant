using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoothImportAssistant.Bridge
{
    /// <summary>
    /// Bridge関連のパス解決
    /// 元ファイル: BridgeManager.cs の GetBridgeScriptPath(), GetBundledNodePath()
    /// </summary>
    public static class BridgePathResolver
    {
        public static string GetBridgeScriptPath()
        {
            // BridgeManager.csの位置から相対的にbridge.jsを探す
            string[] bridgeManagerGuids = AssetDatabase.FindAssets("BridgeManager t:Script", new[] { "Assets", "Packages" });
            foreach (string guid in bridgeManagerGuids)
            {
                string bridgeManagerPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(bridgeManagerPath))
                {
                    continue;
                }
                
                bridgeManagerPath = bridgeManagerPath.Replace('\\', '/');
                
                string fullPath = ConvertAssetPathToFullPath(bridgeManagerPath);
                
                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                {
                    fullPath = fullPath.Replace('\\', '/');
                    string editorFolder = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(editorFolder))
                    {
                        editorFolder = editorFolder.Replace('\\', '/');
                    }
                    var parentDir = Directory.GetParent(editorFolder);
                    if (parentDir != null)
                    {
                        string boothImportAssistantFolder = parentDir.FullName;
                        boothImportAssistantFolder = boothImportAssistantFolder?.Replace('\\', '/') ?? boothImportAssistantFolder;
                        string bridgePath = Path.Combine(boothImportAssistantFolder, "Bridge", "bridge.js");
                        bridgePath = bridgePath.Replace('\\', '/');
                        
                        if (File.Exists(bridgePath))
                        {
                            return bridgePath;
                        }
                    }
                }
            }
            
            // 最後のフォールバック
            string dataPath = Application.dataPath.Replace('\\', '/');
            string fallbackPath = Path.Combine(dataPath, "BoothImportAssistant", "Bridge", "bridge.js");
            return fallbackPath.Replace('\\', '/');
        }

        private static string ConvertAssetPathToFullPath(string assetPath)
        {
            string fullPath = null;
            if (assetPath.StartsWith("Assets/"))
            {
                string normalizedDataPath = Application.dataPath.Replace('\\', '/');
                fullPath = Path.GetFullPath(assetPath.Replace("Assets/", normalizedDataPath + "/"));
                if (!string.IsNullOrEmpty(fullPath))
                {
                    fullPath = fullPath.Replace('\\', '/');
                }
            }
            else if (assetPath.StartsWith("Packages/"))
            {
                string normalizedDataPath = Application.dataPath.Replace('\\', '/');
                string projectRoot = Directory.GetParent(normalizedDataPath)?.FullName;
                if (!string.IsNullOrEmpty(projectRoot))
                {
                    projectRoot = projectRoot.Replace('\\', '/');
                }
                
                string packagesPath = Path.Combine(projectRoot, assetPath);
                packagesPath = packagesPath.Replace('\\', '/');
                if (File.Exists(packagesPath))
                {
                    fullPath = Path.GetFullPath(packagesPath);
                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        fullPath = fullPath.Replace('\\', '/');
                    }
                }
                else
                {
                    // Library/PackageCache/を試す
                    fullPath = ResolvePackageCachePath(assetPath, projectRoot);
                }
            }
            else
            {
                fullPath = Path.GetFullPath(assetPath);
                if (!string.IsNullOrEmpty(fullPath))
                {
                    fullPath = fullPath.Replace('\\', '/');
                }
            }
            
            return fullPath;
        }

        private static string ResolvePackageCachePath(string assetPath, string projectRoot)
        {
            string packageName = assetPath.Split('/')[1];
            string relativePath = assetPath.Substring(assetPath.IndexOf('/', assetPath.IndexOf('/') + 1) + 1);
            string packageCacheDir = Path.Combine(projectRoot, "Library", "PackageCache");
            packageCacheDir = packageCacheDir.Replace('\\', '/');
            
            if (Directory.Exists(packageCacheDir))
            {
                string[] packageDirs = Directory.GetDirectories(packageCacheDir, packageName + "@*");
                foreach (string packageDir in packageDirs)
                {
                    string normalizedPackageDir = packageDir.Replace('\\', '/');
                    string candidatePath = Path.Combine(normalizedPackageDir, relativePath);
                    candidatePath = candidatePath.Replace('\\', '/');
                    if (File.Exists(candidatePath))
                    {
                        string fullPath = Path.GetFullPath(candidatePath);
                        if (!string.IsNullOrEmpty(fullPath))
                        {
                            return fullPath.Replace('\\', '/');
                        }
                    }
                }
            }
            
            return null;
        }

        public static string GetBundledNodePath()
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
            runtimeFolder = runtimeFolder.Replace('\\', '/');
            
            #if UNITY_EDITOR_WIN
                string nodePath = Path.Combine(runtimeFolder, "win-x64", "node.exe");
                return nodePath.Replace('\\', '/');
            #elif UNITY_EDITOR_OSX
                string nodePath = Path.Combine(runtimeFolder, "osx-x64", "node");
                nodePath = nodePath.Replace('\\', '/');
                if (File.Exists(nodePath))
                {
                    MakeExecutable(nodePath);
                }
                return nodePath;
            #elif UNITY_EDITOR_LINUX
                string nodePath = Path.Combine(runtimeFolder, "linux-x64", "node");
                nodePath = nodePath.Replace('\\', '/');
                if (File.Exists(nodePath))
                {
                    MakeExecutable(nodePath);
                }
                return nodePath;
            #else
                return null;
            #endif
        }

        private static void MakeExecutable(string filePath)
        {
            try
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "chmod";
                process.StartInfo.Arguments = "+x \"" + filePath + "\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();
            }
            catch { }
        }
    }
}

