using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>The Company tab: the office, the contractor's licence, the standing, the speciality, the cover, the yard and the books.</summary>
    public sealed partial class ConstructionApp
    {
        private static readonly string[] CompanyRows = { "office", "licence", "reputation", "speciality", "cover", "yard", "record", "books" };

        private static readonly string[] CompanyKeys =
        {
            "reallife_bld_co_office", "reallife_bld_co_licence", "reallife_bld_co_reputation", "reallife_bld_co_speciality", "reallife_bld_co_cover", "reallife_bld_co_yard",
            "reallife_bld_co_record", "reallife_bld_co_books"
        };
        private readonly List<PlaceView> _yards = new List<PlaceView>(4);

        partial void WriteCompany(InfoBox box, BuildState state, ref string selected)
        {
            FirmFile f = state.Firm;
            bool office = _director.Office(out PlaceView place);
            string picked = null;
            int pickedAt = 0;
            for (int i = 0; i < CompanyRows.Length; i++)
            {
                TableRow row = Pick("co:" + CompanyRows[i], ref selected);
                row.Set(0, ConstructionText.T(CompanyKeys[i]));
                row.Set(1, CompanyValue(state, CompanyRows[i], office, place));
                if (row.Id == selected)
                {
                    picked = CompanyRows[i];
                    pickedAt = i;
                }
            }
            if (picked == null)
            {
                box.Title(ConstructionText.T("reallife_bld_tab_company"));
                box.Note(ConstructionText.T(f.Founded > 0 ? "reallife_bld_company_pick" : "reallife_bld_company_found_first"));
                Guide(box);
                return;
            }
            box.Title(ConstructionText.T(CompanyKeys[pickedAt]));
            switch (picked)
            {
                case "office": WriteOffice(box, state, office, place); break;
                case "licence": WriteLicence(box, state); break;
                case "reputation": WriteReputation(box, state); break;
                case "speciality": WriteSpeciality(box, state); break;
                case "cover": WriteCover(box, state); break;
                case "yard": WriteYard(box, state); break;
                case "record": WriteRecord(box, state); break;
                default: WriteBooks(box, state); break;
            }
        }

        private string CompanyValue(BuildState state, string what, bool office, PlaceView place)
        {
            FirmFile f = state.Firm;
            switch (what)
            {
                case "office":
                    return office ? place.Name : ConstructionText.T("reallife_bld_value_none");
                case "licence":
                    return ConstructionText.LicenceWords(f);
                case "reputation":
                    return ConstructionText.N(f.Reputation);
                case "speciality":
                    return ConstructionText.T(BuildCatalogue.SpecialityKeys[(int)f.Speciality]);
                case "cover":
                    return ConstructionText.T(BuildCatalogue.CoverKeys[(int)f.Cover]);
                case "yard":
                    return string.IsNullOrEmpty(f.Yard) ? ConstructionText.T("reallife_bld_value_none") : YardName(f.Yard);
                case "record":
                    return ConstructionText.T("reallife_bld_value_record", "finished", f.Finished.ToString(), "on_time", f.OnTime.ToString());
                default:
                    return ConstructionText.Money(f.Earned - f.Spent);
            }
        }

        private string YardName(string place)
        {
            ConstructionGame.Warehouses(_yards);
            for (int i = 0; i < _yards.Count; i++)
            {
                if (_yards[i].Place == place)
                {
                    return _yards[i].Name;
                }
            }
            return ConstructionText.T("reallife_bld_value_yard_gone");
        }

        private void WriteOffice(InfoBox box, BuildState state, bool office, PlaceView place)
        {
            FirmFile f = state.Firm;
            if (!office)
            {
                box.Note(ConstructionText.T("reallife_bld_glance_no_office"));
                BackButton(box);
                return;
            }
            box.Subtitle(place.Name);
            box.Row("reallife_bld_row_address", ConstructionGame.AddressText(place.Street, place.Number));
            if (f.Founded > 0)
            {
                box.Row("reallife_bld_row_founded", ConstructionText.Day(f.Founded));
                ConstructionGame.Staff(place.Street, place.Number, _staff);
                box.Row("reallife_bld_row_staff", ConstructionText.N(_staff.Count));
                box.Note(ConstructionText.T("reallife_bld_office_note"));
                BackButton(box);
                return;
            }
            box.Note(ConstructionText.T("reallife_bld_glance_found", "name", place.Name));
            InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bld_button_found", () => Answer(_director.Found()));
            Guide(box);
        }

        private void WriteLicence(InfoBox box, BuildState state)
        {
            FirmFile f = state.Firm;
            box.Subtitle(ConstructionText.LicenceWords(f));
            box.RowPair("reallife_bld_row_fee", ConstructionText.Money(ConstructionDirector.LicenceFee), "reallife_bld_row_decided", ConstructionText.T("reallife_bld_value_days",
                "days", BuildCatalogue.LicenceDays.ToString()));
            if (f.Licence == LicenceState.Granted)
            {
                box.Row("reallife_bld_row_until", ConstructionText.Day(f.LicenceUntil));
            }
            box.Note(ConstructionText.T("reallife_bld_licence_note"));
            Transform buttons = box.ButtonRow();
            if (f.Founded > 0 && PlanningRules.MayAsk(f.Licence, f.LicenceDay, f.LicenceUntil, ConstructionGame.Day))
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_city_hall", () => PlanningCall.Open(0));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            Guide(box);
        }

        private void WriteReputation(InfoBox box, BuildState state)
        {
            FirmFile f = state.Firm;
            box.Meter("reallife_bld_row_reputation", f.Reputation / 100f, f.Reputation.ToString(), f.Reputation >= 60 ? CellTone.Green : f.Reputation < 25 ? CellTone.Red : CellTone.Plain);
            int better = 0;
            for (int r = 0; r < BuildCatalogue.RivalCount; r++)
            {
                better += state.Rivals[r].Reputation > f.Reputation ? 1 : 0;
            }
            box.Row("reallife_bld_row_rank", ConstructionText.T("reallife_bld_value_rank", "rank", (better + 1).ToString(), "count", (BuildCatalogue.RivalCount + 1).ToString()));
            box.Note(ConstructionText.T("reallife_bld_reputation_note"));
            BackButton(box);
        }

        private void WriteSpeciality(InfoBox box, BuildState state)
        {
            FirmFile f = state.Firm;
            if (f.SpecialSince > 0)
            {
                box.Row("reallife_bld_row_since", ConstructionText.Day(f.SpecialSince));
            }
            if (f.Founded > 0)
            {
                box.Selector((int)Speciality.Count, (int)f.Speciality, i => ConstructionText.T(BuildCatalogue.SpecialityKeys[i]), i => Answer(_director.SetSpeciality((Speciality)i)));
            }
            box.Note(ConstructionText.T("reallife_bld_speciality_note", "days", BuildCatalogue.SpecialityRest.ToString()));
            BackButton(box);
        }

        private void WriteCover(InfoBox box, BuildState state)
        {
            FirmFile f = state.Firm;
            long underWay = 0;
            for (int i = 0; i < state.Projects.Count; i++)
            {
                ProjectFile p = state.Projects[i];
                underWay += p.Stage == ProjectStage.Building ? Math.Max(p.Estimate, p.ClientValue) : 0L;
            }
            if (f.Founded > 0)
            {
                box.Selector(BuildCatalogue.CoverKeys.Length, (int)f.Cover, i => ConstructionText.T("reallife_bld_sel_cover", "cover", ConstructionText.T(BuildCatalogue.CoverKeys[i]),
                    "amount", ConstructionText.Money(i == 0 ? 0L : FirmRules.Premium((CoverLevel)i, underWay, ConstructionGame.Prices))), i => { _director.SetCover((CoverLevel)i); Refresh(); });
            }
            int l = (int)f.Cover;
            box.Check(ConstructionText.T("reallife_bld_cover_theft", "share", ConstructionText.Share(BuildCatalogue.CoverTheftPermille[l])), BuildCatalogue.CoverTheftPermille[l] > 0);
            box.Check(ConstructionText.T("reallife_bld_cover_claims", "share", ConstructionText.Share(BuildCatalogue.CoverClaimPermille[l])), BuildCatalogue.CoverClaimPermille[l] > 0);
            box.Check(ConstructionText.T("reallife_bld_cover_storm"), BuildCatalogue.CoverStorm[l]);
            box.Note(ConstructionText.T("reallife_bld_cover_note", "excess", ConstructionText.Money((long)BuildCatalogue.CoverExcess * ConstructionGame.Prices / 1000L)));
            BackButton(box);
        }

        private void WriteYard(InfoBox box, BuildState state)
        {
            FirmFile f = state.Firm;
            ConstructionGame.Warehouses(_yards);
            int at = 0;
            for (int i = 0; i < _yards.Count; i++)
            {
                at = _yards[i].Place == f.Yard ? i + 1 : at;
            }
            if (f.Founded > 0 && _yards.Count > 0)
            {
                box.Selector(_yards.Count + 1, at, i => i == 0 ? ConstructionText.T("reallife_bld_sel_yard_none") : _yards[i - 1].Name,
                    i => Answer(_director.SetYard(i == 0 ? string.Empty : _yards[i - 1].Place)));
            }
            int kept = 0;
            for (int i = 0; i < state.Plant.Count; i++)
            {
                kept += state.Plant[i].Project == 0 ? 1 : 0;
            }
            box.Row("reallife_bld_row_plant_kept", ConstructionText.N(kept));
            box.Note(ConstructionText.T(_yards.Count == 0 ? "reallife_bld_yard_none" : "reallife_bld_yard_note"));
            BackButton(box);
        }

        private void WriteRecord(InfoBox box, BuildState state)
        {
            FirmFile f = state.Firm;
            box.RowPair("reallife_bld_row_finished", ConstructionText.N(f.Finished), "reallife_bld_row_on_time", ConstructionText.N(f.OnTime));
            box.RowPair("reallife_bld_row_late", ConstructionText.N(f.Late), "reallife_bld_row_defects", ConstructionText.N(f.Defects));
            box.RowPair("reallife_bld_row_tenders_won", ConstructionText.N(f.TendersWon), "reallife_bld_row_tenders_lost", ConstructionText.N(f.TendersLost));
            BackButton(box);
        }

        private void WriteBooks(InfoBox box, BuildState state)
        {
            FirmFile f = state.Firm;
            box.RowPair("reallife_bld_row_earned", ConstructionText.Money(f.Earned), "reallife_bld_row_spent_all", ConstructionText.Money(f.Spent));
            long owed = 0, investors = 0;
            for (int i = 0; i < state.Projects.Count; i++)
            {
                owed += state.Projects[i].LoanOwed;
                investors += state.Projects[i].InvestorOwed;
            }
            box.RowPair("reallife_bld_row_loans_owed", ConstructionText.Money(owed), "reallife_bld_row_investors_owed", ConstructionText.Money(investors));
            box.Note(ConstructionText.T("reallife_bld_books_note"));
            BackButton(box);
        }
    }
}
