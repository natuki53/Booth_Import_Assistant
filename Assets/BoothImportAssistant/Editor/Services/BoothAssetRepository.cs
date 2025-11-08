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
            jsonFilePath = Path.Combine(projectPath, "BoothBridge", "booth_assets.json");
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
                    int installedCount = assets.Count(a => a.installed);
                    Debug.Log($"[BoothBridge] アセット読み込み: {assets.Count}件 (インストール済み: {installedCount})");
                    
                    OnAssetsChanged?.Invoke();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoothBridge] JSON読み込みエラー: {ex.Message}");
                
                string backupPath = jsonFilePath.Replace(".json", ".backup.json");
                if (File.Exists(backupPath))
                {
                    Debug.LogWarning("[BoothBridge] バックアップファイルが存在します");
                }
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

