using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Ui;

// The Car Showroom's samples: every page reads the one sample world (UiSamples.DealerWorld.cs), a real DealerState, through the functions the live pages use. A head is
// DealerFigures of the same state the rows are drawn from, a chart is DealerReport's series, so nothing a live page works out is typed here.
internal static partial class UiSamples
{
    private static readonly CultureInfo InvD = CultureInfo.InvariantCulture;

    private static string Usd(long amount) => (amount < 0 ? "-$" : "$") + Math.Abs(amount).ToString("N0", InvD);

    private static readonly Dictionary<string, int> StaffOf = new Dictionary<string, int> { { "10 Pioneer Way", 3 }, { "27 Eastside Road", 1 } };

    private static int NowHour => Today * 24 + TodayHour;

    private static string DHours(UiWords t, int hours) => hours <= 1 ? W(t, "reallife_dealer_hours_one", "under an hour") : Fx(W(t, "reallife_dealer_hours", "{hours} hours"), "hours", hours.ToString(InvD));

    private static string DKm(UiWords t, int km) => Fx(W(t, "reallife_dealer_km", "{km} km"), "km", km.ToString("N0", InvD));

    private static string DCondition(UiWords t, int thousandths) => Fx(W(t, "reallife_dealer_condition_value", "{percent}%"), "percent", ((thousandths + 5) / 10).ToString(InvD));

    private static string DStep(UiWords t, int step) => W(t, "reallife_dealer_step_" + step, "step " + step);

    private static string DKind(UiWords t, ListingKind kind) => W(t, "reallife_dealer_kind_" + (kind == ListingKind.Wholesale ? "wholesale" : kind == ListingKind.Used ? "used" : "auction"), kind.ToString());

    private static string DSource(UiWords t, CarSource source) => W(t, "reallife_dealer_kind_" + (source == CarSource.Wholesale ? "wholesale" : source == CarSource.Used ? "used" : source == CarSource.Auction ? "auction" : "trade"), source.ToString());

    private static string DTier(UiWords t, int tier) => tier < 0 ? W(t, "reallife_dealer_tier_none", "No warranty") : W(t, "reallife_dealer_tier_" + (tier == 0 ? "basic" : tier == 1 ? "extended" : "complete"), "Warranty");

    private static string DProfile(UiWords t, int profile) => W(t, "reallife_dealer_profile_" + (profile == 0 ? "bargain" : profile == 1 ? "fair" : "keen"), "a buyer");

    private static string RivalName(int rival) => DealerCatalogue.RivalNames[Math.Max(0, rival) % DealerCatalogue.RivalNames.Length];

    private static CarKind KindOfCar(string type) => ModelOf(type).Kind;

    private static int WorthOfCar(StockCar c) => CarWorths[c];

    // A listing's worth: what a model usually sells for, at its kilometres and condition. It does not move with the bidding.
    private static int WorthOfListing(DealerListing l) => DealerRules.Worth(ModelOf(l.Type).Sold * 1.4f, PriceLevel, l.Km, l.Condition);

    private static UiIcon CarGlyph(string type) => CarClass.Glyph(KindOfCar(type));

    private static UiRole CarTint(string type, int price) => CarClass.Tint(KindOfCar(type), price);

    private static EstateModel DealerBase(UiWords t, int tab, DealerState s)
    {
        UiIcon[] icons = { UiIcon.Store, UiIcon.Car, UiIcon.Tag, UiIcon.Handshake, UiIcon.Receipt, UiIcon.Rocket, UiIcon.Chart };
        string[] labels =
        {
            W(t, "reallife_dealer_tab_lots", "Dealerships"), W(t, "reallife_dealer_tab_stock", "Stock"), W(t, "reallife_dealer_tab_market", "Market"), W(t, "reallife_dealer_tab_offers", "Offers"),
            W(t, "reallife_dealer_tab_finance", "Finance"), W(t, "reallife_dealer_tab_growth", "Growth"), W(t, "reallife_dealer_tab_reports", "Reports")
        };
        // The badges are the live ones: the showrooms, the cars on their lots and the offers waiting.
        LotTotals lots = DealerFigures.Lots(s);
        OfferTotals offers = DealerFigures.Offers(s);
        string[] counts = { s.Yards.Count > 0 ? s.Yards.Count.ToString(InvD) : null, lots.Cars > 0 ? lots.Cars.ToString(InvD) : null, null, offers.Waiting > 0 ? offers.Waiting.ToString(InvD) : null, null, null, null };
        var nav = new UiNavEntry[7];
        for (int i = 0; i < nav.Length; i++)
        {
            nav[i] = new UiNavEntry { Icon = icons[i], Label = labels[i], Count = counts[i] };
        }
        return new EstateModel { Title = W(t, "reallife_dealer_app", "Car Showroom"), Icon = UiIcon.Car, Nav = nav, Tab = tab, TabsAcross = true, Prompts = EstatePrompts(t) };
    }

    public static EstateModel Dealer(UiWords t, string state)
    {
        var runs = new List<Func<bool>>();
        EstateModel m;
        switch (state)
        {
            case "off":
                m = DealerBase(t, 1, DealerWorld("std"));
                m.Nav = new UiNavEntry[0];
                m.Shape = EstateShape.Off;
                m.OffIcon = UiIcon.Car;
                m.OffTitle = W(t, "reallife_dealer_off_title", "The Car Showroom is off");
                m.OffBody = W(t, "reallife_dealer_off_body", "Turn it on in Mod settings, or load a city to run a showroom.");
                break;
            case "stock":
            case "stock_empty":
            case "stock_full":
                {
                    DealerState s = DealerWorld(state == "stock_empty" ? "empty" : state == "stock_full" ? "full" : "std");
                    m = DealerBase(t, 1, s);
                    Stock(t, m, runs, s);
                    break;
                }
            case "market":
                m = DealerBase(t, 2, DealerWorld("std"));
                DealerMarket(t, m, runs, DealerWorld("std"));
                break;
            case "used":
                m = DealerBase(t, 2, DealerWorld("std"));
                DealerUsed(t, m, runs, DealerWorld("std"));
                break;
            case "auction":
            case "auction_new":
                m = DealerBase(t, 2, DealerWorld("std"));
                DealerAuction(t, m, runs, DealerWorld("std"), state == "auction_new");
                break;
            case "offers":
                m = DealerBase(t, 3, DealerWorld("std"));
                DealerOffers(t, m, runs, DealerWorld("std"));
                break;
            case "deal":
                m = DealerBase(t, 3, DealerWorld("std"));
                m.Tab = 3;
                DealerDecide(t, m, runs, DealerWorld("std"));
                break;
            case "finance":
                m = DealerBase(t, 4, DealerWorld("std"));
                DealerFinance(t, m, DealerWorld("std"));
                break;
            case "growth":
            case "event":
                m = DealerBase(t, 5, DealerWorld("std"));
                DealerGrowth(t, m, runs, DealerWorld("std"), state == "event");
                break;
            case "reports":
            case "reports_new":
                m = DealerBase(t, 6, DealerWorld(state == "reports_new" ? "new" : "std"));
                DealerReports(t, m, runs, DealerWorld(state == "reports_new" ? "new" : "std"));
                break;
            default:
                m = DealerBase(t, 0, DealerWorld("std"));
                DealerLots(t, m, runs, DealerWorld("std"), state == "yard");
                break;
        }
        m.Runs = runs.ToArray();
        return m;
    }

    private static UiActionModel DealerAct(List<Func<bool>> runs, string label, UiButtonKind kind, UiIcon icon, UiAction key, int arg) =>
        new UiActionModel { Label = label, Kind = kind, Icon = icon, Key = key, Command = UiCatalogue.First + UiCatalogue.Action, Arg = arg };

    // ------------------------------------------------------------------------------------------------- the stock

    private static void Stock(UiWords t, EstateModel m, List<Func<bool>> runs, DealerState s)
    {
        m.Shape = EstateShape.Catalogue;
        // Every car of every lot in one list, in bay order, so the chips can say how many each holds.
        var all = new List<KeyValuePair<DealerYard, StockCar>>();
        int[] held = new int[4];
        int bays = 0;
        foreach (DealerYard yard in s.Yards)
        {
            bays += yard.Spots.Count;
            foreach (StockCar car in yard.Cars)
            {
                all.Add(new KeyValuePair<DealerYard, StockCar>(yard, car));
                held[0]++;
                held[car.Source == CarSource.Wholesale ? 1 : 2]++;
                held[3] += car.ShopUntil > NowHour ? 1 : 0;
            }
        }
        all.Sort((p, q) => p.Value.Spot != q.Value.Spot ? p.Value.Spot.CompareTo(q.Value.Spot) : s.Yards.IndexOf(p.Key).CompareTo(s.Yards.IndexOf(q.Key)));
        var cards = new List<UiCatalogueCard>();
        foreach (KeyValuePair<DealerYard, StockCar> pair in all)
        {
            StockCar car = pair.Value;
            int days = Today - car.Bought;
            CarKind kind = KindOfCar(car.Type);
            cards.Add(new UiCatalogueCard
            {
                Icon = CarClass.Glyph(kind), Tint = CarClass.Tint(kind, car.Asking), Name = car.Type, Figure = Usd(car.Asking),
                State = days >= 1 ? Fx(W(t, "reallife_ui_dealer_days_chip", "{days} days"), "days", days.ToString(InvD)) : W(t, "reallife_ui_dealer_today", "Today"),
                StateTone = days >= 30 ? UiRole.Danger : days >= 14 ? UiRole.Warn : UiRole.Notice
            });
        }
        var c = new UiCatalogueModel
        {
            Cards = cards.ToArray(), CardCount = cards.Count, Chosen = cards.Count > 0 ? 0 : -1,
            EmptyTitle = W(t, "reallife_dealer_empty_stock", "No car stands on the lots of the showrooms shown."),
            EmptyBody = W(t, "reallife_ui_dealer_empty_stock_body", "The cars you buy appear here with their pictures, their prices and their days on the lot."), EmptyIcon = UiIcon.Car
        };
        var keys = new string[cards.Count];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = "car:" + all[i].Value.Id;
        }
        m.RowKeys = keys;
        if (cards.Count > 0)
        {
            DealerYard yard = all[0].Key;
            StockCar car = all[0].Value;
            int worth = WorthOfCar(car), days = Today - car.Bought, margin = car.Asking - car.Cost, step = DealerRules.AskStepOf(worth, car.Asking);
            bool workshop = car.ShopUntil > NowHour;
            string stateWord = workshop ? W(t, "reallife_dealer_state_workshop", "In the workshop") : DealerRules.HasDeal(yard, car.Id) ? W(t, "reallife_dealer_state_offer", "Offer waiting") : W(t, "reallife_dealer_state_on_show", "On show");
            c.DetailKicker = Fx(W(t, "reallife_ui_dealer_kicker_bay", "{name}, bay {bay}, {state}"), "name", yard.Name, "bay", (car.Spot + 1).ToString(InvD), "state", stateWord);
            var labels = new List<string> { W(t, "reallife_ui_dealer_l_cost", "Cost, all in"), W(t, "reallife_ibox_worth", "Worth"), W(t, "reallife_ibox_asking", "Asking"), W(t, "reallife_ui_dealer_l_margin", "Margin at asking"),
                W(t, "reallife_ui_dealer_l_condition", "Condition"), W(t, "reallife_ui_dealer_l_km", "Mileage"), W(t, "reallife_ui_dealer_l_source", "Came from") };
            var values = new List<string> { Usd(car.Cost), Usd(worth), Usd(car.Asking), (margin >= 0 ? "+" : "-") + Usd(Math.Abs(margin)), DCondition(t, car.Condition), DKm(t, car.Km),
                DSource(t, car.Source) + ", " + Fx(W(t, "reallife_ui_dealer_days_on_lot", "{days} days on the lot"), "days", days.ToString(InvD)) };
            int rival;
            int cheapest = RivalRules.Cheapest(s, car.Type, out rival);
            labels.Add(W(t, "reallife_ui_dealer_l_rival", "Cheapest rival"));
            values.Add(cheapest > 0 && rival >= 0 ? RivalName(rival) + ", " + Usd(cheapest) : W(t, "reallife_ui_dealer_l_no_rival", "None"));
            labels.Add(W(t, "reallife_ui_dealer_l_looked_today", "Looked at today"));
            values.Add(car.ViewsToday.ToString(InvD));
            labels.Add(W(t, "reallife_ui_dealer_l_looked_yesterday", "Looked at yesterday"));
            values.Add(car.ViewsLast.ToString(InvD));
            foreach (DealerOffer d in yard.Deals)
            {
                if (d.Car == car.Id && d.Looks > 0)
                {
                    labels.Add(W(t, "reallife_ui_dealer_l_offer_waiting", "Offer waiting"));
                    values.Add(d.Customer + ", " + Usd(DealerRules.Price(yard, d, car)));
                    break;
                }
            }
            c.DetailLabels = labels.ToArray();
            c.DetailValues = values.ToArray();
            c.DetailInks = new[] { UiRole.None, UiRole.None, UiRole.None, margin >= 0 ? UiRole.OkText : UiRole.DangerText };
            for (int i = 0; i < 4; i++)
            {
                runs.Add(Yes);
            }
            int less = Math.Max(0, step - 1), more = Math.Min(DealerRules.AskSteps - 1, step + 1);
            c.Actions = new[]
            {
                DealerAct(runs, Fx(W(t, "reallife_ui_dealer_ask_to", "Ask {price}"), "price", Usd(DealerRules.AskAt(worth, less))), UiButtonKind.Secondary, UiIcon.Minus, UiAction.Count, 0),
                DealerAct(runs, Fx(W(t, "reallife_ui_dealer_ask_to", "Ask {price}"), "price", Usd(DealerRules.AskAt(worth, more))), UiButtonKind.Secondary, UiIcon.Plus, UiAction.Count, 1),
                DealerAct(runs, W(t, "reallife_dealer_button_prepare", "Prepare it"), UiButtonKind.Secondary, UiIcon.Wrench, UiAction.Option, 2),
                DealerAct(runs, W(t, "reallife_dealer_button_trade", "Sell to the trade"), UiButtonKind.Destructive, UiIcon.Coin, UiAction.Count, 3)
            };
            m.CatalogueRuns = new[] { 0, 1, 2, 3 };
        }
        m.Catalogue = c;
        string[][] chips = { new[] { "reallife_ui_dealer_filter_all", "All {count}" }, new[] { "reallife_ui_dealer_filter_new", "New {count}" }, new[] { "reallife_ui_dealer_filter_used", "Used {count}" },
            new[] { "reallife_ui_dealer_filter_shop", "In the workshop {count}" } };
        m.FilterChips = new UiActionModel[cards.Count > 0 ? chips.Length : 0];
        for (int i = 0; i < m.FilterChips.Length; i++)
        {
            runs.Add(Yes);
            m.FilterChips[i] = new UiActionModel { Label = Fx(W(t, chips[i][0], chips[i][1]), "count", held[i].ToString(InvD)), Kind = UiButtonKind.Chip, Command = EstateFace.ActionCommand, Arg = runs.Count - 1 };
        }
        string[][] sorts = { new[] { "reallife_ui_dealer_sort_bay", "By bay number" }, new[] { "reallife_ui_dealer_sort_price", "By price" }, new[] { "reallife_ui_dealer_sort_days", "Longest on the lot" } };
        m.SortChips = new UiActionModel[cards.Count > 0 ? sorts.Length : 0];
        for (int i = 0; i < m.SortChips.Length; i++)
        {
            runs.Add(Yes);
            m.SortChips[i] = new UiActionModel { Label = W(t, sorts[i][0], sorts[i][1]), Kind = UiButtonKind.Chip, Command = EstateFace.ActionCommand, Arg = runs.Count - 1 };
        }
        m.SearchHint = cards.Count > 0 ? W(t, "reallife_ui_dealer_search", "Search a model") : null;
        int carCount;
        int worthAll = DealerFigures.StockWorth(s, WorthOfCar, out carCount);
        m.HeadSub = Fx(W(t, "reallife_ui_dealer_on_lot", "On the lot, {cars} of {bays} bays"), "cars", carCount.ToString(InvD), "bays", bays.ToString(InvD));
        m.FigLabels = new[] { W(t, "reallife_ui_dealer_stock_worth", "Stock worth") };
        m.FigValues = new[] { Usd(worthAll) };
    }

    // ------------------------------------------------------------------------------------------------- the showrooms

    // A line over the days a report keeps, as the live pages draw it: only the days that have figures, in the report's own unit.
    private static UiChartModel DealerTrend(UiWords t, DealerState s, ReportKind kind, string place, string titleKey, string english)
    {
        int days = DealerCatalogue.ReportDays;
        var values = new long[days];
        var have = new bool[days];
        DealerReport.Series(s, place, kind, Today, days, values, have, new DealerBook());
        bool money = DealerReport.Money(kind);
        var pts = new List<string>(days);
        var line = new List<float>(days);
        for (int i = 0; i < days; i++)
        {
            if (have[i])
            {
                pts.Add(Fx(W(t, "reallife_ui_dealer_chart_day", "Day {day}"), "day", (Today - (days - 1 - i)).ToString(InvD)));
                line.Add(values[i]);
            }
        }
        if (line.Count < 2)
        {
            return null;
        }
        Func<float, string> format = v => DealerFormat(v, kind, money);
        return new UiChartModel
        {
            Kind = UiChartKind.Line, Title = W(t, titleKey, english), Points = pts.ToArray(), Still = true, EvenAxis = true,
            Series = new[] { new UiSeries { Label = W(t, titleKey, english), Values = line.ToArray(), Tint = UiRole.None } }, Format = format, AxisFormat = format
        };
    }

    private static string DealerFormat(float v, ReportKind kind, bool money) =>
        money ? Usd((long)Math.Max(int.MinValue / 2, Math.Min(int.MaxValue / 2, v))) : kind == ReportKind.Conversion ? (v / 10f).ToString("0.#", InvD) + "%" : ((int)v).ToString(InvD);

    private static void DealerLots(UiWords t, EstateModel m, List<Func<bool>> runs, DealerState s, bool yardPage)
    {
        if (yardPage)
        {
            DealerYardPage(t, m, runs, s, s.Yards[0]);
            return;
        }
        var rows = new List<UiDataRow>();
        foreach (DealerYard yard in s.Yards)
        {
            int staff = StaffOf[yard.Place];
            rows.Add(Row(new[] { yard.Name, Fx(W(t, "reallife_dealer_lot_count", "{cars} cars on {places} places"), "cars", yard.Cars.Count.ToString(InvD), "places", yard.Spots.Count.ToString(InvD)),
                    staff.ToString(InvD), yard.Today.Visitors.ToString(InvD), yard.Today.Sold.ToString(InvD), Usd(yard.Today.Revenue), yard.Deals.Count.ToString(InvD) },
                new[] { (int)UiIcon.Store, 0, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, staff == 0 ? UiRole.DangerText : UiRole.None, UiRole.None, UiRole.None, UiRole.None, yard.Deals.Count > 0 ? UiRole.WarnText : UiRole.None },
                new[] { DStep(t, DealerRules.Step(yard.Rep)), null, null, null, null, null, null }));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_showroom", "Showroom"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_lot", "Lot"), Kind = UiCellKind.Text, Width = 168f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_staff", "Staff"), Kind = UiCellKind.Figure, Width = 72f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_visitors", "Visitors"), Kind = UiCellKind.Figure, Width = 88f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_sold", "Sold"), Kind = UiCellKind.Figure, Width = 72f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_takings", "Takings today"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_offers", "Offers"), Kind = UiCellKind.Figure, Width = 80f }
        };
        TableOf(m, UiIcon.Store, W(t, "reallife_dealer_tab_lots", "Dealerships"), Fx(W(t, "reallife_ui_dealer_lots_sub", "{count} showrooms"), "count", s.Yards.Count.ToString(InvD)), cols, rows, null,
            W(t, "reallife_dealer_row_no_dealership", "No dealership yet"), W(t, "reallife_dealer_lots_about", "Each showroom keeps its own lot, staff and figures."));
        LotTotals lots = DealerFigures.Lots(s);
        m.FigLabels = new[] { W(t, "reallife_ui_dealer_fig_cars", "Cars"), W(t, "reallife_ui_dealer_fig_sold_today", "Sold today"), W(t, "reallife_ui_dealer_fig_takings_today", "Takings today"), W(t, "reallife_ui_dealer_fig_offers", "Offers waiting") };
        m.FigValues = new[] { lots.Cars.ToString(InvD), lots.Sold.ToString(InvD), Usd(lots.Revenue), lots.Deals.ToString(InvD) };
        m.Lower = DealerTrend(t, s, ReportKind.Revenue, null, "reallife_dealer_report_revenue", "Revenue");
    }

    private static void DealerYardPage(UiWords t, EstateModel m, List<Func<bool>> runs, DealerState s, DealerYard y)
    {
        var week = new DealerBook();
        DealerReport.Sum(s, y.Place, Today - DealerCatalogue.WeekBooks + 1, Today - 1, Today, week, new DealerBook());
        int step = DealerRules.Step(y.Rep);
        m.Shape = EstateShape.Detail;
        m.Back = W(t, "reallife_ui_dealer_back", "Back");
        m.DetailIcon = UiIcon.Store;
        m.DetailTitle = y.Name;
        m.DetailSub = Fx(W(t, "reallife_ui_dealer_rep_n", "Reputation {rep}"), "rep", y.Rep.ToString(InvD));
        m.DetailChip = DStep(t, step);
        m.DetailChipTone = UiRole.Notice;
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_dealer_fig_visitors_today", "Visitors today"), y.Today.Visitors.ToString(InvD)), Fig(W(t, "reallife_ui_dealer_fig_looked_after", "Looked after"), y.Today.Served.ToString(InvD)),
            Fig(W(t, "reallife_ui_dealer_fig_offers_today", "Offers today"), y.Today.Offers.ToString(InvD)), Fig(W(t, "reallife_ui_dealer_fig_sold_today", "Sold today"), y.Today.Sold.ToString(InvD))
        };
        var shareLabels = new List<string> { W(t, "reallife_ibox_lot", "Places taken"), W(t, "reallife_ui_dealer_sh_looked_after", "Visitors looked after") };
        var shareValues = new List<int> { y.Spots.Count > 0 ? y.Cars.Count * 100 / y.Spots.Count : 0, y.Today.Visitors > 0 ? Math.Min(100, y.Today.Served * 100 / y.Today.Visitors) : 0 };
        if (step + 1 < DealerCatalogue.RepSteps.Length)
        {
            int from = DealerCatalogue.RepSteps[step], to = DealerCatalogue.RepSteps[step + 1];
            shareLabels.Add(W(t, "reallife_ui_dealer_sh_next_step", "On the way to the next step"));
            shareValues.Add(Math.Max(0, Math.Min(100, (y.Rep - from) * 100 / Math.Max(1, to - from))));
        }
        m.ShareTitle = W(t, "reallife_ibox_sec_now", "Now");
        m.ShareLabels = shareLabels.ToArray();
        m.ShareValues = shareValues.ToArray();
        m.Left = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ui_dealer_terms", "Terms"), Heading = true,
                Lines = new[]
                {
                    EstateSwitch(runs, W(t, "reallife_ui_dealer_sw_fixed", "Fixed prices"), y.Fixed),
                    EstateStep(runs, W(t, "reallife_ui_dealer_staff_floor", "Staff may close at"), Fx(W(t, "reallife_dealer_share", "{percent}%"), "percent", ((y.StaffFloor + 5) / 10).ToString(InvD)),
                        W(t, "reallife_ui_dealer_staff_floor_what", "The staff close a deal on their own at this share of the asking price.")),
                    EstateSwitch(runs, W(t, "reallife_ui_dealer_sw_finance", "Offer finance"), y.Finance), EstateSwitch(runs, W(t, "reallife_ui_dealer_sw_warranty", "Offer warranties"), y.Warranty),
                    EstateSwitch(runs, W(t, "reallife_ui_dealer_sw_extras", "Offer extras"), y.Extras)
                },
                Actions = new[] { EstateButton(runs, W(t, "reallife_dealer_button_relay", "Lay the lot out again"), UiButtonKind.Secondary, UiIcon.Grid, UiAction.Count) }
            }
        };
        m.Right = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ibox_sec_this_week", "This week"), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_ui_dealer_fig_cars_sold", "Cars sold"), (week.Sold + y.Today.Sold).ToString(InvD)),
                    UiFactLine.Row(W(t, "reallife_dealer_report_revenue", "Revenue"), Usd(week.Revenue + y.Today.Revenue)),
                    UiFactLine.Row(W(t, "reallife_ui_dealer_fig_bought", "Bought"), Usd(week.Bought + y.Today.Bought)),
                    UiFactLine.Row(W(t, "reallife_ui_dealer_fig_margin", "Margin"), Usd(week.Margin + y.Today.Margin)),
                    UiFactLine.Row(W(t, "reallife_ui_dealer_fig_claims", "Warranty claims"), Usd(week.Claims + y.Today.Claims))
                }
            }
        };
        m.Lower = DealerTrend(t, s, ReportKind.Revenue, y.Place, "reallife_dealer_report_revenue", "Revenue");
        var oldest = new List<StockCar>(y.Cars);
        oldest.Sort((a, b) => a.Bought.CompareTo(b.Bought));
        var longest = new List<EstateRow>();
        for (int i = 0; i < oldest.Count && longest.Count < 3; i++)
        {
            StockCar c = oldest[i];
            runs.Add(Yes);
            longest.Add(new EstateRow
            {
                Icon = CarGlyph(c.Type), Tint = CarTint(c.Type, c.Asking), Title = c.Type, Sub = Fx(W(t, "reallife_ui_dealer_days_on_lot", "{days} days on the lot"), "days", Math.Max(0, Today - c.Bought).ToString(InvD)),
                Amount = Usd(c.Asking), Run = runs.Count - 1
            });
        }
        m.RelatedTitle = W(t, "reallife_ui_dealer_sort_days", "Longest on the lot");
        m.RelatedCount = longest.Count > 0 ? longest.Count.ToString(InvD) : null;
        m.Related = longest.ToArray();
    }

    // ------------------------------------------------------------------------------------------------- the market

    private static string AuctionWords(UiWords t, DealerListing l)
    {
        if (l.Gone)
        {
            return l.Leader == 0 ? W(t, "reallife_dealer_state_won", "Won") : W(t, "reallife_dealer_state_sold_on", "Sold on");
        }
        return l.Leader == 0 ? W(t, "reallife_dealer_state_leading", "You lead") : l.Leader > 0
            ? Fx(W(t, "reallife_dealer_state_rival", "{rival} leads"), "rival", RivalName(l.Leader - 1)) : W(t, "reallife_dealer_state_no_bid", "No bid yet");
    }

    private static string DealerColour(UiWords t, string name)
    {
        string key = name == "Blue" ? "cobalt" : name == "Silver" ? "heather" : name == "Yellow" ? "mustard" : name == "Green" ? "forest" : name.ToLowerInvariant();
        return W(t, "reallife_colour_" + key, name);
    }

    private static void DealerMarket(UiWords t, EstateModel m, List<Func<bool>> runs, DealerState s)
    {
        var rows = new List<UiDataRow>();
        foreach (DealerListing l in s.Market)
        {
            if (l.Gone && l.Kind != ListingKind.Auction)
            {
                continue;
            }
            bool auction = l.Kind == ListingKind.Auction;
            int left = Math.Max(0, l.Ends - NowHour), shown = l.Inspected ? l.Condition : l.Shown;
            string time = auction ? (l.Gone ? W(t, "reallife_dealer_ended", "Ended") : DHours(t, left)) : W(t, "reallife_ui_dealer_today", "Today");
            string chip = auction ? AuctionWords(t, l) : l.Kind == ListingKind.Used ? (l.Inspected ? W(t, "reallife_dealer_state_inspected", "Inspected") : W(t, "reallife_dealer_state_not_inspected", "Not inspected")) : W(t, "reallife_dealer_state_new", "New");
            rows.Add(Row(new[] { l.Type, DKind(t, l.Kind), DKm(t, l.Km), Math.Max(0, Math.Min(100, shown)).ToString(InvD), Usd(l.Asking), time, chip },
                new[] { (int)CarGlyph(l.Type), 0, 0, Math.Max(0, Math.Min(100, shown)), 0, 0, 0 },
                new[] { CarTint(l.Type, l.Asking), UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, auction ? (l.Leader == 0 ? UiRole.Ok : l.Leader > 0 ? UiRole.Warn : UiRole.Notice) : UiRole.Notice },
                new[] { DealerColour(t, l.Colour), null, null, null, null, null, null }));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_car", "Car"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_kind", "Kind"), Kind = UiCellKind.Text, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_km", "Kilometres"), Kind = UiCellKind.Figure, Width = 96f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_condition", "Condition"), Kind = UiCellKind.Meter, Width = 136f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_price", "Price"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_time", "Time"), Kind = UiCellKind.Text, Width = 96f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 216f }
        };
        int news, used, lots;
        DealerFigures.Market(s, out news, out used, out lots);
        TableOf(m, UiIcon.Tag, W(t, "reallife_dealer_market_title", "The car market"),
            Fx(W(t, "reallife_ui_dealer_market_sub", "{new} new, {used} used, {lots} lots at auction"), "new", news.ToString(InvD), "used", used.ToString(InvD), "lots", lots.ToString(InvD)), cols, rows, null,
            W(t, "reallife_ui_dealer_empty_market", "Nothing is on the market today"), W(t, "reallife_dealer_market_about", "New cars come from the wholesaler."));
        m.FigLabels = new[] { W(t, "reallife_dealer_cash", "Cash"), W(t, "reallife_ui_dealer_fig_new", "New"), W(t, "reallife_ui_dealer_fig_used", "Used"), W(t, "reallife_ui_dealer_fig_lots", "At auction") };
        m.FigValues = new[] { Usd(48210), news.ToString(InvD), used.ToString(InvD), lots.ToString(InvD) };
    }

    private static void DealerUsed(UiWords t, EstateModel m, List<Func<bool>> runs, DealerState s)
    {
        DealerListing l = s.Market[1];
        int places = 0;
        foreach (DealerYard y in s.Yards)
        {
            places += s.Room(y);
        }
        DealerYard goes = s.Yards[0];
        m.Shape = EstateShape.Detail;
        m.Back = W(t, "reallife_ui_dealer_back", "Back");
        m.DetailIcon = CarGlyph(l.Type);
        m.DetailTitle = l.Type;
        m.DetailSub = DealerColour(t, l.Colour);
        m.DetailChip = W(t, "reallife_ibox_chip_not_inspected", "Not inspected");
        m.DetailChipTone = UiRole.Warn;
        m.Card = true;
        m.FootBack = W(t, "reallife_ui_estate_not_now", "Not now");
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ibox_asking", "Asking"), Usd(l.Asking)), Fig(W(t, "reallife_ui_dealer_last_counter", "Their last counter"), l.Countered > 0 ? Usd(l.Countered) : W(t, "reallife_dealer_none", "None")),
            Fig(W(t, "reallife_ui_dealer_offers_left", "Offers left"), (DealerCatalogue.HagglesMost - l.Haggles).ToString(InvD)), Fig(W(t, "reallife_ui_dealer_free_places", "Free places"), places.ToString(InvD))
        };
        int offer = DealerRules.HaggleAt(l.Asking, DealerRules.HaggleSteps - 3);
        m.Left = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ibox_sec_offer", "The offer"), Heading = true,
                Lines = new[] { EstateStep(runs, W(t, "reallife_ui_dealer_your_offer", "Your offer"), Usd(offer), Fx(W(t, "reallife_dealer_goes_to", "Goes to {name}, {free} places free"), "name", goes.Name, "free", s.Room(goes).ToString(InvD))) },
                Actions = new[]
                {
                    EstateButton(runs, W(t, "reallife_dealer_button_inspect", "Inspect it"), UiButtonKind.Secondary, UiIcon.Eye, UiAction.Count),
                    EstateButton(runs, W(t, "reallife_dealer_button_buy", "Buy"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                    EstateButton(runs, W(t, "reallife_dealer_button_offer", "Make an offer"), UiButtonKind.Primary, UiIcon.Handshake, UiAction.Alt)
                }
            }
        };
        m.Right = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ibox_sec_car", "The car"), Heading = true,
                Lines = new[] { UiFactLine.Row(W(t, "reallife_ui_dealer_l_km", "Mileage"), DKm(t, l.Km)), UiFactLine.Say(W(t, "reallife_ui_dealer_faults_unknown", "Its faults are not known until it is inspected.")) }
            }
        };
        m.ShareTitle = W(t, "reallife_ui_dealer_sh_said", "As the owner says");
        m.ShareLabels = new[] { W(t, "reallife_ui_dealer_l_condition", "Condition") };
        m.ShareValues = new[] { Math.Max(0, Math.Min(100, l.Shown)) };
        var like = new List<EstateRow>();
        foreach (DealerListing o in s.Market)
        {
            if (o.Id == l.Id || o.Gone || o.Kind != ListingKind.Used || like.Count >= 3)
            {
                continue;
            }
            runs.Add(Yes);
            like.Add(new EstateRow
            {
                Icon = CarGlyph(o.Type), Tint = CarTint(o.Type, o.Asking), Title = o.Type, Sub = DealerColour(t, o.Colour) + ", " + DKm(t, o.Km), Amount = Usd(o.Asking),
                Chip = o.Inspected ? W(t, "reallife_dealer_state_inspected", "Inspected") : W(t, "reallife_dealer_state_not_inspected", "Not inspected"), ChipTone = o.Inspected ? UiRole.Ok : UiRole.Warn, Run = runs.Count - 1
            });
        }
        m.Related2Title = W(t, "reallife_ui_dealer_like_it", "Others like it on the market");
        m.Related2Count = like.Count > 0 ? like.Count.ToString(InvD) : null;
        m.Related2 = like.ToArray();
    }

    private static void DealerAuction(UiWords t, EstateModel m, List<Func<bool>> runs, DealerState s, bool nobid)
    {
        DealerListing l = s.Market[2];
        if (nobid)
        {
            // The same lot before anyone has bid: the sale opens at its first price.
            l = new DealerListing { Id = l.Id, Kind = ListingKind.Auction, Type = l.Type, Colour = l.Colour, Km = l.Km, Condition = l.Condition, Shown = l.Shown, Asking = 21600, Ends = l.Ends, Leader = -1, Ceiling = l.Ceiling };
        }
        int worth = WorthOfListing(l), next = DealerRules.NextBid(l, worth), inc = DealerRules.Increment(worth), bid = Math.Max(next, DealerCatalogue.Named(worth - worth / 12));
        int fee = DealerRules.BuyersFee(bid, DealerCatalogue.BuyersFeeLeast * PriceLevel / 1000);
        int free = 0;
        foreach (DealerYard y in s.Yards)
        {
            free += s.Room(y);
        }
        int left = Math.Max(0, l.Ends - NowHour);
        m.Shape = EstateShape.Auction;
        m.Card = true;
        m.FootBack = W(t, "reallife_ui_estate_not_now", "Not now");
        var a = new EstateAuction { Kicker = W(t, "reallife_ui_dealer_auction_kicker", "Live auction, the trade car auction"), Title = l.Type, Icon = CarGlyph(l.Type), Aspect = 1.8f, Back = W(t, "reallife_ui_dealer_back", "Back") };
        a.Left = new UiFactCardModel
        {
            Lines = new[]
            {
                UiFactLine.Row(W(t, "reallife_ui_dealer_l_worth", "Market value"), Usd(worth)), UiFactLine.Bar(W(t, "reallife_ui_dealer_l_condition", "Condition"), Math.Max(0, Math.Min(100, l.Shown))),
                UiFactLine.Row(W(t, "reallife_ui_dealer_l_km", "Mileage"), DKm(t, l.Km)), UiFactLine.Row(W(t, "reallife_ui_dealer_free_bays", "Your free bays"), free.ToString(InvD))
            }
        };
        var lines = new List<UiFactLine>
        {
            UiFactLine.Row(W(t, "reallife_ui_dealer_current_bid", "Current bid"), Usd(l.Asking)), UiFactLine.Row(W(t, "reallife_ui_dealer_held_by", "Held by"), AuctionWords(t, l)),
            UiFactLine.Bar(W(t, "reallife_ui_dealer_time_left_label", "Time left"), Math.Max(0, Math.Min(100, left * 100 / DealerCatalogue.LotHours[DealerCatalogue.LotHours.Length - 1])), DHours(t, left)),
            EstateStep(runs, W(t, "reallife_ui_dealer_your_bid", "Your bid"), Usd(bid), Fx(W(t, "reallife_ui_dealer_bid_hint", "Bids go up by {step}, so the least you can bid is {least}."), "step", Usd(inc), "least", Usd(next))),
            UiFactLine.Section(W(t, "reallife_ui_dealer_if_you_win", "If you win")), UiFactLine.Row(W(t, "reallife_ui_dealer_your_bid", "Your bid"), Usd(bid)),
            UiFactLine.Row(Fx(W(t, "reallife_ui_dealer_buyers_fee", "Auction fee, {share}"), "share", Fx(W(t, "reallife_dealer_share", "{percent}%"), "percent", ((DealerCatalogue.BuyersFee + 5) / 10).ToString(InvD))), Usd(fee)),
            UiFactLine.Row(W(t, "reallife_ui_dealer_you_pay", "You pay"), Usd(bid + fee)), UiFactLine.Row(W(t, "reallife_ui_dealer_worth_lot", "Worth on your lot"), Usd(worth)),
            UiFactLine.Row(W(t, "reallife_ui_dealer_left_for_you", "Left for you"), (worth - bid - fee >= 0 ? "+" : "-") + Usd(Math.Abs(worth - bid - fee)), worth - bid - fee >= 0 ? UiRole.OkText : UiRole.DangerText)
        };
        if (l.BidLogCount == 0)
        {
            lines.Add(UiFactLine.Section(W(t, "reallife_ui_dealer_bids_chart", "Bids so far")));
            lines.Add(UiFactLine.Say(W(t, "reallife_ui_dealer_no_bids", "Nobody has bid yet.")));
        }
        else
        {
            lines.Add(UiFactLine.Section(Fx(W(t, "reallife_ui_dealer_bidders", "Bidders, the last {count}"), "count", l.BidLogCount.ToString(InvD))));
            for (int i = 0; i < l.BidLogCount && i < DealerCatalogue.BidsKept; i++)
            {
                BidEntry b = l.BidLog[i];
                string who = b.Bidder == 0 ? W(t, "reallife_ui_dealer_you", "You") : RivalName(b.Bidder - 1);
                int ago = Math.Max(0, NowHour - b.Hour);
                lines.Add(UiFactLine.Who(who, ago == 0 ? W(t, "reallife_ui_dealer_this_hour", "this hour") : ago == 1 ? W(t, "reallife_ui_dealer_hour_ago", "an hour ago")
                    : Fx(W(t, "reallife_ui_dealer_hours_ago", "{hours} hours ago"), "hours", ago.ToString(InvD)), Usd(b.Amount), b.Bidder == 0 ? UiRole.AccentText : UiRole.None));
            }
        }
        a.Right = new UiFactCardModel
        {
            Title = Fx(W(t, "reallife_ui_dealer_bidding_on", "Bidding on the {car}"), "car", l.Type), Sub = DealerColour(t, l.Colour), Lines = lines.ToArray(),
            Actions = new[]
            {
                EstateButton(runs, W(t, "reallife_ui_dealer_walk_away", "Walk away"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                EstateButton(runs, W(t, "reallife_dealer_button_inspect", "Inspect it"), UiButtonKind.Secondary, UiIcon.Eye, UiAction.Count),
                EstateButton(runs, Fx(W(t, "reallife_ui_dealer_bid_button", "Bid {amount}"), "amount", Usd(bid)), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, l.Leader == 0)
            }
        };
        var other = new List<EstateRow>();
        foreach (DealerListing o in s.Market)
        {
            if (o.Id == l.Id || o.Gone || o.Kind != ListingKind.Auction || other.Count >= 3)
            {
                continue;
            }
            runs.Add(Yes);
            other.Add(new EstateRow
            {
                Icon = CarGlyph(o.Type), Tint = CarTint(o.Type, o.Asking), Title = o.Type, Sub = Fx(W(t, "reallife_ui_dealer_n_hours_left", "{hours} hours left"), "hours", Math.Max(0, o.Ends - NowHour).ToString(InvD)),
                Amount = Usd(o.Asking), Chip = AuctionWords(t, o), ChipTone = o.Leader == 0 ? UiRole.Ok : o.Leader > 0 ? UiRole.Warn : UiRole.Notice, Run = runs.Count - 1
            });
        }
        m.RelatedTitle = W(t, "reallife_ui_dealer_other_lots", "Other lots at this auction");
        m.RelatedCount = other.Count > 0 ? other.Count.ToString(InvD) : null;
        m.Related = other.ToArray();
        m.Auction = a;
    }

    // ------------------------------------------------------------------------------------------------- offers and sales

    private static void DealerOffers(UiWords t, EstateModel m, List<Func<bool>> runs, DealerState s)
    {
        var rows = new List<UiDataRow>();
        foreach (DealerYard yard in s.Yards)
        {
            foreach (DealerOffer deal in yard.Deals)
            {
                StockCar car = yard.Car(deal.Car);
                if (car == null)
                {
                    continue;
                }
                int offer = DealerRules.Price(yard, deal, car), left = Math.Max(0, deal.Expires - NowHour), share = car.Asking > 0 ? Math.Min(100, offer * 100 / car.Asking) : 0;
                rows.Add(Row(new[] { deal.Customer, car.Type, Usd(offer), Usd(car.Asking), share.ToString(InvD), DHours(t, left),
                        deal.Stage == OfferStage.Driving ? W(t, "reallife_dealer_state_driving", "Test drive") : W(t, "reallife_dealer_state_waiting", "Waiting") },
                    new[] { 0, (int)CarGlyph(car.Type), 0, 0, share, 0, 0 },
                    new[] { UiRole.None, CarTint(car.Type, car.Asking), UiRole.None, UiRole.None, UiRole.None, left <= 3 ? UiRole.DangerText : UiRole.None, UiRole.Warn },
                    new[] { DProfile(t, deal.Profile), yard.Name, null, null, null, null, null }, UiColourHue(deal.Customer)));
            }
        }
        int waiting = rows.Count;
        for (int i = 0; i < s.Sales.Count && i < 12; i++)
        {
            DealerSale sale = s.Sales[i];
            DealerYard yard = s.Yard(sale.Place);
            // A sale shows what it sold for and what it left: the asking price is not kept with it, so that column stays blank.
            rows.Add(Row(new[] { sale.Trade ? W(t, "reallife_dealer_to_trade", "The trade") : sale.Customer, sale.Type, Usd(sale.Price), string.Empty, string.Empty, DayWord(t, sale.Day),
                    Fx(W(t, "reallife_dealer_state_sold", "Sold, margin {margin}"), "margin", Usd(sale.Price - sale.Cost)) },
                new[] { sale.Trade ? UiDataTable.GlyphFace(UiIcon.Store) : 0, (int)CarGlyph(sale.Type), 0, 0, -1, 0, 0 },
                new[] { UiRole.None, CarTint(sale.Type, sale.Price), UiRole.None, UiRole.None, UiRole.None, UiRole.None, sale.Price < sale.Cost ? UiRole.Danger : UiRole.Ok },
                new[] { yard != null ? yard.Name : string.Empty, string.Empty, null, null, null, null, null }, UiColourHue(sale.Customer ?? string.Empty)));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_buyer", "Buyer"), Kind = UiCellKind.Person, Width = 288f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_car", "Car"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_offer", "Offer"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_asking", "Asking"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_against", "Against asking"), Kind = UiCellKind.Meter, Width = 120f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_left", "Time left"), Kind = UiCellKind.Text, Width = 80f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 200f }
        };
        OfferTotals totals = DealerFigures.Offers(s);
        TableOf(m, UiIcon.Handshake, W(t, "reallife_dealer_offers_title", "Offers and sales"),
            Fx(W(t, "reallife_ui_dealer_offers_sub", "{waiting} offers waiting, {sold} sales kept"), "waiting", waiting.ToString(InvD), "sold", (rows.Count - waiting).ToString(InvD)), cols, rows, null,
            W(t, "reallife_ui_dealer_empty_offers", "No offers or sales yet"), W(t, "reallife_dealer_offers_about", "Buyers make an offer and wait for your answer."));
        m.FigLabels = new[] { W(t, "reallife_dealer_sum_waiting", "Waiting for you"), W(t, "reallife_ui_dealer_fig_best_offer", "Best offer"), W(t, "reallife_ui_dealer_fig_sold_today", "Sold today"), W(t, "reallife_ui_dealer_fig_margin_today", "Margin today") };
        m.FigValues = new[] { totals.Waiting.ToString(InvD), totals.Best > 0 ? Usd(totals.Best) : string.Empty, totals.Sold.ToString(InvD), Usd(totals.Margin) };
        m.FigInks = new[] { totals.Waiting > 0 ? UiRole.WarnText : UiRole.None, UiRole.None, UiRole.None, totals.Margin > 0 ? UiRole.OkText : UiRole.None };
        // What the other dealers ask for the cars the waiting offers are for.
        var rivals = new List<EstateRow>();
        foreach (DealerYard yard in s.Yards)
        {
            foreach (DealerOffer deal in yard.Deals)
            {
                StockCar rc = yard.Car(deal.Car);
                int rival;
                int cheapest = rc != null ? RivalRules.Cheapest(s, rc.Type, out rival) : 0;
                if (rc == null || cheapest <= 0 || rivals.Count >= 6)
                {
                    continue;
                }
                RivalRules.Cheapest(s, rc.Type, out rival);
                int gap = cheapest - rc.Asking;
                rivals.Add(new EstateRow
                {
                    Icon = CarGlyph(rc.Type), Tint = CarTint(rc.Type, cheapest), Title = rc.Type, Sub = rival >= 0 ? RivalName(rival) : string.Empty, Amount = Usd(cheapest),
                    Chip = Fx(W(t, gap >= 0 ? "reallife_ui_dealer_over_you" : "reallife_ui_dealer_under_you", gap >= 0 ? "{amount} over yours" : "{amount} under yours"), "amount", Usd(Math.Abs(gap))),
                    ChipTone = gap >= 0 ? UiRole.Ok : UiRole.Warn, Run = -1
                });
            }
        }
        m.RelatedTitle = W(t, "reallife_ui_dealer_rivals_ask", "What the other dealers ask for the same cars");
        m.RelatedCount = rivals.Count > 0 ? rivals.Count.ToString(InvD) : null;
        m.RelatedRows = 6;
        m.Related = rivals.ToArray();
    }

    // ------------------------------------------------------------------------------------------------- finance

    private static void DealerFinance(UiWords t, EstateModel m, DealerState s)
    {
        var rows = new List<UiDataRow>();
        foreach (DealerPlan p in s.Plans)
        {
            DealerYard yard = s.Yard(p.Place);
            rows.Add(Row(new[] { p.Customer, p.Type, Usd(p.Owed), Usd(p.Weekly), DayWord(t, p.Due),
                    p.Missed > 0 ? Fx(W(t, "reallife_dealer_plan_missed", "{weeks} weeks missed"), "weeks", p.Missed.ToString(InvD)) : Fx(W(t, "reallife_dealer_plan_paying", "Paying, rated {grade}"), "grade", GradeLetter(p.Score)) },
                new[] { 0, (int)CarGlyph(p.Type), 0, 0, 0, 0 }, new[] { UiRole.None, CarTint(p.Type, p.Owed), UiRole.None, UiRole.None, UiRole.None, p.Missed > 0 ? UiRole.Danger : UiRole.Ok },
                new[] { yard != null ? yard.Name : string.Empty, string.Empty, null, null, null, null }, UiColourHue(p.Customer)));
        }
        foreach (DealerCover c in s.Covers)
        {
            DealerYard yard = s.Yard(c.Place);
            rows.Add(Row(new[] { c.Customer, c.Type, Usd(c.Paid), string.Empty, DayWord(t, c.Until), DTier(t, c.Tier) },
                new[] { 0, (int)UiIcon.ShieldCheck, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, c.Paid > 0 ? UiRole.Warn : UiRole.None, UiRole.None, UiRole.None, UiRole.Notice },
                new[] { yard != null ? yard.Name : string.Empty, Fx(W(t, "reallife_ui_dealer_claims", "{count} claims"), "count", c.Claims.ToString(InvD)), null, null, null, null }, UiColourHue(c.Customer)));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_customer", "Customer"), Kind = UiCellKind.Person, Width = 288f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_car", "Car"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_owed_paid", "Owed or paid out"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_weekly", "A week"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_until", "Until"), Kind = UiCellKind.Date, Width = 96f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 200f }
        };
        FinanceTotals fin = DealerFigures.Finance(s, GradeIndex);
        TableOf(m, UiIcon.Receipt, W(t, "reallife_dealer_finance_title", "Finance and warranties"),
            Fx(W(t, "reallife_ui_dealer_finance_sub", "{plans} plan(s), {covers} warranty(ies)"), "plans", s.Plans.Count.ToString(InvD), "covers", s.Covers.Count.ToString(InvD)), cols, rows, null,
            W(t, "reallife_ui_dealer_empty_finance", "No finance plans or warranties yet"), W(t, "reallife_dealer_finance_about", "Your own finance and warranties are sold with the cars."));
        m.FigLabels = new[] { W(t, "reallife_ui_dealer_fig_owed", "Owed to you"), W(t, "reallife_ui_dealer_fig_a_week", "Paid in a week"), W(t, "reallife_ui_dealer_fig_behind", "Plans behind"), W(t, "reallife_ui_dealer_fig_paid_out", "Warranties paid out") };
        m.FigValues = new[] { Usd(fin.Owed), Usd(fin.Weekly), fin.Behind.ToString(InvD), Usd(fin.PaidOut) };
        m.FigInks = new[] { UiRole.None, UiRole.None, fin.Behind > 0 ? UiRole.DangerText : UiRole.None, UiRole.None };
        m.SharePlain = true;
        m.ShareTitle = W(t, "reallife_ui_dealer_sh_grades", "How your buyers are rated");
        var names = new List<string>();
        var shares = new List<int>();
        for (int g = 4; g >= 0 && s.Plans.Count > 0; g--)
        {
            if (fin.Grades[g] > 0)
            {
                names.Add(Fx(W(t, "reallife_ui_dealer_grade_n", "Rated {grade}"), "grade", "EDCBA"[g].ToString()));
                shares.Add(fin.Grades[g] * 100 / s.Plans.Count);
            }
        }
        m.ShareLabels = names.ToArray();
        m.ShareValues = shares.ToArray();
        var soonest = new List<DealerCover>(s.Covers);
        soonest.Sort((a, b) => a.Until.CompareTo(b.Until));
        var ending = new List<EstateRow>();
        for (int i = 0; i < soonest.Count && ending.Count < 3; i++)
        {
            DealerCover cv = soonest[i];
            ending.Add(new EstateRow { Icon = UiIcon.ShieldCheck, Title = cv.Type, Sub = cv.Customer, Chip = DayWord(t, cv.Until), ChipTone = UiRole.Notice, Amount = Usd(cv.Paid), Run = -1 });
        }
        m.RelatedTitle = W(t, "reallife_ui_dealer_warranties_end", "Warranties ending soonest");
        m.RelatedCount = ending.Count > 0 ? ending.Count.ToString(InvD) : null;
        m.Related = ending.ToArray();
    }

    // ------------------------------------------------------------------------------------------------- growth

    private static void DealerGrowth(UiWords t, EstateModel m, List<Func<bool>> runs, DealerState s, bool eventPage)
    {
        if (eventPage)
        {
            int kind = DealerCatalogue.OpenWeekend, cost = DealerCatalogue.EventCost[kind] * PriceLevel / 1000;
            m.Shape = EstateShape.Detail;
            m.Back = W(t, "reallife_ui_dealer_back", "Back");
            m.DetailIcon = UiIcon.Megaphone;
            m.DetailTitle = W(t, "reallife_dealer_event_0", "Open weekend");
            m.DetailSub = W(t, "reallife_dealer_event_0_about", "Buyers come from further away for a few days.");
            m.Strip = new[]
            {
                Fig(W(t, "reallife_ibox_cost", "Cost"), Usd(cost)), Fig(W(t, "reallife_ui_dealer_fig_runs", "It runs"), Fx(W(t, "reallife_dealer_days", "{days} days"), "days", DealerCatalogue.EventDays[kind].ToString(InvD))),
                Fig(W(t, "reallife_ui_dealer_fig_rests", "The showroom rests"), Fx(W(t, "reallife_dealer_days", "{days} days"), "days", DealerCatalogue.EventRest.ToString(InvD)))
            };
            m.Left = new[]
            {
                new UiFactCardModel
                {
                    Lines = new[] { EstateStep(runs, W(t, "reallife_ui_dealer_sec_showroom", "Showroom"), s.Yards[0].Name, null) },
                    Actions = new[] { EstateButton(runs, W(t, "reallife_dealer_button_event", "Book the event"), UiButtonKind.Primary, UiIcon.Megaphone, UiAction.Alt) }
                }
            };
            m.Lower = DealerTrend(t, s, ReportKind.Visitors, null, "reallife_dealer_report_visitors", "Visitors");
            return;
        }
        var rows = new List<UiDataRow>();
        for (int k = 0; k < DealerCatalogue.EventKinds; k++)
        {
            DealerYard running = null;
            foreach (DealerYard y in s.Yards)
            {
                running = y.Event == k && y.EventOn(Today) ? y : running;
            }
            rows.Add(Row(new[] { W(t, "reallife_dealer_event_" + k, "Event"), running != null ? running.Name : string.Empty, string.Empty, W(t, "reallife_dealer_event_" + k + "_short", "Buyers come"),
                    running != null ? Fx(W(t, "reallife_dealer_state_event_on", "On until {day}"), "day", DayWord(t, running.EventUntil)) : W(t, "reallife_dealer_state_event_ready", "Ready to book") },
                new[] { (int)UiIcon.Megaphone, 0, -1, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, running != null ? UiRole.Ok : UiRole.Notice },
                new[] { W(t, "reallife_ui_dealer_growth_event", "Event") + ", " + Fx(W(t, "reallife_dealer_days", "{days} days"), "days", DealerCatalogue.EventDays[k].ToString(InvD)) + ", " + Usd(DealerCatalogue.EventCost[k] * PriceLevel / 1000),
                    null, null, null, null }));
        }
        foreach (DealerYard yard in s.Yards)
        {
            for (int mission = 0; mission < DealerCatalogue.Missions; mission++)
            {
                bool done = DealerRules.MissionDone(yard, mission);
                int progress = DealerRules.Progress(yard, mission), target = DealerCatalogue.MissionTarget[mission], share = Math.Min(100, progress * 100 / Math.Max(1, target));
                string about = Fx(W(t, "reallife_dealer_mission_" + mission + "_about", "A mission."), "target", target.ToString(InvD), "days", DealerCatalogue.QuickDays.ToString(InvD),
                    "share", Fx(W(t, "reallife_dealer_share", "{percent}%"), "percent", ((DealerCatalogue.GoodMargin + 5) / 10).ToString(InvD)));
                rows.Add(Row(new[] { W(t, "reallife_dealer_mission_" + mission, "Mission"), yard.Name, share.ToString(InvD),
                        about + " " + Fx(W(t, "reallife_dealer_progress", "{done} of {target}"), "done", progress.ToString(InvD), "target", target.ToString(InvD)) + ".",
                        done ? W(t, "reallife_dealer_state_mission_done", "Done") : W(t, "reallife_dealer_state_mission_open", "Under way") },
                    new[] { (int)UiIcon.Trophy, 0, share, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, done ? UiRole.Ok : UiRole.Notice },
                    new[] { Fx(W(t, "reallife_ui_dealer_growth_mission", "Mission, {points} points"), "points", DealerCatalogue.MissionReward[mission].ToString(InvD)), null, null, null, null }));
            }
            int step = DealerRules.Step(yard.Rep);
            bool top = step + 1 >= DealerCatalogue.RepSteps.Length;
            int towards = top ? 100 : Math.Min(100, yard.Rep * 100 / Math.Max(1, DealerCatalogue.RepSteps[step + 1]));
            rows.Add(Row(new[] { DStep(t, step), yard.Name, towards.ToString(InvD),
                    (step == 0 ? W(t, "reallife_dealer_perk_none", "No perk yet") : W(t, "reallife_dealer_perk_" + step, "A perk")) + (top ? string.Empty : ", " + yard.Rep.ToString(InvD) + " / " + DealerCatalogue.RepSteps[step + 1].ToString(InvD)), string.Empty },
                new[] { (int)UiIcon.Star, 0, towards, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_ui_dealer_growth_rep", "Reputation"), null, null, null, null }));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_what", "What"), Kind = UiCellKind.Tile, Width = 216f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_showroom", "Showroom"), Kind = UiCellKind.Text, Width = 176f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_progress", "Progress"), Kind = UiCellKind.Meter, Width = 176f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_about", "What it does"), Kind = UiCellKind.Text, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 152f }
        };
        TableOf(m, UiIcon.Rocket, W(t, "reallife_dealer_growth_title", "Growth"), W(t, "reallife_ui_dealer_growth_sub", "Events to book, missions and reputation"), cols, rows, null,
            W(t, "reallife_dealer_row_no_dealership", "No dealership yet"), W(t, "reallife_dealer_growth_events", "Events bring buyers."));
        GrowthTotals g = DealerFigures.Growth(s, Today);
        m.FigLabels = new[] { W(t, "reallife_ui_dealer_fig_events_on", "Events on"), W(t, "reallife_ui_dealer_fig_missions_done", "Missions done"), W(t, "reallife_ui_dealer_fig_sold_today", "Sold today") };
        m.FigValues = new[] { g.EventsOn.ToString(InvD), g.MissionsDone.ToString(InvD) + " / " + g.MissionsAll.ToString(InvD), g.SoldToday.ToString(InvD) };
        m.Lower = DealerTrend(t, s, ReportKind.Sold, null, "reallife_dealer_report_sold", "Cars sold");
    }

    // ------------------------------------------------------------------------------------------------- reports

    private static void DealerReports(UiWords t, EstateModel m, List<Func<bool>> runs, DealerState s)
    {
        ReportKind kind = ReportKind.Profit;
        string[] keys = { "reallife_dealer_report_sold|Cars sold", "reallife_dealer_report_revenue|Revenue", "reallife_dealer_report_profit|Profit", "reallife_dealer_report_age|Days on the lot", "reallife_dealer_report_visitors|Visitors",
            "reallife_dealer_report_conversion|Conversion", "reallife_dealer_report_arrears|Arrears" };
        string[] kindWords = keys[(int)kind].Split('|');
        int days = DealerCatalogue.ReportDays;
        var values = new long[days];
        var have = new bool[days];
        var average = new long[days];
        var scratch = new DealerBook();
        DealerReport.Series(s, null, kind, Today, days, values, have, scratch);
        DealerReport.Average(values, have, days, average);
        bool money = DealerReport.Money(kind);
        var pts = new List<string>();
        var line = new List<float>();
        var avg = new List<float>();
        for (int i = 0; i < days; i++)
        {
            if (!have[i])
            {
                continue;
            }
            pts.Add(Fx(W(t, "reallife_ui_dealer_chart_day", "Day {day}"), "day", (Today - (days - 1 - i)).ToString(InvD)));
            line.Add(values[i]);
            avg.Add(average[i]);
        }
        Func<float, string> format = v => DealerFormat(v, kind, money);
        var chart = new UiChartModel
        {
            Kind = UiChartKind.Line, Title = W(t, kindWords[0], kindWords[1]), Points = pts.ToArray(), EvenAxis = true,
            Series = new[]
            {
                new UiSeries { Label = W(t, kindWords[0], kindWords[1]), Values = line.ToArray(), Tint = UiRole.None },
                new UiSeries { Label = Fx(W(t, "reallife_ui_dealer_report_average", "Average of {days} days"), "days", DealerCatalogue.AverageDays.ToString(InvD)), Values = avg.ToArray(), Tint = UiRole.None }
            },
            Format = format, AxisFormat = format, EmptyTitle = W(t, "reallife_ui_dealer_chart_empty", "Not enough days yet"), EmptyBody = W(t, "reallife_dealer_report_none", "No figures yet. They are kept from the first day the showroom trades.")
        };
        if (line.Count < 2)
        {
            chart.Points = new string[0];
            chart.Series = new UiSeries[0];
        }
        m.Shape = EstateShape.Chart;
        m.HeadIcon = UiIcon.Chart;
        m.HeadTitle = W(t, kindWords[0], kindWords[1]);
        m.HeadSub = Fx(W(t, "reallife_ui_dealer_report_sub", "{place}: a point a day for the last {days} days, with the average of the last {average} days. Point at a day to read it."), "place", W(t, "reallife_dealer_report_all", "All showrooms"),
            "days", days.ToString(InvD), "average", DealerCatalogue.AverageDays.ToString(InvD));
        m.Chart = chart;
        m.ChartKinds = new UiActionModel[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            string[] k = keys[i].Split('|');
            runs.Add(Yes);
            m.ChartKinds[i] = new UiActionModel { Label = W(t, k[0], k[1]), Kind = UiButtonKind.Chip, Command = EstateFace.ActionCommand, Arg = runs.Count - 1 };
        }
        m.ChartKind = (int)kind;
        m.ChartKindsTitle = W(t, "reallife_ui_dealer_plot_what", "What to plot");
        m.ChartPlacesTitle = W(t, "reallife_ui_dealer_plot_where", "Which showroom");
        m.ChartPlaces = new UiActionModel[s.Yards.Count > 1 ? s.Yards.Count + 1 : 0];
        for (int i = 0; i < m.ChartPlaces.Length; i++)
        {
            runs.Add(Yes);
            m.ChartPlaces[i] = new UiActionModel { Label = i == 0 ? W(t, "reallife_dealer_report_all", "All showrooms") : s.Yards[i - 1].Name, Kind = UiButtonKind.Chip, Command = EstateFace.ActionCommand, Arg = runs.Count - 1 };
        }
        m.ChartPlace = 0;
        var weekBook = new DealerBook();
        var nowBook = new DealerBook();
        DealerReport.Sum(s, null, Today - DealerCatalogue.WeekBooks + 1, Today, Today, weekBook, scratch);
        DealerReport.Day(s, null, Today, Today, nowBook);
        long conversion = DealerReport.Value(weekBook, ReportKind.Conversion);
        var sums = new List<UiFactLine>
        {
            UiFactLine.Row(W(t, "reallife_ui_dealer_fig_cars_sold", "Cars sold"), weekBook.Sold.ToString(InvD)), UiFactLine.Row(W(t, "reallife_dealer_report_revenue", "Revenue"), Usd(weekBook.Revenue)),
            UiFactLine.Row(W(t, "reallife_ui_dealer_fig_margin", "Margin"), Usd(weekBook.Margin), weekBook.Margin > 0 ? UiRole.OkText : UiRole.None), UiFactLine.Row(W(t, "reallife_ui_dealer_fig_extras", "Extras"), Usd(weekBook.Extras)),
            UiFactLine.Row(W(t, "reallife_ui_dealer_fig_spent", "Events and the workshop"), Usd(weekBook.Spent)), UiFactLine.Row(W(t, "reallife_dealer_report_visitors", "Visitors"), weekBook.Visitors.ToString(InvD)),
            UiFactLine.Row(W(t, "reallife_dealer_report_conversion", "Came to buy"), (conversion / 10.0).ToString("0.#", InvD) + "%"), UiFactLine.Section(W(t, "reallife_ibox_sec_now", "Now")),
            UiFactLine.Row(W(t, "reallife_ui_dealer_fig_cars_lot", "Cars on the lots"), nowBook.Cars.ToString(InvD)),
            UiFactLine.Row(W(t, "reallife_ui_dealer_fig_age", "Days on the lot, on average"), DealerReport.Value(nowBook, ReportKind.StockAge).ToString(InvD)),
            UiFactLine.Row(W(t, "reallife_ui_dealer_fig_arrears", "In arrears"), Usd(nowBook.Arrears), nowBook.Arrears > 0 ? UiRole.DangerText : UiRole.None),
            UiFactLine.Row(W(t, "reallife_ui_dealer_fig_late", "Plans behind"), nowBook.Late.ToString(InvD), nowBook.Late > 0 ? UiRole.DangerText : UiRole.None)
        };
        m.Side = new UiFactCardModel { Title = W(t, "reallife_ibox_sec_this_week", "This week"), Heading = true, Lines = sums.ToArray() };
    }
}
