using RealLifeOverhaul.GameUi;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The app's Agency tab: the instructions, and a page for the office or the instruction picked.</summary>
    public sealed partial class EstateApp
    {
        private int _office;            // the office showing for the agency

        private void WriteAgency(InfoBox box, EstateState state, ref string selected)
        {
            EstateClimate c = _director.Climate;
            AgencyFile agency = state.Agency;
            Instruction picked = null;
            for (int i = 0; i < agency.Open.Count; i++)
            {
                Instruction n = agency.Open[i];
                TableRow row;
                int pays = AgencyRules.Commission(n.Sale, n.Stage == (int)InstructionStage.Completing ? n.Price : n.Value);
                if (Pick("ins:" + n.Id, ref selected, out row))
                {
                    picked = n;
                }
                row.Kind = n.Sale ? EstateTables.IsSale : EstateTables.IsLetting;
                row.Kind2 = n.Stage;
                row.Set(0, EstateText.T(n.Sale ? "reallife_estate_row_sale" : "reallife_estate_row_letting", "district", EstateText.District(n.District)));
                row.Set(1, EstateText.T(n.Sale ? "reallife_estate_filter_sales" : "reallife_estate_filter_lettings"));
                row.Set(2, EstateText.Money(n.Value), n.Value);
                row.Set(3, EstateText.Money(pays), pays);
                row.Set(4, ((n.Hours - n.Done) / 10f).ToString("0.#"), (n.Hours - n.Done) / 10.0);
                row.Set(5, EstateText.Stage(n.Stage), n.Stage == (int)InstructionStage.Offered ? TableRow.Yellow : TableRow.Plain);
                row.Set(6, EstateText.T("reallife_estate_row_until", "day", n.Day.ToString()), n.Day);
            }
            if (agency.Open.Count == 0)
            {
                Say("none", EstateText.T(agency.Running ? "reallife_estate_row_no_instructions" : "reallife_estate_row_no_agency"));
            }
            if (picked != null)
            {
                WriteInstruction(box, agency, picked, c);
                return;
            }
            box.Title(EstateText.T("reallife_estate_tab_agency"));
            EstateGame.Offices(_places);
            if (agency.Running)
            {
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_estate_agency_office", EstateGame.NameOf(agency.Office));
                box.Line(EstateText.T(agency.Negotiators > 0 ? "reallife_estate_agency_staff" : "reallife_estate_agency_no_staff", "count", agency.Negotiators.ToString(),
                    "hours", (agency.Capacity / 10f).ToString("0.#")), agency.Negotiators > 0 ? CellTone.Plain : CellTone.Red);
                box.Line(EstateText.T("reallife_estate_agency_totals", "deals", agency.Deals.ToString(), "lost", agency.Lost.ToString(), "amount", EstateText.Money(agency.Commission)));
                box.Headline("reallife_estate_agency_reputation");
                for (int i = 0; i < agency.Districts.Count; i++)
                {
                    DistrictRep d = agency.Districts[i];
                    if (d.SqmPrice > 0)
                    {
                        box.RowWords(EstateText.District(d.Name),
                            EstateText.Level(d.Rep) + ", " + EstateText.T("reallife_estate_agency_chance", "chance", EstateText.Share(AgencyRules.InstructionChance(d.Rep, c.Cycle))));
                    }
                }
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_estate_btn_close_agency", _director.CloseAgency);
                box.Note(EstateText.T("reallife_estate_agency_what"));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            if (_places.Count == 0)
            {
                box.Line(EstateText.T("reallife_estate_agency_no_office"), CellTone.Yellow);
                box.Note(EstateText.T("reallife_estate_agency_what"));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            _office = EstateCatalogue.Clamp(_office, 0, _places.Count - 1);
            if (!box.HasSelector || !box.Selector(_places.Count, _office, i => _places[i].Address.ToFormattedString(), i => _office = i))
            {
                box.Line(_places[_office].Address.ToFormattedString());
            }
            box.Note(EstateText.T("reallife_estate_agency_open_what"));
            InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_estate_btn_open_agency", () =>
            {
                EstateGame.Offices(_places);
                if (_office >= 0 && _office < _places.Count)
                {
                    _director.OpenAgency(EstateGame.Place(_places[_office].StreetName, _places[_office].StreetNumber));
                }
            });
            box.Note(EstateText.T("reallife_estate_agency_what"));
            Meta.Guide.Point(box, GuideTitle);
        }

        private void WriteInstruction(InfoBox box, AgencyFile agency, Instruction n, EstateClimate c)
        {
            int id = n.Id;
            DistrictRep d = agency.District(n.District);
            int rep = d != null ? d.Rep : 0;
            box.Title(EstateText.T(n.Sale ? "reallife_estate_row_sale" : "reallife_estate_row_letting", "district", EstateText.District(n.District)));
            box.Chip(EstateText.Stage(n.Stage), n.Stage == (int)InstructionStage.Offered ? CellTone.Yellow : n.Stage == (int)InstructionStage.Completing ? CellTone.Green : CellTone.Plain);
            switch ((InstructionStage)n.Stage)
            {
                case InstructionStage.Offered:
                    box.Line(EstateText.T("reallife_estate_ins_offered", "day", n.Day.ToString(), "amount", EstateText.Money(AgencyRules.TakeCost(n)),
                        "days", EstateCatalogue.InstructionLapseDays.ToString()));
                    break;
                case InstructionStage.Marketing:
                    box.Line(EstateText.T("reallife_estate_ins_marketing", "day", n.Day.ToString()));
                    break;
                default:
                    box.Line(EstateText.T("reallife_estate_ins_completing", "amount", EstateText.Money(n.Price), "day", n.Day.ToString()));
                    break;
            }
            box.Note(EstateText.T("reallife_estate_ins_what", "sqm", n.Sqm.ToString(), "amount", EstateText.Money(n.Value), "ask", EstateText.Share(n.Ask)));
            box.Section("reallife_ibox_sec_work");
            box.Meter("reallife_ibox_done", n.Hours > 0 ? n.Done / (float)n.Hours : 1f, ((n.Hours - n.Done) / 10f).ToString("0.#"), CellTone.Plain);
            box.Line(EstateText.T("reallife_estate_ins_hours", "hours", (n.Hours / 10).ToString(), "done", (n.Done / 10f).ToString("0.#"), "capacity", (agency.Capacity / 10f).ToString("0.#")));
            box.Line(EstateText.T("reallife_estate_ins_chance", "chance", EstateText.Share(AgencyRules.DealChance(n.Ask, rep, c.Cycle)),
                "fall", EstateText.Share(EstateCatalogue.FallThrough[EstateCatalogue.Clamp(c.Cycle, 0, 3)]), "reputation", EstateText.Level(rep)));
            int commission = AgencyRules.Commission(n.Sale, n.Stage == (int)InstructionStage.Completing ? n.Price : n.Value);
            box.Line(EstateText.T(n.Sale ? "reallife_estate_ins_pays_sale" : "reallife_estate_ins_pays_let", "amount", EstateText.Money(commission),
                "share", EstateText.Share(n.Sale ? EstateCatalogue.CommissionSale : EstateCatalogue.CommissionLet)));
            Transform buttons = box.ButtonRow();
            if ((InstructionStage)n.Stage == InstructionStage.Offered)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_take", () => _director.TakeInstruction(id));
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_decline", () => _director.DeclineInstruction(id));
            }
            BackButton(buttons);
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
