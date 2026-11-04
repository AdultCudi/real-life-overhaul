using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _stocksChecked;

    static partial void UiCasesStocks(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "stocks")
        {
            return;
        }
        foreach (string state in UiSamples.StockStates)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def,
                Name = "stocks (" + st + ")",
                Title = "Stocks",
                Commands = StocksCommands,
                Lay = (s, f, dark, k, t) => StocksLay.Lay(s, UiSamples.Stocks(t, st), f, dark, k)
            });
        }
        if (!_stocksChecked)
        {
            _stocksChecked = true;
            StocksRules();
        }
    }

    private static readonly int[] StocksCommands =
    {
        UiAppWindow.NavCommand, UiDataTable.First + UiDataTable.Head, UiDataTable.First + UiDataTable.Filter, UiDataTable.First + UiDataTable.Search,
        UiDataTable.First + UiDataTable.Row, UiChart.First + UiChart.Point, UiChartDesk.Command, UiStatement.First + UiStatement.Period,
        UiStatement.First + UiStatement.Group, UiStatement.First + UiStatement.Line, UiTrack.First + UiTrack.Step, UiDecision.ChoiceCommand,
        UiDecision.ButtonCommand, UiChartDesk.Rail, StocksCompanyLay.Range, StocksCompanyLay.Type, StocksCompanyLay.Chip, StocksCompanyLay.AddIndicator, StocksCompanyLay.Pick,
        StocksCompanyLay.Big, StocksCompanyLay.Side, StocksCompanyLay.Kind, StocksCompanyLay.Life, StocksCompanyLay.AlertAdd, StocksCompanyLay.PutBack,
        StocksLay.Less, StocksLay.More, StocksLay.Place,
        StocksLay.Alert, StocksLay.Empty, StocksLay.Shares, StocksLay.Act, StocksLay.PriceLess, StocksLay.PriceMore, StocksLay.PricePlain, StocksLay.Watch,
        StocksLay.Second + UiDataTable.Head, StocksLay.Second + UiDataTable.Filter, StocksLay.Second + UiDataTable.Search, StocksLay.Second + UiDataTable.Row,
        StocksLay.Second + UiDataTable.Clear, UiDataTable.First + UiDataTable.Clear, StocksSheetLay.Pick, StocksSheetLay.Switch, StocksSheetLay.Less, StocksSheetLay.More,
        StocksSheetLay.Type, StocksSheetLay.Ok, StocksSheetLay.Cancel, StocksSheetLay.Extra, StocksSheetLay.Plain
    };

    // The tape, the chart set, the rules and the book, off the layout.
    private static void StocksRules()
    {
        var log = new TradeLog();
        log.Note(new TradePrint { StockId = 75, Side = TradeSide.Buy, Size = 4000, PriceCents = 3700, Day = 12, Phase = 4, Who = TradeWho.Fund, WhoId = 0 });
        log.Note(new TradePrint { StockId = 83, Side = TradeSide.Sell, Size = 200, PriceCents = 5400, Day = 12, Phase = 5, Who = TradeWho.You, WhoId = 0 });
        Check(log.Count == 2 && log.Newest(0).StockId == 83 && log.Newest(1).Size == 4000 && log.Latest(75, out TradePrint gmf) && gmf.Who == TradeWho.Fund,
            "stocks: the tape keeps prints newest first and can name the last one in a company");
        TradeLog back = TradeLog.Read(log.Write(), out int version);
        Check(version == TradeLog.Version && back != null && back.Count == 2 && back.Newest(0).PriceCents == 5400 && TradeLog.Read("v9|1|0", out _) == null,
            "stocks: the tape's line reads back, and a newer line is not taken");
        log.NoteFlow(75, 10000, 10040, 50000, 3, 2);
        Check(log.Count == 2, "stocks: a candle that barely moved is not a print");
        log.NoteFlow(75, 10000, 10100, 50000, 3, 2);
        bool named = (75 + 3) % 11 == 2 % 11;
        Check(log.Count == (named ? 3 : 2), "stocks: a notable candle is a print only on its fixed turn, with no new draw");

        var book = new ChartBook();
        book.ToggleLayer(ChartLayer.Sma20);
        book.ToggleLayer(ChartLayer.Sma50);
        book.TogglePane(ChartPane.Rsi);
        book.TogglePane(ChartPane.Macd);
        book.TogglePane(ChartPane.Atr);
        Check(book.LayerOn(ChartLayer.Sma20) && book.LayerOn(ChartLayer.Sma50) && book.FirstPane == ChartPane.Rsi && book.SecondPane == ChartPane.Atr,
            "stocks: several overlays stay on, and a third pane replaces the second");
        book.AddAlert(new PriceAlert { StockId = 75, Kind = AlertKind.Above, Cents = 4000 });
        book.Check(75, 3900, 1);
        Check(book.HitCount == 0, "stocks: a price under an above alert does not fire");
        book.Check(75, 4000, 2);
        Check(book.HitCount == 1 && book.TakeHit(out int hitStock, out int hitCents, out _) && hitStock == 75 && hitCents == 4000 && book.HitCount == 0,
            "stocks: an alert fires once, on the step the price crosses it");

        var closes = new double[60];
        for (int i = 0; i < closes.Length; i++)
        {
            closes[i] = 1000 + i * 5;
        }
        var a = new double[60];
        var b = new double[60];
        var c = new double[60];
        var scratch = new double[60];
        ChartSignal signal = ChartBook.Signals(closes, closes.Length, a, b, c, scratch);
        Check(signal.Sma == 1, "stocks: a rising price reads as over its 50 day average");
        Check(StocksMath.PeTimes100(3700, 5000) > 0 && StocksMath.YieldBp(3700, 20) == 20 * 4 * 10000 / 3700, "stocks: P/E and yield come from the earnings and the dividend");
        Check(MarketTrading.Commission(3700, 5) == 5, "stocks: the fee is the commission, and never under its floor");

        var state = new MarketState();
        state.Rules.Add(new RuleBot { Id = 1, Kind = RuleKind.Grid, Status = RuleStatus.Running, StockId = 1, Level = 5000, Cash = 100000, Budget = 100000, PerTrade = 10, MaxOpen = 1, Owner = 1, Who = 0 });
        var stock = new Stock { Id = 1, Status = StockStatus.Listed, Last = 4000, Open = 4000 };
        for (int i = 0; i < 5; i++)
        {
            stock.Daily.Add(new Candle(i, 4000, 4100, 3900, 4000, 100));
        }
        state.Stocks.Add(stock);
        long intent = RuleBots.Intent(state.Rules[0], stock, new MarketBots.Work());
        Check(intent == 10, "stocks: a grid rule buys under its level, the same shares every time");
        RuleBots.Step(state, 4, MarketCatalogue.PhaseOpen, null, new MarketBots.Work());
        Check(state.Names.Count == 1 && state.Names.Newest(0).Who == TradeWho.Rival && state.Rules[0].Cash < 100000, "stocks: a rival runs the same rule and the tape records it, not the player's cash");

        var file = new ListingFile { LowCents = 1800, HighCents = 2200, NewShares = 100000, PriceCents = 2000 };
        BookBids.Fill(file, 99UL, 7, 2000);
        Check(file.BidCount == BookBids.Most && file.CoverBp >= 0, "stocks: the book is named bids, and the cover is their size against the offer");
        int set = BookBids.SetPrice(file, 1900);
        Check(set >= file.LowCents && set <= file.HighCents, "stocks: the founder sets the price inside the range");
        long filled = 0;
        for (int i = 0; i < file.BidCount; i++)
        {
            filled += file.Bids[i].Filled;
            if (file.Bids[i].Cents < set)
            {
                Check(file.Bids[i].Cut == BookBids.Under && file.Bids[i].Filled == 0, "stocks: a bid under the price is not filled");
            }
        }
        Check(filled <= file.NewShares, "stocks: the book does not hand out more shares than were offered");
        StocksActionChecks();
        StocksChartChecks();
    }
}
