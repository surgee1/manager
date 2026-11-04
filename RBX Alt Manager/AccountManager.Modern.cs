using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using RBX_Alt_Manager.Classes;
using RBX_Alt_Manager.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private Label modernTitle, modernSubtitle, accountsHeading, launchHeading, aliasHeading, notesHeading;
        private float modernScale = 1f;
        private Form advancedWindow;
        private NeonButton advancedButton;
        private NeonButton roflButton;
        private NeonButton modernUpdateButton;
        private RoflForm roflWindow;

        private void ShowRoflMode()
        {
            if (roflWindow != null && !roflWindow.IsDisposed) { roflWindow.Activate(); return; }
            roflWindow = new RoflForm();
            roflWindow.Show(this);
        }
        private PlacePicker placePicker;
        private Bitmap modernScene;
        private bool modernSceneDirty = true, modernInteractiveMove;
        private long modernSceneRenderCount;
        private void InvalidateModernFrame() { modernSceneDirty = true; Invalidate(); }

        private void ShowPlacePicker()
        {
            placePicker?.Dispose();
            var recent = (RecentGames ?? Enumerable.Empty<Game>()).Where(game => game.Details != null)
                .Select(game => new PlaceResult { PlaceId = game.Details.placeId, Name = game.Details.name })
                .Where(game => game.PlaceId > 0).GroupBy(game => game.PlaceId).Select(group => group.First());
            placePicker = new PlacePicker(recent, PlaceID.Width + 10);
            placePicker.PlaceSelected += selected =>
            {
                if (PlaceID.Text != selected.PlaceId.ToString()) JobID.Text = "";
                PlaceID.Text = selected.PlaceId.ToString();
                CurrentPlace.Text = selected.Name;
            };
            placePicker.Show(PlaceID, new Point(0, PlaceID.Height + 8));
        }

        // Visual QA without account files, client processes or network requests.
        private void InitializeInterfacePreview()
        {
            Instance = this;
            IniSettings = new IniFile();
            General = IniSettings.Section("General");
            General.Set("WindowScale", "1");
            ThemeEditor.LoadTheme();
            AccountsList = new System.Collections.Generic.List<Account>();
            SelectedAccounts = new System.Collections.Generic.List<Account>();
            InitializeComponent();
            Load -= AccountManager_Load;
            Shown -= AccountManager_Shown;
            FormClosing -= AccountManager_FormClosing;
            InitializeModernInterface();
            AccountsView.ContextMenuStrip = null;
            PlaceTimer.Stop();
            Controls.ApplyTheme();
            AccountsView.BackColor = ThemeEditor.AccountBackground;
            AccountsView.ForeColor = ThemeEditor.AccountForeground;
            AccountsView.SetObjects(new[] {
                new Account { Username = "MainAccount", Alias = "Основной", Description = "Любимые игры", Group = "Мои аккаунты" },
                new Account { Username = "SecondAccount", Alias = "Второй", Description = "Для игры с друзьями", Group = "Мои аккаунты" }
            });
            ArgumentsB.Visible = false;
            HistoryIcon.Visible = false;
            PasswordPanel.Visible = false;
            ApplyModernPalette();
            Text += " • Предпросмотр";
            Shown += (sender, args) => PlaceTimer.Stop();
        }

        private void InitializeModernInterface()
        {
            SuspendLayout();
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            modernScale = Math.Min(Program.Scale, Math.Min(Screen.PrimaryScreen.WorkingArea.Width / 1160f,
                Screen.PrimaryScreen.WorkingArea.Height / 790f));
            modernScale = Math.Max(0.8f, modernScale);
            Font = new Font("Segoe UI Semibold", 10.5f * modernScale);
            foreach (Control control in Controls)
            {
                control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                if (control != PasswordPanel) control.Font = Font;
            }
            Text = "Spectrum Manager";
            MaximizeBox = true;
            MinimumSize = SizeFromClientSize(new Size(Ui(1060), Ui(600)));
            ClientSize = new Size(Ui(1120), Ui(640));
            modernTitle = ModernLabel("Spectrum Manager", 22, true);
            modernSubtitle = ModernLabel("ROBLOX  /  ЛИЧНЫЙ ЦЕНТР УПРАВЛЕНИЯ    ·    Ctrl+K — команды", 9, false);
            accountsHeading = ModernLabel("АККАУНТЫ", 10, true);
            launchHeading = ModernLabel("ПОДКЛЮЧЕНИЕ К ИГРЕ", 10, true);
            aliasHeading = ModernLabel("Псевдоним аккаунта", 10, false);
            notesHeading = ModernLabel("Заметки об аккаунте", 10, false);
            CurrentPlace.AutoSize = false;
            CurrentPlace.AutoEllipsis = true;
            CurrentPlace.Text = "Выбери игру";
            LabelPlaceID.Text = "ID игры (Place ID)";
            LabelJobID.Text = "Сервер или приватная ссылка";
            LabelUserID.Text = "Ник игрока для подключения";
            JoinServer.Text = "Подключиться";
            ServerList.Text = "Игры и серверы";
            Follow.Text = "К игроку";
            SetAlias.Text = "Сохранить";
            Alias.Text = "";
            DescriptionBox.Text = "";
            SetDescription.Text = "Сохранить заметки";
            BrowserButton.Text = "Действия с аккаунтом";
            EditTheme.Text = "Оформление";
            LaunchNexus.Text = "Контроль аккаунтов";
            ArgumentsB.Text = "Параметры";
            ArgumentsB.Visible = false;
            HistoryIcon.Visible = false;
            ConfigButton.Image = null;
            ConfigButton.Text = "Настройки";
            modernUpdateButton = new NeonButton { Text = "Обновить", Font = Font, Cursor = Cursors.Hand };
            Controls.Add(modernUpdateButton);
            Hint(modernUpdateButton, "Проверить GitHub Releases и установить новую версию без открытия браузера.");
            modernUpdateButton.Click += (sender, e) => { ConfigButton.PerformClick(); SettingsForm.UpdateManagerFromMain(); };
            SaveToAccount.Image = null;
            SaveToAccount.Text = "Сохранить игру";
            DonateButton.Image = null;
            DonateButton.Text = "Об авторе";
            JoinDiscord.Image = null;
            JoinDiscord.Text = "Discord";
            JoinDiscord.Visible = false;
            Add.Text = "Добавить";
            Remove.Text = "Удалить";
            OpenBrowser.Text = "Браузер";
            HideUsernamesCheckbox.Text = "Скрыть ники";
            Username.Text = "Аккаунт";
            AccountAlias.Text = "Псевдоним";
            Description.Text = "Заметки";
            LastUsedColumn.Text = "Последний вход";
            foreach (var field in Controls.OfType<TextBoxBase>())
            {
                field.BorderStyle = BorderStyle.None;
                field.Enter += (sender, e) => Invalidate();
                field.Leave += (sender, e) => Invalidate();
            }
            AccountsView.BorderStyle = BorderStyle.None;
            AccountsView.RowHeight = Ui(38);
            AccountsView.FullRowSelect = true;
            AccountsView.HideSelection = false;
            Username.Width = Ui(170);
            AccountAlias.Width = Ui(140);
            Description.Width = Ui(210);
            Description.FillsFreeSpace = true;

            SaveTooltip.InitialDelay = 400;
            SaveTooltip.ReshowDelay = 100;
            SaveTooltip.AutoPopDelay = 15000;
            SaveTooltip.ShowAlways = true;
            SaveTooltip.ToolTipIcon = ToolTipIcon.None;
            SaveTooltip.ToolTipTitle = "";
            Hint(ArgumentsB, "Параметры запуска Roblox: режим телепортации, старый способ запуска и версия клиента.");
            Hint(ConfigButton, "Настройки менеджера: запуск, браузер, отображение и дополнительные функции.");
            Hint(AccountsView, "Двойной щелчок по аккаунту — открыть его в браузере через расширение. F2 — редактировать поле.");
            AccountsView.ItemActivate += AccountsView_ItemActivate;
            Hint(JoinServer, "Запустить выбранные аккаунты в указанной игре или на сервере.");
            Hint(ServerList, "Поиск игр, список серверов и избранные игры.");
            Hint(PlaceID, "Числовой ID места Roblox. Можно вставить ссылку на игру.");
            Hint(JobID, "Job ID конкретного сервера или ссылка на приватный сервер. Оставь пустым для обычного входа.");
            Hint(LabelJobID, "Job ID — уникальный идентификатор экземпляра сервера Roblox.");
            Hint(LabelPlaceID, "Place ID — идентификатор места в Roblox.");
            Hint(SaveToAccount, "Сохранить ID игры и сервер для выбранного аккаунта. Очисти поля и нажми снова, чтобы удалить привязку.");
            Hint(ShuffleIcon, "Включить или выключить случайный выбор сервера при каждом подключении.");
            Hint(HistoryIcon, "Показать недавно открытые игры.");
            Hint(Follow, "Подключиться к игре указанного игрока, если его настройки приватности разрешают это.");
            Hint(UserID, "Введи ник игрока, к которому хочешь присоединиться.");
            Hint(Alias, "Свой псевдоним для аккаунта. Сохраняется кнопкой справа.");
            Hint(SetAlias, "Сохранить псевдоним выбранного аккаунта.");
            Hint(DescriptionBox, "Заметки для выбранного аккаунта. Для сохранения нажми кнопку под полем.");
            Hint(SetDescription, "Сохранить заметки выбранного аккаунта.");
            Hint(BrowserButton, "Открыть дополнительные действия и инструменты аккаунта.");
            Hint(LaunchNexus, "Открыть контроль запущенных аккаунтов и настройки автоперезапуска.");
            Hint(EditTheme, "Настроить цвета интерфейса.");
            Hint(Add, "Открыть вход Roblox в основном браузере Windows. После входа нажми «Добавить в менеджер» в расширении.");
            Hint(Remove, "Удалить выбранные записи из менеджера после подтверждения.");
            Hint(OpenBrowser, "Открыть выбранный аккаунт Roblox в браузере Windows через расширение Spectrum Manager 0.4.0+. Сменит аккаунт во всех вкладках Roblox этого профиля браузера.");
            Hint(HideUsernamesCheckbox, "Скрыть имена аккаунтов в списке.");
            Hint(DonateButton, "бог этого мира surgee");
            Hint(JoinDiscord, "Открыть Discord сообщества исходного проекта.");
            TranslateModernMenus();
            OpenBrowserStrip.Items.Clear();
            OpenBrowserStrip.Items.Add("Открыть выбранный аккаунт", null, (sender, e) => OpenSelectedAccountBrowser());
            OpenBrowserStrip.Items.Add("Открыть Roblox с текущим входом браузера", null, (sender, e) => SystemBrowser.Open("https://www.roblox.com/"));
            AddAccountsStrip.Items.Remove(bulkUserPassToolStripMenuItem);
            AddAccountsStrip.Items.Remove(customURLJSToolStripMenuItem);
            OpenBrowserStrip.Items.Remove(URLJSToolStripMenuItem);
            OpenBrowserStrip.Items.Remove(joinGroupToolStripMenuItem);
            var registration = new ToolStripMenuItem("Регистрация в браузере");
            registration.Click += (sender, e) => SystemBrowser.Open("https://www.roblox.com/");
            AddAccountsStrip.Items.Add(registration);
            foreach (Button button in Controls.OfType<Button>())
            {
                button.Cursor = Cursors.Hand;
                button.AccessibleName = button.Text;
                button.AccessibleDescription = SaveTooltip.GetToolTip(button);
            }
            Resize += (sender, args) => LayoutModernInterface();
            ResizeBegin += (sender, args) => { if (!modernInteractiveMove) { modernInteractiveMove = true; AnimationCadence.BeginInteraction(); } };
            ResizeEnd += (sender, args) => { if (modernInteractiveMove) { modernInteractiveMove = false; AnimationCadence.EndInteraction(); } InvalidateModernFrame(); };
            Paint += PaintModernInterface;
            FormClosed += (sender, e) => { placePicker?.Dispose(); modernScene?.Dispose(); modernScene = null; if (modernInteractiveMove) { modernInteractiveMove = false; AnimationCadence.EndInteraction(); } };
            InitializeAdvancedWindow();
            InitializeAccountFolders();
            InitializeShortcuts();
            InitializeAccountImport();
            InitializeLaunchInputSaving();
            InitializePortableBackups();
            roflButton = new NeonButton { Text = "ROFL MODE", Font = Font, Cursor = Cursors.Hand, AccessibleName = "Шуточный экран" };
            roflButton.Click += (sender, e) => ShowRoflMode();
            Controls.Add(roflButton);
            Hint(roflButton, "Полноэкранный ROFL MODE с убегающей кнопкой. Esc или Alt+F4 закрывают экран.");
            var glowTimer = new Timer(components) { Interval = 33 };
            glowTimer.Tick += (sender, args) =>
            {
                glowTimer.Interval = AnimationCadence.Interval;
                if (AnimationCadence.Interacting) return;
                if (WindowState == FormWindowState.Minimized || !Visible || !ThemeEditor.AnimationsEnabled) return;
                // Settings can sit beside the manager: both visible backgrounds must keep moving.
                if (placePicker?.Visible == true || roflWindow?.Visible == true) return;
                InvalidateModernFrame();
                foreach (var button in Controls.OfType<NeonButton>())
                    if (button.Visible) button.Invalidate();
                if (advancedWindow != null && advancedWindow.Visible) advancedWindow.Invalidate(true);
            };
            glowTimer.Start();
            ApplyModernPalette();
            LayoutModernInterface();
            ResumeLayout(false);
        }

        private int Ui(float value) => (int)Math.Round(value * modernScale);

        private Label ModernLabel(string text, float size, bool bold)
        {
            var label = new Label { Text = text, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", size * modernScale, bold ? FontStyle.Bold : FontStyle.Regular) };
            Controls.Add(label);
            return label;
        }

        private void Hint(Control control, string text)
        {
            SaveTooltip.SetToolTip(control, text);
            control.AccessibleDescription = text;
        }

        private void LayoutModernInterface()
        {
            if (modernTitle == null) return;
            int w = (int)(ClientSize.Width / modernScale), h = (int)(ClientSize.Height / modernScale);
            int right = w - 480, leftWidth = right - 64;
            void Put(Control c, int x, int y, int width, int height)
            {
                c.AutoSize = false;
                c.SetBounds(Ui(x), Ui(y), Ui(width), Ui(height));
            }
            Put(modernTitle, 24, 18, 490, 38);
            Put(modernSubtitle, 26, 59, 500, 26);
            Put(ConfigButton, w - 152, 29, 128, 36);
            Put(modernUpdateButton, w - 290, 29, 128, 36);
            Put(accountsHeading, 40, 112, leftWidth - 220, 26);
            Put(folderCreate, right - 215, 108, 142, 34);
            Put(folderMenu, right - 66, 108, 42, 34);
            Put(folderSelector, 40, 154, leftWidth - 32, 32);
            Put(accountSearch, 45, 203, leftWidth - 42, 26);
            Put(AccountsView, 40, 244, leftWidth - 32, h - 394);
            Username.Width = Ui((leftWidth - 36) * .34f);
            AccountAlias.Width = Ui((leftWidth - 36) * .28f);
            Description.Width = Math.Max(Ui(100), AccountsView.Width - Username.Width - AccountAlias.Width - SystemInformation.VerticalScrollBarWidth - Ui(4));
            Put(Add, 40, h - 126, 128, 38);
            Put(importAccountsButton, 176, h - 126, 104, 38);
            Put(Remove, 288, h - 126, 100, 38);
            Put(OpenBrowser, 400, h - 126, right - 456, 38);
            Put(HideUsernamesCheckbox, 40, h - 76, 180, 30);
            Put(JoinDiscord, 244, h - 76, 88, 30);
            Put(DonateButton, 244, h - 76, 96, 30);
            Put(roflButton, 350, h - 76, 136, 30);
            Put(launchHeading, right, 112, 275, 26);
            Put(profilesButton, right + 290, 108, 150, 34);
            Put(CurrentPlace, right, 145, 390, 27);
            Put(HistoryIcon, w - 62, 148, 22, 22);
            Put(LabelPlaceID, right, 198, 400, 24);
            Put(PlaceID, right, 232, 438, 30);
            Put(LabelJobID, right, 287, 430, 24);
            Put(JobID, right, 321, 438, 30);
            Put(JoinServer, right - 4, 383, 450, 48);
            Put(ServerList, right - 4, 445, 220, 42);
            Put(advancedButton, right + 224, 445, 222, 42);
            Put(DownloadProgressBar, 24, h - 15, leftWidth, 8);
            Put(DLChromiumLabel, 40, h - 173, leftWidth - 32, 24);
            PasswordPanel.Bounds = ClientRectangle;
            InvalidateModernFrame();
        }

        private void ApplyModernPalette()
        {
            if (modernTitle == null) return;
            BackColor = ThemeEditor.FormsBackground;
            ForeColor = ThemeEditor.FormsForeground;
            foreach (Label label in Controls.OfType<Label>())
            {
                label.BackColor = Color.Transparent;
                label.ForeColor = ForeColor;
            }
            modernSubtitle.ForeColor = ThemeEditor.LabelForeground;
            if (folderSelector != null) { folderSelector.BackColor = ThemeEditor.TextBoxesBackground; folderSelector.ForeColor = ThemeEditor.TextBoxesForeground; }
            AccountsView.HeaderUsesThemes = false;
            var headers = new BrightIdeasSoftware.HeaderFormatStyle();
            foreach (var style in new[] { headers.Normal, headers.Hot, headers.Pressed })
            { style.BackColor = ThemeEditor.TextBoxesBackground; style.ForeColor = ThemeEditor.TextBoxesForeground; style.Font = Font; }
            AccountsView.HeaderFormatStyle = headers;
            AccountsView.HeaderMinimumHeight = Ui(30);
            AccountsView.EmptyListMsgFont = Font;
            foreach (Button button in Controls.OfType<Button>())
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = ThemeEditor.ButtonsBackground;
                button.ForeColor = ThemeEditor.ButtonsForeground;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = ThemeEditor.ButtonsBorder;
                button.FlatAppearance.MouseOverBackColor = ThemeEditor.ButtonsBackground.DarkenOrBrighten(0.12f);
                button.FlatAppearance.MouseDownBackColor = ThemeEditor.ButtonsBackground.DarkenOrBrighten(0.22f);
            }
            JoinServer.BackColor = Color.FromArgb(97, 91, 235);
            JoinServer.ForeColor = Color.White;
            JoinServer.FlatAppearance.BorderSize = 0;
            JoinServer.FlatAppearance.MouseOverBackColor = Color.FromArgb(116, 110, 248);
            JoinServer.FlatAppearance.MouseDownBackColor = Color.FromArgb(78, 73, 205);
            if (JoinServer is NeonButton launchButton) launchButton.Accent = true;
            if (Add is NeonButton addButton) addButton.Accent = true;
            if (advancedWindow != null)
            {
                advancedWindow.BackColor = BackColor;
                advancedWindow.ForeColor = ForeColor;
                foreach (TabPage page in ((TabControl)advancedWindow.Controls[0]).TabPages)
                {
                    page.BackColor = BackColor;
                    page.ForeColor = ForeColor;
                    page.Controls.ApplyTheme();
                }
            }
            AccountsView.UnfocusedSelectedBackColor = Color.FromArgb(65, 61, 116);
            AccountsView.UnfocusedSelectedForeColor = Color.White;
            InvalidateModernFrame();
        }

        private void PaintModernInterface(object sender, PaintEventArgs e)
        {
            if (modernTitle == null || ClientSize.Width < 1 || ClientSize.Height < 1) return;
            if (modernScene == null || (!AnimationCadence.Interacting && (modernSceneDirty || modernScene.Size != ClientSize)))
            {
                if (modernScene == null || modernScene.Size != ClientSize)
                { modernScene?.Dispose(); modernScene = new Bitmap(ClientSize.Width, ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); }
                using (var canvas = Graphics.FromImage(modernScene)) RenderModernScene(canvas);
                modernSceneDirty = false; modernSceneRenderCount++;
            }
            var state = e.Graphics.Save();
            e.Graphics.CompositingMode = CompositingMode.SourceCopy;
            if (modernScene.Size == ClientSize) e.Graphics.DrawImageUnscaled(modernScene, 0, 0);
            else e.Graphics.DrawImage(modernScene, ClientRectangle);
            e.Graphics.Restore(state);
            PaintModernFields(e.Graphics);
        }
        private void RenderModernScene(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int right = (int)(ClientSize.Width / modernScale) - 480;
            using (var bg = new LinearGradientBrush(ClientRectangle, ThemeEditor.FormsBackground, NeonButton.Mix(ThemeEditor.FormsBackground, ThemeEditor.AccentSecondary, 0.05f), 35f))
                g.FillRectangle(bg, ClientRectangle);
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(Ui(120), -Ui(230), Ui(800), Ui(450));
                using (var aura = new PathGradientBrush(glow))
                {
                    aura.CenterColor = Color.FromArgb(60, NeonButton.Mix(ThemeEditor.AccentPrimary, ThemeEditor.AccentSecondary, NeonButton.Phase));
                    aura.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(aura, glow);
                }
            }
            var cards = new[] {
                new RectangleF(Ui(24), Ui(104), Ui(right - 48), ClientSize.Height - Ui(128)),
                new RectangleF(Ui(right - 16), Ui(104), Ui(476), ClientSize.Height - Ui(128)) };
            foreach (var card in cards)
                using (var path = NeonButton.Round(card, Ui(20)))
                using (var fill = new LinearGradientBrush(card, ThemeEditor.AccountBackground, NeonButton.Mix(ThemeEditor.AccountBackground, ThemeEditor.FormsBackground, 0.55f), 65f))
                using (var stroke = new LinearGradientBrush(card, Color.FromArgb(100, ThemeEditor.AccentPrimary), Color.FromArgb(80, ThemeEditor.AccentSecondary), 45f))
                using (var pen = new Pen(stroke, 1.2f))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }
            VisualBackground.Draw(g, ClientSize);
            using (var neon = new LinearGradientBrush(new Rectangle(24, Ui(90), Width - 48, 2), ThemeEditor.AccentPrimary, ThemeEditor.AccentSecondary, 0f))
            using (var pen = new Pen(neon, 2f))
                g.DrawLine(pen, Ui(24), Ui(90), ClientSize.Width - Ui(24), Ui(90));
        }
        private void PaintModernFields(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var field in Controls.OfType<TextBoxBase>())
            {
                var rect = new RectangleF(field.Left - Ui(5), field.Top - Ui(4), field.Width + Ui(10), field.Height + Ui(8));
                using (var path = NeonButton.Round(rect, Ui(8)))
                using (var brush = new SolidBrush(ThemeEditor.TextBoxesBackground))
                using (var pen = new Pen(field.Focused ? Color.FromArgb(68, 207, 239) : Color.FromArgb(61, 68, 103), field.Focused ? 1.6f : 1f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }
            }
        }

        private void InitializeAdvancedWindow()
        {
            advancedWindow = new Form { Text = "Дополнительные действия", ShowIcon = false,
                StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(Ui(520), Ui(480)),
                Font = Font, MinimizeBox = false, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog };
            var tabs = new NBTabControl { Dock = DockStyle.Fill, Padding = new Point(Ui(14), Ui(10)) };
            advancedWindow.Controls.Add(tabs);
            TabPage Page(string title)
            {
                var page = new TabPage(title) { BackColor = ThemeEditor.FormsBackground, Padding = new Padding(Ui(18)) };
                tabs.TabPages.Add(page);
                return page;
            }
            void Put(TabPage page, Control control, int x, int y, int width, int height)
            {
                page.Controls.Add(control);
                control.AutoSize = false;
                control.SetBounds(Ui(x), Ui(y), Ui(width), Ui(height));
                if (control is TextBoxBase field) field.BorderStyle = BorderStyle.FixedSingle;
            }
            var join = Page("Подключение");
            Put(join, LabelUserID, 22, 26, 450, 26);
            Put(join, UserID, 22, 68, 310, 30);
            Put(join, Follow, 346, 62, 130, 42);
            Put(join, SaveToAccount, 18, 136, 272, 42);
            Put(join, ShuffleIcon, 322, 146, 24, 24);
            var shuffleLabel = new Label { Text = "Случайный сервер", ForeColor = ForeColor, Font = Font };
            Put(join, shuffleLabel, 354, 142, 134, 50);
            var account = Page("Аккаунт");
            Put(account, aliasHeading, 22, 26, 450, 26);
            Put(account, Alias, 22, 68, 310, 30);
            Put(account, SetAlias, 346, 62, 130, 42);
            Put(account, notesHeading, 22, 124, 450, 26);
            Put(account, DescriptionBox, 22, 166, 450, 130);
            Put(account, SetDescription, 18, 318, 218, 42);
            Put(account, BrowserButton, 250, 318, 230, 42);
            var tools = Page("Инструменты");
            Put(tools, EditTheme, 18, 26, 462, 44);
            Put(tools, LaunchNexus, 18, 88, 462, 44);
            advancedWindow.FormClosing += (sender, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; advancedWindow.Hide(); }
            };
            FormClosed += (sender, e) => advancedWindow.Dispose();
            advancedButton = new NeonButton { Text = "Дополнительно", Font = Font, Cursor = Cursors.Hand,
                AccessibleName = "Дополнительно" };
            Controls.Add(advancedButton);
            Hint(advancedButton, "Подключение по нику, сохранение игры, псевдоним, заметки и инструменты аккаунтов.");
            advancedButton.Click += (sender, e) =>
            {
                if (advancedWindow.Visible) advancedWindow.Activate();
                else advancedWindow.Show(this);
            };
        }

        private void TranslateModernMenus()
        {
            manualToolStripMenuItem.Text = "Войти через браузер";
            bulkUserPassToolStripMenuItem.Text = "Импорт логин:пароль";
            byCookieToolStripMenuItem.Text = "Импорт cookie";
            customURLJSToolStripMenuItem.Text = "Свой вход (URL + JS)";
            addAccountsToolStripMenuItem.Text = "Добавить аккаунт";
            removeAccountToolStripMenuItem.Text = "Удалить аккаунт";
            copyToolStripMenuItem.Text = "Копировать";
            copyUsernameToolStripMenuItem.Text = "Ник";
            copyPasswordToolStripMenuItem.Text = "Пароль";
            copyUserPassComboToolStripMenuItem.Text = "Логин:пароль";
            copyProfileToolStripMenuItem.Text = "Ссылку на профиль";
            copyUserIdToolStripMenuItem.Text = "ID аккаунта";
            sortAlphabeticallyToolStripMenuItem.Text = "Сортировать по алфавиту";
            quickLogInToolStripMenuItem.Text = "Быстрый вход";
            moveGroupUpToolStripMenuItem.Text = "Группы";
            toggleToolStripMenuItem.Text = "Свернуть / развернуть";
            moveToToolStripMenuItem.Text = "Переместить в группу";
            copyGroupToolStripMenuItem.Text = "Копировать группу";
            infoToolStripMenuItem.Text = "Помощь";
            groupsToolStripMenuItem.Text = "О группах";
            infoToolStripMenuItem1.Text = "Информация";
            viewFieldsToolStripMenuItem.Text = "Поля аккаунта";
            ShowDetailsToolStripMenuItem.Text = "Подробности";
            getAuthenticationTicketToolStripMenuItem.Text = "Получить билет авторизации";
            copySecurityTokenToolStripMenuItem.Text = "Копировать cookie";
            copyRbxplayerLinkToolStripMenuItem.Text = "Копировать ссылку запуска";
            copyAppLinkToolStripMenuItem.Text = "Копировать ссылку приложения";
            customURLToolStripMenuItem.Text = "Открыть адрес";
            URLJSToolStripMenuItem.Text = "Адрес и JavaScript";
            joinGroupToolStripMenuItem.Text = "Вступить в группу";
        }
    }
}
