using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SurgeeUpdating;

[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.7.2")]
class ProcessArchitectureSmoke
{
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool IsWow64Process(IntPtr process,out bool wow64);
    static int Main(string[] args)
    {
        if(args.Length==1&&args[0]=="--probe"){Thread.Sleep(1500);return 0;}
        try
        {
            string executable=Path.GetFullPath(args[0]);int expected=int.Parse(args[1]);
            using(var child=Process.Start(new ProcessStartInfo(executable,"--probe"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden}))
            {
                bool wow64;if(!IsWow64Process(child.Handle,out wow64))throw new Exception("Cannot inspect probe architecture");
                int actual=Environment.Is64BitOperatingSystem&&!wow64?64:32;
                if(actual!=expected)throw new Exception("Unexpected probe architecture");
                Console.WriteLine("PASS updater/probe architecture pair: "+(IntPtr.Size*8)+" -> "+actual);
                if(IntPtr.Size==4&&actual==64)
                {
                    bool reproduced=false;
                    try{string ignored=child.MainModule.FileName;}
                    catch(Win32Exception error){reproduced=error.NativeErrorCode==299;}
                    if(!reproduced)throw new Exception("Old partial-copy error was not reproduced");
                    Console.WriteLine("PASS original MainModule error 299 reproduced without changing another application");
                }
                UpdateParentProcess.WaitForExit(child.Id,executable,6000);
                if(!child.WaitForExit(1000))throw new Exception("Probe did not exit");
                Console.WriteLine("PASS new updater waits across architectures without reading process memory");
                UpdateParentProcess.WaitForExit(child.Id,executable,0);
                Console.WriteLine("PASS exited parent is handled after cross-architecture wait");
            }
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine("FAIL "+error.Message);return 1;}
    }
}
