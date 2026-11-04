using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using RBX_Alt_Manager.Classes;
using SurgeeUpdating;

internal static class StartupUpdateTests
{
    internal static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        string executable = Assembly.GetExecutingAssembly().Location;
        UpdateParentProcess.WaitForExit(int.MaxValue, executable);
        check(true, "updater accepts a parent that already exited before opening its process handle");
        reject(() => UpdateParentProcess.WaitForExit(0, executable), "updater rejects invalid process identifiers");
        bool timedOut = false;
        try { UpdateParentProcess.WaitForExit(Process.GetCurrentProcess().Id, executable.ToUpperInvariant(), 0); }
        catch (IOException error) { timedOut = error.Message.Contains("не закрылся"); }
        check(timedOut, "updater queries live executable without process-memory access and respects timeout");
        bool mismatch = false;
        try { UpdateParentProcess.WaitForExit(Process.GetCurrentProcess().Id, executable + ".different", 0); }
        catch (IOException error) { mismatch = error.Message.Contains("не совпал"); }
        check(mismatch, "updater refuses a live process belonging to another executable");
        using (var child = Process.Start(new ProcessStartInfo(executable, "--process-probe 350") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
        {
            UpdateParentProcess.WaitForExit(child.Id, executable, 5000);
            check(child.WaitForExit(1000), "updater waits for verified parent termination");
            UpdateParentProcess.WaitForExit(child.Id, executable, 0);
            check(true, "updater tolerates termination between process lookup and inspection");
        }

        string initial = Directory.GetCurrentDirectory();
        string root = Path.Combine(Path.GetTempPath(), "SpectrumStartup-" + Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root,"hidden app"), launch = Path.Combine(root,"shortcut working folder");
        Directory.CreateDirectory(app); Directory.CreateDirectory(launch);
        try
        {
            Directory.SetCurrentDirectory(launch);
            check(!StartupDirectory.Initialize(app) && Directory.GetCurrentDirectory() == app, "shortcut with unrelated working folder resolves storage beside executable");
            check(Directory.GetFiles(launch).Length == 0, "fresh shortcut launch writes no account files into its working folder");
            Directory.SetCurrentDirectory(launch);
            File.WriteAllBytes(Path.Combine(launch,"AccountData.json"),new byte[]{1,19,0,255,29,40});
            File.WriteAllText(Path.Combine(launch,"AccountFolders.json"),"[\"Legacy folder\"]");
            File.WriteAllText(Path.Combine(launch,"RAMTheme.ini"),"legacy-theme");
            File.WriteAllText(Path.Combine(app,"RAMTheme.ini"),"keep-theme");
            File.SetAttributes(app, File.GetAttributes(app) | FileAttributes.Hidden);
            check(StartupDirectory.Initialize(app), "legacy launch-directory account database is copied when application has none");
            check(Convert.ToBase64String(File.ReadAllBytes(Path.Combine(app,"AccountData.json"))) == Convert.ToBase64String(File.ReadAllBytes(Path.Combine(launch,"AccountData.json"))), "startup migration preserves opaque encrypted account bytes and original file");
            check(File.ReadAllText(Path.Combine(app,"AccountFolders.json")).Contains("Legacy folder"), "legacy account folders accompany the copied database");
            check(File.ReadAllText(Path.Combine(app,"RAMTheme.ini")) == "keep-theme", "startup migration preserves existing application preferences");
            check((File.GetAttributes(app)&FileAttributes.Hidden)!=0 && Directory.GetCurrentDirectory()==app, "hidden application folder remains hidden and is used for storage");
            Directory.SetCurrentDirectory(launch);
            File.WriteAllText(Path.Combine(launch,"AccountData.json"),"other legacy data");
            check(!StartupDirectory.Initialize(app) && File.ReadAllBytes(Path.Combine(app,"AccountData.json"))[0] == 1, "existing application database always wins over shortcut working-directory data");
            check(Directory.GetFiles(app,"*.startup-*").Length == 0, "startup migration leaves no partial account-copy files");
        }
        finally { Directory.SetCurrentDirectory(initial); }
    }
}
