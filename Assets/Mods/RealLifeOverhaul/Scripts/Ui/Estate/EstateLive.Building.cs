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
                int fee = EstateRules.AgentFee(value, _c.Index), tax = EstateRules.GainsTax(value, fee, h.Paid, h.Spent, _c.TaxPercent);
                long gain = (long)value - fee - tax - h.Paid - h.Spent;
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = EstateText.T("reallife_ibox_worth"), Value = Money(value) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_bought"), Value = Money(h.Paid) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_works_spent"), Value = Money(h.Spent) },
                    new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_gain"), Value = (gain >= 0 ? "+" : "-") + Cash(Math.Abs(gain)) }
                };
                _m.Left = new[] { LettingCard(h, value) };
                _m.Right = new[] { SellingCard(h, value), MoreCard(h) };
                if (h.Managed && !h.Self)
                {
                    int behind, let = h.LetUnits(out behind);
                    int happy = EstateRules.BuildingHappy(h);
                    var labels = new List<string> { EstateText.T("reallife_ibox_let") };
                    var values = new List<int> { h.Units.Count > 0 ? let * 100 / h.Units.Count : 0 };
                    if (happy >= 0)
                    {
                        labels.Add(EstateText.T("reallife_estate_duty_happy"));
                        values.Add(happy / 10);
                    }
                    labels.Add(EstateText.T("reallife_ui_estate_m_condition"));
                    values.Add(h.Cond / 10);
                    _m.ShareTitle = EstateText.T("reallife_ui_estate_sh_building");
                    _m.ShareLabels = labels.ToArray();
                    _m.ShareValues = values.ToArray();
                    // The flats as the roll's rows: the tenant's face, their state and what they pay a month.
                    var flats = new List<EstateRow>(h.Units.Count);
                    for (int n = 0; n < h.Units.Count; n++)
                    {
                        EstateUnit u = h.Units[n];
                        StateChip(h, u, out string chip, out UiRole tone);
                        Who who = u.Let ? WhoOf(h, u) : default(Who);
                        int at = n;
                        flats.Add(new EstateRow
                        {
                            Face = u.Let ? who.Name : null, Hue = who.Hue, Icon = u.Let ? UiIcon.None : UseIcon(h.Use), Title = u.Let ? who.Name : EstateText.T("reallife_estate_row_nobody"),
                            Sub = EstateText.UnitName(h, n), Chip = chip, ChipTone = tone, Amount = Month(u.Let ? u.Rent : EstateCatalogue.Share(EstateRules.UnitRent(h, _c), u.Ask)),
                            Run = _runs.Do(() => Open(EstateDirector.UnitPick(h, at), "own:" + place))
                        });
                    }
                    _m.RelatedTitle = EstateText.T("reallife_ui_estate_rel_let_to");
                    _m.RelatedCount = EstateText.T("reallife_ui_estate_units_n", "count", h.Units.Count.ToString(Inv));
                    _m.RelatedRows = 6;
                    _m.Related = flats.ToArray();
                    // The works and the servicing booked on it.
                    var works = new List<EstateRow>(4);
                    for (int b = 0; b < h.Services.Count && works.Count < 3; b++)
                    {
                        ServiceBooking v = h.Services[b];
                        works.Add(new EstateRow
                        {
                            Icon = UiIcon.Wrench, Title = EstateText.T("reallife_ui_estate_service_name", "fault", EstateText.Fault(v.Kind)), Sub = ContractorName(v.Contractor),
                            Chip = EstateText.T("reallife_ui_estate_day", "day", v.Day.ToString(Inv)), ChipTone = UiRole.Notice, Amount = Money(v.Fee), Run = -1
                        });
                    }
                    if (h.InspectDay > 0 && works.Count < 4)
                    {
                        works.Add(new EstateRow
                        {
                            Icon = UiIcon.Eye, Title = EstateText.T("reallife_ui_estate_next_inspection"), Sub = EstateText.T(h.LastInspect == (int)InspectResult.Failed ? "reallife_ui_estate_last_failed" : "reallife_ui_estate_last_passed"),
                            Chip = EstateText.T("reallife_ui_estate_in_days", "days", Math.Max(0, h.InspectDay - _day).ToString(Inv)), ChipTone = UiRole.Notice, Run = -1
                        });
                    }
                    _m.Related2Title = EstateText.T("reallife_ui_estate_works_servicing");
                    _m.Related2Count = works.Count > 0 ? works.Count.ToString(Inv) : null;
                    _m.Related2 = works.ToArray();
                }
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
                    if (Duties)
                    {
                        long roll = 0, owed = 0;
                        for (int n = 0; n < h.Units.Count; n++)
                        {
                            roll += h.Units[n].Let ? h.Units[n].Rent : 0;
                            owed += h.Units[n].Let ? h.Units[n].Arrears : 0;
                        }
                        lines.Add(UiFactLine.Row(EstateText.T("reallife_estate_duty_roll"), Month((int)roll)));
                        lines.Add(UiFactLine.Row(EstateText.T("reallife_estate_duty_owed"), Cash(owed), owed > 0 ? UiRole.DangerText : UiRole.None));
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
                return new UiFactCardModel { Title = EstateText.T("reallife_ui_estate_running"), Heading = true, Lines = lines.ToArray() };
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
