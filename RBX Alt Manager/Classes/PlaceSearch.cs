using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RBX_Alt_Manager.Classes
{
    public sealed class PlaceResult
    {
        public long PlaceId { get; set; }
        public string Name { get; set; }
        public override string ToString() => Name;
    }
    public sealed class PlaceSearchPage
    {
        public List<PlaceResult> Items { get; set; }
        public string NextPageToken { get; set; }
    }
    public static class PlaceSearch
    {
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(12) };
        private static readonly Dictionary<string, PlaceSearchPage> Cache = new Dictionary<string, PlaceSearchPage>();
        public static PlaceSearchPage Parse(string json)
        {
            var data = JObject.Parse(json);
            var items = new List<PlaceResult>();
            foreach (var group in data["searchResults"] as JArray ?? new JArray())
            {
                if (group.Value<string>("contentGroupType") != "Game") continue;
                foreach (var item in group["contents"] as JArray ?? new JArray())
                {
                    long id = item.Value<long?>("rootPlaceId") ?? 0;
                    string name = item.Value<string>("name");
                    if (id > 0 && !string.IsNullOrWhiteSpace(name)) items.Add(new PlaceResult { PlaceId = id, Name = name });
                }
            }
            return new PlaceSearchPage { Items = items.GroupBy(item => item.PlaceId).Select(group => group.First()).ToList(),
                NextPageToken = data.Value<string>("nextPageToken") };
        }
        public static async Task<PlaceSearchPage> SearchAsync(string query, string pageToken, CancellationToken cancellation)
        {
            string key = query.Trim().ToLowerInvariant() + "\n" + pageToken;
            lock (Cache) if (Cache.TryGetValue(key, out PlaceSearchPage cached)) return cached;
            string url = "https://apis.roblox.com/search-api/omni-search?searchQuery=" + Uri.EscapeDataString(query) + "&sessionId=" + Guid.NewGuid();
            if (!string.IsNullOrEmpty(pageToken)) url += "&pageToken=" + Uri.EscapeDataString(pageToken);
            using var response = await Client.GetAsync(url, cancellation);
            response.EnsureSuccessStatusCode();
            var result = Parse(await response.Content.ReadAsStringAsync());
            lock (Cache) { if (Cache.Count >= 30) Cache.Clear(); Cache[key] = result; }
            return result;
        }
    }
}
