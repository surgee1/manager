using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Forms
{
    public sealed class PlacePicker : ToolStripDropDown
    {
        private readonly TextBox search;
        private readonly ListBox list;
        private readonly Label status;
        private readonly Button more;
        private readonly CheckBox favoritesOnly;
        private readonly System.Windows.Forms.Timer debounce;
        private readonly List<PlaceResult> recent;
        private CancellationTokenSource request;
        private string next;
        private string currentQuery;
        public event Action<PlaceResult> PlaceSelected;

        public PlacePicker(IEnumerable<PlaceResult> recent, int width)
        {
            this.recent = recent.ToList();
            Padding = Padding.Empty;
            AutoSize = false;
            Size = new Size(Math.Max(420, width), 380);
            var panel = new Panel { Size = Size, BackColor = ThemeEditor.FormsBackground, Padding = new Padding(12) };
            var title = new Label { Text = "Найди игру по названию, ссылке или ID", Dock = DockStyle.Top, Height = 26,
                ForeColor = ThemeEditor.FormsForeground, Font = new Font("Segoe UI", 10) };
            search = new TextBox { Dock = DockStyle.Top, Font = title.Font, BackColor = ThemeEditor.TextBoxesBackground,
                ForeColor = ThemeEditor.TextBoxesForeground, BorderStyle = BorderStyle.FixedSingle, MaxLength = 160 };
            favoritesOnly = new CheckBox { Text = "Только избранное", Dock = DockStyle.Top, Height = 30,
                Font = title.Font, ForeColor = ThemeEditor.FormsForeground };
            list = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = title.Font,
                BackColor = ThemeEditor.AccountBackground, ForeColor = ThemeEditor.AccountForeground, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 48 };
            list.DrawItem += DrawResult;
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 48 };
            status = new Label { Dock = DockStyle.Fill, ForeColor = ThemeEditor.FormsForeground, Font = new Font("Segoe UI", 9), TextAlign = ContentAlignment.MiddleLeft };
            more = new NeonButton { Text = "Ещё", Dock = DockStyle.Right, Width = 80, Enabled = false };
            footer.Controls.Add(status);
            footer.Controls.Add(more);
            var spacer = new Panel { Dock = DockStyle.Top, Height = 12 };
            panel.Controls.Add(list);
            panel.Controls.Add(spacer);
            panel.Controls.Add(favoritesOnly);
            panel.Controls.Add(search);
            panel.Controls.Add(title);
            panel.Controls.Add(footer);
            var host = new ToolStripControlHost(panel) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = Size };
            Items.Add(host);
            debounce = new System.Windows.Forms.Timer { Interval = 400 };
            debounce.Tick += async (sender, e) => { debounce.Stop(); await LoadResults(false); };
            search.TextChanged += (sender, e) =>
            {
                request?.Cancel();
                debounce.Stop();
                debounce.Start();
            };
            favoritesOnly.CheckedChanged += async (sender, e) => { request?.Cancel(); debounce.Stop(); await LoadResults(false); };
            search.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Down && list.Items.Count > 0) { list.Focus(); list.SelectedIndex = 0; e.SuppressKeyPress = true; }
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; debounce.Stop(); _ = LoadResults(false); }
            };
            list.MouseClick += (sender, e) =>
            {
                int index = list.IndexFromPoint(e.Location);
                if (index < 0) return;
                if (e.X >= list.ClientSize.Width - 38)
                {
                    var place = (PlaceResult)list.Items[index];
                    if (!PlaceBookmarks.Toggle(place, out string error)) status.Text = error;
                    else
                    {
                        status.Text = PlaceBookmarks.Contains(place.PlaceId) ? "Добавлено в избранное" : "Удалено из избранного";
                        if (favoritesOnly.Checked) ShowFavorites(search.Text.Trim());
                        list.Invalidate();
                    }
                    return;
                }
                Choose();
            };
            list.KeyDown += (sender, e) => { if (e.KeyCode == Keys.Enter) { Choose(); e.SuppressKeyPress = true; } };
            more.Click += async (sender, e) => await LoadResults(true);
            Opened += (sender, e) => search.Focus();
            Closed += (sender, e) => { debounce.Stop(); request?.Cancel(); };
            ShowRecent();
        }

        private void ShowRecent()
        {
            list.BeginUpdate();
            list.Items.Clear();
            list.Items.AddRange(PlaceBookmarks.All().Concat(recent).GroupBy(item => item.PlaceId).Select(group => (object)group.First()).ToArray());
            list.EndUpdate();
            status.Text = list.Items.Count > 0 ? "Избранное и недавние • ☆ сохраняет игру" : "Введи название игры. ☆ добавляет в избранное";
            more.Enabled = false;
        }
        private void ShowFavorites(string query)
        {
            list.BeginUpdate();
            list.Items.Clear();
            list.Items.AddRange(PlaceBookmarks.All().Where(item => string.IsNullOrEmpty(query) || item.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || item.PlaceId.ToString().Contains(query)).Cast<object>().ToArray());
            list.EndUpdate();
            status.Text = list.Items.Count > 0 ? "Избранные игры" : "Пока пусто. Нажми ☆ у игры в поиске.";
            more.Enabled = false;
        }
        private async Task LoadResults(bool append)
        {
            string query = search.Text.Trim();
            if (favoritesOnly.Checked) { ShowFavorites(query); return; }
            if (query.Length == 0) { ShowRecent(); return; }
            var match = Regex.Match(query, @"(?:^|/games/)(\d+)(?:$|/|\?)");
            if (!append && match.Success && long.TryParse(match.Groups[1].Value, out long placeId) && placeId > 0)
            {
                list.Items.Clear();
                list.Items.Add(new PlaceResult { PlaceId = placeId, Name = "Выбрать ID " + placeId });
                status.Text = "Нажми на строку, чтобы подставить ID";
                more.Enabled = false;
                return;
            }
            request?.Cancel();
            request?.Dispose();
            request = new CancellationTokenSource();
            var token = request.Token;
            if (!append) { currentQuery = query; next = null; }
            more.Enabled = false;
            status.Text = "Поиск в Roblox…";
            try
            {
                var page = await PlaceSearch.SearchAsync(currentQuery, append ? next : null, token);
                if (token.IsCancellationRequested || IsDisposed || !Visible) return;
                list.BeginUpdate();
                try
                {
                    if (!append) list.Items.Clear();
                    foreach (var result in page.Items)
                        if (!list.Items.Cast<PlaceResult>().Any(old => old.PlaceId == result.PlaceId)) list.Items.Add(result);
                }
                finally { list.EndUpdate(); }
                next = page.NextPageToken;
                more.Enabled = !string.IsNullOrEmpty(next);
                status.Text = list.Items.Count == 0 ? "Ничего не найдено. Уточни название." : "Выбери игру — ID подставится автоматически";
            }
            catch (OperationCanceledException) { if (!token.IsCancellationRequested && !IsDisposed) status.Text = "Roblox долго отвечает. Повтори поиск."; }
            catch { if (!token.IsCancellationRequested && !IsDisposed) status.Text = "Поиск недоступен. Можно вставить ссылку или ID вручную."; }
        }
        private void Choose()
        {
            if (!(list.SelectedItem is PlaceResult result)) return;
            PlaceSelected?.Invoke(result);
            Close();
        }
        private void DrawResult(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var value = (PlaceResult)list.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var fill = new SolidBrush(selected ? ThemeEditor.ButtonsBackground : list.BackColor)) e.Graphics.FillRectangle(fill, e.Bounds);
            TextRenderer.DrawText(e.Graphics, value.Name, list.Font, new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + 3, e.Bounds.Width - 52, 24), list.ForeColor, TextFormatFlags.EndEllipsis);
            using (var starFont = new Font("Segoe UI Symbol", 17))
                TextRenderer.DrawText(e.Graphics, PlaceBookmarks.Contains(value.PlaceId) ? "★" : "☆", starFont,
                    new Rectangle(e.Bounds.Right - 35, e.Bounds.Top + 7, 30, 32), PlaceBookmarks.Contains(value.PlaceId) ? ThemeEditor.AccentSecondary : list.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (var small = new Font("Segoe UI", 8)) TextRenderer.DrawText(e.Graphics, "Place ID: " + value.PlaceId, small,
                new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + 27, e.Bounds.Width - 20, 18), ThemeEditor.AccentSecondary, TextFormatFlags.EndEllipsis);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { debounce.Dispose(); request?.Cancel(); request?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
