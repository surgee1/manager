using Newtonsoft.Json;
using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private bool portableBackupReady, portableBackupBusy, accountFileUnreadable;
        private int backupRevision, savedBackupRevision = -1;
        private DateTime backupDay, nextBackupAttempt;
        internal string BackupStatus { get; private set; } = "Копия будет создана после загрузки аккаунтов.";
        internal string BackupDirectory => General.Exists("PortableBackupDirectory")
            ? General.Get("PortableBackupDirectory") : Path.Combine(Environment.CurrentDirectory, "Backups");
        internal bool AutomaticBackupsEnabled => !General.Exists("PortableBackups") || General.Get<bool>("PortableBackups");
        internal static List<Account> ReadStoredAccounts(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 32 };
            var array = Newtonsoft.Json.Linq.JArray.Load(reader, new Newtonsoft.Json.Linq.JsonLoadSettings { DuplicatePropertyNameHandling = Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error });
            if (reader.TokenType != JsonToken.EndArray || reader.Depth != 0 || reader.Read()) throw new InvalidDataException("Неполный или повреждённый файл аккаунтов.");
            return array.Select(token => token is Newtonsoft.Json.Linq.JObject record
                ? Account.FromPortableRecord(record) : throw new InvalidDataException("Неверная запись аккаунта.")).ToList();
        }
        private void InitializePortableBackups()
        {
            if (Program.PreviewMode) return;
            var timer = new System.Windows.Forms.Timer(components) { Interval = 2000 };
            timer.Tick += async (sender, e) => await SaveAutomaticBackup();
            timer.Start();
            FormClosing += (sender, e) =>
            {
                if (e.Cancel || !portableBackupReady || !AutomaticBackupsEnabled || PasswordPanel.Visible) return;
                try
                {
                    // Flush on close, including changes made in the last two seconds.
                    PortableBackups.WriteAutomatic(BackupDirectory, CapturePortableBackup());
                }
                catch { BackupStatus = "Не удалось создать копию при закрытии. Проверь доступ к папке Backups."; }
            };
        }
        private void MarkBackupDirty() => Interlocked.Increment(ref backupRevision);
        private async Task SaveAutomaticBackup()
        {
            if (!portableBackupReady || PasswordPanel.Visible || portableBackupBusy || !AutomaticBackupsEnabled || DateTime.UtcNow < nextBackupAttempt) return;
            int revision = Volatile.Read(ref backupRevision);
            if (revision == savedBackupRevision && backupDay == DateTime.UtcNow.Date) return;
            portableBackupBusy = true;
            try
            {
                var snapshot = CapturePortableBackup();
                string directory = BackupDirectory;
                string path = await Task.Run(() => PortableBackups.WriteAutomatic(directory, snapshot));
                savedBackupRevision = revision; backupDay = snapshot.CreatedUtc.Date;
                BackupStatus = "Последняя копия: " + File.GetLastWriteTime(path).ToString("dd.MM.yyyy HH:mm") + " • " + snapshot.Accounts.Count + " аккаунтов";
            }
            catch
            {
                BackupStatus = "Копия не создана. Проверь свободное место и доступ к папке резервных копий.";
                nextBackupAttempt = DateTime.UtcNow.AddMinutes(1);
            }
            finally { portableBackupBusy = false; }
        }
        internal PortableBackup CapturePortableBackup()
        {
            if (PasswordPanel.Visible) throw new InvalidOperationException("Сначала разблокируй базу аккаунтов.");
            if (accountFileUnreadable) throw new InvalidOperationException("Текущий файл аккаунтов не прочитан. Сначала восстанови переносимую копию.");
            lock (saveLock) return PortableBackups.Capture(AccountsList ?? new List<Account>(), AllFolderNames());
        }
        internal int RestorePortableBackup(PortableBackup backup)
        {
            if (PasswordPanel.Visible) throw new InvalidOperationException("Сначала разблокируй базу аккаунтов.");
            if (folderLoadError != null) throw new InvalidOperationException(folderLoadError);
            // Validate and prepare everything before touching the current database.
            var merged = PortableBackups.Merge(AccountsList ?? new List<Account>(), backup, out int added);
            var folders = savedFolders.Concat(backup.Folders.Values<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (accountFileUnreadable)
            {
                if (File.Exists(SaveFilePath)) File.Copy(SaveFilePath, SaveFilePath + ".before-portable-restore-" + Guid.NewGuid().ToString("N") + ".bak");
            }
            else PortableBackups.Write(PortableBackups.NewPath(BackupDirectory, "before-restore"), CapturePortableBackup());
            lock (saveLock)
            {
                var oldAccounts = AccountsList;
                var oldFolders = savedFolders;
                bool wasUnreadable = accountFileUnreadable;
                byte[] oldFolderFile = File.Exists(FoldersPath) ? File.ReadAllBytes(FoldersPath) : null;
                try
                {
                    savedFolders = folders; SaveFolderNames();
                    AccountsList = merged;
                    accountFileUnreadable = false;
                    SaveAccounts(true, true);
                }
                catch
                {
                    AccountsList = oldAccounts; savedFolders = oldFolders;
                    accountFileUnreadable = wasUnreadable;
                    if (oldFolderFile != null) PortableBackups.AtomicWrite(FoldersPath, oldFolderFile);
                    else if (File.Exists(FoldersPath)) File.Delete(FoldersPath);
                    throw;
                }
            }
            portableBackupReady = true;
            SelectedAccounts.Clear(); LastValidAccount = AccountsList.OrderByDescending(account => account.LastUse).FirstOrDefault();
            AccountsView.SetObjects(AccountsList);
            RefreshView(); MarkBackupDirty();
            BackupStatus = "Копия восстановлена. Добавлено: " + added + ". Всего аккаунтов: " + AccountsList.Count + ".";
            return added;
        }
    }
}
