using System.Globalization;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The dealership's words: every figure it shows is written here, the same way in the app and in a notification.
    /// Every text is a key of en.json; money is written as the Finance app writes it. Called when something is shown or
    /// said, never otherwise.
    /// </summary>
    public static class DealerText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly string[] KindKeys = { "reallife_dealer_kind_wholesale", "reallife_dealer_kind_used", "reallife_dealer_kind_auction" };
        public static readonly string[] SourceKeys = { "reallife_dealer_kind_wholesale", "reallife_dealer_kind_used", "reallife_dealer_kind_auction", "reallife_dealer_kind_trade" };
        public static readonly string[] ProfileKeys = { "reallife_dealer_profile_bargain", "reallife_dealer_profile_fair", "reallife_dealer_profile_keen" };
        public static readonly string[] TierKeys = { "reallife_dealer_tier_basic", "reallife_dealer_tier_extended", "reallife_dealer_tier_complete" };
        public static readonly string[] FaultKeys =
        {
            "reallife_dealer_fault_tyres", "reallife_dealer_fault_brakes", "reallife_dealer_fault_suspension", "reallife_dealer_fault_body", "reallife_dealer_fault_engine", "reallife_dealer_fault_interior"
        };

        public static string T(string key) => FinanceText.T(key);

        public static string T(string key, string name, string value) => FinanceText.T(key, name, value);

        public static string T(string key, string name, string value, string name2, string value2) => FinanceText.T(key, name, value, name2, value2);

        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs);

        public static string Pair(string labelKey, string value) => FinanceText.Pair(labelKey, value);

        public static string Money(int amount) => FinanceText.Money(amount);

        public static string Km(int km) => T("reallife_dealer_km", "km", km.ToString("N0", Inv));

        /// <summary>Thousandths of condition, as a per cent.</summary>
        public static string Condition(int thousandths) => T("reallife_dealer_condition_value", "percent", ((thousandths + 5) / 10).ToString(Inv));

        public static string Share(int thousandths) => T("reallife_dealer_share", "percent", ((thousandths + 5) / 10).ToString(Inv));

        public static string Faults(int faults)
        {
            if (faults == 0)
            {
                return T("reallife_dealer_faults_none");
            }
            var t = new System.Text.StringBuilder(64);
            for (int f = 0; f < DealerCatalogue.Faults; f++)
            {
                if ((faults & (1 << f)) != 0)
                {
                    t.Append(t.Length > 0 ? ", " : string.Empty).Append(T(FaultKeys[f]));
                }
            }
            return t.ToString();
        }

        public static string Kind(ListingKind kind) => T(KindKeys[(int)kind]);

        public static string Source(CarSource source) => T(SourceKeys[(int)source]);

        public static string Profile(int profile) => T(ProfileKeys[DealerCatalogue.Clamp(profile, 0, ProfileKeys.Length - 1)]);

        public static string Tier(int tier) => tier < 0 ? T("reallife_dealer_tier_none") : T(TierKeys[DealerCatalogue.Clamp(tier, 0, TierKeys.Length - 1)]);

        /// <summary>The hour of a day x 24 + hour, as the day and the clock.</summary>
        public static string When(int hourOfGame)
        {
            int day = hourOfGame / 24, hour = hourOfGame % 24;
            return T("reallife_dealer_when", "day", FinanceText.Day(day), "hour", hour.ToString("00", Inv) + ":00");
        }

        /// <summary>Hours from now until an hour of the game.</summary>
        public static string Hours(int hours) => hours <= 1 ? T("reallife_dealer_hours_one") : T("reallife_dealer_hours", "hours", hours.ToString(Inv));
    }
}
