using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class TechLive
    {
        private sealed partial class Page
        {
            // The terms of one offer or of the lead's line: each term and its value, with Law what they cost in a sale and which are hard, with Analysis or Statistics the offer against the market.
            private TechCard TermsCard(string title, Terms t, TermsCosts k, string marketLine)
            {
                var card = new TechCard(_runs, title);
                var rows = new List<TermsView.Row>(TermsCatalogue.Count);
                bool reads = k != null && TermsView.Reads;
                TermsView.Rows(_d, t, reads ? k : null, rows);
                for (int i = 0; i < rows.Count; i++)
                {
                    card.Row(rows[i].Label, rows[i].Value, rows[i].Hard ? UiRole.WarnText : UiRole.None);
                }
                if (reads)
                {
                    var lines = new List<string>(4);
                    TermsView.CostLines(_d, t, k, lines);
                    for (int i = 0; i < lines.Count; i++)
                    {
                        card.Say(lines[i], TermsView.AnyHard(k) && i == lines.Count - 1 ? UiRole.WarnText : UiRole.None);
                    }
                }
                else if (k != null)
                {
                    card.Say(TechText.T("reallife_vc_terms_law_hint"));
                }
                if (marketLine != null)
                {
                    card.Say(marketLine);
                }
                return card;
            }

            // The offers on the table, one card each, beside the raise.
            private UiFactCardModel[] OfferCards(FirmPlace firm, Cap c)
            {
                var cards = new List<UiFactCardModel>(3);
                for (int i = 0; c != null && i < c.Sheets.Count; i++)
                {
                    TermSheet t = c.Sheets[i];
                    string lead = VentureCatalogue.Firms[TalentCatalogue.Clamp(t.Vc, 0, VentureCatalogue.FirmCount - 1)].Name;
                    string market = TermsView.Sees ? TermsView.MarketLine(_d, "reallife_vc_terms_market", t.Pre, _d.MarketOf(firm.File.Place)) : null;
                    TechCard card = TermsCard(TechText.T("reallife_vc_terms_title", "vc", lead), t.T, _d.CostOf(c, t), market);
                    if (t.Pushed)
                    {
                        card.Say(TechText.T("reallife_vc_terms_pushed"));
                    }
                    cards.Add(card.Build());
                }
                return cards.ToArray();
            }

            // The terms the lead of a funded firm holds, on the firm's own page.
            private UiFactCardModel[] HeldCards(Cap c)
            {
                Holder lead = c != null && StartupMoney.Has(c) ? _d.LeadLine(c) : null;
                if (lead == null)
                {
                    return null;
                }
                TechCard card = TermsCard(TechText.T("reallife_vc_terms_held", "vc", VentureCatalogue.Firms[TalentCatalogue.Clamp(lead.Who, 0, VentureCatalogue.FirmCount - 1)].Name),
                    lead.T, lead.T != null ? _d.CostOfHeld(c, lead) : null, null);
                if (lead.T == null)
                {
                    card.Say(TechText.T("reallife_vc_terms_old"));
                }
                return new[] { card.Build() };
            }

            // The board's say over a firm's spending, in the firm account.
            private void RightsLines(TechCard books, Cap c)
            {
                long prov = VentureTerms.ProvisionOf(c);
                var info = new Terms { Info = VentureTerms.InfoOf(c) };
                books.Say(TechText.T("reallife_vc_acct_rights", "info", TermsView.Value(_d, info, TermsCatalogue.TInfo)));
                if (prov > 0)
                {
                    books.Say(TechText.T("reallife_vc_acct_provision", "limit", Cents(_d.Level(prov))));
                    if (VentureTerms.Approved(c, prov, _day))
                    {
                        books.Say(TechText.T("reallife_vc_acct_approved", "amount", Cents(_d.Level(c.Rt.Approved)), "day", c.Rt.ApprovedUntil.ToString()));
                    }
                    if (c.Rt.Breaches > 0 && _day - c.Rt.BreachDay < TermsCatalogue.BreachDays)
                    {
                        books.Say(TechText.T("reallife_vc_acct_breach", "count", c.Rt.Breaches.ToString()), UiRole.WarnText);
                    }
                }
            }

            private void RightsButtons(TechCard out1, string place, Cap c)
            {
                if (VentureTerms.ProvisionOf(c) > 0)
                {
                    out1.Button(TechText.T("reallife_vc_button_ask_spend"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.AskSpend(place)));
                }
                out1.Button(TechText.T("reallife_vc_button_ask_raise"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.AskRaise(place)));
            }

            // A startup the player may be in: what its lead holds, the offer against the going price, and a deck that looks stretched.
            private void StartupTerms(TechCard card, Startup s)
            {
                for (int i = s.Holders.Count - 1; i >= 0; i--)
                {
                    Holder h = s.Holders[i];
                    if (h.Kind == (int)HolderKind.Vc && h.T != null)
                    {
                        card.Say(TechText.T("reallife_vc_terms_lead", "vc", VentureCatalogue.Firms[TalentCatalogue.Clamp(h.Who, 0, VentureCatalogue.FirmCount - 1)].Name,
                            "terms", TermsView.Short(_d, h.T)));
                        break;
                    }
                }
                if (s.Ask != null && TermsView.Sees)
                {
                    card.Say(TermsView.MarketLine(_d, "reallife_vc_terms_ask_market", s.Ask.Pre, _d.GoingPrice(s)));
                }
                if (_d.DeckStretched(s))
                {
                    card.Say(TechText.T("reallife_vc_deck_stretched"), UiRole.WarnText);
                }
            }
        }
    }
}
