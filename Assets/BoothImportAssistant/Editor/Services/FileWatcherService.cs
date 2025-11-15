using System;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace BoothImportAssistant.Services
{
    /// <summary>
    /// ファイル変更の監視サービス
    /// </summary>
    public class FileWatcherService : IDisposable
    {
        private FileSystemWatcher jsonWatcher;
        private FileSystemWatcher packageWatcher;
        private string jsonFilePath;
        private string jsonDirectory;
        private string jsonFilename;
        
        // デバウンス用
        private double lastChangeTime = 0;
        private const double DEBOUNCE_DELAY = 0.5; // 500ms

        public event Action OnJsonFileChanged;
        public event Action<string> OnPackageFileCreated;

        public void StartWatchingJson(string jsonFilePath)
        {
            this.jsonFilePath = jsonFilePath.Replace('\\', '/');
            jsonDirectory = Path.GetDirectoryName(this.jsonFilePath);
            jsonDirectory = jsonDirectory?.Replace('\\', '/') ?? jsonDirectory;
            jsonFilename = Path.GetFileName(this.jsonFilePath);

            if (!Directory.Exists(jsonDirectory))
            {
                // ディレクトリが存在しない場合は作成を試みる
                try
                {
                    Directory.CreateDirectory(jsonDirectory);
                }
                catch
                {
                    // ディレクトリ作成に失敗した場合は監視を開始できない
                    return;
                }
            }

            // ファイルが存在しない場合でも、ディレクトリを監視してファイル作成を検知
            jsonWatcher = new FileSystemWatcher(jsonDirectory, jsonFilename)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };
            
            jsonWatcher.Changed += (sender, e) =>
            {
                if (File.Exists(jsonFilePath))
                {
                    HandleJsonChange();
                }
            };
            
            jsonWatcher.Created += (sender, e) =>
            {
                string fullPath = Path.Combine(jsonDirectory, e.Name);
                fullPath = fullPath.Replace('\\', '/');
                if (fullPath.Equals(jsonFilePath, StringComparison.OrdinalIgnoreCase) || 
                    e.Name.Equals(jsonFilename, StringComparison.OrdinalIgnoreCase))
                {
                    HandleJsonChange();
                }
            };
            
            jsonWatcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// JSONファイル変更を処理（デバウンス付き）
        /// </summary>
        private void HandleJsonChange()
        {
            // FileSystemWatcherのイベントは別スレッドで実行されるため、
            // Unityのメインスレッドで処理するようにディスパッチ
            EditorApplication.delayCall += () =>
            {
                double currentTime = EditorApplication.timeSinceStartup;
                
                // デバウンス：最後の変更から500ms以内の場合は無視
                if (currentTime - lastChangeTime < DEBOUNCE_DELAY)
                {
                    return;
                }
                
                lastChangeTime = currentTime;
                
                // ファイルが書き込まれるまで少し待機（シンプルな待機）
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(300); // 300ms待機
                    
                    // Unityのメインスレッドでイベントを発火
                    EditorApplication.delayCall += () =>
                    {
                        OnJsonFileChanged?.Invoke();
                    };
                });
            };
        }

        public void StartWatchingPackages(string packageDirectory)
        {
            packageDirectory = packageDirectory?.Replace('\\', '/') ?? packageDirectory;
            if (!Directory.Exists(packageDirectory))
            {
                Directory.CreateDirectory(packageDirectory);
            }

            packageWatcher = new FileSystemWatcher(packageDirectory, "*.unitypackage")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = true
            };
            packageWatcher.Created += (sender, e) =>
            {
                System.Threading.Thread.Sleep(500);
                string normalizedPath = e.FullPath.Replace('\\', '/');
                OnPackageFileCreated?.Invoke(normalizedPath);
            };
            packageWatcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            if (jsonWatcher != null)
            {
                jsonWatcher.EnableRaisingEvents = false;
                jsonWatcher.Dispose();
                jsonWatcher = null;
            }
            
            if (packageWatcher != null)
            {
                packageWatcher.EnableRaisingEvents = false;
                packageWatcher.Dispose();
                packageWatcher = null;
            }
        }
    }
}

