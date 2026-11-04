using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a press on the Career app does: the director method behind each (CareersDirector), carried by CareerLive. The numbers start at 100 so
    /// none is ever taken for the screen's own (<see cref="WorkKinds"/>, 1 to 3): the check that walks every action of every page holds it.
    /// </summary>
    public enum CareerAct
    {
        None = 0, Apply = 100, ShowPost = 101, Interview = 102, Withdraw = 103, Accept = 104, AskSmall = 105, AskLarge = 106, Decline = 107, ShowDesk = 108,
        Resign = 109, WalkOut = 110, AskRise = 111, OpenCampus = 112, TaskYes = 113, TaskNo = 114, TaskUp = 115, TakeContract = 116, DropContract = 117,
        Retire = 118, Aiming = 119, Declare = 120, AnswerA = 121, AnswerB = 122, PolicyMore = 123, PolicyLess = 124, RotaLean = 125, RotaStandard = 126, RotaFull = 127,
        AskShiftEarlier = 128, AskShiftLater = 129, AskEquipment = 130, AskInsurance = 131, Raise = 132, PracticeOpen = 133, PracticeClose = 134, Found = 135, FoundWith = 136,
        PracticeLeaveAct = 137, BringColleagues = 138
    }

    /// <summary>One rung of the profession's ladder at the player's employer.</summary>
    public sealed class CareerRung { public string Title, Pay; public int State; }   // State: 0 behind, 1 here, 2 ahead

    /// <summary>A requirement of a rung or a post, met or not, with what the player has against it.</summary>
    public sealed class CareerNeed { public string Words, Have; public bool Met; }

    /// <summary>A person beside the player: a colleague on the shift or another applicant.</summary>
    public sealed class CareerPeer { public string Name, Role, Tag, Day; public int Score, Team; public float Hue; }

    /// <summary>One worked shift of the recent shifts: the day, the hours, the score that day and the pay.</summary>
    public sealed class CareerShift { public string Day, Hours, Pay; public int Score; }

    /// <summary>The job the player holds.</summary>
    public sealed class CareerJob
    {
        public string Title, Employer, Pay, Hours, Months, Status, Desk, Ladder, NextTitle, NextLine, NextNote;
        public UiRole StatusTone = UiRole.Ok;
        public int Progress = -1;
        public int Performance = -1, Punctuality = -1, Teamwork = -1, Reliability = -1;   // -1: not measured yet
        public string WeekHours, WeekPay, PaidNote;
        public bool Leaving;
        public readonly List<CareerRung> Rungs = new List<CareerRung>(8);
        public readonly List<CareerNeed> Needs = new List<CareerNeed>(6);
        public readonly List<CareerPeer> Peers = new List<CareerPeer>(4);
        public string RivalName;
        public int[] Mine = new int[0], Theirs = new int[0];   // the weekly score, oldest first, twelve at most
        public string NextRung;
        public string MatterText, MatterA, MatterB;          // an office matter waiting, and its two answers
        public bool CanDeclare, CanAsk = true;
        public string AskWhy;
        public readonly List<KeyValuePair<string, string>> Life = new List<KeyValuePair<string, string>>(6);   // the working life: next review, boss, overtime, pension
        public int Burnout = -1;
        public readonly List<CareerShift> Shifts = new List<CareerShift>(7);
        public bool SafetyKept;                              // a safety figure is kept: false says the page does not track it
    }

    /// <summary>Everything the Career app shows, read from the careers' own records. Game-free.</summary>
    public sealed class CareerFacts
    {
        public bool Ready;
        public string Player = string.Empty, Day = string.Empty;
        public CareerJob Job;                                   // null: no job
        public string Laid;                                     // the sentence a lay-off or a retirement says on My job
        public readonly List<WorkRow> Best = new List<WorkRow>(5);   // the vacancies a player with no job could go for first
        public WorkTab Professions = new WorkTab(), Jobs = new WorkTab(), Applications = new WorkTab(), Management = new WorkTab(), Own = new WorkTab(), Cv = new WorkTab();
        public int ApplicationsWaiting;
        public bool CanRetire;
    }

    /// <summary>
    /// The Career app's pages as plain data: My job (the profile, the next shift, the ladder, the standing and the people on the shift), Jobs (the
    /// vacancies with what each needs and who else applied), Professions, Applications, Management, Own work and the CV. Each tab builds a page of
    /// its own from its own part of the facts. Game-free (the previews and the checks lay it out).
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
                    && f.Jobs.Rows.Count > 0 ? f.Jobs.Rows.Count.ToString() : null },
                new UiNavEntry { Icon = UiIcon.ClipboardText, Label = w.T("reallife_work_nav_applications", "Applications"), Count = f != null
                    && f.Applications.Rows.Count > 0 ? f.Applications.Rows.Count.ToString() : null },
                new UiNavEntry { Icon = UiIcon.Clock, Label = w.T("reallife_work_nav_my_job", "My job"), Group = w.T("reallife_work_group_career",
                    "Your career") },
                new UiNavEntry { Icon = UiIcon.People, Label = w.T("reallife_work_nav_management", "Management") },
                new UiNavEntry { Icon = UiIcon.Wrench, Label = w.T("reallife_work_nav_own", "Own work") },
                new UiNavEntry { Icon = UiIcon.Person, Label = w.T("reallife_work_nav_cv", "CV") }
            };
        }

        public static WorkPage Build(CareerFacts f, WorkView v, UtilitiesWords w)
        {
            if (!f.Ready)
            {
                var b = new WorkBuilder(w);
                string title = w.T("reallife_career_off_title", "Career is closed");
                string body = w.T("reallife_career_off_body", "Professional jobs are switched off on the options page, or the save is still loading.");
                b.Page.Body = (s, at, x, y, cw, bottom, keys) => UiDraw.EmptyState(s, at, x, y, cw, bottom - y, UiIcon.Briefcase, title, body, null, -1, keys);
                return b.Done();
            }
            int tab = Math.Max(0, Math.Min(Tabs - 1, v.Tab));
            if (tab == MyJob)
            {
                return f.Job == null ? NewPlayer(f, w) : MyJobPage(f, w);
            }
            var app = new WorkApp { Ready = true, Nav = Nav(f, w), Tabs = new WorkTab[Tabs] };
            app.Tabs[Professions] = f.Professions;
            app.Tabs[Jobs] = f.Jobs;
            app.Tabs[Applications] = f.Applications;
            app.Tabs[MyJob] = new WorkTab();
            app.Tabs[Management] = f.Management;
            app.Tabs[Own] = f.Own;
            app.Tabs[Cv] = f.Cv;
            return WorkTabPages.Build(app, v, w);
        }

        // ------------------------------------------------------------------------------------------------- the list tabs' shells

        private static UiDataColumn Col(string title, UiCellKind kind, float width = 0f, bool graded = false) => WorkBuilder.Col(title, kind, width, graded);

        /// <summary>The Jobs tab as a squad page: its chips, its search, its columns and what an empty board says. The source fills its rows.</summary>
        public static WorkTab JobsShell(UtilitiesWords w)
        {
            var t = new WorkTab { Shape = WorkShape.Table, Search = w.T("reallife_work_search_jobs", "Search jobs"), ChipsByMask = true, AllWord = w.T("reallife_work_filter_all_word", "All"),
                Chips = new[] { w.T("reallife_work_filter_open_word", "Open now"), w.T("reallife_work_filter_qualify_word", "You qualify"), w.T("reallife_work_filter_near_word", "Near you") },
                EmptyIcon = UiIcon.Briefcase, EmptyTitle = w.T("reallife_work_empty_jobs_title", "No post is open today"),
                EmptyBody = w.T("reallife_work_empty_jobs", "Employers post their vacancies each morning. The professions show what to train for in the meantime."),
                EmptyActLabel = w.T("reallife_career_button_professions", "See the professions"), EmptyAct = new WorkAct { Kind = WorkKinds.GoTab, Arg = Professions, Opens = true } };
            t.Columns = new[] { Col(w.T("reallife_work_col_job", "Job"), UiCellKind.Tile), Col(w.T("reallife_work_col_where", "Where"), UiCellKind.Text, 128), Col(string.Empty, UiCellKind.Action, 32),
                Col(w.T("reallife_work_col_hours", "Hours"), UiCellKind.Text, 128), Col(w.T("reallife_work_col_pay_hour", "Pay an hour"), UiCellKind.Money, 96),
                Col(w.T("reallife_work_col_against", "Against yours"), UiCellKind.Trend, 104), Col(w.T("reallife_work_col_you", "You"), UiCellKind.Chip, 172) };
            return t;
        }

        /// <summary>The Applications tab as a squad page.</summary>
        public static WorkTab ApplicationsShell(UtilitiesWords w)
        {
            var t = new WorkTab { Shape = WorkShape.Table, EmptyIcon = UiIcon.ClipboardText, EmptyTitle = w.T("reallife_work_empty_apps_title", "No application is waiting"),
                EmptyBody = w.T("reallife_work_empty_applications", "Apply from Jobs and the employer answers within a day or two. Offers and interviews show here."),
                EmptyActLabel = w.T("reallife_career_button_see_jobs", "See all the jobs"), EmptyAct = new WorkAct { Kind = WorkKinds.GoTab, Arg = Jobs, Opens = true },
                NoteLead = w.T("reallife_work_apps_how_lead", "How it goes."),
                NoteText = w.T("reallife_work_apps_how",
                    "An employer answers a day or two after you apply. An interview or an offer shows here, and an offer can be accepted, asked up or declined.") };
            t.Columns = new[] { Col(w.T("reallife_work_col_job", "Job"), UiCellKind.Tile), Col(w.T("reallife_work_col_sent", "Sent"), UiCellKind.Text, 96),
                Col(w.T("reallife_work_col_answer", "Answer by"), UiCellKind.Text, 104), Col(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 156) };
            return t;
        }

        /// <summary>The Professions tab as a squad page.</summary>
        public static WorkTab ProfessionsShell(UtilitiesWords w)
        {
            var t = new WorkTab { Shape = WorkShape.Table, EmptyIcon = UiIcon.ListChecks, EmptyTitle = w.T("reallife_work_empty_pro_title", "No profession is open to you"),
                EmptyBody = w.T("reallife_work_empty_professions", "Courses open the professions that ask for a qualification.") };
            t.Columns = new[] { Col(w.T("reallife_work_col_trade", "Profession"), UiCellKind.Tile), Col(w.T("reallife_work_col_hours", "Hours"), UiCellKind.Text, 176),
                Col(w.T("reallife_work_col_pay_hour", "Pay an hour"), UiCellKind.Money, 104), Col(w.T("reallife_work_col_posts_open", "Posts open"), UiCellKind.Figure, 88, true),
                Col(w.T("reallife_work_col_for_you", "For you"), UiCellKind.Chip, 172) };
            return t;
        }

        // ------------------------------------------------------------------------------------------------- My job

        private static WorkPage MyJobPage(CareerFacts f, UtilitiesWords w)
        {
            var b = new WorkBuilder(w);
            CareerJob j = f.Job;
            var p = new UiProfileModel
            {
                Name = f.Player, Hue = UiColour.HueOf(f.Player), Role = j.Title, RoleIcon = UiIcon.Briefcase, Employer = j.Employer, EmployerIcon = UiIcon.Store,
                Status = j.Status, StatusTone = j.StatusTone,
                FactLabels = new[] { w.T("reallife_career_col_pay", "Pay"), w.T("reallife_work_col_hours", "Hours"), w.T("reallife_work_with_them", "With them") },
                FactValues = new[] { j.Pay, j.Hours, j.Months }
            };
            b.M.Profile = p;
            b.M.Hero = new UiHeroModel { Icon = UiIcon.Clock, Tint = UiRole.None, Kicker = j.NextTitle, Title = j.NextLine,
                TitleStyle = UiStyle.Number, Line = j.Desk, Progress = j.Progress, Note = j.NextNote };
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
                b.M.People = people;
                b.M.PeopleTitle = w.T("reallife_work_on_shift", "On shift with you");
                b.M.PeopleCount = j.Peers.Count.ToString();
            }
            // F is the desk's locate link (a shift starts at the desk, never here), P the rise; the answers to a matter and a conflict come after, and leaving last.
            int head = b.Button(w.T("reallife_work_show_desk", "Show the desk"), UiButtonKind.Primary, UiIcon.Locate, UiAction.Alt, WorkBuilder.Do((int)CareerAct.ShowDesk, 0, null, true));
            int rise = b.Button(w.T("reallife_life_button_rise", "Ask for a rise"), UiButtonKind.Secondary, UiIcon.TrendUp, UiAction.Option,
                WorkBuilder.Do((int)CareerAct.AskRise, 0, j.CanAsk ? null : j.AskWhy));
            int a = -1, c2 = -1, d = -1;
            if (j.MatterText != null)
            {
                a = b.Button(j.MatterA, UiButtonKind.Secondary, UiIcon.Check, UiAction.Count, WorkBuilder.Do((int)CareerAct.AnswerA));
                c2 = b.Button(j.MatterB, UiButtonKind.Secondary, UiIcon.Check, UiAction.Count, WorkBuilder.Do((int)CareerAct.AnswerB));
            }
            if (j.CanDeclare)
            {
                d = b.Button(w.T("reallife_life_button_declare", "Declare the conflict"), UiButtonKind.Secondary, UiIcon.Warning, UiAction.Count, WorkBuilder.Do((int)CareerAct.Declare));
            }
            int resign = j.Leaving ? -1 : b.Button(w.T("reallife_career_button_resign", "Hand in notice"), UiButtonKind.Destructive, UiIcon.SignOut,
                UiAction.Count, WorkBuilder.Do((int)CareerAct.Resign));
            int walk = b.Button(w.T("reallife_career_button_walk_out", "Walk out"), UiButtonKind.Destructive, UiIcon.SignOut, UiAction.Count, WorkBuilder.Do((int)CareerAct.WalkOut));
            var heads = new List<UiActionModel> { b.ModelAt(head), b.ModelAt(rise) };
            var more = new List<UiActionModel>(4);
            foreach (int k in new[] { a, c2, d })
            {
                if (k >= 0)
                {
                    more.Add(b.ModelAt(k));
                }
            }
            var leave = new List<UiActionModel>(2);
            if (resign >= 0)
            {
                leave.Add(b.ModelAt(resign));
            }
            leave.Add(b.ModelAt(walk));
            WorkPage page = b.Done();
            page.Body = (s, at, x, y, cw, bottom, keys) => CareerMyJob.Lay(s, at, j, b.M, w, heads, more, leave, x, y, cw, bottom, keys);
            return page;
        }

        // A player with no job: the vacancies to go for first, how a post is won, and the way into Jobs.
        private static WorkPage NewPlayer(CareerFacts f, UtilitiesWords w)
        {
            var b = new WorkBuilder(w);
            b.Button(w.T("reallife_career_button_see_jobs", "See all the jobs"), UiButtonKind.Primary, UiIcon.Search, UiAction.Alt, WorkBuilder.Do(WorkKinds.GoTab, Jobs, null, true));
            b.Button(w.T("reallife_career_button_see_courses", "See the courses"), UiButtonKind.Secondary, UiIcon.Books, UiAction.Option, WorkBuilder.Do((int)CareerAct.OpenCampus, 0, null, true));
            b.Button(w.T("reallife_career_button_professions", "See the professions"), UiButtonKind.Secondary, UiIcon.ListChecks, UiAction.Count,
                WorkBuilder.Do(WorkKinds.GoTab, Professions, null, true));
            WorkPage page = b.Done();
            UiActionModel[] actions = page.Model.Actions;
            var best = f.Best.ToArray();
            string laid = f.Laid;
            string title = w.T("reallife_career_job_none_title", "No job");
            string line = laid ?? w.T("reallife_career_job_none", "No job. Apply for one under Jobs.");
            string bestTitle = w.T("reallife_career_best_title", "Where to start");
            string stepsTitle = w.T("reallife_career_how_title", "How a post is won");
            string[] steps =
            {
                w.T("reallife_career_how_apply", "Apply to a post that suits your skills"), w.T("reallife_career_how_interview", "Go to the interview at the time you are given"),
                w.T("reallife_career_how_offer", "Accept the offer, or ask for a little more"), w.T("reallife_career_how_desk", "Start each shift at your desk in the workplace")
            };
            page.Body = (s, at, x, y, cw, bottom, keys) =>
            {
                float lw = UiDraw.SnapDown(cw * 0.6f), gap = UiTokens.S8, rw = cw - lw - gap;
                UiDraw.Text(s, at, x, y, cw, UiStyle.Number, UiRole.Text, title, UiFlags.Truncate);
                int id = UiDraw.Text(s, at, x, y + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number)) + UiTokens.S2, cw, UiStyle.Body, UiRole.Text2, line, UiFlags.Wrap | UiFlags.Truncate, 2);
                float cy = UiDraw.Snap(s.Items[id].Y + s.Items[id].H) + UiTokens.S4;
                float right = x + cw;
                for (int i = actions.Length - 1; i >= 0; i--)
                {
                    UiActionModel a = actions[i];
                    float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                    right -= bw;
                    UiDraw.Button(s, at, right, cy, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                    right -= UiTokens.S3;
                }
                cy += UiTokens.Control + UiTokens.S5;
                float ly = cy + WorkPanel.SectionHead(s, at, x, cy, lw, bestTitle, null);
                for (int i = 0; i < best.Length && ly + 56f <= bottom; i++)
                {
                    WorkRow r = best[i];
                    int face = UiDraw.Box(s, at, x, ly, lw, 56f, UiRole.Band, UiRole.None, UiTokens.RadiusLarge);
                    UiAt inside = at.In(face);
                    float tile = UiTokens.IconTile;
                    int t = UiDraw.Tile(s, inside, x + UiTokens.S3, ly + (56f - tile) / 2f, tile, UiRole.None, false);
                    UiDraw.Icon(s, inside.In(t), x + UiTokens.S3 + (tile - UiTokens.Icon) / 2f, ly + (56f - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon, r.Tint, UiFlags.Middle | UiFlags.Centre, true);
                    float chip = UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(r.Status ?? string.Empty, UiStyle.SmallStrong) + UiTokens.S1);
                    float pay = UiTextRules.Width(r.Price ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1;
                    float nx = x + UiTokens.S3 + tile + UiTokens.S3, nw = lw - (nx - x) - chip - pay - UiTokens.S4 * 3f;
                    float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)), top = ly + (56f - lh - sh) / 2f;
                    UiDraw.Text(s, inside, nx, top, nw, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate | UiFlags.Middle);
                    UiDraw.Text(s, inside, nx, top + lh, nw, UiStyle.Small, UiRole.Text2, r.Sub, UiFlags.Truncate | UiFlags.Middle);
                    UiDraw.TextMiddle(s, inside, x + lw - UiTokens.S4 - chip - UiTokens.S4 - pay, ly, 56f, pay, UiStyle.BodyStrong, UiRole.Text, r.Price, UiFlags.Right);
                    if (!string.IsNullOrEmpty(r.Status))
                    {
                        UiDraw.Chip(s, inside, x + lw - UiTokens.S4 - chip, ly + (56f - UiTokens.Badge - UiTokens.S1) / 2f, r.Status, r.Tone, chip);
                    }
                    ly += 56f + UiTokens.S1;
                }
                float ry = cy + WorkPanel.SectionHead(s, at, x + lw + gap, cy, rw, stepsTitle, null);
                for (int i = 0; i < steps.Length; i++)
                {
                    int disc = UiDraw.Circle(s, at, x + lw + gap, ry + (UiTokens.Control - UiTokens.Icon - UiTokens.S2) / 2f,
                        UiTokens.Icon + UiTokens.S2, UiTokens.Icon + UiTokens.S2, UiRole.Tile, UiFlags.Middle);
                    UiDraw.TextMiddle(s, at.In(disc), x + lw + gap, ry, UiTokens.Control, UiTokens.Icon + UiTokens.S2, UiStyle.SmallStrong, UiRole.AccentText, (i + 1).ToString(), UiFlags.Centre);
                    UiDraw.TextMiddle(s, at, x + lw + gap + UiTokens.Icon + UiTokens.S2 + UiTokens.S3, ry, UiTokens.Control,
                        rw - UiTokens.Icon - UiTokens.S2 - UiTokens.S3, UiStyle.Body, UiRole.Text, steps[i],
                        UiFlags.Truncate);
                    ry += UiTokens.Control + UiTokens.S1;
                }
            };
            return page;
        }
    }
}
