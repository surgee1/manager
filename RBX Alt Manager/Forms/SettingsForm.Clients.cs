using RBX_Alt_Manager.Classes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Forms
{
    public partial class SettingsForm
    {
        private CancellationTokenSource clientDetection;
        private void InitializeClientSettings(FlowLayoutPanel flow)
        {
            Section(flow, "LEGIT / CHEAT");
            Description(flow, "Legit — отдельный официальный Roblox. Cheat — клиент, который ты открыл через свой инжектор. Выбор режима находится под кнопкой подключения.");
            var legit = ClientPathRow(flow, false);
            var download = new NeonButton();
            Setting(flow, download, "Скачать / обновить Legit", "Скачивает актуальную версию LIVE с серверов Roblox, проверяет пакеты и устанавливает отдельно. Старый клиент и привязка Cheat сохраняются.");
            download.Click += async (sender, e) =>
            {
                download.Enabled = false;
                try { var path = await AccountManager.Instance.DownloadLegitClientAsync(); if (path != null) legit.Text = path; }
                catch (Exception error) { MessageBox.Show(this, error.Message, "Загрузка Legit", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { if (!download.IsDisposed) download.Enabled = true; }
            };
            var cheat = ClientPathRow(flow, true);
            var detect = new NeonButton();
            Setting(flow, detect, "Определить Cheat", "Находит путь запущенного Roblox. Если игра закрыта, нажми кнопку и затем открой Roblox через свой инжектор. Закрывать игры менеджер не будет.");
            var status = new Label { Width = 650, Height = 56, Text = ClientVersion(cheat.Text), Margin = new Padding(0, 0, 0, 8) };
            flow.Controls.Add(status);
            detect.Click += async (sender, e) =>
            {
                if (clientDetection != null) { clientDetection.Cancel(); return; }
                var paths = RobloxClients.RunningIds().Select(RobloxClients.ProcessPath).Where(path => !string.IsNullOrEmpty(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (paths.Length > 0)
                {
                    var menu = new ContextMenuStrip();
                    foreach (string path in paths) menu.Items.Add(path, null, (s, args) => BindCheat(path, cheat, status));
                    menu.Items.Add("Ждать новый запуск…", null, async (s, args) => await DetectNewClientAsync(detect, cheat, status));
                    menu.Closed += (s, args) => BeginInvoke(new Action(menu.Dispose));
                    menu.Show(detect, new Point(0, detect.Height));
                }
                else await DetectNewClientAsync(detect, cheat, status);
            };
            Disposed += (sender, e) => clientDetection?.Cancel();
        }
        private TextBox ClientPathRow(FlowLayoutPanel flow, bool cheat)
        {
            Description(flow, cheat ? "Путь Cheat" : "Путь Legit");
            var row = new FlowLayoutPanel { Width = 650, Height = 42, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            var path = new TextBox { ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = ThemeEditor.TextBoxesBackground, ForeColor = ThemeEditor.TextBoxesForeground,
                Text = RobloxClients.ConfiguredPath(cheat), Width = 470, Margin = new Padding(0, 7, 8, 0) };
            var browse = new NeonButton { Text = "Выбрать…", Width = 145, Height = 36, Font = Font };
            browse.Click += (sender, e) =>
            {
                using (var dialog = new OpenFileDialog { Title = cheat ? "Выбрать клиент Cheat" : "Выбрать официальный клиент", Filter = "Roblox|RobloxPlayerBeta.exe", CheckFileExists = true })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try { SaveClientPath(cheat, dialog.FileName); path.Text = dialog.FileName; }
                    catch (Exception error) { MessageBox.Show(this, error.Message, "Клиент Roblox"); }
                }
            };
            row.Controls.Add(path); row.Controls.Add(browse); flow.Controls.Add(row);
            Helper.SetToolTip(path, "Путь сохраняется автоматически. После обновления инжектора можно определить клиент повторно.");
            return path;
        }
        private static void SaveClientPath(bool cheat, string path)
        {
            path = RobloxClients.Validate(path);
            string other = RobloxClients.ConfiguredPath(!cheat);
            if (!string.IsNullOrWhiteSpace(other) && string.Equals(Path.GetFullPath(other), path, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Legit и Cheat должны находиться в разных папках.");
            AccountManager.General.Set(cheat ? "CheatRobloxPath" : "LegitRobloxPath", path);
            AccountManager.IniSettings.Save("RAMSettings.ini");
        }
        private void BindCheat(string path, TextBox target, Label status)
        {
            try { SaveClientPath(true, path); target.Text = path; status.Text = "Cheat привязан · " + ClientVersion(path); }
            catch (Exception error) { status.Text = error.Message; }
        }
        private static string ClientVersion(string path)
        {
            try { return File.Exists(path) ? "Сборка " + System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion + " · " + Path.GetFileName(Path.GetDirectoryName(path)) : "Привязывается путь клиента, а не DLL инжектора."; }
            catch { return "Проверь путь к клиенту Roblox."; }
        }
        private async Task DetectNewClientAsync(NeonButton button, TextBox target, Label status)
        {
            if (clientDetection != null) return;
            var known = RobloxClients.RunningIds();
            using (var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
            {
                clientDetection = cancel; button.Text = "Отменить определение";
                status.Text = "Открой Roblox через свой инжектор. Ожидаю новый процесс (2 минуты)…";
                try
                {
                    while (true)
                    {
                        await Task.Delay(500, cancel.Token);
                        var paths = RobloxClients.RunningIds().Except(known).Select(RobloxClients.ProcessPath).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                        if (paths.Length > 1) { status.Text = "Запущено несколько разных клиентов. Нажми «Определить Cheat» и выбери нужный путь."; return; }
                        if (paths.Length == 1) { BindCheat(paths[0], target, status); return; }
                    }
                }
                catch (OperationCanceledException) { if (!IsDisposed) status.Text = "Определение остановлено. Прежняя привязка сохранена."; }
                finally { clientDetection = null; if (!button.IsDisposed) button.Text = "Определить Cheat"; }
            }
        }
    }
}
