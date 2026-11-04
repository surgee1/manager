using RBX_Alt_Manager.Classes;
using System;
using System.Drawing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private NeonButton legitModeButton, cheatModeButton;
        private bool downloadingLegit;
        private void InitializeClientModes()
        {
            legitModeButton = new NeonButton { Text = "Legit", Font = Font, Cursor = Cursors.Hand, AccessibleName = "Официальный клиент Roblox" };
            cheatModeButton = new NeonButton { Text = "Cheat", Font = Font, Cursor = Cursors.Hand, AccessibleName = "Привязанный клиент Roblox" };
            Controls.Add(legitModeButton); Controls.Add(cheatModeButton);
            legitModeButton.Click += (sender, e) => SetClientMode(false);
            cheatModeButton.Click += (sender, e) => SetClientMode(true);
            Hint(legitModeButton, "Запуск отдельного официального Roblox. Если клиент ещё не установлен, менеджер скачает его при подключении.");
            Hint(cheatModeButton, "Запуск привязанного Roblox. Путь определяется в Настройки → Запуск → Определить Cheat. Менеджер сам не запускает инжектор.");
            RefreshClientModes();
        }
        private void SetClientMode(bool cheat)
        {
            General.Set("ClientLaunchMode", cheat ? "Cheat" : "Legit");
            IniSettings.Save("RAMSettings.ini"); RefreshClientModes();
        }
        private void RefreshClientModes()
        {
            if (legitModeButton == null) return;
            legitModeButton.Accent = !RobloxClients.Cheat; cheatModeButton.Accent = RobloxClients.Cheat;
            legitModeButton.Invalidate(); cheatModeButton.Invalidate();
        }
        internal async Task<string> DownloadLegitClientAsync()
        {
            if (downloadingLegit) throw new InvalidOperationException("Legit уже скачивается. Дождись завершения загрузки.");
            downloadingLegit = true;
            using (var cancel = new CancellationTokenSource())
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            using (var dialog = new Form { Text = "Официальный Roblox · Spectrum Manager", ClientSize = new Size(560, 200),
                StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false,
                FormBorderStyle = FormBorderStyle.FixedDialog, Font = Font, BackColor = BackColor, ForeColor = ForeColor })
            {
                var status = new Label { Text = "Проверка версии Roblox…", AutoSize = false, Bounds = new Rectangle(24, 26, 510, 48) };
                var info = new Label { Text = "Официальный канал LIVE • отдельная папка Spectrum Manager\nТекущий клиент останется доступен до завершения загрузки.", Bounds = new Rectangle(24, 76, 510, 50) };
                var stop = new NeonButton { Text = "Отмена", Bounds = new Rectangle(385, 138, 145, 38), Font = Font };
                dialog.Controls.AddRange(new Control[] { status, info, stop });
                stop.Click += (sender, e) => { stop.Enabled = false; status.Text = "Отмена загрузки…"; cancel.Cancel(); };
                dialog.FormClosing += (sender, e) => { if (downloadingLegit) { e.Cancel = true; stop.PerformClick(); } };
                RoundedWindow.Attach(dialog);
                string path = null; Exception failure = null;
                dialog.Shown += async (sender, e) =>
                {
                    try
                    {
                        path = await new OfficialRobloxInstaller(http).InstallAsync(RobloxClients.ManagedRoot,
                            new Progress<string>(text => { if (!dialog.IsDisposed) status.Text = text; }), cancel.Token);
                        General.Set("LegitRobloxPath", path); IniSettings.Save("RAMSettings.ini");
                    }
                    catch (Exception error) { failure = error; }
                    finally { downloadingLegit = false; dialog.Close(); }
                };
                try { dialog.ShowDialog(this); }
                finally { downloadingLegit = false; }
                if (failure is OperationCanceledException) return null;
                if (failure != null) throw new InvalidOperationException("Не удалось скачать Legit. Предыдущая установка сохранена.\n" + failure.Message, failure);
                return path;
            }
        }
        private async Task<string> PrepareSelectedClientAsync()
        {
            bool cheat = RobloxClients.Cheat;
            string path = RobloxClients.ConfiguredPath(cheat);
            if (!cheat && !System.IO.File.Exists(path)) path = await DownloadLegitClientAsync();
            return path == null ? null : RobloxClients.Validate(path);
        }
    }
}
