using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The city's stock market in the game: it keeps the market (Markets/MarketEngine, game-free) on the game's clock,
    /// in the save and on its tape, and is what the Stocks app and the paper talk to. See the README ("The stock market").
    ///
    /// A one-second timer on the real clock waits for the save, opens the market (from the save, or a new one with the
    /// old market's shares brought over once) and keeps the save's handler in place. Each frame the market is played up
    /// to the game's own moment, a few hundred company-steps at most a frame: time that passes in a sleep or a skip is
    /// played out step by step within a few frames, and while the market is behind the clock no order is taken
    /// (OrderWhy.Behind), so nobody can trade at a price the market has already moved past.
    ///
    /// Everything that happens (a fill, a dividend, a call, the news) comes back through the world
    /// (<see cref="MarketGameWorld"/>) and is told to the player once the frame's work is done. A fault anywhere
    /// switches the market off for the session with one line, and leaves the game and the save's market as they were.
    /// </summary>
    public sealed partial class MarketDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeStocks";
        private const int WorkPerFrame = 3000;           // company-steps a frame at most (a step of the session is one for every listed company)
        private const float FrontierEvery = 1f;          // real seconds between writing the furthest moment played to the tape
        private const int StartWaitSeconds = 10;         // how long a new market waits for the wiring before it opens with nothing of the old one

        public static IModLogger Logger;
        public static MarketDirector Instance { get; private set; }

        private readonly List<Note> _notes = new List<Note>(8);
        private MarketGameWorld _world;
        private MarketEngine _engine;
        private GameInstance _save;
        private bool _running, _broken;
        private float _frontierAt;
        private int _startTries;
        private long _frontierWritten;
        private Action _onSave;
        private Action<int, int, int> _say;

        /// <summary>Raised after the market has moved on or something the player did changed it (the app shows it again).</summary>
        public event Action Changed;

        public bool Running => _running && !_broken;

        /// <summary>The market as it stands; null while it is not running.</summary>
        public MarketState State => Running ? _engine.State : null;

        public MarketEngine Engine => Running ? _engine : null;

        /// <summary>True while the market is still catching up with the game's clock (after a sleep or a skip): no order is taken.</summary>
        public bool Behind { get; private set; }

        /// <summary>True while orders can be filled now: a market day's session, caught up, not held after a load.</summary>
        public bool Trading => Running && _engine.Trading && !Behind && !_engine.Held;

        public int CommissionLeast => Prices.At(MarketCatalogue.CommissionLeastAtStart);

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private struct Note
        {
            public NotificationType Type;
            public string Key;
            public string[] Pairs;
        }

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _say = (kind, a, b) => _engine?.Event(kind, a, b);
            _world = new MarketGameWorld(this);
            StartCoroutine(Tick());
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                StandDown(false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------------------------------------------------------------- the clock

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (_broken)
                {
                    continue;
                }
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    Fault("the timer", e);
                }
            }
        }

        // Once a second: start when the save is ready, stand down when the option goes off or another save is loaded.
        private void Slow()
        {
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (_running && !ReferenceEquals(Save, _save))
            {
                StandDown(false);   // another save: what this one's market was is in that save already
            }
            if (!Settings.Markets)
            {
                if (_running)
                {
                    StandDown(true);
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            if (!_running)
            {
                return;
            }
            SaveHook.Keep(_onSave);
            MarketBridges.Poll?.Invoke(_say);
            InstallApp();
        }

        private void Update()
        {
            if (!_running || _broken || Save == null || LoadScene.isLoading)
            {
                return;
            }
            try
            {
                long target = Target();
                if (_engine.State.Now < target)
                {
                    int work = WorkPerFrame;
                    bool there = _engine.RunTo(target, ref work);
                    Behind = !there;
                    Moved();
                }
                else if (Behind)
                {
                    Behind = false;
                }
                Flush();
            }
            catch (Exception e)
            {
                Fault("the market's step", e);
            }
        }

        /// <summary>The moment of the market's grid the game's clock has reached.</summary>
        private static long Target()
        {
            GameInstance save = Save;
            int minute = save.Hour * 60 + Mathf.FloorToInt(save.Minute);
            int phase = MarketCatalogue.TradingDay(save.Day) ? MarketCatalogue.PhaseAt(minute) : MarketCatalogue.PhaseClose;
            return MarketState.Time(save.Day, phase);
        }

        // The tape's new lines go to disk at once (a new day's secret the moment it is drawn), the furthest moment played at least once a second.
        private void Flush()
        {
            MarketTape tape = _engine.Tape;
            if (Time.realtimeSinceStartup >= _frontierAt && tape.Frontier > _frontierWritten)
            {
                tape.WriteFrontier();
                _frontierWritten = tape.Frontier;
                _frontierAt = Time.realtimeSinceStartup + FrontierEvery;
            }
            if (tape.HasPending)
            {
                MarketTapeFile.Append(tape.Id, tape.TakePending());
            }
            for (int i = 0; i < _notes.Count && i < 3; i++)
            {
                Note n = _notes[i];
                Tell(n.Type, n.Key, n.Pairs);
            }
            _notes.RemoveRange(0, Math.Min(3, _notes.Count));
        }

        private void Moved()
        {
            Action changed = Changed;
            if (changed != null)
            {
                try
                {
                    changed();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        // ---------------------------------------------------------------- starting and stopping

        private void StartUp()
        {
            GameInstance save = Save;
            int version;
            MarketState state = MarketStore.Load(save.modData, out version);
            bool fresh = state == null;
            MarketCarryOver.Result moved = default(MarketCarryOver.Result);
            if (fresh)
            {
                // The old market's file, from the wiring: while it is not connected yet, or Ventures is still opening its
                // file, the market waits (a few seconds at most for the wiring, after which it opens with nothing brought).
                Func<OldMarket> old = MarketBridges.Old;
                if (old == null && _startTries++ < StartWaitSeconds)
                {
                    return;
                }
                OldMarket hand = old != null ? old() : new OldMarket();
                if (hand == null)
                {
                    return;   // Ventures is still opening its file: next second
                }
                state = Open(save, hand.File);
                moved = MarketCarryOver.Move(hand.File, state, _world.Wallet);
                if (hand.File != null)
                {
                    hand.Done?.Invoke();
                }
            }
            if (state.YearDays != GameCalendar.DaysPerYear && fresh == false)
            {
                Logger?.Warn("Stock market: the save's year is " + GameCalendar.DaysPerYear + " days and its market's " + state.YearDays + "; the market keeps its own.");
            }
            MarketTape tape = MarketTapeFile.Read(state.Id);
            _engine = new MarketEngine(state, tape, _world);
            _engine.HoldAfterLoad();
            _frontierWritten = tape.Frontier;
            _save = save;
            _running = true;
            Behind = _engine.State.Now < Target();
            if (fresh)
            {
                Logger?.Info("Stock market: opened for this save on day " + save.Day + " with " + state.Stocks.Count + " companies"
                             + (moved.Moved > 0 ? "; " + moved.Moved + " holding(s) of " + moved.Shares + " shares brought over from the old market" : string.Empty)
                             + (moved.PaidOut > 0 ? "; " + moved.PaidOut + " paid out at their old price (" + moved.PaidMoney + ")" : string.Empty) + ".");
                if (moved.Moved > 0)
                {
                    Queue(NotificationType.Info, "reallife_mkt_note_moved", "count", moved.Moved.ToString());
                }
            }
            if (Verbose)
            {
                Logger?.Info("Stock market check (start): store version " + (fresh ? "new" : version.ToString()) + ", day " + state.Day + " phase " + state.Phase + ", tape " + tape.Days
                             + " day(s) to " + tape.FrontierDay + "/" + tape.FrontierPhase + ", held to " + state.HeldDay + "/" + state.HeldPhase + ", " + state.Positions.Count + " holding(s), "
                             + state.Orders.Count + " order(s): PASS");
            }
        }

        // A new market for this save. Its id (the tape's name) and its seed come from the playthrough and the day it is
        // opened, so the same save opened again opens the same market, and its tape replays what was seen.
        private MarketState Open(GameInstance save, Economy.Ventures.VenturesFile old)
        {
            string playthrough = !string.IsNullOrEmpty(save.seed) ? save.seed : save.characterId ?? "city";
            ulong id = MarketRandom.Mix64(MarketRandom.Of(playthrough) ^ ((ulong)(uint)save.Day * 0x9E3779B97F4A7C15UL));
            var names = new List<string>(4);
            var ids = new List<string>(4);
            MarketBridges.Rivals?.Invoke(names, ids);
            List<CarriedPrice> carried = old != null && old.Holdings.Count > 0 && !old.MarketMoved ? MarketCarryOver.Prices(old) : null;
            int minute = save.Hour * 60 + Mathf.FloorToInt(save.Minute);
            return MarketSetup.Open(id, save.Day, minute, GameCalendar.DaysPerYear, _world.Env(save.Day), id, names, ids, carried);
        }

        // The market stops: with 'keep' it goes into the save's data first (a stop in the middle of play, when the game's
        // current save is certainly the one it belongs to); the app leaves the phone.
        private void StandDown(bool keep)
        {
            if (keep && _running && Save != null && ReferenceEquals(Save, _save))
            {
                MarketStore.Save(Save.modData, _engine.State);
                MarketTapeFile.Rewrite(_engine.Tape.Id, _engine.Tape.Whole());
            }
            else if (_running && _engine.Tape.HasPending)
            {
                MarketTapeFile.Append(_engine.Tape.Id, _engine.Tape.TakePending());
            }
            _running = false;
            _engine = null;
            _save = null;
            _notes.Clear();
            Behind = false;
            RemoveApp();
        }

        /// <summary>A fault: one line, and nothing more of the market this session. What the save holds of it is left as it is.</summary>
        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                if (_running && _engine.Tape.HasPending)
                {
                    MarketTapeFile.Append(_engine.Tape.Id, _engine.Tape.TakePending());
                }
                _running = false;
                _engine = null;
                RemoveApp();
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            Logger?.Warn("Stock market: switched off for this session after an error in " + where + ". Prices stand still and nothing is traded; what the save holds of the market is untouched.");
        }

        private void OnSave()
        {
            try
            {
                if (_running && Save != null && ReferenceEquals(Save, _save))
                {
                    MarketStore.Save(Save.modData, _engine.State);
                    MarketTapeFile.Rewrite(_engine.Tape.Id, _engine.Tape.Whole());
                    _frontierWritten = _engine.Tape.Frontier;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- what the player does

        /// <summary>What an order would come to now, and whether it can be placed (nothing is changed).</summary>
        public Estimate Estimate(int stockId, OrderSide side, OrderType type, long qty, int limitCents, int stopCents)
        {
            if (!Running)
            {
                return new Estimate { Why = OrderWhy.Closed };
            }
            if (Behind)
            {
                return new Estimate { Why = OrderWhy.Behind };
            }
            MarketState s = _engine.State;
            return MarketTrading.Check(s, s.Stock(stockId), side, type, qty, limitCents, stopCents, _world.Wallet.Cash, CommissionLeast, Trading, _engine.Held, s.Phase);
        }

        /// <summary>An order placed: filled at once if it can be, else it waits. Null with the reason if it cannot be placed.</summary>
        public Order Place(int stockId, OrderSide side, OrderType type, OrderLife life, long qty, int limitCents, int stopCents, bool test, out OrderWhy why)
        {
            why = OrderWhy.Closed;
            if (!Running)
            {
                return null;
            }
            if (Behind)
            {
                why = OrderWhy.Behind;
                return null;
            }
            if (!Settings.MarketsMargin && _engine.State.Account.Margin && !Closes(stockId, side, qty))
            {
                why = OrderWhy.NoMargin;   // (with margin trading off an account that is open is only wound down)
                return null;
            }
            try
            {
                Order order = _engine.Place(stockId, side, type, life, qty, limitCents, stopCents, Trading, _engine.Held, test, out why);
                if (Verbose && order != null)
                {
                    Logger?.Info("Stock market check (order " + order.Id + "): " + side + " " + qty + " of " + stockId + " (" + type + ", " + life + ") is " + order.State + ": PASS");
                }
                Moved();
                return order;
            }
            catch (Exception e)
            {
                Fault("placing an order", e);
                return null;
            }
        }

        // True for an order that only closes (part of) what is held or owed.
        private bool Closes(int stockId, OrderSide side, long qty)
        {
            long held = _engine.State.Held(stockId);
            return side == OrderSide.Buy ? held < 0 && qty <= -held : held > 0 && qty <= held;
        }

        /// <summary>An open order cancelled; false if it is not open (or orders are held after a load).</summary>
        public bool Cancel(int orderId)
        {
            if (!Running || Behind || _engine.Held)
            {
                return false;
            }
            bool done = MarketTrading.Cancel(_engine.State, orderId);
            Moved();
            return done;
        }

        /// <summary>A company on or off the watch list; false if the list is full.</summary>
        public bool Watch(int stockId, bool on)
        {
            MarketState s = State;
            if (s == null || s.Stock(stockId) == null)
            {
                return false;
            }
            if (!on)
            {
                s.Watch.Remove(stockId);
            }
            else if (!s.Watch.Contains(stockId))
            {
                if (s.Watch.Count >= MarketCatalogue.WatchMost)
                {
                    return false;
                }
                s.Watch.Add(stockId);
            }
            Moved();
            return true;
        }

        /// <summary>Money paid from the player's cash into the margin account; the first payment opens it (at least the opening sum).</summary>
        public bool Deposit(long amount)
        {
            MarketState s = State;
            if (s == null || amount <= 0 || !Settings.MarketsMargin || Behind || _engine.Held)
            {
                return false;
            }
            Account a = s.Account;
            if (!a.Margin && amount < Prices.At(MarketCatalogue.MarginOpenLeastAtStart))
            {
                return false;
            }
            if (!_world.Wallet.Take(amount, MarketMoney.Deposit, string.Empty))
            {
                return false;
            }
            if (!a.Margin)
            {
                a.Margin = true;
                a.Cash = 0;
                a.OpenedDay = s.Day;
            }
            a.Cash += amount;
            MarketTrading.AddTrade(s, new Trade { Day = s.Day, Phase = s.Phase, Kind = TradeKind.Deposit, Value = amount });
            Moved();
            return true;
        }

        /// <summary>The most that can be taken out of the margin account now: its own money, as long as what is held keeps its initial margin.</summary>
        public long MostWithdrawable()
        {
            MarketState s = State;
            if (s == null || !s.Account.Margin || s.Account.Cash <= 0)
            {
                return 0;
            }
            long gross = MarketTrading.GrossExposure(s, s.Phase);
            long equity = MarketTrading.Equity(s, s.Phase);
            long free = equity - (gross * MarketCatalogue.InitialMarginPct + 99) / 100;
            return Math.Max(0, Math.Min(s.Account.Cash, free));
        }

        /// <summary>Money taken out of the margin account into the player's cash; the account closes when it has nothing left and nothing is borrowed.</summary>
        public bool Withdraw(long amount)
        {
            MarketState s = State;
            if (s == null || amount <= 0 || Behind || _engine.Held || amount > MostWithdrawable())
            {
                return false;
            }
            Account a = s.Account;
            a.Cash -= amount;
            _world.Wallet.Give(amount, MarketMoney.Withdrawal, string.Empty);
            MarketTrading.AddTrade(s, new Trade { Day = s.Day, Phase = s.Phase, Kind = TradeKind.Withdrawal, Value = -amount });
            if (a.Cash == 0 && s.Positions.Count == 0)
            {
                a.Margin = false;
                a.CallDay = 0;
                a.CallAmount = 0;
            }
            Moved();
            return true;
        }

        // ---------------------------------------------------------------- what the market tells

        /// <summary>A piece of market news (MarketGameWorld): the player hears of what touches what they hold.</summary>
        internal void Heard(in MarketNews news)
        {
            MarketState s = _engine != null ? _engine.State : null;
            if (s == null || news.Stock <= 0 || (s.Held(news.Stock) == 0 && !s.Watching(news.Stock)))
            {
                return;
            }
            Stock k = s.Stock(news.Stock);
            string name = k != null ? k.Name : string.Empty;
            switch (news.Kind)
            {
                case MarketNewsKind.Results:
                    Queue(NotificationType.Info, news.A >= news.B ? "reallife_mkt_note_results_beat" : "reallife_mkt_note_results_miss", "name", name, "eps", MarketText.PerShare(news.A),
                        "consensus", MarketText.PerShare(news.B));
                    break;
                case MarketNewsKind.Bid:
                    Queue(NotificationType.Info, "reallife_mkt_note_bid", "name", name, "price", MarketText.Price(news.A), "day", news.B.ToString());
                    break;
                case MarketNewsKind.BidFailed:
                    Queue(NotificationType.Warning, "reallife_mkt_note_bid_failed", "name", name);
                    break;
                case MarketNewsKind.Crisis:
                    Queue(NotificationType.Warning, "reallife_mkt_note_crisis", "name", name);
                    break;
                case MarketNewsKind.Windfall:
                    Queue(NotificationType.Success, "reallife_mkt_note_windfall", "name", name);
                    break;
                case MarketNewsKind.Split:
                    Queue(NotificationType.Info, "reallife_mkt_note_split", "name", name, "ratio", news.A.ToString(), "day", news.B.ToString());
                    break;
            }
        }

        /// <summary>An order filled (or refused at the fill): the player is told once the frame's work is done.</summary>
        internal void OrderFilled(Order order, Stock stock, long qty, int cents)
        {
            if (order == null || stock == null)
            {
                return;
            }
            if (qty <= 0)
            {
                Queue(NotificationType.Error, "reallife_mkt_note_refused", "name", stock.Name, "why", MarketText.Why(order.Why));
                return;
            }
            if (order.Forced)
            {
                Queue(NotificationType.Warning, "reallife_mkt_note_forced", "name", stock.Name, "count", qty.ToString());
                return;
            }
            Queue(NotificationType.Success, order.Side == OrderSide.Buy ? "reallife_mkt_note_bought" : "reallife_mkt_note_sold", "name", stock.Name, "count", qty.ToString(),
                "price", MarketText.Price(cents));
        }

        /// <summary>Money in or out of the account for a reason (a dividend, a payout, interest, a fee, what the broker took).</summary>
        internal void MoneyMoved(TradeKind kind, Stock stock, long amount)
        {
            string name = stock != null ? stock.Name : string.Empty;
            switch (kind)
            {
                case TradeKind.Dividend:
                    Queue(NotificationType.Success, "reallife_mkt_note_dividend", "name", name, "amount", MarketText.Money(amount));
                    break;
                case TradeKind.DividendPaid:
                    Queue(NotificationType.Info, "reallife_mkt_note_dividend_owed", "name", name, "amount", MarketText.Money(amount));
                    break;
                case TradeKind.Payout:
                    Queue(amount > 0 ? NotificationType.Info : NotificationType.Warning, "reallife_mkt_note_payout", "name", name, "amount", MarketText.Money(Math.Abs(amount)));
                    break;
                case TradeKind.Shortfall:
                    Queue(NotificationType.Error, "reallife_mkt_note_shortfall", "amount", MarketText.Money(amount));
                    break;
            }
        }

        internal void MarginCalled(long amount, int byDay)
        {
            Queue(NotificationType.Warning, "reallife_mkt_note_call", "amount", MarketText.Money(amount), "day", byDay.ToString());
        }

        private void Queue(NotificationType type, string key, params string[] pairs)
        {
            if (_notes.Count < 24)
            {
                _notes.Add(new Note { Type = type, Key = key, Pairs = pairs });
            }
        }

        /// <summary>A notification of the game's: the key's text with its names filled in (name, value, name, value); a click opens the app.</summary>
        private void Tell(NotificationType type, string key, string[] pairs)
        {
            Dictionary<string, string> data = null;
            if (pairs != null && pairs.Length > 1)
            {
                data = new Dictionary<string, string>(pairs.Length / 2);   // (a dictionary each: the game keeps a notification's data)
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    data[pairs[i]] = pairs[i + 1] ?? string.Empty;
                }
            }
            Notifications.Show(type, key, data, 10f, null, OpenApp, true, false);
        }

        // ---------------------------------------------------------------- the app (MarketDirector.App.cs)

        partial void InstallApp();

        partial void RemoveApp();

        partial void OpenAppPartial();

        private void OpenApp()
        {
            OpenAppPartial();
        }
    }
}
