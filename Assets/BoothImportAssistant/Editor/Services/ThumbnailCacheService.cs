using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using BoothImportAssistant.Models;

namespace BoothImportAssistant.Services
{
    /// <summary>
    /// サムネイル画像のキャッシュ管理
    /// </summary>
    public class ThumbnailCacheService
    {
        private readonly string projectPath;
        private readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public ThumbnailCacheService(string projectPath)
        {
            this.projectPath = projectPath;
        }

        public Texture2D GetThumbnail(BoothAsset asset)
        {
            if (string.IsNullOrEmpty(asset?.localThumbnail))
                return null;

            // キャッシュチェック
            if (cache.TryGetValue(asset.id, out Texture2D cached))
                return cached;

            // 読み込み
            Texture2D texture = LoadThumbnailFromDisk(asset.localThumbnail);
            if (texture != null)
            {
                cache[asset.id] = texture;
            }

            return texture;
        }

        private Texture2D LoadThumbnailFromDisk(string relativePath)
        {
            string fullPath = Path.Combine(projectPath, relativePath);
            if (!File.Exists(fullPath))
                return null;

            try
            {
                byte[] imageData = File.ReadAllBytes(fullPath);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(imageData);
                return texture;
            }
            catch
            {
                return null;
            }
        }

        public void Clear()
        {
            cache.Clear();
        }
    }
}

