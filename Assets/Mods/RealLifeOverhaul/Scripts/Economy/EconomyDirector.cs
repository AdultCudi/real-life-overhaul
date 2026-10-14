using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Dialogs;
using Helpers;
using RealLifeOverhaul.GameUi;
using SpecialServices.Bank;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The economy core: the credit rating, the banks' terms by that rating, loans from the phone and the Finance app.
    /// See the README ("Economy: credit rating, lending and the Finance app").
    ///
    /// This class keeps it going and nothing more. It has no Update. A one-second timer on the real clock waits for
    /// the save, puts the app on the phone, keeps the three event handlers in place (the game empties its events on
    /// every load) and, only while nobody lends, looks whether a bank's dialogue is open, to say why. The work itself
    /// is done on the game's own events: the hour (the loans are read, the banks' terms kept right), the day (what
    /// became of each instalment, the record, the score), the save, and when the app is opened.
    ///
    /// A fault anywhere switches the economy off for the session with one line, puts the banks' own terms back and
    /// leaves the game and its daily run alone (<see cref="Fault"/>).
    /// </summary>
    public sealed class EconomyDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeFinance";
        private const string PhoneSource = "phone";
        private const string LegalSource = "legal";
        private const string TestSource = "test";
        private const int WaitForRecord = 15;   // seconds the start waits for the legal record to be readable
        private const int TestDays = 30;        // "Test: record 30 days of payments on time" (the number is in its label)
        private const int FindTries = 30;       // seconds the banks are looked for again if the city had none at the start

        public static IModLogger Logger;
        public static EconomyDirector Instance { get; private set; }

        /// <summary>
        /// Called once a second on the real clock by this director's own timer, after its own look and whatever state
        /// the credit side is in (off, running or stopped by a fault): the city's economy (Economy/City) looks at its
        /// option and its save with it and has no timer of its own. A handler must not throw.
        /// </summary>
        public static Action Second;

        /// <summary>
        /// Called at the end of this director's own work on the game's hour event and on its day event, while the
        /// credit side runs: the city's economy does its hour and its day here, so it always comes after the loans are
        /// read and the accounts have taken their payments, and needs no place of its own in the game's events.
        /// A handler must not throw.
        /// </summary>
        public static Action AfterHour, AfterDay;

        // The game's new hour and new day for the economy and every feature that follows it (AfterHour, AfterDay).
        private static readonly FeatureGuard Ticks = Guard.For(null, "EconomyDirector's hour and day");

        private readonly GameLoans _loans = new GameLoans();
        private readonly List<LoanView> _live = new List<LoanView>(8);
        private readonly List<LoanFinding> _findings = new List<LoanFinding>(8);
        private readonly CreditFactor[] _tips = new CreditFactor[2];
        private CreditDesk _desk;      // the mod's own accounts, missed payments, the private lender, the ways out of debt

        private FinanceApp _view;
        private PhoneApp _app;
        private bool _running;         // the file is open and the banks' terms are the rating's
        private bool _broken;          // a fault: off for the session
        private bool _gaveUp;          // the game's phone is not built as expected: the app is left off, the rest runs
        private int _waited;
        private int _findTries;
        private int _preRunDay;        // the day whose daily run the hour event has read the loans for
        private object _toldDialogue;  // the bank dialogue that has already been told why nobody lends
        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen;
        private Action _deskSlow, _deskKeep, _deskSave, _deskRefused;   // kept, so that calling the desk each second allocates nothing
        private bool _deskBroken;      // a fault in the credit desk: it is off for the session, the rating and the banks' terms go on
        private Action<CreditGrade, CreditGrade> _onGrade;
        private Action<CreditEvent, int, int> _onPosted;

        public bool Running => _running;

        /// <summary>The credit desk; never null once the director is awake. It does nothing while the economy is not running.</summary>
        public CreditDesk Desk => _desk;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _desk = new CreditDesk(this);
            _deskSlow = _desk.Slow;
            _deskKeep = _desk.KeepHolds;
            _deskSave = _desk.BeforeSave;
            _deskRefused = _desk.Refused;
            _onSave = OnSave;
            _onHour = Guard.Wrap(Ticks, OnHour);   // (the economy's hour and day are every economic feature's: a guard of their own, not the credit file's)
            _onDay = Guard.Wrap(Ticks, OnDay);
            _onGrade = OnGrade;
            _onPosted = OnPosted;
            StartCoroutine(Tick());
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;
            GlobalEvents.onNewDay -= _onDay;
            if (Instance != this)
            {
                // Another city's director has started since: the file, the banks and the ledger are its, and are left alone.
                if (_app != null)
                {
                    _app.Remove();
                    _app = null;
                }
                return;
            }
            // The city is unloading (or the mod is being switched off): the banks have their own terms again before
            // anything else. Nothing is written to the save here: by now the game's current save may be another one,
            // and what a save keeps of the file is written when the game saves (OnSave).
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

        // The first look is a second after the city's objects are made, never in the frame they are made in.
        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
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
                Guard.Each(Second);   // each feature's second in a try of its own (Core/Guard)
            }
        }

        // Once a second: start when the save is ready, stand down when the option goes off, hold the handlers, the app on the phone.
        private void Slow()
        {
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
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            DeskDo("the timer", _deskSlow);
            if (BankTerms.Lenders.Count == 0 && _findTries < FindTries)
            {
                // The city's buildings were not all there yet: looked for again for a while, then left.
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

        // ---------------------------------------------------------------- starting and stopping

        private void StartUp()
        {
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
            LoanBook.Sync(file.Loans, _live, today, null, true);   // the save's marks find their loans again; nothing is said
            if (file.FirstCreditDay <= 0 && _live.Count > 0)
            {
                file.FirstCreditDay = today;   // a loan taken while the economy was off: its history starts when it is first seen
            }
            file.Roll(today);
            DeskDo("the start", () => _desk.Start(file, today));   // the mod's own accounts join the rating before it is first worked out
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
                             + ", rating " + score + " (" + CreditLedger.Grade + "), " + changed + " bank(s) given the terms for it. " + detail + ": " + (banks == 0 ? "looked for again for " + FindTries + " s" : ok ? "PASS" : "FAIL"));
            }
            Badge();
        }

        // The economy stops: the banks get their own terms back and the app leaves the phone. With 'keep' the file goes
        // into the save's data first, so that switching the option on again (or the next save) has it; that is only
        // for a stop in the middle of play, when the game's current save is certainly the one the file belongs to.
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

        // Everything the director asks of the credit desk goes through here. A fault in the desk switches the desk off
        // for the session with one line and gives every loan its own interest and payment back; the rating, the banks'
        // terms, the Finance app and the game's daily run go on. What the save keeps of the accounts is kept.
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

        // The credit desk stops, whatever state it is in: a fault in it must not keep the banks from getting their terms back.
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
                StandDown("a fault", false, false);   // (what a fault leaves of the file is not written anywhere)
            }
            catch (Exception second)
            {
                Logger?.Error(second);
                StopDesk("a fault");
                BankTerms.Restore();
            }
            Logger?.Warn("Economy: switched off for this session after an error in " + where + ". The banks have their own terms again and every loan its own interest and payment; the game's daily run is not affected.");
        }

        private void Install()
        {
            var view = new FinanceApp(this);
            GameSprites.Want(FinanceApp.AppGlyphs);
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_econ_app",
                Icon = like => AppIconPainter.Paint(like, FinanceApp.Glyph(), new Color32(92, 214, 148, 255), new Color32(24, 148, 110, 255), 0.42f, "AppIcon-Finance"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Economy: the game's phone is not built as this version expects, so the Finance app is left off. The rating and the banks' terms still work.");
                }
                return;
            }
            _view = view;
            Badge();
        }

        // ---------------------------------------------------------------- what the ledger reads

        private static readonly Func<int> Today = () => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
        private static readonly Func<float> Income = () => SaveGameManager.Current != null ? PlayerHelper.CalculateDailyIncome() : 0f;
        private static readonly Func<LegalStanding> Legal = () => EconomyBridges.Legal != null ? EconomyBridges.Legal() : default(LegalStanding);

        // ---------------------------------------------------------------- the hour and the day

        private static readonly int HotOnHour = HotSpots.Slot("Economy: EconomyDirector.OnHour");
        // GlobalEvents.onNewHour: every game hour, and first thing in the game's NewDay, before its daily run.
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                DeskDo("the hour", _deskKeep);   // what is held on a loan is in place before the loans are read (the night's run may follow at once)
                SyncLoans(day);
                if (day != CreditLedger.File.SettledDay)
                {
                    bool first = _preRunDay != day;
                    _preRunDay = day;   // the marks are now exactly as the loans stand before this day's run
                    if (first)
                    {
                        DeskDo("the overdraft", () => _desk.BeforeRun(day));   // the overdraft covers a day that is short
                    }
                }
                DeskDo("the hour", () => _desk.Hour(day));
                CreditLedger.Touch();
                KeepTerms("the hour");
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
            Guard.Each(AfterHour);
        }

        private static readonly int HotOnDay = HotSpots.Slot("Economy: EconomyDirector.OnDay");
        // GlobalEvents.onNewDay: after the game's daily run (GameManager.NewDay; LoanHelper.RunDaily is in BusinessHelper.RunDaily before it).
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                Settle(Save.Day);
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
            Guard.Each(AfterDay);
            try
            {
                // The game built yesterday's statement before the mod's day: the costs booked for it since are put into it.
                if (GameBooks.Refresh(Save.Day - 1) && Verbose)
                {
                    Logger?.Info("Economy check (books): the game's statement of day " + (Save.Day - 1) + " brought up to date with the mod's costs booked for it after it was made: PASS.");
                }
                // Another author's dealerships (Core/EmpireMotors): what they spent is deducted by the game's tax, once a day.
                float deducted = GameBooks.Deduct(Save.Day - 1);
                if (Verbose && (deducted > 0f || GameBooks.DealerTakings != 0f || GameBooks.DealerCosts != 0f))
                {
                    Logger?.Info("Economy check (Empire Motors): day " + (Save.Day - 1) + " the dealerships took " + GameBooks.DealerTakings.ToString("0") + " and spent "
                                 + GameBooks.DealerCosts.ToString("0") + " in the game's statements; " + deducted.ToString("0") + " given to the game's tax to deduct: "
                                 + (deducted <= Mathf.Max(0f, GameBooks.DealerCosts) + 1f ? "PASS" : "FAIL") + ".");
                }
            }
            catch (Exception e)
            {
                Fault("the game's statement", e);
            }
        }

        // The loans as they are now against the file's marks: what the player did since the last look.
        private void SyncLoans(int day)
        {
            CreditFile file = CreditLedger.File;
            GameLoans.Read(_live);
            _findings.Clear();
            LoanBook.Sync(file.Loans, _live, day, _findings);
            for (int i = 0; i < _findings.Count; i++)
            {
                LoanFinding f = _findings[i];
                switch (f.Note)
                {
                    case LoanNote.New:
                        // A loan made at the bank (one made from the phone is on file before this runs).
                        CreditLedger.Post(CreditKind.Application, f.Amount, day, GameLoans.AccountKey);
                        CreditLedger.Post(CreditKind.NewCredit, f.Amount, day, GameLoans.AccountKey);
                        break;
                    case LoanNote.Gone:
                        CreditLedger.Post(CreditKind.PaidOffEarly, f.Amount, day, GameLoans.AccountKey);
                        break;
                    case LoanNote.ExtraPaid:
                        CreditLedger.Post(CreditKind.ExtraPayment, f.Amount, day, GameLoans.AccountKey);
                        break;
                    case LoanNote.PaymentChanged:
                        CreditLedger.Post(CreditKind.PaymentChanged, f.Amount, day, GameLoans.AccountKey);
                        break;
                }
            }
        }

        private void Settle(int day)
        {
            CreditFile file = CreditLedger.File;
            bool exact = _preRunDay == day;
            GameLoans.Read(_live);
            _findings.Clear();
            LoanBook.Settle(file.Loans, _live, _findings);
            int payDay = day - 1;   // the game books its daily run on the day that has ended (LoanHelper.RunDaily)
            int paid = 0, paidAmount = 0, missed = 0, missedAmount = 0;
            StringBuilder log = Verbose ? new StringBuilder(200) : null;
            for (int i = 0; i < _findings.Count; i++)
            {
                LoanFinding f = _findings[i];
                switch (f.Note)
                {
                    case LoanNote.Paid:
                    case LoanNote.PaidOff:
                        paid++;
                        paidAmount += f.Amount;
                        break;
                    case LoanNote.Missed:
                        missed++;
                        missedAmount += f.Amount;
                        break;
                }
                if (log != null)
                {
                    log.Append(i > 0 ? "; " : string.Empty).Append(f.Mark.Street).Append(' ').Append(f.Mark.Number).Append(", ").Append(f.Mark.Total.ToString("0"))
                        .Append(", left ").Append(f.Mark.Remaining.ToString("0")).Append(" before the run: ")
                        .Append(f.Note == LoanNote.Paid ? "paid " : f.Note == LoanNote.PaidOff ? "paid off with " : "missed ").Append(f.Amount);
                }
            }
            file.Roll(day);
            CreditLedger.Touch();
            int before = CreditLedger.Score.Value;
            if (paid > 0)
            {
                CreditLedger.Post(CreditKind.PaidOnTime, paidAmount, payDay, GameLoans.AccountKey, paid);
            }
            for (int i = 0; i < _findings.Count; i++)
            {
                if (_findings[i].Note == LoanNote.PaidOff)
                {
                    CreditLedger.Post(CreditKind.PaidOff, (int)_findings[i].Mark.Total, payDay, GameLoans.AccountKey);
                }
            }
            int missEffect = missed > 0 ? CreditLedger.Post(CreditKind.Missed, missedAmount, payDay, GameLoans.AccountKey, missed) : 0;
            // The mod's own accounts take their payments after the game's loans, and each debt that was missed takes its next step.
            DeskDo("the day", () => _desk.Day(day, _findings));
            ReadLoansQuietly(day);   // (the marks were brought up to date a moment ago, so nothing of the borrower's can be passed over here)
            ReadRecord(day);
            file.SettledDay = day;
            CreditLedger.Touch();
            int score = CreditLedger.Refresh();
            if (log != null)
            {
                Logger?.Info("Economy check (day " + day + "): " + _findings.Count + " instalment(s) read " + (exact ? "against the loans as they stood before the run" : "against the last hour's reading (no hour event came before this run)")
                             + (log.Length > 0 ? ". " + log : string.Empty) + ". Rating " + before + " to " + score + ": " + (exact || _findings.Count == 0 ? "PASS" : "FAIL"));
            }
            KeepTerms("the day");
            if (missed > 0)
            {
                Notifications.Show(NotificationType.Error, "reallife_econ_note_missed", new Dictionary<string, string>
                {
                    { "amount", FinanceText.Money(missedAmount) }, { "points", Mathf.Abs(missEffect).ToString() },
                    { "score", score.ToString() }, { "grade", FinanceText.Grade(CreditLedger.Grade) }
                }, 12f, "reallife_econ_note_missed", OpenCredit, true, false);
            }
            Badge();
        }

        // The legal and public record against what the file last saw of it: what is new goes on the file.
        private void ReadRecord(int day)
        {
            CreditFile file = CreditLedger.File;
            LegalStanding now = Legal(), seen = file.LegalSeen;
            if (now.Convictions > seen.Convictions)
            {
                CreditLedger.Post(CreditKind.Conviction, 0, day, LegalSource, now.Convictions - seen.Convictions);
            }
            if (now.Judgments > seen.Judgments)
            {
                CreditLedger.Post(CreditKind.Judgment, 0, day, LegalSource, now.Judgments - seen.Judgments);
            }
            if (now.LateDays > seen.LateDays)
            {
                CreditLedger.Post(CreditKind.FineLate, now.Outstanding, day, LegalSource, now.LateDays - seen.LateDays);
            }
            else if (seen.LateDays > 0 && now.LateDays == 0 && now.Outstanding == 0)
            {
                CreditLedger.Post(CreditKind.DebtsCleared, seen.Outstanding, day, LegalSource);
            }
            file.LegalSeen = now;
        }

        // The banks' terms are what the rating says: asked each hour and after anything that moves the score.
        private void KeepTerms(string when)
        {
            int score = CreditLedger.Score.Value;
            int changed = BankTerms.Apply(score);
            if (changed > 0 && Verbose)
            {
                string detail;
                bool ok = BankTerms.Check(score, false, out detail);
                Logger?.Info("Economy check (terms, " + when + "): rating " + score + ", " + changed + " bank(s) changed. " + detail + ": " + (ok ? "PASS" : "FAIL"));
            }
        }

        private void OnGrade(CreditGrade was, CreditGrade now)
        {
            try
            {
                bool up = now > was;
                int score = CreditLedger.File != null ? CreditLedger.File.LastScore : 0;
                bool shut = score < CreditCatalogue.LendingThreshold;
                Notifications.Show(up ? NotificationType.Success : NotificationType.Warning,
                    up ? "reallife_econ_note_grade_up" : shut ? "reallife_econ_note_grade_down_shut" : "reallife_econ_note_grade_down",
                    new Dictionary<string, string>
                    {
                        { "score", score.ToString() }, { "grade", FinanceText.Grade(now) }, { "was", FinanceText.Grade(was) },
                        { "threshold", CreditCatalogue.LendingThreshold.ToString() }
                    }, 10f, null, OpenCredit, true, false);
                if (Verbose)
                {
                    Logger?.Info("Economy: the grade changed from " + was + " to " + now + " at " + score + ".");
                }
                Badge();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void OnPosted(CreditEvent e, int before, int after)
        {
            if (Verbose)
            {
                Logger?.Info("Economy: " + (e.Test ? "test " : string.Empty) + "event " + e.Kind + " (day " + e.Day + ", amount " + e.Amount + ", count " + e.Count + ", from '" + e.Source
                             + "'): rating " + before + " to " + after + ". " + Factors(CreditLedger.Score));
            }
        }

        private static string Factors(CreditScore s)
        {
            return "Factors: payments " + s.Factor[0].ToString("0.00") + " (" + s.OnTime + " on time, " + s.Missed + " missed), owed " + s.Factor[1].ToString("0.00")
                   + ", length " + s.Factor[2].ToString("0.00") + " (" + s.HistoryDays + " days), record " + s.Factor[3].ToString("0.00")
                   + ", applications " + s.Factor[4].ToString("0.00") + " (" + s.Applications + ").";
        }

        // ---------------------------------------------------------------- the bank in person

        // Under the threshold the bank's cap is 0 and the game's own dialogue says only "the most we can lend you is $0".
        // So when a bank's dialogue opens (at the counter or on a call), the reason is said once, through the game's notification.
        private void TellAtTheBank()
        {
            DialogController open = DialogController.current;
            if (open == null || open.dialog == null)
            {
                _toldDialogue = null;
                return;
            }
            if (!(open.dialog is BankDialog) || ReferenceEquals(open.dialog, _toldDialogue))
            {
                return;
            }
            _toldDialogue = open.dialog;
            BankSettings settings = open.contact != null && open.contact.Address != null ? LoanHelper.GetBankSettings(open.contact.Address) : null;
            if (settings != null && BankTerms.QuestHold(settings))
            {
                return;   // one of the game's quest loans is open here: this bank lends on its own terms
            }
            TellRefused();
            if (Verbose)
            {
                Logger?.Info("Economy: a bank's dialogue opened with the rating at " + CreditLedger.Score.Value + ", under " + CreditCatalogue.LendingThreshold + ": the reason was said (the bank's cap is 0, so the game refuses any amount).");
            }
        }

        // The game's loans read again with nothing said. Only where nothing the borrower did can be waiting to be read:
        // straight after the day's comparison has brought every mark up to date.
        private void ReadLoansQuietly(int day)
        {
            GameLoans.Read(_live);
            LoanBook.Sync(CreditLedger.File.Loans, _live, day, null, true);
        }

        /// <summary>
        /// For the credit desk: a loan it has just made gets its mark in the file now, so that the hour event does not
        /// take it for one made at the bank. Only this loan is marked: anything the borrower did at a bank since the
        /// last hour is still found, and said, at the next. Returns the mark.
        /// </summary>
        public LoanMark Adopt(Loan loan)
        {
            if (!_running || CreditLedger.File == null || Save == null || loan == null)
            {
                return null;
            }
            List<LoanMark> marks = CreditLedger.File.Loans;
            for (int i = 0; i < marks.Count; i++)
            {
                if (ReferenceEquals(marks[i].Ref, loan))
                {
                    return marks[i];
                }
            }
            var bank = loan.bankAddress;
            var mark = new LoanMark
            {
                Street = bank != null ? CreditStore.Clean(bank.streetName) : string.Empty, Number = bank != null ? bank.streetNumber : 0, Total = loan.totalAmount, FirstDay = Save.Day,
                Remaining = loan.remainingAmount, Payment = loan.dailyPayment, Interest = loan.dailyInterest, Ref = loan
            };
            marks.Add(mark);
            return mark;
        }

        /// <summary>For the credit desk: something that moves the rating or a bank's terms has changed.</summary>
        public void TermsChanged(string when)
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            CreditLedger.Touch();
            KeepTerms(when);
            Badge();
        }

        /// <summary>The refusal, with the one or two things that would raise the rating most.</summary>
        public void TellRefused()
        {
            DeskDo("a refusal", _deskRefused);   // the lender of last resort makes itself known, once
            CreditScore score = CreditLedger.Score;
            Notifications.Show(NotificationType.Warning, "reallife_econ_note_refused", new Dictionary<string, string>
            {
                { "score", score.Value.ToString() }, { "grade", FinanceText.Grade(score.Grade) },
                { "threshold", CreditCatalogue.LendingThreshold.ToString() }, { "tips", FinanceText.Tips(score, _tips) }
            }, 14f, "reallife_econ_note_refused", OpenCredit, true, false);
        }

        // ---------------------------------------------------------------- the phone

        /// <summary>
        /// A loan from the Finance app. The rating and the lender's limit are asked again at this moment; an amount
        /// that passes becomes one of the game's loans, and the application and the loan go on the credit file.
        /// </summary>
        public LoanAnswer Borrow(Lender lender, int amount, out LoanQuote quote)
        {
            quote = default(LoanQuote);
            if (!_running || _broken || Save == null)
            {
                return LoanAnswer.BelowThreshold;
            }
            try
            {
                int day = Save.Day;
                SyncLoans(day);   // anything done at a bank since the last hour is on file first
                CreditLedger.Touch();
                int score = CreditLedger.Score.Value;
                float most = PhoneLending.MostNow(lender, score);
                Loan loan;
                LoanAnswer answer = PhoneLending.Take(lender, amount, score, out quote, out loan);
                if (answer == LoanAnswer.Approved)
                {
                    GameLoans.Read(_live);
                    LoanBook.Sync(CreditLedger.File.Loans, _live, day, null, true);   // the new loan has its mark: the hour event will not take it for one made at the bank
                    CreditLedger.Post(CreditKind.Application, amount, day, PhoneSource);
                    CreditLedger.Post(CreditKind.NewCredit, amount, day, PhoneSource);
                    KeepTerms("a loan by phone");
                }
                if (Verbose)
                {
                    Logger?.Info("Economy check (application): " + amount + " from " + (lender != null ? lender.Name : "nobody") + " by phone at rating " + score + ", the most it lends now " + most.ToString("0")
                                 + (answer == LoanAnswer.Approved
                                     ? ": approved at " + quote.AnnualRate.ToString("0.#") + "% a year, " + quote.DailyPayment + " a day and " + quote.DailyInterest + " interest for " + quote.Days + " days, fee " + quote.Fee + ": PASS"
                                     : ": refused (" + answer + "): PASS"));
                }
                return answer;
            }
            catch (Exception e)
            {
                Fault("a loan by phone", e);
                return LoanAnswer.BelowThreshold;
            }
        }

        public void Open(int tab)
        {
            if (_app != null && _view != null && _running)
            {
                _view.Want(tab);
                _app.Open();
            }
        }

        private void OpenCredit()
        {
            Open(Finance.Credit);
        }

        /// <summary>The Finance app on the Standing page.</summary>
        public void OpenStanding()
        {
            if (_app != null && _view != null && _running)
            {
                _view.WantStanding();
                _app.Open();
            }
        }

        /// <summary>The Finance app on the city's page.</summary>
        public void OpenCity()
        {
            if (_app != null && _view != null && _running)
            {
                _view.WantCity();
                _app.Open();
            }
        }

        /// <summary>The app has been opened: the file is brought up to date with the game, and what the badge counted has been seen.</summary>
        public bool AppOpened()
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                SyncLoans(Save.Day);
                CreditLedger.Touch();
                KeepTerms("the app opened");
                CreditFile file = CreditLedger.File;
                file.UnseenMissed = 0;
                file.UnseenGrade = false;
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        /// <summary>
        /// For a bank app (Economy/Banks) as it opens, and after it pays a loan: the file is brought up to date with the
        /// game as when the Finance app opens, and the banks' terms are written. What the Finance app's badge counts is
        /// left for the Finance app to show.
        /// </summary>
        public bool BringUpToDate(string why)
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                SyncLoans(Save.Day);
                CreditLedger.Touch();
                KeepTerms(why);
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault(why, e);
                return false;
            }
        }

        private void Badge()
        {
            CreditFile file = CreditLedger.File;
            if (_app != null && file != null)
            {
                _app.SetBadge(_app.Showing ? 0 : file.UnseenMissed + (file.UnseenGrade ? 1 : 0));
            }
        }

        private void OnSave()
        {
            try
            {
                if (_running && Save != null && CreditLedger.File != null)
                {
                    DeskDo("the save", _deskSave);   // every loan carries its own interest and payment in the save
                    CreditStore.Save(Save.modData, CreditLedger.File);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- for the save tools and the tests

        /// <summary>For the save tools: the banks get their own terms back and the app leaves the phone; the timer starts it again if the option is on.</summary>
        public void StopAll()
        {
            try
            {
                if (_running)
                {
                    DeskDo("leaving the save", () => _desk.Leave());   // nothing of the mod's stays on a loan, and what cash allows is settled
                }
                StandDown("the save tools asked", true, true);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        /// <summary>For the save tools: the save's line has been removed, and so is what was read from it.</summary>
        public void Forget()
        {
            _running = false;
            CreditLedger.Close();
        }

        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || _broken || !_running)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        private void TestDone(string what)
        {
            KeepTerms("a test");
            Badge();
            Notifications.Show(NotificationType.Info, "reallife_econ_test_done", new Dictionary<string, string>
            {
                { "what", FinanceText.T(what) }, { "score", CreditLedger.Score.Value.ToString() }, { "grade", FinanceText.Grade(CreditLedger.Grade) }
            }, 8f, null, OpenCredit, true, false);
        }

        /// <summary>"Test: show my credit report in the log".</summary>
        public void TestReport()
        {
            if (!TestReady("Credit report"))
            {
                return;
            }
            SyncLoans(Save.Day);
            CreditLedger.Touch();
            CreditScore s = CreditLedger.Score;
            CreditFile file = CreditLedger.File;
            CreditInputs inputs = CreditLedger.Gather();
            var text = new StringBuilder(900);
            text.Append("Economy: credit report, day ").Append(Save.Day).Append(". Rating ").Append(s.Value).Append(" (").Append(s.Grade).Append("), lenders want ")
                .Append(CreditCatalogue.LendingThreshold).Append(": ").Append(s.Lends ? "they lend" : "nobody lends").Append(". ").Append(Factors(s))
                .Append(" Owed ").Append(inputs.Owed.ToString("0")).Append(" of ").Append(inputs.Limit.ToString("0")).Append(" that could be borrowed; payments a day ")
                .Append(inputs.DailyService.ToString("0")).Append(" against an income of ").Append(inputs.DailyIncome.ToString("0")).Append(". Record: ")
                .Append(inputs.Legal.Convictions).Append(" conviction(s), ").Append(inputs.Legal.Judgments).Append(" civil judgment(s), ").Append(inputs.Legal.Outstanding)
                .Append(" owed in fines and court debts, ").Append(inputs.Legal.LateDays).Append(" day(s) late, ").Append(inputs.Legal.RecentOffences).Append(" recent offence(s). File: opened day ")
                .Append(file.StartDay).Append(", first credit day ").Append(file.FirstCreditDay).Append(", ").Append(file.Events.Count).Append(" event(s), ").Append(file.Days.Count)
                .Append(" day(s) of instalments, ").Append(file.Loans.Count).Append(" loan(s), test offset ").Append(file.TestOffset).Append(". Banks: ");
            string detail;
            bool ok = BankTerms.Check(s.Value, false, out detail);
            text.Append(detail).Append(": ").Append(ok ? "PASS" : "FAIL");
            Logger?.Warn(text.ToString());   // asked for by a button: written whether or not diagnostics are on
            for (int i = Mathf.Max(0, file.Events.Count - CreditCatalogue.LedgerShown); i < file.Events.Count; i++)
            {
                CreditEvent e = file.Events[i];
                Logger?.Warn("Economy:   day " + e.Day + " " + e.Kind + " amount " + e.Amount + " count " + e.Count + " effect " + e.Effect + (e.Test ? " (test)" : string.Empty));
            }
            TestDone("reallife_econ_test_done_report");
        }

        /// <summary>"Test: record a missed payment": a test entry on the file, which "clear test entries" takes out.</summary>
        public void TestMissed()
        {
            if (TestReady("Missed payment"))
            {
                CreditLedger.Post(CreditKind.Missed, 100, Save.Day, TestSource, 1, true);
                TestDone("reallife_econ_test_done_missed");
            }
        }

        /// <summary>"Test: record 30 days of payments on time": thirty test entries, one a day back from today.</summary>
        public void TestOnTime()
        {
            if (!TestReady("Payments on time"))
            {
                return;
            }
            int today = Save.Day;
            for (int back = TestDays - 1; back >= 0; back--)
            {
                CreditLedger.File.CountPayments(today - back, 1, 0, 0, true);
            }
            CreditLedger.Touch();
            int before = CreditLedger.File.LastScore;
            int after = CreditLedger.Refresh();
            var e = new CreditEvent { Kind = CreditKind.PaidOnTime, Day = today, Amount = 0, Count = TestDays, Effect = after - before, Test = true, Source = TestSource };
            CreditLedger.File.Add(e);
            OnPosted(e, before, after);
            TestDone("reallife_econ_test_done_on_time");
        }

        /// <summary>"Test: set the rating to ...": a test entry that moves the score there.</summary>
        public void TestSet(int target)
        {
            if (TestReady("Set the rating"))
            {
                CreditLedger.TestSet(target, Save.Day);
                TestDone("reallife_econ_test_done_set");
            }
        }

        /// <summary>"Test: clear test entries".</summary>
        public void TestClear()
        {
            if (TestReady("Clear test entries"))
            {
                bool any = CreditLedger.TestClear();
                TestDone(any ? "reallife_econ_test_done_cleared" : "reallife_econ_test_done_nothing");
            }
        }

        /// <summary>"Test: restore the banks' own terms": they stay the banks' own until the next game hour, when the rating's are written again.</summary>
        public void TestRestore()
        {
            if (Save == null)
            {
                TestRefusals.Tell(Logger, "Restore the banks' terms", TestRefusal.NotLoaded);
                return;
            }
            int restored = BankTerms.Restore();
            string detail;
            bool ok = BankTerms.Check(0, true, out detail);
            Logger?.Warn("Economy check (terms restored, the test button): " + restored + " bank(s) put back. " + detail + ": " + (ok ? "PASS" : "FAIL"));
            Notifications.Show(NotificationType.Info, "reallife_econ_test_restored", null, 8f, null, null, true, false);
        }
    }
}
