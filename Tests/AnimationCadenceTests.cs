using RBX_Alt_Manager.Classes;
using System;

internal static class AnimationCadenceTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (int rate in new[] { 30, 20, 10 })
        {
            double next = 0; int frames = 0;
            for (int tick = 0; tick < 640; tick++)
                if (AnimationCadence.FrameDue(ref next, tick / 64d, rate)) frames++;
            check(frames >= rate * 10 - 1 && frames <= rate * 10 + 1, "animation rate is bounded across Windows timer quanta: " + rate + " fps");
        }
        double due = 0;
        check(AnimationCadence.FrameDue(ref due, 0, 30) && !AnimationCadence.FrameDue(ref due, 0, 30), "repeated paint requests cannot spend the same frame budget twice");
        check(AnimationCadence.FrameDue(ref due, 10, 30) && !AnimationCadence.FrameDue(ref due, 10, 30), "a stalled UI drops missed animation frames without a CPU catch-up burst");
    }
}
