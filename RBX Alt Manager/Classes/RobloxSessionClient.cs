using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    internal sealed class SessionReply
    {
        public bool Success;
        public string Ticket = "", Csrf = "", Error = "";
    }

    // Separate per-operation clients prevent cookie containers leaking between accounts.
    internal sealed class RobloxSessionClient : IDisposable
    {
        private readonly HttpClient client;
        public RobloxSessionClient() : this(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { }
        internal RobloxSessionClient(HttpMessageHandler handler)
        { client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) }; }

        internal async Task<SessionReply> RequestAsync(string cookie, string csrf, bool ticketRequired)
        {
            if (string.IsNullOrWhiteSpace(cookie) || cookie.IndexOfAny(new[] { '\r', '\n', ';' }) >= 0)
                return new SessionReply { Error = "Сессия отсутствует. Добавь аккаунт через расширение." };
            try
            {
                for (int attempt = 0; attempt < 2; attempt++)
                    using (var request = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/"))
                    {
                        request.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                        request.Headers.Add("Origin", "https://www.roblox.com");
                        request.Headers.Referrer = new Uri("https://www.roblox.com/");
                        request.Headers.Add("RBXAuthenticationNegotiation", "1");
                        if (!string.IsNullOrEmpty(csrf)) request.Headers.Add("X-CSRF-TOKEN", csrf);
                        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
                        using (var response = await client.SendAsync(request).ConfigureAwait(false))
                        {
                            // HTTP field names are case-insensitive, including HTTP/2 responses.
                            string fresh = Header(response, "x-csrf-token");
                            string ticket = Header(response, "rbx-authentication-ticket");
                            if (response.Headers.Contains("rblx-challenge-id") || response.Headers.Contains("rblx-challenge-type"))
                                return new SessionReply { Error = Explain(response) };
                            if (!string.IsNullOrEmpty(fresh)) csrf = fresh;
                            if (!ticketRequired && !string.IsNullOrEmpty(fresh))
                                return new SessionReply { Success = true, Csrf = fresh };
                            if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(ticket))
                                return new SessionReply { Success = true, Ticket = ticket, Csrf = csrf ?? "" };
                            if (response.StatusCode == HttpStatusCode.Forbidden && !string.IsNullOrEmpty(fresh) && attempt == 0)
                                continue;
                            return new SessionReply { Error = Explain(response), Csrf = csrf ?? "" };
                        }
                    }
            }
            catch (TaskCanceledException) { return new SessionReply { Error = "Roblox не ответил за 15 секунд. Проверь соединение и повтори." }; }
            catch (HttpRequestException) { return new SessionReply { Error = "Не удалось соединиться с Roblox. Проверь сеть, VPN или прокси." }; }
            return new SessionReply { Error = "Roblox повторно отклонил защитный токен. Повтори подключение." };
        }

        private static string Header(HttpResponseMessage response, string name)
        { System.Collections.Generic.IEnumerable<string> values; return response.Headers.TryGetValues(name, out values) ? values.FirstOrDefault() ?? "" : ""; }

        private static string Explain(HttpResponseMessage response)
        {
            int status = (int)response.StatusCode;
            if (response.Headers.Contains("rblx-challenge-id") || response.Headers.Contains("rblx-challenge-type"))
                return "Roblox требует дополнительную проверку входа. Пройди её на сайте Roblox и обнови аккаунт через расширение. [HTTP " + status + "]";
            if (status == 401) return "Roblox не принял сессию. Войди на сайт и обнови этот аккаунт через расширение. [HTTP 401]";
            if (status == 429) return "Roblox ограничил частоту запросов. Подожди немного перед повторным запуском. [HTTP 429]";
            if (status == 403) return "Roblox отклонил выдачу билета запуска. Аккаунт сохранён; проверь вход на сайте и доступ к игре. [HTTP 403]";
            if (status >= 500) return "Сервис авторизации Roblox временно недоступен. Попробуй позже. [HTTP " + status + "]";
            if (status == 0) return "Соединение с Roblox не установлено.";
            return "Roblox не выдал билет запуска. [HTTP " + status + "]";
        }
        public void Dispose() { client.Dispose(); }
    }
}
