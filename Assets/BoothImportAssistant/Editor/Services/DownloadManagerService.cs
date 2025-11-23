using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BoothImportAssistant.Models;

namespace BoothImportAssistant.Services
{
    /// <summary>
    /// ダウンロード済みZIPファイルの保存とインポートを管理
    /// </summary>
    public class DownloadManagerService
    {
        private readonly string downloadsRoot;
        private readonly string downloadsContainer;
        private readonly string metadataFilePath;
        private readonly string tempPackageDirectory;
        private readonly object metadataLock = new object();
        private DownloadedFileMetadata metadataCache;

        public string MetadataFilePath => metadataFilePath;
        public string DownloadsRoot => downloadsRoot;

        public DownloadManagerService(string projectPath)
        {
            downloadsRoot = ResolveDownloadsRoot();
            downloadsContainer = Path.Combine(downloadsRoot, "downloads");
            metadataFilePath = Path.Combine(downloadsRoot, "downloads_metadata.json").Replace('\\', '/');
            tempPackageDirectory = Path.Combine(projectPath, "BoothBridge", "temp").Replace('\\', '/');

            Directory.CreateDirectory(downloadsRoot);
            Directory.CreateDirectory(downloadsContainer);
            Directory.CreateDirectory(tempPackageDirectory);

            LoadMetadata();
        }

        /// <summary>
        /// ZIPファイルを保存先に移動し、メタデータを更新
        /// </summary>
        public bool SaveDownloadedFile(string assetId, string label, string sourceFilePath, bool isMaterial)
        {
            if (string.IsNullOrEmpty(assetId) || string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return false;
            }

            try
            {
                string assetDirectory = GetAssetDownloadDirectory(assetId);
                Directory.CreateDirectory(assetDirectory);

                string originalName = Path.GetFileName(sourceFilePath);
                string destinationPath = Path.Combine(assetDirectory, originalName);
                destinationPath = EnsureUniqueFilePath(destinationPath);

                File.Move(sourceFilePath, destinationPath);

                var info = new DownloadedFileInfo
                {
                    assetId = assetId,
                    label = label ?? Path.GetFileNameWithoutExtension(destinationPath),
                    zipFilePath = NormalizePath(destinationPath),
                    originalFileName = Path.GetFileName(destinationPath),
                    downloadedAt = DateTime.UtcNow.ToString("o"),
                    isMaterial = isMaterial
                };

                UpsertMetadata(info);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoothBridge] ZIP保存エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// メタデータを再読み込み
        /// </summary>
        public void ReloadMetadata()
        {
            LoadMetadata();
        }

        /// <summary>
        /// 指定アセットの保存済みファイル一覧を取得
        /// </summary>
        public List<DownloadedFileInfo> GetDownloadedFiles(string assetId)
        {
            lock (metadataLock)
            {
                PruneMissingEntriesLocked();

                return metadataCache.downloads
                    .Where(d => d.assetId == assetId)
                    .Select(CloneInfo)
                    .ToList();
            }
        }

        /// <summary>
        /// ラベルとマテリアル種別で保存済みファイルを検索
        /// </summary>
        public DownloadedFileInfo FindDownloadedFile(string assetId, string label, bool isMaterial)
        {
            lock (metadataLock)
            {
                var entry = metadataCache.downloads
                    .FirstOrDefault(d =>
                        d.assetId == assetId &&
                        string.Equals(d.label, label, StringComparison.OrdinalIgnoreCase) &&
                        d.isMaterial == isMaterial);

                if (entry == null)
                {
                    return null;
                }

                if (IsEntryMissing(entry))
                {
                    metadataCache.downloads.Remove(entry);
                    SaveMetadataLocked();
                    return null;
                }

                return CloneInfo(entry);
            }
        }

        /// <summary>
        /// 保存済みファイルの有無を確認
        /// </summary>
        public bool HasDownloadedFile(string assetId, string label, bool isMaterial)
        {
            return FindDownloadedFile(assetId, label, isMaterial) != null;
        }

        /// <summary>
        /// 保存済みエントリを手動削除
        /// </summary>
        public bool RemoveDownloadedFile(string assetId, string label, bool isMaterial, bool deleteFromDisk)
        {
            lock (metadataLock)
            {
                var entry = metadataCache.downloads
                    .FirstOrDefault(d =>
                        d.assetId == assetId &&
                        string.Equals(d.label, label, StringComparison.OrdinalIgnoreCase) &&
                        d.isMaterial == isMaterial);

                if (entry == null)
                {
                    return false;
                }

                if (deleteFromDisk && !string.IsNullOrEmpty(entry.zipFilePath))
                {
                    try
                    {
                        if (File.Exists(entry.zipFilePath))
                        {
                            File.Delete(entry.zipFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[BoothBridge] ZIPファイル削除失敗: {ex.Message}");
                    }
                }

                metadataCache.downloads.Remove(entry);
                SaveMetadataLocked();
                return true;
            }
        }

        /// <summary>
        /// 保存先フォルダを開く
        /// </summary>
        public void OpenDownloadFolder(string assetId)
        {
            string directory = GetAssetDownloadDirectory(assetId);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            OpenFolderInExplorer(directory);
        }

        /// <summary>
        /// 保存済みZIPから.unitypackageを抽出し、tempフォルダにコピー
        /// </summary>
        public bool ImportFromDownloadedFile(DownloadedFileInfo fileInfo)
        {
            if (fileInfo == null || string.IsNullOrEmpty(fileInfo.zipFilePath))
            {
                return false;
            }

            string zipPath = fileInfo.zipFilePath.Replace('\\', '/');
            if (!File.Exists(zipPath))
            {
                EditorUtility.DisplayDialog("インポートエラー", "保存済みZIPファイルが見つかりません。", "OK");
                RemoveDownloadedFile(fileInfo.assetId, fileInfo.label, fileInfo.isMaterial, false);
                return false;
            }

            string tempExtractPath = Path.Combine(Path.GetTempPath(), $"booth_saved_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(tempExtractPath);
                ZipFile.ExtractToDirectory(zipPath, tempExtractPath, true);

                string[] unityPackages = Directory.GetFiles(tempExtractPath, "*.unitypackage", SearchOption.AllDirectories);
                if (unityPackages.Length == 0)
                {
                    EditorUtility.DisplayDialog("インポートエラー", ".unitypackageファイルが見つかりませんでした。", "OK");
                    return false;
                }

                foreach (var packagePath in unityPackages)
                {
                    string destination = Path.Combine(tempPackageDirectory, Path.GetFileName(packagePath));
                    destination = EnsureUniqueFilePath(destination);
                    File.Copy(packagePath, destination, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoothBridge] 保存済みZIPの展開エラー: {ex.Message}");
                EditorUtility.DisplayDialog("インポートエラー", $"ZIPの展開に失敗しました。\n{ex.Message}", "OK");
                return false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempExtractPath))
                    {
                        Directory.Delete(tempExtractPath, true);
                    }
                }
                catch (Exception cleanupEx)
                {
                    Debug.LogWarning($"[BoothBridge] 一時フォルダ削除失敗: {cleanupEx.Message}");
                }
            }
        }

        public string GetAssetDownloadDirectory(string assetId)
        {
            string safeId = string.IsNullOrEmpty(assetId) ? "unknown" : assetId;
            return Path.Combine(downloadsContainer, safeId).Replace('\\', '/');
        }

        private void LoadMetadata()
        {
            lock (metadataLock)
            {
                if (!File.Exists(metadataFilePath))
                {
                    metadataCache = new DownloadedFileMetadata();
                    SaveMetadataLocked();
                    return;
                }

                try
                {
                    string json = File.ReadAllText(metadataFilePath);
                    metadataCache = JsonUtility.FromJson<DownloadedFileMetadata>(json);
                    if (metadataCache == null)
                    {
                        metadataCache = new DownloadedFileMetadata();
                    }
                    else if (metadataCache.downloads == null)
                    {
                        metadataCache.downloads = new List<DownloadedFileInfo>();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BoothBridge] メタデータ読み込み失敗: {ex.Message}");
                    metadataCache = new DownloadedFileMetadata();
                }

                PruneMissingEntriesLocked();
            }
        }

        private void UpsertMetadata(DownloadedFileInfo info)
        {
            lock (metadataLock)
            {
                metadataCache.downloads.RemoveAll(d =>
                    d.assetId == info.assetId &&
                    string.Equals(d.label, info.label, StringComparison.OrdinalIgnoreCase) &&
                    d.isMaterial == info.isMaterial);

                metadataCache.downloads.Add(CloneInfo(info));
                SaveMetadataLocked();
            }
        }

        private void SaveMetadataLocked()
        {
            try
            {
                string json = JsonUtility.ToJson(metadataCache, true);
                File.WriteAllText(metadataFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoothBridge] メタデータ保存失敗: {ex.Message}");
            }
        }

        private static string EnsureUniqueFilePath(string path)
        {
            string directory = Path.GetDirectoryName(path);
            string filename = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string candidate = path;
            int counter = 1;

            while (File.Exists(candidate))
            {
                candidate = Path.Combine(directory ?? string.Empty, $"{filename}({counter}){extension}");
                counter++;
            }

            return candidate.Replace('\\', '/');
        }

        private static string NormalizePath(string path)
        {
            return path?.Replace('\\', '/');
        }

        private static DownloadedFileInfo CloneInfo(DownloadedFileInfo info)
        {
            return new DownloadedFileInfo
            {
                assetId = info.assetId,
                label = info.label,
                zipFilePath = info.zipFilePath,
                originalFileName = info.originalFileName,
                downloadedAt = info.downloadedAt,
                isMaterial = info.isMaterial
            };
        }

        private void PruneMissingEntriesLocked()
        {
            if (metadataCache?.downloads == null)
            {
                return;
            }

            bool removed = false;
            for (int i = metadataCache.downloads.Count - 1; i >= 0; i--)
            {
                var entry = metadataCache.downloads[i];
                if (IsEntryMissing(entry))
                {
                    metadataCache.downloads.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
            {
                SaveMetadataLocked();
            }
        }

        private static bool IsEntryMissing(DownloadedFileInfo entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.zipFilePath))
            {
                return true;
            }

            return !File.Exists(entry.zipFilePath);
        }

        private static string ResolveDownloadsRoot()
        {
#if UNITY_EDITOR_WIN
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Booth_Import_Assistant");
#elif UNITY_EDITOR_OSX
            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return Path.Combine(home, "Library", "Application Support", "Booth_Import_Assistant");
#else
            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return Path.Combine(home, ".config", "Booth_Import_Assistant");
#endif
        }

        private static void OpenFolderInExplorer(string directory)
        {
#if UNITY_EDITOR_WIN
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{directory.Replace('/', '\\')}\"",
                UseShellExecute = false
            });
#elif UNITY_EDITOR_OSX
            System.Diagnostics.Process.Start("open", directory);
#else
            System.Diagnostics.Process.Start("xdg-open", directory);
#endif
        }
    }
}

