using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;

// The rival tycoons' businesses (RivalFirms): they hire and let go through the staff rules and lease and renew through the lease rules, the very rules the player's
// company runs by, in the rivals' own day. What they pay comes off the tycoon's worth and the rises their landlords ask feed the trend the player's landlords ask by.
internal static partial class Program
{
    private static RivalFile RivalFirmFile(int seed, int tycoon, int moves)
    {
        var f = new RivalFile { Seed = seed };
        f.Tycoons[tycoon].ActiveDay = 1;
        f.Tycoons[tycoon].Worth = 5000000;
        for (int i = 1; i <= moves; i++)
        {
            f.Moves.Add(new RivalMove { Id = i, Tycoon = tycoon, Kind = RivalMoveKind.Outlet, State = RivalMoveState.Running, Land = 90 + i, End = 160 + i, District = "Midtown", Type = "restaurant", Place = "Main~" + i });
        }
        return f;
    }

    private static void RivalFirmChecks()
    {
        int before = _run, failedBefore = _failed;
        RivalFile f = RivalFirmFile(777, 0, 1);
        int opened = RivalFirms.Sync(f, f.Seed, 100, 30, 100);
        RivalOutlet o = f.Outlets.Count > 0 ? f.Outlets[0] : null;
        Check(opened == 1 && o != null && o.Crew.Count >= RivalFirms.CrewStart && o.Crew.Count <= RivalFirms.CrewStart + 2 && o.Hires == o.Crew.Count && o.Lease != null && o.Lease.Start == 100,
            "rival firms: an outlet that has come opens with a lease on its premises (LeaseRules.Begin) and a crew of three to five hired (StaffRules.Hire)");
        bool made = o != null && o.Crew.TrueForAll(p => p.Standing == CompanyCatalogue.StandingStart && p.AskDay == 0 && p.LateDays == 0 && p.Wage > 0 && p.Going > 0 && p.Place == RivalFirms.Key(o));
        Check(made && o.Lease.Term >= 6 * 30 && o.Lease.Place == RivalFirms.Key(o) && RivalFirms.Sync(f, f.Seed, 101, 30, 100) == 0 && f.Outlets.Count == 1, "rival firms: every hire is a staff record at the starting standing and the outlet opens once, not every day");

        // Twelve hundred days: the crew is late and is let go and replaced, somebody underpaid asks and is answered, the lease runs to its end and renews or is held over.
        var feed = new List<string>();
        long worth = f.Tycoons[0].Worth, paid = 0;
        for (int day = 100; day < 1300; day++)
        {
            paid += RivalFirms.Day(f, f.Seed, day, 30, 20, 100 + (day > 400 ? 12 : 0), (district, rise) => feed.Add(district + ":" + rise));
        }
        Check(o.LetGo >= 1 && o.Hires - o.LetGo == o.Crew.Count && o.Crew.Count >= RivalFirms.CrewStart && o.Crew.Count <= RivalFirms.CrewMost,
            "rival firms: somebody late three days running is let go (StaffRules.LetGo, ManagerRules.LetGo) and the place filled again (" + o.Hires + " hired, " + o.LetGo + " let go, " + o.Crew.Count + " on the crew)");
        Check(o.Raises >= 1, "rival firms: somebody underpaid asks for a raise (StaffRules.Day) and the firm answers it as a manager does (" + o.Raises + " raises agreed)");
        Check(o.Renewals >= 1 && feed.Count == o.Renewals && feed.TrueForAll(x => x.StartsWith("Midtown:", StringComparison.Ordinal)),
            "rival firms: the lease runs by the lease rules, the landlord's offer comes before the end and is answered, and each renewal is told to the company with its district (" + o.Renewals + " renewals, " + o.Holds + " held over)");
        Check(o.Rent > 0 && o.Wages > 0 && f.Tycoons[0].Worth == worth - paid && paid == o.Rent + o.Wages, "rival firms: what they pay comes off the tycoon's worth (" + paid + " in rent and wages over the days)");
        Check(o.Lease.Renewals == o.Renewals - o.Holds, "rival firms: the outlet's counts are the lease's own");

        // The same seed lives the same days; the save keeps the outlet, its lease and its crew.
        RivalFile twin = RivalFirmFile(777, 0, 1);
        RivalFirms.Sync(twin, twin.Seed, 100, 30, 100);
        for (int day = 100; day < 1300; day++)
        {
            RivalFirms.Day(twin, twin.Seed, day, 30, 20, 100 + (day > 400 ? 12 : 0), null);
        }
        string line = RivalStore.Write(f);
        RivalFile back = RivalStore.Read(line, out int version);
        Check(RivalStore.Write(twin) == line, "rival firms: the same seed lives the same twelve hundred days, outlet for outlet");
        RivalOutlet again = back != null && back.Outlets.Count == 1 ? back.Outlets[0] : null;
        Check(again != null && RivalStore.Write(back) == line && again.Crew.Count == o.Crew.Count && again.Lease.End == o.Lease.End && again.Lease.Rise == o.Lease.Rise && again.Hires == o.Hires && again.Rent == o.Rent,
            "rival firms: the save keeps the outlet, its lease and its crew, and writing what was read gives the same line");
        string withoutOutlets = line.Substring(0, line.IndexOf("\nO|", StringComparison.Ordinal));
        RivalFile old = RivalStore.Read(withoutOutlets, out version);
        Check(old != null && old.Outlets.Count == 0 && old.Tycoons[0].Worth == f.Tycoons[0].Worth, "rival firms: a save from before the rivals' businesses loads with none, and the outlet opens again when a move is running");

        // Four outlets at most for one tycoon: the oldest closes and lets its crew go; a tycoon out of play closes all.
        RivalFile many = RivalFirmFile(31, 1, 6);
        RivalFirms.Sync(many, many.Seed, 120, 30, 100);
        RivalOutlet first = many.Outlets.Count > 0 ? many.Outlets[0] : null;
        Check(many.Outlets.Count == RivalFirms.OutletsMost && first != null && first.Move >= 3, "rival firms: a tycoon keeps four outlets at most: the oldest closes (" + many.Outlets.Count + " open)");
        var kept = new List<StaffFile>(first.Crew);
        many.Tycoons[1].DefeatedDay = 130;
        RivalFirms.Sync(many, many.Seed, 131, 30, 100);
        Check(many.Outlets.Count == 0 && kept.TrueForAll(p => p.LateDays == 0 && p.AskDay == 0), "rival firms: a tycoon out of play closes every outlet and lets the crew go");
        Console.WriteLine("  the rivals' businesses: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }
}
