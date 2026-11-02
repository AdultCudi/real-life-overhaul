using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;

// The load's office count spread over frames (docs/standards/performance.md, RLO-PERF-024; the owner's log of 2 Nov 2026: the
// city's offices, shops and warehouses counted in 8.5 ms in one tick, its three lines written in it). Now OfficeCount, a
// building a step, which Core/OfficeCensus drives a slice of a millisecond a frame from TechSites, a line a frame after. Here,
// on a made-up city of this checker's own: the count a step at a time writes the three lines one pass writes, to the last
// character, at any slicing and with another count stepped between; a count stopped part way does no more; in slices of a
// millisecond no frame passes the 2 ms wanted; and, broken on purpose, a count that skips a building is found out.
internal static partial class Program
{
    private const int CensusBuildings = 2600;   // about the game's city: 757 of its buildings counted on 2 Nov 2026

    private static readonly string[] CensusDistricts = { "ba:neighborhood_midtown", "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_murrayhill", null, "" };
    private static readonly string[] CensusHolders = { null, null, null, "techhq_nimbus", "police_3", "hotel_1", "campus", "utilities_office", "courthouse", "someone_else" };

    // One made-up building, the same every time it is asked: what OfficeCensus reads of a building of the game's.
    private static bool CensusRead(int i, out int kind, out string district, out LetState state, out string holder)
    {
        uint h = (uint)i * 2654435761u;
        h ^= h >> 13;
        h *= 0x5bd1e995u;
        h ^= h >> 15;
        kind = (int)(h % 5) - 1;   // -1 to 3: one in five of no counted kind, one in five of a kind past the three
        district = CensusDistricts[(h >> 8) % (uint)CensusDistricts.Length];
        state = (LetState)((h >> 12) % 5);
        holder = CensusHolders[(h >> 16) % (uint)CensusHolders.Length];
        return (h >> 24) % 7 != 0;   // one in seven not counted at all (fenced, gated, a special service, no door)
    }

    // The three lines, as OfficeCensus writes them.
    private static string CensusLines(OfficeCount c) => c.Of(0).Line("office") + "\n" + c.Of(1).Line("retail") + "\n" + c.Of(2).Line("warehouse");

    // The pass OfficeCensus.Log made before, kept here as the reference.
    private static string CensusOnePass(int count)
    {
        var tallies = new[] { new OfficeTally(), new OfficeTally(), new OfficeTally() };
        for (int i = 0; i < count; i++)
        {
            if (CensusRead(i, out int kind, out string district, out LetState state, out string holder) && kind >= 0 && kind < 3)
            {
                tallies[kind].Add(district, state, holder);
            }
        }
        return tallies[0].Line("office") + "\n" + tallies[1].Line("retail") + "\n" + tallies[2].Line("warehouse");
    }

    // The count stepped to its end in slices of <paramref name="each"/> steps, another count's step between every slice when one is given.
    private static string CensusStepped(int count, int each, OfficeCount other, out int slices)
    {
        var census = new OfficeCount();
        census.Begin(count, CensusRead);
        slices = 0;
        bool more = census.Running;
        while (more)
        {
            for (int k = 0; k < each && more; k++)
            {
                more = census.Step();
            }
            slices++;
            other?.Step();
        }
        return CensusLines(census);
    }

    private static void OfficeCountChecks()
    {
        string pass = CensusOnePass(CensusBuildings);
        string whole = CensusLines(OfficeCount.Whole(CensusBuildings, CensusRead));
        var other = new OfficeCount();
        other.Begin(CensusBuildings / 2, (int i, out int kind, out string district, out LetState state, out string holder) => CensusRead(CensusBuildings - 1 - i, out kind, out district, out state, out holder));
        string one = CensusStepped(CensusBuildings, 1, other, out int oneSlices);
        string some = CensusStepped(CensusBuildings, 37, null, out _);
        string all = CensusStepped(CensusBuildings, CensusBuildings * 2, null, out int allSlices);
        Check(whole == pass && one == pass && some == pass && all == pass && pass.Length > 300,
            "office count spread: a building a step writes the three lines one pass writes, to the last character, a step a slice with another count stepped between, 37 a slice, and all in one ("
            + CensusBuildings + " buildings)");
        Check(oneSlices == CensusBuildings && allSlices == 1, "office count spread: a step a slice takes a slice a building (" + oneSlices + "), a whole slice one (" + allSlices + ")");

        var counted = new OfficeCount();
        Check(!counted.Running && !counted.Step(), "office count spread: a count not begun does nothing");
        counted.Begin(0, CensusRead);
        Check(!counted.Running && !counted.Step() && counted.Of(0).Count == 0, "office count spread: a count of no buildings is done at once");
        counted.Begin(CensusBuildings, CensusRead);
        counted.Step();
        counted.Step();
        counted.Stop();
        int at = counted.Counted;
        Check(!counted.Running && !counted.Step() && counted.Counted == at && at == 2, "office count spread: a count stopped part way reads no more buildings");
        counted.Begin(CensusBuildings, CensusRead);
        Check(CensusLines(OfficeCount.Whole(CensusBuildings, CensusRead)) == pass && counted.Of(0).Count == 0 && counted.Running,
            "office count spread: a count begun again forgets what it had counted");

        // Its slices as TechSites takes them (a millisecond, a building more at most), each building a made-up read of some
        // microseconds as the game's are: no frame passes the 2 ms wanted.
        var spread = new OfficeCount();
        spread.Begin(CensusBuildings, (int i, out int kind, out string district, out LetState state, out string holder) =>
        {
            long until = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 250000;   // 4 microseconds
            while (Stopwatch.GetTimestamp() < until)
            {
            }
            return CensusRead(i, out kind, out district, out state, out holder);
        });
        var watch = new SpreadWatch();
        watch.Begin();
        bool going = spread.Running;
        while (going)
        {
            long began = Stopwatch.GetTimestamp();
            WorkSlice slice = WorkSlice.Of(1.0, 1);
            while (going && slice.More())
            {
                long item = Stopwatch.GetTimestamp();
                going = spread.Step();
                watch.Item(0, item);
            }
            watch.Frame(began);
        }
        Check(watch.Frames > 5 && watch.WorstMs < SpreadWatch.WantedMs && CensusLines(spread) == pass,
            "office count spread: in slices of 1 ms the count takes " + watch.Frames + " frames here, the slowest " + watch.WorstMs.ToString("F2") + " ms ("
            + watch.TotalMs.ToString("F1") + " ms in all), and writes what one pass writes");

        // Broken on purpose: a count that skips a building, or reads one twice, is found out against the one pass.
        int first = 0;
        while (!(CensusRead(first, out int firstKind, out _, out _, out _) && firstKind >= 0 && firstKind < 3))
        {
            first++;
        }
        string skipped = CensusLines(OfficeCount.Whole(CensusBuildings, (int i, out int kind, out string district, out LetState state, out string holder) =>
            CensusRead(i, out kind, out district, out state, out holder) && i != first));
        string twice = CensusLines(OfficeCount.Whole(CensusBuildings + 1, (int i, out int kind, out string district, out LetState state, out string holder) =>
            CensusRead(i == CensusBuildings ? first : i, out kind, out district, out state, out holder)));
        Check(skipped != pass && twice != pass, "office count spread, fails on purpose: a count that skips a building or reads one twice is found out");
    }
}
