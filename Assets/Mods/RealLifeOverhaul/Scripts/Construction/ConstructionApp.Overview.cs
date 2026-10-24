using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>The Overview tab: the company at a glance in the white box, and every project still in hand with its next step.</summary>
    public sealed partial class ConstructionApp
    {
        partial void WriteOverview(InfoBox box, BuildState state, ref string selected)
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
                row.Set(0, _director.ProjectName(p));
                row.Set(1, ConstructionText.StageWords(p));
                row.Set(2, NextStep(p), NextTone(p));
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
            WriteGlance(box, state);
        }

        // A project the player still has something to do about: a live one, or a finished one with flats, a loan or its opening ahead.
        private static bool InHand(ProjectFile p)
        {
            return p.Live || (p.Stage == ProjectStage.Finished && (!p.Opened || p.LoanOwed > 0 || p.InvestorOwed > 0 || (p.After == AfterUse.SellFlats && p.FlatsSold < p.Flats)));
        }

        // The company as it stands, or how to start one.
        private void WriteGlance(InfoBox box, BuildState state)
        {
            FirmFile f = state.Firm;
            bool office = _director.Office(out PlaceView place);
            box.Title(office && f.Founded > 0 ? place.Name : ConstructionText.T("reallife_bld_glance_title"));
            if (f.Founded <= 0)
            {
                if (office)
                {
                    box.Subtitle(ConstructionGame.AddressText(place.Street, place.Number));
                }
                box.Note(ConstructionText.T("reallife_bld_glance_client"));   // no company is needed to build: the city's builders are hired
                box.Note(office ? ConstructionText.T("reallife_bld_glance_found", "name", place.Name) : ConstructionText.T("reallife_bld_glance_no_office"));
                Transform client = box.ButtonRow();
                InfoBox.Button(client, InfoBox.Positive, "reallife_bld_button_land", () => ShowTab(ConstructionDirector.TabLand));
                InfoBox.Button(client, InfoBox.Additional, "reallife_bld_button_rivals", () => ShowTab(ConstructionDirector.TabRivals));
                if (office)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_bld_button_found", () => Answer(_director.Found()));
                }
                Guide(box);
                return;
            }
            if (!office)
            {
                box.Note(ConstructionText.T("reallife_bld_glance_no_office"));
                Guide(box);
                return;
            }
            box.Subtitle(ConstructionText.T("reallife_bld_glance_since", "day", ConstructionText.Day(f.Founded)));
            box.Chip(ConstructionText.LicenceWords(f), f.Licence == LicenceState.Granted ? CellTone.Green : CellTone.Yellow);
            box.Meter("reallife_bld_row_reputation", f.Reputation / 100f, f.Reputation.ToString(), f.Reputation >= 60 ? CellTone.Green : f.Reputation < 25 ? CellTone.Red : CellTone.Plain);
            box.RowPair("reallife_bld_row_sites", ConstructionText.N(state.Building()), "reallife_bld_row_crews_all", ConstructionText.N(state.Crews.Count));
            box.RowPair("reallife_bld_row_finished", ConstructionText.N(f.Finished), "reallife_bld_row_on_time", ConstructionText.N(f.OnTime));
            box.RowPair("reallife_bld_row_earned", ConstructionText.Money(f.Earned), "reallife_bld_row_spent_all", ConstructionText.Money(f.Spent));
            int open = 0;
            for (int i = 0; i < state.Tenders.Count; i++)
            {
                open += state.Tenders[i].State == TenderState.Open ? 1 : 0;
            }
            if (open > 0)
            {
                box.Line(ConstructionText.T("reallife_bld_glance_tenders", "count", open.ToString()), CellTone.Plain);
            }
            for (int i = 0; i < state.Crews.Count; i++)
            {
                if (state.Crews[i].PoachRival >= 0)
                {
                    box.Line(ConstructionText.T("reallife_bld_glance_poached", "name", _director.ForemanWords(state.Crews[i])), CellTone.Yellow);
                }
            }
            if (f.Licence != LicenceState.Granted)
            {
                box.Note(ConstructionText.T(f.Licence == LicenceState.Applied ? "reallife_bld_glance_licence_waits" : "reallife_bld_glance_licence"));
            }
            Transform buttons = box.ButtonRow();
            if (f.Licence != LicenceState.Granted && f.Licence != LicenceState.Applied)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_city_hall", () => PlanningCall.Open(0));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_button_land", () => ShowTab(ConstructionDirector.TabLand));
            Guide(box);
        }
    }
}
