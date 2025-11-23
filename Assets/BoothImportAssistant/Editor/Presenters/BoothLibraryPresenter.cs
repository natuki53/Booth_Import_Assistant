using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BoothImportAssistant.Models;
using BoothImportAssistant.Services;
using BoothImportAssistant.UI;

namespace BoothImportAssistant.Presenters
{
    /// <summary>
    /// BoothLibraryWindowのビジネスロジックを管理
    /// </summary>
    public class BoothLibraryPresenter : IDisposable
    {
        private readonly string projectPath;
        private readonly BoothAssetRepository repository;
        private readonly ThumbnailCacheService thumbnailCache;
        private readonly FileWatcherService fileWatcher;
        private readonly PackageImportService packageImport;
        private readonly BridgeService bridge;
        private readonly DownloadManagerService downloadManager;

        // 複数ダウンロード用の選択状態
        private Dictionary<string, int> selectedDownloadIndex = new Dictionary<string, int>();

        private const string DOWNLOAD_MODE_DOWNLOAD_ONLY = "download-only";
        private const string DOWNLOAD_MODE_DL_IMPORT = "dl-import";

        // イベント
        public event Action OnDataChanged;
        public event Action OnShowUpdateNotification;

        public IReadOnlyList<BoothAsset> Assets => repository.Assets;
        public ProgressInfo CurrentProgress => bridge.CurrentProgress;
        public DateTime? LastUpdated => repository.LastUpdated;

        public BoothLibraryPresenter(string projectPath)
        {
            this.projectPath = projectPath;

            // サービスの初期化
            repository = new BoothAssetRepository(projectPath);
            thumbnailCache = new ThumbnailCacheService(projectPath);
            fileWatcher = new FileWatcherService();
            packageImport = new PackageImportService();
            bridge = new BridgeService();
            downloadManager = new DownloadManagerService(projectPath);

            // イベント接続
            repository.OnAssetsChanged += () => OnDataChanged?.Invoke();
            fileWatcher.OnJsonFileChanged += HandleJsonFileChanged;
            fileWatcher.OnPackageFileCreated += HandlePackageDetected;
            fileWatcher.OnDownloadsMetadataChanged += HandleDownloadsMetadataChanged;
            
            bridge.OnProgressUpdated += _ => OnDataChanged?.Invoke();

            // 初期化
            Initialize();
        }

        private void Initialize()
        {
            repository.LoadAssets();

            string jsonPath = repository.GetJsonFilePath();
            string packagePath = Path.Combine(projectPath, "BoothBridge", "temp");
            packagePath = packagePath.Replace('\\', '/');

            fileWatcher.StartWatchingJson(jsonPath);
            fileWatcher.StartWatchingPackages(packagePath);
            fileWatcher.StartWatchingDownloadsMetadata(downloadManager.MetadataFilePath);
        }

        private void HandleJsonFileChanged()
        {
            bool loaded = repository.LoadAssets();
            if (loaded)
            {
                OnShowUpdateNotification?.Invoke();
            }
        }

        private void HandlePackageDetected(string packagePath)
        {
            packageImport.DetectPackage(packagePath);
        }

        private void HandleDownloadsMetadataChanged()
        {
            downloadManager.ReloadMetadata();
            OnDataChanged?.Invoke();
        }

        public void Update()
        {
            // 進捗チェック
            bridge.CheckProgressAsync();

            // パッケージインポート処理
            if (packageImport.ShouldProcessDetectedPackages())
            {
                ProcessDetectedPackages();
            }

            if (packageImport.HasPendingImports)
            {
                packageImport.TryProcessNextImport();
            }
        }

        private void ProcessDetectedPackages()
        {
            List<string> packages = packageImport.GetDetectedPackages();
            
            // tempフォルダ内のすべての.unitypackageファイルをスキャン
            string tempPackagePath = Path.Combine(projectPath, "BoothBridge", "temp");
            tempPackagePath = tempPackagePath.Replace('\\', '/');
            if (Directory.Exists(tempPackagePath))
            {
                string[] allPackages = Directory.GetFiles(tempPackagePath, "*.unitypackage");
                // パスを正規化（バックスラッシュをスラッシュに変換）
                for (int i = 0; i < allPackages.Length; i++)
                {
                    allPackages[i] = allPackages[i].Replace('\\', '/');
                }
                
                // 検出されたパッケージと実際のファイルを比較
                List<string> packagesToImport = new List<string>();
                
                // 検出されたパッケージが存在する場合はそれを使用
                if (packages.Count > 0)
                {
                    foreach (string detectedPackage in packages)
                    {
                        if (File.Exists(detectedPackage))
                        {
                            packagesToImport.Add(detectedPackage);
                        }
                    }
                }
                else
                {
                    // 検出リストが空の場合は、すべてのパッケージを使用
                    packagesToImport.AddRange(allPackages);
                }
                
                if (packagesToImport.Count > 0)
                {
                    // 複数パッケージがある場合はダイアログを表示
                    if (packagesToImport.Count > 1)
                    {
                        PackageImportDialog.ShowDialog(packagesToImport, (selectedPackages) =>
                        {
                            packageImport.EnqueueMultipleImports(selectedPackages);
                        }, (cancelledPackages) =>
                        {
                            // キャンセル時にファイルを削除
                            DeletePackages(cancelledPackages);
                        });
                    }
                    else
                    {
                        // 単一パッケージの場合は確認ダイアログを表示
                        string packageName = Path.GetFileName(packagesToImport[0]);
                        if (EditorUtility.DisplayDialog(
                            "UnityPackageをインポートしますか？",
                            $"以下のパッケージをインポートしますか？\n\n{packageName}",
                            "インポート", "キャンセル"))
                        {
                            packageImport.EnqueueImport(packagesToImport[0]);
                        }
                        else
                        {
                            // キャンセル時にファイルを削除
                            DeletePackages(packagesToImport);
                        }
                    }
                }
            }
        }

        private const string CONSENT_PREF_KEY = "BoothImportAssistant_ConsentGiven";

        public bool SyncWithBooth()
        {
            // 初回利用時の同意確認（同意後にbridgeを起動するため、先に確認）
            if (!HasUserConsent())
            {
                if (!ShowConsentDialog())
                {
                    // ユーザーが同意しなかった場合は処理を中断（bridgeは起動しない）
                    return false;
                }
                // 同意した場合はEditorPrefsに保存
                EditorPrefs.SetBool(CONSENT_PREF_KEY, true);
            }

            // 同意確認後にbridgeを起動
            if (bridge.IsBridgeRunning())
            {
                bridge.StopBridge();
                // StopBridge()内でWaitForExit()を呼んでいるため、追加の待機は不要
            }

            bool started = bridge.StartBridge();
            if (!started) return false;

            EditorUtility.DisplayProgressBar("同期中", "Bridgeを起動しています...", 0.3f);
            System.Threading.Thread.Sleep(3000);

            EditorUtility.DisplayProgressBar("同期中", "BOOTHページを開いています...", 0.6f);
            // 購入ページから同期を開始（購入とギフト両方を取得）
            Application.OpenURL("https://accounts.booth.pm/library?sync=true");

            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog("同期開始",
                "BOOTHページが開きました。\n\nページ読み込み完了後、購入した商品とギフトを自動的に同期します。\n完了まで数秒お待ちください。",
                "OK");

            return true;
        }

        private bool HasUserConsent()
        {
            return EditorPrefs.GetBool(CONSENT_PREF_KEY, false);
        }

        private bool ShowConsentDialog()
        {
            string message = 
                "【重要】本ツールは非公式ツールです\n\n" +
                "本ツールはBOOTH公式が提供するものではなく、非公式のツールです。\n" +
                "使用にあたっては、すべて自己責任で行ってください。\n\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                "本ツールは、BOOTHのライブラリページから以下の情報を取得します：\n\n" +
                "・購入した商品の情報（商品名、作者、サムネイルなど）\n" +
                "・ギフトで受け取った商品の情報\n" +
                "・ダウンロードリンク\n\n" +
                "【重要な注意事項】\n" +
                "・取得した情報はすべてローカル（localhost）で処理されます\n" +
                "・外部のサーバーには一切送信されません\n" +
                "・ユーザー自身の購入ライブラリページのみにアクセスします\n" +
                "・ログインが必要なページにアクセスするため、ブラウザでログインしている必要があります\n\n" +
                "このツールを使用することで、上記の情報取得に同意したものとみなされます。\n\n" +
                "同意して続行しますか？";

            return EditorUtility.DisplayDialog(
                "利用規約への同意",
                message,
                "同意する",
                "キャンセル"
            );
        }

        public bool ReloadAssets()
        {
            bool loaded = repository.LoadAssets();
            thumbnailCache.Clear();
            return loaded;
        }

        public void StopBridge()
        {
            bridge.StopBridge();
        }

        public bool IsBridgeRunning()
        {
            return bridge.IsBridgeRunning();
        }

        public Texture2D GetThumbnail(BoothAsset asset)
        {
            return thumbnailCache.GetThumbnail(asset);
        }

        public void DownloadAsset(BoothAsset asset, int downloadIndex)
        {
            if (!EnsureBridgeRunning())
            {
                return;
            }

            var downloadInfo = GetDownloadInfo(asset, downloadIndex);
            if (downloadInfo != null && !string.IsNullOrEmpty(downloadInfo.url))
            {
                bridge.RegisterDownloadMode(downloadInfo.url, downloadInfo.label, asset.id, downloadInfo.isMaterial, DOWNLOAD_MODE_DL_IMPORT);
                Application.OpenURL(downloadInfo.url);

                EditorUtility.DisplayDialog("ダウンロード",
                    "ダウンロードページが開きました。\n\n" +
                    $"対象: {downloadInfo.label}\n\n" +
                    "ダウンロード完了後、自動的にUnityに展開されます。",
                    "OK");
            }
            else
            {
                Application.OpenURL(asset.productUrl);
                EditorUtility.DisplayDialog("ダウンロード",
                    "商品ページが開きました。\n\n" +
                    "BOOTHからダウンロードしてください。\n" +
                    "ダウンロード完了後、自動的にUnityに展開されます。",
                    "OK");
            }
        }

        public void DownloadOnly(BoothAsset asset, int downloadIndex)
        {
            if (!EnsureBridgeRunning())
            {
                return;
            }

            var downloadInfo = GetDownloadInfo(asset, downloadIndex);
            if (downloadInfo == null || string.IsNullOrEmpty(downloadInfo.url))
            {
                EditorUtility.DisplayDialog("ダウンロード",
                    "ダウンロードURLが見つかりませんでした。商品ページを開きます。",
                    "OK");
                Application.OpenURL(asset.productUrl);
                return;
            }

            bridge.RegisterDownloadMode(downloadInfo.url, downloadInfo.label, asset.id, downloadInfo.isMaterial, DOWNLOAD_MODE_DOWNLOAD_ONLY);
            Application.OpenURL(downloadInfo.url);

            EditorUtility.DisplayDialog("ダウンロードのみ",
                $"{downloadInfo.label} のZIPを保存します。\n\n" +
                "ダウンロード完了後、自動的に保存フォルダへ移動します。\nインポート確認は表示されません。",
                "OK");
        }

        public void ImportFromSavedFile(BoothAsset asset, int downloadIndex)
        {
            if (asset?.downloadUrls == null)
                return;

            var info = GetDownloadInfo(asset, downloadIndex);
            if (info == null)
                return;

            var saved = downloadManager.FindDownloadedFile(asset.id, info.label, info.isMaterial);
            if (saved == null)
            {
                EditorUtility.DisplayDialog("インポート", "保存済みファイルが見つかりませんでした。", "OK");
                return;
            }

            bool success = downloadManager.ImportFromDownloadedFile(saved);
            if (!success)
            {
                return;
            }

            EditorUtility.DisplayDialog("インポート",
                $"{saved.originalFileName} からUnityPackageを検出しました。\n既存のインポートフローに従って処理されます。",
                "OK");
        }

        public void OverwriteDownload(BoothAsset asset, int downloadIndex)
        {
            if (!EnsureBridgeRunning())
            {
                return;
            }

            var info = GetDownloadInfo(asset, downloadIndex);
            if (info == null || string.IsNullOrEmpty(info.url))
            {
                EditorUtility.DisplayDialog("上書きダウンロード",
                    "ダウンロードURLが見つかりませんでした。商品ページを開きます。",
                    "OK");
                Application.OpenURL(asset?.productUrl);
                return;
            }

            downloadManager.RemoveDownloadedFile(asset.id, info.label, info.isMaterial, true);

            bridge.RegisterDownloadMode(info.url, info.label, asset.id, info.isMaterial, DOWNLOAD_MODE_DOWNLOAD_ONLY);
            Application.OpenURL(info.url);

            EditorUtility.DisplayDialog("上書きダウンロード",
                $"{info.label} のZIPを再ダウンロードします。\nダウンロード完了後、保存先に上書きされます。",
                "OK");
        }

        public bool HasSavedFile(BoothAsset asset, int downloadIndex)
        {
            var info = GetDownloadInfo(asset, downloadIndex);
            if (info == null)
                return false;

            return downloadManager.HasDownloadedFile(asset.id, info.label, info.isMaterial);
        }

        public (int downloaded, int total) GetDownloadStatus(BoothAsset asset)
        {
            if (asset?.downloadUrls == null || asset.downloadUrls.Length == 0)
            {
                return (0, 0);
            }

            int total = asset.downloadUrls.Length;
            int downloaded = 0;
            foreach (var url in asset.downloadUrls)
            {
                if (downloadManager.HasDownloadedFile(asset.id, url.label, url.isMaterial))
                {
                    downloaded++;
                }
            }

            return (downloaded, total);
        }

        public bool IsSelectedFileDownloaded(BoothAsset asset, int downloadIndex)
        {
            var info = GetDownloadInfo(asset, downloadIndex);
            if (info == null)
                return false;

            return downloadManager.HasDownloadedFile(asset.id, info.label, info.isMaterial);
        }

        public List<bool> GetMaterialDownloadStatus(BoothAsset asset)
        {
            List<bool> result = new List<bool>();
            if (asset?.downloadUrls == null)
                return result;

            foreach (var url in asset.downloadUrls.Where(u => u.isMaterial))
            {
                bool hasFile = downloadManager.HasDownloadedFile(asset.id, url.label, true);
                result.Add(hasFile);
            }

            return result;
        }

        public void OpenDownloadFolder(BoothAsset asset)
        {
            if (asset == null) return;
            downloadManager.OpenDownloadFolder(asset.id);
        }

        public IReadOnlyList<DownloadedFileInfo> GetDownloadedFiles(BoothAsset asset)
        {
            if (asset == null)
            {
                return Array.Empty<DownloadedFileInfo>();
            }

            return downloadManager.GetDownloadedFiles(asset.id);
        }

        public int GetSelectedDownloadIndex(string assetId)
        {
            if (!selectedDownloadIndex.ContainsKey(assetId))
            {
                selectedDownloadIndex[assetId] = 0;
            }
            return selectedDownloadIndex[assetId];
        }

        public void SetSelectedDownloadIndex(string assetId, int index)
        {
            selectedDownloadIndex[assetId] = index;
        }

        /// <summary>
        /// パッケージファイルを削除
        /// </summary>
        private void DeletePackages(List<string> packagePaths)
        {
            foreach (string packagePath in packagePaths)
            {
                try
                {
                    if (File.Exists(packagePath))
                    {
                        File.Delete(packagePath);
                        Debug.Log($"[BoothBridge] パッケージファイルを削除しました: {Path.GetFileName(packagePath)}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BoothBridge] パッケージファイル削除失敗: {Path.GetFileName(packagePath)} - {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            fileWatcher?.Dispose();
            thumbnailCache?.Clear();
        }

        private bool EnsureBridgeRunning()
        {
            if (!bridge.IsBridgeRunning())
            {
                bool started = bridge.StartBridge();
                if (!started)
                {
                    EditorUtility.DisplayDialog("エラー",
                        "Bridgeが起動していません。\n同期を実行してください。",
                        "OK");
                    return false;
                }
            }

            return true;
        }

        private DownloadUrl GetDownloadInfo(BoothAsset asset, int index)
        {
            if (asset?.downloadUrls == null || index < 0 || index >= asset.downloadUrls.Length)
                return null;

            return asset.downloadUrls[index];
        }
    }
}

