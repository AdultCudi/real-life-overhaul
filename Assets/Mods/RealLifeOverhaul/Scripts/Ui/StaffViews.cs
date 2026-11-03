using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One hired person as the game's My Employees row holds them, in words and figures the game already formed.</summary>
    public struct StaffRow
    {
        public string Name;
        public string Role;          // the primary skill's name
        public string Business;      // the business they work at; null or empty while unassigned
        public int Skill;            // the primary skill, 0 to 100
        public string Wage;          // what is paid an hour, in the game's money words
        public int Satisfaction;     // 0 to 100
        public string Task;          // the game's words for what they do now
        public UiRole TaskTone;
        public bool Picked;
    }

    /// <summary>One candidate as the game's candidates' row holds them.</summary>
    public struct CandidateRow
    {
        public string Name;
        public string Role;
        public int Skill;
        public string Wage;          // what they would be paid an hour
        public string Schedule;      // the hours they ask for, in the game's words
        public string Source;        // where they came from: a job board, an agency, a headhunter
        public string Expires;       // how long until they go, in the game's words
        public bool Soon;            // under a day
        public bool Picked;
    }

    /// <summary>One of a rival's staff as the game's table holds them.</summary>
    public struct RivalRow
    {
        public string Name;
        public string Skill;
        public int Level;
        public int Hours;            // the hours a week they look for
        public bool Talking;         // talks with them are under way
        public bool Poached;         // they once worked for the player
        public string SkillKey;      // the game's name for their skill, for its glyph
        public int Age;
        public string Wage;          // what they would ask an hour, at the city's prices
    }

    /// <summary>The chosen one of a rival's staff, as the game would offer them: what the row has no room for.</summary>
    public sealed class RivalChosenView
    {
        public UiStaffDemand[] Demands = new UiStaffDemand[0];
    }

    /// <summary>The row under the cursor, for the preview beside the table: what the table has no room for.</summary>
    public sealed class StaffPreviewView
    {
        public bool Candidate;
        public string Name, Role;
        public UiIcon RoleIcon = UiIcon.Briefcase;
        public string Business;      // null while unassigned
        public string Status;
        public UiRole StatusTone = UiRole.Muted;
        public int Skill;
        public int Satisfaction = -1;
        public int Age, Hours;
        public string Wage, Schedule, Source, Expires;
        public int DemandsMet, Demands;
        public UiStaffDemand[] DemandRows = new UiStaffDemand[0];
    }

    /// <summary>Someone beside a person: a colleague at the same business, or (for a candidate) one of the staff of that skill.</summary>
    public struct StaffMate
    {
        public string Name, Role;
        public UiIcon RoleIcon;
        public int Skill, Satisfaction;
    }

    /// <summary>One of the game's actions for several picked, by its kind's name and its words.</summary>
    public struct StaffMass
    {
        public string Type;          // the game's name for it ("myemployees_mass_action_fire"), or the mod's
        public string Label;
    }

    /// <summary>One person on a review sheet: who, where, what they are paid now and after, and the result for them.</summary>
    public struct StaffBatchRow
    {
        public string Name, Role, Business;
        public string Now, After, Day;   // pay an hour now, after, and the change for a working day (empty when nothing changes)
        public string Result;            // "Raise", "Will be hired", or why they are left out
        public UiRole Tone;              // the result chip's tone: good, or warn for one left out
    }

    /// <summary>A review sheet's words and rows, as the screen reads them from the pending batch (Core/StaffBatch): plain data for <see cref="StaffViews.Batch"/>.</summary>
    public sealed class StaffBatchView
    {
        public bool Hire;                          // hiring several; otherwise a raise for several
        public string Subject;                     // the raise's words, or the business hired to
        public int Picked, Ready, Left;
        public StaffBatchRow[] Rows = new StaffBatchRow[0];
        public string TotalNow, TotalAfter, TotalDay;   // the picked's pay an hour in all now and after, and the change for a working day of each
        public float Offset;
        public int Reveal = -1;
    }

    /// <summary>One person's page, from the game's employee box: their facts, the box's buttons, skills, demands and work.</summary>
    public sealed class StaffPersonView
    {
        public bool Candidate;
        public string Name;
        public string Role;
        public UiIcon RoleIcon = UiIcon.Briefcase;
        public string Business;                    // null while unassigned
        public string Status;
        public UiRole StatusTone = UiRole.Ok;
        public int Satisfaction = -1;
        public int Level;                          // the primary skill, for the rating
        public int Age;
        public string Wage;
        public int DaysHired;
        public int WeeklyHours, WorkedHours;
        public string Schedule, Source, Expires;
        public bool Soon;
        public string Task;
        public string HrManager;                   // null for none
        public string Positive, Negative, Additional;   // the box's buttons' words as the game wrote them; null for a button not shown
        public bool PositiveOn = true, NegativeOn = true, AdditionalOn = true;
        public bool NegativeArmed;                 // a discard pressed once: the second press does it
        public string Bonus;                       // the bonus button's words ("Pay Bonus ($1,200)"); null for none
        public bool BonusOn;
        public string Cooldown;                    // the bonus's cooldown in the game's words; null when it is ready
        public string Assign;                      // the business chosen in the game's choice
        public bool AssignOn = true;               // false while they train, as the game hides its choice then
        public string Training, TrainingLeft;      // the skill being trained and the hours left, in the game's words; null for none
        public bool TrainingCard;                  // the card over the page that says so, until it is put away
        public UiStaffSkill[] Skills = new UiStaffSkill[0];
        public UiStaffDemand[] Demands = new UiStaffDemand[0];
        public StaffMate[] Mates = new StaffMate[0];
        public int MatesTotal;                     // how many there are, when more than the mates given
        public string MatesAt;                     // the business they share; for a candidate, the skill the staff share
    }

    /// <summary>What My Employees shows now, from the game's own tables, box and filters: plain data for <see cref="StaffViews"/>.</summary>
    public sealed class StaffView
    {
        public bool Candidates;                    // the tab shown
        public int EmployeeCount, CandidateCount;  // the tabs' counts: everyone hired, and the candidates the phone's badge counts
        public StaffRow[] Employees = new StaffRow[0];
        public CandidateRow[] CandidateRows = new CandidateRow[0];
        public int Listed;                         // how many the game's table lists before the business filter
        public string[] Filters = new string[0];   // each filter's value in words, as the tab's filter row holds them
        public int SortColumn = -1;
        public bool SortDown;
        public string Query;
        public bool Filtered;                      // a filter or the search narrows the list
        public bool Picking;
        public int PickedCount;
        public bool AllPicked;
        public StaffMass[] Mass = new StaffMass[0];
        public string AskTitle, AskValue;          // a mass action's panel of choices; AskValue null while none asks
        public bool HideJobBoard;
        public string TotalWage;                   // the wages of those listed, an hour
        public int Selected = -1;
        public float Offset;
        public int Reveal = -1;
        public StaffPersonView Person;
        public StaffBatchView Batch;               // the review sheet of a batch waiting for a yes; null for none
        public StaffPreviewView Preview;           // the row under the cursor; null for none
        public int OpenList = -1;
        public string[] Options = new string[0];
        public int OptionChosen = -1;
        public float OptionOffset;
    }

    /// <summary>
    /// The staff page's view model from what My Employees and a rival's staff show (<see cref="StaffView"/>, the rival's
    /// rows): the tabs with their counts, the squad's columns and rows, the tool row, the picking bar with the game's mass
    /// actions in their looks, the empty states, one person's page and the card of a training under way. Words are the
    /// game's keys where the game has the words, the mod's otherwise. Game-free, so the previews and the checks draw exactly
    /// what ships. Cost: a few arrays a layout.
    /// </summary>
    public static class StaffViews
    {
        public const int Business = 0;    // the employees' filter row: business (the kit's own), skill, demand, status
        public const int ColumnSkill = 2, ColumnWage = 3;
        public const string StaffApp = "MyEmployees", RivalsApp = "Rivals";   // the game's names for the apps, as the phone's looks know them

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>My Employees: the tabs, the squad's table or one person's page, an open list and the training card.</summary>
        public static void Staff(UiStaffModel m, UiWords t, StaffView v)
        {
            m.Title = t("reallife_ui_staff_app", "My Employees");   // the game has only its joined internal name ("MyEmployees") for the app
            m.Icon = PhoneLooks.Glyph(StaffApp);   // the app's own glyph, as the phone and the status bar's row show it
            m.Tabs = new[]
            {
                new UiNavEntry { Icon = UiIcon.People, Tint = UiIconTints.Of(UiIcon.People), Label = t("myemployees_employees_tab", "Employees"), Count = Whole(v.EmployeeCount) },
                new UiNavEntry { Icon = UiIcon.Handshake, Tint = UiIconTints.Of(UiIcon.Handshake), Label = t("myemployees_candidates_tab", "Candidates"),
                    Count = v.CandidateCount > 0 ? Whole(v.CandidateCount) : null },
            };
            m.Tab = v.Candidates ? 1 : 0;
            m.SwitchLabel = v.Candidates && v.Person == null ? t("myemployees_candidates_hide_job_board", "Hide Job Board Candidates") : null;
            m.SwitchOn = v.HideJobBoard;
            m.Next = null;
            m.LeadActions = new UiActionModel[0];
            m.Person = v.Person != null ? Person(t, v.Person) : null;
            m.Preview = v.Person == null && v.Preview != null ? Preview(t, v.Preview) : null;
            m.Card = v.Person != null && v.Person.TrainingCard ? TrainingCard(t, v.Person) : null;
            m.Batch = v.Batch != null ? Batch(t, v.Batch) : null;
            m.EmptyOnly = false;
            if (m.Person == null)
            {
                Table(m.Table, t, v);
                Empty(m, t, v);
            }
            m.OpenList = v.OpenList;
            m.Options = v.Options ?? new string[0];
            m.OptionChosen = v.OptionChosen;
            m.OptionOffset = v.OptionOffset;
            m.Prompts = Prompts(t, v.Person == null);
        }

        // The squad's big table: the columns of the tab, its rows, the tool row or the picking bar, the foot and its empty words.
        private static void Table(UiDataTableModel table, UiWords t, StaffView v)
        {
            table.Columns = v.Candidates ? CandidateColumns(t) : EmployeeColumns(t);
            table.SortColumn = v.SortColumn;
            table.SortDown = v.SortDown;
            if (v.Candidates)
            {
                table.Rows = new UiDataRow[v.CandidateRows.Length];
                for (int i = 0; i < v.CandidateRows.Length; i++)
                {
                    table.Rows[i] = Row(t, ref v.CandidateRows[i]);
                }
            }
            else
            {
                table.Rows = new UiDataRow[v.Employees.Length];
                for (int i = 0; i < v.Employees.Length; i++)
                {
                    table.Rows[i] = Row(t, ref v.Employees[i]);
                }
            }
            table.RowCount = table.Rows.Length;
            table.Selected = v.Selected;
            table.Filters = v.Filters ?? new string[0];
            table.SearchPlaceholder = t("common_search", "Search");
            table.Query = string.IsNullOrEmpty(v.Query) ? null : v.Query;
            table.Count = Fill(t("reallife_ui_staff_count", "{shown} of {all}"), "shown", Whole(table.RowCount), "all", Whole(v.Candidates ? v.CandidateCount : v.EmployeeCount));
            table.Picking = v.Picking;
            table.PickWords = t("reallife_ui_staff_pick", "Pick several");
            table.PickedWords = Fill(t("reallife_ui_staff_picked", "{count} picked"), "count", Whole(v.PickedCount));
            table.DoneWords = t("reallife_ui_staff_done", "Done");
            table.Mass = Mass(t, v);
            table.MassField = v.Picking ? v.AskValue : null;
            table.MassFieldTitle = v.AskTitle;
            table.MassFieldActions = new[]
            {
                new UiActionModel { Label = t("common_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                new UiActionModel { Label = t("common_confirm", "Confirm"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Count },
            };
            table.Totals = !v.Candidates && table.RowCount > 1 ? new[] { null, null, null, v.TotalWage, null, null } : null;   // one row needs no foot
            table.TotalsLabel = t("reallife_ui_staff_totals", "Listed");
            table.Offset = v.Offset;
            table.Reveal = v.Reveal;
            if (v.Filtered)
            {
                table.EmptyIcon = UiIcon.Search;
                table.EmptyTitle = t("reallife_ui_staff_none_match", "Nobody matches");
                table.EmptyBody = t("reallife_ui_staff_none_match_body", "No one here matches these filters and this search. Clear them to see everyone again.");
                table.EmptyAction = t("reallife_ui_staff_clear", "Clear filters");
            }
            else if (v.Candidates)
            {
                table.EmptyIcon = UiIcon.Handshake;
                table.EmptyTitle = t("reallife_ui_staff_no_candidates", "No candidates now");
                table.EmptyBody = t("reallife_ui_staff_no_candidates_body", "Candidates arrive here from a job board at one of your businesses, a recruitment agency or a headhunter.");
                table.EmptyAction = null;
            }
            else
            {
                table.EmptyIcon = UiIcon.People;
                table.EmptyTitle = t("reallife_ui_staff_no_staff", "Nobody works for you yet");
                table.EmptyBody = t("reallife_ui_staff_no_staff_body", "Hire people from the Candidates tab. Once hired, they show here with their pay, their skill and how happy they are.");
                table.EmptyAction = null;
            }
        }

        // Nobody at all to list (not a filter's empty list): the empty state takes the page, without a table's tools over
        // nothing, and for the employees it leads to the candidates.
        private static void Empty(UiStaffModel m, UiWords t, StaffView v)
        {
            if (v.Filtered || m.Table.RowCount > 0 || (v.Candidates ? v.CandidateCount : v.EmployeeCount) > 0)
            {
                return;
            }
            m.EmptyOnly = true;
            m.EmptyIcon = m.Table.EmptyIcon;
            m.EmptyTitle = m.Table.EmptyTitle;
            m.EmptyBody = m.Table.EmptyBody;
            m.EmptyAction = v.Candidates ? null : t("reallife_ui_staff_see_candidates", "See the candidates");
            m.Preview = null;
        }

        /// <summary>
        /// The employees' columns: a face with the name and skill, the business, skill, pay, satisfaction and what they do; the
        /// age and hours stand in the preview beside the table.
        /// </summary>
        public static UiDataColumn[] EmployeeColumns(UiWords t) => new[]
        {
            new UiDataColumn { Title = t("myemployees_employee_name", "Employee Name"), Kind = UiCellKind.Person, Sortable = true },
            new UiDataColumn { Title = t("myemployees_current_business", "Current Business"), Kind = UiCellKind.Text, Sortable = true },
            new UiDataColumn { Title = t("myemployees_jobtype", "Skill"), Kind = UiCellKind.Meter, Width = 144f, Sortable = true },
            new UiDataColumn { Title = t("myemployees_hourly_wage", "Hourly Wage"), Kind = UiCellKind.Money, Width = 112f, Sortable = true },
            new UiDataColumn { Title = t("myemployees_satisfaction", "Satisfaction"), Kind = UiCellKind.Meter, Width = 144f, Sortable = true },
            new UiDataColumn { Title = t("myemployees_task", "Task"), Kind = UiCellKind.Chip, Width = 144f, Sortable = true },
        };

        /// <summary>The candidates' columns: a face with the name and skill, the skill, the wage they would be paid, the hours they ask, where they came from and how long they stay.</summary>
        public static UiDataColumn[] CandidateColumns(UiWords t) => new[]
        {
            new UiDataColumn { Title = t("myemployees_employee_name", "Employee Name"), Kind = UiCellKind.Person, Sortable = true },
            new UiDataColumn { Title = t("myemployees_schedule", "Schedule"), Kind = UiCellKind.Text, Width = 160f, Sortable = true },
            new UiDataColumn { Title = t("myemployees_recruitmentsource", "Recruitment Source"), Kind = UiCellKind.Text },
            new UiDataColumn { Title = t("myemployees_jobtype", "Skill"), Kind = UiCellKind.Meter, Width = 144f, Sortable = true },
            new UiDataColumn { Title = t("reallife_ui_staff_asking", "Asking wage"), Kind = UiCellKind.Money, Width = 112f, Sortable = true },
            new UiDataColumn { Title = t("myemployees_expiresin", "Expires In"), Kind = UiCellKind.Chip, Width = 120f, Sortable = true },
        };

        private static UiDataRow Row(UiWords t, ref StaffRow r) => new UiDataRow
        {
            Cells = new[] { r.Name, string.IsNullOrEmpty(r.Business) ? t("common_unassigned", "Unassigned") : r.Business, Whole(r.Skill), r.Wage, Whole(r.Satisfaction), r.Task },
            Values = new[] { -1, -1, r.Skill, -1, r.Satisfaction, -1 },
            Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, r.TaskTone },
            Sub = r.Role, Hue = UiColour.HueOf(r.Name), Ring = Mood(r.Satisfaction), Picked = r.Picked
        };

        private static UiDataRow Row(UiWords t, ref CandidateRow r) => new UiDataRow
        {
            Cells = new[] { r.Name, r.Schedule, r.Source, Whole(r.Skill), r.Wage, r.Expires },
            Values = new[] { -1, -1, -1, r.Skill, -1, -1 },
            Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, r.Soon ? UiRole.Warn : UiRole.Muted },
            Sub = r.Role, Hue = UiColour.HueOf(r.Name), Ring = UiRole.None, Picked = r.Picked
        };

        /// <summary>
        /// The row under the cursor beside the table: the facts the table has no room for (the business, age and hours, or a
        /// candidate's ask, hours, source and time left), the skill and mood graded, the demands met, and the way to the page.
        /// </summary>
        public static UiStaffPreview Preview(UiWords t, StaffPreviewView v)
        {
            var p = new UiStaffPreview
            {
                Name = v.Name, Hue = UiColour.HueOf(v.Name ?? string.Empty), Ring = v.Candidate ? UiRole.None : Mood(Math.Max(0, v.Satisfaction)), Role = v.Role, RoleIcon = v.RoleIcon,
                Status = v.Status, StatusTone = v.StatusTone, Link = t("reallife_ui_staff_open", "Open their page"),
                DemandsTitle = t("myemployees_wants_and_demands", "Wants & Demands"), Demands = v.DemandRows ?? new UiStaffDemand[0]
            };
            if (v.Candidate)
            {
                p.RowLabels = new[] { t("myemployees_age", "Age"), t("reallife_ui_staff_asking", "Asking wage"), t("myemployees_schedule", "Schedule"),
                    t("myemployees_recruitmentsource", "Recruitment Source") };
                p.RowValues = new[] { Whole(v.Age), v.Wage, v.Schedule, v.Source };
                p.BarLabels = new[] { t("myemployees_jobtype", "Skill") };
                p.BarValues = new[] { v.Skill };
            }
            else
            {
                p.RowLabels = new[] { t("myemployees_current_business", "Current Business"), t("myemployees_age", "Age"), t("reallife_ui_staff_week", "Hours a week"),
                    t("myemployees_hourly_wage", "Hourly Wage") };
                p.RowValues = new[] { string.IsNullOrEmpty(v.Business) ? t("common_unassigned", "Unassigned") : v.Business, Whole(v.Age), Whole(v.Hours), v.Wage };
                p.BarLabels = new[] { t("myemployees_jobtype", "Skill"), t("myemployees_satisfaction", "Satisfaction") };
                p.BarValues = new[] { v.Skill, Math.Max(0, Math.Min(100, v.Satisfaction)) };
                if (v.Demands > 0)
                {
                    p.Note = Fill(t("reallife_ui_staff_demands_met", "{met} of {all} wants and demands met"), "met", Whole(v.DemandsMet), "all", Whole(v.Demands));
                    p.NoteMet = v.DemandsMet == v.Demands;
                }
            }
            return p;
        }

        /// <summary>A portrait's ring for a satisfaction, by the game's own steps (its table turns red at 30 and yellow at 60).</summary>
        public static UiRole Mood(int satisfaction) => UiTokens.Grade(Math.Max(0, Math.Min(100, satisfaction)));

        // While picking: all or none first, then the game's actions in its order with the dangerous ones before, and its main
        // one last (assigning a business for the employees, hiring for the candidates), as the bar puts its main action last.
        private static UiActionModel[] Mass(UiWords t, StaffView v)
        {
            int n = v.Mass?.Length ?? 0;
            var list = new UiActionModel[n + 1];
            list[0] = new UiActionModel
            {
                Label = v.AllPicked ? t("reallife_ui_staff_pick_none", "Pick none") : t("reallife_ui_staff_pick_all", "Pick all"), Kind = UiButtonKind.Secondary, Key = UiAction.Count,
                Command = -1, Arg = -1
            };
            int at = 1;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    MassLook(v.Mass[i].Type, v.Candidates, out UiIcon icon, out UiButtonKind kind, out bool main);
                    int order = kind == UiButtonKind.Destructive ? 0 : main ? 2 : 1;
                    if (order != pass)
                    {
                        continue;
                    }
                    list[at++] = new UiActionModel
                    {
                        Label = v.Mass[i].Label, Kind = kind, Icon = icon, Key = UiAction.Count, Command = -1, Arg = i, Disabled = v.PickedCount == 0
                    };
                }
            }
            return list;
        }

        /// <summary>
        /// A mass action's look by the game's name for it: its glyph, whether it cannot be undone (fire, discard), and whether
        /// it is the tab's main action. One the kit does not know keeps a plain button.
        /// </summary>
        public static void MassLook(string type, bool candidates, out UiIcon icon, out UiButtonKind kind, out bool main)
        {
            string name = type ?? string.Empty;
            kind = UiButtonKind.Secondary;
            main = false;
            icon = UiIcon.None;
            if (name.EndsWith("_fire", StringComparison.Ordinal) || name.EndsWith("_discardcandidate", StringComparison.Ordinal))
            {
                kind = UiButtonKind.Destructive;
                icon = UiIcon.Trash;
            }
            else if (name.EndsWith("_paybonuses", StringComparison.Ordinal))
            {
                icon = UiIcon.Coin;
            }
            else if (name.EndsWith("_trainprimaryskill", StringComparison.Ordinal))
            {
                icon = UiIcon.Cap;
            }
            else if (name.EndsWith("_assignbusinessandhire", StringComparison.Ordinal) || name.EndsWith("_hirecandidate", StringComparison.Ordinal))
            {
                icon = UiIcon.Handshake;
                main = candidates && name.EndsWith("_hirecandidate", StringComparison.Ordinal);
            }
            else if (name.EndsWith("_assignbusiness", StringComparison.Ordinal))
            {
                icon = UiIcon.Store;
                main = !candidates;
            }
            else if (name.EndsWith("_mass_raise", StringComparison.Ordinal))
            {
                icon = UiIcon.ChartLineUp;
            }
            if (main)
            {
                kind = UiButtonKind.Primary;
            }
        }

        /// <summary>A skill's glyph by the game's name for it; a skill the kit does not know wears the briefcase.</summary>
        public static UiIcon SkillIcon(string skill)
        {
            switch (skill)
            {
                case "ba:skill_actor": return UiIcon.Camera;
                case "ba:skill_cleaning": return UiIcon.Wrench;
                case "ba:skill_customerservice": return UiIcon.Headset;
                case "ba:skill_deliverydriver": return UiIcon.Truck;
                case "ba:skill_dj": return UiIcon.MusicNotes;
                case "ba:skill_eventplanner": return UiIcon.Calendar;
                case "ba:skill_factoryworker": return UiIcon.Factory;
                case "ba:skill_graphicdesigner": return UiIcon.PaintBrush;
                case "ba:skill_gymtrainer": return UiIcon.Heartbeat;
                case "ba:skill_hairstylist": return UiIcon.Person;
                case "ba:skill_headhunter": return UiIcon.IdBadge;
                case "ba:skill_hrmanager": return UiIcon.IdCard;
                case "ba:skill_lawyer": return UiIcon.Scales;
                case "ba:skill_logisticsmanager": return UiIcon.Package;
                case "ba:skill_negotiation": return UiIcon.Handshake;
                case "ba:skill_pricingmanager": return UiIcon.Tag;
                case "ba:skill_programmer": return UiIcon.Code;
                case "ba:skill_projectionist": return UiIcon.Image;
                case "ba:skill_purchasingagent": return UiIcon.ShoppingCart;
                case "ba:skill_securityguard": return UiIcon.ShieldCheck;
                case "ba:skill_stagecrew": return UiIcon.Hammer;
                case "ba:skill_travelagent": return UiIcon.Globe;
                default: return UiIcon.Briefcase;
            }
        }

        /// <summary>
        /// One person's page: the head (face in the mood's ring, skill, business, a state chip, four facts, the skill as stars)
        /// with the box's buttons (the danger first, the second on P, the main on F), the skills, the work and the demands.
        /// </summary>
        public static UiStaffPerson Person(UiWords t, StaffPersonView v)
        {
            var p = new UiStaffPerson();
            p.Profile = new UiProfileModel
            {
                Name = v.Name, Hue = UiColour.HueOf(v.Name ?? string.Empty), Ring = v.Candidate ? UiRole.None : Mood(Math.Max(0, v.Satisfaction)), Role = v.Role, RoleIcon = v.RoleIcon,
                RoleTint = UiRole.None, Employer = string.IsNullOrEmpty(v.Business) ? null : v.Business, EmployerIcon = UiIcon.Store, Status = v.Status, StatusTone = v.StatusTone,
                RatingLabel = t("myemployees_jobtype", "Skill"), Rating = Math.Max(0, Math.Min(10, (int)Math.Round(v.Level / 10f, MidpointRounding.AwayFromZero)))
            };
            if (v.Candidate)
            {
                p.Profile.FactLabels = new[] { t("myemployees_age", "Age"), t("reallife_ui_staff_asking", "Asking wage"), t("myemployees_schedule", "Schedule") };
                p.Profile.FactValues = new[] { Whole(v.Age), v.Wage, v.Schedule };
            }
            else
            {
                p.Profile.FactLabels = new[]
                {
                    t("myemployees_age", "Age"), t("myemployees_hourly_wage", "Hourly Wage"), LabelOf(t("myemployees_days_hired", "Days Hired: <b>{daysHired}</b>"), "daysHired"),
                    t("reallife_ui_staff_week", "Hours a week")
                };
                p.Profile.FactValues = new[] { Whole(v.Age), v.Wage, Fill(t("common_days", "{value} days"), "value", Whole(v.DaysHired)), Whole(v.WeeklyHours) };
            }
            p.Actions = Actions(t, v);
            p.SkillsTitle = t("myemployees_skills", "Skills");
            p.TrainWords = t("myemployees_train_skill_button", "Train skill");
            p.Skills = v.Skills ?? new UiStaffSkill[0];
            p.DemandsTitle = t("myemployees_wants_and_demands", "Wants & Demands");
            p.Demands = v.Demands ?? new UiStaffDemand[0];
            p.DemandsEmpty = t("reallife_ui_staff_no_demands", "They ask for nothing in particular.");
            p.AssignLabel = t("myemployees_assignbusiness", "Assign Business");
            p.AssignValue = v.Assign;
            p.AssignDisabled = !v.AssignOn;
            if (v.Candidate)
            {
                p.WorkTitle = t("ba:myemployees_candidateinfo_header", "Candidate info");
                p.Satisfaction = -1;
                p.RowLabels = new[] { t("myemployees_recruitmentsource", "Recruitment Source") };
                p.RowValues = new[] { v.Source };
            }
            else
            {
                p.WorkTitle = t("reallife_ui_staff_work", "Work");
                p.SatisfactionLabel = t("myemployees_satisfaction", "Satisfaction");
                p.Satisfaction = Math.Max(0, Math.Min(100, v.Satisfaction));
                var labels = new System.Collections.Generic.List<string>(4);
                var values = new System.Collections.Generic.List<string>(4);
                if (!string.IsNullOrEmpty(v.Task) && v.Task != v.Status)
                {
                    labels.Add(t("myemployees_task", "Task"));
                    values.Add(v.Task);
                }
                labels.Add(t("reallife_ui_staff_this_week", "This week"));
                values.Add(Fill(t("reallife_ui_staff_week_hours", "{worked} of {scheduled} hours"), "worked", Whole(v.WorkedHours), "scheduled", Whole(v.WeeklyHours)));
                labels.Add(t("myemployees_configure_pay_bonus_tooltip_title", "Bonus"));
                values.Add(!string.IsNullOrEmpty(v.Cooldown) ? v.Cooldown : t("reallife_ui_staff_bonus_ready", "Ready to pay"));
                if (!string.IsNullOrEmpty(v.HrManager))
                {
                    labels.Add(t("myemployees_hrmanager", "HR Manager"));
                    values.Add(v.HrManager);
                }
                p.RowLabels = labels.ToArray();
                p.RowValues = values.ToArray();
            }
            Mates(t, v, p);
            if (!string.IsNullOrEmpty(v.Training))
            {
                p.Note = Fill(t("reallife_ui_staff_training", "Training {skill}: {left}."), "skill", v.Training, "left", v.TrainingLeft ?? string.Empty);
                p.NoteTone = UiRole.None;
            }

            return p;
        }

        // Who works beside them (a candidate: the staff with their skill), each with their skill and mood graded, and the way to all.
        private static void Mates(UiWords t, StaffPersonView v, UiStaffPerson p)
        {
            StaffMate[] mates = v.Mates ?? new StaffMate[0];
            p.People = new UiPersonRow[mates.Length];
            for (int i = 0; i < mates.Length; i++)
            {
                p.People[i] = new UiPersonRow
                {
                    Name = mates[i].Name, Hue = UiColour.HueOf(mates[i].Name ?? string.Empty), RoleIcon = mates[i].RoleIcon, RoleTint = UiRole.None, Role = mates[i].Role,
                    FirstLabel = t("myemployees_jobtype", "Skill"), First = mates[i].Skill, SecondLabel = t("myemployees_satisfaction", "Satisfaction"), Second = mates[i].Satisfaction
                };
            }
            int total = Math.Max(v.MatesTotal, mates.Length);
            p.PeopleTitle = v.Candidate ? t("reallife_ui_staff_same_skill", "Your staff with this skill")
                : Fill(t("reallife_ui_staff_mates", "Also at {business}"), "business", v.MatesAt);
            p.PeopleCount = Fill(t("reallife_ui_staff_people", "{count} people"), "count", Whole(total));
            p.PeopleMore = v.Candidate || total == 0 ? null : Fill(t("reallife_ui_staff_see_all", "See all {count} in the list"), "count", Whole(total));
        }

        // The box's buttons as the game shows them: the danger (fire or discard) first, the bonus or negotiation on P, the main
        // (manage schedule or hire) last on F. A button the game hides is left out; one it holds is drawn held.
        private static UiActionModel[] Actions(UiWords t, StaffPersonView v)
        {
            var list = new System.Collections.Generic.List<UiActionModel>(3);
            if (!string.IsNullOrEmpty(v.Negative))
            {
                list.Add(new UiActionModel
                {
                    Label = v.Negative, Kind = v.NegativeArmed ? UiButtonKind.Armed : UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count,
                    Command = UiAppWindow.ActionCommand, Arg = 0, Disabled = !v.NegativeOn
                });
            }
            if (v.Candidate ? !string.IsNullOrEmpty(v.Additional) : !string.IsNullOrEmpty(v.Bonus))
            {
                list.Add(new UiActionModel
                {
                    Label = v.Candidate ? v.Additional : v.Bonus, Kind = UiButtonKind.Secondary, Icon = v.Candidate ? UiIcon.Message : UiIcon.Coin, Key = UiAction.Option,
                    Command = UiAppWindow.ActionCommand, Arg = 1, Disabled = v.Candidate ? !v.AdditionalOn : !v.BonusOn
                });
            }
            if (!string.IsNullOrEmpty(v.Positive))
            {
                list.Add(new UiActionModel
                {
                    Label = v.Positive, Kind = UiButtonKind.Primary, Icon = v.Candidate ? UiIcon.Handshake : UiIcon.Calendar, Key = UiAction.Alt,
                    Command = UiAppWindow.ActionCommand, Arg = 2, Disabled = !v.PositiveOn
                });
            }
            return list.ToArray();
        }

        /// <summary>
        /// The review sheet of a batch: the raise for several picked or hiring several picked, before anything changes. Each person
        /// is a row with what changes for them and the result (the change, or why they are left out); the foot sums the picked; the
        /// buttons are the way back and the yes, which says what it does and is not offered when nobody would be reached.
        /// </summary>
        public static UiBatchModel Batch(UiWords t, StaffBatchView v)
        {
            var m = new UiBatchModel
            {
                Icon = v.Hire ? UiIcon.Handshake : UiIcon.ChartLineUp,
                Title = v.Hire ? t("reallife_staff_batch_hire_title", "Review the hiring") : t("reallife_staff_batch_raise_title", "Review the pay rise"),
                Lead = v.Hire
                    ? Fill(t("reallife_staff_batch_hire_lead", "{count} of the {picked} picked will be hired at {business}. The others stay candidates. Nobody is hired until you confirm."),
                        "count", Whole(v.Ready), "picked", Whole(v.Picked), "business", v.Subject)
                    : Fill(t("reallife_staff_batch_raise_lead", "{what}. {count} of the {picked} picked will be paid more, {change} more a day in all. Nothing changes until you give it."),
                        "what", v.Subject, "count", Whole(v.Ready), "picked", Whole(v.Picked), "change", v.TotalDay)
            };
            UiDataTableModel table = m.Table;
            string person = t("myemployees_employee_name", "Employee Name"), result = t("reallife_staff_batch_result", "Result");
            table.Columns = v.Hire
                ? new[]
                {
                    new UiDataColumn { Title = person, Kind = UiCellKind.Person },
                    new UiDataColumn { Title = t("reallife_staff_batch_hired_to", "Hired to"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = t("reallife_ui_staff_asking", "Asking wage"), Kind = UiCellKind.Money, Width = 144f },
                    new UiDataColumn { Title = result, Kind = UiCellKind.Chip, Width = 224f },
                }
                : new[]
                {
                    new UiDataColumn { Title = person, Kind = UiCellKind.Person },
                    new UiDataColumn { Title = t("myemployees_current_business", "Current Business"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = t("reallife_staff_batch_now", "Pay now"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = t("reallife_staff_batch_after", "Pay after"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = t("reallife_staff_batch_day", "Change a day"), Kind = UiCellKind.Money, Width = 144f },
                    new UiDataColumn { Title = result, Kind = UiCellKind.Chip, Width = 224f },
                };
            string none = t("common_unassigned", "Unassigned");
            table.Rows = new UiDataRow[v.Rows.Length];
            for (int i = 0; i < v.Rows.Length; i++)
            {
                ref StaffBatchRow r = ref v.Rows[i];
                string business = string.IsNullOrEmpty(r.Business) ? none : r.Business;
                table.Rows[i] = v.Hire
                    ? new UiDataRow
                    {
                        Cells = new[] { r.Name, business, r.Now, r.Result }, Values = new[] { -1, -1, -1, -1 },
                        Tones = new[] { UiRole.None, UiRole.None, UiRole.None, r.Tone }, Sub = r.Role, Hue = UiColour.HueOf(r.Name ?? string.Empty), Ring = UiRole.None
                    }
                    : new UiDataRow
                    {
                        Cells = new[] { r.Name, business, r.Now, r.After, r.Day, r.Result }, Values = new[] { -1, -1, -1, -1, -1, -1 },
                        Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, r.Tone }, Sub = r.Role, Hue = UiColour.HueOf(r.Name ?? string.Empty), Ring = UiRole.None
                    };
            }
            table.RowCount = table.Rows.Length;
            table.Selected = -1;
            table.TotalsLabel = t("reallife_staff_batch_total", "In all");
            table.Totals = v.Hire ? new[] { null, null, v.TotalNow, null } : new[] { null, null, v.TotalNow, v.TotalAfter, v.TotalDay, null };
            table.Offset = v.Offset;
            table.Reveal = v.Reveal;
            m.Buttons = new[]
            {
                new UiActionModel { Label = t("common_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, Command = UiBatch.ButtonCommand, Arg = 0 },
                new UiActionModel
                {
                    Label = v.Hire ? Fill(t("reallife_staff_batch_hire_give", "Hire {count}"), "count", Whole(v.Ready)) : t("reallife_co_mass_raise_give", "Give the raise"),
                    Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Command = UiBatch.ButtonCommand, Arg = 1, Disabled = v.Ready == 0
                },
            };
            m.Focus = v.Ready == 0 ? 0 : 1;
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            };
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        // The training under way, said once as the person's page opens: the game holds their box until it ends.
        private static UiCardModel TrainingCard(UiWords t, StaffPersonView v)
        {
            var card = new UiCardModel
            {
                Icon = UiIcon.Cap, Title = t("myemployees_task_training", "Training"), Face = v.Name, FaceHue = UiColour.HueOf(v.Name ?? string.Empty),
                FaceRing = Mood(Math.Max(0, v.Satisfaction)),
                Body = Fill(t("reallife_ui_staff_training_body", "{name} is training {skill}: {left}. Until it ends they cannot be assigned to a business."),
                    "name", v.Name, "skill", v.Training ?? string.Empty, "left", v.TrainingLeft ?? string.Empty),
                Buttons = new[] { new UiActionModel { Label = t("common_ok", "OK"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Command = UiCard.ButtonCommand, Arg = 0 } },
                Focus = 0
            };
            card.Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") } };
            card.PromptCount = card.Prompts.Length;
            return card;
        }

        /// <summary>
        /// A rival's staff: their table read only, the game's title, and beside it the one chosen as the game would offer
        /// them (their age, the wage they would ask at the city's prices, the hours they look for, their skill and their
        /// wants and demands) with Negotiate at its foot.
        /// </summary>
        public static void Rival(UiStaffModel m, UiWords t, string title, RivalRow[] rows, int chosen, RivalChosenView offer)
        {
            m.Title = title;
            m.Icon = PhoneLooks.Glyph(RivalsApp);
            m.Tabs = new UiNavEntry[0];
            m.SwitchLabel = null;
            m.Person = null;
            m.Card = null;
            m.Batch = null;
            m.EmptyOnly = false;
            m.OpenList = -1;
            m.Next = null;
            m.LeadActions = new UiActionModel[0];
            bool any = rows.Length > 0;
            int at = any ? Math.Max(0, Math.Min(chosen, rows.Length - 1)) : -1;
            RivalRow c = any ? rows[at] : default(RivalRow);
            RivalChosenView o = offer ?? new RivalChosenView();
            m.Preview = !any ? null : new UiStaffPreview
            {
                Name = c.Name, Hue = UiColour.HueOf(c.Name ?? string.Empty), Role = c.Skill, RoleIcon = SkillIcon(c.SkillKey),
                Status = c.Talking ? t("reallife_ui_staff_rival_talks", "In talks") : c.Poached ? t("reallife_ui_staff_rival_former", "Former staff") : null,
                StatusTone = c.Talking ? UiRole.Accent : UiRole.Warn,
                DemandsTitle = t("myemployees_wants_and_demands", "Wants & Demands"), Demands = o.Demands ?? new UiStaffDemand[0],
                Actions = new[]
                {
                    new UiActionModel { Label = t("dialog_negotiate_button", "Negotiate"), Kind = UiButtonKind.Primary, Icon = UiIcon.Handshake, Key = UiAction.Alt, Disabled = c.Talking }
                }
            };
            UiDataTableModel table = m.Table;
            table.Columns = new[]
            {
                new UiDataColumn { Title = t("myemployees_employee_name", "Employee Name"), Kind = UiCellKind.Person },
                new UiDataColumn { Title = t("myemployees_age", "Age"), Kind = UiCellKind.Figure, Width = 64f },
                new UiDataColumn { Title = t("myemployees_jobtype", "Skill"), Kind = UiCellKind.Meter, Width = 200f },
                new UiDataColumn { Title = t("reallife_ui_staff_asking", "Asking wage"), Kind = UiCellKind.Money, Width = 128f },
                new UiDataColumn { Title = t("reallife_ui_staff_week", "Hours a week"), Kind = UiCellKind.Figure, Width = 128f },
                new UiDataColumn { Title = t("bizman_status", "Status"), Kind = UiCellKind.Chip, Width = 160f },
            };
            table.Rows = new UiDataRow[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                RivalRow r = rows[i];
                string state = r.Talking ? t("reallife_ui_staff_rival_talks", "In talks") : r.Poached ? t("reallife_ui_staff_rival_former", "Former staff") : null;
                table.Rows[i] = new UiDataRow
                {
                    Cells = new[] { r.Name, Whole(r.Age), Whole(r.Level), r.Wage, Whole(r.Hours), state }, Values = new[] { -1, -1, r.Level, -1, -1, -1 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, r.Talking ? UiRole.Accent : UiRole.Warn }, Sub = r.Skill,
                    Hue = UiColour.HueOf(r.Name ?? string.Empty), Ring = UiRole.None
                };
            }
            table.RowCount = rows.Length;
            table.Selected = at;
            table.Filters = new string[0];
            table.SearchPlaceholder = null;
            table.Picking = false;
            table.PickWords = null;
            table.Mass = new UiActionModel[0];
            table.MassField = null;
            table.Totals = null;
            table.Count = null;
            table.EmptyIcon = UiIcon.People;
            table.EmptyTitle = t("reallife_ui_staff_rival_none", "No staff listed");
            table.EmptyBody = t("reallife_ui_staff_rival_none_body", "This business has nobody on its books just now. Look again after its next opening day.");
            table.EmptyAction = null;
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            };
        }

        private static UiPrompt[] Prompts(UiWords t, bool squad) => squad
            ? new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_staff_prompt_tab", "Tab") }, new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            }
            : new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            };

        private static string Whole(int n) => n.ToString(Inv);

        /// <summary>
        /// The label of one of the game's "Label: value" texts (its days hired: "Days Hired: {daysHired}" in bold), its tags
        /// and the placeholder taken off, so the game's own words head the fact in every language.
        /// </summary>
        public static string LabelOf(string text, string placeholder)
        {
            string s = text ?? string.Empty;
            int at = s.IndexOf("{" + placeholder + "}", StringComparison.Ordinal);
            if (at >= 0)
            {
                s = s.Substring(0, at);
            }
            var sb = new System.Text.StringBuilder(s.Length);
            bool tag = false;
            foreach (char ch in s)
            {
                if (ch == '<')
                {
                    tag = true;
                }
                else if (ch == '>')
                {
                    tag = false;
                }
                else if (!tag)
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString().TrimEnd(' ', ':', '\u00a0', '\uff1a').Trim();
        }

        /// <summary>A text's placeholders filled, as the game's and the mod's texts carry them in curly brackets.</summary>
        public static string Fill(string text, params string[] pairs)
        {
            string s = text ?? string.Empty;
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                s = s.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return s;
        }
    }
}
