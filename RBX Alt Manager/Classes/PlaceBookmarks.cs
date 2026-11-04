using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RBX_Alt_Manager.Classes
{
    public static class PlaceBookmarks
    {
        private static List<PlaceResult> items;
        private static string loadError;
        private static string FileName => Path.Combine(Environment.CurrentDirectory, "FavoritePlaces.json");
        private static void Load()
        {
            if (items != null) return;
            try
            {
                items = File.Exists(FileName) ? JsonConvert.DeserializeObject<List<PlaceResult>>(File.ReadAllText(FileName)) : new List<PlaceResult>();
                items = (items ?? new List<PlaceResult>()).Where(item => item != null && item.PlaceId > 0 && !string.IsNullOrWhiteSpace(item.Name)).GroupBy(item => item.PlaceId).Select(group => group.First()).ToList();
            }
            catch { items = new List<PlaceResult>(); loadError = "Не удалось прочитать FavoritePlaces.json. Файл сохранён без изменений."; }
        }
        public static List<PlaceResult> All() { Load(); return items.Select(item => new PlaceResult { PlaceId = item.PlaceId, Name = item.Name }).ToList(); }
        public static bool Contains(long id) { Load(); return items.Any(item => item.PlaceId == id); }
        public static bool Toggle(PlaceResult place, out string error)
        {
            Load();
            error = loadError;
            if (loadError != null) return false;
            var updated = All();
            if (updated.Any(item => item.PlaceId == place.PlaceId)) updated.RemoveAll(item => item.PlaceId == place.PlaceId);
            else updated.Add(new PlaceResult { PlaceId = place.PlaceId, Name = place.Name });
            try
            {
                string temp = FileName + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(updated, Formatting.Indented));
                if (File.Exists(FileName)) File.Replace(temp, FileName, FileName + ".backup");
                else File.Move(temp, FileName);
                items = updated;
                return true;
            }
            catch { error = "Не удалось сохранить избранное. Проверь доступ к папке менеджера."; return false; }
        }
    }
}
