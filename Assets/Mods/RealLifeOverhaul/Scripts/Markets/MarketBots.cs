using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Trading bots for hire: four firms, their strategies and terms, every figure of their rules, how a bot decides and books
    /// its trades. Game-free: the offline checks compile it and the Guide prints from it. A bot decides at a close on what
    /// everyone could see and trades at the next session's first step at that moment's price, paying the spread, its move
    /// and commission as any order, so no rule of it beats the market but by luck. The player's own listed company may be
    /// traded within its founder's rules (<see cref="OwnRoom"/>); another player's never is.
    /// </summary>
    public static partial class MarketBots
    {
        public const int FirmsMost = 4;
        public const int CapitalLeastAtStart = 2500;   // at the start's prices (Prices.At)
        public const int FeeLeastAtStart = 5;          // at the start's prices
        public const int TradeLeastAtStart = 100;      // no purchase worth less, at the start's prices
        public const int ListedDaysLeast = 20;         // days listed before it buys
        public const int PicksMost = 12;               // so the Bots tab's picker stays short
        public const int OwnVolumePct = 10;            // per cent of the player's company's ordinary day's volume, all bots together
        public const int ChangeLeastAtStart = 500;     // at the start's prices: five of its least purchases
        public const int TopUpAdded = 1, TopUpDecided = 2;   // Bot.TopUp: capital added, not yet decided on; decided on, not yet traded
        public const int TopUpAgain = 3, TopUpDecidedAgain = 4;   // the same once more after a step the player's cash was short at

        public static readonly int[] PayPcts = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };   // per cent of new profit paid out

        public static readonly int[] Sizes = { 10, 20, 25, 50, 100 };   // per cent of its worth in each holding
        public static readonly int[] Stops = { 0, 5, 10, 15, 25 };      // per cent under what a holding cost; 0 none
        public static readonly int[] Losses = { 20, 35, 50, 75, 90 };   // per cent of what it was given

        // The strategies' rules, on daily closes.
        public const int TrendFast = 20, TrendSlow = 50;                  // in while close > SMA 20 > SMA 50, out under SMA 50
        public const int RsiDays = 5, RsiIn = 30, RsiOut = 60, ReversionFilter = 50, ReversionDaysMost = 10;   // in at RSI(5) < 30 over SMA 50; out over 60 or after 10 days
        public const int MomentumDays = 60, MomentumSkip = 5;            // the gain over 60 market days to a week ago, held for a month
        public const int IncomeQuarters = 4;                             // four profitable quarters, the dividend within them
        public const int CloseHistoryMost = MarketCatalogue.DailyKept;

        private const string P = "reallife";   // apart, so a key made of parts is not taken for a whole key

        public static readonly BotFirm[] All =
        {
            new BotFirm(1, "Meridian Trend Partners", BotStrategy.Trend, 20, 2000, 20, 10, 35, false, P + "_mkt_bot_trend"),
            new BotFirm(2, "Quayside Reversion Fund", BotStrategy.Reversion, 15, 2000, 20, 10, 35, false, P + "_mkt_bot_reversion"),
            new BotFirm(3, "Larkspur Momentum", BotStrategy.Momentum, 20, 2500, 25, 15, 50, true, P + "_mkt_bot_momentum"),
            new BotFirm(4, "Harbourline Income", BotStrategy.Income, 10, 1000, 10, 0, 35, false, P + "_mkt_bot_income")
        };

        public static BotFirm Firm(int id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }

        public static Bot Hired(MarketState state, int firm)
        {
            for (int i = 0; i < state.Bots.Count; i++)
            {
                if (state.Bots[i].Firm == firm)
                {
                    return state.Bots[i];
                }
            }
            return null;
        }

        /// <summary>A setting held to one of its choices: the nearest at or under it, else the first.</summary>
        public static int Choice(int[] choices, int value)
        {
            int best = choices[0];
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i] <= value)
                {
                    best = choices[i];
                }
            }
            return best;
        }

        public static int IndexOf(int[] choices, int value)
        {
            int at = Array.IndexOf(choices, value);
            return at < 0 ? 0 : at;
        }

        public static int Slots(Bot b)
        {
            return Math.Max(1, 100 / Math.Max(1, b.Size));
        }

        public static long Held(MarketState state, int stockId)
        {
            long held = 0;
            for (int i = 0; i < state.Bots.Count; i++)
            {
                BotHolding h = state.Bots[i].Holding(stockId);
                held += h != null ? h.Shares : 0;
            }
            return held;
        }

        /// <summary>A bot's money and its holdings at the bid.</summary>
        public static long Worth(MarketState state, Bot b, int phase)
        {
            long worth = b.Cash;
            for (int i = 0; i < b.Holdings.Count; i++)
            {
                BotHolding h = b.Holdings[i];
                Stock s = state.Stock(h.StockId);
                if (s != null && s.Listed)
                {
                    worth += MarketTrading.Value(h.Shares, MarketTrading.QuoteOf(state, s, phase).Bid, false);
                }
            }
            return worth;
        }

        /// <summary>The worth under which it sells everything and stops.</summary>
        public static long Floor(Bot b)
        {
            return b.Base * (100 - b.Loss) / 100;
        }

        /// <summary>A holding's stop for its cost a share at the bot's setting; 0 none.</summary>
        public static int StopFor(Bot b, long shares, long cost)
        {
            if (b.Stop <= 0 || shares <= 0)
            {
                return 0;
            }
            long each = cost * 100 / shares;   // cents a share
            return (int)Math.Max(1, Math.Min(int.MaxValue, each * (100 - b.Stop) / 100));
        }

        /// <summary>The month's fee at a worth before it is shared over the days hired: a share of the worth, the least at least.</summary>
        public static long Fee(BotFirm f, long worth, int least)
        {
            long share = (Math.Max(0, worth) * f.FeeBp + 9999L) / 10000L;
            return Math.Max(least, share);
        }

        public static int MarketDaysIn(int month, int year)
        {
            int from = MarketHistory.MonthStart(month, year), to = MarketHistory.MonthStart(month + 1, year);
            int count = 0;
            for (int d = from; d < to; d++)
            {
                count += MarketCatalogue.TradingDay(d) ? 1 : 0;
            }
            return Math.Max(1, count);
        }

        /// <summary>A company a bot may buy: listed long enough, nobody's own, no bid, not a penny share, of a size it trades.</summary>
        public static bool Buyable(MarketState state, BotFirm f, Stock s, int day)
        {
            return s.Listed && s.Own == null && (s.Def == null || !s.Def.Holding) && s.BidCents <= 0 && day - s.ListedDay >= ListedDaysLeast && (f.Small || s.Size != MarketSize.Small)
                   && s.Last * 1000L >= 2L * MarketCatalogue.PennyCents * Math.Max(100, state.Env.PriceLevel);
        }

        /// <summary>True for a player's company whose founder is this machine's player.</summary>
        public static bool Mine(MarketState state, Stock s)
        {
            return s != null && s.Own != null && state.Me != 0UL && s.Own.Owner == state.Me && !state.Following;
        }

        /// <summary>Whether a company is in a bot's scope; a holding outside it is sold at the next session.</summary>
        public static bool InScope(MarketState state, Bot b, Stock s)
        {
            if (s == null || (s.Own != null && !Mine(state, s)))
            {
                return false;   // another player's company is never traded by a bot
            }
            switch (b.Scope)
            {
                case BotScope.Picked:
                    return b.Picks.Contains(s.Id);
                case BotScope.Held:
                    return s.Own != null ? s.Own.Founder > 0 : state.Held(s.Id) > 0;
                default:
                    return s.Own == null;
            }
        }

        /// <summary>
        /// A company a bot may buy now: its firm's market by <see cref="Buyable"/>; one the player chose, of any size, but never
        /// one listed too short a time, under a bid or a penny share.
        /// </summary>
        public static bool Allowed(MarketState state, Bot b, Stock s, int day)
        {
            BotFirm f = b.Def;
            if (f == null || s == null)
            {
                return false;
            }
            if (b.Scope == BotScope.Market)
            {
                return Buyable(state, f, s, day);
            }
            return InScope(state, b, s) && s.Listed && s.BidCents <= 0 && day - s.ListedDay >= ListedDaysLeast
                   && s.Last * 1000L >= 2L * MarketCatalogue.PennyCents * Math.Max(100, state.Env.PriceLevel);
        }

        /// <summary>A company picked for a bot or taken off; false when nothing changed or the picks are full.</summary>
        public static bool Pick(Bot b, int stockId, bool on)
        {
            int at = b.Picks.IndexOf(stockId);
            if (on == (at >= 0) || stockId <= 0)
            {
                return false;
            }
            if (!on)
            {
                b.Picks.RemoveAt(at);
                return true;
            }
            if (b.Picks.Count >= PicksMost)
            {
                return false;
            }
            b.Picks.Add(stockId);
            return true;
        }

        /// <summary>The founder's short-swing time: a bot may not sell the player's company within it of buying, nor buy after selling.</summary>
        public static int OwnSwingDays(int year)
        {
            return Math.Max(1, Math.Max(8, year) / ListingCatalogue.ShortSwingYearShare);
        }

        /// <summary>
        /// The most shares of the player's own company their bots may trade now, keeping every rule its founder deals by and a
        /// share of its day's volume so no bot moves its price alone. long.MaxValue for any other company; none for another
        /// player's. Reads only the company and this player's bots, so every machine comes to the same price.
        /// </summary>
        public static long OwnRoom(MarketState state, Stock s, bool buying, int day, int year)
        {
            if (s == null || s.Own == null)
            {
                return long.MaxValue;
            }
            if (!Mine(state, s) || !s.Listed || ListingRules.Closed(s, day, year))
            {
                return 0;
            }
            OwnListing o = s.Own;
            int y = MarketCatalogue.YearOf(day, Math.Max(8, year)), window = OwnSwingDays(year);
            long today = 0, held = 0;
            int bp = ListingRules.YearUsedBp(o, day, year, buying ? o.BoughtBp : o.SoldBp);
            for (int i = 0; i < state.Bots.Count; i++)
            {
                Bot b = state.Bots[i];
                today += b.OwnDay == day ? b.OwnQty : 0;
                bp += b.OwnYear == y ? (buying ? b.OwnBoughtBp : b.OwnSoldBp) : 0;
                int against = buying ? b.OwnSellDay : b.OwnBuyDay;
                if (against > 0 && day - against < window)
                {
                    return 0;
                }
                BotHolding h = b.Holding(s.Id);
                held += h != null ? h.Shares : 0;
            }
            long room = Math.Max(0L, (long)Math.Floor(Math.Max(0.0, s.Adv) * OwnVolumePct / 100.0) - today);
            room = Math.Min(room, s.Shares * Math.Max(0, ListingCatalogue.FounderDealMostBpYear - bp) / 10000L);
            if (buying)
            {
                room = Math.Min(room, s.Shares * ListingCatalogue.FounderMostBp / 10000L - o.Founder - held);
            }
            return Math.Max(0L, room);
        }

        /// <summary>A bot's trade in the player's own company counted against <see cref="OwnRoom"/>'s limits.</summary>
        public static void OwnCount(Bot b, Stock s, bool buying, long qty, int day, int year)
        {
            if (b.OwnDay != day)
            {
                b.OwnDay = day;
                b.OwnQty = 0;
            }
            b.OwnQty += qty;
            int y = MarketCatalogue.YearOf(day, Math.Max(8, year));
            if (b.OwnYear != y)
            {
                b.OwnYear = y;
                b.OwnBoughtBp = b.OwnSoldBp = 0;
            }
            int bp = ListingRules.Bp(qty, s.Shares);
            if (buying)
            {
                b.OwnBoughtBp += bp;
                b.OwnBuyDay = day;
            }
            else
            {
                b.OwnSoldBp += bp;
                b.OwnSellDay = day;
            }
        }

        /// <summary>What a bot has made and kept: gains on sales and dividends after tax, less its fees and share.</summary>
        public static long Profit(Bot b)
        {
            return b.Realised + b.Dividends - b.Fees;
        }

        /// <summary>The losses a bot must make good before it pays anything again.</summary>
        public static long LossesToMakeGood(Bot b)
        {
            return Math.Max(0L, b.PayMark - Profit(b));
        }

        /// <summary>True when a payout falls due at this market day's close.</summary>
        public static bool PayDue(BotPayEvery every, int day, int year)
        {
            int next = MarketTrading.NextTradingDay(day);
            switch (every)
            {
                case BotPayEvery.Daily:
                    return true;
                case BotPayEvery.Weekly:
                    return MarketCatalogue.WeekOf(next) != MarketCatalogue.WeekOf(day);
                default:
                    return MarketCatalogue.MonthOf(next, year) != MarketCatalogue.MonthOf(day, year);
            }
        }

        /// <summary>
        /// A bot's payout at its time: of profit above <see cref="Bot.PayMark"/> (a loss is made good first), its share is owed
        /// and paid out of its money not in shares. The money is the player's cash already, so a payout lowers what it trades
        /// with and every figure its limits are reckoned from, and never counts as a loss. Returns what was paid.
        /// </summary>
        public static long Payout(Bot b)
        {
            long profit = Profit(b);
            if (profit > b.PayMark)
            {
                b.PayOwed += (profit - b.PayMark) * Math.Max(0, Math.Min(100, b.PayPct)) / 100;
                b.PayMark = profit;
            }
            long pay = Math.Min(b.PayOwed, Math.Max(0L, b.Cash));
            if (pay <= 0)
            {
                return 0;
            }
            b.Cash -= pay;
            b.PayOwed -= pay;
            b.PaidOut += pay;
            b.MonthPaid += pay;
            b.Base = Math.Max(0L, b.Base - pay);
            b.Peak = Math.Max(0L, b.Peak - pay);
            b.HighWater -= pay;
            b.MonthWorth -= pay;
            return pay;
        }

        /// <summary>
        /// More capital given to a running bot; as at a hire no money moves. Every figure reckoned from its capital rises by it,
        /// so it never counts as profit or loss; its fee counts it for the days it was there (<see cref="FeeWorth"/>). Its next
        /// decision puts it into what it holds and into new holdings (<see cref="Decide"/>).
        /// </summary>
        public static void Add(Bot b, long amount)
        {
            if (amount <= 0)
            {
                return;
            }
            b.Capital += amount;
            b.Cash += amount;
            b.Base += amount;
            b.Peak += amount;
            b.HighWater += amount;
            b.MonthWorth += amount;
            b.MonthAdded += amount * b.MonthDays;
            b.TopUp = TopUpAdded;
        }

        /// <summary>
        /// The most that can be taken out of a running bot now without selling: its money not in shares less payouts owed,
        /// leaving it the least a bot is given and its cushion over its floor (<see cref="Cushion"/>). 0: none.
        /// </summary>
        public static long MostOut(MarketState state, Bot b, long least, int phase)
        {
            if (b.Status != BotStatus.Running || b.Loss <= 0)
            {
                return 0;
            }
            long cap = Math.Min(b.Cash - b.PayOwed, Math.Min(b.Capital - least, b.Base - least));
            long worth = Worth(state, b, phase);
            long need = Math.Max(1L, Cushion(b, worth - b.Cash));
            long room = worth - Floor(b) - need;
            long most = Math.Min(cap, room >= 0 ? room * 100 / b.Loss : 0);
            // The floor's rounding: a few steps at most, either way.
            while (most > 0 && !Keeps(b, worth, most, need))
            {
                most--;
            }
            while (most >= 0 && most < cap && Keeps(b, worth, most + 1, need))
            {
                most++;
            }
            return Math.Max(0L, most);
        }

        // True when a bot worth so much keeps its cushion over its floor with so much taken out.
        private static bool Keeps(Bot b, long worth, long taken, long need)
        {
            return worth - taken - (b.Base - taken) * (100 - b.Loss) / 100 >= need;
        }

        /// <summary>
        /// What a take-out leaves between worth and floor at least: the limit's share of what is in shares, so its shares may
        /// fall by as much as it may lose in all before it stops. Money not in shares does not move.
        /// </summary>
        public static long Cushion(Bot b, long inShares)
        {
            return inShares <= 0 ? 0L : (inShares * b.Loss + 99) / 100;
        }

        /// <summary>True while capital added has been decided on and waits to be traded at the first step.</summary>
        public static bool TopUpDue(Bot b)
        {
            return b.TopUp == TopUpDecided || b.TopUp == TopUpDecidedAgain;
        }

        /// <summary>After the first step capital added was traded at: done, or once more the next market day if the cash was short.</summary>
        public static void TopUpTraded(Bot b, bool cashShort)
        {
            if (b.TopUp == TopUpDecided && cashShort)
            {
                b.TopUp = TopUpAgain;
            }
            else if (TopUpDue(b))
            {
                b.TopUp = 0;
            }
        }

        /// <summary>The player's cash already given to their bots: every bot's money not in shares. A hire may use only the cash beyond it.</summary>
        public static long Committed(MarketState state)
        {
            long sum = 0;
            for (int i = 0; i < state.Bots.Count; i++)
            {
                sum += Math.Max(0L, state.Bots[i].Cash);
            }
            return sum;
        }

        /// <summary>Capital taken out of a running bot, off its money not in shares; every figure from its capital comes down with it.</summary>
        public static void TakeOut(Bot b, long amount)
        {
            if (amount <= 0)
            {
                return;
            }
            b.Capital -= amount;
            b.Cash -= amount;
            b.Base -= amount;
            b.Peak = Math.Max(0L, b.Peak - amount);
            b.HighWater -= amount;
            b.MonthWorth -= amount;
            b.MonthAdded -= amount * b.MonthDays;
        }

        /// <summary>
        /// The worth a month's fee is a share of: its worth at the month's end with each sum added or taken out counted only
        /// for the market days it was there (Bot.MonthAdded).
        /// </summary>
        public static long FeeWorth(Bot b, long worth, int hired)
        {
            return hired > 0 && b.MonthAdded != 0 ? worth - b.MonthAdded / hired : worth;
        }

        /// <summary>
        /// A bot's shares bought or sold at a price, the player's money moved as their own trades move it. False, nothing changed,
        /// when the bot's money or the cash does not cover a purchase, or it does not hold what it would sell. With
        /// <paramref name="move"/> false the lasting price move is given in <paramref name="kept"/> for a session's tape.
        /// </summary>
        public static bool Fill(MarketState state, Bot b, Stock s, bool buying, long qty, int cents, int least, IMarketWallet wallet, int phase, bool move, out double kept, out bool shortOfCash)
        {
            kept = 0.0;
            shortOfCash = false;
            if (qty <= 0 || cents <= 0)
            {
                return false;
            }
            long value = MarketTrading.Value(qty, cents, buying);
            long fee = MarketTrading.Commission(value, least);
            string name = s.Name + " (" + (b.Def != null ? b.Def.Name : string.Empty) + ")";
            BotHolding h = b.Holding(s.Id);
            if (buying)
            {
                if (b.Cash < value + fee)
                {
                    return false;
                }
                if (!wallet.Ready(value + fee))   // takes back from worked orders' money first
                {
                    shortOfCash = true;
                    return false;
                }
                if (!wallet.Take(value, MarketMoney.Bought, name))
                {
                    shortOfCash = true;
                    return false;
                }
                if (!wallet.Take(fee, MarketMoney.Commission, name))
                {
                    wallet.Force(fee, MarketMoney.Commission, name);
                }
                b.Cash -= value + fee;
                if (h == null)
                {
                    h = new BotHolding { StockId = s.Id, OpenedDay = state.Day };
                    b.Holdings.Add(h);
                }
                h.Shares += qty;
                h.Cost += value + fee;
                h.StopCents = StopFor(b, h.Shares, h.Cost);
            }
            else
            {
                if (h == null || h.Shares < qty)
                {
                    return false;
                }
                fee = Math.Min(fee, value);
                wallet.Give(value, MarketMoney.Sold, name);
                if (!wallet.Take(fee, MarketMoney.Commission, name))
                {
                    wallet.Force(fee, MarketMoney.Commission, name);
                }
                b.Cash += value - fee;
                long part = h.Cost * qty / h.Shares;
                long gain = value - fee - part;
                b.Realised += gain;
                h.Gain += gain;
                h.Cost -= part;
                h.Shares -= qty;
                if (h.Shares <= 0)
                {
                    b.Holdings.Remove(h);
                    b.Closed++;
                    b.Wins += h.Gain > 0 ? 1 : 0;
                }
            }
            b.Commission += fee;
            b.Trades++;
            b.MonthTrades++;
            // The market moves as for the player's own trade, and their recent trading in it grows.
            double share = Math.Sqrt(qty / Math.Max(1.0, s.Adv));
            kept = (buying ? 1.0 : -1.0) * MarketCatalogue.ImpactPermanent * MarketCatalogue.ImpactTemporary * MarketTrading.DayVol(state, s) * share;
            if (move)
            {
                s.Mis += kept;
                s.Sent += kept;
                s.DayVolume += qty;
            }
            s.Impact += qty;
            MarketTrading.AddTrade(state, new Trade { Day = state.Day, Phase = phase, StockId = s.Id, Kind = buying ? TradeKind.Bought : TradeKind.Sold, Qty = qty, PriceCents = cents,
                Value = buying ? -value : value, Fee = fee, Bot = b.Firm });
            return true;
        }

        /// <summary>The most shares a bot may buy now: an order's most, within what the player and their bots may hold together.</summary>
        public static long Room(MarketState state, Stock s)
        {
            long held = Math.Abs(state.Held(s.Id)) + Held(state, s.Id);
            return Math.Max(0L, Math.Min(MarketTrading.MostPerOrder(s), MarketTrading.MostHeld(s) - held));
        }

        /// <summary>A split: every bot's shares, stops and pending trades in the company; a stop rounded down, never easier to reach.</summary>
        public static void Split(MarketState state, Stock s, int ratio)
        {
            for (int i = 0; i < state.Bots.Count; i++)
            {
                Bot b = state.Bots[i];
                BotHolding h = b.Holding(s.Id);
                if (h != null)
                {
                    h.Shares *= ratio;
                    h.StopCents = h.StopCents > 0 ? Math.Max(1, h.StopCents / ratio) : 0;
                }
                for (int j = 0; j < b.Pending.Count; j++)
                {
                    BotOrder o = b.Pending[j];
                    if (o.StockId == s.Id)
                    {
                        o.Qty *= ratio;
                        b.Pending[j] = o;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The dividends paid to the player since last told, for one notification: the one with its company and firm, or how
    /// many and how much. Kept by the director and filled in place, so nothing is allocated.
    /// </summary>
    public struct DividendTally
    {
        public int Count;
        public long Total;
        public int Stock, Firm;      // the first one's company and firm; 0 the player's own shares

        public void Add(int stock, int firm, long money)
        {
            if (money <= 0)
            {
                return;
            }
            if (Count == 0)
            {
                Stock = stock;
                Firm = firm;
            }
            Count++;
            Total += money;
        }

        public void Clear()
        {
            Count = 0;
            Total = 0;
            Stock = Firm = 0;
        }
    }
}
