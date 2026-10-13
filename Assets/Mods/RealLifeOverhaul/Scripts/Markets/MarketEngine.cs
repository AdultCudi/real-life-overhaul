using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What the market needs from outside itself: fresh secrets, the city, money, and someone to tell. Markets/MarketGame is the game's; the checks have their own.</summary>
    public interface IMarketWorld
    {
        /// <summary>A secret nobody can guess, for a market day the tape does not have yet.</summary>
        ulong NewSecret();
        /// <summary>The city as it is now, for a market day being played for the first time.</summary>
        MarketEnv Env(int day);
        IMarketWallet Wallet { get; }
        /// <summary>The least commission, at today's prices.</summary>
        int CommissionLeast { get; }
        /// <summary>A piece of market news.</summary>
        void Said(in MarketNews news);
        /// <summary>An order (or part of one) was filled, at a price in cents.</summary>
        void Filled(Order order, Stock stock, long qty, int cents);
        /// <summary>Money came in or went out of the account for a reason (a dividend, a payout, interest, what the broker took).</summary>
        void Paid(TradeKind kind, Stock stock, long amount);
        /// <summary>A margin call: so much more equity is needed by the close of a day.</summary>
        void Called(long amount, int byDay);
    }

    /// <summary>
    /// The market's clock and everything that happens on it. Game-free. The market runs on a fixed grid of the game's
    /// time (results at 07:30, the open at 09:30, a step every five minutes, the close at 16:00, Monday to Friday) and
    /// <see cref="RunTo"/> works through every moment of the grid up to a time, in slices of a given size, so time
    /// that passes in a sleep, a skip or a prison stay is played out step by step as if the player had watched it.
    ///
    /// A moment the tape has already seen (before its frontier) is played with the tape's secret, city and events;
    /// a moment past it with a fresh secret and the city as it is, both written to the tape.
    ///
    /// Each step, every listed company's price moves by the market's move (beta times it), its sector's and its own:
    ///     most of each is news (fat-tailed): it stays, and at the close it goes into what the market expects of the
    ///         business (Stock.Outlook), which its figures then bear out over the next years (MarketLife.BearOut);
    ///     the rest is the market's mood (Stock.Sent), with the city's day (the weather, crime, a street event), the
    ///         crazes and the player's own trades: it fades slowly, over years.
    /// So a price is a fair bet at every moment: what it does next is news nobody has, and its expected return is the
    /// company's cost of equity. The market's moves come in calm and nervous spells (its variance follows the size of
    /// its recent days, falls more than rises), more near the open and the close; the night between two sessions is a
    /// gap. A company under a takeover bid trades close to what the bid is worth and hardly moves.
    /// </summary>
    public sealed class MarketEngine
    {
        private const int Sectors = (int)MarketSector.Count;
        private const int Steps = MarketCatalogue.Steps;

        public readonly MarketState State;
        public readonly MarketTape Tape;
        private readonly IMarketWorld _world;
        private long _replayUntil;
        private readonly List<MarketEvent> _live = new List<MarketEvent>(16);
        private readonly List<MarketEvent> _due = new List<MarketEvent>(16);
        private readonly double[] _weight = new double[Steps + 1];
        private readonly double[] _sectorNews = new double[Sectors];
        private readonly double[] _sectorMood = new double[Sectors];
        private readonly double[] _sectorSd = new double[Sectors];
        private readonly double[] _sectorValue = new double[Sectors];
        private readonly int[] _sectorOpen = new int[Sectors];
        private readonly List<Stock> _scratch = new List<Stock>(16);
        private ulong _secret;
        private int _secretDay = -1;

        /// <summary>
        /// The host of a Going Public session with guests: every player's trades move the one city's prices, so a trade's
        /// lasting move goes on the tape as an event (<see cref="MarketInputs.EventTrade"/>) and every market plays it at
        /// the same moment, the next of the grid. Off, it is put on the price at once, as alone.
        /// </summary>
        public bool TradesAsEvents;

        /// <summary>A guest that follows the host's market: a trade's lasting move goes to the host (the company, the move in millionths, the shares), which puts it on its tape for everybody.</summary>
        public Action<int, int, long> TradeSink;
        private double _kappa;
        private int _kappaYear;

        public MarketEngine(MarketState state, MarketTape tape, IMarketWorld world)
        {
            State = state;
            Tape = tape;
            _world = world;
            _replayUntil = tape != null ? tape.Frontier : 0;
            double total = 0.0;
            for (int k = 1; k <= Steps; k++)
            {
                _weight[k] = 1.0 + MarketCatalogue.IntradayOpenWeight * Math.Exp(-(k - 1) / MarketCatalogue.IntradayDecaySteps)
                             + MarketCatalogue.IntradayCloseWeight * Math.Exp(-(Steps - k) / MarketCatalogue.IntradayDecaySteps);
                total += _weight[k];
            }
            for (int k = 1; k <= Steps; k++)
            {
                _weight[k] /= total;
            }
        }

        /// <summary>True while the moments being played are ones the tape has seen before (after a load).</summary>
        public bool Replaying => State.Now < _replayUntil;

        /// <summary>True while orders wait after a load: until the market is past the furthest moment ever played (and past the load itself).</summary>
        public bool Held => State.HeldDay > 0 && State.Now <= MarketState.Time(State.HeldDay, State.HeldPhase);

        /// <summary>True while the market trades: a market day, from the open to the close.</summary>
        public bool Trading => MarketCatalogue.TradingDay(State.Day) && State.Phase >= MarketCatalogue.PhaseOpen && State.Phase < MarketCatalogue.PhaseClose;

        /// <summary>
        /// A save has just been loaded into this engine: the tape is followed up to its frontier, and no order is taken
        /// until the market is past both the frontier and the moment of the load (no trade at a price from before it).
        /// </summary>
        public void HoldAfterLoad()
        {
            _replayUntil = Tape != null ? Tape.Frontier : 0;
            long until = Math.Max(_replayUntil, State.Now);
            State.HeldDay = (int)(until / 100);
            State.HeldPhase = (int)(until % 100);
        }

        /// <summary>The time up to which the tape is followed.</summary>
        public long ReplayUntil => _replayUntil;

        /// <summary>
        /// A guest of a Going Public session follows the host's market: its tape is the host's, and the market plays it up
        /// to the furthest moment the host has played (and never past it, while following: see MarketDirector).
        /// </summary>
        public void Follow(long until)
        {
            if (until > _replayUntil)
            {
                _replayUntil = until;
            }
        }

        private int Year => Math.Max(8, State.YearDays);

        private double TradingYear => MarketCatalogue.TradingDaysPerYear(Year);

        // How much of the mood fades in a market day.
        private double Kappa
        {
            get
            {
                if (_kappaYear != Year)
                {
                    _kappaYear = Year;
                    double half = Math.Max(MarketCatalogue.MoodHalfLifeDaysLeast, MarketCatalogue.MoodHalfLifeYears * TradingYear);
                    _kappa = Math.Log(2.0) / half;
                }
                return _kappa;
            }
        }

        // Under a takeover bid the mood fades by half in a market day, and the market's and the sector's moves reach the
        // price only a little (it is held near the bid); its own a little more.
        private const double BidKappa = 0.6931471805599453;
        private const double BidMarketShare = 0.2, BidOwnShare = 0.3;

        // The news and the mood in each kind of move, as shares of its spread (the square roots of the shares of variance).
        private static readonly double MarketNews = Math.Sqrt(1.0 - MarketCatalogue.MarketMoodShare), MarketMood = Math.Sqrt(MarketCatalogue.MarketMoodShare);
        private static readonly double SectorNews = Math.Sqrt(1.0 - MarketCatalogue.SectorMoodShare), SectorMood = Math.Sqrt(MarketCatalogue.SectorMoodShare);
        private static readonly double OwnNews = Math.Sqrt(1.0 - MarketCatalogue.OwnMoodShare), OwnMood = Math.Sqrt(MarketCatalogue.OwnMoodShare);

        // The sector of the first listed company in a trade (MarketInputs.Trades), or -1 for none.
        private int SectorOfTrade(int trade)
        {
            if (trade < 0 || trade >= MarketInputs.Trades.Length)
            {
                return -1;
            }
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Listed && s.Def != null && s.Def.Trade == MarketInputs.Trades[trade])
                {
                    return (int)s.Sector;
                }
            }
            return -1;
        }

        /// <summary>Something happened in the city now that the market answers at once. Ignored while the tape is being followed.</summary>
        public void Event(int kind, int a, int b)
        {
            if (Replaying)
            {
                return;
            }
            _live.Add(new MarketEvent { Day = State.Day, Phase = State.Phase, Kind = kind, A = a, B = b });
        }

        // ---------------------------------------------------------------- the clock

        /// <summary>
        /// Plays the market forward to a moment, doing at most about 'work' company-moments of work (a step of the
        /// session is one for every listed company). True when it has got there; false when it ran out and will go on
        /// next time.
        /// </summary>
        public bool RunTo(long target, ref int work)
        {
            while (State.Now < target)
            {
                if (work <= 0)
                {
                    return false;
                }
                work -= Next();
            }
            return true;
        }

        // The next moment of the grid; what it cost.
        private int Next()
        {
            int day = State.Day, phase = State.Phase;
            if (phase >= MarketCatalogue.PhaseClose)
            {
                State.Day = day + 1;
                State.Phase = MarketCatalogue.PhaseNone;
                return 1;
            }
            if (!MarketCatalogue.TradingDay(day))
            {
                State.Phase = MarketCatalogue.PhaseClose;
                Reach();
                return 1;
            }
            Secret(day);
            ApplyEvents(day, phase);
            int cost = State.Stocks.Count;
            switch (phase)
            {
                case MarketCatalogue.PhaseNone:
                    Results(day);
                    State.Phase = MarketCatalogue.PhaseResults;
                    break;
                case MarketCatalogue.PhaseResults:
                    Open(day);
                    State.Phase = MarketCatalogue.PhaseOpen;
                    cost *= 3;
                    break;
                default:
                    if (phase < MarketCatalogue.PhaseClose - 1)
                    {
                        Step(day, phase - MarketCatalogue.PhaseOpen + 1);
                        State.Phase = phase + 1;
                    }
                    else
                    {
                        Close(day);
                        State.Phase = MarketCatalogue.PhaseClose;
                        cost *= 2;
                    }
                    break;
            }
            Reach();
            return cost;
        }

        private void Reach()
        {
            if (State.Now > State.Frontier)
            {
                State.FrontierDay = State.Day;
                State.FrontierPhase = State.Phase;
            }
            Tape?.Reach(State.Day, State.Phase, false);
        }

        // The day's secret: the tape's if it has it, else a fresh one, written to the tape at once.
        private void Secret(int day)
        {
            if (_secretDay == day)
            {
                return;
            }
            ulong secret;
            if (Tape == null || !Tape.Secret(day, out secret))
            {
                secret = _world.NewSecret();
                Tape?.AddSecret(day, secret);
            }
            _secret = secret;
            _secretDay = day;
        }

        // Events of the city that came in during the moment just played, or the tape's for it.
        private void ApplyEvents(int day, int phase)
        {
            _due.Clear();
            if (Replaying)
            {
                Tape?.EventsAt(day, phase, _due);
            }
            else
            {
                for (int i = 0; i < _live.Count; i++)
                {
                    MarketEvent e = _live[i];
                    e.Day = day;
                    e.Phase = phase;
                    _due.Add(e);
                    Tape?.AddEvent(e);
                }
            }
            _live.Clear();
            for (int i = 0; i < _due.Count; i++)
            {
                Apply(_due[i]);
            }
        }

        private void Apply(in MarketEvent e)
        {
            if (e.Kind == MarketInputs.EventTrade || e.Kind == MarketInputs.EventVolume)
            {
                // A player's trade in a Going Public session: its lasting move of the price, or its shares, on one company.
                Stock traded = State.Stock(e.A);
                if (traded != null && traded.Listed)
                {
                    if (e.Kind == MarketInputs.EventTrade)
                    {
                        traded.Mis += e.B / 1000000.0;
                        traded.Sent += e.B / 1000000.0;
                        traded.Last = traded.MidCents;
                    }
                    else
                    {
                        traded.DayVolume += Math.Max(0, e.B);
                    }
                }
                return;
            }
            int tradeSector = e.Kind == MarketInputs.EventBuzz ? SectorOfTrade(e.A) : -1;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed)
                {
                    continue;
                }
                int bp = MarketInputs.EventBp(e, s.Sector);
                if (tradeSector >= 0)
                {
                    bp += MarketInputs.BuzzBp(e, s.Sector, (MarketSector)tradeSector);
                }
                if (e.Kind == MarketInputs.EventFire && e.A >= 0 && s.Def.Trade != null && e.A < MarketInputs.Trades.Length && MarketInputs.Trades[e.A] == s.Def.Trade)
                {
                    bp += MarketInputs.FireTradeKickBp * (s.Size == MarketSize.Small ? 2 : s.Size == MarketSize.Mid ? 1 : 0) / 2;
                }
                if (bp != 0)
                {
                    s.Mis += bp / 10000.0;   // the market's mood: it fades
                    s.Sent += bp / 10000.0;
                    s.Last = s.MidCents;
                }
            }
            if (e.Kind == MarketInputs.EventPolicy && e.A >= 0 && e.A < MarketEnv.LeverCount)
            {
                // The policy is part of the business from now on, and the market prices it the moment it hears of it.
                int[] positions = new int[MarketEnv.LeverCount];
                for (int i = 0; i < MarketEnv.LeverCount; i++)
                {
                    positions[i] = State.Env.Lever(i);
                }
                positions[e.A] = Math.Max(-1, Math.Min(1, positions[e.A] + e.B));
                State.Env.Levers = MarketEnv.Packed(positions);
                for (int i = 0; i < State.Stocks.Count; i++)
                {
                    Stock s = State.Stocks[i];
                    if (s.Listed)
                    {
                        s.LogFair = MarketLife.LogFairOf(s, State.Env, State.Day, Year);
                        s.Last = s.MidCents;
                    }
                }
            }
            if (e.Kind == MarketInputs.EventElection)
            {
                State.ElectionsSeen = Math.Max(State.ElectionsSeen, e.A);
            }
        }

        // ---------------------------------------------------------------- 07:30: results

        private void Results(int day)
        {
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Listed && s.ResultsDay == day && s.PendingQuarter >= 0)
                {
                    Say(MarketLife.Results(s, day, Year, _secret, State.Env));
                    if (s.DeclaredCents > 0)
                    {
                        Say(new MarketNews { Day = day, Kind = MarketNewsKind.Dividend, Stock = s.Id, A = s.DeclaredCents, B = s.RecordDay, C = s.PayDay });
                    }
                }
            }
        }

        // ---------------------------------------------------------------- 09:30: the open

        private void Open(int day)
        {
            MarketEnv env;
            if (Tape == null || !Tape.Env(day, out env))
            {
                env = _world.Env(day);
                Tape?.AddEnv(day, env);
            }
            int ratePriced = State.RateSeenBp;
            int indexBefore = State.IndexLast;
            State.Env = env;
            Business(day);

            // The night: how long since the last session, and the crash and crazes that may begin with the morning.
            int shut = 1;
            for (int d = day - 1; d >= 1 && !MarketCatalogue.TradingDay(d); d--)
            {
                shut++;
            }
            double night = MarketCatalogue.OvernightShare * (1.0 + MarketCatalogue.WeekendExtra * (shut - 1));
            double variance = DayVariance(env);
            double vs = MarketCatalogue.VolScale(Year);
            double marketSd = MarketCatalogue.IndexDayVol * vs * Math.Sqrt(variance * night);
            double zMarket = MarketRandom.Fat(_secret, MarketRandom.OpenShock, MarketRandom.OpenTail, 0, 0);
            double zMood = MarketRandom.Normal(_secret, MarketRandom.OpenMood, 0, 0);
            double marketNews = marketSd * MarketNews * zMarket, marketMood = marketSd * MarketMood * zMood;
            State.DayZ = Math.Sqrt(night) * (MarketNews * zMarket + MarketMood * zMood);
            State.DayVar = night;
            if (Crashes(env))
            {
                double size = MarketCatalogue.CrashLeast + (MarketCatalogue.CrashMost - MarketCatalogue.CrashLeast) * MarketRandom.Unit(_secret, MarketRandom.CrashSize, 0, 0);
                // (a jump of its own: the variance answers it below, not through the day's shock as well)
                marketNews -= size * (1.0 - MarketCatalogue.CrashMoodShare);
                marketMood -= size * MarketCatalogue.CrashMoodShare;
                State.Variance = Math.Min(MarketCatalogue.VarianceMost, State.Variance * 3.0);
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Crash, A = -(int)Math.Round(size * 10000.0) });
            }
            double marketNewsVar = marketSd * marketSd * (1.0 - MarketCatalogue.MarketMoodShare);
            for (int sec = 0; sec < Sectors; sec++)
            {
                double sd = MarketCatalogue.Sectors[sec].SectorVolBp / 10000.0 * vs * Math.Sqrt(SectorVariance(sec, env) * night);
                double z = MarketRandom.Fat(_secret, MarketRandom.OpenShock, MarketRandom.OpenTail, MarketRandom.MoodSector + sec, 0);
                double zs = MarketRandom.Normal(_secret, MarketRandom.OpenMood, MarketRandom.MoodSector + sec, 0);
                State.SectorDayZ[sec] = Math.Sqrt(night) * (SectorNews * z + SectorMood * zs);
                _sectorSd[sec] = sd;
                _sectorNews[sec] = sd * SectorNews * z;
                _sectorMood[sec] = sd * SectorMood * zs + MarketInputs.DayKickBp(env, (MarketSector)sec) / 10000.0 + Mania(sec, day);
            }
            // An election counted: the doubt is over.
            if (State.ElectionsSeen >= 0 && env.Elections > State.ElectionsSeen)
            {
                marketMood += MarketInputs.ElectionKickBp / 10000.0;
            }
            State.ElectionsSeen = env.Elections;

            // A listing announced for today comes to the market.
            if (State.ListingId > 0 && State.ListingDay <= day)
            {
                List(day);
            }
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed)
                {
                    continue;
                }
                // A dividend whose record day has passed goes out of the price.
                if (s.DeclaredCents > 0 && !s.ExDone && s.RecordDay > 0 && s.RecordDay < day)
                {
                    s.ExDone = true;
                    s.LastExDay = day;
                }
                if (s.SplitDay > 0 && s.SplitDay <= day)
                {
                    MarketLife.Split(State, s, s.SplitRatio);
                }
                double beta = s.Def.Beta / 100.0;
                int sector = (int)s.Sector;
                double own = s.Def.IdioBp / 10000.0 * vs * Math.Sqrt(OwnVariance(variance) * night) * (s.ResultsBoostDay == day ? Math.Sqrt(MarketCatalogue.ResultsDayVolScale) : 1.0);
                double z = MarketRandom.Fat(_secret, MarketRandom.OpenShock, MarketRandom.OpenTail, s.Id, 1);
                double zo = MarketRandom.Normal(_secret, MarketRandom.OpenMood, s.Id, 1);
                s.LogFair = MarketLife.LogFairOf(s, env, day, Year);
                if (s.BidCents > 0)
                {
                    double held = BidMarketShare * (beta * (marketNews + marketMood) + _sectorNews[sector] + _sectorMood[sector]) + BidOwnShare * own * z;
                    s.Sent = s.Sent * Math.Max(0.0, 1.0 - BidKappa * night) + held;
                    s.Mis = s.Sent;
                }
                else
                {
                    double news = beta * marketNews + _sectorNews[sector] + own * OwnNews * z
                                  - 0.5 * (beta * beta * marketNewsVar + _sectorSd[sector] * _sectorSd[sector] * (1.0 - MarketCatalogue.SectorMoodShare) + own * own * (1.0 - MarketCatalogue.OwnMoodShare));
                    double mood = beta * marketMood + _sectorMood[sector] + own * OwnMood * zo;
                    if (s.ListedDay == day && s.Daily.Count == 0)
                    {
                        // A new listing's first trade: the price it was offered at, and the first morning's interest in it.
                        mood += MarketLife.IpoPopMean + MarketLife.IpoPopSpread * MarketRandom.Normal(_secret, MarketRandom.Listing, s.Id, 1);
                    }
                    double was = s.Sent;
                    s.Sent = s.Sent * (1.0 - Kappa * night) + mood;
                    s.Mis += news + (s.Sent - was);
                }
                if (s.LastBarCount >= 0 && s.BarCount > 0)
                {
                    Array.Copy(s.Bars, s.LastBars, s.BarCount);
                    s.LastBarCount = s.BarCount;
                    s.LastBarsDay = day - shut;
                }
                s.BarCount = 0;
                int open = s.MidCents;
                s.Open = s.High = s.Low = s.Last = open;
                s.DayVolume = 0;
            }
            if (State.IndexBarCount > 0)
            {
                Array.Copy(State.IndexBars, State.IndexLastBars, State.IndexBarCount);
                State.IndexLastBarCount = State.IndexBarCount;
                State.IndexLastBarsDay = day - shut;
            }
            State.IndexBarCount = 0;
            Index(out int indexNow);
            State.IndexOpen = State.IndexHigh = State.IndexLow = State.IndexLast = indexNow;
            for (int sec = 0; sec < Sectors; sec++)
            {
                _sectorOpen[sec] = State.SectorLast[sec];
            }
            if (ratePriced >= 0 && env.RateBp != ratePriced && indexBefore > 0)
            {
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Rate, A = env.RateBp - ratePriced, B = (int)((long)(indexNow - indexBefore) * 10000L / indexBefore) });
            }
            State.RateSeenBp = env.RateBp;
            OpenOrders(day);
            Margin(day, MarketCatalogue.PhaseOpen);
        }

        // Every day of the calendar since the last open: each company's business, its quarters.
        private void Business(int day)
        {
            int from = State.BusinessDay > 0 ? State.BusinessDay + 1 : day;
            from = Math.Max(from, day - 30);   // (a very long gap: the last month of it is enough for the business)
            MarketEnv env = State.Env;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed)
                {
                    continue;
                }
                double competition = Competition(s, env);
                for (int d = from; d <= day; d++)
                {
                    int quarter = MarketCatalogue.QuarterOf(d, Year);
                    if (quarter != s.Quarter)
                    {
                        if (s.PendingQuarter >= 0)
                        {
                            // Results that were never given out (a long gap): out now, with the next quarter coming.
                            Say(MarketLife.Results(s, day, Year, _secret, env));
                        }
                        MarketLife.CloseQuarter(s, quarter, Year);
                        if (s.ResultsDay <= day)
                        {
                            s.ResultsDay = MarketTrading.NextTradingDay(day);   // (this morning's results are already out)
                        }
                    }
                    MarketLife.Business(s, env, Year, _secret, d, competition);
                }
            }
            State.BusinessDay = day;
        }

        // The share of a company's turnover the player's businesses in its trade take: half their takings in the trade
        // a year, against all the listed companies in it together.
        private double Competition(Stock s, in MarketEnv env)
        {
            if (s.Def.Trade == null || env.Trades <= 0 || env.TradeOf == null)
            {
                return 0.0;
            }
            int trade = Array.IndexOf(MarketInputs.Trades, s.Def.Trade);
            double sales = 0.0;
            for (int i = 0; i < env.Trades && i < env.TradeOf.Length; i++)
            {
                if (env.TradeOf[i] == trade)
                {
                    sales += env.TradeSales[i];
                }
            }
            if (sales <= 0.0)
            {
                return 0.0;
            }
            double all = 0.0;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock other = State.Stocks[i];
                if (other.Listed && other.Def.Trade == s.Def.Trade)
                {
                    all += other.Rev;
                }
            }
            return all <= 0.0 ? 0.0 : Math.Min(MarketInputs.CompetitionMost, sales * Year * MarketInputs.PlayerShareTaken / all);
        }

        private double DayVariance(in MarketEnv env)
        {
            return State.Variance * MarketCatalogue.CycleVolatility[MarketValuation.Clamp(env.Cycle, 0, 3)] * (env.Campaign != 0 ? 1.1 : 1.0);
        }

        private double SectorVariance(int sector, in MarketEnv env)
        {
            return Math.Max(MarketCatalogue.VarianceLeast, State.SectorVar[sector] <= 0.0 ? 1.0 : State.SectorVar[sector])
                   * MarketCatalogue.CycleVolatility[MarketValuation.Clamp(env.Cycle, 0, 3)];
        }

        // A company's own moves grow in a nervous market too, by half as much.
        private static double OwnVariance(double marketVariance)
        {
            double m = 0.5 + 0.5 * Math.Sqrt(Math.Max(0.1, marketVariance));
            return m * m;
        }

        // Whether the market crashes this morning: rare; more likely in a slowdown or a recession, and when it is dear.
        private bool Crashes(in MarketEnv env)
        {
            double chance = MarketCatalogue.CrashChanceYear / TradingYear;
            if (env.Cycle == 3)
            {
                chance *= MarketCatalogue.CrashRecessionScale;
            }
            else if (env.Cycle == 2)
            {
                chance *= MarketCatalogue.CrashSlowdownScale;
            }
            if (Dearness() > 0.08)
            {
                chance *= MarketCatalogue.CrashDearScale;
            }
            return MarketRandom.Unit(_secret, MarketRandom.Crash, 0, 0) < chance;
        }

        // How far the large companies' prices are above what they are worth, on average.
        private double Dearness()
        {
            double sum = 0.0;
            int count = 0;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Listed && s.Size == MarketSize.Large)
                {
                    sum += s.Mis;
                    count++;
                }
            }
            return count > 0 ? sum / count : 0.0;
        }

        // A sector's craze: may begin this morning, or burst. While it runs, its companies drift up a little each day; when it
        // bursts it gives back on the morning what the drift was worth against the chance of the burst, so riding it pays
        // nothing on average.
        private double Mania(int sector, int day)
        {
            double perDay = 1.0 / Math.Max(5.0, (MarketCatalogue.ManiaDaysLeast + MarketCatalogue.ManiaDaysMost) / 2.0);
            if (State.ManiaDays[sector] > 0)
            {
                if (MarketRandom.Unit(_secret, MarketRandom.Mania, sector, 1) < perDay)
                {
                    State.ManiaDays[sector] = 0;
                    double back = MarketCatalogue.ManiaDrift / perDay;
                    State.ManiaAdded[sector] = 0.0;
                    State.Variance = Math.Min(MarketCatalogue.VarianceMost, State.Variance * 1.3);
                    State.SectorVar[sector] = Math.Min(MarketCatalogue.VarianceMost, Math.Max(1.0, State.SectorVar[sector]) * 2.5);
                    Say(new MarketNews { Day = day, Kind = MarketNewsKind.Mania, A = sector, B = 2 });
                    return -back;
                }
                State.ManiaDays[sector]++;
                State.ManiaAdded[sector] += MarketCatalogue.ManiaDrift;
                return 0.0;   // (the drift is added step by step)
            }
            if (MarketRandom.Unit(_secret, MarketRandom.Mania, sector, 0) < MarketCatalogue.ManiaChanceYear / TradingYear)
            {
                State.ManiaDays[sector] = 1;
                State.ManiaAdded[sector] = 0.0;
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Mania, A = sector, B = 1 });
            }
            return 0.0;
        }

        // ---------------------------------------------------------------- a step of the session

        private void Step(int day, int k)
        {
            MarketEnv env = State.Env;
            double w = _weight[k] * (1.0 - MarketCatalogue.OvernightShare);
            double variance = DayVariance(env);
            double vs = MarketCatalogue.VolScale(Year);
            double marketSd = MarketCatalogue.IndexDayVol * vs * Math.Sqrt(variance * w);
            double zm = MarketRandom.Fat(_secret, MarketRandom.MarketShock, MarketRandom.MarketTail, k, 0);
            double zmm = MarketRandom.Normal(_secret, MarketRandom.Mood, k, 0);
            double marketNews = marketSd * MarketNews * zm, marketMood = marketSd * MarketMood * zmm;
            double marketNewsVar = marketSd * marketSd * (1.0 - MarketCatalogue.MarketMoodShare);
            State.DayZ += Math.Sqrt(w) * (MarketNews * zm + MarketMood * zmm);
            State.DayVar += w;
            for (int sec = 0; sec < Sectors; sec++)
            {
                double sd = MarketCatalogue.Sectors[sec].SectorVolBp / 10000.0 * vs * Math.Sqrt(SectorVariance(sec, env) * w);
                double z = MarketRandom.Fat(_secret, MarketRandom.SectorShock, MarketRandom.SectorTail, k, sec);
                double zs = MarketRandom.Normal(_secret, MarketRandom.Mood, k, MarketRandom.MoodSector + sec);
                State.SectorDayZ[sec] += Math.Sqrt(w) * (SectorNews * z + SectorMood * zs);
                _sectorSd[sec] = sd;
                _sectorNews[sec] = sd * SectorNews * z;
                _sectorMood[sec] = sd * SectorMood * zs + (State.ManiaDays[sec] > 0 ? MarketCatalogue.ManiaDrift / Steps : 0.0);
            }
            double ownScale = vs * Math.Sqrt(OwnVariance(variance) * w);
            double fade = Kappa * w, bidFade = BidKappa * w;
            double decay = 1.0 - 1.0 / MarketCatalogue.ImpactMemorySteps;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed)
                {
                    continue;
                }
                double beta = s.Def.Beta / 100.0;
                int sector = (int)s.Sector;
                double own = s.Def.IdioBp / 10000.0 * ownScale * (s.ResultsBoostDay == day ? Math.Sqrt(MarketCatalogue.ResultsDayVolScale) : 1.0);
                double z = MarketRandom.Fat(_secret, MarketRandom.OwnShock, MarketRandom.OwnTail, k, s.Id);
                double before = s.LogFair + s.Mis;
                double sd;
                if (s.BidCents > 0)
                {
                    double held = BidMarketShare * (beta * (marketNews + marketMood) + _sectorNews[sector] + _sectorMood[sector]) + BidOwnShare * own * z;
                    s.Sent = s.Sent * (1.0 - bidFade) + held;
                    s.Mis = s.Sent;
                    sd = Math.Sqrt(BidMarketShare * BidMarketShare * (beta * beta * marketSd * marketSd + _sectorSd[sector] * _sectorSd[sector]) + BidOwnShare * BidOwnShare * own * own);
                }
                else
                {
                    double zo = MarketRandom.Normal(_secret, MarketRandom.Mood, k, MarketRandom.MoodOwn + s.Id);
                    double news = beta * marketNews + _sectorNews[sector] + own * OwnNews * z
                                  - 0.5 * (beta * beta * marketNewsVar + _sectorSd[sector] * _sectorSd[sector] * (1.0 - MarketCatalogue.SectorMoodShare) + own * own * (1.0 - MarketCatalogue.OwnMoodShare));
                    double mood = beta * marketMood + _sectorMood[sector] + own * OwnMood * zo;
                    double was = s.Sent;
                    s.Sent = s.Sent * (1.0 - fade) + mood;
                    s.Mis += news + (s.Sent - was);
                    sd = Math.Sqrt(beta * beta * marketSd * marketSd + _sectorSd[sector] * _sectorSd[sector] + own * own);
                }
                double after = s.LogFair + s.Mis;
                Bar(s, day, k, before, after, sd);
                if (s.Impact > 0.0)
                {
                    s.Impact = s.Impact > 1.0 ? s.Impact * decay : 0.0;
                }
            }
            Index(out int index);
            int prev = State.IndexBarCount > 0 ? State.IndexBars[State.IndexBarCount - 1].C : State.IndexOpen;
            if (State.IndexBarCount < State.IndexBars.Length)
            {
                State.IndexBars[State.IndexBarCount++] = new Candle(day, prev, Math.Max(prev, index), Math.Min(prev, index), index, 0);
            }
            State.IndexHigh = Math.Max(State.IndexHigh, index);
            State.IndexLow = Math.Min(State.IndexLow, index);
            State.IndexLast = index;
            RestingOrders(day, k);
            Margin(day, MarketCatalogue.PhaseOpen + k);
        }

        // The step's candle of a company: its first and last price, and the highest and lowest it reached between (the
        // extremes of a random walk tied to both ends, drawn exactly), and the shares traded.
        private void Bar(Stock s, int day, int k, double from, double to, double sd)
        {
            double width = Math.Abs(to - from);
            double var = sd * sd;
            double up = Math.Max(from, to) + Math.Max(0.0, (Math.Sqrt(width * width - 2.0 * var * Math.Log(MarketRandom.Unit(_secret, MarketRandom.BarHigh, k, s.Id))) - width) * 0.5);
            double down = Math.Min(from, to) - Math.Max(0.0, (Math.Sqrt(width * width - 2.0 * var * Math.Log(MarketRandom.Unit(_secret, MarketRandom.BarLow, k, s.Id))) - width) * 0.5);
            int o = Cents(from), c = Cents(to);
            int h = Math.Max(Math.Max(o, c), Cents(up)), l = Math.Min(Math.Min(o, c), Cents(down));
            double normal = sd > 0.0 ? width / sd : 0.0;
            long v = (long)(s.Adv * _weight[k] * (0.7 + 0.45 * normal) * Math.Exp(0.3 * MarketRandom.Normal(_secret, MarketRandom.Volume, k, s.Id)) * (s.ResultsBoostDay == day ? 2.5 : 1.0));
            if (s.BarCount < s.Bars.Length)
            {
                s.Bars[s.BarCount++] = new Candle(day, o, h, l, c, v);
            }
            s.High = Math.Max(s.High, h);
            s.Low = Math.Min(s.Low, l);
            s.Last = c;
            s.DayVolume += v;
        }

        private static int Cents(double logPrice)
        {
            double cents = Math.Exp(logPrice) * 100.0;
            return cents >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)Math.Round(cents));
        }

        // The city's index and the sectors': every listed company's market value over their divisors.
        private void Index(out int index)
        {
            double all = 0.0;
            for (int sec = 0; sec < Sectors; sec++)
            {
                _sectorValue[sec] = 0.0;
            }
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed)
                {
                    continue;
                }
                double value = s.Shares * s.Mid;
                all += value;
                _sectorValue[(int)s.Sector] += value;
            }
            if (State.IndexDivisor <= 0.0)
            {
                State.IndexDivisor = all / 1000.0;
            }
            index = State.IndexDivisor > 0.0 ? (int)Math.Round(all / State.IndexDivisor * 100.0) : 0;
            for (int sec = 0; sec < Sectors; sec++)
            {
                if (State.SectorDivisor[sec] <= 0.0 && _sectorValue[sec] > 0.0)
                {
                    State.SectorDivisor[sec] = _sectorValue[sec] / 1000.0;
                }
                State.SectorLast[sec] = State.SectorDivisor[sec] > 0.0 ? (int)Math.Round(_sectorValue[sec] / State.SectorDivisor[sec] * 100.0) : 0;
            }
        }

        // A company joins or leaves the indices: their divisors change so that the indices do not jump. Called before the
        // change (a company joining is not counted yet; one leaving still is).
        private void Reweigh(Stock s, bool joining)
        {
            Index(out int index);
            double value = s.Shares * s.Mid;
            if (index > 0 && State.IndexDivisor > 0.0)
            {
                double all = State.IndexDivisor * index / 100.0;
                double after = all + (joining ? value : -value);
                State.IndexDivisor = after > 0.0 ? after / (index / 100.0) : 0.0;
            }
            int sec = (int)s.Sector;
            int level = State.SectorLast[sec];
            if (level > 0 && State.SectorDivisor[sec] > 0.0)
            {
                double all = State.SectorDivisor[sec] * level / 100.0;
                double after = all + (joining ? value : -value);
                State.SectorDivisor[sec] = after > 0.0 ? after / (level / 100.0) : 0.0;
            }
        }

        // ---------------------------------------------------------------- 16:00: the close

        private void Close(int day)
        {
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed)
                {
                    continue;
                }
                var candle = new Candle(day, s.Open, s.High, s.Low, s.Last, s.DayVolume);
                MarketHistory.AddDay(s.Daily, s.Monthly, candle, Year);
                s.Adv = s.Adv * 0.95 + Math.Max(s.Adv * 0.3, s.DayVolume) * 0.05;
            }
            MarketHistory.AddDay(State.IndexDaily, State.IndexMonthly, new Candle(day, State.IndexOpen, State.IndexHigh, State.IndexLow, State.IndexLast, 0), Year);
            for (int sec = 0; sec < Sectors; sec++)
            {
                List<int> closes = State.SectorDaily[sec];
                closes.Add(State.SectorLast[sec]);
                while (closes.Count > MarketCatalogue.DailyKept)
                {
                    closes.RemoveAt(0);
                }
            }
            Settle(day);
            Dividends(day);
            Corporate(day);
            Variance();
            Interest(day);
            Margin(day, MarketCatalogue.PhaseClose);
            Expire(day);
            CloseNews(day);
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Listed)
                {
                    s.PrevClose = s.Last;
                }
            }
            State.IndexPrevClose = State.IndexLast;
            for (int sec = 0; sec < Sectors; sec++)
            {
                State.SectorPrevClose[sec] = State.SectorLast[sec];
            }
            // Old news goes.
            for (int i = State.News.Count - 1; i >= 0; i--)
            {
                if (State.News[i].Day < day - 6)
                {
                    State.News.RemoveAt(i);
                }
            }
        }

        // After the close: what the day's news did to each price goes into what the market expects of the business (its
        // Outlook), worked out so that the price stays exactly where it closed; what is left in the distance from the
        // business's worth is the mood alone.
        private void Settle(int day)
        {
            MarketEnv env = State.Env;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed || s.BidCents > 0)
                {
                    continue;
                }
                double logPrice = s.LogFair + s.Mis;
                double accrued = MarketLife.Accrued(s, day, Year);
                double rest = Math.Exp(logPrice - s.Sent) - accrued;
                if (rest > 0.0)
                {
                    s.Outlook = MarketValuation.OutlookFor(s, env, rest, s.Outlook);
                }
                s.LogFair = MarketLife.LogFairOf(s, env, day, Year);
                s.Mis = logPrice - s.LogFair;
                s.Sent = s.Mis;
            }
        }

        // The market's and the sectors' variance for tomorrow, from today's moves.
        private void Variance()
        {
            double z = State.DayVar > 0.0 ? State.DayZ / Math.Sqrt(State.DayVar) : 0.0;
            double omega = 1.0 - MarketCatalogue.GarchA - MarketCatalogue.GarchB - MarketCatalogue.GarchGamma * 0.5;
            double v = State.Variance;
            v = omega + (MarketCatalogue.GarchA + (z < 0.0 ? MarketCatalogue.GarchGamma : 0.0)) * z * z * v + MarketCatalogue.GarchB * v;
            State.Variance = MarketValuation.Clamp(v, MarketCatalogue.VarianceLeast, MarketCatalogue.VarianceMost);
            State.LastShock = z;
            double sectorOmega = 1.0 - MarketCatalogue.SectorGarchA - MarketCatalogue.SectorGarchB;
            for (int sec = 0; sec < Sectors; sec++)
            {
                double sz = State.DayVar > 0.0 ? State.SectorDayZ[sec] / Math.Sqrt(State.DayVar) : 0.0;
                double sv = State.SectorVar[sec] <= 0.0 ? 1.0 : State.SectorVar[sec];
                sv = sectorOmega + MarketCatalogue.SectorGarchA * sz * sz * sv + MarketCatalogue.SectorGarchB * sv;
                State.SectorVar[sec] = MarketValuation.Clamp(sv, MarketCatalogue.VarianceLeast, MarketCatalogue.VarianceMost);
                State.SectorShock[sec] = sz;
                State.SectorDayZ[sec] = 0.0;
            }
            State.DayZ = 0.0;
            State.DayVar = 0.0;
        }

        // ---------------------------------------------------------------- dividends

        private void Dividends(int day)
        {
            // Holders at today's close of a company whose record day it is are owed its dividend.
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed || s.RecordDay != day || s.DeclaredCents <= 0)
                {
                    continue;
                }
                Position p = State.Position(s.Id);
                if (p != null && p.Shares != 0)
                {
                    State.Dues.Add(new DividendDue { StockId = s.Id, Cents = p.Shares * s.DeclaredCents, PayDay = s.PayDay, Test = p.Test });
                }
                // The company pays it out of its book.
                s.Equity -= s.Shares * s.DeclaredCents / 100.0;
            }
            for (int i = State.Dues.Count - 1; i >= 0; i--)
            {
                DividendDue due = State.Dues[i];
                if (due.PayDay > day)
                {
                    continue;
                }
                State.Dues.RemoveAt(i);
                Stock s = State.Stock(due.StockId);
                Position p = State.Position(due.StockId);
                if (due.Cents > 0)
                {
                    long money = due.Cents / 100;   // rounded down
                    if (money <= 0)
                    {
                        continue;
                    }
                    if (State.Account.Margin)
                    {
                        State.Account.Cash += money;
                    }
                    else
                    {
                        _world.Wallet.Give(money, MarketMoney.Dividend, s != null ? s.Name : string.Empty);
                    }
                    State.Account.Dividends += money;
                    if (p != null)
                    {
                        p.Dividends += money;
                    }
                    MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = due.StockId, Kind = TradeKind.Dividend, Value = money, Test = due.Test });
                    _world.Paid(TradeKind.Dividend, s, money);
                }
                else if (due.Cents < 0)
                {
                    long money = (-due.Cents + 99) / 100;   // rounded up: what a short owes the lender
                    State.Account.Cash -= money;
                    State.Account.DividendsPaid += money;
                    if (p != null)
                    {
                        p.Dividends -= money;
                    }
                    MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = due.StockId, Kind = TradeKind.DividendPaid, Value = -money, Test = due.Test });
                    _world.Paid(TradeKind.DividendPaid, s, money);
                }
            }
        }

        // ---------------------------------------------------------------- the companies' lives

        private void Corporate(int day)
        {
            double year = TradingYear;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed)
                {
                    continue;
                }
                // A takeover bid due today completes or falls through (decided now, never before).
                if (s.BidCents > 0 && s.BidEndDay <= day)
                {
                    if (MarketRandom.Unit(_secret, MarketRandom.Takeover, s.Id, 1) < MarketCatalogue.TakeoverFailChance)
                    {
                        // Back to what it is worth on its own, from the next open.
                        s.BidCents = 0;
                        s.BidEndDay = 0;
                        s.BidDay = 0;
                        Say(new MarketNews { Day = day, Kind = MarketNewsKind.BidFailed, Stock = s.Id });
                    }
                    else
                    {
                        Leave(s, day, GoneReason.TakenOver, s.BidCents);
                        continue;
                    }
                }
                // Shares that have traded under a unit for weeks leave the market at their last price.
                s.LowDays = s.Last * 1000L < (long)MarketCatalogue.PennyCents * Math.Max(100, State.Env.PriceLevel) ? s.LowDays + 1 : 0;
                if (s.LowDays >= MarketCatalogue.PennyDays)
                {
                    Leave(s, day, GoneReason.Delisted, s.Last);
                    continue;
                }
                // A company in distress may fail (the market prices the chance: MarketValuation.Fair).
                double hazard = MarketValuation.Hazard(s.Distress);
                if (hazard > 0.0 && MarketRandom.Unit(_secret, MarketRandom.Failure, s.Id, 0) < hazard / year)
                {
                    int payout = 0;
                    if (MarketRandom.Unit(_secret, MarketRandom.Recovery, s.Id, 0) < MarketCatalogue.RecoveryChance)
                    {
                        payout = (int)(s.Last * MarketCatalogue.RecoveryMost * (1.0 - s.Distress) * MarketRandom.Unit(_secret, MarketRandom.Recovery, s.Id, 1));
                    }
                    Leave(s, day, GoneReason.Failed, payout);
                    continue;
                }
                // News of its own: a crisis or a windfall (priced too: MarketValuation.OwnNewsLoss).
                if (s.BidCents <= 0)
                {
                    double scale = MarketCatalogue.CrisisSizeScale[(int)s.Size] / year;
                    double draw = MarketRandom.Unit(_secret, MarketRandom.Crisis, s.Id, 0);
                    if (draw < MarketCatalogue.CrisisChanceYear * scale)
                    {
                        CompanyNews(s, day, true, false);
                    }
                    else if (draw < (MarketCatalogue.CrisisChanceYear + MarketCatalogue.WindfallChanceYear) * scale)
                    {
                        CompanyNews(s, day, false, false);
                    }
                }
                // A bid for it: small companies are bid for more often (priced too: MarketValuation.TakeoverYield).
                if (s.BidCents <= 0 && day - s.ListedDay > 10 && MarketValuation.TakeoverYield(s) > 0.0)
                {
                    double chance = MarketCatalogue.TakeoverChanceYear / year * MarketCatalogue.TakeoverSizeScale[(int)s.Size];
                    if (MarketRandom.Unit(_secret, MarketRandom.Takeover, s.Id, 0) < chance)
                    {
                        Bid(s, day);
                    }
                }
                // A board splits its shares when they trade high.
                if (s.SplitDay <= 0 && s.BidCents <= 0 && s.Last > MarketCatalogue.SplitAboveCents)
                {
                    s.SplitRatio = MarketLife.SplitRatioAt(s.Last);
                    s.SplitDay = MarketTrading.MarketDays(day, MarketCatalogue.SplitNoticeDays);
                    Say(new MarketNews { Day = day, Kind = MarketNewsKind.Split, Stock = s.Id, A = s.SplitRatio, B = s.SplitDay });
                }
            }
            // A new listing announced, while the pipeline has a company to bring.
            if (State.ListingId <= 0 && MarketRandom.Unit(_secret, MarketRandom.Listing, 0, 0) < MarketCatalogue.ListingChanceYear / year && State.Stocks.Count < MarketCatalogue.CompaniesMost)
            {
                CompanyDef next = NextListing(day);
                if (next != null)
                {
                    State.ListingId = next.Id;
                    State.ListingDay = MarketTrading.MarketDays(day, MarketCatalogue.ListingNoticeDays);
                    Say(new MarketNews { Day = day, Kind = MarketNewsKind.Listing, Stock = next.Id, A = 0, B = State.ListingDay });
                }
            }
        }

        // A company's own big news, after the close: its usual margin moves for good, and its price by what that does to its
        // worth, at the next open.
        private void CompanyNews(Stock s, int day, bool crisis, bool session)
        {
            MarketEnv env = State.Env;
            double u = MarketRandom.Unit(_secret, MarketRandom.CrisisTerms, s.Id, 0);
            double usual = Math.Max(0.02, s.Def.MarginBp / 10000.0);
            double hit = (crisis ? -(MarketCatalogue.CrisisMarginLeast + (MarketCatalogue.CrisisMarginMost - MarketCatalogue.CrisisMarginLeast) * u)
                                 : MarketCatalogue.WindfallMarginLeast + (MarketCatalogue.WindfallMarginMost - MarketCatalogue.WindfallMarginLeast) * u) * usual;
            double before = MarketValuation.Fair(s, env);
            double shift = MarketValuation.Clamp(s.MarginShift + hit, -2.0 * usual, usual + 0.05);
            double change = shift - s.MarginShift;
            s.MarginShift = shift;
            s.Margin = MarketValuation.Clamp(s.Margin + change, -0.5, 0.8);
            s.MarginTarget += change;
            s.Distress = MarketValuation.DistressOf(s, env);
            double after = MarketValuation.Fair(s, env);
            // The market prices it at once (after the close: what the shares are worth now, traded from the next open).
            s.LogFair = MarketLife.LogFairOf(s, env, State.Day, Year);
            if (session)
            {
                s.Last = s.MidCents;
            }
            s.ResultsBoostDay = MarketTrading.NextTradingDay(day);   // (its own moves are larger the day after, as on a results day)
            Say(new MarketNews { Day = day, Kind = crisis ? MarketNewsKind.Crisis : MarketNewsKind.Windfall, Stock = s.Id, A = (int)Math.Round(Math.Abs(after / before - 1.0) * 10000.0) });
        }

        // A takeover bid: a premium on today's price, to complete in some weeks. From the next open the price is what the
        // bid is worth less the chance it falls through, and the market's mood about the company no longer counts.
        private void Bid(Stock s, int day)
        {
            double premium = MarketCatalogue.TakeoverPremiumLeast + (MarketCatalogue.TakeoverPremiumMost - MarketCatalogue.TakeoverPremiumLeast) * MarketRandom.Unit(_secret, MarketRandom.TakeoverTerms, s.Id, 0);
            s.BidCents = Math.Max(1, (int)Math.Round(s.Mid * (1.0 + premium) * 100.0));
            s.Mis -= s.Sent;
            s.Sent = 0.0;
            s.BidDay = day;
            s.BidEndDay = MarketTrading.MarketDays(day, MarketCatalogue.TakeoverDaysLeast + MarketRandom.Below(_secret, MarketRandom.TakeoverTerms, s.Id, 1,
                MarketCatalogue.TakeoverDaysMost - MarketCatalogue.TakeoverDaysLeast + 1));
            // The bidder: a larger company of the same sector, if there is one; it pays a premium and its price eases.
            _scratch.Clear();
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock other = State.Stocks[i];
                if (other.Listed && other != s && other.Sector == s.Sector && other.MarketValue > s.MarketValue * 3.0 && other.BidCents <= 0)
                {
                    _scratch.Add(other);
                }
            }
            s.BidderId = 0;
            if (_scratch.Count > 0 && MarketRandom.Unit(_secret, MarketRandom.Bidder, s.Id, 0) < 0.7)
            {
                Stock bidder = _scratch[MarketRandom.Below(_secret, MarketRandom.Bidder, s.Id, 1, _scratch.Count)];
                s.BidderId = bidder.Id;
                bidder.Outlook -= 0.02;   // (the premium it pays: from the next open)
            }
            _scratch.Clear();
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.Bid, Stock = s.Id, A = s.BidCents, B = s.BidEndDay, C = s.BidderId });
        }

        /// <summary>A takeover bid for a company now (the test tools), made as one would be: from the next open its price is what the bid is worth.</summary>
        public bool BidFor(int stockId)
        {
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || s.BidCents > 0)
            {
                return false;
            }
            Bid(s, State.Day);
            return true;
        }

        /// <summary>A crisis (or a windfall) at a company now (the test tools), as one would come: its price follows at the next open.</summary>
        public bool NewsFor(int stockId, bool crisis)
        {
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || s.BidCents > 0)
            {
                return false;
            }
            CompanyNews(s, State.Day, crisis, Trading);
            return true;
        }

        /// <summary>A company leaves the market now (the test tools): failed, paying holders 'cents' a share, or taken over at that price.</summary>
        public bool Delist(int stockId, GoneReason reason, int cents)
        {
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed)
            {
                return false;
            }
            Leave(s, State.Day, reason, cents);
            return true;
        }

        // A company leaves the market: what is left is paid to its holders (a short is closed at the same price), its orders go.
        private void Leave(Stock s, int day, GoneReason reason, int centsEach)
        {
            Reweigh(s, false);
            s.Status = StockStatus.Gone;
            s.GoneDay = day;
            s.Reason = reason;
            s.GoneCents = Math.Max(0, centsEach);
            s.BidCents = 0;
            s.SplitDay = 0;
            Position p = State.Position(s.Id);
            if (p != null && p.Shares != 0)
            {
                long held = p.Shares;
                long money = MarketTrading.Value(Math.Abs(held), s.GoneCents, held < 0);
                if (held > 0)
                {
                    if (State.Account.Margin)
                    {
                        State.Account.Cash += money;
                    }
                    else if (money > 0)
                    {
                        _world.Wallet.Give(money, MarketMoney.Payout, s.Name);
                    }
                    MarketTrading.Book(p, -held, money, State.Account);
                }
                else
                {
                    State.Account.Cash -= money;
                    MarketTrading.Book(p, -held, money, State.Account);
                }
                State.Account.Payouts += held > 0 ? money : -money;
                MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = s.Id, Kind = TradeKind.Payout, Qty = Math.Abs(held), PriceCents = s.GoneCents,
                    Value = held > 0 ? money : -money, Test = p.Test });
                State.Positions.Remove(p);
                _world.Paid(TradeKind.Payout, s, held > 0 ? money : -money);
            }
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                if (o.StockId == s.Id && o.IsOpen)
                {
                    o.State = OrderState.Cancelled;
                    o.Why = OrderWhy.CompanyGone;
                }
            }
            // Its history goes but for its last day (the save keeps no charts of companies that are gone).
            if (s.Daily.Count > 1)
            {
                s.Daily.RemoveRange(0, s.Daily.Count - 1);
            }
            s.Monthly.Clear();
            s.BarCount = s.LastBarCount = 0;
            // (A dividend it already owes the player is still paid on its day: it is in the dues.)
            Say(new MarketNews { Day = day, Kind = reason == GoneReason.TakenOver ? MarketNewsKind.BidDone : reason == GoneReason.Delisted ? MarketNewsKind.Delisted : MarketNewsKind.Failure, Stock = s.Id, A = s.GoneCents });
        }

        // The next company of the pipeline; when it is empty, one that left the market two years ago or more may come back.
        private CompanyDef NextListing(int day)
        {
            int free = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int pick = pass == 1 && free > 0 ? MarketRandom.Below(_secret, MarketRandom.ListingPick, 0, 0, free) : -1;
                int seen = 0;
                for (int i = 0; i < MarketCatalogue.All.Length; i++)
                {
                    CompanyDef d = MarketCatalogue.All[i];
                    Stock s = State.Stock(d.Id);
                    bool can = d.Pipeline ? s == null : s != null && !s.Listed && day - s.GoneDay > 2 * Year;
                    if (!can)
                    {
                        continue;
                    }
                    if (pass == 0)
                    {
                        free++;
                    }
                    else if (seen++ == pick)
                    {
                        return d;
                    }
                }
                if (free == 0)
                {
                    return null;
                }
            }
            return null;
        }

        // The listing announced comes to the market at its price, at today's price level.
        private void List(int day)
        {
            CompanyDef d = MarketCatalogue.Find(State.ListingId);
            State.ListingId = 0;
            State.ListingDay = 0;
            if (d == null)
            {
                return;
            }
            Stock old = State.Stock(d.Id);
            if (old != null)
            {
                State.Stocks.Remove(old);
            }
            Stock s = MarketLife.Make(d, null, day, State.Env, Year, _secret, 0);
            s.Daily.Clear();
            s.Monthly.Clear();
            Reweigh(s, true);   // (before it is counted: the indices stay where they were)
            State.Stocks.Add(s);
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.Listing, Stock = s.Id, A = s.MidCents, B = day });
        }

        // ---------------------------------------------------------------- orders

        /// <summary>
        /// An order the player places now: checked, made, and if it is at the market while the market trades (or a limit
        /// already met) filled at once. Null with the reason when it cannot be placed.
        /// </summary>
        public Order Place(int stockId, OrderSide side, OrderType type, OrderLife life, long qty, int limitCents, int stopCents, bool open, bool held, bool test, out OrderWhy why)
        {
            Stock s = State.Stock(stockId);
            Order order = MarketTrading.Place(State, s, side, type, life, qty, limitCents, stopCents, _world.Wallet.Cash, _world.CommissionLeast, open, held, State.Phase, test, out why);
            if (order == null || !open)
            {
                return order;
            }
            if (type == OrderType.Market || (type == OrderType.Limit && Marketable(s, order)))
            {
                int cents = MarketTrading.MarketPrice(State, s, side == OrderSide.Buy, qty, State.Phase);
                if (type == OrderType.Limit && (side == OrderSide.Buy ? cents > limitCents : cents < limitCents))
                {
                    return order;   // its size moves the price past its limit: it waits for the market
                }
                if (!FillNow(s, order, qty, cents, State.Phase))
                {
                    why = order.Why;
                }
            }
            return order;
        }

        private bool Marketable(Stock s, Order o)
        {
            Quote q = MarketTrading.QuoteOf(State, s, State.Phase);
            return o.Side == OrderSide.Buy ? q.Ask <= o.LimitCents : q.Bid >= o.LimitCents;
        }

        private bool FillNow(Stock s, Order o, long qty, int cents, int phase)
        {
            // A trade's lasting move is the city's in a session, and so it is on a day played again that was played in one
            // (its tape has the move: the fill played again must not add it a second time).
            bool city = TradesAsEvents || TradeSink != null || (Replaying && Tape != null && Tape.Traded(State.Day));
            if (!MarketTrading.Fill(State, s, o, qty, cents, _world.CommissionLeast, _world.Wallet, phase, !city, out double kept))
            {
                o.State = o.Filled > 0 ? OrderState.Cancelled : OrderState.Refused;
                o.Why = o.Side == OrderSide.Buy ? (State.Account.Margin ? OrderWhy.BelowMargin : OrderWhy.NoMoney) : (State.Account.Margin ? OrderWhy.BelowMargin : OrderWhy.NoShares);
                _world.Filled(o, s, 0, 0);
                return false;
            }
            if (city)
            {
                int micros = MarketInputs.TradeMicros(kept);
                int shares = (int)Math.Min(qty, int.MaxValue);
                if (TradeSink != null)
                {
                    TradeSink(s.Id, micros, qty);
                }
                else if (!Replaying)
                {
                    Event(MarketInputs.EventTrade, s.Id, micros);
                    Event(MarketInputs.EventVolume, s.Id, shares);
                }
            }
            s.Last = s.MidCents;
            _world.Filled(o, s, qty, cents);
            return true;
        }

        /// <summary>The host: a guest's trade, to be played by every market at the next moment of the grid (bounded as a trade can be).</summary>
        public bool GuestTrade(int stockId, int micros, long shares)
        {
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || Replaying || shares <= 0)
            {
                return false;
            }
            Event(MarketInputs.EventTrade, stockId, Math.Max(-200000, Math.Min(200000, micros)));
            Event(MarketInputs.EventVolume, stockId, (int)Math.Min(shares, int.MaxValue));
            return true;
        }

        // At the open: orders waiting for it (at the market, placed while shut), and resting ones against the open's prices.
        private void OpenOrders(int day)
        {
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                Stock s = o.IsOpen ? State.Stock(o.StockId) : null;
                if (s == null || !s.Listed)
                {
                    continue;
                }
                Quote q = MarketTrading.QuoteOf(State, s, MarketCatalogue.PhaseOpen);
                bool buying = o.Side == OrderSide.Buy;
                long room = Math.Max(1L, (long)(s.Adv * _weight[1] * 3.0 * MarketCatalogue.ParticipationPct / 100.0));
                switch (o.Type)
                {
                    case OrderType.Market:
                        FillNow(s, o, o.Left, MarketTrading.MarketPrice(State, s, buying, o.Left, MarketCatalogue.PhaseOpen), MarketCatalogue.PhaseOpen);
                        break;
                    case OrderType.Limit:
                        if (buying ? q.Ask <= o.LimitCents : q.Bid >= o.LimitCents)
                        {
                            FillNow(s, o, Math.Min(o.Left, room), buying ? q.Ask : q.Bid, MarketCatalogue.PhaseOpen);
                        }
                        break;
                    case OrderType.Stop:
                        if (buying ? q.Ask >= o.StopCents : q.Bid <= o.StopCents)
                        {
                            o.Triggered = true;
                            FillNow(s, o, o.Left, MarketTrading.MarketPrice(State, s, buying, o.Left, MarketCatalogue.PhaseOpen), MarketCatalogue.PhaseOpen);
                        }
                        break;
                }
            }
        }

        // After a step: resting orders against the prices the step really reached.
        private void RestingOrders(int day, int k)
        {
            int phase = MarketCatalogue.PhaseOpen + k;
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                Stock s = o.IsOpen && o.Type != OrderType.Market ? State.Stock(o.StockId) : null;
                if (s == null || !s.Listed || s.BarCount <= 0)
                {
                    continue;
                }
                Candle bar = s.Bars[s.BarCount - 1];
                double half = MarketTrading.HalfSpread(State, s, phase);
                bool buying = o.Side == OrderSide.Buy;
                long room = Math.Max(1L, (long)(bar.V * MarketCatalogue.ParticipationPct / 100.0));
                int askOpen = (int)Math.Ceiling(bar.O * (1.0 + half)), askLow = (int)Math.Ceiling(bar.L * (1.0 + half)), askHigh = (int)Math.Ceiling(bar.H * (1.0 + half));
                int bidOpen = (int)Math.Floor(bar.O * (1.0 - half)), bidLow = (int)Math.Floor(bar.L * (1.0 - half)), bidHigh = (int)Math.Floor(bar.H * (1.0 - half));
                if (o.Type == OrderType.Limit)
                {
                    if (buying && askLow <= o.LimitCents)
                    {
                        FillNow(s, o, Math.Min(o.Left, room), Math.Min(o.LimitCents, askOpen), phase);
                    }
                    else if (!buying && bidHigh >= o.LimitCents)
                    {
                        FillNow(s, o, Math.Min(o.Left, room), Math.Max(o.LimitCents, Math.Max(1, bidOpen)), phase);
                    }
                }
                else if (o.Type == OrderType.Stop)
                {
                    if (buying && askHigh >= o.StopCents)
                    {
                        o.Triggered = true;
                        double impact = MarketTrading.Impact(State, s, o.Left);
                        int at = (int)Math.Ceiling(Math.Max(o.StopCents, askOpen) * (1.0 + impact));
                        FillNow(s, o, o.Left, at, phase);
                    }
                    else if (!buying && bidLow <= o.StopCents)
                    {
                        o.Triggered = true;
                        double impact = MarketTrading.Impact(State, s, o.Left);
                        int at = Math.Max(1, (int)Math.Floor(Math.Min(o.StopCents, Math.Max(1, bidOpen)) * (1.0 - impact)));
                        FillNow(s, o, o.Left, at, phase);
                    }
                }
            }
        }

        // At the close: orders for the day, and orders until cancelled that have stood their time, expire.
        private void Expire(int day)
        {
            for (int i = State.Orders.Count - 1; i >= 0; i--)
            {
                Order o = State.Orders[i];
                if (o.IsOpen && o.ExpireDay <= day)
                {
                    o.State = OrderState.Expired;
                    o.Why = OrderWhy.Expired;
                }
                // Finished orders are kept a while for the app, then go.
                if (!o.IsOpen && day - o.PlacedDay > 10)
                {
                    State.Orders.RemoveAt(i);
                }
            }
        }

        // ---------------------------------------------------------------- the margin account

        private void Interest(int day)
        {
            Account a = State.Account;
            if (!a.Margin)
            {
                return;
            }
            double year = Year;
            if (a.Cash < 0)
            {
                double rate = MarketValuation.BankRate(State.Env) + MarketCatalogue.MarginRateOverBp / 10000.0;
                long interest = (long)Math.Ceiling(-a.Cash * Math.Max(0.0, rate) / year * DaysSincePrevious(day));
                if (interest > 0)
                {
                    a.Cash -= interest;
                    a.Interest += interest;
                    MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, Kind = TradeKind.Interest, Value = -interest });
                    _world.Paid(TradeKind.Interest, null, interest);
                }
            }
            for (int i = 0; i < State.Positions.Count; i++)
            {
                Position p = State.Positions[i];
                Stock s = State.Stock(p.StockId);
                if (s == null || p.Shares >= 0)
                {
                    continue;
                }
                long value = MarketTrading.Value(-p.Shares, s.Last, true);
                long fee = (long)Math.Ceiling(value * MarketTrading.BorrowFeeBp(State, s) / 10000.0 / year * DaysSincePrevious(day));
                if (fee > 0)
                {
                    a.Cash -= fee;
                    a.BorrowFees += fee;
                    MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = s.Id, Kind = TradeKind.BorrowFee, Value = -fee, Test = p.Test });
                    _world.Paid(TradeKind.BorrowFee, s, fee);
                }
            }
        }

        // Calendar days a day's interest is for: since the session before (a Monday's counts the weekend).
        private static int DaysSincePrevious(int day)
        {
            int days = 1;
            for (int d = day - 1; d >= 1 && !MarketCatalogue.TradingDay(d); d--)
            {
                days++;
            }
            return days;
        }

        /// <summary>
        /// The margin account's health: under its maintenance at the close a call is made, to be met by the next close;
        /// unmet then, or far under it at any moment of the session, the broker sells (and buys back shares sold short) at
        /// the market, largest positions first, until the account is healthy again. What is still owed after everything
        /// is sold is taken from the player's cash.
        /// </summary>
        private void Margin(int day, int phase)
        {
            Account a = State.Account;
            if (!a.Margin || State.Positions.Count == 0 && a.Cash >= 0)
            {
                return;
            }
            long equity = MarketTrading.Equity(State, phase);
            long need = MarketTrading.Maintenance(State, phase);
            if (phase == MarketCatalogue.PhaseClose)
            {
                if (equity < need)
                {
                    if (a.CallDay <= 0)
                    {
                        a.CallDay = MarketTrading.MarketDays(day, MarketCatalogue.CallDays);
                        a.CallAmount = need - equity;
                        _world.Called(a.CallAmount, a.CallDay);
                    }
                    else
                    {
                        a.CallAmount = need - equity;
                    }
                }
                else
                {
                    a.CallDay = 0;
                    a.CallAmount = 0;
                }
                return;
            }
            bool forced = (a.CallDay > 0 && day > a.CallDay) || equity * 100 < need * MarketCatalogue.ForcedBelowPct;
            if (!forced && !(State.Positions.Count == 0 && a.Cash < 0))
            {
                return;
            }
            // Sell down, largest first, until the equity is back over a tenth more than what must be kept.
            int guard = 0;
            while (State.Positions.Count > 0 && guard++ < 50)
            {
                equity = MarketTrading.Equity(State, phase);
                need = MarketTrading.Maintenance(State, phase);
                if (equity * 100 >= need * 110 && equity > 0)
                {
                    break;
                }
                Position biggest = null;
                long most = -1;
                for (int i = 0; i < State.Positions.Count; i++)
                {
                    Position p = State.Positions[i];
                    Stock s = State.Stock(p.StockId);
                    long value = s != null ? MarketTrading.Value(Math.Abs(p.Shares), s.Last, false) : 0;
                    if (s != null && s.Listed && value > most)
                    {
                        most = value;
                        biggest = p;
                    }
                }
                if (biggest == null)
                {
                    break;
                }
                Stock target = State.Stock(biggest.StockId);
                var order = new Order
                {
                    Id = State.NextOrderId++, StockId = target.Id, Side = biggest.Shares > 0 ? OrderSide.Sell : OrderSide.Buy, Type = OrderType.Market, Life = OrderLife.Day,
                    Qty = Math.Abs(biggest.Shares), PlacedDay = day, PlacedPhase = phase, ExpireDay = day, State = OrderState.Open, Forced = true, Test = biggest.Test
                };
                State.Orders.Add(order);
                int cents = MarketTrading.MarketPrice(State, target, order.Side == OrderSide.Buy, order.Qty, phase);
                if (!FillNow(target, order, order.Qty, cents, phase))
                {
                    break;
                }
            }
            if (State.Positions.Count == 0 && a.Cash < 0)
            {
                long owed = -a.Cash;
                _world.Wallet.Force(owed, MarketMoney.Shortfall, string.Empty);
                a.Cash = 0;
                MarketTrading.AddTrade(State, new Trade { Day = day, Phase = phase, Kind = TradeKind.Shortfall, Value = -owed });
                _world.Paid(TradeKind.Shortfall, null, owed);
            }
            a.CallDay = 0;
            a.CallAmount = 0;
        }

        // ---------------------------------------------------------------- news

        private void Say(in MarketNews news)
        {
            State.News.Add(news);
            if (State.News.Count > MarketCatalogue.NewsKept)
            {
                State.News.RemoveAt(0);
            }
            _world.Said(news);
        }

        // The day's close: the index, the biggest movers either way and the sector that moved most.
        private void CloseNews(int day)
        {
            int prev = State.IndexPrevClose > 0 ? State.IndexPrevClose : State.IndexOpen;
            int change = prev > 0 ? (int)((long)(State.IndexLast - prev) * 10000L / prev) : 0;
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.Close, A = State.IndexLast, B = change });
            Stock up = null, down = null;
            int upBp = 0, downBp = 0;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed || s.PrevClose <= 0 || s.ListedDay == day)
                {
                    continue;
                }
                int bp = (int)((long)(s.Last - s.PrevClose) * 10000L / s.PrevClose);
                if (bp > upBp)
                {
                    upBp = bp;
                    up = s;
                }
                if (bp < downBp)
                {
                    downBp = bp;
                    down = s;
                }
            }
            if (up != null)
            {
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Mover, Stock = up.Id, A = upBp });
            }
            if (down != null)
            {
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Mover, Stock = down.Id, A = downBp });
            }
            int bestSector = -1, bestBp = 0;
            for (int sec = 0; sec < Sectors; sec++)
            {
                int before = State.SectorPrevClose[sec] > 0 ? State.SectorPrevClose[sec] : _sectorOpen[sec];
                if (before <= 0)
                {
                    continue;
                }
                int bp = (int)((long)(State.SectorLast[sec] - before) * 10000L / before);
                if (Math.Abs(bp) > Math.Abs(bestBp))
                {
                    bestBp = bp;
                    bestSector = sec;
                }
            }
            if (bestSector >= 0)
            {
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Sector, A = bestSector, B = bestBp });
            }
        }
    }
}
