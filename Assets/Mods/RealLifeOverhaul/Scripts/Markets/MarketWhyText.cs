using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>The words that say why the market moved: the clause for each cause (<see cref="WhyCause"/>), by whole keys. Built when a line is written, never per frame.</summary>
    public static class MarketWhyText
    {
        /// <summary>The clause that gives a cause, to follow "Insurers fell 1.2%, ": a whole key of the mod's texts, filled in with its detail.</summary>
        public static string Clause(WhyCause cause, int detail)
        {
            switch (cause)
            {
                case WhyCause.Crash: return MarketText.T("reallife_mkt_cause_crash");
                case WhyCause.Rate:
                    return MarketText.Fill(detail >= 0 ? "reallife_mkt_cause_rate_up" : "reallife_mkt_cause_rate_down", "points", (Math.Abs(detail) / 100f).ToString("0.00"));
                case WhyCause.Election: return MarketText.T("reallife_mkt_cause_election");
                case WhyCause.Craze: return MarketText.T("reallife_mkt_cause_craze");
                case WhyCause.CrazeBegun: return MarketText.T("reallife_mkt_cause_craze_begun");
                case WhyCause.CrazeBurst: return MarketText.T("reallife_mkt_cause_craze_burst");
                case WhyCause.Weather: return MarketText.T(detail >= 2 ? "reallife_mkt_cause_storm" : "reallife_mkt_cause_rain");
                case WhyCause.Spell: return MarketText.T(detail == 1 ? "reallife_mkt_cause_heat" : detail == 2 ? "reallife_mkt_cause_cold" : "reallife_mkt_cause_ice");
                case WhyCause.Street: return Street(detail);
                case WhyCause.Crime: return MarketText.T("reallife_mkt_cause_crime");
                case WhyCause.BreakIn: return MarketText.T("reallife_mkt_cause_breakin");
                case WhyCause.Fire: return MarketText.T("reallife_mkt_cause_fire");
                case WhyCause.Accident: return MarketText.T("reallife_mkt_cause_accident");
                case WhyCause.Buzz: return MarketText.T("reallife_mkt_cause_buzz");
                case WhyCause.Policy: return MarketText.T(detail >= 100 ? "reallife_mkt_cause_policy_tight" : "reallife_mkt_cause_policy_relax");
                case WhyCause.Economy:
                    return MarketText.T(detail <= 0 ? "reallife_mkt_cause_economy_boom" : detail == 1 ? "reallife_mkt_cause_economy_steady"
                        : detail == 2 ? "reallife_mkt_cause_economy_slow" : "reallife_mkt_cause_economy_recession");
                case WhyCause.Inflation: return MarketText.T(detail >= 0 ? "reallife_mkt_cause_inflation_up" : "reallife_mkt_cause_inflation_down");
                case WhyCause.Prices: return MarketText.T("reallife_mkt_cause_prices");
                case WhyCause.Incomes: return MarketText.T(detail >= 0 ? "reallife_mkt_cause_incomes_up" : "reallife_mkt_cause_incomes_down");
                case WhyCause.Results: return MarketText.T(detail == 1 ? "reallife_mkt_cause_results_beat" : "reallife_mkt_cause_results_miss");
                case WhyCause.Crisis: return MarketText.T("reallife_mkt_cause_crisis");
                case WhyCause.Windfall: return MarketText.T("reallife_mkt_cause_windfall");
                case WhyCause.Bid: return MarketText.T("reallife_mkt_cause_bid");
                case WhyCause.Builder: return MarketText.T("reallife_mkt_cause_builder");
                default: return string.Empty;
            }
        }

        // The street events as the city's economy counts them: a market, the farmers' market, a parade, a protest, a fun run, a shoot, fireworks.
        private static string Street(int event_)
        {
            switch (event_)
            {
                case 1: return MarketText.T("reallife_mkt_cause_street_market");
                case 2: return MarketText.T("reallife_mkt_cause_street_farmers");
                case 3: return MarketText.T("reallife_mkt_cause_street_parade");
                case 4: return MarketText.T("reallife_mkt_cause_street_protest");
                case 5: return MarketText.T("reallife_mkt_cause_street_run");
                case 6: return MarketText.T("reallife_mkt_cause_street_shoot");
                default: return MarketText.T("reallife_mkt_cause_street_fireworks");
            }
        }

        /// <summary>One line saying why: the subject, how far it moved and the cause; empty for a piece of news with no cause named.</summary>
        public static string Line(in MarketNews n, MarketState s, out string key, out string[] data)
        {
            Stock k = n.Stock > 0 && s != null ? s.Stock(n.Stock) : null;
            string subject = k != null ? k.Name : MarketText.Sector((MarketSector)MarketWhy.SectorOf(n));
            string clause = Clause(MarketWhy.CauseOf(n), n.C);
            data = new[] { "subject", subject, "change", MarketText.Percent(Math.Abs(n.B)), "cause", clause };
            key = clause.Length == 0 ? string.Empty : n.B >= 0 ? "reallife_mkt_news_why_up" : "reallife_mkt_news_why_down";
            return key;
        }
    }
}
