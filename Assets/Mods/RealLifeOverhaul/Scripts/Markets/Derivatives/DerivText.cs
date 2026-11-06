using System;
using Extensions;

namespace RealLifeOverhaul.Markets
{
    /// <summary>The futures' and options' words: a contract's name, a price a unit to four places under one unit, the reasons. Built when a page is written.</summary>
    public static class DerivText
    {
        private const string P = "reallife";   // apart, so a key made of parts is not taken for a whole key

        private static readonly string[] WhyKeys =
        {
            P + "_mkt_drv_why_none", P + "_mkt_drv_why_closed", P + "_mkt_drv_why_held", P + "_mkt_drv_why_behind", P + "_mkt_drv_why_off", P + "_mkt_drv_why_no_city",
            P + "_mkt_drv_why_unknown", P + "_mkt_drv_why_not_offered", P + "_mkt_drv_why_too_many", P + "_mkt_drv_why_too_much", P + "_mkt_drv_why_no_cash",
            P + "_mkt_drv_why_not_held", P + "_mkt_drv_why_ended", P + "_mkt_drv_why_no_place", P + "_mkt_drv_why_no_call"
        };

        private static readonly string[] KindKeys = { P + "_mkt_drv_kind_long", P + "_mkt_drv_kind_short", P + "_mkt_drv_kind_call", P + "_mkt_drv_kind_put" };

        private static readonly string[] EndKeys =
        {
            P + "_mkt_drv_end_open", P + "_mkt_drv_end_closed", P + "_mkt_drv_end_settled", P + "_mkt_drv_end_exercised", P + "_mkt_drv_end_lapsed", P + "_mkt_drv_end_delivered",
            P + "_mkt_drv_end_forced", P + "_mkt_drv_end_gone"
        };

        public static string Why(DerivWhy why)
        {
            return MarketText.T(WhyKey(why));
        }

        /// <summary>The sentence's key of why a contract cannot be bought, for a page of another feature's to say.</summary>
        public static string WhyKey(DerivWhy why)
        {
            int i = (int)why;
            return i >= 0 && i < WhyKeys.Length ? WhyKeys[i] : WhyKeys[0];
        }

        public static string Kind(ContractKind kind)
        {
            int i = (int)kind;
            return MarketText.T(i >= 0 && i < KindKeys.Length ? KindKeys[i] : KindKeys[0]);
        }

        public static string End(ContractEnd end)
        {
            int i = (int)end;
            return MarketText.T(i >= 0 && i < EndKeys.Length ? EndKeys[i] : EndKeys[0]);
        }

        public static string Group(MaterialGroup group)
        {
            int g = Math.Max(0, Math.Min(DerivCatalogue.GroupKeys.Length - 1, (int)group));
            return MarketText.T(DerivCatalogue.GroupKeys[g]);
        }

        /// <summary>A price a unit in micros: with its cents from one unit up, to four places under it (a hundredth of a unit is 0.0100).</summary>
        public static string Unit(long micros)
        {
            double money = micros / (double)DerivCatalogue.Micro;
            return money >= 1.0 ? ((float)money).ToCurrencyFormat() : money.ToString("C4", CultureHelper.CultureInfo);
        }

        /// <summary>A sum with its sign: what a contract made, or lost.</summary>
        public static string Signed(long money)
        {
            return (money > 0 ? "+" : money < 0 ? "-" : string.Empty) + MarketText.Money(Math.Abs(money));
        }

        /// <summary>
        /// A contract's name: "Barley, the week from day 50 (future)", "Call on Barley at 0.2500, day 47", "Put on Merchants Union
        /// Bank at 48.00, day 47".
        /// </summary>
        public static string Name(DerivContract c)
        {
            if (c == null)
            {
                return string.Empty;
            }
            string on = c.OnShares ? Company(c.StockId) : MaterialGame.Name(c.Material);
            if (c.IsFuture)
            {
                return MarketText.Fill("reallife_mkt_drv_name_future", "material", on, "day", DerivRules.WeekFrom(c.SettleDay).ToString());
            }
            return MarketText.Fill(c.Kind == ContractKind.Call ? "reallife_mkt_drv_name_call" : "reallife_mkt_drv_name_put", "name", on, "strike", Unit(c.Price), "day", c.SettleDay.ToString());
        }

        private static string Company(int stockId)
        {
            MarketState s = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
            Stock k = s != null ? s.Stock(stockId) : null;
            return k != null ? k.Name : stockId.ToString();
        }

        /// <summary>Where a future bought is delivered: its place's name, or in cash.</summary>
        public static string Place(DerivContract c)
        {
            return c != null && !string.IsNullOrEmpty(c.Warehouse) ? c.PlaceName : MarketText.T("reallife_mkt_drv_place_cash");
        }
    }
}
