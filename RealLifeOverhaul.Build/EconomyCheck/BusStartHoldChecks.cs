using System;
using RealLifeOverhaul;

// A bus held at its start to keep the timetable stands only while nobody can see it (Transport/RideRules.StartHold; the
// owner, 21 Oct 2026: "The bus is still stuck", the log: "it is held at its start, out of sight, to keep the timetable (6 s
// more); seen: yes, 59 m from the player"). On a model of the director's four ticks a second, with the frames' lateness:
// seen at any moment of its hold it drives on within a second; never seen, it holds to the end; and over the sources the bus
// looks while it holds, drives on when seen, and the director makes its promise that much sooner.
internal static partial class Program
{
    private const float BusTick = 0.25f;   // seconds: TransitDirector.TickInterval

    private static void BusStartHold()
    {
        // ---- seen at any moment of an 11 s hold (the owner's M9), on ticks four times a second, some of them a frame late.
        float worst = 0f;
        bool all = true, cutRight = true;
        for (float seenAfter = 0f; seenAfter < 11f; seenAfter += 0.05f)
        {
            var hold = new RideRules.StartHold();
            const float put = 100f;
            hold.Set(put, 11f);
            float movesAt = -1f;
            int frame = 0;
            for (float now = put; now < put + 20f; now += BusTick + (frame++ % 3 == 0 ? 0.04f : 0f))
            {
                bool seen = now >= put + seenAfter;
                if (!Stands(hold, now, seen))
                {
                    movesAt = now;
                    break;
                }
            }
            float waited = movesAt - (put + seenAfter);
            all &= movesAt > 0f && waited >= 0f;
            worst = Math.Max(worst, waited);
            cutRight &= movesAt >= put + 11f ? hold.Cut == 0f : Math.Abs(hold.Cut - (put + 11f - movesAt)) < 0.001f && hold.Cut > 0f;
        }
        Check(all && worst <= 1f, "bus start hold: a held bus that comes into view drives on within a second, whenever in its hold (at worst "
                                  + worst.ToString("F2") + " s, a look every " + RideRules.StartLookEvery + " s on ticks of " + BusTick + " s)");
        Check(cutRight, "bus start hold: what is let go is the rest of the hold, so the bus is early at its stop by exactly that");

        // ---- never seen: it holds to the end, looked at no more than twice a second.
        var unseen = new RideRules.StartHold();
        unseen.Set(50f, 11f);
        int looks = 0;
        float goes = -1f;
        bool counting = true;
        float lastLeft = float.MaxValue;
        for (float now = 50f; now < 70f; now += BusTick)
        {
            if (unseen.Holds(now))
            {
                float left = unseen.Left(now);
                counting &= left <= lastLeft && left > 0f;
                lastLeft = left;
                if (unseen.LookDue(now))
                {
                    looks++;
                    unseen.Looked(now, false);
                }
                continue;
            }
            goes = now;
            break;
        }
        Check(goes >= 61f && goes < 61f + BusTick + 0.001f && unseen.Cut == 0f && unseen.Left(goes) == 0f,
            "bus start hold: out of sight it stands its whole hold and sets off on the tick after it ends, nothing let go");
        Check(counting, "bus start hold: the hold left counts down while it stands");
        Check(looks >= 20 && looks <= 23, "bus start hold: out of sight it is looked at every half second while it holds, no more (" + looks + " looks in 11 s)");

        // ---- seen as it is put (the test button may put one in view): it never stands.
        var atOnce = new RideRules.StartHold();
        atOnce.Set(10f, 8f);
        Check(!Stands(atOnce, 10f, true) && Math.Abs(atOnce.Cut - 8f) < 0.001f, "bus start hold: a bus seen where it is put drives off on its first tick, all 8 s let go");

        // ---- seen only after the hold has ended: nothing let go; a look does nothing once it is over.
        var late = new RideRules.StartHold();
        late.Set(0f, 5f);
        Check(!late.Holds(5f) && !late.LookDue(5f) && !late.Looked(6f, true) && late.Cut == 0f, "bus start hold: seen after its hold ended, nothing is let go");

        // ---- no hold, and a new bus: nothing held, nothing carried over.
        var none = new RideRules.StartHold();
        none.Set(3f, 0f);
        Check(!none.Holds(3f) && none.Left(3f) == 0f && !none.LookDue(3f), "bus start hold: a bus with no hold is never held");
        late.Set(20f, 6f);
        late.Looked(21f, true);
        late.Clear();
        Check(!late.Holds(21f) && late.Cut == 0f && late.Until == 0f, "bus start hold: a bus put on the road again carries nothing of the last hold");
        late.Set(30f, 6f);
        Check(late.Cut == 0f && late.Holds(30f) && late.LookDue(30f), "bus start hold: a new hold starts with nothing let go and a look at once");

        // ---- over the sources.
        string scripts = System.IO.Path.Combine(LocalesFolder(), "..", "Scripts");
        string bus = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Transport", "TransitBus.cs"));
        string held = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Transport", "TransitBus.Held.cs"));
        string start = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Transport", "TransitBus.Start.cs"));
        string buses = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Transport", "TransitDirector.Buses.cs"));
        int branch = bus.IndexOf("if (State == Phase.Approaching && _startHold.Holds(now) && _driver.Active)", StringComparison.Ordinal);
        string inBranch = branch >= 0 ? bus.Substring(branch, Math.Min(900, bus.Length - branch)) : string.Empty;
        Check(inBranch.Contains("if (!_startHold.LookDue(now) || !_startHold.Looked(now, InView))") && inBranch.Contains("HoldLetGo();")
              && inBranch.IndexOf("_driver.CruiseSpeed = 0f;", StringComparison.Ordinal) < inBranch.IndexOf("HoldLetGo();", StringComparison.Ordinal),
            "bus start hold source: while it holds the bus is looked at by the rule, stands only while unseen, and drives on from the tick it is seen");
        Check(!bus.Contains("_holdUntil") && !held.Contains("_holdUntil") && !start.Contains("_holdUntil") && held.Contains("!_startHold.Holds(now)")
              && held.Contains("_startHold.Holds(Time.time)") && start.Contains("_startHold.Clear();") && bus.Contains("_startHold.Set(Time.time, seconds);"),
            "bus start hold source: every reading of the hold goes through the one rule, cleared when a bus is put on the road");
        Check(held.Contains("Held0 = Mathf.Max(0f, Held0 - cut);") && buses.Contains("_bus.TakeHoldCut(out float early)") && buses.Contains("_promisedAt -= early;")
              && buses.Contains("_next.TakeHoldCut(out _);"),
            "bus start hold source: a hold let go counts only what it stood, and the director's promise comes that much sooner");
    }

    // One tick of the bus as TransitBus.Drive has it: true while it stands at its start.
    private static bool Stands(RideRules.StartHold hold, float now, bool seen)
    {
        if (!hold.Holds(now))
        {
            return false;
        }
        return !hold.LookDue(now) || !hold.Looked(now, seen);
    }
}
