using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>Why the market could not take a firm or a startup now.</summary>
    public enum StockWhy
    {
        None = 0,
        Off,        // the market is not running here, or this machine follows another player's
        Already,    // the firm is on the market already
        TooMany,    // as many of the player's firms, or of the city's startups, as the market holds
        Full        // the market holds as many companies as it may
    }

    /// <summary>
    /// A firm of the player's that listed, asking for its stock: its cap table and its listing as the market needs them, a share's price and the money
    /// at today's prices. The firm's place is how the market finds it again. Game-free.
    /// </summary>
    public struct StockAsk
    {
        public string Place, Name;
        public long Shares;               // in issue, every line of the cap table's
        public long Founder;              // the player's line
        public long Float;                // the public's line
        public int PriceCents;            // a share, today's prices
        public int LockDay;               // the player sells nothing before this day
        public int ListedDay;             // the day the firm listed (the exit's)
        public long VestBase;             // the player's shares still vesting...
        public int VestDay, VestCliff, VestSpan;   // ...from this day, with its cliff and its end this many days on
        public long Debt;                 // the bridge loan, cents at today's prices
    }

    /// <summary>A startup of the city that listed, asking for a company of the market's: its turnover at the start's prices, its cap table's shares and its listing price. Game-free.</summary>
    public struct StartupAsk
    {
        public string Name;
        public int Venture;               // what it does (Venture)
        public long ArrCents;             // recurring revenue a year, cents at the start's prices
        public long Shares;
        public int PriceCents;            // a share, today's prices
    }

    /// <summary>What the market says of a firm's stock now. Game-free.</summary>
    public struct StockInfo
    {
        public bool Live;                 // on the market and listed
        public StockWhy Why;              // when it is not: why the market would not take it
        public int Id;
        public string Ticker, Name;
        public long Shares, Founder, Float;
        public int Last;                  // a share, cents at today's prices
        public int LockDay;
    }

    /// <summary>One firm's stock as the tech sector remembers it: that it was made, for which listing, and how it ended. Saved in <see cref="ExitStockStore"/>. Game-free.</summary>
    public sealed class StockFirm
    {
        public string Place = string.Empty;
        public int ListDay;               // the listing (FirmExit.ListDay) the stock was made for
        public int OpenDay;               // the day it came to the market
        public int EndDay;                // the day it ended; 0 while it trades
        public int Ended;                 // StockEnd...: how it ended
        public bool Old;                  // made for a firm that listed before there was a stock
        public string Ticker = string.Empty;   // its ticker, to name it while the market cannot be asked

        public bool Live => OpenDay > 0 && EndDay == 0;
    }

    /// <summary>How a firm's stock ended. Saved as a number.</summary>
    public static class StockEnd
    {
        public const int None = 0, WoundDown = 1, Sold = 2, Gone = 3;
    }

    /// <summary>Everything the tech sector keeps of its firms on the market. Game-free.</summary>
    public sealed class StockFile
    {
        public int LastDay = -1;
        public readonly List<StockFirm> Firms = new List<StockFirm>(4);

        /// <summary>The newest record of a firm.</summary>
        public StockFirm Of(string place)
        {
            StockFirm found = null;
            for (int i = 0; i < Firms.Count; i++)
            {
                if (Firms[i].Place == place && (found == null || Firms[i].OpenDay >= found.OpenDay))
                {
                    found = Firms[i];
                }
            }
            return found;
        }

        public StockFirm Make(string place)
        {
            var f = new StockFirm { Place = place };
            Firms.Add(f);
            return f;
        }
    }
}
