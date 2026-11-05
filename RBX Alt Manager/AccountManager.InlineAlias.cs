using RBX_Alt_Manager.Classes;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private void InitializeInlineAliases()
        {
            AccountAlias.IsEditable = true;
            AccountsView.CellClick += (sender, e) =>
            {
                if (e.Column != AccountAlias || e.Item == null || e.ClickCount != 1) return;
                prefixEditingUntil = DateTime.UtcNow.AddSeconds(1);
                AccountsView.EditSubItem(e.Item, e.ColumnIndex); e.Handled = true;
            };
            AccountsView.CellEditStarting += (sender, e) =>
            {
                if (e.Column != AccountAlias) return;
                prefixEditingUntil = DateTime.UtcNow.AddSeconds(1);
                var editor = new BorderedTextBox { MaxLength = 50, Text = ((Account)e.RowObject).Alias, Font = AccountsView.Font,
                    BackColor = Forms.ThemeEditor.TextBoxesBackground, ForeColor = Forms.ThemeEditor.TextBoxesForeground,
                    BorderColor = Forms.ThemeEditor.AccentPrimary, Bounds = Rectangle.Inflate(e.CellBounds, -2, -3) };
                e.Control = editor;
                BeginInvoke(new Action(() => { if (!editor.IsDisposed) editor.SelectAll(); }));
            };
            AccountsView.CellEditFinishing += (sender, e) =>
            {
                if (e.Column != AccountAlias) return;
                prefixEditingUntil = DateTime.UtcNow.AddMilliseconds(250);
                if (e.Cancel) return;
                var account = (Account)e.RowObject; string previous = account.Alias;
                e.Cancel = true;
                try
                {
                    string alias = e.Control.Text;
                    if (alias == previous) return;
                    account.SetAliasValue(alias); SaveAccounts(true);
                    if (SelectedAccount == account) Alias.Text = alias;
                }
                catch { account.SetAliasValue(previous); UiMessageBox.Show(this, "Не удалось сохранить псевдоним. Проверь доступ к папке менеджера.", "Псевдоним"); }
                AccountsView.RefreshObject(account);
            };
        }
    }
}
