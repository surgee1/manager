using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Forms
{
    internal sealed class QuickCommands : Form
    {
        private readonly List<Tuple<string, Action>> commands;
        private readonly ListBox list;
        private readonly TextBox search;
        internal QuickCommands(List<Tuple<string, Action>> commands)
        {
            this.commands = commands;
            Text = "Быстрые команды • Ctrl+K"; StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(590, 410); FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = MaximizeBox = false; Font = new Font("Segoe UI Semibold", 11);
            BackColor = ThemeEditor.FormsBackground; ForeColor = ThemeEditor.FormsForeground;
            search = new TextBox { Location = new Point(20, 20), Width = 550, AccessibleName = "Найти команду" };
            list = new ListBox { Location = new Point(20, 65), Size = new Size(550, 295), BorderStyle = BorderStyle.None,
                ItemHeight = 38, DrawMode = DrawMode.OwnerDrawFixed, DisplayMember = "Item1" };
            var hint = new Label { Text = "↑ ↓ — выбрать    Enter — выполнить    Esc — закрыть", AutoSize = true, Location = new Point(20, 378) };
            Controls.AddRange(new Control[] { search, list, hint }); Controls.ApplyTheme();
            list.DrawItem += (sender, e) =>
            {
                e.DrawBackground();
                if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, ((Tuple<string, Action>)list.Items[e.Index]).Item1, Font,
                    new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 18, e.Bounds.Height), e.ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            search.TextChanged += (sender, e) => Filter(); list.DoubleClick += (sender, e) => Choose();
            Shown += (sender, e) => search.Focus(); Filter();
        }
        private void Filter()
        {
            list.BeginUpdate(); list.Items.Clear();
            list.Items.AddRange(commands.Where(item => item.Item1.IndexOf(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).Cast<object>().ToArray());
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            list.EndUpdate();
        }
        private void Choose()
        {
            if (!(list.SelectedItem is Tuple<string, Action> command)) return;
            // Run on the owner after this modal form has been disposed.
            Owner.BeginInvoke(command.Item2); Close();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keys)
        {
            if (keys == Keys.Escape) { Close(); return true; }
            if (keys == Keys.Enter) { Choose(); return true; }
            if ((keys == Keys.Down || keys == Keys.Up) && list.Items.Count > 0)
            { list.SelectedIndex = Math.Max(0, Math.Min(list.Items.Count - 1, list.SelectedIndex + (keys == Keys.Down ? 1 : -1))); return true; }
            return base.ProcessCmdKey(ref msg, keys);
        }
    }
}
