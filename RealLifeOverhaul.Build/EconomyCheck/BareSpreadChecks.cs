using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;

// The load's bare ground check spread over frames (docs/standards/performance.md, RLO-PERF-024; the own-spike scan, 28 Oct 2026:
// ConstructionDirector at 143 and 181 ms in a frame). Two causes, both mended: every column's lot was found by walking all the
// district's lots (ConstructionDirector.Spaces.cs, Space, Before, After: now LotFinder, one lookup), and the whole check ran in one
// frame (now BareAudit, a step a frame's slice). Here: LotFinder answers what the walk answered on the plan and on lots with
// doubled ids and places; BareAudit's report is the whole Audit's to the last bit on the plan and on every changed plan the fail
// checks use, at any slicing and with another check stepped between; a step is short; and each is shown failing on a break.
internal static partial class Program
{
    private const double BareStepMostMs = 0.5;   // ms one step of the spread check may take here (a row of spots or a column's facts)

    private static void BareSpreadChecks()
    {
        BareFinderChecks();
        BareAuditChecks();
    }

    // The walk the director did before, kept here as the reference.
    private static int WalkFirst(List<LotView> lots, string id)
    {
        for (int i = 0; id != null && i < lots.Count; i++)
        {
            if (lots[i].Id == id)
            {
                return i;
            }
        }
        return -1;
    }

    private static int WalkBefore(List<LotView> lots, LotView l)
    {
        for (int i = 0; i < lots.Count; i++)
        {
            if (lots[i].Strip == l.Strip && lots[i].Index + Math.Max(1, lots[i].Spaces) == l.Index)
            {
                return i;
            }
        }
        return -1;
    }

    private static int WalkAfter(List<LotView> lots, LotView l)
    {
        for (int i = 0; (l.Strip > 0 && l.Spaces > 0) && i < lots.Count; i++)
        {
            if (lots[i].Strip == l.Strip && lots[i].Index == l.Index + Math.Max(1, l.Spaces))
            {
                return i;
            }
        }
        return -1;
    }

    private static void BareFinderChecks()
    {
        List<LotView> plan = BuildModePlan();
        // Doubled ids and doubled places, and a lot with no id: the first of each answers, as the walk's first match did.
        var lots = new List<LotView>(plan);
        LotView twin = plan[plan.Count / 2];
        twin.X += 5f;
        lots.Add(twin);
        LotView stranger = plan[7];
        stranger.Id = "lot_with_a_borrowed_place";
        lots.Add(stranger);
        lots.Add(new LotView { Id = null, Strip = plan[3].Strip, Index = plan[3].Index });
        var finder = new LotFinder();
        finder.Build(lots, 1);
        int asked = 0, same = 0;
        string differ = null;
        foreach (LotView l in lots)
        {
            asked += 3;
            int f = finder.First(l.Id), b = finder.Ending(l.Strip, l.Index), a = (l.Strip > 0 && l.Spaces > 0) ? finder.Starting(l.Strip, l.Index + Math.Max(1, l.Spaces)) : -1;
            same += (f == WalkFirst(lots, l.Id) ? 1 : 0) + (b == WalkBefore(lots, l) ? 1 : 0) + (a == WalkAfter(lots, l) ? 1 : 0);
            if (differ == null && (f != WalkFirst(lots, l.Id) || b != WalkBefore(lots, l) || a != WalkAfter(lots, l)))
            {
                differ = l.Id ?? "(no id)";
            }
        }
        Check(same == asked && lots.Count > 600 && finder.First("no_such_lot") == -1 && finder.First(null) == -1 && finder.Built == 1,
            "bare ground spread: the lot finder answers what the walk over all lots answered, by id, the lot before and the lot after, on " + lots.Count
            + " lots with a doubled id, a doubled place and a lot with no id (" + same + " of " + asked + (differ != null ? ", the first apart " + differ : string.Empty) + ")");

        // Broken on purpose: a finder that keeps the last of a doubled id answers otherwise than the walk.
        var last = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < lots.Count; i++)
        {
            if (lots[i].Id != null)
            {
                last[lots[i].Id] = i;
            }
        }
        Check(last[twin.Id] != WalkFirst(lots, twin.Id) && finder.First(twin.Id) == WalkFirst(lots, twin.Id),
            "bare ground spread, fails on purpose: a finder keeping the last lot of a doubled id is found out against the walk (" + last[twin.Id] + " against " + WalkFirst(lots, twin.Id) + ")");

        // What it saves: one walk a column against one lookup a column, the director's two asks a column (its lot and whether it is held).
        CellGrid grid = CellGrid.Of(plan);
        long walk = BestOf(3, () =>
        {
            int found = 0;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                found += WalkFirst(plan, grid.Columns[c].LotId) >= 0 ? 1 : 0;
                found += WalkFirst(plan, grid.Columns[c].LotId) >= 0 ? 1 : 0;
            }
            return found;
        });
        var built = new LotFinder();
        built.Build(plan, 2);
        long look = BestOf(3, () =>
        {
            int found = 0;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                found += built.First(grid.Columns[c].LotId) >= 0 ? 1 : 0;
                found += built.First(grid.Columns[c].LotId) >= 0 ? 1 : 0;
            }
            return found;
        });
        double walkMs = walk * 1000.0 / Stopwatch.Frequency, lookMs = look * 1000.0 / Stopwatch.Frequency;
        Check(lookMs < walkMs / 20.0 && lookMs < 2.0,
            "bare ground spread: finding every column's lot twice takes " + lookMs.ToString("F2") + " ms by the finder against " + walkMs.ToString("F1") + " ms by the walk over "
            + plan.Count + " lots (" + grid.Columns.Count + " columns; Mono some three times slower)");
    }

    private static long BestOf(int tries, Func<int> work)
    {
        long best = long.MaxValue;
        for (int t = 0; t < tries; t++)
        {
            long began = Stopwatch.GetTimestamp();
            work();
            best = Math.Min(best, Stopwatch.GetTimestamp() - began);
        }
        return best;
    }

    // Every field of a report, written out: two reports are the same to the last bit when these are.
    private static string ReportBits(BareReport r)
    {
        if (r == null)
        {
            return "null";
        }
        return string.Join("|", r.Blocks, r.Columns, r.Cells, r.Held, string.Join(",", r.Classes), string.Join(",", r.HeldKinds), r.PlacesDown,
            BitConverter.DoubleToInt64Bits(r.CellM2), BitConverter.DoubleToInt64Bits(r.HeldM2), BitConverter.DoubleToInt64Bits(r.MarginM2), BitConverter.DoubleToInt64Bits(r.BareM2),
            r.FirstColumn ?? "-", r.FirstBare ?? "-", r.WorstBlock ?? "-", BitConverter.DoubleToInt64Bits(r.WorstM2), r.Line());
    }

    // The check stepped to its end, another check's step between every <paramref name="between"/> of its own when one is given.
    private static BareReport Stepped(CellGrid grid, List<BlockView> blocks, Func<int, BuildModeRules.ColumnFacts> facts, float step, int between, BareAudit other, out int steps, out double slowestMs)
    {
        var audit = new BareAudit();
        audit.Begin(grid, blocks, facts, step);
        steps = 0;
        slowestMs = 0.0;
        bool more = true;
        while (more)
        {
            long began = Stopwatch.GetTimestamp();
            more = audit.Step();
            slowestMs = Math.Max(slowestMs, (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency);
            steps++;
            if (other != null && between > 0 && steps % between == 0)
            {
                other.Step();
            }
        }
        return audit.Report;
    }

    private static void BareAuditChecks()
    {
        CellGrid grid = BuildGroundGrid(out Func<int, BuildModeRules.ColumnFacts> facts, out List<BlockView> blocks);
        CellGrid none = BuildGroundGrid(out Func<int, BuildModeRules.ColumnFacts> f1, out List<BlockView> b1, true, true);
        CellGrid down = BuildGroundGrid(out Func<int, BuildModeRules.ColumnFacts> f2, out List<BlockView> b2, false);
        CellGrid park = BuildGroundGrid(out Func<int, BuildModeRules.ColumnFacts> f5, out List<BlockView> b5);
        park.Held.RemoveAll(h => h.Kind == HeldWhat.Park && h.Block == 117);
        int built = 0;
        while (built < grid.Columns.Count && !facts(built).Stands)
        {
            built++;
        }
        Func<int, BuildModeRules.ColumnFacts> lie = c =>
        {
            BuildModeRules.ColumnFacts f = facts(c);
            f.Stands = f.Stands && c != built;
            return f;
        };
        var cases = new (string name, CellGrid grid, Func<int, BuildModeRules.ColumnFacts> facts, List<BlockView> blocks, float step)[]
        {
            ("the plan at 2 m", grid, facts, blocks, 2f), ("the plan at 1 m", grid, facts, blocks, 1f), ("the plan at 4 m", grid, facts, blocks, 4f),
            ("the special places left out", none, f1, b1, 1f), ("the places down", down, f2, b2, 2f), ("a lot declared built", grid, lie, blocks, 4f),
            ("a park left out", park, f5, b5, 2f)
        };
        int same = 0, mostSteps = 0;
        double slowest = 0.0;
        string differ = null;
        foreach ((string name, CellGrid g, Func<int, BuildModeRules.ColumnFacts> f, List<BlockView> b, float step) in cases)
        {
            string whole = ReportBits(BuildGroundRules.Audit(g, b, f, step));
            var other = new BareAudit();
            other.Begin(none, b1, f1, 1f);   // another check stepped between: nothing the steps keep is shared
            string spread = ReportBits(Stepped(g, b, f, step, 3, other, out _, out _));
            string alone = ReportBits(Stepped(g, b, f, step, 0, null, out int steps, out double slow));   // timed once its code is compiled
            mostSteps = Math.Max(mostSteps, steps);
            slowest = Math.Max(slowest, slow);
            if (whole == spread && whole == alone)
            {
                same++;
            }
            else
            {
                differ = differ ?? name;
            }
        }
        Check(same == cases.Length,
            "bare ground spread: the check a step at a time reports what the whole check reports, to the last bit, on the plan at 1, 2 and 4 m and on each changed plan the fail checks use, "
            + "with another check stepped between (" + same + " of " + cases.Length + (differ != null ? ", the first apart: " + differ : string.Empty) + ")");
        Check(slowest < BareStepMostMs && mostSteps > grid.Columns.Count,
            "bare ground spread: no step takes more than " + BareStepMostMs + " ms here (the slowest " + slowest.ToString("F3") + " ms of up to " + mostSteps + " steps)");

        // Its slices as the director takes them (a slice of 1 ms, one step more at most): the frames stay under the 2 ms wanted.
        var audit = new BareAudit();
        audit.Begin(grid, blocks, facts, 2f);
        var watch = new SpreadWatch();
        watch.Begin();
        bool more = true;
        while (more)
        {
            long began = Stopwatch.GetTimestamp();
            WorkSlice slice = WorkSlice.Of(1.0, 1);
            while (more && slice.More())
            {
                long item = Stopwatch.GetTimestamp();
                more = audit.Step();
                watch.Item(0, item);
            }
            watch.Frame(began);
        }
        Check(watch.Frames > 1 && watch.WorstMs < SpreadWatch.WantedMs && SpreadWatch.Verdict(watch.WorstMs) == "PASS" && ReportBits(audit.Report) == ReportBits(BuildGroundRules.Audit(grid, blocks, facts, 2f)),
            "bare ground spread: in slices of 1 ms the check takes " + watch.Frames + " frames here, the slowest " + watch.WorstMs.ToString("F2") + " ms (" + watch.TotalMs.ToString("F1")
            + " ms in all), and reports what the whole check reports");
        Check(SpreadWatch.Verdict(2.5).StartsWith("FAIL", StringComparison.Ordinal), "bare ground spread, fails on purpose: a frame of 2.5 ms makes its line say FAIL");

        // Broken on purpose: a check that walks the blocks in another order is found out by the comparison.
        BareReport whole1 = BuildGroundRules.Audit(none, b1, f1, 1f);
        var flipped = new List<BlockView>(b1);
        flipped.Reverse();
        BareReport reversed = BuildGroundRules.Audit(none, flipped, f1, 1f);
        Check(ReportBits(reversed) != ReportBits(whole1), "bare ground spread, fails on purpose: blocks walked in another order are found out by the comparison (" + reversed.FirstBare + " against " + whole1.FirstBare + ")");
        var stopped = new BareAudit();
        stopped.Begin(grid, blocks, facts, 2f);
        stopped.Step();
        stopped.Stop();
        Check(!stopped.Running && stopped.Report == null && !stopped.Step(), "bare ground spread: a check stopped part way says nothing and does no more");
    }
}
