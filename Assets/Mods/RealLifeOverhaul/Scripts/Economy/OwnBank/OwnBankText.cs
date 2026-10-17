using System.Globalization;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The bank's words and figures, written alike on its page and in its notifications.</summary>
    public static class OwnBankText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string T(string key)
        {
            return FinanceText.T(key);
        }

        public static string T(string key, string name, string value)
        {
            return FinanceText.T(key, name, value);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return FinanceText.T(key, name, value, name2, value2);
        }

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            return FinanceText.Fill(key, pairs);
        }

        public static string Money(long cents)
        {
            return FinanceText.Money(cents / 100f);
        }

        /// <summary>An amount that may be less than nothing.</summary>
        public static string Signed(long cents)
        {
            return cents < 0 ? "-" + Money(-cents) : Money(cents);
        }

        /// <summary>A rate in hundredths of a per cent: 825 is "8.25%".</summary>
        public static string Rate(int bp)
        {
            return (bp / 100.0).ToString("0.##", Inv) + "%";
        }

        /// <summary>Points over or under another rate: "+1.25 points", "-0.5 points", "the same".</summary>
        public static string Points(int bp)
        {
            if (bp == 0)
            {
                return T("reallife_ob_points_same");
            }
            return T("reallife_ob_points", "points", (bp > 0 ? "+" : "-") + (System.Math.Abs(bp) / 100.0).ToString("0.##", Inv));
        }

        /// <summary>Thousandths as a percentage with one decimal: 125 is "12.5%".</summary>
        public static string Ratio(int thousandths)
        {
            if (thousandths >= OwnBankRules.RatioMost)
            {
                return T("reallife_ob_ratio_nothing");
            }
            return (thousandths / 10.0).ToString("0.#", Inv) + "%";
        }

        /// <summary>A part of a whole as a percentage with one decimal: "4.7%"; "-" for no whole.</summary>
        public static string Share(long part, long whole)
        {
            return whole > 0 ? (part * 1000.0 / whole / 10.0).ToString("0.#", Inv) + "%" : "-";
        }

        public static string Saver(int kind)
        {
            return T(OwnBankCatalogue.SaverKeys[OwnBankRules.Clamp(kind, 0, OwnBankCatalogue.Savers - 1)]);
        }

        public static string Segment(int segment)
        {
            return T(OwnBankCatalogue.SegmentKeys[OwnBankRules.Clamp(segment, 0, OwnBankCatalogue.Segments - 1)]);
        }

        public static string Strategy(int strategy)
        {
            return T(OwnBankCatalogue.StrategyKeys[OwnBankRules.Clamp(strategy, 0, OwnBankCatalogue.Strategies - 1)]);
        }

        /// <summary>What the regulator makes of the bank, in a few words.</summary>
        public static string Stage(OwnBank b)
        {
            if (b.Stage == Regulator.Breach)
            {
                return T(b.Strikes > 0 ? "reallife_ob_stage_fined" : "reallife_ob_stage_breach", "day", FinanceText.Day(b.Deadline));
            }
            return T(b.Stage == Regulator.Watch ? "reallife_ob_stage_watch" : "reallife_ob_stage_clear");
        }

        public static string WhyKey(ClosedWhy why)
        {
            switch (why)
            {
                case ClosedWhy.Strikes: return "reallife_ob_why_strikes";
                case ClosedWhy.Insolvent: return "reallife_ob_why_insolvent";
                case ClosedWhy.Collapse: return "reallife_ob_why_collapse";
                default: return "reallife_ob_why_run";
            }
        }
    }
}
