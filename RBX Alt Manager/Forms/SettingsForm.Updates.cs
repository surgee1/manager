using RBX_Alt_Manager.Classes;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Forms
{
    public partial class SettingsForm
    {
        private Button checkUpdate, installUpdate, importUpdate;
        private Label updateStatus;
        private ReleaseInfo availableRelease;
        private TabPage updatesTab;
        private void InitializeUpdateSettings()
        {
            var page = SettingsPage("Обновления");
            updatesTab = (TabPage)page.Parent;
            SettingsTC.Multiline = true;
            Section(page, "SURGEE MANAGER " + ManagerUpdates.Version);
            Description(page, "Обновления из surgee1/manager. Аккаунты, папки, избранное и оформление сохраняются.");
            updateStatus = new Label { Text = "Нажми «Проверить», чтобы найти новую версию.", AutoSize = true, MaximumSize = new Size(630, 0), Margin = new Padding(3, 15, 3, 15) };
            page.Controls.Add(updateStatus);
            checkUpdate = new NeonButton { Text = "Проверить обновления", Width = 320, Height = 42, Margin = new Padding(0, 8, 0, 0) };
            installUpdate = new NeonButton { Text = "Обновить", Width = 320, Height = 42, Enabled = false, Margin = new Padding(0, 8, 0, 0) };
            importUpdate = new NeonButton { Text = "Установить из ZIP…", Width = 320, Height = 42, Margin = new Padding(0, 8, 0, 0) };
            page.Controls.Add(checkUpdate); page.Controls.Add(installUpdate); page.Controls.Add(importUpdate);
            checkUpdate.Click += async (sender, e) => await CheckManagerUpdate();
            installUpdate.Click += async (sender, e) =>
            {
                if (availableRelease == null) return;
                if (MessageBox.Show(this, "Установить Surgee " + availableRelease.Version + "? Менеджер скачает обновление и перезапустится.", "Обновление", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                SetUpdateBusy(true);
                try
                {
                    updateStatus.Text = "Скачиваем обновление…";
                    string archive = await ManagerUpdates.DownloadAsync(availableRelease);
                    await InstallArchive(archive);
                }
                catch (Exception ex) { updateStatus.Text = "Обновление не установлено: " + ex.Message; }
                finally { SetUpdateBusy(false); }
            };
            importUpdate.Click += async (sender, e) =>
            {
                using var dialog = new OpenFileDialog { Filter = "Обновление Surgee (*.zip)|*.zip", Title = "Выбери ZIP менеджера версии 6 или новее" };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (MessageBox.Show(this, "Установить обновление из выбранного ZIP и перезапустить менеджер?", "Обновление", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                SetUpdateBusy(true);
                try { await InstallArchive(dialog.FileName); }
                catch (Exception ex) { updateStatus.Text = "Обновление не установлено: " + ex.Message; }
                finally { SetUpdateBusy(false); }
            };
            Description(page, "Для установки интернет-обновления браузер открывать не нужно. ZIP можно установить вручную, если GitHub недоступен.");
        }
        public async void UpdateManagerFromMain()
        {
            SettingsTC.SelectedTab = updatesTab;
            if (!checkUpdate.Enabled) return;
            await CheckManagerUpdate();
            if (availableRelease != null && !IsDisposed) installUpdate.PerformClick();
        }
        private async Task CheckManagerUpdate()
        {
            SetUpdateBusy(true); availableRelease = null;
            updateStatus.Text = "Проверяем GitHub Releases…";
            try
            {
                var release = await ManagerUpdates.CheckAsync();
                if (new Version(release.Version) > new Version(ManagerUpdates.Version))
                { availableRelease = release; updateStatus.Text = "Доступна версия " + release.Version + ". Нажми «Обновить»."; }
                else updateStatus.Text = "У тебя актуальная версия: " + ManagerUpdates.Version + ".";
            }
            catch (Exception ex) { updateStatus.Text = "Проверка не завершена: " + ex.Message; }
            finally { SetUpdateBusy(false); }
        }
        private async Task InstallArchive(string path)
        {
            updateStatus.Text = "Проверяем файлы обновления…";
            string stage = await Task.Run(() => ManagerUpdates.Stage(path));
            AccountManager.SaveAccounts(true);
            updateStatus.Text = "Перезапускаем менеджер…";
            ManagerUpdates.StartInstaller(stage);
            AccountManager.Instance.Close();
        }
        private void SetUpdateBusy(bool busy)
        {
            if (IsDisposed) return;
            checkUpdate.Enabled = importUpdate.Enabled = !busy;
            installUpdate.Enabled = !busy && availableRelease != null;
        }
    }
}
