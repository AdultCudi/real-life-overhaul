using Extensions;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Planner
{
    /// <summary>
    /// One source of things to do. A source reads a feature's (or the game's) own state and never changes it. It is
    /// asked only when the Planner looks: when the app opens, once a game hour for the badge, and when a reminder is
    /// due. "Available" is false while its feature is switched off or not loaded, and then it costs nothing.
    /// A new feature adds one class in Planner/Sources and one line in PlannerDirector.Sources.
    /// </summary>
    public interface IPlannerSource
    {
        /// <summary>Short and never changed: it is kept in the save with a reminder ("work", "court").</summary>
        string Id { get; }
        bool Available { get; }
        /// <summary>
        /// Adds what falls on the days fromDay..toDay (into.Add gives a blank entry, or null for a day outside them).
        /// The numbers are always filled; the texts only if into.WantText.
        /// </summary>
        void Collect(int fromDay, int toDay, PlannerList into);
    }

    /// <summary>The Planner's words: every text is a key of en.json or one of the game's own keys; times and days are written as the game writes them.</summary>
    public static class PlannerText
    {
        // The game's own icons, by category (GameUi.GameSprites finds them in memory; a missing one leaves the row without a picture).
        private static readonly string[] IconNames =
        {
            "Icon-PersonEngineer", "Icon-Security", "Icon-Traffic", "Icon-People", "Icon-Chat", "Icon-Route",
            "Icon-Neighborhood", "Icon-Shop", "Icon-Condition", "Icon-Money", "Icon-Edit"
        };

        private static readonly string[] CategoryKeys =
        {
            "reallife_planner_cat_work", "reallife_planner_cat_court", "reallife_planner_cat_licence", "reallife_planner_cat_family",
            "reallife_planner_cat_social", "reallife_planner_cat_travel", "reallife_planner_cat_city", "reallife_planner_cat_business",
            "reallife_planner_cat_health", "reallife_planner_cat_money", "reallife_planner_cat_note"
        };

        public static void WantIcons()
        {
            GameUi.GameSprites.Want(IconNames);
            GameUi.GameSprites.Want("Icon-Calendar");
        }

        public static Sprite Icon(PlannerCategory category)
        {
            int index = (int)category;
            return index >= 0 && index < IconNames.Length ? GameUi.GameSprites.Get(IconNames[index]) : null;
        }

        public static string CategoryKey(PlannerCategory category)
        {
            int index = (int)category;
            return index >= 0 && index < CategoryKeys.Length ? CategoryKeys[index] : CategoryKeys[CategoryKeys.Length - 1];
        }

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
            return key.GetLocalization().Replace("{" + name + "}", value ?? string.Empty).Replace("{" + name2 + "}", value2 ?? string.Empty);
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        /// <summary>A time of day as the game writes it, in the 12 or 24 hours of the game's own option (TimeHelper.GetFormattedTime).</summary>
        public static string Clock(int minutesOfDay)
        {
            int m = ((minutesOfDay % PlannerTime.MinutesPerDay) + PlannerTime.MinutesPerDay) % PlannerTime.MinutesPerDay;
            return (m / 60).GetFormattedTime(m % 60);
        }

        private static readonly string[] DayKeys =
        {
            "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday"
        };

        public static string Weekday(int day)
        {
            return DayKeys[PlannerTime.Weekday(day)].GetLocalization();
        }

        /// <summary>A day as the game's top bar writes it: "Monday (Day 12)" (its key topbar_date_format).</summary>
        public static string Date(int day)
        {
            return "topbar_date_format".GetLocalization().Replace("{DayOfWeek}", Weekday(day)).Replace("{CurrentNumberDay}", day.ToString());
        }

        /// <summary>"Today", "Tomorrow" (the game's own words), or the day as the top bar writes it.</summary>
        public static string Day(int day, int today)
        {
            return day == today ? "common_today".GetLocalization() : day == today + 1 ? "common_tomorrow".GetLocalization() : Date(day);
        }

        /// <summary>"09:00", "All day", "By 17:00".</summary>
        public static string When(PlannerEntry entry)
        {
            if (entry.Deadline && entry.End >= 0)
            {
                return T("reallife_planner_when_by", "time", Clock(entry.End));
            }
            return entry.Start >= 0 ? Clock(entry.Start) : T("reallife_planner_when_all_day");
        }

        /// <summary>"Today, 09:00 to 17:00", "Tomorrow, all day", "Friday (Day 19), by 17:00".</summary>
        public static string WhenLong(PlannerEntry entry, int today)
        {
            string day = Day(entry.Day, today);
            if (entry.Deadline && entry.End >= 0)
            {
                return T("reallife_planner_when_long_by", "day", day, "time", Clock(entry.End));
            }
            if (entry.Start < 0)
            {
                return T("reallife_planner_when_long_all_day", "day", day);
            }
            if (entry.End >= 0 && entry.End != entry.Start)
            {
                return T("reallife_planner_when_long_from_to", "day", day, "time", Clock(entry.Start)).Replace("{end}", Clock(entry.End));
            }
            return T("reallife_planner_when_long_at", "day", day, "time", Clock(entry.Start));
        }

        public static string StatusKey(PlannerEntry entry)
        {
            if (entry.Clash && entry.State != PlannerState.Done)
            {
                return "reallife_planner_state_clash";
            }
            switch (entry.State)
            {
                case PlannerState.Now: return "reallife_planner_state_now";
                case PlannerState.Done: return "reallife_planner_state_done";
                case PlannerState.Missed: return "reallife_planner_state_missed";
                case PlannerState.Over: return "reallife_planner_state_over";
                default: return entry.Next ? "reallife_planner_state_next" : null;
            }
        }

        public static string LeadKey(ReminderLead lead)
        {
            switch (lead)
            {
                case ReminderLead.AtTheTime: return "reallife_planner_lead_at";
                case ReminderLead.HourBefore: return "reallife_planner_lead_hour";
                case ReminderLead.EveningBefore: return "reallife_planner_lead_evening";
                default: return "reallife_planner_lead_none";
            }
        }
    }
}
