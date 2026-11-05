using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using RBX_Alt_Manager.Forms;

namespace RBX_Alt_Manager.Classes
{
    internal static class VisualBackground
    {
        private static readonly Dictionary<string, Image> Images = new Dictionary<string, Image>();
        private static readonly Dictionary<string, Bitmap> FittedImages = new Dictionary<string, Bitmap>();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        internal static double Time => Math.Max(0, Clock.Elapsed.TotalSeconds - AnimationCadence.PausedSeconds);
        static VisualBackground()
        {
            Application.ApplicationExit += (sender, e) =>
            {
                foreach (var image in Images.Values) image?.Dispose();
                foreach (var image in FittedImages.Values) image.Dispose();
                Images.Clear(); FittedImages.Clear(); PortraitMotion.Clear();
            };
        }
        public static void Draw(Graphics g, Size size)
        {
            double time = ThemeEditor.AnimationsEnabled ? Time : 12;
            switch (ThemeEditor.VisualProfile)
            {
                case "matrix": Matrix(g, size, time); break;
                case "nyan": Nyan(g, size, time); break;
                case "aurora": Aurora(g, size, time); break;
                case "sunset": Sunset(g, size, time); break;
                case "sakura": Petals(g, size, time); break;
                case "anime-night": Stars(g, size, time); break;
                case "manga":
                case "kaneki":
                case "naruto":
                case "gojo":
                case "kaneki_manga":
                case "uruma":
                case "happyend":
                case "gangsta":
                case "subaru":
                case "alex":
                case "kurapika":
                case "zerotwo":
                case "akame":
                case "deathnote":
                case "kagami":
                case "ichigo":
                case "baki":
                case "nogamenolife":
                case "hellsing":
                    Portrait(g, size, time, ThemeEditor.VisualProfile);
                    Stars(g, size, time);
                    break;
            }
        }
        private static void Portrait(Graphics g, Size size, double time, string profile)
        {
            if (!Images.TryGetValue(profile, out Image image))
            {
                string path = Path.Combine(Application.StartupPath, "Themes", profile + ".png");
                if (File.Exists(path))
                    try { using (var original = Image.FromFile(path)) image = new Bitmap(original); } catch { }
                Images[profile] = image;
            }
            if (image == null) return;
            string key = profile + ":" + size.Width + "x" + size.Height;
            if (!FittedImages.TryGetValue(key, out Bitmap fitted))
            {
                if (FittedImages.Count >= 4) { foreach (var old in FittedImages.Values) old.Dispose(); FittedImages.Clear(); PortraitMotion.Clear(); }
                fitted = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                using (var canvas = Graphics.FromImage(fitted))
                using (var attributes = new ImageAttributes())
                {
                    canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    attributes.SetColorMatrix(new ColorMatrix { Matrix33 = 0.38f });
                    if (profile == "ichigo")
                    {
                        float ratio = Math.Min(size.Width * .64f / image.Width, (float)size.Height / image.Height);
                        int width = (int)(image.Width * ratio), height = (int)(image.Height * ratio);
                        canvas.DrawImage(image, new Rectangle(size.Width - width, (size.Height - height) / 2, width, height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }
                    else canvas.DrawImage(image, new Rectangle(Point.Empty, size), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                }
                FittedImages[key] = fitted;
            }
            image = fitted;
            image = PortraitMotion.Render(fitted, key, time, ThemeEditor.AnimationsEnabled);
            if (image.Size == size) g.DrawImageUnscaled(image, 0, 0);
            else g.DrawImage(image, new Rectangle(Point.Empty, size));
        }
        private static void Stars(Graphics g, Size size, double time)
        {
            for (int i = 0; i < 32; i++)
            {
                float x = (float)((i * 173 + Math.Sin(time * .2 + i) * 13 + size.Width) % size.Width);
                float y = 100 + (float)((i * 97 - time * (3 + i % 4) + 100000) % Math.Max(1, size.Height - 110));
                int alpha = (int)(30 + (Math.Sin(time + i * 2) + 1) * 32);
                using (var brush = new SolidBrush(Color.FromArgb(alpha, ThemeEditor.AccentSecondary)))
                {
                    g.FillEllipse(brush, x, y, 2, 2);
                    if (i % 6 == 0) g.FillRectangle(brush, x - 2, y + .5f, 6, 1);
                }
            }
        }
        private static void Petals(Graphics g, Size size, double time)
        {
            for (int i = 0; i < 22; i++)
            {
                float x = (float)((i * 137 + time * 13 + Math.Sin(time * .6 + i) * 30) % (size.Width + 60)) - 30;
                float y = (float)((i * 79 + time * (13 + i % 5)) % (size.Height + 60)) - 30;
                var state = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform((float)(time * 23 + i * 37));
                using (var petal = new GraphicsPath())
                using (var fill = new SolidBrush(Color.FromArgb(90 + i % 5 * 12, ThemeEditor.AccentPrimary)))
                {
                    petal.AddBezier(0, -9, 12, -6, 7, 6, 0, 9);
                    petal.AddBezier(0, 9, -7, 6, -12, -6, 0, -9);
                    g.FillPath(fill, petal);
                }
                g.Restore(state);
            }
        }
        private static void Matrix(Graphics g, Size size, double time)
        {
            const string symbols = "010101ZX$<>/{}[]";
            using (var font = new Font("Consolas", 11, FontStyle.Regular))
            {
                for (int column = 0; column < size.Width / 26 + 1; column++)
                {
                    float head = (float)((time * (26 + column % 7 * 7) + column * 113) % (size.Height + 250));
                    for (int row = 0; row < 13; row++)
                    {
                        float y = head - row * 18;
                        if (y < 98 || y > size.Height) continue;
                        int index = Math.Abs(column * 37 + row * 13 + (int)(time * 3)) % symbols.Length;
                        using (var brush = new SolidBrush(Color.FromArgb(row == 0 ? 105 : Math.Max(5, 65 - row * 5), row == 0 ? Color.FromArgb(184, 255, 209) : ThemeEditor.AccentPrimary)))
                            g.DrawString(symbols[index].ToString(), font, brush, column * 26 + 8, y);
                    }
                }
            }
        }
        private static void Nyan(Graphics g, Size size, double time)
        {
            float x = (float)((time * 62) % (size.Width + 420)) - 200;
            float y = size.Height - 100 + (float)Math.Sin(time * 4) * 7;
            Color[] rainbow = { Color.Red, Color.Orange, Color.Yellow, Color.LimeGreen, Color.DodgerBlue, Color.MediumPurple };
            for (int band = 0; band < rainbow.Length; band++)
                using (var brush = new SolidBrush(Color.FromArgb(120, rainbow[band])))
                    for (int step = 0; step < 24; step++)
                        g.FillRectangle(brush, x - step * 10, y + band * 7 + (float)Math.Sin(step * .55 + time * 5) * 3, 11, 8);
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            void Pixel(Color color, int px, int py, int w, int h)
            { using (var brush = new SolidBrush(color)) g.FillRectangle(brush, x + px * 3, y + py * 3, w * 3, h * 3); }
            Color edge = Color.FromArgb(210, 23, 23, 47), grey = Color.FromArgb(220, 170, 177, 204);
            Pixel(edge, 0, 1, 29, 17); Pixel(Color.FromArgb(230, 240, 187, 143), 1, 2, 26, 14);
            Pixel(Color.FromArgb(240, 245, 141, 201), 3, 4, 22, 10);
            Pixel(edge, 21, 5, 13, 13); Pixel(grey, 22, 6, 11, 11);
            Pixel(edge, 22, 2, 4, 5); Pixel(edge, 29, 2, 4, 5);
            Pixel(grey, 23, 3, 2, 5); Pixel(grey, 30, 3, 2, 5);
            Pixel(edge, 24, 9, 1, 2); Pixel(edge, 30, 9, 1, 2); Pixel(edge, 27, 12, 2, 1);
            Pixel(grey, -4, 9, 5, 3); Pixel(grey, -6, 6, 3, 4);
            int feet = ((int)(time * 8) % 2);
            Pixel(grey, 4 + feet, 17, 4, 3); Pixel(grey, 16 - feet, 17, 4, 3); Pixel(grey, 26 + feet, 17, 4, 3);
            for (int i = 0; i < 9; i++) Pixel(Color.FromArgb(220, 199, 72, 154), 4 + i * 7 % 19, 5 + i * 3 % 7, 1, 1);
            g.SmoothingMode = old;
        }
        private static void Aurora(Graphics g, Size size, double time)
        {
            for (int band = 0; band < 4; band++)
            {
                var points = new PointF[25];
                for (int i = 0; i < points.Length; i++) points[i] = new PointF(size.Width * i / 24f,
                    170 + band * 70 + (float)Math.Sin(i * .2 + time * .3 + band) * 80);
                using (var pen = new Pen(Color.FromArgb(24, band % 2 == 0 ? ThemeEditor.AccentPrimary : ThemeEditor.AccentSecondary), 45))
                    g.DrawCurve(pen, points);
            }
        }
        private static void Sunset(Graphics g, Size size, double time)
        {
            float radius = 180 + (float)Math.Sin(time * .3) * 15;
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(size.Width - radius * 2, size.Height - radius * 1.6f, radius * 2, radius * 2);
                using (var glow = new PathGradientBrush(path))
                {
                    glow.CenterColor = Color.FromArgb(95, ThemeEditor.AccentPrimary);
                    glow.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(glow, path);
                }
            }
        }
    }
}
