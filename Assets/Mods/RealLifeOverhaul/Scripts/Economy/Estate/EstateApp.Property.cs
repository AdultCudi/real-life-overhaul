using System;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The app's Mine, Tenants and Works tabs: their rows, and a page for what is picked.</summary>
    public sealed partial class EstateApp
    {
        private static readonly int[] ListShares = { 900, 950, 1000, 1050, 1100, 1150, 1200, 1300 };

        private int _listShare = 2;     // the asking price showing, a place in ListShares
        private int _toUse = -1;        // the use showing for a change of use
        private int _unit;              // the unit showing on a building's page

        private static int YieldOf(long rentADay, int daysPerYear, int value)
        {
            return (int)(rentADay * daysPerYear * 10000 / Math.Max(1, value));
        }

        // What a building is doing, in a few words, on the Mine tab.
        private static string StateOf(EstateHolding h)
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
            return u.Arrears > 0 ? EstateText.T("reallife_estate_state_arrears", "days", u.ArrearsDays.ToString())
                : EstateText.T("reallife_estate_state_let", "who", EstateText.Tenant(h.Use, u.Tenant));
        }

        private void OpenLease(EstateHolding h, int unit)
        {
            _tab = EstateDirector.TabTenants;
            _pick = EstateDirector.UnitPick(h, unit);
            Refresh();
        }

        private void WriteMine(InfoBox box, EstateState state, ref string selected)
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
                int income = h.Managed ? EstateRules.PayingRent(h) : EstateRules.GameNight(EstateGame.GameRent(h), c.RentScale);
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
                row.Set(2, StateOf(h), behind > 0 ? TableRow.Red : empty && !h.Busy ? TableRow.Yellow : TableRow.Plain);
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
                WriteHolding(box, state, picked, c);
                return;
            }
            box.Title(EstateText.T("reallife_estate_tab_mine"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_estate_mine_count", state.Holdings.Count.ToString());
            box.Row("reallife_estate_mine_worth", EstateText.Money(worth));
            box.Row("reallife_estate_mine_rent", EstateText.T("reallife_estate_row_a_day", "amount", EstateText.Money(rent)));
            box.Note(EstateText.T("reallife_estate_mine_what"));
            box.Note(EstateText.T("reallife_estate_mine_game"));
            Meta.Guide.Point(box, GuideTitle);
        }

        // One building: what it is worth and why, what it brings, its cover, and selling it.
        private void WriteHolding(InfoBox box, EstateState state, EstateHolding h, EstateClimate c)
        {
            string place = h.Place;
            int value = EstateRules.Value(h, state.Real, c.Index);
            box.Title(h.Name);
            box.Subtitle(EstateText.Use(h.Use));
            if (h.SaleDay > 0 || h.Offer > 0 || h.List > 0)
            {
                box.Chip(EstateText.T(h.SaleDay > 0 ? "reallife_ibox_chip_sold" : h.Offer > 0 ? "reallife_ibox_chip_offer" : "reallife_ibox_chip_for_sale"),
                    h.Offer > 0 ? CellTone.Green : CellTone.Plain);
            }
            box.Section("reallife_ibox_sec_money");
            box.Row("reallife_ibox_worth", EstateText.Money(value));
            box.Note(EstateText.T("reallife_estate_own_worth", "amount", EstateText.Money(value), "paid", EstateText.Money(h.Paid), "day", h.BoughtDay.ToString()));
            box.Line(EstateText.T("reallife_estate_own_year", "rent", EstateText.Money(h.RentYear), "costs", EstateText.Money(h.CostYear), "spent", EstateText.Money(h.Spent)));
            box.Section("reallife_ibox_sec_letting");
            if (h.Self)
            {
                box.Note(EstateText.T("reallife_estate_own_self"));
            }
            else if (!h.Managed)
            {
                box.Line(EstateText.T("reallife_estate_own_game", "amount", EstateText.Money(EstateRules.GameNight(EstateGame.GameRent(h), c.RentScale)),
                    "market", EstateText.Money(EstateRules.MarketRent(h, c))));
            }
            else if (h.Units.Count == 1)
            {
                EstateUnit u = h.Units[0];
                box.Line(u.Let
                    ? EstateText.T("reallife_estate_own_let", "who", EstateText.Tenant(h.Use, u.Tenant), "amount", EstateText.Money(u.Rent),
                        "yield", EstateText.Yield(YieldOf(u.Rent, c.DaysPerYear, value)))
                    : EstateText.T("reallife_estate_own_empty", "days", u.VoidDays.ToString(), "count", u.Applicants.Count.ToString()), u.Let ? CellTone.Plain : CellTone.Yellow);
            }
            else
            {
                WriteUnits(box, h, c, value);
            }
            if (!box.HasSwitch || !box.Switch("reallife_estate_own_cover", h.Cover, on => _director.SetCover(place, on)))
            {
                box.Line(EstateText.T(h.Cover ? "reallife_estate_own_cover_on" : "reallife_estate_own_cover_off"), h.Cover ? CellTone.Green : CellTone.Yellow);
            }

            box.Headline("reallife_estate_own_selling");
            if (h.SaleDay > 0)
            {
                box.Line(EstateText.T("reallife_estate_own_sale_agreed", "amount", EstateText.Money(h.SalePrice), "day", h.SaleDay.ToString(),
                    "fee", EstateText.Money(EstateRules.AgentFee(h.SalePrice, c.Index))), CellTone.Green);
            }
            else if (h.Offer > 0)
            {
                int fee = EstateRules.AgentFee(h.Offer, c.Index), tax = EstateRules.GainsTax(h.Offer, fee, h.Paid, h.Spent, c.TaxPercent);
                box.Line(EstateText.T("reallife_estate_own_offer", "amount", EstateText.Money(h.Offer), "day", h.OfferLapse.ToString(), "asked", EstateText.Money(h.List)), CellTone.Green);
                box.Line(EstateText.T("reallife_estate_own_offer_net", "fee", EstateText.Money(fee), "tax", EstateText.Money(tax), "amount", EstateText.Money(h.Offer - fee - tax)));
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_accept", () => _director.AcceptOffer(place));
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_refuse", () => _director.RefuseOffer(place));
                box.Note(EstateText.T("reallife_estate_own_offer_days", "days", EstateCatalogue.CompletionDays.ToString()));
            }
            else if (h.List > 0)
            {
                box.Line(EstateText.T("reallife_estate_own_listed", "amount", EstateText.Money(h.List), "chance", EstateText.Share(EstateRules.SaleChance(h.List, value, c.Cycle))));
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_estate_btn_unlist", () => _director.List(place, 0));
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
                box.Note(EstateText.T("reallife_estate_own_list_what", "amount", EstateText.Money(asked), "chance", EstateText.Share(EstateRules.SaleChance(asked, value, c.Cycle)),
                    "fee", EstateText.Share(EstateCatalogue.AgentFee)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_estate_btn_list", () => _director.List(place, share));
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
            box.Note(EstateText.T("reallife_estate_own_made_of", "base", EstateText.Money(h.Base), "market", EstateText.Share(state.Real), "prices", EstateText.Share(c.Index))
                     + " " + EstateText.T("reallife_estate_own_made_of_2", "district", EstateText.Share(EstateRules.DistrictValue(h.Income)), "condition", EstateText.Level(h.Cond),
                         "share", EstateText.Share(EstateRules.ConditionValue(h.Cond))));
            box.Note(EstateText.T("reallife_estate_own_cover_what", "amount", EstateText.Money(EstateRules.Premium(h.Base, c)), "excess", EstateText.Share(EstateCatalogue.CoverExcess)));
            Meta.Guide.Point(box, GuideTitle);
        }

        // How a building's units stand, and the one picked with the selector, whose lease opens from the button under it.
        private void WriteUnits(InfoBox box, EstateHolding h, EstateClimate c, int value)
        {
            int behind, let = h.LetUnits(out behind);
            long agreed = 0;
            for (int i = 0; i < h.Units.Count; i++)
            {
                agreed += h.Units[i].Let ? h.Units[i].Rent : 0;
            }
            box.Meter("reallife_ibox_let", LookRules.Fill(let, h.Units.Count), EstateText.T("reallife_ibox_of", "value", let.ToString(), "most", h.Units.Count.ToString()),
                behind > 0 ? CellTone.Yellow : CellTone.Green);
            box.Line(EstateText.T("reallife_estate_own_units", "units", h.Units.Count.ToString(), "let", let.ToString(), "behind", behind.ToString()), behind > 0 ? CellTone.Yellow : CellTone.Plain);
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
                : EstateText.Fill("reallife_estate_own_unit_let", "unit", unit, "who", EstateText.Tenant(h.Use, u.Tenant), "amount", EstateText.Money(u.Rent), "day", u.Until.ToString()),
                !u.Let ? CellTone.Yellow : u.Arrears > 0 ? CellTone.Red : CellTone.Plain);
        }

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
                    _pick = EstateDirector.UnitPick(h, 0);   // a building picked elsewhere opens its first unit's lease
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
                        row.NoTotal = true;   // an offer is not rent yet
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
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_estate_tenants_let", let.ToString());
                box.Row("reallife_estate_tenants_empty", empty.ToString(), empty > 0 ? CellTone.Yellow : CellTone.Plain);
                box.Row("reallife_estate_tenants_owed", EstateText.Money(owed), owed > 0 ? CellTone.Red : CellTone.Plain);
                box.Section("reallife_ibox_sec_how");
                box.Note(EstateText.T("reallife_estate_tenants_notice_what", "days", EstateCatalogue.NoticeArrearsDays.ToString(), "notice", EstateRules.NoticeLength(c.RentLever).ToString(),
                    "court", EstateCatalogue.CourtDays.ToString()));
                box.Note(EstateText.T("reallife_estate_tenants_what"));
                box.Note(EstateText.T("reallife_estate_tenants_rent_what"));
                Meta.Guide.Point(box, GuideTitle);
            }
        }

        private void WriteApplicant(InfoBox box, EstateHolding h, int unit, RentApplicant a, EstateClimate c)
        {
            string place = h.Place;
            int id = a.Id, market = EstateRules.UnitRent(h, c);
            box.Title(EstateText.Tenant(h.Use, a.Kind));
            box.Subtitle(EstateText.UnitName(h, unit));
            box.Chip(EstateText.Record(a.Record));
            box.Line(EstateText.T("reallife_estate_app_for", "building", EstateText.UnitName(h, unit), "day", a.Lapse.ToString()));
            box.Section("reallife_ibox_sec_terms");
            box.Line(EstateText.T("reallife_estate_app_offer", "amount", EstateText.Money(a.Offer), "market", EstateText.Money(market),
                "share", EstateText.Share(market > 0 ? (int)((long)a.Offer * 1000 / market) : 0)));
            box.Line(EstateText.T("reallife_estate_app_term", "days", a.Term.ToString(), "deposit", EstateText.Money(a.Offer * EstateCatalogue.DepositDays)));
            box.Line(EstateText.T("reallife_estate_app_record", "record", EstateText.Record(a.Record), "miss", EstateText.Share(EstateRules.MissChance(a.Record, c.Cycle)),
                "back", EstateText.Share(EstateRules.CatchUpChance(a.Record))));
            Transform buttons = box.ButtonRow();
            if (!h.Units[unit].Let && !h.Busy)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_accept", () =>
                {
                    int taken = _director.Accept(place, id);
                    if (taken >= 0)
                    {
                        _pick = EstateDirector.UnitPick(h, taken);   // the new lease is the page shown next
                        Refresh();
                    }
                });
            }
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_refuse", () => _director.Refuse(place, id));
            BackButton(buttons);
            box.Note(EstateText.T("reallife_estate_app_note"));
            Meta.Guide.Point(box, GuideTitle);
        }

        // One unit's lease, or the unit empty with its asking rent and whether applicants are looked for.
        private void WriteTenancy(InfoBox box, EstateHolding h, int unit, int day, EstateClimate c)
        {
            string place = h.Place;
            EstateUnit u = h.Units[unit];
            int market = EstateRules.UnitRent(h, c);
            box.Title(EstateText.UnitName(h, unit));
            box.Chip(EstateText.T(!u.Let ? "reallife_ibox_chip_empty" : u.CourtDay > 0 ? "reallife_ibox_chip_in_court" : u.NoticeEnd > 0 ? "reallife_ibox_chip_notice"
                    : u.Arrears > 0 ? "reallife_ibox_chip_behind" : "reallife_ibox_chip_let"),
                !u.Let ? CellTone.Yellow : u.CourtDay > 0 || u.Arrears > 0 ? CellTone.Red : u.NoticeEnd > 0 ? CellTone.Yellow : CellTone.Green);
            if (u.Let)
            {
                if (u.Arrears > 0)
                {
                    box.Line(EstateText.T("reallife_estate_ten_arrears", "amount", EstateText.Money(u.Arrears), "days", u.ArrearsDays.ToString(),
                        "back", EstateText.Share(EstateRules.CatchUpChance(u.Record))), CellTone.Red);
                }
                if (u.CourtDay > 0)
                {
                    box.Line(EstateText.T("reallife_estate_ten_court", "day", u.CourtDay.ToString()), CellTone.Red);
                }
                else if (u.NoticeEnd > 0)
                {
                    box.Line(EstateText.T(day > u.NoticeEnd ? "reallife_estate_ten_notice_over" : "reallife_estate_ten_notice", "day", u.NoticeEnd.ToString(),
                        "fee", EstateText.Money(EstateRules.CourtFee(c.Index)), "days", EstateCatalogue.CourtDays.ToString()), CellTone.Yellow);
                }
                box.Section("reallife_ibox_sec_tenant");
                box.Line(EstateText.T("reallife_estate_ten_who", "who", EstateText.Tenant(h.Use, u.Tenant), "amount", EstateText.Money(u.Rent), "day", u.Until.ToString()));
                box.Line(EstateText.T("reallife_estate_ten_record", "record", EstateText.Record(u.Record), "deposit", EstateText.Money(u.Deposit)));
            }
            else
            {
                box.Line(EstateText.T("reallife_estate_ten_empty", "days", u.VoidDays.ToString()), CellTone.Yellow);
            }
            box.Section("reallife_ibox_sec_market");
            box.Line(market > 0
                ? EstateText.Fill("reallife_estate_ten_market", "amount", EstateText.Money(market), "use", EstateText.Use(h.Use), "yield", EstateText.Share(EstateRules.Yield(h.Use, h.Income)),
                    "condition", EstateText.Level(h.Cond))
                : EstateText.T("reallife_estate_ten_unfit", "condition", EstateText.Level(h.Cond)));
            if (h.Units.Count > 1)
            {
                box.Line(EstateText.T("reallife_estate_ten_share", "units", h.Units.Count.ToString(), "amount", EstateText.Money(EstateRules.MarketRent(h, c))));
            }
            if (!u.Let)
            {
                box.Headline("reallife_estate_ten_asking");
                Steps(box, EstateCatalogue.AskLeast, EstateCatalogue.AskMost, EstateCatalogue.AskStep, u.Ask, ask => _director.SetAsk(place, unit, ask));
                box.Note(EstateText.T("reallife_estate_ten_asking_what", "amount", EstateText.Money(EstateCatalogue.Share(market, u.Ask)),
                    "chance", EstateText.Share(EstateRules.ApplicantChance(u.Ask, h.Cond, c.Cycle))));
                if (!box.HasSwitch || !box.Switch("reallife_estate_ten_to_let", u.ToLet, on => _director.SetToLet(place, unit, on)))
                {
                    box.Line(EstateText.T(u.ToLet ? "reallife_estate_ten_to_let_on" : "reallife_estate_ten_to_let_off"), u.ToLet ? CellTone.Green : CellTone.Yellow);
                }
            }
            Transform buttons = box.ButtonRow();
            if (u.Let && EstateRules.MayServeNotice(h, unit, day))
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_notice", () => _director.ServeNotice(place, unit));
            }
            else if (u.Let && EstateRules.MayGoToCourt(h, unit, day))
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_court", () => _director.GoToCourt(place, unit));
            }
            BackButton(buttons);
            Meta.Guide.Point(box, GuideTitle);
        }

        private void WriteWorks(InfoBox box, EstateState state, ref string selected)
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
                box.Section("reallife_ibox_sec_how");
                box.Line(EstateText.T("reallife_estate_works_wear", "poor", EstateText.Level(EstateCatalogue.ConditionPoor), "unfit", EstateText.Level(EstateCatalogue.ConditionUnfit)));
                box.Line(EstateText.T("reallife_estate_works_market", "market", EstateText.Share(state.Real)));
                box.Note(EstateText.T("reallife_estate_works_what"));
                box.Note(EstateText.T("reallife_estate_works_no_new"));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            WriteWorksOf(box, state, picked, c);
        }

        private void WriteWorksOf(InfoBox box, EstateState state, EstateHolding h, EstateClimate c)
        {
            string place = h.Place;
            box.Title(h.Name);
            box.Chip(EstateText.Level(h.Cond), h.Cond < EstateCatalogue.ConditionUnfit ? CellTone.Red : h.Cond < EstateCatalogue.ConditionPoor ? CellTone.Yellow : CellTone.Green);
            if (h.Busy)
            {
                box.Line(EstateText.T("reallife_estate_works_busy", "work", EstateText.Work(h.Work), "day", h.WorkEnd.ToString(), "amount", EstateText.Money(h.WorkCost)), CellTone.Yellow);
            }
            box.Section("reallife_ibox_sec_condition");
            box.Line(EstateText.T("reallife_estate_works_cond", "condition", EstateText.Level(h.Cond), "value", EstateText.Share(EstateRules.ConditionValue(h.Cond)),
                "rent", EstateText.Share(EstateRules.ConditionRent(h.Cond))));
            if (h.Busy)
            {
                BackButton(box.ButtonRow());
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            int repair = EstateRules.Quote(h, EstateWork.Repair, -1, c);
            box.Section("reallife_ibox_sec_works");
            if (repair > 0)
            {
                box.Line(EstateText.T("reallife_estate_works_repair", "amount", EstateText.Money(repair), "days", EstateRules.RepairDays(h.Cond, c.PermitLever).ToString()));
            }
            int refurbish = EstateRules.Quote(h, EstateWork.Refurbish, -1, c);
            if (refurbish > 0)
            {
                int gain = EstateRules.LevelGain(h, h.Level + 1, state.Real, c.Index);
                box.Line(EstateText.T("reallife_estate_works_refurbish", "level", (h.Level + 1).ToString(), "amount", EstateText.Money(refurbish),
                    "days", EstateRules.SiteDays(EstateCatalogue.LevelDays[h.Level + 1], c.PermitLever).ToString()));
                box.Line(EstateText.T(gain >= refurbish ? "reallife_estate_works_gain_more" : "reallife_estate_works_gain_less", "gain", EstateText.Money(gain),
                    "rent", EstateText.Share(EstateCatalogue.LevelRent[h.Level + 1] - EstateCatalogue.LevelRent[h.Level]), "market", EstateText.Share(state.Real)));
            }
            else if (h.Level >= EstateCatalogue.LevelMost)
            {
                box.Note(EstateText.T("reallife_estate_works_level_most"));
            }
            else
            {
                box.Note(EstateText.T("reallife_estate_works_need_empty"));
            }
            if (repair > 0 || refurbish > 0)
            {
                Transform buttons = box.ButtonRow();
                if (repair > 0)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_repair", () => _director.StartWork(place, EstateWork.Repair, -1));
                }
                if (refurbish > 0)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_estate_btn_refurbish", () => _director.StartWork(place, EstateWork.Refurbish, -1));
                }
            }

            // A change of use: the council first, then the builders.
            if (h.Managed && !h.AnyLet && !h.Self)
            {
                box.Headline("reallife_estate_works_use");
                if (h.PlanUse >= 0)
                {
                    int convert = EstateRules.Quote(h, EstateWork.Convert, -1, c);
                    box.Line(EstateText.Fill("reallife_estate_works_convert", "use", EstateText.Use(h.PlanUse), "amount", EstateText.Money(convert),
                        "days", EstateRules.SiteDays(EstateCatalogue.ConvertDays, c.PermitLever).ToString(), "yield", EstateText.Share(EstateRules.Yield(h.PlanUse, h.Income))));
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_estate_btn_convert", () => _director.StartWork(place, EstateWork.Convert, -1));
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
                        box.Line(EstateText.T("reallife_estate_works_yields", "now", EstateText.Share(EstateRules.Yield(h.Use, h.Income)), "then", EstateText.Share(EstateRules.Yield(to, h.Income)),
                            "use", EstateText.Use(to)));
                        InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_estate_btn_planning", () => _director.StartWork(place, EstateWork.Planning, to));
                    }
                    else
                    {
                        box.Note(EstateText.T("reallife_estate_works_same_use"));
                    }
                }
            }
            BackButton(box.ButtonRow());
            if (h.Managed && !h.AnyLet && !h.Self)
            {
                box.Note(EstateText.T("reallife_estate_works_use_note"));
            }
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
