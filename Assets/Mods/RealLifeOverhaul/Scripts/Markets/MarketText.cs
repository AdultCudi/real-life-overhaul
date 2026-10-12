using System;
using Extensions;
using Localizor;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The stock market's words: every text is a key of en.json, prices are written as the game writes money with its
    /// cents (ToCurrencyFormat), sums of money in whole units (ToShortCurrencyFormat). Texts are built when a page is
    /// written or a notification goes out, never per frame.
    /// </summary>
    public static class MarketText
    {
        private const string P = "reallife";   // (apart, so that a key made from parts is not taken for a whole one)

        private static readonly string[] WhyKeys = Keys(P + "_mkt_why_", "none", "closed", "held", "behind", "no_shares", "no_money", "too_large", "holding_limit", "not_listed",
            "no_borrow", "no_margin", "below_margin", "bad_price", "too_many", "halted", "expired", "cancelled", "company_gone", "forced");

        private static string[] Keys(string prefix, params string[] names)
        {
            var keys = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                keys[i] = prefix + names[i];
            }
            return keys;
        }

        public static string T(string key)
        {
            return key.GetLocalization();
        }

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        /// <summary>Why an order cannot be placed or was stopped, as a sentence.</summary>
        public static string Why(OrderWhy why)
        {
            int i = (int)why;
            return T(i >= 0 && i < WhyKeys.Length ? WhyKeys[i] : WhyKeys[0]);
        }

        /// <summary>A share price in cents, with its cents.</summary>
        public static string Price(int cents)
        {
            return (cents / 100f).ToCurrencyFormat();
        }

        /// <summary>Money a share in hundredths of a cent (earnings, a dividend's history), with its cents.</summary>
        public static string PerShare(int tenThousandths)
        {
            return (tenThousandths / 10000f).ToCurrencyFormat();
        }

        /// <summary>A sum of money in whole units.</summary>
        public static string Money(long amount)
        {
            return ((double)amount).ToShortCurrencyFormat();
        }

        /// <summary>A large sum (a company's worth, its turnover) shortened: "1.2M".</summary>
        public static string Large(double amount)
        {
            return amount.ToShortCurrencyFormat(true);
        }

        /// <summary>A move in basis points with its sign: "+1.25%", "-0.80%".</summary>
        public static string Change(int bp)
        {
            return (bp > 0 ? "+" : string.Empty) + (bp / 100f).ToString("0.00") + "%";
        }

        /// <summary>A share of something in basis points: "4.2%".</summary>
        public static string Percent(int bp)
        {
            return (bp / 100f).ToString("0.0") + "%";
        }

        /// <summary>A piece of market news as a sentence (the app's box, the City Ledger's market page).</summary>
        public static string News(in MarketNews n, MarketState s)
        {
            Stock k = n.Stock > 0 && s != null ? s.Stock(n.Stock) : null;
            string name = k != null ? k.Name : string.Empty;
            switch (n.Kind)
            {
                case MarketNewsKind.Close:
                    return Fill("reallife_mkt_news_close", "level", (n.A / 100.0).ToString("N2"), "change", Change(n.B));
                case MarketNewsKind.Mover:
                    return Fill(n.A >= 0 ? "reallife_mkt_news_up" : "reallife_mkt_news_down", "name", name, "change", Change(n.A));
                case MarketNewsKind.Results:
                    return Fill(n.A >= n.B ? "reallife_mkt_news_results_beat" : "reallife_mkt_news_results_miss", "name", name, "eps", PerShare(n.A), "consensus", PerShare(n.B));
                case MarketNewsKind.Dividend:
                    return Fill("reallife_mkt_news_dividend", "name", name, "amount", Price(n.A), "record", n.B.ToString(), "pay", n.C.ToString());
                case MarketNewsKind.Listing:
                    return n.A > 0 ? Fill("reallife_mkt_news_listed", "name", name, "price", Price(n.A)) : Fill("reallife_mkt_news_listing", "name", NameOf(n.Stock), "day", n.B.ToString());
                case MarketNewsKind.Failure:
                    return Fill(n.A > 0 ? "reallife_mkt_news_failed_paid" : "reallife_mkt_news_failed", "name", name, "price", Price(n.A));
                case MarketNewsKind.Bid:
                    return Fill(n.C > 0 ? "reallife_mkt_news_bid_by" : "reallife_mkt_news_bid", "name", name, "price", Price(n.A), "day", n.B.ToString(), "bidder", s != null && s.Stock(n.C) != null ? s.Stock(n.C).Name : string.Empty);
                case MarketNewsKind.BidDone:
                    return Fill("reallife_mkt_news_taken_over", "name", name, "price", Price(n.A));
                case MarketNewsKind.BidFailed:
                    return Fill("reallife_mkt_news_bid_failed", "name", name);
                case MarketNewsKind.Split:
                    return Fill("reallife_mkt_news_split", "name", name, "ratio", n.A.ToString(), "day", n.B.ToString());
                case MarketNewsKind.Crash:
                    return Fill("reallife_mkt_news_crash", "change", Change(n.A));
                case MarketNewsKind.Sector:
                    return Fill(n.B >= 0 ? "reallife_mkt_news_sector_up" : "reallife_mkt_news_sector_down", "sector", Sector((MarketSector)Math.Max(0, Math.Min((int)MarketSector.Count - 1, n.A))), "change", Change(n.B));
                case MarketNewsKind.Rate:
                    return Fill(n.A > 0 ? "reallife_mkt_news_rate_up" : "reallife_mkt_news_rate_down", "points", (Math.Abs(n.A) / 100f).ToString("0.00"), "change", Change(n.B));
                case MarketNewsKind.Mania:
                    return Fill(n.B == 1 ? "reallife_mkt_news_craze" : "reallife_mkt_news_craze_burst", "sector", Sector((MarketSector)Math.Max(0, Math.Min((int)MarketSector.Count - 1, n.A))));
                case MarketNewsKind.Crisis:
                    return Fill("reallife_mkt_news_crisis", "name", name, "change", Percent(n.A));
                case MarketNewsKind.Windfall:
                    return Fill("reallife_mkt_news_windfall", "name", name, "change", Percent(n.A));
                case MarketNewsKind.Delisted:
                    return Fill("reallife_mkt_news_delisted", "name", name, "price", Price(n.A));
                default:
                    return string.Empty;
            }
        }

        // A company that is not in the market yet (a listing announced): its name from the catalogue.
        private static string NameOf(int id)
        {
            CompanyDef d = MarketCatalogue.Find(id);
            return d != null ? d.Name : string.Empty;
        }

        /// <summary>A sector's name.</summary>
        public static string Sector(MarketSector sector)
        {
            int i = (int)sector;
            return T(i >= 0 && i < MarketCatalogue.SectorKeys.Length ? MarketCatalogue.SectorKeys[i] : MarketCatalogue.SectorKeys[0]);
        }

        /// <summary>A company's size.</summary>
        public static string Size(MarketSize size)
        {
            int i = (int)size;
            return T(i >= 0 && i < MarketCatalogue.SizeKeys.Length ? MarketCatalogue.SizeKeys[i] : MarketCatalogue.SizeKeys[0]);
        }
    }
}
