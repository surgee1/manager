using System;
using System.IO;
using System.Windows.Forms;

namespace RBX_Alt_Manager
{
    public partial class AccountManager
    {
        private Timer launchInputsSaveTimer;
        private bool launchInputsReady, launchInputsDirty;

        private void InitializeLaunchInputSaving()
        {
            launchInputsSaveTimer = new Timer(components) { Interval = 350 };
            launchInputsSaveTimer.Tick += (sender, e) => FlushLaunchInputs();
            EventHandler changed = (sender, e) =>
            {
                if (!launchInputsReady || Program.PreviewMode) return;
                launchInputsDirty = true;
                launchInputsSaveTimer.Stop();
                launchInputsSaveTimer.Start();
            };
            PlaceID.TextChanged += changed;
            JobID.TextChanged += changed;
            PlaceID.Leave += (sender, e) => FlushLaunchInputs();
            JobID.Leave += (sender, e) => FlushLaunchInputs();
        }

        private void RestoreLaunchInputs()
        {
            launchInputsReady = false;
            launchInputsSaveTimer.Stop();
            PlaceID.Text = General.Exists("SavedPlaceId") ? General.Get("SavedPlaceId") : General.Get<bool>("SavedLaunchInputs") ? "" : "5315046213";
            JobID.Text = General.Exists("SavedJobId") ? General.Get("SavedJobId") : "";
            launchInputsDirty = false;
            launchInputsReady = true;
        }

        private void FlushLaunchInputs()
        {
            launchInputsSaveTimer?.Stop();
            if (!launchInputsReady || !launchInputsDirty || Program.PreviewMode) return;
            General.Set("SavedPlaceId", PlaceID.Text);
            General.Set("SavedJobId", JobID.Text);
            General.Set("SavedLaunchInputs", "true");
            try
            {
                IniSettings.Save("RAMSettings.ini");
                launchInputsDirty = false;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                SaveTooltip.SetToolTip(PlaceID, "Не удалось автоматически сохранить игру: проверь доступ к папке менеджера.");
                SaveTooltip.SetToolTip(JobID, "Не удалось автоматически сохранить сервер: проверь доступ к папке менеджера.");
            }
        }
    }
}
