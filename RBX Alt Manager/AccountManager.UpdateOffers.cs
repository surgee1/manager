using RBX_Alt_Manager.Classes;
using RBX_Alt_Manager.Forms;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private bool automaticUpdateChecked;
        internal static bool ShouldOfferUpdate(string installed, string latest)
            => Version.TryParse(installed, out var current) && Version.TryParse(latest, out var next) && next > current;
        private async Task OfferUpdateAfterStartupAsync()
        {
            if (automaticUpdateChecked || Program.PreviewMode || (General.Exists("AutoOfferUpdates") && !General.Get<bool>("AutoOfferUpdates"))) return;
            automaticUpdateChecked = true;
            try
            {
                await Task.Delay(1800);
                var release = await ManagerUpdates.CheckFreshAsync();
                while (!IsDisposed && credentialImportForm?.Visible == true) await Task.Delay(1000);
                if (IsDisposed || Disposing || !ShouldOfferUpdate(ManagerUpdates.Version, release.Version)) return;
                using (var dialog = new Form { Text = "Доступно обновление", ClientSize = new Size(500, 206),
                    StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                    MinimizeBox = false, MaximizeBox = false, Font = Font, BackColor = BackColor, ForeColor = ForeColor })
                {
                    var title = new Label { Text = "Spectrum Manager " + release.Version, Font = new Font(Font.FontFamily, 17, FontStyle.Bold), Bounds = new Rectangle(24, 24, 450, 36) };
                    var message = new Label { Text = "Доступна новая версия. Установить обновление сейчас?\nАккаунты и настройки сохранятся.", Bounds = new Rectangle(26, 74, 450, 54) };
                    var update = new NeonButton { Text = "Обновить сейчас", Accent = true, DialogResult = DialogResult.Yes, Bounds = new Rectangle(24, 145, 224, 38) };
                    var later = new NeonButton { Text = "Позже", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(258, 145, 214, 38) };
                    dialog.Controls.AddRange(new Control[] { title, message, update, later }); dialog.AcceptButton = update; dialog.CancelButton = later;
                    RoundedWindow.Attach(dialog); UiText.Attach(dialog);
                    if (dialog.ShowDialog(this) != DialogResult.Yes) return;
                }
                ConfigButton_Click(this, EventArgs.Empty);
                await SettingsForm.InstallOfferedReleaseAsync(release);
            }
            catch { Program.Logger.Info("Automatic update check unavailable; manual update remains available."); }
        }
    }
}
