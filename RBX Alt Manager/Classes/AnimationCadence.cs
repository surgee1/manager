using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    internal static class AnimationCadence
    {
        internal static int Interval
        {
            get
            {
                bool economy = AccountManager.General == null || !AccountManager.General.Exists("AnimationEconomy") || AccountManager.General.Get<bool>("AnimationEconomy");
                // Any manager dialog counts as active, so main and settings animate together.
                return economy && Form.ActiveForm == null ? 200 : 33;
            }
        }
    }
}
