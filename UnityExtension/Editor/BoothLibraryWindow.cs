using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        
        // 複数ダウンロード用の選択状態
        private Dictionary<string, int> selectedDownloadIndex = new Dictionary<string, int>();
        private Dictionary<string, bool> showDownloadOptions = new Dictionary<string, bool>();

        [MenuItem("Tools/BOOTH Library")]
        public static void ShowWindow()
        {
            var window = GetWindow<BoothLibraryWindow>("BOOTH Library");
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

            Debug.Log("[BoothBridge] BOOTH Library ウィンドウを開きました");
        }

        private void OnDisable()
        {
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
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var asset in assets)
            {
                DrawAssetItem(asset);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetItem(BoothAsset asset)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();

            // サムネイル
            Texture2D thumbnail = LoadThumbnail(asset);
            if (thumbnail != null)
            {
                GUILayout.Label(thumbnail, GUILayout.Width(64), GUILayout.Height(64));
            }
            else
            {
                GUILayout.Label(placeholderIcon, GUILayout.Width(64), GUILayout.Height(64));
            }

            // 情報
            EditorGUILayout.BeginVertical();
            
            GUILayout.Label(asset.title, EditorStyles.boldLabel);
            GUILayout.Label("作者: " + asset.author, EditorStyles.miniLabel);
            GUILayout.Label("購入日: " + asset.purchaseDate, EditorStyles.miniLabel);
            
            if (asset.installed)
            {
                GUILayout.Label("✅ インポート済み (" + asset.importPath + ")", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // ボタン
            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            
            // 複数ダウンロードリンク対応（改善版）
            if (asset.downloadUrls != null && asset.downloadUrls.Length > 0)
            {
                if (asset.downloadUrls.Length == 1)
                {
                    // 単一ダウンロードの場合
                    if (GUILayout.Button("📥 ダウンロード", GUILayout.Height(28)))
                    {
                        DownloadAsset(asset, 0);
                    }
                }
                else if (asset.downloadUrls.Length <= 3)
                {
                    // 3件以下：コンパクトボタン表示
                    GUILayout.Label("📥 ダウンロード:", EditorStyles.miniLabel);
                    for (int i = 0; i < asset.downloadUrls.Length; i++)
                    {
                        string label = asset.downloadUrls[i].label;
                        if (label.Length > 18)
                        {
                            label = label.Substring(0, 15) + "...";
                        }
                        
                        if (GUILayout.Button(label, GUILayout.Height(24)))
                        {
                            DownloadAsset(asset, i);
                        }
                    }
                }
                else
                {
                    // 4件以上：ドロップダウン + ダウンロードボタン
                    GUILayout.Label("📥 バリエーション選択:", EditorStyles.miniLabel);
                    
                    // 選択インデックスの初期化
                    if (!selectedDownloadIndex.ContainsKey(asset.id))
                    {
                        selectedDownloadIndex[asset.id] = 0;
                    }
                    
                    // ドロップダウン用のラベル配列を作成
                    string[] options = new string[asset.downloadUrls.Length];
                    for (int i = 0; i < asset.downloadUrls.Length; i++)
                    {
                        options[i] = asset.downloadUrls[i].label;
                    }
                    
                    // ドロップダウンで選択
                    selectedDownloadIndex[asset.id] = EditorGUILayout.Popup(
                        selectedDownloadIndex[asset.id], 
                        options, 
                        GUILayout.Height(20)
                    );
                    
                    // 選択したバリエーションをダウンロード
                    if (GUILayout.Button("選択中をDL", GUILayout.Height(28)))
                    {
                        DownloadAsset(asset, selectedDownloadIndex[asset.id]);
                    }
                    
                    // 全てダウンロードボタン（オプション）
                    if (GUILayout.Button("全てDL", GUILayout.Height(22)))
                    {
                        if (EditorUtility.DisplayDialog("確認", 
                            $"{asset.downloadUrls.Length}個のファイルをすべてダウンロードしますか？", 
                            "はい", "キャンセル"))
                        {
                            DownloadAllVariants(asset);
                        }
                    }
                }
            }
            else
            {
                // ダウンロードリンクがない場合
                if (GUILayout.Button("📄 商品ページで確認", GUILayout.Height(28)))
                {
                    Application.OpenURL(asset.productUrl);
                }
            }
            
            EditorGUILayout.Space(3);
            
            // 商品ページを開くボタン
            if (GUILayout.Button("🌐 商品ページ", GUILayout.Height(25)))
            {
                Application.OpenURL(asset.productUrl);
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
        }

        private void SyncWithBooth()
        {
            // Bridge起動
            bool started = BridgeManager.StartBridge();
            
            if (!started)
            {
                return;
            }

            // 3秒待機（Bridge起動完了待ち）
            EditorUtility.DisplayProgressBar("同期中", "Bridgeを起動しています...", 0.3f);
            System.Threading.Thread.Sleep(3000);

            // BOOTHページを開く
            EditorUtility.DisplayProgressBar("同期中", "BOOTHページを開いています...", 0.6f);
            Application.OpenURL("https://accounts.booth.pm/library");

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

        private void DownloadAllVariants(BoothAsset asset)
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

            if (asset.downloadUrls == null || asset.downloadUrls.Length == 0)
            {
                EditorUtility.DisplayDialog("エラー", 
                    "ダウンロードリンクが見つかりません。", 
                    "OK");
                return;
            }

            // 全バリエーションのダウンロードページを順番に開く
            for (int i = 0; i < asset.downloadUrls.Length; i++)
            {
                Application.OpenURL(asset.downloadUrls[i].url);
                
                // ブラウザが複数タブを開くのを待つ
                if (i < asset.downloadUrls.Length - 1)
                {
                    System.Threading.Thread.Sleep(500);
                }
            }

            // 案内メッセージ
            string message = $"{asset.downloadUrls.Length}個のダウンロードページを開きました。\n\n";
            message += "各ページでダウンロードボタンをクリックしてください：\n\n";
            
            for (int i = 0; i < asset.downloadUrls.Length; i++)
            {
                message += $"[{i + 1}] {asset.downloadUrls[i].label}\n";
            }
            
            message += "\nダウンロード完了後、自動的にUnityに展開されます。";
            
            EditorUtility.DisplayDialog("全バリエーションダウンロード", message, "OK");
        }

        private void LoadAssets()
        {
            assets.Clear();

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

            string importedAssetsPath = Path.Combine(projectPath, "Assets", "ImportedAssets");
            
            if (!Directory.Exists(importedAssetsPath))
            {
                Directory.CreateDirectory(importedAssetsPath);
            }

            packageWatcher = new FileSystemWatcher(importedAssetsPath, "*.unitypackage");
            packageWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime;
            packageWatcher.IncludeSubdirectories = true;
            
            packageWatcher.Created += OnPackageFileCreated;
            
            packageWatcher.EnableRaisingEvents = true;

            Debug.Log("[BoothBridge] PackageWatcher設定完了: " + importedAssetsPath);
        }

        private void OnPackageFileCreated(object sender, FileSystemEventArgs e)
        {
            // ファイルが完全に書き込まれるまで少し待つ
            System.Threading.Thread.Sleep(500);
            
            // インポートキューに追加
            lock (pendingPackageImports)
            {
                pendingPackageImports.Enqueue(e.FullPath);
            }
            
            Debug.Log("[BoothBridge] .unitypackage検出: " + e.FullPath);
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
                
                Debug.Log("[BoothBridge] ✓ .unitypackageインポート開始");
                Debug.Log($"[BoothBridge]   ファイル: {Path.GetFileName(packagePath)}");
                Debug.Log($"[BoothBridge]   サイズ: {fileSizeKB} KB");
                Debug.Log($"[BoothBridge]   パス: {packagePath}");
                
                // インタラクティブモードでインポート（ユーザーが選択可能）
                AssetDatabase.ImportPackage(packagePath, true);
                
                Debug.Log("[BoothBridge] ✓ インポートダイアログ表示完了");
                Debug.Log("[BoothBridge] ユーザーによる確認を待機中...");
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
    /// ダウンロードリンク情報
    /// </summary>
    [Serializable]
    public class DownloadUrl
    {
        public string url;
        public string label;
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
}

