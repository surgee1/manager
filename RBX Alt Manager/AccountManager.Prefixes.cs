using BrightIdeasSoftware;
using RBX_Alt_Manager.Classes;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private OLVColumn prefixColumn;
        private PrefixMenu prefixMenu;
        private DateTime prefixEditingUntil;
        private void InitializeAccountPrefixes()
        {
            prefixColumn = new OLVColumn("Prefix", "") { Width = Ui(116), MinimumWidth = Ui(100), IsEditable = false, IsVisible = true,
                ToolTipText = UiText.ShowHints ? UiText.Translate("Legit, Cheat или обычный запуск Windows без префикса.") : "", Renderer = new PrefixRenderer() };
            prefixColumn.AspectGetter = model => { string prefix = RobloxClients.Prefix((Account)model); return (prefix == "" ? UiText.Translate("Без префикса") : prefix) + " ▾"; };
            AccountsView.AllColumns.Insert(2, prefixColumn);
            AccountsView.RebuildColumns();
            prefixColumn.DisplayIndex = 2;
            AccountsView.OwnerDraw = true;
            AccountsView.CellClick += (sender, e) =>
            {
                if (e.Column != prefixColumn || e.Item == null || e.ClickCount != 1) return;
                prefixEditingUntil = DateTime.UtcNow.AddSeconds(1);
                ShowPrefixMenu((Account)e.Item.RowObject, e.Item.SubItems[e.ColumnIndex].Bounds);
                e.Handled = true;
            };
            AccountsView.KeyDown += (sender, e) =>
            {
                if (e.KeyCode != Keys.F4 && !(e.Alt && e.KeyCode == Keys.Down)) return;
                if (!(AccountsView.FocusedObject is Account account)) return;
                var item = AccountsView.ModelToItem(account);
                if (item == null) return;
                int index = AccountsView.Columns.IndexOf(prefixColumn);
                ShowPrefixMenu(account, item.SubItems[index].Bounds);
                e.Handled = e.SuppressKeyPress = true;
            };
            FormClosed += (sender, e) => prefixMenu?.Dispose();
        }
        private void ShowPrefixMenu(Account account, Rectangle cell)
        {
            prefixEditingUntil = DateTime.UtcNow.AddSeconds(1);
            prefixMenu?.Dispose();
            prefixMenu = new PrefixMenu(AccountsView.Font, Math.Max(Ui(178), cell.Width));
            string selected = RobloxClients.Prefix(account);
            foreach (string value in new[] { "", "Legit", "Cheat" })
            {
                var choice = new ToolStripMenuItem(value == "" ? UiText.Translate("Без префикса") : value)
                { Checked = selected == value, AutoSize = false, Size = new Size(prefixMenu.Width - Ui(12), Ui(34)),
                    ForeColor = Forms.ThemeEditor.TextBoxesForeground };
                choice.Click += (sender, e) =>
                {
                    prefixMenu.Close();
                    if (!AccountsList.Contains(account) || RobloxClients.Prefix(account) == value) return;
                    try { SetAccountPrefix(account, value); }
                    catch (Exception error) { UiMessageBox.Show(this, error.Message, "Сохранение префикса", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    AccountsView.RefreshObject(account);
                };
                prefixMenu.Items.Add(choice);
            }
            prefixMenu.Closed += (sender, e) => prefixEditingUntil = DateTime.UtcNow.AddMilliseconds(250);
            prefixMenu.Show(AccountsView, new Point(cell.Left, cell.Bottom));
            prefixMenu.Items[selected == "Legit" ? 1 : selected == "Cheat" ? 2 : 0].Select();
        }
        private void SetAccountPrefix(Account account, string prefix)
        {
            if (prefix != "" && prefix != "Legit" && prefix != "Cheat") throw new ArgumentException("Неизвестный префикс.");
            bool hadValue = account.Fields.TryGetValue("ClientPrefix", out string previous);
            try
            {
                if (prefix == "") account.Fields.Remove("ClientPrefix");
                else account.Fields["ClientPrefix"] = prefix;
                SaveAccounts(true);
            }
            catch { if (hadValue) account.Fields["ClientPrefix"] = previous; else account.Fields.Remove("ClientPrefix"); throw; }
        }
    }
    internal sealed class PrefixMenu : ContextMenuStrip
    {
        internal PrefixMenu(Font font, int width)
        {
            Font = font; AutoSize = false; Width = width;
            Height = (int)(114 * Program.Scale);
            Padding = new Padding((int)(6 * Program.Scale));
            ShowImageMargin = false; ShowCheckMargin = true;
            BackColor = Forms.ThemeEditor.TextBoxesBackground;
            ForeColor = Forms.ThemeEditor.TextBoxesForeground;
            Renderer = new PrefixMenuRenderer();
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width < 10 || Height < 10) return;
            using (var path = NeonButton.Round(new RectangleF(0, 0, Width, Height), 10 * Program.Scale))
            { var previous = Region; Region = new Region(path); previous?.Dispose(); }
        }
    }
    internal sealed class PrefixMenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            => e.Graphics.Clear(Forms.ThemeEditor.TextBoxesBackground);
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = NeonButton.Round(new RectangleF(0.5f, 0.5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 10 * Program.Scale))
            using (var pen = new Pen(Forms.ThemeEditor.ButtonsBorder)) e.Graphics.DrawPath(pen, path);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = NeonButton.Round(new RectangleF(1, 1, e.Item.Width - 2, e.Item.Height - 2), 6 * Program.Scale))
            using (var brush = new SolidBrush(NeonButton.Mix(Forms.ThemeEditor.TextBoxesBackground, Forms.ThemeEditor.AccentPrimary, 0.25f)))
                e.Graphics.FillPath(brush, path);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        { e.TextColor = Forms.ThemeEditor.TextBoxesForeground; base.OnRenderItemText(e); }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var box = e.ImageRectangle;
            using (var pen = new Pen(Forms.ThemeEditor.AccentSecondary, Math.Max(1.6f, 1.6f * Program.Scale)))
                e.Graphics.DrawLines(pen, new[] { new PointF(box.Left + box.Width * .2f, box.Top + box.Height * .5f),
                    new PointF(box.Left + box.Width * .42f, box.Top + box.Height * .72f), new PointF(box.Left + box.Width * .82f, box.Top + box.Height * .28f) });
        }
    }
    internal sealed class PrefixRenderer : BaseRenderer
    {
        public override void Render(Graphics graphics, Rectangle bounds)
        {
            DrawBackground(graphics, bounds);
            if (!(RowObject is Account account)) return;
            var box = Rectangle.Inflate(bounds, -4, -6);
            if (box.Width < 20 || box.Height < 10) return;
            var previous = graphics.SmoothingMode;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = NeonButton.Round(box, 6 * Program.Scale))
            using (var brush = new SolidBrush(Forms.ThemeEditor.TextBoxesBackground))
            using (var border = new Pen(Forms.ThemeEditor.ButtonsBorder))
            { graphics.FillPath(brush, path); graphics.DrawPath(border, path); }
            graphics.SmoothingMode = previous;
            string prefix = RobloxClients.Prefix(account);
            var text = new Rectangle(box.Left + 7, box.Top, box.Width - 23, box.Height);
            TextRenderer.DrawText(graphics, prefix == "" ? UiText.Translate("Без префикса") : prefix, ListView.Font, text, Forms.ThemeEditor.TextBoxesForeground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(graphics, "▾", ListView.Font, new Rectangle(box.Right - 18, box.Top, 16, box.Height), Forms.ThemeEditor.LabelForeground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
