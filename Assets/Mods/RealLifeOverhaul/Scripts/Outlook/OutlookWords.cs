using System;
using System.Globalization;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// The Economy app's sentences, game-free so that EconomyCheck can read them: a figure written one way everywhere, and the
    /// verdict's line and each piece of advice composed from a key's text and the figures the rule gave. The texts and the
    /// money and district names come from whoever asks (the game's locale in play, the English file in the checks).
    /// </summary>
    public sealed class OutlookWords
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A key's text.</summary>
        public Func<string, string> T;
        /// <summary>Whole money as the game writes it.</summary>
        public Func<long, string> Money;
        /// <summary>A district's id as its name.</summary>
        public Func<string, string> District;

        /// <summary>The six kinds of goods' names, the city's economy's own texts (Economy/City/CityText.KindKeys).</summary>
        public static readonly string[] KindKeys =
        {
            "reallife_macro_kind_fresh", "reallife_macro_kind_groceries", "reallife_macro_kind_clothing", "reallife_macro_kind_electronics",
            "reallife_macro_kind_home", "reallife_macro_kind_general"
        };

        public static readonly string[] UseKeys =
        {
            "reallife_econapp_use_homes", "reallife_econapp_use_shops", "reallife_econapp_use_offices", "reallife_econapp_use_warehouses"
        };

        // ------------------------------------------------------------------ figures

        /// <summary>Basis points as a per cent: "4.2%", "-0.5%"; the figure's own sign.</summary>
        public static string Percent(int bp)
        {
            return (bp / 100.0).ToString("0.#", Inv) + "%";
        }

        /// <summary>Basis points as a per cent with its sign: "+4.2%", "-0.5%", "0%".</summary>
        public static string Signed(int bp)
        {
            return (bp > 0 ? "+" : string.Empty) + Percent(bp);
        }

        /// <summary>Thousandths as a decimal with one place: the price index, 104.2.</summary>
        public static string Index(int thousandths)
        {
            return (thousandths / 1000.0).ToString("0.0", Inv);
        }

        /// <summary>A rate in basis points: "4.25%".</summary>
        public static string Rate(int bp)
        {
            return (bp / 100.0).ToString("0.##", Inv) + "%";
        }

        /// <summary>Quarter points as a per cent: "0.25%", "1%".</summary>
        public static string Quarters(int quarters)
        {
            return (Math.Abs(quarters) / (double)MacroCatalogue.QuartersPerPoint).ToString("0.##", Inv) + "%";
        }

        /// <summary>Hundredths of a per cent: inflation, "2.1%".</summary>
        public static string Hundredths(int hundredths)
        {
            return (hundredths / 100.0).ToString("0.0#", Inv) + "%";
        }

        /// <summary>Per mille as a per cent: unemployment, "3.5%".</summary>
        public static string Permille(int permille)
        {
            return (permille / 10.0).ToString("0.0", Inv) + "%";
        }

        /// <summary>Tenths of a point: an income level, "104.5".</summary>
        public static string Tenths(int tenths)
        {
            return (tenths / 10.0).ToString("0.#", Inv);
        }

        /// <summary>Thousandths as a change on 1000: "+4.2%".</summary>
        public static string Factor(int thousandths)
        {
            return Signed(thousandths - 1000);
        }

        /// <summary>A per cent of customers with its sign: "+6%", "-12%".</summary>
        public static string Whole(int percent)
        {
            return (percent > 0 ? "+" : string.Empty) + percent.ToString(Inv) + "%";
        }

        /// <summary>A whole per cent without a sign, for a sentence that says which way: "6%".</summary>
        public static string Plain(int percent)
        {
            return Math.Abs(percent).ToString(Inv) + "%";
        }

        // ------------------------------------------------------------------ the verdict

        public string VerdictWord(OutlookVerdict verdict)
        {
            return T(OutlookRules.VerdictKeys[(int)verdict]);
        }

        /// <summary>The verdict in a sentence, with the figures it stands on.</summary>
        public string VerdictLine(OutlookVerdict verdict, OutlookReadings r)
        {
            string key = OutlookRules.VerdictLineKeys[(int)verdict];
            if (verdict == OutlookVerdict.Growing && r.Cycle != (int)CycleState.Boom)
            {
                key += "_steady";
            }
            else if (verdict == OutlookVerdict.Slowing && r.Cycle != (int)CycleState.Slowdown)
            {
                key += "_steady";
            }
            return Fill(key, "footfall", Plain(r.Footfall), "spend", Plain((r.Spend - 1000) / 10), "inflation", Hundredths(r.Inflation), "target", Hundredths(MacroCatalogue.InflationTarget));
        }

        // ------------------------------------------------------------------ the advice

        /// <summary>The first sentence of a piece of advice: what has happened, with the figures.</summary>
        public string Head(in OutlookInsight i)
        {
            string key = OutlookInsights.HeadKeys[i.Rule];
            switch (i.Rule)
            {
                case OutlookInsights.RateUp:
                case OutlookInsights.RateDown:
                    return Fill(key, "points", Quarters(i.A), "rate", Rate(i.B));
                case OutlookInsights.RateAheadUp:
                case OutlookInsights.RateAheadDown:
                    return Fill(key, "points", Quarters(i.A), "days", i.B.ToString(Inv), "rate", Rate(i.C));
                case OutlookInsights.PricesHigh:
                case OutlookInsights.PricesLow:
                    return Fill(key, "inflation", Hundredths(i.A), "target", Hundredths(i.B));
                case OutlookInsights.CostsUp:
                    return Fill(key, "change", Percent(i.A), "days", i.B.ToString(Inv));
                case OutlookInsights.JobsTight:
                case OutlookInsights.JobsSlack:
                    return Fill(key, "rate", Permille(i.A));
                case OutlookInsights.Boom:
                case OutlookInsights.Slump:
                    return Fill(key, "footfall", Plain(i.A), "spend", Plain(i.B));
                case OutlookInsights.WorthUp:
                case OutlookInsights.WorthDown:
                    return Fill(key, "district", Name(i.Name), "change", Percent(i.A), "days", i.B.ToString(Inv));
                case OutlookInsights.IncomeUp:
                case OutlookInsights.IncomeDown:
                    return Fill(key, "district", Name(i.Name), "change", Tenths(i.A), "income", Tenths(i.B));
                case OutlookInsights.Shortage:
                case OutlookInsights.Glut:
                    return Fill(i.D == 1 ? key + "_soon" : key, "kind", T(KindKeys[Math.Max(0, Math.Min(KindKeys.Length - 1, i.A))]),
                        "percent", Percent(i.B * 100), "days", i.C.ToString(Inv));
                case OutlookInsights.StocksUp:
                case OutlookInsights.StocksDown:
                    return Fill(key, "change", Percent(i.A), "days", i.B.ToString(Inv));
                case OutlookInsights.DemandHigh:
                case OutlookInsights.DemandLow:
                    return Fill(key, "district", Name(i.Name), "use", T(UseKeys[Math.Max(0, Math.Min(UseKeys.Length - 1, i.A))]), "demand", i.B.ToString(Inv));
                case OutlookInsights.Building:
                    return Fill(key, "district", Name(i.Name), "count", i.A.ToString(Inv));
                case OutlookInsights.Deficit:
                    return Fill(key, "amount", Money(Math.Abs((long)i.A)), "reserve", Money(i.B));
                default:
                    return T(key);
            }
        }

        /// <summary>The second sentence: what it means for the player.</summary>
        public string Mean(in OutlookInsight i)
        {
            string key = i.Variant == 1 && OutlookInsights.HasTwo(i.Rule) ? OutlookInsights.MeanTwoKeys[i.Rule] : OutlookInsights.MeanKeys[i.Rule];
            return T(key);
        }

        private string Name(string district)
        {
            return District != null ? District(district) : district;
        }

        private string Fill(string key, params string[] pairs)
        {
            string text = T(key) ?? key;
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }
    }
}
