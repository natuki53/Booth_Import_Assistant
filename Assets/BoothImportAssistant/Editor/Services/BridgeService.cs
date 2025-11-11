using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using BoothImportAssistant.Models;

namespace BoothImportAssistant.Services
{
    /// <summary>
    /// Bridge（Node.js）との通信を管理
    /// </summary>
    public class BridgeService
    {
        private const string BRIDGE_URL = "http://localhost:49729";
        private ProgressInfo currentProgress;
        private bool isCheckingProgress = false;
        private double lastProgressCheckTime = 0;
        private const double PROGRESS_CHECK_INTERVAL = 0.5; // 0.5秒ごとにチェック

        public ProgressInfo CurrentProgress => currentProgress;
        public event Action<ProgressInfo> OnProgressUpdated;

        public bool IsBridgeRunning()
        {
            return BridgeManager.IsBridgeRunning();
        }

        public bool StartBridge()
        {
            return BridgeManager.StartBridge();
        }

        public void StopBridge()
        {
            BridgeManager.StopBridge();
        }

        public async void CheckProgressAsync()
        {
            // 頻度制限：前回のチェックから一定時間経過していない場合はスキップ
            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - lastProgressCheckTime < PROGRESS_CHECK_INTERVAL)
                return;
            
            if (isCheckingProgress || !IsBridgeRunning())
                return;

            lastProgressCheckTime = currentTime;
            isCheckingProgress = true;

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(1);
                    var response = await client.GetStringAsync($"{BRIDGE_URL}/progress");
                    currentProgress = JsonUtility.FromJson<ProgressInfo>(response);
                    OnProgressUpdated?.Invoke(currentProgress);
                }
            }
            catch
            {
                // Bridgeが起動していない場合は無視
            }
            finally
            {
                await Task.Delay(500);
                isCheckingProgress = false;
            }
        }
    }
}

