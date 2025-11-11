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

        // 複数ダウンロード用の選択状態
        private Dictionary<string, int> selectedDownloadIndex = new Dictionary<string, int>();

        // 遅延再読み込み用
        private double jsonChangeTime = 0;

        // イベント
        public event Action OnDataChanged;
        public event Action OnShowUpdateNotification;

        public IReadOnlyList<BoothAsset> Assets => repository.Assets;
        public ProgressInfo CurrentProgress => bridge.CurrentProgress;

        public BoothLibraryPresenter(string projectPath)
        {
            this.projectPath = projectPath;

            // サービスの初期化
            repository = new BoothAssetRepository(projectPath);
            thumbnailCache = new ThumbnailCacheService(projectPath);
            fileWatcher = new FileWatcherService();
            packageImport = new PackageImportService();
            bridge = new BridgeService();

            // イベント接続
            repository.OnAssetsChanged += () => OnDataChanged?.Invoke();
            
            fileWatcher.OnJsonFileChanged += HandleJsonFileChanged;
            fileWatcher.OnPackageFileCreated += HandlePackageDetected;
            
            bridge.OnProgressUpdated += _ => OnDataChanged?.Invoke();

            // 初期化
            Initialize();
        }

        private void Initialize()
        {
            repository.LoadAssets();

            string jsonPath = repository.GetJsonFilePath();
            string packagePath = Path.Combine(projectPath, "BoothBridge", "temp");

            fileWatcher.StartWatchingJson(jsonPath);
            fileWatcher.StartWatchingPackages(packagePath);
        }

        private void HandleJsonFileChanged()
        {
            // デバウンス（200ms後に再読み込み）
            jsonChangeTime = EditorApplication.timeSinceStartup + 0.2;
        }

        private void HandlePackageDetected(string packagePath)
        {
            packageImport.DetectPackage(packagePath);
        }

        public void Update()
        {
            // JSON変更チェック
            if (jsonChangeTime > 0 && EditorApplication.timeSinceStartup >= jsonChangeTime)
            {
                jsonChangeTime = 0;
                repository.LoadAssets();
                OnShowUpdateNotification?.Invoke();
            }

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
            if (Directory.Exists(tempPackagePath))
            {
                string[] allPackages = Directory.GetFiles(tempPackagePath, "*.unitypackage");
                
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
                    }
                }
            }
        }

        public void SyncWithBooth()
        {
            if (bridge.IsBridgeRunning())
            {
                bridge.StopBridge();
                // StopBridge()内でWaitForExit()を呼んでいるため、追加の待機は不要
            }

            bool started = bridge.StartBridge();
            if (!started) return;

            EditorUtility.DisplayProgressBar("同期中", "Bridgeを起動しています...", 0.3f);
            System.Threading.Thread.Sleep(3000);

            EditorUtility.DisplayProgressBar("同期中", "BOOTHページを開いています...", 0.6f);
            // 購入ページから同期を開始（購入とギフト両方を取得）
            Application.OpenURL("https://accounts.booth.pm/library?sync=true");

            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog("同期開始",
                "BOOTHページが開きました。\n\nページ読み込み完了後、購入した商品とギフトを自動的に同期します。\n完了まで数秒お待ちください。",
                "OK");
        }

        public void ReloadAssets()
        {
            repository.LoadAssets();
            thumbnailCache.Clear();
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
            // Bridgeが起動していることを確認
            if (!bridge.IsBridgeRunning())
            {
                bool started = bridge.StartBridge();
                if (!started)
                {
                    EditorUtility.DisplayDialog("エラー",
                        "Bridgeが起動していません。\n同期を実行してください。",
                        "OK");
                    return;
                }
            }

            // ダウンロードURLがある場合は直接開く
            if (asset.downloadUrls != null &&
                downloadIndex >= 0 &&
                downloadIndex < asset.downloadUrls.Length &&
                !string.IsNullOrEmpty(asset.downloadUrls[downloadIndex].url))
            {
                string downloadUrl = asset.downloadUrls[downloadIndex].url;
                string label = asset.downloadUrls[downloadIndex].label;

                Application.OpenURL(downloadUrl);

                EditorUtility.DisplayDialog("ダウンロード",
                    "ダウンロードページが開きました。\n\n" +
                    "対象: " + label + "\n\n" +
                    "ダウンロード完了後、自動的にUnityに展開されます。",
                    "OK");
            }
            else
            {
                // ダウンロードURLがない場合は商品ページを開く
                Application.OpenURL(asset.productUrl);

                EditorUtility.DisplayDialog("ダウンロード",
                    "商品ページが開きました。\n\n" +
                    "BOOTHからダウンロードしてください。\n" +
                    "ダウンロード完了後、自動的にUnityに展開されます。",
                    "OK");
            }
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

        public void Dispose()
        {
            fileWatcher?.Dispose();
            thumbnailCache?.Clear();
        }
    }
}

