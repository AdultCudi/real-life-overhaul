using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul.Construction
{
    /// <summary>The overview: the builder first, the next decision, the projects as stage tracks and the money, crews, planning and rivals beside them.</summary>
    public sealed partial class ConstructionLive
    {
        private const int WeekMost = 5, WeekDays = 14;

        // The projects still in hand are the rows, so that a press picks one and its page is written by the Projects tab's writer.
        partial void WriteOverview(KitBox box, BuildState state, ref string selected)
        {
            ProjectFile picked = null;
            for (int i = 0; i < state.Projects.Count; i++)
            {
                ProjectFile p = state.Projects[i];
                if (!InHand(p))
                {
                    continue;
                }
                TableRow row = Pick("prj:" + p.Id, ref selected);
                if (row.Id == selected)
                {
                    picked = p;
                }
            }
            if (picked != null)
            {
                WriteProject(box, state, picked);
            }
        }

        // A project the player still has something to do about: a live one, or a finished one with flats, a loan or its opening ahead.
        private static bool InHand(ProjectFile p)
        {
            return p.Live || (p.Stage == ProjectStage.Finished && (!p.Opened || p.LoanOwed > 0 || p.InvestorOwed > 0 || (p.After == AfterUse.SellFlats && p.FlatsSold < p.Flats)));
        }

        // Whether the project waits on the player now, the question the lead and the chip answer: a step only they can take.
        private bool NeedsYou(ProjectFile p)
        {
            switch (p.Stage)
            {
                case ProjectStage.Design:
                    return NextStep(p) != ConstructionText.T("reallife_bld_next_wait_plans") && NextStep(p) != ConstructionText.T("reallife_bld_next_wait_engineer");
                case ProjectStage.Planning:
                    return p.Planning == PlanningState.Refused || p.Planning == PlanningState.AppealLost;
                case ProjectStage.Ready:
                    return true;
                case ProjectStage.Building:
                    return !p.Hired && (ConstructionDirector.FailedInspection(p) >= 0 || ConstructionDirector.DueInspection(p) >= 0 || (!SiteRules.Supplied(p.Have, p.Need) && p.OrderDay == 0));
                case ProjectStage.Finished:
                    return (!p.Opened && p.Opening == ConstructionGame.Day) || p.LoanOwed > 0;
                default:
                    return false;
            }
        }

        private void ComposeOverview(ConstructionModel m, BuildState state)
        {
            FirmFile f = state.Firm;
            bool office = Director.Office(out PlaceView place);
            m.Shape = ConstructionShape.Overview;
            m.HeadIcon = UiIcon.Building;
            m.HeadTitle = office && f.Founded > 0 ? place.Name : ConstructionText.T("reallife_bld_glance_title");
            m.HeadMark = f.Founded > 0 && office ? Initials(place.Name) : null;
            int crewsOn = 0;
            for (int i = 0; i < state.Crews.Count; i++)
            {
                crewsOn += state.Crews[i].Project > 0 ? 1 : 0;
            }
            if (f.Founded > 0 && office)
            {
                m.HeadSub = ConstructionText.T("reallife_ui_cons_head_sub", "floors", Director.FloorsLicensed.ToString(Inv), "crews", state.Crews.Count.ToString(Inv),
                    "address", ConstructionGame.AddressText(place.Street, place.Number));
                m.FigLabels = new[] { ConstructionText.T("reallife_ui_cons_fig_worth"), ConstructionText.T("reallife_ui_cons_fig_under_way"),
                    ConstructionText.T("reallife_ui_cons_fig_on_site"),
                    ConstructionText.T("reallife_ui_cons_fig_done") };
                m.FigValues = new[] { ConstructionText.Money(Director.Worth()), state.Building().ToString(Inv), ConstructionText.T("reallife_ui_cons_x_of_y", "n", crewsOn.ToString(Inv), "of",
                    state.Crews.Count.ToString(Inv)), f.Finished.ToString(Inv) };
            }
            else
            {
                m.HeadSub = ConstructionText.T("reallife_bld_glance_client");
            }
            m.Lead = Lead(state, f, office, place);
            m.RowsTitle = ConstructionText.T("reallife_ui_cons_projects_title");
            m.RowsEmpty = ConstructionText.T("reallife_bld_empty_projects");
            m.RowsLink = state.Projects.Count > 0 ? ConstructionText.T("reallife_ui_cons_all_projects") : null;
            m.RowsLinkRun = state.Projects.Count > 0 ? _runs.Do(() => ShowTab(ConstructionDirector.TabProjects)) : -1;
            var projects = new List<ConstructionProject>(8);
            for (int i = 0; i < state.Projects.Count; i++)
            {
                ProjectFile p = state.Projects[i];
                if (InHand(p))
                {
                    projects.Add(ProjectRow(p));
                }
            }
            m.Projects = projects.ToArray();
            m.Side = Side(state, f);
            m.WeekTitle = ConstructionText.T("reallife_ui_cons_week_title");
            m.Week = Week(state);
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            string[] words = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string a = words.Length > 0 && words[0].Length > 0 ? words[0].Substring(0, 1) : string.Empty;
            string b = words.Length > 1 && words[1].Length > 0 ? words[1].Substring(0, 1) : words.Length > 0 && words[0].Length > 1 ? words[0].Substring(1, 1) : string.Empty;
            return (a + b).ToUpperInvariant();
        }

        private ConstructionProject ProjectRow(ProjectFile p)
        {
            string id = "prj:" + p.Id;
            int at = p.Stage == ProjectStage.Finished ? StepKeys.Length : Math.Min((int)p.Stage, StepKeys.Length - 1);
            var labels = new string[StepKeys.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = ConstructionText.T(StepKeys[i]);
            }
            bool needs = NeedsYou(p);
            CellTone tone = NextTone(p);
            return new ConstructionProject
            {
                Icon = UiIcon.Hammer, Title = Director.ProjectName(p), Sub = ConstructionText.StageWords(p), StepLabels = labels, StepAt = at,
                Progress = p.Stage == ProjectStage.Building ? (int)Math.Round(Progress(p) * 100.0) : -1,
                Spent = ConstructionText.Money(p.Spent), Of = p.Estimate > 0 ? ConstructionText.T("reallife_ui_cons_of", "amount", ConstructionText.Money(p.Estimate)) : null,
                Chip = NextStep(p), ChipTone = tone == CellTone.Red ? UiRole.Danger : tone == CellTone.Yellow || needs
                    ? UiRole.Warn : p.Stage == ProjectStage.Finished ? UiRole.Ok : UiRole.Muted,
                Run = _runs.Do(() => Jump(ConstructionDirector.TabProjects, id))
            };
        }

        // The next decision: the first project that waits on the player, or the firm's own start (found it, the licence) before there are any.
        private ConstructionLead Lead(BuildState state, FirmFile f, bool office, PlaceView place)
        {
            if (f.Founded <= 0)
            {
                var actions = new List<UiActionModel>(3);
                if (office)
                {
                    actions.Add(_runs.Button(ConstructionText.T("reallife_bld_button_found"), UiButtonKind.Primary, UiIcon.None, UiAction.Alt, () => Answer(Director.Found())));
                }
                actions.Add(_runs.Button(ConstructionText.T("reallife_bld_button_land"), UiButtonKind.Secondary, UiIcon.None, office ? UiAction.Option : UiAction.Alt,
                    () => ShowTab(ConstructionDirector.TabLand)));
                BuildModeAction(actions);
                return new ConstructionLead
                {
                    Icon = UiIcon.Briefcase, Kicker = ConstructionText.T("reallife_ui_cons_lead_start"),
                    Title = office ? ConstructionText.T("reallife_bld_glance_found", "name", place.Name) : ConstructionText.T("reallife_bld_glance_no_office"),
                    Body = null, Actions = actions.ToArray()
                };
            }
            if (f.Licence != LicenceState.Granted)
            {
                var actions = new List<UiActionModel>(2);
                if (f.Licence != LicenceState.Applied)
                {
                    actions.Add(_runs.Button(ConstructionText.T("reallife_bld_button_city_hall"), UiButtonKind.Primary, UiIcon.None, UiAction.Alt, () => PlanningCall.Open(0)));
                }
                BuildModeAction(actions);
                return new ConstructionLead
                {
                    Icon = UiIcon.Scales, Kicker = ConstructionText.T("reallife_ui_cons_lead_licence"), Title = ConstructionText.LicenceWords(f),
                    Body = ConstructionText.T(f.Licence == LicenceState.Applied ? "reallife_bld_glance_licence_waits" : "reallife_bld_glance_licence"), Actions = actions.ToArray()
                };
            }
            for (int i = 0; i < state.Projects.Count; i++)
            {
                ProjectFile p = state.Projects[i];
                if (!InHand(p) || !NeedsYou(p))
                {
                    continue;
                }
                string id = "prj:" + p.Id;
                var next = new List<UiActionModel>(2)
                {
                    _runs.Button(ConstructionText.T("reallife_ui_cons_open"), UiButtonKind.Primary, UiIcon.None, UiAction.Alt, () => Jump(ConstructionDirector.TabProjects, id))
                };
                BuildModeAction(next, UiAction.Option);
                return new ConstructionLead
                {
                    Icon = UiIcon.Hammer, Kicker = ConstructionText.T("reallife_ui_cons_lead_next", "project", Director.ProjectName(p)),
                        Title = NextStep(p), Body = ConstructionText.StageWords(p),
                    Actions = next.ToArray()
                };
            }
            // Nothing waits on the player: the band offers the city map, where the next building is placed.
            var build = new List<UiActionModel>(1);
            BuildModeAction(build, UiAction.Alt, UiButtonKind.Primary);
            return build.Count == 0 ? null : new ConstructionLead
            {
                Icon = UiIcon.Building, Kicker = ConstructionText.T("reallife_ui_cons_lead_free"),
                    Title = ConstructionText.T("reallife_ui_cons_build_title"), Body = ConstructionText.T("reallife_ui_cons_build_note"),
                Actions = build.ToArray()
            };
        }

        private void BuildModeAction(List<UiActionModel> actions, UiAction key = UiAction.Count, UiButtonKind kind = UiButtonKind.Secondary)
        {
            if (!Settings.ConstructionBuildMode)
            {
                return;
            }
            actions.Add(_runs.Button(ConstructionText.T("reallife_bld_button_build_mode"), kind, UiIcon.None, key, () =>
            {
                string why = Director.OpenBuildMode();
                if (why != null)
                {
                    Answer(why);
                }
            }));
        }

        // The cards beside the projects: what the jobs cost and still cost, the crews, what waits on the council, and the rival builders.
        private UiFactCardModel[] Side(BuildState state, FirmFile f)
        {
            var cards = new List<UiFactCardModel>(5);
            long budget = 0, spent = 0, owed = 0;
            int jobs = 0;
            for (int i = 0; i < state.Projects.Count; i++)
            {
                ProjectFile p = state.Projects[i];
                owed += p.LoanOwed + p.InvestorOwed;
                if (!p.Live)
                {
                    continue;
                }
                jobs++;
                budget += Math.Max(p.Estimate, p.ClientValue);
                spent += p.Spent;
            }
            if (jobs > 0 || owed > 0)
            {
                long cash = (long)ConstructionGame.Cash;
                var lines = new List<UiFactLine>(5)
                {
                    UiFactLine.Row(ConstructionText.T("reallife_ui_cons_budgets", "count", jobs.ToString(Inv)), ConstructionText.Money(budget)),
                    UiFactLine.Row(ConstructionText.T("reallife_ui_cons_spent_far"), ConstructionText.Money(spent)),
                    UiFactLine.Row(ConstructionText.T("reallife_ui_cons_still_pay"), ConstructionText.Money(Math.Max(0L, budget - spent)), UiRole.WarnText),
                    UiFactLine.Row(ConstructionText.T("reallife_ui_cons_cash_held"), ConstructionText.Money(cash), cash >= budget - spent ? UiRole.OkText : UiRole.WarnText)
                };
                if (owed > 0)
                {
                    lines.Add(UiFactLine.Row(ConstructionText.T("reallife_ui_cons_loans_owed"), ConstructionText.Money(owed), UiRole.DangerText));
                }
                cards.Add(new UiFactCardModel { Title = ConstructionText.T("reallife_ui_cons_money_title"), Heading = true, Lines = lines.ToArray() });
            }
            if (state.Crews.Count > 0)
            {
                var lines = new List<UiFactLine>(4);
                for (int i = 0; i < state.Crews.Count && i < 3; i++)
                {
                    CrewFile c = state.Crews[i];
                    ProjectFile on = state.Project(c.Project);
                    lines.Add(UiFactLine.Who(Director.ForemanWords(c), on != null ? Director.ProjectName(on) : ConstructionText.T("reallife_bld_crews_at_yard"),
                        ConstructionText.T("reallife_ui_cons_workers", "count", PeopleIn(c).ToString(Inv))));
                }
                cards.Add(new UiFactCardModel { Title = ConstructionText.T("reallife_ui_cons_crews_title"), Heading = true, Lines = lines.ToArray() });
            }
            var plan = new List<UiFactLine>(3);
            for (int i = 0; i < state.Projects.Count && plan.Count < 3; i++)
            {
                ProjectFile p = state.Projects[i];
                if (p.Stage == ProjectStage.Planning)
                {
                    plan.Add(UiFactLine.Row(Director.ProjectName(p), p.Planning == PlanningState.Submitted && p.DecideDay > 0
                        ? ConstructionText.Day(p.DecideDay) : ConstructionText.StageWords(p),
                        p.Planning == PlanningState.Refused || p.Planning == PlanningState.AppealLost ? UiRole.DangerText : UiRole.None));
                }
            }
            if (plan.Count > 0)
            {
                cards.Add(new UiFactCardModel { Title = ConstructionText.T("reallife_ui_cons_planning_title"), Heading = true, Lines = plan.ToArray() });
            }
            var order = new int[BuildCatalogue.RivalCount];
            for (int r = 0; r < order.Length; r++)
            {
                order[r] = r;
            }
            Array.Sort(order, (a, b) => state.Rivals[b].Reputation.CompareTo(state.Rivals[a].Reputation));
            var rivals = new List<UiFactLine>(3);
            for (int i = 0; i < 2 && i < order.Length; i++)
            {
                int r = order[i];
                rivals.Add(UiFactLine.Who(ConstructionText.T(BuildCatalogue.RivalKeys[r]),
                    ConstructionText.T(BuildCatalogue.SpecialityKeys[(int)BuildCatalogue.RivalSpeciality[r]]),
                    ConstructionText.T("reallife_ui_cons_rep", "n", state.Rivals[r].Reputation.ToString(Inv))));
            }
            cards.Add(new UiFactCardModel { Title = ConstructionText.T("reallife_ui_cons_rivals_title"), Heading = true, Lines = rivals.ToArray() });
            return cards.ToArray();
        }

        private static int PeopleIn(CrewFile c)
        {
            int n = 0;
            int[] people = c.People;
            for (int i = 0; i < people.Length; i++)
            {
                n += people[i];
            }
            return n;
        }

        // What is coming to the sites: the deliveries, the inspector's visits, the council's decisions, the plans, the tenders that close and the openings, soonest first.
        private ConstructionDay[] Week(BuildState state)
        {
            int today = ConstructionGame.Day;
            var list = new List<KeyValuePair<int, ConstructionDay>>(8);
            for (int i = 0; i < state.Projects.Count; i++)
            {
                ProjectFile p = state.Projects[i];
                if (!InHand(p))
                {
                    continue;
                }
                string name = Director.ProjectName(p), id = "prj:" + p.Id;
                if (p.OrderDay >= today && p.OrderDay - today <= WeekDays)
                {
                    list.Add(Dated(p.OrderDay, ConstructionText.T("reallife_ui_cons_wk_delivery", "project", name, "merchant",
                        ConstructionText.T(BuildCatalogue.MerchantKeys[p.Merchant])), null, id));
                }
                for (int k = 0; k < (int)Inspection.Count; k++)
                {
                    if (p.Insp[k] == InspectionState.Booked && p.InspDay[k] >= today && p.InspDay[k] - today <= WeekDays)
                    {
                        list.Add(Dated(p.InspDay[k], ConstructionText.T("reallife_ui_cons_wk_inspect", "what",
                            ConstructionText.T(BuildCatalogue.InspectionKeys[k]), "project", name),
                            ConstructionText.T("reallife_ui_cons_no_cost"), id));
                    }
                }
                if (p.Planning == PlanningState.Submitted && p.DecideDay >= today && p.DecideDay - today <= WeekDays)
                {
                    list.Add(Dated(p.DecideDay, ConstructionText.T("reallife_ui_cons_wk_council", "project", name), null, id));
                }
                if (p.PlansDay >= today && !p.PlansDone && p.PlansDay - today <= WeekDays)
                {
                    list.Add(Dated(p.PlansDay, ConstructionText.T("reallife_ui_cons_wk_plans", "project", name), null, id));
                }
                if (p.Stage == ProjectStage.Finished && !p.Opened && p.Opening >= today && p.Opening - today <= WeekDays)
                {
                    list.Add(Dated(p.Opening, ConstructionText.T("reallife_ui_cons_wk_opening", "project", name), null, id));
                }
            }
            for (int i = 0; i < state.Tenders.Count; i++)
            {
                TenderFile t = state.Tenders[i];
                int closes = t.Closes / 24;
                if (t.State == TenderState.Open && closes >= today && closes - today <= WeekDays)
                {
                    int tender = t.Id;
                    list.Add(new KeyValuePair<int, ConstructionDay>(closes, new ConstructionDay
                    {
                        When = ConstructionText.Day(closes), What = ConstructionText.T("reallife_ui_cons_wk_tender", "works",
                            Director.TenderName(t)), Amount = ConstructionText.Money(t.Value),
                        Run = _runs.Do(() => Jump(ConstructionDirector.TabTenders, "tnd:" + tender))
                    }));
                }
            }
            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            var week = new ConstructionDay[Math.Min(WeekMost, list.Count)];
            for (int i = 0; i < week.Length; i++)
            {
                week[i] = list[i].Value;
            }
            return week;
        }

        private KeyValuePair<int, ConstructionDay> Dated(int day, string what, string amount, string pick)
        {
            return new KeyValuePair<int, ConstructionDay>(day, new ConstructionDay
            {
                When = ConstructionText.Day(day), What = what, Amount = amount, Run = _runs.Do(() => Jump(ConstructionDirector.TabProjects, pick))
            });
        }
    }
}
