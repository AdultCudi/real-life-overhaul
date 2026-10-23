using System;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>The Tenders tab: works put out to tender (the city's, a business's, a public building), the player's bid and the award.</summary>
    public sealed partial class ConstructionApp
    {
        private int _bidFor, _bidPermille = 1000;
        private bool _bidFast;

        partial void WriteTenders(InfoBox box, BuildState state, ref string selected)
        {
            TenderFile picked = null;
            for (int i = state.Tenders.Count - 1; i >= 0; i--)
            {
                TenderFile t = state.Tenders[i];
                TableRow row = Pick("tnd:" + t.Id, ref selected);
                row.Set(0, _director.TenderName(t));
                row.Set(1, ConstructionText.Money(t.Value), t.Value);
                row.Set(2, ConstructionText.When(t.Closes), t.Closes);
                row.Set(3, t.BidPermille > 0 ? ConstructionText.Share(t.BidPermille) + (t.Fast ? " " + ConstructionText.T("reallife_bld_value_fast") : string.Empty) : "-");
                row.Set(4, TenderWords(t), t.State == TenderState.Won ? CellTone.Green : t.State == TenderState.Lost ? CellTone.Red : CellTone.Plain);
                if (row.Id == selected)
                {
                    picked = t;
                }
            }
            if (picked != null)
            {
                WriteTender(box, state, picked);
                return;
            }
            box.Title(ConstructionText.T("reallife_bld_tab_tenders"));
            box.Note(ConstructionText.T(state.Firm.Founded <= 0 ? "reallife_bld_tenders_no_company" : _director.Licensed ? "reallife_bld_tenders_pick" : "reallife_bld_tenders_no_licence"));
            Guide(box);
        }

        private string TenderWords(TenderFile t)
        {
            switch (t.State)
            {
                case TenderState.Open:
                    return ConstructionText.T("reallife_bld_tender_open");
                case TenderState.Won:
                    return ConstructionText.T("reallife_bld_tender_won");
                case TenderState.Lost:
                    return ConstructionText.T("reallife_bld_tender_lost", "rival", RivalName(t.Winner));
                default:
                    return t.Winner >= 0 ? ConstructionText.T("reallife_bld_tender_gone", "rival", RivalName(t.Winner)) : ConstructionText.T("reallife_bld_tender_lapsed");
            }
        }

        private static string RivalName(int rival)
        {
            return rival >= 0 && rival < BuildCatalogue.RivalCount ? ConstructionText.T(BuildCatalogue.RivalKeys[rival]) : ConstructionText.T("reallife_bld_rival_none");
        }

        private void WriteTender(InfoBox box, BuildState state, TenderFile t)
        {
            box.Title(_director.TenderName(t));
            box.Subtitle(ConstructionText.T(t.Source == TenderSource.CityWorks ? "reallife_bld_source_city" : t.Source == TenderSource.Public
                ? "reallife_bld_source_public" : t.Source == TenderSource.Landmark ? "reallife_lmk_source" : "reallife_bld_source_client"));
            int days = TenderRules.StandardDays(t.Hours);
            box.RowPair("reallife_bld_row_value", ConstructionText.Money(t.Value), "reallife_bld_row_hours", ConstructionText.N(t.Hours));
            box.RowPair("reallife_bld_row_usual_days", ConstructionText.N(days), "reallife_bld_row_closes", ConstructionText.When(t.Closes));
            Speciality need = TenderRules.NeedOf(t.Source, t.CivicKind, t.Use);
            if (need != Speciality.None)
            {
                bool ours = state.Firm.Speciality == need;
                box.Check(ConstructionText.T("reallife_bld_tender_suits", "speciality", ConstructionText.T(BuildCatalogue.SpecialityKeys[(int)need])), ours);
            }
            RingLines(box, state, t);
            if (t.State != TenderState.Open)
            {
                box.Section("reallife_bld_sec_award");
                if (t.Winner == -1)
                {
                    box.Line(ConstructionText.T("reallife_bld_tender_you_won", "price", ConstructionText.Money(t.WinningPrice), "days", t.WinningDays.ToString()), CellTone.Green);
                    if (t.Project > 0)
                    {
                        int project = t.Project;
                        InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bld_button_to_project", () => Jump(ConstructionDirector.TabProjects, "prj:" + project));
                    }
                }
                else if (t.Winner >= 0)
                {
                    box.Line(ConstructionText.T("reallife_bld_tender_rival_won", "rival", RivalName(t.Winner), "price", ConstructionText.Money(t.WinningPrice), "days", t.WinningDays.ToString()),
                        t.BidPermille > 0 ? CellTone.Red : CellTone.Plain);
                }
                BackButton(box);
                return;
            }
            if (state.Firm.Founded <= 0 || !_director.Licensed)
            {
                box.Line(ConstructionText.T(state.Firm.Founded <= 0 ? "reallife_bld_tenders_no_company" : "reallife_bld_tenders_no_licence"), CellTone.Yellow);
                BackButton(box);
                return;
            }
            if (_bidFor != t.Id)
            {
                _bidFor = t.Id;
                _bidPermille = t.BidPermille > 0 ? t.BidPermille : 1000;
                _bidFast = t.Fast;
            }
            if (_director.Approach(t) != null)
            {
                WriteApproach(box, state, t);
                return;
            }
            box.Section("reallife_bld_sec_bid");
            int steps = (BuildCatalogue.BidMostPermille - BuildCatalogue.BidLeastPermille) / BuildCatalogue.BidStepPermille + 1;
            int at = Mathf.Clamp((_bidPermille - BuildCatalogue.BidLeastPermille) / BuildCatalogue.BidStepPermille, 0, steps - 1);
            long value = t.Value;
            box.Selector(steps, at, i =>
            {
                int permille = BuildCatalogue.BidLeastPermille + i * BuildCatalogue.BidStepPermille;
                return ConstructionText.T("reallife_bld_sel_bid", "share", ConstructionText.Share(permille), "price", ConstructionText.Money(value * permille / 1000L));
            }, i => { _bidPermille = BuildCatalogue.BidLeastPermille + i * BuildCatalogue.BidStepPermille; Refresh(); });
            int fastDays = Math.Max(1, days * BuildCatalogue.FastPermille / 1000);
            box.Switch("reallife_bld_switch_fast", _bidFast, on => { _bidFast = on; Refresh(); });
            box.Note(ConstructionText.T("reallife_bld_bid_note", "days", (_bidFast ? fastDays : days).ToString(), "reputation", state.Firm.Reputation.ToString()));
            if (t.BidPermille > 0)
            {
                box.Line(ConstructionText.T("reallife_bld_bid_in", "share", ConstructionText.Share(t.BidPermille)), CellTone.Green);
            }
            Transform buttons = box.ButtonRow();
            int id = t.Id;
            InfoBox.Button(buttons, InfoBox.Positive, t.BidPermille > 0 ? "reallife_bld_button_bid_again" : "reallife_bld_button_bid",
                () => Answer(_director.Bid(state.Tender(id), _bidPermille, _bidFast)));
            if (t.BidPermille > 0)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_withdraw", () => { _director.Withdraw(state.Tender(id)); Refresh(); });
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            Guide(box);
        }

        // A ring's approach on an open tender: cover for a cut, turn it down, or report it to the city.
        private void WriteApproach(InfoBox box, BuildState state, TenderFile t)
        {
            RingFile g = _director.Approach(t);
            box.Section("reallife_bld_sec_ring");
            box.Line(ConstructionText.Fill("reallife_bld_ring_approach", "rival", RivalName(g.Leader), "share", ConstructionText.Share(RingRules.PlayerCoverPermille),
                "cut", ConstructionText.Share(RingRules.CutPermille)), CellTone.Yellow);
            box.Note(ConstructionText.T("reallife_bld_ring_note", "days", ConstructionText.N(RingRules.FoundDays)));
            Transform buttons = box.ButtonRow();
            int id = t.Id;
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_ring_join", () => Answer(_director.RingJoin(state.Tender(id))));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_button_ring_refuse", () => Answer(_director.RingRefuse(state.Tender(id))));
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_ring_report", () => Answer(_director.RingReport(state.Tender(id))));
            Guide(box);
        }

        // What the player knows of a ring on a tender: their answer, and the inquiry's finding.
        private void RingLines(InfoBox box, BuildState state, TenderFile t)
        {
            RingFile g = state.Ring(t.Id);
            if (g == null || (!g.Asked && g.Found == 0))
            {
                return;
            }
            if (g.Answer != RingFile.NoAnswer)
            {
                box.Line(ConstructionText.T(g.Answer == RingFile.Joined ? "reallife_bld_ring_you_joined" : g.Answer == RingFile.Reported ? "reallife_bld_ring_you_reported"
                    : "reallife_bld_ring_you_refused", "rival", RivalName(g.Leader)), g.Answer == RingFile.Joined ? CellTone.Red : CellTone.Plain);
            }
            if (g.Cut > 0)
            {
                box.Line(ConstructionText.T("reallife_bld_ring_cut_paid", "amount", ConstructionText.Money(g.Cut)), CellTone.Yellow);
            }
            if (g.Found > 0)
            {
                box.Line(ConstructionText.T("reallife_bld_ring_found", "names", _director.RingNames(g), "day", ConstructionText.Day(g.Found)), CellTone.Red);
            }
        }
    }
}
