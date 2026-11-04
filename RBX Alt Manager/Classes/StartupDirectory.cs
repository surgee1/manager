using System;
using System.IO;

namespace RBX_Alt_Manager.Classes
{
    internal static class StartupDirectory
    {
        internal static bool Initialize(string applicationDirectory)
        {
            applicationDirectory = Path.GetFullPath(applicationDirectory);
            string launchDirectory = Directory.GetCurrentDirectory();
            bool migrated = false;
            if (!string.Equals(applicationDirectory.TrimEnd(Path.DirectorySeparatorChar), launchDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                && !File.Exists(Path.Combine(applicationDirectory, "AccountData.json")) && File.Exists(Path.Combine(launchDirectory, "AccountData.json")))
            {
                // Preserve a legacy launch-directory database without changing its original files.
                foreach (string name in new[] { "RAMSettings.ini", "RAMTheme.ini", "AccountFolders.json", "FavoritePlaces.json", "RecentGames.json", "LaunchProfiles.json" })
                    CopyMissing(launchDirectory, applicationDirectory, name);
                migrated = CopyMissing(launchDirectory, applicationDirectory, "AccountData.json"); // Database commits last.
            }
            Directory.SetCurrentDirectory(applicationDirectory);
            return migrated;
        }

        private static bool CopyMissing(string sourceDirectory, string destinationDirectory, string name)
        {
            string source = Path.Combine(sourceDirectory, name), target = Path.Combine(destinationDirectory, name);
            if (!File.Exists(source) || File.Exists(target)) return false;
            string temporary = target + ".startup-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Copy(source, temporary);
                File.SetAttributes(temporary, FileAttributes.Normal);
                try { File.Move(temporary, target); }
                catch (IOException) { if (!File.Exists(target)) throw; else return false; }
                return true;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
