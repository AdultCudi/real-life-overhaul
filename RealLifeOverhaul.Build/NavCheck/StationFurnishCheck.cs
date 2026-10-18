using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Law;

// What furnishing a police station costs, and where it is paid (Law/StationGrid.cs, and over the sources Law/LawDirector.cs
// and the StationInterior parts). A walk into a precinct once took a frame of 283 ms: the whole station furnished in one
// frame, the game's design files read on the main thread, the floor sampled twice and a walk over the whole grid for every
// target and every seat. Now the walkway rule walks from the door once for as long as the pieces stand as they are, with the
// same answers; the floor is measured once; the designs are read on a worker; and the pass runs only where a long frame is
// not seen. What it takes in the game is the "Law check (furnish)" line of Player.log.
internal static partial class Program
{
    private const double FurnishWalkBudgetMs = 15.0;   // a large station's walkway rule and seats, once on the way in

    private static void StationFurnish()
    {
        StationWalksAgree();
        StationWalksTimed();
        StationFurnishSources();
    }

    // A room with walls of blocks inside, and pieces in rows: the kind of floor a furnished station is.
    private static StationGrid FurnishedRoom(int seed, List<FloorRect> pieces, List<bool> keep, List<bool> isFixed, float wide, float deep)
    {
        var random = new Random(seed);
        StationGrid grid = Room(wide, deep);
        for (int r = 0; r < grid.Rows; r++)
        {
            for (int c = 0; c < grid.Cols; c++)
            {
                if (random.NextDouble() < 0.04)
                {
                    grid.SetWalkable(c, r, false);   // a column, a step, a hole in the measured floor
                }
            }
        }
        pieces.Clear();
        keep.Clear();
        isFixed.Clear();
        for (int i = 0; i < 120; i++)
        {
            float a = (float)(random.NextDouble() - 0.5) * wide * 0.9f, d = 1.2f + (float)random.NextDouble() * (deep - 2f);
            pieces.Add(new FloorRect(a, d, 0.2f + (float)random.NextDouble() * 0.6f, 0.2f + (float)random.NextDouble() * 0.5f));
            keep.Add(true);
            isFixed.Add(i % 9 == 0);
        }
        return grid;
    }

    // The walk from the door made once gives what a walk for every target and every seat gave.
    private static void StationWalksAgree()
    {
        var pieces = new List<FloorRect>();
        var keep = new List<bool>();
        var isFixed = new List<bool>();
        var targetsA = new List<float>();
        var targetsD = new List<float>();
        bool sameKept = true, sameSeats = true;
        int removedAll = 0;
        for (int seed = 1; seed <= 40; seed++)
        {
            var random = new Random(seed * 31);
            targetsA.Clear();
            targetsD.Clear();
            for (int t = 0; t < 10; t++)
            {
                targetsA.Add((float)(random.NextDouble() - 0.5) * 9f);
                targetsD.Add(1f + (float)random.NextDouble() * 12f);
            }
            // All the targets at once, against one at a time: each target's answer is what it was alone, after the ones before it.
            StationGrid grid = FurnishedRoom(seed, pieces, keep, isFixed, 10f, 14f);
            int removed = grid.KeepWaysOpen(0f, 0.4f, targetsA, targetsD, pieces, keep, isFixed, 0.24f, 0.9f);
            var kept = new List<bool>(keep);
            StationGrid alone = FurnishedRoom(seed, pieces, keep, isFixed, 10f, 14f);
            int removedAlone = 0;
            for (int t = 0; t < targetsA.Count; t++)
            {
                removedAlone += alone.KeepWaysOpen(0f, 0.4f, new List<float> { targetsA[t] }, new List<float> { targetsD[t] }, pieces, keep, isFixed, 0.24f, 0.9f);
            }
            for (int i = 0; i < keep.Count; i++)
            {
                sameKept &= keep[i] == kept[i];
            }
            sameKept &= removed == removedAlone;
            removedAll += removed;

            // The seats: asked one after another with the pieces as they stand, as asked each with a fresh walk.
            bool walked = false;
            for (int s = 0; s < 30; s++)
            {
                float a = (float)(random.NextDouble() - 0.5) * 9f, d = 1f + (float)random.NextDouble() * 12f;
                float reach = s % 3 == 0 ? 1.6f : 1.0f;
                bool once = grid.NearestOpen(0f, 0.4f, a, d, reach, pieces, keep, 0.24f, out float oa, out float od, walked);
                walked = true;
                bool fresh = alone.NearestOpen(0f, 0.4f, a, d, reach, pieces, keep, 0.24f, out float fa, out float fd);
                sameSeats &= once == fresh && oa == fa && od == fd;
            }
        }
        Check(sameKept && removedAll > 0, "station furnish: one walk from the door for every target keeps and takes out the same pieces as a walk for each (" + removedAll + " taken out in 40 rooms)");
        Check(sameSeats, "station furnish: every seat's place to stand is the one a fresh walk from the door gives, 1,200 seats in 40 rooms");
    }

    // A large station's walkway rule and its seats, timed.
    private static void StationWalksTimed()
    {
        var pieces = new List<FloorRect>();
        var keep = new List<bool>();
        var isFixed = new List<bool>();
        var targetsA = new List<float>();
        var targetsD = new List<float>();
        for (int t = 0; t < 12; t++)
        {
            targetsA.Add(-8f + t * 1.4f);
            targetsD.Add(2f + t * 2f);
        }
        var one = new List<float>(1) { 0f };
        var oneD = new List<float>(1) { 0f };
        double once = double.MaxValue, each = double.MaxValue;
        for (int run = 0; run < 6; run++)   // the first of each pays for the code being compiled
        {
            StationGrid grid = FurnishedRoom(7, pieces, keep, isFixed, 20f, 30f);
            var watch = Stopwatch.StartNew();
            Walks(grid, pieces, keep, isFixed, targetsA, targetsD, null, null);
            once = run > 0 ? Math.Min(once, watch.Elapsed.TotalMilliseconds) : once;
            grid = FurnishedRoom(7, pieces, keep, isFixed, 20f, 30f);
            watch.Restart();
            Walks(grid, pieces, keep, isFixed, targetsA, targetsD, one, oneD);
            each = run > 0 ? Math.Min(each, watch.Elapsed.TotalMilliseconds) : each;
        }
        Check(once < FurnishWalkBudgetMs && once * 2.0 < each,
            $"station furnish: a 20 by 30 m station's walkway rule (12 targets, 120 pieces) and 40 seats take {once:0.00} ms, under {FurnishWalkBudgetMs:0} ms and under half the {each:0.00} ms of a walk for each");
    }

    // The walkway rule and the seats: with one walk from the door while the pieces stand (one and oneD null), or a walk for
    // each target and each seat, as the rule was once asked.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void Walks(StationGrid grid, List<FloorRect> pieces, List<bool> keep, List<bool> isFixed, List<float> targetsA, List<float> targetsD,
        List<float> one, List<float> oneD)
    {
        if (one == null)
        {
            grid.KeepWaysOpen(0f, 0.4f, targetsA, targetsD, pieces, keep, isFixed, 0.24f, 0.9f);
        }
        else
        {
            for (int t = 0; t < targetsA.Count; t++)
            {
                one[0] = targetsA[t];
                oneD[0] = targetsD[t];
                grid.KeepWaysOpen(0f, 0.4f, one, oneD, pieces, keep, isFixed, 0.24f, 0.9f);
            }
        }
        bool walked = false;
        for (int s = 0; s < 40; s++)
        {
            grid.NearestOpen(0f, 0.4f, -9f + (s % 10) * 2f, 2f + (s / 10) * 6f, 1.0f, pieces, keep, 0.24f, out _, out _, walked);
            walked = one == null;
        }
    }

    // Over the sources: where the pass runs, what it reads and how often it measures.
    private static void StationFurnishSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string director = Source(scripts, @"Law\LawDirector.cs");
        Check(Count(director, "_interior.Begin(") == 1 && Body(director, "private void Furnish(Precinct precinct)").Contains("_interior.Begin(precinct);")
              && Body(director, "private void Furnish(Precinct precinct)").Contains("Law check (furnish)"),
            "station furnish: the station is furnished in one place, which times it for its self-check line");
        string tick = Body(director, "private void Tick()");
        string owed = Body(director, "private void FurnishOwed()");
        Check(!tick.Contains("Furnish(") && tick.Contains("_roomOwed = ") && owed.Contains("if (!SceneFinds.Hidden)") && owed.Contains("Furnish(")
              && Body(director, "private void OnUpdate()").Contains("FurnishOwed();"),
            "station furnish: when the door's event was missed, the station is furnished only behind a dark screen or a pause, never in play");
        Check(Body(director, "private void Moved()").Contains("Furnish(precinct);"), "station furnish: on the door's own event, under its black screen, it is furnished at once");

        string design = Source(scripts, @"Law\StationInterior.Design.cs");
        Check(Body(design, "private static Task<DesignPick> Search(").Contains("Task.Run(") && !Body(design, "private BusinessLayoutSet FindDesign(").Contains("Read(")
              && director.Contains("StationInterior.SearchDesigns(_precincts);"),
            "station furnish: the game's design files are listed and read on a worker thread, begun when the precincts are chosen");
        string rooms = Source(scripts, @"Law\StationInterior.Rooms.cs");
        string fill = Source(scripts, @"Law\StationInterior.Fill.cs");
        int measures = 0;
        foreach (string file in Directory.GetFiles(Path.Combine(scripts, "Law"), "StationInterior*.cs"))
        {
            measures += Count(Code(File.ReadAllText(file)), "RoomMeasure.Measure(");
        }
        Check(measures == 1 && Body(rooms, "private void MeasureGrid()").Contains("RoomMeasure.Measure(") && !rooms.Contains("NavMesh.SamplePosition")
              && fill.Contains("r.Grid = _room;"),
            "station furnish: the room is measured once, and the station's grid and the back office both read that one measurement");
    }
}
