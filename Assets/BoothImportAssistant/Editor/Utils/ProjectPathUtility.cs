using System.IO;
using UnityEngine;

namespace BoothImportAssistant.Utils
{
    /// <summary>
    /// プロジェクトパス取得ユーティリティ
    /// 元ファイル: BoothLibraryWindow.cs, BridgeManager.cs の GetProjectPath()
    /// </summary>
    public static class ProjectPathUtility
    {
        public static string GetProjectPath()
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (string.IsNullOrEmpty(dataPath))
            {
                return null;
            }
            
            var parent = Directory.GetParent(dataPath);
            if (parent == null)
            {
                return null;
            }
            
            string projectPath = parent.FullName;
            // macOSでもバックスラッシュが含まれる可能性があるため、スラッシュに正規化
            return projectPath.Replace('\\', '/');
        }
    }
}

