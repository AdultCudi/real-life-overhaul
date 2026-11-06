using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The Crews tab: the company's crews, this week's crews to be had, its plant and plant to buy, and the office's staff with
    /// the roles the company gives them.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        private readonly List<ProjectFile> _workable = new List<ProjectFile>(8);

        partial void WriteCrews(InfoBox box, BuildState state, ref string selected)
        {
            string picked = null;
            for (int i = 0; i < state.Crews.Count; i++)
            {
                CrewFile c = state.Crews[i];
                TableRow row = Pick("crew:" + c.Id, ref selected);
                row.Kind = ConstructionTables.CrewsCrew;
                row.Set(0, _director.ForemanWords(c));
                row.Set(1, ConstructionText.T(BuildCatalogue.GangKeys[BuildCatalogue.Clamp(c.Gang, 0, BuildCatalogue.GangCount - 1)]));
                row.Set(2, ConstructionText.N(c.Skill), c.Skill);
                row.Set(3, ConstructionText.N(c.Morale), c.Morale, c.Morale <= BuildCatalogue.StrikeAt ? CellTone.Red : c.Morale < 40 ? CellTone.Yellow : CellTone.Plain);
                row.Set(4, ConstructionText.Money(c.Rate), c.Rate);
                row.Set(5, CrewWhere(state, c), c.PoachRival >= 0 ? CellTone.Yellow : CellTone.Plain);
                picked = row.Id == selected ? row.Id : picked;
            }
            if (state.Firm.Founded > 0)
            {
                for (int slot = 0; slot < BuildCatalogue.OffersAWeek; slot++)
                {
                    CrewOffer offer = _director.OfferAt(slot, out bool taken);
                    if (taken)
                    {
                        continue;
                    }
                    TableRow row = Pick("offer:" + slot, ref selected);
                    row.Kind = ConstructionTables.CrewsOffer;
                    row.NoTotal = true;
                    row.Set(0, OfferName(offer));
                    row.Set(1, ConstructionText.T(BuildCatalogue.GangKeys[BuildCatalogue.Clamp(offer.Gang, 0, BuildCatalogue.GangCount - 1)]));
                    row.Set(2, ConstructionText.N(offer.Skill), offer.Skill);
                    row.Set(3, "-");
                    row.Set(4, ConstructionText.Money(offer.Rate), offer.Rate);
                    row.Set(5, ConstructionText.T("reallife_bld_crews_offer_where"));
                    picked = row.Id == selected ? row.Id : picked;
                }
            }
            for (int i = 0; i < state.Plant.Count; i++)
            {
                PlantFile q = state.Plant[i];
                TableRow row = Pick("plant:" + q.Id, ref selected);
                row.Kind = ConstructionTables.CrewsPlant;
                row.Set(0, ConstructionText.T(BuildCatalogue.PlantKeys[(int)q.Kind]));
                row.Set(1, ConstructionText.T("reallife_bld_kind_plant_owned"));
                row.Set(2, "-");
                row.Set(3, "-");
                long upkeep = FirmRules.Upkeep(q.Price);
                row.Set(4, ConstructionText.Money(upkeep), upkeep);
                ProjectFile on = state.Project(q.Project);
                row.Set(5, on != null ? _director.ProjectName(on) : ConstructionText.T("reallife_bld_crews_at_yard"));
                picked = row.Id == selected ? row.Id : picked;
            }
            if (state.Firm.Founded > 0)
            {
                for (int k = 0; k < (int)Plant.Count; k++)
                {
                    TableRow row = Pick("buy:" + k, ref selected);
                    row.Kind = ConstructionTables.CrewsPlant;
                    row.NoTotal = true;
                    row.Set(0, ConstructionText.T(BuildCatalogue.PlantKeys[k]));
                    row.Set(1, ConstructionText.T("reallife_bld_kind_plant_sale"));
                    row.Set(2, "-");
                    row.Set(3, "-");
                    long price = (long)BuildCatalogue.BuyPrice[k] * ConstructionGame.Prices / 1000L;
                    row.Set(4, ConstructionText.Money(price), price);
                    row.Set(5, ConstructionText.T("reallife_bld_crews_hire_instead", "amount", ConstructionText.Money(PlansRules.HireDay((Plant)k, ConstructionGame.Prices))));
                    picked = row.Id == selected ? row.Id : picked;
                }
            }
            if (_director.Office(out PlaceView office) && state.Firm.Founded > 0)
            {
                ConstructionGame.Staff(office.Street, office.Number, _staff);
                _director.TidyStaff(_staff);
                for (int i = 0; i < _staff.Count; i++)
                {
                    StaffView v = _staff[i];
                    TableRow row = Pick("staff:" + v.Id, ref selected);
                    row.Kind = ConstructionTables.CrewsStaff;
                    row.NoTotal = true;
                    row.Set(0, v.Name);
                    row.Set(1, ConstructionText.T(BuildCatalogue.RoleKeys[(int)_director.RoleOf(v)]));
                    row.Set(2, ConstructionText.N(v.Value), v.Value);
                    row.Set(3, "-");
                    row.Set(4, "-");
                    StaffFile s = state.StaffOf(v.Id);
                    ProjectFile managed = s != null ? state.Project(s.Project) : null;
                    row.Set(5, managed != null ? _director.ProjectName(managed) : ConstructionText.T("reallife_bld_crews_at_office"));
                    picked = row.Id == selected ? row.Id : picked;
                }
            }
            if (picked != null)
            {
                WriteCrewPick(box, state, picked);
                return;
            }
            box.Title(ConstructionText.T("reallife_bld_tab_crews"));
            box.Note(ConstructionText.T(state.Firm.Founded <= 0 ? "reallife_bld_crews_no_company" : "reallife_bld_crews_pick"));
            Guide(box);
        }

        private string CrewWhere(BuildState state, CrewFile c)
        {
            if (c.StrikeUntil >= ConstructionGame.Day)
            {
                return ConstructionText.T("reallife_bld_crews_on_strike", "day", ConstructionText.Day(c.StrikeUntil));
            }
            ProjectFile p = state.Project(c.Project);
            return p != null ? _director.ProjectName(p) : ConstructionText.T("reallife_bld_crews_free");
        }

        private static string OfferName(CrewOffer offer)
        {
            return ConstructionText.T("reallife_bld_crew_name", "foreman",
                ConstructionText.T(BuildCatalogue.ForemanKeys[BuildCatalogue.Clamp(offer.Foreman, 0, BuildCatalogue.ForemanKeys.Length - 1)]));
        }

        private void WriteCrewPick(InfoBox box, BuildState state, string pick)
        {
            int n = PickedNumber("crew:");
            if (n > 0 && state.Crew(n) is CrewFile c)
            {
                WriteCrew(box, state, c);
                return;
            }
            if (Picked("offer:") != null)
            {
                WriteOffer(box, PickedNumber("offer:"));
                return;
            }
            n = PickedNumber("plant:");
            if (n > 0 && state.PlantOf(n) is PlantFile q)
            {
                WritePlant(box, state, q);
                return;
            }
            if (Picked("buy:") != null)
            {
                WriteBuy(box, state, (Plant)BuildCatalogue.Clamp(PickedNumber("buy:"), 0, (int)Plant.Count - 1));
                return;
            }
            string employee = Picked("staff:");
            if (employee != null && ConstructionGame.Employee(employee, out StaffView v))
            {
                WriteStaff(box, v);
                return;
            }
            BackButton(box);
        }

        private void WriteCrew(InfoBox box, BuildState state, CrewFile c)
        {
            box.Title(_director.ForemanWords(c));
            box.Subtitle(ConstructionText.GangWords(c.Gang));
            box.Meter("reallife_bld_row_skill", c.Skill / 100f, c.Skill.ToString(), CellTone.Plain);
            box.Meter("reallife_bld_row_morale", c.Morale / 100f, c.Morale.ToString(), c.Morale <= BuildCatalogue.StrikeAt ? CellTone.Red : c.Morale < 40 ? CellTone.Yellow : CellTone.Green);
            box.RowPair("reallife_bld_row_rate", ConstructionText.Money(c.Rate), "reallife_bld_row_hired", ConstructionText.Day(c.Hired));
            if (c.StrikeUntil >= ConstructionGame.Day)
            {
                box.Line(ConstructionText.T("reallife_bld_crews_on_strike", "day", ConstructionText.Day(c.StrikeUntil)), CellTone.Red);
            }
            if (c.PoachRival >= 0)
            {
                box.Line(ConstructionText.T("reallife_bld_crews_poach", "rival", RivalName(c.PoachRival), "share", ConstructionText.Share(c.PoachPay), "day", ConstructionText.Day(c.PoachUntil)),
                    CellTone.Yellow);
            }
            _workable.Clear();
            for (int i = 0; i < state.Projects.Count; i++)
            {
                ProjectFile p = state.Projects[i];
                if ((p.Stage == ProjectStage.Ready || p.Stage == ProjectStage.Building) && p.Contractor < 0)   // a contractor's refit has the contractor's crews
                {
                    _workable.Add(p);
                }
            }
            int at = 0;
            for (int i = 0; i < _workable.Count; i++)
            {
                at = _workable[i].Id == c.Project ? i + 1 : at;
            }
            int crew = c.Id;
            box.Selector(_workable.Count + 1, at, i => i == 0 ? ConstructionText.T("reallife_bld_sel_works_none")
                : ConstructionText.T("reallife_bld_sel_works_on", "project", _director.ProjectName(_workable[i - 1])),
                i => Answer(_director.Assign(crew, i == 0 ? 0 : _workable[i - 1].Id)));
            Transform buttons = box.ButtonRow();
            if (c.PoachRival >= 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_match", () => Answer(_director.Match(crew)));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_button_raise", () =>
                Confirm("reallife_bld_confirm_raise", "reallife_bld_confirm_raise_body", _director.ForemanWords(c),
                    (long)c.Rate * BuildCatalogue.RaisePermille / 1000L, () => _director.Raise(crew), false));
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_release", () =>
                Confirm("reallife_bld_confirm_release", "reallife_bld_confirm_release_body", _director.ForemanWords(c),
                    (long)c.Rate * BuildCatalogue.ReleaseDays, () => _director.Release(crew), true));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_bld_back", Back);
            Guide(box);
        }

        private void WriteOffer(InfoBox box, int slot)
        {
            CrewOffer offer = _director.OfferAt(slot, out bool taken);
            box.Title(OfferName(offer));
            box.Subtitle(ConstructionText.GangWords(offer.Gang));
            box.Meter("reallife_bld_row_skill", offer.Skill / 100f, offer.Skill.ToString(), CellTone.Plain);
            box.Row("reallife_bld_row_rate", ConstructionText.Money(offer.Rate));
            box.Note(ConstructionText.T("reallife_bld_crews_offer_note"));
            Transform buttons = box.ButtonRow();
            if (!taken)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_hire", () => Answer(_director.Hire(slot)));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            Guide(box);
        }

        private void WritePlant(InfoBox box, BuildState state, PlantFile q)
        {
            box.Title(ConstructionText.T(BuildCatalogue.PlantKeys[(int)q.Kind]));
            ProjectFile on = state.Project(q.Project);
            box.Subtitle(on != null ? _director.ProjectName(on) : ConstructionText.T("reallife_bld_crews_at_yard"));
            box.RowPair("reallife_bld_row_bought", ConstructionText.Day(q.Bought), "reallife_bld_row_paid", ConstructionText.Money(q.Price));
            long back = FirmRules.Resale(q.Price, ConstructionGame.Day - q.Bought);
            box.RowPair("reallife_bld_row_upkeep", ConstructionText.Money(FirmRules.Upkeep(q.Price)), "reallife_bld_row_resale", ConstructionText.Money(back));
            Transform buttons = box.ButtonRow();
            int id = q.Id;
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_sell_plant", () =>
                Confirm("reallife_bld_confirm_sell_plant", "reallife_bld_confirm_sell_plant_body",
                    ConstructionText.T(BuildCatalogue.PlantKeys[(int)q.Kind]), back, () => _director.SellPlant(id), true));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            Guide(box);
        }

        private void WriteBuy(InfoBox box, BuildState state, Plant kind)
        {
            box.Title(ConstructionText.T(BuildCatalogue.PlantKeys[(int)kind]));
            long price = (long)BuildCatalogue.BuyPrice[(int)kind] * ConstructionGame.Prices / 1000L;
            box.RowPair("reallife_bld_row_price", ConstructionText.Money(price), "reallife_bld_row_upkeep", ConstructionText.Money(FirmRules.Upkeep((int)System.Math.Min(int.MaxValue, price))));
            box.Row("reallife_bld_row_hire", ConstructionText.Money(PlansRules.HireDay(kind, ConstructionGame.Prices)));
            box.Note(ConstructionText.T("reallife_bld_plant_note"));
            Transform buttons = box.ButtonRow();
            if (string.IsNullOrEmpty(state.Firm.Yard))
            {
                box.Line(ConstructionText.T("reallife_bld_answer_no_yard"), CellTone.Yellow);
            }
            else
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_buy_plant", () =>
                    Confirm("reallife_bld_confirm_buy_plant", "reallife_bld_confirm_buy_plant_body",
                        ConstructionText.T(BuildCatalogue.PlantKeys[(int)kind]), price, () => _director.BuyPlant(kind), false));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            Guide(box);
        }

        private void WriteStaff(InfoBox box, StaffView v)
        {
            box.Title(v.Name);
            box.Subtitle(ConstructionText.T("reallife_bld_staff_skill", "skill", ConstructionGame.SkillWords(v.Skill), "value", v.Value.ToString()));
            StaffRole role = _director.RoleOf(v);
            string employee = v.Id;
            box.Selector((int)StaffRole.Count, (int)role, i => ConstructionText.T("reallife_bld_sel_role", "role", ConstructionText.T(BuildCatalogue.RoleKeys[i])), i =>
            {
                _director.SetRole(employee, (StaffRole)i, 0);
                Refresh();
            });
            box.Note(ConstructionText.T(BuildCatalogue.RoleNoteKeys[(int)role]));
            BackButton(box);
        }
    }
}
