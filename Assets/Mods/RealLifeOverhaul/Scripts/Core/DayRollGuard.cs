using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Items;
using Entities;
using Helpers;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps the game's night whole (the why is in <see cref="DayRollRules"/>). The game calls its hour event first thing
    /// in its new day, after the day is counted on and before it pays the day's wages and before the rivals' step
    /// (GameManager.NewDay), and that call is the one with the hour at nothing. There, once a game day:
    ///
    ///   the market   goods in the city's market that the game no longer knows (a business type of a mod that is not
    ///                loaded) are taken out of it, so the rivals' step that looks them up cannot throw and stop the
    ///                night. Nothing else of the market is touched; were the mod loaded again, the game's own daily run
    ///                puts its goods back (ProductMarketHelper.UpdateMarketDemands makes an entry for every item it has).
    ///   the hours    where the night before stopped short of the employees' day (its day event never came), or an
    ///                employee is counted more hours than a day has, the hours about to be paid are brought back to
    ///                their shifts on the day that ended, and the week's hours and days to their shifts from its Monday
    ///                to that day. And at the hour after a night that stopped before its day event, the counts its
    ///                employees' day would have cleared are cleared: each one's hours of the day, and the week's hours
    ///                and days on a Monday. A night that ran as the game meant is never touched.
    ///
    /// Whatever the cause, it is said once a session in a notice and every time in one line of Player.log. Only the
    /// player's own employees are mended in a Going Public session (Core/GoingPublic.Foreign). The market is left alone on
    /// a Going Public guest: the rivals' step runs on the host only (Going Public skips it on a guest), and the host's
    /// market reaches the guest again with its next update; on the host, taking goods nobody can sell out of the market
    /// is the same on every machine with the same mods.
    ///
    /// Cost: no Update. A one-second timer on the real clock keeps the two handlers on the game's events (one reference
    /// comparison each); the hour handler is one comparison every game hour, and once a game day (twice after a stopped night) a pass over the market's
    /// entries (a few hundred dictionary looks) and the employees (one schedule look each).
    /// </summary>
    public sealed class DayRollGuard : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "DayRollGuard", typeof(DayRollGuard));   // its handlers and timer (Core/Guard)

        public static IModLogger Logger;

        private const int NamesShown = 3;   // goods named in the log line: the rest are counted

        private Action _onHour, _onDay, _hourSeen, _daySeen;
        private GameInstance _of;           // the save the days below belong to
        private int _rollDay;               // the day whose night was last seen to start (its hour at nothing)
        private int _doneDay;               // the day whose day event was last seen
        private int _clearedDay;            // the day whose stopped night's hours were cleared at its next hour
        private bool _told;                 // the notice has been shown this session
        private readonly List<string> _gone = new List<string>(4);

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            _onHour = Guard.Wrap(FrameGuard, OnHour);
            _onDay = Guard.Wrap(FrameGuard, OnDay);
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            GlobalEvents.onNewHour -= _onHour;
            GlobalEvents.onNewDay -= _onDay;
        }

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (FrameGuard.Off || Save == null || LoadScene.isLoading)
                {
                    continue;
                }
                EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
                EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            }
        }

        private void Watch()
        {
            if (!ReferenceEquals(_of, Save))
            {
                _of = Save;
                _rollDay = _doneDay = _clearedDay = 0;   // (a save just loaded: nothing of its nights has been seen)
            }
        }

        private void OnDay()
        {
            Watch();
            _doneDay = Save.Day;
        }

        private static readonly int HotRoll = HotSpots.Slot("Core: DayRollGuard, the night's first hour");
        private void OnHour()
        {
            GameInstance save = Save;
            if (save == null || save.Hour > 1 || !ModState.Loaded)
            {
                return;
            }
            Watch();
            if (save.Hour == 1)
            {
                AfterNight(save);
                return;
            }
            int day = save.Day;
            if (day == _rollDay)
            {
                return;
            }
            using var hotSpot = HotSpots.Time(HotRoll);
            bool missed = DayRollRules.Missed(_rollDay, _doneDay, day);
            _rollDay = day;
            _gone.Clear();
            int entries = 0;
            int goods = GoingPublic.IsGuest ? 0 : Market(save, _gone, out entries);
            int people = Hours(save, day, missed, out int mended, out int hoursBefore, out int hoursAfter);
            if (goods > 0 || mended > 0)
            {
                Logger?.Warn("Day roll: the game's night of day " + day + ": " + goods + " good(s) of " + entries + " in the city's market the game no longer knows taken out ("
                             + Names(_gone) + "); the night before " + (missed ? "stopped before the employees' day" : "was not seen to stop") + "; " + mended + " of " + people
                             + " employee(s) had their hours put right before the wages, " + hoursBefore + " hour(s) to " + hoursAfter + ".");
                Tell(goods > 0);
            }
            else if (Verbose)
            {
                Logger?.Info("Day roll check (day " + day + "): " + entries + " good(s) in the city's market, every one the game's; the night before " + (missed ? "stopped before the employees' day" : "ran to its end or was not seen")
                             + "; " + people + " employee(s), every one's hours as the game counted them: PASS");
            }
        }

        // The hour after the night: if the night stopped before its day event, the counts the employees' day it never reached
        // would have cleared are cleared here as the game does it (EmployeeHelper.RunDaily: the day's hours, and the week's
        // hours and days on a Monday). The stopped night took the game's first hour with it, so nothing has been counted
        // since the wages were paid.
        private void AfterNight(GameInstance save)
        {
            int day = save.Day;
            if (!DayRollRules.Stopped(_rollDay, _doneDay, day) || _clearedDay == day)
            {
                return;
            }
            using var hotSpot = HotSpots.Time(HotRoll);
            _clearedDay = day;
            bool monday = TimeHelper.GetDayOfWeek(day) == DayOfWeekOrdered.Monday;
            List<EmployeeInstance> all = save.EmployeeInstances;
            int cleared = 0;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (e == null || e.IsCandidate || Foreign(e))
                {
                    continue;
                }
                e.workedHoursToday = DayRollRules.Cleared(e.workedHoursToday);
                e.workedHoursThisWeek = DayRollRules.ClearedWeek(e.workedHoursThisWeek, monday);
                e.workedDays = DayRollRules.ClearedWeek(e.workedDays, monday);
                cleared++;
            }
            Logger?.Warn("Day roll: the game's night of day " + day + " stopped before its day event (the error just before this in Player.log); the hours of " + cleared
                         + " employee(s) cleared as its employees' day would have" + (monday ? ", and the week's hours and days, it being a Monday" : string.Empty) + ".");
            Tell(false);
        }

        private static bool Foreign(EmployeeInstance e)
        {
            BuildingRegistration reg = e.assignedAddress != null ? BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
            return reg != null && GoingPublic.Foreign(reg);
        }

        // Goods of the market the game cannot find, taken out; how many. (ItemsGetter.GetByName with its error kept quiet.)
        private static int Market(GameInstance save, List<string> gone, out int entries)
        {
            List<ProductMarketEntry> market = save.productMarketEntries;
            entries = market != null ? market.Count : 0;
            int removed = 0;
            for (int i = entries - 1; i >= 0; i--)
            {
                ProductMarketEntry entry = market[i];
                if (entry != null && !string.IsNullOrEmpty(entry.itemName) && ItemsGetter.GetByName(entry.itemName, true) != null)
                {
                    continue;
                }
                if (entry != null && gone.Count < NamesShown)
                {
                    gone.Add(entry.itemName ?? "?");
                }
                market.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        // The hours about to be paid tonight, put right where the clearing was missed; how many employees were looked at.
        private static int Hours(GameInstance save, int day, bool missed, out int mended, out int before, out int after)
        {
            mended = before = after = 0;
            List<EmployeeInstance> all = save.EmployeeInstances;
            int people = 0;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (e == null || e.IsCandidate)
                {
                    continue;
                }
                people++;
                int worked = e.workedHoursToday;
                if (!DayRollRules.Suspect(worked, missed))
                {
                    continue;   // (a day the game counted as it meant: one comparison)
                }
                BuildingRegistration reg = e.assignedAddress != null ? BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
                if (reg != null && GoingPublic.Foreign(reg))
                {
                    continue;   // another player's employee: theirs to keep
                }
                int shifts = Shifts(e, reg, day - 1, out int sinceMonday, out int daysOn);
                int kept = DayRollRules.Mend(worked, shifts, true);
                int week = DayRollRules.Week(e.workedHoursThisWeek, sinceMonday);
                int days = DayRollRules.Days(e.workedDays, daysOn);
                if (kept == worked && week == e.workedHoursThisWeek && days == e.workedDays)
                {
                    continue;
                }
                before += Math.Max(0, worked);
                after += kept;
                e.workedHoursToday = kept;   // (a delivery driver's pay is a fixed day whatever this count: EmployeeInstance.PayWage)
                e.workedHoursThisWeek = week;
                e.workedDays = days;
                mended++;
            }
            return people;
        }

        // An employee's shifts on a day, in hours, and from that week's Monday to the day, in hours and in days with a shift;
        // nothing with no business or schedule.
        private static int Shifts(EmployeeInstance e, BuildingRegistration reg, int day, out int sinceMonday, out int daysOn)
        {
            sinceMonday = daysOn = 0;
            if (reg == null || reg.scheduleDays == null)
            {
                return 0;
            }
            DayOfWeekOrdered weekday = TimeHelper.GetDayOfWeek(day);
            int on = 0;
            for (int i = 0; i < reg.scheduleDays.Count; i++)
            {
                ScheduleDay d = reg.scheduleDays[i];
                if (d == null || d.day > weekday)
                {
                    continue;
                }
                int hours = e.GetWorkingHoursOnDay(d);
                sinceMonday += hours;
                daysOn += hours > 0 ? 1 : 0;
                on += d.day == weekday ? hours : 0;
            }
            return on;
        }

        private void Tell(bool goods)
        {
            if (_told)
            {
                return;
            }
            _told = true;
            if (goods)
            {
                Notifications.Show(NotificationType.Warning, "reallife_dayroll_note_goods", null, 16f, null, null, true, false);
            }
            else
            {
                Notifications.Show(NotificationType.Warning, "reallife_dayroll_note_hours", null, 16f, null, null, true, false);
            }
        }

        private static string Names(List<string> names)
        {
            if (names.Count == 0)
            {
                return "none";
            }
            var text = new StringBuilder(64);
            for (int i = 0; i < names.Count; i++)
            {
                text.Append(i > 0 ? ", " : string.Empty).Append(names[i]);
            }
            return text.ToString();
        }
    }
}
