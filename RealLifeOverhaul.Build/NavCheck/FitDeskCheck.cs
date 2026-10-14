using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.FitOut;
using RealLifeOverhaul.Tech;

// The presets of a working day's desk (FitOut/FitPlanner.Zones.cs: FitKind.OfficeRow and after), for a job whose employer's
// building has no station of the game's that suits it: one room of its kind on the floor a furnished building leaves free,
// round the game's furniture. Each preset: the room it is, its desks with a computer and a chair, nothing within a walkway of
// the game's furniture, every way still open, no lamps of its own, the same plan every time; and the player's desk picked
// among a floor's desks, the same every visit.
internal static partial class Program
{
    // A hospital's floor of 20 x 16 m as the game furnishes it: beds along both side walls, the reception counter, waiting seats by the door.
    private static RoomGrid FurnishedFloor()
    {
        RoomGrid g = FitFloor(20f, 16f);
        Furnish(g, 0.3f, 9f, 2.6f, 15.7f);     // beds along the left wall
        Furnish(g, 17.4f, 9f, 19.7f, 15.7f);   // ...and the right
        Furnish(g, 7f, 5f, 13f, 5.9f);         // the reception counter
        Furnish(g, 0.3f, 1.5f, 4.5f, 2.4f);    // waiting seats
        return g;
    }

    // --fit: each preset drawn in the furnished room (the game's furniture is the blank cells).
    private static void DrawPresets()
    {
        foreach (FitKind kind in new[] { FitKind.OfficeRow, FitKind.Ward, FitKind.Classroom, FitKind.ServiceDesk, FitKind.BackOffice })
        {
            RoomGrid g = FurnishedFloor();
            FitSite site = FitSiteAt(kind, 10f, 0.35f, 0f, 1f, seed: 31);
            var plan = new FitPlan();
            FitPlanner.Plan(g, site, null, plan);
            DrawFit(g, site, plan, "the preset " + kind + " in a furnished room of 20 x 16 m: " + plan.Kept + " pieces");
        }
    }

    private static void FitPresets()
    {
        double slowest = 0;
        var kinds = new[]
        {
            (FitKind.OfficeRow, FitPlanner.ZoneType.Desks, 2, FitPlanner.RowDesks),
            (FitKind.Ward, FitPlanner.ZoneType.Nurses, 2, 2),
            (FitKind.Classroom, FitPlanner.ZoneType.Teacher, 1, 1),
            (FitKind.ServiceDesk, FitPlanner.ZoneType.Service, 1, 2),
            (FitKind.BackOffice, FitPlanner.ZoneType.BackDesk, 1, 1)
        };
        foreach (var (kind, zone, least, most) in kinds)
        {
            string at = "fit preset: " + kind + " in a furnished room: ";
            Check(FitPlanner.IsPreset(kind), at + "a preset, not a whole floor");
            RoomGrid g = FurnishedFloor();
            FitSite site = FitSiteAt(kind, 10f, 0.35f, 0f, 1f, seed: 31);
            site.ZoneLamps = false;
            var plan = new FitPlan();
            var watch = Stopwatch.StartNew();
            FitPlanner.Plan(g, site, null, plan);
            slowest = Math.Max(slowest, watch.Elapsed.TotalMilliseconds);
            FitKeeps(g, site, plan, at);
            Check(plan.Zones == 1 && FitZones(plan, zone) == 1, at + "one room of its own kind and nothing more (" + plan.Zones + " rooms)");
            int desks = FitCount(plan, "officedesk1");
            Check(desks >= least && desks <= most && plan.Seats(FitUse.Desk) == desks, at + "its desks, each with its chair (" + desks + ")");
            Check(FitCount(plan, "computer") == desks && FitCount(plan, "computermonitor") == desks, at + "a computer and a monitor on every desk");
            var known = new HashSet<string>(FitPlanner.Pieces(kind));
            string stranger = null;
            foreach (FitPiece p in plan.Pieces)
            {
                if (p.Kept && !known.Contains(p.Prefab))
                {
                    stranger = p.Prefab;
                }
            }
            Check(stranger == null, at + "only the game's own furniture of the preset (" + stranger + ")");
            Check(!plan.HasLamps, at + "no lamps of its own: the game has lit the room");
            var near = new bool[g.Count];
            g.NearFurniture(FitPlanner.FromFurniture - 0.05f, near);
            bool apart = true;
            for (int i = 0; i < g.Count; i++)
            {
                g.Centre(i, out float x, out float z);
                foreach (FitPiece p in plan.Pieces)
                {
                    apart &= !(near[i] && Standing(p) && p.Contains(x, z, -0.01f));
                }
            }
            Check(apart, at + "nothing within a walkway of the game's furniture (its beds, its counter, its seats)");
            var again = new FitPlan();
            FitPlanner.Plan(FurnishedFloor(), site, null, again);
            bool same = again.Pieces.Count == plan.Pieces.Count;
            for (int i = 0; same && i < plan.Pieces.Count; i++)
            {
                same = again.Pieces[i].Prefab == plan.Pieces[i].Prefab && again.Pieces[i].X == plan.Pieces[i].X && again.Pieces[i].Z == plan.Pieces[i].Z && again.Pieces[i].Kept == plan.Pieces[i].Kept;
            }
            Check(same, at + "the same room gives the same desk every visit");
            if (kind == FitKind.Ward)
            {
                Check(FitCount(plan, "smallcabinet") + FitCount(plan, "bigcabinet") >= 1, at + "the medical cabinets beside the desks");
            }
            if (kind == FitKind.ServiceDesk)
            {
                Check(plan.Seats(FitUse.Waiting) == desks, at + "a visitor's chair across each desk");
                Check(FitCount(plan, "officelockers") + FitCount(plan, "bigcabinet") >= 1, at + "the filing along the wall");
            }
            if (kind == FitKind.BackOffice || kind == FitKind.Classroom)
            {
                Check(FitCount(plan, "smallcabinet") == 1, at + "a cabinet beside the desk");
            }
        }

        // Too small a room is left as it is: no preset is forced in.
        {
            RoomGrid g = FitFloor(3.6f, 3.6f);
            FitSite site = FitSiteAt(FitKind.Ward, 1.8f, 0.35f, 0f, 1f);
            var plan = new FitPlan();
            FitPlanner.Plan(g, site, null, plan);
            Check(plan.Why == null && plan.Zones == 0 && plan.Kept == 0, "fit preset: a room of 3.6 m square is left as it is (" + plan.Kept + " pieces)");
        }

        // The player's desk among a floor's desks: one of them, the same every visit, spread over them by the seed.
        bool inRange = true, stable = true;
        var used = new HashSet<int>();
        for (int seats = 1; seats <= 30; seats++)
        {
            for (int seed = 0; seed < 40; seed++)
            {
                int pick = FitPlanner.PickDesk(seats, seed * 7919);
                inRange &= pick >= 0 && pick < seats;
                stable &= pick == FitPlanner.PickDesk(seats, seed * 7919);
                if (seats == 12)
                {
                    used.Add(pick);
                }
            }
        }
        Check(inRange && stable && FitPlanner.PickDesk(0, 5) == -1, "fit preset: the player's desk is one of the floor's, the same every visit; none on a floor without desks");
        Check(used.Count >= 10, "fit preset: different jobs get different desks of a floor (" + used.Count + " of 12)");

        // Colleagues at an office's row; the player's desk alone elsewhere.
        Check(FitCrew.For(FitKind.OfficeRow, 3, 0, 0, 0, 0, 9).AtDesks == 3 && FitCrew.For(FitKind.Ward, 1, 0, 0, 0, 0, 9).AtDesks == 1
              && FitCrew.For(FitKind.Classroom, 0, 0, 0, 0, 0, 9).Total == 0 && FitCrew.For(FitKind.BackOffice, 0, 0, 0, 0, 0, 9).Total == 0,
            "fit preset: colleagues at an office's other desks and at the nurses' other desk; nobody at a teacher's or a back office desk");
        Check(slowest < 1500, "fit preset: a preset is planned in under a second and a half here (" + slowest.ToString("F0") + " ms at most); once a visit, off the main thread");
    }
}
