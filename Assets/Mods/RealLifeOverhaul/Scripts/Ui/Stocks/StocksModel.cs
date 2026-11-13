namespace RealLifeOverhaul.Ui
{
    /// <summary>Which page of the Stocks desk is up.</summary>
    public enum StocksPage : byte
    {
        Market = 0,
        Companies,
        Watch,
        Portfolio,
        Orders,
        History,
        Bots,
        Listing,
        Bonds,
        Futures,
        Company,
        Ticket,
        Index
    }

    /// <summary>One labelled figure of the ticket, the position or a signal: the label says what the number is.</summary>
    public struct StocksRow
    {
        public string Label, Value;
        public UiRole Tone;               // the value's colour: None for plain, OkText or DangerText for a gain or a loss
    }

    /// <summary>One line the chart reads: a dot in its tone and the words. The indicator it speaks of is on the chart.</summary>
    public struct StocksSignal
    {
        public string Words;
        public UiRole Tone;
        public StocksInd Ind;             // the indicator it reads, or Count when it reads a drawing
    }

    /// <summary>The order ticket's figures, already in the game's words. Plain data.</summary>
    public sealed class StocksTicket
    {
        public string Side, Kind, Life, Shares, Limit, Cost, Fee, Total, CashAfter, After, Max;
        public bool CanLess = true, CanMore = true;
        public string Place, Instead;
        public bool PlaceDisabled;
        public string Impact;                         // "Its size moves the price 1.2% against it" when the order is big enough to matter, else null
        public string Price, Why, Watch, PutBack;     // a limit or stop price (null at the market), why it cannot be placed, the watch button, the share of dividends put back
        // The order panel of the company page: segmented choices and labelled rows.
        public string Title, CashWords;               // "Order" and "Cash $1.9B"
        public string[] Sides = new string[0], Kinds = new string[0], Lives = new string[0];
        public int SideAt, KindAt, LifeAt;
        public string LifeLabel, SharesLabel, PriceLabel, PutBackLabel;
        public StocksRow[] Rows = new StocksRow[0];   // price, cost, fee, total and the most that can be bought or sold
        public int MostRow = -1;                      // which of the rows is that most
        public long Most;                             // the most shares the cash buys (or the holding sells), the number the row shows
        public bool Buying = true;
    }

    /// <summary>
    /// What the Stocks desk shows. Words are already resolved. No colour of its own. The screen and the samples fill it;
    /// the layout only draws it.
    /// </summary>
    public sealed class StocksModel
    {
        public const int NewsShown = 3;                            // the newest pieces of news the Market page carries; the Ledger's markets page has the rest
        public string Title = "Stocks";
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int NavAt;
        public string[] Groups = new string[0];
        public int[] GroupAt = new int[0];   // the first entry of each group
        public StocksPage Page;
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public StocksCompany Co;                                   // the company page
        public UiDataTableModel Table;
        public UiChartModel Chart;
        public UiStatementModel Statement;
        public UiTrackModel Track;
        public UiHeroModel Hero;
        public UiFirmHeadModel Head;
        public UiDecisionModel Decision;
        public float[] Donut;
        public string[] DonutLabels = new string[0];
        public string[] DonutValues = new string[0];
        public string Note;
        public StocksTicket Ticket;
        public string TapeTitle;
        public string[] Tape = new string[0];
        public string OrdersTitle, OrdersCount;                    // the Market's open-orders box: "Your orders" and "2 open"
        public string[] OrderLines = new string[0], OrderValues = new string[0];   // what each open order is, and its value
        public string[] ViewLabels = new string[0];                // the Portfolio's views: Held, Pays, Account
        public int View;                                           // which one is up
        public readonly float[] ViewEdges = new float[8];          // written back by the layout: the edges of the segmented choice
        public string RowsTitle, Banner;                           // a page's card of labelled rows, and the line over it (a margin call)
        public StocksRow[] Rows = new StocksRow[0];
        public UiRole BannerTone = UiRole.WarnText;
        public string EmptyTitle, EmptyBody;
        public UiIcon EmptyIcon = UiIcon.ChartLineUp;
        public string PromptBack, PromptMove, PromptSelect, PromptPlace, PromptOther;
        public UiFormRow[] Form = new UiFormRow[0];
        public string RateLine;                                    // the bank rate and inflation, under the Market's figures and the index page's chart
        public string NewsTitle;                                   // the Market's news card: its title, its lines (newest first) and whether the lines are the no-news note
        public string[] News = new string[0];
        public bool NewsQuiet;
        public string Status;                                      // the market's state, over the page's actions
        public UiActionModel[] Actions = new UiActionModel[0];     // the page's own buttons: pay a call, add a rule, deposit
        public string TableTitle, Table2Title;                     // the capitals over a table, when the page holds two
        public UiDataTableModel Table2;                            // the lower table of a page of two: what is held under what is on offer
        public StocksSheetModel Sheet;                             // the sheet over the page
        public int ClipOne = -1, ClipTwo = -1, ListOne = -1, ListTwo = -1;                     // written back by the layout: the scroll regions of the tables
    }
}
