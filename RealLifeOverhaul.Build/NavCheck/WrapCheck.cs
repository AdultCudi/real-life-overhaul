using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul;

// The game's own scaffolding stood round a building (Civic/WrapRules) and a new building's stages on given ground
// (Civic/PlotRules), game-free: a building's shell read from its boxes (36 5th Avenue's as the game has them, its door
// marker 0.30 m out on the pavement), runs that keep every post out of a doorway and every module off what stands in the
// way, the game's 5.17 m pitch to the roof, fabric from the second lift up, the stages of a rebuild and a plot, the pace
// at which a wrap may grow (nothing while in view, a few pieces a frame), the plot's record and its frame. Over the
// sources: each piece's renderers join the building's fade and map lists and leave them before the piece is destroyed,
// and the wrap's holder names the building before it wakes.
internal static partial class Program
{
    private static void Wraps()
    {
        WrapShellRead();
        WrapRuns();
        WrapDoorsAndObstacles();
        WrapStagesAndPace();
        WrapTiming();
        PlotRecord();
        PlotFrame();
        WrapSources();
    }

    // 36 5th Avenue in the building's axes, from the Buildings scene: the ground floor's box, the upper floors' standing
    // 0.8 m out over the street, the door 0.30 m in front of the ground floor's face.
    private static readonly ShellBox[] FifthAvenue =
    {
        new ShellBox(-6f, 6f, 0.06f, 5.06f, -5.98f, 18.04f),
        new ShellBox(-6f, 6f, 5.055f, 15.705f, -6.785f, 17.265f),
        new ShellBox(-6f, 6f, -0.05f, 0.05f, -12f, 12f)     // the bottom plane: too low to be a wall
    };

    private static void WrapShellRead()
    {
        var shell = new WrapShell();
        WrapRules.Measure(FifthAvenue, FifthAvenue.Length, shell);
        ShellSide front = shell.Sides[2];
        Check(shell.Valid && Near(shell.Top, 15.705f) && Near(front.Ground, 5.98f) && Near(front.Outer, 6.785f) && Near(front.ProudFrom, 5.055f),
            "wrap: 36 5th Avenue's front is its ground floor's face at 5.98 m, the upper floors stand out to 6.785 m from 5.06 m up, the roof at 15.7 m");
        Check(Near(front.RunMin, -6f) && Near(front.RunMax, 6f) && Near(shell.Sides[0].Outer, 18.04f) && Near(shell.Sides[1].Outer, 6f),
            "wrap: each side's run and face are read from the boxes in the building's own axes");
        // The door marker stands on the pavement, 0.30 m out: its wall is the one it is nearest, found from the boxes alone.
        Check(WrapRules.SideOf(shell, 0f, -6.28f) == 2 && Near(WrapRules.Outward(2, 0f, -6.28f) - front.Ground, 0.30f),
            "wrap: the door 0.30 m out on the pavement belongs to the front (the wall is never looked for between the pavement and the door)");
        var none = new WrapShell();
        WrapRules.Measure(new[] { FifthAvenue[2] }, 1, none);
        Check(!none.Valid, "wrap: a building with no box a storey high has no shell");
        Check(WrapRules.LiftsFor(15.705f) == 3 && WrapRules.LiftsFor(3f) == 1 && WrapRules.LiftsFor(200f) == WrapRules.MostLifts && WrapRules.LiftsFor(20f) == 4,
            "wrap: lifts to the roof at the game's 5.17 m pitch: 3 for 15.7 m, 4 for 20 m, one at least, ten at most");
        bool axes = true;
        for (int side = 0; side < 4; side++)
        {
            WrapRules.Point(side, 3f, 2f, out float x, out float z);
            axes &= Near(WrapRules.Outward(side, x, z), 3f) && Near(WrapRules.Along(side, x, z), 2f);
            WrapRules.Axes(side, out float ox, out float oz, out float rx, out float rz);
            // The yaw turns +Z out of the side and +X along its run, as Unity turns (sin, cos) and (cos, -sin).
            double yaw = WrapRules.YawOf(side) * Math.PI / 180.0;
            axes &= Near((float)Math.Sin(yaw), ox) && Near((float)Math.Cos(yaw), oz) && Near((float)Math.Cos(yaw), rx) && Near(-(float)Math.Sin(yaw), rz);
        }
        Check(axes, "wrap: every side's axes, its points and the yaw that turns a piece out of it agree");
    }

    private static WrapPlan PlanOf(ShellBox[] boxes, bool[] street, List<WrapKeepOut> keepOuts)
    {
        var shell = new WrapShell();
        WrapRules.Measure(boxes, boxes.Length, shell);
        var plan = new WrapPlan();
        WrapRules.Plan(shell, street, keepOuts, WrapRules.MostLifts, WrapRules.MostPieces, plan);
        return plan;
    }

    private static void WrapRuns()
    {
        var doors = new List<WrapKeepOut> { WrapRules.Door(2, 0f) };
        WrapPlan plan = PlanOf(FifthAvenue, new[] { false, false, true, false }, doors);
        int corners = 0, longs = 0, fabric = 0, lift0Fabric = 0;
        foreach (WrapPiece piece in plan.Pieces)
        {
            corners += piece.Kind == WrapPieceKind.Corner ? 1 : 0;
            longs += piece.Kind == WrapPieceKind.Long ? 1 : 0;
            fabric += piece.Fabric ? 1 : 0;
            lift0Fabric += piece.Fabric && piece.Lift == 0 ? 1 : 0;
        }
        Check(plan.Lifts == 3 && plan.Modules == 3 && plan.Runs == 1 && corners == 6 && longs == 3 && fabric == 6 && lift0Fabric == 0 && plan.Pieces.Count == 15 && plan.Why == null,
            "wrap: 36 5th Avenue's 12 m front takes a corner, a 6 m module and a corner on each of its 3 lifts, fabric on the upper two: 15 pieces");
        // Every piece stands off the outermost face (the upper floors'), its back 0.12 m clear of it, and the corners close the ends.
        bool placed = true;
        WrapPiece first = plan.Pieces[0], last = plan.Pieces[2];
        foreach (WrapPiece piece in plan.Pieces)
        {
            float outward = WrapRules.Outward(2, piece.X, piece.Z) - (piece.Fabric ? WrapRules.FabricOut : 0f);
            placed &= Near(outward, 6.785f + WrapRules.Gap + WrapRules.Depth * 0.5f) && Near(piece.Y, piece.Lift * WrapRules.Pitch + (piece.Fabric ? WrapRules.FabricLift : 0f));
        }
        Check(placed, "wrap: modules stand 0.12 m off the outermost face at the game's pitch; fabric hangs 1.37 m out on their fronts");
        Check(first.Kind == WrapPieceKind.Corner && last.Kind == WrapPieceKind.Corner && Near(first.Yaw, 180f) && Near(last.Yaw, 270f),
            "wrap: a run's first corner faces out as the run does and its last is turned a quarter, which closes that end as the game's are");
        // Buildings of every width: the run never passes the side's ends and fills all but less than one 3 m unit of it.
        bool fills = true;
        for (float width = 6f; width <= 60f; width += 0.7f)
        {
            var box = new[] { new ShellBox(-width / 2f, width / 2f, 0f, 12f, -5f, 5f) };
            WrapPlan wide = PlanOf(box, new[] { true, false, false, false }, null);
            float from = float.MaxValue, to = float.MinValue;
            foreach (WrapPiece piece in wide.Pieces)
            {
                float half = piece.Kind == WrapPieceKind.Long || piece.Kind == WrapPieceKind.LargeFabric ? WrapRules.Unit : WrapRules.Unit * 0.5f;
                from = Math.Min(from, piece.X - half);
                to = Math.Max(to, piece.X + half);
            }
            fills &= from >= -width / 2f - 0.01f && to <= width / 2f + 0.01f && to - from > width - WrapRules.Unit;
        }
        Check(fills, "wrap: on fronts of 6 to 60 m the run stays within the front and leaves less than one 3 m unit of it bare");
        WrapPlan narrow = PlanOf(new[] { new ShellBox(-2.5f, 2.5f, 0f, 12f, -5f, 5f) }, new[] { true, false, false, false }, null);
        Check(narrow.Pieces.Count == 0 && narrow.Why != null, "wrap: a front under 6 m takes no scaffold, and says why");
        // A tall long building is cut to the pieces a wrap may have, with the reason.
        WrapPlan tall = PlanOf(new[] { new ShellBox(-30f, 30f, 0f, 60f, -30f, 30f) }, new[] { true, true, true, true }, null);
        Check(tall.Pieces.Count <= WrapRules.MostPieces && tall.Lifts >= 1 && tall.Why != null, "wrap: a building that would take more than " + WrapRules.MostPieces + " pieces stops lower, and says so");
        // The same building, the same wrap.
        WrapPlan again = PlanOf(FifthAvenue, new[] { false, false, true, false }, doors);
        bool same = again.Pieces.Count == plan.Pieces.Count;
        for (int i = 0; same && i < plan.Pieces.Count; i++)
        {
            same = again.Pieces[i].Kind == plan.Pieces[i].Kind && again.Pieces[i].X == plan.Pieces[i].X && again.Pieces[i].Z == plan.Pieces[i].Z && again.Pieces[i].Yaw == plan.Pieces[i].Yaw;
        }
        Check(same, "wrap: the same building gives the same wrap every time (every machine sets out the same)");
        // Bottom up: no piece of a lift comes before one of the lift below.
        bool ordered = true;
        for (int i = 1; i < tall.Pieces.Count; i++)
        {
            ordered &= tall.Pieces[i].Lift >= tall.Pieces[i - 1].Lift;
        }
        Check(ordered, "wrap: the pieces are in the order they go up, lift by lift from the street");
    }

    // The posts of every run (its ends and every joint), as the pieces of its street lift say.
    private static List<float> Posts(WrapPlan plan, int side)
    {
        var posts = new List<float>();
        foreach (WrapPiece piece in plan.Pieces)
        {
            if (piece.Lift != 0 || piece.Fabric || piece.Side != side)
            {
                continue;
            }
            float along = WrapRules.Along(side, piece.X, piece.Z);
            float half = piece.Kind == WrapPieceKind.Long ? WrapRules.Unit : WrapRules.Unit * 0.5f;
            posts.Add(along - half);
            posts.Add(along + half);
        }
        return posts;
    }

    private const float DoorWay = 0.9f;   // metres from a door's middle to the nearest post: a door's half and a shoulder, held whatever the rules say

    private static void WrapDoorsAndObstacles()
    {
        var random = new Random(51);
        bool clear = true, some = true;
        for (int trial = 0; trial < 400; trial++)
        {
            float width = 8f + (float)random.NextDouble() * 40f;
            var box = new[] { new ShellBox(-width / 2f, width / 2f, 0f, 14f, -6f, 6f) };
            var keep = new List<WrapKeepOut>();
            int doors = 1 + random.Next(3);
            for (int d = 0; d < doors; d++)
            {
                keep.Add(WrapRules.Door(0, -width / 2f + 1f + (float)random.NextDouble() * (width - 2f)));
            }
            WrapPlan plan = PlanOf(box, new[] { true, false, false, false }, keep);
            foreach (float post in Posts(plan, 0))
            {
                foreach (WrapKeepOut door in keep)
                {
                    clear &= Math.Abs(post - (door.From + door.To) * 0.5f) >= DoorWay;
                }
            }
            some &= plan.Pieces.Count > 0 || width < 14f;
        }
        Check(clear, "wrap: on 400 fronts with one to three doors anywhere, no post of the scaffold stands within 0.9 m of a door's middle");
        Check(some, "wrap: a front of 14 m or more always keeps some scaffold, the run split round its doors where it must be");
        // A door at a joint the slack cannot move: the run is split round it, and both parts are clear of it.
        var atJoint = new List<WrapKeepOut> { WrapRules.Door(0, -4.5f) };
        WrapPlan split = PlanOf(new[] { new ShellBox(-7.5f, 7.5f, 0f, 14f, -6f, 6f) }, new[] { true, false, false, false }, atJoint);
        bool apart = split.Pieces.Count > 0;
        foreach (float post in Posts(split, 0))
        {
            apart &= Math.Abs(post + 4.5f) >= DoorWay;
        }
        Check(apart, "wrap: a 15 m front with its door where a joint must be keeps a run beside the door, none in front of it");
        // A tree's crown over a stretch: no module stands over it at any lift.
        var tree = new List<WrapKeepOut> { new WrapKeepOut { Side = 0, From = 2f, To = 3.5f, PostsOnly = false } };
        WrapPlan round = PlanOf(new[] { new ShellBox(-12f, 12f, 0f, 14f, -6f, 6f) }, new[] { true, false, false, false }, tree);
        bool overTree = false;
        foreach (WrapPiece piece in round.Pieces)
        {
            float half = piece.Kind == WrapPieceKind.Long || piece.Kind == WrapPieceKind.LargeFabric ? WrapRules.Unit : WrapRules.Unit * 0.5f;
            float along = WrapRules.Along(0, piece.X, piece.Z);
            overTree |= along + half > 2f && along - half < 3.5f;
        }
        Check(round.Pieces.Count > 0 && !overTree && round.Runs == 2, "wrap: a tree in front of a 24 m front splits it into two runs, neither over the tree");
        // A side that is not a street side (against the next house) gets nothing.
        WrapPlan oneSide = PlanOf(FifthAvenue, new[] { false, false, true, false }, null);
        bool onlyFront = true;
        foreach (WrapPiece piece in oneSide.Pieces)
        {
            onlyFront &= piece.Side == 2;
        }
        Check(onlyFront, "wrap: only the street sides are wrapped");
    }

    private static void WrapStagesAndPace()
    {
        bool rebuild = true;
        int[] shownBy = new int[5];
        bool[] fabricBy = new bool[5];
        for (int stage = 0; stage < 5; stage++)
        {
            WrapRules.RebuildStage(stage, 4, out shownBy[stage], out fabricBy[stage]);
        }
        rebuild &= shownBy[0] == 0 && shownBy[1] == 2 && !fabricBy[1] && shownBy[2] == 4 && fabricBy[2] && shownBy[3] == 4 && fabricBy[3] && shownBy[4] == 2 && !fabricBy[4];
        Check(rebuild, "wrap: a rebuild shows hoarding only, then half the scaffold, then all of it with the fabric through the works and the inspection, then half coming down");
        WrapRules.PlotStage(3, 5, out int frameStage, out bool frameFabric);
        WrapRules.PlotStage(4, 5, out int cladStage, out bool cladFabric);
        Check(frameStage == 0 && !frameFabric && cladStage == 5 && cladFabric, "wrap: a plot is wrapped, fabric on, only in its last stage");
        var piece = new WrapPiece { Kind = WrapPieceKind.LargeFabric, Lift = 2 };
        Check(WrapRules.Shown(piece, 3, true) && !WrapRules.Shown(piece, 3, false) && !WrapRules.Shown(piece, 2, true), "wrap: a piece stands only on a lift shown, fabric only while it is hung");
        Check(WrapRules.Budget(true, true, false, false) == 0 && WrapRules.Budget(false, true, false, false) == 0,
            "wrap: nothing is set out or taken down while the wrap is in view");
        Check(WrapRules.Budget(true, false, false, false) == WrapRules.PlacePerFrame && WrapRules.Budget(false, false, false, false) == WrapRules.TakePerFrame
              && WrapRules.Budget(true, true, false, true) == WrapRules.PlacePerFrame && WrapRules.Budget(true, true, true, false) == WrapRules.BlackPerFrame,
            "wrap: out of view (or for a test) a few pieces a frame; behind the game's black screen more");
        // A whole wrap going up out of view: spread over frames, never at once.
        int frames = 0, most = 0, left = WrapRules.MostPieces;
        while (left > 0)
        {
            int now = Math.Min(left, WrapRules.Budget(true, frames % 3 == 0, false, false));
            most = Math.Max(most, now);
            left -= now;
            frames++;
        }
        Check(most <= WrapRules.PlacePerFrame && frames >= WrapRules.MostPieces / WrapRules.PlacePerFrame && WrapRules.PlacePerFrame <= 8,
            "wrap: the largest wrap goes up over " + frames + " frames, " + most + " pieces at most in one (in view every third frame: none then)");
    }

    private static void WrapTiming()
    {
        var shell = new WrapShell();
        var box = new[] { new ShellBox(-25f, 25f, 0f, 40f, -18f, 18f) };
        var keep = new List<WrapKeepOut> { WrapRules.Door(0, 1f), WrapRules.Door(2, -7f), WrapRules.Door(1, 4f) };
        var plan = new WrapPlan();
        var street = new[] { true, true, true, true };
        var watch = Stopwatch.StartNew();
        const int rounds = 200;
        for (int i = 0; i < rounds; i++)
        {
            WrapRules.Measure(box, 1, shell);
            WrapRules.Plan(shell, street, keep, WrapRules.MostLifts, WrapRules.MostPieces, plan);
        }
        double ms = watch.Elapsed.TotalMilliseconds / rounds;
        Check(ms < 2.0, "wrap: laying out a large building's wrap (4 sides, 3 doors) takes " + ms.ToString("F3") + " ms, wanted under 2 ms (once, when a site is set out)");
    }

    private static void PlotRecord()
    {
        string text = PlotRules.Encode(24, 20, 90, 5);
        Check(text == "plot:24x20:090:5" && PlotRules.IsPlot(text) && !PlotRules.IsPlot("ba:street_fifthavenue") && !PlotRules.IsPlot(null),
            "plot: the ground's record reads plot:24x20:090:5, told from a street's id");
        Check(PlotRules.TryRead(text, out int w, out int d, out int yaw, out int storeys) && w == 24 && d == 20 && yaw == 90 && storeys == 5,
            "plot: the record reads back as written");
        Check(PlotRules.Encode(30, 22, -90, 3) == "plot:30x22:270:3" && PlotRules.Encode(30, 22, 450, 3) == "plot:30x22:090:3", "plot: a yaw is kept between 0 and 359");
        Check(!PlotRules.TryRead("plot:24x20:090", out _, out _, out _, out _) && !PlotRules.TryRead("plot:axb:1:2", out _, out _, out _, out _)
              && !PlotRules.TryRead("plot:5x5:000:1", out _, out _, out _, out _) && !PlotRules.TryRead("ba:street_x", out _, out _, out _, out _),
            "plot: a record cut short, unreadable, too small for a plot or not a plot is refused");
        Check(PlotRules.Unfit(24, 20, 5) == null && PlotRules.Unfit(24, 15, 5) != null && PlotRules.Unfit(24, 20, 0) != null
              && PlotRules.Unfit(70, 20, 5) != null && PlotRules.Unfit(24, 20, 11) != null,
            "plot: ground of 21 by 16 m to 60 m each way, 1 to 10 storeys");
        Check(PlotRules.Unfit(11, 24, 6) == null && PlotRules.Unfit(20, 20, 5) == null && PlotRules.Unfit(9, 24, 3) != null && PlotRules.Unfit(11, 19, 3) != null
              && PlotRules.Unfit(11, 24, 7) != null && PlotRules.Narrow(11) && PlotRules.Narrow(20) && !PlotRules.Narrow(21),
            "plot: narrower ground, 10 to 20 m wide and 20 m deep at least, takes a narrow plot of up to 6 storeys (a city lot of a small shop or home)");
        Check(PlotRules.TryRead(PlotRules.Encode(11, 24, 180, 4), out int nw, out int nd, out _, out int ns) && nw == 11 && nd == 24 && ns == 4
              && !PlotRules.TryRead(PlotRules.Encode(11, 24, 180, 8), out _, out _, out _, out _),
            "plot: a narrow plot's record reads back as written, and one of more storeys than it takes is refused");
        // Storeys as the work goes: none before the frame stage, one more for each share of it, all after.
        bool rising = true;
        int last = 0;
        for (float p = 0f; p < 1f; p += 0.005f)
        {
            int phase = Math.Min(4, (int)(p * 5f));
            int standing = PlotRules.Standing(phase, p, 5);
            rising &= standing >= last && (phase < 3 ? standing == 0 : phase > 3 ? standing == 5 : standing >= 1 && standing <= 5);
            last = standing;
        }
        Check(rising, "plot: the frame rises a storey at a time through its stage and never comes down");
        // The test's presses: from the start, each a storey, then the cladding, then handed over.
        float progress = PlotRules.NextStep(0, 0f, 5);
        int presses = 1;
        var seen = new List<int> { PlotRules.Standing(3, progress, 5) };
        while (progress < 1f && presses < 20)
        {
            int phase = Math.Min(4, (int)(progress * 5f));
            progress = PlotRules.NextStep(phase, progress, 5);
            presses++;
            int now = Math.Min(4, (int)(progress * 5f));
            if (progress < 1f)
            {
                seen.Add(PlotRules.Standing(now, progress, 5));
            }
        }
        Check(seen.Count == 6 && seen[0] == 1 && seen[1] == 2 && seen[4] == 5 && seen[5] == 5 && progress >= 1f,
            "plot: the storey test raises one storey a press (1 to 5), then the cladding, then hands the plot over");
        Check(PlotRules.Step(3, 0.65f, 5) != PlotRules.Step(3, 0.70f, 5) && PlotRules.Step(2, 0.5f, 5) != PlotRules.Step(3, 0.61f, 5),
            "plot: a storey more is a change of what stands, as a new stage is");
    }

    private static void PlotFrame()
    {
        const int width = 24, depth = 20;
        PlotRules.Frame(width, depth, out float minX, out float maxX, out float minZ, out float maxZ);
        Check(Near(minX, -8.5f) && Near(maxX, 3f) && Near(minZ, -6.5f) && Near(maxZ, 6.5f) && maxX - minX >= PlotRules.MinFrame && maxZ - minZ >= PlotRules.MinFrame,
            "plot: the frame stands 3.5 m in from three edges of the land, 9 m from the yard's");
        Check(PlotRules.Margin >= WrapRules.Depth + WrapRules.Gap, "plot: the margin round the frame takes the game's scaffold");
        PlotNarrow();
        var concrete = new List<ShellBox>();
        var metal = new List<ShellBox>();
        PlotRules.FrameBoxes(width, depth, 3, true, concrete, metal);
        bool inside = true;
        float top = 0f;
        foreach (ShellBox b in concrete)
        {
            inside &= b.MinX >= minX - 0.01f && b.MaxX <= maxX + 0.01f && b.MinZ >= minZ - 0.01f && b.MaxZ <= maxZ + 0.01f && b.MinY >= 0f && b.MaxY > b.MinY;
            top = Math.Max(top, b.MaxY);
        }
        Check(inside && Near(top, PlotRules.GroundSlab + 3 * PlotRules.StoreyHeight), "plot: three storeys of columns and slabs stand on the ground slab within the frame's footprint");
        // Columns at least at both ends of each way, no span over 6 m.
        int columns = 0;
        float lowest = float.MaxValue;
        foreach (ShellBox b in concrete)
        {
            if (b.MaxX - b.MinX < 1f && b.MinY < PlotRules.GroundSlab + 0.01f)
            {
                columns++;
                lowest = Math.Min(lowest, b.MinX);
            }
        }
        int nx = (int)Math.Ceiling((maxX - minX - PlotRules.ColumnSide) / PlotRules.Span) + 1, nz = (int)Math.Ceiling((maxZ - minZ - PlotRules.ColumnSide) / PlotRules.Span) + 1;
        Check(columns == nx * nz && Near(lowest, minX) && (maxX - minX - PlotRules.ColumnSide) / (nx - 1) <= PlotRules.Span + 0.01f,
            "plot: a storey's columns stand on a grid of spans no wider than 6 m, the outer ones at the frame's edges");
        // The rails: one post a corner, round the top.
        int posts = 0;
        foreach (ShellBox b in metal)
        {
            posts += b.MaxY - b.MinY > PlotRules.RailHeight - 0.01f && b.MaxX - b.MinX < 0.2f && b.MaxZ - b.MinZ < 0.2f ? 1 : 0;
        }
        var cladLess = new List<ShellBox>();
        PlotRules.FrameBoxes(width, depth, 3, false, new List<ShellBox>(), cladLess);
        Check(posts > 8 && cladLess.Count < metal.Count && cladLess.Count > 0, "plot: the top slab has its edge protection; the cladding adds panels to the storeys below it");
        var slab = new List<ShellBox>();
        var bars = new List<ShellBox>();
        PlotRules.SlabBoxes(width, depth, slab, bars);
        Check(slab.Count == 1 && bars.Count == nx * nz * 4, "plot: the slab stage is the ground slab and four starter bars a column");
        // The yard: the crane's pad, the cabin, the toilet and the stacks on the land and off the frame.
        var spots = new List<PlotSpot>();
        PlotRules.StackSpots(width, depth, spots);
        PlotRules.YardSpots(width, depth, out PlotSpot cabin, out PlotSpot toilet);
        spots.Add(cabin);
        spots.Add(toilet);
        PlotSpot crane = PlotRules.CraneSpot(width, depth);
        bool yard = crane.X - PlotRules.CranePad / 2f >= maxX && crane.X + PlotRules.CranePad / 2f <= width / 2f && crane.Z + PlotRules.CranePad / 2f <= depth / 2f;
        foreach (PlotSpot s in spots)
        {
            yard &= s.X > maxX && s.X < width / 2f && s.Z > -depth / 2f && s.Z < depth / 2f;
        }
        Check(yard, "plot: the crane's pad, the cabin, the toilet and the stacks all stand in the yard, on the land and off the frame");
        var hoarding = new List<PlotSpot>();
        PlotRules.HoardingSpots(width, depth, hoarding);
        PlotRules.Gate(width, out float gateFrom, out float gateTo);
        bool gate = hoarding.Count > 4;
        foreach (PlotSpot s in hoarding)
        {
            gate &= (s.X + PlotRules.Hoarding / 2f <= gateFrom + 0.01f || s.X - PlotRules.Hoarding / 2f >= gateTo - 0.01f) && Near(s.Z, -depth / 2f);
        }
        Check(gate && gateTo - gateFrom >= 6f, "plot: the hoarding runs along the land's front with a 6 m gate left open at the yard");
        var fence = new List<FenceRun>();
        PlotRules.FenceRuns(width, depth, fence);
        Check(fence.Count == 3, "plot: the fence wire runs round the other three sides");
        PlotRules.ToWorld(10f, 20f, 90, 0f, 1f, out float wx, out float wz);
        PlotRules.ToWorld(10f, 20f, 90, 1f, 0f, out float rx, out float rz);
        Check(Near(wx, 11f) && Near(wz, 20f) && Near(rx, 10f) && Near(rz, 19f), "plot: a land turned 90 degrees has its back to +X, as Unity turns it");
    }

    private static void WrapSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string wrap = Source(scripts, @"Civic\SiteWrap.cs");
        string put = Body(wrap, "private void Put(int index)"), take = Body(wrap, "private void Take(int index)"), holder = Body(wrap, "private Transform Holder()");
        Check(put.Contains("_building.renderersToFade.Add(") && put.Contains("_building.renderersToHide.Add(") && put.Contains("_mapSet?.Add(") && put.Contains("SetRendererToHideVisibility"),
            "wrap source: every renderer of a piece set out joins its building's fade, hide and map lists, hidden at once if the building is");
        int removed = take.IndexOf("renderersToHide.Remove(", StringComparison.Ordinal), destroyed = take.IndexOf("Destroy(made)", StringComparison.Ordinal);
        Check(take.Contains("renderersToFade.Remove(") && take.Contains("_mapSet?.Remove(") && removed >= 0 && destroyed > removed,
            "wrap source: every renderer leaves its building's lists before its piece is destroyed (a destroyed one would stop the game's fade)");
        int named = holder.IndexOf("OwnerField.SetValue(", StringComparison.Ordinal), woke = holder.IndexOf("holder.SetActive(true)", StringComparison.Ordinal);
        Check(holder.Contains("SetActive(false)") && holder.Contains("AddComponent<ViewBlockingEntityExtension>") && named >= 0 && woke > named,
            "wrap source: the holder's extension names the building before the holder wakes, as the game's own scaffold's does");
        string strip = Body(wrap, "public void Strip()");
        Check(strip.Contains("Take(i)") && strip.Contains("Destroy(_holder.gameObject)"), "wrap source: taking a wrap down at once takes every piece out of the lists first");
        string dresser = Source(scripts, @"Civic\SiteDresser.cs") + Source(scripts, @"Civic\SiteDresser.Wraps.cs");
        string step = Body(dresser, "private void StepWrap(");
        Check(step.Contains("BoxInView(wrap.Bounds)") && step.Contains("WrapRules.Budget(") && step.Contains("map && !taking"),
            "wrap source: a wrap grows only by WrapRules.Budget, judged by its whole box in the camera's view, and never while the city map shows");
        string director = Source(scripts, @"Civic\CivicDirector.cs");
        string update = Body(director, "private void OnUpdate()");
        Check(update.Contains("if (_sites.Framed)") && update.IndexOf("_sites.Frame(now)", StringComparison.Ordinal) > update.IndexOf("if (_sites.Framed)", StringComparison.Ordinal),
            "wrap source: the director steps the sites' wraps only while there is something to step (one comparison a frame otherwise)");
        string kit = Source(scripts, @"Civic\SiteKit.cs");
        Check(kit.Contains("isPartOfStaticBatch") && kit.Contains("is MonoBehaviour") && director.Contains("ScenePrewarm.AddLate(") && director.Contains("SiteKit.Prewarm("),
            "wrap source: the game's pieces are copied unbatched, their scripts taken off, behind the loading screen");
        string place = Source(scripts, @"Civic\SiteDresser.Place.cs");
        string wall = Body(place, "private SiteRefusal ResolveWall(");
        Check(wall.Contains("BuildingShell.Read(") && !wall.Contains("door - pavement") && !wall.Contains("CivicSpots.Wall("),
            "wall source: a scaffold's or hoarding's wall is the building's own box, never looked for between the pavement and the door (the NoWall of 36 5th Avenue)");
        string slew = Source(scripts, @"Civic\CraneSlew.cs");
        Check(slew.Contains("legacy = true") && slew.Contains("AddComponent<Animation>") && slew.Contains("AnimationCullingType.BasedOnRenderers") && !slew.Contains("void Update"),
            "crane source: the jib slews by the engine's own Animation and a legacy clip, culled with its renderer, with no frame code of the mod's");
    }

    // A narrow plot (a small lot of the new district): the frame across its width with a metre each side, the yard behind
    // it, the gate at one end of the front, everything on its own land, and no crane's pad.
    private static void PlotNarrow()
    {
        const int width = 11, depth = 24;
        PlotRules.Frame(width, depth, out float minX, out float maxX, out float minZ, out float maxZ);
        Check(Near(minX, -4.5f) && Near(maxX, 4.5f) && Near(minZ, -8.5f) && Near(maxZ, 4.5f) && maxX - minX >= PlotRules.MinFrame && maxZ - minZ >= PlotRules.MinFrame,
            "plot, narrow: the frame stands a metre in from the sides, 3.5 m from the front and 7.5 m from the back, at least 8 m each way");
        PlotRules.Gate(width, out float gateFrom, out float gateTo);
        Check(gateFrom >= -width * 0.5f && Near(gateTo, width * 0.5f) && Near(gateTo - gateFrom, PlotRules.NarrowGate), "plot, narrow: the gate is at the end of its front, on its own land");
        PlotRules.YardSpots(width, depth, out PlotSpot cabin, out PlotSpot toilet);
        var stacks = new List<PlotSpot>();
        PlotRules.StackSpots(width, depth, stacks);
        bool behind = cabin.Z > maxZ && toilet.Z > maxZ && Math.Abs(cabin.X) < width * 0.5f && Math.Abs(toilet.X) < width * 0.5f && cabin.Z < depth * 0.5f && toilet.Z < depth * 0.5f;
        foreach (PlotSpot s in stacks)
        {
            behind &= s.Z > maxZ && s.Z < cabin.Z && Math.Abs(s.X) < width * 0.5f;
        }
        Check(behind && stacks.Count == 2, "plot, narrow: the cabin, the toilet and two stacks stand in the yard behind the frame, on its own land");
        var spoil = new List<PlotSpot>();
        PlotRules.SpoilSpots(width, depth, spoil);
        bool onLand = true;
        foreach (PlotSpot s in spoil)
        {
            onLand &= Math.Abs(s.X) <= width * 0.5f && Math.Abs(s.Z) <= depth * 0.5f;
        }
        Check(onLand, "plot, narrow: the dig's spoil is heaped on its own land");
    }
}
