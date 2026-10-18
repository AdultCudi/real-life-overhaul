using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What the market needs from outside: fresh secrets, the city, money, and someone to tell. MarketGame is the game's.</summary>
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
        /// <summary>Money came in or went out of the account for a reason: a dividend, a payout, interest, what the broker took.</summary>
        void Paid(TradeKind kind, Stock stock, long amount);
        /// <summary>A margin call: so much more equity is needed by the close of a day.</summary>
        void Called(long amount, int byDay);
    }

    /// <summary>
    /// The market's clock and everything that happens on it. Game-free. The market runs on a fixed grid of game time
    /// (results at 07:30, the open at 09:30, a step every five minutes, the close at 16:00, weekdays) and plays every moment
    /// of it, so a sleep or a skip is played out step by step. The heavy open and close are worked a company at a time
    /// over frames (<see cref="Midway"/>) with the same result to the bit wherever a frame ends. A moment before the tape's
    /// frontier is played with the tape's secret, city and events; one past it with fresh ones, written to the tape. Each
    /// step a price moves by the market's, its sector's and its own fat-tailed move, mostly news that stays (into
    /// Stock.Outlook at the close) and partly mood that fades, so a price is a fair bet at every moment.
    /// </summary>
    public sealed partial class MarketEngine
    {
        private const int Sectors = (int)MarketSector.Count;
        private const int Steps = MarketCatalogue.Steps;

        public readonly MarketState State;
        public readonly MarketTape Tape;
        private readonly IMarketWorld _world;

        /// <summary>
        /// The player's cash as the market counts it: the game's, with worked orders' money with the broker still the player's
        /// for every other purchase (BrokerWallet). A worked order's own day of money parks from the cash in hand alone.
        /// </summary>
        public readonly BrokerWallet Wallet;
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
        /// A Going Public host with guests: every player's trades move the one city's prices, so a trade's lasting move goes on
        /// the tape as an event that every market plays at the next moment of the grid. Off, it is put on the price at once.
        /// </summary>
        public bool TradesAsEvents;

        /// <summary>A guest following the host: a trade's lasting move goes to the host (the company, millionths, the shares) for its tape.</summary>
        public Action<int, int, long> TradeSink;
        private double _kappa;
        private int _kappaYear;

        public MarketEngine(MarketState state, MarketTape tape, IMarketWorld world)
        {
            State = state;
            Tape = tape;
            _world = world;
            Wallet = new BrokerWallet(world.Wallet, state);
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

        /// <summary>
        /// True while orders wait after a load, up to the furthest moment ever played; at that moment orders are taken. A load
        /// at the save's own moment takes orders at once: the clock stands still while the phone is open.
        /// </summary>
        public bool Held => State.HeldDay > 0 && State.Now < MarketState.Time(State.HeldDay, State.HeldPhase);

        /// <summary>True while the market trades: a market day, from the open to the close.</summary>
        public bool Trading => State.InSession;

        /// <summary>A save has just been loaded: the tape is followed to its frontier and no order is taken until the market is there again.</summary>
        public void HoldAfterLoad()
        {
            _replayUntil = Tape != null ? Tape.Frontier : 0;
            long until = Math.Max(_replayUntil, State.Now);
            State.HeldDay = (int)(until / 100);
            State.HeldPhase = (int)(until % 100);
        }

        /// <summary>The time up to which the tape is followed.</summary>
        public long ReplayUntil => _replayUntil;

        /// <summary>A Going Public guest follows the host's market, its tape the host's, up to the furthest moment the host has played.</summary>
        public void Follow(long until)
        {
            if (until > _replayUntil)
            {
                if (_part != Part.None && !Replaying && until > State.Now)
                {
                    Finish();   // a moment begun as a new one is played out as one
                }
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

        // Under a bid the mood halves in a market day and the market's and sector's moves barely reach the price.
        private const double BidKappa = 0.6931471805599453;
        private const double BidMarketShare = 0.2, BidOwnShare = 0.3;

        // The news and mood shares of each kind of move's spread: the roots of their shares of variance.
        private static readonly double MarketNews = Math.Sqrt(1.0 - MarketCatalogue.MarketMoodShare), MarketMood = Math.Sqrt(MarketCatalogue.MarketMoodShare);
        private static readonly double SectorNews = Math.Sqrt(1.0 - MarketCatalogue.SectorMoodShare), SectorMood = Math.Sqrt(MarketCatalogue.SectorMoodShare);
        private static readonly double OwnNews = Math.Sqrt(1.0 - MarketCatalogue.OwnMoodShare), OwnMood = Math.Sqrt(MarketCatalogue.OwnMoodShare);

        // The sector of the first listed company in a trade, or -1.
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
            if (Played < _replayUntil)
            {
                return;
            }
            _live.Add(new MarketEvent { Day = State.Day, Phase = State.Phase, Kind = kind, A = a, B = b });
        }

        /// <summary>
        /// Plays the market forward to a moment, doing at most about <paramref name="work"/> company-moments of work; true
        /// when it got there, false when it will go on next time.
        /// </summary>
        public bool RunTo(long target, ref int work)
        {
            return Run(target, ref work, long.MaxValue);
        }

        /// <summary>
        /// Plays the market forward to a moment for at most so many Stopwatch ticks, a piece at a time (a step, or one company's
        /// or bot's part of the open or close). What it plays does not depend on where the time runs out.
        /// </summary>
        public bool RunFor(long target, long ticks)
        {
            int work = int.MaxValue;
            return Run(target, ref work, System.Diagnostics.Stopwatch.GetTimestamp() + ticks);
        }

        /// <summary>
        /// True while the open or the close is half played. <see cref="MarketState.Now"/> stays at the moment before it until
        /// its last piece, so the market is behind and takes no order; anything changing the market from outside finishes it
        /// first (<see cref="Finish"/>), so it plays exactly as in one frame on every machine.
        /// </summary>
        public bool Midway => _part != Part.None;

        /// <summary>The moment half played, played to its end now; nothing while the market is at its own work.</summary>
        public void Finish()
        {
            if (_working)
            {
                return;
            }
            _working = true;
            try
            {
                while (_part != Part.None)
                {
                    Continue();
                }
            }
            finally
            {
                _working = false;
            }
        }

        private bool _working;   // at its own work: Run, Finish

        private bool Run(long target, ref int work, long until)
        {
            if (_working)
            {
                return false;
            }
            _working = true;
            try
            {
                while (_part != Part.None || State.Now < target)
                {
                    if (work <= 0)
                    {
                        return false;
                    }
                    work -= _part != Part.None ? Continue() : Next();
                    if (until != long.MaxValue && System.Diagnostics.Stopwatch.GetTimestamp() >= until && (_part != Part.None || State.Now < target))
                    {
                        return false;
                    }
                }
                return true;
            }
            finally
            {
                _working = false;
            }
        }

        // The moment that counts for what comes in from outside: one half played counts as played.
        private long Played => _part == Part.None ? State.Now
            : MarketState.Time(_partDay, _part >= Part.CloseCandles ? MarketCatalogue.PhaseClose : MarketCatalogue.PhaseOpen);

        // The pieces of the open and the close worked through over frames.
        private enum Part
        {
            None,
            OpenBusiness,
            OpenNight,
            OpenCompanies,
            OpenEnd,
            CloseCandles,
            CloseSettle,
            CloseBooks,
            CloseBots,
            CloseEnd
        }

        private Part _part;          // the piece half played next to work, or None
        private int _at;             // the next company or bot of that piece
        private int _partDay;        // the moment's day

        // The next moment of the grid, or its first piece (the open and the close); what it cost.
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
                    OpenBegin(day);   // the rest in pieces: Continue
                    return 1;
                default:
                    if (phase < MarketCatalogue.PhaseClose - 1)
                    {
                        Step(day, phase - MarketCatalogue.PhaseOpen + 1);
                        State.Phase = phase + 1;
                    }
                    else
                    {
                        Begin(Part.CloseCandles, day);   // in pieces: Continue
                        return 1;
                    }
                    break;
            }
            Reach();
            return cost;
        }

        private void Begin(Part part, int day)
        {
            _part = part;
            _partDay = day;
            _at = 0;
        }

        // The next piece of the moment half played; its last piece moves the clock on.
        private int Continue()
        {
            int day = _partDay;
            switch (_part)
            {
                case Part.OpenBusiness:
                    if (_at < State.Stocks.Count)
                    {
                        Business(State.Stocks[_at++], day);
                        return 1;
                    }
                    State.BusinessDay = day;
                    _part = Part.OpenNight;
                    return 1;
                case Part.OpenNight:
                    OpenNight(day);
                    Begin(Part.OpenCompanies, day);
                    return 1;
                case Part.OpenCompanies:
                    if (_at < State.Stocks.Count)
                    {
                        OpenCompany(State.Stocks[_at++], day);
                        return 1;
                    }
                    _part = Part.OpenEnd;
                    return 1;
                case Part.OpenEnd:
                    OpenEnd(day);
                    _part = Part.None;
                    State.Phase = MarketCatalogue.PhaseOpen;
                    Reach();
                    return 1;
                case Part.CloseCandles:
                    if (_at < State.Stocks.Count)
                    {
                        Candles(State.Stocks[_at++], day);
                        return 1;
                    }
                    CloseIndices(day);
                    Begin(Part.CloseSettle, day);
                    return 1;
                case Part.CloseSettle:
                    if (_at < State.Stocks.Count)
                    {
                        Settle(State.Stocks[_at++], day);
                        return 1;
                    }
                    _part = Part.CloseBooks;
                    return 1;
                case Part.CloseBooks:
                    CloseBooks(day);
                    Begin(Part.CloseBots, day);
                    return 1;
                case Part.CloseBots:
                    if (_at < State.Bots.Count)
                    {
                        BotClose(State.Bots[_at++], day);
                        return 1;
                    }
                    _part = Part.CloseEnd;
                    return 1;
                case Part.CloseEnd:
                    CloseEnd(day);
                    _part = Part.None;
                    State.Phase = MarketCatalogue.PhaseClose;
                    Reach();
                    return 1;
            }
            _part = Part.None;
            return 1;
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

        // The day's secret: the tape's, else a fresh one written to the tape at once.
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

        // The city's events that came in during the moment just played, or the tape's.
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
            if (e.Kind == MarketInputs.EventCompany)
            {
                // News of one company from the tech sector: its mood moves and fades as moods do.
                Stock news = State.Stock(e.A);
                if (news != null && news.Listed)
                {
                    double move = Math.Max(-MarketInputs.CompanyMicrosMost, Math.Min(MarketInputs.CompanyMicrosMost, e.B)) / 1000000.0;
                    news.Mis += move;
                    news.Sent += move;
                    news.Last = news.MidCents;
                }
                return;
            }
            if (e.Kind >= MarketInputs.EventOwn && e.Kind < MarketInputs.EventCompany)
            {
                OwnEvent(e);   // MarketEngine.Own.cs
                return;
            }
            if (e.Kind == MarketInputs.EventTrade || e.Kind == MarketInputs.EventVolume)
            {
                // A player's trade in a Going Public session: its lasting move of the price, or its shares.
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
                // The policy is part of the business from now on, priced the moment it is heard of.
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
    }
}
