using RBX_Alt_Manager.Classes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Forms
{
    internal sealed class CredentialImportForm : Form
    {
        private readonly Func<CredentialPair, bool, bool, IProgress<string>, CancellationToken, Task<CredentialImportResult>> import;
        private readonly List<CredentialPair> pairs = new List<CredentialPair>();
        private readonly DataGridView grid;
        private readonly ComboBox mode;
        private readonly CheckBox savePassword;
        private readonly Label status, explanation;
        private readonly TextBox single;
        private readonly NeonButton start, stop, paste, file, addSingle, clear;
        private CancellationTokenSource cancellation;
        private bool running, closeAfter;

        internal CredentialImportForm(Func<CredentialPair, bool, bool, IProgress<string>, CancellationToken, Task<CredentialImportResult>> import, bool storePasswords)
        {
            this.import = import;
            Text = "Добавить user:pass"; Font = new Font("Segoe UI Semibold", 10);
            ClientSize = new Size(760, 560); MinimumSize = new Size(700, 540); StartPosition = FormStartPosition.CenterParent;
            BackColor = ThemeEditor.FormsBackground; ForeColor = ThemeEditor.FormsForeground; MaximizeBox = false;
            var top = new Panel { Dock = DockStyle.Top, Height = 206, Padding = new Padding(18) };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 104, Padding = new Padding(18) };
            paste = Button("Вставить user:pass", new Rectangle(18, 16, 215, 36));
            file = Button("Из файла .txt…", new Rectangle(243, 16, 190, 36));
            clear = Button("Очистить список", new Rectangle(443, 16, 190, 36));
            single = new TextBox { UseSystemPasswordChar = true, Bounds = new Rectangle(20, 69, 412, 30), BackColor = ThemeEditor.TextBoxesBackground, ForeColor = ThemeEditor.TextBoxesForeground };
            addSingle = Button("Добавить строку", new Rectangle(443, 63, 190, 36));
            var methodLabel = new Label { Text = "Способ входа", Bounds = new Rectangle(18, 113, 155, 28) };
            mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Bounds = new Rectangle(180, 110, 453, 32),
                BackColor = ThemeEditor.TextBoxesBackground, ForeColor = ThemeEditor.TextBoxesForeground };
            mode.Items.AddRange(new object[] { "Chromium (Chrome / Edge)", UiText.Translate("Основной браузер Windows (расширение 0.5.0+)") }); mode.SelectedIndex = 0;
            explanation = new Label { Bounds = new Rectangle(18, 151, 708, 50), Font = new Font("Segoe UI", 9) };
            top.Controls.AddRange(new Control[] { paste, file, clear, single, addSingle, methodLabel, mode, explanation });
            grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = ThemeEditor.AccountBackground, BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowTemplate = { Height = 32 } };
            grid.DefaultCellStyle.BackColor = ThemeEditor.AccountBackground; grid.DefaultCellStyle.ForeColor = ThemeEditor.AccountForeground;
            grid.DefaultCellStyle.SelectionBackColor = ThemeEditor.ButtonsBackground; grid.DefaultCellStyle.SelectionForeColor = ThemeEditor.FormsForeground;
            grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeEditor.TextBoxesBackground; grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeEditor.TextBoxesForeground;
            grid.Columns.Add("username", UiText.Translate("Логин")); grid.Columns[0].FillWeight = 28;
            grid.Columns.Add("password", UiText.Translate("Пароль")); grid.Columns[1].FillWeight = 18;
            grid.Columns.Add("state", UiText.Translate("Состояние")); grid.Columns[2].FillWeight = 54;
            savePassword = new CheckBox { Text = "Сохранять введённые пароли в менеджере", Checked = storePasswords, Bounds = new Rectangle(18, 9, 480, 28) };
            status = new Label { Text = "Одна пара username:password на строку. Пароли в списке скрыты.", Bounds = new Rectangle(18, 40, 480, 44), Font = new Font("Segoe UI", 9) };
            start = Button("Начать импорт", new Rectangle(510, 44, 218, 38)); start.Accent = true;
            stop = Button("Отменить", new Rectangle(510, 0, 218, 36)); stop.Enabled = false;
            bottom.Resize += (sender, e) =>
            {
                start.Left = stop.Left = bottom.ClientSize.Width - 236;
                status.Width = savePassword.Width = Math.Max(180, start.Left - 36);
            };
            bottom.Controls.AddRange(new Control[] { savePassword, status, stop, start });
            Controls.Add(grid); Controls.Add(bottom); Controls.Add(top);
            paste.Click += (sender, e) => { if (Clipboard.ContainsText()) AddText(Clipboard.GetText()); };
            file.Click += (sender, e) =>
            {
                using (var dialog = new OpenFileDialog { Title = UiText.Translate("Выбери файл user:pass"), Filter = "Text (*.txt)|*.txt|All files (*.*)|*.*" })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    try { if (new FileInfo(dialog.FileName).Length > 400000) throw new InvalidDataException("Слишком большой список user:pass."); AddText(File.ReadAllText(dialog.FileName)); }
                    catch { status.Text = "Не удалось прочитать файл user:pass."; }
            };
            addSingle.Click += (sender, e) => { AddText(single.Text); single.Clear(); };
            clear.Click += (sender, e) => { foreach (var pair in pairs) pair.Dispose(); pairs.Clear(); grid.Rows.Clear(); status.Text = "Список очищен."; };
            mode.SelectedIndexChanged += (sender, e) => Explain(); Explain();
            start.Click += async (sender, e) => await ImportAll();
            stop.Click += (sender, e) => { stop.Enabled = false; cancellation?.Cancel(); status.Text = "Останавливаем импорт…"; };
            FormClosing += (sender, e) => { if (running) { closeAfter = true; e.Cancel = true; cancellation?.Cancel(); } };
            FormClosed += (sender, e) => { foreach (var pair in pairs) pair.Dispose(); single.Clear(); };
            RoundedWindow.Attach(this); UiText.Attach(this);
        }
        private NeonButton Button(string text, Rectangle bounds) => new NeonButton { Text = text, Bounds = bounds, Font = Font };
        private void Explain()
        {
            explanation.Text = mode.SelectedIndex == 0
                ? "Отдельная сессия для каждого аккаунта. В основном браузере вход не меняется. CAPTCHA и подтверждение — вручную."
                : "Нужно расширение Spectrum 0.5.0+. Вход в обычном браузере временно переключается и восстанавливается после импорта.";
        }
        internal void AddText(string text)
        {
            List<CredentialPair> parsed = null;
            try
            {
                parsed = CredentialPairs.Parse(text);
                if (pairs.Count + parsed.Count > 100 || parsed.Any(pair => pairs.Any(old => string.Equals(old.Username, pair.Username, StringComparison.OrdinalIgnoreCase))))
                    throw new InvalidDataException("В списке есть повторяющиеся логины или больше 100 аккаунтов.");
                foreach (var pair in parsed) { pairs.Add(pair); grid.Rows.Add(pair.Username, "••••••••", UiText.Translate("Ожидает входа")); }
                parsed = null; status.Text = "В списке аккаунтов: " + pairs.Count;
            }
            catch (InvalidDataException error) { status.Text = error.Message; }
            finally { if (parsed != null) foreach (var pair in parsed) pair.Dispose(); }
        }
        private async Task ImportAll()
        {
            if (running || !pairs.Any(pair => pair.Password != null)) return;
            running = true; cancellation = new CancellationTokenSource(); bool chromium = mode.SelectedIndex == 0, store = savePassword.Checked;
            start.Enabled = paste.Enabled = file.Enabled = clear.Enabled = addSingle.Enabled = mode.Enabled = savePassword.Enabled = single.Enabled = false; stop.Enabled = true;
            int added = 0;
            try
            {
                for (int index = 0; index < pairs.Count; index++)
                {
                    cancellation.Token.ThrowIfCancellationRequested(); var pair = pairs[index]; if (pair.Password == null) continue;
                    int row = index;
                    var progress = new Progress<string>(message => { if (!IsDisposed) { status.Text = message; grid.Rows[row].Cells[2].Value = UiText.Translate(message); } });
                    try
                    {
                        var result = await import(pair, chromium, store, progress, cancellation.Token);
                        grid.Rows[index].Cells[2].Value = UiText.Translate(result.Warning ?? "Добавлен"); added++;
                    }
                    catch (OperationCanceledException) { grid.Rows[index].Cells[2].Value = UiText.Translate("Отменено"); throw; }
                    catch (Exception error) { grid.Rows[index].Cells[2].Value = UiText.Translate(error is InvalidOperationException ? error.Message : "Вход не завершён. Попробуй снова."); }
                    finally { pair.Dispose(); }
                }
                status.Text = "Импорт завершён. Добавлено: " + added;
            }
            catch (OperationCanceledException) { status.Text = "Импорт остановлен. Уже добавленные аккаунты сохранены."; }
            finally
            {
                running = false; cancellation.Dispose(); cancellation = null;
                start.Enabled = paste.Enabled = file.Enabled = clear.Enabled = addSingle.Enabled = mode.Enabled = savePassword.Enabled = single.Enabled = true; stop.Enabled = false;
                if (closeAfter) Close();
            }
        }
    }
}
