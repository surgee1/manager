using System;
using System.IO;
using RBX_Alt_Manager.Classes;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.7.2")]
class ShortcutPathProbe
{
    static int Main()
    {
        try
        {
            string started=Directory.GetCurrentDirectory();
            bool copied=StartupDirectory.Initialize(AppDomain.CurrentDomain.BaseDirectory);
            File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-proof.txt"),new[]{
                "different-launch-folder="+(!string.Equals(started.TrimEnd(Path.DirectorySeparatorChar),Directory.GetCurrentDirectory().TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)),
                "legacy-database-copied="+copied,
                "storage-beside-exe="+string.Equals(Directory.GetCurrentDirectory().TrimEnd(Path.DirectorySeparatorChar),AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)});
            return 0;
        }
        catch{File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-error.txt"),"Startup check failed");return 1;}
    }
}
