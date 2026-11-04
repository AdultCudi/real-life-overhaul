using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a press on the Career app does: the director method behind each (CareersDirector), carried by CareerLive.</summary>
    public enum CareerAct
    {
        None = 0, Apply = 1, ShowPost = 2, Register = 3, Interview = 4, Withdraw = 5, Accept = 6, AskSmall = 7, AskLarge = 8, Decline = 9, ShowDesk = 10,
        Resign = 11, WalkOut = 12, AskRise = 13, OpenCampus = 14, GoTab = 15, TaskYes = 16, TaskNo = 17, TaskUp = 18, TakeContract = 19, DropContract = 20,
        Retire = 21, ShowBoard = 22, Aiming = 23, SetFilter = 24
    }

    /// <summary>One rung of the profession's ladder at the player's employer.</summary>
    public sealed class CareerRung { public string Title, Pay; public int State; }   // State: 0 behind, 1 here, 2 ahead

    /// <summary>A requirement of a rung or a post, met or not, with what the player has against it.</summary>
    public sealed class CareerNeed { public string Words, Have; public bool Met; }

    /// <summary>A person beside the player: a colleague on the shift or another applicant.</summary>
    public sealed class CareerPeer { public string Name, Role, Tag, Day; public int Score, Team; public float Hue; }

    /// <summary>The job the player holds.</summary>
    public sealed class CareerJob
    {
        public string Title, Employer, Pay, Hours, Months, Status, Desk, Ladder, NextTitle, NextLine, NextNote;
        public UiRole StatusTone = UiRole.Ok;
        public int Progress = -1;
        public int Performance = -1, Punctuality = -1, Teamwork = -1, Reliability = -1;   // -1: not measured yet
        public string WeekHours, WeekPay;
        public bool Leaving;
        public readonly List<CareerRung> Rungs = new List<CareerRung>(8);
        public readonly List<CareerNeed> Needs = new List<CareerNeed>(6);
        public readonly List<CareerPeer> Peers = new List<CareerPeer>(4);
        public string RivalName;
        public int[] Mine = new int[0], Theirs = new int[0];   // the weekly score, oldest first, twelve at most
        public string NextRung;
    }

    /// <summary>A vacancy, an application, a profession, a task, a contract or a line of the CV: one row each, with the detail the chosen one shows.</summary>
    public sealed class CareerRow
    {
        public int Id;
        public UiIcon Icon = UiIcon.Briefcase;
        public string[] Cells;
        public string Status;
        public UiRole Tone = UiRole.Ok;
        public int Grade = -1;
        public string Detail;                              // a sentence about it, said under the lead when chosen
        public readonly List<CareerNeed> Needs = new List<CareerNeed>(5);
        public readonly List<CareerPeer> Others = new List<CareerPeer>(3);
        public readonly List<CareerAct> Acts = new List<CareerAct>(4);   // what the chosen row can do, the first the main one
        public readonly List<string> Why = new List<string>(4);          // a reason a listed act cannot be done now ("" for none), by act
        public bool Open, Qualify, Near;                    // for the filter chips
    }

    /// <summary>Everything the Career app shows, read from the careers' own records. Game-free.</summary>
    public sealed class CareerFacts
    {
        public bool Ready;
        public string Player = string.Empty, Day = string.Empty;
        public CareerJob Job;                                   // null: no job
        public string Laid;                                     // the sentence a lay-off or a retirement says on My job
        public readonly List<CareerRow> Jobs = new List<CareerRow>(40), Applications = new List<CareerRow>(24), Professions = new List<CareerRow>(40),
            Management = new List<CareerRow>(40), Own = new List<CareerRow>(16), Cv = new List<CareerRow>(32);
        public string ManagementLine, OwnLine, CvLine;
        public int OpenNow, Qualify, NearYou, ApplicationsWaiting;
        public bool CanRetire;
    }

    /// <summary>
    /// The Career app's pages as plain data: My job (the profile, the next shift, the ladder, the standing and the people on the shift),
    /// Jobs (the vacancies with what each needs and who else applied), Professions, Applications, Management, Own work and the CV. Each
    /// tab builds a page of its own from its own part of the facts. Game-free (the previews and the checks lay it out).
    /// </summary>
    public static class CareerPages
    {
        public const int Professions = 0, Jobs = 1, Applications = 2, MyJob = 3, Management = 4, Own = 5, Cv = 6, Tabs = 7;

        public static UiNavEntry[] Nav(CareerFacts f, UtilitiesWords w)
        {
            return new[]
            {
                new UiNavEntry { Icon = UiIcon.ListChecks, Label = w.T("reallife_work_nav_professions", "Professions"),
                    Group = w.T("reallife_work_group_find", "Find work") },
                new UiNavEntry { Icon = UiIcon.Search, Label = w.T("reallife_work_nav_jobs", "Jobs"), Count = f != null
                    && f.Jobs.Count > 0 ? f.Jobs.Count.ToString() : null },
                new UiNavEntry { Icon = UiIcon.ClipboardText, Label = w.T("reallife_work_nav_applications", "Applications"), Count = f != null
                    && f.ApplicationsWaiting > 0 ? f.ApplicationsWaiting.ToString() : null },
                new UiNavEntry { Icon = UiIcon.Clock, Label = w.T("reallife_work_nav_my_job", "My job"), Group = w.T("reallife_work_group_career",
                    "Your career") },
                new UiNavEntry { Icon = UiIcon.People, Label = w.T("reallife_work_nav_management", "Management") },
                new UiNavEntry { Icon = UiIcon.Wrench, Label = w.T("reallife_work_nav_own", "Own work") },
                new UiNavEntry { Icon = UiIcon.Person, Label = w.T("reallife_work_nav_cv", "CV") }
            };
        }

        public static WorkPage Build(CareerFacts f, WorkView v, UtilitiesWords w)
        {
            var b = new WorkBuilder(w);
            b.M.Title = w.T("reallife_career_app", "Career");
            if (!f.Ready)
            {
                b.About(UiIcon.Briefcase, null, w.T("reallife_work_nothing_here", "Nothing here yet"), w.T("reallife_career_off_body",
                    "Load a save with careers switched on to see your working life."));
                return b.Done();
            }
            switch (Math.Max(0, Math.Min(Tabs - 1, v.Tab)))
            {
                case MyJob: MyJobPage(b, f, v); break;
                case Jobs: JobsPage(b, f, v); break;
                case Applications: ListPage(b, f, v, f.Applications, "reallife_career_tab_applications", "Applications", "reallife_work_empty_applications",
                    "Nothing sent yet. Apply from Jobs and the employer answers in a day or two."); break;
                case Management: ListPage(b, f, v, f.Management, "reallife_career_tab_ladder", "Management", "reallife_work_empty_management",
                    f.ManagementLine ?? "The management ladder opens when you work at one of the city's companies."); break;
                case Own: ListPage(b, f, v, f.Own, "reallife_career_tab_own", "Own work", "reallife_work_empty_own",
                    f.OwnLine ?? "No contracts yet. Clients ask once you have a profession to offer."); break;
                case Cv: ListPage(b, f, v, f.Cv, "reallife_career_tab_cv", "CV", "reallife_work_empty_cv",
                    f.CvLine ?? "Your jobs, qualifications and references appear here as you earn them."); break;
                default: ListPage(b, f, v, f.Professions, "reallife_career_tab_start", "Professions", "reallife_work_empty_professions",
                    "No professions are open to you yet."); break;
            }
            return b.Done();
        }

        // ------------------------------------------------------------------------------------------------- My job

        private static void MyJobPage(WorkBuilder b, CareerFacts f, WorkView v)
        {
            UtilitiesWords w = b.W;
            CareerJob j = f.Job;
            if (j == null)
            {
                b.About(UiIcon.Briefcase, null, w.T("reallife_work_nothing_here", "Nothing here yet"), f.Laid ?? w.T("reallife_career_job_none",
                    "You have no job. Find one under Jobs, and apply; the employer weighs your record."));
                b.Button(w.T("reallife_career_button_vacancies", "See the jobs"), UiButtonKind.Primary, UiIcon.Search, UiAction.Alt,
                    WorkBuilder.Do(WorkKinds.GoTab, Jobs, null, true));
                return;
            }
            var p = new UiProfileModel
            {
                Name = f.Player, Hue = UiColour.HueOf(f.Player), Role = j.Title, RoleIcon = UiIcon.Briefcase, Employer = j.Employer,
                    EmployerIcon = UiIcon.Store,
                Status = j.Status, StatusTone = j.StatusTone,
                FactLabels = new[] { w.T("reallife_career_col_pay", "Pay"), w.T("reallife_work_col_hours", "Hours"), w.T("reallife_work_with_them",
                    "With them") },
                FactValues = new[] { j.Pay, j.Hours, j.Months }
            };
            b.M.Profile = p;
            b.M.Hero = new UiHeroModel { Icon = UiIcon.Clock, Tint = UiRole.None, Kicker = j.NextTitle, Title = j.NextLine, Line = j.Desk,
                Progress = j.Progress, Note = j.NextNote };
            var figures = new List<UiFigureModel>(3);
            if (j.WeekHours != null)
            {
                figures.Add(new UiFigureModel { Label = w.T("reallife_work_week_hours", "Hours this week"), Value = j.WeekHours });
            }
            if (j.WeekPay != null)
            {
                figures.Add(new UiFigureModel { Label = w.T("reallife_work_week_pay", "Pay this week"), Value = j.WeekPay });
            }
            b.M.Figures = figures.ToArray();
            var labels = new List<string>(4);
            var values = new List<int>(4);
            void Mark(string key, string english, int value)
            {
                if (value >= 0)
                {
                    labels.Add(w.T(key, english));
                    values.Add(value);
                }
            }
            Mark("reallife_work_stand_performance", "Performance", j.Performance);
            Mark("reallife_work_stand_punctuality", "Punctuality", j.Punctuality);
            Mark("reallife_work_stand_teamwork", "Teamwork", j.Teamwork);
            Mark("reallife_work_stand_reliability", "Reliability", j.Reliability);
            b.M.AttributesTitle = w.T("reallife_work_standing", "Standing");
            b.M.AttributeLabels = labels.ToArray();
            b.M.AttributeValues = values.ToArray();
            var rows = new UiAppRow[j.Rungs.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                CareerRung r = j.Rungs[i];
                rows[i] = WorkBuilder.Row(r.State == 1 ? UiIcon.Star : UiIcon.ListChecks, UiRole.None, new[] { r.Title, r.Pay },
                    r.State == 1 ? w.T("reallife_work_rung_you", "You") : r.State == 2 && i == FirstAhead(j) ? w.T("reallife_work_rung_next",
                        "Next") : null, UiRole.Accent);
            }
            b.Table(w.F("reallife_work_ladder_at", "The ladder at {employer}", "employer", j.Employer),
                new[] { WorkBuilder.Col(w.T("reallife_work_col_rung", "Rung"), 0f, false, true), WorkBuilder.Col(w.T("reallife_career_col_pay",
                    "Pay"), 160f, true) }, -1, rows, null, -1);
            if (j.Peers.Count > 0)
            {
                var people = new UiPersonRow[j.Peers.Count];
                for (int i = 0; i < people.Length; i++)
                {
                    CareerPeer c = j.Peers[i];
                    people[i] = new UiPersonRow { Name = c.Name, Hue = c.Hue, RoleIcon = UiIcon.Person, Role = c.Tag != null ? c.Role + ", " + c.Tag : c.Role,
                        FirstLabel = w.T("reallife_work_stand_performance", "Performance"), First = c.Score,
                        SecondLabel = w.T("reallife_work_stand_teamwork", "Teamwork"), Second = c.Team };
                }
                b.People(w.T("reallife_work_on_shift", "On shift with you"), j.Peers.Count.ToString(), people, null);
                if (j.RivalName != null && j.Mine.Length >= 2)
                {
                    b.M.Note = w.F("reallife_work_rival_note", "{name} is also up for the next rung. Your twelve weeks: {mine}. Theirs: {theirs}.",
                        "name", j.RivalName,
                        "mine", Last(j.Mine), "theirs", Last(j.Theirs));
                    b.M.NoteTone = UiRole.Accent;
                }
            }
            b.Button(w.T("reallife_career_button_map", "Show the desk"), UiButtonKind.Secondary, UiIcon.Locate, UiAction.Option,
                WorkBuilder.Do((int)CareerAct.ShowDesk, 0, null, true));
            if (!j.Leaving)
            {
                b.Button(w.T("reallife_career_button_resign", "Resign"), UiButtonKind.Destructive, UiIcon.SignOut, UiAction.Count,
                    WorkBuilder.Do((int)CareerAct.Resign));
            }
            b.Button(w.T("reallife_life_button_rise", "Ask for a rise"), UiButtonKind.Secondary, UiIcon.TrendUp, UiAction.Count,
                WorkBuilder.Do((int)CareerAct.AskRise));
        }

        private static int FirstAhead(CareerJob j)
        {
            for (int i = 0; i < j.Rungs.Count; i++)
            {
                if (j.Rungs[i].State == 2)
                {
                    return i;
                }
            }
            return -1;
        }

        private static string Last(int[] ring) => ring.Length == 0 ? "-" : string.Join(", ", Array.ConvertAll(ring, x => x.ToString()));

        // ------------------------------------------------------------------------------------------------- Jobs

        private static void JobsPage(WorkBuilder b, CareerFacts f, WorkView v)
        {
            UtilitiesWords w = b.W;
            string[] filters =
            {
                w.F("reallife_work_filter_all", "All {n}", "n", f.Jobs.Count.ToString()), w.F("reallife_work_filter_open", "Open now {n}", "n",
                    f.OpenNow.ToString()),
                w.F("reallife_work_filter_qualify", "You qualify {n}", "n", f.Qualify.ToString()), w.F("reallife_work_filter_near", "Near you {n}",
                    "n", f.NearYou.ToString())
            };
            var shown = new List<CareerRow>(f.Jobs.Count);
            for (int i = 0; i < f.Jobs.Count; i++)
            {
                CareerRow r = f.Jobs[i];
                if (v.Filter == 0 || (v.Filter == 1 && r.Open) || (v.Filter == 2 && r.Qualify) || (v.Filter == 3 && r.Near))
                {
                    shown.Add(r);
                }
            }
            int at = v.Row >= 0 && v.Row < shown.Count ? v.Row : shown.Count > 0 ? 0 : -1;
            CareerRow chosen = at >= 0 ? shown[at] : null;
            if (chosen != null)
            {
                string needs = chosen.Needs.Count == 0 ? null : w.F("reallife_work_needs_met", "{met} of {all} met", "met",
                    Met(chosen.Needs).ToString(), "all", chosen.Needs.Count.ToString());
                var sb = new System.Text.StringBuilder(chosen.Cells[0]);
                sb.Append(". ").Append(chosen.Detail);
                if (needs != null)
                {
                    sb.Append(' ').Append(needs).Append(": ");
                    for (int i = 0; i < chosen.Needs.Count; i++)
                    {
                        sb.Append(i > 0 ? ", " : string.Empty).Append(chosen.Needs[i].Met ? string.Empty : w.T("reallife_work_not_yet",
                            "not yet:") + " ").Append(chosen.Needs[i].Words);
                        if (!chosen.Needs[i].Met && chosen.Needs[i].Have != null)
                        {
                            sb.Append(" (").Append(chosen.Needs[i].Have).Append(')');
                        }
                    }
                    sb.Append('.');
                }
                b.About(chosen.Icon, chosen.Status, chosen.Cells[0], sb.ToString().Substring(chosen.Cells[0].Length > 0 ? Math.Min(sb.Length,
                    chosen.Cells[0].Length + 2) : 0));
                AddActs(b, chosen);
                if (chosen.Others.Count > 0)
                {
                    var people = new UiPersonRow[chosen.Others.Count];
                    for (int i = 0; i < people.Length; i++)
                    {
                        CareerPeer c = chosen.Others[i];
                        people[i] = new UiPersonRow { Name = c.Name, Hue = c.Hue, RoleIcon = UiIcon.Person, Role = c.Role + ", " + c.Day,
                            FirstLabel = w.T("reallife_work_record", "Record"), First = c.Score,
                            SecondLabel = w.T("reallife_work_stand_teamwork", "Teamwork"), Second = c.Team };
                    }
                    b.People(w.T("reallife_work_also_applied", "Also applied"), w.F("reallife_work_n_people", "{n} people", "n",
                        chosen.Others.Count.ToString()), people, null);
                }
            }
            for (int i = 0; i < filters.Length; i++)
            {
                b.Chip(filters[i], v.Filter == i, WorkBuilder.Do(WorkKinds.Filter, i, null, true));
            }
            b.Table(null, JobColumns(w), -1, RowsOf(shown), OpensOf(shown, null), at);
            if (shown.Count == 0)
            {
                b.About(UiIcon.Search, null, w.T("reallife_work_nothing_here", "Nothing here yet"), w.T("reallife_work_empty_jobs",
                    "No post matches this filter. Another filter, or tomorrow's board, may show one."));
            }
        }

        private static UiColumn[] JobColumns(UtilitiesWords w) => new[]
        {
            WorkBuilder.Col(w.T("reallife_work_col_job", "Job"), 0f, false, true), WorkBuilder.Col(w.T("reallife_work_col_where", "Where"), 184f),
            WorkBuilder.Col(w.T("reallife_work_col_pay_hour", "Pay an hour"), 140f, true),
            WorkBuilder.Col(w.T("reallife_work_col_against", "Against yours"), 140f, true)
        };

        private static int Met(List<CareerNeed> needs)
        {
            int n = 0;
            for (int i = 0; i < needs.Count; i++)
            {
                n += needs[i].Met ? 1 : 0;
            }
            return n;
        }

        // ------------------------------------------------------------------------------------------------- the other tabs: a list and what the chosen row can do

        private static void ListPage(WorkBuilder b, CareerFacts f, WorkView v, List<CareerRow> list, string titleKey, string title, string emptyKey,
            string empty)
        {
            UtilitiesWords w = b.W;
            int at = v.Row >= 0 && v.Row < list.Count ? v.Row : list.Count > 0 ? 0 : -1;
            CareerRow chosen = at >= 0 ? list[at] : null;
            if (chosen != null)
            {
                b.About(chosen.Icon, chosen.Status, chosen.Cells[0], chosen.Detail);
                AddActs(b, chosen);
            }
            else
            {
                b.About(UiIcon.Search, null, w.T("reallife_work_nothing_here", "Nothing here yet"), w.T(emptyKey, empty));
            }
            if (list.Count > 0)
            {
                b.Table(null, ColumnsFor(w, v.Tab, list), -1, RowsOf(list), OpensOf(list, null), at);
            }
        }

        private static UiColumn[] ColumnsFor(UtilitiesWords w, int tab, List<CareerRow> list)
        {
            int n = list.Count > 0 ? list[0].Cells.Length : 2;
            string[] names, english;
            switch (tab)
            {
                case Applications: names = new[] { "reallife_work_col_job", "reallife_work_col_firm", "reallife_work_col_sent",
                    "reallife_work_col_answer" }; english = new[] { "Job", "Firm", "Sent", "Answer by" }; break;
                case Management: names = new[] { "reallife_work_col_matter", "reallife_work_col_place", "reallife_work_col_who",
                    "reallife_work_col_asked" }; english = new[] { "Matter", "Place", "Who", "Asked" }; break;
                case Own: names = new[] { "reallife_work_col_client", "reallife_work_col_trade", "reallife_work_col_fee",
                    "reallife_work_col_due" }; english = new[] { "Client", "Trade", "Fee", "Due" }; break;
                case Cv: names = new[] { "reallife_work_col_post_held", "reallife_work_col_firm", "reallife_work_col_from",
                    "reallife_work_col_to" }; english = new[] { "Post", "Firm", "From", "To" }; break;
                default: names = new[] { "reallife_work_col_trade", "reallife_work_col_first_post", "reallife_work_col_pay_from",
                    "reallife_work_col_open" }; english = new[] { "Trade", "First post", "Pay from", "Open" }; break;
            }
            var cols = new UiColumn[Math.Min(n, names.Length)];
            for (int i = 0; i < cols.Length; i++)
            {
                cols[i] = WorkBuilder.Col(w.T(names[i], english[Math.Min(i, english.Length - 1)]), i == 0 ? 0f : 184f, i == cols.Length - 1 && i > 1, i == 0);
            }
            return cols;
        }

        private static UiAppRow[] RowsOf(List<CareerRow> list)
        {
            var rows = new UiAppRow[list.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                CareerRow r = list[i];
                rows[i] = WorkBuilder.Row(r.Icon, UiRole.None, r.Cells, r.Status, r.Tone, r.Grade);
            }
            return rows;
        }

        private static WorkDo[] OpensOf(List<CareerRow> list, string unused)
        {
            return new WorkDo[list.Count];   // a row only chooses what the detail shows; its actions are the buttons
        }

        private static void AddActs(WorkBuilder b, CareerRow r)
        {
            UtilitiesWords w = b.W;
            for (int i = 0; i < r.Acts.Count; i++)
            {
                CareerAct a = r.Acts[i];
                string why = i < r.Why.Count && !string.IsNullOrEmpty(r.Why[i]) ? r.Why[i] : null;
                bool main = i == 0;
                b.Button(LabelOf(w, a), main ? UiButtonKind.Primary : a == CareerAct.Withdraw || a == CareerAct.Decline || a == CareerAct.TaskNo
                    || a == CareerAct.DropContract ? UiButtonKind.Destructive : UiButtonKind.Secondary,
                    IconOf(a), main ? UiAction.Alt : i == 1 ? UiAction.Option : UiAction.Count, WorkBuilder.Do((int)a, r.Id, why,
                        a == CareerAct.ShowPost || a == CareerAct.ShowBoard || a == CareerAct.OpenCampus));
            }
        }

        public static string LabelOf(UtilitiesWords w, CareerAct a)
        {
            switch (a)
            {
                case CareerAct.Apply: return w.T("reallife_career_button_apply", "Apply");
                case CareerAct.ShowPost:
                case CareerAct.ShowBoard: return w.T("reallife_career_button_map", "Show on map");
                case CareerAct.Register: return w.T("reallife_career_button_register", "Register at the agency");
                case CareerAct.Interview: return w.T("reallife_career_button_interview", "Go to the interview");
                case CareerAct.Withdraw: return w.T("reallife_career_button_withdraw", "Withdraw");
                case CareerAct.Accept: return w.T("reallife_career_button_accept", "Accept");
                case CareerAct.AskSmall: return w.T("reallife_career_button_ask_small", "Ask for a little more");
                case CareerAct.AskLarge: return w.T("reallife_career_button_ask_large", "Ask for a lot more");
                case CareerAct.Decline: return w.T("reallife_career_button_decline", "Decline");
                case CareerAct.OpenCampus: return w.T("reallife_career_button_campus", "See the courses");
                case CareerAct.TaskYes: return w.T("reallife_ladder_button_yes", "Agree");
                case CareerAct.TaskNo: return w.T("reallife_ladder_button_no", "Refuse");
                case CareerAct.TaskUp: return w.T("reallife_ladder_button_up", "Pass it up");
                case CareerAct.TakeContract: return w.T("reallife_own_button_take", "Take the contract");
                case CareerAct.DropContract: return w.T("reallife_own_button_drop", "Drop it");
                case CareerAct.Retire: return w.T("reallife_life_button_retire", "Retire");
                case CareerAct.Aiming: return w.T("reallife_ladder_aiming", "Put your name forward");
                default: return w.T("reallife_work_button_open", "Open");
            }
        }

        private static UiIcon IconOf(CareerAct a)
        {
            switch (a)
            {
                case CareerAct.ShowPost:
                case CareerAct.ShowBoard:
                case CareerAct.ShowDesk: return UiIcon.Locate;
                case CareerAct.Withdraw:
                case CareerAct.Decline:
                case CareerAct.TaskNo:
                case CareerAct.DropContract: return UiIcon.Close;
                case CareerAct.OpenCampus: return UiIcon.Books;
                case CareerAct.Retire: return UiIcon.SignOut;
                default: return UiIcon.Check;
            }
        }
    }
}
