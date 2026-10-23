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
    /// The city's stock market in the game: it keeps the game-free MarketEngine on the game's clock, in the save and on its
    /// tape (docs/features/economy-markets.md). Each frame it plays the market to the game's moment for about a millisecond
    /// (<see cref="BudgetMs"/>); while the market is behind the clock no order is taken. Idle, a comparison a frame. A fault
    /// switches the market off for the session and leaves the save's market as it was.
    /// </summary>
    public sealed partial class MarketDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("markets", "MarketDirector", typeof(MarketDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public const string AppId = "RealLifeStocks";
        private const double BudgetMs = 1.0;             // ms of a played frame to catch up with the clock, a piece more at most
        private const double HiddenBudgetMs = 8.0;       // ms of a frame behind a loading screen or a pause
        private const int MidwayWaitFrames = 120;        // frames the second's work waits at most for a moment half played
        private const float FrontierEvery = 1f;          // real seconds between writing the furthest moment played to the tape
        private const int StartWaitSeconds = 10;         // seconds a new market waits for the wiring before it opens without the old one

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
        private bool _following;                                    // a guest: the host's market is this one, and its tape the host's
        private readonly List<long> _checks = new List<long>(8);   // a guest: the host's checks still to come, moment and sum
        private readonly System.Text.StringBuilder _shared = new System.Text.StringBuilder(256);   // the host: tape lines its guests have not had
        private long _checkedAt;
        private long _hostFrontier;                                // a guest: the furthest moment the host has played
        private Action<int, int, long> _tradeSink;
        private Action<string> _askTrade;
        private readonly GuestTrades _guestTrades = new GuestTrades();   // a guest: its fills' moves by company, to the host once a second
        private Action<BotNote> _botTold;

        /// <summary>Raised after the market moved on or the player changed it, so the app shows it again.</summary>
        public event Action Changed;

        public bool Running => _running && !_broken;

        public MarketState State => Running ? _engine.State : null;

        public MarketEngine Engine => Running ? _engine : null;

        /// <summary>True while the market is catching up with the game's clock after a sleep or a skip: no order is taken.</summary>
        public bool Behind { get; private set; }

        /// <summary>A session, caught up, not held after a load.</summary>
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
            GameUi.GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            _onSave = OnSave;
            _say = (kind, a, b) => _engine?.Event(kind, a, b);
            _world = new MarketGameWorld(this);
            _tradeSink = (stock, micros, shares) => _guestTrades.Add(stock, micros, shares);   // sent once a second: SendTrades
            _askTrade = line => SharedCity.Ask('M', line);
            _botTold = BotHeard;
            StartCoroutine(Guard.Routine(Tick()));
            SharedCity.Register(new SharedCityPart
            {
                Id = 'M', Name = "stock market",
                Ready = () => Running && Save != null && ReferenceEquals(Save, _save),
                Write = () =>
                {
                    _engine.Finish();
                    Flush();   // the tape's lines so far go in the market sent; the guests have them with the next update
                    return MarketShare.Write(_engine.State, _engine.Tape);
                },
                Changes = HostChanges,
                Apply = ApplyHost,
                Request = HostRequest,
                Left = Unfollow
            });
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                StandDown(false);
                MarketTapeFile.Drain();   // what is on its way to the tape is on disk before the game goes
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
                while (StartLine.Holds(StartSeats.Markets))
                {
                    yield return null;   // its first load after a save loads waits for a frame of its own (Core/StartLine)
                }
                for (int frames = 0; _running && _engine != null && _engine.Midway && frames < MidwayWaitFrames; frames++)
                {
                    yield return null;   // a moment half played is played out first: the second's work never sees half of one
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

        private static readonly int HotTick = HotSpots.Slot("Markets: MarketDirector.Tick");
        // Once a second: start when the save is ready, stand down when the option goes off or another save is loaded.
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (_running && !ReferenceEquals(Save, _save))
            {
                StandDown(false);   // another save: this one's market is in that save already
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
            FinishMoment();   // waited for above; one comparison
            SaveHook.Keep(_onSave);
            MarketBridges.Poll?.Invoke(_say);   // a guest hears nothing of its own: the tape's events are the host's
            BuildersSecond();   // the construction game's news of the city's builders (MarketDirector.Builders.cs)
            _engine.TradesAsEvents = SharedCity.Leading || _engine.Tape.Traded(_engine.State.Day);   // for the rest of a day that had a shared trade, so a replay takes every move from the tape
            OwnSecond();
            BotsSecond();
            InvestSecond();   // worked orders, dividends put back and rollovers
            DerivsSecond();   // the raw materials exchange and the player's futures and options
            SendTrades();
            InstallApp();
        }

        private static readonly int HotUpdate = HotSpots.Slot("Markets: MarketDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults (Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (!_running || _broken || Save == null || LoadScene.isLoading)
            {
                return;
            }
            try
            {
                long target = Goal();
                if (_engine.State.Now < target || _engine.Midway)
                {
                    long was = _engine.State.Now;
                    bool there = _engine.RunFor(target, (long)((SceneFinds.Hidden ? HiddenBudgetMs : BudgetMs) * System.Diagnostics.Stopwatch.Frequency / 1000.0));
                    bool moved = _engine.State.Now != was || there == Behind;
                    Behind = !there;
                    if (moved)
                    {
                        Moved();   // a frame that only worked on a moment half played changed nothing the app shows
                    }
                    if (_following)
                    {
                        Checked();
                    }
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

        // The moment the market is played to: the game's clock, and for a guest never past the host's furthest moment.
        private long Goal()
        {
            long target = Target();
            if (_following)
            {
                target = Math.Min(target, _hostFrontier);   // the market goes no further while it follows
                if (_checks.Count >= 2 && _checks[0] > _engine.State.Now && _checks[0] < target)
                {
                    target = _checks[0];   // a moment the host checked: played to, looked at, then on
                }
            }
            return target;
        }

        private static long Target()
        {
            GameInstance save = Save;
            int minute = save.Hour * 60 + Mathf.FloorToInt(save.Minute);
            int phase = MarketCatalogue.TradingDay(save.Day) ? MarketCatalogue.PhaseAt(minute) : MarketCatalogue.PhaseClose;
            return MarketState.Time(save.Day, phase);
        }

        // The tape's new lines go to disk at once, the furthest moment played at least once a second.
        private void Flush()
        {
            DeliverDue();   // a future's goods its day's close asked for, after the frame's market work
            MarketTape tape = _engine.Tape;
            if (Time.realtimeSinceStartup >= _frontierAt && tape.Frontier > _frontierWritten)
            {
                tape.WriteFrontier();
                _frontierWritten = tape.Frontier;
                _frontierAt = Time.realtimeSinceStartup + FrontierEvery;
            }
            if (tape.HasPending)
            {
                string lines = tape.TakePending();
                MarketTapeFile.Append(tape.Id, lines);
                if (SharedCity.Leading && !_following)
                {
                    _shared.Append(lines);   // the host's guests play the same moments
                }
            }
            TellPaid();
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

        private void StartUp()
        {
            GameInstance save = Save;
            MarketState state = MarketStore.Load(save.modData, out int version);
            bool fresh = state == null;
            MarketCarryOver.Result moved = default(MarketCarryOver.Result);
            if (fresh)
            {
                // The old market's file from the wiring: while it is not connected or Ventures is still opening it, the market
                // waits a few seconds at most, then opens with nothing brought.
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
            JoinNew(state, fresh);   // catalogue companies a save's market never had
            if (state.YearDays != GameCalendar.DaysPerYear && fresh == false)
            {
                Logger?.Warn("Stock market: the save's year is " + GameCalendar.DaysPerYear + " days and its market's " + state.YearDays + "; the market keeps its own.");
            }
            MarketTape tape = MarketTapeFile.Read(state.Id);
            _engine = new MarketEngine(state, tape, _world);
            _engine.BotTold = _botTold;
            HookBonds();
            HookInvest();
            HookDerivs();
            _engine.HoldAfterLoad();
            OwnStarted(save);   // who the player is, and their company back if a save as a guest set it aside
            _frontierWritten = tape.Frontier;
            _save = save;
            _running = true;
            BuilderMarket.Clear();   // news of builders heard before this save's market ran is no news of its
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

        // A new market for this save; its id and seed come from the playthrough and the day, so the same save opens the same market.
        private MarketState Open(GameInstance save, Economy.Ventures.VenturesFile old)
        {
            string playthrough = !string.IsNullOrEmpty(save.seed) ? save.seed : save.characterId ?? "city";
            ulong id = MarketRandom.Mix64(MarketRandom.Of(playthrough) ^ ((ulong)(uint)save.Day * 0x9E3779B97F4A7C15UL));
            var names = new List<string>(4);
            var ids = new List<string>(4);
            MarketBridges.Rivals?.Invoke(names, ids);
            List<CarriedPrice> carried = old != null && old.Holdings.Count > 0 && !old.MarketMoved ? MarketCarryOver.Prices(old) : null;
            int minute = save.Hour * 60 + Mathf.FloorToInt(save.Minute);
            return MarketSetup.Open(id, save.Day, minute, GameCalendar.DaysPerYear, _world.Env(save.Day), id, names, ids, carried, Settings.MarketsBuilders);
        }

        // The market stops; with 'keep' it goes into the save's data first, when the current save is certainly its own.
        private void StandDown(bool keep)
        {
            if (keep && _running && Save != null && ReferenceEquals(Save, _save))
            {
                _engine.Finish();
                long back = _engine.SettleAll();   // worked orders' money with the broker back in the cash: the market is off a while
                if (back > 0)
                {
                    Logger?.Info("Stock market: stopping; " + back + " of worked orders' money with the broker is back in the cash.");
                }
                MarketStore.Save(Save.modData, _engine.State);
                MarketTapeFile.Rewrite(_engine.Tape.Id, _engine.Tape.Whole());
            }
            else if (_running)
            {
                KeepTape();
            }
            _running = false;
            _engine = null;
            _save = null;
            _notes.Clear();
            Behind = false;
            _following = false;
            _checks.Clear();
            _shared.Length = 0;
            _guestTrades.Clear();   // a guest's unsent trades never reach another host
            _deliveries.Clear();
            BuilderMarket.Clear();   // the builders' news waiting was this save's
            RemoveApp();
            try
            {
                MarketBridges.MaterialsFixed?.Invoke();   // the importers' prices are their kinds' again
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The tape's lines on their way with the furthest moment played, so a load holds orders back exactly as far as it went.
        private void KeepTape()
        {
            MarketTape tape = _engine.Tape;
            if (tape.Frontier > _frontierWritten)
            {
                tape.WriteFrontier();
                _frontierWritten = tape.Frontier;
            }
            if (tape.HasPending)
            {
                MarketTapeFile.Append(tape.Id, tape.TakePending());
            }
        }

        /// <summary>A fault: one line, and nothing more of the market this session; the save's market is left as it is.</summary>
        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                if (_running)
                {
                    KeepTape();
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

        private static readonly int HotSave = HotSpots.Slot("Markets: MarketDirector.OnSave");
        private void OnSave()
        {
            using var hotSpot = HotSpots.Time(HotSave);   // the market and its tape written whole
            try
            {
                if (_running && Save != null && ReferenceEquals(Save, _save))
                {
                    _engine.Finish();
                    Flush();   // the lines on their way, to the file and the guests, before the tape is written whole
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

        /// <summary>A moment half played played out at once, so it plays as in one frame; one comparison when none is.</summary>
        private void FinishMoment()
        {
            if (!Running || !_engine.Midway)
            {
                return;
            }
            _engine.Finish();
            if (_following)
            {
                Checked();
            }
            if (Behind && _engine.State.Now >= Goal())
            {
                Behind = false;   // caught up with the clock, as when a moment was played in one frame
            }
        }

        partial void InstallApp();

        partial void RemoveApp();

        partial void OpenAppPartial();

        private void OpenApp()
        {
            OpenAppPartial();
        }
    }
}
