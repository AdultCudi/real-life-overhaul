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
        MarketChartGuarded();
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
              && MemberText(file, "private static void Write()").Contains("MarketTapeSwap.Write(job.Path, job.Text);") && !file.Contains("File.Delete(job.Path)"),
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
