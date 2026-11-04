using BrightIdeasSoftware;
using Newtonsoft.Json;
using RBX_Alt_Manager.Classes;
using RBX_Alt_Manager.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private ComboBox folderSelector;
        private NeonButton folderCreate, folderMenu;
        private bool updatingFolders;
        private List<string> savedFolders = new List<string>();
        private string folderLoadError;
        private string FoldersPath => Path.Combine(Environment.CurrentDirectory, "AccountFolders.json");
        private sealed class FolderItem
        {
            public string Name;
            public int Count;
            public override string ToString() => (Name ?? "Все аккаунты") + "  (" + Count + ")";
        }
        private static string FolderName(string group) => string.IsNullOrWhiteSpace(group) || group == "Default" ? "Без папки" : group;
        private string ActiveFolder => (folderSelector?.SelectedItem as FolderItem)?.Name;
        private string FolderForNewAccount => ActiveFolder == null || ActiveFolder == "Без папки" ? "Default" : ActiveFolder;

        private void InitializeAccountFolders()
        {
            try { if (File.Exists(FoldersPath)) savedFolders = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(FoldersPath)) ?? new List<string>(); }
            catch { folderLoadError = "Не удалось прочитать AccountFolders.json. Файл не будет перезаписан."; }
            folderSelector = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Font,
                DrawMode = DrawMode.OwnerDrawFixed, FlatStyle = FlatStyle.Flat, ItemHeight = Ui(24), AccessibleName = "Папка аккаунтов",
                BackColor = ThemeEditor.TextBoxesBackground, ForeColor = ThemeEditor.TextBoxesForeground };
            folderSelector.DrawItem += (sender, e) =>
            {
                using (var fill = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? ThemeEditor.ButtonsBackground : ThemeEditor.TextBoxesBackground)) e.Graphics.FillRectangle(fill, e.Bounds);
                if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, folderSelector.Items[e.Index].ToString(), Font,
                    new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height), ThemeEditor.FormsForeground,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            folderSelector.SelectedIndexChanged += (sender, e) => { if (!updatingFolders) { AccountsView.BuildList(); UpdateAccountSearchCaption(); } };
            folderCreate = new NeonButton { Text = "+ Папка", Font = Font, Cursor = Cursors.Hand, AccessibleName = "Создать папку" };
            folderCreate.Click += (sender, e) => PromptNewFolder(false);
            folderMenu = new NeonButton { Text = "⋯", Font = Font, Cursor = Cursors.Hand, AccessibleName = "Действия с папками" };
            folderMenu.Click += (sender, e) => ShowFolderMenu();
            Controls.Add(folderSelector); Controls.Add(folderCreate); Controls.Add(folderMenu);
            Hint(folderSelector, "Выбери папку, чтобы показать только её аккаунты. Пустые папки тоже сохраняются.");
            Hint(folderCreate, "Создать новую папку аккаунтов.");
            Hint(folderMenu, "Переместить выбранные аккаунты, переименовать или удалить папку.");
            AccountsView.UseFiltering = true;
            AccountsView.ModelFilter = new ModelFilter(model => (ActiveFolder == null || string.Equals(FolderName(((Account)model).Group), ActiveFolder, StringComparison.OrdinalIgnoreCase)) && AccountMatchesSearch((Account)model));
            moveToToolStripMenuItem.Click -= moveToToolStripMenuItem_Click;
            moveToToolStripMenuItem.Text = "Переместить в папку";
            moveToToolStripMenuItem.DropDownOpening += (sender, e) => FillMoveMenu(moveToToolStripMenuItem.DropDownItems);
            moveToToolStripMenuItem.DropDownItems.Add("Выбрать папку");
            UpdateFolderSelector();
        }
        private IEnumerable<string> AllFolderNames() => savedFolders.Concat((AccountsList ?? new List<Account>()).Select(account => FolderName(account.Group)))
            .Concat(new[] { "Без папки" }).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name);
        private void UpdateFolderSelector(string selected = null)
        {
            if (folderSelector == null || updatingFolders) return;
            selected = selected ?? ActiveFolder;
            var accounts = AccountsList ?? new List<Account>();
            var names = AllFolderNames().ToList();
            // Avoid rebuilding a live dropdown on every account repaint.
            var desired = new List<FolderItem> { new FolderItem { Name = null, Count = accounts.Count } };
            desired.AddRange(names.Select(name => new FolderItem { Name = name, Count = accounts.Count(account => string.Equals(FolderName(account.Group), name, StringComparison.OrdinalIgnoreCase)) }));
            if (folderSelector.Items.Count == desired.Count && folderSelector.Items.Cast<FolderItem>().Zip(desired, (a, b) => a.Name == b.Name && a.Count == b.Count).All(equal => equal)) return;
            updatingFolders = true;
            try
            {
                folderSelector.BeginUpdate(); folderSelector.Items.Clear(); folderSelector.Items.AddRange(desired.Cast<object>().ToArray());
                folderSelector.SelectedIndex = Math.Max(0, desired.FindIndex(item => item.Name == selected));
            }
            finally { folderSelector.EndUpdate(); updatingFolders = false; }
        }
        private void SaveFolderNames()
        {
            if (folderLoadError != null) throw new InvalidOperationException(folderLoadError);
            string temp = FoldersPath + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(savedFolders, Formatting.Indented));
            if (File.Exists(FoldersPath)) File.Replace(temp, FoldersPath, FoldersPath + ".backup");
            else File.Move(temp, FoldersPath);
            MarkBackupDirty();
        }
        private void ValidateFolder(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 40 || name == "Все аккаунты" || name == "Без папки" || name == "Default")
                throw new InvalidOperationException("Введи название от 1 до 40 символов. «Без папки» и «Все аккаунты» — служебные названия.");
            if (AllFolderNames().Contains(name, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Папка с таким названием уже есть.");
        }
        private void CreateFolderCore(string name)
        {
            name = name.Trim(); ValidateFolder(name); savedFolders.Add(name); SaveFolderNames(); UpdateFolderSelector(name);
            folderSelector.SelectedItem = folderSelector.Items.Cast<FolderItem>().First(item => item.Name == name);
            AccountsView.BuildList();
        }
        private void MoveSelectedToFolder(string name)
        {
            foreach (var account in AccountsView.SelectedObjects.Cast<Account>().ToList()) account.Group = name == "Без папки" ? "Default" : name;
            SaveAccounts(true); RefreshView();
        }
        private void RenameFolderCore(string oldName, string newName)
        {
            newName = newName.Trim(); ValidateFolder(newName);
            savedFolders.RemoveAll(name => name.Equals(oldName, StringComparison.OrdinalIgnoreCase)); savedFolders.Add(newName); SaveFolderNames();
            foreach (var account in AccountsList.Where(account => string.Equals(FolderName(account.Group), oldName, StringComparison.OrdinalIgnoreCase))) account.Group = newName;
            SaveAccounts(true); UpdateFolderSelector(newName); RefreshView();
        }
        private void DeleteFolderCore(string name)
        {
            if (name == "Без папки") throw new InvalidOperationException("Служебную папку удалить нельзя.");
            savedFolders.RemoveAll(item => item.Equals(name, StringComparison.OrdinalIgnoreCase)); SaveFolderNames();
            foreach (var account in AccountsList.Where(account => string.Equals(FolderName(account.Group), name, StringComparison.OrdinalIgnoreCase))) account.Group = "Default";
            SaveAccounts(true); UpdateFolderSelector("Без папки"); RefreshView();
        }
        private void PromptNewFolder(bool move)
        {
            string name = ShowDialog("Название новой папки", "Создать папку");
            if (name == "/UC") return;
            var selected = AccountsView.SelectedObjects.Cast<Account>().ToList();
            try
            {
                CreateFolderCore(name);
                if (move) { foreach (var account in selected) account.Group = name.Trim(); SaveAccounts(true); RefreshView(); }
            }
            catch (Exception e) { MessageBox.Show(e.Message, "Папки аккаунтов"); }
        }
        private void FillMoveMenu(ToolStripItemCollection items)
        {
            items.Clear();
            foreach (string name in AllFolderNames())
            {
                string target = name;
                items.Add(name, null, (sender, e) => { try { MoveSelectedToFolder(target); } catch (Exception error) { MessageBox.Show(error.Message, "Папки аккаунтов"); } });
            }
            items.Add(new ToolStripSeparator()); items.Add("+ Новая папка и перенести…", null, (sender, e) => PromptNewFolder(true));
        }
        private void ShowFolderMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Выбрать видимые  ·  Ctrl+Shift+A", null, (sender, e) => SelectVisibleAccounts());
            menu.Items.Add("Снять выделение", null, (sender, e) => AccountsView.SelectedObjects = new List<Account>());
            menu.Items.Add("Вернуть удалённые аккаунты", null, (sender, e) =>
            { try { UndoLastRemoval(); } catch { MessageBox.Show(this, "Не удалось сохранить восстановленные записи."); } });
            menu.Items.Add(new ToolStripSeparator());
            var move = new ToolStripMenuItem("Переместить выбранные") { Enabled = AccountsView.SelectedObjects.Count > 0 };
            FillMoveMenu(move.DropDownItems); menu.Items.Add(move);
            string name = ActiveFolder;
            var rename = new ToolStripMenuItem("Переименовать папку…") { Enabled = name != null && name != "Без папки" };
            rename.Click += (sender, e) => { string next = ShowDialog("Новое название папки", "Переименовать папку", name); if (next == "/UC" || next == name) return; try { RenameFolderCore(name, next); } catch (Exception error) { MessageBox.Show(error.Message, "Папки аккаунтов"); } };
            menu.Items.Add(rename);
            var remove = new ToolStripMenuItem("Удалить папку…") { Enabled = name != null && name != "Без папки" };
            remove.Click += (sender, e) => { if (MessageBox.Show("Удалить папку «" + name + "»? Аккаунты будут перенесены в «Без папки».", "Удалить папку", MessageBoxButtons.YesNo) == DialogResult.Yes) try { DeleteFolderCore(name); } catch (Exception error) { MessageBox.Show(error.Message, "Папки аккаунтов"); } };
            menu.Items.Add(remove); menu.Closed += (sender, e) => menu.Dispose(); menu.Show(folderMenu, new Point(0, folderMenu.Height));
        }
    }
}
