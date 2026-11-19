using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flats and studios on the kit's Property app: each unit for sale is a row of the Market tab beside the buildings and hotels, its page
    /// buys it for cash or on a mortgage (a deposit and a term stepped on the page), and every flat held is a row of the rent roll with a page
    /// for its figures, its letting and its sale. Every press goes to <see cref="EstateDirector"/>'s own call, asked first in the game's
    /// question box where money moves.
    /// </summary>
    public sealed partial class EstateLive
    {
        private sealed partial class Page
        {
            private const string FlatPick = "flt:";

            private static void AskFlat(string titleKey, string body, Action yes, bool negative)
            {
                GameConfirm.Show(titleKey.GetLocalization(), body, () => UiClone.Safe(yes), null, null, null, false, negative);
            }

            // The Market tab's rows: a row a unit for sale, with its price, its condition and the rent it would let for.
            private void FlatRows(List<UiDataRow> rows, List<string> keys, List<int> locate, List<string> where)
            {
                IReadOnlyList<FlatListing> list = _d.On(EstatePart.Flats) ? _d.FlatListings() : null;
                for (int i = 0; list != null && i < list.Count; i++)
                {
                    FlatListing l = list[i];
                    int rent = EstateRules.MarketRent(l.Price, (int)EstateUse.Homes, l.Income, l.Cond, 0, _c);
                    int yieldHundredths = rent > 0 && l.Price > 0 ? (int)((long)rent * _c.DaysPerYear * 10000 / l.Price) : -1;
                    locate.Add(-1);   // a unit has no building of its own to move the camera to
                    where.Add(EstateText.District(l.District));
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { EstateDirector.FlatName(l.Name, l.Kind), Money(l.Price), (l.Cond / 10).ToString(Inv), rent > 0 ? Month(rent) : string.Empty,
                            yieldHundredths >= 0 ? YieldOne(yieldHundredths) : string.Empty, string.Empty },
                        Values = new[] { (int)UiIcon.Home, 0, l.Cond / 10, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.Pair(EstateText.T("reallife_flat_kind_filter"), EstateText.District(l.District)), null, null, null, null, null },
                        SortKeys = new[] { N, l.Price, l.Cond / 10, rent > 0 ? (long)rent * _monthDays : N, yieldHundredths >= 0 ? yieldHundredths : N, N },
                        Kinds = new[] { 0, EstateTables.IsFlat }
                    });
                    keys.Add(FlatPick + l.Key);
                }
            }

            // The rent roll's rows: a row a flat held, which opens its page.
            private void FlatHeld(List<UiDataRow> rows, List<string> keys)
            {
                FlatBook book = _d.On(EstatePart.Flats) ? _d.Flats : null;
                for (int i = 0; book != null && i < book.Flats.Count; i++)
                {
                    OwnedFlat f = book.Flats[i];
                    string name = EstateDirector.FlatName(f.Name, f.Kind);
                    int income = f.Let && f.Unit.Paying ? f.Unit.Rent : 0;
                    var card = new UiDataCard
                    {
                        Column = 3, Title = name,
                        Labels = new[] { EstateText.T("reallife_estate_col_worth"), EstateText.T("reallife_estate_col_paid") },
                        Values = new[] { Money(f.Value), Money(f.Paid) }
                    };
                    rows.Add(OtherRow(UiIcon.Home, name, FlatLine(f), income, EstateText.T("reallife_flat_kind_filter"), f.Let && f.Unit.Arrears > 0 ? UiRole.Danger : UiRole.None,
                        EstateTables.OwnFlat, card));
                    keys.Add(FlatPick + f.Key);
                }
            }

            private static string FlatLine(OwnedFlat f)
            {
                EstateHolding h = f.Hold;
                if (h.SaleDay > 0)
                {
                    return EstateText.T("reallife_estate_state_sold", "day", h.SaleDay.ToString(Inv));
                }
                if (!h.Managed)
                {
                    return EstateText.T("reallife_flat_state_home");
                }
                EstateUnit u = f.Unit;
                return !u.Let ? EstateText.Empty(u.VoidDays) : u.Arrears > 0 ? EstateText.T("reallife_flat_state_arrears", "days", u.ArrearsDays.ToString(Inv))
                    : EstateText.T("reallife_estate_state_let", "who", EstateText.Tenant(h.Use, u.Tenant));
            }

            // A flat's page: a listing on the Market tab, one held on the Mine tab. True if one was written.
            private bool FlatPage()
            {
                string key = Picked(_v.Pick, FlatPick);
                if (key == null || !_d.On(EstatePart.Flats))
                {
                    return false;
                }
                OwnedFlat held = _d.Flats != null ? _d.Flats.Find(key) : null;
                if (held != null)
                {
                    _v.Tab = Array.IndexOf(NavTab, EstateDirector.TabMine);   // a flat held is looked after on the rent roll's page
                    _m.Tab = _v.Tab;
                    HeldPage(held);
                    return true;
                }
                string place;
                int slot;
                EstateApp.FlatPlace(key, out place, out slot);
                FlatListing l;
                if (!_d.ListingOf(place, slot, out l))
                {
                    return false;
                }
                _v.Tab = Array.IndexOf(NavTab, EstateDirector.TabMarket);
                _m.Tab = _v.Tab;
                ListingFlat(l);
                return true;
            }

            private void FlatCard(UiIcon icon, string title, string sub, string chip, UiRole chipTone)
            {
                _m.Shape = EstateShape.Detail;
                _m.Card = true;   // a decision about one thing: a card over the dimmed world, as tall as its words
                _m.FootBack = EstateText.T("reallife_ui_estate_not_now");
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = icon;
                _m.DetailTitle = title;
                _m.DetailSub = sub;
                _m.DetailChip = chip;
                _m.DetailChipTone = chipTone;
            }

            private void ListingFlat(FlatListing l)
            {
                string key = l.Key, name = EstateDirector.FlatName(l.Name, l.Kind);
                int price = l.Price, rent = EstateRules.MarketRent(price, (int)EstateUse.Homes, l.Income, l.Cond, 0, _c);
                bool runDown = EstateRules.RunDown(l.Cond);
                FlatCard(UiIcon.Home, name, EstateText.T("reallife_flat_sub", "kind", EstateText.T(EstateDirector.FlatKindKeys[l.Kind]), "district", EstateText.District(l.District)),
                    EstateText.Level(l.Cond), runDown ? UiRole.Warn : UiRole.Ok);
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = EstateText.T("reallife_flat_l_price"), Value = Money(price) },
                    new UiFigureModel { Label = EstateText.T("reallife_flat_l_floor"), Value = EstateText.T("reallife_flat_sqm", "sqm", l.Sqm.ToString(Inv)) },
                    new UiFigureModel { Label = EstateText.T("reallife_flat_l_freehold"), Value = Money(l.Freehold) },
                    new UiFigureModel { Label = EstateText.T("reallife_flat_l_lease"), Value = EstateText.Share(FlatCatalogue.LeaseShare) }
                };
                int percent = EstateCatalogue.Clamp(_v.FlatPercent, ProductCatalogue.HomeDepositLeast, ProductCatalogue.HomeDepositMost);
                int term = EstateCatalogue.Clamp(_v.FlatTerm, ProductCatalogue.HomeTermLeast, ProductCatalogue.HomeTermMost);
                int financed, instalment;
                ProductAnswer answer = _d.FlatMortgage(price, percent, term, out financed, out instalment);
                var costs = new List<UiFactLine>(6)
                {
                    UiFactLine.Row(EstateText.T("reallife_flat_l_service"), Money(FlatRules.ServiceMonth(rent, _monthDays))),
                    UiFactLine.Row(EstateText.T("reallife_flat_l_tax"), Money(FlatRules.TaxMonth(l.TaxYear, l.Share))),
                    UiFactLine.Row(EstateText.T("reallife_flat_l_owner"), string.IsNullOrEmpty(l.Landlord) ? EstateText.T("reallife_flat_landlord") : l.Landlord)
                };
                if (rent > 0)
                {
                    costs.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_col_rent_month"), Month(rent)));
                    costs.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_listing_yield"), YieldOne((int)((long)rent * _c.DaysPerYear * 10000 / Math.Max(1, price)))));
                }
                if (l.Renting)
                {
                    costs.Add(UiFactLine.Say(EstateText.T("reallife_flat_renting"), UiRole.OkText));
                }
                costs.Add(UiFactLine.Say(EstateText.T("reallife_flat_how"), UiRole.Muted));
                costs.Add(UiFactLine.Say(GuideNote(EstateDirector.TabMarket, FlatPick), UiRole.Muted));   // a decision card has no free foot to hold the window's guide line
                _m.Left = new[] { new UiFactCardModel { Title = EstateText.T("reallife_flat_sec_bills"), Heading = true, Lines = costs.ToArray() } };
                var how = new List<UiFactLine>(4)
                {
                    _runs.Step(EstateText.T("reallife_flat_l_deposit"), EstateText.T("reallife_flat_deposit_at", "percent", percent.ToString(Inv)), null,
                        percent > ProductCatalogue.HomeDepositLeast, percent < ProductCatalogue.HomeDepositMost,
                        () => Show(() => _v.FlatPercent = percent - ProductCatalogue.HomeDepositStep), () => Show(() => _v.FlatPercent = percent + ProductCatalogue.HomeDepositStep)),
                    _runs.Step(EstateText.T("reallife_flat_l_term"), EstateText.T("reallife_flat_term_at", "days", term.ToString(Inv)), null,
                        term > ProductCatalogue.HomeTermLeast, term < ProductCatalogue.HomeTermMost,
                        () => Show(() => _v.FlatTerm = term - ProductCatalogue.HomeTermStep), () => Show(() => _v.FlatTerm = term + ProductCatalogue.HomeTermStep)),
                    answer == ProductAnswer.Yes
                        ? UiFactLine.Say(EstateText.T("reallife_flat_mortgage_line", "deposit", Money(price - financed), "financed", Money(financed), "payment", Money(instalment)))
                        : UiFactLine.Say(EstateText.T("reallife_flat_mortgage_no"), UiRole.WarnText)
                };
                int shown = price;
                var actions = new List<UiActionModel>(2);
                if (answer == ProductAnswer.Yes)
                {
                    actions.Add(_runs.Button(EstateText.T("reallife_flat_btn_buy_mortgage"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => AskFlat("reallife_flat_q_buy_title",
                        EstateText.Fill("reallife_flat_q_buy_mortgage", "flat", name, "deposit", Money(shown - financed), "financed", Money(financed), "payment", Money(instalment)),
                        () => Bought(key, shown, percent, term), false))));
                }
                actions.Add(_runs.Button(EstateText.T("reallife_flat_btn_buy_cash"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() => AskFlat("reallife_flat_q_buy_title",
                    EstateText.T("reallife_flat_q_buy_cash", "flat", name, "amount", Money(shown)), () => Bought(key, shown, FlatCatalogue.PayInFull, term), false))));
                _m.Right = new[] { new UiFactCardModel { Title = EstateText.T("reallife_flat_sec_mortgage"), Heading = true, Lines = how.ToArray(), Actions = actions.ToArray() } };
            }

            private void Bought(string key, int shown, int percent, int term)
            {
                string place;
                int slot;
                EstateApp.FlatPlace(key, out place, out slot);
                if (_d.BuyFlat(place, slot, shown, percent, term))
                {
                    Open(FlatPick + key);
                }
                else
                {
                    _live._local++;
                }
            }

            private void HeldPage(OwnedFlat f)
            {
                string key = f.Key, name = EstateDirector.FlatName(f.Name, f.Kind);
                EstateHolding h = f.Hold;
                EstateUnit u = f.Unit;
                int owed = _d.FlatOwed(f), day = _day;
                FlatCard(UiIcon.Home, name, EstateText.T("reallife_flat_sub", "kind", EstateText.T(EstateDirector.FlatKindKeys[f.Kind]), "district", EstateText.District(f.District)),
                    FlatLine(f), h.Offer > 0 ? UiRole.Ok : UiRole.Notice);
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = EstateText.T("reallife_ibox_worth"), Value = Money(f.Value) },
                    new UiFigureModel { Label = EstateText.T("reallife_estate_col_paid"), Value = Money(f.Paid) },
                    new UiFigureModel { Label = EstateText.T("reallife_flat_l_owed"), Value = Money(owed) },
                    new UiFigureModel { Label = EstateText.T("reallife_flat_l_service"), Value = Money(_d.FlatService(f)) }
                };
                var own = new List<UiFactLine>(10)
                {
                    UiFactLine.Row(EstateText.T("reallife_flat_l_tax"), Money(_d.FlatTax(f))),
                    UiFactLine.Row(EstateText.T("reallife_flat_l_owner"), string.IsNullOrEmpty(f.Landlord) ? EstateText.T("reallife_flat_landlord") : f.Landlord),
                    UiFactLine.Row(EstateText.T("reallife_estate_col_year_in"), Money(h.RentYear)),
                    UiFactLine.Row(EstateText.T("reallife_estate_col_year_out"), Money(h.CostYear))
                };
                var actions = new List<UiActionModel>(3);
                if (!h.Managed)
                {
                    own.Add(UiFactLine.Say(EstateText.T("reallife_flat_home_what")));
                    if (h.SaleDay == 0)
                    {
                        actions.Add(_runs.Button(EstateText.T("reallife_flat_btn_let"), UiButtonKind.Secondary, UiIcon.Key, UiAction.Option, () => Show(() => _d.LetFlat(key, true))));
                    }
                }
                else if (u.Let)
                {
                    own.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_let", "who", EstateText.Tenant(h.Use, u.Tenant), "amount", Money(u.Rent),
                        "yield", EstateText.Yield((int)((long)u.Rent * _c.DaysPerYear * 10000 / Math.Max(1, f.Value))))));
                    if (u.Arrears > 0)
                    {
                        own.Add(UiFactLine.Say(EstateText.T("reallife_estate_ten_arrears", "amount", Money(u.Arrears), "days", u.ArrearsDays.ToString(Inv),
                            "back", EstateText.Share(EstateRules.CatchUpChance(u.Record))), UiRole.DangerText));
                    }
                    if (EstateRules.MayServeNotice(h, 0, day))
                    {
                        actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_notice"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _d.ServeFlatNotice(key))));
                    }
                    if (EstateRules.MayGoToCourt(h, 0, day))
                    {
                        actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_court"), UiButtonKind.Secondary, UiIcon.None, UiAction.Alt, () => Show(() => _d.FlatToCourt(key))));
                    }
                }
                else
                {
                    int market = EstateRules.MarketRent(h, _c);
                    int ask = EstateCatalogue.Clamp(u.Ask, EstateCatalogue.AskLeast, EstateCatalogue.AskMost);
                    own.Add(UiFactLine.Say(EstateText.T("reallife_flat_own_empty", "days", u.VoidDays.ToString(Inv), "count", u.Applicants.Count.ToString(Inv)), UiRole.WarnText));
                    own.Add(_runs.Step(EstateText.T("reallife_estate_ten_asking"), EstateText.Share(ask),
                        EstateText.T("reallife_estate_ten_asking_what", "amount", Money(EstateCatalogue.Share(market, ask)), "chance",
                            EstateText.Share(EstateRules.ApplicantChance(ask, h.Cond, _c.Cycle))),
                        ask > EstateCatalogue.AskLeast, ask < EstateCatalogue.AskMost,
                        () => Show(() => _d.SetFlatAsk(key, ask - EstateCatalogue.AskStep)), () => Show(() => _d.SetFlatAsk(key, ask + EstateCatalogue.AskStep))));
                    if (u.Applicants.Count > 0)
                    {
                        RentApplicant a = u.Applicants[0];
                        int id = a.Id;
                        own.Add(UiFactLine.Say(EstateText.Fill("reallife_flat_applicant", "who", EstateText.Tenant(h.Use, a.Kind), "amount", Money(a.Offer), "record", EstateText.Record(a.Record),
                            "day", a.Lapse.ToString(Inv)), UiRole.OkText));
                        actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _d.RefuseFlatApplicant(key, id))));
                        actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_accept"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() => _d.AcceptFlatApplicant(key, id))));
                    }
                    else
                    {
                        actions.Add(_runs.Button(EstateText.T("reallife_flat_btn_stop_let"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _d.LetFlat(key, false))));
                    }
                }
                if (h.Managed && _d.On(EstatePart.Duties))
                {
                    int manager = h.Manager;
                    own.Add(_runs.Step(EstateText.T("reallife_estate_duty_who"), EstateText.Manager(manager), null, manager > 0, manager < EstateCatalogue.Managers - 1,
                        () => Show(() => _d.SetManager(key, manager - 1)), () => Show(() => _d.SetManager(key, manager + 1))));
                }
                if (h.Cond < EstateCatalogue.ConditionNew && !h.Busy && h.SaleDay == 0)
                {
                    own.Add(UiFactLine.Say(EstateText.T("reallife_flat_repair_what", "amount", Money(EstateRules.Quote(h, EstateWork.Repair, -1, _c)), "condition", EstateText.Level(h.Cond))));
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_repair"), UiButtonKind.Secondary, UiIcon.Wrench, UiAction.Option,
                        () => Show(() => _d.StartWork(key, EstateWork.Repair, -1))));
                }
                else if (h.Busy)
                {
                    own.Add(UiFactLine.Say(EstateText.T("reallife_estate_state_works", "work", EstateText.Work(h.Work), "day", h.WorkEnd.ToString(Inv))));
                }
                _m.Left = new[] { new UiFactCardModel { Title = EstateText.T("reallife_flat_sec_yours"), Heading = true, Lines = own.ToArray(), Actions = actions.ToArray() } };
                var sell = new List<UiFactLine>(6);
                var sellActions = new List<UiActionModel>(2);
                if (h.SaleDay > 0)
                {
                    sell.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_sale_agreed", "amount", Money(h.SalePrice), "day", h.SaleDay.ToString(Inv),
                        "fee", Money(EstateRules.AgentFee(h.SalePrice, _c.Index))), UiRole.OkText));
                }
                else if (h.Offer > 0)
                {
                    int fee = EstateRules.AgentFee(h.Offer, _c.Index), tax = EstateRules.GainsTax(h.Offer, fee, f.Paid, h.Spent, _c.TaxPercent), offer = h.Offer;
                    sell.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_offer", "amount", Money(offer), "day", h.OfferLapse.ToString(Inv), "asked", Money(h.List)), UiRole.OkText));
                    sell.Add(UiFactLine.Say(EstateText.Fill("reallife_flat_offer_net", "fee", Money(fee), "tax", Money(tax), "owed", Money(owed),
                        "amount", Money(Math.Max(0, offer - fee - tax - owed)))));
                    sellActions.Add(_runs.Button(EstateText.T("reallife_estate_btn_refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _d.RefuseFlatOffer(key))));
                    sellActions.Add(_runs.Button(EstateText.T("reallife_estate_btn_accept"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() => AskFlat("reallife_flat_q_sell_title",
                        EstateText.T("reallife_flat_q_sell", "flat", name, "amount", Money(offer)), () => _d.AcceptFlatOffer(key), true))));
                }
                else if (h.List > 0)
                {
                    sell.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_listed", "amount", Money(h.List), "chance", EstateText.Share(EstateRules.SaleChance(h.List, f.Value, _c.Cycle)))));
                    sellActions.Add(_runs.Button(EstateText.T("reallife_estate_btn_unlist"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _d.ListFlat(key, 0))));
                }
                else
                {
                    int at = EstateCatalogue.Clamp(_v.ListShare, 0, ListShares.Length - 1), share = ListShares[at], asked = EstateCatalogue.Share(f.Value, share);
                    sell.Add(_runs.Step(EstateText.T("reallife_flat_l_asking"), EstateText.Share(share),
                        EstateText.T("reallife_flat_list_what", "amount", Money(asked), "chance", EstateText.Share(EstateRules.SaleChance(asked, f.Value, _c.Cycle)),
                            "fee", EstateText.Share(EstateCatalogue.AgentFee)),
                        at > 0, at < ListShares.Length - 1, () => Show(() => _v.ListShare = at - 1), () => Show(() => _v.ListShare = at + 1)));
                    sell.Add(UiFactLine.Say(EstateText.T("reallife_flat_sell_what"), UiRole.Muted));
                    sellActions.Add(_runs.Button(EstateText.T("reallife_estate_btn_list"), UiButtonKind.Primary, UiIcon.Tag, UiAction.Alt, () => Show(() => _d.ListFlat(key, share))));
                }
                sell.Add(UiFactLine.Say(GuideNote(EstateDirector.TabMine, FlatPick), UiRole.Muted));
                _m.Right = new[] { new UiFactCardModel { Title = EstateText.T("reallife_estate_own_selling"), Heading = true, Lines = sell.ToArray(), Actions = sellActions.ToArray() } };
            }
        }
    }
}
