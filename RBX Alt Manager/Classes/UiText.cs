using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Classes
{
    internal static class UiText
    {
        private sealed class Binding
        {
            internal string Source; internal bool Rendering;
            internal Func<string> Get; internal Action<string> Set;
            internal void Render() { string value = Translate(Source); if (Get() == value) return; Rendering = true; try { Set(value); } finally { Rendering = false; } }
        }
        private static readonly Dictionary<string, string> English = Load();
        private static readonly KeyValuePair<string, string>[] Fragments = English.OrderByDescending(pair => pair.Key.Length).ToArray();
        private static readonly ConditionalWeakTable<object, Binding> Texts = new ConditionalWeakTable<object, Binding>();
        private static readonly ConditionalWeakTable<Control, object> Registered = new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<ToolStripItem, object> RegisteredItems = new ConditionalWeakTable<ToolStripItem, object>();
        private static readonly ConditionalWeakTable<ToolTip, Dictionary<Control, string>> Tips = new ConditionalWeakTable<ToolTip, Dictionary<Control, string>>();
        private static readonly List<WeakReference<Binding>> Bindings = new List<WeakReference<Binding>>();
        private static bool started;
        internal static bool IsEnglish => AccountManager.General?.Get("InterfaceLanguage") == "en";
        internal static bool ShowHints => AccountManager.General == null || !AccountManager.General.Exists("ShowTooltips") || AccountManager.General.Get<bool>("ShowTooltips");
        internal static event EventHandler Changed;
        private static Dictionary<string, string> Load()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            string name = typeof(UiText).Assembly.GetManifestResourceNames().Single(resource => resource.EndsWith("Localization.en.tsv", StringComparison.Ordinal));
            using (var stream = typeof(UiText).Assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                { int tab = line.IndexOf('\t'); if (tab > 0) map[line.Substring(0, tab).Replace("\\n", "\n")] = line.Substring(tab + 1).Replace("\\n", "\n").Replace("\\s", " "); }
            }
            return map;
        }
        internal static string Translate(string source)
        {
            if (!IsEnglish || string.IsNullOrEmpty(source)) return source;
            if (English.TryGetValue(source, out string translated)) return translated;
            if (!source.Any(character => character >= '\u0400' && character <= '\u04ff')) return source;
            foreach (var pair in Fragments) source = source.Replace(pair.Key, pair.Value);
            return source;
        }
        private static void Bind(object target, Func<string> get, Action<string> set, Action<EventHandler> subscribe = null)
        {
            if (Texts.TryGetValue(target, out var existing)) { existing.Render(); return; }
            var binding = new Binding { Source = get(), Get = get, Set = set };
            Texts.Add(target, binding); Bindings.Add(new WeakReference<Binding>(binding));
            subscribe?.Invoke((sender, e) => { if (binding.Rendering) return; binding.Source = binding.Get(); binding.Render(); });
            binding.Render();
        }
        internal static void Attach(Control control)
        {
            if (!started)
            {
                started = true;
                Application.Idle += (sender, e) => { foreach (Form form in Application.OpenForms) if (!form.IsDisposed) Attach(form); };
            }
            if (Registered.TryGetValue(control, out _)) return;
            Registered.Add(control, new object());
            if (!(control is TextBoxBase) && !(control is ComboBox) && !(control is ListView) && !(control is ListBox))
                Bind(control, () => control.Text, value => control.Text = value, handler => control.TextChanged += handler);
            if (control is ListView list)
                foreach (ColumnHeader column in list.Columns) Bind(column, () => column.Text, value => column.Text = value);
            if (control is ToolStrip strip)
            {
                foreach (ToolStripItem item in strip.Items) AttachItem(item);
                strip.ItemAdded += (sender, e) => AttachItem(e.Item);
            }
            control.ControlAdded += (sender, e) => Attach(e.Control);
            foreach (Control child in control.Controls) Attach(child);
            if (control is Form form) { SpectrumBrand.Apply(form); ApplyTips(form); }
        }
        private static void AttachItem(ToolStripItem item)
        {
            if (RegisteredItems.TryGetValue(item, out _)) return;
            RegisteredItems.Add(item, new object());
            if (item is ToolStripControlHost host) { Attach(host.Control); return; }
            Bind(item, () => item.Text, value => item.Text = value, handler => item.TextChanged += handler);
            if (item is ToolStripDropDownItem menu)
            {
                foreach (ToolStripItem child in menu.DropDownItems) AttachItem(child);
                menu.DropDownOpening += (sender, e) => { foreach (ToolStripItem child in menu.DropDownItems) AttachItem(child); };
            }
        }
        private static IEnumerable<Control> Tree(Control root)
        { yield return root; foreach (Control child in root.Controls) foreach (var item in Tree(child)) yield return item; }
        private static void ApplyTips(Form form)
        {
            var field = form.GetType().GetField("components", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!(field?.GetValue(form) is IContainer container)) return;
            foreach (var tooltip in container.Components.OfType<ToolTip>())
            {
                tooltip.Active = ShowHints;
                var originals = Tips.GetOrCreateValue(tooltip);
                foreach (var control in Tree(form))
                {
                    if (!originals.TryGetValue(control, out string source)) originals[control] = source = tooltip.GetToolTip(control);
                    if (!string.IsNullOrEmpty(source)) tooltip.SetToolTip(control, Translate(source));
                }
            }
        }
        internal static void SetToolTip(ToolTip tooltip, Control control, string source)
        { Tips.GetOrCreateValue(tooltip)[control] = source; tooltip.Active = ShowHints; tooltip.SetToolTip(control, Translate(source)); }
        internal static void SetLanguage(string language)
        {
            if (language != "ru" && language != "en") throw new ArgumentException("Unknown UI language.");
            AccountManager.General.Set("InterfaceLanguage", language); AccountManager.IniSettings.Save("RAMSettings.ini"); ApplyAll();
        }
        internal static void SetHints(bool show)
        { AccountManager.General.Set("ShowTooltips", show.ToString()); AccountManager.IniSettings.Save("RAMSettings.ini"); ApplyAll(); }
        private static void ApplyAll()
        {
            for (int index = Bindings.Count - 1; index >= 0; index--)
            { if (Bindings[index].TryGetTarget(out var binding)) binding.Render(); else Bindings.RemoveAt(index); }
            foreach (Form form in Application.OpenForms) { Attach(form); ApplyTips(form); form.Invalidate(true); }
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
