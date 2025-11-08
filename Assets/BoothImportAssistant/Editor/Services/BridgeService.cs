using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using BoothImportAssistant.Models;

namespace BoothImportAssistant.Services
{
    /// <summary>
    /// Bridge（Node.js）との通信を管理
    /// </summary>
    public class BridgeService
    {
        private const string BRIDGE_URL = "http://localhost:4823";
        private ProgressInfo currentProgress;
        private bool isCheckingProgress = false;

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
            if (isCheckingProgress || !IsBridgeRunning())
                return;

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

