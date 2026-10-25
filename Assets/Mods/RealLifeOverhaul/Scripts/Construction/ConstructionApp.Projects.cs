using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>The Projects tab: every project in the table, and the one picked in the white box by where it stands.</summary>
    public sealed partial class ConstructionApp
    {
        private readonly List<DesignChoice> _designs = new List<DesignChoice>(32);
        private readonly List<StaffView> _staff = new List<StaffView>(8);
        private int _merchant, _investor, _offPlan = 1;

        partial void WriteProjects(InfoBox box, BuildState state, ref string selected)
        {
            ProjectFile picked = null;
            for (int i = 0; i < state.Projects.Count; i++)
            {
                ProjectFile p = state.Projects[i];
                TableRow row = Pick("prj:" + p.Id, ref selected);
                row.Set(0, _director.ProjectName(p));
                row.Set(1, ConstructionText.StageWords(p));
                double progress = Progress(p);
                row.Set(2, p.Stage == ProjectStage.Building ? Mathf.RoundToInt((float)progress * 100f) + "%" : string.Empty, progress);
                row.Set(3, ConstructionText.Money(p.Spent), p.Spent);
                row.Set(4, NextStep(p), NextTone(p));
                if (row.Id == selected)
                {
                    picked = p;
                }
            }
            if (picked != null)
            {
                WriteProject(box, state, picked);
                return;
            }
            box.Title(ConstructionText.T("reallife_bld_tab_projects"));
            box.Note(ConstructionText.T(state.Projects.Count == 0 ? "reallife_bld_projects_none" : "reallife_bld_projects_pick"));
            Guide(box);
        }

        private double Progress(ProjectFile p)
        {
            if (p.Stage != ProjectStage.Building || p.SiteId <= 0)
            {
                return p.Stage == ProjectStage.Finished ? 1.0 : 0.0;
            }
            SiteFacts site = ConstructionDirector.SiteOf(p.SiteId);
            return site.Known && site.Hours > 0f ? Mathf.Clamp01(site.Done / site.Hours) : 0.0;
        }

        // What the player is to do next about a project, in a few words.
        private string NextStep(ProjectFile p)
        {
            switch (p.Stage)
            {
                case ProjectStage.Design:
                    return ConstructionText.T(!p.PlansDone ? p.PlansDay > 0 ? "reallife_bld_next_wait_plans" : "reallife_bld_next_plans"
                        : !p.Engineered && PlansRules.NeedsEngineer(p.Storeys, p.Kind) ? p.EngineerDay > 0 ? "reallife_bld_next_wait_engineer" : "reallife_bld_next_engineer"
                        : "reallife_bld_next_apply");
                case ProjectStage.Planning:
                    return ConstructionText.T(p.Planning == PlanningState.Refused ? "reallife_bld_next_refused"
                        : p.Planning == PlanningState.AppealLost ? "reallife_bld_next_redraw" : "reallife_bld_next_wait_decision");
                case ProjectStage.Ready:
                    return ConstructionText.T(_director.StartBlock(p) == null ? "reallife_bld_next_start" : ConstructionDirector.HireKind(p) && _director.HireBlock(p) == null
                        ? "reallife_bld_next_hire" : "reallife_bld_next_prepare");
                case ProjectStage.Building when p.Hired:
                    return ConstructionText.T("reallife_bld_next_builder");
                case ProjectStage.Building:
                {
                    int due = ConstructionDirector.DueInspection(p), failed = ConstructionDirector.FailedInspection(p);
                    return ConstructionText.T(failed >= 0 ? "reallife_bld_next_failed_short" : due >= 0 ? "reallife_bld_next_book_short"
                        : !SiteRules.Supplied(p.Have, p.Need) && p.OrderDay == 0 ? "reallife_bld_next_order_short" : "reallife_bld_next_working");
                }
                case ProjectStage.Finished:
                    return ConstructionText.T(!p.Opened && p.Opening == ConstructionGame.Day ? "reallife_bld_next_opening" : p.LoanOwed > 0 ? "reallife_bld_next_loan" : "reallife_bld_next_done");
                default:
                    return string.Empty;
            }
        }

        private static CellTone NextTone(ProjectFile p)
        {
            if (p.Stage == ProjectStage.Building && (ConstructionDirector.FailedInspection(p) >= 0 || ConstructionDirector.DueInspection(p) >= 0))
            {
                return CellTone.Yellow;
            }
            return p.Planning == PlanningState.Refused || p.Planning == PlanningState.AppealLost ? CellTone.Red : CellTone.Plain;
        }

        private void WriteProject(InfoBox box, BuildState state, ProjectFile p)
        {
            box.Title(_director.ProjectName(p));
            box.Subtitle(ConstructionText.StageWords(p));
            if (p.Contractor >= 0 && !p.Hired)
            {
                WriteContracted(box, p);
                return;
            }
            if (_page == 3 && p.Stage == ProjectStage.Ready && ConstructionDirector.HireKind(p))
            {
                WriteQuotes(box, p);   // ConstructionApp.Hire.cs
                return;
            }
            if (_page == 1 && (p.Stage == ProjectStage.Ready || p.Stage == ProjectStage.Building || p.Stage == ProjectStage.Finished))
            {
                WriteMoney(box, state, p);
                return;
            }
            if (_page == 2 && (p.Stage == ProjectStage.Ready || p.Stage == ProjectStage.Building || p.Stage == ProjectStage.Finished))
            {
                WriteHedge(box, p);   // ConstructionApp.Hedge.cs
                return;
            }
            switch (p.Stage)
            {
                case ProjectStage.Design:
                    WritePlans(box, state, p);
                    break;
                case ProjectStage.Planning:
                    WritePlanning(box, p);
                    break;
                case ProjectStage.Ready:
                    WriteReady(box, state, p);
                    break;
                case ProjectStage.Building when p.Hired:
                    WriteHired(box, p);   // ConstructionApp.Hire.cs
                    break;
                case ProjectStage.Building:
                    WriteSite(box, state, p);
                    break;
                case ProjectStage.Finished:
                    WriteFinished(box, state, p);
                    break;
                default:
                    box.Note(ConstructionText.T("reallife_bld_project_abandoned"));
                    BackButton(box);
                    break;
            }
        }

        // ------------------------------------------------------------------ the plans

        private void WritePlans(InfoBox box, BuildState state, ProjectFile p)
        {
            bool asked = p.PlansDay > 0 && !p.PlansDone;
            box.Section("reallife_bld_sec_plans");
            if (p.Kind == ProjectKind.Plot && !asked && !p.PlansDone)
            {
                LotFile lot = state.Lot(p.Lot);
                int zones = lot != null ? _director.ZonesNow(lot) : 15;
                var uses = new List<BuildUse>(4);
                for (int u = 0; u < (int)BuildUse.Count; u++)
                {
                    if (LandRules.Zoned(zones, (BuildUse)u))
                    {
                        uses.Add((BuildUse)u);
                    }
                }
                int at = Math.Max(0, uses.IndexOf(p.Use));
                if (uses.Count > 1)
                {
                    box.Selector(uses.Count, at, i => ConstructionText.T("reallife_bld_sel_use", "use", ConstructionText.T(BuildCatalogue.UseKeys[(int)uses[i]])),
                        i => Answer(_director.SetUse(p, uses[i])));
                }
                else
                {
                    box.Row("reallife_bld_row_use", ConstructionText.T(BuildCatalogue.UseKeys[(int)p.Use]));
                }
            }
            else
            {
                box.Row("reallife_bld_row_use", ConstructionText.T(BuildCatalogue.UseKeys[(int)p.Use]));
            }
            bool ready = _director.DesignChoices(p, _designs);
            if (!ready)
            {
                box.Note(ConstructionText.T("reallife_bld_designs_wait"));
            }
            else if (_designs.Count == 0)
            {
                box.Line(ConstructionText.T("reallife_bld_designs_none"), CellTone.Red);
            }
            else if (!asked && !p.PlansDone)
            {
                int at = 0;
                for (int i = 0; i < _designs.Count; i++)
                {
                    at = _designs[i].Name == p.Design ? i : at;
                }
                int count = _designs.Count;
                box.Selector(count, at, i => ConstructionText.Fill("reallife_bld_sel_design", "number", (i + 1).ToString(), "count", count.ToString(),
                        "storeys", PlansRules.Storeys(_designs[i].Height).ToString(), "area", ConstructionText.N(p.Kind == ProjectKind.Rebuild ? p.Area : Math.Max(1, _designs[i].TotalSqm))),
                    i => Answer(_director.SetDesign(p, i)));
            }
            box.RowPair("reallife_bld_row_area", ConstructionText.T("reallife_bld_value_area", "area", ConstructionText.N(p.Area)), "reallife_bld_row_storeys", ConstructionText.N(p.Storeys));
            WithRefitSelector(box, p);
            if (!asked && !p.PlansDone)
            {
                ArchitectSelector(box, p);
                box.Selector(2, p.ParkingUnder ? 0 : 1, i => ConstructionText.T(i == 0 ? "reallife_bld_sel_parking_under"
                    : "reallife_bld_sel_parking_none"), i => { _director.SetParking(p, i == 0); Refresh(); });
            }
            CostSheet sheet = _director.Preview(p, out int exits, out int parking, out int limit, out bool engineer);
            box.Section("reallife_bld_sec_requirements");
            box.Check(ConstructionText.T("reallife_bld_req_exits", "count", exits.ToString()), true);
            box.Check(ConstructionText.T(p.ParkingUnder || parking == 0 ? "reallife_bld_req_parking_met" : "reallife_bld_req_parking_short", "count",
                parking.ToString()), p.ParkingUnder || parking == 0);
            box.Check(ConstructionText.T("reallife_bld_req_height", "storeys", p.Storeys.ToString(), "limit", limit.ToString()), p.Storeys <= limit);
            if (engineer)
            {
                box.Check(ConstructionText.T(p.Engineered ? "reallife_bld_req_engineer_met" : "reallife_bld_req_engineer"), p.Engineered);
            }
            box.Row(p.PlansDone ? "reallife_bld_row_estimate" : "reallife_bld_row_estimate_about", ConstructionText.Money(p.PlansDone ? p.Estimate : sheet.Total));
            if (p.Kind == ProjectKind.Plot)
            {
                long value = _director.ValueOf(p, out _);
                if (value > 0)
                {
                    box.Row("reallife_bld_row_value", ConstructionText.Money(value));
                }
            }
            Transform buttons = box.ButtonRow();
            if (!p.PlansDone)
            {
                if (asked)
                {
                    box.Note(p.Architect == ArchitectKind.Player ? ConstructionText.T("reallife_bld_plans_yours")
                        : ConstructionText.T("reallife_bld_plans_coming", "day", ConstructionText.Day(p.PlansDay)));
                }
                else
                {
                    PlansQuote quote = _director.QuoteOf(p);
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_draw", () =>
                        Confirm("reallife_bld_confirm_plans", "reallife_bld_confirm_plans_body", _director.ProjectName(p), quote.Fee, () => _director.DrawPlans(p), false));
                }
            }
            else if (engineer && !p.Engineered)
            {
                if (p.EngineerDay > 0)
                {
                    box.Note(ConstructionText.T("reallife_bld_engineer_coming", "day", ConstructionText.Day(p.EngineerDay)));
                }
                else
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_engineer_firm", () => Answer(_director.AskEngineer(p, false)));
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_engineer_staff", () => Answer(_director.AskEngineer(p, true)));
                }
            }
            else
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_city_hall", () => PlanningCall.Open(p.Id));
                box.Note(ConstructionText.T("reallife_bld_apply_note", "fee", ConstructionText.Money(_director.PlanningFee(p))));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            AbandonRow(box, p);
            Guide(box);
        }

        // The architect: a practice, an architect on the staff, or the player once their training lets them.
        private void ArchitectSelector(InfoBox box, ProjectFile p)
        {
            var labels = new List<string>(6);
            var kinds = new List<ArchitectKind>(6);
            var firms = new List<int>(6);
            var ids = new List<string>(6);
            for (int f = 0; f < BuildCatalogue.FirmCount; f++)
            {
                labels.Add(ConstructionText.T("reallife_bld_sel_firm", "firm", ConstructionText.T(BuildCatalogue.FirmKeys[f]), "fee", ConstructionText.Share(BuildCatalogue.FirmFeePermille[f]),
                    "quality", BuildCatalogue.FirmQuality[f].ToString()));
                kinds.Add(ArchitectKind.Firm);
                firms.Add(f);
                ids.Add(null);
            }
            if (_director.Office(out PlaceView office))
            {
                ConstructionGame.Staff(office.Street, office.Number, _staff);
                for (int i = 0; i < _staff.Count; i++)
                {
                    if (_director.RoleOf(_staff[i]) != StaffRole.Architect)
                    {
                        continue;
                    }
                    labels.Add(ConstructionText.T("reallife_bld_sel_staff_architect", "name", _staff[i].Name, "skill", _staff[i].Value.ToString()));
                    kinds.Add(ArchitectKind.Staff);
                    firms.Add(0);
                    ids.Add(_staff[i].Id);
                }
            }
            if (ConstructionDirector.Qualified("architect"))
            {
                labels.Add(ConstructionText.T("reallife_bld_sel_player_architect"));
                kinds.Add(ArchitectKind.Player);
                firms.Add(0);
                ids.Add(null);
            }
            int at = 0;
            for (int i = 0; i < kinds.Count; i++)
            {
                if (kinds[i] == p.Architect && (p.Architect != ArchitectKind.Firm || firms[i] == p.Firm) && (p.Architect != ArchitectKind.Staff || ids[i] == p.ArchitectId))
                {
                    at = i;
                }
            }
            box.Selector(labels.Count, at, i => labels[i], i => Answer(_director.SetArchitect(p, kinds[i], firms[i], ids[i])));
        }

        // ------------------------------------------------------------------ planning

        private void WritePlanning(InfoBox box, ProjectFile p)
        {
            box.Section("reallife_bld_sec_planning");
            box.Row("reallife_bld_row_applied", ConstructionText.Day(p.PlanningDay));
            Transform buttons = box.ButtonRow();
            switch (p.Planning)
            {
                case PlanningState.Submitted:
                    box.Row("reallife_bld_row_decision", ConstructionText.Day(p.DecideDay));
                    box.Row("reallife_bld_row_objections", p.Consulted ? p.Objections.ToString() : ConstructionText.T("reallife_bld_value_not_yet"));
                    break;
                case PlanningState.Appealed:
                    box.Line(ConstructionText.T("reallife_bld_refused_why", "why", ConstructionText.Reasons(p.Reasons)), CellTone.Yellow);
                    box.Row("reallife_bld_row_hearing", ConstructionText.Day(p.AppealDay));
                    break;
                case PlanningState.Refused:
                    box.Line(ConstructionText.T("reallife_bld_refused_why", "why", ConstructionText.Reasons(p.Reasons)), CellTone.Red);
                    box.Row("reallife_bld_row_objections", p.Objections.ToString());
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_appeal", () =>
                        Confirm("reallife_bld_confirm_appeal", "reallife_bld_confirm_appeal_body", _director.ProjectName(p), (long)BuildCatalogue.AppealFee * ConstructionGame.Prices / 1000L,
                            () => _director.Appeal(p), false));
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_redraw", () => Answer(_director.Redraw(p)));
                    break;
                case PlanningState.AppealLost:
                    box.Line(ConstructionText.T("reallife_bld_appeal_lost"), CellTone.Red);
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_redraw", () => Answer(_director.Redraw(p)));
                    break;
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            AbandonRow(box, p);
            Guide(box);
        }

        // ------------------------------------------------------------------ ready to build

        private void WriteReady(InfoBox box, BuildState state, ProjectFile p)
        {
            box.Section("reallife_bld_sec_ready");
            box.Row("reallife_bld_row_estimate", ConstructionText.Money(p.Estimate));
            if (p.Kind == ProjectKind.Contract)
            {
                box.Row("reallife_bld_row_contract", ConstructionText.Money(p.ClientValue));
            }
            bool company = state.Firm.Founded > 0;
            bool hire = ConstructionDirector.HireKind(p);
            if (company)
            {
                box.Row("reallife_bld_row_crews", state.CrewsOn(p.Id).ToString());
            }
            if (p.Kind == ProjectKind.Plot && p.Use == BuildUse.Homes)
            {
                box.Selector(2, (int)p.After, i => ConstructionText.T(i == 0 ? "reallife_bld_sel_keep" : "reallife_bld_sel_sell_flats"), i => Answer(_director.SetAfter(p, (AfterUse)i)));
            }
            WithRefitSelector(box, p);
            FitSelector(box, p);
            if (p.Kind == ProjectKind.Refit || (p.Kind == ProjectKind.Rebuild && p.WithRefit))
            {
                box.Note(ConstructionText.T(p.Kind == ProjectKind.Refit ? "reallife_bld_refit_ready_note" : "reallife_bld_refit_rebuild_note"));
            }
            string block = _director.StartBlock(p);
            if (company)
            {
                ManagerRow(box, state, p);
                if (block != null)
                {
                    box.Line(ConstructionText.T(block), CellTone.Yellow);
                }
            }
            if (hire)
            {
                box.Note(ConstructionText.T(company ? "reallife_bld_hire_or_build_note" : "reallife_bld_hire_ready_note"));   // build it yourself, or put it out to the city's builders
            }
            if (hire)
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bld_button_hire_builder", () => { _page = 3; Refresh(); });   // a row of its own: the client's way, company or not
            }
            Transform buttons = box.ButtonRow();
            if (block == null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_start", () => Answer(_director.StartBuilding(p)));
            }
            if (p.Kind != ProjectKind.Contract)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_money", () => { _page = 1; Refresh(); });
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            HedgeButton(box);
            AbandonRow(box, p);
            Guide(box);
        }

        // ------------------------------------------------------------------ the site

        private void WriteSite(InfoBox box, BuildState state, ProjectFile p)
        {
            double progress = Progress(p);
            box.Meter("reallife_bld_row_progress", (float)progress, Mathf.RoundToInt((float)progress * 100f) + "%", CellTone.Plain);
            box.RowPair("reallife_bld_row_started", ConstructionText.Day(p.StartDay), "reallife_bld_row_due", ConstructionText.Day(p.DueDay));
            box.Row("reallife_bld_row_crews", state.CrewsOn(p.Id).ToString(), state.CrewsOn(p.Id) == 0 ? CellTone.Red : CellTone.Plain);
            box.Section("reallife_bld_sec_materials");
            for (int m = 0; m < (int)Material.Count; m++)
            {
                if (p.Need[m] <= 0)
                {
                    continue;
                }
                box.Check(ConstructionText.Fill("reallife_bld_material_line", "material", ConstructionText.T(BuildCatalogue.MaterialKeys[m]), "have", (p.Have[m] / 10.0).ToString("0.#"),
                    "need", (p.Need[m] / 10.0).ToString("0.#")), p.Have[m] >= p.Need[m]);
            }
            if (p.OrderDay > 0)
            {
                box.Note(ConstructionText.T("reallife_bld_delivery_coming", "merchant", ConstructionText.T(BuildCatalogue.MerchantKeys[p.Merchant]), "day", ConstructionText.Day(p.OrderDay)));
            }
            WorkStage stage = (WorkStage)Math.Max(0, p.StageSeen);
            for (int k = 0; k < (int)Plant.Count; k++)
            {
                bool needed = p.Model == ProjectKind.Contract ? PlansRules.NeedsWorksPlant(p.CivicKind, (Plant)k) : PlansRules.NeedsPlant(stage, (Plant)k, p.Storeys);
                if (needed)
                {
                    box.Check(ConstructionText.T(p.Plant[k] < 0 ? "reallife_bld_plant_missing" : p.Plant[k] == 0 ? "reallife_bld_plant_hired" : "reallife_bld_plant_own",
                        "plant", ConstructionText.T(BuildCatalogue.PlantKeys[k])), p.Plant[k] >= 0);
                }
            }
            int due = ConstructionDirector.DueInspection(p), failed = ConstructionDirector.FailedInspection(p);
            for (int i = 0; i < (int)Inspection.Count; i++)
            {
                if (p.Insp[i] == InspectionState.Booked)
                {
                    box.Note(ConstructionText.T("reallife_bld_inspection_booked", "what", ConstructionText.T(BuildCatalogue.InspectionKeys[i]), "day", ConstructionText.Day(p.InspDay[i])));
                }
            }
            if (p.Rework > 0)
            {
                box.Line(ConstructionText.T("reallife_bld_rework", "hours", (p.Rework / 10).ToString()), CellTone.Yellow);
            }
            ManagerRow(box, state, p);
            bool order = !SiteRules.Supplied(p.Have, p.Need) && p.OrderDay == 0;
            if (order)
            {
                box.Selector(BuildCatalogue.MerchantCount, _merchant, i => ConstructionText.T("reallife_bld_sel_merchant", "merchant", ConstructionText.T(BuildCatalogue.MerchantKeys[i]),
                    "days", BuildCatalogue.MerchantDays[i].ToString(), "price", ConstructionText.Share(BuildCatalogue.MerchantPricePermille[i])), i => { _merchant = i; Refresh(); });
            }
            Transform buttons = box.ButtonRow();
            if (order)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_order", () => Answer(_director.Order(p, _merchant)));
            }
            else if (due >= 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_book", () => Answer(_director.BookInspection(p.Id)));
            }
            else if (failed >= 0 && p.Rework > 0)
            {
                int bribe = _director.BribeAmount(p);
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_bribe", () =>
                    Confirm("reallife_bld_confirm_bribe", "reallife_bld_confirm_bribe_body", _director.ProjectName(p), bribe, () => _director.Bribe(p.Id), true));
            }
            bool missingPlant = false;
            for (int k = 0; k < (int)Plant.Count && !missingPlant; k++)
            {
                missingPlant = (p.Model == ProjectKind.Contract ? PlansRules.NeedsWorksPlant(p.CivicKind, (Plant)k) : PlansRules.NeedsPlant(stage, (Plant)k, p.Storeys)) && p.Plant[k] < 0;
            }
            Transform more = box.ButtonRow();
            if (missingPlant)
            {
                InfoBox.Button(more, InfoBox.Positive, "reallife_bld_button_plant", () => Answer(TakeAllPlant(p, stage)));
            }
            if (p.Kind != ProjectKind.Contract)
            {
                InfoBox.Button(more, InfoBox.Negative, "reallife_bld_button_money", () => { _page = 1; Refresh(); });
            }
            InfoBox.Button(more, InfoBox.Additional, "reallife_bld_back", Back);
            HedgeButton(box);
            Guide(box);
        }

        private string TakeAllPlant(ProjectFile p, WorkStage stage)
        {
            string last = null;
            for (int k = 0; k < (int)Plant.Count; k++)
            {
                bool needed = p.Model == ProjectKind.Contract ? PlansRules.NeedsWorksPlant(p.CivicKind, (Plant)k) : PlansRules.NeedsPlant(stage, (Plant)k, p.Storeys);
                if (needed && p.Plant[k] < 0)
                {
                    last = _director.TakePlant(p, (Plant)k);
                }
            }
            return last;
        }

        // A site manager for the project: one of the office's staff in the role, chosen here.
        private void ManagerRow(InfoBox box, BuildState state, ProjectFile p)
        {
            if (!_director.Office(out PlaceView office))
            {
                return;
            }
            ConstructionGame.Staff(office.Street, office.Number, _staff);
            var names = new List<string>(4) { ConstructionText.T("reallife_bld_sel_manager_none") };
            var ids = new List<string>(4) { null };
            int at = 0;
            for (int i = 0; i < _staff.Count; i++)
            {
                if (_director.RoleOf(_staff[i]) != StaffRole.SiteManager)
                {
                    continue;
                }
                StaffFile s = state.StaffOf(_staff[i].Id);
                names.Add(ConstructionText.T("reallife_bld_sel_manager", "name", _staff[i].Name, "skill", _staff[i].Value.ToString()));
                ids.Add(_staff[i].Id);
                if (s != null && s.Project == p.Id)
                {
                    at = ids.Count - 1;
                }
            }
            if (names.Count > 1)
            {
                box.Selector(names.Count, at, i => names[i], i =>
                {
                    StaffFile was = _director.ManagerOf(p);
                    if (was != null)
                    {
                        _director.SetRole(was.Employee, StaffRole.SiteManager, 0);
                    }
                    if (ids[i] != null)
                    {
                        _director.SetRole(ids[i], StaffRole.SiteManager, p.Id);
                    }
                    Refresh();
                });
            }
        }

        private void AbandonRow(InfoBox box, ProjectFile p)
        {
            if (p.Kind == ProjectKind.Contract)
            {
                return;
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_bld_button_abandon", () =>
                Confirm("reallife_bld_confirm_abandon", "reallife_bld_confirm_abandon_body", _director.ProjectName(p), p.Spent, () => _director.Abandon(p), true));
        }

        // ------------------------------------------------------------------ money

        private void WriteMoney(InfoBox box, BuildState state, ProjectFile p)
        {
            box.Section("reallife_bld_sec_money");
            box.RowPair("reallife_bld_row_estimate", ConstructionText.Money(p.Estimate), "reallife_bld_row_spent", ConstructionText.Money(p.Spent));
            bool repay = false, loan = false, investor = false;
            long most = 0L;
            if (p.LoanAgreed > 0)
            {
                box.RowPair("reallife_bld_row_loan", ConstructionText.Money(p.LoanAgreed), "reallife_bld_row_owed", ConstructionText.Money(p.LoanOwed));
                box.Row("reallife_bld_row_rate", ConstructionText.Rate(p.LoanRateBp));
                repay = p.LoanOwed > 0 && p.Stage == ProjectStage.Finished;
            }
            else if (_director.LoanOffer(p, out most, out int rate, out long fee))
            {
                string bank = ConstructionDirector.BankName() ?? ConstructionText.T("reallife_bld_bank");
                box.Line(ConstructionText.Fill("reallife_bld_loan_offer", "bank", bank, "most", ConstructionText.Money(most), "rate",
                    ConstructionText.Rate(rate), "fee", ConstructionText.Money(fee)), CellTone.Plain);
                loan = true;
            }
            if (p.Investor >= 0)
            {
                box.Row("reallife_bld_row_investor", ConstructionText.T("reallife_bld_value_investor", "investor", ConstructionText.T(BuildCatalogue.InvestorKeys[p.Investor]),
                    "owed", ConstructionText.Money(p.InvestorOwed)));
            }
            else if (_director.InvestorOffer(p, _investor, out _, out _))
            {
                box.Selector(BuildCatalogue.InvestorCount, _investor, i =>
                {
                    _director.InvestorOffer(p, i, out long put, out long owed);
                    return ConstructionText.T("reallife_bld_sel_investor", "investor", ConstructionText.T(BuildCatalogue.InvestorKeys[i]), "put",
                        ConstructionText.Money(put), "owed", ConstructionText.Money(owed));
                }, i => { _investor = i; Refresh(); });
                investor = true;
            }
            if (repay || loan || investor)
            {
                Transform buttons = box.ButtonRow();
                long agreed = most;
                if (repay)
                {
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_repay", () =>
                        Confirm("reallife_bld_confirm_repay", "reallife_bld_confirm_repay_body", _director.ProjectName(p), p.LoanOwed, () => _director.Repay(p), false));
                }
                if (loan)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_loan", () =>
                        Confirm("reallife_bld_confirm_loan", "reallife_bld_confirm_loan_body", _director.ProjectName(p), agreed, () => _director.TakeLoan(p), false));
                }
                if (investor)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_button_investor", () => Answer(_director.TakeInvestor(p, _investor)));
                }
            }
            if (p.Kind == ProjectKind.Plot && p.Use == BuildUse.Homes && !p.InDistrict && p.Stage != ProjectStage.Finished)
            {
                int flats = ConstructionDirector.FlatsOf(p);
                int left = flats - p.FlatsSold;
                box.Row("reallife_bld_row_flats", ConstructionText.T("reallife_bld_value_flats", "sold", p.FlatsSold.ToString(), "flats", flats.ToString()));
                if (left > 0 && (p.Stage == ProjectStage.Ready || p.Stage == ProjectStage.Building))
                {
                    int room = Math.Min(left, 10);
                    _offPlan = Mathf.Clamp(_offPlan, 1, room);
                    box.Selector(room, _offPlan - 1, i => ConstructionText.T("reallife_bld_sel_off_plan", "count", (i + 1).ToString()), i => { _offPlan = i + 1; Refresh(); });
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bld_button_off_plan", () => Answer(_director.SellOffPlan(p, _offPlan)));
                }
                WriteShowFlat(box, p);
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_bld_button_site", () => { _page = 0; Refresh(); });
            Guide(box);
        }

        // ------------------------------------------------------------------ finished

        private void WriteFinished(InfoBox box, BuildState state, ProjectFile p)
        {
            box.Section("reallife_bld_sec_finished");
            box.RowPair("reallife_bld_row_handed", ConstructionText.Day(p.Handed), "reallife_bld_row_spent", ConstructionText.Money(p.Spent));
            if (!p.Opened)
            {
                box.Line(ConstructionText.T(p.Opening == ConstructionGame.Day ? "reallife_bld_opening_today" : "reallife_bld_opening_on", "day", ConstructionText.Day(p.Opening)), CellTone.Green);
            }
            if (p.Value > 0)
            {
                box.Row("reallife_bld_row_value", ConstructionText.Money(p.Value));
            }
            if (p.Kind == ProjectKind.Plot)
            {
                if (p.After == AfterUse.SellFlats)
                {
                    box.Row("reallife_bld_row_flats", ConstructionText.T("reallife_bld_value_flats", "sold", p.FlatsSold.ToString(), "flats", p.Flats.ToString()));
                    if (p.FlatsSold < p.Flats)
                    {
                        box.Selector(BuildCatalogue.AskPermille.Length, p.Ask, i => ConstructionText.T("reallife_bld_sel_ask", "share", ConstructionText.Share(BuildCatalogue.AskPermille[i])),
                            i => { _director.SetAsk(p, i); Refresh(); });
                    }
                }
                else
                {
                    box.Note(ConstructionText.T(p.InDistrict ? "reallife_bld_yours_next_load" : "reallife_bld_district_waits"));
                }
            }
            else if (p.Kind == ProjectKind.Rebuild)
            {
                box.Note(ConstructionText.T("reallife_bld_rebuilt_note"));
            }
            else if (p.Kind == ProjectKind.Refit)
            {
                box.Row("reallife_bld_row_refit_grade", ConstructionText.T(RefitGradeKey(p.Grade)));
            }
            else
            {
                box.RowPair("reallife_bld_row_contract", ConstructionText.Money(p.ClientValue), "reallife_bld_row_paid", ConstructionText.Money(p.ClientPaid));
            }
            if (p.Hired)
            {
                HiredRows(box, p);   // the builder, the contract and what it paid back (ConstructionApp.Hire.cs)
            }
            HedgeEndRow(box, p);   // what the hedge saved or cost
            Transform buttons = box.ButtonRow();
            if (p.LoanOwed > 0 || p.InvestorOwed > 0)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_money", () => { _page = 1; Refresh(); });
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            if (p.HedgeDay > 0)
            {
                HedgeButton(box);
            }
            Guide(box);
        }
    }
}
