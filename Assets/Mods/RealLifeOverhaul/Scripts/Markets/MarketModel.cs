using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Whether a company's shares can be traded. Saved as a number.</summary>
    public enum StockStatus { Listed = 0, Gone = 2 }

    /// <summary>Why a company left the market. Saved as a number.</summary>
    public enum GoneReason { None = 0, Failed = 1, TakenOver = 2, Delisted = 3 }

    /// <summary>
    /// One candle: the first, highest, lowest and last price of a stretch of trading, and the shares traded in it.
    /// Prices in cents (an index in hundredths of a point); <see cref="Day"/> is the game day the stretch began on.
    /// </summary>
    public struct Candle
    {
        public int Day;
        public int O, H, L, C;
        public long V;

        public Candle(int day, int o, int h, int l, int c, long v)
        {
            Day = day;
            O = o;
            H = h;
            L = l;
            C = c;
            V = v;
        }

        public bool Empty => C <= 0;

        /// <summary>This candle with the next one after it: first open, highest high, lowest low, last close, volumes added.</summary>
        public Candle Then(in Candle next)
        {
            if (Empty)
            {
                return next;
            }
            if (next.Empty)
            {
                return this;
            }
            return new Candle(Day, O, Math.Max(H, next.H), Math.Min(L, next.L), next.C, V + next.V);
        }
    }

    /// <summary>A quarter's results as the company gave them out.</summary>
    public struct QuarterResult
    {
        public int Quarter;          // counted from the start of the game
        public int Day;              // the day they were given out
        public long RevenueK;        // turnover in the quarter, thousands
        public long ProfitK;         // profit after tax in the quarter, thousands
        public int Eps;              // earnings a share in the quarter, hundredths of a cent
        public int Consensus;        // what the market expected, hundredths of a cent
        public int DpsCents;         // the dividend declared with them, a share
    }

    /// <summary>
    /// One listed company as it stands: its price, its business, its results and dividends, its history.
    ///
    /// Its price is what the market makes of its business and of what it expects of it, with its mood:
    ///     ln price = LogFair + Mis, LogFair = ln(worth of the business as it stands x e^Outlook + the dividend built up)
    /// Each move of the price is partly news (it stays: at the close it goes into Outlook, which the business then bears
    /// out in its figures over the next years) and partly mood (Sent, which fades slowly). Mis is the day's moves so far
    /// on top of the mood; after each close it is the mood alone. The figures shown to the player are the quarters'
    /// results as they come out; nothing of what is still to come is decided before it happens.
    /// </summary>
    public sealed class Stock
    {
        public int Id;
        public CompanyDef Def;
        public string Name = string.Empty;
        public string Ticker = string.Empty;
        public MarketSector Sector;
        public MarketSize Size;
        public string RivalId;                   // a holding of one of the game's rivals: the game's id of the rival; else null

        public StockStatus Status;
        public int ListedDay;
        public int GoneDay;
        public GoneReason Reason;
        public int GoneCents;                    // what each share was paid out at when it left (0: nothing)

        // ---- the price
        public double LogFair;                   // ln of what the market thinks a share is worth, in money
        public double Mis;                       // ln of the price less LogFair: today's moves so far and the mood
        public double Sent;                      // the market's mood about it (ln), which fades
        public double Outlook;                   // what the market expects of the business beyond its figures (ln), which they bear out in time
        public long Shares;                      // in issue
        public double Impact;                    // the player's recent trading in it, in shares, fading
        public int PrevClose, Open, High, Low, Last;   // cents; Last is the price of the last step; PrevClose the close today's move is from (set at the open: MarketLife.DayBases)
        public long DayVolume;

        // ---- the business
        public double Rev;                      // turnover a year, in money at today's prices
        public double Margin;                   // operating profit on turnover
        public double Growth;                   // real growth a year as things stand
        public double MarginShift;               // its usual margin has moved by this for good (what the market expected of it came through)
        public double MarginTarget, GrowthTarget; // where the city put its margin and growth at the last open: its own ups and downs are kept apart from the city's
        public double Debt;                      // in money
        public double DebtRate;                  // what the debt costs a year
        public double Equity;                    // book value, in money
        public double Distress;                  // 0 to 1: how near it is to failing
        public double Competition;               // the share of its turnover the player's businesses in its trade take

        // ---- this quarter so far, and the quarter waiting for its results
        public int Quarter = -1;
        public double QRev, QNi;                 // turnover and profit after tax so far
        public int PendingQuarter = -1;
        public double PendingRev, PendingNi;
        public int ResultsDay;                   // when the pending quarter's results come out; 0 none
        public readonly List<QuarterResult> Results = new List<QuarterResult>(MarketCatalogue.QuartersKept + 1);

        // ---- dividends
        public int DpsCents;                     // the last quarterly dividend declared, a share
        public int DeclaredCents;                // a dividend declared and not yet paid
        public int RecordDay;                    // holders at the close of this day are paid it
        public int PayDay;                       // ...on this day
        public bool ExDone;                      // the price has already dropped by it
        public int LastExDay;                    // the day the last dividend left the price

        // ---- the life of the company
        public double Adv;                       // shares traded on an ordinary day
        public int SplitDay, SplitRatio;         // a split announced, effective at the open of that day
        public int BidDay, BidEndDay, BidCents;  // a takeover bid: made, to complete on, at (whether it completes is drawn on that day, never before)
        public int BidderId;                     // the listed company that bids, or 0 for a private buyer
        public int ResultsBoostDay;              // its own moves are larger on this day (its results)
        public int LowDays;                      // market days running it has closed under PennyCents

        // ---- candles
        public readonly Candle[] Bars = new Candle[MarketCatalogue.IntradayBars];   // today's, one a step
        public int BarCount;
        public readonly Candle[] LastBars = new Candle[MarketCatalogue.IntradayBars];  // the session before today's
        public int LastBarCount;
        public int LastBarsDay;
        public readonly List<Candle> Daily = new List<Candle>(MarketCatalogue.DailyKept + 1);
        public readonly List<Candle> Monthly = new List<Candle>(16);

        /// <summary>A player's own company, listed (Markets/Listing): its founder, its reserve, its board and meetings. Null for every company of the city's.</summary>
        public OwnListing Own;

        public bool Listed => Status == StockStatus.Listed;

        /// <summary>The middle price, in money.</summary>
        public double Mid => Math.Exp(LogFair + Mis);

        /// <summary>The middle price in whole cents (one at least).</summary>
        public int MidCents => Math.Max(1, (int)Math.Round(Mid * 100.0));

        /// <summary>What all its shares are worth at the last price, in money.</summary>
        public double MarketValue => Shares * (Last > 0 ? Last : MidCents) / 100.0;
    }

    /// <summary>The player's shares in one company. Shares under nothing are shares sold short (a margin account only).</summary>
    public sealed class Position
    {
        public int StockId;
        public long Shares;
        public long Cost;            // what the shares held cost, commission included (for a short: what selling them brought, less commission)
        public long Dividends;       // received (for a short: paid) so far
        public long Realised;        // gains and losses taken so far
        public bool Test;
        public int OpenedDay;
    }

    public enum OrderSide { Buy = 0, Sell = 1 }

    public enum OrderType { Market = 0, Limit = 1, Stop = 2 }

    public enum OrderLife { Day = 0, UntilCancelled = 1 }

    public enum OrderState { Open = 0, Filled = 1, Cancelled = 2, Expired = 3, Refused = 4 }

    /// <summary>Why an order was refused or stopped. Saved as a number; each has a sentence (MarketText).</summary>
    public enum OrderWhy
    {
        None = 0, Closed, Held, Behind, NoShares, NoMoney, TooLarge, HoldingLimit, NotListed, NoBorrow, NoMargin, BelowMargin, BadPrice, TooMany,
        Halted, Expired, Cancelled, CompanyGone, Forced,
        NothingHeld,    // a stop loss or a take profit with nothing (left) to close
        StopSide,       // a stop loss on the wrong side of the price: to sell it is under the bid, to buy back over the offer
        ProfitSide,     // a take profit on the wrong side: to sell it is over the bid, to buy back under the offer
        Count
    }

    /// <summary>An order to buy or sell. Prices in cents.</summary>
    public sealed class Order
    {
        public int Id;
        public int StockId;
        public OrderSide Side;
        public OrderType Type;
        public OrderLife Life;
        public long Qty;
        public long Filled;
        public long FilledValue;     // money paid or received for what is filled (before commission)
        public int LimitCents;
        public int StopCents;
        public int PlacedDay, PlacedPhase;
        public int ExpireDay;        // the last day it stands
        public OrderState State;
        public OrderWhy Why;
        public bool Triggered;       // a stop has been reached and is now an order at the market
        public bool Test;
        public bool Forced;          // the broker's own order (a margin call)
        public bool Closing;         // a stop loss (a stop) or a take profit (a limit): it only ever closes what is held, and holds nothing back for other orders

        public long Left => Qty - Filled;
        public bool IsOpen => State == OrderState.Open;
    }

    /// <summary>What a line of the account's history is.</summary>
    public enum TradeKind { Bought = 0, Sold = 1, Dividend = 2, DividendPaid = 3, Payout = 4, Interest = 5, BorrowFee = 6, Deposit = 7, Withdrawal = 8, Shortfall = 9 }

    /// <summary>One line of the account's history.</summary>
    public sealed class Trade
    {
        public int Day, Phase;
        public int StockId;
        public TradeKind Kind;
        public long Qty;
        public int PriceCents;
        public long Value;           // money in (more than nothing) or out, before commission
        public long Fee;
        public bool Test;
        public int Bot;              // a trading bot's (its firm, MarketBots), 0 the player's own
    }

    /// <summary>The player's account with the broker.</summary>
    public sealed class Account
    {
        public bool Margin;          // a margin account: may borrow and sell short
        public long Cash;            // a margin account's own money (under nothing: borrowed)
        public int CallDay;          // a margin call to be met by the close of this day; 0 none
        public long CallAmount;
        public long Commission, Interest, BorrowFees, Dividends, DividendsPaid, Realised, Payouts;
        public int OpenedDay;
    }

    /// <summary>
    /// What the city was like on a market day, as the market reads it (<see cref="MarketInputs"/>). Kept on the tape
    /// for every day, so that a day replayed after a load is the day that was played.
    /// </summary>
    public struct MarketEnv
    {
        public const int SupplyKinds = 6, LeverCount = 13, TradesMost = 12;

        public int Cycle;            // boom 0, steady 1, slowdown 2, recession 3
        public int RateBp;           // the bank rate, a year
        public int RateNextBp;       // the next decision as it can be read from prices and the bank's rule
        public int InflationBp;      // a year
        public int PriceLevel;       // tenths of a point (1000 at the start)
        public int DistrictIncome;   // the districts' mean income, tenths of a point (1000 ordinary)
        public int[] Supply;         // each kind of goods' price factor, thousandths
        public int Levers;           // the council's policies, packed: one base-3 digit each (0 relaxed, 1 as was, 2 tightened)
        public int Campaign;         // an election campaign is on
        public int Elections;        // elections counted so far
        public int Sky;              // 0 fair, 1 rain, 2 a storm
        public int Spell;            // 0 none, 1 a heat wave, 2 a cold snap, 3 ice
        public int Event;            // the street event on: as the city's economy counts them (0 none)
        public int Crimes, BreakIns, Fires, Accidents;   // reported yesterday
        public int Trades;           // how many of the next two are given
        public int[] TradeOf;        // the player's businesses' trades (MarketInputs.Trades)...
        public int[] TradeSales;     // ...and their takings yesterday, in money
        public int RivalMood;        // the game's rivals: their income against a month ago, per cent (100 the same)

        // The player's own listed company (Markets/Listing), from the game's own daily statements of its businesses: the
        // statement days counted (OwnFrom and the OwnDays after it), their takings and profit together, in money; the
        // player's Standing (0 to 100, -1 not known), what the papers made of them (tenths of a point either way) and a
        // scandal that came out; what the player owes on loans. Nothing while the player has no company on the market.
        public int OwnFrom, OwnDays;
        public long OwnRev, OwnProfit;
        public int OwnStanding;
        public int OwnPress;
        public int OwnScandal;
        public long OwnDebt;

        public static MarketEnv Quiet()
        {
            return new MarketEnv
            {
                Cycle = 1, RateBp = 300, InflationBp = 200, PriceLevel = 1000, DistrictIncome = 1000, Supply = new[] { 1000, 1000, 1000, 1000, 1000, 1000 }, Levers = Packed(null),
                TradeOf = new int[TradesMost], TradeSales = new int[TradesMost], RivalMood = 100, OwnStanding = -1
            };
        }

        /// <summary>Lever positions (-1, 0, 1 each; null all 0) packed as base-3 digits.</summary>
        public static int Packed(int[] positions)
        {
            int packed = 0;
            for (int i = LeverCount - 1; i >= 0; i--)
            {
                int p = positions != null && i < positions.Length ? positions[i] : 0;
                packed = packed * 3 + (p < 0 ? 0 : p > 0 ? 2 : 1);
            }
            return packed;
        }

        /// <summary>One lever's position: -1, 0 or 1.</summary>
        public int Lever(int lever)
        {
            int packed = Levers;
            for (int i = 0; i < lever; i++)
            {
                packed /= 3;
            }
            return packed % 3 - 1;
        }
    }

    /// <summary>Something that happened during a day that the market answers at once (a fire, a crime, a policy changed). Kept on the tape.</summary>
    public struct MarketEvent
    {
        public int Day, Phase;
        public int Kind;             // MarketInputs.Event...
        public int A, B;
    }

    /// <summary>A dividend owed to (or by, for a short) the player: worked out at the close of its record day, paid on its day.</summary>
    public struct DividendDue
    {
        public int StockId;
        public long Cents;           // the whole sum, in cents: shares held at the record day times the dividend (under nothing: owed)
        public int PayDay;
        public bool Test;
        public int Bot;              // on a trading bot's holding (its firm): paid to the player and into the bot's money; 0 the player's own
    }

    /// <summary>What kind of piece the market has for the paper or a notification. Saved as a number.</summary>
    public enum MarketNewsKind
    {
        Close = 0,          // the day's close: A the index in hundredths, B its change in bp
        Mover = 1,          // a company that moved most: Stock, A the change in bp
        Results = 2,        // results: Stock, A eps (hundredths of a cent), B consensus, C the price move in bp
        Dividend = 3,       // a dividend declared: Stock, A cents a share, B record day, C pay day
        Listing = 4,        // a company comes to the market: Stock, A the price in cents, B the day
        Failure = 5,        // a company fails: Stock, A cents a share paid out
        Bid = 6,            // a takeover bid: Stock, A cents a share, B the day it completes, C the bidder (0 private)
        BidDone = 7,        // a takeover completed: Stock, A cents a share
        BidFailed = 8,      // a takeover fell through: Stock
        Split = 9,          // a split announced: Stock, A the ratio, B the day
        Crash = 10,         // the market fell hard: A the change in bp
        Sector = 11,        // a sector moved most: A the sector, B the change in bp
        Rate = 12,          // the market answered the bank rate: A the move in bp, B the index change
        Mania = 13,         // a sector craze: A the sector, B 1 begun, 2 burst
        Crisis = 14,        // a company's crisis: Stock, A what it does to the company's worth, in bp (the price follows at the open)
        Windfall = 15,      // a company's windfall: Stock, A the same
        Delisted = 16,      // shares that traded under a unit for weeks leave the market: Stock, A cents a share paid out
        // A player's own company (Markets/Listing):
        OwnPriced = 17,     // its shares priced by the book: Stock the id it will have, A the price in cents, B its first day, C the share offered in bp
        OwnPulled = 18,     // its listing called off: A the price the book came to, B the least the player would take
        OwnDeal = 19,       // its founder bought or sold: Stock, A shares (under nothing: sold), B the price in cents
        OwnBuyBack = 20,    // it bought back shares: Stock, A shares, B the price in cents
        OwnIssue = 21,      // it sold new shares: Stock, A shares, B the price in cents
        OwnMeeting = 22,    // a meeting of its shareholders: Stock, A what passed (ListingVotes bits), B against the board in bp, C 1 for an extraordinary meeting
        OwnActivist = 23,   // an investor bought in to press a demand: Stock, A their stake in bp, B the demand (ActivistDemand), C the meeting's day
        OwnPrivate = 24,    // taken off the market by its founder: Stock, A the price paid a share
        OwnDisgorged = 25,  // the founder paid back a gain on shares bought and sold within the time the rules allow: Stock, A the money
        OwnBidBlocked = 26, // a bid for it lapsed: Stock, A 1 the founder's holding stood in its way, 0 the holders did not take it
        OwnRefused = 27,    // something its founder asked for could not be done when its moment came: Stock, A the action (OwnAction), B why (OwnWhy)
        OwnWithdrawn = 28,  // an investor dropped a demand that was met: Stock, A the demand
        OwnAway = 29,       // another player's company left this market (its founder's city is no longer this one): Stock, A cents a share paid out
        OwnAudit = 30,      // its yearly audit found the books wanting: Stock, A what it found (ListingBook bits)
        Count
    }

    /// <summary>One piece of market news.</summary>
    public struct MarketNews
    {
        public int Day;
        public MarketNewsKind Kind;
        public int Stock;
        public int A, B, C;
    }

    /// <summary>
    /// Everything the market keeps of a save: one line of the save's data (<see cref="MarketStore"/>), plus the tape
    /// outside it (<see cref="MarketTape"/>). Game-free.
    /// </summary>
    public sealed class MarketState
    {
        public ulong Id;                         // the playthrough's: the tape's name
        public int CreatedDay;
        public int YearDays = 60;                // the game's year, in days
        public int Day, Phase;                   // the market clock: the day and the phase of it last done
        public int FrontierDay, FrontierPhase;   // the furthest market time played in this playthrough, in any session (from the tape)
        public int HeldDay, HeldPhase;           // after a load: no order until the clock is past this
        public double Variance = 1.0;            // the market's variance against the ordinary, today
        public double LastShock;                 // yesterday's market shock, for the variance
        public double DayZ, DayVar;              // today's market shock so far, and the share of the day's variance it is for
        public readonly double[] SectorDayZ = new double[(int)MarketSector.Count];
        public readonly double[] SectorVar = new double[(int)MarketSector.Count];
        public readonly double[] SectorShock = new double[(int)MarketSector.Count];
        public readonly int[] ManiaDays = new int[(int)MarketSector.Count];      // a craze: days it has run (0 none)
        public readonly double[] ManiaAdded = new double[(int)MarketSector.Count]; // what it has added so far (ln)
        public double IndexDivisor;              // the index is the market value of every listed company over this
        public int ListingId, ListingDay;        // a listing announced: the company and its first day
        public int NextOrderId = 1;
        public bool CarriedOver;                 // the Ventures market's shares have been brought over
        public int RateSeenBp = -1;              // the bank rate as the market last priced it
        public int ElectionsSeen = -1;
        public MarketEnv Env = MarketEnv.Quiet();

        public readonly List<Stock> Stocks = new List<Stock>(160);
        public readonly List<Position> Positions = new List<Position>(16);
        public readonly List<Order> Orders = new List<Order>(16);
        public readonly List<Trade> Trades = new List<Trade>(MarketCatalogue.TradesKept + 1);
        public readonly List<int> Watch = new List<int>(16);
        public readonly Account Account = new Account();
        public readonly List<MarketNews> News = new List<MarketNews>(32);   // the last few days' pieces
        public readonly List<DividendDue> Dues = new List<DividendDue>(8);  // dividends worked out at a record day, to be paid
        public readonly List<Bot> Bots = new List<Bot>(4);                  // the trading bots the player has hired (MarketBots), one a firm
        public int BusinessDay;                  // the last day of the calendar the companies' business has been run for

        // The player's own company (Markets/Listing). Me is who the player of this machine is (a hash of their save, set by
        // the director at every start, never saved): a listed company whose Own.Owner is Me is theirs, any other player's
        // is another holder's company to them. Listing is their way to the market and their books (saved, never shared);
        // Following is set while this market follows a Going Public host's (nothing of the player's own company moves then).
        public ulong Me;
        public bool Following;
        public ListingFile Listing = new ListingFile();

        // The city's index and the sector indices: today's bars and the daily history (hundredths of a point).
        public readonly Candle[] IndexBars = new Candle[MarketCatalogue.IntradayBars];
        public int IndexBarCount;
        public readonly Candle[] IndexLastBars = new Candle[MarketCatalogue.IntradayBars];
        public int IndexLastBarCount, IndexLastBarsDay;
        public readonly List<Candle> IndexDaily = new List<Candle>(MarketCatalogue.DailyKept + 1);
        public readonly List<Candle> IndexMonthly = new List<Candle>(16);
        public int IndexOpen, IndexHigh, IndexLow, IndexLast, IndexPrevClose;
        public readonly double[] SectorDivisor = new double[(int)MarketSector.Count];
        public readonly int[] SectorLast = new int[(int)MarketSector.Count];
        public readonly int[] SectorPrevClose = new int[(int)MarketSector.Count];
        public readonly List<int>[] SectorDaily = NewSectorLists();

        private static List<int>[] NewSectorLists()
        {
            var lists = new List<int>[(int)MarketSector.Count];
            for (int i = 0; i < lists.Length; i++)
            {
                lists[i] = new List<int>(MarketCatalogue.DailyKept + 1);
            }
            return lists;
        }

        public Stock Stock(int id)
        {
            for (int i = 0; i < Stocks.Count; i++)
            {
                if (Stocks[i].Id == id)
                {
                    return Stocks[i];
                }
            }
            return null;
        }

        public Position Position(int stockId)
        {
            for (int i = 0; i < Positions.Count; i++)
            {
                if (Positions[i].StockId == stockId)
                {
                    return Positions[i];
                }
            }
            return null;
        }

        public Order Order(int id)
        {
            for (int i = 0; i < Orders.Count; i++)
            {
                if (Orders[i].Id == id)
                {
                    return Orders[i];
                }
            }
            return null;
        }

        /// <summary>Shares held of a company: under nothing for a short.</summary>
        public long Held(int stockId)
        {
            Position p = Position(stockId);
            return p != null ? p.Shares : 0L;
        }

        public bool Watching(int stockId)
        {
            return Watch.Contains(stockId);
        }

        /// <summary>The market time as one number that only grows: day and phase.</summary>
        public static long Time(int day, int phase)
        {
            return (long)day * 100 + phase;
        }

        public long Now => Time(Day, Phase);

        public long Frontier => Time(FrontierDay, FrontierPhase);

        /// <summary>True from a market day's open to its close (the open done, the close not yet).</summary>
        public bool InSession => MarketCatalogue.TradingDay(Day) && Phase >= MarketCatalogue.PhaseOpen && Phase < MarketCatalogue.PhaseClose;
    }
}
