using System;
using System.Collections.Generic;

namespace BoothImportAssistant.Models
{
    /// <summary>
    /// 保存済みダウンロードファイル情報
    /// </summary>
    [Serializable]
    public class DownloadedFileInfo
    {
        public string assetId;
        public string label;
        public string zipFilePath;
        public string originalFileName;
        public string downloadedAt; // ISO 8601 string for JsonUtility compatibility
        public bool isMaterial;
    }

    /// <summary>
    /// メタデータファイル構造
    /// </summary>
    [Serializable]
    public class DownloadedFileMetadata
    {
        public string version = "1.1.0-beta";
        public List<DownloadedFileInfo> downloads = new List<DownloadedFileInfo>();
    }
}

