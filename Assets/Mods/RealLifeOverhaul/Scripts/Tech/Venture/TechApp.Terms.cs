using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The terms on the Tech app's older pages: an offer's, the lead's on the firm's page, the board's say in the firm account, and a startup's.</summary>
    public sealed partial class TechApp
    {
        // An offer's terms, line by line; Law adds what they cost in a sale and which are hard, Analysis or Statistics the offer against the market.
        private void WriteOffer(InfoBox box, FirmPlace firm, Cap c, TermSheet t)
        {
            TermsCosts k = _director.CostOf(c, t);
            WriteTerms(box, t.T, k);
            if (TermsView.Sees)
            {
                box.Line(TermsView.MarketLine(_director, "reallife_vc_terms_market", t.Pre, _director.MarketOf(firm.File.Place)));
            }
        }

        private void WriteTerms(InfoBox box, Terms t, TermsCosts k)
        {
            var rows = new List<TermsView.Row>(TermsCatalogue.Count);
            bool reads = k != null && TermsView.Reads;
            TermsView.Rows(_director, t, reads ? k : null, rows);
            for (int i = 0; i < rows.Count; i++)
            {
                box.Line(rows[i].Label + ": " + rows[i].Value, rows[i].Hard ? CellTone.Yellow : CellTone.Plain);
            }
            if (reads)
            {
                var lines = new List<string>(4);
                TermsView.CostLines(_director, t, k, lines);
                for (int i = 0; i < lines.Count; i++)
                {
                    box.Line(lines[i], TermsView.AnyHard(k) && i == lines.Count - 1 ? CellTone.Yellow : CellTone.Plain);
                }
            }
            else if (k != null)
            {
                box.Line(TechText.T("reallife_vc_terms_law_hint"));
            }
        }

        private void WriteHeld(InfoBox box, Cap c)
        {
            Holder lead = StartupMoney.Has(c) ? _director.LeadLine(c) : null;
            if (lead == null)
            {
                return;
            }
            box.Line(TechText.T("reallife_vc_terms_held", "vc", VentureCatalogue.Firms[TalentCatalogue.Clamp(lead.Who, 0, VentureCatalogue.FirmCount - 1)].Name));
            WriteTerms(box, lead.T, lead.T != null ? _director.CostOfHeld(c, lead) : null);
            if (lead.T == null)
            {
                box.Line(TechText.T("reallife_vc_terms_old"));
            }
        }

        // The board's say over a firm's spending in the firm account, and the two things the player can ask of it.
        private void WriteRights(InfoBox box, string place, Cap c, int day, Transform buttons)
        {
            long prov = VentureTerms.ProvisionOf(c);
            var info = new Terms { Info = VentureTerms.InfoOf(c) };
            box.Line(TechText.T("reallife_vc_acct_rights", "info", TermsView.Value(_director, info, TermsCatalogue.TInfo)));
            if (prov > 0)
            {
                box.Line(TechText.T("reallife_vc_acct_provision", "limit", TechText.Cents(_director.Level(prov))));
                if (VentureTerms.Approved(c, prov, day))
                {
                    box.Line(TechText.T("reallife_vc_acct_approved", "amount", TechText.Cents(_director.Level(c.Rt.Approved)), "day", c.Rt.ApprovedUntil.ToString()));
                }
                if (c.Rt.Breaches > 0 && day - c.Rt.BreachDay < TermsCatalogue.BreachDays)
                {
                    box.Line(TechText.T("reallife_vc_acct_breach", "count", c.Rt.Breaches.ToString()), CellTone.Yellow);
                }
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_vc_button_ask_spend", () => Do(() => _director.AskSpend(place)));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_vc_button_ask_raise", () => Do(() => _director.AskRaise(place)));
        }

        private void WriteStartupTerms(InfoBox box, Startup s)
        {
            for (int i = s.Holders.Count - 1; i >= 0; i--)
            {
                Holder h = s.Holders[i];
                if (h.Kind == (int)HolderKind.Vc && h.T != null)
                {
                    box.Line(TechText.T("reallife_vc_terms_lead", "vc", VentureCatalogue.Firms[TalentCatalogue.Clamp(h.Who, 0, VentureCatalogue.FirmCount - 1)].Name,
                        "terms", TermsView.Short(_director, h.T)));
                    break;
                }
            }
            if (s.Ask != null && TermsView.Sees)
            {
                box.Line(TermsView.MarketLine(_director, "reallife_vc_terms_ask_market", s.Ask.Pre, _director.GoingPrice(s)));
            }
            if (_director.DeckStretched(s))
            {
                box.Line(TechText.T("reallife_vc_deck_stretched"), CellTone.Yellow);
            }
        }
    }
}
