namespace RealLifeOverhaul.Ui
{
    /// <summary>An indicator on the chart, as the chip says it: its colour, its name with its settings, and an X that takes it off.</summary>
    public struct StocksChip
    {
        public string Label;
        public UiRole Tint;
        public StocksInd Ind;
    }

    /// <summary>One alert of the company: its words ("Above $56.00") and whether it still waits.</summary>
    public struct StocksAlertRow
    {
        public string Words, State;
    }

    /// <summary>The company's figures as two columns of labelled rows and the newest quarters' results under them. Plain data.</summary>
    public sealed class StocksFacts
    {
        public StocksRow[] Left = new StocksRow[0], Right = new StocksRow[0];
        public string[] ResultHead = new string[0];
        public string[][] Results = new string[0][];      // a row a quarter, newest first: quarter, day, turnover, profit, earnings, expected, dividend
        public bool[] Beat = new bool[0];                 // earnings at or over what was expected: green, else red
    }

    /// <summary>
    /// The company page: its head, the chart's toolbar, the chart and the order panel. Words are already the player's; the
    /// layout draws it and the screen answers it. The ticket, the chart and the signals are built from the market once for a change,
    /// and a hover or a pan only lays them out again. Plain data.
    /// </summary>
    public sealed class StocksCompany
    {
        // The head.
        public string Name, Ticker, Sector, Meta;           // Meta: "Mid cap"
        public UiRole LogoTint = UiRole.Chart1;
        public string Price, ChangeMoney, ChangePercent;    // "$53.42", "+$0.69", "+1.31%"
        public int Way;                                     // 1 up, -1 down, 0 flat
        public string[] FactLabels = new string[0], FactValues = new string[0];
        public bool Watching;

        // The chart's toolbar.
        public string[] Ranges = new string[0];
        public int Range;
        public string[] Types = new string[0];
        public int Type;
        public StocksChip[] Chips = new StocksChip[0];
        public string AddLabel;                             // "+ Indicator"
        public bool Big;                                    // the chart is open across the window
        public string[] Views = new string[0];              // the page's two views: the chart and the figures
        public int View;                                    // which one is up
        public StocksFacts Facts;                           // the figures view's rows, built while it is up

        // The indicator picker.
        public bool PickOpen;
        public string[] PickNames = new string[0], PickHints = new string[0];
        public bool[] PickOn = new bool[0];

        // The chart, the order, the position, what the chart says and the price alerts.
        public UiChartDeskModel Chart;
        public StocksTicket Order;
        public string PositionTitle, PositionNone;
        public StocksRow[] Position = new StocksRow[0];
        public string[] Notes = new string[0];              // what the company is in a few words (a bid, a split, its results day) and what a property trust pays
        public string SignalsTitle, SignalsNone;
        public StocksSignal[] Signals = new StocksSignal[0];
        public string AlertsTitle, AlertsNone, AlertSet, AlertLine;   // the card's title, its empty line, "Set an alert" and "Alert below this line"
        public StocksAlertRow[] Alerts = new StocksAlertRow[0];
        public bool AlertsFull;                             // no room for another alert
        public bool AlertLineOff;                           // no level line is selected to stand an alert under

        // Written back by the layout, for the pointer: the edges of each segmented control and the room of the chart.
        public readonly float[] RangeEdges = new float[8];
        public readonly float[] TypeEdges = new float[8];
        public readonly float[] SideEdges = new float[8];
        public readonly float[] KindEdges = new float[8];
        public readonly float[] LifeEdges = new float[8];
        public readonly float[] ViewEdges = new float[8];
        public float PickX, PickY;                          // where the picker stands
    }
}
