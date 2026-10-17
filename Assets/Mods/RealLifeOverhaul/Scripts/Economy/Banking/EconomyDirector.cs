using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The economy's core: the credit rating, the banks' terms by it, phone loans, the Finance app and the credit desk. Its
    /// one-second timer and its hour and day are every economic feature's (<see cref="Follow"/>, <see cref="AfterHour"/>).
    /// Cost: nothing per frame; once a second a few comparisons, the rest on the game's events.
    /// </summary>
    public sealed partial class EconomyDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeFinance";
        private const string PhoneSource = "phone";
        private const string LegalSource = "legal";
        private const string TestSource = "test";
        private const int WaitForRecord = 15;   // seconds the start waits for the legal record to be readable
        private const int TestDays = 30;        // "Test: record 30 days of payments on time" (the number is in its label)
        private const int FindTries = 30;       // seconds the banks are looked for again if the city had none at the start

        private static readonly int HotTick = HotSpots.Slot("Economy: EconomyDirector.Tick");
        // the first second after a load starts and installs the app in one call, each named apart for the session record
        private static readonly int HotStartUp = HotSpots.Slot("Economy: EconomyDirector.StartUp");
        private static readonly int HotInstall = HotSpots.Slot("Economy: EconomyDirector.Install");

        public static IModLogger Logger;
        public static EconomyDirector Instance { get; private set; }

        // A feature that follows this director's second, in the order of the seats on the start line.
        private struct Follower
        {
            public int Seat;
            public Action Second;
            public FeatureGuard Owner;   // the guard of the handler's class: its faults count there, and a feature switched off is skipped
        }

        private static readonly List<Follower> Followers = new List<Follower>(12);

        /// <summary>Called after the credit side's own hour and day, so every other feature comes after the loans are read. A handler must not throw.</summary>
        public static Action AfterHour, AfterDay;

        // The game's new hour and day are every economic feature's: a guard of their own, not the credit file's.
        private static readonly FeatureGuard Ticks = Guard.For(null, "EconomyDirector's hour and day");

        private static readonly Func<int> Today = () => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
        private static readonly Func<float> Income = () => SaveGameManager.Current != null ? PlayerHelper.CalculateDailyIncome() : 0f;
        private static readonly Func<LegalStanding> Legal = () => EconomyBridges.Legal != null ? EconomyBridges.Legal() : default(LegalStanding);

        private readonly GameLoans _loans = new GameLoans();
        private readonly List<ICreditAccount> _others = new List<ICreditAccount>(2);   // other features' credit (EconomyBridges.OtherAccounts)
        private readonly List<LoanView> _live = new List<LoanView>(8);
        private readonly List<LoanFinding> _findings = new List<LoanFinding>(8);
        private readonly CreditFactor[] _tips = new CreditFactor[2];
        private CreditDesk _desk;

        private FinanceApp _view;
        private PhoneApp _app;
        private bool _running;         // the file is open and the banks' terms are the rating's
        private bool _broken;          // a fault: off for the session
        private bool _deskBroken;      // a fault in the credit desk: it is off for the session, the rating and the banks' terms go on
        private bool _gaveUp;          // the game's phone is not built as expected: the app is left off, the rest runs
        private int _waited;
        private int _findTries;
        private int _preRunDay;        // the day whose daily run the hour event has read the loans for
        private float _multiplier;     // the game's interest multiplier the loans' figures were last read at
        private object _toldDialogue;  // the bank dialogue already told why nobody lends
        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen;
        private Action<string> _onGameEvent, _gameEventSeen;
        private Action _deskSlow, _deskKeep, _deskSave, _deskRefused;   // kept, so that calling the desk each second allocates nothing
        private Action<CreditGrade, CreditGrade> _onGrade;
        private Action<CreditEvent, int, int> _onPosted;

        public bool Running => _running;

        /// <summary>The credit desk; never null once the director is awake. It does nothing while the economy is not running.</summary>
        public CreditDesk Desk => _desk;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        /// <summary>
        /// A feature's second, called once a second by this director's timer in the seats' order (Core/StartRules), its first
        /// after a load at its seat on the start line. Called in the feature's Awake with the handler it keeps.
        /// </summary>
        public static void Follow(int seat, Action second)
        {
            if (second == null)
            {
                return;
            }
            Unfollow(second);
            int at = Place(seat + 1);
            Followers.Insert(at, new Follower { Seat = seat, Second = second, Owner = Guard.OfHandler(second) });
        }

        /// <summary>Takes a feature's second off (in its OnDestroy), by the handler it was given with.</summary>
        public static void Unfollow(Action second)
        {
            for (int i = Followers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(Followers[i].Second, second))
                {
                    Followers.RemoveAt(i);
                }
            }
        }

        private static int Place(int seat)
        {
            int at = 0;
            while (at < Followers.Count && Followers[at].Seat < seat)
            {
                at++;
            }
            return at;
        }

        private void Awake()
        {
            Instance = this;
            _desk = new CreditDesk(this);
            _deskSlow = _desk.Slow;
            _deskKeep = _desk.KeepHolds;
            _deskSave = _desk.BeforeSave;
            _deskRefused = _desk.Refused;
            _onSave = OnSave;
            _onHour = Guard.Wrap(Ticks, OnHour);
            _onDay = Guard.Wrap(Ticks, OnDay);
            _onGameEvent = Guard.Wrap<string>(OnGameEvent);
            _onGrade = OnGrade;
            _onPosted = OnPosted;
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;
            GlobalEvents.onNewDay -= _onDay;
            GameEvent.onGameEventTriggered -= _onGameEvent;   // the handler that was put on: Guard.Wrap's
            CreditLedger.GradeChanged -= _onGrade;   // the ledger's events are static: they would keep this director alive
            CreditLedger.Posted -= _onPosted;
            if (Instance != this)
            {
                // another city's director has started since: the file, the banks and the ledger are its
                if (_app != null)
                {
                    _app.Remove();
                    _app = null;
                }
                return;
            }
            // The city is unloading: nothing is written to the save here (the game's current save may be another one by now).
            try
            {
                StandDown("the city is unloading", false, false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                StopDesk("the city is unloading");
                BankTerms.Restore();
            }
            BankTerms.Forget();
            CreditLedger.Clock = null;
            CreditLedger.Income = null;
            CreditLedger.Legal = null;
            Instance = null;
        }

        // The first one after a load, and each follower's first, waits for its seat on the start line: one first load a frame.
        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                while (StartLine.Holds(StartSeats.Credit))
                {
                    yield return null;
                }
                if (!_broken)
                {
                    try
                    {
                        Slow();
                    }
                    catch (Exception e)
                    {
                        Fault("the timer", e);
                    }
                }
                for (int i = 0; i < Followers.Count; i++)
                {
                    int seat = Followers[i].Seat;
                    if (_running && StartLine.Holds(seat))
                    {
                        // every follower needs the credit side running: with it off, none of them starts
                        do
                        {
                            yield return null;
                        }
                        while (StartLine.Holds(seat));
                        i = Place(seat);   // the list may have changed while it waited: a feature's component gone
                        if (i >= Followers.Count || Followers[i].Seat != seat)
                        {
                            i--;
                            continue;
                        }
                    }
                    Call(Followers[i]);
                }
            }
        }

        // A follower's second in a try of its own, as Guard.Each does: a fault counts for its class, and a feature switched off is skipped.
        private static void Call(Follower f)
        {
            if (f.Owner != null && f.Owner.Off)
            {
                return;
            }
            try
            {
                f.Second();
            }
            catch (Exception e)
            {
                Guard.Fail(f.Owner, e);
            }
        }

        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);   // the timer's own look; each follower's second has a scope of its own
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;   // LifeEvents makes the save's modData ready first
            }
            if (!Settings.Economy)
            {
                if (_running)
                {
                    StandDown("the option is off", true, true);
                }
                return;
            }
            if (!_running)
            {
                if (EconomyBridges.LegalReady != null && !EconomyBridges.LegalReady() && ++_waited < WaitForRecord)
                {
                    return;   // the record is part of the starting estimate: a few seconds for its feature to read the save
                }
                StartUp();
            }
            KeepLoanRates();
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            EventHold.Keep(ref GameEvent.onGameEventTriggered, _onGameEvent, ref _gameEventSeen);
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            DeskDo("the timer", _deskSlow);
            if (BankTerms.Lenders.Count == 0 && _findTries < FindTries)
            {
                // the city's buildings were not all there yet: looked for again for a while, then left
                _findTries++;
                if (BankTerms.Find() > 0)
                {
                    CreditLedger.Touch();
                    KeepTerms("the banks were found");
                }
            }
            if (!CreditLedger.Score.Lends)
            {
                TellAtTheBank();
            }
        }

        private void StartUp()
        {
            using var hotSpot = HotSpots.Time(HotStartUp);
            int today = Save.Day;
            CreditLedger.Clock = Today;
            CreditLedger.Income = Income;
            CreditLedger.Legal = Legal;
            int banks = BankTerms.Find();
            GameLoans.Read(_live);
            bool fresh;
            int version;
            CreditFile file = CreditStore.Load(Save.modData, out fresh, out version);
            if (file == null)
            {
                file = new CreditFile();
                CreditScoring.Open(file, _live, today);
                file.LegalSeen = Legal();
            }
            CreditLedger.Open(file);
            CreditLedger.Register(_loans);
            _others.Clear();
            EconomyBridges.OtherAccounts?.Invoke(_others);   // the student loan: the ledger let go of it when the credit side last stood down
            for (int i = 0; i < _others.Count; i++)
            {
                CreditLedger.Register(_others[i]);
            }
            _multiplier = GameLoans.Multiplier;   // the loans' figures are the game's at this multiplier
            LoanBook.Sync(file.Loans, _live, today, null, true);   // the save's marks find their loans again; nothing is said
            if (file.FirstCreditDay <= 0 && _live.Count > 0)
            {
                file.FirstCreditDay = today;   // a loan taken while the economy was off: its history starts when it is first seen
            }
            file.Roll(today);
            DeskDo("the start", () => _desk.Start(file, today));   // the mod's accounts join the rating before it is first worked out
            int score = CreditLedger.Refresh();
            CreditLedger.GradeChanged -= _onGrade;
            CreditLedger.GradeChanged += _onGrade;
            CreditLedger.Posted -= _onPosted;
            CreditLedger.Posted += _onPosted;
            _running = true;
            _preRunDay = 0;
            if (fresh)
            {
                CreditLedger.Post(CreditKind.Started, score, today, GameLoans.AccountKey);
                Logger?.Info("Economy: a credit file is opened for this save on day " + today + " at " + score + " (" + CreditLedger.Grade + "). " + Factors(CreditLedger.Score)
                             + " From " + _live.Count + " loan(s), taken as paid on time for " + file.OldOnTime + " day(s) so far; first credit on day " + file.FirstCreditDay + ".");
            }
            int changed = BankTerms.Apply(score);
            if (Verbose)
            {
                string detail;
                bool ok = BankTerms.Check(score, false, out detail);
                Logger?.Info("Economy check (start): " + banks + " bank(s) found, " + _live.Count + " loan(s), file version " + (fresh ? "new" : version.ToString())
                             + ", rating " + score + " (" + CreditLedger.Grade + "), " + changed + " bank(s) given the terms for it. " + detail + ": "
                             + (banks == 0 ? "looked for again for " + FindTries + " s" : ok ? "PASS" : "FAIL"));
            }
            Badge();
        }

        // The banks get their own terms back and the app leaves the phone. Only 'keep' (a stop in the middle of play, when the
        // game's current save is certainly the file's) writes the file into the save's data first.
        private void StandDown(string why, bool tell, bool keep)
        {
            bool was = _running || BankTerms.Applied;
            if (keep && _running && Save != null && CreditLedger.File != null)
            {
                CreditStore.Save(Save.modData, CreditLedger.File);
            }
            _running = false;
            CreditLedger.GradeChanged -= _onGrade;
            CreditLedger.Posted -= _onPosted;
            StopDesk(why);   // every one of the game's loans has its own figures again
            CreditLedger.Close();
            int restored = BankTerms.Restore();
            if (_app != null)
            {
                _app.Remove();
                _app = null;
                _view = null;
            }
            _gaveUp = false;
            _waited = 0;
            _findTries = 0;
            if (was && (tell || Verbose))
            {
                string detail;
                bool ok = BankTerms.Check(0, true, out detail);
                Logger?.Info("Economy check (terms restored, " + why + "): " + restored + " bank(s) put back. " + detail + ": " + (ok ? "PASS" : "FAIL"));
            }
        }

        // Everything asked of the credit desk: a fault switches the desk off for the session, every loan its own figures again,
        // and the rest of the economy goes on.
        private void DeskDo(string where, Action what)
        {
            if (_deskBroken)
            {
                return;
            }
            try
            {
                what();
            }
            catch (Exception e)
            {
                _deskBroken = true;
                Logger?.Error(e);
                StopDesk("a fault in the credit desk");
                Logger?.Warn("Economy: the credit desk (the mod's own accounts, missed payments, the private lender, the ways out of debt) is switched off for this session after an error in "
                             + where + ". Every loan has its own interest and payment again; the credit rating, the banks' terms and the game's loans are not affected.");
            }
        }

        /// <summary>For the options page: one of the credit desk's test tools.</summary>
        public void DeskTest(Action<CreditDesk> test)
        {
            if (_deskBroken || _broken || !_running)
            {
                TestRefusals.Tell(Logger, "Credit desk", Save == null || !ModState.Loaded ? TestRefusal.NotLoaded : TestRefusal.FeatureOff);
                return;
            }
            DeskDo("a test", () => test(_desk));
        }

        // The desk stops whatever state it is in: a fault in it must not keep the banks from getting their terms back.
        private void StopDesk(string why)
        {
            try
            {
                _desk.Stop(why);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>A fault: one line, the banks' own terms back, and nothing more of the economy this session.</summary>
        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                StandDown("a fault", false, false);   // what a fault leaves of the file is not written anywhere
            }
            catch (Exception second)
            {
                Logger?.Error(second);
                StopDesk("a fault");
                BankTerms.Restore();
            }
            Logger?.Warn("Economy: switched off for this session after an error in " + where
                         + ". The banks have their own terms again and every loan its own interest and payment; the game's daily run is not affected.");
        }

        private void OnSave()
        {
            try
            {
                if (_running && Save != null && CreditLedger.File != null)
                {
                    KeepLoanRates();   // before the desk writes each loan's own figures back for the save
                    DeskDo("the save", _deskSave);   // every loan carries its own interest and payment in the save
                    CreditStore.Save(Save.modData, CreditLedger.File);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
