using System.Globalization;
using Helpers;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The words of the cleaning service and of selling a business: every figure they show is written here, the same
    /// way on the page and in a notification. Every text is a key of en.json; money is written as the Finance app
    /// writes it and a time of day as the game writes one. Called when something is shown or said, never otherwise.
    /// </summary>
    public static class ServicesText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly string[] KindKeys = { "reallife_svc_buyer_rival", "reallife_svc_buyer_investor", "reallife_svc_buyer_staff", "reallife_svc_buyer_chain" };
        private static readonly string[] ModeKeys = { "reallife_svc_mode_needed", "reallife_svc_mode_1", "reallife_svc_mode_2", "reallife_svc_mode_3" };
        private static readonly string[] WhyKeys = { null, "reallife_svc_why_nobody", "reallife_svc_why_far", "reallife_svc_why_time" };

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

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3)
        {
            return FinanceText.T(key, name, value, name2, value2, name3, value3);
        }

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            return FinanceText.Fill(key, pairs);
        }

        public static string Pair(string labelKey, string value)
        {
            return FinanceText.Pair(labelKey, value);
        }

        public static string Money(float amount)
        {
            return FinanceText.Money(amount);
        }

        /// <summary>A sum that may be less than nothing, written so that it reads as one.</summary>
        public static string Signed(int amount)
        {
            return amount < 0 ? "-" + FinanceText.Money(-amount) : FinanceText.Money(amount);
        }

        /// <summary>A minute of the day as the game writes a time (its own 12 or 24 hour option).</summary>
        public static string Clock(int minute)
        {
            int hour = ServicesCatalogue.Clamp(minute, 0, 24 * 60 - 1) / 60;
            return hour.GetFormattedTime(minute % 60);
        }

        public static string Hour(int hour)
        {
            return Clock(ServicesCatalogue.Clamp(hour, 0, 23) * 60);
        }

        public static string Minutes(int minutes)
        {
            return T("reallife_svc_minutes", "count", minutes.ToString(Inv));
        }

        /// <summary>A way: "850 m", "1.3 km".</summary>
        public static string Distance(int metres)
        {
            return metres < 1000 ? metres.ToString(Inv) + " m" : (metres / 1000f).ToString("0.#", Inv) + " km";
        }

        public static string Percent(int percent)
        {
            return percent.ToString(Inv) + "%";
        }

        /// <summary>Today's cleanliness against the morning's: "82 (+4 since the morning)".</summary>
        public static string Trend(int now, int before)
        {
            if (before < 0 || now == before)
            {
                return T("reallife_svc_trend_level", "now", now.ToString(Inv));
            }
            return T(now > before ? "reallife_svc_trend_up" : "reallife_svc_trend_down", "now", now.ToString(Inv), "by", System.Math.Abs(now - before).ToString(Inv));
        }

        public static string Mode(int mode)
        {
            return T(ModeKeys[ServicesCatalogue.Clamp(mode, 0, ModeKeys.Length - 1)]);
        }

        /// <summary>Why a visit that is due is not planned; null if nothing is left over.</summary>
        public static string Why(int why)
        {
            return why > 0 && why < WhyKeys.Length ? T(WhyKeys[why]) : null;
        }

        /// <summary>A kind of buyer: "a rival owner".</summary>
        public static string Kind(int buyer)
        {
            return T(KindKeys[ServicesCatalogue.Clamp(buyer, 0, KindKeys.Length - 1)]);
        }

        /// <summary>Who makes an offer: one of three of its kind, always the same for the same offer.</summary>
        public static string Buyer(SaleOffer offer)
        {
            return T(KindKeys[ServicesCatalogue.Clamp(offer.Buyer, 0, KindKeys.Length - 1)] + "_" + ServicesCatalogue.Clamp(offer.Face, 0, 2).ToString(Inv));
        }

        public static string District(string district)
        {
            return CompanyText.District(district);
        }
    }
}
