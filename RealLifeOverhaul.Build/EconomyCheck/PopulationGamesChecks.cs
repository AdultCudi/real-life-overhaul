using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Tech;

// The people's games, the game studio's audience (Population/GameRules): the population's table of the genres each platform's players like is the
// studio's own; the generated city plays on each platform at its share by age and likes the world's genres, every genre on every platform with
// residents; its audience on the first day is the documented world's (StudioBridges.World), and after it follows the residents (births, deaths, moves)
// and the world's growth; the same seed gives the same games; the counts are the people's, by district and class; and the save and a Going Public
// guest keep them.
internal static partial class Program
{
    private static void PopulationGamesChecks()
    {
        bool same = GameRules.Genres == (int)Genre.Count && GameRules.Platforms == StudioCatalogue.GamePlatforms;
        for (int p = 0; same && p < GameRules.Platforms; p++)
        {
            for (int g = 0; g < GameRules.Genres; g++)
            {
                same &= PopulationCatalogue.GameGenreShare[p][g] == StudioCatalogue.GenreShare[p][g];
            }
        }
        Check(same, "population games: the genres and platforms are the studio's, and the share of each platform's players who like each genre is its table");

        PopulationSim sim = PopulationMade(20261101, out PopulationState s, out _);
        int day = s.Day;
        bool started = true, worldly = true;
        long players = 0, people = 0;
        for (int cell = 0; cell < GameRules.Cells; cell++)
        {
            int g = cell / GameRules.Platforms, p = cell % GameRules.Platforms;
            started &= s.GameStart[cell] > 0 && s.GameStart[cell] == s.GameCounts[cell];
            long world = StudioBridges.World(g, p, day), audience = GameRules.Audience(s.GameCounts[cell], s.GameStart[cell], world);
            worldly &= Math.Abs(audience - world) <= world / 1000 + 1 && Math.Abs(audience - StudioBridges.World(g, p, 0)) <= StudioBridges.World(g, p, 0) / 100;
        }
        Check(started, "population games: every genre on every platform has residents, and the studio's start is the city's first day");
        Check(worldly, "population games: the audience of the first day is the documented world's for every genre and platform (within a thousandth, a hundredth of day 0's)");

        // Who plays on what: the shares of the people old enough, and each platform's favourite genres against the studio's table.
        var on = new int[GameRules.Platforms];
        int old = 0;
        for (int i = 0; i < s.People.Count; i++)
        {
            if (s.People.Id[i] == 0 || s.Age(i, s.Day) < GameRules.GameAge)
            {
                continue;
            }
            old++;
            int platforms = GameRules.PlatformsOf(s.People.Games[i]);
            for (int p = 0; p < GameRules.Platforms; p++)
            {
                on[p] += (platforms & 1 << p) != 0 ? 1 : 0;
            }
            players += platforms != 0 ? 1 : 0;
            people++;
        }
        int pc = on[0] * 1000 / Math.Max(1, old), console = on[1] * 1000 / Math.Max(1, old), phone = on[2] * 1000 / Math.Max(1, old);
        bool favourites = true;
        double spreadMost = 0;
        var report = new List<string>();
        for (int p = 0; p < GameRules.Platforms; p++)
        {
            int cityTop = 0, tableTop = 0;
            double least = double.MaxValue, most = 0;
            for (int g = 0; g < GameRules.Genres; g++)
            {
                int cell = g * GameRules.Platforms + p;
                cityTop = s.GameCounts[cell] > s.GameCounts[cityTop * GameRules.Platforms + p] ? g : cityTop;
                tableTop = StudioCatalogue.GenreShare[p][g] > StudioCatalogue.GenreShare[p][tableTop] ? g : tableTop;
                double factor = StudioBridges.World(g, p, 0) / (double)Math.Max(1, s.GameStart[cell]);
                least = Math.Min(least, factor);
                most = Math.Max(most, factor);
            }
            int third = 0;   // the city's top genres on the platform: the table's favourite among its first three
            for (int g = 0; g < GameRules.Genres; g++)
            {
                third += s.GameCounts[g * GameRules.Platforms + p] > s.GameCounts[tableTop * GameRules.Platforms + p] ? 1 : 0;
            }
            favourites &= third < 3;
            spreadMost = Math.Max(spreadMost, most / Math.Max(1, least));
            report.Add((Genre)cityTop + " (the table's " + (Genre)tableTop + "), factors " + least.ToString("F0", CultureInfo.InvariantCulture) + " to " + most.ToString("F0", CultureInfo.InvariantCulture));
        }
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            Console.WriteLine("  games: " + players * 100 / Math.Max(1, people) + "% of people 6 and over play; on PC " + pc / 10 + "%, console " + console / 10 + "%, phone " + phone / 10
                              + "%; favourites by platform: " + string.Join("; ", report));
        }
        Check(phone > console && phone > pc && phone >= 400 && phone <= 750 && console >= 200 && console <= 500 && pc >= 200 && pc <= 450,
            "population games: most of the people who play do so on phones, fewer on consoles and PCs (" + pc / 10 + "% PC, " + console / 10 + "% console, " + phone / 10 + "% phone)");
        Check(favourites && spreadMost < 6.0, "population games: each platform's favourite genre in the studio's table is among the city's first three there, and the world's factor"
              + " varies less than sixfold between a platform's genres (" + spreadMost.ToString("F1", CultureInfo.InvariantCulture) + ")");
        int kids = 0, kidsPlaying = 0;
        for (int i = 0; i < s.People.Count; i++)
        {
            if (s.People.Id[i] != 0 && s.Age(i, s.Day) < GameRules.GameAge)
            {
                kids++;
                kidsPlaying += s.People.Games[i] != 0 ? 1 : 0;
            }
        }
        Check(kids > 0 && kidsPlaying == 0, "population games: a child under six plays no game of their own (" + kids + " children)");

        // The counts are the people's, by district and class, and the same seed gives the same games.
        var recount = new int[GameRules.Cells];
        int byDistrict = 0, byClass = 0, total = 0;
        for (int h = 0; h < s.Households.Count; h++)
        {
            if (s.Households.Alive(h) && (s.Households.Flags[h] & HouseholdFlags.Away) == 0)
            {
                for (int i = s.Households.First[h]; i >= 0; i = s.People.Next[i])
                {
                    GameRules.Add(s.People.Games[i], recount, 0);
                }
            }
        }
        for (int cell = 0; cell < GameRules.Cells; cell++)
        {
            total += s.GameCounts[cell];
            for (int d = 0; d < s.Districts.Count; d++)
            {
                byDistrict += s.GameByDistrict[d * GameRules.Cells + cell];
            }
            for (int c = 0; c < PopulationCatalogue.Classes; c++)
            {
                byClass += s.GameByClass[c * GameRules.Cells + cell];
            }
        }
        bool counted = true;
        for (int cell = 0; cell < GameRules.Cells; cell++)
        {
            counted &= recount[cell] == s.GameCounts[cell];
        }
        Check(counted && byDistrict == total && byClass == total, "population games: the day's counts are the residents' games, and the districts' and the classes' sum to the city's (" + total + ")");
        PopulationMade(20261101, out PopulationState again, out _);
        bool alike = again.People.Count == s.People.Count;
        for (int i = 0; alike && i < s.People.Count; i++)
        {
            alike &= again.People.Games[i] == s.People.Games[i];
        }
        Check(alike, "population games: the same seed gives every person the same games");

        // Two years on: the counts follow births, deaths and moves, the start stays, and the audience follows the residents and the world.
        int[] first = (int[])s.GameCounts.Clone(), start = (int[])s.GameStart.Clone();
        for (int d = day + 1; d <= day + 120; d++)
        {
            sim.BeginDay(d, Ordinary, null);
            sim.RunAll();
        }
        Array.Clear(recount, 0, recount.Length);
        for (int h = 0; h < s.Households.Count; h++)
        {
            if (s.Households.Alive(h) && (s.Households.Flags[h] & HouseholdFlags.Away) == 0)
            {
                for (int i = s.Households.First[h]; i >= 0; i = s.People.Next[i])
                {
                    GameRules.Add(s.People.Games[i], recount, 0);
                }
            }
        }
        bool follows = true, moved = false, kept = true, grows = true;
        for (int cell = 0; cell < GameRules.Cells; cell++)
        {
            follows &= recount[cell] == s.GameCounts[cell];
            moved |= s.GameCounts[cell] != first[cell];
            kept &= s.GameStart[cell] == start[cell];
            int g = cell / GameRules.Platforms, p = cell % GameRules.Platforms;
            long now = GameRules.Audience(s.GameCounts[cell], s.GameStart[cell], StudioBridges.World(g, p, s.Day));
            grows &= now == s.GameCounts[cell] * StudioBridges.World(g, p, s.Day) / s.GameStart[cell];
        }
        Check(follows && moved && kept && grows, "population games: two years on the counts are the residents' as births, deaths and moves left them, the start is kept, and the audience is"
              + " the residents times the world's growth");
        Check(GameRules.Audience(5, 0, 1000) == -1 && GameRules.Audience(0, 10, 1000) == 0, "population games: with no start the studio takes the documented world; with no residents, no audience");

        // The save keeps every person's games and the start; a guest takes the host's counts.
        PopulationState back = PopulationStore.Decode(PopulationStore.Encode(s), out _);
        bool saved = back != null;
        for (int i = 0; saved && i < s.People.Count; i++)
        {
            saved &= back.People.Games[i] == s.People.Games[i];
        }
        for (int cell = 0; saved && cell < GameRules.Cells; cell++)
        {
            saved &= back.GameStart[cell] == s.GameStart[cell] && back.GameCounts[cell] == s.GameCounts[cell];
        }
        Check(saved, "population games: the save keeps every person's games and the studio's start, and the counts come back with them");
        var guest = new PopulationState();
        bool shared = PopulationShare.Apply(guest, PopulationShare.Write(s));
        for (int cell = 0; shared && cell < GameRules.Cells; cell++)
        {
            shared &= guest.GameCounts[cell] == s.GameCounts[cell] && guest.GameStart[cell] == s.GameStart[cell];
        }
        Check(shared, "population games: a Going Public guest's studio has the host's audience");
    }
}
