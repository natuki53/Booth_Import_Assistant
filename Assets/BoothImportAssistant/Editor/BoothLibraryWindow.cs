using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace BoothImportAssistant
{
    /// <summary>
    /// BOOTH Library表示ウィンドウ
    /// </summary>
    public class BoothLibraryWindow : EditorWindow
    {
        private List<BoothAsset> assets = new List<BoothAsset>();
        private Vector2 scrollPosition;
        private FileSystemWatcher fileWatcher;
        private FileSystemWatcher packageWatcher;
        private bool needsReload = false;
        private double reloadTime = 0;
        private bool showUpdateNotification = false;
        private double notificationEndTime = 0;
        private string jsonFilePath;
        private Texture2D placeholderIcon;
        private Queue<string> pendingPackageImports = new Queue<string>();
        private List<string> detectedPackages = new List<string>();
        private double lastPackageDetectionTime = 0;
        private const double PACKAGE_DETECTION_DELAY = 2.0; // 2秒待機してから複数パッケージをスキャン
        
        // 複数ダウンロード用の選択状態
        private Dictionary<string, int> selectedDownloadIndex = new Dictionary<string, int>();
        private Dictionary<string, bool> showDownloadOptions = new Dictionary<string, bool>();
        
        // サムネイルキャッシュ（パフォーマンス改善）
        private Dictionary<string, Texture2D> thumbnailCache = new Dictionary<string, Texture2D>();
        
        // リアルタイム更新用
        private double lastRepaintTime = 0;
        
        // 進捗情報
        private ProgressInfo currentProgress = null;
        private bool isCheckingProgress = false;

        [MenuItem("Tools/BOOTH Library")]
        public static void ShowWindow()
        {
            var window = GetWindow<BoothLibraryWindow>("BOOTH Library");
            window.minSize = new Vector2(500, 300); // 最小ウィンドウサイズを設定
            window.Show();
        }

        private void OnEnable()
        {
            // プロジェクトパス取得
            string projectPath = GetProjectPath();
            if (string.IsNullOrEmpty(projectPath))
            {
                Debug.LogWarning("[BoothBridge] プロジェクトパスを取得できません");
                return;
            }

            jsonFilePath = Path.Combine(projectPath, "BoothBridge", "booth_assets.json");
            
            // プレースホルダーアイコン
            placeholderIcon = EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;

            // JSONファイル読み込み
            LoadAssets();

            // FileSystemWatcher設定
            SetupFileWatcher();
            SetupPackageWatcher();
            
            // エディタ更新ハンドラーを追加（Bridgeステータスのリアルタイム更新用）
            EditorApplication.update += OnEditorUpdate;

            Debug.Log("[BoothBridge] BOOTH Library ウィンドウを開きました");
        }

        private void OnDisable()
        {
            // エディタ更新ハンドラーを削除
            EditorApplication.update -= OnEditorUpdate;
            
            // FileSystemWatcher解放
            if (fileWatcher != null)
            {
                fileWatcher.EnableRaisingEvents = false;
                fileWatcher.Dispose();
                fileWatcher = null;
            }
            
            if (packageWatcher != null)
            {
                packageWatcher.EnableRaisingEvents = false;
                packageWatcher.Dispose();
                packageWatcher = null;
            }
            
            // サムネイルキャッシュをクリア
            thumbnailCache.Clear();
        }
        
        private void OnEditorUpdate()
        {
            // 進捗情報をチェック（0.5秒ごと）
            if (!isCheckingProgress && BridgeManager.IsBridgeRunning())
            {
                CheckProgress();
            }
            
            // Bridgeステータスをリアルタイムで更新（1秒ごと）
            if (EditorApplication.timeSinceStartup - lastRepaintTime > 1.0)
            {
                lastRepaintTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }
        
        private async void CheckProgress()
        {
            isCheckingProgress = true;
            
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(1);
                    var response = await client.GetStringAsync("http://localhost:4823/progress");
                    currentProgress = JsonUtility.FromJson<ProgressInfo>(response);
                    Repaint();
                }
            }
            catch
            {
                // エラーは無視（Bridgeが起動していない場合など）
            }
            finally
            {
                await System.Threading.Tasks.Task.Delay(500);
                isCheckingProgress = false;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            // ヘッダー
            DrawHeader();

            // 更新通知
            if (showUpdateNotification && EditorApplication.timeSinceStartup < notificationEndTime)
            {
                EditorGUILayout.HelpBox("✅ BOOTHデータが更新されました！", MessageType.Info);
            }
            else if (showUpdateNotification)
            {
                showUpdateNotification = false;
            }
            
            // 進捗バー表示
            if (currentProgress != null && currentProgress.active)
            {
                EditorGUILayout.Space(5);
                Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(progressRect, currentProgress.progress / 100f, currentProgress.message);
                EditorGUILayout.Space(5);
            }

            // アセットリスト
            if (assets.Count == 0)
            {
                DrawEmptyState();
            }
            else
            {
                DrawAssetList();
            }

            EditorGUILayout.EndVertical();

            // リロードチェック
            if (needsReload && EditorApplication.timeSinceStartup >= reloadTime)
            {
                needsReload = false;
                LoadAssets();
                showUpdateNotification = true;
                notificationEndTime = EditorApplication.timeSinceStartup + 2.0;
                Repaint();
            }
            
            // 検出されたパッケージをチェック（一定時間経過後）
            if (detectedPackages.Count > 0 && 
                EditorApplication.timeSinceStartup - lastPackageDetectionTime >= PACKAGE_DETECTION_DELAY)
            {
                // tempフォルダ内のすべての.unitypackageファイルをスキャン
                string tempPackagePath = Path.Combine(GetProjectPath(), "BoothBridge", "temp");
                if (Directory.Exists(tempPackagePath))
                {
                    string[] allPackages = Directory.GetFiles(tempPackagePath, "*.unitypackage");
                    
                    // 検出されたパッケージと実際のファイルを比較
                    List<string> packagesToImport = new List<string>();
                    
                    // 検出されたパッケージが存在する場合はそれを使用
                    if (detectedPackages.Count > 0)
                    {
                        foreach (string detectedPackage in detectedPackages)
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
                                foreach (string package in selectedPackages)
                                {
                                    pendingPackageImports.Enqueue(package);
                                }
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
                                pendingPackageImports.Enqueue(packagesToImport[0]);
                            }
                        }
                    }
                }
                
                // 検出リストをクリア
                detectedPackages.Clear();
                lastPackageDetectionTime = 0;
            }
            
            // .unitypackageファイルの自動インポート
            if (pendingPackageImports.Count > 0)
            {
                string packagePath = pendingPackageImports.Dequeue();
                ImportUnityPackage(packagePath);
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            
            GUILayout.Label("BOOTH Import Assistant", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            // 同期ボタン
            if (GUILayout.Button("同期", GUILayout.Height(30), GUILayout.Width(100)))
            {
                SyncWithBooth();
            }
            
            // 再読み込みボタン
            if (GUILayout.Button("再読み込み", GUILayout.Height(30), GUILayout.Width(100)))
            {
                LoadAssets();
                Repaint();
            }
            
            // Bridge停止ボタン
            bool isBridgeRunning = BridgeManager.IsBridgeRunning();
            GUI.enabled = isBridgeRunning; // Bridgeが起動中のみ有効
            if (GUILayout.Button("Bridge停止", GUILayout.Height(30), GUILayout.Width(100)))
            {
                BridgeManager.StopBridge();
                Repaint();
            }
            GUI.enabled = true; // GUI.enabledをリセット
            
            GUILayout.FlexibleSpace();
            
            // Bridgeステータス
            bool isRunning = BridgeManager.IsBridgeRunning();
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.normal.textColor = isRunning ? Color.green : Color.gray;
            GUILayout.Label(isRunning ? "● Bridge起動中" : "○ Bridge停止中", statusStyle);
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.Space(50);
            
            GUIStyle centeredStyle = new GUIStyle(GUI.skin.label);
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            centeredStyle.wordWrap = true;
            
            GUILayout.Label("まだBOOTHの同期が行われていません", centeredStyle);
            EditorGUILayout.Space(10);
            GUILayout.Label("上の「同期」ボタンを押して、BOOTH購入リストを取得してください", centeredStyle);
        }

        private void DrawAssetList()
        {
            // 縦スクロールバーのみ表示（横スクロールバーは非表示）
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var asset in assets)
            {
                DrawAssetItem(asset);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetItem(BoothAsset asset)
        {
            // 外側のボックス全体を幅いっぱいに
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginHorizontal();
            
            // ===== 左：サムネイル（固定幅） =====
            Texture2D thumbnail = LoadThumbnailCached(asset);
            if (thumbnail != null)
            {
                GUILayout.Label(thumbnail, GUILayout.Width(120), GUILayout.Height(120));
            }
            else
            {
                GUILayout.Label(placeholderIcon, GUILayout.Width(120), GUILayout.Height(120));
            }
            
            GUILayout.Space(10);
            
            // ===== 中央：情報（余った幅を使用、長いテキストは改行） =====
            EditorGUILayout.BeginVertical();
            
            // タイトル（改行対応）
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.wordWrap = true;
            GUILayout.Label(asset.title, titleStyle);
            
            // 作者（改行対応）
            GUIStyle authorStyle = new GUIStyle(EditorStyles.miniLabel);
            authorStyle.wordWrap = true;
            GUILayout.Label("作者: " + asset.author, authorStyle);
            
            // 購入日
            GUILayout.Label("購入日: " + asset.purchaseDate, EditorStyles.miniLabel);
            
            if (asset.installed)
            {
                GUILayout.Label("✅ インポート済み", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // ===== 右：ボタン（固定幅） =====
            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            
            // ダウンロードボタン領域
            if (asset.downloadUrls != null && asset.downloadUrls.Length > 0)
            {
                // アバター別とマテリアルを分類
                List<int> avatarIndices = new List<int>();
                List<int> materialIndices = new List<int>();
                
                for (int i = 0; i < asset.downloadUrls.Length; i++)
                {
                    if (asset.downloadUrls[i].isMaterial)
                    {
                        materialIndices.Add(i);
                    }
                    else
                    {
                        avatarIndices.Add(i);
                    }
                }
                
                // アバター別のダウンロード
                if (avatarIndices.Count > 0)
                {
                    if (avatarIndices.Count == 1)
                    {
                        // 単一アバター
                        if (GUILayout.Button("📥 ダウンロード", GUILayout.Height(26)))
                        {
                            DownloadAsset(asset, avatarIndices[0]);
                        }
                    }
                    else
                    {
                        // 複数アバター：プルダウンメニュー
                        // 選択インデックスの初期化
                        if (!selectedDownloadIndex.ContainsKey(asset.id))
                        {
                            selectedDownloadIndex[asset.id] = 0;
                        }
                        
                        // ドロップダウン用のラベル配列を作成
                        string[] options = new string[avatarIndices.Count];
                        for (int i = 0; i < avatarIndices.Count; i++)
                        {
                            string label = asset.downloadUrls[avatarIndices[i]].label;
                            if (label.Length > 25)
                            {
                                label = label.Substring(0, 22) + "...";
                            }
                            options[i] = label;
                        }
                        
                        // ドロップダウンで選択
                        int selectedIndex = EditorGUILayout.Popup(
                            selectedDownloadIndex[asset.id], 
                            options,
                            GUILayout.Width(180)
                        );
                        
                        // 範囲チェック
                        if (selectedIndex >= 0 && selectedIndex < avatarIndices.Count)
                        {
                            selectedDownloadIndex[asset.id] = selectedIndex;
                        }
                        else
                        {
                            selectedDownloadIndex[asset.id] = 0;
                        }
                        
                        // 選択したアバターをダウンロード
                        if (GUILayout.Button("DL", GUILayout.Height(24)))
                        {
                            int actualIndex = avatarIndices[selectedDownloadIndex[asset.id]];
                            DownloadAsset(asset, actualIndex);
                        }
                    }
                }
                
                // マテリアルのダウンロード
                if (materialIndices.Count > 0)
                {
                    int materialCount = 1;
                    foreach (int index in materialIndices)
                    {
                        // マテリアルボタン（統一ラベル）
                        string buttonLabel = materialIndices.Count > 1 ? $"マテリアル {materialCount}" : "マテリアル";
                        if (GUILayout.Button(buttonLabel, GUILayout.Height(24)))
                        {
                            DownloadAsset(asset, index);
                        }
                        materialCount++;
                    }
                }
            }
            else
            {
                // ダウンロードリンクがない場合
                if (GUILayout.Button("商品ページ", GUILayout.Height(26)))
                {
                    Application.OpenURL(asset.productUrl);
                }
            }

            EditorGUILayout.EndVertical(); // ボタンエリア終了
            
            EditorGUILayout.EndHorizontal(); // 横並び終了
            EditorGUILayout.EndVertical(); // ボックス終了
            
            EditorGUILayout.Space(5);
        }

        private void SyncWithBooth()
        {
            // 既存のBridgeプロセスを停止（ポートの競合を防ぐ）
            if (BridgeManager.IsBridgeRunning())
            {
                Debug.Log("[BoothBridge] 既存のBridgeプロセスを停止します");
                BridgeManager.StopBridge();
                System.Threading.Thread.Sleep(500); // プロセス終了を待つ
            }
            
            // Bridge起動
            bool started = BridgeManager.StartBridge();
            
            if (!started)
            {
                return;
            }

            // 3秒待機（Bridge起動完了待ち）
            EditorUtility.DisplayProgressBar("同期中", "Bridgeを起動しています...", 0.3f);
            System.Threading.Thread.Sleep(3000);

            // BOOTHページを開く（sync=trueパラメータを付加して、自動同期を有効化）
            EditorUtility.DisplayProgressBar("同期中", "BOOTHページを開いています...", 0.6f);
            Application.OpenURL("https://accounts.booth.pm/library?sync=true");

            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog("同期開始", 
                "BOOTHページが開きました。\n\nページ読み込み完了後、自動的に同期が行われます。\n完了まで数秒お待ちください。", 
                "OK");
        }

        private void DownloadAsset(BoothAsset asset, int downloadIndex)
        {
            // Bridgeが起動していることを確認
            if (!BridgeManager.IsBridgeRunning())
            {
                bool started = BridgeManager.StartBridge();
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
                    "BOOTHのダウンロードボタンをクリックしてください。\n" +
                    "ダウンロード完了後、自動的にUnityに展開されます。", 
                    "OK");
            }
            else
            {
                // ダウンロードURLがない場合は商品ページを開く
                Application.OpenURL(asset.productUrl);
                
                EditorUtility.DisplayDialog("ダウンロード", 
                    "商品ページが開きました。\n\n" +
                    "BOOTHから「booth_" + asset.id.Replace("booth_", "") + ".zip」という名前でダウンロードしてください。\n" +
                    "ダウンロードフォルダに保存すると、自動的にUnityに展開されます。", 
                    "OK");
            }
        }

        private void LoadAssets()
        {
            assets.Clear();
            thumbnailCache.Clear(); // サムネイルキャッシュもクリア

            if (!File.Exists(jsonFilePath))
            {
                Debug.Log("[BoothBridge] booth_assets.json が見つかりません: " + jsonFilePath);
                return;
            }

            try
            {
                string json = File.ReadAllText(jsonFilePath);
                var wrapper = JsonUtility.FromJson<BoothAssetListWrapper>("{\"items\":" + json + "}");
                
                if (wrapper != null && wrapper.items != null)
                {
                    assets = wrapper.items.ToList();
                    
                    // 購入日で降順ソート
                    assets = assets.OrderByDescending(a => a.purchaseDate).ToList();
                    
                    Debug.Log("[BoothBridge] ✓ アセット読み込み完了: " + assets.Count + "件");
                    
                    // サマリー情報
                    int installedCount = assets.Count(a => a.installed);
                    int withDownloadUrls = assets.Count(a => a.downloadUrls != null && a.downloadUrls.Length > 0);
                    Debug.Log($"[BoothBridge]   インストール済み: {installedCount}件");
                    Debug.Log($"[BoothBridge]   ダウンロードURL有: {withDownloadUrls}件");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[BoothBridge] JSON読み込みエラー: " + ex.Message);
                Debug.LogError("[BoothBridge] スタックトレース: " + ex.StackTrace);
                Debug.LogError("[BoothBridge] ファイルパス: " + jsonFilePath);
                
                // バックアップファイルの確認
                string backupPath = jsonFilePath.Replace(".json", ".backup.json");
                if (File.Exists(backupPath))
                {
                    Debug.LogWarning("[BoothBridge] バックアップファイルが存在します: " + backupPath);
                    Debug.LogWarning("[BoothBridge] 必要に応じてバックアップから復元してください");
                }
            }
        }

        private Texture2D LoadThumbnailCached(BoothAsset asset)
        {
            // キャッシュをチェック
            if (thumbnailCache.ContainsKey(asset.id))
            {
                return thumbnailCache[asset.id];
            }
            
            // キャッシュにない場合は読み込む
            Texture2D thumbnail = LoadThumbnail(asset);
            
            // キャッシュに保存
            if (thumbnail != null)
            {
                thumbnailCache[asset.id] = thumbnail;
            }
            
            return thumbnail;
        }
        
        private Texture2D LoadThumbnail(BoothAsset asset)
        {
            if (string.IsNullOrEmpty(asset.localThumbnail))
            {
                return null;
            }

            string projectPath = GetProjectPath();
            string thumbnailPath = Path.Combine(projectPath, asset.localThumbnail);

            if (!File.Exists(thumbnailPath))
            {
                return null;
            }

            try
            {
                byte[] imageData = File.ReadAllBytes(thumbnailPath);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(imageData);
                return texture;
            }
            catch
            {
                return null;
            }
        }

        private void SetupFileWatcher()
        {
            if (!File.Exists(jsonFilePath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(jsonFilePath);
            string filename = Path.GetFileName(jsonFilePath);

            if (!Directory.Exists(directory))
            {
                return;
            }

            fileWatcher = new FileSystemWatcher(directory, filename);
            fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            
            fileWatcher.Changed += OnFileChanged;
            
            fileWatcher.EnableRaisingEvents = true;

            Debug.Log("[BoothBridge] FileSystemWatcher設定完了");
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // デバウンス（200ms後に再読み込み）
            needsReload = true;
            reloadTime = EditorApplication.timeSinceStartup + 0.2;
        }

        private void SetupPackageWatcher()
        {
            string projectPath = GetProjectPath();
            if (string.IsNullOrEmpty(projectPath))
            {
                return;
            }

            string tempPackagePath = Path.Combine(projectPath, "BoothBridge", "temp");
            
            if (!Directory.Exists(tempPackagePath))
            {
                Directory.CreateDirectory(tempPackagePath);
            }

            packageWatcher = new FileSystemWatcher(tempPackagePath, "*.unitypackage");
            packageWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime;
            packageWatcher.IncludeSubdirectories = true;
            
            packageWatcher.Created += OnPackageFileCreated;
            
            packageWatcher.EnableRaisingEvents = true;

            Debug.Log("[BoothBridge] PackageWatcher設定完了: " + tempPackagePath);
        }

        private void OnPackageFileCreated(object sender, FileSystemEventArgs e)
        {
            // ファイルが完全に書き込まれるまで少し待つ
            System.Threading.Thread.Sleep(500);
            
            Debug.Log("[BoothBridge] .unitypackage検出: " + e.FullPath);
            
            // 検出時刻を記録
            lastPackageDetectionTime = EditorApplication.timeSinceStartup;
            
            // 検出されたパッケージをリストに追加（重複チェック）
            if (!detectedPackages.Contains(e.FullPath))
            {
                detectedPackages.Add(e.FullPath);
            }
        }

        private void ImportUnityPackage(string packagePath)
        {
            if (!File.Exists(packagePath))
            {
                Debug.LogWarning("[BoothBridge] パッケージファイルが見つかりません: " + packagePath);
                Debug.LogWarning("[BoothBridge] ファイルが削除されたか、移動された可能性があります");
                return;
            }

            try
            {
                FileInfo fileInfo = new FileInfo(packagePath);
                long fileSizeKB = fileInfo.Length / 1024;
                
                Debug.Log("[BoothBridge] ✓ .unitypackage自動インポート開始");
                Debug.Log($"[BoothBridge]   ファイル: {Path.GetFileName(packagePath)}");
                Debug.Log($"[BoothBridge]   サイズ: {fileSizeKB} KB");
                Debug.Log($"[BoothBridge]   パス: {packagePath}");
                
                // 自動インポート（インタラクティブモードOFF）
                AssetDatabase.ImportPackage(packagePath, false);
                
                Debug.Log("[BoothBridge] ✓ インポート完了");
                Debug.Log("[BoothBridge] Assetsフォルダに展開されました");
                
                // インポート完了後、.unitypackageファイルを削除（遅延実行）
                string pathToDelete = packagePath;
                EditorApplication.delayCall += (EditorApplication.CallbackFunction)(() => DeletePackageFileDelayed(pathToDelete));
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[BoothBridge] .unitypackageインポートエラー: " + ex.Message);
                Debug.LogError("[BoothBridge] スタックトレース: " + ex.StackTrace);
                Debug.LogError("[BoothBridge] ファイルパス: " + packagePath);
                
                EditorUtility.DisplayDialog("インポートエラー", 
                    "UnityPackageのインポートに失敗しました。\n\n" + ex.Message + "\n\nUnityコンソールで詳細を確認してください。", 
                    "OK");
            }
        }
        
        private void DeletePackageFileDelayed(string packagePath)
        {
            // 非同期で削除（インポート完了を待つ）
            double deleteTime = EditorApplication.timeSinceStartup + 3.0; // 3秒後
            
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
                            Debug.Log($"[BoothBridge] ✓ .unitypackageファイルを削除: {Path.GetFileName(packagePath)}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[BoothBridge] .unitypackageファイルの削除に失敗（無視しても問題ありません）: {ex.Message}");
                    }
                }
            };
            
            EditorApplication.update += deleteCallback;
        }

        private string GetProjectPath()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
            {
                return null;
            }
            
            return Directory.GetParent(dataPath).FullName;
        }
    }

    /// <summary>
    /// 進捗情報
    /// </summary>
    [Serializable]
    public class ProgressInfo
    {
        public bool active;
        public string stage;
        public string fileName;
        public float progress;
        public string message;
    }

    /// <summary>
    /// ダウンロードリンク情報
    /// </summary>
    [Serializable]
    public class DownloadUrl
    {
        public string url;
        public string label;
        public bool isMaterial;  // マテリアルかどうか
    }

    /// <summary>
    /// BOOTHアセット情報
    /// </summary>
    [Serializable]
    public class BoothAsset
    {
        public string id;
        public string title;
        public string author;
        public string productUrl;
        public string thumbnailUrl;
        public DownloadUrl[] downloadUrls; // 複数ダウンロードリンク対応
        public string purchaseDate;
        public string localThumbnail;
        public bool installed;
        public string importPath;
        public string notes;
    }

    /// <summary>
    /// JSON配列デシリアライズ用ラッパー
    /// </summary>
    [Serializable]
    public class BoothAssetListWrapper
    {
        public BoothAsset[] items;
    }

    /// <summary>
    /// UnityPackageインポート選択ダイアログ
    /// </summary>
    public class PackageImportDialog : EditorWindow
    {
        private List<string> packagePaths;
        private Dictionary<string, bool> packageSelections;
        private System.Action<List<string>> onImport;
        private Vector2 scrollPosition;

        public static void ShowDialog(List<string> packages, System.Action<List<string>> callback)
        {
            var window = GetWindow<PackageImportDialog>(true, "UnityPackageをインポート");
            window.packagePaths = packages;
            window.onImport = callback;
            window.packageSelections = new Dictionary<string, bool>();
            
            // すべてデフォルトで選択
            foreach (string package in packages)
            {
                window.packageSelections[package] = true;
            }
            
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("以下のUnityPackageをインポートしますか？", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (string packagePath in packagePaths)
            {
                EditorGUILayout.BeginHorizontal();
                
                string fileName = Path.GetFileName(packagePath);
                bool isSelected = packageSelections.ContainsKey(packagePath) && packageSelections[packagePath];
                
                // チェックボックス
                bool newSelection = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                packageSelections[packagePath] = newSelection;
                
                // ファイル名とサイズ
                if (File.Exists(packagePath))
                {
                    FileInfo fileInfo = new FileInfo(packagePath);
                    long fileSizeMB = fileInfo.Length / 1024 / 1024;
                    EditorGUILayout.LabelField($"{fileName} ({fileSizeMB} MB)", 
                        newSelection ? EditorStyles.label : EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField(fileName, EditorStyles.miniLabel);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(10);
            
            // ボタン
            EditorGUILayout.BeginHorizontal();
            
            // すべて選択/解除
            int selectedCount = packageSelections.Values.Count(v => v);
            if (GUILayout.Button(selectedCount == packagePaths.Count ? "すべて解除" : "すべて選択", 
                GUILayout.Height(30)))
            {
                bool selectAll = selectedCount != packagePaths.Count;
                foreach (string package in packagePaths)
                {
                    packageSelections[package] = selectAll;
                }
            }
            
            GUILayout.FlexibleSpace();
            
            // キャンセル
            if (GUILayout.Button("キャンセル", GUILayout.Height(30), GUILayout.Width(100)))
            {
                Close();
            }
            
            // インポート
            GUI.enabled = selectedCount > 0;
            if (GUILayout.Button("選択したものをインポート", GUILayout.Height(30), GUILayout.Width(180)))
            {
                List<string> selectedPackages = new List<string>();
                foreach (var kvp in packageSelections)
                {
                    if (kvp.Value)
                    {
                        selectedPackages.Add(kvp.Key);
                    }
                }
                
                if (onImport != null)
                {
                    onImport(selectedPackages);
                }
                
                Close();
            }
            
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
        }
    }
}

