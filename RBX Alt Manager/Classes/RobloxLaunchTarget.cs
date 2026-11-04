using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace RBX_Alt_Manager.Classes
{
    internal sealed class RobloxLaunchTarget
    {
        public long PlaceId;
        public string JobId = "", LinkCode = "", AccessCode = "", Error = "";
        public bool IsPrivate;

        internal static async Task<RobloxLaunchTarget> ResolveAsync(long place, string input, string cookie, string csrf,
            HttpMessageHandler transport = null)
        {
            var target = new RobloxLaunchTarget { PlaceId = place };
            input = (input ?? "").Trim();
            if (input.Length == 0) return target;
            Uri url;
            if (!Uri.TryCreate(input, UriKind.Absolute, out url))
            {
                Guid job;
                if (!Guid.TryParse(input, out job)) target.Error = "В поле сервера нужен Job ID или полная ссылка Roblox на приватный сервер.";
                else target.JobId = input;
                return target;
            }
            if (url.Scheme != "https" || !(url.Host.Equals("www.roblox.com", StringComparison.OrdinalIgnoreCase) || url.Host.Equals("roblox.com", StringComparison.OrdinalIgnoreCase)))
            { target.Error = "Используй полную HTTPS-ссылку на www.roblox.com."; return target; }
            var query = HttpUtility.ParseQueryString(url.Query);
            if (url.AbsolutePath.TrimEnd('/').Equals("/share", StringComparison.OrdinalIgnoreCase))
            {
                string code = query["code"];
                if (!Regex.IsMatch(code ?? "", "^[a-zA-Z0-9_-]{8,128}$") || !string.Equals(query["type"], "Server", StringComparison.OrdinalIgnoreCase))
                { target.Error = "Это не ссылка приглашения на сервер Roblox."; return target; }
                using (var client = new HttpClient(transport ?? new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) })
                    try
                    {
                        for (int attempt = 0; attempt < 2; attempt++)
                            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://apis.roblox.com/sharelinks/v1/resolve-link"))
                            {
                                request.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                                request.Headers.Add("X-CSRF-TOKEN", csrf ?? "");
                                request.Headers.Add("Origin", "https://www.roblox.com");
                                request.Headers.Referrer = new Uri("https://www.roblox.com/");
                                request.Content = new StringContent(new JObject { ["linkId"] = code, ["linkType"] = "Server" }.ToString(), Encoding.UTF8, "application/json");
                                using (var response = await client.SendAsync(request).ConfigureAwait(false))
                                {
                                    System.Collections.Generic.IEnumerable<string> fresh;
                                    if (response.StatusCode == HttpStatusCode.Forbidden && attempt == 0 && response.Headers.TryGetValues("x-csrf-token", out fresh))
                                    { csrf = System.Linq.Enumerable.FirstOrDefault(fresh); continue; }
                                    if (!response.IsSuccessStatusCode)
                                    { target.Error = "Roblox не открыл приглашение на сервер. Проверь ссылку и доступ аккаунта. [HTTP " + (int)response.StatusCode + "]"; return target; }
                                    return FromShareReply(place, JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false)));
                                }
                            }
                    }
                    catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException || e is Newtonsoft.Json.JsonException)
                    { target.Error = "Не удалось прочитать приглашение Roblox. Проверь соединение и повтори."; }
                return target;
            }
            var match = Regex.Match(url.AbsolutePath, "^/games/(\\d+)(?:/|$)");
            long linkedPlace;
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out linkedPlace) || linkedPlace != place)
            { target.Error = "Ссылка сервера относится к другой игре или имеет неверный формат. Проверь Place ID."; return target; }
            target.LinkCode = query["privateServerLinkCode"] ?? "";
            if (!Regex.IsMatch(target.LinkCode, "^[a-zA-Z0-9_-]{1,256}$"))
                target.Error = "В ссылке отсутствует код приватного сервера.";
            else target.IsPrivate = true;
            return target;
        }

        internal static RobloxLaunchTarget FromShareReply(long place, JObject reply)
        {
            var target = new RobloxLaunchTarget { PlaceId = place, IsPrivate = true };
            var data = (reply["privateServerInviteData"] ?? reply["linkData"] ?? reply["data"] ?? reply) as JObject;
            if (data == null) { target.Error = "Roblox вернул непонятный ответ на приглашение."; return target; }
            long resolved;
            if (long.TryParse((string)data["placeId"], out resolved) && resolved != place)
            { target.Error = "Приглашение ведёт в другую игру. Укажи Place ID " + resolved + "."; return target; }
            target.LinkCode = (string)data["linkCode"] ?? "";
            target.AccessCode = (string)data["accessCode"] ?? "";
            Guid access;
            if (target.AccessCode.Length == 0 && Guid.TryParse(target.LinkCode, out access))
            { target.AccessCode = target.LinkCode; target.LinkCode = ""; }
            if ((target.AccessCode.Length == 0 && target.LinkCode.Length == 0) ||
                (target.LinkCode.Length > 0 && !Regex.IsMatch(target.LinkCode, "^[a-zA-Z0-9_-]{1,256}$")) ||
                (target.AccessCode.Length > 0 && !Regex.IsMatch(target.AccessCode, "^[a-zA-Z0-9_-]{1,256}$")))
                target.Error = "Roblox не вернул код подключения к приватному серверу. Проверь приглашение на сайте.";
            return target;
        }
    }
}
