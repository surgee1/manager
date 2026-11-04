using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RBX_Alt_Manager.Classes
{
    internal sealed class LaunchProfile
    {
        public string Name { get; set; }
        public string PlaceId { get; set; }
        public string Server { get; set; }
        public string Folder { get; set; }
        public string GameName { get; set; }
    }
    internal static class LaunchProfiles
    {
        internal static List<LaunchProfile> Read(string path)
        {
            if (!File.Exists(path)) return new List<LaunchProfile>();
            if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("Файл профилей слишком большой.");
            var document = JObject.Parse(File.ReadAllText(path));
            if ((int?)document["version"] != 1 || !(document["profiles"] is JArray array)) throw new InvalidDataException("Неизвестный формат профилей.");
            var profiles = array.ToObject<List<LaunchProfile>>();
            Validate(profiles);
            return profiles;
        }
        private static void Validate(List<LaunchProfile> profiles)
        {
            if (profiles == null || profiles.Count > 200) throw new InvalidDataException("Допустимо до 200 профилей.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in profiles)
                if (profile == null || string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 60 || !names.Add(profile.Name)
                    || !long.TryParse(profile.PlaceId, out long place) || place <= 0 || (profile.Server?.Length ?? 0) > 4000
                    || (profile.Folder?.Length ?? 0) > 1000 || (profile.GameName?.Length ?? 0) > 500)
                    throw new InvalidDataException("Проверь названия и Place ID профилей.");
        }
        internal static void Save(string path, List<LaunchProfile> profiles)
        {
            Validate(profiles);
            PortableBackups.AtomicWrite(path, Encoding.UTF8.GetBytes(new JObject { ["version"] = 1, ["profiles"] = JArray.FromObject(profiles) }.ToString(Formatting.Indented)));
        }
        internal static List<LaunchProfile> Upsert(IEnumerable<LaunchProfile> current, LaunchProfile profile)
        {
            var result = current.Where(item => !item.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            result.Add(profile); Validate(result); return result;
        }
    }
}
