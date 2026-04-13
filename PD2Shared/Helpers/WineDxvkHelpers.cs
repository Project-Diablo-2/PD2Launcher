using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PD2Shared.Helpers
{
    public static class WineDxvkHelpers
    {
        private const string DxvkFileName = "dxvk.conf";
        private const string LauncherSection = "[PD2Launcher.exe]";
        private const string ShaderLine = "d3d9.shaderModel = 1";

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        public static bool IsRunningUnderWine()
        {
            try
            {
                IntPtr ntdll = GetModuleHandle("ntdll.dll");
                if (ntdll != IntPtr.Zero)
                {
                    IntPtr wineProc = GetProcAddress(ntdll, "wine_get_version");
                    if (wineProc != IntPtr.Zero)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            try
            {
                using var hkcu = Registry.CurrentUser.OpenSubKey(@"Software\Wine");
                if (hkcu != null)
                {
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                using var hklm = Registry.LocalMachine.OpenSubKey(@"Software\Wine");
                if (hklm != null)
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public static void EnsureDxvkConfigForLauncher(string rootPath)
        {
            try
            {
                if (!IsRunningUnderWine())
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                {
                    return;
                }

                string dxvkPath = Path.Combine(rootPath, DxvkFileName);
                string desiredBlock = $"{LauncherSection}{Environment.NewLine}{ShaderLine}";

                if (!File.Exists(dxvkPath))
                {
                    File.WriteAllText(dxvkPath, desiredBlock + Environment.NewLine, Encoding.UTF8);
                    Debug.WriteLine($"Created {dxvkPath} for Wine/Proton launcher compatibility.");
                    return;
                }

                string existing = File.ReadAllText(dxvkPath);

                if (existing.Contains(LauncherSection, StringComparison.OrdinalIgnoreCase) &&
                    existing.Contains(ShaderLine, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine("dxvk.conf already contains PD2Launcher.exe shader workaround.");
                    return;
                }

                var sb = new StringBuilder(existing.TrimEnd());

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                }

                sb.AppendLine(LauncherSection);
                sb.AppendLine(ShaderLine);

                File.WriteAllText(dxvkPath, sb.ToString() + Environment.NewLine, Encoding.UTF8);
                Debug.WriteLine($"Updated {dxvkPath} with PD2Launcher.exe shader workaround.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create/update dxvk.conf: {ex.Message}");
            }
        }
    }
}
