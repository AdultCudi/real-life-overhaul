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
    }
}
