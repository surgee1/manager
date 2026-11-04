using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RBX_Alt_Manager.Classes
{
    internal static class RobloxClients
    {
        internal const string ExecutableName = "RobloxPlayerBeta.exe";
        internal static string ManagedRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spectrum Manager", "Roblox-Legit");
        internal static bool Cheat => AccountManager.General.Get("ClientLaunchMode") == "Cheat";
        internal static string ConfiguredPath(bool cheat)
        {
            string configured = AccountManager.General.Get(cheat ? "CheatRobloxPath" : "LegitRobloxPath");
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return FindNewest(Path.Combine(local, cheat ? @"Roblox\Versions" : "MeowlRoblox-Legit"));
        }
        internal static string FindNewest(string root)
        {
            if (!Directory.Exists(root)) return "";
            try { return Directory.GetDirectories(root, "version-*").Select(dir => Path.Combine(dir, ExecutableName))
                .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? ""; }
            catch (IOException) { return ""; }
            catch (UnauthorizedAccessException) { return ""; }
        }
        internal static string Validate(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !string.Equals(Path.GetFileName(path), ExecutableName, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new IOException("Клиент Roblox не найден. Открой Настройки → Запуск: скачай Legit или укажи клиент Cheat.");
            return Path.GetFullPath(path);
        }
        internal static string SelectedPath() => Validate(ConfiguredPath(Cheat));
        internal static ProcessStartInfo ProtocolStart(string executable, string protocol)
        {
            if (protocol == null || !protocol.StartsWith("roblox-player:", StringComparison.Ordinal) || protocol.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0)
                throw new ArgumentException("Некорректная команда запуска Roblox.");
            return new ProcessStartInfo(Validate(executable), "\"" + protocol + "\"")
            { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable)) };
        }
        internal static string ProcessPath(int pid)
        {
            using (var handle = new SafeWaitHandle(OpenProcess(0x1000, false, pid), true))
            {
                if (handle.IsInvalid) return null;
                var path = new StringBuilder(32768); int length = path.Capacity;
                return QueryFullProcessImageName(handle, 0, path, ref length) ? path.ToString() : null;
            }
        }
        internal static int[] RunningIds()
        {
            return Process.GetProcessesByName("RobloxPlayerBeta").Select(process => { using (process) return process.Id; }).ToArray();
        }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(SafeWaitHandle process, uint flags, StringBuilder path, ref int length);
    }
}
