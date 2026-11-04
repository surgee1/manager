using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    // A small loopback-only protocol. It never returns account cookies.
    public sealed class BrowserBridge : IDisposable
    {
        private readonly TcpListener listener;
        private readonly string token;
        private readonly Func<string, Task<object>> import;
        private readonly Func<Task<object>> updates;
        private readonly SemaphoreSlim slots = new SemaphoreSlim(4);
        private bool stopped;
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public BrowserBridge(int port, string token, Func<string, Task<object>> import)
            : this(port, token, import, null) { }
        public BrowserBridge(int port, string token, Func<string, Task<object>> import, Func<Task<object>> updates)
        {
            if (string.IsNullOrEmpty(token) || token.Length < 32) throw new ArgumentException("Missing pairing token");
            this.token = token;
            this.import = import;
            this.updates = updates;
            listener = new TcpListener(IPAddress.Loopback, port);
        }

        public void Start() { listener.Start(); _ = Accept(); }
        private async Task Accept()
        {
            while (!stopped)
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    if (!slots.Wait(0)) { client.Close(); continue; }
                    _ = Task.Run(async () => { try { await Serve(client); } finally { slots.Release(); } });
                }
                catch (SocketException) { if (!stopped) Dispose(); }
                catch (ObjectDisposedException) { break; }
        }

        private async Task Serve(TcpClient client)
        {
            using (client)
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                var stream = client.GetStream();
                string origin = null;
                try
                {
                    var headerBytes = new List<byte>();
                    while (headerBytes.Count < 8192)
                    {
                        int b = stream.ReadByte();
                        if (b < 0) return;
                        headerBytes.Add((byte)b);
                        int n = headerBytes.Count;
                        if (n >= 4 && headerBytes[n - 4] == 13 && headerBytes[n - 3] == 10 && headerBytes[n - 2] == 13 && b == 10) break;
                    }
                    if (headerBytes.Count >= 8192) { Reply(stream, 413, new { error = "Слишком большой запрос." }, null); return; }
                    var lines = Encoding.ASCII.GetString(headerBytes.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
                    var route = lines[0].Split(' ');
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        int colon = lines[i].IndexOf(':');
                        if (colon > 0)
                        {
                            string key = lines[i].Substring(0, colon).Trim();
                            if (headers.ContainsKey(key)) { Reply(stream, 400, new { error = "Повтор заголовка." }, null); return; }
                            headers[key] = lines[i].Substring(colon + 1).Trim();
                        }
                    }
                    if (route.Length != 3 || !headers.TryGetValue("Host", out string host) || host != "127.0.0.1:" + Port)
                    { Reply(stream, 400, new { error = "Неверный адрес." }, null); return; }
                    if (headers.TryGetValue("Origin", out string incomingOrigin))
                    {
                        if (!Regex.IsMatch(incomingOrigin, "^chrome-extension://[a-p]{32}$"))
                        { Reply(stream, 403, new { error = "Запрос разрешён только расширению." }, null); return; }
                        origin = incomingOrigin;
                    }
                    if (route[0] == "OPTIONS") { Reply(stream, 204, null, origin); return; }
                    headers.TryGetValue("Authorization", out string auth);
                    if (!Same(auth ?? "", "Bearer " + token))
                    { Reply(stream, 401, new { error = "Неверный код подключения. Скопируй его из настроек менеджера." }, origin); return; }
                    if (route[0] == "GET" && route[1] == "/v1/status")
                    { Reply(stream, 200, new { ok = true, app = "Surgee Manager", version = ManagerUpdates.Version }, origin); return; }
                    if (route[0] == "GET" && route[1] == "/v1/updates" && updates != null)
                    {
                        try { Reply(stream, 200, await updates(), origin); }
                        catch (InvalidOperationException ex) { Reply(stream, 409, new { error = ex.Message }, origin); }
                        catch { Reply(stream, 502, new { error = "Не удалось проверить обновления GitHub. Повтори позже." }, origin); }
                        return;
                    }
                    if (route[0] != "POST" || route[1] != "/v1/accounts/import")
                    { Reply(stream, 404, new { error = "Неизвестная команда." }, origin); return; }
                    if (headers.ContainsKey("Transfer-Encoding") || !headers.TryGetValue("Content-Length", out string size) ||
                        !int.TryParse(size, out int length) || length < 1 || length > 8192)
                    { Reply(stream, 413, new { error = "Неверный размер запроса." }, origin); return; }
                    if (!headers.TryGetValue("Content-Type", out string contentType) || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                    { Reply(stream, 400, new { error = "Ожидается JSON." }, origin); return; }
                    var body = new byte[length];
                    int read = 0;
                    while (read < length)
                    {
                        int count = stream.Read(body, read, length - read);
                        if (count == 0) return;
                        read += count;
                    }
                    string cookie;
                    try { cookie = JObject.Parse(Encoding.UTF8.GetString(body)).Value<string>("cookie"); }
                    catch (JsonException) { Reply(stream, 400, new { error = "Неверный JSON." }, origin); return; }
                    if (string.IsNullOrWhiteSpace(cookie) || cookie.Length > 6000 || Regex.IsMatch(cookie, "[\\r\\n;]"))
                    { Reply(stream, 400, new { error = "Cookie отсутствует или имеет неверный формат." }, origin); return; }
                    var result = await import(cookie);
                    Reply(stream, 200, result, origin);
                }
                catch (InvalidOperationException e) { try { Reply(stream, 409, new { error = e.Message }, origin); } catch { } }
                catch { try { Reply(stream, 502, new { error = "Не удалось проверить аккаунт. Проверь соединение с Roblox и повтори." }, origin); } catch { } }
            }
        }

        private static bool Same(string a, string b)
        {
            int diff = a.Length ^ b.Length;
            for (int i = 0; i < b.Length; i++) diff |= (i < a.Length ? a[i] : 0) ^ b[i];
            return diff == 0;
        }
        private static void Reply(Stream stream, int status, object value, string origin)
        {
            byte[] body = value == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));
            string cors = origin == null ? "" : "Access-Control-Allow-Origin: " + origin + "\r\nVary: Origin\r\n";
            string header = "HTTP/1.1 " + status + " Result\r\nContent-Type: application/json; charset=utf-8\r\nCache-Control: no-store\r\nConnection: close\r\n" +
                cors + "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Authorization, Content-Type\r\nAccess-Control-Allow-Private-Network: true\r\nContent-Length: " + body.Length + "\r\n\r\n";
            byte[] head = Encoding.ASCII.GetBytes(header);
            stream.Write(head, 0, head.Length);
            stream.Write(body, 0, body.Length);
        }
        public void Dispose() { stopped = true; listener.Stop(); }
    }
}
