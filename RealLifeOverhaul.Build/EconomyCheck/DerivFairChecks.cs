using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Markets;

// No money printer: over many seeds of whole years of the market, every way of buying a future or an option and holding it
// to its day loses on average, by about its spread and commission, and the fair prices are the true expectations. Timing
// (buying the day after a fall or a rise, on a Monday or a Thursday) earns nothing either; the words in every language.
internal static partial class Program
{
    private struct Bucket
    {
        public double Paid, Fair, Got;
        public int Count;
    }

    private static void DerivFairChecks()
    {
        MaterialsFair();
        SharesFair();
    }

    // Materials: the exchange's own process, through the engine, so the very closes the game would play.
    private static void MaterialsFair()
    {
        var futures = new Dictionary<string, Bucket>();
        var options = new Dictionary<string, Bucket>();
        var timing = new Dictionary<string, Bucket>();
        int seeds = 8, lastDay = 300;
        for (int seed = 1; seed <= seeds; seed++)
        {
            MarketEngine m = DerivMarket(4000UL + (ulong)seed, 1000000000L, out MarketWorld world);
            var pending = new List<(int settle, string name, string key, bool call, long strike, long ask, long fair, long forward, long askF, long bidF, string time)>();
            var days = new int[4];
            for (int d = 1; d <= lastDay; d++)
            {
                world.City.PriceLevel = (int)Math.Round(1000.0 * Math.Pow(1.0 + 0.02 / 12.0, (d - 1) / 5));
                if (!MarketCatalogue.TradingDay(d))
                {
                    RunTo(m, d, MarketCatalogue.PhaseClose);
                    continue;
                }
                RunTo(m, d, MarketCatalogue.PhaseOpen + 30);
                int n = m.ContractDays(days);
                bool quote = d < lastDay - 30;
                for (int i = 0; quote && i < m.State.Materials.Count; i += 3)
                {
                    RawMaterial mat = m.State.Materials[i];
                    int lastMove = MaterialRules.MoveBp(mat, 1);
                    string time = lastMove > 0 ? "after a rise" : lastMove < 0 ? "after a fall" : "flat";
                    for (int w = 0; w < n; w++)
                    {
                        DerivQuote f = m.QuoteFuture(mat.Name, days[w], true, 1000);
                        foreach (int step in DerivCatalogue.StrikeStepsBp)
                        {
                            foreach (bool call in new[] { true, false })
                            {
                                DerivQuote o = m.QuoteOption(call, mat.Name, 0, days[w], step, 1000);
                                pending.Add((days[w], mat.Name, (call ? "call " : "put ") + step + "bp, week " + (w + 1), call, o.Strike, o.Ask, o.Fair, f.Forward, f.Ask, f.Bid, time));
                            }
                        }
                    }
                }
                RunTo(m, d, MarketCatalogue.PhaseClose);
                foreach (var p in pending.Where(p => p.settle == d).ToList())
                {
                    long price = m.MaterialOf(p.name).Fix;
                    long pays = OptionRules.Intrinsic(p.call, price, p.strike);
                    double f = p.forward;   // every sum a share of its forward, so a cheap material counts as much as a dear one
                    Add(options, p.key, p.ask / f, p.fair / f, pays / f);
                    if (p.call && p.strike == DerivRules.Strike(p.forward, 0))
                    {
                        Add(futures, "long", p.askF / f, 1.0, price / f);
                        Add(futures, "short", -p.bidF / f, -1.0, -price / f);
                        Add(timing, "long " + p.time, p.askF / f, 1.0, price / f);
                        Add(timing, "short " + p.time, -p.bidF / f, -1.0, -price / f);
                    }
                }
                pending.RemoveAll(p => p.settle <= d);
            }
        }
        // The forward is the price expected: what a future settles at, on average, is its fair price, both ways.
        Bucket lf = futures["long"];
        double bias = lf.Got / lf.Fair - 1.0;
        Console.WriteLine("  materials, fair: " + lf.Count + " futures, what they settled at against their fair price " + (bias * 100).ToString("+0.00;-0.00") + "%");
        Check(Math.Abs(bias) < 0.006, "fair: a material's future settles on average at its fair price, to within 0.6% over thousands (" + (bias * 100).ToString("0.00") + "%)");
        Check(futures["long"].Got < futures["long"].Paid && futures["short"].Got < futures["short"].Paid,
            "fair: a future bought and held to its day loses on average, and so does one sold: the spread is the exchange's");
        string timed = string.Join("; ", timing.Where(t => t.Value.Count >= 500 && t.Value.Got >= t.Value.Paid).Select(t => t.Key));
        Check(timed.Length == 0, "fair: buying or selling after a rise, after a fall or on a flat day earns nothing (" + timed + ")");
        // Every option, by kind, strike and week: what it paid against its fair premium, and against what it cost.
        string worse = string.Empty, off = string.Empty;
        foreach (KeyValuePair<string, Bucket> b in options.OrderBy(x => x.Key))
        {
            double ratio = b.Value.Got / Math.Max(1e-9, b.Value.Fair);
            if (b.Value.Got >= b.Value.Paid)
            {
                worse += b.Key + " (" + (b.Value.Got / b.Value.Paid).ToString("0.000") + "); ";
            }
            if (b.Value.Fair > b.Value.Count * 0.01 && Math.Abs(ratio - 1.0) > 0.10)
            {
                off += b.Key + " (" + ratio.ToString("0.000") + "); ";
            }
        }
        Bucket all = Sum(options.Values);
        Console.WriteLine("  materials, options: " + all.Count + " bought; paid out " + (all.Got / all.Fair).ToString("0.000") + " of their fair premiums, " + (all.Got / all.Paid).ToString("0.000")
                          + " of what they cost");
        Check(worse.Length == 0, "fair: every option on a material, of every kind, strike and week, bought and held, pays out less than it cost (" + worse + ")");
        Check(off.Length == 0 && Math.Abs(all.Got / all.Fair - 1.0) < 0.03, "fair: on average an option on a material pays out its fair premium (" + off + ")");
    }

    private static void Add(Dictionary<string, Bucket> buckets, string key, double paid, double fair, double got)
    {
        buckets.TryGetValue(key, out Bucket b);
        b.Paid += paid;
        b.Fair += fair;
        b.Got += got;
        b.Count++;
        buckets[key] = b;
    }

    private static Bucket Sum(IEnumerable<Bucket> buckets)
    {
        var all = new Bucket();
        foreach (Bucket b in buckets)
        {
            all.Paid += b.Paid;
            all.Fair += b.Fair;
            all.Got += b.Got;
            all.Count += b.Count;
        }
        return all;
    }

    // Shares: the market's own companies through whole years, each option priced as the Stocks app would offer it and held
    // to its day, settled as the engine settles it (the last hour's mean, or what a company that left paid out).
    private static void SharesFair()
    {
        var options = new Dictionary<string, Bucket>();
        var bySize = new Dictionary<string, Bucket>();
        var drift = new Bucket();
        int seeds = 4, lastDay = 330;
        for (int seed = 1; seed <= seeds; seed++)
        {
            MarketEngine m = DerivMarket(5000UL + (ulong)seed, 1000000000L, out MarketWorld world);
            var pending = new List<(int settle, int stock, string key, string size, bool call, long strike, long ask, long fair, long forward, long shares, int quoted)>();
            var days = new int[4];
            for (int d = 1; d <= lastDay; d++)
            {
                if (!MarketCatalogue.TradingDay(d))
                {
                    RunTo(m, d, MarketCatalogue.PhaseClose);
                    continue;
                }
                RunTo(m, d, MarketCatalogue.PhaseOpen + 30);
                int n = m.ContractDays(days);
                bool quote = d > 25 && d < lastDay - 30 && MarketCatalogue.Weekday(d) % 2 == 0;
                for (int i = 0; quote && i < m.State.Stocks.Count; i++)
                {
                    Stock s = m.State.Stocks[i];
                    if (DerivRules.ShareOffered(s, d) != DerivWhy.None)
                    {
                        continue;
                    }
                    for (int w = 0; w < n; w++)
                    {
                        foreach (int step in DerivCatalogue.StrikeStepsBp)
                        {
                            foreach (bool call in new[] { true, false })
                            {
                                DerivQuote o = m.QuoteOption(call, null, s.Id, days[w], step, 100);
                                pending.Add((days[w], s.Id, (call ? "call " : "put ") + step + "bp, week " + (w + 1), s.Size.ToString(), call, o.Strike, o.Ask, o.Fair, o.Forward, s.Shares, d));
                            }
                        }
                    }
                }
                RunTo(m, d, MarketCatalogue.PhaseClose);
                foreach (var p in pending.Where(p => p.settle == d || m.State.Stock(p.stock) == null || !m.State.Stock(p.stock).Listed).ToList())
                {
                    Stock s = m.State.Stock(p.stock);
                    long price = s == null ? p.strike : !s.Listed ? (long)s.GoneCents * 10000 : DerivRules.SettleMicros(s);
                    long ratio = s != null && p.shares > 0 ? Math.Max(1L, (long)Math.Round((double)s.Shares / p.shares)) : 1L;   // a split since: a share then is ratio now
                    price *= ratio;
                    long pays = OptionRules.Intrinsic(p.call, price, p.strike);
                    double f = Math.Max(1, p.forward);   // a share of its forward, so every company counts alike
                    Add(options, p.key, p.ask / f, p.fair / f, pays / f);
                    Add(bySize, p.size + (p.call ? " calls" : " puts"), p.ask / f, p.fair / f, pays / f);
                    if (p.call && p.strike > 0 && p.key.StartsWith("call 0bp", StringComparison.Ordinal))
                    {
                        drift.Fair += 1.0;
                        drift.Got += price / f;
                        drift.Count++;
                    }
                }
                pending.RemoveAll(p => p.settle <= d || m.State.Stock(p.stock) == null || !m.State.Stock(p.stock).Listed);
            }
        }
        Bucket all = Sum(options.Values);
        double forwardBias = drift.Got / drift.Fair - 1.0;
        Console.WriteLine("  shares, options: " + all.Count + " bought; paid out " + (all.Got / all.Fair).ToString("0.000") + " of their fair premiums, " + (all.Got / all.Paid).ToString("0.000")
                          + " of what they cost; settled against the forward " + (forwardBias * 100).ToString("+0.00;-0.00") + "%");
        foreach (KeyValuePair<string, Bucket> b in bySize.OrderBy(x => x.Key))
        {
            Console.WriteLine("    " + b.Key + ": " + b.Value.Count + ", paid out " + (b.Value.Got / b.Value.Fair).ToString("0.000") + " of fair, " + (b.Value.Got / b.Value.Paid).ToString("0.000")
                              + " of cost");
        }
        // The market's own way over a few years moves calls against puts, so each strike and week is judged as a straddle (a
        // call and a put together, the market's way cancelled): every one pays out less than it cost; and no option is priced
        // grossly off its fair premium.
        string worse = string.Empty, off = string.Empty;
        foreach (int step in DerivCatalogue.StrikeStepsBp)
        {
            for (int w = 1; w <= DerivCatalogue.WeeksAhead; w++)
            {
                Bucket c = options["call " + step + "bp, week " + w], p = options["put " + step + "bp, week " + w];
                if (c.Got + p.Got >= c.Paid + p.Paid)
                {
                    worse += step + "bp week " + w + " (" + ((c.Got + p.Got) / (c.Paid + p.Paid)).ToString("0.000") + "); ";
                }
                foreach (Bucket b in new[] { c, p })
                {
                    double ratio = b.Got / Math.Max(1e-9, b.Fair);
                    if (ratio < 0.75 || ratio > 1.25)
                    {
                        off += step + "bp week " + w + " (" + ratio.ToString("0.000") + "); ";
                    }
                }
            }
        }
        Check(worse.Length == 0, "fair: on shares, a call and a put of every strike and week together, bought and held, pay out less than they cost (" + worse + ")");
        Check(off.Length == 0 && all.Got / all.Fair > 0.9 && all.Got / all.Fair < 1.06 && all.Got / all.Paid < 0.95,
            "fair: no option on shares is priced far off what it pays out on average, and together they pay out their fair premium, less than they cost (" + off + ")");
        string sizes = string.Join("; ", bySize.Where(b => b.Value.Got >= b.Value.Paid).Select(b => b.Key));
        Check(sizes.Length == 0, "fair: large and middling companies' calls, and their puts, each pay out less than they cost (" + sizes + ")");
        Check(Math.Abs(forwardBias) < 0.01, "fair: a share's forward is the price it settles at on average, to within 1% (" + (forwardBias * 100).ToString("0.00") + "%)");
    }

    // ---------------------------------------------------------------- the words

    private static void DerivWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("futures and options", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0];
        string[] keys = en.Keys.Where(k => k.StartsWith("reallife_mkt_drv_", StringComparison.Ordinal)).Concat(new[]
        {
            "reallife_mkt_tab_futures", "reallife_opt_tip_markets_derivatives", "reallife_meta_title_markets_derivatives", "reallife_guide_markets_derivatives",
            "reallife_guide_markets_derivatives_about", DerivMoney.Margin, DerivMoney.Back, DerivMoney.Bought, DerivMoney.Sold, DerivMoney.Paid, DerivMoney.Goods
        }).ToArray();
        Check(keys.Length > 120, "words: the futures and options have their texts (" + keys.Length + ")");
        string missing = string.Join(", ", keys.Where(k => LanguagesLacking(langs, codes, k).Length > 0).Select(k => k + " (" + LanguagesLacking(langs, codes, k) + ")"));
        Check(missing.Length == 0, "words: every text of the futures and options is in every language with the same {names} (" + missing + ")");
        string[] named = DerivCatalogue.GroupKeys.Concat(new[] { "reallife_mkt_drv_filter_materials", "reallife_mkt_drv_filter_companies", "reallife_mkt_drv_filter_contracts" }).ToArray();
        Check(named.All(en.ContainsKey) && Enumerable.Range(0, (int)DerivWhy.Count).All(i => en.ContainsKey(WhyKeyOf((DerivWhy)i))),
            "words: every group, filter and reason has its sentence");
        string longNotes = string.Join(", ", en.Where(kv => kv.Key.StartsWith("reallife_mkt_drv_note_", StringComparison.Ordinal) && kv.Value.Length > 200).Select(kv => kv.Key));
        Check(longNotes.Length == 0, "words: every notification is short (240 characters with its names at most: " + longNotes + ")");
        Check(new[] { DerivMoney.Margin, DerivMoney.Back, DerivMoney.Bought, DerivMoney.Sold, DerivMoney.Paid }.All(k => k.Contains("shares_") && en.ContainsKey(k) && en.ContainsKey(k + "_label")
                  && RealLifeOverhaul.Economy.CashCategory.For(k) == RealLifeOverhaul.Economy.CashCategory.Investments)
              && en.ContainsKey(DerivMoney.Goods + "_label") && RealLifeOverhaul.Economy.CashCategory.For(DerivMoney.Goods) == RealLifeOverhaul.Economy.CashCategory.Stock,
            "words: each movement is a transaction of its own with its text and label: the margin and options under investments, a future's goods under stock");
        string guide = en["reallife_guide_markets_derivatives"];
        Check(guide.Contains("# Futures") && guide.Contains("# Options") && guide.Contains("{drv_margin_farm}") && guide.Contains("{drv_maintenance}")
              && langs[1]["reallife_guide_markets_derivatives"].Contains("{drv_settle_steps}") && langs[2]["reallife_guide_markets_derivatives"].Contains("{drv_units}"),
            "words: the Guide's page tells the exchange, futures and options with their figures, in each language");
        // In the next row until its version is made, and in that version's own row after (the release empties the next row).
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("new Futures tab"))
            {
                line = !row.Value.StartsWith("New: ", StringComparison.Ordinal) && !row.Value.StartsWith("Fixed: ", StringComparison.Ordinal);
                break;
            }
        }
        Check(line, "words: What's new has the feature's line, with no prefix (in the next row, or its version's row once it is made)");
    }

    private static string WhyKeyOf(DerivWhy why)
    {
        string[] names = { "none", "closed", "held", "behind", "off", "no_city", "unknown", "not_offered", "too_many", "too_much", "no_cash", "not_held", "ended", "no_place", "no_call" };
        return "reallife_mkt_drv_why_" + names[(int)why];
    }
}
