using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Markets;

// The stock market's why and its size segments (Scripts/Markets/MarketWhy.cs, MarketSegments.cs, MarketEngine.Why.cs, MarketStatement.cs, MarketFigures.cs): a line of news says why a sector or
// a large company moved only from a cause the engine applied that day, and none for a move it drew at random; a company's segment is held to the catalogue's bands by its market value,
// reviewed once a quarter with a margin so it does not flicker, and a change leaves its price where it was; the player's company has the same review; its statement is split from
// the figures the books already have. Each source check is also run on a made-up wrong case, which must fail.
internal static partial class Program
{
    private static void MarketWhySegments()
    {
        MarketSegmentRules();
        MarketSegmentCatalogue();
        MarketSegmentEngine();
        MarketSegmentSaves();
        MarketWhyKicks();
        MarketWhyRules();
        MarketWhyRun();
        MarketStatementRules();
        WhySegmentSources();
    }

    private static void MarketSegmentRules()
    {
        int level = 1000;
        double large = MarketSegments.Band(MarketSegments.LargeFromM, level), mid = MarketSegments.Band(MarketSegments.MidFromM, level);
        Check(large > mid * 2 && mid > 0 && MarketSegments.HoldBp > 0 && MarketSegments.HoldBp < 5000, "segments: the large band is well over the mid band, and the margin is a fifth");
        Check(MarketSegments.Of(large, level) == MarketSize.Large && MarketSegments.Of(large - 1, level) == MarketSize.Mid && MarketSegments.Of(mid, level) == MarketSize.Mid && MarketSegments.Of(mid - 1, level) == MarketSize.Small
              && MarketSegments.Of(0, level) == MarketSize.Small, "segments: a market value at a band is in the segment above it, a unit under it is not");
        Check(MarketSegments.Band(MarketSegments.LargeFromM, 2000) == 2 * large && MarketSegments.Band(MarketSegments.MidFromM, 50) == MarketSegments.Band(MarketSegments.MidFromM, 100), "segments: the bands grow with the price level, as the companies' turnovers do, and not below its floor");
        // a review: up at the band, down only a fifth under it
        Check(MarketSegments.Review(MarketSize.Small, large * 2, level) == MarketSize.Large && MarketSegments.Review(MarketSize.Small, large, level) == MarketSize.Large && MarketSegments.Review(MarketSize.Small, mid, level) == MarketSize.Mid
              && MarketSegments.Review(MarketSize.Mid, large, level) == MarketSize.Large, "segments: a company reaching a band joins the segment above it, skipping one if it is far over");
        double leave = large * (10000 - MarketSegments.HoldBp) / 10000.0, midLeave = mid * (10000 - MarketSegments.HoldBp) / 10000.0;
        Check(MarketSegments.Review(MarketSize.Large, large * 0.99, level) == MarketSize.Large && MarketSegments.Review(MarketSize.Large, leave, level) == MarketSize.Large
              && MarketSegments.Review(MarketSize.Large, leave * 0.99, level) == MarketSize.Mid && MarketSegments.Review(MarketSize.Large, midLeave * 0.99, level) == MarketSize.Small,
            "segments: a large company stays large down to a fifth under its band, and falls only past that, to mid or small by its value");
        Check(MarketSegments.Review(MarketSize.Mid, mid * 0.99, level) == MarketSize.Mid && MarketSegments.Review(MarketSize.Mid, midLeave * 0.99, level) == MarketSize.Small && MarketSegments.Review(MarketSize.Small, mid * 0.99, level) == MarketSize.Small,
            "segments: a mid company stays mid down to a fifth under its band, a small one joins it only at the band");
        // no flicker: a value that crosses the band and back every quarter changes segment once, not each time
        MarketSize now = MarketSize.Small;
        int changes = 0;
        var rng = new Random(3);
        for (int quarter = 0; quarter < 400; quarter++)
        {
            double value = large * (0.9 + rng.NextDouble() * 0.2);   // ten per cent either side of the band
            MarketSize next = MarketSegments.Review(now, value, level);
            changes += next != now ? 1 : 0;
            now = next;
        }
        Check(changes <= 2, "segments: a value wandering ten per cent either side of the band changes the segment at most twice (small to mid to large) in a hundred years of quarters (" + changes + ")");
        changes = 0;
        now = MarketSize.Large;
        for (int quarter = 0; quarter < 400; quarter++)
        {
            double value = large * (0.8 + rng.NextDouble() * 0.4);   // a fifth under to a fifth over
            MarketSize next = MarketSegments.Review(now, value, level);
            changes += next != now ? 1 : 0;
            now = next;
        }
        Check(changes < 100, "segments: even a value that swings a fifth either side moves between segments far less than every quarter (" + changes + " changes in 400)");
        Check(MarketSegments.Due(-1, 3) && MarketSegments.Due(2, 3) && !MarketSegments.Due(3, 3), "segments: a company is due its review in a quarter it has not been reviewed in");
        Check(MarketSegments.NextBandBp(MarketSize.Large, large, level) == 10000 && MarketSegments.NextBandBp(MarketSize.Small, mid / 2, level) == 5000 && MarketSegments.NextBandBp(MarketSize.Mid, large * 2, level) == 10000,
            "segments: how near the next band is read from the value, and a large company has none above it");
    }

    private static List<Stock> MarketWhyCompanies(MarketEngine m)
    {
        return m.State.Stocks.Where(s => s.Listed && s.Own == null && s.Def != null && !s.Def.Holding && s.RivalId == null).ToList();
    }

    private static void MarketSegmentCatalogue()
    {
        // The catalogue's rows give each company's starting segment; the bands are read from them. Print what each segment's companies are worth at the start.
        var worth = new double[3][];
        var seen = new List<double>[] { new List<double>(), new List<double>(), new List<double>() };
        for (ulong seed = 1; seed <= 3; seed++)
        {
            MarketEngine m = NewMarket(seed + 70, 60, 1, 0);
            foreach (Stock s in MarketWhyCompanies(m))
            {
                seen[(int)s.Size].Add(s.MarketValue);
            }
        }
        double[] least = new double[3], most = new double[3];
        for (int z = 0; z < 3; z++)
        {
            seen[z].Sort();
            worth[z] = seen[z].ToArray();
            least[z] = worth[z].Length > 0 ? worth[z][0] : 0.0;
            most[z] = worth[z].Length > 0 ? worth[z][worth[z].Length - 1] : 0.0;
            Console.WriteLine("  segments: " + (MarketSize)z + " at the start: " + worth[z].Length + " companies over three markets, worth " + (least[z] / 1e6).ToString("N0") + "M to " + (most[z] / 1e6).ToString("N0") + "M, median "
                              + (worth[z].Length > 0 ? worth[z][worth[z].Length / 2] / 1e6 : 0.0).ToString("N0") + "M");
        }
        double large = MarketSegments.Band(MarketSegments.LargeFromM, 1000), mid = MarketSegments.Band(MarketSegments.MidFromM, 1000);
        int wrong = 0, all = 0;
        for (ulong seed = 1; seed <= 3; seed++)
        {
            MarketEngine m = NewMarket(seed + 70, 60, 1, 0);
            foreach (Stock s in MarketWhyCompanies(m))
            {
                all++;
                wrong += MarketSegments.Review(s.Size, s.MarketValue, m.State.Env.PriceLevel) != s.Size ? 1 : 0;
            }
        }
        var values = new List<(MarketSize size, double value)>();
        for (ulong seed = 1; seed <= 3; seed++)
        {
            foreach (Stock s in MarketWhyCompanies(NewMarket(seed + 70, 60, 1, 0)))
            {
                values.Add((s.Size, s.MarketValue));
            }
        }
        int bestWrong = int.MaxValue;
        double bestLarge = 0, bestMid = 0;
        for (double lg = 2000; lg <= 20000; lg += 250)
        {
            for (double md = 300; md <= 3000 && md < lg; md += 50)
            {
                int bad = 0;
                foreach (var v in values)
                {
                    MarketSize now = v.value >= lg * 1e6 ? MarketSize.Large : v.value >= md * 1e6 ? MarketSize.Mid : MarketSize.Small;
                    // with the margin: a company already in a segment stays down to a fifth under its band
                    MarketSize kept = v.size == MarketSize.Large && v.value >= lg * 1e6 * 0.8 ? MarketSize.Large : v.size == MarketSize.Mid && v.value < lg * 1e6 && v.value >= md * 1e6 * 0.8 ? MarketSize.Mid
                        : v.size == MarketSize.Small && v.value < md * 1e6 ? MarketSize.Small : now;
                    bad += kept != v.size ? 1 : 0;
                }
                if (bad < bestWrong)
                {
                    bestWrong = bad;
                    bestLarge = lg;
                    bestMid = md;
                }
            }
        }
        Console.WriteLine("  segments: the bands that keep the most catalogue companies where their rows put them, with the margin: large from " + bestLarge + "M, mid from " + bestMid + "M, moving " + bestWrong + " of " + values.Count);
        Check(all > 150 && wrong * 100 <= all * 15, "segments: the bands keep at least eighty five in a hundred of the catalogue's companies in the segment its row gives them at the start (" + wrong + " of " + all + " would change at the first review)");
        Check(mid < large / 2 && least[(int)MarketSize.Large] > most[(int)MarketSize.Small], "segments: the bands put the large over the small, with the mid between (" + (mid / 1e6).ToString("N0") + "M, " + (large / 1e6).ToString("N0") + "M)");
    }

    private static int MarketFirstCloseOfQuarter(MarketEngine m, int day)
    {
        int year = m.State.YearDays;
        return TradingOnOrAfter(MarketCatalogue.QuarterStart(MarketCatalogue.QuarterOf(day, year) + 1, year));
    }

    private static void MarketSegmentEngine()
    {
        var world = new MarketWorld(11);
        MarketEngine m = NewMarket(11, 60, 1, 0, world);
        int year = m.State.YearDays;
        int first = TradingOnOrAfter(1);
        var list = MarketWhyCompanies(m).Where(s => s.BidCents <= 0 && s.SplitDay <= 0).ToList();
        int level = m.State.Env.PriceLevel;
        var movers = list.Where(s => MarketSegments.Review(s.Size, s.MarketValue, level) != s.Size).ToList();
        var from = movers.ToDictionary(s => s.Id, s => s.Size);
        Check(movers.Count > 0 && movers.Count * 100 <= list.Count * 15, "segments: at the start some of the catalogue's companies are over a band their row does not match, and no more than fifteen in a hundred (" + movers.Count + " of " + list.Count + ")");
        RunTo(m, first, MarketCatalogue.PhaseClose);
        int quarter = MarketCatalogue.QuarterOf(first, year);
        var before = movers.ToDictionary(s => s.Id, s => s.LogFair + s.Mis);
        Check(movers.All(s => s.Size == from[s.Id]), "segments: a company's segment changes at the next moment, never inside the close that decided it");
        int next = TradingOnOrAfter(first + 1);
        RunTo(m, next, MarketCatalogue.PhaseResults);
        double worst = 0.0;
        int changed = 0;
        foreach (Stock s in movers)
        {
            worst = Math.Max(worst, Math.Abs(s.LogFair + s.Mis - before[s.Id]));
            changed += s.Size != from[s.Id] && s.Size == MarketSegments.Review(from[s.Id], s.MarketValue, level) ? 1 : 0;
        }
        Check(changed >= movers.Count * 9 / 10 && movers.All(s => s.SizeQuarter == quarter), "segments: at the first close every company is reviewed and the ones over a band take its segment at the next moment (" + changed + " of " + movers.Count + ")");
        Check(worst < 1e-9, "segments: no price moves with a company's segment: the largest change of a price over the moment is " + worst.ToString("0.0E+0"));
        int told = m.State.News.Count(n => n.Kind == MarketNewsKind.Segment);
        Check(told >= changed - 1 && m.State.News.Where(n => n.Kind == MarketNewsKind.Segment).All(n => n.A != n.B && n.C >= 0 && (!from.ContainsKey(n.Stock) || n.B == (int)from[n.Stock])),
            "segments: each change is in the news, from its old segment to the new, with the market value in millions (" + told + " pieces)");
        Check(list.All(s => s.SizeQuarter == quarter), "segments: every company is marked reviewed in its quarter");
        // once a quarter: nothing more in the middle of it, everything again at the next
        int mid = TradingOnOrAfter(first + 2);
        RunTo(m, mid, MarketCatalogue.PhaseClose);
        Check(list.All(s => s.SizeQuarter == quarter), "segments: a company is reviewed once a quarter, not at every close of it");
        int boundary = TradingOnOrAfter(MarketCatalogue.QuarterStart(quarter + 1, year));
        RunTo(m, boundary, MarketCatalogue.PhaseClose);
        RunTo(m, TradingOnOrAfter(boundary + 1), MarketCatalogue.PhaseResults);
        Check(list.All(s => s.SizeQuarter == quarter + 1 || !s.Listed || s.BidCents > 0), "segments: at the first close of the next quarter every company is reviewed again");
        // the option off, nobody is reviewed
        var off = NewMarket(12, 60, 1, 0);
        off.ExtrasOn = false;
        int f = TradingOnOrAfter(1);
        RunTo(off, f, MarketCatalogue.PhaseClose);
        RunTo(off, TradingOnOrAfter(f + 1), MarketCatalogue.PhaseResults);
        Check(off.State.Stocks.All(s => s.SizeQuarter == -1) && !off.State.News.Any(n => n.Kind == MarketNewsKind.Segment), "segments: with the extras option off nobody is reviewed and every company keeps its catalogue segment");
    }

    private static void MarketSegmentSaves()
    {
        MarketEngine m = NewMarket(21, 60, 1, 0);
        List<Stock> list = MarketWhyCompanies(m);
        Stock a = list[0], b = list[1];
        a.Size = MarketSize.Small;
        a.SizeQuarter = 5;
        a.SizeDay = 70;
        b.Size = (MarketSize)((int)b.Def.Size == 0 ? 1 : 0);
        b.SizeQuarter = 6;
        b.SizeDay = 80;
        list[2].SizeQuarter = -1;
        string text = MarketStore.Write(m.State);
        MarketState back = MarketStore.Read(text, out _);
        Stock ba = back.Stock(a.Id), bb = back.Stock(b.Id), bc = back.Stock(list[2].Id);
        Check(ba != null && ba.Size == MarketSize.Small && ba.SizeQuarter == 5 && ba.SizeDay == 70 && bb.Size == b.Size && bb.SizeQuarter == 6 && bb.SizeDay == 80 && bc.SizeQuarter == -1 && bc.Size == list[2].Def.Size,
            "segments: each company's segment, the quarter it was reviewed in and the day it changed survive the save; one never reviewed keeps the catalogue's");
        int at = text.IndexOf("~s:", StringComparison.Ordinal);
        int end = text.IndexOf('~', at + 3);
        Check(at > 0, "segments: the reviews are in a part of the market's line of their own");
        string older = text.Substring(0, at) + (end < 0 ? string.Empty : text.Substring(end));
        MarketState reader = MarketStore.Read(older, out _);
        Stock oa = reader != null ? reader.Stock(a.Id) : null;
        Check(oa != null && oa.Size == oa.Def.Size && oa.SizeQuarter == -1 && oa.LogFair == a.LogFair && oa.Mis == a.Mis, "segments: a build that does not know the part skips it, and every company is as the catalogue has it with its price where it was");
        string wrong = text.Replace("~s:" + a.Id + ",", "~s:" + a.Id + ",9");
        MarketState clamped = MarketStore.Read(wrong, out _);
        Check(clamped != null && (int)clamped.Stock(a.Id).Size <= (int)MarketSize.Small, "segments: a segment out of range in a save is brought within it");
        // news of the two new kinds survives a save, in kinds the line knows
        m.State.News.Add(new MarketNews { Day = 3, Kind = MarketNewsKind.Why, Stock = 0, A = (int)WhyCause.Weather | (7 << 8), B = -90, C = 2 });
        m.State.News.Add(new MarketNews { Day = 3, Kind = MarketNewsKind.Segment, Stock = a.Id, A = 0, B = 2, C = 7500 });
        MarketState news = MarketStore.Read(MarketStore.Write(m.State), out _);
        Check(news.News.Any(n => n.Kind == MarketNewsKind.Why && n.A == ((int)WhyCause.Weather | (7 << 8)) && n.B == -90 && n.C == 2) && news.News.Any(n => n.Kind == MarketNewsKind.Segment && n.C == 7500),
            "why: a piece of news that says why and one that says a segment changed survive the save");
        Check((int)MarketNewsKind.Why == 36 && (int)MarketNewsKind.Segment == 37 && (int)MarketNewsKind.Count == 38 && MarketInputs.EventSegment == MarketInputs.EventSplit + 1 && MarketInputs.EventCount == MarketInputs.EventSegment + 1,
            "why: the new kinds of news and of event are numbered after the others, never in their place");
    }

    private static void MarketWhyKicks()
    {
        var rng = new Random(9);
        bool same = true;
        var parts = new int[7];
        for (int i = 0; i < 4000; i++)
        {
            MarketEnv env = MarketEnv.Quiet();
            env.Sky = rng.Next(3);
            env.Spell = rng.Next(4);
            env.Event = rng.Next(8);
            env.Crimes = rng.Next(40);
            env.BreakIns = rng.Next(40);
            env.Fires = rng.Next(12);
            env.Accidents = rng.Next(30);
            var sector = (MarketSector)rng.Next((int)MarketSector.Count);
            MarketInputs.DayKickParts(env, sector, parts);
            same &= parts.Sum() == MarketInputs.DayKickBp(env, sector);
        }
        Check(same, "why: the parts of a sector's one-off move at the open (the sky, the spell, the street event and the day's reports) add up to the move the engine applies");
    }

    private static WhyDay MarketWhyQuietDay(MarketState s, int day)
    {
        var w = new WhyDay();
        w.Clear(day);
        return w;
    }

    private static void MarketWhyRules()
    {
        MarketEngine m = NewMarket(31, 60, 1, 0);
        MarketState s = m.State;
        int ins = (int)MarketSector.Insurance, banks = (int)MarketSector.Banks;
        MarketEnv quiet = MarketEnv.Quiet();
        WhyDay w = MarketWhyQuietDay(s, 5);
        // nothing applied, no cause: a move that was only the market's mood has no line
        bool none = true;
        for (int sec = 0; sec < (int)MarketSector.Count; sec++)
        {
            foreach (int move in new[] { -600, -90, 90, 600 })
            {
                none &= !MarketWhy.ForSector(s, w, quiet, quiet, true, sec, move, 5, out WhyPick p) && p.Cause == WhyCause.None;
            }
        }
        Check(none, "why: with nothing applied that day no sector's move has a cause, whatever its size");
        // the sky: a storm took insurers down 40 bp
        MarketEnv storm = quiet;
        storm.Sky = 2;
        Check(MarketWhy.ForSector(s, w, storm, quiet, true, ins, -90, 5, out WhyPick weather) && weather.Cause == WhyCause.Weather && weather.Detail == 2 && weather.Bp < 0,
            "why: insurers down 0.9% on a day of storm are said to be the storm's");
        Check(!MarketWhy.ForSector(s, w, storm, quiet, true, ins, 90, 5, out _), "why: ...and when they rose that day the storm is not named");
        Check(!MarketWhy.ForSector(s, w, storm, quiet, true, ins, -300, 5, out _) && MarketWhy.ForSector(s, w, storm, quiet, true, ins, -190, 5, out _),
            "why: a cause must account for a fifth of the move: 40 bp is no cause of a fall of 3% and is of one of 1.9%");
        Check(!MarketWhy.ForSector(s, w, storm, quiet, true, banks, -90, 5, out _), "why: ...and the sky is not named for a sector it did not touch");
        MarketEnv heat = quiet;
        heat.Spell = 1;
        MarketEnv fest = quiet;
        fest.Event = 3;
        Check(MarketWhy.ForSector(s, w, heat, quiet, true, (int)MarketSector.Power, 60, 5, out WhyPick spell) && spell.Cause == WhyCause.Spell && spell.Detail == 1
              && MarketWhy.ForSector(s, w, fest, quiet, true, (int)MarketSector.Leisure, 70, 5, out WhyPick street) && street.Cause == WhyCause.Street && street.Detail == 3,
            "why: a heat wave lifts power companies and a parade lifts leisure, and are named with which they were");
        // reports and events heard of
        MarketEnv fires = quiet;
        fires.Fires = 3;
        Check(MarketWhy.ForSector(s, w, fires, quiet, true, ins, -40, 5, out WhyPick fire) && fire.Cause == WhyCause.Fire, "why: three fires reported yesterday are named for insurers' fall");
        w.Event(ins, WhyCause.Accident, -30);
        Check(MarketWhy.ForSector(s, w, quiet, quiet, true, ins, -40, 5, out WhyPick heard) && heard.Cause == WhyCause.Accident && heard.Bp == -30, "why: an event heard of that day is named, with what it applied");
        w.Clear(5);
        // a policy priced, an election counted
        w.PolicyLever = 5;
        w.PolicyDirection = 1;
        w.PolicyLn[(int)MarketSector.Property] = -0.012;
        w.PolicyN[(int)MarketSector.Property] = 1;
        Check(MarketWhy.ForSector(s, w, quiet, quiet, true, (int)MarketSector.Property, -100, 5, out WhyPick policy) && policy.Cause == WhyCause.Policy && policy.Detail == 105 && policy.Bp == -120,
            "why: a council policy priced that day is named with its lever and its direction, and what it did to the sector's worth");
        w.Clear(5);
        w.Election = true;
        Check(MarketWhy.ForSector(s, w, quiet, quiet, true, banks, 50, 5, out WhyPick vote) && vote.Cause == WhyCause.Election && !MarketWhy.ForSector(s, w, quiet, quiet, true, banks, -50, 5, out _),
            "why: an election counted that morning is named for a rise and not for a fall");
        w.Clear(5);
        // a craze, begun, running and burst
        s.ManiaDays[(int)MarketSector.Software] = 7;
        Check(MarketWhy.ForSector(s, w, quiet, quiet, true, (int)MarketSector.Software, 80, 5, out WhyPick craze) && craze.Cause == WhyCause.Craze && !MarketWhy.ForSector(s, w, quiet, quiet, true, (int)MarketSector.Software, -80, 5, out _),
            "why: a craze that is running is named for a rise in its sector");
        s.News.Add(new MarketNews { Day = 5, Kind = MarketNewsKind.Mania, A = (int)MarketSector.Software, B = 2 });
        Check(MarketWhy.ForSector(s, w, quiet, quiet, true, (int)MarketSector.Software, -600, 5, out WhyPick burst) && burst.Cause == WhyCause.CrazeBurst && burst.Bp == MarketWhy.BurstBp() && MarketWhy.BurstBp() < -500,
            "why: a craze that burst that morning is named for the fall, and is worth what the drift of its days was");
        s.ManiaDays[(int)MarketSector.Software] = 0;
        s.News.RemoveAt(s.News.Count - 1);
        s.News.Add(new MarketNews { Day = 5, Kind = MarketNewsKind.Crash, A = -900 });
        Check(MarketWhy.ForSector(s, w, quiet, quiet, true, banks, -700, 5, out WhyPick crash) && crash.Cause == WhyCause.Crash && !MarketWhy.ForSector(s, w, quiet, quiet, true, banks, -700, 6, out _),
            "why: a crash that morning is named for the fall, and only on its day");
        s.News.RemoveAt(s.News.Count - 1);
        // the city between yesterday and today: the economy, the rate, inflation, goods, incomes
        MarketEnv slump = quiet;
        slump.Cycle = 3;
        Check(MarketWhy.ForSector(s, w, slump, quiet, true, banks, -200, 5, out WhyPick economy) && economy.Cause == WhyCause.Economy && economy.Detail == 3 && economy.Bp < 0
              && !MarketWhy.ForSector(s, w, slump, quiet, true, banks, 200, 5, out _) && !MarketWhy.ForSector(s, w, slump, quiet, false, banks, -200, 5, out _) && MarketWhy.ForSector(s, w, quiet, slump, true, banks, 200, 5, out WhyPick up) && up.Cause == WhyCause.Economy,
            "why: a sector whose companies were revalued for the city's economy turning is told so, in the direction of the move, and not without yesterday's city");
        MarketEnv dear = quiet;
        dear.RateBp = quiet.RateBp + 100;
        dear.RateNextBp = quiet.RateNextBp + 100;
        bool rate = MarketWhy.ForSector(s, w, dear, quiet, true, (int)MarketSector.Property, -150, 5, out WhyPick rated);
        Check(!rate || (rated.Cause == WhyCause.Rate && rated.Detail == 100 && rated.Bp < 0), "why: a rise of the bank rate that took a sector's worth down is named with its size (" + (rate ? rated.Bp.ToString() : "too small to name") + ")");
        MarketEnv goods = quiet;
        goods.Supply = new[] { 1300, 1300, 1300, 1300, 1300, 1300 };
        bool consistent = true;
        int named = 0;
        for (int sec = 0; sec < (int)MarketSector.Count; sec++)
        {
            foreach (int move in new[] { -100, 100 })
            {
                if (MarketWhy.ForSector(s, w, goods, quiet, true, sec, move, 5, out WhyPick p) && p.Cause == WhyCause.Prices)
                {
                    named++;
                    consistent &= (p.Bp > 0) == (move > 0) && Math.Abs(p.Bp) >= MarketWhy.LeastBp;
                }
            }
        }
        Check(consistent, "why: dearer goods are named only where the valuation moved with them and only in the direction of the move (" + named + " sectors, a margin's change reaches the price through the business, not the open)");
        // a company
        Stock k = s.Stocks.First(x => x.Listed && x.Own == null && x.Size == MarketSize.Large);
        s.News.Add(new MarketNews { Day = 5, Kind = MarketNewsKind.Results, Stock = k.Id, A = 500, B = 400, C = 450 });
        Check(MarketWhy.ForStock(s, k, 450, 5, false, default, 0, out WhyPick results) && results.Cause == WhyCause.Results && results.Detail == 1 && !MarketWhy.ForStock(s, k, -450, 5, false, default, 0, out _),
            "why: a company that rose on the day its results beat what was expected is told so, and one that fell on it is not");
        s.News.Add(new MarketNews { Day = 5, Kind = MarketNewsKind.Crisis, Stock = k.Id, A = 700 });
        Check(MarketWhy.ForStock(s, k, -800, 5, false, default, 0, out WhyPick crisis) && crisis.Cause == WhyCause.Crisis && crisis.Bp == -700, "why: a company that fell on the day of its crisis is told so, with what the crisis cost it");
        s.News.Add(new MarketNews { Day = 5, Kind = MarketNewsKind.Bid, Stock = k.Id, A = 5000, B = 9, C = 0 });
        MarketWhy.ForStock(s, k, -300, 5, false, default, 0, out WhyPick fell);
        Check(MarketWhy.ForStock(s, k, 900, 5, false, default, 0, out WhyPick bid) && bid.Cause == WhyCause.Bid && fell.Cause != WhyCause.Bid, "why: a bid is named for a rise, never for a fall");
        s.News.Add(new MarketNews { Day = 5, Kind = MarketNewsKind.Builder, Stock = k.Id, A = (int)BuilderNewsKind.Strike, B = -400, C = 3 });
        Check(MarketWhy.ForStock(s, k, -450, 5, false, default, 0, out WhyPick builder) && (builder.Cause == WhyCause.Builder || builder.Cause == WhyCause.Crisis), "why: a builder's news that moved its price is named for its fall");
        var sectorPick = new WhyPick { Cause = WhyCause.Weather, Detail = 1, Bp = -40 };
        Stock quiet2 = s.Stocks.First(x => x.Listed && x.Own == null && x.Size == MarketSize.Large && x.Id != k.Id);
        Check(MarketWhy.ForStock(s, quiet2, -150, 5, true, sectorPick, -100, out WhyPick withSector) && withSector.Cause == WhyCause.Weather && !MarketWhy.ForStock(s, quiet2, -400, 5, true, sectorPick, -100, out _)
              && !MarketWhy.ForStock(s, quiet2, 150, 5, true, sectorPick, -100, out _) && !MarketWhy.ForStock(s, quiet2, -150, 5, false, sectorPick, -100, out _),
            "why: a company moving with its sector in the same direction by at least half as much carries its sector's cause; one that moved more than twice as much, the other way, or in no sector that moved, has none");
        // the piece of news
        MarketNews piece = MarketWhy.News(5, 0, (int)MarketSector.Insurance, -90, weather);
        Check(piece.Kind == MarketNewsKind.Why && MarketWhy.CauseOf(piece) == WhyCause.Weather && MarketWhy.SectorOf(piece) == ins && piece.B == -90 && piece.C == 2 && piece.Stock == 0, "why: the piece of news keeps the cause, the sector, the move and the detail");
        Check(MarketWhy.CauseOf(new MarketNews { A = 255 }) == WhyCause.Count - 1 && MarketWhy.SectorOf(new MarketNews { A = 255 << 8 }) == (int)MarketSector.Count - 1, "why: a piece of news read from a save is brought within the causes and sectors there are");
    }

    private static void MarketWhyRun()
    {
        var runs = new List<MarketNews>[2];
        int pieces = 0, stockPieces = 0;
        for (int round = 0; round < 2; round++)
        {
            var world = new MarketWorld(41);
            MarketEngine m = NewMarket(41, 60, 1, 0, world);
            var all = new List<MarketNews>();
            for (int day = 2; day <= 420; day += 7)
            {
                RunTo(m, TradingOnOrAfter(day), MarketCatalogue.PhaseClose);
                foreach (MarketNews n in m.State.News)
                {
                    if (!all.Any(x => x.Day == n.Day && x.Kind == n.Kind && x.Stock == n.Stock && x.A == n.A && x.B == n.B && x.C == n.C))
                    {
                        all.Add(n);
                    }
                }
            }
            runs[round] = all;
            if (round == 0)
            {
                bool bounds = true, tape = true;
                foreach (MarketNews n in all.Where(x => x.Kind == MarketNewsKind.Why))
                {
                    pieces++;
                    stockPieces += n.Stock > 0 ? 1 : 0;
                    WhyCause c = MarketWhy.CauseOf(n);
                    bounds &= c != WhyCause.None && c < WhyCause.Count && Math.Abs(n.B) >= (n.Stock > 0 ? MarketWhy.CompanyLeastBp : MarketWhy.SectorLeastBp) && (n.Stock == 0 || m.State.Stock(n.Stock) != null);
                    if (m.Tape.Env(n.Day, out MarketEnv env) && n.Stock == 0)
                    {
                        tape &= c != WhyCause.Spell || env.Spell == n.C;
                        tape &= c != WhyCause.Street || env.Event == n.C;
                        tape &= c != WhyCause.Economy || env.Cycle == n.C;
                    }
                }
                Check(bounds, "why: over sixty weeks of market days every piece of why names a cause, a move of the size that is notable, and a company the market lists");
                Check(tape, "why: what a piece says about the sky, the street event or the economy is what the city's day had");
            }
        }
        Console.WriteLine("  why: " + pieces + " pieces of why in sixty weeks, " + stockPieces + " of them about a company");
        bool same = runs[0].Count == runs[1].Count;
        for (int i = 0; same && i < runs[0].Count; i++)
        {
            same &= runs[0][i].Day == runs[1][i].Day && runs[0][i].Kind == runs[1][i].Kind && runs[0][i].Stock == runs[1][i].Stock && runs[0][i].A == runs[1][i].A && runs[0][i].B == runs[1][i].B && runs[0][i].C == runs[1][i].C;
        }
        Check(same && pieces > 0, "why: the same market played twice says the same things, why included (" + runs[0].Count + " pieces of news, " + pieces + " of why)");
    }

    private static void MarketStatementRules()
    {
        QuarterStatement q = MarketStatement.Split(15000, 1404, 2000, 0.22);
        Check(Math.Abs(q.Costs + q.Pay + q.Tax + q.Net - q.Revenue) < 1e-6 && Math.Abs(q.Pay - 450) < 1e-6 && Math.Abs(q.Tax - 396) < 1e-6 && Math.Abs(q.Costs - 12750) < 1e-6 && q.Known,
            "statement: takings split into costs, the founder's pay, the tax and what is left, and they add up to the takings");
        QuarterStatement loss = MarketStatement.Split(10000, -1500, 2000, 0.22);
        Check(loss.Pay == 0 && loss.Tax == 0 && Math.Abs(loss.Costs - 11500) < 1e-6 && Math.Abs(loss.Costs + loss.Pay + loss.Tax + loss.Net - loss.Revenue) < 1e-6, "statement: a loss has no pay and no tax, and its costs are the takings and the loss");
        Check(MarketStatement.Split(0, 0, 2000, 0.22).Net == 0 && MarketStatement.Split(-5, 0, 0, 0.22).Revenue == 0, "statement: nothing taken is nothing in every line");
        var env = MarketEnv.Quiet();
        var stock = new Stock
        {
            Growth = 0.0, QRev = 7000, QNi = 700, Own = new OwnListing { PayBp = 2000, RevQ = 1000, NiQ = 100, RevY = 1000, NiY = 120 }
        };
        QuarterStatement now = MarketStatement.Now(stock, env);
        QuarterStatement next = MarketStatement.Next(stock, env, 5, 60, out int days);
        Check(now.Known && now.Revenue == 7000 && now.Net == 700 && days == 15 && Math.Abs(next.Revenue - 15000) < 1e-6 && Math.Abs(next.Net - 1404) < 1e-6 && Math.Abs(next.Costs - 12750) < 1e-6,
            "statement: the next quarter is the last quarter's rate over its days at the year's margin, less the pay and the tax (" + next.Revenue + " in, " + next.Net + " left over " + days + " days)");
        stock.Growth = 0.2;
        Check(MarketStatement.Next(stock, env, 5, 60, out _).Revenue > 15000, "statement: a company the market believes will grow is projected to take more");
        Check(!MarketStatement.Next(new Stock(), env, 5, 60, out _).Known && !MarketStatement.Now(new Stock(), env).Known, "statement: a company that is not the player's has no statement");
        stock.Own.RevQ = 0;
        Check(!MarketStatement.Next(stock, env, 5, 60, out _).Known, "statement: with no takings to follow nothing is projected");
    }

    private static void WhySegmentSources()
    {
        string engine = ModSource(@"Markets\MarketEngine.cs"), why = ModSource(@"Markets\MarketEngine.Why.cs"), close = ModSource(@"Markets\MarketEngine.Close.cs"), session = ModSource(@"Markets\MarketEngine.Session.cs");
        string page = ModSource(@"Markets\MarketPage.cs"), kit = ModSource(@"Ui\Stocks\StocksBuild.Figures.cs"), text = ModSource(@"Markets\MarketText.cs"), ledger = ModSource(@"Ledger\LedgerCatalogue.cs");
        string director = ModSource(@"Markets\MarketDirector.News.cs"), figures = ModSource(@"Markets\MarketFigures.cs"), store = ModSource(@"Markets\MarketStore.cs"), guide = ModSource(@"Markets\Extras\ExtrasGuide.cs");
        // the review is decided only where a close is played first, and put on the tape
        string review = MemberText(why, "private void ExtrasSegments(int day)");
        Func<string, bool> gated = t => t.Contains("if (!ExtrasOn || State.Following || Replaying)") && t.Contains("Event(MarketInputs.EventSegment, s.Id, (int)next);");
        Check(gated(review) && !gated(review.Replace("State.Following || ", string.Empty)) && close.Contains("ExtrasSegments(day);"), "segments: the review is decided only where the close is played first (not a guest, not a replay, not with the option off) and put on the tape");
        Check(engine.Contains("SegmentEvent(e);") && MemberText(why, "private void SegmentEvent(in MarketEvent e)").Contains("Settle(s, State.Day);") && MemberText(why, "private void SegmentEvent(in MarketEvent e)").IndexOf("s.Size = next;", StringComparison.Ordinal)
              < MemberText(why, "private void SegmentEvent(in MarketEvent e)").IndexOf("Settle(s, State.Day);", StringComparison.Ordinal), "segments: a segment from the tape is applied and the price settled where it was, in that order");
        Check(close.Contains("WhyNews(day, bestSector, bestBp, up, upBp, down, downBp);") && session.Contains("WhyOpen(day, env);") && engine.Contains("WhyHeard(e);") && session.Contains("_why.Election = true;"), "why: the day's record is kept as the engine applies a cause, and the close names the cause from it");
        string heard = MemberText(why, "private void WhyHeard(in MarketEvent e)");
        Check(heard.Contains("WhyCause.Fire") && heard.Contains("WhyCause.Weather") && heard.Contains("WhyCause.Buzz") && heard.Contains("MarketInputs.EventBp("), "why: an event heard of is written down by what it did to each sector");
        Check(!why.Contains("MarketRandom") && !why.Contains("new Random") && !why.Contains("_secret"), "why: nothing is drawn in naming a cause, so the lines are the same on every machine");
        // both interfaces, the news, the paper and the notification
        Check(page.Contains("MarketFigures.Rows(") && kit.Contains("MarketFigures.Rows("), "why: the old page and the kit's Figures view both carry the segment, the news and the player's statement");
        Check(text.Contains("case MarketNewsKind.Why:") && text.Contains("case MarketNewsKind.Segment:") && ledger.Contains("\"_mkt_news_why\"") && ledger.Contains("\"_mkt_news_segment\"") && director.Contains("case MarketNewsKind.Segment:"),
            "why: the news, the City Ledger and the notification know both new kinds");
        Check(figures.Contains("PageNews(s, k, day, NewsShown, news);") && figures.Contains("MarketStatement.Now(") && figures.Contains("MarketStatement.Next(") && figures.Contains("ListingSector.Top("), "why: the page's rows come from the market's own figures: the news, the mix by sector, the books' statement");
        Check(store.Contains("Segments(t, s);") && store.Contains("ReadSegments(s, segments);") && guide.Contains("stx_seg_large"), "segments: the save writes and reads the reviews, and the Guide's facts are the catalogue's bands");
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_mkt_news_why_up", "reallife_mkt_news_why_down", "reallife_mkt_cause_crash", "reallife_mkt_cause_rate_up", "reallife_mkt_cause_rate_down", "reallife_mkt_cause_election", "reallife_mkt_cause_craze",
            "reallife_mkt_cause_craze_begun", "reallife_mkt_cause_craze_burst", "reallife_mkt_cause_rain", "reallife_mkt_cause_storm", "reallife_mkt_cause_heat", "reallife_mkt_cause_cold", "reallife_mkt_cause_ice",
            "reallife_mkt_cause_street_market", "reallife_mkt_cause_street_farmers", "reallife_mkt_cause_street_parade", "reallife_mkt_cause_street_protest", "reallife_mkt_cause_street_run",
            "reallife_mkt_cause_street_shoot", "reallife_mkt_cause_street_fireworks", "reallife_mkt_cause_crime", "reallife_mkt_cause_breakin", "reallife_mkt_cause_fire", "reallife_mkt_cause_accident",
            "reallife_mkt_cause_buzz", "reallife_mkt_cause_policy_tight", "reallife_mkt_cause_policy_relax", "reallife_mkt_cause_economy_boom", "reallife_mkt_cause_economy_steady", "reallife_mkt_cause_economy_slow",
            "reallife_mkt_cause_economy_recession", "reallife_mkt_cause_inflation_up", "reallife_mkt_cause_inflation_down", "reallife_mkt_cause_prices", "reallife_mkt_cause_incomes_up", "reallife_mkt_cause_incomes_down",
            "reallife_mkt_cause_results_beat", "reallife_mkt_cause_results_miss", "reallife_mkt_cause_crisis", "reallife_mkt_cause_windfall", "reallife_mkt_cause_bid", "reallife_mkt_cause_builder",
            "reallife_mkt_news_segment_up", "reallife_mkt_news_segment_down", "reallife_mkt_note_segment", "reallife_mkt_fig_segment", "reallife_mkt_fig_segment_value", "reallife_mkt_fig_news", "reallife_mkt_fig_lines",
            "reallife_mkt_fig_line_value", "reallife_mkt_fig_q_now", "reallife_mkt_fig_q_next", "reallife_mkt_fig_q_expect", "reallife_mkt_fig_statement"
        };
        string wording = ModSource(@"Markets\MarketWhyText.cs");
        bool all = true, ours = true, short240 = true;
        foreach (string k in keys)
        {
            all &= en.TryGetValue(k, out string said) && !string.IsNullOrWhiteSpace(said);
            ours &= text.Contains("\"" + k + "\"") || wording.Contains("\"" + k + "\"") || figures.Contains("\"" + k + "\"") || director.Contains("\"" + k + "\"");
            short240 &= en.TryGetValue(k, out string t) && t.Length <= 240;
        }
        Check(all && ours && short240, "why: every new text is in the English file, written whole in the code that uses it, and a notice is at most 240 characters");
        bool style = true;
        foreach (string k in keys)
        {
            style &= en.TryGetValue(k, out string said) && !said.Contains('—') && !said.Contains('–') && !said.Contains(" - ") && !said.Contains('!');
        }
        Check(style, "why: no dash and no exclamation mark in any of them");
        Check(en.TryGetValue("reallife_guide_markets", out string g) && g.Contains("# Why prices move") && en.TryGetValue("reallife_guide_markets_extras", out string e) && e.Contains("{stx_seg_large}") && e.Contains("{stx_seg_hold}"),
            "why: the Guide says why prices move and what the segments are, with the bands as facts");
        Check(WhatsNewHas(en, "says why a sector or a large company moved"), "why: What's new tells of the why and the segments");
        Check(en.TryGetValue("reallife_opt_tip_markets_extras", out string tip) && tip.Split(' ').Length <= 32 && tip.Length <= 190 && tip.Contains("sizes"), "why: the extras option's tip names the sizes, in at most 32 words and 190 characters");
    }
}
