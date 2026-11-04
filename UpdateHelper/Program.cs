using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using SurgeeUpdating;

internal static class UpdateProgram
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            if (args.Length != 3) throw new InvalidOperationException("Неверные параметры обновления.");
            int pid;
            if (!int.TryParse(args[0], out pid)) throw new InvalidOperationException("Неверный процесс менеджера.");
            string destination = Path.GetFullPath(args[1]), stage = Path.GetFullPath(args[2]);
            string executable = Path.Combine(destination, "Roblox Account Manager.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("Папка менеджера не найдена.");
            try
            {
                using (var parent = Process.GetProcessById(pid))
                {
                    if (!string.Equals(parent.MainModule.FileName, executable, StringComparison.OrdinalIgnoreCase)) throw new IOException("Путь менеджера не совпал.");
                    if (!parent.WaitForExit(30000)) throw new IOException("Менеджер не закрылся. Обновление отменено.");
                }
            }
            catch (ArgumentException) { /* The manager has already closed. */ }
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(Path.Combine(stage, "update-plan.tsv")))
            {
                string[] pair = line.Split('\t');
                if (pair.Length != 2 || pair[1].Length != 64 || !FileUpdateTransaction.Allowed(pair[0]) || files.ContainsKey(pair[0]))
                    throw new InvalidDataException("Неверный план обновления.");
                files.Add(pair[0], pair[1]);
            }
            if (!files.ContainsKey("Roblox Account Manager.exe") || files.Count > 256) throw new InvalidDataException("В обновлении нет менеджера.");
            FileUpdateTransaction.Apply(stage, destination, files);
            Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = destination, UseShellExecute = true });
        }
        catch (Exception e)
        {
            MessageBox.Show("Не удалось установить обновление. Аккаунты и настройки не заменялись.\n\n" + e.Message,
                "Обновление Surgee", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
