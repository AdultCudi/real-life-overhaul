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
