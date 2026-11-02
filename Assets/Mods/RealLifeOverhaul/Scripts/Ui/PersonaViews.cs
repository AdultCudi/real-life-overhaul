using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One thing that lifts or lowers the player's happiness, as the game's Persona lists it (the game's and the mod's moods alike).</summary>
    public struct PersonaModifier
    {
        public string Name;          // in the game's words
        public int Amount;           // the percent it adds or takes
        public string Left;          // how long it lasts, in the game's words; null while it has no end
    }

    /// <summary>One course as the game's education box lists it: its name, its state in the game's words, and the group it stands in.</summary>
    public struct PersonaCourse
    {
        public string Name;
        public string Status;
        public PersonaCourseState State;
        public string Group;         // the group's name as its heading reads; the game's own diplomas have none
        public string GroupKey;      // the heading's key, for the group's glyph
    }

    /// <summary>Where a course stands, by the key of the game's or the mod's words for it.</summary>
    public enum PersonaCourseState : byte
    {
        NotStarted = 0,
        Under,       // classes, homework, a share done
        Waiting,     // a fee owed, an exam or a retake ahead
        Done,
        Stopped      // failed, withdrawn or barred
    }

    /// <summary>One of the game's tiered personal goals: the goal its next tier names, how far along, and the tiers reached here and on Steam.</summary>
    public struct PersonaGoal
    {
        public string Title;
        public string Progress;      // the game's words, or null where the goal has none
        public int Tiers;            // the tiers reached, 0 to 3
        public int Steam;            // the highest tier reached on Steam, -1 for none
        public string Kind;          // the game's kind of goal (its class: "BankBalanceGoal"), for its glyph
    }

    /// <summary>
    /// The player's Persona as the game's app filled it, in words and figures the game formed: name, age, businesses,
    /// staff, the worth's parts, the weekly income over the last days, the needs, what moves the happiness, the courses
    /// and the goals with the sort the game's toggles hold.
    /// </summary>
    public sealed class PersonaView
    {
        public int Tab;
        public string Name;
        public int Age;
        public int Businesses;
        public int Employees;
        public float Cash, Investments, Assets, Loans;
        public float WeeklyIncome;
        public float[] IncomeDays = new float[0];   // the weekly income on each of the last days, oldest first
        public int[] IncomeDayNumbers = new int[0]; // the day each of them was
        public Func<float, string> Money;           // a sum in the game's short money words
        public int Energy, Hunger, Happiness;       // 0 to 100
        public PersonaModifier[] Modifiers = new PersonaModifier[0];
        public PersonaCourse[] Courses = new PersonaCourse[0];
        public PersonaGoal[] Goals = new PersonaGoal[0];
        public int GoalSort;                        // the game's toggles: 0 none, 1 the current tier, 2 Steam's
        public bool GoalSortDown;
        public int Selected = -1;                   // the row the cursor is on in the tab's table
        public float Offset;
        public int Reveal = -1;
        public int IncomeFocus = -1;                // the income's point read in its card
    }

    /// <summary>
    /// The Persona view into the profile page's model: the tabs, the head, the overview's donut and line, the needs and
    /// what moves the happiness, the courses and the goals. Game-free: the game's screen and the samples hand it the same
    /// view. Cost: a few small arrays a layout.
    /// </summary>
    public static class PersonaViews
    {
        public const string App = "Persona";          // the game's name for the app, as the phone's looks know it
        public const int GoalsTiers = 3;              // the game's tiers a goal has: bronze, silver, gold

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Persona(UiPersonaModel m, UiWords t, PersonaView v)
        {
            m.Title = t("smartphone_persona", "Persona");
            m.Icon = PhoneLooks.Glyph(App);
            int done = Done(v.Courses), goalsDone = GoalsDone(v.Goals);
            m.Tabs = new[]
            {
                Tab(UiIcon.IdCard, t("reallife_ui_persona_overview", "Overview"), null),
                Tab(UiIcon.Heartbeat, t("reallife_ui_persona_needs", "Needs"), v.Modifiers.Length > 0 ? Whole(v.Modifiers.Length) : null),
                Tab(UiIcon.Cap, t("persona_education", "Education"), Whole(done) + "/" + Whole(v.Courses.Length)),
                Tab(UiIcon.Trophy, t("reallife_ui_persona_goals", "Goals"), Whole(goalsDone) + "/" + Whole(v.Goals.Length)),
            };
            m.Tab = Math.Max(0, Math.Min(m.Tabs.Length - 1, v.Tab));
            Head(m.Profile, t, v);
            Overview(m, t, v);
            m.Table = new UiDataTableModel();
            switch (m.Tab)
            {
                case UiPersona.Needs:
                    Modifiers(m, t, v);
                    break;
                case UiPersona.Education:
                    Courses(m, t, v, done);
                    break;
                case UiPersona.Goals:
                    GoalsTable(m, t, v, goalsDone);
                    break;
            }
            m.Table.Selected = -1;   // the tabs' lists are read only: the cursor walks and scrolls them, nothing stands chosen
            m.Table.Offset = v.Offset;
            m.Table.Reveal = v.Reveal;
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_staff_prompt_tab", "Tab") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            };
        }

        private static UiNavEntry Tab(UiIcon icon, string label, string count) => new UiNavEntry { Icon = icon, Tint = UiIconTints.Of(icon), Label = label, Count = count };

        // The head: the face in the ring of the player's happiness, the name and age, and the key facts: the worth, and the
        // businesses and staff once there are any (a new character's head is not a row of zeros).
        private static void Head(UiProfileModel p, UiWords t, PersonaView v)
        {
            p.Name = v.Name;
            p.Hue = UiColour.HueOf(v.Name ?? string.Empty);
            p.Ring = Mood(v.Happiness);
            p.Role = StaffViews.Fill(t("common_years_amount", "{years} years"), "years", Whole(v.Age));
            p.RoleIcon = UiIcon.Person;
            p.RoleTint = UiRole.None;
            p.Employer = null;
            p.Status = null;
            var labels = new List<string>(3) { StaffViews.LabelOf(t("persona_characterinfo_networth", "Total net worth: <b>{amount}</b>"), "amount") };
            var values = new List<string>(3) { Money(v, Worth(v)) };
            if (v.Businesses > 0)
            {
                labels.Add(t("persona_characterinfo_businesses", "Total businesses"));
                values.Add(Whole(v.Businesses));
            }
            if (v.Employees > 0)
            {
                labels.Add(t("persona_characterinfo_employees", "Total employees"));
                values.Add(Whole(v.Employees));
            }
            p.FactLabels = labels.ToArray();
            p.FactValues = values.ToArray();
            p.RatingLabel = null;
            p.Rating = -1;
        }

        /// <summary>The worth as the game adds it: cash, investments and assets, less the loans.</summary>
        public static float Worth(PersonaView v) => v.Cash + v.Investments - v.Loans + v.Assets;

        /// <summary>A face's ring: the happiness on the scale every mood ring uses.</summary>
        public static UiRole Mood(int happiness) => UiTokens.Grade(Math.Max(0, Math.Min(100, happiness)));

        // The overview: what is owned as slices, each part there only once there is some (a debt is no slice: it stands under
        // them), the weekly income's last days as a line, the goals nearest their next tier, and what moves the happiness most.
        private static void Overview(UiPersonaModel m, UiWords t, PersonaView v)
        {
            m.WealthTitle = t("persona_personal_wealth", "Personal wealth");
            var labels = new List<string>(3);
            var values = new List<string>(3);
            var shares = new List<float>(3);
            Part(labels, values, shares, t("persona_cash", "Cash"), v.Cash, v);
            Part(labels, values, shares, t("common_investments", "Investments"), v.Investments, v);
            Part(labels, values, shares, t("persona_assets", "Assets"), v.Assets, v);
            m.WealthLabels = labels.ToArray();
            m.WealthValues = values.ToArray();
            m.WealthShares = shares.ToArray();
            m.LoansLabel = t("econoview_loans", "Loans");
            m.LoansValue = v.Loans > 0f ? "-" + Money(v, v.Loans) : null;
            m.WealthEmpty = t("reallife_ui_persona_wealth_empty", "Nothing owned yet. Cash in hand, shares and the buildings you own make up your wealth.");
            Income(m.Income, t, v);
            Needs(m, t, v);
            m.MoodsTitle = t("reallife_ui_persona_modifiers", "What moves your happiness");
            m.MoodsEmpty = t("reallife_ui_persona_no_modifiers_body", "Food, rest, your home and what you do lift or lower it. Each shows here with how long it lasts.");
            m.Moods = TopMoods(v.Modifiers);
            m.MoodsMore = v.Modifiers.Length > m.Moods.Length
                ? StaffViews.Fill(t("reallife_ui_persona_all_moods", "See all {count} in Needs"), "count", Whole(v.Modifiers.Length)) : null;
            m.GoalsTitle = t("reallife_ui_persona_next_goals", "Goals on their way");
            m.GoalsEmpty = t("reallife_ui_persona_goals_done", "Every personal goal has all three tiers.");
            m.Goals = NextGoals(t, v.Goals);
            m.GoalsMore = v.Goals.Length > m.Goals.Length
                ? StaffViews.Fill(t("reallife_ui_persona_all_goals", "See all {count} in Goals"), "count", Whole(v.Goals.Length)) : null;
            m.GoalSteps = GoalsTiers;
        }

        private static void Part(List<string> labels, List<string> values, List<float> shares, string label, float amount, PersonaView v)
        {
            if (amount <= 0f)
            {
                return;
            }
            labels.Add(label);
            values.Add(Money(v, amount));
            shares.Add(amount);
        }

        /// <summary>What moves the happiness most, the largest share first, a few for the overview.</summary>
        public static UiPersonaLine[] TopMoods(PersonaModifier[] mods)
        {
            var order = new List<int>(mods.Length);
            for (int i = 0; i < mods.Length; i++)
            {
                order.Add(i);
            }
            order.Sort((a, b) => Math.Abs(mods[a].Amount) != Math.Abs(mods[b].Amount) ? Math.Abs(mods[b].Amount).CompareTo(Math.Abs(mods[a].Amount)) : a.CompareTo(b));
            int n = Math.Min(UiPersona.MostLines, order.Count);
            var lines = new UiPersonaLine[n];
            for (int i = 0; i < n; i++)
            {
                PersonaModifier mod = mods[order[i]];
                lines[i] = new UiPersonaLine { Words = mod.Name, Figure = Signed(mod.Amount), Sign = mod.Amount >= 0 ? 1 : -1 };
            }
            return lines;
        }

        /// <summary>The goals nearest their next tier (furthest through it first), those with every tier left out, a few for the overview.</summary>
        public static UiPersonaLine[] NextGoals(UiWords t, PersonaGoal[] goals)
        {
            var order = new List<int>(goals.Length);
            for (int i = 0; i < goals.Length; i++)
            {
                if (goals[i].Tiers < GoalsTiers)
                {
                    order.Add(i);
                }
            }
            order.Sort((a, b) =>
            {
                float sa = Math.Max(0f, Fraction(goals[a].Progress)), sb = Math.Max(0f, Fraction(goals[b].Progress));
                return sa != sb ? sb.CompareTo(sa) : a.CompareTo(b);
            });
            int n = Math.Min(UiPersona.MostLines, order.Count);
            var lines = new UiPersonaLine[n];
            for (int i = 0; i < n; i++)
            {
                PersonaGoal g = goals[order[i]];
                lines[i] = new UiPersonaLine
                {
                    Icon = GoalIcon(g.Kind), Words = g.Title, Figure = TierWords(t, g.Tiers), Reached = g.Tiers, Share = Math.Max(0f, Fraction(g.Progress))
                };
            }
            return lines;
        }

        /// <summary>
        /// How far a goal is through its tier from the game's progress words ("186,420 / 1,000,000"): the figures before and
        /// after the slash, their digits read whatever the language's separators; -1 where the words hold no such pair.
        /// </summary>
        public static float Fraction(string progress)
        {
            int slash = string.IsNullOrEmpty(progress) ? -1 : progress.IndexOf('/');
            if (slash < 0)
            {
                return -1f;
            }
            double done = Digits(progress, 0, slash), all = Digits(progress, slash + 1, progress.Length);
            return all > 0 && done >= 0 ? (float)Math.Max(0.0, Math.Min(1.0, done / all)) : -1f;
        }

        // The whole number in a stretch of words (a sum's or a count's digits), -1 with none: a separator between thousands
        // is passed over, a decimal part dropped.
        private static double Digits(string text, int from, int to)
        {
            double n = 0;
            bool any = false;
            for (int i = from; i < to; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    n = n * 10 + (c - '0');
                    any = true;
                }
                else if (any && (c == '.' || c == ',') && !(i + 3 < to && char.IsDigit(text[i + 1]) && char.IsDigit(text[i + 2]) && char.IsDigit(text[i + 3])
                                                            && (i + 4 >= to || !char.IsDigit(text[i + 4]))))
                {
                    break;
                }
            }
            return any ? n : -1;
        }

        /// <summary>A goal's glyph by the game's kind of goal; one the kit does not know the trophy.</summary>
        public static UiIcon GoalIcon(string kind)
        {
            switch (kind)
            {
                case "BankBalanceGoal": return UiIcon.Bank;
                case "BusinessWeeklyIncomeGoal": return UiIcon.ChartLineUp;
                case "EmploymentGoal": return UiIcon.People;
                case "EmployeeMaxLevelGoal": return UiIcon.IdBadge;
                case "VehicleGoal": case "CarRepairGoal": case "GasPaidGoal": return UiIcon.Car;
                case "TaxiRideGoal": return UiIcon.Navigation;
                case "RealEstateGoal": case "BuildingSizeGoal": return UiIcon.Building;
                case "BuildingRegistrationsGoal": return UiIcon.Store;
                case "ApartmentValueGoal": return UiIcon.Home;
                case "TaxesPaidGoal": case "ParkingTicketGoal": return UiIcon.Receipt;
                case "CasinoWinGoal": case "CasinoBoatVisitGoal": return UiIcon.Star;
                case "HospitalizationGoal": return UiIcon.Heartbeat;
                case "PlayerAgeGoal": return UiIcon.Calendar;
                case "UncleFredObjectivesGoal": return UiIcon.Lightbulb;
                case "InteriorDesignerPaidGoal": return UiIcon.PaintBrush;
                case "TotalDailyCustomerGoal": return UiIcon.ShoppingCart;
                case "ItemsInStockGoal": return UiIcon.Package;
                case "ValuationGoal": return UiIcon.ChartPie;
                case "BoatGoal": return UiIcon.Globe;
                default: return UiIcon.Trophy;
            }
        }

        private static string TierWords(UiWords t, int reached) => reached <= 0 ? null : reached == 1 ? t("reallife_ui_persona_bronze", "Bronze")
            : reached == 2 ? t("reallife_ui_persona_silver", "Silver") : t("reallife_ui_persona_gold", "Gold");

        private static void Needs(UiPersonaModel m, UiWords t, PersonaView v)
        {
            m.NeedIcons = new[] { UiIcon.Lightning, UiIcon.ForkKnife, UiIcon.SmileyHappy };
            m.NeedLabels = new[] { t("common_energy", "Energy"), t("common_hunger", "Hunger"), t("common_happiness", "Happiness") };
            m.NeedValues = new[] { Clamp(v.Energy), Clamp(v.Hunger), Clamp(v.Happiness) };
        }

        // The weekly income on each of the last days as a line, each point named by its day, the sums in the game's words.
        private static void Income(UiChartModel c, UiWords t, PersonaView v)
        {
            c.Kind = UiChartKind.Line;
            c.Title = t("common_weekly_income", "Weekly Income");
            int n = v.IncomeDays.Length;
            c.Points = new string[n];
            for (int i = 0; i < n; i++)
            {
                c.Points[i] = StaffViews.Fill(t("common_day_number", "Day {number}"), "number", Whole(i < v.IncomeDayNumbers.Length ? v.IncomeDayNumbers[i] : i + 1));
            }
            c.Reads = null;
            c.Series = new[] { new UiSeries { Label = c.Title, Values = v.IncomeDays, Tint = UiRole.Accent } };
            Func<float, string> money = v.Money ?? (f => f.ToString("0", Inv));
            c.Format = money;
            c.AxisFormat = money;
            c.Focus = v.IncomeFocus;
            c.EmptyTitle = t("reallife_ui_persona_income_empty", "No income yet");
            c.EmptyBody = t("reallife_ui_persona_income_empty_body", "Your businesses' takings over the last days draw here once they open.");
        }

        // What moves the happiness: a row each, its arrow and sign, its cause and how long it lasts, as the game lists them.
        private static void Modifiers(UiPersonaModel m, UiWords t, PersonaView v)
        {
            m.ListTitle = t("reallife_ui_persona_modifiers", "What moves your happiness");
            UiDataTableModel table = m.Table;
            table.Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_ui_persona_change", "Change"), Kind = UiCellKind.Trend, Width = 96f },
                new UiDataColumn { Title = t("reallife_ui_persona_cause", "Cause"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("reallife_ui_persona_lasts", "Lasts"), Kind = UiCellKind.Date, Width = 160f },
            };
            table.Rows = new UiDataRow[v.Modifiers.Length];
            for (int i = 0; i < v.Modifiers.Length; i++)
            {
                ref PersonaModifier mod = ref v.Modifiers[i];
                table.Rows[i] = new UiDataRow
                {
                    Cells = new[] { Signed(mod.Amount), mod.Name, mod.Left ?? t("reallife_ui_persona_ongoing", "Ongoing") },
                    Values = new[] { Math.Sign(mod.Amount), -1, -1 }
                };
            }
            table.RowCount = table.Rows.Length;
            int net = 0;
            for (int i = 0; i < v.Modifiers.Length; i++)
            {
                net += v.Modifiers[i].Amount;
            }
            table.Totals = v.Modifiers.Length > 1 ? new[] { Signed(net), t("reallife_ui_persona_net", "Altogether"), null } : null;   // one row needs no foot
            table.TotalsLabel = null;
            table.EmptyIcon = UiIcon.SmileyHappy;
            table.EmptyTitle = t("reallife_ui_persona_no_modifiers", "Nothing moves your happiness now");
            table.EmptyBody = t("reallife_ui_persona_no_modifiers_body", "Food, rest, your home and what you do lift or lower it. Each shows here with how long it lasts.");
        }

        /// <summary>A share as the game writes a modifier: its sign, then the percent.</summary>
        public static string Signed(int amount) => (amount >= 0 ? "+" : string.Empty) + amount.ToString(Inv) + "%";

        // The courses in the game's order: each with its group's glyph and name, and its state as a chip.
        private static void Courses(UiPersonaModel m, UiWords t, PersonaView v, int done)
        {
            m.ListTitle = null;   // the tab names the list and counts what is done
            UiDataTableModel table = m.Table;
            table.Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_ui_persona_course", "Course"), Kind = UiCellKind.Tile },
                new UiDataColumn { Title = t("reallife_ui_persona_kind", "Kind"), Kind = UiCellKind.Text, Width = 288f },
                new UiDataColumn { Title = t("bizman_status", "Status"), Kind = UiCellKind.Chip, Width = 224f },
            };
            string school = t("common_businessschool", "Manhattan Business School");
            table.Rows = new UiDataRow[v.Courses.Length];
            for (int i = 0; i < v.Courses.Length; i++)
            {
                ref PersonaCourse c = ref v.Courses[i];
                UiIcon glyph = GroupIcon(c.GroupKey);
                table.Rows[i] = new UiDataRow
                {
                    Cells = new[] { c.Name, string.IsNullOrEmpty(c.Group) ? school : c.Group, c.Status },
                    Values = new[] { (int)glyph, -1, -1 },
                    Tones = new[] { UiRole.None, UiRole.None, StateTone(c.State) }
                };
            }
            table.RowCount = table.Rows.Length;
            table.EmptyIcon = UiIcon.Cap;
            table.EmptyTitle = t("reallife_ui_persona_no_courses", "No courses to show");
            table.EmptyBody = t("reallife_ui_persona_no_courses_body", "The business school and the city's colleges list their courses here, each with how far along you are.");
        }

        /// <summary>A course group's glyph by its heading's key: the game's own diplomas the business school's briefcase.</summary>
        public static UiIcon GroupIcon(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return UiIcon.Briefcase;
            }
            int cut = key.LastIndexOf('_');
            switch (cut >= 0 ? key.Substring(cut + 1) : key)
            {
                case "degree": return UiIcon.Cap;
                case "postgrad": return UiIcon.Lightbulb;
                case "nursing": return UiIcon.Heartbeat;
                case "short": return UiIcon.Clock;
                case "diploma": return UiIcon.Presentation;
                case "online": return UiIcon.Globe;
                case "certificate": return UiIcon.IdBadge;
                default: return UiIcon.Cap;
            }
        }

        /// <summary>
        /// Where a course stands by the key of its status words (the game's, or the mod's for its own courses) and whether
        /// its tick shows: done, under way, waiting on a fee or an exam, stopped, or not started.
        /// </summary>
        public static PersonaCourseState StateOf(string key, bool ticked)
        {
            if (ticked || key == "persona_education_completed" || Ends(key, "_passed"))
            {
                return PersonaCourseState.Done;
            }
            if (string.IsNullOrEmpty(key) || key == "persona_education_not_started")
            {
                return PersonaCourseState.NotStarted;
            }
            if (Ends(key, "_fee") || Ends(key, "_exam") || Ends(key, "_retake"))
            {
                return PersonaCourseState.Waiting;
            }
            if (Ends(key, "_failed") || Ends(key, "_withdrawn") || Ends(key, "_barred"))
            {
                return PersonaCourseState.Stopped;
            }
            return PersonaCourseState.Under;
        }

        private static bool Ends(string key, string end) => key != null && key.EndsWith(end, StringComparison.Ordinal);

        /// <summary>A course's chip: done in the good tone, under way in the cyan, waiting on a step in amber, stopped in red.</summary>
        public static UiRole StateTone(PersonaCourseState state) => state == PersonaCourseState.Done ? UiRole.Ok : state == PersonaCourseState.Under ? UiRole.Accent
            : state == PersonaCourseState.Waiting ? UiRole.Warn : state == PersonaCourseState.Stopped ? UiRole.Danger : UiRole.Muted;

        // The goals in the game's order (its toggles sort it): the goal, how far along, the tiers as pips and Steam's medal.
        private static void GoalsTable(UiPersonaModel m, UiWords t, PersonaView v, int goalsDone)
        {
            m.ListTitle = null;   // the tab names the goals and counts those done, as the game's header does
            bool steam = false;
            for (int i = 0; i < v.Goals.Length; i++)
            {
                steam |= v.Goals[i].Steam >= 0 && v.Goals[i].Steam < GoalsTiers;
            }
            UiDataTableModel table = m.Table;
            var columns = new List<UiDataColumn>
            {
                new UiDataColumn { Title = t("reallife_ui_persona_goal", "Goal"), Kind = UiCellKind.Tile },
                new UiDataColumn { Title = t("reallife_ui_persona_progress", "Progress"), Kind = UiCellKind.Figure, Width = 200f },
                new UiDataColumn { Title = t("persona_personalgoal_current_goals", "Current Goals"), Kind = UiCellKind.Steps, Steps = GoalsTiers,
                    Width = UiDataCells.StepsWidth(GoalsTiers) + UiTokens.S8, Sortable = true },
            };
            if (steam)
            {
                // Steam's medals only once one is won: a column with nothing in it is a head over nothing.
                columns.Add(new UiDataColumn { Title = t("persona_personalgoal_steam_achievements", "Steam"), Kind = UiCellKind.Chip, Width = 120f, Sortable = true });
            }
            table.Columns = columns.ToArray();
            table.SortColumn = v.GoalSort == 1 ? 2 : v.GoalSort == 2 && steam ? 3 : -1;
            table.SortDown = v.GoalSortDown;
            UiRole[] medals = { UiRole.Chart2, UiRole.Text2, UiRole.Gold };
            table.Rows = new UiDataRow[v.Goals.Length];
            for (int i = 0; i < v.Goals.Length; i++)
            {
                ref PersonaGoal g = ref v.Goals[i];
                int reached = Math.Max(0, Math.Min(GoalsTiers, g.Tiers));
                bool won = g.Steam >= 0 && g.Steam < GoalsTiers;
                table.Rows[i] = new UiDataRow
                {
                    Cells = steam ? new[] { g.Title, g.Progress, TierWords(t, reached), won ? TierWords(t, g.Steam + 1) : null } : new[] { g.Title, g.Progress, TierWords(t, reached) },
                    Values = steam ? new[] { (int)GoalIcon(g.Kind), -1, reached, -1 } : new[] { (int)GoalIcon(g.Kind), -1, reached },
                    Tones = steam ? new[] { UiRole.None, UiRole.None, UiRole.None, won ? medals[g.Steam] : UiRole.None } : null,
                    Share = reached < GoalsTiers ? Math.Max(0f, Fraction(g.Progress)) : 0f
                };
            }
            table.RowCount = table.Rows.Length;
            table.EmptyIcon = UiIcon.Trophy;
            table.EmptyTitle = t("reallife_ui_persona_no_goals", "No goals to show");
            table.EmptyBody = t("reallife_ui_persona_no_goals_body", "The game's personal goals list here with their three tiers as you reach them.");
        }

        /// <summary>The courses done.</summary>
        public static int Done(PersonaCourse[] courses)
        {
            int n = 0;
            for (int i = 0; courses != null && i < courses.Length; i++)
            {
                n += courses[i].State == PersonaCourseState.Done ? 1 : 0;
            }
            return n;
        }

        /// <summary>The goals with every tier reached, as the game's header counts them.</summary>
        public static int GoalsDone(PersonaGoal[] goals)
        {
            int n = 0;
            for (int i = 0; goals != null && i < goals.Length; i++)
            {
                n += goals[i].Tiers >= GoalsTiers ? 1 : 0;
            }
            return n;
        }

        private static string Money(PersonaView v, float amount) => v.Money != null ? v.Money(amount) : amount.ToString("0", Inv);

        private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));

        private static string Whole(int n) => n.ToString(Inv);
    }
}
