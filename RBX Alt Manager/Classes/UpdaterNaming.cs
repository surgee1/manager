using System;
using System.IO;
using SurgeeUpdating;

namespace RBX_Alt_Manager.Classes
{
    internal static class UpdaterNaming
    {
        internal const string Current = "SpectrumUpdater.exe", Legacy = "SurgeeUpdater.exe";
        internal static void PromoteLegacy(string directory)
        {
            string old = Path.Combine(directory, Legacy), current = Path.Combine(directory, Current);
            if (!File.Exists(old)) return;
            string temporary = Path.Combine(directory, ".spectrum-updater-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.Copy(old, temporary);
                if (FileUpdateTransaction.Hash(old) != FileUpdateTransaction.Hash(temporary)) throw new IOException("Updater migration verification failed.");
                if (File.Exists(current)) File.Replace(temporary, current, null); else File.Move(temporary, current);
                File.Delete(old);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static string Resolve(string directory)
        {
            string primary = Path.Combine(directory, Current);
            if (File.Exists(primary)) return primary;
            string legacy = Path.Combine(directory, Legacy);
            if (File.Exists(legacy)) return legacy;
            throw new IOException("SpectrumUpdater.exe не найден. Распакуй полный ZIP менеджера.");
        }
    }
}
