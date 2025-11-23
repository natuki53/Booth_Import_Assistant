using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BoothImportAssistant.Bridge
{
    /// <summary>
    /// Node.jsプロセスの起動・停止・監視
    /// 元ファイル: BridgeManager.cs のプロセス管理部分
    /// </summary>
    public class NodeProcessManager
    {
        private Process bridgeProcess;

        public bool StartNodeProcess(string nodePath, string bridgeScriptPath, string projectPath)
        {
            // 既存のプロセスが存在する場合、確実に停止
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                StopNodeProcess();
            }
            
            // ポート49729を使用しているプロセスを検出して終了
            PortManager.KillProcessUsingPort(49729);
            System.Threading.Thread.Sleep(500); // プロセス終了を待機

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = nodePath,
                    Arguments = $"\"{bridgeScriptPath}\" --projectPath \"{projectPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                bridgeProcess = new Process { StartInfo = startInfo };
                
                // エラーのみ出力
                bridgeProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Debug.LogError(args.Data);
                    }
                };

                bridgeProcess.Start();
                bridgeProcess.BeginErrorReadLine();

                System.Threading.Thread.Sleep(500);

                if (bridgeProcess.HasExited)
                {
                    Debug.LogError("[BoothBridge] Bridge起動失敗");
                    PortManager.KillProcessUsingPort(49729);
                    EditorUtility.DisplayDialog("エラー", 
                        "Bridgeの起動に失敗しました。", 
                        "OK");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[BoothBridge] Bridge起動エラー: " + ex.Message);
                EditorUtility.DisplayDialog("エラー", 
                    "Bridgeの起動に失敗しました。\n\n" + ex.Message, 
                    "OK");
                return false;
            }
        }

        public void StopNodeProcess()
        {
            bool stopped = false;
            
            // プロセス参照がある場合、それを停止
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                try
                {
                    bridgeProcess.Kill();
                    
                    if (!bridgeProcess.WaitForExit(5000))
                    {
                        if (bridgeProcess.HasExited)
                        {
                            stopped = true;
                        }
                    }
                    else
                    {
                        stopped = true;
                    }
                    
                    bridgeProcess.Dispose();
                    bridgeProcess = null;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[BoothBridge] Bridge終了エラー: " + ex.Message);
                    try
                    {
                        bridgeProcess?.Dispose();
                    }
                    catch { }
                    bridgeProcess = null;
                }
            }
            
            // ポート49729を使用しているプロセスを停止
            if (!stopped)
            {
                PortManager.KillProcessUsingPort(49729);
            }
        }

        public bool IsProcessRunning()
        {
            // プロセス参照がある場合、それをチェック
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                return true;
            }
            
            return false;
        }

        public bool HasProcessReference()
        {
            return bridgeProcess != null;
        }
    }
}

