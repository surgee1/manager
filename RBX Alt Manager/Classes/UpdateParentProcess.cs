using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SurgeeUpdating
{
    internal static class UpdateParentProcess
    {
        internal static void WaitForExit(int pid, string executable, int timeoutMilliseconds = 30000)
        {
            if (pid <= 0 || timeoutMilliseconds < 0) throw new ArgumentOutOfRangeException();
            string expected = Normalize(executable);
            using (var process = new SafeWaitHandle(OpenProcess(0x00101000, false, pid), true))
            {
                if (process.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 87) return; // Parent exited before the helper started.
                    throw new Win32Exception(error, "Не удалось получить доступ к процессу менеджера.");
                }
                if (WaitForSingleObject(process, 0) == 0) return;
                var path = new StringBuilder(32768); int size = path.Capacity;
                if (!QueryFullProcessImageName(process, 0, path, ref size))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (WaitForSingleObject(process, 0) == 0) return;
                    throw new Win32Exception(error, "Не удалось проверить путь менеджера.");
                }
                if (!string.Equals(Normalize(path.ToString()), expected, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Путь менеджера не совпал. Обновление отменено.");
                uint result = WaitForSingleObject(process, (uint)timeoutMilliseconds);
                if (result == 0) return;
                if (result == 258) throw new IOException("Менеджер не закрылся. Обновление отменено.");
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось дождаться закрытия менеджера.");
            }
        }

        private static string Normalize(string path)
        {
            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) path = @"\\" + path.Substring(8);
            else if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) path = path.Substring(4);
            return Path.GetFullPath(path);
        }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(SafeWaitHandle process, uint flags, StringBuilder path, ref int size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(SafeWaitHandle process, uint timeout);
    }
}
