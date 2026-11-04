using System;
using System.Drawing;
using System.Windows.Forms;
using RBX_Alt_Manager.Classes;

namespace RBX_Alt_Manager.Forms
{
    public partial class SettingsForm
    {
        private readonly System.Collections.Generic.List<Action> colorLabels = new System.Collections.Generic.List<Action>();
        private void InitializeColorSettings()
        {
            var flow = SettingsPage("Цвета");
            Section(flow, "ВИЗУАЛЬНЫЕ ПРОФИЛИ");
            Description(flow, "Выбери анимированный стиль или профиль с персонажем. Ниже можно менять цвета и отключать движение.");
            var presets = new FlowLayoutPanel { Width = 650, Height = ((ThemeEditor.SupportedProfiles.Length + 2) / 3) * 46 + 11, WrapContents = true };
            flow.Controls.Add(presets);
            void Preset(string name, string profile)
            {
                var button = new NeonButton { Text = name, Width = 190, Height = 40, Font = Font };
                button.Click += (sender, e) => ApplyVisualProfile(profile);
                presets.Controls.Add(button);
            }
            Preset("Nyan Cat", "nyan");
            Preset("Tokyo Ghoul", "kaneki");
            Preset("Naruto", "naruto");
            Preset("Jujutsu Kaisen", "gojo");
            Preset("Tokyo Ghoul: Manga", "kaneki_manga");
            Preset("Juujika no Rokunin", "uruma");
            Preset("Massacre Happy End", "happyend");
            Preset("Gangsta.", "gangsta");
            Preset("Re:Zero", "subaru");
            Preset("Gangsta. II", "alex");
            Preset("Hunter x Hunter", "kurapika");
            Preset("Darling in the Franxx", "zerotwo");
            Preset("Akame ga Kill!", "akame");
            Preset("Death Note", "deathnote");
            var motion = new CheckBox { Text = "Анимация фона и переливов", AutoSize = true,
                Checked = ThemeEditor.AnimationsEnabled, Margin = new Padding(0, 12, 0, 12) };
            motion.CheckedChanged += (sender, e) => { ThemeEditor.AnimationsEnabled = motion.Checked; SaveAppearance(); };
            flow.Controls.Add(motion);
            var strengthLabel = new Label { Text = "Сила движения персонажей: " + ThemeEditor.CharacterMotion + "%", AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            flow.Controls.Add(strengthLabel);
            var strength = new TrackBar { Minimum = 0, Maximum = 100, Value = ThemeEditor.CharacterMotion,
                TickFrequency = 25, Width = 610, Height = 44, BackColor = ThemeEditor.FormsBackground };
            flow.Controls.Add(strength);
            var saveStrength = new Timer(components) { Interval = 200 };
            saveStrength.Tick += (sender, e) => { saveStrength.Stop(); SaveAppearance(); };
            strength.ValueChanged += (sender, e) =>
            {
                ThemeEditor.CharacterMotion = strength.Value;
                strengthLabel.Text = "Сила движения персонажей: " + strength.Value + "%";
                saveStrength.Stop(); saveStrength.Start();
            };
            Helper.SetToolTip(strength, "0% — неподвижный персонаж. Выше — заметнее дыхание, наклон головы и ветер в волосах.");
            Section(flow, "СВОИ ЦВЕТА");
            ColorSetting(flow, "Начало перелива", () => ThemeEditor.AccentPrimary, c => ThemeEditor.AccentPrimary = c);
            ColorSetting(flow, "Конец перелива", () => ThemeEditor.AccentSecondary, c => ThemeEditor.AccentSecondary = c);
            ColorSetting(flow, "Фон приложения", () => ThemeEditor.FormsBackground, c => ThemeEditor.FormsBackground = c);
            ColorSetting(flow, "Фон списка аккаунтов и панелей", () => ThemeEditor.AccountBackground, c => ThemeEditor.AccountBackground = c);
            ColorSetting(flow, "Фон полей ввода", () => ThemeEditor.TextBoxesBackground, c => ThemeEditor.TextBoxesBackground = c);
            ColorSetting(flow, "Основной текст", () => ThemeEditor.FormsForeground, c =>
            {
                ThemeEditor.FormsForeground = ThemeEditor.AccountForeground = ThemeEditor.ButtonsForeground =
                    ThemeEditor.TextBoxesForeground = ThemeEditor.LabelForeground = c;
            });
        }

        private void ColorSetting(FlowLayoutPanel flow, string label, Func<Color> get, Action<Color> set)
        {
            var row = new FlowLayoutPanel { Width = 650, Height = 50, WrapContents = false, Margin = new Padding(0, 3, 0, 3) };
            row.Controls.Add(new Label { Text = label, Width = 390, Height = 42, TextAlign = ContentAlignment.MiddleLeft });
            var button = new NeonButton { Text = "Выбрать " + ThemeEditor.ToHexString(get()), Width = 210, Height = 40 };
            button.Click += (sender, e) =>
            {
                using (var dialog = new ColorDialog { FullOpen = true, Color = get() })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        set(dialog.Color);
                        SaveAppearance();
                    }
            };
            row.Controls.Add(button);
            flow.Controls.Add(row);
            flow.VisibleChanged += (sender, e) => button.Text = "Выбрать " + ThemeEditor.ToHexString(get());
            colorLabels.Add(() => button.Text = "Выбрать " + ThemeEditor.ToHexString(get()));
            Helper.SetToolTip(button, "Выбрать свой цвет: " + label.ToLowerInvariant());
        }

        private void ApplyColorPreset(string primary, string secondary, string background, string surface)
        {
            ThemeEditor.AccentPrimary = ColorTranslator.FromHtml(primary);
            ThemeEditor.AccentSecondary = ColorTranslator.FromHtml(secondary);
            ThemeEditor.FormsBackground = ColorTranslator.FromHtml(background);
            ThemeEditor.AccountBackground = ColorTranslator.FromHtml(surface);
            ThemeEditor.ButtonsBackground = NeonButton.Mix(ThemeEditor.AccountBackground, Color.White, 0.05f);
            ThemeEditor.TextBoxesBackground = NeonButton.Mix(ThemeEditor.AccountBackground, Color.White, 0.04f);
            ThemeEditor.ButtonsBorder = ThemeEditor.TextBoxesBorder = NeonButton.Mix(ThemeEditor.AccentPrimary, ThemeEditor.FormsBackground, 0.55f);
            ThemeEditor.FormsForeground = ThemeEditor.AccountForeground = ThemeEditor.ButtonsForeground =
                ThemeEditor.TextBoxesForeground = ThemeEditor.LabelForeground = Color.FromArgb(233, 236, 245);
            ThemeEditor.LightImages = true;
            SaveAppearance();
        }

        private void ApplyVisualProfile(string profile)
        {
            if (Array.IndexOf(ThemeEditor.SupportedProfiles, profile) < 0) profile = "kaneki_manga";
            ThemeEditor.VisualProfile = profile;
            switch (profile)
            {
                case "nyan": ApplyColorPreset("#F572C2", "#7A9CFF", "#11112D", "#202044"); break;
                case "kaneki": ApplyColorPreset("#C92B52", "#C6C9D6", "#110C12", "#21171F"); break;
                case "naruto": ApplyColorPreset("#EE8D28", "#F6CC63", "#14121A", "#27232B"); break;
                case "gojo": ApplyColorPreset("#55BBFF", "#9D71EF", "#0B1021", "#19213B"); break;
                case "kaneki_manga": ApplyColorPreset("#C4C6CC", "#AF344E", "#0C0C10", "#1B1B22"); break;
                case "uruma": ApplyColorPreset("#C1C5CD", "#8796B5", "#0B0D11", "#191D25"); break;
                case "happyend": ApplyColorPreset("#B7B5CB", "#74809A", "#101017", "#20202D"); break;
                case "gangsta": ApplyColorPreset("#B6C0CB", "#61ACAB", "#0D1115", "#1A2229"); break;
                case "subaru": ApplyColorPreset("#ED913D", "#A277DA", "#100F19", "#201B2A"); break;
                case "alex": ApplyColorPreset("#80B7CF", "#A6C2C1", "#0C131A", "#182631"); break;
                case "kurapika": ApplyColorPreset("#D9B35E", "#CB3F58", "#0C111D", "#192337"); break;
                case "zerotwo": ApplyColorPreset("#ED8DB8", "#62CCD5", "#101220", "#24243B"); break;
                case "akame": ApplyColorPreset("#D3475E", "#B8C1D0", "#100D14", "#251B28"); break;
                case "deathnote": ApplyColorPreset("#B8C6DC", "#738CB6", "#0D1119", "#1C2430"); break;
                default: ApplyColorPreset("#AE36DB", "#20A5CF", "#111521", "#191D2A"); break;
            }
        }

        private void SaveAppearance()
        {
            ThemeEditor.SaveTheme();
            AccountManager.Instance.ApplyTheme();
            foreach (var update in colorLabels) update();
            Invalidate(true);
        }
    }
}
