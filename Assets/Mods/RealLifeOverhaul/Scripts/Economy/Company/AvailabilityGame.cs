using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Entities;
using Entities.Employee.JobDemands;
using Entities.Employee.JobDemands.Requirements;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Part-timers with set hours (AvailabilityRules) as the game holds them: whose they are, in words, and the day's shifts outside them judged. The game's own demands
    /// are never changed: the hours are a rule of the mod's, read from the employee's id and their Part-time demand, and kept nowhere. Cost: a few reads of an employee's
    /// demands when a card, a page or the rota asks, and one pass over the staff on a new day (<see cref="Judge"/>).
    /// </summary>
    internal static class AvailabilityGame
    {
        public static IModLogger Logger;

        /// <summary>The part-timers the day just judged found at work outside their hours: how many, the first by name and its hours. For the one notice the company tells.</summary>
        public static int BreachCount, BreachHours;
        public static string BreachName = string.Empty;

        /// <summary>A new day's judging begins: nothing is counted yet.</summary>
        public static void BeginDay()
        {
            BreachCount = BreachHours = 0;
            BreachName = string.Empty;
        }

        /// <summary>The company has told of the day's breaches (or none is to be told).</summary>
        public static void EndDay() => BeginDay();

        private static readonly string[] DayKeys =
        {
            "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday"
        };

        /// <summary>
        /// The set hours of an employee or candidate, none while the option is off, for anybody with no part-time week, and for professionals a tech firm seats by its own
        /// weeks (they keep to the game's demands alone).
        /// </summary>
        public static AvailabilityWindow Of(EmployeeInstance e)
        {
            if (!Settings.StaffAvailability || e == null || string.IsNullOrEmpty(e.id) || e.demands == null)
            {
                return default(AvailabilityWindow);
            }
            try
            {
                int most = 0;
                for (int i = 0; i < e.demands.Count; i++)
                {
                    var hours = DemandOf(e.demands[i]) as HoursWorkingPerWeek;
                    if (hours != null && hours.MaxHours > 0)
                    {
                        most = most == 0 ? hours.MaxHours : Math.Min(most, hours.MaxHours);
                    }
                }
                if (!AvailabilityRules.PartTime(most) || (!e.IsCandidate && TechSeated(e)))
                {
                    return default(AvailabilityWindow);
                }
                return AvailabilityRules.Of(e.id, most);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex);
                return default(AvailabilityWindow);
            }
        }

        // The game's demand of a name, null for one this build does not know.
        private static JobDemand DemandOf(string name)
        {
            try
            {
                return JobDemandHelper.GetByName(name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // An employee at the player's tech firm, the kind the tech pass seats a week at a time (Tech/TechDirector.Seating).
        private static bool TechSeated(EmployeeInstance e)
        {
            BuildingRegistration reg = e.assignedAddress != null && !e.assignedAddress.IsUndefined() ? BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
            return reg != null && CompanyBridges.TechSeats != null && CompanyBridges.TechSeats(reg, e.GetPrimarySkill());
        }

        /// <summary>The window in words for a card or a page: "Student: Monday, Wednesday, Friday, 4:00 PM to 10:00 PM"; empty for none.</summary>
        public static string Words(AvailabilityWindow w)
        {
            string key = AvailabilityRules.KindKey(w.Kind);
            if (key == null)
            {
                return string.Empty;
            }
            return Say("reallife_avail_words", "kind", LocalizorManager.GetLocalization(key), "days", DaysWords(w), "from", Hour(w.From), "to", Hour(w.To));
        }

        /// <summary>What a planner demand of the window is called when a rota leaves it unmet: "Student hours".</summary>
        public static string DemandWords(AvailabilityWindow w) => Say("reallife_avail_demand", "kind", KindWords(w));

        /// <summary>Just the kind's name ("Student").</summary>
        public static string KindWords(AvailabilityWindow w)
        {
            string key = AvailabilityRules.KindKey(w.Kind);
            return key != null ? LocalizorManager.GetLocalization(key) : string.Empty;
        }

        /// <summary>The days: "Monday to Friday", "Saturday and Sunday" or each by name.</summary>
        public static string DaysWords(AvailabilityWindow w)
        {
            if (w.Days == AvailabilityRules.WeekdaysMask)
            {
                return Say("reallife_avail_weekdays");
            }
            if (w.Days == AvailabilityRules.WeekendMask)
            {
                return Say("reallife_avail_weekend_days");
            }
            string days = string.Empty;
            for (int d = 0; d < 7; d++)
            {
                if ((w.Days >> d & 1) != 0)
                {
                    days += (days.Length > 0 ? ", " : string.Empty) + LocalizorManager.GetLocalization(DayKeys[d]);
                }
            }
            return days;
        }

        // An hour of the day as the game writes it, by its own twelve or twenty-four hour setting.
        private static string Hour(int hour) => HourWords(hour);

        /// <summary>An hour of the day as the game writes the time, by its own twelve or twenty-four hour setting.</summary>
        public static string HourWords(int hour) => (hour % 24).GetFormattedTime(0f, false);

        private static string Say(string key, params string[] pairs)
        {
            string text = LocalizorManager.GetLocalization(key);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
            }
            return text;
        }

        /// <summary>
        /// The hours of a day's shifts of an employee that fall outside their set hours: the business's schedule for the weekday, every shift of theirs on it. 0 for an
        /// employee with no set hours or no shift that day.
        /// </summary>
        public static int HoursOff(EmployeeInstance e, AvailabilityWindow w, DayOfWeekOrdered weekday)
        {
            BuildingRegistration reg = e != null && e.assignedAddress != null && !e.assignedAddress.IsUndefined() ? BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
            List<ScheduleDay> week = reg != null ? reg.scheduleDays : null;
            int off = 0, day = (int)weekday - 1;
            for (int d = 0; !w.None && week != null && d < week.Count; d++)
            {
                if (week[d] == null || week[d].day != weekday || week[d].workShifts == null)
                {
                    continue;
                }
                for (int s = 0; s < week[d].workShifts.Count; s++)
                {
                    WorkShift shift = week[d].workShifts[s];
                    if (shift != null && shift.employeeId == e.id)
                    {
                        off += w.HoursOff(day, shift.startingHour, shift.endingHour);
                    }
                }
            }
            return off;
        }

        /// <summary>The hours of the business's whole week of an employee's shifts that fall outside their set hours: 0 when the week keeps to them (or they have no set hours).</summary>
        public static int WeekOff(EmployeeInstance e, AvailabilityWindow w)
        {
            int off = 0;
            for (int d = 1; d <= 7; d++)
            {
                off += HoursOff(e, w, (DayOfWeekOrdered)d);
            }
            return off;
        }

        /// <summary>
        /// The day that has ended judged for one employee at one of the player's businesses: the hours of their shifts outside their set hours cost mood at the weight the
        /// rota gives a medium demand (<see cref="AvailabilityRules.Cost"/>), never below the floor business pressure keeps. Absent or training that day: nothing. Returns the
        /// mood taken, tenths of a point.
        /// </summary>
        public static int Judge(EmployeeInstance e, DayOfWeekOrdered weekday, out int hoursOff)
        {
            hoursOff = 0;
            AvailabilityWindow w = Of(e);
            if (w.None || e.isAbsent || e.isTrainingDay)
            {
                return 0;
            }
            hoursOff = HoursOff(e, w, weekday);
            int cost = AvailabilityRules.Cost(hoursOff);
            if (cost > 0)
            {
                if (BreachCount++ == 0)
                {
                    BreachName = e.characterData != null && !string.IsNullOrEmpty(e.characterData.name) ? e.characterData.name : "?";
                    BreachHours = hoursOff;
                }
                float before = e.satisfaction;
                e.satisfaction = Mathf.Clamp(before - cost / 10f, Mathf.Min(before, BusinessCatalogue.SatisfactionFloor), 100f);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Company check (availability, " + (e.characterData != null ? e.characterData.name : "?") + "): " + hoursOff + " hour(s) of the day's shifts outside "
                                 + w.Kind + " hours, mood " + before.ToString("F1") + " to " + e.satisfaction.ToString("F1") + ": PASS");
                }
            }
            return cost;
        }

        /// <summary>The planner's two demands for a window: no work on the days outside it and none in the hours outside it, both of the highest priority, as the game's own are.</summary>
        public static void Demands(AvailabilityWindow w, List<RotaDemand> into)
        {
            ulong days = AvailabilityRules.DaysOffMask(w);
            if (days != 0)
            {
                into.Add(new RotaDemand { Kind = RotaDemandKind.FreeDays, Priority = RotaCatalogue.PriorityHigh, Mask = days });
            }
            into.Add(new RotaDemand { Kind = RotaDemandKind.NoHours, Priority = RotaCatalogue.PriorityHigh, Mask = AvailabilityRules.HoursOffMask(w) });
        }

        /// <summary>Whether a planner demand is one of the window's two (by what it asks), for the words said of it.</summary>
        public static bool IsWindowDemand(AvailabilityWindow w, RotaDemand d)
        {
            return !w.None && ((d.Kind == RotaDemandKind.FreeDays && d.Mask == AvailabilityRules.DaysOffMask(w))
                               || (d.Kind == RotaDemandKind.NoHours && d.Mask == AvailabilityRules.HoursOffMask(w)));
        }
    }
}
