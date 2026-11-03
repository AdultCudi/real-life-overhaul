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
        Ticket
    }

    /// <summary>The order ticket's figures, already in the game's words. Plain data.</summary>
    public sealed class StocksTicket
    {
        public string Side, Kind, Life, Shares, Limit, Cost, Fee, Total, CashAfter, After, Max;
        public bool CanLess = true, CanMore = true;
        public string Place, Instead;
        public bool PlaceDisabled;
        public string Price, Why, Watch, PutBack;     // a limit or stop price (null at the market), why it cannot be placed, the watch button, the share of dividends put back
    }

    /// <summary>
    /// What the Stocks desk shows. Words are already resolved. No colour of its own. The screen and the samples fill it;
    /// the layout only draws it.
    /// </summary>
    public sealed class StocksModel
    {
        public string Title = "Stocks";
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int NavAt;
        public string[] Groups = new string[0];
        public int[] GroupAt = new int[0];   // the first entry of each group
        public StocksPage Page;
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public UiDataTableModel Table;
        public UiChartModel Chart;
        public UiChartDeskModel Desk;
        public UiStatementModel Statement;
        public UiTrackModel Track;
        public UiHeroModel Hero;
        public UiFirmHeadModel Head;
        public UiDecisionModel Decision;
        public float[] Donut;
        public string[] DonutLabels = new string[0];
        public string[] DonutValues = new string[0];
        public string Note;
        public string[] Signals = new string[0];
        public string[] FactLabels = new string[0];
        public string[] FactValues = new string[0];
        public string[] Chips = new string[0];
        public int Tool = -1;
        public string[] Tools = new string[0];
        public StocksTicket Ticket;
        public string[] Alerts = new string[0];
        public string TapeTitle;
        public string[] Tape = new string[0];
        public string EmptyTitle, EmptyBody;
        public UiIcon EmptyIcon = UiIcon.ChartLineUp;
        public string PromptBack, PromptMove, PromptSelect, PromptPlace, PromptOther;
        public UiFormRow[] Form = new UiFormRow[0];
        public string Status;                                      // the market's state, over the page's actions
        public UiActionModel[] Actions = new UiActionModel[0];     // the page's own buttons: pay a call, add a rule, deposit
        public string TableTitle, Table2Title;                     // the capitals over a table, when the page holds two
        public UiDataTableModel Table2;                            // the lower table of a page of two: what is held under what is on offer
        public StocksSheetModel Sheet;                             // the sheet over the page
        public int ClipOne = -1, ClipTwo = -1, ListOne = -1, ListTwo = -1;                     // written back by the layout: the scroll regions of the tables
    }
}
