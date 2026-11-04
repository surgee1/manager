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
        private DateTime prefixEditingUntil;
        private void InitializeAccountPrefixes()
        {
            prefixColumn = new OLVColumn("Prefix", "") { Width = Ui(116), MinimumWidth = Ui(100), IsEditable = true, IsVisible = true,
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
                AccountsView.EditSubItem(e.Item, e.ColumnIndex);
                e.Handled = true;
            };
            AccountsView.CellEditStarting += (sender, e) =>
            {
                if (e.Column != prefixColumn) return;
                var editor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = AccountsView.Font,
                    BackColor = Forms.ThemeEditor.TextBoxesBackground, ForeColor = Forms.ThemeEditor.TextBoxesForeground,
                    Bounds = e.CellBounds, IntegralHeight = true };
                editor.Items.AddRange(new object[] { UiText.Translate("Без префикса"), "Legit", "Cheat" });
                string prefix = RobloxClients.Prefix((Account)e.RowObject);
                editor.SelectedIndex = prefix == "Legit" ? 1 : prefix == "Cheat" ? 2 : 0;
                e.Control = editor;
                editor.SelectionChangeCommitted += (s, args) => AccountsView.FinishCellEdit();
                BeginInvoke(new Action(() => { if (!editor.IsDisposed && editor.Visible) editor.DroppedDown = true; }));
            };
            AccountsView.CellEditFinishing += (sender, e) =>
            {
                if (e.Column != prefixColumn || e.Cancel) return;
                var editor = e.Control as ComboBox;
                if (editor == null) return;
                try { SetAccountPrefix((Account)e.RowObject, editor.SelectedIndex == 1 ? "Legit" : editor.SelectedIndex == 2 ? "Cheat" : ""); }
                catch (Exception error) { UiMessageBox.Show(this, error.Message, "Сохранение префикса", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                e.Cancel = true; // Value was committed explicitly; no reflection-based setter is needed.
                AccountsView.RefreshObject(e.RowObject);
            };
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
