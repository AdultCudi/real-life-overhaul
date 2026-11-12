using System;
using RealLifeOverhaul.Construction;

// The Construction app's new rules (Scripts/Construction: LevelRules, AskRules, QuoteRules, BuildState.Kit, BuildStore.Kit): the builder's licence by floors and the city's builders
// on the same ladder, the land's monthly asks and the change on the month, the quotes' risk and the rent a wait costs, the save's part (written, read back, an old line read), and
// the months a company and a city run through: a licence earned and kept, a ring that fills, rolls over and never repeats a month.
internal static partial class Program
{
    private static void ConstructionKitChecks()
    {
        LevelChecks();
        AskChecks();
        QuoteChecks();
        KitStoreChecks();
        KitRunChecks();
    }

    private static void LevelChecks()
    {
        Check(LevelRules.For(0, 100) == 0 && LevelRules.For(100, 0) == 0, "construction kit: builds alone or standing alone earn no level above the first");
        Check(LevelRules.For(2, 30) == 1 && LevelRules.For(5, 45) == 2 && LevelRules.For(9, 60) == 3 && LevelRules.For(50, 100) == LevelRules.Levels - 1,
            "construction kit: each level asks for its builds and its standing together, and the top is the last");
        bool monotone = true;
        for (int b = 0; b <= 20; b++)
        {
            for (int s = 0; s < 100; s++)
            {
                monotone &= LevelRules.For(b + 1, s) >= LevelRules.For(b, s) && LevelRules.For(b, s + 1) >= LevelRules.For(b, s);
            }
        }
        Check(monotone, "construction kit: more builds or more standing never lowers the level");
        Check(LevelRules.FloorsOf(0) == 6 && LevelRules.FloorsOf(LevelRules.Levels - 1) == BuildCatalogue.UptownHeightLimit && LevelRules.FloorsOf(-3) == 6 && LevelRules.FloorsOf(99) == 12,
            "construction kit: the first level covers six floors, the last the district's twelve, and a level out of range is the nearest");
        Check(LevelRules.Allows(1, 8) && !LevelRules.Allows(1, 9) && LevelRules.Allows(3, 12), "construction kit: a building is allowed up to its level's floors and not past them");
        bool hasNext = LevelRules.Next(0, out int builds, out int standing, out int floors);
        Check(hasNext && builds == 2 && standing == 30 && floors == 8 && !LevelRules.Next(LevelRules.Levels - 1, out _, out _, out _),
            "construction kit: the next level names its builds, its standing and its floors, and the top has none");
        bool seeded = true;
        for (int r = 0; r < BuildCatalogue.RivalCount; r++)
        {
            int level = LevelRules.Rival(0, BuildCatalogue.RivalReputation[r]);
            seeded &= level >= 1 && level <= LevelRules.Levels - 1;
        }
        Check(seeded && LevelRules.Rival(0, 0) == 0, "construction kit: the city's builders start with the level their standing earned before the save began, and an unknown one with none");
    }

    private static void AskChecks()
    {
        Check(AskRules.MonthOf(1, 360) == 0 && AskRules.MonthOf(30, 360) == 0 && AskRules.MonthOf(31, 360) == 1 && AskRules.MonthOf(361, 360) == 12,
            "construction kit: a month is a twelfth of the game's year, counted from day one");
        Check(AskRules.Mean(1000000L, 500L) == 2000L && AskRules.Mean(0L, 500L) == 0L && AskRules.Mean(1000L, 0L) == 0L,
            "construction kit: an ask a square metre is the price over the area, and none without either");
        Check(AskRules.ChangePermille(2000L, 2062L) == 31 && AskRules.ChangePermille(2000L, 1984L) == -8 && AskRules.ChangePermille(0L, 5L) == 0 && AskRules.ChangePermille(5L, 0L) == 0,
            "construction kit: the change on the month is in thousandths, and none where a month has no figure");
        var months = new int[AskRules.RingMonths];
        var asks = new long[AskRules.RingMonths];
        int count = AskRules.Put(months, asks, 0, 5, 1000L);
        count = AskRules.Put(months, asks, count, 5, 1100L);
        Check(count == 1 && asks[0] == 1100L, "construction kit: the same month again replaces its figure");
        count = AskRules.Put(months, asks, count, 4, 900L);
        Check(count == 1 && months[0] == 5, "construction kit: an earlier month than the newest is ignored");
        count = AskRules.Put(months, asks, count, 6, 1133L);
        Check(count == 2 && AskRules.Change(months, asks, count) == 30, "construction kit: the next month is added and its change read against the month before (1,100 to 1,133 is 30 in 1,000)");
        count = AskRules.Put(months, asks, count, 9, 1200L);
        Check(AskRules.Change(months, asks, count) == 0, "construction kit: months that are not neighbours show no change");
        for (int m = 10; m < 40; m++)
        {
            count = AskRules.Put(months, asks, count, m, 1000L + m);
        }
        Check(count == AskRules.RingMonths && months[0] == 28 && months[count - 1] == 39 && AskRules.Put(months, asks, count, 40, 0L) == count,
            "construction kit: the ring keeps the last twelve months and a month with no ask adds nothing");
    }

    private static void QuoteChecks()
    {
        bool order = true;
        for (int rep = 0; rep <= 100; rep += 10)
        {
            for (int busy = 0; busy < 4; busy++)
            {
                order &= QuoteRules.Risk(busy + 1, rep, 1000) >= QuoteRules.Risk(busy, rep, 1000) && QuoteRules.Risk(busy, rep, 1200) >= QuoteRules.Risk(busy, rep, 1000)
                    && QuoteRules.Risk(busy, rep, 1000) >= QuoteRules.Risk(busy, Math.Min(100, rep + 10), 1000);
            }
        }
        Check(order, "construction kit: a quote's risk never falls with a busier builder or a faster promise, and never rises with a better standing");
        Check(QuoteRules.Risk(0, 100, 1000) == QuoteRules.Low && QuoteRules.Risk(1, 0, 1000) == QuoteRules.High && QuoteRules.Risk(4, 100, 1000) >= QuoteRules.Medium,
            "construction kit: a respected, idle builder is a low risk and an unknown, busy one a high risk");
        Check(QuoteRules.RentLost(7000L, 14) == 14000L && QuoteRules.RentLost(0L, 14) == 0L && QuoteRules.RentLost(7000L, 0) == 0L,
            "construction kit: the rent a wait costs is a week's rent over seven for each day");
    }

    private static void KitStoreChecks()
    {
        var s = new BuildState();
        s.Firm.Founded = 3;
        s.Firm.Level = 2;
        s.Rivals[1].Jobs = 4;
        AskRing a = s.AskOf("reallife_district_uptown", true);
        a.Count = AskRules.Put(a.Months, a.Asks, a.Count, 7, 1500L);
        a.Count = AskRules.Put(a.Months, a.Asks, a.Count, 8, 1545L);
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out _);
        AskRing b = back != null ? back.AskOf("reallife_district_uptown", false) : null;
        Check(back != null && back.Firm.Level == 2 && back.Rivals[1].Jobs == 4 && b != null && b.Count == 2 && b.Latest == 1545L && b.Change == 30 && BuildStore.Write(back) == line,
            "construction kit: the licence level, the jobs for the player and each district's asks are written and read back, and written again as they were");
        var plain = new BuildState();
        plain.Firm.Founded = 3;
        string old = BuildStore.Write(plain);
        BuildState oldBack = BuildStore.Read(old, out _);
        Check(old.IndexOf("^a:", StringComparison.Ordinal) < 0 && oldBack != null && oldBack.Firm.Level == 0 && oldBack.Asks.Count == 0 && oldBack.Rivals[0].Jobs == 0,
            "construction kit: a company with no level and no asks writes no part, and a line from before the part loads with level 0, no asks and no jobs");
        string unknown = line.Replace("^a:", "^z:");   // a part of another version's is passed over
        BuildState passed = BuildStore.Read(unknown, out _);
        Check(passed != null && passed.Firm.Level == 0 && passed.Asks.Count == 0, "construction kit: a part this reader does not know is passed over");
    }

    // A company and a city through two years of days: the licence is earned as builds finish and the standing holds, and never lost; the ring takes one ask a month and keeps twelve.
    private static void KitRunChecks()
    {
        int level = 0, builds = 0, standing = 40, lost = 0, earned = 0;
        var months = new int[AskRules.RingMonths];
        var asks = new long[AskRules.RingMonths];
        int count = 0, lastMonth = -1, puts = 0;
        for (int day = 1; day <= 720; day++)
        {
            builds += day % 90 == 0 ? 1 : 0;
            standing += day % 60 == 0 ? 3 : 0;
            standing -= day == 600 ? 40 : 0;   // a bad stretch: the standing falls, the licence stays
            int now = LevelRules.For(builds, standing);
            if (now > level)
            {
                level = now;
                earned++;
            }
            lost += now < level ? 1 : 0;
            int month = AskRules.MonthOf(day, 360);
            if (month != lastMonth)
            {
                lastMonth = month;
                count = AskRules.Put(months, asks, count, month, 1000L + month * 10L);
                puts++;
            }
        }
        Check(earned >= 2 && level >= 2 && lost > 0, "construction kit: over two years the licence is earned level by level, and a fall in standing leaves the level earned (" + earned + " levels earned, level " + level + ")");
        Check(puts == 24 && count == AskRules.RingMonths && months[count - 1] == 23 && months[0] == 12 && AskRules.Change(months, asks, count) > 0,
            "construction kit: two years give twenty four monthly asks, a ring of the last twelve and a rise on the month before (" + puts + " asks, " + count + " kept)");
    }
}
