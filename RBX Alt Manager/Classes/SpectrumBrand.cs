using System.Drawing;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    internal static class SpectrumBrand
    {
        private static readonly Icon icon = Load();
        private static Icon Load()
        {
            var assembly = typeof(SpectrumBrand).Assembly;
            using (var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(name => name.EndsWith("Resources.Spectrum.ico"))))
            using (var source = new Icon(stream)) return (Icon)source.Clone();
        }
        internal static void Apply(Form form) { form.Icon = icon; }
        internal static async Task RefreshShortcutsAsync(string executable)
        {
            try
            {
                await Task.Run(() =>
                {
                    string identity;
                    using (var sha = SHA256.Create()) identity = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(executable).ToLowerInvariant()))).Replace("-", "").Substring(0, 16);
                    string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spectrum Manager", "Icons", identity);
                    string iconFile = Path.Combine(root, "Spectrum-" + ManagerUpdates.Version + ".ico"), marker = iconFile + ".ready";
                    if (File.Exists(marker)) return;
                    Directory.CreateDirectory(root);
                    using (var stream = File.Create(iconFile)) icon.Save(stream);
                    string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string[] folders = { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                        Path.Combine(roaming, @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"),
                        Path.Combine(roaming, @"Microsoft\Internet Explorer\Quick Launch\User Pinned\StartMenu") };
                    UpdateShortcuts(folders, executable, iconFile);
                    SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
                    File.WriteAllText(marker, ManagerUpdates.Version);
                });
            }
            catch { Program.Logger.Info("Shortcut icon refresh deferred; application icon is already updated."); }
        }
        internal static int UpdateShortcuts(string[] folders, string executable, string iconFile)
        {
            int updated = 0; object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                foreach (string folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!Directory.Exists(folder)) continue;
                    string[] paths;
                    try { paths = Directory.GetFiles(folder, "*.lnk", SearchOption.AllDirectories); }
                    catch (IOException) { continue; } catch (UnauthorizedAccessException) { continue; }
                    foreach (string path in paths)
                    {
                        object instance = null;
                        try
                        {
                            dynamic automation = shell; instance = automation.CreateShortcut(path); dynamic shortcut = instance;
                            string target = shortcut.TargetPath;
                            if (string.IsNullOrWhiteSpace(target) || !string.Equals(Path.GetFullPath(target), Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase)) continue;
                            shortcut.IconLocation = iconFile + ",0"; shortcut.Save(); updated++;
                        }
                        catch { /* Unwritable or unrelated shortcuts do not prevent startup. */ }
                        finally { if (instance != null && Marshal.IsComObject(instance)) Marshal.FinalReleaseComObject(instance); }
                    }
                }
            }
            finally { if (Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
            return updated;
        }
        [DllImport("shell32.dll")] private static extern void SHChangeNotify(uint change, uint flags, IntPtr first, IntPtr second);
    }
}
