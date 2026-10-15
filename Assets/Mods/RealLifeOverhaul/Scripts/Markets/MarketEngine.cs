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
    /// <see cref="RunFor"/> works through every moment of the grid up to a time, for so long a frame, so time that
    /// passes in a sleep, a skip or a prison stay is played out step by step as if the player had watched it. The open
    /// and the close, the heavy moments (a company's business since the last open, its settling at the close: about 1 to
    /// 3 ms for 140 companies), are worked through a company at a time over frames (<see cref="Midway"/>), with the
    /// same result to the last bit wherever a frame ends.
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
    public sealed partial class MarketEngine
    {
        private const int Sectors = (int)MarketSector.Count;
        private const int Steps = MarketCatalogue.Steps;

        public readonly MarketState State;
        public readonly MarketTape Tape;
        private readonly IMarketWorld _world;

        /// <summary>
        /// The player's cash as the market moves and counts it: the game's (the world's), with the money worked orders have
        /// with the broker still the player's for every other purchase and charge (MarketEngine.Invest.cs, BrokerWallet).
        /// Every take, force and look at what can be afforded in the market goes through it; a worked order's own day of
        /// money parks from the cash in hand alone.
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
        /// True while orders wait after a load: while the moments being played are ones played before it, up to the furthest
        /// moment ever played. At that moment itself orders are taken: nothing past it has been seen. (A load at the moment
        /// of the save, the save being the furthest moment, takes orders at once: the game's clock stands still while the
        /// phone is open, so a hold to the next step would keep the ticket shut for as long as the app is.)
        /// </summary>
        public bool Held => State.HeldDay > 0 && State.Now < MarketState.Time(State.HeldDay, State.HeldPhase);

        /// <summary>True while the market trades: a market day, from the open to the close.</summary>
        public bool Trading => State.InSession;

        /// <summary>
        /// A save has just been loaded into this engine: the tape is followed up to its frontier, and no order is taken
        /// until the market has played up to it again (no trade at a price already seen moving on).
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
                if (_part != Part.None && !Replaying && until > State.Now)
                {
                    Finish();   // (a moment half played that was begun as a new one is played out as one)
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
            if (Played < _replayUntil)
            {
                return;
            }
            _live.Add(new MarketEvent { Day = State.Day, Phase = State.Phase, Kind = kind, A = a, B = b });
        }

        // ---------------------------------------------------------------- the clock

        /// <summary>
        /// Plays the market forward to a moment, doing at most about 'work' company-moments of work (a step of the
        /// session is one for every listed company; a piece of the open or the close, one). True when it has got there;
        /// false when it ran out and will go on next time.
        /// </summary>
        public bool RunTo(long target, ref int work)
        {
            return Run(target, ref work, long.MaxValue);
        }

        /// <summary>
        /// Plays the market forward to a moment for about so many ticks of <see cref="System.Diagnostics.Stopwatch"/> at most (a frame's
        /// budget), a piece at a time: a step of the session (0.1 to 0.2 ms for 140 companies), or one company's (or one
        /// bot's) part of the open or the close. True when it has got there; false when the time ran out and it will go on
        /// next time. What it plays does not depend on where the time runs out (<see cref="Midway"/>).
        /// </summary>
        public bool RunFor(long target, long ticks)
        {
            int work = int.MaxValue;
            return Run(target, ref work, System.Diagnostics.Stopwatch.GetTimestamp() + ticks);
        }

        /// <summary>
        /// True while a moment of the grid is half played: the open or the close, worked through a few companies a frame
        /// (each a few microseconds, the close's settling of 140 companies about 1 ms in all). <see cref="MarketState.Now"/>
        /// stays at the moment before it until its last piece, so the market is behind the clock meanwhile and takes no
        /// order. Everything that changes the market from outside finishes it first (<see cref="Finish"/>), so it is played
        /// exactly as in one frame, and the same on every machine of a Going Public session.
        /// </summary>
        public bool Midway => _part != Part.None;

        /// <summary>
        /// The moment half played, played to its end at once: before anything from outside changes the market, or it is
        /// written. (Asked for by something the market told while at its own work, it does nothing: that is inside the
        /// moment, as it always was.)
        /// </summary>
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

        private bool _working;   // the market is at its own work (Run, Finish)

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

        // The moment the market stands at for what comes in from outside: one half played counts as played (as it did when
        // a moment was always played in one go).
        private long Played => _part == Part.None ? State.Now
            : MarketState.Time(_partDay, _part >= Part.CloseCandles ? MarketCatalogue.PhaseClose : MarketCatalogue.PhaseOpen);

        // The pieces of a moment worked through over frames: the open's (each company's business since the last open, the
        // night's draws, each company's open, then the orders and bots) and the close's (each company's day candle, each
        // company settled, the day's books, each bot, the news).
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

        private Part _part;          // the piece of the moment half played next to work, or None
        private int _at;             // the next company (or bot) of that piece
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
                    OpenBegin(day);   // (the rest in pieces: Continue)
                    return 1;
                default:
                    if (phase < MarketCatalogue.PhaseClose - 1)
                    {
                        Step(day, phase - MarketCatalogue.PhaseOpen + 1);
                        State.Phase = phase + 1;
                    }
                    else
                    {
                        Begin(Part.CloseCandles, day);   // (in pieces: Continue)
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

        // The next piece of the moment half played; what it cost (1). Its last piece moves the clock on.
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
            if (e.Kind == MarketInputs.EventCompany)
            {
                // News of one company (the tech sector): its mood moves, as a news item moves it, and fades as moods do.
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
                OwnEvent(e);   // what the founder of a player's company asked for (MarketEngine.Own.cs)
                return;
            }
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
                    Say(s.Own != null ? ListingRules.Results(s, day, Year, State.Env) : MarketLife.Results(s, day, Year, _secret, State.Env));
                    if (s.Own != null)
                    {
                        OwnResults(s, day);
                    }
                    if (s.DeclaredCents > 0 && !TrustRules.Is(s))   // (a property trust's distributions come every few days: TrustRules)
                    {
                        Say(new MarketNews { Day = day, Kind = MarketNewsKind.Dividend, Stock = s.Id, A = s.DeclaredCents, B = s.RecordDay, C = s.PayDay });
                    }
                }
            }
        }

        // ---------------------------------------------------------------- 09:30: the open

        // What the open's first pieces leave for its companies and its end (OpenBegin, OpenNight).
        private MarketEnv _openEnv;
        private int _openFrom, _openShut, _openRate, _openIndex;
        private double _openNight, _openVariance, _openVs, _openMarketNews, _openMarketMood, _openMarketNewsVar;

        // The open's first piece: the city of the day; then every company's business since the last open (Business).
        private void OpenBegin(int day)
        {
            MarketEnv env;
            if (Tape == null || !Tape.Env(day, out env))
            {
                env = _world.Env(day);
                Tape?.AddEnv(day, env);
            }
            _openRate = State.RateSeenBp;
            _openIndex = State.IndexLast;
            State.Env = env;
            _openEnv = env;
            int from = State.BusinessDay > 0 ? State.BusinessDay + 1 : day;
            _openFrom = Math.Max(from, day - 30);   // (a very long gap: the last month of it is enough for the business)
            Begin(Part.OpenBusiness, day);
        }

        // The night: how long since the last session, and the crash and crazes that may begin with the morning.
        private void OpenNight(int day)
        {
            MarketEnv env = _openEnv;
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
            _openShut = shut;
            _openNight = night;
            _openVariance = variance;
            _openVs = vs;
            _openMarketNews = marketNews;
            _openMarketMood = marketMood;
            _openMarketNewsVar = marketNewsVar;

            // A new session: every day's move is measured from the last close from now on (until the next open).
            MarketLife.DayBases(State, true);
            // A listing announced for today comes to the market, and the player's own company priced or listed (MarketEngine.Own.cs).
            if (State.ListingId > 0 && State.ListingDay <= day)
            {
                List(day);
            }
            OwnMorning(day, env);
        }

        // A company's open: its dividend out of the price, a split, the night's move.
        private void OpenCompany(Stock s, int day)
        {
            if (!s.Listed)
            {
                return;
            }
            MarketEnv env = _openEnv;
            int shut = _openShut;
            double night = _openNight, vs = _openVs, marketNews = _openMarketNews, marketMood = _openMarketMood, marketNewsVar = _openMarketNewsVar;
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
            if (TrustRules.Is(s))
            {
                TrustRules.Declare(s, day, Year);   // a property trust's next distribution, as its last leaves the price (Markets/TrustRules)
            }
            double beta = s.Def.Beta / 100.0;
            int sector = (int)s.Sector;
            double own = s.Def.IdioBp / 10000.0 * vs * Math.Sqrt(OwnVariance(_openVariance) * night) * (s.ResultsBoostDay == day ? Math.Sqrt(MarketCatalogue.ResultsDayVolScale) : 1.0);
            double z = MarketRandom.Fat(_secret, MarketRandom.OpenShock, MarketRandom.OpenTail, s.Id, 1);
            double zo = MarketRandom.Normal(_secret, MarketRandom.OpenMood, s.Id, 1);
            double founder = s.Own != null ? ListingRules.OpenNews(s, env) : 0.0;   // a player's company: what the city made of its founder yesterday
            s.LogFair = MarketLife.LogFairOf(s, env, day, Year);
            if (s.BidCents > 0)
            {
                double held = BidMarketShare * (beta * (marketNews + marketMood) + _sectorNews[sector] + _sectorMood[sector]) + BidOwnShare * own * z;
                s.Sent = s.Sent * Math.Max(0.0, 1.0 - BidKappa * night) + held + founder * BidOwnShare;
                s.Mis = s.Sent;
            }
            else
            {
                double news = beta * marketNews + _sectorNews[sector] + own * OwnNews * z
                              - 0.5 * (beta * beta * marketNewsVar + _sectorSd[sector] * _sectorSd[sector] * (1.0 - MarketCatalogue.SectorMoodShare) + own * own * (1.0 - MarketCatalogue.OwnMoodShare));
                double mood = beta * marketMood + _sectorMood[sector] + own * OwnMood * zo + founder;
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

        // The open's last piece: the index's open, the rate's news, the orders waiting for the open, the bots.
        private void OpenEnd(int day)
        {
            MarketEnv env = _openEnv;
            if (State.IndexBarCount > 0)
            {
                Array.Copy(State.IndexBars, State.IndexLastBars, State.IndexBarCount);
                State.IndexLastBarCount = State.IndexBarCount;
                State.IndexLastBarsDay = day - _openShut;
            }
            State.IndexBarCount = 0;
            Index(out int indexNow);
            State.IndexOpen = State.IndexHigh = State.IndexLow = State.IndexLast = indexNow;
            for (int sec = 0; sec < Sectors; sec++)
            {
                _sectorOpen[sec] = State.SectorLast[sec];
            }
            if (_openRate >= 0 && env.RateBp != _openRate && _openIndex > 0)
            {
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Rate, A = env.RateBp - _openRate, B = (int)((long)(indexNow - _openIndex) * 10000L / _openIndex) });
            }
            State.RateSeenBp = env.RateBp;
            OpenOrders(day);
            BotsOpen();   // the player's bots: stops the night went through, and their floors (MarketEngine.Bots.cs)
            Margin(day, MarketCatalogue.PhaseOpen);
        }

        // A company's business on every day of the calendar since the last open, its quarters.
        private void Business(Stock s, int day)
        {
            if (!s.Listed)
            {
                return;
            }
            int from = _openFrom;
            MarketEnv env = _openEnv;
            if (s.Own != null)
            {
                OwnBusiness(s, env, from, day);   // (its business is its founder's books: MarketEngine.Own.cs)
                return;
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
            WorkedOrders(day, k);   // each worked order's slice of the step's trading (MarketEngine.Invest.cs)
            BotsStep(day, k);
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

        private readonly MarketValuation.Terms _terms = new MarketValuation.Terms();   // a company's worth taken apart for its settling (Settle)

        // A company's day as a candle of its history.
        private void Candles(Stock s, int day)
        {
            if (!s.Listed)
            {
                return;
            }
            var candle = new Candle(day, s.Open, s.High, s.Low, s.Last, s.DayVolume);
            MarketHistory.AddDay(s.Daily, s.Monthly, candle, Year);
            s.Adv = s.Adv * 0.95 + Math.Max(s.Adv * 0.3, s.DayVolume) * 0.05;
        }

        // The index's day and the sectors' closes, after every company's.
        private void CloseIndices(int day)
        {
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
        }

        // After every company is settled: the money of the day, the companies' lives, tomorrow's variance, the account.
        private void CloseBooks(int day)
        {
            Dividends(day);
            Income(day);   // the player's bonds (MarketEngine.Bonds.cs)
            Corporate(day);
            Variance();
            Interest(day);
            Margin(day, MarketCatalogue.PhaseClose);
            Expire(day);
            WorkedClose(day);   // each worked order's day, told (MarketEngine.Invest.cs)
        }

        // The close's last piece, after the bots: the day's news, and old news goes.
        private void CloseEnd(int day)
        {
            CloseNews(day);
            // (the closes the day's moves are measured from stay as they are until the next open: MarketLife.DayBases)
            for (int i = State.News.Count - 1; i >= 0; i--)
            {
                if (State.News[i].Day < day - 6)
                {
                    State.News.RemoveAt(i);
                }
            }
        }

        // After the close: what the day's news did to a company's price goes into what the market expects of the business
        // (its Outlook), worked out so that the price stays exactly where it closed; what is left in the distance from the
        // business's worth is the mood alone. The company is taken apart once for the search and its new worth (_terms).
        private void Settle(Stock s, int day)
        {
            if (!s.Listed || s.BidCents > 0)
            {
                return;
            }
            MarketEnv env = State.Env;
            double logPrice = s.LogFair + s.Mis;
            double accrued = MarketLife.Accrued(s, day, Year);
            double rest = Math.Exp(logPrice - s.Sent) - accrued;
            _terms.Fill(s, env);
            if (rest > 0.0)
            {
                s.Outlook = MarketValuation.OutlookFor(_terms, rest, s.Outlook);
            }
            s.LogFair = MarketLife.LogFairAt(s, _terms.Worth(s.Outlook), day, Year);
            s.Mis = logPrice - s.LogFair;
            s.Sent = s.Mis;
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
                BotDues(s);
                // The company pays it out of its book (a player's company: out of its reserve).
                s.Equity -= s.Shares * s.DeclaredCents / 100.0;
                if (s.Own != null)
                {
                    ListingRules.PaidOut(s, s.DeclaredCents);
                }
            }
            for (int i = State.Dues.Count - 1; i >= 0; i--)
            {
                DividendDue due = State.Dues[i];
                if (due.PayDay > day)
                {
                    continue;
                }
                State.Dues.RemoveAt(i);
                PayDue(due, day);
            }
        }

        // A dividend owed, paid: to the player (a bot's shares: into the bot's money too), or by them on shares sold short;
        // the share of it the holding puts back is bought at the next open (MarketEngine.Invest.cs).
        private void PayDue(in DividendDue due, int day)
        {
            Stock s = State.Stock(due.StockId);
            if (due.Bot > 0)
            {
                BotDividend(due, s, day);   // (a bot's shares: paid to the player, into the bot's money)
                return;
            }
            Position p = State.Position(due.StockId);
            if (due.Cents > 0)
            {
                long money = due.Cents / 100;   // rounded down
                if (money <= 0)
                {
                    return;
                }
                if (State.Account.Margin)
                {
                    State.Account.Cash += money;
                }
                else
                {
                    Wallet.Give(money, MarketMoney.Dividend, s != null ? s.Name : string.Empty);
                }
                State.Account.Dividends += money;
                if (p != null)
                {
                    p.Dividends += money;
                }
                MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = due.StockId, Kind = TradeKind.Dividend, Value = money, Test = due.Test });
                if (!due.Test)
                {
                    Say(new MarketNews { Day = day, Kind = MarketNewsKind.Paid, Stock = due.StockId, A = (int)Math.Min(int.MaxValue, money) });   // (the player's own news: MarketEngine.Bots.cs)
                }
                _world.Paid(TradeKind.Dividend, s, money);
                PutBack(s, p, money, day, due.Test);
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
                if (s.Own != null)
                {
                    OwnCorporate(s, day);   // (a player's company: its meetings, its investors, a bid for it: MarketEngine.Own.cs)
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
            Finish();   // (a moment half played is played out first: Midway)
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || s.BidCents > 0)
            {
                return false;
            }
            if (s.Own != null)
            {
                OwnBid(s, State.Day);   // (a player's company: its own bid, decided by its founder's holding)
                return true;
            }
            Bid(s, State.Day);
            return true;
        }

        /// <summary>A crisis (or a windfall) at a company now (the test tools), as one would come: its price follows at the next open.</summary>
        public bool NewsFor(int stockId, bool crisis)
        {
            Finish();   // (a moment half played is played out first: Midway)
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || s.BidCents > 0 || s.Own != null)   // (a player's company has no crises drawn for it: its news is its books)
            {
                return false;
            }
            CompanyNews(s, State.Day, crisis, Trading);
            return true;
        }

        /// <summary>A company leaves the market now (the test tools): failed, paying holders 'cents' a share, or taken over at that price.</summary>
        public bool Delist(int stockId, GoneReason reason, int cents)
        {
            Finish();   // (a moment half played is played out first: Midway)
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed)
            {
                return false;
            }
            Leave(s, State.Day, reason, cents);
            return true;
        }

        // A company leaves the market: what is left is paid to its holders (a short is closed at the same price), its orders go.
        // The news is the reason's, or the one given (a player's company taken private).
        private void Leave(Stock s, int day, GoneReason reason, int centsEach, MarketNewsKind kind = MarketNewsKind.Count)
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
                        Wallet.Give(money, MarketMoney.Payout, s.Name);
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
            BotsLeave(s, day);
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                if (o.StockId == s.Id && o.IsOpen)
                {
                    o.State = OrderState.Cancelled;
                    o.Why = OrderWhy.CompanyGone;
                    if (o.Worked)
                    {
                        o.EndDay = day;
                        Settle(o, s.Name);   // (its day's money back, its takings paid: MarketEngine.Invest.cs)
                    }
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
            if (kind == MarketNewsKind.Count)
            {
                kind = reason == GoneReason.TakenOver ? MarketNewsKind.BidDone : reason == GoneReason.Delisted ? MarketNewsKind.Delisted : MarketNewsKind.Failure;
            }
            Say(new MarketNews { Day = day, Kind = kind, Stock = s.Id, A = s.GoneCents });
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
                    bool can = d.Holding ? false : d.Pipeline ? s == null : s != null && !s.Listed && day - s.GoneDay > 2 * Year;   // (a rival's holding comes only with its tycoon: ListHolding)
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
        public Order Place(int stockId, OrderSide side, OrderType type, OrderLife life, long qty, int limitCents, int stopCents, bool open, bool held, bool test, out OrderWhy why,
            bool closing = false)
        {
            Finish();   // (a moment half played is played out first: Midway)
            Stock s = State.Stock(stockId);
            Order order = MarketTrading.Place(State, s, side, type, life, qty, limitCents, stopCents, Wallet.Cash, _world.CommissionLeast, open, held, State.Phase, test, out why, closing);
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

        // ('least' the least commission when it is not the day's: a worked order's slices after its first pay none, MarketEngine.Invest.cs)
        private bool FillNow(Stock s, Order o, long qty, int cents, int phase, int least = -1)
        {
            if (o.Side == OrderSide.Buy && !o.Forced && State.Held(s.Id) + qty > MarketTrading.MostHeld(s))
            {
                // No purchase of the player's own ever takes a holding past the most they may hold: an order's checks keep it
                // under when it is placed, and this holds it against whatever changed since (fewer shares in issue, say).
                o.State = o.Filled > 0 ? OrderState.Cancelled : OrderState.Refused;
                o.Why = OrderWhy.HoldingLimit;
                _world.Filled(o, s, 0, 0);
                return false;
            }
            // A trade's lasting move is the city's in a session, and so it is on a day played again that was played in one
            // (its tape has the move: the fill played again must not add it a second time).
            bool city = TradesAsEvents || TradeSink != null || (Replaying && Tape != null && Tape.Traded(State.Day));
            if (!MarketTrading.Fill(State, s, o, qty, cents, least >= 0 ? least : _world.CommissionLeast, Wallet, phase, !city, out double kept))
            {
                o.State = o.Filled > 0 ? OrderState.Cancelled : OrderState.Refused;
                o.Why = o.Side == OrderSide.Buy ? (State.Account.Margin ? OrderWhy.BelowMargin : OrderWhy.NoMoney) : (State.Account.Margin ? OrderWhy.BelowMargin : OrderWhy.NoShares);
                _world.Filled(o, s, 0, 0);
                return false;
            }
            if (city)
            {
                Shared(s, kept, qty);
            }
            s.Last = s.MidCents;
            if (!o.Forced && State.Held(s.Id) == 0)
            {
                CloseClosing(s.Id, o.Id);   // nothing held now: a stop loss or take profit left on it has nothing to close
                if (o.Closing && o.IsOpen)
                {
                    o.State = OrderState.Filled;   // (it closed what there was: it is done)
                }
            }
            _world.Filled(o, s, qty, cents);
            return true;
        }

        // A trade's lasting move in a Going Public session: to the host (a guest), or on the tape for every market at the next moment (the host).
        private void Shared(Stock s, double kept, long qty)
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

        // Every open stop loss and take profit on a company, but one, cancelled: there is nothing left for them to close.
        private void CloseClosing(int stockId, int but)
        {
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order other = State.Orders[i];
                if (other.IsOpen && other.Closing && other.StockId == stockId && other.Id != but)
                {
                    other.State = OrderState.Cancelled;
                    other.Why = OrderWhy.NothingHeld;
                }
            }
        }

        // What of a stop loss or a take profit can be filled now: what is left of it, never more than is still held. With
        // nothing held it is done: filled as far as it went, else cancelled.
        private long Closable(Order o, long wanted)
        {
            if (!o.Closing)
            {
                return wanted;
            }
            long held = State.Held(o.StockId);
            long holding = o.Side == OrderSide.Sell ? Math.Max(0L, held) : Math.Max(0L, -held);
            long qty = Math.Min(wanted, holding);
            if (qty <= 0)
            {
                o.State = o.Filled > 0 ? OrderState.Filled : OrderState.Cancelled;
                o.Why = o.Filled > 0 ? OrderWhy.None : OrderWhy.NothingHeld;
            }
            return qty;
        }

        /// <summary>The host: a guest's trade, to be played by every market at the next moment of the grid (bounded as a trade can be).</summary>
        public bool GuestTrade(int stockId, int micros, long shares)
        {
            Finish();   // (a moment half played is played out first: Midway)
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
                        if (o.Back)
                        {
                            BackNow(o, s, MarketCatalogue.PhaseOpen);   // a dividend put back: what its money buys, if the option is still on (MarketEngine.Invest.cs)
                        }
                        else if (!o.Worked)   // (a worked order's slices are the steps': WorkedOrders)
                        {
                            FillNow(s, o, o.Left, MarketTrading.MarketPrice(State, s, buying, o.Left, MarketCatalogue.PhaseOpen), MarketCatalogue.PhaseOpen);
                        }
                        break;
                    case OrderType.Limit:
                        if (buying ? q.Ask <= o.LimitCents : q.Bid >= o.LimitCents)
                        {
                            long qty = Closable(o, Math.Min(o.Left, room));
                            if (qty > 0)
                            {
                                FillNow(s, o, qty, buying ? q.Ask : q.Bid, MarketCatalogue.PhaseOpen);
                            }
                        }
                        break;
                    case OrderType.Stop:
                        if (buying ? q.Ask >= o.StopCents : q.Bid <= o.StopCents)
                        {
                            // A gap through the stop: it goes at the open's price, whatever the stop was.
                            o.Triggered = true;
                            long qty = Closable(o, o.Left);
                            if (qty > 0)
                            {
                                FillNow(s, o, qty, MarketTrading.MarketPrice(State, s, buying, qty, MarketCatalogue.PhaseOpen), MarketCatalogue.PhaseOpen);
                            }
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
                    if (buying ? askLow <= o.LimitCents : bidHigh >= o.LimitCents)
                    {
                        long qty = Closable(o, Math.Min(o.Left, room));
                        if (qty > 0)
                        {
                            FillNow(s, o, qty, buying ? Math.Min(o.LimitCents, askOpen) : Math.Max(o.LimitCents, Math.Max(1, bidOpen)), phase);
                        }
                    }
                }
                else if (o.Type == OrderType.Stop)
                {
                    // Reached in the step: from then it is an order at the market, filled at the market's price the step
                    // ends on (the next price), with its move: the stop's own price only if the market is there.
                    if (buying ? askHigh >= o.StopCents : bidLow <= o.StopCents)
                    {
                        o.Triggered = true;
                        long qty = Closable(o, o.Left);
                        if (qty > 0)
                        {
                            FillNow(s, o, qty, MarketTrading.MarketPrice(State, s, buying, qty, phase), phase);
                        }
                    }
                }
            }
        }

        // At the close: orders for the day, and orders until cancelled that have stood their time, expire (a worked order
        // never does: it stands until it is done, stopped or cancelled).
        private void Expire(int day)
        {
            for (int i = State.Orders.Count - 1; i >= 0; i--)
            {
                Order o = State.Orders[i];
                if (o.IsOpen && o.Life != OrderLife.UntilDone && o.ExpireDay <= day)
                {
                    o.State = OrderState.Expired;
                    o.Why = OrderWhy.Expired;
                }
                if (!o.IsOpen && o.Worked && o.EndDay <= 0)
                {
                    o.EndDay = day;   // (one cancelled, or its company gone: kept from the day it ended)
                }
                // Finished orders are kept a while for the app, then go.
                if (!o.IsOpen && day - Math.Max(o.PlacedDay, o.EndDay) > 10)
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
                Wallet.Force(owed, MarketMoney.Shortfall, string.Empty);
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
                int bp = MarketHistory.DayMoveBp(s);   // (the app's own rule: the day's biggest movers are the ones it shows)
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
