using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    public static class SystemBrowser
    {
        public static ProcessStartInfo CreateStartInfo(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
                throw new ArgumentException("Нужна ссылка http или https.");
            return new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true };
        }
        public static void Open(string url)
        {
            try { Process.Start(CreateStartInfo(url)); }
            catch { MessageBox.Show("Не удалось открыть основной браузер Windows. Проверь браузер по умолчанию.", "Браузер"); }
        }
    }
}
