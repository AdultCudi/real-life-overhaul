using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Population;

// The city that grows, offline: the city as the game lists it at a load (Build 3682's and the mod's two districts north of Manhattan, Uptown and
// Cedar Heights, standing from the first day), their land standing in for what the construction game measures in the game (its free land and the
// building code's room over what stands, PopulationBridges.LandHomes). The city's developers are stood in for by the
// construction game's own pace (Construction/GrowRules: at most four sites under way in a district, two days between starts, a building's keys about
// a month after its land): a site starts while the population's homes pull for its district is open, and its building joins the city's table on
// the day it stands (PopulationSim.Rebuild), as the director binds the game's finished buildings at the month's turn. The rules' own checks, each
// made to fail once while written, follow: the pressure, the homes wanted, the land, the pull, the firms, the turnover, the save and the share.
internal static partial class Program
{
    // The mod's land offline, in flats: Uptown and Cedar Heights hold some 7,500 flats in their 222 home buildings on the first day of a game
    // (POPULATION.md section 16); the building code's twelve storeys over the half of it most of their buildings stand at, and their 65 free spaces
    // (plans/growables.md), leave room for about as many again. This stands in for what the game measures itself (the construction game's free
    // columns at their usual floor and each standing home's room under the code, PopulationBridges.LandHomes).
    private static readonly string[] BandIds = { "reallife_neighborhood_uptown", "reallife_neighborhood_cedarheights" };
    private static readonly int[] BandLand = { 4000, 4600 };
    private const int BandSiteSqm = 2400;     // m2 of a grown home building's floor: a site of one or two spaces at Uptown's storeys
    private const int BandBuildDays = 30;     // game days from a site's start to its keys (GrowRules' plans, City Hall and works: 16 to 38, about a month)
    private const int BandSitesMost = 4, BandGapDays = 2;

    // The developers' sites under way and the land left, for one run.
    private sealed class BandBuilder
    {
        public readonly int[] Land = (int[])BandLand.Clone();
        public readonly List<(int District, int Done, bool Office)> Sites = new List<(int, int, bool)>(8);
        public int NextStart, Number = 1, Built;
        public int[] Band = new int[BandIds.Length];   // the districts' places in the city's table
    }

    // The city as the game lists it, generated whole in a calendar of so many days a year; the mod's two districts' land measured.
    private static PopulationSim LivingBandMade(int seed, int yearDays, out PopulationState s, out BandBuilder builder)
    {
        s = PopulationCity(out List<BusinessSeed> seeds);
        var sim = new PopulationSim();
        sim.BeginGenerate(s, seed, 1, yearDays, seeds);
        sim.RunAll();
        builder = new BandBuilder();
        for (int i = 0; i < BandIds.Length; i++)
        {
            builder.Band[i] = s.DistrictOf(BandIds[i]);
            GrowthRules.SetLand(s, builder.Band[i], builder.Land[i]);
        }
        return sim;
    }

    // The developers' day, after the population's: buildings whose day has come join the table, and a site starts in a district whose homes pull is open.
    private static void BandDay(PopulationSim sim, PopulationState s, BandBuilder b, int day)
    {
        bool done = false;
        for (int i = b.Sites.Count - 1; i >= 0; i--)
        {
            if (b.Sites[i].Done <= day)
            {
                done = true;
            }
        }
        if (done)
        {
            var list = new List<CityBuilding>(s.Buildings);
            for (int i = b.Sites.Count - 1; i >= 0; i--)
            {
                if (b.Sites[i].Done <= day)
                {
                    list.Add(b.Sites[i].Office ? BandOffice(b.Sites[i].District, b.Number++) : BandBuilding(s, b.Sites[i].District, b.Number++));
                    b.Sites.RemoveAt(i);
                    b.Built++;
                }
            }
            sim.Rebuild(list);
        }
        if (day < b.NextStart || sim.Pulls == null)
        {
            return;
        }
        for (int k = 0; k < BandIds.Length; k++)
        {
            int i = (day + k) % BandIds.Length, d = b.Band[i];
            int under = 0;
            foreach (var site in b.Sites)
            {
                under += site.District == d ? 1 : 0;
            }
            int flats = MeasureRules.Flats(BandBuilding(s, d, 0), s.Districts[d]);
            if (d * 4 < sim.Pulls.Length && sim.Pulls[d * 4] > 0 && under < BandSitesMost && b.Land[i] >= flats)
            {
                b.Sites.Add((d, day + BandBuildDays, false));
                b.Land[i] -= flats;
                GrowthRules.SetLand(s, d, b.Land[i]);
                GrowthRules.Start(s, flats, sim.Pulls);
                b.NextStart = day + BandGapDays;
                return;
            }
            // the developers' offices: while the crowded firms' year of offices is not all started the population raises the offices pull past their
            // threshold (DemandRules: 45)
            int room = GrowthRules.FlatsOf(s, d, BandOfficeSqm);
            if (d * 4 + 2 < sim.Pulls.Length && sim.Pulls[d * 4 + 2] >= GrowthRules.OfficesPull && s.Growth.OfficesStarted < s.Growth.OfficesWanted && under < BandSitesMost
                && b.Land[i] >= room && OfficesUnderWay(b) == 0)
            {
                b.Sites.Add((d, day + BandBuildDays, true));
                b.Land[i] -= room;
                GrowthRules.SetLand(s, d, b.Land[i]);
                GrowthRules.StartOffices(s, GrowthRules.JobsOf(BandOfficeSqm), sim.Pulls);
                b.NextStart = day + BandGapDays;
                return;
            }
        }
    }

    private const int BandOfficeSqm = 4000;   // m2 of a grown office building's floor

    private static int OfficesUnderWay(BandBuilder b)
    {
        int n = 0;
        foreach (var site in b.Sites)
        {
            n += site.Office ? 1 : 0;
        }
        return n;
    }

    private static CityBuilding BandOffice(int d, int number)
    {
        return new CityBuilding { Street = "reallife_street_grown", Number = number, District = d, Use = Use.Office, UnitSqm = 285, TotalSqm = BandOfficeSqm, Traffic = 30 };
    }

    private static CityBuilding BandBuilding(PopulationState s, int d, int number)
    {
        return new CityBuilding { Street = "reallife_street_grown", Number = number, District = d, Use = Use.Homes, UnitSqm = 96, TotalSqm = BandSiteSqm, Traffic = 30 };
    }

    // ---------------------------------------------------------------- the rules' own checks

    private static void GrowthChecks()
    {
        // The pressure: none at the share the city builds from or over it; the most past the whole span under it; households turned away add to it.
        Check(GrowthRules.Pressure(20000, 1400, 0) == 0 && GrowthRules.Pressure(20000, 2000, 0) == 0 && GrowthRules.Pressure(20000, 600, 0) == 100
              && GrowthRules.Pressure(20000, 1000, 0) == 50 && GrowthRules.Pressure(20000, 1400, 200) == 40 && GrowthRules.Pressure(20000, 1000, 200) == 90
              && GrowthRules.Pressure(0, 0, 9) == 0,
            "growth: the housing pressure is none at 7% free flats, half at 5%, the most at 3%, and households turned away add to it");
        // The homes wanted: New York's 0.8% of the homes a year at the most pressure, none with none, and less as the land runs out.
        Check(GrowthRules.Wanted(20000, 100, 1000) == 160 && GrowthRules.Wanted(20000, 0, 1000) == 0 && GrowthRules.Wanted(20000, 100, 500) == 80
              && GrowthRules.Wanted(20000, 50, 1000) == 80 && GrowthRules.Wanted(20000, 100, 0) == 0,
            "growth: the city wants at most 0.8% of its homes a year, half at half the pressure or half the land, none once the land is gone");
        PopulationSim sim = LivingBandMade(4321, CalendarRules.YearDefault, out PopulationState s, out BandBuilder b);
        int uptown = b.Band[0], midtown = s.DistrictOf("ba:neighborhood_midtown");
        Check(s.Growth.Land[uptown] == BandLand[0] && s.Growth.Land[midtown] == -1 && GrowthRules.LandLeft(s.Growth) == 1000,
            "growth: the mod's land is measured in Uptown and Cedar Heights, a district of the game's has none (" + s.Growth.Land[uptown] + ", " + s.Growth.Land[midtown] + ")");
        GrowthRules.SetLand(s, uptown, BandLand[0] / 2);
        Check(GrowthRules.LandLeft(s.Growth) == (int)(1000L * (BandLand[0] / 2 + BandLand[1]) / (BandLand[0] + BandLand[1])) && s.Growth.LandStart[uptown] == BandLand[0],
            "growth: land taken leaves its share against the most measured (" + GrowthRules.LandLeft(s.Growth) + ")");
        GrowthRules.SetLand(s, uptown, BandLand[0]);
        // The pull: open while homes are wanted, closed once the year's are started; a district of the game's keeps its own.
        var pulls = new int[s.Districts.Count * 4];
        for (int i = 0; i < pulls.Length; i++)
        {
            pulls[i] = 7;
        }
        s.Growth.Unformed = 400;   // a month of households turned away: the pressure at its most
        GrowthRules.Month(s, pulls);
        bool open = pulls[uptown * 4] >= PopulationCatalogue.PullRoom && pulls[midtown * 4] == 7 && s.Growth.Wanted > 0;
        GrowthRules.Start(s, s.Growth.Wanted, pulls);
        bool closedAtOnce = pulls[uptown * 4] == PopulationCatalogue.PullClosed;
        s.Growth.Unformed = 400;
        GrowthRules.Month(s, pulls);
        Check(open && closedAtOnce && pulls[uptown * 4] == PopulationCatalogue.PullClosed && pulls[midtown * 4] == 7 && pulls[uptown * 4 + 1] == 7,
            "growth: the mod's land's homes pull is open while the year's homes are wanted and closed once they are started; the game's districts and other uses keep theirs");
        // The studio's residents who play read as a share: the same count in a city half again as big is two thirds of it at the start's people.
        Check(GameRules.Share(900, 90000, 60000) == 600 && GameRules.Share(900, 60000, 60000) == 900 && GameRules.Share(900, 0, 60000) == 900 && GameRules.Share(900, 60000, 0) == 900,
            "growth: the studio reads the residents who play as the city's tastes, not its size");
        // The firms: the level the city asks for, within the floor's room; the slots of a floor at a level; the start's level changes nothing.
        Check(CityFirmRules.Target(1000, 1000, 500, 500) == 1000 && CityFirmRules.Target(2000, 1000, 1000, 500) == CityFirmRules.StockMost
              && CityFirmRules.Target(100, 1000, 50, 500) == PopulationCatalogue.StockLeast && CityFirmRules.Target(1100, 1000, 520, 500) == 1055
              && CityFirmRules.OfficesWantedOf(1150, 50000, 50000, 1111) == 400 && CityFirmRules.OfficesWantedOf(1115, 50000, 50000, 1111) == 200
              && CityFirmRules.OfficesWantedOf(1100, 50000, 50000, 1111) == 0 && CityFirmRules.OfficesWantedOf(1150, 50000, 52000, 1111) == 0
              && CityFirmRules.SlotsOf(400, 1000) == 400 && CityFirmRules.SlotsOf(400, 1100) == 440 && CityFirmRules.StockMost == 1111,
            "growth: the firms follow three parts the city's labour force and one its spending, within the office floor's empty tenth and a fifth down; offices on new land "
            + "only for the jobs the floors cannot hold, 0.8% a year at the most");
        // A building finished joins the table, its flats free and counted as homes built; a demolished one sends its households away to look again.
        PopulationSim fresh = LivingBandMade(4321, CalendarRules.YearDefault, out PopulationState t, out BandBuilder tb);
        int homes = GrowthRules.Homes(t), people = t.People.Live;
        var list = new List<CityBuilding>(t.Buildings) { BandBuilding(t, tb.Band[1], 1) };
        var clock = Stopwatch.StartNew();
        int away = fresh.Rebuild(list);
        double ms = clock.Elapsed.TotalMilliseconds;
        int added = MeasureRules.Flats(BandBuilding(t, tb.Band[1], 1), t.Districts[tb.Band[1]]);
        Check(away == 0 && GrowthRules.Homes(t) == homes + added && t.Growth.Built == added && t.People.Live == people && t.Figures.Length == t.Districts.Count,
            "growth: a home building finished joins the city's table with its " + added + " flats free, counted as built, nobody moved (" + away + " away)");
        Check(ms < 200, "growth: binding the city's table to its buildings again takes under 200 ms (" + ms.ToString("F1", CultureInfo.InvariantCulture) + " ms), once a month at the most");
        list.RemoveAt(list.Count - 1);
        int gone = -1;
        for (int i = 0; i < list.Count && gone < 0; i++)
        {
            int at = t.BuildingOfKey(PopulationState.KeyOf(list[i].Street, list[i].Number));
            if (list[i].Use == Use.Homes && at >= 0 && t.FlatCount[at] > 3 && !t.Districts[list[i].District].Villas)
            {
                gone = i;
            }
        }
        list.RemoveAt(gone);
        away = fresh.Rebuild(list);
        Check(away > 0 && t.People.Live == people && GrowthRules.Differs(t, list) == false,
            "growth: a building gone sends its households away to look for flats, nobody lost (" + away + " away), and the table matches the city again");
        // The turnover: an older household leaves; arrivals are young.
        GrowthTurnover();
        GrowthSaveShare();
    }

    // Over a year of the default's, households of retired people leave at about their rate and those who come from the region are young.
    private static void GrowthTurnover()
    {
        PopulationSim sim = PopulationMade(2468, out PopulationState s, out _, CalendarRules.YearDefault);
        long agesIn = 0, came = 0;
        int before = s.NextHousehold;
        for (int day = 2; day <= CalendarRules.YearDefault + 1; day++)
        {
            sim.BeginDay(day, Ordinary, null);
            sim.RunAll();
        }
        for (int h = 0; h < s.Households.Count; h++)
        {
            if (s.Households.Alive(h) && s.Households.Id[h] >= before && s.Households.First[h] >= 0 && s.Households.Since[h] > 1)
            {
                agesIn += s.Age(s.Households.First[h], s.Day);
                came++;
            }
        }
        int meanIn = came > 0 ? (int)(agesIn / came) : 0;
        Check(came > 200 && meanIn >= 25 && meanIn <= 42, "growth: households that came from the region or formed in the city in a year have young heads (" + came + ", " + meanIn + " years on average)");
    }

    // The growth's state through the save's line and to a Going Public guest.
    private static void GrowthSaveShare()
    {
        PopulationSim sim = LivingBandMade(97, CalendarRules.YearDefault, out PopulationState s, out BandBuilder b);
        GrowthState g = s.Growth;
        g.Pressure = 61;
        g.Wanted = 187;
        g.Started = 55;
        g.Built = 31;
        g.StartedLast = 170;
        g.BuiltLast = 150;
        g.BuiltAll = 412;
        g.Unformed = 9;
        g.Stock = 1043;
        g.Opened = 120;
        g.Closed = 101;
        g.OpenedLast = 160;
        g.ClosedLast = 149;
        g.FirmsYear = 1700;
        g.SpendAnchor = 123456789L;
        g.LabourAnchor = 31000;
        g.OfficesWanted = 480;
        g.OfficesStarted = 200;
        g.PeopleStart = 55555;
        g.OfficeAnchor = 51234;
        string line = PopulationStore.Encode(s);
        PopulationState back = PopulationStore.Decode(line, out int version);
        GrowthState r = back != null ? back.Growth : null;
        bool same = r != null && version == PopulationStore.Version && r.Pressure == 61 && r.Wanted == 187 && r.Started == 55 && r.Built == 31 && r.StartedLast == 170 && r.BuiltLast == 150
                    && r.BuiltAll == 412 && r.Unformed == 9 && r.Stock == 1043 && r.Opened == 120 && r.Closed == 101 && r.OpenedLast == 160 && r.ClosedLast == 149 && r.FirmsYear == 1700
                    && r.SpendAnchor == 123456789L && r.LabourAnchor == 31000 && r.OfficesWanted == 480 && r.OfficesStarted == 200 && r.PeopleStart == 55555 && r.OfficeAnchor == 51234 && r.Year == g.Year && r.Land.Length == g.Land.Length && r.Land[b.Band[0]] == BandLand[0]
                    && r.LandStart[b.Band[1]] == BandLand[1] && r.Land[0] == g.Land[0];
        Check(same, "growth: the homes, the land, the firms and their year come back from the save's line (version " + version + ")");
        // A line of version 2 (before the growth) loads with the growth empty: the firms at the start's level, the land unmeasured.
        PopulationState old = PopulationStore.Decode(PopulationStoreOld(s), out int oldVersion);
        Check(old != null && oldVersion == 2 && old.Growth.Stock == 1000 && old.Growth.Land.Length == 0 && old.Growth.Year == -1 && old.People.Live == s.People.Live
              && old.Growth.PeopleStart == old.People.Live,
            "growth: a save from before the growth loads with the firms at their start and the land measured afresh (" + (old != null ? old.Growth.Stock : -1) + ")");
        // The guest: the host's figures, a line without them leaving a guest's as they were.
        var guest = new PopulationState();
        guest.Districts.AddRange(s.Districts);
        CityTable.Lay(guest);
        bool applied = PopulationShare.Apply(guest, PopulationShare.Write(s));
        GrowthState q = guest.Growth;
        Check(applied && q.Pressure == 61 && q.Wanted == 187 && q.Built == 31 && q.BuiltLast == 150 && q.Opened == 120 && q.Closed == 101 && q.OpenedLast == 160 && q.ClosedLast == 149
              && q.Stock == 1043 && q.BuiltAll == 412 && q.PeopleStart == 55555,
            "growth: a Going Public guest reads the host's homes wanted and built and its firms opened and closed");
        string without = PopulationShare.Write(s);
        int cut = without.IndexOf("~w:", StringComparison.Ordinal);
        int end = cut >= 0 ? without.IndexOf('~', cut + 1) : -1;
        without = cut >= 0 ? without.Substring(0, cut) + (end >= 0 ? without.Substring(end) : string.Empty) : without;
        var older = new PopulationState();
        older.Districts.AddRange(s.Districts);
        CityTable.Lay(older);
        older.Growth.Wanted = 5;
        Check(cut >= 0 && PopulationShare.Apply(older, without) && older.Growth.Wanted == 5, "growth: a host's part without the growth leaves a guest's figures as they were (an older host)");
    }

    // The population's line as version 2 wrote it: version 3's line with its growth's columns cut off and its version said as 2.
    private static string PopulationStoreOld(PopulationState s)
    {
        return PopulationStore.EncodeVersion(s, 2);
    }
}
