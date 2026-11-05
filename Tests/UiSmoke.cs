using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

class UiSmoke
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static object Field(object value, string name) { return value.GetType().GetField(name, Flags).GetValue(value); }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); File.AppendAllText("ui-checks.txt", "PASS " + label + Environment.NewLine); }
    static void Capture(Form form, string path) { using (var b = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(b, new Rectangle(Point.Empty, form.Size)); b.Save(path); } }
    static Form FindLaunch(Control parent) { foreach (Control child in parent.Controls) { if (child.GetType().Name == "ArgumentsForm") return (Form)child; var nested = FindLaunch(child); if (nested != null) return nested; } return null; }
    static void MissingEnglish(Control root, System.Collections.Generic.HashSet<string> missing)
    {
        if ((root is Label || root is ButtonBase || root is TabPage || root is Form) && root.Text != "Проверка выбора игры" && System.Text.RegularExpressions.Regex.IsMatch(root.Text, "[А-Яа-яЁё]")) missing.Add(root.Text);
        foreach (Control child in root.Controls) MissingEnglish(child, missing);
    }
    static string BackgroundFrame(Assembly assembly)
    {
        using (var bitmap = new Bitmap(800, 480))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var stream = new MemoryStream())
        {
            assembly.GetType("RBX_Alt_Manager.Classes.VisualBackground").GetMethod("Draw", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { graphics, bitmap.Size });
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToBase64String(stream.ToArray());
        }
    }
    static void Pump(int milliseconds)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
    }
    static Bitmap PortraitFrame(Assembly assembly, string profile, double time)
    {
        var bitmap = new Bitmap(800, 480);
        using (var graphics = Graphics.FromImage(bitmap))
            assembly.GetType("RBX_Alt_Manager.Classes.VisualBackground").GetMethod("Portrait", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { graphics, bitmap.Size, time, profile });
        return bitmap;
    }
    [STAThread]
    static void Main()
    {
        try
        {
            Application.EnableVisualStyles();
            var assembly = Assembly.LoadFrom("Roblox Account Manager.exe");
            var type = assembly.GetType("RBX_Alt_Manager.AccountManager");
            var main = (Form)Activator.CreateInstance(type);
            main.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), main, type.GetMethod("AccountManager_Load", Flags));
            main.Shown -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), main, type.GetMethod("AccountManager_Shown", Flags));
            main.Show();
            Check(type.GetField("legitModeButton", Flags) == null && type.GetField("cheatModeButton", Flags) == null, "Global client switch removed");
            Check(main.Text == "Spectrum Manager" && ((Control)Field(main, "modernTitle")).Text == "Spectrum Manager", "Spectrum Manager branding appears in window title and main heading");
            // Startup loading is detached in this isolated test; dismiss its designer overlay.
            ((Control)Field(main, "PasswordPanel")).Visible = false;
            type.GetMethod("RestoreLaunchInputs", Flags).Invoke(main, null);
            Application.DoEvents();
            const string savedPrivate = "https://www.roblox.com/share?code=SYNTHETIC_PRIVATE_LINK&type=Server";
            ((TextBox)Field(main, "PlaceID")).Text = "123456789";
            ((TextBox)Field(main, "JobID")).Text = savedPrivate;
            Pump(600);
            string preferences = File.ReadAllText("RAMSettings.ini");
            Check(preferences.Contains("SavedPlaceId=123456789") && preferences.Contains("SavedJobId=" + savedPrivate), "place and full private URL autosave without clicking Save");
            ((TextBox)Field(main, "PlaceID")).Text = "";
            ((TextBox)Field(main, "JobID")).Text = "";
            Pump(600);
            preferences = File.ReadAllText("RAMSettings.ini");
            Check(!preferences.Contains("SavedPlaceId=") && !preferences.Contains("SavedJobId=") && preferences.Contains("SavedLaunchInputs=true"), "cleared launch fields are saved as empty");
            Check(!((Control)Field(main, "ArgumentsB")).Visible, "one settings button on main window");
            Check(!(bool)assembly.GetType("RBX_Alt_Manager.Program").GetField("PreviewMode", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), "production button handlers enabled");
            ((Button)Field(main, "advancedButton")).PerformClick();
            Application.DoEvents();
            var advanced = (Form)Field(main, "advancedWindow");
            Check(advanced.Visible, "advanced button opens window");
            Check(((Control)Field(main, "UserID")).FindForm() == advanced, "follow by username moved to advanced window");
            Capture(advanced, "advanced-panel.png");
            advanced.Hide();
            ((Button)Field(main, "ConfigButton")).PerformClick();
            Application.DoEvents();
            var settings = (Form)Field(main, "SettingsForm");
            Check(settings.Visible, "settings button opens settings");
            var tabs = (TabControl)Field(settings, "SettingsTC");
            TabPage launchPage = null;
            foreach (TabPage page in tabs.TabPages) if (page.Text == "Запуск") launchPage = page;
            Check(launchPage != null, "launch options included in unified settings");
            tabs.SelectedTab = launchPage;
            Application.DoEvents();
            Capture(settings, "client-launch-settings.png");
            var launch = FindLaunch(launchPage);
            Check(launch == null, "Legacy teleport, version and join controls removed from settings");
            Capture(settings, "settings-panel.png");
            settings.GetType().GetMethod("ApplyColorPreset", Flags).Invoke(settings, new object[] { "#397FFF", "#23D6AF", "#0B1720", "#152936" });
            var theme = assembly.GetType("RBX_Alt_Manager.Forms.ThemeEditor");
            Check(((Color)theme.GetField("AccentPrimary").GetValue(null)).ToArgb() == ColorTranslator.FromHtml("#397FFF").ToArgb(), "custom palette applied to live interface");
            Check(File.ReadAllText("RAMTheme.ini").Contains("AccentPrimary=#397FFF"), "custom palette persists to theme file");
            var profileMethod = settings.GetType().GetMethod("ApplyVisualProfile", Flags);
            foreach (string profile in new[] { "nyan", "kaneki", "naruto", "gojo", "kaneki_manga", "uruma", "happyend", "gangsta", "subaru", "alex", "kurapika", "zerotwo", "akame", "deathnote", "kagami", "ichigo", "baki", "nogamenolife" })
            {
                profileMethod.Invoke(settings, new object[] { profile });
                Check((string)theme.GetField("VisualProfile").GetValue(null) == profile, "profile switches: " + profile);
                Capture(main, "profile-" + profile + ".png");
            }
            Check(File.Exists("Themes/kaneki_manga.png"), "illustrated theme assets are bundled");
            var supported = (string[])theme.GetField("SupportedProfiles", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(supported.Length == 18 && Array.IndexOf(supported, "matrix") < 0 && Array.IndexOf(supported, "manga") < 0 && Array.IndexOf(supported, "sakura") < 0, "removed theme presets are absent from supported catalog");
            profileMethod.Invoke(settings, new object[] { "manga" });
            Check((string)theme.GetField("VisualProfile").GetValue(null) == "kaneki_manga", "old removed theme resolves to a supported character profile");
            TabPage portraitColors = null;
            foreach (TabPage page in tabs.TabPages) if (page.Text == "Цвета") portraitColors = page;
            tabs.SelectedTab = portraitColors;
            main.Location = new Point(Screen.PrimaryScreen.WorkingArea.Left, Screen.PrimaryScreen.WorkingArea.Top);
            settings.Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - settings.Width, Screen.PrimaryScreen.WorkingArea.Top + 40);
            main.BringToFront();
            main.Activate();
            settings.Activate();
            theme.GetField("AnimationsEnabled").SetValue(null, true);
            theme.GetField("CharacterMotion").SetValue(null, 75);
            var portraitHashes = new System.Collections.Generic.HashSet<string>();
            var general = type.GetField("General").GetValue(null);
            general.GetType().GetMethod("Set").Invoke(general, new object[] { "AnimationEconomy", "false", null });
            foreach (string profile in new[] { "kaneki", "naruto", "gojo", "kaneki_manga", "uruma", "happyend", "gangsta", "subaru", "alex", "kurapika", "zerotwo", "akame", "deathnote", "kagami", "ichigo", "baki", "nogamenolife" })
            {
                profileMethod.Invoke(settings, new object[] { profile });
                using (var first = PortraitFrame(assembly, profile, .4))
                using (var second = PortraitFrame(assembly, profile, 2.6))
                using (var stream = new MemoryStream())
                {
                    int populated = 0, changed = 0, leftChanged = 0;
                    for (int y = 0; y < first.Height; y += 2)
                        for (int x = 0; x < first.Width; x += 2)
                        {
                            if (first.GetPixel(x, y).A > 0) populated++;
                            if (first.GetPixel(x, y) != second.GetPixel(x, y)) { changed++; if (x < first.Width / 2) leftChanged++; }
                        }
                    Check(populated > (profile == "ichigo" ? 40000 : 80000), "portrait asset loads: " + profile);
                    Check(changed > 1000 && leftChanged == 0, "character itself moves with anchored background: " + profile);
                    first.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    Check(portraitHashes.Add(Convert.ToBase64String(stream.ToArray())), "distinct portrait artwork: " + profile);
                    first.Save("portrait-" + profile + ".png");
                }
                int mainPaints = 0, settingsPaints = 0;
                PaintEventHandler mainPaint = (sender, args) => mainPaints++;
                PaintEventHandler panelPaint = (sender, args) => settingsPaints++;
                main.Paint += mainPaint;
                int invalidations = 0;
                InvalidateEventHandler invalidation = (sender, args) => invalidations++;
                main.Invalidated += invalidation;
                var panel = portraitColors.Controls[0];
                panel.Paint += panelPaint;
                Pump(450);
                main.Invalidated -= invalidation;
                main.Paint -= mainPaint;
                panel.Paint -= panelPaint;
                Check(invalidations >= 3 && settingsPaints >= 3, "main redraw timer continues alongside settings animation: " + profile);
                Capture(settings, "settings-profile-" + profile + ".png");
            }
            profileMethod.Invoke(settings, new object[] { "kaneki_manga" });
            theme.GetField("AnimationsEnabled").SetValue(null, true);
            string moving = BackgroundFrame(assembly);
            System.Threading.Thread.Sleep(150);
            Check(moving != BackgroundFrame(assembly), "portrait breathing and particles animate");
            theme.GetField("AnimationsEnabled").SetValue(null, false);
            string frozen = BackgroundFrame(assembly);
            System.Threading.Thread.Sleep(150);
            Check(frozen == BackgroundFrame(assembly), "animation switch freezes theme background");
            theme.GetField("AnimationsEnabled").SetValue(null, true);
            profileMethod.Invoke(settings, new object[] { "futuristic" });
            Capture(settings, "settings-panel.png");
            TabPage colors = null;
            foreach (TabPage page in tabs.TabPages) if (page.Text == "Цвета") colors = page;
            Check(colors != null, "color customization page exists");
            tabs.SelectedTab = colors;
            Application.DoEvents();
            Capture(settings, "colors-panel.png");
            settings.Hide();
            var accountType = assembly.GetType("RBX_Alt_Manager.Account");
            var account = Activator.CreateInstance(accountType);
            accountType.GetField("Username").SetValue(account, "UiTestAccount");
            accountType.GetField("UserID").SetValue(account, 123L);
            var list = (IList)type.GetField("AccountsList").GetValue(null);
            list.Add(account);
            var view = Field(main, "AccountsView");
            view.GetType().GetMethod("SetObjects", new[] { typeof(IEnumerable) }).Invoke(view, new object[] { list });
            view.GetType().GetProperty("SelectedObject").SetValue(view, account, null);
            Application.DoEvents();
            var prefixColumn = Field(main, "prefixColumn");
            Check(((ColumnHeader)prefixColumn).Text == "Prefix", "Prefix column visible in accounts header");
            Check(((ListView)view).OwnerDraw && ((ColumnHeader)prefixColumn).DisplayIndex == 2, "Prefix dropdown cells render between alias and notes");
            var item = view.GetType().GetMethod("ModelToItem").Invoke(view, new object[] { account });
            int prefixIndex = ((ListView)view).Columns.IndexOf((ColumnHeader)prefixColumn);
            view.GetType().GetMethod("EditSubItem").Invoke(view, new object[] { item, prefixIndex });
            Application.DoEvents();
            ComboBox prefixEditor = null;
            foreach (Control child in ((Control)view).Controls) if (child is ComboBox) prefixEditor = (ComboBox)child;
            Check(prefixEditor != null && prefixEditor.DropDownStyle == ComboBoxStyle.DropDownList && prefixEditor.Items.Count == 3, "Prefix cell opens three-choice dropdown");
            prefixEditor.SelectedIndex = 1;
            view.GetType().GetMethod("FinishCellEdit", Type.EmptyTypes).Invoke(view, null);
            Check((string)accountType.GetMethod("GetField").Invoke(account, new object[] { "ClientPrefix" }) == "Legit", "Prefix dropdown commits Legit to selected account");
            type.GetMethod("SetAccountPrefix", Flags).Invoke(main, new object[] { account, "Cheat" });
            Check((string)accountType.GetMethod("GetField").Invoke(account, new object[] { "ClientPrefix" }) == "Cheat", "Cheat prefix replaces previous selection");
            type.GetMethod("SetAccountPrefix", Flags).Invoke(main, new object[] { account, "" });
            Check((string)accountType.GetMethod("GetField").Invoke(account, new object[] { "ClientPrefix" }) == "", "Clearing prefix restores default Windows route");
            advanced.Show(main);
            ((TabControl)advanced.Controls[0]).SelectedIndex = 1;
            Application.DoEvents();
            ((TextBox)Field(main, "Alias")).Text = "Проверка кнопки";
            ((Button)Field(main, "SetAlias")).PerformClick();
            Check((string)accountType.GetProperty("Alias").GetValue(account, null) == "Проверка кнопки", "save alias button executes handler");
            ((RichTextBox)Field(main, "DescriptionBox")).Text = "Тест заметки";
            ((Button)Field(main, "SetDescription")).PerformClick();
            Check((string)accountType.GetProperty("Description").GetValue(account, null) == "Тест заметки", "save notes button executes handler");
            var stored = type.GetMethod("StoreBrowserSession", Flags).Invoke(main, new object[] { "SYNTHETIC_TEST_SESSION", 123L, "ValidatedNickname", "Display nickname" });
            Check(list.Count == 1, "browser import updates existing user instead of duplicating");
            Check((string)accountType.GetProperty("Alias").GetValue(account, null) == "Проверка кнопки", "browser import preserves alias");
            Check((string)accountType.GetProperty("Description").GetValue(account, null) == "Тест заметки", "browser import preserves notes");
            Check((string)stored.GetType().GetProperty("username").GetValue(stored, null) == "ValidatedNickname", "browser response contains verified nickname");
            var info = (System.Diagnostics.ProcessStartInfo)assembly.GetType("RBX_Alt_Manager.Classes.SystemBrowser").GetMethod("CreateStartInfo").Invoke(null, new object[] { "https://www.roblox.com/login" });
            Check(info.UseShellExecute && info.FileName == "https://www.roblox.com/login", "login opens Windows default browser");
            Check(!((Control)Field(main, "JoinDiscord")).Visible, "Discord button removed");
            var tooltip = (ToolTip)Field(main, "SaveTooltip");
            Check(tooltip.GetToolTip((Control)Field(main, "DonateButton")) == "бог этого мира surgee", "author tooltip uses requested text");
            type.GetMethod("CreateFolderCore", Flags).Invoke(main, new object[] { "UI Folder A" });
            Check(File.ReadAllText("AccountFolders.json").Contains("UI Folder A"), "empty account folder persists");
            var folders = (ComboBox)Field(main, "folderSelector");
            folders.SelectedIndex = 0;
            view.GetType().GetProperty("SelectedObject").SetValue(view, account, null);
            type.GetMethod("MoveSelectedToFolder", Flags).Invoke(main, new object[] { "UI Folder A" });
            Check((string)accountType.GetProperty("Group").GetValue(account, null) == "UI Folder A", "selected account moves to folder");
            type.GetMethod("RenameFolderCore", Flags).Invoke(main, new object[] { "UI Folder A", "UI Folder B" });
            Check((string)accountType.GetProperty("Group").GetValue(account, null) == "UI Folder B", "renaming folder updates its accounts");
            type.GetMethod("DeleteFolderCore", Flags).Invoke(main, new object[] { "UI Folder B" });
            Check(list.Count == 1 && (string)accountType.GetProperty("Group").GetValue(account, null) == "Default", "deleting folder preserves its accounts");
            Check(type.GetMethod("ShowRoflMode", Flags) == null, "ROFL mode removed");
            Check(tabs.Font.FontFamily.Name.Contains("Segoe UI"), "settings tabs use readable Segoe font");
            type.GetMethod("ShowPlacePicker", Flags).Invoke(main, null);
            var picker = (ToolStripDropDown)Field(main, "placePicker");
            Check(picker.Visible, "Place ID click opens game dropdown");
            var resultType = assembly.GetType("RBX_Alt_Manager.Classes.PlaceResult");
            var result = Activator.CreateInstance(resultType);
            resultType.GetProperty("PlaceId").SetValue(result, 987654321L, null);
            resultType.GetProperty("Name").SetValue(result, "Проверка выбора игры", null);
            Check(!((Control)Field(main, "HistoryIcon")).Visible, "history clock removed from main window");
            var bookmarks = assembly.GetType("RBX_Alt_Manager.Classes.PlaceBookmarks");
            if ((bool)bookmarks.GetMethod("Contains").Invoke(null, new object[] { 987654321L })) bookmarks.GetMethod("Toggle").Invoke(null, new object[] { result, null });
            Check((bool)bookmarks.GetMethod("Toggle").Invoke(null, new object[] { result, null }), "game can be added to favorites");
            bookmarks.GetField("items", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            Check((bool)bookmarks.GetMethod("Contains").Invoke(null, new object[] { 987654321L }), "favorite survives file reload");
            Check((bool)bookmarks.GetMethod("Toggle").Invoke(null, new object[] { result, null }), "game can be removed from favorites");
            var results = (ListBox)Field(picker, "list");
            results.Items.Add(result);
            results.SelectedItem = result;
            picker.GetType().GetMethod("Choose", Flags).Invoke(picker, null);
            Check(((TextBox)Field(main, "PlaceID")).Text == "987654321", "choosing game fills exact Place ID");
            Check(((Label)Field(main, "CurrentPlace")).Text == "Проверка выбора игры", "choosing game updates its name");
            advanced.Hide();
            var search = (TextBox)Field(main, "accountSearch");
            search.Text = "NoSuchSyntheticAccount"; Pump(200);
            Check(((ListView)view).Items.Count == 0, "account search filters unmatched rows");
            search.Text = "Проверка кнопки"; Pump(200);
            Check(((ListView)view).Items.Count == 1, "account search finds alias in current folder");
            search.Clear(); Pump(200);
            Check(((ListView)view).Items.Count == 1, "clearing search restores visible accounts");
            ((TextBox)Field(main, "PlaceID")).Text = "987654321";
            ((TextBox)Field(main, "JobID")).Text = savedPrivate;
            type.GetMethod("SaveLaunchProfileCore", Flags).Invoke(main, new object[] { "UI smoke profile" });
            Check(File.ReadAllText("LaunchProfiles.json").Contains("UI smoke profile"), "saving launch profile writes its named preset");
            ((TextBox)Field(main, "PlaceID")).Text = "111";
            ((TextBox)Field(main, "JobID")).Text = "";
            object savedProfile = null;
            foreach (object profile in (IEnumerable)Field(main, "launchProfiles"))
                if ((string)profile.GetType().GetProperty("Name").GetValue(profile, null) == "UI smoke profile") savedProfile = profile;
            type.GetMethod("ApplyLaunchProfile", Flags).Invoke(main, new object[] { savedProfile });
            Check(((TextBox)Field(main, "PlaceID")).Text == "987654321" && ((TextBox)Field(main, "JobID")).Text == savedPrivate, "applying launch profile restores exact place and private URL");
            Check(((ListView)view).SelectedItems.Count == 0, "applying profile clears stale account selection");
            var commands = new System.Collections.Generic.List<Tuple<string, Action>>();
            int executions = 0;
            commands.Add(Tuple.Create("Тестовая команда", new Action(delegate { executions++; })));
            commands.Add(Tuple.Create("Другое действие", new Action(delegate { })));
            var commandType = assembly.GetType("RBX_Alt_Manager.Forms.QuickCommands");
            var commandForm = (Form)Activator.CreateInstance(commandType, Flags, null, new object[] { commands }, null);
            commandForm.Show(main);
            ((TextBox)Field(commandForm, "search")).Text = "Тестовая";
            Check(((ListBox)Field(commandForm, "list")).Items.Count == 1, "command palette filters actions by text");
            commandType.GetMethod("Choose", Flags).Invoke(commandForm, null); Pump(100);
            Check(executions == 1 && !commandForm.Visible, "command palette executes selected action once and closes");
            string emptyFolderName = "Portable folder " + Guid.NewGuid().ToString("N").Substring(0, 6);
            type.GetMethod("CreateFolderCore", Flags).Invoke(main, new object[] { emptyFolderName });
            var snapshot = type.GetMethod("CapturePortableBackup", Flags).Invoke(main, null);
            var backups = assembly.GetType("RBX_Alt_Manager.Classes.PortableBackups");
            const BindingFlags StaticInternal = BindingFlags.Static | BindingFlags.NonPublic;
            backups.GetMethod("Write", StaticInternal).Invoke(null, new object[] { "ui-portable.surgee-backup.json", snapshot });
            var portable = backups.GetMethod("Read", StaticInternal).Invoke(null, new object[] { "ui-portable.surgee-backup.json" });
            int restoredCount = (int)type.GetMethod("RestorePortableBackup", Flags).Invoke(main, new object[] { portable });
            Check(restoredCount == 0 && ((IList)type.GetField("AccountsList").GetValue(null)).Count == 1, "UI restore merges duplicate account without losing its data");
            Check(Directory.GetFiles("Backups", "before-restore-*.surgee-backup.json").Length > 0, "UI restore creates independent before-restore copy");
            Pump(2500);
            Check(Directory.GetFiles("Backups", "auto-*.surgee-backup.json").Length > 0, "automatic backup timer creates portable copy after account change");
            Check(File.ReadAllText("AccountFolders.json").Contains(emptyFolderName), "restoring keeps empty account folders");
            var currentList = (IList)type.GetField("AccountsList").GetValue(null);
            var removal = Array.CreateInstance(accountType, 1); removal.SetValue(currentList[0], 0);
            type.GetMethod("RemoveAccountsCore", Flags).Invoke(main, new object[] { removal });
            Check(((IList)type.GetField("AccountsList").GetValue(null)).Count == 0 && File.ReadAllBytes("AccountData.json").Length > 0, "removing last account saves an explicit empty database");
            var emptyStored = type.GetMethod("ReadStoredAccounts", BindingFlags.Static | BindingFlags.NonPublic);
            Check(((IList)emptyStored.Invoke(null, new object[] { "[]" })).Count == 0, "empty stored account array loads without setter side effects");
            type.GetMethod("UndoLastRemoval", Flags).Invoke(main, null);
            Check(((IList)type.GetField("AccountsList").GetValue(null)).Count == 1, "undo returns last deleted account after saving empty database");
            Check(Directory.GetFiles("Backups", "before-delete-*.surgee-backup.json").Length > 0, "deletion creates portable recovery copy before mutation");
            byte[] protectedBefore = File.ReadAllBytes("AccountData.json");
            type.GetField("accountFileUnreadable", Flags).SetValue(main, true);
            bool blockedSave = false;
            try { type.GetMethod("SaveAccounts").Invoke(null, new object[] { true, true }); } catch (TargetInvocationException) { blockedSave = true; }
            Check(blockedSave && Convert.ToBase64String(protectedBefore) == Convert.ToBase64String(File.ReadAllBytes("AccountData.json")), "unreadable database guard prevents overwriting the original account file");
            type.GetMethod("RestorePortableBackup", Flags).Invoke(main, new object[] { portable });
            Check(!(bool)Field(main, "accountFileUnreadable"), "portable restore recovers database when Windows-bound original is unreadable");
            Check(Directory.GetFiles(".", "AccountData.json.before-portable-restore-*.bak").Length > 0, "restore preserves raw unreadable original in a separate file");
            ((ComboBox)Field(main, "folderSelector")).SelectedIndex = 0;
            Pump(100);
            Check(((ListView)view).Items.Count == 1, "restored objects appear immediately in account list");
            main.Show(); main.Activate(); Pump(50);
            var cadence = assembly.GetType("RBX_Alt_Manager.Classes.AnimationCadence");
            Check((int)cadence.GetProperty("Interval", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null) == 33, "active manager retains smooth animation cadence");
            general.GetType().GetMethod("Set").Invoke(general, new object[] { "AnimationEconomy", "true", null });
            main.Hide(); Pump(80);
            Check((int)cadence.GetProperty("Interval", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null) == 200, "inactive manager reduces animation cadence to five frames per second");
            main.Show(); main.Activate();
            var jsonType = Type.GetType("Newtonsoft.Json.Linq.JObject, Newtonsoft.Json");
            var metadataTask = (System.Threading.Tasks.Task<object>)type.GetMethod("HandleBrowserActionAsync", Flags).Invoke(main, new object[] { "/v1/accounts/list", null });
            Pump(100);
            var metadata = metadataTask.GetAwaiter().GetResult();
            Check(metadata.GetType().GetProperty("accounts") != null && metadata.GetType().GetProperty("cookie") == null, "extension list callback exposes metadata without account session");
            object openPayload = jsonType.GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new object[] { "{\"id\":\"123\"}" });
            var openTask = (System.Threading.Tasks.Task<object>)type.GetMethod("HandleBrowserActionAsync", Flags).Invoke(main, new object[] { "/v1/accounts/open", openPayload });
            Pump(100); object openedAccount = openTask.GetAwaiter().GetResult();
            Check((string)openedAccount.GetType().GetProperty("userId").GetValue(openedAccount, null) == "123" && (string)openedAccount.GetType().GetProperty("cookie").GetValue(openedAccount, null) == "SYNTHETIC_TEST_SESSION", "extension open callback resolves the exact selected account session");
            string fixtureImport = "ui-original-import.json";
            File.WriteAllText(fixtureImport, "[{\"UserID\":456,\"Username\":\"ImportedUiAccount\",\"SecurityToken\":\"SYNTHETIC_IMPORTED_SESSION\",\"Alias\":\"Из менеджера\",\"Description\":\"Импорт работает\",\"Group\":\"Imported folder\"}]");
            var importer = assembly.GetType("RBX_Alt_Manager.Classes.AccountImports");
            var entropy = (byte[])type.GetField("Entropy", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var importBatch = importer.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { fixtureImport, entropy, null });
            object[] importArgs = { importBatch, false, 0, 0 };
            type.GetMethod("ImportAccountsCore", Flags).Invoke(main, importArgs);
            Check((int)importArgs[2] == 1 && ((IList)type.GetField("AccountsList").GetValue(null)).Count == 2, "GUI import core adds accounts from another manager and persists them");
            Check(Directory.GetFiles("Backups", "before-import-*.surgee-backup.json").Length > 0, "GUI import creates portable copy before merging account data");
            Check(((Button)Field(main, "importAccountsButton")).Text == "Импорт", "Import button is available in main window");
            var importedList = (IList)type.GetField("AccountsList").GetValue(null);
            type.GetMethod("ImportAccountsCore", Flags).Invoke(main, new object[] { importBatch, false, 0, 0 });
            Check(((IList)type.GetField("AccountsList").GetValue(null)).Count == 2, "repeated GUI import does not duplicate persisted accounts");
            type.GetMethod("InvalidateModernFrame", Flags).Invoke(main, null);
            using (var scene = new Bitmap(main.Width, main.Height))
            {
                main.DrawToBitmap(scene, new Rectangle(Point.Empty, scene.Size));
                long renders = (long)Field(main, "modernSceneRenderCount");
                for (int i = 0; i < 5; i++) main.DrawToBitmap(scene, new Rectangle(Point.Empty, scene.Size));
                Check((long)Field(main, "modernSceneRenderCount") == renders, "transparent children reuse one scene instead of repeatedly repainting character");
                type.GetMethod("OnResizeBegin", Flags).Invoke(main, new object[] { EventArgs.Empty });
                type.GetMethod("InvalidateModernFrame", Flags).Invoke(main, null);
                main.DrawToBitmap(scene, new Rectangle(Point.Empty, scene.Size));
                Check((long)Field(main, "modernSceneRenderCount") == renders, "window drag uses frozen scene without heavy background work");
                type.GetMethod("OnResizeEnd", Flags).Invoke(main, new object[] { EventArgs.Empty });
                main.DrawToBitmap(scene, new Rectangle(Point.Empty, scene.Size));
                Check((long)Field(main, "modernSceneRenderCount") == renders + 1, "animation scene resumes once after window drag ends");
            }
            ((System.Windows.Forms.Timer)Field(main, "PlaceTimer")).Stop();
            main.ClientSize = new Size(1060, 600);
            Application.DoEvents();
            Check(((Control)Field(main, "advancedButton")).Bottom < main.ClientSize.Height, "compact layout fits minimum window");
            Capture(main, "compact-interface.png");
            var uiText = assembly.GetType("RBX_Alt_Manager.Classes.UiText");
            var setLanguage = uiText.GetMethod("SetLanguage", BindingFlags.Static | BindingFlags.NonPublic);
            setLanguage.Invoke(null, new object[] { "en" });
            Check(settings.Text == "Settings • Spectrum Manager" && ((Button)Field(main, "JoinServer")).Text == "Join", "English applies live to main window and settings");
            Check(File.ReadAllText("RAMSettings.ini").Contains("InterfaceLanguage=en"), "Language preference saves to disk");
            var credentialsType = assembly.GetType("RBX_Alt_Manager.Forms.CredentialImportForm");
            using (var credentials = (Form)Activator.CreateInstance(credentialsType, Flags, null, new object[] { null, false }, null))
            {
                credentialsType.GetMethod("AddText", Flags).Invoke(credentials, new object[] { "Fixture_User:PRIVATE_UI_SENTINEL" });
                var grid = (DataGridView)Field(credentials, "grid");
                Check(grid.Rows.Count == 1 && (string)grid.Rows[0].Cells[1].Value == "••••••••", "Credential queue masks passwords");
                Check(((TextBox)Field(credentials, "single")).UseSystemPasswordChar && grid.ReadOnly && grid.ClipboardCopyMode == DataGridViewClipboardCopyMode.Disable, "Credential form prevents accidental visible or grid clipboard password exposure");
                var untranslated = new System.Collections.Generic.HashSet<string>(); MissingEnglish(credentials, untranslated);
                File.WriteAllLines("credentials-missing-english.txt", untranslated);
                Check(untranslated.Count == 0 && credentials.Text == "Add user:pass", "Credential import dialog supports English");
                credentials.Show(); Pump(80);
                var importStart = (Button)Field(credentials, "start");
                Check(importStart.Visible && importStart.Right <= importStart.Parent.ClientSize.Width && importStart.Bottom <= importStart.Parent.ClientSize.Height, "Credential import action fits visible dialog");
                Check(grid.Columns[0].HeaderText == "Username", "Credential table headers support English");
                Capture(credentials, "credentials-english.png"); credentials.Close();
            }
            var missingEnglish = new System.Collections.Generic.HashSet<string>(); MissingEnglish(main, missingEnglish); MissingEnglish(settings, missingEnglish); MissingEnglish(advanced, missingEnglish);
            File.WriteAllLines("missing-english.txt", missingEnglish);
            Check(missingEnglish.Count == 0, "Main, settings and advanced interface contain no untranslated static Russian text in English mode");
            Capture(main, "interface-english.png"); Capture(settings, "settings-english.png");
            uiText.GetMethod("SetHints", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { false });
            Check(!((ToolTip)Field(main, "SaveTooltip")).Active && !((ToolTip)Field(settings, "Helper")).Active, "Tooltips can be hidden in manager and settings");
            uiText.GetMethod("SetHints", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { true });
            setLanguage.Invoke(null, new object[] { "ru" });
            Check(settings.Text == "Настройки • Spectrum Manager" && ((Button)Field(main, "JoinServer")).Text == "Подключиться", "Russian returns without restarting or changing account data");
            ((Button)Field(main, "ConfigButton")).PerformClick();
            settings.GetType().GetMethod("ShowAccountBackups", Flags).Invoke(settings, null);
            Capture(settings, "backup-settings.png");
            settings.Hide();
            ((TextBox)Field(main, "PlaceID")).Text = "987654321";
            ((TextBox)Field(main, "JobID")).Text = savedPrivate;
            main.Close();
            Check(File.ReadAllText("RAMSettings.ini").Contains("SavedJobId=" + savedPrivate), "closing immediately flushes pending private URL");
            var reopened = (Form)Activator.CreateInstance(type);
            reopened.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), reopened, type.GetMethod("AccountManager_Load", Flags));
            reopened.Shown -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), reopened, type.GetMethod("AccountManager_Shown", Flags));
            reopened.Show();
            type.GetMethod("RestoreLaunchInputs", Flags).Invoke(reopened, null);
            Check(((TextBox)Field(reopened, "PlaceID")).Text == "987654321", "fresh manager instance restores place from disk");
            Check(((TextBox)Field(reopened, "JobID")).Text == savedPrivate, "fresh manager instance restores full private URL from disk");
            reopened.Close();
        }
        catch (Exception e) { File.AppendAllText("ui-checks.txt", "FAIL " + e); Environment.ExitCode = 1; }
    }
}
