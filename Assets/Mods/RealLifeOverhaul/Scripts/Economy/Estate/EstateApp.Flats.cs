using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Flats and studios in the Property app in the game's frame: a row a unit for sale on the Market tab with a page that buys it (cash, or
    /// a mortgage at a deposit and a term chosen on the page), a row a flat held on the Mine tab with a page for its figures, its letting and
    /// its sale. Written only when a page is; every press goes to <see cref="EstateDirector"/>'s own call, asked first where money moves.
    /// </summary>
    public sealed partial class EstateApp
    {
        private const string FlatRow = "flt:";
        private const string FlatGuideTitle = "reallife_meta_title_economy_estate_flats";

        private int _flatPercent = ProductCatalogue.HomeDepositLeast;   // the deposit showing on a flat's page, per cent; 100 is never offered here: cash is a button of its own
        private int _flatTerm = ProductCatalogue.HomeTermLeast;         // the mortgage's term showing, days

        private static void AskFlat(string titleKey, string body, Action yes, bool negative)
        {
            GameConfirm.Show(titleKey.GetLocalization(), body, () => UiClone.Safe(yes), null, null, null, false, negative);
        }

        // The Market tab's rows: a row a unit for sale. True if one is picked.
        private bool WriteFlatRows(List<string> districts, ref string selected, out FlatListing picked)
        {
            picked = default(FlatListing);
            if (!_director.On(EstatePart.Flats))
            {
                return false;
            }
            EstateClimate c = _director.Climate;
            IReadOnlyList<FlatListing> list = _director.FlatListings();
            bool any = false;
            for (int i = 0; i < list.Count; i++)
            {
                FlatListing l = list[i];
                string district = EstateText.District(l.District);
                if (Pick(FlatRow + l.Key, ref selected, out TableRow row))
                {
                    picked = l;
                    any = true;
                }
                int rent = EstateRules.MarketRent(l.Price, (int)EstateUse.Homes, l.Income, l.Cond, 0, c);
                row.Kind = KindOf(districts, district);
                row.Kind2 = EstateTables.IsFlat;
                row.NoTotal = true;   // a flat is not a building for sale: the Market's total of prices is the buildings' alone
                row.Set(0, EstateDirector.FlatName(l.Name, l.Kind));
                row.Set(1, district);
                row.Set(2, EstateText.T("reallife_flat_kind_filter"));
                row.Set(3, EstateText.Money(l.Price), l.Price);
                row.Set(4, EstateText.Level(l.Cond), l.Cond, EstateRules.RunDown(l.Cond) ? TableRow.Yellow : TableRow.Plain);
                if (rent > 0 && l.Price > 0)
                {
                    row.Set(5, EstateText.Money(rent), rent);
                    row.Set(6, EstateText.Yield((int)((long)rent * c.DaysPerYear * 10000 / l.Price)), (double)rent * c.DaysPerYear * 100.0 / l.Price);
                }
            }
            return any;
        }

        // The Mine tab's rows: a row a flat the player holds. Returns the one picked, or null.
        private OwnedFlat WriteFlatsHeld(ref string selected)
        {
            FlatBook book = _director.Flats;
            OwnedFlat picked = null;
            for (int i = 0; _director.On(EstatePart.Flats) && book != null && i < book.Flats.Count; i++)
            {
                OwnedFlat f = book.Flats[i];
                if (Pick(FlatRow + f.Key, ref selected, out TableRow row))
                {
                    picked = f;
                }
                int income = f.Let ? f.Unit.Paying ? f.Unit.Rent : 0 : 0;
                row.Kind = EstateTables.OwnFlat;
                row.NoTotal = true;   // the buildings' worth, rent and year figures are theirs alone
                row.Set(0, EstateDirector.FlatName(f.Name, f.Kind));
                row.Set(1, EstateText.T("reallife_flat_kind_filter"));
                row.Set(2, FlatState(f));
                row.Set(3, EstateText.Money(f.Value), f.Value);
                row.Set(4, EstateText.Money(f.Paid), f.Paid);
                row.Set(5, EstateText.Money(income), income);
                row.Set(6, EstateText.Money(f.Hold.RentYear), f.Hold.RentYear);
                row.Set(7, EstateText.Money(f.Hold.CostYear), f.Hold.CostYear);
            }
            return picked;
        }

        private static string FlatState(OwnedFlat f)
        {
            EstateHolding h = f.Hold;
            if (h.SaleDay > 0)
            {
                return EstateText.T("reallife_estate_state_sold", "day", h.SaleDay.ToString());
            }
            if (h.Managed)
            {
                EstateUnit u = f.Unit;
                return !u.Let ? EstateText.Empty(u.VoidDays) : u.Arrears > 0 ? EstateText.T("reallife_flat_state_arrears", "days", u.ArrearsDays.ToString())
                    : EstateText.T("reallife_estate_state_let", "who", EstateText.Tenant(h.Use, u.Tenant));
            }
            return EstateText.T("reallife_flat_state_home");
        }

        // A unit for sale: what makes its price, the bills that come with it, and buying it for cash or on a mortgage.
        private void WriteFlatListing(InfoBox box, FlatListing l, EstateClimate c)
        {
            string key = l.Key, name = EstateDirector.FlatName(l.Name, l.Kind);
            int price = l.Price, rent = EstateRules.MarketRent(price, (int)EstateUse.Homes, l.Income, l.Cond, 0, c);
            int monthDays = MacroCatalogue.MonthDays(Math.Max(1, c.DaysPerYear));
            box.Title(name);
            box.Subtitle(EstateText.T("reallife_flat_sub", "kind", EstateText.T(EstateDirector.FlatKindKeys[l.Kind]), "district", EstateText.District(l.District)));
            box.Chip(EstateText.Level(l.Cond), EstateRules.RunDown(l.Cond) ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_ibox_sec_money");
            box.Row("reallife_flat_l_price", EstateText.Money(price));
            box.Line(EstateText.Fill("reallife_flat_price_made", "share", EstateText.Share(l.Share / 1000), "sqm", l.Sqm.ToString(), "freehold", EstateText.Money(l.Freehold),
                "lease", EstateText.Share(FlatCatalogue.LeaseShare)));
            box.Section("reallife_flat_sec_bills");
            box.Line(EstateText.T("reallife_flat_bills", "service", EstateText.Money(FlatRules.ServiceMonth(rent, monthDays)), "tax", EstateText.Money(FlatRules.TaxMonth(l.TaxYear, l.Share)),
                "owner", string.IsNullOrEmpty(l.Landlord) ? EstateText.T("reallife_flat_landlord") : l.Landlord));
            if (rent > 0)
            {
                box.Line(EstateText.T("reallife_flat_let_rent", "amount", EstateText.Money(rent), "yield", EstateText.Yield((int)((long)rent * c.DaysPerYear * 10000 / Math.Max(1, price)))));
            }
            if (l.Renting)
            {
                box.Line(EstateText.T("reallife_flat_renting"), CellTone.Green);
            }
            box.Section("reallife_flat_sec_mortgage");
            int least = ProductCatalogue.HomeDepositLeast, most = ProductCatalogue.HomeDepositMost, step = ProductCatalogue.HomeDepositStep;
            _flatPercent = EstateCatalogue.Clamp(_flatPercent, least, most);
            if (!box.HasSelector || !box.Selector((most - least) / step + 1, (_flatPercent - least) / step, i => EstateText.T("reallife_flat_deposit_at", "percent", (least + i * step).ToString()),
                i =>
                {
                    _flatPercent = least + i * step;
                    Refresh();
                }))
            {
                box.Line(EstateText.T("reallife_flat_deposit_at", "percent", _flatPercent.ToString()));
            }
            int termLeast = ProductCatalogue.HomeTermLeast, termMost = ProductCatalogue.HomeTermMost, termStep = ProductCatalogue.HomeTermStep;
            _flatTerm = EstateCatalogue.Clamp(_flatTerm, termLeast, termMost);
            if (!box.HasSelector || !box.Selector((termMost - termLeast) / termStep + 1, (_flatTerm - termLeast) / termStep,
                i => EstateText.T("reallife_flat_term_at", "days", (termLeast + i * termStep).ToString()), i =>
                {
                    _flatTerm = termLeast + i * termStep;
                    Refresh();
                }))
            {
                box.Line(EstateText.T("reallife_flat_term_at", "days", _flatTerm.ToString()));
            }
            int financed, instalment;
            ProductAnswer answer = _director.FlatMortgage(price, _flatPercent, _flatTerm, out financed, out instalment);
            if (answer == ProductAnswer.Yes)
            {
                box.Line(EstateText.T("reallife_flat_mortgage_line", "deposit", EstateText.Money(price - financed), "financed", EstateText.Money(financed), "payment", EstateText.Money(instalment)));
            }
            else
            {
                box.Line(EstateText.T("reallife_flat_mortgage_no"), CellTone.Yellow);
            }
            int percent = _flatPercent, term = _flatTerm, shown = price;
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_flat_btn_buy_cash", () => AskFlat("reallife_flat_q_buy_title",
                EstateText.T("reallife_flat_q_buy_cash", "flat", name, "amount", EstateText.Money(shown)), () => FlatBought(key, shown, FlatCatalogue.PayInFull, term), false));
            if (answer == ProductAnswer.Yes)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_flat_btn_buy_mortgage", () => AskFlat("reallife_flat_q_buy_title",
                    EstateText.Fill("reallife_flat_q_buy_mortgage", "flat", name, "deposit", EstateText.Money(shown - financed), "financed", EstateText.Money(financed),
                        "payment", EstateText.Money(instalment)),
                    () => FlatBought(key, shown, percent, term), false));
            }
            BackButton(box.ButtonRow());
            box.Note(EstateText.T("reallife_flat_how"));
            Meta.Guide.Point(box, FlatGuideTitle);
        }

        private void FlatBought(string key, int shown, int percent, int term)
        {
            string place;
            int slot;
            FlatPlace(key, out place, out slot);
            if (_director.BuyFlat(place, slot, shown, percent, term))
            {
                _tab = EstateDirector.TabMine;
                _pick = FlatRow + key;
            }
            Refresh();
        }

        // "flat:<place>:<slot>" into its building and unit.
        internal static void FlatPlace(string key, out string place, out int slot)
        {
            place = string.Empty;
            slot = 0;
            int at = key != null ? key.LastIndexOf(':') : -1;
            if (at > 5 && int.TryParse(key.Substring(at + 1), out slot))
            {
                place = key.Substring(5, at - 5);
            }
        }

        // One flat held: its worth and what it cost, its bills, its letting and its sale.
        private void WriteFlat(InfoBox box, OwnedFlat f, EstateClimate c)
        {
            string key = f.Key, name = EstateDirector.FlatName(f.Name, f.Kind);
            EstateHolding h = f.Hold;
            EstateUnit u = f.Unit;
            int owed = _director.FlatOwed(f);
            box.Title(name);
            box.Subtitle(EstateText.T("reallife_flat_sub", "kind", EstateText.T(EstateDirector.FlatKindKeys[f.Kind]), "district", EstateText.District(f.District)));
            box.Chip(FlatState(f), h.Offer > 0 ? CellTone.Green : CellTone.Plain);
            box.Section("reallife_ibox_sec_money");
            box.Row("reallife_ibox_worth", EstateText.Money(f.Value));
            box.Line(EstateText.T("reallife_flat_own_paid", "paid", EstateText.Money(f.Paid), "day", f.BoughtDay.ToString()));
            if (owed > 0)
            {
                box.Line(EstateText.T("reallife_flat_own_owed", "amount", EstateText.Money(owed)));
            }
            box.Line(EstateText.T("reallife_flat_bills", "service", EstateText.Money(_director.FlatService(f)), "tax", EstateText.Money(_director.FlatTax(f)),
                "owner", string.IsNullOrEmpty(f.Landlord) ? EstateText.T("reallife_flat_landlord") : f.Landlord));
            box.Line(EstateText.T("reallife_estate_own_year", "rent", EstateText.Money(h.RentYear), "costs", EstateText.Money(h.CostYear), "spent", EstateText.Money(h.Spent)));
            box.Section("reallife_ibox_sec_letting");
            if (!h.Managed)
            {
                box.Note(EstateText.T("reallife_flat_home_what"));
                if (h.SaleDay == 0)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_flat_btn_let", () => _director.LetFlat(key, true));
                }
            }
            else
            {
                int market = EstateRules.MarketRent(h, c);
                if (u.Let)
                {
                    box.Line(EstateText.T("reallife_estate_own_let", "who", EstateText.Tenant(h.Use, u.Tenant), "amount", EstateText.Money(u.Rent),
                        "yield", EstateText.Yield((int)((long)u.Rent * c.DaysPerYear * 10000 / Math.Max(1, f.Value)))));
                    if (u.Arrears > 0)
                    {
                        box.Line(EstateText.T("reallife_estate_ten_arrears", "amount", EstateText.Money(u.Arrears), "days", u.ArrearsDays.ToString(),
                            "back", EstateText.Share(EstateRules.CatchUpChance(u.Record))), CellTone.Red);
                    }
                    Transform own = box.ButtonRow();
                    if (EstateRules.MayServeNotice(h, 0, SaveGameManager.Current.Day))
                    {
                        InfoBox.Button(own, InfoBox.Negative, "reallife_estate_btn_notice", () => _director.ServeFlatNotice(key));
                    }
                    if (EstateRules.MayGoToCourt(h, 0, SaveGameManager.Current.Day))
                    {
                        InfoBox.Button(own, InfoBox.Negative, "reallife_estate_btn_court", () => _director.FlatToCourt(key));
                    }
                }
                else
                {
                    box.Line(EstateText.T("reallife_flat_own_empty", "days", u.VoidDays.ToString(), "count", u.Applicants.Count.ToString()), CellTone.Yellow);
                    int ask = EstateCatalogue.Clamp(u.Ask, EstateCatalogue.AskLeast, EstateCatalogue.AskMost);
                    box.Line(EstateText.T("reallife_estate_ten_asking_what", "amount", EstateText.Money(EstateCatalogue.Share(market, ask)), "chance",
                        EstateText.Share(EstateRules.ApplicantChance(ask, h.Cond, c.Cycle))));
                    Steps(box, EstateCatalogue.AskLeast, EstateCatalogue.AskMost, EstateCatalogue.AskStep, ask, value => _director.SetFlatAsk(key, value));
                    if (u.Applicants.Count > 0)
                    {
                        RentApplicant a = u.Applicants[0];
                        int id = a.Id;
                        box.Line(EstateText.Fill("reallife_flat_applicant", "who", EstateText.Tenant(h.Use, a.Kind), "amount", EstateText.Money(a.Offer), "record", EstateText.Record(a.Record),
                            "day", a.Lapse.ToString()), CellTone.Green);
                        Transform buttons = box.ButtonRow();
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_accept", () => _director.AcceptFlatApplicant(key, id));
                        InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_refuse", () => _director.RefuseFlatApplicant(key, id));
                    }
                    InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_flat_btn_stop_let", () => _director.LetFlat(key, false));
                }
            }
            if (h.Managed && _director.On(EstatePart.Duties) && box.HasSelector)
            {
                int choices = _director.AgencyOpen || h.Manager == (int)EstateManager.Agency ? EstateCatalogue.Managers : EstateCatalogue.Managers - 1;   // your own agency only while it is open, as for a building
                if (!box.Selector(choices, h.Manager, EstateText.Manager, i => _director.SetManager(key, i)))
                {
                    box.Line(EstateText.Manager(h.Manager));
                }
            }
            if (h.Cond < EstateCatalogue.ConditionNew && !h.Busy && h.SaleDay == 0)
            {
                int repair = EstateRules.Quote(h, EstateWork.Repair, -1, c);
                box.Line(EstateText.T("reallife_flat_repair_what", "amount", EstateText.Money(repair), "condition", EstateText.Level(h.Cond)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_estate_btn_repair", () => _director.StartWork(key, EstateWork.Repair, -1));
            }
            else if (h.Busy)
            {
                box.Line(EstateText.T("reallife_estate_state_works", "work", EstateText.Work(h.Work), "day", h.WorkEnd.ToString()));
            }
            box.Headline("reallife_estate_own_selling");
            int value = f.Value;
            if (h.SaleDay > 0)
            {
                box.Line(EstateText.T("reallife_estate_own_sale_agreed", "amount", EstateText.Money(h.SalePrice), "day", h.SaleDay.ToString(),
                    "fee", EstateText.Money(EstateRules.AgentFee(h.SalePrice, c.Index))), CellTone.Green);
            }
            else if (h.Offer > 0)
            {
                int fee = EstateRules.AgentFee(h.Offer, c.Index), tax = EstateRules.GainsTax(h.Offer, fee, f.Paid, h.Spent, c.TaxPercent);
                box.Line(EstateText.T("reallife_estate_own_offer", "amount", EstateText.Money(h.Offer), "day", h.OfferLapse.ToString(), "asked", EstateText.Money(h.List)), CellTone.Green);
                box.Line(EstateText.Fill("reallife_flat_offer_net", "fee", EstateText.Money(fee), "tax", EstateText.Money(tax), "owed", EstateText.Money(owed),
                    "amount", EstateText.Money(Math.Max(0, h.Offer - fee - tax - owed))));
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_accept", () => AskFlat("reallife_flat_q_sell_title",
                    EstateText.T("reallife_flat_q_sell", "flat", name, "amount", EstateText.Money(h.Offer)), () => _director.AcceptFlatOffer(key), true));
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_refuse", () => _director.RefuseFlatOffer(key));
            }
            else if (h.List > 0)
            {
                box.Line(EstateText.T("reallife_estate_own_listed", "amount", EstateText.Money(h.List), "chance", EstateText.Share(EstateRules.SaleChance(h.List, value, c.Cycle))));
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_estate_btn_unlist", () => _director.ListFlat(key, 0));
            }
            else
            {
                int share = ListShares[EstateCatalogue.Clamp(_listShare, 0, ListShares.Length - 1)];
                int asked = EstateCatalogue.Share(value, share);
                if (!box.HasSelector || !box.Selector(ListShares.Length, _listShare, i => EstateText.Share(ListShares[i]), i =>
                    {
                        _listShare = i;
                        Refresh();
                    }))
                {
                    box.Line(EstateText.Share(share));
                }
                box.Note(EstateText.T("reallife_flat_list_what", "amount", EstateText.Money(asked), "chance", EstateText.Share(EstateRules.SaleChance(asked, value, c.Cycle)),
                    "fee", EstateText.Share(EstateCatalogue.AgentFee)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_estate_btn_list", () => _director.ListFlat(key, share));
            }
            BackButton(box.ButtonRow());
            Meta.Guide.Point(box, FlatGuideTitle);
        }
    }
}
