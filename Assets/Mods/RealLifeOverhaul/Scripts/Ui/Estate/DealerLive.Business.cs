using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class DealerLive
    {
        private sealed partial class Page
        {
            // ------------------------------------------------------------------------------------------------- offers

            public void Offers()
            {
                int id;
                DealerOffer picked = null;
                DealerYard pickedYard = null;
                bool want = Parse(_v.Pick, "deal:", out id);
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                for (int y = 0; y < _s.Yards.Count; y++)
                {
                    DealerYard yard = _s.Yards[y];
                    for (int d = 0; d < yard.Deals.Count; d++)
                    {
                        DealerOffer deal = yard.Deals[d];
                        StockCar car = yard.Car(deal.Car);
                        if (car == null)
                        {
                            continue;
                        }
                        int offer = DealerRules.Price(yard, deal, car), left = Mathf.Max(0, deal.Expires - _now);
                        int share = car.Asking > 0 ? Math.Min(100, offer * 100 / car.Asking) : 0;
                        rows.Add(new UiDataRow
                        {
                            Cells = new[] { deal.Customer, DealerGame.CarName(car.Type), Money(offer), Money(car.Asking), share.ToString(Inv), DealerText.Hours(left),
                                deal.Stage == OfferStage.Driving ? DealerText.T("reallife_dealer_state_driving") : DealerText.T("reallife_dealer_state_waiting") },
                            Values = new[] { 0, (int)UiIcon.Car, 0, 0, share, 0, 0 },
                                Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, left <= 3 ? UiRole.DangerText : UiRole.None, UiRole.Warn },
                            Subs = new[] { DealerText.Profile(deal.Profile), yard.Name, null, null, null, null, null }, Hue = UiColour.HueOf(deal.Customer), Ring = UiRole.None
                        });
                        keys.Add("deal:" + deal.Id);
                        if (want && deal.Id == id)
                        {
                            picked = deal;
                            pickedYard = yard;
                        }
                    }
                }
                if (picked != null)
                {
                    DealPage(pickedYard, picked);
                    return;
                }
                _v.Pick = null;
                int waiting = rows.Count;
                for (int i = 0; i < _s.Sales.Count && i < 12; i++)
                {
                    DealerSale s = _s.Sales[i];
                    DealerYard yard = _s.Yard(s.Place);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { s.Trade ? DealerText.T("reallife_dealer_to_trade") : s.Customer, DealerGame.CarName(s.Type), Money(s.Price),
                            Money(s.Cost), string.Empty, FinanceText.Day(s.Day),
                            DealerText.T("reallife_dealer_state_sold", "margin", Money(s.Price - s.Cost)) },
                        Values = new[] { 0, (int)UiIcon.Car, 0, 0, -1, 0, 0 },
                            Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, s.Price < s.Cost ? UiRole.Danger : UiRole.Ok },
                        Subs = new[] { yard != null ? yard.Name : string.Empty, string.Empty, null, null, null, null, null }, Hue = UiColour.HueOf(s.Customer ?? string.Empty)
                    });
                    keys.Add(null);   // a car sold is history: nothing to open
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_buyer"), Kind = UiCellKind.Person, Width = 0f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_car"), Kind = UiCellKind.Tile, Width = 184f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_offer"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_asking"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_against"), Kind = UiCellKind.Meter, Width = 120f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_left"), Kind = UiCellKind.Text, Width = 80f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_state"), Kind = UiCellKind.Chip, Width = 200f }
                };
                int sold = 0, margin = 0;
                for (int y = 0; y < _s.Yards.Count; y++)
                {
                    sold += _s.Yards[y].Today.Sold;
                    margin += _s.Yards[y].Today.Margin;
                }
                var side = new UiFactCardModel
                {
                    Title = DealerText.T("reallife_dealer_offers_title"), Heading = true,
                    Lines = new[]
                    {
                        UiFactLine.Row(DealerText.T("reallife_dealer_sum_waiting"), waiting.ToString(Inv), waiting > 0 ? UiRole.WarnText : UiRole.None),
                        UiFactLine.Say(DealerText.Fill("reallife_dealer_offers_today", "sold", sold.ToString(Inv), "margin", Money(margin))),
                        UiFactLine.Say(DealerText.T("reallife_dealer_offers_about"))
                    }
                };
                TableShape(UiIcon.Handshake, DealerText.T("reallife_dealer_offers_title"),
                    DealerText.T("reallife_ui_dealer_offers_sub", "waiting", waiting.ToString(Inv), "sold", (rows.Count - waiting).ToString(Inv)), cols, rows, keys, side,
                    DealerText.T("reallife_ui_dealer_empty_offers"), DealerText.T("reallife_dealer_offers_about"));
            }

            private string Extras(DealerOffer d)
            {
                var t = new List<string>(5);
                if (d.WantsDrive && !d.Drove)
                {
                    t.Add(DealerText.T("reallife_dealer_extra_drive"));
                }
                if (d.WantsFinance)
                {
                    t.Add(DealerText.T("reallife_dealer_extra_finance"));
                }
                if (d.TradeType != null)
                {
                    t.Add(DealerText.T("reallife_dealer_extra_trade"));
                }
                if (d.Tier >= 0)
                {
                    t.Add(DealerText.Tier(d.Tier));
                }
                if (d.Extras != 0)
                {
                    t.Add(DealerText.Extras(d.Extras));
                }
                return string.Join(", ", t.ToArray());
            }

            private static int ExtrasPrice(int extras, int price, out int cost)
            {
                int sell = 0;
                cost = 0;
                for (int e = 0; e < DealerCatalogue.ExtraKinds; e++)
                {
                    if ((extras & (1 << e)) != 0)
                    {
                        int one = DealerRules.ExtraPrice(price, e, Prices.At(DealerCatalogue.ExtraLeast[e]));
                        sell += one;
                        cost += DealerRules.ExtraCost(one, e);
                    }
                }
                return sell;
            }

            private void DealPage(DealerYard yard, DealerOffer d)
            {
                StockCar car = yard.Car(d.Car);
                int id = d.Id;
                int offer = DealerRules.Price(yard, d, car);
                DetailHead(UiIcon.Handshake, d.Customer, DealerText.Profile(d.Profile) + ", " + DealerGame.CarName(car.Type) + ", " + yard.Name,
                    DealerText.T(d.Stage == OfferStage.Driving ? "reallife_ibox_chip_driving" : "reallife_dealer_state_waiting"), UiRole.Warn);
                var lines = new List<UiFactLine>(10)
                {
                    UiFactLine.Row(DealerText.T("reallife_ibox_offer"), Money(offer), UiRole.AccentText),
                    UiFactLine.Row(DealerText.T("reallife_ibox_asking"), Money(car.Asking)),
                    UiFactLine.Say(DealerText.Fill("reallife_dealer_deal_what", "car", DealerGame.CarName(car.Type), "name", yard.Name, "offer",
                        Money(offer), "asking", Money(car.Asking), "left", DealerText.Hours(Mathf.Max(0, d.Expires - _now))))
                };
                if (d.Looks > 0)
                {
                    lines.Add(UiFactLine.Say(DealerText.T(d.Looks > 1 ? "reallife_ui_dealer_deal_looked" : "reallife_ui_dealer_deal_looked_once", "times", d.Looks.ToString(Inv))));
                }
                if (d.Stage == OfferStage.Driving)
                {
                    lines.Add(UiFactLine.Say(DealerText.T("reallife_dealer_deal_driving")));
                }
                else if (d.WantsDrive && !d.Drove)
                {
                    lines.Add(UiFactLine.Say(DealerText.T("reallife_dealer_deal_wants_drive"), UiRole.WarnText));
                }
                if (yard.Fixed)
                {
                    lines.Add(UiFactLine.Say(DealerText.T("reallife_dealer_deal_fixed"), UiRole.OkText));
                }
                var extra = new List<UiFactLine>(6);
                if (d.WantsFinance)
                {
                    bool lends = yard.Finance && DealerRules.Lends(d.Score);
                    extra.Add(UiFactLine.Say(DealerText.Fill(lends ? "reallife_dealer_deal_finance" : yard.Finance
                        ? "reallife_dealer_deal_finance_refused" : "reallife_dealer_deal_finance_off", "deposit", d.Deposit.ToString(Inv),
                        "weeks", ((d.Term + DealerCatalogue.WeekDays - 1) / DealerCatalogue.WeekDays).ToString(Inv), "grade", FinanceText.Grade(CreditCatalogue.GradeOf(d.Score)))));
                }
                if (d.TradeType != null)
                {
                    extra.Add(UiFactLine.Say(DealerText.Fill("reallife_dealer_deal_trade", "car", DealerGame.CarName(d.TradeType), "km",
                        DealerText.Km(d.TradeKm), "allowance", Money(d.TradeAllowance))));
                }
                if (DealerRules.Tier(yard.Warranty, d.Tier, _s) >= 0)
                {
                    extra.Add(UiFactLine.Say(DealerText.T("reallife_dealer_deal_warranty", "tier", DealerText.Tier(d.Tier), "premium",
                        Money(DealerRules.Premium(offer, d.Tier, Prices.At(DealerCatalogue.PremiumLeast))))));
                }
                if (d.Extras != 0 && yard.Extras)
                {
                    int cost;
                    int sell = ExtrasPrice(d.Extras, offer, out cost);
                    extra.Add(UiFactLine.Say(DealerText.Fill("reallife_dealer_deal_extras", "extras", DealerText.Extras(d.Extras), "price", Money(sell), "margin", Money(sell - cost))));
                }
                if (d.Lift > 0)
                {
                    extra.Add(UiFactLine.Say(DealerText.T("reallife_dealer_deal_lift"), UiRole.OkText));
                }
                int steps = yard.Fixed ? 1 : DealerRules.CounterSteps(d, car.Asking);
                string what = "deal:" + d.Id;
                int step = Choice(what, steps - 1);
                int counter = DealerRules.CounterAt(d, car.Asking, step);
                if (d.Stage == OfferStage.Open && steps > 1)
                {
                    lines.Add(_runs.Step(DealerText.T("reallife_ui_dealer_your_counter"), Money(counter),
                        DealerText.T("reallife_ui_dealer_counters_left", "left", (DealerCatalogue.CountersMost - d.Counters).ToString(Inv)),
                        step > 0, step < steps - 1, () => Show(() => SetChoice(what, step - 1)), () => Show(() => SetChoice(what, step + 1))));
                }
                var actions = new List<UiActionModel>(4);
                if (d.Stage == OfferStage.Open)
                {
                    actions.Add(_runs.Button(DealerText.T("reallife_dealer_button_decline"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count, () => Show(() =>
                    {
                        _d.Decline(id);
                        _v.Pick = null;
                    })));
                    if (d.WantsDrive && !d.Drove)
                    {
                        actions.Add(_runs.Button(DealerText.T("reallife_dealer_button_drive"), UiButtonKind.Secondary, UiIcon.Car, UiAction.Count, () => Show(() => _d.Drive(id))));
                    }
                    if (steps > 1)
                    {
                        actions.Add(_runs.Button(DealerText.T("reallife_dealer_button_counter"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() =>
                        {
                            bool gone;
                            bool taken = _d.Counter(id, counter, out gone);
                            if (taken || gone)
                            {
                                _v.Pick = null;
                            }
                            else
                            {
                                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_dealer_note_counter_refused", null, 6f);
                            }
                        })));
                    }
                    actions.Add(_runs.Button(DealerText.T("reallife_dealer_button_accept"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() =>
                    {
                        if (_d.Accept(id))
                        {
                            _v.Pick = null;
                        }
                    })));
                }
                _m.Left = new[] { new UiFactCardModel { Title = DealerText.T("reallife_ibox_sec_offer"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() } };
                if (extra.Count > 0)
                {
                    // The car itself first (what it cost and is worth, what the offer leaves, how the other dealers ask), then what the buyer brings with it.
                    int worth = DealerDirector.Worth(car, _level), margin = offer - car.Cost;
                    var carLines = new List<UiFactLine>(7)
                    {
                        UiFactLine.Row(DealerText.T("reallife_ui_dealer_l_cost"), Money(car.Cost)),
                        UiFactLine.Row(DealerText.T("reallife_ibox_worth"), Money(worth)),
                        UiFactLine.Row(DealerText.T("reallife_ui_dealer_l_margin_offer"), (margin >= 0 ? "+" : "-") + Money(Math.Abs(margin)), margin >= 0 ? UiRole.OkText : UiRole.DangerText),
                        UiFactLine.Row(DealerText.T("reallife_ui_dealer_l_condition"), DealerText.Condition(car.Condition)),
                        UiFactLine.Row(DealerText.T("reallife_ui_dealer_l_km"), DealerText.Km(car.Km))
                    };
                    int rivalAt;
                    int cheapest = RivalRules.Cheapest(_s, car.Type, out rivalAt);
                    carLines.Add(UiFactLine.Say(cheapest > 0 && rivalAt >= 0
                        ? DealerText.T("reallife_ui_dealer_rival_asks", "name", DealerCatalogue.RivalNames[rivalAt % DealerCatalogue.RivalNames.Length], "price", Money(cheapest))
                        : DealerText.T("reallife_ui_dealer_no_rival")));
                    var cards = new List<UiFactCardModel>(2) { new UiFactCardModel { Title = DealerText.T("reallife_ui_dealer_the_car"), Heading = true, Lines = carLines.ToArray() } };
                    if (extra.Count > 0)
                    {
                        cards.Add(new UiFactCardModel { Title = DealerText.T("reallife_ui_dealer_with_it"), Heading = true, Lines = extra.ToArray() });
                    }
                    _m.Right = cards.ToArray();
                }
            }

            // ------------------------------------------------------------------------------------------------- finance

            public void Finance()
            {
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                int owed = 0, weekly = 0, missed = 0, paidOut = 0;
                for (int i = 0; i < _s.Plans.Count; i++)
                {
                    DealerPlan p = _s.Plans[i];
                    DealerYard yard = _s.Yard(p.Place);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { p.Customer, DealerGame.CarName(p.Type), Money(p.Owed), Money(p.Weekly), FinanceText.Day(p.Due),
                            p.Missed > 0 ? DealerText.T("reallife_dealer_plan_missed", "weeks", p.Missed.ToString(Inv))
                                : DealerText.T("reallife_dealer_plan_paying", "grade", FinanceText.Grade(CreditCatalogue.GradeOf(p.Score))) },
                        Values = new[] { 0, (int)UiIcon.Car, 0, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, p.Missed > 0 ? UiRole.Danger : UiRole.Ok },
                        Subs = new[] { yard != null ? yard.Name : string.Empty, string.Empty, null, null, null, null }, Hue = UiColour.HueOf(p.Customer)
                    });
                    keys.Add(null);
                    owed += p.Owed;
                    weekly += p.Weekly;
                    missed += p.Missed > 0 ? 1 : 0;
                }
                for (int i = 0; i < _s.Covers.Count; i++)
                {
                    DealerCover c = _s.Covers[i];
                    DealerYard yard = _s.Yard(c.Place);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { c.Customer, DealerGame.CarName(c.Type), Money(c.Paid), string.Empty, FinanceText.Day(c.Until), DealerText.Tier(c.Tier) },
                        Values = new[] { 0, (int)UiIcon.ShieldCheck, 0, 0, 0, 0 },
                            Tones = new[] { UiRole.None, UiRole.None, c.Paid > 0 ? UiRole.Warn : UiRole.None, UiRole.None, UiRole.None, UiRole.Notice },
                        Subs = new[] { yard != null ? yard.Name : string.Empty,
                            DealerText.T("reallife_ui_dealer_claims", "count", c.Claims.ToString(Inv)), null, null, null, null }, Hue = UiColour.HueOf(c.Customer)
                    });
                    keys.Add(null);
                    paidOut += c.Paid;
                }
                _v.Pick = null;
                var cols = new[]
                {
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_customer"), Kind = UiCellKind.Person, Width = 0f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_car"), Kind = UiCellKind.Tile, Width = 184f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_owed_paid"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_weekly"), Kind = UiCellKind.Money, Width = 104f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_until"), Kind = UiCellKind.Date, Width = 96f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_state"), Kind = UiCellKind.Chip, Width = 200f }
                };
                var side = new UiFactCardModel
                {
                    Title = DealerText.T("reallife_dealer_finance_title"), Heading = true,
                    Lines = new[]
                    {
                        UiFactLine.Say(DealerText.Fill("reallife_dealer_finance_plans", "plans", _s.Plans.Count.ToString(Inv), "owed", Money(owed),
                            "weekly", Money(weekly), "missed", missed.ToString(Inv))),
                        UiFactLine.Say(DealerText.Fill("reallife_dealer_finance_covers", "covers", _s.Covers.Count.ToString(Inv), "paid",
                            Money(paidOut))), UiFactLine.Say(DealerText.T("reallife_dealer_finance_about"))
                    }
                };
                TableShape(UiIcon.Receipt, DealerText.T("reallife_dealer_finance_title"),
                    DealerText.T("reallife_ui_dealer_finance_sub", "plans", _s.Plans.Count.ToString(Inv), "covers", _s.Covers.Count.ToString(Inv)), cols, rows, keys, side,
                    DealerText.T("reallife_ui_dealer_empty_finance"), DealerText.T("reallife_dealer_finance_about"));
            }

            // ------------------------------------------------------------------------------------------------- growth

            private DealerYard Running(int kind)
            {
                for (int y = 0; y < _s.Yards.Count; y++)
                {
                    if (_s.Yards[y].Event == kind && _s.Yards[y].EventOn(_day))
                    {
                        return _s.Yards[y];
                    }
                }
                return null;
            }

            public void Growth()
            {
                string ev = Picked(_v.Pick, "event:");
                int kind;
                if (ev != null && int.TryParse(ev, out kind) && kind >= 0 && kind < DealerCatalogue.EventKinds)
                {
                    EventPage(kind);
                    return;
                }
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                for (int k = 0; k < DealerCatalogue.EventKinds; k++)
                {
                    DealerYard running = Running(k);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { DealerText.Event(k, 0), running != null ? running.Name
                            : DealerText.T("reallife_dealer_days", "days", DealerCatalogue.EventDays[k].ToString(Inv)), Money(DealerDirector.EventCost(k)), DealerText.EventShort(k),
                            running != null ? DealerText.T("reallife_dealer_state_event_on", "day", FinanceText.Day(running.EventUntil)) : DealerText.T("reallife_dealer_state_event_ready") },
                        Values = new[] { (int)UiIcon.Megaphone, 0, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, running != null ? UiRole.Ok : UiRole.Notice },
                        Subs = new[] { DealerText.T("reallife_ui_dealer_growth_event"), null, null, null, null }
                    });
                    keys.Add(DealerDirector.EventRow(k));
                }
                for (int y = 0; y < _s.Yards.Count; y++)
                {
                    DealerYard yard = _s.Yards[y];
                    for (int m = 0; m < DealerCatalogue.Missions; m++)
                    {
                        bool done = DealerRules.MissionDone(yard, m);
                        int progress = DealerRules.Progress(yard, m);
                        rows.Add(new UiDataRow
                        {
                            Cells = new[] { DealerText.Mission(m), yard.Name,
                                DealerText.T("reallife_dealer_progress", "done", progress.ToString(Inv), "target",
                                DealerCatalogue.MissionTarget[m].ToString(Inv)), DealerText.MissionAbout(m),
                                DealerText.T(done ? "reallife_dealer_state_mission_done" : "reallife_dealer_state_mission_open") },
                            Values = new[] { (int)UiIcon.Trophy, 0, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, done ? UiRole.Ok : UiRole.Notice },
                            Subs = new[] { DealerText.T("reallife_ui_dealer_growth_mission", "points", DealerCatalogue.MissionReward[m].ToString(Inv)), null, null, null, null }
                        });
                        keys.Add(null);
                    }
                    int step = DealerRules.Step(yard.Rep);
                    bool top = step + 1 >= DealerCatalogue.RepSteps.Length;
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { DealerText.Step(step), yard.Name, yard.Rep.ToString(Inv)
                            + (top ? string.Empty : " / " + DealerCatalogue.RepSteps[step + 1].ToString(Inv)), DealerText.Perk(step), string.Empty },
                        Values = new[] { (int)UiIcon.Star, 0, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { DealerText.T("reallife_ui_dealer_growth_rep"), null, null, null, null }
                    });
                    keys.Add(null);
                }
                _v.Pick = null;
                var cols = new[]
                {
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_what"), Kind = UiCellKind.Tile, Width = 216f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_showroom"), Kind = UiCellKind.Text, Width = 176f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_progress"), Kind = UiCellKind.Text, Width = 144f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_about"), Kind = UiCellKind.Text, Width = 0f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_state"), Kind = UiCellKind.Chip, Width = 200f }
                };
                var side = new UiFactCardModel
                {
                    Title = DealerText.T("reallife_dealer_growth_title"), Heading = true,
                    Lines = new[] { UiFactLine.Say(DealerText.T("reallife_dealer_growth_events")),
                        UiFactLine.Say(DealerText.T("reallife_dealer_growth_rep")), UiFactLine.Say(DealerText.T("reallife_dealer_growth_missions")) }
                };
                TableShape(UiIcon.Rocket, DealerText.T("reallife_dealer_growth_title"), DealerText.T("reallife_ui_dealer_growth_sub"), cols, rows,
                    keys, side, DealerText.T("reallife_dealer_row_no_dealership"), DealerText.T("reallife_dealer_growth_events"));
            }

            private readonly List<int> _kinds = new List<int>(6);
            private static string _eventYard;
            private static int _eventFeature;

            private void EventPage(int kind)
            {
                int cost = DealerDirector.EventCost(kind);
                DetailHead(UiIcon.Megaphone, DealerText.Event(kind, 0), DealerText.EventShort(kind), null, UiRole.Notice);
                var terms = new List<UiFactLine>
                {
                    UiFactLine.Say(DealerText.EventAbout(kind)), UiFactLine.Row(DealerText.T("reallife_ibox_cost"), Money(cost)),
                    UiFactLine.Say(DealerText.Fill("reallife_dealer_event_terms", "cost", Money(cost), "days",
                        DealerCatalogue.EventDays[kind].ToString(Inv), "rest", DealerCatalogue.EventRest.ToString(Inv))),
                    UiFactLine.Say(DealerText.T("reallife_dealer_event_campaigns"))
                };
                _m.Left = new[] { new UiFactCardModel { Title = DealerText.T("reallife_ibox_sec_terms"), Heading = true, Lines = terms.ToArray() } };
                var where = new List<UiFactLine>(5);
                var actions = new List<UiActionModel>(1);
                if (_s.Yards.Count == 0)
                {
                    where.Add(UiFactLine.Say(DealerText.T("reallife_dealer_refuse_off"), UiRole.WarnText));
                    _m.Right = new[] { new UiFactCardModel { Title = DealerText.T("reallife_ibox_sec_where"), Heading = true, Lines = where.ToArray() } };
                    return;
                }
                int at = 0;
                for (int i = 0; i < _s.Yards.Count; i++)
                {
                    at = _s.Yards[i].Place == _eventYard ? i : at;
                }
                DealerYard yard = _s.Yards[at];
                _eventYard = yard.Place;
                string label = DealerText.T("reallife_dealer_event_at", "name", yard.Name);
                if (_s.Yards.Count > 1)
                {
                    where.Add(_runs.Step(DealerText.T("reallife_ibox_sec_where"), label, null, at > 0, at < _s.Yards.Count - 1,
                        () => Show(() => _eventYard = _s.Yards[at - 1].Place), () => Show(() => _eventYard = _s.Yards[at + 1].Place)));
                }
                else
                {
                    where.Add(UiFactLine.Say(label));
                }
                int feature = 0;
                if (kind == DealerCatalogue.FeaturedKind)
                {
                    _kinds.Clear();
                    IReadOnlyList<MarketModel> models = _d.Models;
                    for (int k = 1; k <= 6; k++)
                    {
                        for (int m = 0; m < models.Count; m++)
                        {
                            if (models[m].Kind == k)
                            {
                                _kinds.Add(k);
                                break;
                            }
                        }
                    }
                    if (_kinds.Count > 0)
                    {
                        int k = Math.Max(0, _kinds.IndexOf(_eventFeature));
                        feature = _kinds[k];
                        _eventFeature = feature;
                        var kinds = new List<int>(_kinds);
                        where.Add(_runs.Step(DealerText.T("reallife_ui_dealer_featured"),
                            DealerText.T("reallife_dealer_event_kind", "kind", DealerText.CarKind(feature)), null, k > 0, k < kinds.Count - 1,
                            () => Show(() => _eventFeature = kinds[k - 1]), () => Show(() => _eventFeature = kinds[k + 1])));
                    }
                }
                EventRefusal no = DealerRules.CanBook(yard, kind, _day);
                if (no == EventRefusal.Running)
                {
                    where.Add(UiFactLine.Say(DealerText.Fill("reallife_dealer_event_busy", "event", DealerText.Event(yard.Event, yard.Feature), "day",
                        FinanceText.Day(yard.EventUntil)), UiRole.WarnText));
                }
                else if (no == EventRefusal.Rested)
                {
                    where.Add(UiFactLine.Say(DealerText.T("reallife_dealer_event_resting", "day", FinanceText.Day(DealerRules.RestUntil(yard, kind) + 1)), UiRole.WarnText));
                }
                string place = yard.Place;
                int chosen = feature;
                bool can = no == EventRefusal.None && (kind != DealerCatalogue.FeaturedKind || feature > 0);
                actions.Add(_runs.Button(DealerText.T("reallife_dealer_button_event"), UiButtonKind.Primary, UiIcon.Megaphone, UiAction.Alt, () => Show(() => Confirm("reallife_dealer_q_event_title",
                    DealerText.Fill("reallife_dealer_q_event", "event", DealerText.Event(kind, chosen), "name", yard.Name, "cost", Money(cost), "days", DealerCatalogue.EventDays[kind].ToString(Inv)),
                    () => _d.BookEvent(place, kind, chosen), false)), !can));
                _m.Right = new[] { new UiFactCardModel { Title = DealerText.T("reallife_ibox_sec_where"), Heading = true, Lines = where.ToArray(), Actions = actions.ToArray() } };
            }

            // ------------------------------------------------------------------------------------------------- reports

            private static readonly string[] ReportKeys =
            {
                "reallife_dealer_report_sold", "reallife_dealer_report_revenue", "reallife_dealer_report_profit", "reallife_dealer_report_age", "reallife_dealer_report_visitors",
                "reallife_dealer_report_conversion", "reallife_dealer_report_arrears"
            };

            private readonly DealerBook _rWeek = new DealerBook(), _rAll = new DealerBook(), _rNow = new DealerBook(), _rDay = new DealerBook(), _rScratch = new DealerBook();

            public void Reports()
            {
                RealLifeOverhaul.Economy.Dealership.ReportKind kind = (RealLifeOverhaul.Economy.Dealership.ReportKind)Math.Max(0, Math.Min(ReportKeys.Length - 1, _v.Report));
                string place = _v.ReportPlace;
                DealerYard yard = place != null ? _s.Yard(place) : null;
                if (yard == null)
                {
                    place = null;
                    _v.ReportPlace = null;
                }
                int days = DealerCatalogue.ReportDays;
                var values = new long[days];
                var have = new bool[days];
                var average = new long[days];
                int first = DealerReport.Series(_s, place, kind, _day, days, values, have, _rScratch);
                DealerReport.Average(values, have, days, average);
                bool money = DealerReport.Money(kind);
                // Only the days that exist are plotted: the days before the first kept have no point, and nothing is drawn as zero.
                int from = Math.Max(0, Math.Min(days, first));
                var pts = new List<string>(days);
                var line = new List<float>(days);
                var avg = new List<float>(days);
                for (int i = from; i < days; i++)
                {
                    if (!have[i])
                    {
                        continue;
                    }
                    pts.Add(DealerText.T("reallife_ui_dealer_chart_day", "day", (_day - (days - 1 - i)).ToString(Inv)));
                    line.Add(money ? values[i] / 1f : values[i]);
                    avg.Add(average[i]);
                }
                var model = new UiChartModel
                {
                    Kind = UiChartKind.Line, Title = DealerText.T(ReportKeys[(int)kind]), Points = pts.ToArray(),
                    Series = new[]
                    {
                        new UiSeries { Label = DealerText.T(ReportKeys[(int)kind]), Values = line.ToArray(), Tint = UiRole.None },
                        new UiSeries { Label = DealerText.T("reallife_ui_dealer_report_average", "days", DealerCatalogue.AverageDays.ToString(Inv)), Values = avg.ToArray(), Tint = UiRole.None }
                    },
                    Format = v => FormatFigure(v, kind, money), AxisFormat = v => FormatFigure(v, kind, money),
                    EmptyTitle = DealerText.T("reallife_ui_dealer_chart_empty"), EmptyBody = DealerText.T("reallife_dealer_report_none")
                };
                if (line.Count < 2)
                {
                    model.Points = new string[0];
                    model.Series = new UiSeries[0];
                }
                _m.Shape = EstateShape.Chart;
                _m.HeadIcon = UiIcon.Chart;
                _m.HeadTitle = DealerText.T(ReportKeys[(int)kind]);
                _m.HeadSub = DealerText.Fill("reallife_ui_dealer_report_sub", "place", yard != null ? yard.Name
                    : DealerText.T("reallife_dealer_report_all"), "days", days.ToString(Inv), "average", DealerCatalogue.AverageDays.ToString(Inv));
                _m.Chart = model;
                var kinds = new UiActionModel[ReportKeys.Length];
                for (int i = 0; i < kinds.Length; i++)
                {
                    int k = i;
                    kinds[i] = _runs.Button(DealerText.T(ReportKeys[i]), UiButtonKind.Chip, UiIcon.None, UiAction.Count, () => Show(() => _v.Report = k));
                }
                _m.ChartKinds = kinds;
                _m.ChartKind = (int)kind;
                var places = new UiActionModel[_s.Yards.Count > 1 ? _s.Yards.Count + 1 : 0];
                int placeAt = 0;
                for (int i = 0; i < places.Length; i++)
                {
                    string target = i == 0 ? null : _s.Yards[i - 1].Place;
                    placeAt = target == place ? i : placeAt;
                    places[i] = _runs.Button(i == 0 ? DealerText.T("reallife_dealer_report_all") : _s.Yards[i - 1].Name, UiButtonKind.Chip,
                        UiIcon.None, UiAction.Count, () => Show(() => _v.ReportPlace = target));
                }
                _m.ChartPlaces = places;
                _m.ChartPlace = placeAt;
                int week = DealerReport.Sum(_s, place, _day - DealerCatalogue.WeekBooks + 1, _day, _day, _rWeek, _rDay);
                int kept = DealerReport.Sum(_s, place, _day - DealerCatalogue.ReportDays + 1, _day, _day, _rAll, _rDay);
                DealerReport.Day(_s, place, _day, _day, _rNow);
                _m.Side = new UiFactCardModel
                {
                    Title = DealerText.T("reallife_ui_dealer_sums"), Heading = true,
                    Lines = new[]
                    {
                        UiFactLine.Section(DealerText.T("reallife_ibox_sec_this_week")), UiFactLine.Say(Sums("reallife_dealer_reports_week", _rWeek, week)),
                        UiFactLine.Section(DealerText.T("reallife_ibox_sec_kept")), UiFactLine.Say(Sums("reallife_dealer_reports_kept", _rAll, kept)),
                        UiFactLine.Section(DealerText.T("reallife_ibox_sec_now")),
                        UiFactLine.Say(DealerText.Fill("reallife_dealer_reports_now", "cars", _rNow.Cars.ToString(Inv), "age",
                            DealerReport.Value(_rNow, RealLifeOverhaul.Economy.Dealership.ReportKind.StockAge).ToString(Inv),
                            "arrears", Money(_rNow.Arrears), "late", _rNow.Late.ToString(Inv)))
                    }
                };
            }

            private string FormatFigure(float v, RealLifeOverhaul.Economy.Dealership.ReportKind kind, bool money)
            {
                return money ? Money((int)Math.Max(int.MinValue / 2, Math.Min(int.MaxValue / 2, v)))
                    : kind == RealLifeOverhaul.Economy.Dealership.ReportKind.Conversion ? (v / 10f).ToString("0.#", Inv) + "%" : ((int)v).ToString(Inv);
            }

            private string Sums(string key, DealerBook b, int days)
            {
                long conversion = DealerReport.Value(b, RealLifeOverhaul.Economy.Dealership.ReportKind.Conversion);
                return DealerText.Fill(key, "days", days.ToString(Inv), "sold", b.Sold.ToString(Inv), "revenue", Money(b.Revenue), "margin",
                    Money(b.Margin), "extras", Money(b.Extras), "spent", Money(b.Spent),
                    "visitors", b.Visitors.ToString(Inv), "conversion", (conversion / 10.0).ToString("0.#", Inv) + "%");
            }
        }
    }
}
