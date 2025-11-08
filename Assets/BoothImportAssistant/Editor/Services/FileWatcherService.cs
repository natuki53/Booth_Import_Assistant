using System;
using System.IO;
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

        public event Action OnJsonFileChanged;
        public event Action<string> OnPackageFileCreated;

        public void StartWatchingJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
                return;

            string directory = Path.GetDirectoryName(jsonFilePath);
            string filename = Path.GetFileName(jsonFilePath);

            if (!Directory.Exists(directory))
                return;

            jsonWatcher = new FileSystemWatcher(directory, filename)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            jsonWatcher.Changed += (sender, e) => OnJsonFileChanged?.Invoke();
            jsonWatcher.EnableRaisingEvents = true;
        }

        public void StartWatchingPackages(string packageDirectory)
        {
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
                OnPackageFileCreated?.Invoke(e.FullPath);
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

