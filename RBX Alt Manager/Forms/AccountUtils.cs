using RBX_Alt_Manager.Forms;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountUtils : Form
    {
        // sorry i got lazy with this part of code and didnt name any elemennts

        public AccountUtils()
        {
            AccountManager.SetDarkBar(Handle);

            InitializeComponent();
            InitializeModernUtilities();
        }

        public void ApplyTheme()
        {
            BackColor = ThemeEditor.FormsBackground;
            ForeColor = ThemeEditor.FormsForeground;

            foreach (Control control in UtilityControls(this))
            {
                if (control is Button || control is CheckBox)
                {
                    if (control is Button)
                    {
                        Button b = control as Button;
                        b.FlatStyle = ThemeEditor.ButtonStyle;
                        b.FlatAppearance.BorderColor = ThemeEditor.ButtonsBorder;
                    }

                    if (!(control is CheckBox)) control.BackColor = ThemeEditor.ButtonsBackground;
                    control.ForeColor = ThemeEditor.ButtonsForeground;
                }
                else if (control is TextBox || control is RichTextBox)
                {
                    if (control is Classes.BorderedTextBox)
                    {
                        Classes.BorderedTextBox b = control as Classes.BorderedTextBox;
                        b.BorderColor = ThemeEditor.TextBoxesBorder;
                    }

                    if (control is Classes.BorderedRichTextBox)
                    {
                        Classes.BorderedRichTextBox b = control as Classes.BorderedRichTextBox;
                        b.BorderColor = ThemeEditor.TextBoxesBorder;
                    }

                    control.BackColor = ThemeEditor.TextBoxesBackground;
                    control.ForeColor = ThemeEditor.TextBoxesForeground;
                }
                else if (control is Label)
                {
                    control.BackColor = Color.Transparent;
                    control.ForeColor = ThemeEditor.LabelForeground;
                }
                else if (control is ListBox)
                {
                    control.BackColor = ThemeEditor.ButtonsBackground;
                    control.ForeColor = ThemeEditor.ButtonsForeground;
                }
                else if (control is UtilityCard) control.BackColor = Classes.NeonButton.Mix(ThemeEditor.TextBoxesBackground, ThemeEditor.FormsBackground, .35f);
                else if (control is UtilityInput) control.BackColor = control.Parent.BackColor;
                else if (control is Panel) control.BackColor = ThemeEditor.FormsBackground;
            }
            Invalidate(true);
        }

        private void AccountUtils_FormClosing(object sender, FormClosingEventArgs e)
        {
            ClearSensitiveFields();
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        }

        private void WhoFollow_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (editingAccount == null || WhoFollow.SelectedIndex < 0) return;

            if (editingAccount.SetFollowPrivacy(WhoFollow.SelectedIndex)) privacyButton.Text = PrivacyChoices[WhoFollow.SelectedIndex];
            else
            {
                WhoFollow.SelectedIndex = -1;
                privacyButton.Text = "Кто может присоединяться";
                UiMessageBox.Show(this, "Roblox не применил настройку приватности. Проверь вход и попробуй снова.", "Приватность");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (editingAccount == null) return;

            DialogResult result = UiMessageBox.Show(this, "Завершить остальные сеансы этого аккаунта?", "Действия с аккаунтом", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
                editingAccount.LogOutOfOtherSessions();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (editingAccount == null) return;

            editingAccount.UnlockPin(textBox5.Text);
        }

        private void textBox5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (editingAccount == null) return;

            if (e.KeyChar == (char)Keys.Enter)
            {
                editingAccount.UnlockPin(textBox5.Text);
                button7.Focus();

                return;
            }

            e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar);
        }

        private void textBox5_Enter(object sender, EventArgs e)
        {
            textBox5.Text = "";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (editingAccount == null) return;

            editingAccount.ChangePassword(textBox1.Text, textBox2.Text);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (editingAccount == null) return;

            editingAccount.ChangeEmail(textBox1.Text, textBox3.Text);
        }

        private void Block_Click(object sender, EventArgs e)
        {
            if (editingAccount == null) return;

            bool WasUnblocked = false;

            try
            {
                if (editingAccount.TogglePlayerBlocked(Username.Text, ref WasUnblocked))
                    UiMessageBox.Show($"{(WasUnblocked ? "Unb" : "B")}locked {Username.Text}");
                else
                    UiMessageBox.Show($"Failed to {(WasUnblocked ? "Unb" : "B")}lock {Username.Text}");
            }
            catch (Exception x) { UiMessageBox.Show($"Failed to {(WasUnblocked ? "Unb" : "B")}lock {Username.Text}\n\n{x.Message}"); }
        }

        private void SetDisplayName_Click(object sender, EventArgs e)
        {
            if (editingAccount == null) return;

            try
            {
                editingAccount.SetDisplayName(DisplayName.Text);
                UiMessageBox.Show($"Successfully set {editingAccount.Username}'s Display Name to {DisplayName.Text}", "Account Utilities", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception x) { UiMessageBox.Show(x.Message, "Account Utilities", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void AddFriend_Click(object sender, EventArgs e)
        {
            if (editingAccount == null) return;

            editingAccount.SendFriendRequest(Username.Text);
        }
    }
}
