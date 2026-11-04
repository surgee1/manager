using RBX_Alt_Manager.Classes;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

internal static class ClientLaunchTests
{
    private sealed class Deployment : HttpMessageHandler
    {
        internal byte[] Zip; internal bool Corrupt;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            string url = request.RequestUri.AbsoluteUri;
            string hash; using (var md5 = MD5.Create()) hash = BitConverter.ToString(md5.ComputeHash(Zip)).Replace("-", "").ToLowerInvariant();
            HttpContent body = url.Contains("client-version") ? (HttpContent)new StringContent("{\"clientVersionUpload\":\"version-0123456789abcdef\"}") :
                url.EndsWith("rbxPkgManifest.txt") ? new StringContent("v0\nRobloxApp.zip\n" + hash + "\n" + Zip.Length + "\n10\n") :
                new ByteArrayContent(Corrupt ? new byte[Zip.Length] : Zip);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body });
        }
    }
    internal static async Task Run(Action<bool, string> check, Action<Action, string> reject)
    {
        string root = Path.Combine(Path.GetTempPath(), "SpectrumClientTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string legit = Path.Combine(root, "legit", RobloxClients.ExecutableName), cheat = Path.Combine(root, "cheat", RobloxClients.ExecutableName);
            Directory.CreateDirectory(Path.GetDirectoryName(legit)); Directory.CreateDirectory(Path.GetDirectoryName(cheat));
            File.WriteAllText(legit, "original"); File.WriteAllText(cheat, "modified");
            const string protocol = "roblox-player:1+launchmode:play+gameinfo:synthetic+placelauncherurl:https%3a%2f%2fexample.test";
            var start = RobloxClients.ProtocolStart(legit, protocol);
            check(start.FileName == legit && start.Arguments == "\"" + protocol + "\"" && !start.UseShellExecute, "Legit invokes selected EXE, independent of protocol registration");
            check(RobloxClients.ProtocolStart(cheat, protocol).FileName == cheat && File.ReadAllText(legit) == "original", "Cheat routes to separate existing EXE without modifying clients");
            reject(() => RobloxClients.ProtocolStart(legit, protocol + "\" --other"), "Reject protocol argument injection");
            reject(() => RobloxClients.Validate(Path.Combine(root, "missing", RobloxClients.ExecutableName)), "Missing client fails before authentication");
            reject(() => OfficialRobloxInstaller.VersionId("{\"clientVersionUpload\":\"../bad\"}"), "Deployment version cannot escape install root");
            reject(() => OfficialRobloxInstaller.ParseManifest("v0\nUnknown.zip\n" + new string('a', 32) + "\n1\n1\n"), "Unknown Roblox package fails closed");
            reject(() => OfficialRobloxInstaller.ParseManifest("v0\nRobloxApp.zip\n" + new string('a', 32) + "\n1\n"), "Truncated deployment manifest rejected");
            byte[] zip;
            using (var memory = new MemoryStream())
            {
                using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
                {
                    archive.CreateEntry("\\");
                    archive.CreateEntry("\\abilities\\");
                    using (var writer = new StreamWriter(archive.CreateEntry(RobloxClients.ExecutableName).Open())) writer.Write("testclient");
                }
                zip = memory.ToArray();
            }
            using (var http = new HttpClient(new Deployment { Zip = zip }))
            {
                string installed = await new OfficialRobloxInstaller(http).InstallAsync(Path.Combine(root, "managed"), null, CancellationToken.None);
                check(File.ReadAllText(installed) == "testclient" && File.Exists(Path.Combine(Path.GetDirectoryName(installed), "AppSettings.xml")), "Official packages verified, extracted and activated after completion");
                using (var corrupt = new HttpClient(new Deployment { Zip = zip, Corrupt = true }))
                {
                    bool failed = false; try { await new OfficialRobloxInstaller(corrupt).InstallAsync(Path.Combine(root, "managed"), null, CancellationToken.None); } catch (IOException) { failed = true; }
                    check(failed && File.ReadAllText(installed) == "testclient" && !Directory.GetDirectories(Path.Combine(root, "managed"), ".download-*").Any(), "Bad download preserves installed client and removes staging");
                }
                using (var cancel = new CancellationTokenSource())
                {
                    cancel.Cancel(); bool stopped = false;
                    try { await new OfficialRobloxInstaller(http).InstallAsync(Path.Combine(root, "managed"), null, cancel.Token); } catch (OperationCanceledException) { stopped = true; }
                    check(stopped && File.Exists(installed), "Cancelled install preserves working client");
                }
            }
            string malicious = Path.Combine(root, "bad.zip");
            using (var archive = ZipFile.Open(malicious, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open())) writer.Write("bad");
            reject(() => OfficialRobloxInstaller.ExtractPackage(malicious, Path.Combine(root, "extract"), new OfficialRobloxInstaller.Package { Destination = "", Unpacked = 3 }, CancellationToken.None), "ZIP traversal rejected");
            check(!File.Exists(Path.Combine(root, "escape.txt")) && File.ReadAllText(cheat) == "modified", "Installer never writes outside staging or into Cheat");
        }
        finally { Directory.Delete(root, true); }
    }
}
