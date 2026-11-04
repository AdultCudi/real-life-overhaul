using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class EstateLive
    {
        private static readonly int[] ListShares = { 900, 950, 1000, 1050, 1100, 1150, 1200, 1300 };

        private sealed partial class Page
        {
            // One building: what it is worth and why, what it brings, its units, its cover and who runs it, and selling it.
            private void BuildingPage(EstateHolding h)
            {
                string place = h.Place;
                int value = EstateRules.Value(h, _s.Real, _c.Index);
                _m.Shape = EstateShape.Detail;
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = UseIcon(h.Use);
                _m.DetailTitle = h.Name;
                _m.DetailSub = EstateText.T("reallife_ibox_type_in", "type", EstateText.Use(h.Use), "district", EstateText.District(h.District));
                _m.DetailLocate = EstateText.T("reallife_ui_estate_locate");
                _m.DetailLocateRun = _runs.Do(() => EstateGame.Locate(place));
                if (h.SaleDay > 0 || h.Offer > 0 || h.List > 0)
                {
                    _m.DetailChip = EstateText.T(h.SaleDay > 0 ? "reallife_ibox_chip_sold" : h.Offer > 0 ? "reallife_ibox_chip_offer" : "reallife_ibox_chip_for_sale");
                    _m.DetailChipTone = h.Offer > 0 ? UiRole.Ok : UiRole.Notice;
                }
                _m.Left = new[] { MoneyCard(h, value), LettingCard(h, value) };
                _m.Right = new[] { SellingCard(h, value), MoreCard(h) };
                _m.Lower = RentChart(h);
            }

            // What each let unit pays a month, side by side, under a building of several: the rent roll's own figures for this building.
            private UiChartModel RentChart(EstateHolding h)
            {
                if (!h.Managed || h.Self)
                {
                    return null;
                }
                var names = new List<string>(h.Units.Count);
                var rents = new List<float>(h.Units.Count);
                for (int n = 0; n < h.Units.Count && names.Count < 8; n++)
                {
                    if (h.Units[n].Let)
                    {
                        string name = EstateText.UnitName(h, n);
                        int comma = name.IndexOf(',');
                        names.Add(comma > 0 ? name.Substring(comma + 1).Trim() : name);
                        rents.Add((float)h.Units[n].Rent * _monthDays);
                    }
                }
                return names.Count >= 2 ? EstateCharts.Bars(EstateText.T("reallife_ui_estate_col_rent_month"), names.ToArray(), rents.ToArray(), null, EstateCharts.Dollars) : null;
            }

            private UiFactCardModel MoneyCard(EstateHolding h, int value)
            {
                var lines = new List<UiFactLine>
                {
                    UiFactLine.Row(EstateText.T("reallife_ibox_worth"), Money(value)),
                    UiFactLine.Say(EstateText.T("reallife_estate_own_worth", "amount", Money(value), "paid", Money(h.Paid), "day", h.BoughtDay.ToString(Inv))),
                    UiFactLine.Say(EstateText.T("reallife_estate_own_year", "rent", Money(h.RentYear), "costs", Money(h.CostYear), "spent", Money(h.Spent)))
                };
                int fee = EstateRules.AgentFee(value, _c.Index), tax = EstateRules.GainsTax(value, fee, h.Paid, h.Spent, _c.TaxPercent);
                long kept = (long)value - fee - tax, gain = kept - h.Paid - h.Spent;
                lines.Add(UiFactLine.Say(EstateText.Fill(gain >= 0 ? "reallife_estate_flip_gain" : "reallife_estate_flip_loss", "paid", Money(h.Paid), "spent", Money(h.Spent), "amount", Cash(kept),
                    "gain", Cash(Math.Abs(gain))), gain >= 0 ? UiRole.OkText : UiRole.WarnText));
                return new UiFactCardModel { Title = EstateText.T("reallife_ibox_sec_money"), Heading = true, Lines = lines.ToArray() };
            }

            private UiFactCardModel LettingCard(EstateHolding h, int value)
            {
                string place = h.Place;
                var lines = new List<UiFactLine>(12);
                if (h.Self)
                {
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_self")));
                }
                else if (!h.Managed)
                {
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_game", "amount",
                        Money(EstateRules.GameNight(EstateGame.GameRent(h), _c.RentScale)), "market", Money(EstateRules.MarketRent(h, _c)))));
                }
                else
                {
                    int behind, let = h.LetUnits(out behind);
                    lines.Add(UiFactLine.Bar(EstateText.T("reallife_ibox_let"), h.Units.Count > 0 ? let * 100 / h.Units.Count : 0));
                    for (int n = 0; n < h.Units.Count; n++)
                    {
                        EstateUnit u = h.Units[n];
                        int at = n;
                        string who = u.Let ? WhoOf(h, u).Name : EstateText.T("reallife_estate_row_nobody");
                        string line = EstateText.UnitName(h, n) + ", " + who + ", " + (u.Let ? Month(u.Rent) : EstateText.T("reallife_estate_state_empty", "days", u.VoidDays.ToString(Inv)));
                        lines.Add(_runs.Link(line, () => Open(EstateDirector.UnitPick(h, at), "own:" + place)));
                    }
                    if (Duties)
                    {
                        long roll = 0, owed = 0;
                        for (int n = 0; n < h.Units.Count; n++)
                        {
                            roll += h.Units[n].Let ? h.Units[n].Rent : 0;
                            owed += h.Units[n].Let ? h.Units[n].Arrears : 0;
                        }
                        lines.Add(UiFactLine.Row(EstateText.T("reallife_estate_duty_roll"), EstateText.T("reallife_estate_row_a_day", "amount", Cash(roll))));
                        lines.Add(UiFactLine.Row(EstateText.T("reallife_estate_duty_owed"), Cash(owed), owed > 0 ? UiRole.DangerText : UiRole.None));
                        int happy = EstateRules.BuildingHappy(h);
                        if (happy >= 0)
                        {
                            lines.Add(UiFactLine.Bar(EstateText.T("reallife_estate_duty_happy"), happy / 10));
                        }
                        int calls = EstateRules.OpenCalls(h);
                        if (calls > 0)
                        {
                            lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_duty_calls", "count", calls.ToString(Inv)), UiRole.WarnText));
                        }
                        if (EstateRules.Barred(h))
                        {
                            lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_duty_barred", "day", h.InspectDay.ToString(Inv)), UiRole.DangerText));
                        }
                        else if (h.OrderDay > 0)
                        {
                            lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_duty_order", "day", h.OrderDay.ToString(Inv), "mark",
                                EstateText.Level(EstateCatalogue.InspectPass)), UiRole.DangerText));
                        }
                        else if (h.InspectDay > 0)
                        {
                            lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_duty_inspect", "day", h.InspectDay.ToString(Inv), "score", EstateText.Level(EstateRules.InspectScore(h, _day)),
                                "mark", EstateText.Level(EstateCatalogue.InspectPass))));
                        }
                        lines.Add(UiFactLine.Row(EstateText.T("reallife_estate_duty_costs_row"), Cash((long)h.CallsYear + h.FeesYear + h.FinesYear)));
                        int manager = h.Manager;
                        lines.Add(_runs.Step(EstateText.T("reallife_estate_duty_who"), EstateText.Manager(manager),
                            EstateText.T(manager == (int)EstateManager.Manager ? "reallife_estate_duty_manager_what"
                                : manager == (int)EstateManager.Agent ? "reallife_estate_duty_agent_what" : "reallife_estate_duty_self_what",
                                "share", EstateText.Share(EstateCatalogue.ManagerShare), "days", EstateCatalogue.AgentLetDays.ToString(Inv)),
                            manager > 0, manager < EstateCatalogue.Managers - 1, () => Show(() => _d.SetManager(place, manager - 1)), () => Show(() => _d.SetManager(place, manager + 1))));
                    }
                    bool cover = h.Cover;
                    lines.Add(_runs.Switch(EstateText.T("reallife_estate_own_cover"), cover, on => Show(() => _d.SetCover(place, on))));
                }
                return new UiFactCardModel { Title = EstateText.T("reallife_ibox_sec_letting"), Heading = true, Lines = lines.ToArray() };
            }

            private UiFactCardModel SellingCard(EstateHolding h, int value)
            {
                string place = h.Place;
                var lines = new List<UiFactLine>(6);
                var actions = new List<UiActionModel>(2);
                if (h.SaleDay > 0)
                {
                    lines.Add(UiFactLine.Tick(EstateText.T("reallife_estate_own_sale_agreed", "amount", Money(h.SalePrice), "day",
                        h.SaleDay.ToString(Inv), "fee", Money(EstateRules.AgentFee(h.SalePrice, _c.Index))), true));
                }
                else if (h.Offer > 0)
                {
                    int fee = EstateRules.AgentFee(h.Offer, _c.Index), tax = EstateRules.GainsTax(h.Offer, fee, h.Paid, h.Spent, _c.TaxPercent);
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_offer", "amount", Money(h.Offer), "day", h.OfferLapse.ToString(Inv), "asked", Money(h.List)), UiRole.OkText));
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_offer_net", "fee", Money(fee), "tax", Money(tax), "amount", Money(h.Offer - fee - tax))));
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_offer_days", "days", EstateCatalogue.CompletionDays.ToString(Inv))));
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _d.RefuseOffer(place))));
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_accept"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() => _d.AcceptOffer(place))));
                }
                else if (h.List > 0)
                {
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_listed", "amount", Money(h.List), "chance", EstateText.Share(EstateRules.SaleChance(h.List, value, _c.Cycle)))));
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_unlist"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count, () => Show(() => _d.List(place, 0))));
                }
                else
                {
                    int at = Math.Max(0, Math.Min(ListShares.Length - 1, _v.ListShare));
                    int share = ListShares[at], asked = EstateCatalogue.Share(value, share);
                    lines.Add(_runs.Step(EstateText.T("reallife_estate_own_selling"), EstateText.Share(share),
                        EstateText.T("reallife_estate_own_list_what", "amount", Money(asked), "chance",
                            EstateText.Share(EstateRules.SaleChance(asked, value, _c.Cycle)), "fee", EstateText.Share(EstateCatalogue.AgentFee)),
                        at > 0, at < ListShares.Length - 1, () => Show(() => _v.ListShare = at - 1), () => Show(() => _v.ListShare = at + 1)));
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_list"), UiButtonKind.Primary, UiIcon.Tag, UiAction.Alt, () => Show(() => _d.List(place, share))));
                }
                return new UiFactCardModel { Title = EstateText.T("reallife_estate_own_selling"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() };
            }

            // Taking a building on or handing it back, its works and what its value is made of.
            private UiFactCardModel MoreCard(EstateHolding h)
            {
                string place = h.Place;
                var lines = new List<UiFactLine>(6);
                var actions = new List<UiActionModel>(3);
                if (!h.Self && !h.Managed)
                {
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_take_on"), UiButtonKind.Primary, UiIcon.Key, UiAction.Count, () => Show(() => _d.TakeOn(place))));
                }
                else if (h.Managed && !h.AnyLet && !h.Busy)
                {
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_hand_back"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count, () => Show(() => _d.HandBack(place))));
                }
                lines.Add(_runs.Link(EstateText.T("reallife_ui_estate_works_link"), () => Show(() => OpenWorks("wrk:" + place))));
                lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_made_of", "base", Money(h.Base), "market", EstateText.Share(_s.Real), "prices", EstateText.Share(_c.Index)) + " "
                    + EstateText.T("reallife_estate_own_made_of_2", "district", EstateText.Share(EstateRules.DistrictValue(h.Income)), "condition", EstateText.Level(h.Cond),
                        "share", EstateText.Share(EstateRules.ConditionValue(h.Cond)))));
                lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_own_cover_what", "amount", Money(EstateRules.Premium(h.Base, _c)), "excess", EstateText.Share(EstateCatalogue.CoverExcess))));
                return new UiFactCardModel { Title = EstateText.T("reallife_ui_estate_more"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() };
            }
        }
    }
}
