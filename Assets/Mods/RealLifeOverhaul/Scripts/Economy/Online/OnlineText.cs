using System.Globalization;
using Extensions;
using Localizor;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The online shops' words and figures, written the same way in the app, the Planner and a notification.</summary>
    public static class OnlineText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly string[] QualityKeys = { "reallife_online_quality_basic", "reallife_online_quality_standard", "reallife_online_quality_professional" };
        private static readonly string[] WayKeys = { "reallife_online_way_vans", "reallife_online_way_courier" };
        private static readonly string[] PromiseKeys = { "reallife_online_promise_same", "reallife_online_promise_next" };
        private static readonly string[] StageKeys = { "reallife_online_stage_waiting", "reallife_online_stage_picked", "reallife_online_stage_out" };

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

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            return FinanceText.Fill(key, pairs);
        }

        /// <summary>A sum kept in cents, in whole units as the Finance app writes a day's figure.</summary>
        public static string Money(int cents)
        {
            return FinanceText.Money(cents / 100f);
        }

        /// <summary>A price kept in cents, with its cents, as the game writes a price.</summary>
        public static string Price(int cents)
        {
            return (cents / 100f).ToCurrencyFormat();
        }

        /// <summary>Thousandths of a star: "4.2".</summary>
        public static string Stars(int thousandths)
        {
            return (thousandths / 1000f).ToString("0.0", Inv);
        }

        /// <summary>Thousandths as a per cent: "125%".</summary>
        public static string Share(int thousandths)
        {
            return (thousandths / 10f).ToString("0.#", Inv) + "%";
        }

        /// <summary>An hour counted from day 0, as "17:00" today or "Day 12, 17:00", in the game's own clock.</summary>
        public static string When(int hour, int today)
        {
            string time = (hour % 24).GetFormattedTime(0f);
            return hour / 24 == today ? time : T("reallife_online_when", "day", (hour / 24).ToString(Inv), "time", time);
        }

        public static string Quality(int quality)
        {
            return T(QualityKeys[OnlineCatalogue.Quality(quality)]);
        }

        public static string Way(int way)
        {
            return T(WayKeys[OnlineCatalogue.Clamp(way, 0, WayKeys.Length - 1)]);
        }

        public static string Promise(int promise)
        {
            return T(PromiseKeys[OnlineCatalogue.Clamp(promise, 0, PromiseKeys.Length - 1)]);
        }

        public static string Stage(OrderStage stage)
        {
            return T(StageKeys[OnlineCatalogue.Clamp((int)stage, 0, StageKeys.Length - 1)]);
        }

        /// <summary>A district's name as the game writes it.</summary>
        public static string District(string district)
        {
            return string.IsNullOrEmpty(district) ? "?" : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }
    }
}
