using System;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.GameUi;

// Checks for three small rules that are plain arithmetic and are compiled here as they ship:
//   Core/GameUi/PanelPlacement.cs   where a panel of the mod's stands when the game's building box is in its place
//   Core/GameUi/BarRule.cs          when a bar of the game's is written again, and a zone cut off at the bar's ends
//   Camera/CameraWatch.cs           the counting behind "another mod has the camera": when to stand down, when to come back
//   Core/OtherModName.cs            whether a loaded assembly's name is a known mod's
//
// What is not here: anything of the game. Where the two boxes really are on the screen, whether the bar looks like the
// game's and whether another mod's camera is seen are for the game to show, in the "Panel check", "Gauge check" and
// "Camera check" lines of Player.log.
internal static partial class Program
{
    private const float ScreenW = 1920f, ScreenH = 1080f;

    private static void Polish()
    {
        PanelRule();
        PanelHysteresis();
        PanelDue();
        BarSteps();
        CameraCounting();
        ModNames();
        PolishTiming();
    }

    // The panel at home as left, bottom, right, top; the building box likewise.
    private static PanelSide Place(bool up, float bl, float bb, float br, float bt, float pl, float pb, float pr, float pt, PanelSide now, out float dx, out float dy)
    {
        return PanelPlacement.Place(up, bl, bb, br, bt, pl, pb, pr, pt, ScreenW, ScreenH, now, out dx, out dy);
    }

    private static bool Clear(float bl, float bb, float br, float bt, float pl, float pb, float pr, float pt, float dx, float dy)
    {
        return !PanelPlacement.Overlap(bl, bb, br, bt, pl + dx, pb + dy, pr + dx, pt + dy, 0f);
    }

    private static void PanelRule()
    {
        float dx, dy;
        // A building box in the middle of the screen, 400 x 300, and a panel 300 x 200.
        const float bl = 760f, bb = 400f, br = 1160f, bt = 700f;

        Check(Place(false, bl, bb, br, bt, 800f, 450f, 1100f, 650f, PanelSide.Home, out dx, out dy) == PanelSide.Home && dx == 0f && dy == 0f,
            "panel: with no building box up, the panel stays over its own place");
        Check(Place(true, bl, bb, br, bt, 100f, 100f, 400f, 300f, PanelSide.Home, out dx, out dy) == PanelSide.Home && dx == 0f && dy == 0f,
            "panel: a building box far from the panel moves nothing");

        // Right on top of it, a little left of its middle: the shorter step is to the left.
        PanelSide side = Place(true, bl, bb, br, bt, 780f, 450f, 1080f, 650f, PanelSide.Home, out dx, out dy);
        Check(side == PanelSide.Left && dy == 0f && Near(780f + 300f + dx, bl - PanelPlacement.Gap), "panel: over the box and left of its middle, it steps to the left, the gap away from the box");
        Check(Clear(bl, bb, br, bt, 780f, 450f, 1080f, 650f, dx, dy), "panel: after the step to the left the two share no part of the screen");

        // Right of its middle: to the right.
        side = Place(true, bl, bb, br, bt, 900f, 450f, 1200f, 650f, PanelSide.Home, out dx, out dy);
        Check(side == PanelSide.Right && dy == 0f && Near(900f + dx, br + PanelPlacement.Gap), "panel: over the box and right of its middle, it steps to the right");
        Check(Clear(bl, bb, br, bt, 900f, 450f, 1200f, 650f, dx, dy), "panel: after the step to the right the two share no part of the screen");

        // Exactly in the middle: left and right are the same step, and left is taken (the first named).
        side = Place(true, bl, bb, br, bt, 810f, 450f, 1110f, 650f, PanelSide.Home, out dx, out dy);
        Check(side == PanelSide.Left, "panel: with the same step either way it goes left, always");

        // Only touching within the gap, not overlapping: still moved, so the gap is kept.
        side = Place(true, bl, bb, br, bt, 1165f, 450f, 1465f, 650f, PanelSide.Home, out dx, out dy);
        Check(side == PanelSide.Right && Near(dx, br + PanelPlacement.Gap - 1165f), "panel: nearer than the gap counts as in the way, and the step is the few pixels that make the gap");
        Check(Place(true, bl, bb, br, bt, br + PanelPlacement.Gap + 1f, 450f, br + PanelPlacement.Gap + 301f, 650f, PanelSide.Home, out dx, out dy) == PanelSide.Home,
            "panel: a pixel more than the gap away is clear");

        // The box at the left edge of the screen: no room on its left, so right even though left would be the shorter step.
        side = Place(true, 20f, 400f, 420f, 700f, 30f, 450f, 330f, 650f, PanelSide.Home, out dx, out dy);
        Check(side == PanelSide.Right && Near(30f + dx, 420f + PanelPlacement.Gap), "panel: with no room on the left of the box it goes right");
        // ...and at the right edge: left.
        side = Place(true, 1500f, 400f, 1900f, 700f, 1600f, 450f, 1900f, 650f, PanelSide.Home, out dx, out dy);
        Check(side == PanelSide.Left && 1600f + dx >= 0f, "panel: with no room on the right of the box it goes left");

        // A box as wide as the screen: neither side has room, so above or below, whichever is the shorter step.
        side = Place(true, 0f, 400f, ScreenW, 700f, 800f, 380f, 1100f, 580f, PanelSide.Home, out dx, out dy);
        Check(side == PanelSide.Below && dx == 0f && Near(580f + dy, 400f - PanelPlacement.Gap), "panel: a box the width of the screen sends it below (the shorter step)");
        side = Place(true, 0f, 400f, ScreenW, 700f, 800f, 560f, 1100f, 760f, PanelSide.Home, out dx, out dy);
        Check(side == PanelSide.Above && Near(560f + dy, 700f + PanelPlacement.Gap), "panel: ...or above, where that is the shorter step");
        Check(Clear(0f, 400f, ScreenW, 700f, 800f, 560f, 1100f, 760f, dx, dy), "panel: above it, the two share no part of the screen");

        // A box that fills the screen: nowhere to go.
        side = Place(true, 0f, 0f, ScreenW, ScreenH, 800f, 450f, 1100f, 650f, PanelSide.Home, out dx, out dy);
        Check(side == PanelSide.Wait && dx == 0f && dy == 0f, "panel: a box that leaves no side free makes the panel wait, and moves nothing");
        // A panel too big for any side.
        side = Place(true, 860f, 440f, 1060f, 640f, 10f, 10f, 1910f, 1070f, PanelSide.Home, out dx, out dy);
        Check(side == PanelSide.Wait, "panel: a panel too large for any side waits");

        // Nonsense in: nothing out.
        Check(Place(true, 500f, 500f, 500f, 500f, 400f, 400f, 700f, 600f, PanelSide.Home, out dx, out dy) == PanelSide.Home, "panel: a box of no size is no box");
        Check(Place(true, bl, bb, br, bt, 900f, 500f, 900f, 500f, PanelSide.Home, out dx, out dy) == PanelSide.Home, "panel: a panel of no size is not moved");

        // Whatever the two rectangles, a side that is given never leaves them overlapping and never leaves the screen.
        var random = new Random(7);
        bool sound = true;
        int moved = 0, waited = 0;
        for (int i = 0; i < 20000 && sound; i++)
        {
            float w = 120f + (float)random.NextDouble() * 700f, h = 80f + (float)random.NextDouble() * 500f;
            float x = (float)random.NextDouble() * (ScreenW - w), y = (float)random.NextDouble() * (ScreenH - h);
            float pw = 150f + (float)random.NextDouble() * 500f, ph = 100f + (float)random.NextDouble() * 400f;
            float px = (float)random.NextDouble() * (ScreenW - pw), py = (float)random.NextDouble() * (ScreenH - ph);
            PanelSide got = Place(true, x, y, x + w, y + h, px, py, px + pw, py + ph, PanelSide.Home, out dx, out dy);
            if (got == PanelSide.Wait)
            {
                waited++;
                sound = dx == 0f && dy == 0f;
            }
            else if (got != PanelSide.Home)
            {
                moved++;
                sound = Clear(x, y, x + w, y + h, px, py, px + pw, py + ph, dx, dy)
                        && px + dx >= -0.01f && px + pw + dx <= ScreenW + 0.01f && py + dy >= -0.01f && py + ph + dy <= ScreenH + 0.01f;
            }
            else
            {
                sound = dx == 0f && dy == 0f && Clear(x, y, x + w, y + h, px, py, px + pw, py + ph, 0f, 0f);
            }
        }
        Check(sound && moved > 1000, "panel: over 20,000 made-up pairs, a moved panel is clear of the box and whole on the screen, and one left at home was clear already (" + moved + " moved, " + waited + " waited)");
    }

    private static void PanelHysteresis()
    {
        float dx, dy;
        const float bl = 760f, bb = 400f, br = 1160f, bt = 700f;

        // Once to the left it stays left while left has room, even where right has become the shorter step.
        PanelSide side = Place(true, bl, bb, br, bt, 900f, 450f, 1200f, 650f, PanelSide.Left, out dx, out dy);
        Check(side == PanelSide.Left && Near(1200f + dx, bl - PanelPlacement.Gap), "panel: on the left it stays on the left while the left has room (no hopping across)");
        // ...until the left has no room: then the other side.
        side = Place(true, 200f, 400f, 600f, 700f, 250f, 450f, 550f, 650f, PanelSide.Left, out dx, out dy);
        Check(side == PanelSide.Right, "panel: when its side has no room left it takes another");

        // Going home needs more room than going away did: just outside the gap is not enough once it has moved.
        float homeLeft = br + PanelPlacement.Gap + 5f;
        Check(Place(true, bl, bb, br, bt, homeLeft, 450f, homeLeft + 300f, 650f, PanelSide.Home, out dx, out dy) == PanelSide.Home, "panel: five pixels beyond the gap is clear for a panel at home");
        Check(Place(true, bl, bb, br, bt, homeLeft, 450f, homeLeft + 300f, 650f, PanelSide.Right, out dx, out dy) == PanelSide.Right, "panel: ...but a panel that stepped aside does not go back for five pixels");
        homeLeft = br + PanelPlacement.Gap + PanelPlacement.Keep + 1f;
        Check(Place(true, bl, bb, br, bt, homeLeft, 450f, homeLeft + 300f, 650f, PanelSide.Right, out dx, out dy) == PanelSide.Home && dx == 0f, "panel: it goes home once home is clear by the wider margin");
        Check(Place(false, bl, bb, br, bt, 900f, 450f, 1200f, 650f, PanelSide.Right, out dx, out dy) == PanelSide.Home && dx == 0f && dy == 0f, "panel: the box gone, it goes home at once");

        // A camera that sways a few pixels to and fro about the edge: the side is taken once and kept, never flickering.
        PanelSide now = PanelSide.Home;
        int changes = 0;
        for (int i = 0; i < 400; i++)
        {
            float sway = (float)Math.Sin(i * 0.37) * 10f;
            float left = br + PanelPlacement.Gap + sway;   // about the edge of "in the way"
            PanelSide next = Place(true, bl, bb, br, bt, left, 450f, left + 300f, 650f, now, out dx, out dy);
            if (next != now)
            {
                changes++;
                now = next;
            }
        }
        Check(changes == 1 && now == PanelSide.Right, "panel: a view swaying ten pixels about the edge moves the panel once and never back (" + changes + " change(s))");

        // The same question twice has the same answer.
        PanelSide a = Place(true, bl, bb, br, bt, 780f, 450f, 1080f, 650f, PanelSide.Home, out float ax, out float ay);
        PanelSide b = Place(true, bl, bb, br, bt, 780f, 450f, 1080f, 650f, PanelSide.Home, out float bx, out float by);
        Check(a == b && ax == bx && ay == by, "panel: the same two boxes always give the same step");
    }

    private static void PanelDue()
    {
        Check(!PanelPlacement.Due(false, false, 0f, 0f) && !PanelPlacement.Due(false, false, 1e6f, 1e6f), "panel: with no building box up nothing is measured, however the panel moves");
        Check(PanelPlacement.Due(true, false, 0f, 0f) && PanelPlacement.Due(false, true, 0f, 0f), "panel: the box coming, and the box going, are each measured");
        Check(!PanelPlacement.Due(true, true, 0f, 0f) && !PanelPlacement.Due(true, true, PanelPlacement.MovedSqr, PanelPlacement.MovedSqr), "panel: two boxes standing still (or moved by exactly the threshold) are not measured again");
        Check(PanelPlacement.Due(true, true, PanelPlacement.MovedSqr + 0.01f, 0f) && PanelPlacement.Due(true, true, 0f, PanelPlacement.MovedSqr + 0.01f), "panel: either box moved by more than the threshold is measured again");
        Check(PanelPlacement.Word(PanelSide.Left) == "moved to the left of it" && PanelPlacement.Word(PanelSide.Home).Length > 0 && PanelPlacement.Word(PanelSide.Wait).Length > 0, "panel: each side has its words for the log");
    }

    private static void BarSteps()
    {
        Check(BarRule.Due(-1f, 0.5f) && BarRule.Due(-1f, 0f), "bar: a bar that shows nothing yet is written, whatever the value");
        Check(!BarRule.Due(0.42f, 0.42f) && !BarRule.Due(0.42f, 0.42f + BarRule.Step * 0.9f) && !BarRule.Due(0.42f, 0.42f - BarRule.Step * 0.9f), "bar: less than a step either way is not written");
        Check(BarRule.Due(0.42f, 0.42f + BarRule.Step * 1.1f) && BarRule.Due(0.42f, 0.42f - BarRule.Step * 1.1f), "bar: more than a step either way is written");
        Check(BarRule.Due(0.003f, 0f) && BarRule.Due(0.998f, 1f) && !BarRule.Due(0f, 0f) && !BarRule.Due(1f, 1f), "bar: the ends are always reached, and once shown are not written again");
        Check(BarRule.Clamp(-3f) == 0f && BarRule.Clamp(7f) == 1f && BarRule.Clamp(0.3f) == 0.3f && BarRule.Clamp(float.NaN) == 0f, "bar: a value outside the bar, or no number at all, is brought in");
        Check(BarRule.Due(0.5f, float.NaN) && !BarRule.Due(0f, float.NaN) && BarRule.Due(0.5f, 9f) && !BarRule.Due(1f, 9f), "bar: a value out of range is treated as the end it is beyond");

        // A marker crossing the bar in a second at 60 frames a second is written every frame; at 1,000 a second, only on a step.
        int writes = 0;
        float shown = -1f;
        for (int frame = 0; frame <= 1000; frame++)
        {
            float value = frame / 1000f;
            if (BarRule.Due(shown, value))
            {
                shown = BarRule.Clamp(value);
                writes++;
            }
        }
        Check(writes <= (int)(1f / BarRule.Step) + 2 && writes >= (int)(1f / BarRule.Step) - 40 && shown == 1f, "bar: a pass in a thousand small moves is written about two hundred times and ends at the end (" + writes + ")");
        writes = 0;
        shown = -1f;
        for (int frame = 0; frame < 600; frame++)
        {
            if (BarRule.Due(shown, 0.42f))
            {
                shown = 0.42f;
                writes++;
            }
        }
        Check(writes == 1, "bar: a bar that stands still for ten seconds is written once");

        Check(BarRule.Span(0.72f, 0.26f, out float from, out float to) && Near(from, 0.59f) && Near(to, 0.85f), "bar: the fishing cast's zone is 59% to 85% of the bar");
        Check(BarRule.Span(0.95f, 0.3f, out from, out to) && Near(from, 0.8f) && to == 1f, "bar: a zone that runs past the end is cut off at the end");
        Check(BarRule.Span(0.02f, 0.2f, out from, out to) && from == 0f && Near(to, 0.12f), "bar: ...and at the start");
        Check(!BarRule.Span(0.5f, 0f, out from, out to) && !BarRule.Span(0.5f, -0.2f, out from, out to) && !BarRule.Span(1.5f, 0.2f, out from, out to), "bar: no width, or wholly off the bar, is no zone");
        Check(BarRule.Span(0.5f, 5f, out from, out to) && from == 0f && to == 1f, "bar: a zone wider than the bar is the whole bar");
    }

    private static void CameraCounting()
    {
        var watch = new CameraWatch();
        Check(!watch.StoodDown && watch.Foreign == 0 && watch.Clean == 0, "camera: at the start nothing has been seen and the camera is this mod's");

        // Clean frames change nothing.
        bool changed = false;
        for (int i = 0; i < 500; i++)
        {
            changed |= watch.Feed(false, false);
        }
        Check(!changed && !watch.StoodDown, "camera: frames in which nothing else drove the camera change nothing");

        // One short of the count: not yet. The next: stood down, and said once.
        for (int i = 0; i < CameraWatch.ToYield - 1; i++)
        {
            changed |= watch.Feed(true, false);
        }
        Check(!changed && !watch.StoodDown && watch.Foreign == CameraWatch.ToYield - 1, "camera: one frame short of the count, the camera is still this mod's");
        Check(watch.Feed(true, false) && watch.StoodDown, "camera: at the count it stands down, and says so in that frame");
        Check(!watch.Feed(true, false) && watch.StoodDown, "camera: ...and only in that frame");

        // Back: one short of the count is not enough, and an interruption starts the count again.
        for (int i = 0; i < CameraWatch.ToReturn - 1; i++)
        {
            changed |= watch.Feed(false, false);
        }
        Check(!changed && watch.StoodDown, "camera: one clean frame short of the count, it is still stood down");
        Check(!watch.Feed(true, false) && watch.StoodDown && watch.Clean == 0, "camera: one frame of the other mod's starts the clean count again");
        for (int i = 0; i < CameraWatch.ToReturn - 1; i++)
        {
            changed |= watch.Feed(false, false);
        }
        Check(!changed && watch.StoodDown, "camera: ...so the camera does not come back early");
        Check(watch.Feed(false, false) && !watch.StoodDown, "camera: after the full count of clean frames it comes back, and says so once");

        // Odd single frames never add up.
        watch.Reset();
        for (int i = 0; i < 5000; i++)
        {
            changed |= watch.Feed(i % 3 == 0, false);
        }
        Check(!changed && !watch.StoodDown, "camera: a frame now and then (every third, for 5,000 frames) never stands the camera down");
        watch.Reset();
        for (int i = 0; i < 5000; i++)
        {
            changed |= watch.Feed(i % (CameraWatch.ToYield) != 0, false);   // eleven foreign, one clean, over and over
        }
        Check(!changed && !watch.StoodDown, "camera: runs one frame short of the count, however many, do not stand it down either");

        // A known camera mod: sooner.
        watch.Reset();
        for (int i = 0; i < CameraWatch.ToYieldKnown - 1; i++)
        {
            changed |= watch.Feed(true, true);
        }
        Check(!changed && watch.Feed(true, true) && watch.StoodDown, "camera: with a known camera mod installed it stands down after the shorter count");

        // Two cameras never take turns: between standing down and coming back there are at least ToReturn frames, whatever is fed.
        watch.Reset();
        var random = new Random(11);
        int last = -1000000, shortest = int.MaxValue, turns = 0;
        bool down = false;
        for (int frame = 0; frame < 200000; frame++)
        {
            bool burst = (frame / 137) % 2 == 0;
            if (watch.Feed(burst ? random.Next(10) < 9 : random.Next(50) == 0, frame % 2 == 0))
            {
                turns++;
                if (down && !watch.StoodDown)
                {
                    shortest = Math.Min(shortest, frame - last);
                }
                if (!down && watch.StoodDown)
                {
                    last = frame;
                }
                down = watch.StoodDown;
            }
        }
        Check(turns > 0 && shortest >= CameraWatch.ToReturn, "camera: over 200,000 made-up frames the camera never comes back sooner than the clean count after it stood down (shortest " + shortest + ", " + turns + " changes)");
        Check(CameraWatch.ToReturn >= CameraWatch.ToYield * 4 && CameraWatch.ToYieldKnown >= 2, "camera: coming back takes several times longer than standing down, and never one frame");

        // The largest count does not wrap.
        watch.Reset();
        watch.Feed(true, false);
        var held = watch;
        for (int i = 0; i < 100; i++)
        {
            held.Feed(true, false);
        }
        Check(held.StoodDown && held.Foreign == 101, "camera: the count goes on while the other mod has the camera");
        watch.Reset();
        Check(!watch.StoodDown && watch.Foreign == 0 && watch.Clean == 0, "camera: a new city starts from nothing");
    }

    private static void ModNames()
    {
        Check(OtherModName.Is("Immersion", "Immersion") && OtherModName.Is("Immersion-1.2.0-test8", "Immersion") && OtherModName.Is("immersion_2", "Immersion") && OtherModName.Is("VoogleRoute", "VoogleRoute"),
            "other mods: an assembly named as the mod, with or without a version after it, is the mod");
        Check(!OtherModName.Is("ImmersionTools", "Immersion") && !OtherModName.Is("Immersio", "Immersion") && !OtherModName.Is("MyImmersion", "Immersion") && !OtherModName.Is("VoogleRouteExtras", "VoogleRoute"),
            "other mods: a longer word, a shorter one, or the name inside another is not the mod");
        Check(!OtherModName.Is(null, "Immersion") && !OtherModName.Is("", "Immersion") && !OtherModName.Is("Immersion", null) && !OtherModName.Is("Immersion", ""),
            "other mods: no name is no mod");
    }

    private static void PolishTiming()
    {
        // The frame's routine while a panel is up, and the measurement: a million of each must be nothing.
        var clock = Stopwatch.StartNew();
        int due = 0;
        float sum = 0f;
        for (int i = 0; i < 1000000; i++)
        {
            float moved = (i & 63) * 0.5f;
            if (PanelPlacement.Due(true, (i & 1023) != 0, moved, 0f))
            {
                due++;
                PanelPlacement.Place(true, 760f, 400f, 1160f, 700f, 780f + (i & 127), 450f, 1080f + (i & 127), 650f, ScreenW, ScreenH, PanelSide.Home, out float dx, out float dy);
                sum += dx + dy;
            }
        }
        var watch = new CameraWatch();
        int writes = 0;
        float shown = -1f;
        for (int i = 0; i < 1000000; i++)
        {
            watch.Feed((i & 7) == 0, false);
            float value = (i % 1000) / 1000f;
            if (BarRule.Due(shown, value))
            {
                shown = value;
                writes++;
            }
        }
        clock.Stop();
        Check(clock.Elapsed.TotalMilliseconds < 400.0 && due > 0 && writes > 0 && !float.IsNaN(sum), "polish: a million frames of the three rules take " + clock.Elapsed.TotalMilliseconds.ToString("F0") + " ms (well under a microsecond a frame)");
    }
}
