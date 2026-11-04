using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class DealerLive
    {
        private sealed partial class Page
        {
            // A car's glyph by its body: the work vehicles wear the truck, the rest the car, so a lot of them is not one repeated picture.
            private static UiIcon CarGlyph(string type)
            {
                string t = (type ?? string.Empty).ToLowerInvariant();
                return t.Contains("truck") || t.Contains("pickup") || t.Contains("van") || t.Contains("bus") || t.Contains("camper") ? UiIcon.Truck : UiIcon.Car;
            }

            private string State(DealerYard yard, StockCar car)
            {
                if (car.ShopUntil > _now)
                {
                    return DealerText.T("reallife_dealer_state_workshop", "when", DealerText.When(car.ShopUntil));
                }
                if (car.OutUntil > _now)
                {
                    return DealerText.T("reallife_dealer_state_driving");
                }
                return DealerRules.HasDeal(yard, car.Id) ? DealerText.T("reallife_dealer_state_offer") : DealerText.T("reallife_dealer_state_on_show");
            }

            // ------------------------------------------------------------------------------------------------- the stock

            // Every car of every lot as a card with its picture, its price and its days on the lot; the chosen car's money and what it can be given beside them.
            public void Stock()
            {
                int pickedId = -1;
                string pick = Picked(_v.Pick, "car:");
                if (pick != null)
                {
                    int.TryParse(pick, out pickedId);
                }
                var cards = new List<UiCatalogueCard>(16);
                var keys = new List<string>(16);
                int chosen = -1, cars = 0, free = 0, bays = 0, worthAll = 0;
                StockCar picked = null;
                DealerYard pickedYard = null;
                // Every car in one list, so the filter chips can say how many each holds and the order can be changed.
                var all = new List<KeyValuePair<DealerYard, StockCar>>(16);
                int[] held = new int[4];
                for (int y = 0; y < _s.Yards.Count; y++)
                {
                    DealerYard yard = _s.Yards[y];
                    free += _s.Room(yard);
                    bays += yard.Spots.Count;
                    for (int c = 0; c < yard.Cars.Count; c++)
                    {
                        StockCar car = yard.Cars[c];
                        all.Add(new KeyValuePair<DealerYard, StockCar>(yard, car));
                        held[0]++;
                        held[car.Source == CarSource.Wholesale ? 1 : 2]++;
                        held[3] += car.ShopUntil > _now ? 1 : 0;
                    }
                }
                int filter = Math.Max(0, Math.Min(3, _v.Filter)), sort = Math.Max(0, Math.Min(2, _v.Sort));
                string query = (_v.Query ?? string.Empty).Trim();
                all.Sort((p, q) => sort == 1 ? q.Value.Asking.CompareTo(p.Value.Asking) : sort == 2 ? p.Value.Bought.CompareTo(q.Value.Bought) : p.Value.Spot.CompareTo(q.Value.Spot));
                for (int k = 0; k < all.Count; k++)
                {
                    DealerYard yard = all[k].Key;
                    StockCar car = all[k].Value;
                    {
                        bool shows = filter == 0 || (filter == 1 && car.Source == CarSource.Wholesale) || (filter == 2 && car.Source != CarSource.Wholesale) || (filter == 3 && car.ShopUntil > _now);
                        shows &= query.Length == 0 || DealerGame.CarName(car.Type).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
                        int worth = DealerDirector.Worth(car, _level), days = _day - car.Bought;
                        cars++;
                        worthAll += worth;
                        if (car.Id == pickedId && shows)
                        {
                            chosen = cards.Count;
                            picked = car;
                            pickedYard = yard;
                        }
                        if (!shows)
                        {
                            continue;
                        }
                        float aspect;
                        string picture = DealerLive.PictureOf(car.Type, out aspect);
                        cards.Add(new UiCatalogueCard
                        {
                            Picture = picture, Aspect = aspect, Icon = CarGlyph(car.Type), Name = DealerGame.CarName(car.Type), Figure = Money(car.Asking),
                            State = days >= 1 ? DealerText.T("reallife_ui_dealer_days_chip", "days", days.ToString(Inv)) : DealerText.T("reallife_ui_dealer_today"),
                            StateTone = days >= 30 ? UiRole.Danger : days >= 14 ? UiRole.Warn : UiRole.Notice
                        });
                        keys.Add("car:" + car.Id);
                    }
                }
                for (int i = 0; filter == 0 && i < _s.Deliveries.Count; i++)
                {
                    DealerDelivery q = _s.Deliveries[i];
                    DealerYard yard = _s.Yard(q.Place);
                    float aspect;
                    string picture = DealerLive.PictureOf(q.Type, out aspect);
                    cards.Add(new UiCatalogueCard
                    {
                        Picture = picture, Aspect = aspect, Icon = UiIcon.Truck, Name = DealerGame.CarName(q.Type), Figure = Money(q.Cost),
                        State = DealerText.T(q.Told ? "reallife_dealer_state_way_waiting" : "reallife_dealer_state_way", "when",
                            DealerText.When(q.Due)), StateTone = q.Told ? UiRole.Danger : UiRole.Warn, Disabled = true
                    });
                    keys.Add(null);
                }
                if (picked == null)
                {
                    _v.Pick = null;
                }
                var m = new UiCatalogueModel
                {
                    Cards = cards.ToArray(), CardCount = cards.Count, Chosen = chosen,
                    EmptyTitle = DealerText.T(_s.Yards.Count == 0 ? "reallife_dealer_row_no_dealership" : "reallife_dealer_empty_stock"),
                    EmptyBody = DealerText.T("reallife_ui_dealer_empty_stock_body"), EmptyIcon = UiIcon.Car
                };
                var runs = new List<int>(8);
                if (picked != null)
                {
                    Detail(m, runs, pickedYard, picked);
                }
                else
                {
                    m.DetailKicker = DealerText.T("reallife_dealer_app");
                    m.DetailLabels = new[] { DealerText.T("reallife_dealer_sum_waiting"), DealerText.T("reallife_dealer_sum_cars"), DealerText.T("reallife_dealer_sum_worth") };
                    m.DetailValues = new[] { _d.Waiting().ToString(Inv), DealerText.T("reallife_dealer_sum_cars_value", "cars", cars.ToString(Inv), "free", free.ToString(Inv)), Money(worthAll) };
                    m.DetailNote = DealerText.T("reallife_ui_dealer_pick_car");
                }
                string[] filterKeys = { "reallife_ui_dealer_filter_all", "reallife_ui_dealer_filter_new", "reallife_ui_dealer_filter_used", "reallife_ui_dealer_filter_shop" };
                string[] sortKeys = { "reallife_ui_dealer_sort_bay", "reallife_ui_dealer_sort_price", "reallife_ui_dealer_sort_days" };
                _m.FilterChips = new UiActionModel[cars > 0 ? filterKeys.Length : 0];
                for (int i = 0; i < _m.FilterChips.Length; i++)
                {
                    int at = i;
                    _m.FilterChips[i] = _runs.Button(DealerText.T(filterKeys[i], "count", held[i].ToString(Inv)), UiButtonKind.Chip, UiIcon.None, UiAction.Count, () => Show(() => _v.Filter = at));
                }
                _m.FilterAt = filter;
                _m.SortChips = new UiActionModel[cars > 0 ? sortKeys.Length : 0];
                for (int i = 0; i < _m.SortChips.Length; i++)
                {
                    int at = i;
                    _m.SortChips[i] = _runs.Button(DealerText.T(sortKeys[i]), UiButtonKind.Chip, UiIcon.None, UiAction.Count, () => Show(() => _v.Sort = at));
                }
                _m.SortAt = sort;
                _m.SearchHint = cars > 0 ? DealerText.T("reallife_ui_dealer_search") : null;
                _m.SearchCount = query.Length > 0 ? cards.Count.ToString(Inv) : null;
                _m.Query = _v.Query;
                _m.Shape = EstateShape.Catalogue;
                _m.Catalogue = m;
                _m.CatalogueRuns = runs.ToArray();
                _m.RowKeys = keys.ToArray();
                _m.HeadSub = DealerText.T("reallife_ui_dealer_on_lot", "cars", cars.ToString(Inv), "bays", bays.ToString(Inv));
                _m.FigLabels = new[] { DealerText.T("reallife_ui_dealer_stock_worth") };
                _m.FigValues = new[] { Money(worthAll) };
            }

            // The chosen car: what it cost and is worth, what it asks and would make, how it stands against the other dealers and its buyers, and what can be done with it.
            private void Detail(UiCatalogueModel m, List<int> runs, DealerYard yard, StockCar car)
            {
                int worth = DealerDirector.Worth(car, _level), days = _day - car.Bought;
                int step = DealerRules.AskStepOf(worth, car.Asking);
                m.DetailKicker = car.Spot >= 0 ? DealerText.Fill("reallife_ui_dealer_kicker_bay", "name", yard.Name, "bay", (car.Spot + 1).ToString(Inv), "state", State(yard, car))
                    : DealerText.T("reallife_ui_dealer_kicker_car", "name", yard.Name, "state", State(yard, car));
                int trade = DealerRules.TradePrice(worth), margin = car.Asking - car.Cost;
                var labels = new List<string>(8);
                var values = new List<string>(8);
                labels.Add(DealerText.T("reallife_ui_dealer_l_cost"));
                values.Add(Money(car.Cost));
                labels.Add(DealerText.T("reallife_ibox_worth"));
                values.Add(Money(worth));
                labels.Add(DealerText.T("reallife_ibox_asking"));
                values.Add(Money(car.Asking));
                labels.Add(DealerText.T("reallife_ui_dealer_l_margin"));
                values.Add((margin >= 0 ? "+" : "-") + Money(Math.Abs(margin)));
                labels.Add(DealerText.T("reallife_ui_dealer_l_condition"));
                values.Add(DealerText.Condition(car.Condition));
                labels.Add(DealerText.T("reallife_ui_dealer_l_km"));
                values.Add(DealerText.Km(car.Km));
                labels.Add(DealerText.T("reallife_ui_dealer_l_source"));
                values.Add(DealerText.Source(car.Source) + ", " + DealerText.T("reallife_ui_dealer_days_on_lot", "days", days.ToString(Inv)));
                if (car.Known != 0)
                {
                    labels.Add(DealerText.T("reallife_dealer_car_faults"));
                    values.Add(DealerText.Faults(car.Known));
                }
                m.DetailLabels = labels.ToArray();
                m.DetailValues = values.ToArray();
                // What the other dealers ask for the same model, and who has looked at this car.
                var note = new List<string>(3);
                int rival;
                int cheapest = RivalRules.Cheapest(_s, car.Type, out rival);
                if (cheapest > 0 && rival >= 0)
                {
                    note.Add(DealerText.T("reallife_ui_dealer_rival_asks", "name", DealerCatalogue.RivalNames[rival % DealerCatalogue.RivalNames.Length], "price", Money(cheapest)));
                }
                else
                {
                    note.Add(DealerText.T("reallife_ui_dealer_no_rival"));
                }
                note.Add(DealerText.T("reallife_ui_dealer_views", "today", car.ViewsToday.ToString(Inv), "yesterday", car.ViewsLast.ToString(Inv)));
                for (int d = 0; d < yard.Deals.Count; d++)
                {
                    if (yard.Deals[d].Car == car.Id && yard.Deals[d].Looks > 0)
                    {
                        note.Add(DealerText.Fill(yard.Deals[d].Looks > 1 ? "reallife_ui_dealer_looked_times" : "reallife_ui_dealer_looked_once",
                            "name", yard.Deals[d].Customer, "times", yard.Deals[d].Looks.ToString(Inv),
                            "offer", Money(DealerRules.Price(yard, yard.Deals[d], car))));
                        break;
                    }
                }
                m.DetailNote = string.Join(" ", note.ToArray());
                var actions = new List<UiActionModel>(7);
                int id = car.Id;
                bool away = car.ShopUntil > _now || car.OutUntil > _now;
                // The asking price steps down and up through the catalogue's steps; the label says where it goes.
                int less = Math.Max(0, step - 1), more = Math.Min(DealerRules.AskSteps - 1, step + 1);
                actions.Add(Act(runs, DealerText.T("reallife_ui_dealer_ask_to", "price", Money(DealerRules.AskAt(worth, less))),
                    UiButtonKind.Secondary, UiIcon.Minus, UiAction.Count, () => Show(() => _d.Ask(id, DealerRules.AskAt(worth, less))), step <= 0));
                actions.Add(Act(runs, DealerText.T("reallife_ui_dealer_ask_to", "price", Money(DealerRules.AskAt(worth, more))),
                    UiButtonKind.Secondary, UiIcon.Plus, UiAction.Count, () => Show(() => _d.Ask(id, DealerRules.AskAt(worth, more))), step >= DealerRules.AskSteps - 1));
                if (yard == _d.YardInside() && car.Spot >= 0 && car.OutUntil <= ShowroomGame.Hour)
                {
                    actions.Add(Act(runs, DealerText.T("reallife_showroom_button_look"), UiButtonKind.Secondary, UiIcon.Eye, UiAction.Count, () => Show(() => ShowroomView.Instance?.Look(id)), false));
                    actions.Add(Act(runs, DealerText.T("reallife_showroom_button_bay"), UiButtonKind.Secondary, UiIcon.MapPin, UiAction.Count,
                        () => Show(() => ShowroomView.Instance?.Place(id)), false));
                }
                if (car.Condition < 1000 || car.Known != 0)
                {
                    int prepare = _d.PrepareCost(car);
                    actions.Add(Act(runs, DealerText.T("reallife_dealer_button_prepare"), UiButtonKind.Secondary, UiIcon.Wrench, UiAction.Option,
                        () => Show(() => Confirm("reallife_dealer_q_prepare_title",
                        DealerText.T("reallife_dealer_q_prepare", "car", DealerGame.CarName(car.Type), "cost", Money(prepare)), () => _d.Prepare(id), false)), away));
                }
                if (car.ShopUntil <= _now && DealerRules.WorkshopWorth(car))
                {
                    int cost = _d.WorkshopCost(car), hours = DealerRules.WorkshopHours(car.Condition + DealerRules.FaultCondition(car.Faults));
                    actions.Add(Act(runs, DealerText.T("reallife_dealer_button_workshop"), UiButtonKind.Secondary, UiIcon.Gear, UiAction.Count,
                        () => Show(() => Confirm("reallife_dealer_q_workshop_title",
                        DealerText.Fill("reallife_dealer_q_workshop", "car", DealerGame.CarName(car.Type), "cost", Money(cost), "hours", hours.ToString(Inv)), () => _d.Workshop(id), false)), false));
                }
                actions.Add(Act(runs, DealerText.T("reallife_dealer_button_trade"), UiButtonKind.Destructive, UiIcon.Coin, UiAction.Count, () => Show(() => Confirm("reallife_dealer_q_trade_title",
                    DealerText.T("reallife_dealer_q_trade", "car", DealerGame.CarName(car.Type), "price", Money(trade)), () =>
                    {
                        if (_d.SellToTrade(id))
                        {
                            _v.Pick = null;
                        }
                    }, true)), away));
                m.Actions = actions.ToArray();
            }

            // A button of the catalogue's detail: its run is kept in the list the screen presses by the button's number.
            private UiActionModel Act(List<int> runs, string label, UiButtonKind kind, UiIcon icon, UiAction key, Action run, bool disabled)
            {
                int index = runs.Count;
                runs.Add(disabled ? -1 : _runs.Do(run));
                return new UiActionModel { Label = label, Kind = kind, Icon = icon, Key = key, Command = UiCatalogue.First + UiCatalogue.Action, Arg = index, Disabled = disabled };
            }

            // ------------------------------------------------------------------------------------------------- the showrooms

            public void Lots()
            {
                string place = Picked(_v.Pick, "lot:");
                DealerYard picked = place != null ? _s.Yard(place) : null;
                if (picked != null)
                {
                    YardPage(picked);
                    return;
                }
                _v.Pick = null;
                var rows = new List<UiDataRow>(4);
                var keys = new List<string>(4);
                for (int y = 0; y < _s.Yards.Count; y++)
                {
                    DealerYard yard = _s.Yards[y];
                    BuildingRegistration reg = _d.RegOf(yard);
                    int staff = reg != null ? DealerGame.Salespeople(reg) : 0;
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { yard.Name, yard.Laid
                            ? DealerText.T("reallife_dealer_lot_count", "cars", yard.Cars.Count.ToString(Inv), "places",
                            yard.Spots.Count.ToString(Inv)) : DealerText.T("reallife_dealer_lot_unlaid"),
                            staff.ToString(Inv), yard.Today.Visitors.ToString(Inv), yard.Today.Sold.ToString(Inv), Money(yard.Today.Takings), yard.Deals.Count.ToString(Inv) },
                        Values = new[] { (int)UiIcon.Store, 0, 0, 0, 0, 0, 0 },
                            Tones = new[] { UiRole.None, UiRole.None, staff == 0 ? UiRole.DangerText : UiRole.None, UiRole.None, UiRole.None,
                            UiRole.None, yard.Deals.Count > 0 ? UiRole.WarnText : UiRole.None },
                        Subs = new[] { DealerText.Step(DealerRules.Step(yard.Rep)), null, null, null, null, null, null }
                    });
                    keys.Add("lot:" + yard.Place);
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_showroom"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_lot"), Kind = UiCellKind.Text, Width = 168f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_staff"), Kind = UiCellKind.Figure, Width = 72f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_visitors"), Kind = UiCellKind.Figure, Width = 88f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_sold"), Kind = UiCellKind.Figure, Width = 72f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_takings"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = DealerText.T("reallife_ui_dealer_col_offers"), Kind = UiCellKind.Figure, Width = 80f }
                };
                TableShape(UiIcon.Store, DealerText.T("reallife_dealer_tab_lots"), DealerText.T("reallife_ui_dealer_lots_sub", "count", _s.Yards.Count.ToString(Inv)), cols, rows, keys, null,
                    DealerText.T("reallife_dealer_row_no_dealership"), DealerText.T("reallife_dealer_lots_about"));
                int carsAll = 0, takingsAll = 0, soldAll = 0, dealsAll = 0;
                for (int y = 0; y < _s.Yards.Count; y++)
                {
                    carsAll += _s.Yards[y].Cars.Count;
                    takingsAll += _s.Yards[y].Today.Takings;
                    soldAll += _s.Yards[y].Today.Sold;
                    dealsAll += _s.Yards[y].Deals.Count;
                }
                if (_s.Yards.Count > 0)
                {
                    _m.FigLabels = new[] { DealerText.T("reallife_ui_dealer_fig_cars"), DealerText.T("reallife_ui_dealer_fig_sold_today"),
                        DealerText.T("reallife_ui_dealer_fig_takings_today"), DealerText.T("reallife_ui_dealer_fig_offers") };
                    _m.FigValues = new[] { carsAll.ToString(Inv), soldAll.ToString(Inv), Money(takingsAll), dealsAll.ToString(Inv) };
                    _m.Lower = Trend(RealLifeOverhaul.Economy.Dealership.ReportKind.Revenue, null, "reallife_dealer_report_revenue");
                }
            }

            private readonly DealerBook _weekSum = new DealerBook(), _weekDay = new DealerBook();

            private void YardPage(DealerYard y)
            {
                DealerReport.Sum(_s, y.Place, _day - DealerCatalogue.WeekBooks + 1, _day - 1, _day, _weekSum, _weekDay);
                DealerBook w = _weekSum;
                BuildingRegistration reg = _d.RegOf(y);
                int step = DealerRules.Step(y.Rep);
                string place = y.Place;
                DetailHead(UiIcon.Store, y.Name, DealerText.T("reallife_ui_dealer_rep_n", "rep", y.Rep.ToString(Inv)), DealerText.Step(step), UiRole.Notice);
                // The showroom's day as figures, its week as rows, and the lot, the visitors looked after and the way to the next step as meters.
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = DealerText.T("reallife_ui_dealer_fig_visitors_today"), Value = y.Today.Visitors.ToString(Inv) },
                    new UiFigureModel { Label = DealerText.T("reallife_ui_dealer_fig_looked_after"), Value = y.Today.Served.ToString(Inv) },
                    new UiFigureModel { Label = DealerText.T("reallife_ui_dealer_fig_offers_today"), Value = y.Today.Offers.ToString(Inv) },
                    new UiFigureModel { Label = DealerText.T("reallife_ui_dealer_fig_sold_today"), Value = y.Today.Sold.ToString(Inv) }
                };
                var shareLabels = new List<string>(3);
                var shareValues = new List<int>(3);
                if (y.Laid)
                {
                    shareLabels.Add(DealerText.T("reallife_ibox_lot"));
                    shareValues.Add(y.Spots.Count > 0 ? y.Cars.Count * 100 / y.Spots.Count : 0);
                }
                shareLabels.Add(DealerText.T("reallife_ui_dealer_sh_looked_after"));
                shareValues.Add(y.Today.Visitors > 0 ? Math.Min(100, y.Today.Served * 100 / y.Today.Visitors) : 0);
                if (step + 1 < DealerCatalogue.RepSteps.Length)
                {
                    int from = DealerCatalogue.RepSteps[step], to = DealerCatalogue.RepSteps[step + 1];
                    shareLabels.Add(DealerText.T("reallife_ui_dealer_sh_next_step"));
                    shareValues.Add(Math.Max(0, Math.Min(100, (y.Rep - from) * 100 / Math.Max(1, to - from))));
                }
                _m.ShareTitle = DealerText.T("reallife_ibox_sec_now");
                _m.ShareLabels = shareLabels.ToArray();
                _m.ShareValues = shareValues.ToArray();
                var now = new List<UiFactLine>(8);
                if (reg != null && DealerGame.Salespeople(reg) == 0)
                {
                    now.Add(UiFactLine.Say(DealerText.T("reallife_dealer_yard_no_staff"), UiRole.DangerText));
                }
                if (!y.Laid)
                {
                    now.Add(UiFactLine.Say(DealerText.T("reallife_dealer_yard_unlaid"), UiRole.WarnText));
                }
                if (y.EventOn(_day))
                {
                    now.Add(UiFactLine.Say(DealerText.Fill("reallife_dealer_yard_event", "event", DealerText.Event(y.Event, y.Feature), "day", FinanceText.Day(y.EventUntil)), UiRole.OkText));
                }
                now.Add(UiFactLine.Row(DealerText.T("reallife_ui_dealer_fig_cars_sold"), (w.Sold + y.Today.Sold).ToString(Inv)));
                now.Add(UiFactLine.Row(DealerText.T("reallife_dealer_report_revenue"), Money(w.Takings + y.Today.Takings)));
                now.Add(UiFactLine.Row(DealerText.T("reallife_ui_dealer_fig_bought"), Money(w.Bought + y.Today.Bought)));
                now.Add(UiFactLine.Row(DealerText.T("reallife_ui_dealer_fig_margin"), Money(w.Margin + y.Today.Margin)));
                now.Add(UiFactLine.Row(DealerText.T("reallife_ui_dealer_fig_claims"), Money(w.Claims + y.Today.Claims)));
                var terms = new List<UiFactLine>(8);
                bool fix = y.Fixed;
                terms.Add(_runs.Switch(DealerText.T("reallife_ui_dealer_sw_fixed"), fix, on => Show(() => _d.FixedPrices(place, on))));
                if (!fix)
                {
                    int steps = (DealerCatalogue.StaffFloorMost - DealerCatalogue.StaffFloorLeast) / DealerCatalogue.StaffFloorStep + 1;
                    int at = (y.StaffFloor - DealerCatalogue.StaffFloorLeast) / DealerCatalogue.StaffFloorStep;
                    terms.Add(_runs.Step(DealerText.T("reallife_ui_dealer_staff_floor"), DealerText.Share(y.StaffFloor), DealerText.T("reallife_ui_dealer_staff_floor_what"), at > 0, at < steps - 1,
                        () => Show(() => _d.Floor(place, DealerCatalogue.StaffFloorLeast + (at - 1) * DealerCatalogue.StaffFloorStep)),
                            () => Show(() => _d.Floor(place, DealerCatalogue.StaffFloorLeast + (at + 1) * DealerCatalogue.StaffFloorStep))));
                }
                else
                {
                    terms.Add(UiFactLine.Say(DealerText.T("reallife_dealer_yard_fixed_on"), UiRole.OkText));
                }
                terms.Add(_runs.Switch(DealerText.T("reallife_ui_dealer_sw_finance"), y.Finance, on => Show(() => _d.OfferFinance(place, on))));
                terms.Add(_runs.Switch(DealerText.T("reallife_ui_dealer_sw_warranty"), y.Warranty, on => Show(() => _d.OfferWarranty(place, on))));
                terms.Add(_runs.Switch(DealerText.T("reallife_ui_dealer_sw_extras"), y.Extras, on => Show(() => _d.OfferExtras(place, on))));
                var actions = new List<UiActionModel>(1);
                if (reg != null && DealerGame.Inside(reg))
                {
                    actions.Add(_runs.Button(DealerText.T("reallife_dealer_button_relay"), UiButtonKind.Secondary, UiIcon.Grid, UiAction.Count,
                        () => Show(() => Confirm("reallife_dealer_q_relay_title", DealerText.T("reallife_dealer_q_relay"),
                        () => _d.Relay(place), false))));
                }
                _m.Left = new[] { new UiFactCardModel { Title = DealerText.T("reallife_ui_dealer_terms"), Heading = true, Lines = terms.ToArray(), Actions = actions.ToArray() } };
                _m.Right = new[] { new UiFactCardModel { Title = DealerText.T("reallife_ibox_sec_this_week"), Heading = true, Lines = now.ToArray() } };
                _m.Lower = Trend(RealLifeOverhaul.Economy.Dealership.ReportKind.Revenue, y.Place, "reallife_dealer_report_revenue");
            }
        }
    }
}
