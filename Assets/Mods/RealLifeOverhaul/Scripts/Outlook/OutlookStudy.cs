namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// What the Economy app works out of one look at the game, in one place so the app, the Finance app's line and the tests agree:
    /// each tile's changes from the history, the stock market's from the market's own, and the verdict. Run when a page is
    /// written; a few passes over 120 days of a dozen figures, nothing allocated.
    /// </summary>
    internal static class OutlookStudy
    {
        /// <summary>The trends of a look and the verdict they give.</summary>
        public static OutlookVerdict Run(OutlookBook book, OutlookReadings r, int[] scratch)
        {
            OutlookRules.Trends(book, r, scratch);
            if (r.MarketOn)
            {
                StocksWindow(r, scratch);
                r.Short[OutlookCatalogue.TileStocks] = OutlookRules.Over(scratch, scratch.Length, OutlookCatalogue.ShortDays, r.Stocks, OutlookChange.Percent);
                r.Long[OutlookCatalogue.TileStocks] = OutlookRules.Over(scratch, scratch.Length, OutlookCatalogue.LongDays, r.Stocks, OutlookChange.Percent);
            }
            return OutlookRules.Judge(r.Cycle, r.Inflation, r.Long[OutlookCatalogue.TileIncome], r.Long[OutlookCatalogue.TileStocks]);
        }

        /// <summary>The index's closes of the last days into a window, the no-value mark where the market was shut or has no history.</summary>
        public static void StocksWindow(OutlookReadings r, int[] into)
        {
            for (int i = 0; i < into.Length; i++)
            {
                into[i] = OutlookCatalogue.NoValue;
            }
            OutlookBridges.StocksHistory history = OutlookBridges.Stocks;
            if (history != null && r.MarketOn)
            {
                history(r.Day, into.Length, into);
            }
        }
    }
}
