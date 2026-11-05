using System;
using System.Drawing;

internal static class PrefixRenderingTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (string label in new[] { "Legit", "Cheat", "Без префикса" })
        using (var font = new Font("Segoe UI", 10))
        using (var bitmap = new Bitmap(260, 120))
        {
            var cell = new Rectangle(70, 40, 140, 32);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Black);
                graphics.TranslateTransform(cell.X, cell.Y);
                graphics.SetClip(new Rectangle(0, 0, cell.Width, cell.Height));
                RBX_Alt_Manager.PrefixRenderer.DrawContent(graphics, new Rectangle(0, 0, cell.Width, cell.Height), label, font, Color.White);
            }
            int textPixels = 0, outside = 0;
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).GetBrightness() < .6f) continue;
                if (!cell.Contains(x, y)) outside++;
                else if (x < cell.Right - 24) textPixels++;
            }
            check(textPixels > 25, "Selected prefix text remains visible in translated cell: " + label);
            check(outside == 0, "Selected prefix text stays inside its cell: " + label);
        }
    }
}
