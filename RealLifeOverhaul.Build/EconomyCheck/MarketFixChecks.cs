using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RealLifeOverhaul.Markets;

// The stock market (Scripts/Markets): the faults found when the folder was cleaned up, each checked against what the old
// code did wrong. A guest's trade lines are written and read culture-free; a company joining a save's market leaves the
// city's index where it was with a company over the cap; the tape is swapped in whole and a load finds one left mid-swap;
// the chart's pointer leaving does no work once its guard is off.
internal static partial class Program
{
    private static void MarketFixes()
    {
        MarketTradeLines();
        MarketJoinCapped();
        MarketTapeSwapped();
        MarketTapeLines();
        MarketChartGuarded();
    }

    // The tape's kept lines (perf/app-spikes, the own-spikes scan of 28 Oct 2026): a save hands the writer the lines as they
    // stand, which must be the whole tape to the byte through every way the tape changes; a tape unchanged since a save
    // counts no change, so the file is left as that save wrote it; and handing the lines over costs a sliver of making the
    // whole again on the main thread, which grew with every day played.
    private static void MarketTapeLines()
    {
        var rng = new Random(7);
        var tape = new MarketTape(0xABCDEF0123456789UL);
        bool same = true;
        int rounds = 0;
        for (int step = 0; step < 4000; step++)
        {
            int day = 1 + rng.Next(step / 8 + 2);
            switch (rng.Next(10))
            {
                case 0:
                case 1:
                    tape.AddSecret(day, ((ulong)rng.Next() << 24) ^ (ulong)step);
                    break;
                case 2:
                case 3:
                    tape.AddEnv(day, TapeEnv(rng));
                    break;
                case 4:
                    tape.AddEvent(new MarketEvent { Day = day, Phase = rng.Next(MarketCatalogue.PhaseClose + 1), Kind = rng.Next(5), A = rng.Next(200) - 100, B = rng.Next(100000) });
                    break;
                case 5:
                    tape.SetSecret(day, (ulong)rng.Next());   // a guest following the host: a day's secret in place of its own
                    break;
                case 6:
                    tape.SetEnv(day, TapeEnv(rng));
                    break;
                case 7:
                    tape.Reach(day, rng.Next(MarketCatalogue.PhaseClose + 1), rng.Next(2) == 0);
                    break;
                case 8:
                    if (rng.Next(25) == 0)
                    {
                        tape.Forget(MarketState.Time(day, rng.Next(MarketCatalogue.PhaseClose + 1)));   // what a guest played alone, forgotten
                    }
                    break;
                default:
                    tape.TakePending();
                    break;
            }
            if (step % 40 == 39)
            {
                same &= string.Concat(tape.Lines()) == tape.Whole();
                rounds++;
            }
        }
        MarketTape read = MarketTape.Read(tape.Id, tape.Whole());
        bool readSame = string.Concat(read.Lines()) == read.Whole() && read.Whole() == tape.Whole();
        long before = tape.Changes;
        tape.Lines();
        tape.Whole();
        tape.Secret(1, out _);
        tape.Env(1, out _);
        tape.AddSecret(1, 5UL);   // a day it has: nothing changes
        bool still = tape.Changes == before;
        tape.AddEvent(new MarketEvent { Day = 3, Phase = 2, Kind = 1, A = 4, B = 5 });
        bool counted = tape.Changes > before;
        long frontier = tape.Changes;
        tape.WriteFrontier();
        bool frontierCounted = tape.Changes > frontier;   // a line given to the file: the file is no longer the last whole
        Check(same && rounds == 100 && readSame && still && counted && frontierCounted,
            "markets, the tape: its kept lines are the whole tape to the byte after secrets, the city, events, a guest's secrets and city in place of its own, events forgotten and the frontier, "
            + rounds + " times, and after a load; reading it counts no change, a change or a line for the file does (same " + same + ", after a load " + readSame + ", unchanged " + still
            + ", counted " + counted + ", the frontier's line " + frontierCounted + ")");

        // The file the writer makes from the lines is the file the whole text made, byte for byte.
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-tape-lines-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string byLines = System.IO.Path.Combine(folder, "a.tape"), byText = System.IO.Path.Combine(folder, "b.tape");
            MarketTapeSwap.Write(byLines, tape.Lines());
            MarketTapeSwap.Write(byText, tape.Whole());
            byte[] a = File.ReadAllBytes(byLines), b = File.ReadAllBytes(byText);
            Check(a.Length > 1000 && a.SequenceEqual(b) && a[0] == (byte)'T', "markets, the tape: written from its lines, the file is the whole text's to the byte, no byte order mark (" + a.Length + " bytes)");
        }
        finally
        {
            Directory.Delete(folder, true);
        }

        // The saving: a tape of 400 trading days, each with its city and a dozen events, made whole against handed over.
        var year = new MarketTape(42UL);
        var r = new Random(11);
        for (int d = 1; d <= 400; d++)
        {
            year.AddSecret(d, (ulong)r.Next() * 7919UL);
            year.AddEnv(d, TapeEnv(r));
            for (int k = 0; k < 12; k++)
            {
                year.AddEvent(new MarketEvent { Day = d, Phase = r.Next(MarketCatalogue.PhaseClose + 1), Kind = r.Next(5), A = r.Next(200), B = r.Next(100000) });
            }
            year.Reach(d, MarketCatalogue.PhaseClose, true);
            year.TakePending();
        }
        double whole = double.MaxValue, lines = double.MaxValue;
        for (int i = 0; i < 9; i++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            year.Whole();
            whole = Math.Min(whole, watch.Elapsed.TotalMilliseconds);
            watch.Restart();
            year.Lines();
            lines = Math.Min(lines, watch.Elapsed.TotalMilliseconds);
        }
        Check(lines < whole && lines < 1.0,
            "markets, the tape: a save's main-thread part on a tape of 400 days and 4800 events: " + lines.ToString("0.000", CultureInfo.InvariantCulture) + " ms to hand its lines over (wanted under 1 ms), "
            + whole.ToString("0.000", CultureInfo.InvariantCulture) + " ms to make it whole as every save did");
    }

    private static MarketEnv TapeEnv(Random rng)
    {
        MarketEnv e = MarketEnv.Quiet();
        e.Cycle = rng.Next(4);
        e.RateBp = rng.Next(3000);
        e.InflationBp = rng.Next(-500, 3000);
        e.PriceLevel = 100 + rng.Next(9000);
        e.DistrictIncome = 300 + rng.Next(2700);
        e.Crimes = rng.Next(1000);
        for (int k = 0; k < MarketEnv.SupplyKinds; k++)
        {
            e.Supply[k] = 300 + rng.Next(2700);
        }
        e.Trades = rng.Next(4);
        for (int i = 0; i < e.Trades; i++)
        {
            e.TradeOf[i] = rng.Next(20);
            e.TradeSales[i] = rng.Next(1000000);
        }
        if (rng.Next(3) == 0)
        {
            e.OwnDays = 1 + rng.Next(300);
            e.OwnRev = rng.Next(1000000);
            e.OwnStanding = rng.Next(100);
        }
        return e;
    }

    // A machine whose language writes its minus sign as U+2212: the line still says "-", and the host reads it back.
    private static void MarketTradeLines()
    {
        CultureInfo was = CultureInfo.CurrentCulture;
        var odd = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        odd.NumberFormat.NegativeSign = "−";
        var sent = new List<string>(4);
        try
        {
            CultureInfo.CurrentCulture = odd;
            var trades = new GuestTrades();
            trades.Add(5, -1500, 20L);
            trades.Send(sent.Add, GuestTrades.LinesMost);
            bool read = sent.Count == 1 && GuestTrades.Read(sent[0], out int stock, out int micros, out long shares) && stock == 5 && micros == -1500 && shares == 20L;
            Check(sent.Count == 1 && sent[0] == "i|5|-1500|20" && read,
                "markets, Going Public: a guest's falling trade is written \"i|5|-1500|20\" and read back on a machine of another language (" + (sent.Count > 0 ? sent[0] : "none") + ")");
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
        Check(!GuestTrades.Read("k|5|10|20", out _, out _, out _) && !GuestTrades.Read("i|5|10", out _, out _, out _) && !GuestTrades.Read(null, out _, out _, out _)
              && !GuestTrades.Read("i|5|1,5|20", out _, out _, out _) && GuestTrades.Read("i|7|200000|2147483648", out _, out int most, out long many) && most == 200000 && many == 2147483648L,
            "markets, Going Public: only a trade line is read, whole numbers only, a line's largest move and shares past an int as they are");
        string request = MemberText(ModSource(@"Markets\MarketDirector.GoingPublic.cs"), "private void HostRequest(string text, string who)");
        Check(request.Contains("GuestTrades.Read(lines[i], out int stock, out int micros, out long shares)") && !request.Contains("TryParse("),
            "markets, Going Public: the host reads a guest's trades only through the culture-free reader");
    }

    // A save's market with one company over the cap: the trusts join it, and the index stays where it was.
    private static void MarketJoinCapped()
    {
        MarketEngine m = NewMarket(141, 60);
        RunTo(m, 40);
        string line = MarketStore.Write(m.State);
        foreach (CompanyDef d in MarketCatalogue.All.Where(d => d.PayDays > 0))
        {
            line = RemoveCompany(line, d.Id);
        }
        MarketState state = MarketStore.Read(line, out _);
        Stock big = state != null ? state.Stocks.Where(s => s.Listed && !s.Def.Holding).OrderByDescending(s => s.Shares * s.Mid).FirstOrDefault() : null;
        if (big == null || !state.Weighted)
        {
            Check(false, "markets, the index: a weighted save's market to join the trusts to");
            return;
        }
        big.Shares *= 40;   // well over a tenth of the index
        bool[] capped = null;
        double weight = MarketIndex.WeightOf(state.Stocks, big, ref capped);
        var levels = typeof(MarketEngine).GetMethod("Index", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        object[] args = { 0 };
        levels.Invoke(new MarketEngine(state, new MarketTape(state.Id), new MarketWorld(1)), args);
        int index = (int)args[0];
        int joined = MarketSetup.Join(state);
        levels.Invoke(new MarketEngine(state, new MarketTape(state.Id), new MarketWorld(1)), args);
        int now = (int)args[0];
        Check(joined > 0 && Math.Abs(weight - MarketIndex.CapShare) < 1e-9 && Math.Abs(now - index) <= 1,
            "markets, the index: companies joining a save's market with one over the cap leave the index where it was (" + index + " to " + now + ", " + joined + " joined)");
    }

    // The tape written whole twice, then each state a crash can leave: a load finds a whole tape, the newest there is.
    private static void MarketTapeSwapped()
    {
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-tape-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = System.IO.Path.Combine(folder, "0000000000000001.tape");
            string fresh = MarketTapeSwap.NewOf(path), old = MarketTapeSwap.OldOf(path);
            MarketTapeSwap.Write(path, "first");
            MarketTapeSwap.Write(path, "second");
            Check(TapeText(path) == "second" && !File.Exists(fresh) && !File.Exists(old), "markets, the tape: written whole twice, the second is the tape and nothing is left beside it");

            MoveAside(path, null);
            File.WriteAllText(fresh, "whole");
            MarketTapeSwap.Settle(path);
            bool olderWriter = TapeText(path) == "whole" && !File.Exists(fresh);

            File.WriteAllText(fresh, "half wr");
            MarketTapeSwap.Settle(path);
            bool halfGone = TapeText(path) == "whole" && !File.Exists(fresh);

            MoveAside(path, old);
            File.WriteAllText(fresh, "newer");
            MarketTapeSwap.Settle(path);
            bool midSwap = TapeText(path) == "newer" && !File.Exists(fresh) && !File.Exists(old);

            MoveAside(path, old);
            MarketTapeSwap.Settle(path);
            bool onlyOld = TapeText(path) == "newer" && !File.Exists(old);

            File.WriteAllText(old, "stale");
            MarketTapeSwap.Settle(path);
            bool staleGone = TapeText(path) == "newer" && !File.Exists(old);
            Check(olderWriter && halfGone && midSwap && onlyOld && staleGone,
                "markets, the tape: a crash mid-swap leaves a whole tape a load finds (only the new one " + olderWriter + ", a half-written new one " + halfGone
                + ", both moved aside " + midSwap + ", only the old " + onlyOld + ", a stale old one " + staleGone + ")");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
        string file = ModSource(@"Markets\MarketTapeFile.cs");
        string read = MemberText(file, "public static MarketTape Read(ulong id)");
        Check(read.IndexOf("MarketTapeSwap.Settle(path);", StringComparison.Ordinal) >= 0
              && read.IndexOf("MarketTapeSwap.Settle(path);", StringComparison.Ordinal) < read.IndexOf("File.ReadAllText(path)", StringComparison.Ordinal)
              && MemberText(file, "private static void Write()").Contains("MarketTapeSwap.Write(job.Path, job.Lines);") && !file.Contains("File.Delete(job.Path)"),
            "markets, the tape: the whole tape is written only through the swap, and a read puts back one left mid-swap first");
    }

    // A crash's leavings made by hand: the tape moved aside (or deleted, with nowhere to go), if it is there.
    private static void MoveAside(string path, string to)
    {
        if (File.Exists(path) && to != null)
        {
            File.Move(path, to, true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    // A tape file's text, or null with none there.
    private static string TapeText(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    // Every pointer handler of the chart that draws stands down with the guard, as its frame methods do.
    private static void MarketChartGuarded()
    {
        string chart = ModSource(@"Markets\MarketChart.cs");
        int handlers = 0;
        bool guarded = chart.Length > 0;
        foreach (string head in new[] { "public void OnScroll(", "public void OnDrag(", "public void OnPointerMove(", "public void OnPointerExit(" })
        {
            string body = MemberText(chart, head);
            handlers += body.Contains("UiSafe(") ? 1 : 0;
            int guard = body.IndexOf("if (FrameGuard.Off)", StringComparison.Ordinal), work = body.IndexOf("try", StringComparison.Ordinal);
            guarded &= guard >= 0 && guard < work;
        }
        Check(guarded && handlers == 4, "markets, the chart: its scroll, drag, pointer move and pointer leaving each stand down once its guard is off, before any drawing");
    }
}
