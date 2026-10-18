using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using RealLifeOverhaul.FitOut;
using RealLifeOverhaul.Tech;
using UI.Elements;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The working day at the desk: the click and the game's work panel, the day from home, and the boss when the player is away.</summary>
    public sealed partial class CareersDirector
    {
        private int _awayTold = -1;

        /// <summary>The player clicked their desk (Careers/DeskGame): they walk to it, and the game's work panel opens there.</summary>
        internal void DeskClicked(EntityController e)
        {
            if (!_workOn || (_deskWork != null && _deskWork.Open) || Work == null || Work.Job == null)
            {
                return;
            }
            if (e is DeskPointer && _deskFit != null)
            {
                FitDesk desk = _deskFit;
                DeskGame.WalkTo(desk.Stand, () => DeskPanel(null, desk));
            }
            else if (_deskStation != null && ReferenceEquals(e, _deskStation))
            {
                EmployeeStationController station = _deskStation;
                DeskGame.WalkTo(station, () => DeskPanel(station, null));
            }
        }

        private readonly List<LabelInfo> _deskLabels = new List<LabelInfo>(6);

        // The game's work panel for the job at a desk: its labels, Cancel, Start working or Work late.
        private void DeskPanel(EmployeeStationController station, FitDesk desk)
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null || Save == null || (_deskWork != null && _deskWork.Open))
            {
                return;
            }
            Profession p = j.Def;
            int day = Save.Day, minute = CareersGame.MinuteOfDay;
            DeskState state = DeskRules.State(j, day, minute, false, out int minutes, out bool late);
            bool canStart = DeskRules.Startable(state);
            _deskLabels.Clear();
            LabelInfo closed = DeskGame.Closed();
            if (closed != null)
            {
                _deskLabels.Add(closed);
            }
            string key = DeskRules.Label(state);
            if (key != null)
            {
                int colour = state == DeskState.NotYet || state == DeskState.StartsSoon ? 1 : 2;
                _deskLabels.Add(key.StartsWith("reallife_", StringComparison.Ordinal)
                    ? DeskGame.Label(state == DeskState.NotHired ? CareersText.T(key, "day", CareersText.Day(j.Hired)) : CareersText.T(key), colour)
                    : DeskGame.GameLabel(key, colour));
            }
            if (station != null && DeskGame.Taken(station))
            {
                _deskLabels.Add(DeskGame.Label(CareersText.T("reallife_desk_label_taken"), 1));
                canStart = false;
            }
            _deskLabels.Add(DeskGame.Label(CareersText.T("reallife_desk_label_hours", "from", CareersText.Hour(p.Hour), "to", CareersText.Hour((p.Hour + p.Hours) % 24)), 0));
            if (canStart)
            {
                _deskLabels.Add(DeskGame.Label(CareersText.T("reallife_work_skip_pay", "pay", CareersText.Rate(Prices.At(j.Pay))), 0));
                if (late && !(j.TodayDay == day && j.TodayMinutes > 0))
                {
                    _deskLabels.Add(DeskGame.Label(CareersText.T("reallife_desk_label_late"), 1));
                }
            }
            bool canLate = state == DeskState.Done && CanWorkLate() == null && !(station != null && DeskGame.Taken(station));
            string title = CareersText.Fill("reallife_work_skip_title", "title", CareersText.Title(j.Post.Prof, j.Post.Rank), "employer", CareersText.Employer(j.Post.Employer));
            var work = new DeskWork(this, station, desk, title, _deskLabels.ToArray(), canStart, canLate);
            if (DeskGame.Show(work))
            {
                _deskWork = work;
            }
            if (Verbose)
            {
                Logger?.Info("Careers check (desk panel): day " + day + " at " + CareersText.Hour(minute / 60) + ", " + state + " (" + minutes + " minutes left"
                             + (late ? ", late" : string.Empty) + "), Start " + (canStart ? "offered" : "not offered") + ", Work late " + (canLate ? "offered" : "not offered")
                             + ": " + (DeskRules.Startable(state) == (minutes > 0) ? "PASS" : "FAIL: the minutes do not agree with the state"));
            }
        }

        /// <summary>
        /// Start working or Work late pressed at the desk: the Career app's checks but for the place, and the day's clock-in.
        /// Null with the sitting, or the reason key.
        /// </summary>
        internal string DeskBegin(bool overtime, out Sitting sitting)
        {
            sitting = null;
            Job j = Work != null ? Work.Job : null;
            if (j == null || Save == null)
            {
                return "reallife_work_why_no_job";
            }
            if (_sitting != null || (_skip != null && _skip.Open))
            {
                return "reallife_study_why_busy";
            }
            if (CareersGame.Energy < StudyCatalogue.TiredEnergy)
            {
                return "reallife_study_why_tired";
            }
            int day = Save.Day;
            if (overtime)
            {
                string why = CanWorkLate();
                if (why != null)
                {
                    return why;
                }
                sitting = LateSitting(j);
            }
            else
            {
                bool home = CareersGame.AtHome();
                DeskState state = DeskRules.State(j, day, CareersGame.MinuteOfDay, home, out int minutes, out bool late);
                if (!DeskRules.Startable(state))
                {
                    return DeskRules.Label(state) ?? "reallife_work_why_not_hours";
                }
                string offline = home ? StudyRules.NoNetWhy(CareersBridges.HomeNetNow()) : null;
                if (offline != null)
                {
                    return offline;
                }
                ClockIn(j, day, late);
                sitting = DaySitting(j, minutes);
            }
            _sitting = sitting;
            Logger?.Info("Careers: " + (overtime ? "overtime" : "a working day") + " begun at the desk for " + j.Post.Employer + " on day " + day + " at "
                         + CareersText.Hour(CareersGame.MinuteOfDay / 60)
                         + ", " + sitting.SkipMinutes + " minutes.");
            return null;
        }

        /// <summary>The first stint of a working day: its clock-in, and lateness counted once a day.</summary>
        private static void ClockIn(Job j, int day, bool late)
        {
            if (j.TodayDay == day)
            {
                return;
            }
            j.TodayDay = day;
            j.TodayMinutes = 0;
            j.ClockIn = CareersGame.MinuteOfDay;
            if (late)
            {
                j.Late++;
            }
        }

        /// <summary>The rest of a working day as one of the game's activities.</summary>
        private Sitting DaySitting(Job j, int minutes)
        {
            Profession p = j.Def;
            return new Sitting(this, SittingKind.Work, 0, minutes, CareersText.Fill("reallife_work_skip_title", "title", CareersText.Title(j.Post.Prof, j.Post.Rank),
                    "employer", CareersText.Employer(j.Post.Employer)), true,
                CareersText.T("reallife_work_skip_until", "time", CareersText.Hour((p.Hour + p.Hours) % 24)),
                CareersText.T("reallife_work_skip_pay", "pay", CareersText.Rate(Prices.At(j.Pay))));
        }

        /// <summary>A stint of overtime as one of the game's activities.</summary>
        private Sitting LateSitting(Job j)
        {
            return new Sitting(this, SittingKind.Overtime, 0, LifeRules.OvertimeMinutes, CareersText.Fill("reallife_work_skip_title", "title", CareersText.Title(j.Post.Prof, j.Post.Rank),
                "employer", CareersText.Employer(j.Post.Employer)), true, CareersText.T("reallife_life_skip_overtime"),
                CareersText.T("reallife_work_skip_pay", "pay", CareersText.Rate(Prices.At(j.Pay))));
        }

        /// <summary>
        /// The home computer's button for the job beside Pass the time, or null for none: Start the working day, Work late, or,
        /// greyed, the hours before it can begin and the desk's words after (too late, worked already).
        /// </summary>
        internal string ComputerWork(out bool can, out bool overtime)
        {
            can = false;
            overtime = false;
            Job j = Work != null ? Work.Job : null;
            if (j == null || Save == null || DeskFallback(j))
            {
                return null;   // no job, or the Career app starts the day
            }
            DeskState state = DeskRules.State(j, Save.Day, CareersGame.MinuteOfDay, true, out _, out _);
            switch (DeskRules.AtComputer(state, CanWorkLate() == null))
            {
                case HomeWork.Start:
                    can = true;
                    return CareersText.T("reallife_career_button_work");
                case HomeWork.Late:
                    can = true;
                    overtime = true;
                    return CareersText.T("reallife_life_button_late");
                case HomeWork.NotYet:
                    Profession p = j.Def;
                    return CareersText.T("reallife_desk_label_hours", "from", CareersText.Hour(p.Hour), "to", CareersText.Hour((p.Hour + p.Hours) % 24));
                case HomeWork.Why:
                    return CareersText.T(DeskRules.Label(state));
                default:
                    return null;
            }
        }

        /// <summary>
        /// The working day or overtime at a home computer, from its own button: the desk's checks and clock-in
        /// (<see cref="DeskBegin"/>), then the walk and the sitting (Careers/ComputerSitting). Null if under way, else the reason key.
        /// </summary>
        internal string WorkAtComputer(ComputerController computer, bool overtime)
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null)
            {
                return "reallife_work_why_no_job";
            }
            if (!CareersGame.AtHome())
            {
                return "reallife_work_why_home_day";
            }
            if (computer == null || !DeskGame.Usable(computer) || computer.FindChair() == null)
            {
                return "reallife_career_desk_no_computer";
            }
            string busy = SeatBusy();
            if (busy != null)
            {
                return busy;
            }
            if (PlayerHelper.ItemInstanceInHands != null && !PlayerHelper.IsHoldingBag)
            {
                return "notification_need_empty_hands_to_interact";   // the game's own words, as its computer says them
            }
            int lateBefore = j.Late;
            string why = DeskBegin(overtime, out Sitting sitting);
            if (why != null)
            {
                return why;
            }
            why = StartAtComputer(computer, sitting, j.Post.Employer);
            if (why != null)
            {
                _sitting = null;   // the game's panel was busy: nothing begun; the clock-in stands, as at a desk
                return why;
            }
            if (j.Late > lateBefore)
            {
                Tell2(NotificationType.Warning, "reallife_desk_label_late", null, TabJob, null);   // what the desk's panel says before Start
            }
            return null;
        }

        /// <summary>An hour into a working day not begun, with the player not there: the boss texts, as the game's own employer does.</summary>
        private void DeskHour(Job j, int day, int hour)
        {
            if (!DeskRules.Away(j, day, hour) || _awayTold == day || Working || Excused(day))
            {
                return;
            }
            Profession p = j.Def;
            bool there = ProRules.FromHome((WorkMode)j.Work, day) ? CareersGame.AtHome() : WorkGame.AtWork(p, j.Post);
            if (there)
            {
                return;
            }
            _awayTold = day;
            var data = Data("from", CareersText.Hour(p.Hour), "to", CareersText.Hour((p.Hour + p.Hours) % 24), "until", CareersText.Hour(Math.Min(23, (p.Hour * 60 + ProRules.ClockLateMinutes) / 60)));
            bool sent = DeskGame.BossText(CareersText.Employer(j.Post.Employer), j.Post.Street, j.Post.Number, "reallife_work_text_away", data);
            Logger?.Info("Careers: an hour into the working day at " + j.Post.Employer + " and the player is not there: the boss " + (sent ? "texted." : "could not text; nothing more is said."));
        }

        /// <summary>The boss's text for a working day missed, or the sack; a notification as before where the text cannot be sent.</summary>
        private void BossSays(Job j, string key, Dictionary<string, string> data, NotificationType type, string noticeKey, int tab)
        {
            if (!DeskGame.BossText(CareersText.Employer(j.Post.Employer), j.Post.Street, j.Post.Number, key, data))
            {
                Tell2(type, noticeKey, data, tab, null);
            }
        }

        /// <summary>What and where the player's desk is: seen this visit or before, else what the job is to have.</summary>
        internal string DeskLine(Job j)
        {
            string what = _deskWhat != null && _deskWhatOf == JobPlace(j) ? _deskWhat : DeskRules.Expected(j.Def);
            return CareersText.Fill("reallife_career_desk_at", "what", CareersText.T(what), "employer", CareersText.Employer(j.Post.Employer),
                "address", WorkGame.Address(j.Post.Street, j.Post.Number));
        }

        /// <summary>What to do today, in a line; null on a day with nothing to do.</summary>
        internal string DeskToday(Job j)
        {
            int day = Save != null ? Save.Day : 0;
            if (!ProRules.WorkDay(j.Def, day) || day < j.Hired || j.SettledDay >= day)
            {
                return null;
            }
            return CareersText.T(ProRules.FromHome((WorkMode)j.Work, day) ? "reallife_career_desk_home" : "reallife_career_desk_how");
        }

        /// <summary>The game's destination marker on the employer's building. Null if done, else a reason key.</summary>
        internal string DeskRoute()
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null)
            {
                return "reallife_work_why_no_job";
            }
            return DeskGame.Route(j.Post.Street, j.Post.Number) ? null : "reallife_career_why_route";
        }

        /// <summary>The test button: where the desk is; inside the employer's building, its work panel opened at once (its labels as the hour has them).</summary>
        public void TestDesk()
        {
            if (!WorkReady())
            {
                return;
            }
            try
            {
                Job j = _study.Work.Job;
                if (j == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_work_test_desk_none", null, 8f);
                    return;
                }
                if (_deskAt == JobPlace(j))
                {
                    _deskFailed = false;   // looked for again
                }
                DeskSetUp(SceneFinds.Hidden);
                Logger?.Info("Careers test: the desk of " + j.Def.Key + " at " + j.Post.Employer + ", " + j.Post.Street + " " + j.Post.Number + ": "
                             + (_deskStation != null ? "a station of the game's, " + StationName(_deskStation)
                                 : _deskFit != null ? "a fitted desk (" + _deskFit.Kind + ")" : _deskFailed ? "none this visit" : "not here")
                             + "; expected " + DeskRules.Expected(j.Def) + ".");
                var data = Data("what", CareersText.T(_deskWhat != null && _deskWhatOf == JobPlace(j) ? _deskWhat : DeskRules.Expected(j.Def)), "employer", CareersText.Employer(j.Post.Employer),
                    "address", WorkGame.Address(j.Post.Street, j.Post.Number));
                if (_deskStation != null || _deskFit != null)
                {
                    DeskPanel(_deskStation, _deskFit);
                    Notifications.Show(NotificationType.Success, "reallife_work_test_desk_here", data, 8f);
                    return;
                }
                DeskRoute();
                Notifications.Show(NotificationType.Info, "reallife_work_test_desk_route", data, 10f);
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }
    }
}
