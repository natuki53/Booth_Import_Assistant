using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BoothImportAssistant.Services
{
    /// <summary>
    /// UnityPackageのインポート処理を管理
    /// </summary>
    public class PackageImportService
    {
        private readonly Queue<string> importQueue = new Queue<string>();
        private readonly List<string> detectedPackages = new List<string>();
        private double lastDetectionTime = 0;
        private const double DETECTION_DELAY = 2.0;

        public bool HasPendingImports => importQueue.Count > 0;
        public bool HasDetectedPackages => detectedPackages.Count > 0;

        public void DetectPackage(string packagePath)
        {
            if (!detectedPackages.Contains(packagePath))
            {
                detectedPackages.Add(packagePath);
                lastDetectionTime = EditorApplication.timeSinceStartup;
            }
        }

        public bool ShouldProcessDetectedPackages()
        {
            return detectedPackages.Count > 0 && 
                   EditorApplication.timeSinceStartup - lastDetectionTime >= DETECTION_DELAY;
        }

        public List<string> GetDetectedPackages()
        {
            var result = new List<string>(detectedPackages);
            detectedPackages.Clear();
            lastDetectionTime = 0;
            return result;
        }

        public void EnqueueImport(string packagePath)
        {
            importQueue.Enqueue(packagePath);
        }

        public void EnqueueMultipleImports(IEnumerable<string> packagePaths)
        {
            foreach (var path in packagePaths)
            {
                importQueue.Enqueue(path);
            }
        }

        public bool TryProcessNextImport()
        {
            if (importQueue.Count == 0)
                return false;

            string packagePath = importQueue.Dequeue();
            ImportPackage(packagePath);
            return true;
        }

        private void ImportPackage(string packagePath)
        {
            if (!File.Exists(packagePath))
            {
                Debug.LogWarning($"[BoothBridge] パッケージファイルが見つかりません");
                return;
            }

            try
            {
                FileInfo fileInfo = new FileInfo(packagePath);
                long fileSizeMB = fileInfo.Length / 1024 / 1024;

                Debug.Log($"[BoothBridge] インポート開始: {Path.GetFileName(packagePath)} ({fileSizeMB} MB)");

                AssetDatabase.ImportPackage(packagePath, false);

                // 3秒後に削除
                string pathToDelete = packagePath;
                EditorApplication.delayCall += (EditorApplication.CallbackFunction)(() => DeletePackageDelayed(pathToDelete, 3.0));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoothBridge] インポートエラー: {ex.Message}");
                EditorUtility.DisplayDialog("インポートエラー",
                    $"UnityPackageのインポートに失敗しました。\n\n{ex.Message}",
                    "OK");
            }
        }

        private void DeletePackageDelayed(string packagePath, double delaySeconds)
        {
            double deleteTime = EditorApplication.timeSinceStartup + delaySeconds;

            EditorApplication.CallbackFunction deleteCallback = null;
            deleteCallback = () =>
            {
                if (EditorApplication.timeSinceStartup >= deleteTime)
                {
                    EditorApplication.update -= deleteCallback;

                    try
                    {
                        if (File.Exists(packagePath))
                        {
                            File.Delete(packagePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[BoothBridge] パッケージ削除失敗: {ex.Message}");
                    }
                }
            };

            EditorApplication.update += deleteCallback;
        }
    }
}

