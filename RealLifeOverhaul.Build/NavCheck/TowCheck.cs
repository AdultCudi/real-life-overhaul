using System;
using System.Diagnostics;
using RealLifeOverhaul;

// The tow truck's game-free rules (Traffic/Tow/TowRules.cs): the bed's motion, a car on the way up it and carried on it,
// the pull along the road to its foot, the stretch of road the truck takes to stand ahead of a wreck, the time it is said
// to take to come, and when it may be put on the road or taken off it.
internal static partial class Program
{
    private static void Tow()
    {
        // ---- the bed: at rest, down, and everything between without a jump.
        TowRules.DeckPose(0f, out float tilt0, out float slide0);
        TowRules.DeckPose(1f, out float tilt1, out float slide1);
        Check(tilt0 == 0f && slide0 == 0f, "tow: at rest the bed is neither turned nor slid");
        Check(Near(tilt1, TowRules.TiltMost, 0.0001f) && Near(slide1, TowRules.SlideMost, 0.0001f), "tow: down, it is turned " + TowRules.TiltMost + " degrees and slid " + TowRules.SlideMost.ToString("F2") + " m");
        Check(TowRules.SlideMost > 3f && TowRules.SlideMost < 4.5f, "tow: the slide down is that of a rollback (3 to 4.5 m): " + TowRules.SlideMost.ToString("F2"));
        TowRules.TailDown(out float tailUp, out float tailAhead);
        Check(Near(tailUp, TowRules.TailOverRoad, 0.001f), "tow: with the bed down its tail lies " + TowRules.TailOverRoad + " m over the road: " + tailUp.ToString("F4"));
        Check(tailAhead < TowRules.BedTail - 3f, "tow: the tail comes down well behind the truck: " + tailAhead.ToString("F2") + " m");
        float worstTilt = 0f, worstSlide = 0f, lowest = float.MaxValue;
        bool rising = true;
        float lastTilt = 0f, lastSlide = 0f;
        const int steps = 2000;
        float length = TowRules.HeadBoard - TowRules.BedTail;
        for (int i = 1; i <= steps; i++)
        {
            TowRules.DeckPose(i / (float)steps, out float tilt, out float slide);
            worstTilt = Math.Max(worstTilt, Math.Abs(tilt - lastTilt));
            worstSlide = Math.Max(worstSlide, Math.Abs(slide - lastSlide));
            rising &= tilt >= lastTilt - 1e-5f && slide >= lastSlide - 1e-5f;
            lastTilt = tilt;
            lastSlide = slide;
            for (int k = 0; k <= 12; k++)
            {
                TowRules.BedPoint(length * k / 12f, tilt, slide, out float up, out _);
                lowest = Math.Min(lowest, up);
            }
        }
        Check(worstTilt < 0.05f && worstSlide < 0.01f, "tow: no jump in the bed's motion (most per 1/2000: " + worstTilt.ToString("F4") + " degrees, " + worstSlide.ToString("F4") + " m)");
        Check(rising, "tow: going down the bed only ever turns further and slides further");
        Check(lowest >= TowRules.TailOverRoad - 0.001f, "tow: no part of the bed's top goes under the road on the way down: lowest " + lowest.ToString("F4") + " m");
        TowRules.DeckPose(0.002f, out float tiltStart, out _);
        TowRules.DeckPose(0.998f, out _, out float slideEnd);
        Check(tiltStart < 0.01f && TowRules.SlideMost - slideEnd < 0.001f, "tow: it starts and stops gently (no jolt at either end)");
        // The bed's front, down, still lies on the subframe ahead of its pivot.
        TowRules.BedPoint(length, TowRules.TiltMost, TowRules.SlideMost, out _, out float frontAhead);
        Check(frontAhead - TowRules.PivotAhead > 1f, "tow: down, the bed's front is still on the subframe, " + (frontAhead - TowRules.PivotAhead).ToString("F2") + " m ahead of its pivot");
        TowRules.BedPoint(2f, 0f, 0f, out float restUp, out float restAhead);
        Check(Near(restUp, TowRules.BedTop, 0.0001f) && Near(restAhead, TowRules.BedTail + 2f, 0.0001f), "tow: at rest a point of the bed is where the model has it");

        // ---- a car going up the bed: from the foot to its place, without a jump, pitching up and levelling with the bed.
        foreach (float half in new[] { 1.8f, 2.25f, 2.8f })
        {
            float foot = TowRules.FootAlong(half), rest = TowRules.RestAlong(half);
            TowRules.CarOnBed(foot, half, out float fu, out float fa, out float fp);
            Check(Near(fu, 0f, 0.0001f) && Near(fp, 0f, 0.001f) && Near(fa, TowRules.FootAhead(half), 0.001f),
                "tow: a car " + (2 * half).ToString("F1") + " m long at the foot is level on the road, where FootAhead says");
            TowRules.CarOnBed(rest, half, out float ru, out float ra, out float rp);
            TowRules.BedPoint(rest, TowRules.TiltMost, TowRules.SlideMost, out float bu, out float ba);
            Check(Near(rp, TowRules.TiltMost, 0.01f) && Near(ru, bu, 0.002f) && Near(ra, ba, 0.002f),
                "tow: up the bed it lies on the bed's top at the bed's slope (" + rp.ToString("F2") + " degrees): carried with the bed from there, nothing jumps");
            Check(rest + half <= length - TowRules.HeadGap + 0.0001f && rest - half > 0f, "tow: a car " + (2 * half).ToString("F1") + " m long fits between the tail and the headboard");
            float worstPlace = 0f, worstPitch = 0f;
            float lu = fu, la = fa, lp = fp;
            const int climb = 4000;
            for (int i = 1; i <= climb; i++)
            {
                TowRules.CarOnBed(foot + (rest - foot) * i / climb, half, out float u, out float a, out float p);
                worstPlace = Math.Max(worstPlace, Math.Max(Math.Abs(u - lu), Math.Abs(a - la)));
                worstPitch = Math.Max(worstPitch, Math.Abs(p - lp));
                lu = u;
                la = a;
                lp = p;
            }
            Check(worstPlace < 0.005f && worstPitch < 0.05f, "tow: up the bed no jump for a car " + (2 * half).ToString("F1") + " m long (most per step " + worstPlace.ToString("F4") + " m, " + worstPitch.ToString("F3") + " degrees)");
        }
        Check(TowRules.Fits(4.5f, 1.8f) && TowRules.Fits(5.6f, 2.3f), "tow: a car or a small van goes on the bed");
        Check(!TowRules.Fits(5.7f, 1.8f) && !TowRules.Fits(4.5f, 2.4f) && !TowRules.Fits(0f, 0f), "tow: a truck, a bus or nothing at all does not");

        // ---- the pull along the road.
        TowRules.Drag(0f, 1.5f, -14f, 25f, 0f, -11f, 0f, out float x0, out float z0, out float y0);
        TowRules.Drag(1f, 1.5f, -14f, 25f, 0f, -11f, 0f, out float x1, out float z1, out float y1);
        Check(Near(x0, 1.5f) && Near(z0, -14f) && Near(y0, 25f), "tow: the pull starts exactly where the car stands");
        Check(Near(x1, 0f) && Near(z1, -11f) && Near(y1, 0f), "tow: it ends at the foot, along the truck");
        TowRules.Drag(0.999f, 1.5f, -14f, 25f, 0f, -11f, 0f, out float xe, out float ze, out _);
        Check(Math.Abs(xe - x1) < Math.Abs(ze - z1) * 0.05f + 1e-4f, "tow: it arrives along the truck, not sideways");
        float worstDrag = 0f, lx = x0, lz = z0;
        for (int i = 1; i <= 1000; i++)
        {
            TowRules.Drag(i / 1000f, 1.5f, -14f, 25f, 0f, -11f, 0f, out float x, out float z, out _);
            worstDrag = Math.Max(worstDrag, (float)Math.Sqrt((x - lx) * (x - lx) + (z - lz) * (z - lz)));
            lx = x;
            lz = z;
        }
        Check(worstDrag < 0.02f, "tow: no jump along the pull (most per 1/1000: " + worstDrag.ToString("F4") + " m)");
        TowRules.Drag(0.5f, 0f, -14f, 170f, 0f, -11f, 180f, out _, out _, out float yawHalf);
        Check(yawHalf >= 170f && yawHalf <= 180f, "tow: a car facing nearly backwards turns the short way to tail first: " + yawHalf.ToString("F1"));
        Check(TowRules.LoadHeading(10f) == 0f && TowRules.LoadHeading(-80f) == 0f && TowRules.LoadHeading(170f) == 180f && TowRules.LoadHeading(-100f) == 180f,
            "tow: a car goes on nose first or tail first, whichever is the smaller turn");
        Check(Near(TowRules.DeltaAngle(350f, 10f), 20f) && Near(TowRules.DeltaAngle(10f, 350f), -20f), "tow: turns are taken the short way round");

        // ---- where it stands: past the wreck, over, and ahead of it with the foot ahead of the wreck.
        foreach (float across in new[] { 3.0f, -3.3f, 6.0f })
        {
            TowRules.Stretch(0f, 2.3f, across, 2.25f, out float from, out float to, out float stand);
            float footAt = stand - TowRules.AxleFront + TowRules.FootAhead(2.25f);
            Check(from - (TowRules.AxleFront - TowRules.AxleRear) >= 2.3f + TowRules.PassClear - 0.001f, "tow: it moves over only once its rear axle is past the wreck (" + across + " m across)");
            Check(to - from >= TowRules.MergeLeast && to - from >= Math.Abs(across) * TowRules.MergePerMetre - 0.001f, "tow: it moves over gently: " + (to - from).ToString("F1") + " m for " + Math.Abs(across) + " m across");
            Check(stand >= to + TowRules.StandAfter - 0.001f, "tow: it is wholly in the wreck's lane before it stands");
            Check(footAt >= TowRules.DragLeast - 0.001f && footAt < 30f, "tow: the foot of its bed is ahead of the wreck, the car drawn forward " + footAt.ToString("F1") + " m");
        }
        Check(TowRules.Across(5f, 5f, 17f) == 0f && TowRules.Across(17f, 5f, 17f) == 1f && Near(TowRules.Across(11f, 5f, 17f), 0.5f), "tow: the move over runs smoothly from the lane beside to the wreck's");

        // ---- how long it takes, as the notice says it.
        Check(Near(TowRules.ArrivalSeconds(20f, 150f), 20f + 150f / TowRules.ApproachPace + TowRules.StandSeconds), "tow: the wait is the hold, the road at its pace, and the time to stand");
        Check(TowRules.Minutes(0f, 1f) == 1 && TowRules.Minutes(59.2f, 1f) == 60 && TowRules.Minutes(30f, 2f) == 60 && TowRules.Minutes(30f, 0f) == 30,
            "tow: minutes by the game's clock, rounded up, never under one, an unreadable speed taken as 1");
        float least = TowRules.ArrivalSeconds(TowRules.HoldLeast, 70f), most = TowRules.ArrivalSeconds(TowRules.HoldMost, 400f);
        Check(least >= 20f && most <= 120f, "tow: it comes in a sensible time, " + least.ToString("F0") + " to " + most.ToString("F0") + " s (as many game minutes)");

        // ---- on the road and off it.
        Check(!TowRules.OutOfSight(true, 300f, 60f), "tow: never put on or taken off the road in view");
        Check(!TowRules.OutOfSight(false, 59.9f, 60f) && TowRules.OutOfSight(false, 60f, 60f), "tow: never within 60 m of the player, seen or not");
        Check(TowRules.WorthSending(380f, 380f) && !TowRules.WorthSending(380.5f, 380f), "tow: a wreck beyond where the street is drawn is not sent a truck");

        // ---- cost: a frame's worth of work is nothing.
        var watch = Stopwatch.StartNew();
        float sink = 0f;
        for (int i = 0; i < 100000; i++)
        {
            TowRules.DeckPose((i % 1000) / 1000f, out float t, out float s);
            TowRules.CarOnBed(-2f + (i % 600) / 100f, 2.25f, out float u, out float a, out float p);
            sink += t + s + u + a + p;
        }
        watch.Stop();
        Check(watch.ElapsedMilliseconds < 200 && !float.IsNaN(sink), "tow: 100 000 poses of the bed and a car take " + watch.ElapsedMilliseconds + " ms");
    }
}
