using Extensions;
using Localizor;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>
    /// The calendar's words: every text is a key of en.json or one of the game's own; money, hours and weekdays are written
    /// as the game writes them. Read only when the app writes a page.
    /// </summary>
    public static class CalendarText
    {
        private static readonly string[] MonthKeys =
        {
            "reallife_calendar_month_1", "reallife_calendar_month_2", "reallife_calendar_month_3", "reallife_calendar_month_4",
            "reallife_calendar_month_5", "reallife_calendar_month_6", "reallife_calendar_month_7", "reallife_calendar_month_8",
            "reallife_calendar_month_9", "reallife_calendar_month_10", "reallife_calendar_month_11", "reallife_calendar_month_12"
        };

        private static readonly string[] DayKeys =
        {
            "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday"
        };

        /// <summary>The keys of the twelve months' names, January to December.</summary>
        public static string[] MonthKeyList => MonthKeys;

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

        /// <summary>The name of a month by its place in the year (0 is the first): January to December.</summary>
        public static string Month(int inYear)
        {
            return MonthKeys[((inYear % MonthKeys.Length) + MonthKeys.Length) % MonthKeys.Length].GetLocalization();
        }

        /// <summary>The weekday of a game day, in the game's own word.</summary>
        public static string Weekday(int day)
        {
            return DayKeys[MonthViewRules.Weekday(day)].GetLocalization();
        }

        /// <summary>"Mon": the first letters of the game's own word for a weekday, for the heads of the month's columns.</summary>
        public static string WeekdayOfColumn(int column)
        {
            return DayKeys[((column % 7) + 7) % 7].GetLocalization();
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        /// <summary>A short amount for a day of the grid ("$1.2K"), as the game's tables shorten theirs.</summary>
        public static string Short(float amount)
        {
            return amount.ToShortCurrencyFormat();
        }

        /// <summary>A time of day as the game writes it, in the 12 or 24 hours of its own option.</summary>
        public static string Clock(int minutesOfDay)
        {
            int m = ((minutesOfDay % 1440) + 1440) % 1440;
            return (m / 60).GetFormattedTime(m % 60);
        }
    }
}
