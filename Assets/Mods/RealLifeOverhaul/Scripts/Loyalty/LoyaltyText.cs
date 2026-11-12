using System.Globalization;
using Localizor;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Loyalty
{
    /// <summary>The loyalty cards' words: their keys filled from the figures. Built when a page is written or a notice shown, never per frame.</summary>
    public static class LoyaltyText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly string[] PushKeys = { "reallife_loyalty_push_none", "reallife_loyalty_push_staff", "reallife_loyalty_push_bonus" };

        private static readonly string[] DayKeys =
        {
            "reallife_day_monday", "reallife_day_tuesday", "reallife_day_wednesday", "reallife_day_thursday", "reallife_day_friday", "reallife_day_saturday",
            "reallife_day_sunday"
        };

        private static readonly string[] WhyKeys =
        {
            "reallife_loyalty_refuse_off", "reallife_loyalty_refuse_off", "reallife_loyalty_refuse_shop", "reallife_loyalty_refuse_cash", "reallife_loyalty_refuse_full"
        };

        public static string T(string key, params string[] pairs) => FinanceText.Fill(key, pairs);

        public static string N(long value) => value.ToString(Inv);

        /// <summary>Hundredths as a whole count: members are kept in hundredths.</summary>
        public static string Whole(int hundredths) => (hundredths / 100).ToString(Inv);

        /// <summary>Cents as money, the game's own way.</summary>
        public static string Money(long cents) => FinanceText.Money(cents / 100f);

        /// <summary>Thousandths as per cent with one decimal: 25 is "2.5%".</summary>
        public static string Mille(int mille) => (mille / 10f).ToString("0.#", Inv) + "%";

        /// <summary>A reward rate's step as the share of a basket given back.</summary>
        public static string Rate(int step) => Mille(LoyaltyRules.RateMille[LoyaltyRules.Clamp(step, 0, LoyaltyRules.Rates - 1)]);

        /// <summary>A sign-up push's step by its name.</summary>
        public static string Push(int step) => PushKeys[LoyaltyRules.Clamp(step, 0, PushKeys.Length - 1)].GetLocalization();

        /// <summary>A day of the week by its name (0 for Monday).</summary>
        public static string Day(int weekday) => DayKeys[LoyaltyRules.Clamp(weekday, 0, DayKeys.Length - 1)].GetLocalization();

        /// <summary>The text key that says why a press did nothing.</summary>
        public static string Why(LoyaltyRefusal r) => WhyKeys[LoyaltyRules.Clamp((int)r, 0, WhyKeys.Length - 1)];
    }
}
