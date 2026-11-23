using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The aftermath of an arrest: its case in the officer's case book, a detective's work on open cases from the station's front desk, the officer's evidence at a
    /// hearing, and the day's one notice of case news. The rules and every figure are CaseRules (game-free); the timings, the charges and the words are Justice's own.
    /// Cost: nothing per frame; the day's turn walks the book (forty cases at most), and the one-second look adds one comparison on a hearing day.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private int _caseBusyUntil = -1;   // the minute of the day the officer is in court until: no incident is looked for before it
        private int _caseOffered = -1;     // the case whose evidence was last offered, so a list closed unanswered is offered again only after the gap

        // ------------------------------------------------------------------------------------------------- an arrest opens a case

        private void CaseOpen(Job j, DutyCall call, int margin)
        {
            DutyFile f = Duty;
            int day = Save.Day, rank = j.Post.Rank;
            int quality = CaseRules.Quality(margin, rank, DutyPoints(DutyMove.Arrest));
            PoolPerson who = PeoplePool.For(unchecked(StudySeed * 131L + f.NextCase * 7919L + day));
            string district = CareersBridges.DistrictNow();
            OfficerCase c = CaseRules.Open(f, who.Name, who.Id, CaseRules.Offence(call.Id), district, day, quality, StudySeed);
            Logger?.Info("Careers: an arrest opened case " + c.Id + " (" + c.Offence + ", quality " + quality + "): " + (c.Open ? "charged, heard on day " + c.Hearing : "released") + ".");
        }

        // ------------------------------------------------------------------------------------------------- the day's turn

        /// <summary>The day's turn: hearings behind are decided once, and one notice sums the day's case news. A fault stays here.</summary>
        private void CaseDay(int day)
        {
            DutyFile f = Duty;
            if (f == null || (f.Cases.Count == 0 && f.PendOpened == 0))
            {
                return;
            }
            CaseRules.Advance(f, day, StudySeed);
            if (!CaseRules.NoticeDue(f, day))
            {
                return;
            }
            CaseRules.TakeNotice(f, day, out int opened, out int released, out int convicted, out int acquitted, out int commended);
            Dictionary<string, string> news = Data("opened", opened.ToString(), "released", released.ToString(), "convicted", convicted.ToString());
            news["acquitted"] = acquitted.ToString();
            news["commended"] = commended.ToString();
            Tell2(NotificationType.Info, "reallife_police_note_cases", news, TabJob, null);
        }

        // ------------------------------------------------------------------------------------------------- a detective's work

        /// <summary>The front desk's second entry: whether it is offered (a detective or higher) and can be used now, with its words' reason key.</summary>
        public bool CaseDesk(out bool can, out string why)
        {
            can = false;
            why = null;
            Job j = Work != null ? Work.Job : null;
            if (!IsPolice(j) || j.Post.Rank < CaseRules.DetectiveRank)
            {
                return false;
            }
            DutyFile f = Duty;
            if (!_dutyOn)
            {
                why = "reallife_police_why_case_off_duty";
            }
            else if (f.ActionsToday >= CaseRules.ActionsMost)
            {
                why = "reallife_police_why_case_actions";
            }
            else if (CaseRules.OpenCount(f) == 0)
            {
                why = "reallife_police_why_case_none";
            }
            can = why == null;
            return true;
        }

        /// <summary>The front desk's case entry pressed: its list of open cases opens. Null if it did, else the reason key.</summary>
        public string CasePress()
        {
            CaseDesk(out bool can, out string why);
            if (!can)
            {
                return why ?? "reallife_police_why_case_rank";
            }
            return CaseMenu() ? null : "reallife_study_why_game_busy";
        }

        // Step one: the cases that wait, each with its offence, name and strength; a pick goes on to the three actions on that case.
        private bool CaseMenu()
        {
            DutyFile f = Duty;
            int day = Save.Day, rank = Work.Job.Post.Rank;
            _dutyEntries.Clear();
            for (int i = 0; i < f.Cases.Count && _dutyEntries.Count < 5; i++)
            {
                OfficerCase c = f.Cases[i];
                if (!c.Open || c.Hearing < day)
                {
                    continue;
                }
                _dutyEntries.Add(new DutyEntry
                {
                    Id = c.Id, Name = CaseTitle(c), Enabled = c.WorkedDay != day,
                    Detail = CareersText.T("reallife_police_list_case", "strength", c.Strength.ToString(), "day", CareersText.Day(c.Hearing))
                });
            }
            if (_dutyEntries.Count == 0)
            {
                return false;
            }
            if (_dutyDesk == null)
            {
                _dutyDesk = new PoliceDesk();
            }
            return _dutyDesk.Open(CareersText.T("reallife_police_list_cases_title"), CareersText.T("reallife_police_list_cases_info", "left", (CaseRules.ActionsMost - f.ActionsToday).ToString()),
                _dutyEntries, id => Guard.Step("choosing a case", () => CaseActions(id, rank)), null);
        }

        // Step two: the three actions on one case, each with what it would add; a pick does it.
        private void CaseActions(int caseId, int rank)
        {
            DutyFile f = Duty;
            OfficerCase c = CaseOf(f, caseId);
            int day = Save.Day;
            if (c == null)
            {
                return;
            }
            _dutyEntries.Clear();
            for (int a = 1; a <= 3; a++)
            {
                var action = (CaseAction)a;
                string why = CaseRules.ActionWhy(c, action, day, f.ActionsToday, rank);
                int gain = CaseRules.Gain(StudyRules.Points(_study, CaseRules.SkillOf(action)), rank);
                _dutyEntries.Add(new DutyEntry
                {
                    Id = caseId * 10 + a, Name = CareersText.T(CaseActionKeys[a - 1]), Enabled = why == null,
                    Detail = why != null ? CareersText.T(why) : CareersText.T("reallife_police_list_action", "gain", gain.ToString(), "strength", c.Strength.ToString())
                });
            }
            _dutyDesk?.Open(CaseTitle(c), CareersText.T("reallife_police_list_actions_info", "left", (CaseRules.ActionsMost - f.ActionsToday).ToString()), _dutyEntries,
                id => Guard.Step("working a case", () => CaseDo(id)), null);
        }

        private static readonly string[] CaseActionKeys = { "reallife_police_action_witness", "reallife_police_action_scene", "reallife_police_action_write" };

        private void CaseDo(int id)
        {
            Job j = Work != null ? Work.Job : null;
            DutyFile f = Duty;
            OfficerCase c = CaseOf(f, id / 10);
            var action = (CaseAction)(id % 10);
            if (!_dutyOn || !IsPolice(j) || c == null || CaseRules.ActionWhy(c, action, Save.Day, f.ActionsToday, j.Post.Rank) != null)
            {
                return;
            }
            int before = c.Strength;
            CaseRules.Work(c, action, Save.Day, StudyRules.Points(_study, CaseRules.SkillOf(action)), j.Post.Rank, f);
            Notifications.Show(NotificationType.Success, "reallife_police_note_case_worked", Data("name", c.Name, "strength", c.Strength.ToString(), "gain", (c.Strength - before).ToString()), 8f);
            Logger?.Info("Careers: case " + c.Id + " worked (" + action + "): strength " + before + " to " + c.Strength + ".");
            Changed();
        }

        private static OfficerCase CaseOf(DutyFile f, int id)
        {
            for (int i = 0; f != null && i < f.Cases.Count; i++)
            {
                if (f.Cases[i].Id == id)
                {
                    return f.Cases[i];
                }
            }
            return null;
        }

        private static string CaseTitle(OfficerCase c)
        {
            ChargeDef def = JusticeLaw.Find(c.Offence);
            return CareersText.T("reallife_police_case_title", "name", c.Name, "offence", def != null ? CareersText.T(def.NameKey) : c.Offence);
        }

        // ------------------------------------------------------------------------------------------------- the officer's evidence

        // From the one-second look: a detective on a shift whose case is heard today is called to court once, and chooses how to give evidence.
        private bool CaseWitness(Job j, int day)
        {
            if (j.Post.Rank < CaseRules.DetectiveRank || (_dutyDesk != null && _dutyDesk.IsOpen))
            {
                return false;
            }
            DutyFile f = Duty;
            for (int i = 0; i < f.Cases.Count; i++)
            {
                OfficerCase c = f.Cases[i];
                if (!c.Open || c.Hearing != day || c.Summoned)
                {
                    continue;
                }
                if (_caseOffered == c.Id && Time.time < _dutyQuiet)
                {
                    return false;
                }
                _caseOffered = c.Id;
                bool written = (c.Done & (1 << (int)CaseAction.Write)) != 0;
                _dutyEntries.Clear();
                for (int k = 1; k <= 3; k++)
                {
                    var choice = (WitnessChoice)k;
                    _dutyEntries.Add(new DutyEntry
                    {
                        Id = c.Id * 10 + k, Name = CareersText.T(CaseWitnessKeys[k - 1]), Enabled = true,
                        Detail = CareersText.T("reallife_police_list_witness", "value", CaseRules.WitnessValue(choice, written).ToString("+0;-0;0"))
                    });
                }
                if (_dutyDesk == null)
                {
                    _dutyDesk = new PoliceDesk();
                }
                Action closed = () => _dutyQuiet = Time.time + PoliceRules.PromptGapSeconds;
                if (_dutyDesk.Open(CareersText.T("reallife_police_list_witness_title"), CareersText.T("reallife_police_list_witness_info", "case", CaseTitle(c),
                        "hour", (CaseRules.WitnessHourMinutes / 60).ToString()), _dutyEntries, id => Guard.Step("giving evidence", () => CaseGive(id)), closed))
                {
                    return true;
                }
            }
            return false;
        }

        private static readonly string[] CaseWitnessKeys = { "reallife_police_witness_plain", "reallife_police_witness_prepared", "reallife_police_witness_hold" };

        private void CaseGive(int id)
        {
            DutyFile f = Duty;
            OfficerCase c = CaseOf(f, id / 10);
            if (c == null || !c.Open || c.Summoned)
            {
                return;
            }
            bool written = (c.Done & (1 << (int)CaseAction.Write)) != 0;
            c.Witness = CaseRules.WitnessValue((WitnessChoice)(id % 10), written);
            c.Summoned = true;
            _caseBusyUntil = Math.Min(24 * 60, CareersGame.MinuteOfDay + CaseRules.WitnessHourMinutes);   // an hour of the shift in court: no call comes to them
            Notifications.Show(NotificationType.Info, "reallife_police_note_witness", Data("name", c.Name, "value", c.Witness.ToString("+0;-0;0")), 8f);
            Logger?.Info("Careers: evidence given at the hearing of case " + c.Id + ": " + c.Witness + " to the odds.");
            Changed();
        }

        // ------------------------------------------------------------------------------------------------- the Career page

        /// <summary>The Career page's lines of the open cases, for a detective or higher: the count and each of the first three, as final words.</summary>
        public void DutyCaseLines(List<KeyValuePair<string, string>> into)
        {
            Job j = Work != null ? Work.Job : null;
            DutyFile f = Duty;
            if (!IsPolice(j) || f == null || j.Post.Rank < CaseRules.DetectiveRank)
            {
                return;
            }
            int shown = 0;
            for (int i = 0; i < f.Cases.Count && shown < 3; i++)
            {
                OfficerCase c = f.Cases[i];
                if (c.Open)
                {
                    shown++;
                    into.Add(new KeyValuePair<string, string>(CaseTitle(c), CareersText.T("reallife_police_line_case", "strength", c.Strength.ToString(), "day", CareersText.Day(c.Hearing))));
                }
            }
            int open = CaseRules.OpenCount(f);
            into.Insert(0, new KeyValuePair<string, string>(CareersText.T("reallife_police_file_cases"), open.ToString()));
        }
    }
}
