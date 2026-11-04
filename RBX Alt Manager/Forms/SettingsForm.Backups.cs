using RBX_Alt_Manager.Classes;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Forms
{
    public partial class SettingsForm
    {
        private Label backupStatusLabel;
        private TabPage backupsTab;
        internal void ShowAccountBackups() { SettingsTC.SelectedTab = backupsTab; }
        private void InitializeBackupSettings(FlowLayoutPanel page)
        {
            backupsTab = (TabPage)page.Parent;
            Section(page, "ПЕРЕНОСИМЫЕ РЕЗЕРВНЫЕ КОПИИ");
            Description(page, "Восстанавливаются после переустановки Windows без старых ключей шифрования. Сохраняются аккаунты, сессии, пароли, заметки и папки.");
            var automatic = new CheckBox { Checked = AccountManager.Instance.AutomaticBackupsEnabled };
            Setting(page, automatic, "Создавать копии автоматически", "После изменения данных и раз в день при открытом менеджере. Храним 30 автоматических копий; ручные не удаляются.");
            automatic.CheckedChanged += (sender, e) =>
            {
                AccountManager.General.Set("PortableBackups", automatic.Checked.ToString());
                AccountManager.IniSettings.Save("RAMSettings.ini");
            };
            var path = new Label { AutoSize = true, MaximumSize = new Size(630, 0), Text = AccountManager.Instance.BackupDirectory };
            page.Controls.Add(path);
            var actions = new FlowLayoutPanel { Width = 650, Height = 45, WrapContents = false, Margin = new Padding(0, 8, 0, 8) };
            var open = new NeonButton { Text = "Открыть папку", Width = 200, Height = 38 };
            var choose = new NeonButton { Text = "Выбрать папку…", Width = 210, Height = 38 };
            actions.Controls.Add(open); actions.Controls.Add(choose); page.Controls.Add(actions);
            open.Click += (sender, e) => BackupAction(() =>
            {
                Directory.CreateDirectory(AccountManager.Instance.BackupDirectory);
                Process.Start(new ProcessStartInfo(AccountManager.Instance.BackupDirectory) { UseShellExecute = true });
            });
            choose.Click += (sender, e) => BackupAction(() =>
            {
                using var dialog = new FolderBrowserDialog { Description = "Папка для переносимых копий аккаунтов", SelectedPath = AccountManager.Instance.BackupDirectory };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                AccountManager.General.Set("PortableBackupDirectory", dialog.SelectedPath);
                AccountManager.IniSettings.Save("RAMSettings.ini");
                path.Text = AccountManager.Instance.BackupDirectory;
                backupStatusLabel.Text = "Новая папка выбрана. Предыдущие копии остаются на месте.";
            });
            var export = new NeonButton();
            Setting(page, export, "Создать / экспортировать копию…", "Сохраняет переносимый JSON в выбранный файл. Его можно перенести на другой компьютер.");
            export.Click += async (sender, e) =>
            {
                try
                {
                    var snapshot = AccountManager.Instance.CapturePortableBackup();
                    Directory.CreateDirectory(AccountManager.Instance.BackupDirectory);
                    using var dialog = new SaveFileDialog { Filter = "Копия Spectrum Manager (*.surgee-backup.json)|*.surgee-backup.json", DefaultExt = "surgee-backup.json",
                        FileName = "accounts-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + PortableBackups.Extension,
                        InitialDirectory = AccountManager.Instance.BackupDirectory, Title = "Копия содержит данные входа — храни её у себя", OverwritePrompt = true };
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    export.Enabled = false;
                    await Task.Run(() => PortableBackups.Write(dialog.FileName, snapshot));
                    backupStatusLabel.Text = "Копия сохранена: " + snapshot.Accounts.Count + " аккаунтов. " + Path.GetFileName(dialog.FileName);
                }
                catch { backupStatusLabel.Text = "Не удалось сохранить копию. Проверь папку и свободное место."; }
                finally { export.Enabled = true; }
            };
            var restore = new NeonButton();
            Setting(page, restore, "Восстановить из копии…", "Объединяет без дублей. Текущие сессии и заполненные данные сохраняются; недостающие данные добавляются из копии. Перед восстановлением создаётся отдельная копия текущей базы.");
            restore.Click += async (sender, e) =>
            {
                using var dialog = new OpenFileDialog { Filter = "Копия Spectrum Manager (*.surgee-backup.json)|*.surgee-backup.json|JSON (*.json)|*.json", InitialDirectory = AccountManager.Instance.BackupDirectory, Title = "Выбери переносимую копию Spectrum Manager" };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                restore.Enabled = false;
                try
                {
                    var backup = await Task.Run(() => PortableBackups.Read(dialog.FileName));
                    if (MessageBox.Show(this, "Копия от " + backup.CreatedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm") + "\nАккаунтов: " + backup.Accounts.Count + "\n\nОбъединить с текущей базой?", "Восстановление", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    int added = AccountManager.Instance.RestorePortableBackup(backup);
                    backupStatusLabel.Text = "Восстановлено. Добавлено аккаунтов: " + added + ". Всего: " + AccountManager.AccountsList.Count;
                }
                catch { backupStatusLabel.Text = "Восстановление не выполнено. Проверь файл копии и доступ к папке менеджера. Текущая база сохранена."; }
                finally { restore.Enabled = true; }
            };
            Description(page, "Копии без пароля содержат данные входа. Не отправляй их другим людям. Перед очисткой диска C: скопируй Backups на другой диск или флешку.");
            backupStatusLabel = new Label { Text = AccountManager.Instance.BackupStatus, AutoSize = true, MaximumSize = new Size(630, 0), Margin = new Padding(0, 10, 0, 18) };
            page.Controls.Add(backupStatusLabel);
            VisibleChanged += (sender, e) => { if (Visible) { path.Text = AccountManager.Instance.BackupDirectory; backupStatusLabel.Text = AccountManager.Instance.BackupStatus; } };
        }
        private void BackupAction(Action action)
        { try { action(); } catch { backupStatusLabel.Text = "Не удалось открыть или изменить папку копий. Проверь доступ."; } }
    }
}
