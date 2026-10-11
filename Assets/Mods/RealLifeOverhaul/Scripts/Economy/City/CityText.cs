using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The city's economy in words: every figure it shows is written here, the same way in the Finance app, the paper,
    /// the Planner and a notification. Every text is a key of en.json. Called when something is shown, never otherwise.
    /// </summary>
    public static class CityText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly string[] StateKeys = { "reallife_macro_state_boom", "reallife_macro_state_steady", "reallife_macro_state_slowdown", "reallife_macro_state_recession" };
        public static readonly string[] KindKeys =
        {
            "reallife_macro_kind_fresh", "reallife_macro_kind_groceries", "reallife_macro_kind_clothing", "reallife_macro_kind_electronics", "reallife_macro_kind_home", "reallife_macro_kind_general"
        };
        private static readonly string[] ReasonKeys =
        {
            "reallife_macro_reason_hold", "reallife_macro_reason_prices_high", "reallife_macro_reason_prices_low", "reallife_macro_reason_slowdown", "reallife_macro_reason_recession", "reallife_macro_reason_normal"
        };
        private static readonly string[] CycleNewsKeys = { "reallife_macro_news_cycle_boom", "reallife_macro_news_cycle_steady", "reallife_macro_news_cycle_slowdown", "reallife_macro_news_cycle_recession" };

        public static string State(CycleState state)
        {
            return FinanceText.T(StateKeys[MacroCatalogue.Clamp((int)state, 0, StateKeys.Length - 1)]);
        }

        public static string Kind(int kind)
        {
            return FinanceText.T(KindKeys[MacroCatalogue.Clamp(kind, 0, KindKeys.Length - 1)]);
        }

        /// <summary>A kind of goods at the head of a line: its name with a capital.</summary>
        public static string KindTitle(int kind)
        {
            string name = Kind(kind);
            return string.IsNullOrEmpty(name) ? name : char.ToUpper(name[0], Inv) + name.Substring(1);
        }

        /// <summary>Quarter points as points: "0.25", "1", "1.5".</summary>
        public static string Points(int quarters)
        {
            return (Math.Abs(quarters) / (float)MacroCatalogue.QuartersPerPoint).ToString("0.##", Inv);
        }

        /// <summary>The rate as the app writes it: "the banks' own", "banks' own +1.25", "banks' own -0.5".</summary>
        public static string Rate(int quarters)
        {
            return quarters == 0 ? FinanceText.T("reallife_macro_rate_own")
                : FinanceText.T("reallife_macro_rate_value", "points", (quarters > 0 ? "+" : "-") + Points(quarters));
        }

        /// <summary>The index: "104.2".</summary>
        public static string Index(int thousandths)
        {
            return (thousandths / 1000f).ToString("0.0", Inv);
        }

        /// <summary>Hundredths of a per cent without a sign: "0.8".</summary>
        public static string Hundredths(int hundredths)
        {
            return (Math.Abs(hundredths) / 100f).ToString("0.0#", Inv);
        }

        /// <summary>A factor in thousandths as a change: "+4.2%", "-3%", "0%".</summary>
        public static string Change(int thousandths)
        {
            int tenths = thousandths - 1000;
            return (tenths > 0 ? "+" : string.Empty) + (tenths / 10f).ToString("0.#", Inv) + "%";
        }

        /// <summary>Whole per cent with its sign: "+6%", "-12%".</summary>
        public static string Percent(int percent)
        {
            return (percent > 0 ? "+" : string.Empty) + percent.ToString(Inv) + "%";
        }

        /// <summary>A district's income: "104.5".</summary>
        public static string Income(int tenths)
        {
            return (tenths / 10f).ToString("0.#", Inv);
        }

        public static string Prices(CityState state)
        {
            string key = state.MonthChange > 0 ? "reallife_macro_prices_up" : state.MonthChange < 0 ? "reallife_macro_prices_down" : "reallife_macro_prices_flat";
            return FinanceText.T(key, "index", Index(state.Index), "change", Hundredths(state.MonthChange));
        }

        public static string Economy(CityState state)
        {
            return FinanceText.T("reallife_macro_economy_value", "state", State(state.Cycle), "month", state.MonthsIn.ToString(Inv));
        }

        /// <summary>A decision in a sentence with its reason: what the paper prints the day before, and the Planner's row.</summary>
        public static string Decision(int move, RateReason reason, int rateAfter)
        {
            string why = FinanceText.T(ReasonKeys[MacroCatalogue.Clamp((int)reason, 0, ReasonKeys.Length - 1)]);
            return move == 0 ? FinanceText.T("reallife_macro_news_rate_hold", "rate", Rate(rateAfter), "reason", why)
                : FinanceText.T(move > 0 ? "reallife_macro_news_rate_up" : "reallife_macro_news_rate_down", "points", Points(move), "rate", Rate(rateAfter), "reason", why);
        }

        /// <summary>What a shortage or a glut does, in a sentence: "Wholesale prices for it rise by about 30% for 5 days."</summary>
        public static string ShockEffect(SupplyShock shock, int percent, int days)
        {
            return FinanceText.T(shock == SupplyShock.Shortage ? "reallife_macro_shock_rise" : "reallife_macro_shock_fall", "percent", Math.Abs(percent).ToString(Inv), "days", days.ToString(Inv));
        }

        /// <summary>
        /// One piece for the paper: its key and what fills it. A new dictionary each time (the paper keeps it).
        /// <paramref name="district"/> gives a district's name as the game writes it.
        /// </summary>
        public static string News(in CityNews news, int rateNow, Func<string, string> district, out Dictionary<string, string> data)
        {
            data = new Dictionary<string, string>(4);
            switch (news.Kind)
            {
                case CityNewsKind.Supply:
                    data["kind"] = Kind(news.A);
                    data["percent"] = Math.Abs(news.D).ToString(Inv);
                    data["days"] = news.C.ToString(Inv);
                    return news.B == (int)SupplyShock.Shortage ? "reallife_macro_news_shortage" : "reallife_macro_news_glut";
                case CityNewsKind.RateDecision:
                    data["points"] = Points(news.A);
                    data["rate"] = Rate(MacroCatalogue.Clamp(rateNow + news.A, MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost));
                    data["reason"] = FinanceText.T(ReasonKeys[MacroCatalogue.Clamp(news.B, 0, ReasonKeys.Length - 1)]);
                    return news.A == 0 ? "reallife_macro_news_rate_hold" : news.A > 0 ? "reallife_macro_news_rate_up" : "reallife_macro_news_rate_down";
                case CityNewsKind.Cycle:
                    return CycleNewsKeys[MacroCatalogue.Clamp(news.A, 0, CycleNewsKeys.Length - 1)];
                case CityNewsKind.Prices:
                    data["index"] = Index(news.A);
                    data["change"] = Hundredths(news.B);
                    return news.B > 0 ? "reallife_macro_news_prices_up" : news.B < 0 ? "reallife_macro_news_prices_down" : "reallife_macro_news_prices_flat";
                case CityNewsKind.Rival:
                    data["business"] = news.Name ?? string.Empty;
                    data["days"] = MacroCatalogue.CopycatDays.ToString(Inv);
                    return "reallife_macro_news_rival";
                default:
                    data["district"] = district != null ? district(news.Name) : news.Name ?? string.Empty;
                    data["points"] = Income(Math.Abs(news.A));
                    data["income"] = Income(news.B);
                    return news.A >= 0 ? "reallife_macro_news_district_up" : "reallife_macro_news_district_down";
            }
        }
    }
}
