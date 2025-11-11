using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using BoothImportAssistant.Models;

namespace BoothImportAssistant.Services
{
    /// <summary>
    /// BOOTHアセットデータの読み込み・保存を管理
    /// </summary>
    public class BoothAssetRepository
    {
        private readonly string jsonFilePath;
        private List<BoothAsset> assets = new List<BoothAsset>();

        public event Action OnAssetsChanged;
        public IReadOnlyList<BoothAsset> Assets => assets.AsReadOnly();

        public BoothAssetRepository(string projectPath)
        {
            // AppDataディレクトリからJSONを読み込む
            string appDataPath = GetAppDataPath();
            jsonFilePath = Path.Combine(appDataPath, "booth_assets.json");
        }
        
        /// <summary>
        /// アプリケーションデータの保存先を取得（OS別）
        /// </summary>
        private static string GetAppDataPath()
        {
            string appDataPath;
            
            #if UNITY_EDITOR_WIN
                // Windows: %LOCALAPPDATA%\Booth_Import_Assistant
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                appDataPath = Path.Combine(localAppData, "Booth_Import_Assistant");
            #elif UNITY_EDITOR_OSX
                // Mac: ~/Library/Application Support/Booth_Import_Assistant
                string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                appDataPath = Path.Combine(home, "Library", "Application Support", "Booth_Import_Assistant");
            #else
                // Linux: ~/.config/Booth_Import_Assistant
                string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                appDataPath = Path.Combine(home, ".config", "Booth_Import_Assistant");
            #endif
            
            return appDataPath;
        }

        public string GetJsonFilePath()
        {
            return jsonFilePath;
        }

        public bool LoadAssets()
        {
            assets.Clear();

            if (!File.Exists(jsonFilePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(jsonFilePath);
                var wrapper = JsonUtility.FromJson<BoothAssetListWrapper>("{\"items\":" + json + "}");

                if (wrapper?.items != null)
                {
                    assets = wrapper.items.OrderByDescending(a => a.purchaseDate).ToList();
                    OnAssetsChanged?.Invoke();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoothBridge] JSON読み込みエラー: {ex.Message}");
            }

            return false;
        }

        public BoothAsset GetAssetById(string id)
        {
            return assets.FirstOrDefault(a => a.id == id);
        }

        public void UpdateAssetInstalledStatus(string id, bool installed)
        {
            var asset = GetAssetById(id);
            if (asset != null)
            {
                asset.installed = installed;
                OnAssetsChanged?.Invoke();
            }
        }
    }
}

