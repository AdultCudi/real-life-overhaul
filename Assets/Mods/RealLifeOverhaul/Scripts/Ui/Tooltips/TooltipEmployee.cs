using System;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using Entities;
using Helpers;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the mod adds to the game's employee tooltip on the card: the business and the person's shift under their name ("Speedy Bites ·
    /// on shift until 19:00"), the game's satisfaction bar named as their mood (a word and its bar), and today's customers served. The
    /// game's own words (their years, hours a week, skills, demands, the overworked days) are the card's as the game wrote them. The
    /// customers are the business's own count today (the customers the game completed, as the Customers tab counts them), shared among
    /// the customer service staff whose shifts cover each hour (UiTooltipRules.Served): derived when the card is made, never saved. A
    /// person in another trade, or a business with no list of customers, gets no such row. Cost: a read when the card is made; a walk
    /// of today's shifts and customers of one business.
    /// </summary>
    internal static class TooltipEmployee
    {
        private const string CustomerService = "ba:skill_customerservice";
        private static readonly int[] PerHour = new int[24];
        private static readonly UiTipShift[] Crew = new UiTipShift[32];

        /// <summary>The employee the game's current tooltip is for, or null when it is not an employee's.</summary>
        internal static EmployeeInstance Current()
        {
            var member = GameContract.TipCurrent.Field;
            var target = member != null ? member.GetValue(null) as EmployeeTooltip : null;
            var person = GameContract.TipEmployee.Field;
            return target != null && person != null ? person.GetValue(target) as EmployeeInstance : null;
        }

        /// <summary>The employee's rows added to the model the game's words made.</summary>
        internal static void Extend(UiTooltipModel m, EmployeeInstance e)
        {
            if (e == null || e.isBeingReplaced || m.Blocks.Count == 0 || m.Blocks[0].Kind != UiTipKind.Header)
            {
                return;
            }
            BuildingRegistration reg = e.assignedAddress != null && !e.assignedAddress.IsUndefined() ? BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
            string place = reg != null ? reg.BusinessName : null, shift = Shift(e, reg);
            m.Blocks[0].Detail = !string.IsNullOrEmpty(place) && !string.IsNullOrEmpty(shift) ? place + " · " + shift : !string.IsNullOrEmpty(place) ? place : shift;
            int bar = -1;
            for (int i = 0; i < m.Blocks.Count && bar < 0; i++)
            {
                bar = m.Blocks[i].Kind == UiTipKind.Meter ? i : -1;
            }
            if (bar >= 0)
            {
                int mood = Mathf.RoundToInt(e.satisfaction);
                UiTipBlock b = m.Blocks[bar];
                b.Title = UiText.T("reallife_ui_tip_mood");
                b.Sub = UiText.T(UiTooltipRules.MoodStep(mood) == 0 ? "reallife_ui_tip_mood_low" : UiTooltipRules.MoodStep(mood) == 1 ? "reallife_ui_tip_mood_fair" : "reallife_ui_tip_mood_good");
                b.Value = Mathf.Clamp(mood, 0, 100);
                b.Tone = UiTooltipRules.MoodTone(mood);
            }
            int served = Served(e, reg);
            if (served >= 0)
            {
                int at = bar >= 0 ? bar + 1 : 1;
                var today = new UiTipBlock { Kind = UiTipKind.Category, Text = UiText.T("reallife_ui_tip_today") };
                var rows = new UiTipBlock { Kind = UiTipKind.Rows };
                rows.Lines.Add(new UiTipLine { Left = UiText.T("reallife_ui_tip_served"), Right = served.ToString() });
                m.Blocks.Insert(at, rows);
                m.Blocks.Insert(at, today);
            }
        }

        // "on shift until 19:00", "next shift at 14:00", "not on shift today", "away today"; null for a person with no business.
        private static string Shift(EmployeeInstance e, BuildingRegistration reg)
        {
            if (reg == null)
            {
                return null;
            }
            if (e.isAbsent)
            {
                return UiText.T("reallife_ui_tip_shift_away");
            }
            WorkShift now = e.GetWorkShiftAssignedInThisMoment();
            if (now != null)
            {
                return UiText.T("reallife_ui_tip_shift_until").Replace("{time}", UiClock.At(now.endingHour, 0));
            }
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            int hour = SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0, next = int.MaxValue;
            for (int i = 0; today != null && today.workShifts != null && i < today.workShifts.Count; i++)
            {
                WorkShift s = today.workShifts[i];
                if (s != null && s.employeeId == e.id && s.startingHour > hour && s.startingHour < next)
                {
                    next = s.startingHour;
                }
            }
            return next != int.MaxValue ? UiText.T("reallife_ui_tip_shift_next").Replace("{time}", UiClock.At(next, 0)) : UiText.T("reallife_ui_tip_shift_none");
        }

        // The customers this person served of the business's today, or -1 where there is no honest figure to give.
        private static int Served(EmployeeInstance e, BuildingRegistration reg)
        {
            if (reg == null || e.GetPrimarySkill() != CustomerService || !reg.RentedByPlayer)
            {
                return -1;
            }
            List<CustomerEntry> entries = CustomerDraw.Entries(reg);
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            if (entries == null || today == null || today.workShifts == null)
            {
                return -1;
            }
            int day = TimeHelper.CurrentDay;
            Array.Clear(PerHour, 0, PerHour.Length);
            for (int i = 0; i < entries.Count; i++)
            {
                CustomerEntry c = entries[i];
                if (c != null && c.completed && c.spawnTime != null && c.spawnTime.Day == day && c.spawnTime.Hour >= 0 && c.spawnTime.Hour < 24)
                {
                    PerHour[c.spawnTime.Hour]++;
                }
            }
            int crew = 0, mine = Who(e.id);
            for (int i = 0; i < today.workShifts.Count && crew < Crew.Length; i++)
            {
                WorkShift s = today.workShifts[i];
                EmployeeInstance who = s != null && !string.IsNullOrEmpty(s.employeeId) ? EmployeeHelper.GetEmployeeById(s.employeeId, false) : null;
                if (who != null && who.GetPrimarySkill() == CustomerService)
                {
                    Crew[crew++] = new UiTipShift { Who = Who(s.employeeId), From = s.startingHour, To = s.endingHour };
                }
            }
            return UiTooltipRules.Served(PerHour, Crew, crew, mine);
        }

        // A stable order for the crew from an id (a string's own hash may differ between runs).
        private static int Who(string id)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; id != null && i < id.Length; i++)
                {
                    h = (h ^ id[i]) * 16777619u;
                }
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}
