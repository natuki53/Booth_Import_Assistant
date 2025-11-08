using System;
using UnityEngine;

namespace BoothImportAssistant.Models
{
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
        public string source;  // "purchased" or "gift"
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

