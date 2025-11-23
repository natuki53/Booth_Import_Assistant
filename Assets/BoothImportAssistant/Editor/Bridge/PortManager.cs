using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BoothImportAssistant.Bridge
{
    /// <summary>
    /// ポート管理（ポート使用チェック、プロセス終了）
    /// 元ファイル: BridgeManager.cs の IsPortInUse(), KillProcessUsingPort()
    /// </summary>
    public static class PortManager
    {
        public static bool IsPortInUse(int port)
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    return IsPortInUseWindows(port);
                }
                else if (Application.platform == RuntimePlatform.OSXEditor || 
                         Application.platform == RuntimePlatform.LinuxEditor)
                {
                    return IsPortInUseUnix(port);
                }
            }
            catch
            {
                // エラーが発生した場合はfalseを返す
            }
            
            return false;
        }

        private static bool IsPortInUseWindows(int port)
        {
            // Windows: netstatでポートを使用しているプロセスIDを取得
            Process netstatProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c netstat -ano | findstr :" + port,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            
            netstatProcess.Start();
            string output = netstatProcess.StandardOutput.ReadToEnd();
            netstatProcess.WaitForExit();
            
            // 出力からプロセスIDを抽出
            string[] lines = output.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (line.Contains(":" + port) && line.Contains("LISTENING"))
                {
                    string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int pid))
                    {
                        try
                        {
                            Process process = Process.GetProcessById(pid);
                            if (process.ProcessName.ToLower().Contains("node"))
                            {
                                return true;
                            }
                        }
                        catch
                        {
                            // プロセスが存在しない場合は無視
                        }
                    }
                }
            }
            
            return false;
        }

        private static bool IsPortInUseUnix(int port)
        {
            // Mac/Linux: lsofでポートを使用しているプロセスIDを取得
            Process lsofProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "lsof",
                    Arguments = "-ti:" + port,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            
            lsofProcess.Start();
            string output = lsofProcess.StandardOutput.ReadToEnd();
            lsofProcess.WaitForExit();
            
            if (!string.IsNullOrWhiteSpace(output))
            {
                string[] pids = output.Trim().Split('\n');
                foreach (string pidStr in pids)
                {
                    if (int.TryParse(pidStr.Trim(), out int pid))
                    {
                        try
                        {
                            Process process = Process.GetProcessById(pid);
                            if (process.ProcessName.ToLower().Contains("node"))
                            {
                                return true;
                            }
                        }
                        catch
                        {
                            // プロセスが存在しない場合は無視
                        }
                    }
                }
            }
            
            return false;
        }

        public static void KillProcessUsingPort(int port)
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    KillProcessUsingPortWindows(port);
                }
                else if (Application.platform == RuntimePlatform.OSXEditor || 
                         Application.platform == RuntimePlatform.LinuxEditor)
                {
                    KillProcessUsingPortUnix(port);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoothBridge] ポート検出失敗: {ex.Message}");
            }
        }

        private static void KillProcessUsingPortWindows(int port)
        {
            // Windows: netstatでポートを使用しているプロセスIDを取得
            Process netstatProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c netstat -ano | findstr :" + port,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            
            netstatProcess.Start();
            string output = netstatProcess.StandardOutput.ReadToEnd();
            netstatProcess.WaitForExit();
            
            // 出力からプロセスIDを抽出
            string[] lines = output.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (line.Contains(":" + port) && line.Contains("LISTENING"))
                {
                    string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int pid))
                    {
                        try
                        {
                            Process process = Process.GetProcessById(pid);
                            if (process.ProcessName.ToLower().Contains("node"))
                            {
                                // Node.jsプロセスを終了
                                process.Kill();
                                process.WaitForExit(3000);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[BoothBridge] プロセス終了失敗: {ex.Message}");
                        }
                    }
                }
            }
        }

        private static void KillProcessUsingPortUnix(int port)
        {
            // Mac/Linux: lsofでポートを使用しているプロセスIDを取得
            Process lsofProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "lsof",
                    Arguments = "-ti:" + port,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            
            lsofProcess.Start();
            string output = lsofProcess.StandardOutput.ReadToEnd();
            lsofProcess.WaitForExit();
            
            if (!string.IsNullOrWhiteSpace(output))
            {
                string[] pids = output.Trim().Split('\n');
                foreach (string pidStr in pids)
                {
                    if (int.TryParse(pidStr.Trim(), out int pid))
                    {
                        try
                        {
                            Process process = Process.GetProcessById(pid);
                            if (process.ProcessName.ToLower().Contains("node"))
                            {
                                // Node.jsプロセスを終了
                                process.Kill();
                                process.WaitForExit(3000);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[BoothBridge] プロセス終了失敗: {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}

