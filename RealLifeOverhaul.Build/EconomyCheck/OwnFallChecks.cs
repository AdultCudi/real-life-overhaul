using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

// A fall of the player's own company's price (Scripts/Markets/Listing/OwnFallRules.cs, MarketEngine.OwnFall.cs): a player reported their company's price halving twice in a dozen game years with
// no split and results within a tenth of the quarter before. The engine does not halve a company on a rule: over many years of steady books its price wanders by the city, its sector and a mood
// that fades, a fall of a third in a year happening now and then. What was missing was the telling, so the price's fall is measured against its year's high and the close that first crosses
// under the limit says why (the books, the city's index, the sector's other companies or the market's mood), once a quarter, on every machine alike; the page carries the same line while the price stands under.
internal static partial class Program
{
    private static void OwnFallAll()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        OwnFallRulesChecks();
        OwnFallSteadyChecks();
        OwnFallShockChecks();
        OwnFallSources();
        Console.WriteLine("  a fall of the player's own company: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static void OwnFallRulesChecks()
    {
        var daily = new List<Candle>();
        for (int d = 1; d <= 40; d++)
        {
            int close = d <= 10 ? 1000 + d * 10 : 1100 - (d - 10) * 20;   // up to 1100 on day 10, then down by 20 a day
            daily.Add(new Candle(d, close, close, close, close, 1));
        }
        int bp = OwnFallRules.Drawdown(daily, 40, 500, 60, out int peak, out int peakDay);
        Check(bp == 5454 && peak == 1100 && peakDay == 10, "own fall: 500 against a high of 1100 on day 10 is 54.54% under it (" + bp + ", " + peak + " on " + peakDay + ")");
        int shorter = OwnFallRules.Drawdown(daily, 40, 500, 20, out peak, out peakDay);
        Check(shorter == (int)((peak - 500L) * 10000L / peak) && peakDay >= 20, "own fall: a shorter window looks only at the days inside it (high " + peak + " on " + peakDay + ")");
        Check(OwnFallRules.Drawdown(daily, 40, 1100, 60, out _, out _) == 0 && OwnFallRules.Drawdown(daily, 40, 2000, 60, out _, out _) == 0 && OwnFallRules.Drawdown(new List<Candle>(), 5, 100, 60, out _, out _) == 0,
            "own fall: a price at or over its high, or with no closes kept, is not under it");
        Check(OwnFallRules.CloseBefore(daily, 40) == daily[38].C && OwnFallRules.CloseOn(daily, 40) == daily[39].C && OwnFallRules.CloseOn(daily, 0) == 0, "own fall: yesterday's close and the close on a day are read from the kept closes");
        Check(OwnFallRules.WindowDays(60) == 60 && OwnFallRules.WindowDays(365) == 365 && OwnFallRules.GapDays(60) == 15 && OwnFallRules.GapDays(365) == 91 && OwnFallRules.GapDays(2) == 2,
            "own fall: the window is a game year and the gap between two notices a quarter, whatever the year's length");
        // the cause: figures first, then the larger of the city and the sector, each only by its share of the fall, else the mood
        Check(OwnFallRules.Cause(5000, -3000, 100, -3000, true, -1500) == OwnFallCause.Figures && OwnFallRules.Cause(5000, -3000, 100, -3000, true, -1499) == OwnFallCause.Market,
            "own fall: a profit down by 15% on the quarter before is the cause whatever else moved, one point short of it is not");
        Check(OwnFallRules.Cause(5000, -2000, 100, 0, false, 0) == OwnFallCause.Market && OwnFallRules.Cause(5000, -1999, 100, 0, false, 0) == OwnFallCause.Mood,
            "own fall: the city's index must stand for 40% of the fall to be named, one basis point short is the mood");
        Check(OwnFallRules.Cause(5000, -1500, 150, 0, false, 0) == OwnFallCause.Market && OwnFallRules.Cause(5000, -2900, 50, 0, false, 0) == OwnFallCause.Mood,
            "own fall: the index counts by the company's beta, a share that moves half as much is not blamed for what it would not have felt");
        Check(OwnFallRules.Cause(5000, -2100, 100, -2600, false, 0) == OwnFallCause.Sector && OwnFallRules.Cause(5000, -2600, 100, -2100, false, 0) == OwnFallCause.Market,
            "own fall: of the city and the sector both big enough, the larger is named");
        Check(OwnFallRules.Cause(5000, 800, 100, 1200, true, 300) == OwnFallCause.Mood && OwnFallRules.Cause(5000, 0, 100, 0, false, 0) == OwnFallCause.Mood,
            "own fall: a city and a sector that rose, and a profit that held, leave the market's mood");
    }

    // The company's close a day, collected by the check from the market's own state, so the crossings are counted independently of the engine's rule.
    private sealed class OwnFallLog
    {
        public readonly List<int> Days = new List<int>(), Closes = new List<int>();
        public readonly List<MarketNews> Notices = new List<MarketNews>();
        public readonly List<int> NoticeDays = new List<int>();
    }

    private static int OwnFallHigh(OwnFallLog log, int upTo, int day, int window)
    {
        int high = 0;
        for (int i = 0; i < upTo; i++)
        {
            if (log.Days[i] >= day - window && log.Days[i] <= day)
            {
                high = Math.Max(high, log.Closes[i]);
            }
        }
        return high;
    }

    private static OwnFallLog OwnFallPlay(ulong seed, int years, Action<MarketEngine, Stock, int> each, out MarketEngine m, out OwnWorld world)
    {
        world = new OwnWorld(seed);
        m = LListed(seed, world, 4000, out Stock own);
        var log = new OwnFallLog();
        if (own == null)
        {
            return log;
        }
        int start = m.State.Day, end = start + years * LYear, seen = 0;
        for (int d = start + 1; d <= end; d++)
        {
            if (!MarketCatalogue.TradingDay(d))
            {
                continue;
            }
            RunTo(m, d);
            Stock s = m.MineListed();
            if (s == null)
            {
                break;
            }
            log.Days.Add(d);
            log.Closes.Add(s.Last);
            for (; seen < world.Heard.Count; seen++)
            {
                if (world.Heard[seen].Kind == DerivedNews.OwnFall)
                {
                    log.Notices.Add(world.Heard[seen]);
                    log.NoticeDays.Add(world.Heard[seen].Day);
                }
            }
            each?.Invoke(m, s, d);
        }
        return log;
    }

    // Twelve years of steady books, five seeds: the price wanders and now and then stands a third under its year's high. The close that first crosses says so once a quarter at most, never otherwise.
    private static void OwnFallSteadyChecks()
    {
        int notices = 0, crossings = 0, silent = 0, extra = 0, spaced = 0, causeless = 0, falls = 0;
        double worstYear = 0.0;
        for (ulong seed = 1; seed <= 5; seed++)
        {
            OwnFallLog log = OwnFallPlay(seed, 12, null, out MarketEngine m, out OwnWorld world);
            int window = OwnFallRules.WindowDays(LYear), gap = OwnFallRules.GapDays(LYear), last = -1000;
            for (int i = 1; i < log.Days.Count; i++)
            {
                int day = log.Days[i];
                int high = OwnFallHigh(log, i + 1, day, window), highBefore = OwnFallHigh(log, i, day - 1, window);
                bool now = high > 0 && (high - log.Closes[i]) * 10000L / high >= OwnFallRules.FallBp;
                bool was = highBefore > 0 && (highBefore - log.Closes[i - 1]) * 10000L / highBefore >= OwnFallRules.FallBp;
                worstYear = Math.Max(worstYear, high > 0 ? (high - log.Closes[i]) / (double)high : 0.0);
                bool told = log.NoticeDays.Contains(day);
                if (now && !was && day - last >= gap)
                {
                    crossings++;
                    silent += told ? 0 : 1;
                    last = day;
                }
                else if (told)
                {
                    extra++;
                }
                if (now)
                {
                    falls++;
                }
            }
            for (int i = 0; i < log.Notices.Count; i++)
            {
                notices++;
                causeless += log.Notices[i].A == (int)OwnFallCause.None || log.Notices[i].B < OwnFallRules.FallBp ? 1 : 0;
                spaced += i > 0 && log.NoticeDays[i] - log.NoticeDays[i - 1] < gap ? 1 : 0;
            }
        }
        Check(crossings > 0 && falls > 0, "own fall: in sixty years of steady books the price stood a third under its year's high " + falls + " days, crossing " + crossings + " times (the check would be empty otherwise)");
        Check(silent == 0, "own fall: every first crossing under the limit was told (" + silent + " of " + crossings + " went unsaid)");
        Check(extra == 0, "own fall: nothing was told on a day that was not a crossing (" + extra + ")");
        Check(spaced == 0 && causeless == 0 && notices == crossings, "own fall: each notice names a cause and a fall over the limit and stands a quarter after the one before (" + notices + " notices)");
        Check(worstYear > OwnFallRules.FallBp / 10000.0 && worstYear < 0.8, "own fall: with steady books the price does fall a third in a year now and then and never to a fifth (the deepest " + (worstYear * 100).ToString("F0") + "% under a high)");
        // the same seed again gives the same notices: every machine says the same
        OwnFallLog a = OwnFallPlay(3, 12, null, out _, out _), b = OwnFallPlay(3, 12, null, out _, out _);
        bool same = a.Notices.Count == b.Notices.Count;
        for (int i = 0; same && i < a.Notices.Count; i++)
        {
            same &= a.Notices[i].Day == b.Notices[i].Day && a.Notices[i].A == b.Notices[i].A && a.Notices[i].B == b.Notices[i].B;
        }
        Check(same, "own fall: the same market played twice says the same things on the same days");
    }

    // A fall made on purpose after two years: the mood, the books, the rest of the city or the company's own sector.
    private static void OwnFallShockChecks()
    {
        foreach (string kind in new[] { "mood", "figures", "city", "sector" })
        {
            bool done = false;
            int shockDay = 0;
            OwnFallLog log = OwnFallPlay(11, 3, (m, s, d) =>
            {
                if (done || d < 2 * LYear + 220)
                {
                    return;
                }
                done = true;
                shockDay = d;
                double ln = Math.Log(0.5);
                s.Mis += ln;
                s.Sent += ln;
                if (kind == "figures")
                {
                    QuarterResult last = s.Results[s.Results.Count - 1];
                    last.ProfitK = s.Results[s.Results.Count - 2].ProfitK * 6 / 10;
                    s.Results[s.Results.Count - 1] = last;
                }
                if (kind == "city" || kind == "sector")
                {
                    for (int i = 0; i < m.State.Stocks.Count; i++)
                    {
                        Stock other = m.State.Stocks[i];
                        if (other != s && other.Listed && (other.Sector == s.Sector) == (kind == "sector"))
                        {
                            other.Mis += ln;
                            other.Sent += ln;
                        }
                    }
                }
            }, out MarketEngine engine, out OwnWorld world);
            Stock own = engine.MineListed();
            MarketNews n = default(MarketNews);
            int count = 0;
            for (int i = 0; i < log.Notices.Count; i++)
            {
                if (log.Notices[i].Day >= shockDay && log.Notices[i].Day <= shockDay + 8)
                {
                    n = log.Notices[i];
                    count++;
                }
            }
            OwnFallCause want = kind == "mood" ? OwnFallCause.Mood : kind == "figures" ? OwnFallCause.Figures : kind == "sector" ? OwnFallCause.Sector : OwnFallCause.Market;
            Check(done && own != null && count == 1, "own fall (" + kind + "): a price cut in half is told once within a week (" + count + " notices, shock on day " + shockDay + ")");
            Check(count == 1 && n.A == (int)want && n.B >= OwnFallRules.FallBp, "own fall (" + kind + "): the cause named is " + want + " (" + (OwnFallCause)n.A + ", " + n.B + " bp under)");
            OwnFall f = own != null ? own.Own.Fall : default(OwnFall);
            Check(own != null && f.PeakCents > f.NowCents && f.PeakDay > 0 && f.Cause == want && (kind != "mood" || f.WorthCents > 0),
                "own fall (" + kind + "): the figures it was told by are kept for the line (high " + f.PeakCents + " on " + f.PeakDay + ", now " + f.NowCents + ", worth " + f.WorthCents + ")");
            if (own != null)
            {
                int high = OwnFallHigh2(own, engine.State.Day, LYear);
                bool under = high > 0 && (high - own.Last) * 10000L / high >= OwnFallRules.FallBp;
                Check(OwnFallRules.Measure(engine.State, own, engine.State.Day, own.Last, engine.State.Env).Fell == under, "own fall (" + kind + "): the page's measure agrees with the closes about whether the price stands under the limit");
            }
        }
    }

    private static int OwnFallHigh2(Stock s, int day, int window)
    {
        int high = 0;
        for (int i = 0; i < s.Daily.Count; i++)
        {
            if (s.Daily[i].Day >= day - window && s.Daily[i].Day <= day)
            {
                high = Math.Max(high, s.Daily[i].C);
            }
        }
        return high;
    }

    private static void OwnFallSources()
    {
        string rules = ModSource(@"Markets\Listing\OwnFallRules.cs"), engine = ModSource(@"Markets\MarketEngine.OwnFall.cs"), close = ModSource(@"Markets\MarketEngine.Close.cs");
        string director = ModSource(@"Markets\MarketDirector.Own.cs"), figures = ModSource(@"Markets\MarketFigures.cs"), text = ModSource(@"Markets\MarketText.cs"), words = ModSource(@"Markets\Listing\OwnFallText.cs");
        Check(close.Contains("OwnFalls(day);") && engine.Contains("if (Replaying)") && engine.Contains("SayDerived(new MarketNews") && engine.Contains("DerivedNews.OwnFall") && !engine.Contains("Event("),
            "own fall: the close measures it, not on a replayed day, and the line is one the machine derives (never on the tape)");
        Check(director.Contains("case DerivedNews.OwnFall:") && director.Contains("\"reallife_own_fall_note\"") && figures.Contains("OwnFallRules.Measure(") && figures.Contains("\"reallife_mkt_fig_fall\"")
              && text.Contains("case DerivedNews.OwnFall:") && words.Contains("\"reallife_own_fall_row\""), "own fall: the founder is told, and the company's page and the news carry the line, in both interfaces' shared rows");
        Check(rules.Contains("OwnFallCause.Mood") && !rules.Contains("Random") && !rules.Contains("DateTime"), "own fall: the cause is a pure function of the market's state");
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_own_fall_note", "reallife_own_fall_row", "reallife_mkt_fig_fall", "reallife_own_fall_why_figures", "reallife_own_fall_why_market", "reallife_own_fall_why_sector",
            "reallife_own_fall_why_mood", "reallife_own_fall_why_mood_plain"
        };
        bool all = true, wording = true;
        foreach (string k in keys)
        {
            all &= en.TryGetValue(k, out string said) && !string.IsNullOrWhiteSpace(said);
            wording &= text.Contains("\"" + k + "\"") || words.Contains("\"" + k + "\"") || director.Contains("\"" + k + "\"") || figures.Contains("\"" + k + "\"");
        }
        Check(all && wording, "own fall: every text is written and used by whole key");
        // The longest notice: a long name, a high of a thousand, the longest cause clause, filled in.
        string note = en.TryGetValue("reallife_own_fall_note", out string n) ? n : string.Empty, mood = en.TryGetValue("reallife_own_fall_why_mood", out string m) ? m : string.Empty;
        string longest = note.Replace("{name}", "Harbour Lights Holdings Group").Replace("{change}", "99.9%").Replace("{peak}", "$1,234.56").Replace("{day}", "12345").Replace("{why}", mood.Replace("{worth}", "$1,234.56"));
        Check(longest.Length > 0 && longest.Length <= 240 && !longest.Contains("{"), "own fall: the notice with the longest cause is " + longest.Length + " characters, within the notice's 240");
        bool style = true;
        foreach (string k in keys)
        {
            string t = en.TryGetValue(k, out string v) ? v : string.Empty;
            style &= t.IndexOf('—') < 0 && t.IndexOf('–') < 0 && t.IndexOf('!') < 0 && t.IndexOf(';') < 0;
        }
        Check(style, "own fall: no dash, exclamation mark or semicolon in the words");
        Check(WhatsNewHas(en, "A big fall in your own company's share price now comes with a notice and a line on its page that say what caused it"), "own fall: What's new tells of the line");
    }
}
