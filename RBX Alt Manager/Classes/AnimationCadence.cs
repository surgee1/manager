using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    internal static class AnimationCadence
    {
        private static readonly Stopwatch InteractionClock = Stopwatch.StartNew();
        private static int interactions;
        private static double pausedTotal, pauseStart;
        internal static bool Interacting => interactions > 0;
        internal static double PausedSeconds => pausedTotal + (Interacting ? InteractionClock.Elapsed.TotalSeconds - pauseStart : 0);
        internal static void BeginInteraction() { if (interactions++ == 0) pauseStart = InteractionClock.Elapsed.TotalSeconds; }
        internal static void EndInteraction() { if (interactions > 0 && --interactions == 0) pausedTotal += InteractionClock.Elapsed.TotalSeconds - pauseStart; }
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
