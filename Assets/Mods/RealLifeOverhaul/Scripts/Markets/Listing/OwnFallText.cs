using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The words of a fall in the player's own company's price: the notice, the line on its page and the clause that says what caused it.
    /// Built when a line is written, never per frame.
    /// </summary>
    public static class OwnFallText
    {
        /// <summary>The clause that names the cause, whole keys filled in with the figures the cause was named by.</summary>
        public static string Why(in OwnFall f, Stock k)
        {
            switch (f.Cause)
            {
                case OwnFallCause.Figures:
                    return MarketText.Fill("reallife_own_fall_why_figures", "profit", MarketText.Percent(Math.Abs(f.ProfitBp)));
                case OwnFallCause.Market:
                    return MarketText.Fill("reallife_own_fall_why_market", "index", MarketText.Percent(Math.Abs(f.IndexBp)));
                case OwnFallCause.Sector:
                    return MarketText.Fill("reallife_own_fall_why_sector", "sector", MarketText.Sector(k.Sector), "peers", MarketText.Percent(Math.Abs(f.PeersBp)));
                default:
                    return f.WorthCents > 0 ? MarketText.Fill("reallife_own_fall_why_mood", "worth", MarketText.Price(f.WorthCents)) : MarketText.T("reallife_own_fall_why_mood_plain");
            }
        }

        /// <summary>The names of the notice and of the news line: the company, how far it fell, its high and the day of it, and the clause.</summary>
        public static string[] Pairs(in OwnFall f, Stock k)
        {
            return new[] { "name", k.Name, "change", MarketText.Percent(f.FallBp), "peak", MarketText.Price(f.PeakCents), "day", f.PeakDay.ToString(), "why", Why(f, k) };
        }

        /// <summary>The line on the company's page: how far under its year's high it stands, from when, and why.</summary>
        public static string Row(in OwnFall f, Stock k)
        {
            return MarketText.Fill("reallife_own_fall_row", "change", MarketText.Percent(f.FallBp), "peak", MarketText.Price(f.PeakCents), "day", f.PeakDay.ToString(), "why", Why(f, k));
        }
    }
}
