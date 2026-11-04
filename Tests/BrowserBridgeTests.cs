using Newtonsoft.Json.Linq;
using RBX_Alt_Manager.Classes;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

static class BrowserBridgeTests
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        DateTime now = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);
        var intents = new BrowserIntents(() => now);
        string first = intents.Create(42), other = intents.Create(99);
        check(first.Length == 64 && first != other && !first.Contains("42" + new string('0', 40)), "browser handoff uses independent random nonces");
        check(intents.Consume(first) == 42, "browser nonce selects exact requested user ID");
        reject(() => intents.Consume(first), "browser nonce cannot be replayed");
        now = now.AddMinutes(3);
        reject(() => intents.Consume(other), "expired browser nonce rejected");
        reject(() => intents.Consume("../bad"), "malformed browser nonce rejected");
        reject(() => intents.Create(0), "account without ID cannot request browser handoff");
        string token = new string('a', 64), origin = "chrome-extension://" + new string('a', 32);
        using var bridge = new BrowserBridge(0, token, cookie => Task.FromResult<object>(new { ok = true }), null, (route, payload) =>
        {
            if (route == "/v1/accounts/list") return Task.FromResult<object>(new { ok = true, accounts = new[] { new { id = "42", username = "SyntheticName" } } });
            long id;
            if (route == "/v1/browser/consume") id = intents.Consume((string)payload["nonce"]);
            else if ((string)payload["id"] == "42") id = 42;
            else throw new InvalidOperationException("Выбери существующий аккаунт.");
            return Task.FromResult<object>(new { ok = true, userId = id.ToString(), username = "SyntheticName", cookie = "SYNTHETIC_SESSION" });
        });
        bridge.Start(); int port = bridge.Port;
        var response = Request(port, "GET", "/v1/accounts/list", null, origin);
        check(response.StartsWith("HTTP/1.1 401") && !response.Contains("SyntheticName"), "account metadata requires pairing");
        response = Request(port, "GET", "/v1/accounts/list", token, "https://example.com");
        check(response.StartsWith("HTTP/1.1 403"), "web origins cannot enumerate accounts even with an auth header");
        response = Request(port, "GET", "/v1/accounts/list", token, origin);
        check(response.StartsWith("HTTP/1.1 200") && response.Contains("SyntheticName") && !response.Contains("SYNTHETIC_SESSION"), "paired list returns names without sessions");
        response = Request(port, "POST", "/v1/accounts/open", token, origin, "{\"id\":\"42\"}");
        check(response.StartsWith("HTTP/1.1 200") && response.Contains("SYNTHETIC_SESSION"), "explicit paired account open delivers selected session locally");
        check(Request(port, "GET", "/v1/accounts/open", token, origin).StartsWith("HTTP/1.1 404"), "session retrieval cannot be triggered by GET");
        check(Request(port, "POST", "/v1/accounts/open", null, origin, "{\"id\":\"42\"}").StartsWith("HTTP/1.1 401"), "browser switching rejects unauthenticated caller");
        check(Request(port, "POST", "/v1/accounts/open", token, "null", "{\"id\":\"42\"}").StartsWith("HTTP/1.1 403"), "opaque web origins cannot switch accounts");
        check(Request(port, "POST", "/v1/accounts/open", token, origin, "{\"id\":\"999\"}").StartsWith("HTTP/1.1 409"), "deleted or unknown account fails without session response");
        string nonce = intents.Create(42);
        response = Request(port, "GET", "/browser/open/" + nonce, null, null);
        check(response.StartsWith("HTTP/1.1 200") && response.Contains("text/html") && !response.Contains("SYNTHETIC_SESSION") && !response.Contains(token), "public browser handoff page contains no session or pairing key");
        response = Request(port, "POST", "/v1/browser/consume", token, origin, new JObject { ["nonce"] = nonce }.ToString());
        check(response.StartsWith("HTTP/1.1 200") && response.Contains("SYNTHETIC_SESSION"), "paired extension consumes a manager-issued nonce");
        response = Request(port, "POST", "/v1/browser/consume", token, origin, new JObject { ["nonce"] = nonce }.ToString());
        check(response.StartsWith("HTTP/1.1 409") && !response.Contains("SYNTHETIC_SESSION"), "protocol rejects replay without disclosing session");
        check(Request(port, "GET", "/browser/open/not-a-nonce", null, null).StartsWith("HTTP/1.1 401"), "malformed browser URL does not bypass API authorization");
    }
    private static string Request(int port, string method, string path, string token, string origin, string body = null)
    {
        using var client = new TcpClient("127.0.0.1", port);
        client.ReceiveTimeout = 5000;
        using var stream = client.GetStream();
        string headers = method + " " + path + " HTTP/1.1\r\nHost: 127.0.0.1:" + port + "\r\n";
        if (token != null) headers += "Authorization: Bearer " + token + "\r\n";
        if (origin != null) headers += "Origin: " + origin + "\r\n";
        if (body != null) headers += "Content-Type: application/json\r\nContent-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\n";
        byte[] bytes = Encoding.UTF8.GetBytes(headers + "\r\n" + body); stream.Write(bytes, 0, bytes.Length);
        var head = new StringBuilder();
        while (!head.ToString().EndsWith("\r\n\r\n"))
        { int b = stream.ReadByte(); if (b < 0) throw new IOException("Missing response headers."); head.Append((char)b); }
        int length = 0;
        foreach (string line in head.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None))
            if (line.StartsWith("Content-Length:")) length = int.Parse(line.Substring(15).Trim());
        byte[] bodyBytes = new byte[length]; int received = 0;
        while (received < length) { int count = stream.Read(bodyBytes, received, length - received); if (count == 0) throw new IOException("Truncated response."); received += count; }
        return head + Encoding.UTF8.GetString(bodyBytes);
    }
}
