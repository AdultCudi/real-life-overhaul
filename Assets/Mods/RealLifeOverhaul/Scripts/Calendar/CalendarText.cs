using Extensions;
using Localizor;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>The calendar's words: locale keys, the game's own included, and money, hours and weekdays as the game writes them.</summary>
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

        /// <summary>A month's name by its place in the year, 0 for January.</summary>
        public static string Month(int inYear)
        {
            return MonthKeys[((inYear % MonthKeys.Length) + MonthKeys.Length) % MonthKeys.Length].GetLocalization();
        }

        public static string Weekday(int day)
        {
            return DayKeys[MonthViewRules.Weekday(day)].GetLocalization();
        }

        /// <summary>The game's word for the weekday of a grid column, 0 for Monday.</summary>
        public static string WeekdayOfColumn(int column)
        {
            return DayKeys[((column % 7) + 7) % 7].GetLocalization();
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        /// <summary>A short amount for the grid ("$1.2K"), as the game's tables shorten theirs.</summary>
        public static string Short(float amount)
        {
            return amount.ToShortCurrencyFormat();
        }

        /// <summary>A time of day in the game's own 12 or 24 hour format.</summary>
        public static string Clock(int minutesOfDay)
        {
            int m = ((minutesOfDay % 1440) + 1440) % 1440;
            return (m / 60).GetFormattedTime(m % 60);
        }
    }
}
