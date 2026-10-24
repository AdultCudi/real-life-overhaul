using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The Rivals tab: the city's other builders, what they are known for, their standing, what they have won and built in
    /// this save, and their office in Uptown (a business of the construction company's type on the map's filter and in
    /// BizMan), which a press makes the game's destination.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        partial void WriteRivals(InfoBox box, BuildState state, ref string selected)
        {
            int picked = -1;
            for (int r = 0; r < BuildCatalogue.RivalCount; r++)
            {
                RivalFile rival = state.Rivals[r];
                TableRow row = Pick("rival:" + r, ref selected);
                row.Set(0, RivalName(r));
                row.Set(1, ConstructionText.T(BuildCatalogue.SpecialityKeys[(int)BuildCatalogue.RivalSpeciality[r]]));
                row.Set(2, ConstructionText.N(rival.Reputation), rival.Reputation);
                row.Set(3, ConstructionText.N(rival.Won), rival.Won);
                row.Set(4, ConstructionText.N(rival.Built), rival.Built);
                picked = row.Id == selected ? r : picked;
            }
            if (state.Firm.Founded > 0)
            {
                TableRow own = Row("rival:you");
                own.Pinned = true;
                own.Set(0, ConstructionText.T("reallife_bld_rival_you"));
                own.Set(1, ConstructionText.T(BuildCatalogue.SpecialityKeys[(int)state.Firm.Speciality]));
                own.Set(2, ConstructionText.N(state.Firm.Reputation), state.Firm.Reputation, CellTone.Green);
                own.Set(3, ConstructionText.N(state.Firm.TendersWon), state.Firm.TendersWon);
                own.Set(4, ConstructionText.N(state.Firm.Finished), state.Firm.Finished);
            }
            if (picked < 0)
            {
                box.Title(ConstructionText.T("reallife_bld_tab_rivals"));
                box.Note(ConstructionText.T("reallife_bld_rivals_pick"));
                Guide(box);
                return;
            }
            WriteRival(box, state, picked);
        }

        private void WriteRival(InfoBox box, BuildState state, int r)
        {
            RivalFile rival = state.Rivals[r];
            box.Title(RivalName(r));
            box.Subtitle(ConstructionText.T("reallife_bld_rival_known", "speciality", ConstructionText.T(BuildCatalogue.SpecialityKeys[(int)BuildCatalogue.RivalSpeciality[r]])));
            box.Meter("reallife_bld_row_reputation", rival.Reputation / 100f, rival.Reputation.ToString(), CellTone.Plain);
            box.RowPair("reallife_bld_row_crews_all", ConstructionText.N(BuildCatalogue.RivalCrews[r]), "reallife_bld_row_margin", ConstructionText.Share(BuildCatalogue.RivalMarginPermille[r]));
            box.RowPair("reallife_bld_row_tenders_won", ConstructionText.N(rival.Won), "reallife_bld_row_built", ConstructionText.N(rival.Built));
            int going = 0;
            for (int i = 0; i < state.Developments.Count; i++)
            {
                DevFile dev = state.Developments[i];
                if (dev.SiteId > 0 && !dev.Handed && !dev.Public && _director.Lot(dev.Lot, out LotView lot))
                {
                    going += DevelopmentRules.Rival(lot.Id, DevelopmentRules.Use(lot.Id, lot.Class)) == r ? 1 : 0;
                }
            }
            box.RowPair("reallife_bld_row_going_up", ConstructionText.N(going), "reallife_bld_row_for_you", ConstructionText.N(_director.HiredBy(r)));
            string office = ConstructionBridges.RivalOffice != null ? ConstructionBridges.RivalOffice(r) : null;
            box.Row("reallife_bld_row_office", office ?? ConstructionText.T("reallife_bld_rival_office_none"));
            int against = 0;
            for (int i = 0; i < state.Tenders.Count; i++)
            {
                TenderFile t = state.Tenders[i];
                against += t.BidPermille > 0 && t.State != TenderState.Open && t.Winner == r ? 1 : 0;
            }
            if (against > 0)
            {
                box.Line(ConstructionText.T("reallife_bld_rival_beat_you", "count", against.ToString()), CellTone.Yellow);
            }
            box.Note(ConstructionText.T("reallife_bld_rival_note"));
            box.Note(ConstructionText.T("reallife_bld_rival_hire_note"));
            if (office != null)
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bld_rival_office_go", () => GoToOffice(r, office));
            }
            BackButton(box);
        }

        // The game's own destination at the rival's office, said in a notice of the game's.
        private void GoToOffice(int r, string office)
        {
            Func<int, bool> go = ConstructionBridges.RivalOfficeGo;
            if (go != null && go(r))
            {
                Notifications.Show(NotificationType.Info, "reallife_bld_rival_office_set", new Dictionary<string, string> { { "rival", RivalName(r) }, { "address", office } }, 6f);
            }
            Refresh();
        }
    }
}
