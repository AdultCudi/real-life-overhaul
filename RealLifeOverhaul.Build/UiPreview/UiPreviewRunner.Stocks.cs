using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The Stocks desk over a street, one state at a time. Backdrops are the day street, the other street and the night street.
    static partial void AddStocks(List<Screen> screens)
    {
        foreach (string state in UiSamples.StockStates)
        {
            string st = state;
            bool ticket = st == "ticket";
            bool sheet = st.StartsWith("sheet_");
            bool empty = st == "watch_empty" || st == "portfolio_empty";
            var screen = new Screen
            {
                Name = "stocks_" + st,
                AppName = "Stocks",
                Icon = UiIcon.ChartLineUp,
                Backdrop = st == "market_crash" || st == "history" || st == "listing" || st == "company_months" ? "11.jpg" : st == "company_weeks" || st == "portfolio_full" || st == "futures" || st == "watch" ? "02.jpg" : "13.jpg",
                Bar = () => UiSamples.ShellBar(System.Array.IndexOf(UiSamples.RowIds, "RealLifeStocks"), false),
                Lay = (s, f, d, k) => StocksLay.Lay(s, UiSamples.Stocks(English, st), f, d, k),
                HoverCommand = sheet ? StocksSheetLay.Cancel : ticket ? UiDecision.ButtonCommand : empty ? StocksLay.Empty : UiAppWindow.NavCommand,
                HoverArg = 0,
                FocusCommand = sheet ? StocksSheetLay.Cancel : ticket ? UiDecision.ButtonCommand : UiAppWindow.NavCommand,
                FocusArg = 0,
                Walk = Moves("DDU")
            };
            screens.Add(screen);
        }
    }
}
