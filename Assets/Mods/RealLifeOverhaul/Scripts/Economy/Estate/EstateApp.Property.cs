using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Mine, Tenants, Works and Agency tabs. Each writes its rows and, for what is picked, a page in the white
    /// box that says in sentences what each figure is made of. Written when the tab is shown; nothing is kept. A
    /// building the player lets himself is let unit by unit: the Mine tab says how its units stand and opens one's
    /// lease, and the Tenants tab has a row for each unit and each applicant.
    /// </summary>
    public sealed partial class EstateApp
    {
        private static readonly int[] ListShares = { 900, 950, 1000, 1050, 1100, 1150, 1200, 1300 };

        private readonly List<BuildingRegistration> _places = new List<BuildingRegistration>(16);
        private int _listShare = 2;     // the asking price showing, a place in ListShares
        private int _toUse = -1;        // the use showing for a change of use
        private int _office;            // the office showing for the agency
        private int _unit;              // the unit showing on a building's page

        private static int YieldOf(long rentADay, int daysPerYear, int value)
        {
            return (int)(rentADay * daysPerYear * 10000 / Math.Max(1, value));
        }

        // What a building is doing, in a few words: the second cell of its row on the Mine tab.
        private static string StateOf(EstateHolding h, int day)
        {
            if (h.SaleDay > 0)
            {
                return EstateText.T("reallife_estate_state_sold", "day", h.SaleDay.ToString());
            }
            if (h.Busy)
            {
                return EstateText.T("reallife_estate_state_works", "work", EstateText.Work(h.Work), "day", h.WorkEnd.ToString());
            }
            if (h.Self)
            {
                return EstateText.T("reallife_estate_state_self");
            }
            if (!h.Managed)
            {
                return EstateText.T("reallife_estate_state_game");
            }
            if (h.Units.Count > 1)
            {
                int behind, let = h.LetUnits(out behind);
                return behind > 0 ? EstateText.T("reallife_estate_state_units_behind", "behind", behind.ToString(), "units", h.Units.Count.ToString())
                    : EstateText.T("reallife_estate_state_units", "let", let.ToString(), "units", h.Units.Count.ToString());
            }
            EstateUnit u = h.Units[0];
            if (!u.Let)
            {
                return EstateText.T("reallife_estate_state_empty", "days", u.VoidDays.ToString());
            }
            return u.Arrears > 0 ? EstateText.T("reallife_estate_state_arrears", "days", u.ArrearsDays.ToString()) : EstateText.T("reallife_estate_state_let", "who", EstateText.Tenant(h.Use, u.Tenant));
        }

        // A unit's lease on the Tenants tab.
        private void OpenLease(EstateHolding h, int unit)
        {
            _tab = EstateDirector.TabTenants;
            _pick = EstateDirector.UnitPick(h, unit);
            Refresh();
        }

        // ---------------------------------------------------------------- Mine

        private void WriteMine(InfoBox box, EstateState state, int day, ref string selected)
        {
            EstateClimate c = _director.Climate;
            EstateHolding picked = null;
            long worth = 0, rent = 0;
            TableRow row;
            bool property = _director.On(EstatePart.Property);
            for (int i = 0; property && i < state.Holdings.Count; i++)
            {
                EstateHolding h = state.Holdings[i];
                int value = EstateRules.Value(h, state.Real, c.Index), behind;
                int income = h.Managed ? EstateRules.PayingRent(h) : EstateGame.GameRent(h);
                bool empty = h.LetUnits(out behind) < h.Units.Count && h.Managed;
                worth += value;
                rent += income;
                if (Pick("own:" + h.Place, ref selected, out row))
                {
                    picked = h;
                }
                row.Kind = EstateTables.OwnBuilding;
                row.Set(0, h.Name);
                row.Set(1, EstateText.T("reallife_estate_kind_building"));
                row.Set(2, StateOf(h, day), behind > 0 ? TableRow.Red : empty && !h.Busy ? TableRow.Yellow : TableRow.Plain);
                row.Set(3, EstateText.Money(value), value);
                row.Set(4, EstateText.Money(h.Paid), h.Paid);
                row.Set(5, EstateText.Money(income), income);
                row.Set(6, EstateText.Money(h.RentYear), h.RentYear);
                row.Set(7, EstateText.Money(h.CostYear), h.CostYear);
            }
            for (int i = 0; _director.On(EstatePart.Hotel) && i < state.Hotels.Count; i++)
            {
                HotelFile f = state.Hotels[i];
                Pick("hot:" + f.Slot, ref selected, out row);
                row.Kind = EstateTables.OwnHotel;
                row.Set(0, f.Name);
                row.Set(1, EstateText.T("reallife_estate_kind_hotel"));
                row.Set(2, EstateText.T(f.Leased ? "reallife_estate_row_hotel_leased" : "reallife_estate_row_hotel_owned"));
                row.Set(4, EstateText.Money(f.Paid), f.Paid);
                row.Set(5, EstateText.Money(f.Takings - f.Costs), f.Takings - f.Costs, f.Takings < f.Costs ? TableRow.Red : TableRow.Plain);
                row.Set(6, EstateText.Money(f.TakingsYear), f.TakingsYear);
                row.Set(7, EstateText.Money(f.CostsYear), f.CostsYear);
            }
            if (_director.On(EstatePart.Hire) && state.Hire.Running)
            {
                Pick("lot:", ref selected, out row);
                row.Kind = EstateTables.OwnLot;
                row.Set(0, EstateGame.NameOf(state.Hire.Lot));
                row.Set(1, EstateText.T("reallife_estate_kind_lot"));
                row.Set(2, EstateText.T("reallife_estate_row_lot", "out", state.Hire.Count(HireState.Out).ToString(), "count", state.Hire.Cars.Count.ToString()));
                row.Set(6, EstateText.Money(state.Hire.TakingsYear), state.Hire.TakingsYear);
                row.Set(7, EstateText.Money(state.Hire.CostsYear), state.Hire.CostsYear);
            }
            if (_rows.Count == 0)
            {
                Say("none", EstateText.T("reallife_estate_row_nothing_owned"));
            }
            if (picked != null)
            {
                WriteHolding(box, state, picked, day, c);
                return;
            }
            box.Title(EstateText.T("reallife_estate_tab_mine"));
            box.Line(EstateText.Pair("reallife_estate_mine_count", state.Holdings.Count.ToString()));
            box.Line(EstateText.Pair("reallife_estate_mine_worth", EstateText.Money(worth)));
            box.Line(EstateText.Pair("reallife_estate_mine_rent", EstateText.T("reallife_estate_row_a_day", "amount", EstateText.Money(rent))));
            box.Line(EstateText.T("reallife_estate_mine_what"));
            box.Line(EstateText.T("reallife_estate_mine_game"));
        }

        // One building: what it is worth and why, what it brings, its cover, and selling it.
        private void WriteHolding(InfoBox box, EstateState state, EstateHolding h, int day, EstateClimate c)
        {
            string place = h.Place;
            int value = EstateRules.Value(h, state.Real, c.Index);
            box.Title(h.Name);
            box.Line(EstateText.T("reallife_estate_own_worth", "amount", EstateText.Money(value), "paid", EstateText.Money(h.Paid), "day", h.BoughtDay.ToString()));
            box.Line(EstateText.T("reallife_estate_own_made_of", "base", EstateText.Money(h.Base), "market", EstateText.Share(state.Real), "prices", EstateText.Share(c.Index))
                     + " " + EstateText.T("reallife_estate_own_made_of_2", "district", EstateText.Share(EstateRules.DistrictValue(h.Income)), "condition", EstateText.Level(h.Cond), "share", EstateText.Share(EstateRules.ConditionValue(h.Cond))));
            box.Line(EstateText.T("reallife_estate_own_year", "rent", EstateText.Money(h.RentYear), "costs", EstateText.Money(h.CostYear), "spent", EstateText.Money(h.Spent)));
            if (h.Self)
            {
                box.Line(EstateText.T("reallife_estate_own_self"));
            }
            else if (!h.Managed)
            {
                box.Line(EstateText.T("reallife_estate_own_game", "amount", EstateText.Money(EstateGame.GameRent(h) + EstateRules.GameRentTopUp(EstateGame.GameRent(h), c.RentScale)), "market", EstateText.Money(EstateRules.MarketRent(h, c))));
            }
            else if (h.Units.Count == 1)
            {
                EstateUnit u = h.Units[0];
                box.Line(u.Let
                    ? EstateText.T("reallife_estate_own_let", "who", EstateText.Tenant(h.Use, u.Tenant), "amount", EstateText.Money(u.Rent), "yield", EstateText.Yield(YieldOf(u.Rent, c.DaysPerYear, value)))
                    : EstateText.T("reallife_estate_own_empty", "days", u.VoidDays.ToString(), "count", u.Applicants.Count.ToString()));
            }
            else
            {
                WriteUnits(box, h, c, value);
            }
            if (!box.HasSwitch || !box.Switch("reallife_estate_own_cover", h.Cover, on => _director.SetCover(place, on)))
            {
                box.Line(EstateText.T(h.Cover ? "reallife_estate_own_cover_on" : "reallife_estate_own_cover_off"));
            }
            box.Line(EstateText.T("reallife_estate_own_cover_what", "amount", EstateText.Money(EstateRules.Premium(h.Base, c)), "excess", EstateText.Share(EstateCatalogue.CoverExcess)));

            // selling
            box.Headline("reallife_estate_own_selling");
            Transform buttons = box.ButtonRow();
            if (h.SaleDay > 0)
            {
                box.Line(EstateText.T("reallife_estate_own_sale_agreed", "amount", EstateText.Money(h.SalePrice), "day", h.SaleDay.ToString(), "fee", EstateText.Money(EstateRules.AgentFee(h.SalePrice, c.Index))));
            }
            else if (h.Offer > 0)
            {
                int fee = EstateRules.AgentFee(h.Offer, c.Index), tax = EstateRules.GainsTax(h.Offer, fee, h.Paid, h.Spent, c.TaxPercent);
                box.Line(EstateText.T("reallife_estate_own_offer", "amount", EstateText.Money(h.Offer), "day", h.OfferLapse.ToString(), "asked", EstateText.Money(h.List)));
                box.Line(EstateText.T("reallife_estate_own_offer_net", "fee", EstateText.Money(fee), "tax", EstateText.Money(tax), "amount", EstateText.Money(h.Offer - fee - tax)));
                box.Line(EstateText.T("reallife_estate_own_offer_days", "days", EstateCatalogue.CompletionDays.ToString()));
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_accept", () => _director.AcceptOffer(place));
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_refuse", () => _director.RefuseOffer(place));
            }
            else if (h.List > 0)
            {
                box.Line(EstateText.T("reallife_estate_own_listed", "amount", EstateText.Money(h.List), "chance", EstateText.Share(EstateRules.SaleChance(h.List, value, c.Cycle))));
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_unlist", () => _director.List(place, 0));
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
                box.Line(EstateText.T("reallife_estate_own_list_what", "amount", EstateText.Money(asked), "chance", EstateText.Share(EstateRules.SaleChance(asked, value, c.Cycle)),
                    "fee", EstateText.Share(EstateCatalogue.AgentFee)));
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_list", () => _director.List(place, share));
            }
            Transform more = box.ButtonRow();
            if (!h.Self && !h.Managed)
            {
                InfoBox.Button(more, InfoBox.Positive, "reallife_estate_btn_take_on", () => _director.TakeOn(place));
            }
            else if (h.Managed)
            {
                int unit = EstateCatalogue.Clamp(_unit, 0, h.Units.Count - 1);
                InfoBox.Button(more, InfoBox.Positive, "reallife_estate_btn_lease", () => OpenLease(h, unit));
                if (!h.AnyLet && !h.Busy)
                {
                    InfoBox.Button(more, InfoBox.Negative, "reallife_estate_btn_hand_back", () => _director.HandBack(place));
                }
            }
            BackButton(more);
        }

        // How a building's units stand, and one of them, picked with the selector: its lease opens from the button under it.
        private void WriteUnits(InfoBox box, EstateHolding h, EstateClimate c, int value)
        {
            int behind, let = h.LetUnits(out behind);
            long agreed = 0;
            for (int i = 0; i < h.Units.Count; i++)
            {
                agreed += h.Units[i].Let ? h.Units[i].Rent : 0;
            }
            box.Line(EstateText.T("reallife_estate_own_units", "units", h.Units.Count.ToString(), "let", let.ToString(), "behind", behind.ToString()));
            if (let > 0)
            {
                box.Line(EstateText.T("reallife_estate_own_units_rent", "amount", EstateText.Money(agreed), "yield", EstateText.Yield(YieldOf(agreed, c.DaysPerYear, value))));
            }
            _unit = EstateCatalogue.Clamp(_unit, 0, h.Units.Count - 1);
            if (!box.HasSelector || !box.Selector(h.Units.Count, _unit, i => EstateText.T("reallife_estate_unit_short", "unit", (i + 1).ToString()), i =>
                {
                    _unit = i;
                    Refresh();
                }))
            {
                box.Line(EstateText.T("reallife_estate_unit_short", "unit", (_unit + 1).ToString()));
            }
            EstateUnit u = h.Units[_unit];
            string unit = (_unit + 1).ToString();
            box.Line(!u.Let ? EstateText.T("reallife_estate_own_unit_empty", "unit", unit, "days", u.VoidDays.ToString(), "count", u.Applicants.Count.ToString())
                : u.Arrears > 0 ? EstateText.T("reallife_estate_own_unit_behind", "unit", unit, "who", EstateText.Tenant(h.Use, u.Tenant), "amount", EstateText.Money(u.Arrears))
                : EstateText.Fill("reallife_estate_own_unit_let", "unit", unit, "who", EstateText.Tenant(h.Use, u.Tenant), "amount", EstateText.Money(u.Rent), "day", u.Until.ToString()));
        }

        // ---------------------------------------------------------------- Tenants

        private void WriteTenants(InfoBox box, EstateState state, int day, ref string selected)
        {
            EstateClimate c = _director.Climate;
            EstateHolding picked = null, applied = null;
            RentApplicant applicant = null;
            TableRow row;
            int let = 0, empty = 0, owed = 0, pickedUnit = 0, appliedUnit = 0;
            for (int i = 0; i < state.Holdings.Count; i++)
            {
                EstateHolding h = state.Holdings[i];
                if (!h.Managed)
                {
                    continue;
                }
                if (_pick == "own:" + h.Place)
                {
                    _pick = EstateDirector.UnitPick(h, 0);   // (a building picked elsewhere opens its first unit's lease)
                }
                for (int n = 0; n < h.Units.Count; n++)
                {
                    EstateUnit u = h.Units[n];
                    string name = EstateText.UnitName(h, n);
                    let += u.Let ? 1 : 0;
                    empty += u.Let ? 0 : 1;
                    owed += u.Arrears;
                    string who = u.Let ? EstateText.Tenant(h.Use, u.Tenant) : EstateText.T("reallife_estate_row_nobody");
                    string standing = !u.Let ? EstateText.T("reallife_estate_state_empty", "days", u.VoidDays.ToString())
                        : u.CourtDay > 0 ? EstateText.T("reallife_estate_state_court", "day", u.CourtDay.ToString())
                        : u.NoticeEnd > 0 ? EstateText.T("reallife_estate_state_notice", "day", u.NoticeEnd.ToString())
                        : u.Arrears > 0 ? EstateText.T("reallife_estate_state_arrears", "days", u.ArrearsDays.ToString()) : EstateText.T("reallife_estate_state_paying");
                    if (Pick(EstateDirector.UnitPick(h, n), ref selected, out row))
                    {
                        picked = h;
                        pickedUnit = n;
                    }
                    row.Kind = !u.Let ? EstateTables.TenantNone : u.CourtDay > 0 || u.NoticeEnd > 0 ? EstateTables.TenantNotice : u.Arrears > 0 ? EstateTables.TenantBehind : EstateTables.TenantPaying;
                    row.Set(0, name);
                    row.Set(1, who);
                    row.Set(6, standing, u.Arrears > 0 ? TableRow.Red : !u.Let ? TableRow.Yellow : TableRow.Plain);
                    if (u.Let)
                    {
                        row.Set(2, EstateText.Money(u.Rent), u.Rent);
                        row.Set(3, EstateText.Money(u.Arrears), u.Arrears);
                        row.Set(4, EstateText.Record(u.Record), u.Record);
                        row.Set(5, EstateText.T("reallife_estate_row_until", "day", u.Until.ToString()), u.Until);
                    }
                    for (int a = 0; a < u.Applicants.Count; a++)
                    {
                        RentApplicant p = u.Applicants[a];
                        if (Pick("app:" + h.Place + ":" + p.Id, ref selected, out row))
                        {
                            applied = h;
                            applicant = p;
                            appliedUnit = n;
                        }
                        row.Kind = EstateTables.TenantApplies;
                        row.NoTotal = true;   // (an offer is not rent yet)
                        row.Set(0, name);
                        row.Set(1, EstateText.Tenant(h.Use, p.Kind));
                        row.Set(2, EstateText.Money(p.Offer), p.Offer);
                        row.Set(4, EstateText.Record(p.Record), p.Record);
                        row.Set(5, EstateText.T("reallife_estate_row_until", "day", p.Lapse.ToString()), p.Lapse);
                        row.Set(6, EstateText.T("reallife_estate_filter_applicants"), TableRow.Green);
                    }
                }
            }
            if (_rows.Count == 0)
            {
                Say("none", EstateText.T(state.Holdings.Count == 0 ? "reallife_estate_row_nothing_owned" : "reallife_estate_row_none_managed"));
            }
            if (applicant != null)
            {
                WriteApplicant(box, applied, appliedUnit, applicant, c);
            }
            else if (picked != null)
            {
                WriteTenancy(box, picked, pickedUnit, day, c);
            }
            else
            {
                box.Title(EstateText.T("reallife_estate_tab_tenants"));
                box.Line(EstateText.Pair("reallife_estate_tenants_let", let.ToString()));
                box.Line(EstateText.Pair("reallife_estate_tenants_empty", empty.ToString()));
                box.Line(EstateText.Pair("reallife_estate_tenants_owed", EstateText.Money(owed)));
                box.Line(EstateText.T("reallife_estate_tenants_what"));
                box.Line(EstateText.T("reallife_estate_tenants_rent_what"));
                box.Line(EstateText.T("reallife_estate_tenants_notice_what", "days", EstateCatalogue.NoticeArrearsDays.ToString(), "notice", EstateRules.NoticeLength(c.RentLever).ToString(), "court", EstateCatalogue.CourtDays.ToString()));
            }
        }

        private void WriteApplicant(InfoBox box, EstateHolding h, int unit, RentApplicant a, EstateClimate c)
        {
            string place = h.Place;
            int id = a.Id, market = EstateRules.UnitRent(h, c);
            box.Title(EstateText.Tenant(h.Use, a.Kind));
            box.Line(EstateText.T("reallife_estate_app_for", "building", EstateText.UnitName(h, unit), "day", a.Lapse.ToString()));
            box.Line(EstateText.T("reallife_estate_app_offer", "amount", EstateText.Money(a.Offer), "market", EstateText.Money(market), "share", EstateText.Share(market > 0 ? (int)((long)a.Offer * 1000 / market) : 0)));
            box.Line(EstateText.T("reallife_estate_app_term", "days", a.Term.ToString(), "deposit", EstateText.Money(a.Offer * EstateCatalogue.DepositDays)));
            box.Line(EstateText.T("reallife_estate_app_record", "record", EstateText.Record(a.Record), "miss", EstateText.Share(EstateRules.MissChance(a.Record, c.Cycle)), "back", EstateText.Share(EstateRules.CatchUpChance(a.Record))));
            box.Line(EstateText.T("reallife_estate_app_note"));
            Transform buttons = box.ButtonRow();
            if (!h.Units[unit].Let && !h.Busy)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_accept", () =>
                {
                    int taken = _director.Accept(place, id);
                    if (taken >= 0)
                    {
                        _pick = EstateDirector.UnitPick(h, taken);   // (the new lease is what the page shows next)
                        Refresh();
                    }
                });
            }
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_refuse", () => _director.Refuse(place, id));
            BackButton(buttons);
        }

        // One unit's lease, or the unit empty: its asking rent and whether applicants are looked for.
        private void WriteTenancy(InfoBox box, EstateHolding h, int unit, int day, EstateClimate c)
        {
            string place = h.Place;
            EstateUnit u = h.Units[unit];
            int market = EstateRules.UnitRent(h, c);
            box.Title(EstateText.UnitName(h, unit));
            box.Line(market > 0
                ? EstateText.Fill("reallife_estate_ten_market", "amount", EstateText.Money(market), "use", EstateText.Use(h.Use), "yield", EstateText.Share(EstateRules.Yield(h.Use, h.Income)), "condition", EstateText.Level(h.Cond))
                : EstateText.T("reallife_estate_ten_unfit", "condition", EstateText.Level(h.Cond)));
            if (h.Units.Count > 1)
            {
                box.Line(EstateText.T("reallife_estate_ten_share", "units", h.Units.Count.ToString(), "amount", EstateText.Money(EstateRules.MarketRent(h, c))));
            }
            Transform buttons = box.ButtonRow();
            if (u.Let)
            {
                box.Line(EstateText.T("reallife_estate_ten_who", "who", EstateText.Tenant(h.Use, u.Tenant), "amount", EstateText.Money(u.Rent), "day", u.Until.ToString()));
                box.Line(EstateText.T("reallife_estate_ten_record", "record", EstateText.Record(u.Record), "deposit", EstateText.Money(u.Deposit)));
                if (u.Arrears > 0)
                {
                    box.Line(EstateText.T("reallife_estate_ten_arrears", "amount", EstateText.Money(u.Arrears), "days", u.ArrearsDays.ToString(), "back", EstateText.Share(EstateRules.CatchUpChance(u.Record))));
                }
                if (u.CourtDay > 0)
                {
                    box.Line(EstateText.T("reallife_estate_ten_court", "day", u.CourtDay.ToString()));
                }
                else if (u.NoticeEnd > 0)
                {
                    box.Line(EstateText.T(day > u.NoticeEnd ? "reallife_estate_ten_notice_over" : "reallife_estate_ten_notice", "day", u.NoticeEnd.ToString(), "fee", EstateText.Money(EstateRules.CourtFee(c.Index)),
                        "days", EstateCatalogue.CourtDays.ToString()));
                }
                if (EstateRules.MayServeNotice(h, unit, day))
                {
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_notice", () => _director.ServeNotice(place, unit));
                }
                else if (EstateRules.MayGoToCourt(h, unit, day))
                {
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_court", () => _director.GoToCourt(place, unit));
                }
            }
            else
            {
                box.Line(EstateText.T("reallife_estate_ten_empty", "days", u.VoidDays.ToString()));
                box.Headline("reallife_estate_ten_asking");
                Steps(box, EstateCatalogue.AskLeast, EstateCatalogue.AskMost, EstateCatalogue.AskStep, u.Ask, ask => _director.SetAsk(place, unit, ask));
                box.Line(EstateText.T("reallife_estate_ten_asking_what", "amount", EstateText.Money(EstateCatalogue.Share(market, u.Ask)), "chance", EstateText.Share(EstateRules.ApplicantChance(u.Ask, h.Cond, c.Cycle))));
                if (!box.HasSwitch || !box.Switch("reallife_estate_ten_to_let", u.ToLet, on => _director.SetToLet(place, unit, on)))
                {
                    box.Line(EstateText.T(u.ToLet ? "reallife_estate_ten_to_let_on" : "reallife_estate_ten_to_let_off"));
                }
            }
            BackButton(buttons);
        }

        // ---------------------------------------------------------------- Works

        private void WriteWorks(InfoBox box, EstateState state, int day, ref string selected)
        {
            EstateClimate c = _director.Climate;
            EstateHolding picked = null;
            for (int i = 0; i < state.Holdings.Count; i++)
            {
                EstateHolding h = state.Holdings[i];
                TableRow row;
                int repair = EstateRules.RepairCost(h.Base, h.Cond, c.Index);
                if (Pick("own:" + h.Place, ref selected, out row))
                {
                    picked = h;
                }
                row.Kind = h.Busy ? EstateTables.WorksOn : h.Cond < EstateCatalogue.ConditionPoor ? EstateTables.WorksNeeded : EstateTables.WorksNone;
                row.Set(0, h.Name);
                row.Set(1, EstateText.Level(h.Cond), h.Cond, h.Cond < EstateCatalogue.ConditionUnfit ? TableRow.Red : h.Cond < EstateCatalogue.ConditionPoor ? TableRow.Yellow : TableRow.Plain);
                row.Set(2, h.Level.ToString(), h.Level);
                row.Set(3, EstateText.Use(h.Use));
                row.Set(4, EstateText.Money(repair), repair);
                if (h.Busy)
                {
                    row.Set(5, EstateText.Work(h.Work));
                    row.Set(6, EstateText.T("reallife_estate_row_until", "day", h.WorkEnd.ToString()), h.WorkEnd);
                }
            }
            if (_rows.Count == 0)
            {
                Say("none", EstateText.T("reallife_estate_row_nothing_owned"));
            }
            if (picked == null)
            {
                box.Title(EstateText.T("reallife_estate_tab_works"));
                box.Line(EstateText.T("reallife_estate_works_what"));
                box.Line(EstateText.T("reallife_estate_works_wear", "poor", EstateText.Level(EstateCatalogue.ConditionPoor), "unfit", EstateText.Level(EstateCatalogue.ConditionUnfit)));
                box.Line(EstateText.T("reallife_estate_works_market", "market", EstateText.Share(state.Real)));
                box.Line(EstateText.T("reallife_estate_works_no_new"));
                return;
            }
            WriteWorksOf(box, state, picked, c);
        }

        private void WriteWorksOf(InfoBox box, EstateState state, EstateHolding h, EstateClimate c)
        {
            string place = h.Place;
            box.Title(h.Name);
            box.Line(EstateText.T("reallife_estate_works_cond", "condition", EstateText.Level(h.Cond), "value", EstateText.Share(EstateRules.ConditionValue(h.Cond)), "rent", EstateText.Share(EstateRules.ConditionRent(h.Cond))));
            Transform buttons = box.ButtonRow();
            if (h.Busy)
            {
                box.Line(EstateText.T("reallife_estate_works_busy", "work", EstateText.Work(h.Work), "day", h.WorkEnd.ToString(), "amount", EstateText.Money(h.WorkCost)));
                BackButton(buttons);
                return;
            }
            int repair = EstateRules.Quote(h, EstateWork.Repair, -1, c);
            if (repair > 0)
            {
                box.Line(EstateText.T("reallife_estate_works_repair", "amount", EstateText.Money(repair), "days", EstateRules.RepairDays(h.Cond, c.PermitLever).ToString()));
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_repair", () => _director.StartWork(place, EstateWork.Repair, -1));
            }
            int refurbish = EstateRules.Quote(h, EstateWork.Refurbish, -1, c);
            if (refurbish > 0)
            {
                int gain = EstateRules.LevelGain(h, h.Level + 1, state.Real, c.Index);
                box.Line(EstateText.T("reallife_estate_works_refurbish", "level", (h.Level + 1).ToString(), "amount", EstateText.Money(refurbish), "days", EstateRules.SiteDays(EstateCatalogue.LevelDays[h.Level + 1], c.PermitLever).ToString()));
                box.Line(EstateText.T(gain >= refurbish ? "reallife_estate_works_gain_more" : "reallife_estate_works_gain_less", "gain", EstateText.Money(gain), "rent", EstateText.Share(EstateCatalogue.LevelRent[h.Level + 1] - EstateCatalogue.LevelRent[h.Level]),
                    "market", EstateText.Share(state.Real)));
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_estate_btn_refurbish", () => _director.StartWork(place, EstateWork.Refurbish, -1));
            }
            else if (h.Level >= EstateCatalogue.LevelMost)
            {
                box.Line(EstateText.T("reallife_estate_works_level_most"));
            }
            else
            {
                box.Line(EstateText.T("reallife_estate_works_need_empty"));
            }

            // a change of use: the council first, then the builders
            if (h.Managed && !h.AnyLet && !h.Self)
            {
                box.Headline("reallife_estate_works_use");
                Transform useButtons = box.ButtonRow();
                if (h.PlanUse >= 0)
                {
                    int convert = EstateRules.Quote(h, EstateWork.Convert, -1, c);
                    box.Line(EstateText.Fill("reallife_estate_works_convert", "use", EstateText.Use(h.PlanUse), "amount", EstateText.Money(convert), "days", EstateRules.SiteDays(EstateCatalogue.ConvertDays, c.PermitLever).ToString(),
                        "yield", EstateText.Share(EstateRules.Yield(h.PlanUse, h.Income))));
                    InfoBox.Button(useButtons, InfoBox.Positive, "reallife_estate_btn_convert", () => _director.StartWork(place, EstateWork.Convert, -1));
                }
                else
                {
                    int to = _toUse >= 0 && _toUse < EstateCatalogue.Uses && _toUse != h.Use ? _toUse : (h.Use + 1) % EstateCatalogue.Uses;
                    if (!box.HasSelector || !box.Selector(EstateCatalogue.Uses, to, EstateText.Use, i =>
                        {
                            _toUse = i;
                            Refresh();
                        }))
                    {
                        box.Line(EstateText.Use(to));
                    }
                    if (to != h.Use)
                    {
                        box.Line(EstateText.T("reallife_estate_works_planning", "fee", EstateText.Money(EstateRules.PlanningFee(h.Base, c.Index)), "days", EstateCatalogue.PlanningDays.ToString(),
                            "chance", EstateText.Share(EstateCatalogue.PlanningChance[EstateCatalogue.Lever(c.PermitLever)])));
                        box.Line(EstateText.T("reallife_estate_works_yields", "now", EstateText.Share(EstateRules.Yield(h.Use, h.Income)), "then", EstateText.Share(EstateRules.Yield(to, h.Income)), "use", EstateText.Use(to)));
                        InfoBox.Button(useButtons, InfoBox.Positive, "reallife_estate_btn_planning", () => _director.StartWork(place, EstateWork.Planning, to));
                    }
                    else
                    {
                        box.Line(EstateText.T("reallife_estate_works_same_use"));
                    }
                }
                box.Line(EstateText.T("reallife_estate_works_use_note"));
            }
            BackButton(box.ButtonRow());
        }

        // ---------------------------------------------------------------- Agency

        private void WriteAgency(InfoBox box, EstateState state, int day, ref string selected)
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
            box.Line(EstateText.T("reallife_estate_agency_what"));
            EstateGame.Offices(_places);
            if (agency.Running)
            {
                box.Line(EstateText.Pair("reallife_estate_agency_office", EstateGame.NameOf(agency.Office)));
                box.Line(EstateText.T(agency.Negotiators > 0 ? "reallife_estate_agency_staff" : "reallife_estate_agency_no_staff", "count", agency.Negotiators.ToString(), "hours", (agency.Capacity / 10f).ToString("0.#")));
                box.Line(EstateText.T("reallife_estate_agency_totals", "deals", agency.Deals.ToString(), "lost", agency.Lost.ToString(), "amount", EstateText.Money(agency.Commission)));
                box.Headline("reallife_estate_agency_reputation");
                for (int i = 0; i < agency.Districts.Count; i++)
                {
                    DistrictRep d = agency.Districts[i];
                    if (d.SqmPrice > 0)
                    {
                        box.Line(EstateText.District(d.Name) + ": <b>" + EstateText.Level(d.Rep) + "</b>, " + EstateText.T("reallife_estate_agency_chance", "chance", EstateText.Share(AgencyRules.InstructionChance(d.Rep, c.Cycle))));
                    }
                }
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_estate_btn_close_agency", _director.CloseAgency);
                return;
            }
            if (_places.Count == 0)
            {
                box.Line(EstateText.T("reallife_estate_agency_no_office"));
                return;
            }
            _office = EstateCatalogue.Clamp(_office, 0, _places.Count - 1);
            if (!box.HasSelector || !box.Selector(_places.Count, _office, i => _places[i].Address.ToFormattedString(), i => _office = i))
            {
                box.Line(_places[_office].Address.ToFormattedString());
            }
            box.Line(EstateText.T("reallife_estate_agency_open_what"));
            InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_estate_btn_open_agency", () =>
            {
                EstateGame.Offices(_places);
                if (_office >= 0 && _office < _places.Count)
                {
                    _director.OpenAgency(EstateGame.Place(_places[_office].StreetName, _places[_office].StreetNumber));
                }
            });
        }

        private void WriteInstruction(InfoBox box, AgencyFile agency, Instruction n, EstateClimate c)
        {
            int id = n.Id;
            DistrictRep d = agency.District(n.District);
            int rep = d != null ? d.Rep : 0;
            box.Title(EstateText.T(n.Sale ? "reallife_estate_row_sale" : "reallife_estate_row_letting", "district", EstateText.District(n.District)));
            box.Line(EstateText.T("reallife_estate_ins_what", "sqm", n.Sqm.ToString(), "amount", EstateText.Money(n.Value), "ask", EstateText.Share(n.Ask)));
            box.Line(EstateText.T("reallife_estate_ins_hours", "hours", (n.Hours / 10).ToString(), "done", (n.Done / 10f).ToString("0.#"), "capacity", (agency.Capacity / 10f).ToString("0.#")));
            box.Line(EstateText.T("reallife_estate_ins_chance", "chance", EstateText.Share(AgencyRules.DealChance(n.Ask, rep, c.Cycle)), "fall", EstateText.Share(EstateCatalogue.FallThrough[EstateCatalogue.Clamp(c.Cycle, 0, 3)]),
                "reputation", EstateText.Level(rep)));
            int commission = AgencyRules.Commission(n.Sale, n.Stage == (int)InstructionStage.Completing ? n.Price : n.Value);
            box.Line(EstateText.T(n.Sale ? "reallife_estate_ins_pays_sale" : "reallife_estate_ins_pays_let", "amount", EstateText.Money(commission), "share", EstateText.Share(n.Sale ? EstateCatalogue.CommissionSale : EstateCatalogue.CommissionLet)));
            Transform buttons = box.ButtonRow();
            switch ((InstructionStage)n.Stage)
            {
                case InstructionStage.Offered:
                    box.Line(EstateText.T("reallife_estate_ins_offered", "day", n.Day.ToString(), "amount", EstateText.Money(AgencyRules.TakeCost(n)), "days", EstateCatalogue.InstructionLapseDays.ToString()));
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_take", () => _director.TakeInstruction(id));
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_decline", () => _director.DeclineInstruction(id));
                    break;
                case InstructionStage.Marketing:
                    box.Line(EstateText.T("reallife_estate_ins_marketing", "day", n.Day.ToString()));
                    break;
                default:
                    box.Line(EstateText.T("reallife_estate_ins_completing", "amount", EstateText.Money(n.Price), "day", n.Day.ToString()));
                    break;
            }
            BackButton(buttons);
        }
    }
}
