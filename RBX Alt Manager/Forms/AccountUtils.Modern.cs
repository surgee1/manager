using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountUtils
    {
        private Account editingAccount;
        private Label accountCaption;
        private NeonButton privacyButton;
        private PrefixMenu privacyMenu;
        private static readonly string[] PrivacyChoices = { "Все игроки", "Друзья, подписки и подписчики", "Друзья и подписки", "Только друзья", "Никто" };
        private static int U(int value) => (int)Math.Round(value * Program.Scale);
        private static IEnumerable<Control> UtilityControls(Control root)
        {
            foreach (Control child in root.Controls)
            { yield return child; foreach (var descendant in UtilityControls(child)) yield return descendant; }
        }
        private void ClearSensitiveFields() { textBox1.Clear(); textBox2.Clear(); textBox5.Clear(); }
        public void ShowForAccount(Account account, IWin32Window owner)
        {
            if (editingAccount != account)
            {
                ClearSensitiveFields(); textBox3.Clear(); DisplayName.Clear(); Username.Clear();
                WhoFollow.SelectedIndex = -1;
                editingAccount = account;
                privacyButton.Text = "Кто может присоединяться";
            }
            accountCaption.Text = "Аккаунт: " + account.Username;
            ApplyTheme();
            if (!Visible) Show(owner);
            WindowState = FormWindowState.Normal; Activate();
        }
        private void InitializeModernUtilities()
        {
            SuspendLayout(); Controls.Clear();
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI Semibold", 9.5f * Program.Scale);
            Text = "Действия с аккаунтом"; ShowIcon = true; MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent;
            MinimumSize = Size.Empty;
            ClientSize = new Size(U(760), Math.Min(U(650), Math.Max(U(460), Screen.PrimaryScreen.WorkingArea.Height - U(90))));
            var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMinSize = new Size(U(750), U(644)) };
            Controls.Add(body);
            Label Caption(string text, int x, int y, int width, int height, float size, bool bold)
            {
                var label = new Label { Text = text, Bounds = new Rectangle(U(x), U(y), U(width), U(height)),
                    AutoSize = false, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Segoe UI", size * Program.Scale, bold ? FontStyle.Bold : FontStyle.Regular) };
                body.Controls.Add(label); return label;
            }
            Caption("Действия с аккаунтом", 24, 17, 700, 34, 19, true);
            accountCaption = Caption("Выбери аккаунт в менеджере", 26, 56, 700, 28, 11, false);
            UtilityCard Card(string title, int x, int y, int height)
            {
                var card = new UtilityCard { Bounds = new Rectangle(U(x), U(y), U(348), U(height)) };
                var heading = new Label { Text = title, Bounds = new Rectangle(U(18), U(13), U(312), U(28)),
                    BackColor = Color.Transparent, Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
                card.Controls.Add(heading); body.Controls.Add(card); return card;
            }
            void LabelAt(Control parent, Label label, string text, int y)
            {
                label.AutoSize = false; label.Text = text; label.Font = Font;
                label.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                label.Bounds = new Rectangle(U(18), U(y), U(312), U(24));
                parent.Controls.Add(label);
            }
            void Input(Control parent, TextBox field, int x, int y, int width, bool secret = false)
            {
                field.Font = Font; field.BorderStyle = BorderStyle.None;
                field.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                field.UseSystemPasswordChar = secret;
                var host = new UtilityInput(field) { Bounds = new Rectangle(U(x), U(y), U(width), U(34)) };
                parent.Controls.Add(host);
            }
            void ButtonAt(Control parent, Button button, string text, int x, int y, int width)
            {
                button.Text = text; button.Font = Font; button.Cursor = Cursors.Hand;
                button.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                button.Bounds = new Rectangle(U(x), U(y), U(width), U(40));
                parent.Controls.Add(button);
            }
            var security = Card("Безопасность", 24, 102, 300);
            LabelAt(security, label1, "Текущий пароль", 48); Input(security, textBox1, 18, 76, 312, true);
            LabelAt(security, label2, "Новый пароль", 119); Input(security, textBox2, 18, 147, 312, true);
            ButtonAt(security, button1, "Изменить пароль", 16, 195, 316);
            ButtonAt(security, button2, "Завершить остальные сеансы", 16, 245, 316);
            var profile = Card("Профиль", 388, 102, 300);
            var emailLabel = new Label(); LabelAt(profile, emailLabel, "Электронная почта", 48);
            Input(profile, textBox3, 18, 76, 312); ButtonAt(profile, button3, "Изменить почту", 16, 118, 316);
            var nameLabel = new Label(); LabelAt(profile, nameLabel, "Отображаемое имя", 176);
            Input(profile, DisplayName, 18, 204, 312); ButtonAt(profile, SetDisplayName, "Сохранить имя", 16, 246, 316);
            var social = Card("Игроки", 24, 418, 184);
            LabelAt(social, label4, "Ник игрока", 44); Input(social, Username, 18, 72, 312);
            ButtonAt(social, AddFriend, "В друзья", 16, 124, 154); ButtonAt(social, Block, "Блок / разблок", 178, 124, 154);
            var privacy = Card("Приватность", 388, 418, 184);
            var pinLabel = new Label(); LabelAt(privacy, pinLabel, "PIN аккаунта", 44);
            textBox5.Clear(); Input(privacy, textBox5, 18, 72, 106, true);
            ButtonAt(privacy, button7, "Разблокировать", 134, 69, 198);
            privacyButton = new NeonButton(); ButtonAt(privacy, privacyButton, "Кто может присоединяться", 16, 124, 316);
            WhoFollow.Visible = false; body.Controls.Add(WhoFollow);
            label3.Visible = false; body.Controls.Add(label3);
            privacyButton.Click += (sender, e) => ShowPrivacyMenu();
            Caption("Для изменения почты используется текущий пароль слева.", 26, 611, 710, 26, 9, false);
            EmailTip.ToolTipTitle = "";
            UiText.SetToolTip(EmailTip, textBox3, "Для изменения почты используется текущий пароль слева.");
            KeyPreview = true; KeyDown += (sender, e) => { if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; } };
            FormClosed += (sender, e) => privacyMenu?.Dispose();
            ApplyTheme(); RoundedWindow.Attach(this); UiText.Attach(this);
            ResumeLayout(false);
        }
        private void ShowPrivacyMenu()
        {
            privacyMenu?.Dispose();
            privacyMenu = new PrefixMenu(Font, U(316)) { Height = U(182) };
            var choices = PrivacyChoices;
            for (int index = 0; index < choices.Length; index++)
            {
                int selected = index;
                var item = new ToolStripMenuItem(choices[index]) { AutoSize = false, Size = new Size(U(304), U(34)), Checked = WhoFollow.SelectedIndex == index };
                item.Click += (sender, e) => { privacyMenu.Close(); WhoFollow.SelectedIndex = selected; };
                privacyMenu.Items.Add(item);
            }
            UiText.Attach(privacyMenu);
            privacyMenu.Show(privacyButton, new Point(0, privacyButton.Height));
        }
        private sealed class UtilityCard : Panel
        {
            internal UtilityCard() { DoubleBuffered = true; }
            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Parent?.BackColor ?? Forms.ThemeEditor.FormsBackground);
            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                if (Width < 8 || Height < 8) return;
                using (var path = NeonButton.Round(new RectangleF(0, 0, Width, Height), U(14)))
                { var previous = Region; Region = new Region(path); previous?.Dispose(); }
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = NeonButton.Round(new RectangleF(1, 1, Width - 3, Height - 3), U(14)))
                using (var brush = new SolidBrush(NeonButton.Mix(Forms.ThemeEditor.TextBoxesBackground, Forms.ThemeEditor.FormsBackground, .35f)))
                using (var pen = new Pen(Color.FromArgb(110, Forms.ThemeEditor.ButtonsBorder)))
                { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
                base.OnPaint(e);
            }
        }
        private sealed class UtilityInput : Panel
        {
            private readonly TextBox field;
            internal UtilityInput(TextBox field)
            {
                this.field = field; DoubleBuffered = true; Controls.Add(field);
                field.Enter += (sender, e) => Invalidate(); field.Leave += (sender, e) => Invalidate();
            }
            protected override void OnResize(EventArgs e)
            { base.OnResize(e); if (field != null) field.SetBounds(U(10), (Height - field.PreferredHeight) / 2, Math.Max(1, Width - U(20)), field.PreferredHeight); }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = NeonButton.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), U(8)))
                using (var brush = new SolidBrush(Forms.ThemeEditor.TextBoxesBackground))
                using (var pen = new Pen(field.Focused ? Forms.ThemeEditor.AccentPrimary : Forms.ThemeEditor.TextBoxesBorder))
                { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
                base.OnPaint(e);
            }
        }
    }
}
