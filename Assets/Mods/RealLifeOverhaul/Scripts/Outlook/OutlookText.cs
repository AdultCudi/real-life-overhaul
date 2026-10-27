using System;
using System.Globalization;
using Extensions;
using Helpers;
using Localizor;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>The Economy app's words in the game's language: its keys, the game's own where it has one, money as the game writes it, and the sentences of <see cref="OutlookWords"/>.</summary>
    internal static class OutlookText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static OutlookWords _words;

        /// <summary>The sentences, with the game's locale behind them.</summary>
        public static OutlookWords Words => _words ?? (_words = new OutlookWords { T = T, Money = Money, District = CityGame.Name });

        public static string T(string key)
        {
            return key.GetLocalization();
        }

        public static string T(string key, string name, string value)
        {
            return key.GetLocalization().Replace("{" + name + "}", value ?? string.Empty);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return T(key, name, value).Replace("{" + name2 + "}", value2 ?? string.Empty);
        }

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3)
        {
            return T(key, name, value, name2, value2).Replace("{" + name3 + "}", value3 ?? string.Empty);
        }

        public static string Money(long amount)
        {
            return ((float)amount).ToShortCurrencyFormat();
        }

        public static string Day(int day)
        {
            return T("reallife_econapp_day", "day", day.ToString(Inv));
        }

        /// <summary>"7d": a number of days after a change.</summary>
        public static string Days(int days)
        {
            return T("reallife_econapp_days_short", "days", days.ToString(Inv));
        }

        /// <summary>The game's date as its top bar writes it: "Monday (Day 41)".</summary>
        public static string Date(int day)
        {
            try
            {
                string weekday = TimeHelper.GetDayOfWeek(Math.Max(1, day)).GetLocalization();
                return weekday + " (" + Day(day) + ")";
            }
            catch (Exception)
            {
                return Day(day);
            }
        }

        public static string Pts(string number)
        {
            return T("reallife_econapp_points", "n", number);
        }
    }
}
