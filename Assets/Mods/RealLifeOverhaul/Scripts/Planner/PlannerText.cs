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
        // The game's own icons, by category: the one that fits best, then one that the phone's own scene uses and
        // that is therefore certainly in memory (docs/game-ui.md lists which are). GameUi.GameSprites finds them by
        // name; if neither is there the row has no picture.
        private static readonly string[][] IconNames =
        {
            new[] { "Icon-PersonEngineer", "Icon-PersonManage" }, new[] { "Icon-Security" }, new[] { "Icon-Traffic", "Icon-Parking" },
            new[] { "Icon-People", "Icon-PersonManage" }, new[] { "Icon-Chat", "Icon-Info" }, new[] { "Icon-Route" },
            new[] { "Icon-Neighborhood", "Icon-Building" }, new[] { "Icon-Shop" }, new[] { "Icon-Condition" },
            new[] { "Icon-Money", "Icon-Coin" }, new[] { "Icon-Edit" }
        };

        /// <summary>The glyph on the app's own icon: a calendar, or the clock face the game's opening hours use.</summary>
        public static readonly string[] AppGlyphs = { "Icon-Calendar", "Icon-OpeningHours" };

        private static readonly string[] CategoryKeys =
        {
            "reallife_planner_cat_work", "reallife_planner_cat_court", "reallife_planner_cat_licence", "reallife_planner_cat_family",
            "reallife_planner_cat_social", "reallife_planner_cat_travel", "reallife_planner_cat_city", "reallife_planner_cat_business",
            "reallife_planner_cat_health", "reallife_planner_cat_money", "reallife_planner_cat_note"
        };

        public static void WantIcons()
        {
            for (int i = 0; i < IconNames.Length; i++)
            {
                GameUi.GameSprites.Want(IconNames[i]);
            }
            GameUi.GameSprites.Want(AppGlyphs);
        }

        public static Sprite Icon(PlannerCategory category)
        {
            int index = (int)category;
            return index >= 0 && index < IconNames.Length ? First(IconNames[index]) : null;
        }

        public static Sprite First(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Sprite sprite = GameUi.GameSprites.Get(names[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
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

        /// <summary>An entry's "Costs $120", with the sum kept for the day's box (PlannerEntry.Owes). Words: the sum as its feature writes it.</summary>
        public static void Costs(PlannerEntry entry, float amount, string words)
        {
            entry.Owes = amount > 0f ? amount : 0f;
            entry.Money = T("reallife_planner_costs", "amount", words);
        }

        // ---------------------------------------------------------------- kinds, groups, tags

        private static readonly string[] GroupKeys =
        {
            "reallife_planner_group_work", "reallife_planner_group_money", "reallife_planner_group_life", "reallife_planner_group_business"
        };

        /// <summary>The filter's words for the four groups (PlannerGroup), in their order.</summary>
        public static string[] GroupKeyList => GroupKeys;

        // The short word on an entry in the Week tab: the kind's own where it is short, a shorter one where it is not.
        private static readonly string[] TagKeys =
        {
            "reallife_planner_cat_work", "reallife_planner_tag_court", "reallife_planner_tag_licence", "reallife_planner_cat_family",
            "reallife_planner_cat_social", "reallife_planner_cat_travel", "reallife_planner_cat_city", "reallife_planner_cat_business",
            "reallife_planner_cat_health", "reallife_planner_cat_money", "reallife_planner_cat_note"
        };

        public static string TagKey(PlannerCategory category)
        {
            int index = (int)category;
            return index >= 0 && index < TagKeys.Length ? TagKeys[index] : TagKeys[TagKeys.Length - 1];
        }

        /// <summary>The tone a kind's tag is written in (the mod's tones, GameUi.CellTone): work and study yellow, money green, the rest plain; red is kept for a clash or a miss.</summary>
        public static GameUi.CellTone TagTone(PlannerCategory category)
        {
            switch (PlannerRules.GroupOf(category))
            {
                case PlannerGroup.Work: return GameUi.CellTone.Yellow;
                case PlannerGroup.Money: return GameUi.CellTone.Green;
                default: return GameUi.CellTone.Plain;
            }
        }

        /// <summary>"3 hours 20 minutes", "45 minutes": a stretch of game time.</summary>
        public static string Span(int minutes)
        {
            minutes = minutes < 0 ? 0 : minutes;
            int hours = minutes / 60, rest = minutes % 60;
            if (hours == 0)
            {
                return T("reallife_planner_span_minutes", "minutes", rest.ToString());
            }
            if (rest == 0)
            {
                return T("reallife_planner_span_hours", "hours", hours.ToString());
            }
            return T("reallife_planner_span_both", "hours", hours.ToString(), "minutes", rest.ToString());
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
                case ReminderLead.QuarterBefore: return "reallife_planner_lead_quarter";
                case ReminderLead.HalfHourBefore: return "reallife_planner_lead_half";
                case ReminderLead.HourBefore: return "reallife_planner_lead_hour";
                case ReminderLead.EveningBefore: return "reallife_planner_lead_evening";
                case ReminderLead.LeaveBy: return "reallife_planner_lead_leave";
                default: return "reallife_planner_lead_none";
            }
        }

        /// <summary>
        /// The reminders an entry's box offers, in this order: none, 15, 30 and 60 minutes before, at the time, the
        /// evening before, and (for one at a place) when to leave.
        /// </summary>
        public static readonly ReminderLead[] Leads =
        {
            ReminderLead.None, ReminderLead.QuarterBefore, ReminderLead.HalfHourBefore, ReminderLead.HourBefore,
            ReminderLead.AtTheTime, ReminderLead.EveningBefore, ReminderLead.LeaveBy
        };

        /// <summary>The place of a reminder in Leads; 0 (none) for one not there.</summary>
        public static int LeadIndex(ReminderLead lead)
        {
            int index = System.Array.IndexOf(Leads, lead);
            return index < 0 ? 0 : index;
        }
    }
}
