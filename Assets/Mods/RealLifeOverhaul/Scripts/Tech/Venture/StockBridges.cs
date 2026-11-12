using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What a firm that listed asks of the stock market, filled in by Wiring/ExitStockLinks. Each is optional: without it the firm lists as it always did, with
    /// no stock, and the rest works. Asked on a game day, a click or a deal, never per frame.
    /// </summary>
    public static class StockBridges
    {
        /// <summary>True while a firm can be put on the market from here: the market runs, plays alone, and this machine's player owns it.</summary>
        public static Func<bool> Open;

        /// <summary>The firm on the market now. The answer is live, or says why not.</summary>
        public static Func<StockAsk, StockInfo> Make;

        /// <summary>A firm's stock as the market has it; not live when it has none.</summary>
        public static Func<string, StockInfo> Look;

        /// <summary>A firm's stock ends: its id, how (0 failed, 1 taken over, 2 delisted) and what each share is paid out at, cents at today's prices. True if it was listed.</summary>
        public static Func<int, int, int, bool> End;

        /// <summary>Shares of a firm's that are neither the player's nor the public's made (or, under nothing, cancelled): the stock's id and the shares.</summary>
        public static Func<int, long, bool> Grow;

        /// <summary>The player's unvested shares of a firm taken back, as a bad leaver's are: the stock's id and the day the vesting is counted at. Returns how many.</summary>
        public static Func<int, int, long> Cancel;

        /// <summary>The firms of the player's still listed, by place: any stock of theirs for a firm that is not among them leaves the market. Returns how many left.</summary>
        public static Func<System.Collections.Generic.IList<string>, int> Sweep;

        /// <summary>A startup of the city that listed, as a company of the market's. Null for the reason it could not (None: it is on the market).</summary>
        public static Func<StartupAsk, StockWhy> Startup;

        /// <summary>The Stocks app opened on a stock's page.</summary>
        public static Action<int> Show;

        public static bool Ready => Open != null && Make != null && Look != null;

        public static bool OpenNow()
        {
            try
            {
                return Open != null && Open();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A firm's stock as the market has it; a stock that is not there when the market cannot be asked.</summary>
        public static StockInfo LookNow(string place)
        {
            try
            {
                return Look != null ? Look(place) : default(StockInfo);
            }
            catch (Exception)
            {
                return default(StockInfo);
            }
        }

        public static void Reset()
        {
            Open = null;
            Make = null;
            Look = null;
            End = null;
            Grow = null;
            Cancel = null;
            Sweep = null;
            Startup = null;
            Show = null;
        }
    }
}
