using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private NeonButton importAccountsButton;
        private void InitializeAccountImport()
        {
            importAccountsButton = new NeonButton { Text = "Импорт", Font = Font, Cursor = Cursors.Hand, AccessibleName = "Импорт аккаунтов из файла другого менеджера" };
            Controls.Add(importAccountsButton);
            Hint(importAccountsButton, "Перенести аккаунты из AccountData.json другого Roblox Account Manager или из переносимой копии. Без дублей; с папками и заметками.");
            importAccountsButton.Click += async (sender, e) => await ImportAccountsFromFile();
        }
        private async Task ImportAccountsFromFile()
        {
            using var file = new OpenFileDialog { Title = "Выбери AccountData.json исходного менеджера", Filter = "Файлы аккаунтов (*.json;*.backup;*.bak)|*.json;*.backup;*.bak|Все файлы (*.*)|*.*", CheckFileExists = true };
            if (file.ShowDialog(this) != DialogResult.OK) return;
            importAccountsButton.Enabled = false;
            try
            {
                PortableBackup source;
                try { source = await Task.Run(() => AccountImports.Read(file.FileName, Entropy)); }
                catch (ImportPasswordRequiredException)
                {
                    string password = PromptImportPassword();
                    if (password == null) return;
                    source = await Task.Run(() => AccountImports.Read(file.FileName, Entropy, password));
                }
                AccountImports.Merge(AccountsList, source, false, out int added, out int duplicates);
                using var preview = new Form { Text = "Импорт аккаунтов • Spectrum Manager", StartPosition = FormStartPosition.CenterParent,
                    ClientSize = new Size(545, 270), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false,
                    Font = Font, BackColor = Forms.ThemeEditor.FormsBackground, ForeColor = Forms.ThemeEditor.FormsForeground };
                var summary = new Label { Location = new Point(22, 20), Size = new Size(500, 90), Text = "Найдено аккаунтов: " + source.Accounts.Count + "\nНовых: " + added + " • Совпадений: " + duplicates + "\nПапки, псевдонимы и заметки будут перенесены. Перед импортом создадим резервную копию." };
                var replace = new CheckBox { Location = new Point(22, 120), Size = new Size(500, 42), Text = "Заменить сессии совпадающих аккаунтов данными из файла", Checked = false };
                var explanation = new Label { Location = new Point(22, 163), Size = new Size(500, 38), Text = "По умолчанию текущие сессии сохраняются. Включи замену, если вход работает только в исходном менеджере.", Font = new Font("Segoe UI", 9) };
                var accept = new NeonButton { Text = "Импортировать", Location = new Point(205, 218), Size = new Size(170, 36), DialogResult = DialogResult.OK };
                var cancel = new NeonButton { Text = "Отмена", Location = new Point(385, 218), Size = new Size(135, 36), DialogResult = DialogResult.Cancel };
                preview.Controls.AddRange(new Control[] { summary, replace, explanation, accept, cancel }); preview.Controls.ApplyTheme(); preview.AcceptButton = accept; preview.CancelButton = cancel;
                if (preview.ShowDialog(this) != DialogResult.OK) return;
                ImportAccountsCore(source, replace.Checked, out added, out duplicates);
                UiMessageBox.Show(this, "Импорт завершён. Добавлено: " + added + ". Совпадений объединено: " + duplicates + ".\nВсего аккаунтов: " + AccountsList.Count + ". Исходный файл не изменён.", "Импорт аккаунтов");
            }
            catch (InvalidDataException error) { UiMessageBox.Show(this, error.Message, "Импорт не выполнен", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch { UiMessageBox.Show(this, "Импорт не выполнен. Проверь файл и доступ к папке менеджера. Текущая база сохранена.", "Импорт аккаунтов"); }
            finally { importAccountsButton.Enabled = true; }
        }
        private string PromptImportPassword()
        {
            using var dialog = new Form { Text = "Мастер-пароль исходного менеджера", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(450, 160),
                FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, Font = Font,
                BackColor = Forms.ThemeEditor.FormsBackground, ForeColor = Forms.ThemeEditor.FormsForeground };
            var label = new Label { Text = "Введите мастер-пароль этого файла (не пароль Roblox).", Location = new Point(18, 18), Size = new Size(415, 30) };
            var input = new TextBox { UseSystemPasswordChar = true, Location = new Point(18, 55), Width = 410, MaxLength = 5000 };
            var accept = new NeonButton { Text = "Открыть", DialogResult = DialogResult.OK, Location = new Point(285, 105), Size = new Size(145, 36) };
            dialog.Controls.AddRange(new Control[] { label, input, accept }); dialog.AcceptButton = accept; dialog.Controls.ApplyTheme();
            return dialog.ShowDialog(this) == DialogResult.OK ? input.Text : null;
        }
        private void ImportAccountsCore(PortableBackup source, bool replaceSessions, out int added, out int duplicates)
        {
            if (PasswordPanel.Visible) throw new InvalidOperationException("Разблокируй менеджер перед импортом.");
            if (folderLoadError != null) throw new InvalidOperationException(folderLoadError);
            var merged = AccountImports.Merge(AccountsList, source, replaceSessions, out added, out duplicates);
            var folders = savedFolders.Concat(source.Folders.Values<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (accountFileUnreadable) File.Copy(SaveFilePath, SaveFilePath + ".before-file-import-" + Guid.NewGuid().ToString("N") + ".bak");
            else PortableBackups.Write(PortableBackups.NewPath(BackupDirectory, "before-import"), CapturePortableBackup());
            lock (saveLock)
            {
                var oldAccounts = AccountsList; var oldFolders = savedFolders; bool wasUnreadable = accountFileUnreadable;
                byte[] folderBytes = File.Exists(FoldersPath) ? File.ReadAllBytes(FoldersPath) : null;
                try { savedFolders = folders; SaveFolderNames(); AccountsList = merged; accountFileUnreadable = false; SaveAccounts(true, true); }
                catch
                {
                    AccountsList = oldAccounts; savedFolders = oldFolders; accountFileUnreadable = wasUnreadable;
                    if (folderBytes != null) PortableBackups.AtomicWrite(FoldersPath, folderBytes); else if (File.Exists(FoldersPath)) File.Delete(FoldersPath);
                    throw;
                }
            }
            portableBackupReady = true; SelectedAccounts.Clear(); SelectedAccount = null;
            LastValidAccount = AccountsList.OrderByDescending(account => account.LastUse).FirstOrDefault();
            accountSearch.Clear(); AccountsView.SetObjects(AccountsList); UpdateFolderSelector(); folderSelector.SelectedIndex = 0; RefreshView(); MarkBackupDirty();
        }
    }
}
