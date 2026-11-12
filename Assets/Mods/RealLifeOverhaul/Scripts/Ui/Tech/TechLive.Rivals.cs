using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Competition in niches on the kit: a niche's page says who sells there (price, share and money), a founded firm's page says who is coming at it and why, one short line a
    /// reaction, and the bake offs it can enter. Every button is the director's call and nothing is read in a frame: the page is written when it changes.
    /// </summary>
    public sealed partial class TechLive
    {
        private sealed partial class Page
        {
            private static readonly List<RivalryLine> RivalRows = new List<RivalryLine>(8);
            private static readonly List<RivalryReaction> RivalLog = new List<RivalryReaction>(8);
            private static readonly List<BakeOff> BakeRows = new List<BakeOff>(4);

            // The rivals that sell into a niche: one line each with the price it asks, its share of the niche's buyers and its money.
            private void NicheRivals(List<UiFactCardModel> into, int niche)
            {
                var card = new TechCard(_runs, TechText.T("reallife_rival_card_niche"));
                int n = _d.RivalsIn(niche, RivalRows);
                for (int i = 0; i < n && i < 6; i++)
                {
                    RivalryLine r = RivalRows[i];
                    string line = TechText.Fill("reallife_rival_row", "price", Cents(r.Price), "share", TechText.Mille(r.Share), "customers", TechText.Whole(r.Customers));
                    string money = r.Cash >= 0 ? Cents(r.Cash) : TechText.T("reallife_rival_listed");
                    string backed = !string.IsNullOrEmpty(r.Company) && r.Listed == false ? TechText.Fill("reallife_rival_backed_by", "company", r.Company) + ". " + line : line;
                    card.Who(TitleRules.By(r.Title, r.Name), backed, money, r.Funded ? UiRole.WarnText : UiRole.None);   // the product first, then who makes it
                }
                if (n == 0)
                {
                    card.Say(TechText.T("reallife_rival_none"));
                }
                else if (n > 6)
                {
                    card.Say(TechText.Fill("reallife_rival_more", "count", (n - 6).ToString(Inv)));
                }
                card.Say(TechText.T("reallife_rival_pool_about"));
                into.Add(card.Build());
            }

            // A founded firm's page: who is coming at it and why, a short line each, and the bake offs it can enter.
            private void FirmRivals(List<UiFactCardModel> into, List<UiFactCardModel> also, string place)
            {
                var coming = new TechCard(_runs, TechText.T("reallife_rival_card_firm"));
                int n = _d.ReactionsOf(place, RivalLog);
                for (int i = 0; i < n && i < 6; i++)
                {
                    RivalryReaction x = RivalLog[i];
                    coming.Say(TechText.Fill(ReactKind.LineKey(x.Kind), "rival", x.Name, "niche", x.Niche >= 0 ? TechText.T(NicheCatalogue.NameKey(x.Niche)) : "-",
                        "days", x.Amount.ToString(Inv), "features", x.Amount.ToString(Inv), "amount", Cents(Level(x.Amount)), "share", TechText.Mille((int)x.Amount),
                        "percent", TechText.Mille((int)x.Amount), "company", x.Other, "day", x.Day.ToString(Inv)));
                }
                if (n == 0)
                {
                    coming.Say(TechText.T("reallife_rival_firm_none"));
                }
                into.Add(coming.Build());
                var bake = new TechCard(_runs, TechText.T("reallife_rival_card_bake"));
                int b = _d.BakesOf(place, BakeRows);
                BakeOff open = _d.BakeFor(place);
                for (int i = 0; i < b; i++)
                {
                    BakeOff x = BakeRows[i];
                    string customer = x.Customer, niche = TechText.T(NicheCatalogue.NameKey(x.Niche));
                    if (x.State == BakeState.Open)
                    {
                        bake.Say(TechText.Fill(x.EntryOf(place) != null ? "reallife_rival_bake_entered_line" : "reallife_rival_bake_open_line", "customer", customer, "niche", niche,
                            "value", Cents(Level(x.Value)), "day", x.Closes.ToString(Inv)));
                    }
                    else if (x.State == BakeState.Won)
                    {
                        bake.Say(TechText.Fill("reallife_rival_bake_won_line", "customer", customer, "amount", Cents(Level(x.Booked))), x.Place == place ? UiRole.OkText : UiRole.None);
                    }
                    else
                    {
                        bake.Say(TechText.Fill(x.State == BakeState.Lost ? "reallife_rival_bake_lost_line" : "reallife_rival_bake_missed_line", "customer", customer, "winner", x.Winner),
                            UiRole.WarnText);
                    }
                }
                if (b == 0)
                {
                    bake.Say(TechText.T("reallife_rival_bake_none"));
                }
                if (open != null)
                {
                    bake.Button(TechText.T("reallife_tech_desk_bake_c0"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.EnterBake(place, 0)));
                    bake.Button(TechText.T("reallife_tech_desk_bake_c1"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.EnterBake(place, 1)));
                    bake.Button(TechText.T("reallife_tech_desk_bake_c2"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.EnterBake(place, 2)));
                    bake.Say(TechText.T("reallife_rival_bake_about"));
                }
                also.Add(bake.Build());
            }
        }
    }
}
