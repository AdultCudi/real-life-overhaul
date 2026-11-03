using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>A rule the player or a rival runs. Saved as a number: never renumber, add at the end.</summary>
    public enum RuleKind : byte
    {
        Dip = 0,          // buy while RSI is under the level
        Rider = 1,        // buy while the 20 day average is over the 50
        Dividend = 2,     // buy a payer, sell when the dividend is gone
        Rebalance = 3,    // buy up to the budget, sell a gain
        Grid = 4,         // buy under the level, sell the gain above it
        Earnings = 5,     // buy after a profitable quarter
        StopGuard = 6,    // sell through the stop
        Gap = 7           // buy a gap down at the open
    }

    /// <summary>Where a rule bot stands. Saved as a number. Waiting, paused and out of budget are derived, not stored.</summary>
    public enum RuleStatus : byte
    {
        Running = 0,
        Paused = 1,
        Stopped = 2
    }

    /// <summary>
    /// One rule bot. The player's orders go through the market's own place; a rival runs the same rule and only the
    /// tape and its own budget move, never the player's cash. Game-free apart from the place it is handed.
    /// </summary>
    public sealed class RuleBot
    {
        public int Id;
        public RuleKind Kind;
        public int Owner;            // 0 the player, 1 a rival
        public int Who;              // the rival's index when Owner is 1
        public int StockId;
        public int Level = 30;       // RSI, or a price in cents for a grid, or a gap in basis points
        public int GainBp = 800;     // a gain that sells, basis points
        public OrderType Type = OrderType.Market;
        public long Budget;
        public long PerTrade;        // shares a trade, or 0 for what the budget buys
        public int StopBp = 500;
        public bool OpenOnly = true;
        public bool Reinvest;
        public bool Tell;
        public int MaxOpen = 1;
        public RuleStatus Status;
        public long Cash;
        public long DayResult;
        public long WeekResult;
        public int OpenTrades;
        public int LastDay;
        public int ToldStock;        // a trade to mention, cleared when the director has said it

        /// <summary>Running, waiting, paused, out of budget or stopped, as the screen's chip says it.</summary>
        public int Chip
        {
            get
            {
                if (Status == RuleStatus.Stopped)
                {
                    return 4;
                }
                if (Status == RuleStatus.Paused)
                {
                    return 2;
                }
                if (Cash <= 0 && Budget > 0)
                {
                    return 3;
                }
                if (OpenTrades <= 0)
                {
                    return 1;   // waiting on the rule
                }
                return 0;
            }
        }
    }

    /// <summary>
    /// What a rule would trade, from the candles and the indicators, the same for the player and a rival. No draw:
    /// the same company and day give the same order. The caller places a player's order through the engine.
    /// </summary>
    public static class RuleBots
    {
        public const int Most = 12;          // every rule of the save: the rivals' four and the player's
        public const int PlayerMost = 8;     // the player's own

        public delegate bool PlaceRule(RuleBot rule, int stockId, bool buy, long qty, int limitCents, OrderType type);

        /// <summary>Shares to buy, under nothing to sell, or 0 to stand still.</summary>
        public static long Intent(RuleBot rule, Stock stock, MarketBots.Work work)
        {
            long intent = IntentRaw(rule, stock, work);
            if (intent > 0 && stock != null && stock.Last > 0)
            {
                long affordable = Math.Max(0L, rule.Cash) * 100L / stock.Last;   // its budget is a cap on what it buys, not a hope
                intent = Math.Min(intent, affordable);
            }
            return intent;
        }

        private static long IntentRaw(RuleBot rule, Stock stock, MarketBots.Work work)
        {
            if (rule == null || stock == null || !stock.Listed || rule.Status != RuleStatus.Running || work == null)
            {
                return 0;
            }
            int n = Math.Min(stock.Daily.Count, work.Closes.Length);
            int from = stock.Daily.Count - n;
            for (int i = 0; i < n; i++)
            {
                work.Closes[i] = stock.Daily[from + i].C;
            }
            if (n < 2 || stock.Last <= 0)
            {
                return 0;
            }
            bool held = rule.OpenTrades > 0;
            long qty = rule.PerTrade > 0 ? rule.PerTrade : rule.Cash > 0 ? rule.Cash * 100L / stock.Last : 0;
            if (qty < 1)
            {
                qty = 1;
            }
            switch (rule.Kind)
            {
                case RuleKind.Dip:
                    MarketIndicators.Rsi(work.Closes, n, 14, work.A);
                    if (double.IsNaN(work.A[n - 1]))
                    {
                        return 0;
                    }
                    if (!held && work.A[n - 1] < rule.Level && rule.OpenTrades < rule.MaxOpen)
                    {
                        return qty;
                    }
                    return held && work.A[n - 1] > 70.0 ? -qty : 0;
                case RuleKind.Rider:
                    if (n < 50)
                    {
                        return 0;
                    }
                    MarketIndicators.Sma(work.Closes, n, 20, work.A);
                    MarketIndicators.Sma(work.Closes, n, 50, work.B);
                    if (double.IsNaN(work.A[n - 1]) || double.IsNaN(work.B[n - 1]))
                    {
                        return 0;
                    }
                    bool up = work.A[n - 1] > work.B[n - 1];
                    if (!held && up && rule.OpenTrades < rule.MaxOpen)
                    {
                        return qty;
                    }
                    return held && !up ? -qty : 0;
                case RuleKind.Dividend:
                    if (!held && stock.DpsCents > 0 && rule.OpenTrades < rule.MaxOpen)
                    {
                        return qty;
                    }
                    return held && stock.DpsCents <= 0 ? -qty : 0;
                case RuleKind.Rebalance:
                    if (!held && rule.Cash > 0 && rule.OpenTrades < rule.MaxOpen)
                    {
                        return qty;
                    }
                    return held && Gain(rule, stock) >= rule.GainBp ? -qty : 0;
                case RuleKind.Grid:
                    if (!held && stock.Last < rule.Level && rule.OpenTrades < rule.MaxOpen)
                    {
                        return qty;
                    }
                    return held && stock.Last > rule.Level + rule.Level * rule.GainBp / 10000 ? -qty : 0;
                case RuleKind.Earnings:
                    int eps = stock.Results.Count > 0 ? stock.Results[stock.Results.Count - 1].Eps : 0;
                    if (!held && eps > 0 && rule.OpenTrades < rule.MaxOpen)
                    {
                        return qty;
                    }
                    return held && eps < 0 ? -qty : 0;
                case RuleKind.StopGuard:
                    return held && stock.Last * 10000L < rule.Level * (10000L - rule.StopBp) ? -qty : 0;
                default:
                    if (n < 2)
                    {
                        return 0;
                    }
                    int prev = (int)work.Closes[n - 2];
                    int gap = prev > 0 ? (prev - stock.Open) * 10000 / prev : 0;
                    if (!held && gap >= rule.Level && rule.OpenTrades < rule.MaxOpen)
                    {
                        return qty;
                    }
                    return held && Gain(rule, stock) >= rule.GainBp ? -qty : 0;
            }
        }

        public static int Gain(RuleBot rule, Stock stock)
        {
            if (rule.PerTrade <= 0 || stock.Last <= 0 || rule.Cash >= rule.Budget)
            {
                return 0;
            }
            long cost = (rule.Budget - rule.Cash) * 100L / Math.Max(1, rule.PerTrade);
            if (cost <= 0)
            {
                return 0;
            }
            return (int)((stock.Last - cost) * 10000L / cost);
        }

        /// <summary>
        /// One pass at the open of a session. A player's order is placed through <paramref name="place"/>. A rival's
        /// is the same rule and a print, and never the player's book.
        /// </summary>
        public static void Step(MarketState state, int day, int phase, PlaceRule place, MarketBots.Work work)
        {
            if (state == null || phase < MarketCatalogue.PhaseOpen || phase >= MarketCatalogue.PhaseClose)
            {
                return;
            }
            for (int i = 0; i < state.Rules.Count; i++)
            {
                RuleBot rule = state.Rules[i];
                if (rule.OpenOnly && phase >= MarketCatalogue.PhaseClose)
                {
                    continue;
                }
                if (rule.LastDay == day)
                {
                    continue;   // once a day, so a replay of the step does not deal twice
                }
                Stock stock = state.Stock(rule.StockId);
                if (rule.Kind == RuleKind.StopGuard && rule.Owner == 0 && stock != null)
                {
                    rule.OpenTrades = state.Held(stock.Id) > 0 ? Math.Max(1, rule.OpenTrades) : 0;   // it guards what is held: it never buys, so a trade count would stay empty
                }
                long intent = Intent(rule, stock, work);
                if (intent == 0)
                {
                    continue;
                }
                bool buy = intent > 0;
                long qty = buy ? intent : -intent;
                if (!buy && rule.Kind == RuleKind.StopGuard && rule.Owner == 0)
                {
                    qty = Math.Max(1L, state.Held(stock.Id));   // a stop sells the whole holding
                }
                int limit = rule.Type == OrderType.Limit ? stock.Last : 0;
                if (rule.Owner == 0)
                {
                    if (place == null || !place(rule, stock.Id, buy, qty, limit, rule.Type))
                    {
                        continue;
                    }
                }
                else
                {
                    long value = qty * stock.Last / 100L;
                    if (buy && value > rule.Cash)
                    {
                        continue;
                    }
                    state.Names.Note(new TradePrint
                    {
                        StockId = stock.Id,
                        Side = buy ? TradeSide.Buy : TradeSide.Sell,
                        Size = qty,
                        PriceCents = stock.Last,
                        Day = day,
                        Phase = phase,
                        Who = TradeWho.Rival,
                        WhoId = rule.Who
                    });
                    rule.Cash += buy ? -value : value;
                    if (rule.Reinvest && !buy)
                    {
                        rule.Budget = rule.Cash;
                    }
                }
                rule.LastDay = day;
                rule.OpenTrades += buy ? 1 : -1;
                if (rule.OpenTrades < 0)
                {
                    rule.OpenTrades = 0;
                }
                if (rule.Tell)
                {
                    rule.ToldStock = stock.Id;
                }
                long signed = (buy ? -1 : 1) * qty * stock.Last / 100L;
                rule.DayResult += signed;
                rule.WeekResult += signed;
            }
        }

        /// <summary>How many of the save's rules are the player's.</summary>
        public static int PlayerCount(MarketState state)
        {
            int n = 0;
            for (int i = 0; state != null && i < state.Rules.Count; i++)
            {
                n += state.Rules[i].Owner == 0 ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// A rule of the player's, written on a listed company with its budget: null when the company is not listed, the budget is
        /// under nothing, or the player runs as many as they may. Its id is one past the largest, so a removed rule's is not reused.
        /// </summary>
        public static RuleBot NewRule(MarketState state, RuleKind kind, int stockId, int level, int gainBp, OrderType type, long budget, long perTrade, int stopBp,
            bool openOnly, bool reinvest, bool tell, int maxOpen)
        {
            Stock stock = state != null ? state.Stock(stockId) : null;
            if (stock == null || !stock.Listed || stock.Own != null || budget <= 0 || PlayerCount(state) >= PlayerMost || state.Rules.Count >= Most)
            {
                return null;
            }
            int id = 0;
            for (int i = 0; i < state.Rules.Count; i++)
            {
                id = Math.Max(id, state.Rules[i].Id);
            }
            var rule = new RuleBot
            {
                Id = id + 1, Kind = kind, Owner = 0, StockId = stockId, Level = level, GainBp = gainBp, Type = type, Budget = budget, Cash = budget,
                PerTrade = Math.Max(1L, perTrade), StopBp = stopBp, OpenOnly = openOnly, Reinvest = reinvest, Tell = tell, MaxOpen = Math.Max(1, maxOpen), Status = RuleStatus.Running
            };
            state.Rules.Add(rule);
            return rule;
        }

        /// <summary>A rule of the player's taken away; a rival's is never touched.</summary>
        public static bool Remove(MarketState state, int index)
        {
            if (state == null || index < 0 || index >= state.Rules.Count || state.Rules[index].Owner != 0)
            {
                return false;
            }
            state.Rules.RemoveAt(index);
            return true;
        }

        /// <summary>One rule for each rival template, on a listed company, seeded from the catalogue. Once, when the list is empty.</summary>
        public static void SeedRivals(MarketState state)
        {
            if (state == null || state.Rules.Count > 0 || state.Stocks.Count == 0)
            {
                return;
            }
            int n = MarketCatalogue.RivalTemplates.Length;
            for (int i = 0; i < n && state.Rules.Count < Most; i++)
            {
                Stock stock = null;
                for (int s = 0; s < state.Stocks.Count; s++)
                {
                    Stock k = state.Stocks[(s + i) % state.Stocks.Count];
                    if (k.Listed && k.Own == null)
                    {
                        stock = k;
                        break;
                    }
                }
                if (stock == null)
                {
                    return;
                }
                long budget = Math.Max(1000L, stock.Last * 20L);
                state.Rules.Add(new RuleBot
                {
                    Id = i + 1,
                    Kind = (RuleKind)(i % 8),
                    Owner = 1,
                    Who = i,
                    StockId = stock.Id,
                    Level = stock.Last,
                    Budget = budget,
                    Cash = budget,
                    PerTrade = 10,
                    Status = RuleStatus.Running
                });
            }
        }
    }
}
