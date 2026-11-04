using System;
using System.Drawing;
using System.Windows.Forms;
using RBX_Alt_Manager.Classes;

namespace RBX_Alt_Manager.Forms
{
    public partial class SettingsForm
    {
        private FlowLayoutPanel SettingsPage(string title)
        {
            var page = new TabPage(title) { BackColor = ThemeEditor.FormsBackground };
            var flow = new ThemedSettingsPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(22) };
            page.Controls.Add(flow);
            SettingsTC.TabPages.Add(page);
            return flow;
        }

        private void Section(FlowLayoutPanel flow, string title)
        {
            flow.Controls.Add(new Label { Text = title, Width = 650, Height = 30,
                Font = new Font(Font, FontStyle.Bold), ForeColor = ThemeEditor.AccentPrimary,
                Margin = new Padding(0, 12, 0, 8) });
        }

        private void Setting(FlowLayoutPanel flow, Control control, string title, string explanation)
        {
            control.Text = title;
            control.Font = Font;
            control.AutoSize = false;
            control.Size = new Size(control is Button ? 320 : 650, control is Button ? 40 : 28);
            control.Margin = new Padding(0, 8, 0, 0);
            flow.Controls.Add(control);
            Helper.SetToolTip(control, explanation);
            Description(flow, explanation);
        }

        private void Description(FlowLayoutPanel flow, string text)
        {
            flow.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(650, 0),
                Font = new Font("Segoe UI", 9), ForeColor = ThemeEditor.LabelForeground,
                Margin = new Padding(3, 0, 0, 10) });
        }

        private void SettingRow(FlowLayoutPanel flow, Label caption, Control input, string title, string hint)
        {
            var row = new FlowLayoutPanel { Width = 650, Height = 38, WrapContents = false, Margin = new Padding(0, 10, 0, 0) };
            caption.Text = title;
            caption.RightToLeft = RightToLeft.No;
            caption.Font = Font;
            input.Font = Font;
            caption.AutoSize = false;
            caption.Size = new Size(410, 30);
            caption.TextAlign = ContentAlignment.MiddleLeft;
            input.Width = 190;
            row.Controls.Add(caption);
            row.Controls.Add(input);
            flow.Controls.Add(row);
            Helper.SetToolTip(input, hint);
            Helper.SetToolTip(caption, hint);
            Description(flow, hint);
        }

        private void InitializeClearSettings()
        {
            Text = "Настройки • Surgee Manager";
            Font = new Font("Segoe UI Semibold", 10.5f);
            ClientSize = new Size(760, 600);
            MinimumSize = new Size(776, 639);
            SettingsTC.Dock = DockStyle.None;
            SettingsTC.SetBounds(16, 92, 728, 490);
            SettingsTC.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            SettingsTC.Padding = new Point(14, 10);
            SettingsTC.Font = new Font("Segoe UI Semibold", 11f);
            SettingsTC.TabPages.Clear();
            var title = new Label { Text = "Настрой под себя", Font = new Font("Segoe UI", 20, FontStyle.Bold),
                AutoSize = true, Location = new Point(24, 16) };
            var subtitle = new Label { Text = "Изменения сохраняются автоматически. Наведи на настройку, чтобы узнать больше.",
                AutoSize = true, Location = new Point(26, 61), Font = new Font("Segoe UI", 9) };
            Controls.Add(title);
            Controls.Add(subtitle);
            Helper.InitialDelay = 400;
            Helper.AutoPopDelay = 15000;
            DoubleBuffered = true;
            var animation = new Timer(components) { Interval = 33 };
            animation.Tick += (sender, e) =>
            {
                if (!Visible || WindowState == FormWindowState.Minimized || !ThemeEditor.AnimationsEnabled) return;
                SettingsTC.Invalidate();
                if (SettingsTC.SelectedTab != null)
                    foreach (Control child in SettingsTC.SelectedTab.Controls) child.Invalidate(true);
            };
            animation.Start();

            var main = SettingsPage("Основные");
            Section(main, "ПРИЛОЖЕНИЕ");
            Setting(main, StartOnPCStartup, "Открывать менеджер при запуске Windows", "Добавляет менеджер в автозагрузку текущего пользователя.");
            Setting(main, PresenceCB, "Показывать статус аккаунтов", "Отображает, находится ли аккаунт на сайте или в игре.");
            SettingRow(main, PresenceUpdateLabel, PresenceUpdateRateNum, "Обновлять статус каждые, мин.", "Частота запроса состояния аккаунтов.");
            SettingRow(main, MRGLabel, MaxRecentGamesNumber, "Количество недавних игр", "Сколько игр сохранять в истории для быстрого открытия.");

            var accounts = SettingsPage("Аккаунты");
            Section(accounts, "СЕССИИ И ДАННЫЕ");
            Setting(accounts, SavePasswordCB, "Сохранять пароли при ручном добавлении", "Расширение добавляет сессию и ник; пароль Roblox оно не читает.");
            Setting(accounts, DisableAgingAlertCB, "Скрывать отметки о долгом простое", "Отключает жёлтые и красные индикаторы у аккаунтов, которыми давно не пользовались.");
            Setting(accounts, AutoCookieRefreshCB, "Автоматически обновлять старые сессии", "Старый механизм обновления может завершать другие сеансы Roblox этого аккаунта. По умолчанию выключен.");
            Setting(accounts, EncryptionSelectionButton, "Защита файла аккаунтов…", "Выбрать защиту Windows или мастер-пароль для сохранённой базы.");

            var launch = SettingsPage("Запуск");
            Section(launch, "ПОДКЛЮЧЕНИЕ К ИГРЕ");
            SettingRow(launch, DelayLabel, LaunchDelayNumber, "Задержка между запусками, сек.", "Пауза перед запуском следующего выбранного аккаунта.");
            Setting(launch, AsyncJoinCB, "Ждать завершения предыдущего запуска", "Перед запуском следующего аккаунта дождаться сигнала от предыдущего клиента.");
            Setting(launch, MultiRobloxCB, "Разрешить несколько клиентов Roblox", "Использует механизм нескольких экземпляров из исходного проекта. Для изменения закрой Roblox.");
            Setting(launch, HideMRobloxCB, "Скрывать предупреждение о нескольких клиентах", "Отключает уведомление, если Roblox уже открыт при запуске менеджера.");
            Setting(launch, ShuffleLowestServerCB, "Выбирать наименее заполненный сервер", "Применяется при включённом случайном выборе сервера в дополнительных действиях.");
            Section(launch, "ПРОИЗВОДИТЕЛЬНОСТЬ");
            Setting(launch, UnlockFPSCB, "Задавать свой лимит кадров", "Передаёт ограничение FPS через настройки клиента Roblox.");
            SettingRow(launch, FPSCapLabel, MaxFPSValue, "Максимум кадров в секунду", "Используется, когда включён свой лимит кадров.");
            Setting(launch, OverrideWithCustomCB, "Использовать свой файл настроек клиента", "Выбрать ClientAppSettings.json вместо настроек FPS выше.");
            Section(launch, "ДОПОЛНИТЕЛЬНЫЕ ПАРАМЕТРЫ ЗАПУСКА");
            var launchOptions = new ArgumentsForm { TopLevel = false, FormBorderStyle = FormBorderStyle.None,
                MaximumSize = Size.Empty, MinimumSize = Size.Empty, Size = new Size(640, 180) };
            launch.Controls.Add(launchOptions);
            launchOptions.Show();
            launchOptions.ApplyTheme();

            var advanced = SettingsPage("Дополнительно");
            Section(advanced, "ФУНКЦИИ ИСХОДНОГО ПРОЕКТА");
            Setting(advanced, EnableDMCB, "Показывать дополнительные действия аккаунтов", "Открывает поля аккаунта и действия с сессиями в контекстном меню.");
            Setting(advanced, EnableWSCB, "Включить старый локальный API", "Этот API не нужен расширению Surgee. Для изменения нужен перезапуск.");
            SettingRow(advanced, PortLabel, PortNumber, "Порт старого API", "Порт расширения Surgee отдельный: 7964.");
            Setting(advanced, ERRPCB, "Требовать пароль для каждого запроса API", "Настройка только для старого API исходного проекта.");
            Setting(advanced, AllowGCCB, "Разрешить API получать cookie", "Относится только к старому API. Расширение Surgee не использует эту функцию.");
            Setting(advanced, AllowGACB, "Разрешить API читать список аккаунтов", "Доступ к списку через старый API.");
            Setting(advanced, AllowLACB, "Разрешить API запускать аккаунты", "Запуск игр командами старого API.");
            Setting(advanced, AllowAECB, "Разрешить API изменять данные аккаунтов", "Редактирование псевдонимов и заметок через старый API.");
            Setting(advanced, AllowExternalConnectionsCB, "Разрешить подключения к старому API по сети", "По умолчанию выключено. Расширение Surgee работает локально без этой настройки.");
            SettingRow(advanced, WSPWLabel, PasswordTextBox, "Пароль старого API", "Отдельный пароль API; не пароль Roblox и не код подключения расширения.");
            SettingRow(advanced, RegionFormatLabel, RegionFormatTB, "Формат региона сервера (старый механизм)", "Шаблон названия региона из исходного проекта.");
            AutoUpdateCB.Visible = false;
            ForceUpdateButton.Visible = false;
            DisableImagesCB.Visible = false;
            RSLabel.Visible = false;
        }
    }

    internal class ThemedSettingsPanel : FlowLayoutPanel
    {
        private Bitmap backdrop;
        private string backdropKey;
        private long backdropFrame = -1;
        private double scrollingUntil;
        public ThemedSettingsPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width < 1 || Height < 1) return;
            string key = Width + ":" + Height + ":" + ThemeEditor.VisualProfile + ":" + ThemeEditor.FormsBackground.ToArgb() + ":" + ThemeEditor.AccountBackground.ToArgb() + ":" + ThemeEditor.AccentPrimary.ToArgb() + ":" + ThemeEditor.AccentSecondary.ToArgb() + ":" + ThemeEditor.AnimationsEnabled;
            long frame = ThemeEditor.AnimationsEnabled ? (long)(VisualBackground.Time * 30) : 0;
            if (VisualBackground.Time < scrollingUntil && backdropFrame >= 0) frame = backdropFrame;
            if (backdrop == null || backdropKey != key || backdropFrame != frame)
            {
                if (backdrop == null || backdrop.Size != ClientSize) { backdrop?.Dispose(); backdrop = new Bitmap(Width, Height); }
                using (var canvas = Graphics.FromImage(backdrop))
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle,
                    ThemeEditor.FormsBackground, NeonButton.Mix(ThemeEditor.AccountBackground, ThemeEditor.FormsBackground, 0.4f), 45f))
                {
                    canvas.FillRectangle(brush, ClientRectangle);
                    VisualBackground.Draw(canvas, ClientSize);
                }
                backdropKey = key;
                backdropFrame = frame;
            }
            e.Graphics.DrawImageUnscaled(backdrop, 0, 0);
        }
        protected override void OnScroll(ScrollEventArgs e) { scrollingUntil = VisualBackground.Time + .16; base.OnScroll(e); Invalidate(true); Update(); }
        protected override void Dispose(bool disposing) { if (disposing) backdrop?.Dispose(); base.Dispose(disposing); }
    }
}
