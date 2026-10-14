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
            "no_borrow", "no_margin", "below_margin", "bad_price", "too_many", "halted", "expired", "cancelled", "company_gone", "forced", "nothing_held", "stop_side", "profit_side");

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

        /// <summary>A piece of market news as a sentence (the app's box).</summary>
        public static string News(in MarketNews n, MarketState s)
        {
            string key = NewsKey(n, s, out string[] data);
            return key.Length > 0 ? Fill(key, data) : string.Empty;
        }

        /// <summary>
        /// A piece of market news as the key of its sentence and the names that fill it (name, value, name, value): what
        /// the City Ledger keeps of it, to write it out in the language of the moment. An empty key for a kind it does not know.
        /// </summary>
        public static string NewsKey(in MarketNews n, MarketState s, out string[] data)
        {
            Stock k = n.Stock > 0 && s != null ? s.Stock(n.Stock) : null;
            string name = k != null ? k.Name : string.Empty;
            switch (n.Kind)
            {
                case MarketNewsKind.Close:
                    data = new[] { "level", (n.A / 100.0).ToString("N2"), "change", Change(n.B) };
                    return "reallife_mkt_news_close";
                case MarketNewsKind.Mover:
                    data = new[] { "name", name, "change", Percent(Math.Abs(n.A)) };   // "rose 3.2%", "fell 2.5%": the verb carries the sign
                    return n.A >= 0 ? "reallife_mkt_news_up" : "reallife_mkt_news_down";
                case MarketNewsKind.Results:
                    data = new[] { "name", name, "eps", PerShare(n.A), "consensus", PerShare(n.B), "move", Change(n.C) };
                    return n.A >= n.B ? "reallife_mkt_news_results_beat" : "reallife_mkt_news_results_miss";
                case MarketNewsKind.Dividend:
                    data = new[] { "name", name, "amount", Price(n.A), "record", n.B.ToString(), "pay", n.C.ToString() };
                    return "reallife_mkt_news_dividend";
                case MarketNewsKind.Listing:
                    if (n.A > 0)
                    {
                        data = new[] { "name", name, "price", Price(n.A) };
                        return "reallife_mkt_news_listed";
                    }
                    data = new[] { "name", NameOf(n.Stock), "day", n.B.ToString() };
                    return "reallife_mkt_news_listing";
                case MarketNewsKind.Failure:
                    data = new[] { "name", name, "price", Price(n.A) };
                    return n.A > 0 ? "reallife_mkt_news_failed_paid" : "reallife_mkt_news_failed";
                case MarketNewsKind.Bid:
                    data = new[] { "name", name, "price", Price(n.A), "day", n.B.ToString(), "bidder", s != null && s.Stock(n.C) != null ? s.Stock(n.C).Name : string.Empty };
                    return n.C > 0 ? "reallife_mkt_news_bid_by" : "reallife_mkt_news_bid";
                case MarketNewsKind.BidDone:
                    data = new[] { "name", name, "price", Price(n.A) };
                    return "reallife_mkt_news_taken_over";
                case MarketNewsKind.BidFailed:
                    data = new[] { "name", name };
                    return "reallife_mkt_news_bid_failed";
                case MarketNewsKind.Split:
                    data = new[] { "name", name, "ratio", n.A.ToString(), "day", n.B.ToString() };
                    return "reallife_mkt_news_split";
                case MarketNewsKind.Crash:
                    data = new[] { "change", Change(n.A) };
                    return "reallife_mkt_news_crash";
                case MarketNewsKind.Sector:
                    data = new[] { "sector", Sector((MarketSector)Math.Max(0, Math.Min((int)MarketSector.Count - 1, n.A))), "change", Change(n.B) };
                    return n.B >= 0 ? "reallife_mkt_news_sector_up" : "reallife_mkt_news_sector_down";
                case MarketNewsKind.Rate:
                    data = new[] { "points", (Math.Abs(n.A) / 100f).ToString("0.00"), "change", Change(n.B) };
                    return n.A > 0 ? "reallife_mkt_news_rate_up" : "reallife_mkt_news_rate_down";
                case MarketNewsKind.Mania:
                    data = new[] { "sector", Sector((MarketSector)Math.Max(0, Math.Min((int)MarketSector.Count - 1, n.A))) };
                    return n.B == 1 ? "reallife_mkt_news_craze" : "reallife_mkt_news_craze_burst";
                case MarketNewsKind.Crisis:
                    data = new[] { "name", name, "change", Percent(n.A) };
                    return "reallife_mkt_news_crisis";
                case MarketNewsKind.Windfall:
                    data = new[] { "name", name, "change", Percent(n.A) };
                    return "reallife_mkt_news_windfall";
                case MarketNewsKind.Delisted:
                    data = new[] { "name", name, "price", Price(n.A) };
                    return "reallife_mkt_news_delisted";
                // A player's own company (Markets/Listing): what the city hears of it. Its founder's refusals and another
                // city's company leaving this one are no news.
                case MarketNewsKind.OwnPriced:
                    data = new[] { "name", s != null && k == null ? s.Listing.Name : name, "price", Price(n.A), "day", n.B.ToString() };
                    return "reallife_mkt_news_own_priced";
                case MarketNewsKind.OwnPulled:
                    data = new[] { "name", s != null ? s.Listing.Name : string.Empty, "price", Price(n.A) };
                    return "reallife_mkt_news_own_pulled";
                case MarketNewsKind.OwnDeal:
                    data = new[] { "name", name, "count", Math.Abs((long)n.A).ToString("N0"), "price", Price(n.B) };
                    return n.A > 0 ? "reallife_mkt_news_own_deal_buy" : "reallife_mkt_news_own_deal_sell";
                case MarketNewsKind.OwnBuyBack:
                    data = new[] { "name", name, "count", n.A.ToString("N0"), "price", Price(n.B) };
                    return "reallife_mkt_news_own_buyback";
                case MarketNewsKind.OwnIssue:
                    data = new[] { "name", name, "count", n.A.ToString("N0"), "price", Price(n.B) };
                    return "reallife_mkt_news_own_issue";
                case MarketNewsKind.OwnMeeting:
                    data = new[] { "name", name, "result", ListingText.Meeting(n.A), "against", Percent(n.B) };
                    return (n.A & (ListingVotes.PayLost | ListingVotes.PayoutVoted | ListingVotes.BoardLost | ListingVotes.DemandPassed)) != 0
                        ? "reallife_mkt_news_own_meeting_revolt" : "reallife_mkt_news_own_meeting";
                case MarketNewsKind.OwnActivist:
                    data = new[] { "name", name, "stake", Percent(n.A), "demand", ListingText.Demand((ActivistDemand)n.B), "day", n.C.ToString() };
                    return "reallife_mkt_news_own_activist";
                case MarketNewsKind.OwnWithdrawn:
                    data = new[] { "name", name };
                    return "reallife_mkt_news_own_withdrawn";
                case MarketNewsKind.OwnPrivate:
                    data = new[] { "name", name, "price", Price(n.A) };
                    return "reallife_mkt_news_own_private";
                case MarketNewsKind.OwnDisgorged:
                    data = new[] { "name", name, "amount", Money(n.A) };
                    return "reallife_mkt_news_own_disgorged";
                case MarketNewsKind.OwnBidBlocked:
                    data = new[] { "name", name };
                    return n.A == 1 ? "reallife_mkt_news_own_bid_blocked" : "reallife_mkt_news_own_bid_refused";
                case MarketNewsKind.OwnAudit:
                    data = new[] { "name", name, "found", ListingText.Findings(n.A) };
                    return "reallife_mkt_news_own_audit";
                default:
                    data = new string[0];
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
