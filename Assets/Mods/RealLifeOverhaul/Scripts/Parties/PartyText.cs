using System.Globalization;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Parties
{
    /// <summary>Parties' and company events' words: every line the pages, the notices and the tests show, through the mod's one way of filling a text.</summary>
    public static class PartyText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string T(string key) => FinanceText.T(key);

        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs ?? System.Array.Empty<string>());

        public static string Whole(int n) => n.ToString(Inv);

        public static string RefusalKey(PartyRefusal why) => PartyKeys.Refusal(why);

        /// <summary>A day and an hour as the game writes the hour: today, tomorrow, or the day's number.</summary>
        public static string When(int day, int hour, int today)
        {
            string time = TimeHelper.GetFormattedTime(hour, 0f, false);
            return day == today ? Fill("reallife_party_when_today", "time", time) : day == today + 1 ? Fill("reallife_party_when_tomorrow", "time", time)
                : Fill("reallife_party_when_day", "day", Whole(day), "time", time);
        }
    }
}
