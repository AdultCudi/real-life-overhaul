using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Ui;

// The second half of the Stocks logic audit's checks: the save, the Pays and Account figures, the alerts and the screen's commands.
internal static partial class Program
{
    // ------------------------------------------------------------------------------------------------ the save

    // The gain figures sit at the end of the rule's and the bot's own records, and a save from before them loads and plays on.
    private static void StocksBotFigureStore()
    {
        MarketEngine m = _stocksRun;
        MarketState s = m.State;
        RuleBot mine = s.Rules.First(r => r.Owner == 0);
        mine.DayGain = 1234;
        mine.WeekGain = -5678;
        mine.FigDay = s.Day;
        mine.Shares = 77;
        mine.Cost = 4242;
        Bot firm = s.Bots.First();
        firm.DayGain = 321;
        firm.WeekGain = 654;
        firm.FigDay = s.Day;
        string line = MarketStore.Write(s);
        MarketState back = MarketStore.Read(line, out _);
        RuleBot r2 = back != null ? back.Rules.FirstOrDefault(r => r.Id == mine.Id) : null;
        Bot b2 = back != null ? MarketBots.Hired(back, firm.Firm) : null;
        Check(r2 != null && r2.DayGain == 1234 && r2.WeekGain == -5678 && r2.FigDay == s.Day && r2.Shares == 77 && r2.Cost == 4242 && b2 != null && b2.DayGain == 321 && b2.WeekGain == 654 && b2.FigDay == s.Day,
            "stocks bots save: a rule's and a firm's day and week gains come back with the day they are for");
        Check(back != null && MarketStore.Write(back) == line, "stocks bots save: the line writes again as it was read" + FirstDifference(line, back != null ? MarketStore.Write(back) : null));
        // The new fields are the last of each record; a record without them (a save from before) loads with the gains at nothing and all else as it was.
        string desk = Part(line, 'z');
        string older = line.Replace(desk, string.Join(";", desk.Split(';').Select(r => r.StartsWith("r|") && r.Split('|').Length >= 26 ? string.Join("|", r.Split('|').Take(21)) : r)));
        string bots = Part(line, 'b');
        older = older.Replace(bots, string.Join(";", bots.Split(';').Select(r => string.Join("|", r.Split('|').Take(44)))));
        MarketState old = MarketStore.Read(older, out _);
        RuleBot ro = old != null ? old.Rules.FirstOrDefault(r => r.Id == mine.Id) : null;
        Bot bo = old != null ? MarketBots.Hired(old, firm.Firm) : null;
        Check(ro != null && ro.DayGain == 0 && ro.WeekGain == 0 && ro.Shares == 0 && ro.Cost == 0 && ro.Cash == mine.Cash && ro.Budget == mine.Budget && ro.OpenTrades == mine.OpenTrades
              && bo != null && bo.DayGain == 0 && bo.WeekGain == 0 && bo.Realised == firm.Realised && bo.Capital == firm.Capital,
            "stocks bots save: a save from before the gain figures loads the rule and the firm as they were, with the figures at nothing");
        // And plays on: the next day turn gives a rule loaded with no day a figure for that day.
        if (old != null)
        {
            RuleBots.Turn(old, old.Day + 1);
            Check(old.Rules.All(r => r.FigDay == old.Day + 1 && r.DayGain == 0), "stocks bots save: a loaded save's rules turn to the next day and read nothing for it");
        }
    }

    // ------------------------------------------------------------------------------------------------ Pays and Account

    // What the Portfolio's Pays and Account views say is what the market holds: the income of the shares held, the payment to come, and the
    // commission and dividends of the account, read after real trades and a real dividend; and a margin call after a real fall.
    private static void StocksPaysAccountChecks()
    {
        int paid = 0, looked = 0;
        for (ulong seed = 41; seed <= 52 && looked == 0; seed++)
        {
            var world = new MarketWorld(seed);
            world.Purse.Money = 1000000000;
            MarketEngine m = NewMarket(seed, 60, 1, 0, world);
            Stock s = null;
            for (int d = 1; d < 80 && s == null; d++)
            {
                RunTo(m, d, MarketCatalogue.PhaseOpen);
                s = m.State.Stocks.FirstOrDefault(k => k.Listed && k.DeclaredCents > 0 && !k.ExDone && k.RecordDay > d + 1 && k.Size != MarketSize.Small && k.BidCents <= 0 && k.SplitDay <= 0 && !TrustRules.Is(k));
            }
            if (s == null)
            {
                continue;
            }
            looked++;
            int record = s.RecordDay, pay = s.PayDay, ex = MarketTrading.NextTradingDay(record);
            int cents = s.DeclaredCents;
            RunTo(m, record, MarketCatalogue.PhaseOpen + 70);
            long qty = 1000;
            Trade(m, s, OrderSide.Buy, qty);
            RunTo(m, ex, MarketCatalogue.PhaseOpen);
            var f = new PaysFigures();
            StocksFigures.Pays(m.State, m.State.Phase, true, f);
            int kind = (int)InvestRules.KindOf(s);
            Position held = m.State.Position(s.Id);
            Check(held != null && held.Shares == qty && f.Sums.YearCents[kind] == qty * Math.Max(0, s.DpsCents) * 4 && f.YearCents == f.Sums.YearCents[kind] && f.Any,
                "stocks pays: the next year's income of the shares held is their quarterly dividend four times (" + f.Sums.YearCents[kind] + " cents of " + qty + " shares at " + s.DpsCents + ")");
            Check(f.Next.Count == 1 && f.NextAll == 1 && f.Next[0].Stock == s.Id && f.Next[0].Day == pay && f.Next[0].Cents == qty * cents,
                "stocks pays: the payment to come is the dividend owed, on its pay day (" + (f.Next.Count > 0 ? f.Next[0].Day + " / " + f.Next[0].Cents : "none") + " against " + pay + " / " + qty * cents + ")");
            RunTo(m, pay);
            AccountFigures a = StocksFigures.Account(m.State, m.State.Phase, 0);
            long fees = m.State.Trades.Where(t => t.Kind == TradeKind.Bought || t.Kind == TradeKind.Sold).Sum(t => t.Fee);
            long dividends = m.State.Trades.Where(t => t.Kind == TradeKind.Dividend && t.Bot == 0).Sum(t => t.Value);
            paid = (int)Math.Min(int.MaxValue, dividends);
            Check(a.Commission > 0 && a.Commission == fees && a.Dividends > 0 && a.Dividends == dividends && !a.Margin && !a.Called && a.DividendsPaid == 0,
                "stocks account: the commission paid and the dividends paid to the player are the sums of the trades' fees and of the dividend lines (" + a.Commission + " and " + a.Dividends + ")");
        }
        Check(looked == 1 && paid > 0, "stocks account: a company that declared a dividend was found, held through its record day and paid");
        // The margin call after a real fall, read as the Account view says it.
        var w2 = new MarketWorld(142);
        w2.Purse.Money = 100000000;
        MarketEngine e = NewMarket(142, 60, 1, 0, w2);
        int d2 = SessionDay(e, 2, 10);
        e.State.Account.Margin = true;
        e.State.Account.Cash = 100000;
        Stock t = Liquid(e, MarketSize.Large);
        Trade(e, t, OrderSide.Buy, 190000L * 100 / Math.Max(1, t.Last));
        t.Mis -= 0.48;
        t.Last = t.MidCents;
        RunTo(e, d2);
        AccountFigures call = StocksFigures.Account(e.State, e.State.Phase, 12345);
        Check(call.Margin && call.Called && call.CallDay == e.State.Account.CallDay && call.CallAmount == e.State.Account.CallAmount && call.CallAmount > 0 && call.CallDay > d2,
            "stocks account: a margin call after a fall is read with its amount and the day it must be met by (" + call.CallAmount + " by day " + call.CallDay + ")");
        Check(call.Equity == MarketTrading.Equity(e.State, e.State.Phase) && call.Need == MarketTrading.Maintenance(e.State, e.State.Phase) && call.Equity < call.Need && call.Power >= 0 && call.Withdrawable == 12345,
            "stocks account: a margin account says its equity, what it must keep (more than it has: that is the call), its buying power and what can be taken out");
        AccountFigures plain = StocksFigures.Account(new MarketState(), 0, 0);
        Check(!plain.Margin && !plain.Called && plain.Equity == 0 && plain.Need == 0 && plain.Commission == 0, "stocks account: a cash account has no margin lines");
    }

    // ------------------------------------------------------------------------------------------------ alerts

    private static void StocksAlertChecks()
    {
        var book = new ChartBook();
        Check(book.SetAlert(7, AlertKind.Above, 5300, 5000) == AlertWhy.None && book.Alerts.Count == 1 && book.Alerts[0].Kind == AlertKind.Above && book.Alerts[0].Cents == 5300
              && book.SetAlert(7, AlertKind.Below, 4100, 5000) == AlertWhy.None && book.Alerts[1].Kind == AlertKind.Below && book.Alerts[1].Cents == 4100,
            "stocks alerts: the player sets the level, over the price or under it, and any level is kept as set");
        Check(book.SetAlert(7, AlertKind.Above, 4900, 5000) == AlertWhy.BadLevel && book.SetAlert(7, AlertKind.Below, 5100, 5000) == AlertWhy.BadLevel && book.SetAlert(7, AlertKind.Below, 0, 5000) == AlertWhy.BadLevel
              && book.SetAlert(7, AlertKind.Support, 4000, 5000) == AlertWhy.BadLevel && book.Alerts.Count == 2,
            "stocks alerts: a level the price is on or past, and the support kind set by hand, are refused and nothing is kept");
        Check(book.CheckAlert(7, AlertKind.Above, 5500, 5000) == AlertWhy.None && book.Alerts.Count == 2, "stocks alerts: the sheet asks whether a level would be set without setting it");
        for (int i = book.Alerts.Count; i < ChartBook.AlertsMost; i++)
        {
            book.SetAlert(7, AlertKind.Above, 6000 + i, 5000);
        }
        Check(book.SetAlert(7, AlertKind.Above, 9000, 5000) == AlertWhy.Full && book.CheckAlert(7, AlertKind.Above, 9000, 5000) == AlertWhy.Full, "stocks alerts: the player keeps no more than " + ChartBook.AlertsMost);
        // A level line under the price: an alert that follows the line, only for a level line and a line under the price.
        var lines = new ChartBook();
        lines.AddDrawing(new ChartDrawing { StockId = 7, Kind = ChartMark.Trend, FromCents = 4000, ToCents = 4200 });
        lines.AddDrawing(new ChartDrawing { StockId = 9, Kind = ChartMark.Level, FromCents = 3000, ToCents = 3000 });
        lines.AddDrawing(new ChartDrawing { StockId = 7, Kind = ChartMark.Level, FromCents = 4400, ToCents = 4400 });
        lines.AddDrawing(new ChartDrawing { StockId = 7, Kind = ChartMark.Level, FromCents = 5600, ToCents = 5600 });
        Check(lines.SetLineAlert(7, 0, 5000) == AlertWhy.NoLine && lines.SetLineAlert(7, 5, 5000) == AlertWhy.NoLine && lines.SetLineAlert(7, 2, 5000) == AlertWhy.BadLevel && lines.Alerts.Count == 0,
            "stocks alerts: a trend line, a line that is not there and a level over the price give no support alert");
        Check(lines.SetLineAlert(7, 1, 5000) == AlertWhy.None && lines.Alerts.Count == 1 && lines.Alerts[0].Kind == AlertKind.Support && lines.Alerts[0].Drawing == 2 && lines.LevelOf(2, 7) == 4400
              && lines.SetLineAlert(7, 1, 5000) == AlertWhy.None && lines.Alerts.Count == 1,
            "stocks alerts: the company's second drawing is the level it is counted by, not another company's, and a second alert under one line is not made");
        lines.RemoveDrawing(7, 1);
        Check(lines.Alerts.Count == 0, "stocks alerts: the alert under a line goes with the line");
        // Typed prices.
        Check(StocksSheetState.TypedPrice("56.4", 1, 100000, 7) == 5640 && StocksSheetState.TypedPrice("56,40", 1, 100000, 7) == 5640 && StocksSheetState.TypedPrice("120", 1, 100000, 7) == 12000
              && StocksSheetState.TypedPrice("", 1, 100000, 7) == 7 && StocksSheetState.TypedPrice("5.678", 1, 100000, 7) == 567 && StocksSheetState.TypedPrice("99999", 1, 100000, 7) == 100000
              && StocksSheetState.TypedPrice("0", 1, 100000, 7) == 1,
            "stocks alerts: a price typed in dollars is cents, with a comma for a point, kept between its ends, and an empty text keeps the level");

        // Through the real market: an alert at a set level fires when the price reaches it, and so does one under a drawn line.
        for (int pass = 0; pass < 3; pass++)
        {
            var world = new MarketWorld(9031 + (ulong)pass);
            world.Purse.Money = 100000000;
            MarketEngine m = NewMarket(9031 + (ulong)pass, 60, 1, 0, world);
            int day = SessionDay(m, 3, 10);
            Stock s = Liquid(m, MarketSize.Large);
            ChartBook cb = m.State.Charts;
            int level = s.Last * (pass == 0 ? 112 : 93) / 100;
            if (pass == 2)
            {
                cb.AddDrawing(new ChartDrawing { StockId = s.Id, Kind = ChartMark.Level, FromCents = level, ToCents = level, FromIndex = day, ToIndex = day });
            }
            AlertWhy why = pass == 0 ? cb.SetAlert(s.Id, AlertKind.Above, level, s.Last) : pass == 1 ? cb.SetAlert(s.Id, AlertKind.Below, level, s.Last) : cb.SetLineAlert(s.Id, 0, s.Last);
            RunTo(m, day, MarketCatalogue.PhaseOpen + 12);
            bool early = cb.Alerts.Count > 0 && cb.Alerts[0].Hit;
            s.Mis += pass == 0 ? 0.4 : -0.4;   // the price moves through the level
            s.Last = s.MidCents;
            RunTo(m, MarketTrading.NextTradingDay(day), MarketCatalogue.PhaseOpen + 6);
            bool hit = cb.Alerts.Count > 0 && cb.Alerts[0].Hit && cb.Alerts[0].HitDay >= day;
            bool told = cb.TakeHit(out int stock, out int cents, out int kind);
            string name = pass == 0 ? "at a set level over the price" : pass == 1 ? "at a set level under the price" : "under a drawn support line";
            Check(why == AlertWhy.None && !early && hit && told && stock == s.Id && kind == (pass == 0 ? 0 : pass == 1 ? 1 : 2) && (pass == 0 ? cents >= level : cents <= level),
                "stocks alerts: an alert " + name + " fires on the candle that reaches it, once, with the director's notice waiting (" + why + ", early " + early + ", hit " + hit + ", " + cents + " against " + level + ")");
        }
    }

    // ------------------------------------------------------------------------------------------------ the screen's commands

    // The commands the Stocks screen can press, by name. Their numbers are not unique across the kit (the app window's navigation, the decision card's choice and
    // the dashboard's tabs are all 1), so a handler must tell a press by its page and these must not collide among themselves.
    private static (string name, int command)[] StocksRegistry() => new (string, int)[]
    {
        ("Close", UiDraw.CloseCommand), ("Nav", UiAppWindow.NavCommand), ("TableHead", UiDataTable.First + UiDataTable.Head), ("TableFilter", UiDataTable.First + UiDataTable.Filter),
        ("TableSearch", UiDataTable.First + UiDataTable.Search), ("TableRow", UiDataTable.First + UiDataTable.Row), ("TableClear", UiDataTable.First + UiDataTable.Clear),
        ("Chart.Point", UiChart.First + UiChart.Point), ("ChartDesk", UiChartDesk.Command), ("ChartDesk.Rail", UiChartDesk.Rail), ("Statement.Period", UiStatement.First + UiStatement.Period),
        ("Statement.Group", UiStatement.First + UiStatement.Group), ("Statement.Line", UiStatement.First + UiStatement.Line), ("Track", UiTrack.First + UiTrack.Step),
        ("Ticket.Button", UiDecision.ButtonCommand),
        ("Lay.Less", StocksLay.Less), ("Lay.More", StocksLay.More), ("Lay.Place", StocksLay.Place), ("Lay.Alert", StocksLay.Alert), ("Lay.Empty", StocksLay.Empty), ("Lay.Shares", StocksLay.Shares),
        ("Lay.Act", StocksLay.Act), ("Lay.PriceLess", StocksLay.PriceLess), ("Lay.PriceMore", StocksLay.PriceMore), ("Lay.PricePlain", StocksLay.PricePlain), ("Lay.Watch", StocksLay.Watch),
        ("Lay.View", StocksLay.View), ("Lay.OrderRow", StocksLay.OrderRow),
        ("Second.Head", StocksLay.Second + UiDataTable.Head), ("Second.Filter", StocksLay.Second + UiDataTable.Filter), ("Second.Search", StocksLay.Second + UiDataTable.Search),
        ("Second.Row", StocksLay.Second + UiDataTable.Row), ("Second.Clear", StocksLay.Second + UiDataTable.Clear),
        ("Co.Range", StocksCompanyLay.Range), ("Co.Type", StocksCompanyLay.Type), ("Co.Chip", StocksCompanyLay.Chip), ("Co.AddIndicator", StocksCompanyLay.AddIndicator),
        ("Co.Pick", StocksCompanyLay.Pick), ("Co.Big", StocksCompanyLay.Big), ("Co.Side", StocksCompanyLay.Side), ("Co.Kind", StocksCompanyLay.Kind), ("Co.Life", StocksCompanyLay.Life),
        ("Co.AlertAdd", StocksCompanyLay.AlertAdd), ("Co.PutBack", StocksCompanyLay.PutBack), ("Co.AlertLine", StocksCompanyLay.AlertLine),
        ("Sheet.Pick", StocksSheetLay.Pick), ("Sheet.Switch", StocksSheetLay.Switch), ("Sheet.Less", StocksSheetLay.Less), ("Sheet.More", StocksSheetLay.More), ("Sheet.Type", StocksSheetLay.Type),
        ("Sheet.Ok", StocksSheetLay.Ok), ("Sheet.Cancel", StocksSheetLay.Cancel), ("Sheet.Extra", StocksSheetLay.Extra), ("Sheet.Plain", StocksSheetLay.Plain)
    };

    // A recording source: it knows what each drawn command is and fails when an action reaches it as another kind than the one drawn (the career's Apply that
    // set a filter was a source that said yes to everything). Every action of every state of the screen is pressed at it.
    private sealed class StocksRecorder
    {
        private readonly Dictionary<int, string> _kinds = new Dictionary<int, string>();
        public readonly List<string> Wrong = new List<string>();
        public int Pressed;

        public StocksRecorder((string name, int command)[] registry)
        {
            foreach ((string name, int command) in registry)
            {
                if (_kinds.TryGetValue(command, out string other))
                {
                    Wrong.Add("number " + command + " is both " + other + " and " + name);
                }
                else
                {
                    _kinds[command] = name;
                }
            }
        }

        // The press as the screen would receive it: a number and an argument, and the kind drawn behind it.
        public void Press(string state, int command, int arg, int count)
        {
            Pressed++;
            if (!_kinds.ContainsKey(command))
            {
                Wrong.Add(state + ": a drawn command " + command + " is of no kind of the screen");
            }
            else if (arg < 0 || arg > 100000 || (count >= 0 && arg >= count))
            {
                Wrong.Add(state + ": " + _kinds[command] + " reaches the screen with argument " + arg + " of " + count);
            }
        }
    }

    private static void StocksCommandChecks()
    {
        var recorder = new StocksRecorder(StocksRegistry());
        Check(recorder.Wrong.Count == 0, "stocks commands: no two kinds of action on the desk share a number (" + string.Join("; ", recorder.Wrong) + ")");
        string root = FindRoot();
        string dir = System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "Stocks");
        string screen = string.Join("\n", System.IO.Directory.GetFiles(dir, "StocksScreen*.cs").Select(System.IO.File.ReadAllText));
        // Every command the desk can press has a place in the screen that reads it: the name of each is in the screen's source.
        var dead = new List<string>();
        foreach ((string name, int command) in StocksRegistry())
        {
            string[] parts = name.Split('.');
            string[] words = name.StartsWith("Lay.") ? new[] { "StocksLay." + parts[1] } : name.StartsWith("Co.") ? new[] { "StocksCompanyLay." + parts[1] }
                : name.StartsWith("Sheet.") ? new[] { "StocksSheetLay." + parts[1] } : name.StartsWith("Second.") ? new[] { "StocksLay.Second" }
                : name == "Nav" ? new[] { "UiAppWindow.NavCommand" } : name.StartsWith("Table") ? new[] { "UiDataTable.First" } : name == "ChartDesk" ? new[] { "UiChartDesk.Command" }
                : name == "ChartDesk.Rail" ? new[] { "UiChartDesk.Rail" } : name == "Ticket.Button" ? new[] { "UiDecision.ButtonCommand" } : null;
            if (words != null && !words.Any(w => screen.IndexOf(w, StringComparison.Ordinal) >= 0) && name != "Lay.Empty" && name != "Lay.Alert" && !name.StartsWith("Statement") && name != "Track" && name != "Chart.Point")
            {
                dead.Add(name);
            }
        }
        Check(dead.Count == 0, "stocks commands: every command of the desk has a handler in the screen (" + string.Join(", ", dead) + " not)");
        // Every drawn action of every state of the desk is pressed at the recording source.
        int states = 0;
        foreach (string state in UiSamples.StockStates)
        {
            var scene = new UiScene();
            StocksLay.Lay(scene, UiSamples.Stocks((key, english) => english, state), UiFrame.FullMenu(16f / 9f, "Stocks"), true, UiDefaultKeys.Keyboard);
            states++;
            for (int i = 0; i < scene.NodeCount; i++)
            {
                UiNode n = scene.Nodes[i];
                recorder.Press(state, n.Command, n.Arg, -1);
            }
        }
        Check(recorder.Wrong.Count == 0 && recorder.Pressed > 400 && states >= 20, "stocks commands: every action drawn in " + states + " states reaches the desk as the kind drawn (" + recorder.Pressed + " presses; "
            + string.Join("; ", recorder.Wrong.Take(5)) + ")");
    }

    // The new pages and states, laid by the kit's own layout and read for what they say and what they press.
    private static void StocksScreenChecks()
    {
        UiWords english = (key, en) => en;
        var texts = new Dictionary<string, string>();
        foreach (string state in new[] { "bots_figures", "portfolio_pays", "portfolio_account", "portfolio_call", "market_orders", "company_full", "company_alertline", "company_sell", "sheet_alert", "sheet_ticket", "history", "listing" })
        {
            var scene = new UiScene();
            StocksLay.Lay(scene, UiSamples.Stocks(english, state), UiFrame.FullMenu(16f / 9f, "Stocks"), true, UiDefaultKeys.Keyboard);
            texts[state] = TextsOf(scene);
        }
        Check(texts["bots_figures"].Contains("Gain realised today") && texts["bots_figures"].Contains("Gain realised this week") && !texts["bots_figures"].Contains(">Today<"),
            "stocks screens: the Bots page says its figures are the gain realised today and this week");
        Check(texts["portfolio_pays"].Contains("Income over the next year") && texts["portfolio_pays"].Contains("Payments") && texts["portfolio_pays"].Contains("Pays"),
            "stocks screens: the Portfolio's Pays view says the income of the year and the payments to come");
        Check(texts["portfolio_account"].Contains("Commission paid") && texts["portfolio_account"].Contains("Dividends paid to you") && texts["portfolio_account"].Contains("Buying power"),
            "stocks screens: the Portfolio's Account view says the commission, the dividends and a margin account's lines");
        Check(texts["portfolio_call"].Contains("Margin call"), "stocks screens: a margin call is the first line of the Account view");
        Check(texts["market_orders"].Contains("Your orders") && texts["market_orders"].Contains("5 days"), "stocks screens: the Market has its open orders and a five day spark for each company");
        Check(texts["company_full"].Contains("Set an alert") && texts["company_full"].Contains("Pick a line first") && !texts["company_full"].Contains("+5%") && !texts["company_full"].Contains("NYSE"),
            "stocks screens: the company page sets an alert at a level the player chooses, and has no exchange of a made-up name");
        Check(texts["company_alertline"].Contains("Alert on the line"), "stocks screens: with a level line selected the alert under it can be set");
        Check(texts["company_sell"].Contains("Position after") && texts["company_sell"].Contains("600 at $44.10") && !texts["company_sell"].Contains("$88.20"),
            "stocks screens: a sale of half a holding shows the position after at the average cost, not twice it");
        Check(texts["sheet_alert"].Contains("Set a price alert") && texts["sheet_alert"].Contains("Level"), "stocks screens: the alert sheet has a level to set");
        Check(texts["sheet_ticket"].Contains("Buy shares of") && texts["sheet_ticket"].Contains("Commission") || texts["sheet_ticket"].Contains("Fee"), "stocks screens: the order's own question is a sheet with the cost, the fee and the total");
        // Each view of the Portfolio is a different page, and the held one is not one of the others.
        Check(texts["portfolio_pays"] != texts["portfolio_account"] && texts["portfolio_pays"] != TextsOf(LayStocks("portfolio_full")) && texts["portfolio_account"] != TextsOf(LayStocks("portfolio_full")),
            "stocks screens: Held, Pays and Account are three pages, not one");
        // The sample's presses on the Portfolio views and the orders box have arguments inside what is drawn.
        var recorder = new StocksRecorder(StocksRegistry());
        foreach (string state in new[] { "portfolio_pays", "portfolio_account", "market_orders", "company_sell", "bots_figures" })
        {
            UiScene scene = LayStocks(state);
            StocksModel m = UiSamples.Stocks(english, state);
            for (int i = 0; i < scene.NodeCount; i++)
            {
                UiNode n = scene.Nodes[i];
                int count = n.Command == StocksLay.View ? m.ViewLabels.Length : n.Command == StocksLay.OrderRow ? m.OrderLines.Length : n.Command == StocksLay.Act ? m.Actions.Length : -1;
                recorder.Press(state, n.Command, n.Arg, count);
            }
        }
        Check(recorder.Wrong.Count == 0, "stocks screens: the view, the order rows and the page buttons of the new states are pressed with an argument of what is drawn (" + string.Join("; ", recorder.Wrong.Take(4)) + ")");
    }

    private static UiScene LayStocks(string state)
    {
        var scene = new UiScene();
        StocksLay.Lay(scene, UiSamples.Stocks((key, en) => en, state), UiFrame.FullMenu(16f / 9f, "Stocks"), true, UiDefaultKeys.Keyboard);
        return scene;
    }
}
