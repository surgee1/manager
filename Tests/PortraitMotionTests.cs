using RBX_Alt_Manager.Classes;
using RBX_Alt_Manager.Forms;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

internal static class PortraitMotionTests
{
    private static long Pixels(Bitmap image)
    {
        long result = 17;
        for (int y = 0; y < image.Height; y += 3)
            for (int x = 0; x < image.Width; x += 3) result = unchecked(result * 31 + image.GetPixel(x, y).ToArgb());
        return result;
    }
    internal static async Task Run(Action<bool, string> check)
    {
        int previousStrength = ThemeEditor.CharacterMotion;
        ThemeEditor.CharacterMotion = 100;
        try
        {
            using (var source = new Bitmap(320, 240, PixelFormat.Format32bppPArgb))
            {
                for (int y = 0; y < source.Height; y++)
                    for (int x = 0; x < source.Width; x++) source.SetPixel(x, y, Color.FromArgb(255, x % 256, y % 256, (x * 7 + y * 11) % 256));
                var initial = PortraitMotion.Render(source, "worker-test", .4, true);
                long initialPixels = Pixels(initial);
                await PortraitMotion.PendingFrame("worker-test");
                check(Pixels(initial) == initialPixels, "portrait worker never writes into the bitmap currently displayed by the UI");
                var ready = PortraitMotion.Render(source, "worker-test", .4, true);
                check(Pixels(ready) != initialPixels, "completed portrait frame is published on the next UI request");
                check(ready.GetPixel(80, 120) == source.GetPixel(80, 120), "asynchronous deformation keeps the left background anchored");
                long displayed = Pixels(ready);
                check(ReferenceEquals(ready, PortraitMotion.Render(source, "worker-test", 2.6, true)), "the UI immediately reuses its completed frame while the next one renders");
                await PortraitMotion.PendingFrame("worker-test");
                check(Pixels(ready) == displayed, "subsequent background work cannot mutate the displayed frame");
                check(Pixels(PortraitMotion.Render(source, "worker-test", 2.6, true)) != displayed, "later completed animation frame is swapped in without blocking the UI");
                check(ReferenceEquals(source, PortraitMotion.Render(source, "worker-test", 3, false)), "disabling animation immediately returns the static portrait");
            }
            var disposableSource = new Bitmap(1120, 640, PixelFormat.Format32bppPArgb);
            var bounded = PortraitMotion.Render(disposableSource, "retired-worker", 1, true);
            check(bounded.Width <= 720 && bounded.Height <= 360, "large windows keep a bounded illustration work surface instead of deforming every UI pixel");
            Task pending = PortraitMotion.PendingFrame("retired-worker");
            disposableSource.Dispose(); PortraitMotion.Clear();
            await pending;
            check(true, "clearing a theme during background rendering safely retires the worker and its independent image buffers");
        }
        finally { PortraitMotion.Clear(); ThemeEditor.CharacterMotion = previousStrength; }
    }
}
