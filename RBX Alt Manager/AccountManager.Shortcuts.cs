using BrightIdeasSoftware;
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
        private TextBox accountSearch;
        private NeonButton profilesButton;
        private List<LaunchProfile> launchProfiles = new List<LaunchProfile>();
        private string profilesError;
        private string ProfilesPath => Path.Combine(Environment.CurrentDirectory, "LaunchProfiles.json");
        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SearchCue(IntPtr handle, uint message, IntPtr parameter, string text);
        private void InitializeShortcuts()
        {
            accountSearch = new TextBox { Font = Font, BorderStyle = BorderStyle.None, AccessibleName = "Поиск аккаунтов",
                BackColor = ThemeEditor.TextBoxesBackground, ForeColor = ThemeEditor.TextBoxesForeground };
            Controls.Add(accountSearch);
            SearchCue(accountSearch.Handle, 0x1501, new IntPtr(1), "Поиск аккаунтов…  Ctrl+F");
            Hint(accountSearch, "Поиск по нику, псевдониму, заметкам и папке. Ctrl+F — поиск, Esc — очистить.");
            var debounce = new Timer(components) { Interval = 120 };
            accountSearch.TextChanged += (sender, e) => { debounce.Stop(); debounce.Start(); };
            debounce.Tick += (sender, e) => { debounce.Stop(); AccountsView.BuildList(); UpdateAccountSearchCaption(); };
            accountSearch.Enter += (sender, e) => Invalidate(); accountSearch.Leave += (sender, e) => Invalidate();
            profilesButton = new NeonButton { Text = "Профили ▾", Font = Font, Cursor = Cursors.Hand };
            Controls.Add(profilesButton);
            profilesButton.Click += (sender, e) => ShowProfilesMenu();
            Hint(profilesButton, "Сохрани игру, приватную ссылку и папку аккаунтов под одним названием. Выбор профиля заполняет поля.");
            try { if (!Program.PreviewMode) launchProfiles = LaunchProfiles.Read(ProfilesPath); }
            catch { profilesError = "Не удалось прочитать LaunchProfiles.json. Исходный файл сохранён без изменений."; }
            KeyPreview = true;
        }
        private bool AccountMatchesSearch(Account account)
        {
            string query = accountSearch?.Text.Trim() ?? "";
            if (query.Length == 0) return true;
            return new[] { account.Username, account.Alias, account.Description, FolderName(account.Group) }
                .Any(value => value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        private void UpdateAccountSearchCaption()
        {
            if (accountSearch == null) return;
            int visible = (AccountsList ?? new List<Account>()).Count(account =>
                (ActiveFolder == null || string.Equals(FolderName(account.Group), ActiveFolder, StringComparison.OrdinalIgnoreCase)) && AccountMatchesSearch(account));
            accountsHeading.Text = string.IsNullOrWhiteSpace(accountSearch.Text) ? "АККАУНТЫ  ·  " + visible : "НАЙДЕНО: " + visible;
            AccountsView.EmptyListMsg = !string.IsNullOrWhiteSpace(accountSearch.Text) ? "Ничего не найдено. Измени запрос или папку." : "В этой папке пока нет аккаунтов.";
        }
        private void SelectVisibleAccounts()
        {
            AccountsView.SelectedObjects = (AccountsList ?? new List<Account>()).Where(account =>
                (ActiveFolder == null || string.Equals(FolderName(account.Group), ActiveFolder, StringComparison.OrdinalIgnoreCase)) && AccountMatchesSearch(account)).ToList();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!PasswordPanel.Visible && accountSearch != null)
            {
                if (keyData == (Keys.Control | Keys.F)) { accountSearch.Focus(); accountSearch.SelectAll(); return true; }
                if (keyData == (Keys.Control | Keys.K)) { ShowQuickCommands(); return true; }
                if (keyData == (Keys.Control | Keys.Shift | Keys.A)) { SelectVisibleAccounts(); return true; }
                if (keyData == (Keys.Control | Keys.Oemcomma)) { ConfigButton.PerformClick(); return true; }
                if (keyData == Keys.Escape && accountSearch.Focused) { accountSearch.Clear(); AccountsView.Focus(); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        private void ShowProfilesMenu()
        {
            var menu = new ContextMenuStrip();
            if (profilesError != null) menu.Items.Add(profilesError).Enabled = false;
            foreach (var profile in launchProfiles)
            {
                var item = menu.Items.Add(profile.Name, null, (sender, e) => ApplyLaunchProfile(profile));
                item.ToolTipText = "Place ID: " + profile.PlaceId + " • " + (profile.Folder ?? "Все аккаунты");
            }
            if (launchProfiles.Count == 0) menu.Items.Add("Пока нет сохранённых профилей").Enabled = false;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Сохранить текущие поля…", null, (sender, e) => PromptSaveLaunchProfile()).Enabled = profilesError == null;
            var remove = new ToolStripMenuItem("Удалить профиль");
            foreach (var profile in launchProfiles)
                remove.DropDownItems.Add(profile.Name, null, (sender, e) =>
                {
                    if (MessageBox.Show(this, "Удалить профиль «" + profile.Name + "»? Аккаунты останутся.", "Профили", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    try { var next = launchProfiles.Where(item => item != profile).ToList(); LaunchProfiles.Save(ProfilesPath, next); launchProfiles = next; }
                    catch { MessageBox.Show(this, "Не удалось сохранить файл профилей."); }
                });
            remove.Enabled = launchProfiles.Count > 0 && profilesError == null; menu.Items.Add(remove);
            menu.Closed += (sender, e) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(profilesButton, new Point(0, profilesButton.Height));
        }
        private void SaveLaunchProfileCore(string name)
        {
            if (profilesError != null) throw new InvalidOperationException(profilesError);
            var profile = new LaunchProfile { Name = name.Trim(), PlaceId = PlaceID.Text.Trim(), Server = JobID.Text.Trim(),
                Folder = ActiveFolder, GameName = CurrentPlace.Text };
            var next = LaunchProfiles.Upsert(launchProfiles, profile);
            LaunchProfiles.Save(ProfilesPath, next); launchProfiles = next;
        }
        private void PromptSaveLaunchProfile()
        {
            if (!long.TryParse(PlaceID.Text.Trim(), out long place) || place <= 0) { MessageBox.Show(this, "Сначала выбери игру или введи Place ID."); return; }
            using var dialog = new Form { Text = "Новый профиль запуска", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(470, 185),
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Font = Font,
                BackColor = ThemeEditor.FormsBackground, ForeColor = ThemeEditor.FormsForeground };
            var label = new Label { Text = "Название профиля", AutoSize = true, Location = new Point(20, 18) };
            var input = new TextBox { Location = new Point(20, 47), Width = 430, MaxLength = 60 };
            var hint = new Label { Text = "Сохранится игра, сервер и текущая папка аккаунтов.", AutoSize = true, Location = new Point(20, 87) };
            var save = new NeonButton { Text = "Сохранить", Location = new Point(290, 125), Size = new Size(160, 38) };
            dialog.Controls.AddRange(new Control[] { label, input, hint, save }); dialog.AcceptButton = save; dialog.Controls.ApplyTheme();
            save.Click += (sender, e) =>
            {
                if (launchProfiles.Any(item => item.Name.Equals(input.Text.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                    MessageBox.Show(dialog, "Заменить существующий профиль?", "Профили", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                try { SaveLaunchProfileCore(input.Text); dialog.DialogResult = DialogResult.OK; }
                catch { MessageBox.Show(dialog, "Введи название от 1 до 60 символов и корректный Place ID. Проверь доступ к файлу."); }
            };
            dialog.ShowDialog(this);
        }
        private void ApplyLaunchProfile(LaunchProfile profile)
        {
            if (profile.Folder != null && !AllFolderNames().Contains(profile.Folder, StringComparer.OrdinalIgnoreCase))
            { MessageBox.Show(this, "Папка профиля была удалена. Выбери папку и сохрани профиль заново."); return; }
            accountSearch.Clear();
            UpdateFolderSelector(profile.Folder);
            folderSelector.SelectedItem = folderSelector.Items.Cast<FolderItem>().First(item => string.Equals(item.Name, profile.Folder, StringComparison.OrdinalIgnoreCase));
            AccountsView.SelectedObjects = new List<Account>(); SelectedAccounts.Clear();
            PlaceID.Text = profile.PlaceId; JobID.Text = profile.Server ?? ""; CurrentPlace.Text = profile.GameName ?? "Выбери игру";
            FlushLaunchInputs();
        }
        private void ShowQuickCommands()
        {
            var commands = new List<Tuple<string, Action>> {
                Tuple.Create("Поиск аккаунтов  ·  Ctrl+F", new Action(() => accountSearch.Focus())),
                Tuple.Create("Выбрать все видимые аккаунты  ·  Ctrl+Shift+A", new Action(SelectVisibleAccounts)),
                Tuple.Create("Снять выделение аккаунтов", new Action(() => AccountsView.SelectedObjects = new List<Account>())),
                Tuple.Create("Новая папка аккаунтов", new Action(() => PromptNewFolder(false))),
                Tuple.Create("Сохранить профиль запуска", new Action(PromptSaveLaunchProfile)),
                Tuple.Create("Игры и избранное", new Action(ShowPlacePicker)),
                Tuple.Create("Настройки  ·  Ctrl+,", new Action(() => ConfigButton.PerformClick())),
                Tuple.Create("Резервные копии и восстановление", new Action(() => { ConfigButton.PerformClick(); SettingsForm.ShowAccountBackups(); })),
                Tuple.Create("Проверить обновления", new Action(() => modernUpdateButton.PerformClick()))
            };
            commands.AddRange(launchProfiles.Select(profile => Tuple.Create("Профиль: " + profile.Name, new Action(() => ApplyLaunchProfile(profile)))));
            commands.Add(Tuple.Create("Вернуть удалённые аккаунты", new Action(() =>
            { try { UndoLastRemoval(); } catch { MessageBox.Show(this, "Не удалось сохранить восстановленные записи. Проверь доступ к папке менеджера."); } })));
            using var dialog = new QuickCommands(commands);
            dialog.ShowDialog(this);
        }
    }
}
