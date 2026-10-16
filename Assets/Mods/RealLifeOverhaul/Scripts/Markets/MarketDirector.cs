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
    /// in the save and on its tape, and is what the Stocks app and the paper talk to. See docs/features/economy-markets.md.
    ///
    /// A one-second timer on the real clock waits for the save, opens the market (from the save, or a new one with the
    /// old market's shares brought over once) and keeps the save's handler in place. Each frame the market is played up
    /// to the game's own moment for about a millisecond at most (<see cref="BudgetMs"/>; more behind a loading screen or a
    /// pause): a step of the session is 0.1 to 0.2 ms, and the open and the close (about 1 to 3 ms in all) are worked
    /// through a company at a time over a few frames (MarketEngine.Midway). Time that passes in a sleep or a skip is
    /// played out step by step within a few frames, and while the market is behind the clock no order is taken
    /// (OrderWhy.Behind), so nobody can trade at a price the market has already moved past. Anything that changes the
    /// market from outside, or writes it, plays a moment half played out first (<see cref="FinishMoment"/>).
    ///
    /// IN A GOING PUBLIC SESSION the market is the city's, one for every player (Core/SharedCity, part M; Markets/
    /// MarketShare): the host plays it as alone and sends the new lines of its tape; a guest's market is the host's,
    /// with the guest's own holdings, and follows the host's tape, never past the host's furthest moment, so it plays
    /// the very moments the host played. Every player's trades move the one city's prices: their lasting moves are
    /// events of the host's tape (MarketEngine.TradesAsEvents, TradeSink; a guest sends its own once a second, added up
    /// by company, never more lines than the host reads of a request: MarketShare's GuestTrades). When the guest
    /// leaves, its market goes on alone from there, with secrets of its own.
    ///
    /// Everything that happens (a fill, a dividend, a call, the news) comes back through the world
    /// (<see cref="MarketGameWorld"/>) and is told to the player once the frame's work is done. A fault anywhere
    /// switches the market off for the session with one line, and leaves the game and the save's market as they were.
    /// </summary>
    public sealed partial class MarketDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("markets", "MarketDirector", typeof(MarketDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public const string AppId = "RealLifeStocks";
        private const double BudgetMs = 1.0;             // milliseconds of a played frame the market may take to catch up with the clock (a piece more at most)
        private const double HiddenBudgetMs = 8.0;       // ...and of a frame behind a loading screen or a pause (SceneFinds.Hidden)
        private const int MidwayWaitFrames = 120;        // frames the second's work waits at most for a moment half played (it takes a few; what it changes plays it out anyway)
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
        private bool _following;                                    // a guest: the host's market is this one, and its tape the host's
        private readonly List<long> _checks = new List<long>(8);   // a guest: the host's checks still to come (moment, sum)
        private readonly System.Text.StringBuilder _shared = new System.Text.StringBuilder(256);   // the host: tape lines its guests have not had
        private long _checkedAt;
        private long _hostFrontier;                                // a guest: the furthest moment the host has played
        private Action<int, int, long> _tradeSink;
        private Action<string> _askTrade;
        private readonly GuestTrades _guestTrades = new GuestTrades();   // a guest: its fills' moves by company, to the host once a second (MarketShare)
        private Action<BotNote> _botTold;

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
            _tradeSink = (stock, micros, shares) => _guestTrades.Add(stock, micros, shares);   // (sent once a second: SendTrades)
            _askTrade = line => SharedCity.Ask('M', line);
            _botTold = BotHeard;
            StartCoroutine(Guard.Routine(Tick()));
            SharedCity.Register(new SharedCityPart
            {
                Id = 'M', Name = "stock market",
                Ready = () => Running && Save != null && ReferenceEquals(Save, _save),
                Write = () =>
                {
                    _engine.Finish();   // (a moment half played is played out first: MarketEngine.Midway)
                    Flush();   // (the tape's lines up to now are in the market sent; the guests that follow have them with the next update)
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
                MarketTapeFile.Drain();   // (what is on its way to the tape is on disk before the game goes)
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
                while (StartLine.Holds(StartSeats.Markets))
                {
                    yield return null;   // its first load after a save loads waits for a frame of its own (Core/StartLine)
                }
                for (int frames = 0; _running && _engine != null && _engine.Midway && frames < MidwayWaitFrames; frames++)
                {
                    yield return null;   // a moment half played is played out first (a few frames): the second's work never sees half of one
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
            using var hotSpot = HotSpots.Time(HotTick);   // the timer's tick (Core/HotSpots)
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
            FinishMoment();   // (waited for above; one comparison)
            SaveHook.Keep(_onSave);
            MarketBridges.Poll?.Invoke(_say);   // (a guest that follows the host's market hears nothing of its own: the tape's events are the host's)
            _engine.TradesAsEvents = SharedCity.Leading || _engine.Tape.Traded(_engine.State.Day);   // (for the rest of a day that had a shared trade: a replay of it takes every move from the tape)
            OwnSecond();
            BotsSecond();
            InvestSecond();   // (the options of worked orders, dividends put back and rollovers: MarketDirector.Invest.cs)
            SendTrades();
            InstallApp();
        }

        private static readonly int HotUpdate = HotSpots.Slot("Markets: MarketDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
                        Moved();   // (a frame that only worked on a moment half played changed nothing the app shows)
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

        // The moment the market is played to now: the game's clock, and for a guest never past the host's furthest moment.
        private long Goal()
        {
            long target = Target();
            if (_following)
            {
                target = Math.Min(target, _hostFrontier);   // the host's furthest moment: the market goes no further while it follows
                if (_checks.Count >= 2 && _checks[0] > _engine.State.Now && _checks[0] < target)
                {
                    target = _checks[0];   // a moment the host checked: played to, looked at, then on
                }
            }
            return target;
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
                string lines = tape.TakePending();
                MarketTapeFile.Append(tape.Id, lines);
                if (SharedCity.Leading && !_following)
                {
                    _shared.Append(lines);   // the host's guests play the same moments
                }
            }
            TellPaid();   // (MarketDirector.Bots.cs)
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
            JoinNew(state, fresh);   // (companies of the catalogue a save's market never had: MarketDirector.Bonds.cs)
            if (state.YearDays != GameCalendar.DaysPerYear && fresh == false)
            {
                Logger?.Warn("Stock market: the save's year is " + GameCalendar.DaysPerYear + " days and its market's " + state.YearDays + "; the market keeps its own.");
            }
            MarketTape tape = MarketTapeFile.Read(state.Id);
            _engine = new MarketEngine(state, tape, _world);
            _engine.BotTold = _botTold;
            HookBonds();   // (its bonds' news: MarketDirector.Bonds.cs)
            HookInvest();  // (its worked orders' news and the options it follows: MarketDirector.Invest.cs)
            _engine.HoldAfterLoad();
            OwnStarted(save);   // (the player's own company: who they are, and their company back if a save made as a guest left it aside)
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
                _engine.Finish();   // (a moment half played is played out first: MarketEngine.Midway)
                long back = _engine.SettleAll();   // (worked orders' money with the broker back in the cash: the market is off a while)
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
            _guestTrades.Clear();   // (a guest's trades not sent never reach another host)
            RemoveApp();
        }

        // The tape's lines on their way, with the furthest moment played (written once a second as the market goes, so the
        // last second's could be missing): a load after this holds orders back exactly as far as the market went.
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

        /// <summary>A fault: one line, and nothing more of the market this session. What the save holds of it is left as it is.</summary>
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
            using var hotSpot = HotSpots.Time(HotSave);   // the market and its tape written whole (Core/HotSpots)
            try
            {
                if (_running && Save != null && ReferenceEquals(Save, _save))
                {
                    _engine.Finish();   // (a moment half played is played out first: MarketEngine.Midway)
                    Flush();   // (the lines on their way, to the file and to the guests, before the tape is written whole)
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

        // ---------------------------------------------------------------- a Going Public session

        // The host: what its guests have not had of the tape, with where the market stands now; null if nothing has moved.
        private string HostChanges()
        {
            if (!Running)
            {
                _shared.Length = 0;
                return null;
            }
            if (_engine.Midway)
            {
                return null;   // (a moment half played: its lines and its check go with the next update, once it is played out)
            }
            string lines = _shared.Length > 0 ? _shared.ToString() : null;
            _shared.Length = 0;
            return MarketShare.Changes(lines, _engine.State, ref _checkedAt);
        }

        // A guest, once a second: its trades since the last look, a line for each company (more for a large move), no more
        // than a host reads of one request with what is already waiting in it; the rest at the next second (MarketShare's
        // GuestTrades). The same lines as ever: an older host takes them.
        private void SendTrades()
        {
            if (!_following || _guestTrades.Empty)
            {
                return;
            }
            int room = GuestTrades.LinesMost - SharedCity.Waiting('M');
            int sent = room > 0 ? _guestTrades.Send(_askTrade, room) : 0;
            if (Verbose && sent > 0)
            {
                Logger?.Info("Stock market check (a guest): " + sent + " trade line(s) for the host, " + _guestTrades.Lines + " waiting for the next second, at most "
                             + GuestTrades.LinesMost + " a request: PASS");
            }
        }

        // The host: a guest's trades, put on the tape for every market at the next moment.
        private void HostRequest(string text, string who)
        {
            if (!Running || string.IsNullOrEmpty(text))
            {
                return;
            }
            string[] lines = text.Split('\n');
            int done = 0;
            for (int i = 0; i < lines.Length && i < GuestTrades.LinesMost; i++)
            {
                string[] f = lines[i].Split('|');
                if (f.Length >= 4 && f[0] == "i" && int.TryParse(f[1], out int stock) && int.TryParse(f[2], out int micros) && long.TryParse(f[3], out long shares)
                    && _engine.GuestTrade(stock, micros, shares))
                {
                    done++;
                }
            }
            if (Verbose)
            {
                Logger?.Info("Stock market check (the host): " + done + " trade(s) of " + who + " put on the tape for the next moment: PASS");
            }
        }

        // A guest: the host's market, whole (it becomes this machine's market, with the player's own in it) or the new lines of its tape.
        private bool ApplyHost(string text, bool whole)
        {
            if (!Running)
            {
                return false;
            }
            _engine.Finish();   // (a moment half played is played out first: MarketEngine.Midway)
            if (!whole)
            {
                if (!_following || !MarketShare.Follow(_engine.Tape, text, _checks, ref _hostFrontier))
                {
                    return false;
                }
                _engine.Follow(_hostFrontier);
                return true;
            }
            MarketTape tape;
            long frontier;
            var checks = new List<long>(4);
            MarketState state = MarketShare.Adopt(_engine.State, text, checks, HostTape(text), out tape, out frontier);
            if (state == null)
            {
                return false;
            }
            if (_engine.Tape.HasPending)
            {
                MarketTapeFile.Append(_engine.Tape.Id, _engine.Tape.TakePending());   // (this machine's own tape, as it was)
            }
            ulong was = _engine.State.Id;
            _engine = new MarketEngine(state, tape, _world);
            _engine.BotTold = _botTold;
            HookBonds();   // (its bonds' news: MarketDirector.Bonds.cs)
            HookInvest();  // (its worked orders' news and the options it follows: MarketDirector.Invest.cs)
            _engine.Follow(frontier);
            _engine.TradeSink = _tradeSink;
            state.Following = true;   // (the player's own company is set aside meanwhile: MarketShare.Adopt)
            _hostFrontier = frontier;
            MarketTapeFile.Rewrite(state.Id, tape.Whole());
            _frontierWritten = tape.Frontier;
            _following = true;
            _checks.Clear();
            _checks.AddRange(checks);
            Behind = false;
            _notes.Clear();
            Moved();
            Logger?.Info("Stock market: the host's market is this one now (" + state.Stocks.Count + " companies, day " + state.Day + " phase " + state.Phase + ", its tape to "
                         + tape.FrontierDay + "/" + tape.FrontierPhase + (was != state.Id ? "; this save's own market was another playthrough's" : string.Empty) + "), with this player's "
                         + state.Positions.Count + " holding(s) and " + state.Orders.Count + " order(s).");
            return true;
        }

        // The tape a guest already has of the host's market (this machine's own, or a copy of the host's from an earlier
        // session), so that its past is kept; null for a market it has never followed.
        private MarketTape HostTape(string text)
        {
            int at = text.IndexOf("~h:", StringComparison.Ordinal);
            if (at < 0 || text.Length < at + 19 || !ulong.TryParse(text.Substring(at + 3, 16), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ulong id))
            {
                return null;
            }
            if (_engine.Tape.Id == id)
            {
                return _engine.Tape;
            }
            return System.IO.File.Exists(MarketTapeFile.PathOf(id)) ? MarketTapeFile.Read(id) : null;
        }

        // A guest at a moment the host checked: the same prices, or the market is asked for again.
        private void Checked()
        {
            while (_checks.Count >= 2 && _checks[0] <= _engine.State.Now)
            {
                long at = _checks[0], sum = _checks[1];
                _checks.RemoveRange(0, 2);
                if (at != _engine.State.Now)
                {
                    continue;   // (passed in a jump of the clock: the next one will do)
                }
                long mine = MarketShare.Checksum(_engine.State);
                if (mine != sum)
                {
                    Logger?.Warn("Stock market: at " + at / 100 + "/" + at % 100 + " this guest's market is not the host's (" + mine + " against " + sum + "); it is asked for again.");
                    SharedCity.AskAgain('M');
                }
                else if (Verbose)
                {
                    Logger?.Info("Stock market check (a guest, " + at / 100 + "/" + at % 100 + "): the same prices as the host's: PASS");
                }
            }
        }

        // A guest no longer follows: the market goes on from here alone, with secrets of its own past the host's last moment.
        private void Unfollow()
        {
            _following = false;
            _checks.Clear();
            _guestTrades.Clear();   // (not sent: the host is no longer followed)
            if (_engine != null)
            {
                _engine.Finish();   // (a moment half played is played out as the guest's: MarketEngine.Midway)
                _engine.TradeSink = null;
                _engine.State.Following = false;
                Logger?.Info("Stock market: the host's market is no longer followed; it goes on alone from day " + _engine.State.Day + " phase " + _engine.State.Phase + ".");
                OwnAlone();
            }
        }

        // ---------------------------------------------------------------- what the player does

        /// <summary>What an order would come to now, and whether it can be placed (nothing is changed).</summary>
        public Estimate Estimate(int stockId, OrderSide side, OrderType type, long qty, int limitCents, int stopCents, bool closing = false)
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
            return MarketTrading.Check(s, s.Stock(stockId), side, type, qty, limitCents, stopCents, _engine.Wallet.Cash, CommissionLeast, Trading, _engine.Held, s.Phase, closing);   // (the money with the broker counts: MarketEngine.Wallet)
        }

        /// <summary>An order placed: filled at once if it can be, else it waits. Null with the reason if it cannot be placed.</summary>
        public Order Place(int stockId, OrderSide side, OrderType type, OrderLife life, long qty, int limitCents, int stopCents, bool test, out OrderWhy why, bool closing = false)
        {
            FinishMoment();
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
            if (!Settings.MarketsMargin && _engine.State.Account.Margin && !closing && !Closes(stockId, side, qty))
            {
                why = OrderWhy.NoMargin;   // (with margin trading off an account that is open is only wound down)
                return null;
            }
            try
            {
                Order order = _engine.Place(stockId, side, type, life, qty, limitCents, stopCents, Trading, _engine.Held, test, out why, closing);
                if (Verbose && order != null)
                {
                    Logger?.Info("Stock market check (order " + order.Id + "): " + side + " " + qty + " of " + stockId + " (" + type + (closing ? ", closing" : string.Empty) + ", " + life + ") is "
                                 + order.State + ": PASS");
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
            FinishMoment();
            if (!Running || Behind || _engine.Held)
            {
                return false;
            }
            bool done = _engine.Cancel(orderId);   // (a worked order's money with the broker settled at once: MarketEngine.Invest.cs)
            Moved();
            return done;
        }

        /// <summary>A company on or off the watch list; false if the list is full.</summary>
        public bool Watch(int stockId, bool on)
        {
            FinishMoment();
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
            FinishMoment();
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
            if (!_engine.Wallet.Take(amount, MarketMoney.Deposit, string.Empty))   // (what is short taken back from worked orders' money with the broker first)
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
            FinishMoment();
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

        /// <summary>
        /// A moment of the market half played (MarketEngine.Midway: the open or the close, worked through over a few
        /// frames) played out at once: every method that changes the market from outside asks for this first, so the
        /// moment is played exactly as in one frame. Nothing when none is (one comparison).
        /// </summary>
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
                Behind = false;   // (caught up with the clock, as it was when a moment was played in one frame)
            }
        }

        // ---------------------------------------------------------------- what the market tells

        /// <summary>A piece of market news (MarketGameWorld): the player hears of what touches what they hold.</summary>
        internal void Heard(in MarketNews news)
        {
            MarketState s = _engine != null ? _engine.State : null;
            if (s == null || news.Kind == MarketNewsKind.Paid || OwnHeard(news))
            {
                return;   // (news of the player's own company, or of their way to the market: MarketDirector.Own.cs)
            }
            if (news.Stock <= 0 || (s.Held(news.Stock) == 0 && !s.Watching(news.Stock)))
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
            if (order == null || stock == null || order.Worked)
            {
                return;   // (a worked order's slices are told by the day, and its end as it comes: MarketDirector.Invest.cs)
            }
            if (order.Back)
            {
                // A dividend put back: what it bought (only a part of it, with the reason: one order's most or the holding
                // limit), or why it could not; what it does not buy stays in the cash.
                if (qty <= 0)
                {
                    Queue(NotificationType.Warning, "reallife_mkt_inv_note_back_refused", "name", stock.Name, "why", MarketText.Why(order.Why));
                }
                else if (order.Why != OrderWhy.None)
                {
                    Queue(NotificationType.Info, "reallife_mkt_inv_note_back_part", "name", stock.Name, "count", qty.ToString("N0"), "price", MarketText.Price(cents), "why",
                        MarketText.Why(order.Why));
                }
                else
                {
                    Queue(NotificationType.Success, "reallife_mkt_inv_note_back", "name", stock.Name, "count", qty.ToString("N0"), "price", MarketText.Price(cents));
                }
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
            if (order.Closing)
            {
                // A stop loss says where it was set: a gap or a fast fall takes it under.
                Queue(order.Type == OrderType.Stop ? NotificationType.Warning : NotificationType.Success,
                    order.Type == OrderType.Stop ? "reallife_mkt_note_stop_loss" : "reallife_mkt_note_take_profit", "name", stock.Name, "count", qty.ToString(), "price", MarketText.Price(cents),
                    "stop", MarketText.Price(order.StopCents));
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
                    _paid.Add(stock != null ? stock.Id : 0, 0, amount);   // (told with any others paid at the same moment: TellPaid)
                    DividendTax(stock, amount);
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
            Notifications.Show(type, key, data, 10f, null, Guard.Wrap(OpenApp), true, false);
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
