using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A police shift inside the working life: the officer goes on patrol from the front desk of a station (or the Career app), wears the officer's
    /// uniform, and the incidents the city raises near them (Crime, Traffic, Ragdoll) become calls they answer from a list with the danger and the odds
    /// shown. Rules are PoliceRules (game-free); the day, the pay, the ranks and the dismissal are the careers' own. Cost: nothing per frame; from the
    /// one-second timer, while on duty, a clock read, one look for an incident (a few field reads in Crime, Traffic and Ragdoll) and, every few seconds,
    /// one look at the conduct and health. Off duty it costs one flag test a second.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private const int Kept = 4;                  // incidents answered or backed off that are not offered again

        private bool _dutyOn;
        private GameInstance _dutySave;              // the save the shift began in: another save ends it
        private int _dutyDay, _dutyLast, _dutyPending, _dutyAnswered, _dutyLook, _dutyTold = -1;
        private float _dutyQuiet;
        private PoliceDesk _dutyDesk;
        private JobUniform _dutyUniform;
        private readonly int[] _dutyKept = new int[Kept];
        private readonly List<DutyEntry> _dutyEntries = new List<DutyEntry>(5);
        private DutyCall _dutyOpen;                  // the incident whose list is up

        /// <summary>True while the player is on a police shift.</summary>
        public bool DutyOn => _dutyOn;

        // ------------------------------------------------------------------------------------------------- who may serve

        /// <summary>
        /// The reason the player cannot join the service, with its figures, or null: asked of a post of police work wherever a post is applied for or
        /// taken (the entry exam is the cadet rung's own qualification, asked as every rung's is).
        /// </summary>
        private string DutyEntryWhy(Profession p)
        {
            if (p == null || p.Id != ProCatalogue.Police)
            {
                return null;
            }
            string why = PoliceRules.EntryWhy(CareersBridges.ConductOver(int.MaxValue), Year, out int days);
            return why == null ? null : CareersWhy.With(why, "days", days.ToString());
        }

        // ------------------------------------------------------------------------------------------------- the shift

        /// <summary>
        /// Puts the uniform on and begins the shift's patrol. Null once begun, else the reason key: the same checks the working day has (the hours, the
        /// place, a day still to work) and the officer's own (well, a licence, no gig under way).
        /// </summary>
        public string DutyBegin()
        {
            Job j = Work != null ? Work.Job : null;
            if (!IsPolice(j))
            {
                return "reallife_work_why_no_job";
            }
            if (_dutyOn)
            {
                return null;
            }
            string why = CanWork(out int minutes, out bool late);
            if (why != null)
            {
                return why;
            }
            if (_broken || !isActiveAndEnabled)
            {
                return "reallife_study_test_off";
            }
            if (_book.Count > 0 || SittingNow)
            {
                return "reallife_study_why_busy";
            }
            if (!CareersGame.Free())
            {
                return "reallife_study_why_game_busy";
            }
            if (CareersGame.Energy < StudyCatalogue.TiredEnergy)
            {
                return TiredWhy();
            }
            int day = Save.Day;
            ClockIn(j, day, late);
            bool dressed = DutyStart(day);
            Logger?.Info("Careers: a patrol begun for " + j.Post.Employer + " on day " + day + " at " + CareersText.Hour(CareersGame.MinuteOfDay / 60) + ", " + minutes
                         + " minutes, rating " + _duty.Rating + (dressed ? "." : "; the uniform could not be put on."));
            Tell2(NotificationType.Success, "reallife_police_note_on", Data("until", CareersText.Hour((j.Def.Hour + j.Def.Hours) % 24)), TabJob, null);
            Changed();
            return null;
        }

        // The shift's state at its start, the uniform on; false if the uniform could not be put on (the patrol goes on in the player's own clothes).
        private bool DutyStart(int day)
        {
            DutyLoad();
            _duty.Begin(day);
            _dutyOn = true;
            _dutySave = Save;
            _dutyDay = day;
            _dutyLast = CareersGame.MinuteOfDay;
            _dutyPending = 0;
            _dutyAnswered = 0;
            _dutyTold = -1;
            _dutyQuiet = 0f;
            Array.Clear(_dutyKept, 0, _dutyKept.Length);
            _caseBusyUntil = -1;
            return DutyDress(true);
        }

        /// <summary>The player ends the shift, or the hours ran out (<see cref="DutyEnd.Handed"/>), or something ended it.</summary>
        internal void DutyStop(DutyEnd why)
        {
            if (!_dutyOn)
            {
                return;
            }
            Job j = Work != null ? Work.Job : null;
            try
            {
                DutyBank();
                _dutyOn = false;
                _dutyDesk?.Close();
                DutyDress(false);
                DutyLoad();
                int rating = _duty.Rating;
                bool quiet = _duty.Today == 0;
                _duty.Shifts++;
                _duty.Close();
                if (PoliceRules.ShiftCommended(rating, _duty.Today))
                {
                    _duty.Commendations++;
                    Tell2(NotificationType.Success, "reallife_police_note_commend_shift", Data("rating", rating.ToString()), TabJob, null);
                }
                int worked = j != null && j.TodayDay == _dutyDay ? j.TodayMinutes : 0;
                string key = why == DutyEnd.Hurt ? "reallife_police_note_off_hurt" : why == DutyEnd.Skipped ? "reallife_police_note_off_skipped"
                    : why == DutyEnd.Dismissed ? null : quiet ? "reallife_police_note_off_quiet" : "reallife_police_note_off";
                if (key != null)
                {
                    Tell2(NotificationType.Info, key, Data("minutes", CareersText.Minutes(worked), "rating", rating.ToString(), "answered", _duty.Today.ToString()), TabJob, null);
                }
                Logger?.Info("Careers: the patrol ended (" + why + "): " + CareersText.Minutes(worked) + " worked, rating " + rating + ", " + _duty.Today + " incident(s) answered.");
            }
            catch (Exception e)
            {
                Fault("the end of a patrol", e);
            }
            _dutyOn = false;
            Changed();
        }

        // Every way the shift can stop without a word: the uniform off, what was counted kept, nothing said.
        private void DutyDown(string why)
        {
            if (!_dutyOn && _dutyUniform == null)
            {
                return;
            }
            try
            {
                if (_dutyOn)
                {
                    _dutyOn = false;
                    if (_workOn && Work != null && IsPolice(Work.Job) && ReferenceEquals(_dutySave, Save))
                    {
                        DutyBank();
                    }
                    Logger?.Info("Careers: the patrol stopped (" + why + ").");
                }
                _dutyPending = 0;
                _dutyDesk?.Close();
                DutyDress(false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private bool DutyDress(bool on)
        {
            try
            {
                if (_dutyUniform == null)
                {
                    if (!on)
                    {
                        return true;
                    }
                    _dutyUniform = new JobUniform(Logger);
                }
                if (!on)
                {
                    _dutyUniform.Off();
                    return true;
                }
                return _dutyUniform.Wear(Helpers.PlayerHelper.PlayerController, CityLook.Officer);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // The minutes patrolled since the last count are worked and paid as any working time is (the day, the skills, the ladder).
        private void DutyBank()
        {
            int minutes = _dutyPending;
            _dutyPending = 0;
            Job j = Work != null ? Work.Job : null;
            if (minutes > 0 && IsPolice(j))
            {
                WorkBanked(j, false, minutes);
            }
        }

        /// <summary>The hour's turn: what was patrolled is counted, and a shift whose hours are over ends.</summary>
        private void DutyHour(Job j)
        {
            if (!_dutyOn || !IsPolice(j))
            {
                return;
            }
            DutyBank();
            if (CareersGame.MinuteOfDay >= (j.Def.Hour + j.Def.Hours) * 60 || Save.Day != _dutyDay)
            {
                DutyStop(DutyEnd.Handed);
            }
        }

        // ------------------------------------------------------------------------------------------------- the timer

        // From the one-second timer while the working life runs: the clock, the hours, the officer's health, the incidents.
        private void DutyTick()
        {
            if (!_dutyOn)
            {
                return;
            }
            Job j = Work != null ? Work.Job : null;
            if (!IsPolice(j) || !ReferenceEquals(_dutySave, Save))
            {
                DutyDown("the job or the save changed");
                return;
            }
            int now = CareersGame.MinuteOfDay;
            int moved = now - _dutyLast;
            if (PoliceRules.Jumped(moved) || Save.Day != _dutyDay)
            {
                DutyStop(DutyEnd.Skipped);   // what was not watched is not worked
                return;
            }
            _dutyLast = now;
            _dutyPending += PoliceRules.Counted(moved);
            if (now >= (j.Def.Hour + j.Def.Hours) * 60)
            {
                DutyStop(DutyEnd.Handed);
                return;
            }
            if (++_dutyLook >= PoliceRules.LookEverySeconds)
            {
                _dutyLook = 0;
                if (!CareersBridges.ConductOver(0).Fit)
                {
                    DutyStop(DutyEnd.Hurt);   // ill or hurt some other way: sent home
                    return;
                }
                if (DutyConduct(j, Save.Day))
                {
                    return;
                }
            }
            if (_dutyDesk != null && _dutyDesk.Watch())
            {
                return;
            }
            if ((_dutyDesk != null && _dutyDesk.IsOpen) || Time.time < _dutyQuiet || now < _caseBusyUntil)
            {
                return;   // a list is up, the officer closed one a moment ago, or is in court for the hour
            }
            if (CaseWitness(j, Save.Day))
            {
                return;
            }
            DutyCall call = CareersBridges.IncidentNow();
            if (!call.Present || DutyKept(call.Key))
            {
                return;
            }
            if (call.Metres > PoliceRules.ArriveMetres)
            {
                if (call.Metres <= PoliceRules.NoticeMetres && _dutyTold != call.Key)
                {
                    _dutyTold = call.Key;
                    Notifications.Show(call.Danger >= PoliceRules.Risky ? NotificationType.Warning : NotificationType.Info, "reallife_police_note_call",
                        Data("kind", CareersText.T(PoliceRules.KindKeys[(int)call.Kind]), "danger", CareersText.T(PoliceRules.DangerKeys[call.Danger]), "metres", call.Metres.ToString()), 9f);
                }
                return;
            }
            if (!CareersGame.Free())
            {
                return;   // in a car, or with the game's panel up: the list waits for the officer to get out
            }
            DutyOffer(j, call);
        }

        private bool DutyKept(int key)
        {
            for (int i = 0; i < Kept; i++)
            {
                if (_dutyKept[i] == key && key != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void DutyKeep(int key)
        {
            for (int i = Kept - 1; i > 0; i--)
            {
                _dutyKept[i] = _dutyKept[i - 1];
            }
            _dutyKept[0] = key;
        }

        // ------------------------------------------------------------------------------------------------- the list

        private void DutyOffer(Job j, DutyCall call)
        {
            int rank = j.Post.Rank;
            _dutyEntries.Clear();
            DutyMove[] moves = PoliceRules.Moves(call.Kind);
            for (int i = 0; i < moves.Length; i++)
            {
                DutyMove move = moves[i];
                PoliceRules.Odds(move, call.Danger, rank, DutyPoints(move), out int success, out int hurt, out _);
                string key = move == DutyMove.Backup ? "reallife_police_list_backup" : hurt > 0 ? "reallife_police_list_odds" : "reallife_police_list_odds_safe";
                _dutyEntries.Add(new DutyEntry
                {
                    Id = (int)move, Name = CareersText.T(PoliceRules.MoveKeys[(int)move]), Enabled = true,
                    Detail = CareersText.T(key, "chance", PoliceRules.Percent(success).ToString(), "risk", PoliceRules.Percent(hurt).ToString())
                });
            }
            _dutyEntries.Add(new DutyEntry
            {
                Id = (int)DutyMove.BackOff, Name = CareersText.T(PoliceRules.MoveKeys[(int)DutyMove.BackOff]), Enabled = true,
                Detail = CareersText.T("reallife_police_list_back_off", "points", (-PoliceRules.Change(DutyMove.BackOff, DutyResult.Failed, call.Danger)).ToString())
            });
            if (_dutyDesk == null)
            {
                _dutyDesk = new PoliceDesk();
            }
            string title = CareersText.T(PoliceRules.KindKeys[(int)call.Kind]);
            string info = CareersText.T("reallife_police_list_info", "danger", CareersText.T(PoliceRules.DangerKeys[call.Danger]), "rating", Duty.Rating.ToString());
            _dutyOpen = call;
            Action<int> answer = id => Guard.Step("answering an incident on duty", () => DutyAnswer(_dutyOpen, (DutyMove)id));
            Action closed = () => _dutyQuiet = Time.time + PoliceRules.PromptGapSeconds;
            if (!_dutyDesk.Open(title, info, _dutyEntries, answer, closed))
            {
                _dutyQuiet = Time.time + PoliceRules.PromptGapSeconds;
                Logger?.Warn("Careers: the game's list could not be copied for an incident on duty; it is left.");
                return;
            }
            Logger?.Info("Careers: an incident offered on duty: " + call.Kind + ", danger " + call.Danger + (call.Id != null ? " (" + call.Id + ")" : string.Empty) + ".");
        }

        private int DutyPoints(DutyMove move)
        {
            int skill = PoliceRules.SkillOf(move);
            return skill < 0 || _study == null ? 0 : StudyRules.Points(_study, skill);
        }

        // ------------------------------------------------------------------------------------------------- the answer

        private void DutyAnswer(DutyCall call, DutyMove move)
        {
            Job j = Work != null ? Work.Job : null;
            if (!_dutyOn || !IsPolice(j))
            {
                return;
            }
            DutyLoad();
            DutyFile f = _duty;
            int day = Save.Day, rank = j.Post.Rank, margin = 0;
            DutyResult result = DutyResult.Failed;
            if (move != DutyMove.BackOff)
            {
                int roll = PoliceRules.Roll(StudySeed, day, f.Answered, rank);
                result = PoliceRules.Result(move, call.Danger, rank, DutyPoints(move), roll);
                PoliceRules.Odds(move, call.Danger, rank, DutyPoints(move), out int chance, out _, out _);
                margin = chance - roll;   // how far under the chance the roll fell: the arrest's quality
            }
            f.Answered++;
            DutyKeep(call.Key);
            f.Rating = PoliceRules.Clamp(f.Rating + PoliceRules.Change(move, result, call.Danger));
            bool took = false;
            if (move != DutyMove.BackOff)
            {
                f.Today++;
                _dutyAnswered++;
                try
                {
                    took = CareersBridges.Settle != null && CareersBridges.Settle(call, move, result);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            Count(f, move, result);
            if (move == DutyMove.Arrest && result == DutyResult.Success && took && call.Source == DutySource.Crime)
            {
                CaseOpen(j, call, margin);   // the arrest's aftermath: a case in the officer's book
            }
            Logger?.Info("Careers: an incident answered on duty (" + call.Kind + ", danger " + call.Danger + "): " + move + " -> " + result + ", rating " + f.Rating
                         + (took ? ", the scene took it." : ", the scene was gone or left alone."));
            DutyTell(move, result, call, f);
            if (result == DutyResult.Hurt)
            {
                DutyHurt(call);
            }
            Changed();
        }

        private static void Count(DutyFile f, DutyMove move, DutyResult result)
        {
            if (move == DutyMove.BackOff)
            {
                f.BackedOff++;
                return;
            }
            if (result == DutyResult.Hurt)
            {
                f.Hurt++;
            }
            if (move == DutyMove.Backup)
            {
                f.Backups++;
            }
            if (result != DutyResult.Success)
            {
                return;
            }
            switch (move)
            {
                case DutyMove.Arrest: f.Arrests++; break;
                case DutyMove.TalkDown: f.Talked++; break;
                case DutyMove.FirstAid: f.Aid++; break;
                case DutyMove.Ticket: f.Tickets++; break;
            }
        }

        private void DutyTell(DutyMove move, DutyResult result, DutyCall call, DutyFile f)
        {
            string key;
            NotificationType type = NotificationType.Info;
            if (move == DutyMove.BackOff)
            {
                key = "reallife_police_note_back_off";
            }
            else if (result == DutyResult.Success)
            {
                type = NotificationType.Success;
                key = move == DutyMove.Arrest ? "reallife_police_note_done_arrest" : move == DutyMove.TalkDown ? "reallife_police_note_done_talk"
                    : move == DutyMove.Backup ? "reallife_police_note_done_backup" : move == DutyMove.FirstAid ? "reallife_police_note_done_aid" : "reallife_police_note_done_ticket";
            }
            else if (result == DutyResult.Hurt)
            {
                return;   // the injury's own notice follows (DutyHurt)
            }
            else
            {
                type = NotificationType.Warning;
                key = "reallife_police_note_failed";
            }
            Notifications.Show(type, key, Data("rating", f.Rating.ToString(), "points", Math.Abs(PoliceRules.Change(move, result, call.Danger)).ToString()), 8f);
            if (PoliceRules.Commended(move, result, call.Danger))
            {
                f.Commendations++;
                Notifications.Show(NotificationType.Success, "reallife_police_note_commend", null, 8f);
            }
        }

        // The injury goes the way every danger's does: Health names it and gives it a course as an accident at work, Permadeath judges it (a warning
        // first, and only a deadly incident can end a life), and the shift is over for the officer.
        private void DutyHurt(DutyCall call)
        {
            int hours = PoliceRules.HurtHours(DutyResult.Hurt, call.Danger);
            int fate = 0;
            try
            {
                fate = CareersBridges.Hurt != null ? CareersBridges.Hurt(hours, call.Danger) : 0;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (fate == 2)
            {
                DutyDown("a death on duty");   // the fade, the save and the obituary are Permadeath's
                return;
            }
            DutyStop(DutyEnd.Hurt);
        }

        // ------------------------------------------------------------------------------------------------- the front desk

        /// <summary>
        /// What a police station's front desk offers the player (Law's list, through Wiring/PoliceLinks): false for anyone who is no officer. <paramref name="on"/>: a
        /// patrol is under way and the entry ends it; <paramref name="why"/> the reason key it cannot begin now, else null; <paramref name="hours"/> the shift's hours.
        /// </summary>
        public bool DutyDesk(out bool on, out string why, out string hours)
        {
            on = _dutyOn;
            why = null;
            hours = null;
            Job j = Work != null ? Work.Job : null;
            if (!IsPolice(j))
            {
                return false;
            }
            hours = CareersText.T("reallife_desk_label_hours", "from", CareersText.Hour(j.Def.Hour), "to", CareersText.Hour((j.Def.Hour + j.Def.Hours) % 24));
            if (!on)
            {
                why = CanWork(out int _, out bool _);
            }
            return true;
        }

        /// <summary>The front desk's entry pressed: the patrol begins, or ends if it was on. Null if it did, else the reason key.</summary>
        public string DutyPress()
        {
            if (_dutyOn)
            {
                DutyStop(DutyEnd.Handed);
                return null;
            }
            return DutyBegin();
        }

        // ------------------------------------------------------------------------------------------------- the career

        /// <summary>
        /// The service lets the player go if their record or the courts say so on a day after they joined: a stay in a cell, a warrant or a conviction.
        /// The job ends as any sacking does (a poor reference, the notice said). True if it did.
        /// </summary>
        private bool DutyConduct(Job j, int day)
        {
            if (!IsPolice(j) || day < j.Hired || !PoliceRules.Dismissed(CareersBridges.ConductOver(Math.Max(0, day - j.Hired))))
            {
                return false;
            }
            if (_dutyOn)
            {
                DutyStop(DutyEnd.Dismissed);
            }
            Tell2(NotificationType.Error, "reallife_police_note_dismissed", Data("employer", CareersText.Employer(j.Post.Employer)), TabCv, null);
            Logger?.Info("Careers: the service let the officer go on day " + day + ": a stay in a cell, a warrant or a conviction since they joined on day " + j.Hired + ".");
            EndJob(LeaveReason.Misconduct, day, false);
            return true;
        }

        // What a post of police work says: where to report, what to do today, and why the Career app starts the shift itself.
        private string DutyLine(Job j)
        {
            return CareersText.Fill("reallife_police_desk_at", "employer", CareersText.Employer(j.Post.Employer), "address", WorkGame.Address(j.Post.Street, j.Post.Number));
        }

        /// <summary>The Career page's lines of the officer's file: the label's locale key and the value; nothing for anyone who is not an officer.</summary>
        public void DutyLines(List<KeyValuePair<string, string>> into)
        {
            Job j = Work != null ? Work.Job : null;
            DutyFile f = Duty;
            if (!IsPolice(j) || f == null)
            {
                return;
            }
            into.Add(new KeyValuePair<string, string>("reallife_police_file_arrests", f.Arrests.ToString()));
            into.Add(new KeyValuePair<string, string>("reallife_police_file_tickets", f.Tickets.ToString()));
            into.Add(new KeyValuePair<string, string>("reallife_police_file_commendations", f.Commendations.ToString()));
            into.Add(new KeyValuePair<string, string>("reallife_police_file_aid", f.Aid.ToString()));
            into.Add(new KeyValuePair<string, string>("reallife_police_file_hurt", f.Hurt.ToString()));
            into.Add(new KeyValuePair<string, string>("reallife_police_file_backed_off", f.BackedOff.ToString()));
            int day = Save != null ? Save.Day : 0;
            into.Add(new KeyValuePair<string, string>("reallife_police_file_rating", _dutyOn || f.Day == day ? f.Rating.ToString() : "-"));
        }

        /// <summary>The locale key of the Career page's start button: the working day's, or an officer's patrol begun or ended.</summary>
        public string StartButtonKey(Job j) => IsPolice(j) ? (_dutyOn ? "reallife_police_button_end" : "reallife_police_button_start") : "reallife_career_button_work";

        // ------------------------------------------------------------------------------------------------- the tests

        /// <summary>Test button: the player is hurt on the patrol they are on at a risky call, as a real one would hurt them (Health, and Permadeath's judgement of it).</summary>
        public void TestDutyHurt()
        {
            if (!WorkReady())
            {
                return;
            }
            try
            {
                if (!_dutyOn)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_police_test_hurt_off", null, 8f);
                    return;
                }
                DutyHurt(new DutyCall { Present = true, Source = DutySource.Crime, Kind = DutyKind.Fight, Danger = PoliceRules.Risky, Id = "test" });
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>Test button: puts the player on a patrol at once, wherever they stand, as the working day's own test starts a day.</summary>
        public void TestDuty()
        {
            if (!WorkReady())
            {
                return;
            }
            try
            {
                Job j = _study.Work.Job;
                if (!IsPolice(j))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_police_test_none", null, 8f);
                    return;
                }
                if (_dutyOn)
                {
                    DutyStop(DutyEnd.Handed);
                    return;
                }
                if (_book.Count > 0 || SittingNow || CareersBridges.GigOnNow() || !CareersGame.Free())
                {
                    Notifications.Show(NotificationType.Warning, "reallife_police_test_busy", null, 8f);
                    return;
                }
                int day = Save.Day;
                ClockIn(j, day, false);
                DutyStart(day);
                Logger?.Info("Careers test: a patrol begun at once on day " + day + "; it ends with the shift's hours or when the test is pressed again.");
                Notifications.Show(NotificationType.Success, "reallife_police_test_on", null, 8f);
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }
    }
}
