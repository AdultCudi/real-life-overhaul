using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One debt, whichever kind it is: an account of the mod's, or one of the game's loans with its mark in the credit file.</summary>
    public struct Debt
    {
        public CreditAccount Account;
        public LoanMark Mark;

        public Debt(CreditAccount account)
        {
            Account = account;
            Mark = null;
        }

        public Debt(LoanMark mark)
        {
            Account = null;
            Mark = mark;
        }

        public bool Valid => Account != null ? !Account.Closed : Mark != null && Mark.Ref is Loan;
        public Loan Loan => Mark != null ? Mark.Ref as Loan : null;
        public LadderState Ladder => Account != null ? Account.Ladder : Mark.Ladder;
        public int Owed => Account != null ? Account.Owed : Loan != null ? Mathf.CeilToInt(Mathf.Max(0f, Loan.remainingAmount)) : 0;
        public string Key => Account != null ? Account.Key : GameLoans.AccountKey;
        public string Name => Account != null ? CreditDesk.Name(Account) : FinanceText.T("reallife_econ_debt_loan", "lender", Lender);
        public string Lender => Account != null ? CreditDesk.LenderName(Account) : GameLoans.BankName(Loan);
    }

    /// <summary>
    /// The credit desk: the mod's own accounts, what lenders do when payments are missed, refinancing, the lender of
    /// last resort and the ways out of debt, on top of the credit rating of <see cref="EconomyDirector"/>. A plain
    /// object the director owns and calls: on the game's hour (before the night's run: the overdraft covers a short
    /// day, what is held on the game's loans is written), on the game's day (after its own loans: each account's
    /// payment, each debt's next step), once a second only for what has to be noticed between the two (a purchase on
    /// finance, a vehicle to be taken once it stands out of sight), when the game saves, and when the Finance app or
    /// a phone conversation asks for something. Nothing runs per frame.
    ///
    /// The rules are the game-free classes beside it (<see cref="AccountRules"/>, <see cref="LadderRules"/>,
    /// <see cref="LoanHolds"/>, <see cref="SharkRules"/>, <see cref="ReliefRules"/>); this class is their game side:
    /// money through <see cref="Wallet"/>, the game's loans, vehicles and buildings, the game's notifications.
    /// Other features are reached only through <see cref="EconomyBridges"/>.
    /// </summary>
    public sealed partial class CreditDesk
    {
        public const string AccountsSource = "accounts";
        private const float RepossessionKeepAway = 30f;   // metres: a vehicle is not taken with the player nearer than this

        private readonly EconomyDirector _director;
        private readonly Wallet _wallet = new Wallet();
        private readonly HashSet<string> _owned = new HashSet<string>();
        private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>(8);
        private readonly Action _openAccounts, _openCollections, _openAdvice, _openLender;
        private CreditFile _file;
        private bool _afterSave;       // the loans' own figures were written back for a save: what is held is written again
        private bool _repoWaiting;     // a vehicle is to be taken as soon as it stands out of sight
        private int _ownedCount = -1;
        private bool _claimedToday;

        public CreditDesk(EconomyDirector director)
        {
            _director = director;
            _openAccounts = () => _director.Open(Finance.Accounts);
            _openCollections = () => DebtPhone.Open(DebtPhone.Collections);
            _openAdvice = () => DebtPhone.Open(DebtPhone.Advice);
            _openLender = () => DebtPhone.Open(DebtPhone.Lender);
        }

        public bool Running => _file != null;
        public CreditFile File => _file;
        public Wallet Wallet => _wallet;

        /// <summary>How hard not paying hits, 0 to 3 (the option's four steps).</summary>
        public static int Level => ProductCatalogue.Level(Settings.EconomyLevel);

        private static IModLogger Logger => EconomyDirector.Logger;
        private static bool Verbose => Meta.Dev.Verbose;
        private static GameInstance Save => SaveGameManager.Current;
        private static float Multiplier => Save != null && Save.gameVariables != null ? Save.gameVariables.bankInterestMultiplier : 1f;
        private static int Today => Save != null ? Save.Day : 0;

        // ---------------------------------------------------------------- names

        /// <summary>An account's name for the player: its kind, and what it is secured on if anything.</summary>
        public static string Name(CreditAccount a)
        {
            string kind = FinanceText.T(a.NameKey);
            string name = a.SecurityName.Length > 0 ? FinanceText.T("reallife_econ_account_named", "kind", kind, "name", Thing(a)) : kind;
            return a.Test ? FinanceText.T("reallife_econ_account_test", "name", name) : name;
        }

        /// <summary>The thing an account is, or was, secured on: the vehicle's name as the game writes it, or the building's address.</summary>
        public static string Thing(CreditAccount a)
        {
            return a.Kind == ProductKind.CarFinance && LocalizorManager.IsLocalizedKey(a.SecurityName) ? a.SecurityName.GetLocalization() : a.SecurityName;
        }

        public static string LenderName(CreditAccount a)
        {
            Lender lender = BankTerms.At(a.Street, a.Number);
            return lender != null ? lender.Name : FinanceText.T("reallife_econ_lender_bank");
        }

        private void Note(NotificationType type, string key, Dictionary<string, string> data, Action click = null, float seconds = 12f)
        {
            Notifications.Show(type, key, data, seconds, null, click ?? _openAccounts, true, false);
        }

        private static Dictionary<string, string> Data(string a, string av, string b = null, string bv = null, string c = null, string cv = null, string d = null, string dv = null)
        {
            // (a dictionary each: the game keeps hold of a notification's data for as long as it lists the notification)
            var data = new Dictionary<string, string>(4) { { a, av ?? string.Empty } };
            if (b != null)
            {
                data[b] = bv ?? string.Empty;
            }
            if (c != null)
            {
                data[c] = cv ?? string.Empty;
            }
            if (d != null)
            {
                data[d] = dv ?? string.Empty;
            }
            return data;
        }

        private static void Check(string what, bool ok = true)
        {
            if (Verbose)
            {
                Logger?.Info("Economy check (" + what + "): " + (ok ? "PASS" : "FAIL"));
            }
        }

        // ---------------------------------------------------------------- starting and stopping

        /// <summary>The file is open and the game's loans have found their marks: the accounts join the rating, and what is held on a loan is written.</summary>
        public void Start(CreditFile file, int today)
        {
            _file = file;
            AccountRules.DaysPerYear = Save != null && Save.gameVariables != null ? Mathf.Max(1, Save.gameVariables.daysPerYear) : 60;
            BankTerms.Secured = SecuredAt;
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                if (!file.Accounts[i].Closed && !file.Accounts[i].Test)
                {
                    CreditLedger.Register(file.Accounts[i]);   // (a test account is never part of the rating)
                }
            }
            SelfCheck(today);
            ApplyHolds("the start");
            RememberOwned();
            DebtPhone.Setup(this);
        }

        /// <summary>
        /// The economy stops (the option, the city unloading, a fault): every one of the game's loans has its own
        /// figures again. What was held stays noted in the file, so it is written again if the economy starts again.
        /// </summary>
        public void Stop(string why)
        {
            if (_file == null)
            {
                return;
            }
            try
            {
                RestoreHolds(why);
            }
            finally
            {
                _file = null;
                _afterSave = false;
                _repoWaiting = false;
                BankTerms.Secured = null;
                DebtPhone.Forget();
            }
        }

        // At load: every loan with something held has its own figures remembered and is still there; every security is still there.
        private void SelfCheck(int today)
        {
            int held = 0, unsound = 0, orphans = 0, unsecured = 0;
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                if (m.Held != LoanHold.None)
                {
                    held++;
                    if (!LoanHolds.Sound(m))
                    {
                        // Nothing to write back: the loan keeps the figures it has, which a save always holds as the game's own.
                        unsound++;
                        m.Held = LoanHold.None;
                        m.Ladder.Penalty = false;
                    }
                }
                if (m.Purpose == LoanPurpose.Business && m.Security.Length > 0 && Books.Business(m.Security) == null)
                {
                    orphans++;
                    m.Security = string.Empty;
                    m.Ladder.RepoDay = 0;
                }
            }
            _repoWaiting = false;
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed)
                {
                    continue;
                }
                if (a.Secured && !SecurityThere(a))
                {
                    unsecured++;
                    AccountRules.Unsecure(a);
                }
                _repoWaiting |= a.Ladder.RepoPending;
            }
            bool arranged = _file.Relief.Arranged;
            if (Verbose || unsound + orphans + unsecured > 0)
            {
                Logger?.Info("Economy check (accounts at load, day " + today + "): " + _file.Accounts.Count + " account(s), " + held + " loan(s) with something held ("
                             + unsound + " without its own figures: cleared), " + orphans + " business loan(s) whose business has gone (now unsecured), " + unsecured
                             + " account(s) whose security has gone (now unsecured)" + (arranged ? ", a debt arrangement running" : string.Empty)
                             + (_file.Shark.Active ? ", " + _file.Shark.Owed + " owed to the private lender" : string.Empty) + ": " + (unsound == 0 ? "PASS" : "FAIL"));
            }
        }

        private static bool SecurityThere(CreditAccount a)
        {
            return a.Kind == ProductKind.Mortgage ? Books.Estate(a.Security) != null : Books.Vehicle(a.Security) != null;
        }

        // ---------------------------------------------------------------- what is held on the game's loans

        // What is owed on loans secured on a business at the banks that share these settings (BankTerms.Secured).
        private float SecuredAt(SpecialServices.Bank.BankSettings settings)
        {
            float sum = 0f;
            for (int i = 0; _file != null && i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                if (m.Purpose != LoanPurpose.Business || m.Security.Length == 0)
                {
                    continue;
                }
                Lender lender = BankTerms.At(m.Street, m.Number);
                if (lender != null && lender.Settings == settings)
                {
                    sum += Mathf.Max(0f, m.Remaining);
                }
            }
            return sum;
        }

        /// <summary>Writes what is held onto the game's loans in memory. Only a figure that differs is written. Returns how many loans were written to.</summary>
        public int ApplyHolds(string when)
        {
            _afterSave = false;
            int written = 0;
            for (int i = 0; _file != null && i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                var loan = m.Ref as Loan;
                if (m.Held == LoanHold.None || loan == null)
                {
                    continue;
                }
                int interest = loan.dailyInterest, payment = loan.dailyPayment;
                if (LoanHolds.Apply(m, ref interest, ref payment, Multiplier, AccountRules.DaysPerYear))
                {
                    Rate(m, loan, interest, payment, when);
                    written++;
                }
            }
            return written;
        }

        /// <summary>Writes every loan's own figures back. Returns how many loans were written to.</summary>
        public int RestoreHolds(string when)
        {
            int written = 0;
            for (int i = 0; _file != null && i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                var loan = m.Ref as Loan;
                if (m.Held == LoanHold.None || loan == null)
                {
                    continue;
                }
                int interest = loan.dailyInterest, payment = loan.dailyPayment;
                if (LoanHolds.Restore(m, ref interest, ref payment, Multiplier, AccountRules.DaysPerYear))
                {
                    Rate(m, loan, interest, payment, when);
                    written++;
                }
            }
            return written;
        }

        private static void Rate(LoanMark m, Loan loan, int interest, int payment, string when)
        {
            if (Verbose)
            {
                Logger?.Info("Economy check (rate, " + when + "): the loan of " + m.Total.ToString("0") + " at " + m.Street + " " + m.Number + ": daily interest " + loan.dailyInterest + " to " + interest
                             + ", daily payment " + loan.dailyPayment + " to " + payment + "; its own " + m.OwnInterest + " and " + m.OwnPayment + " are remembered, held "
                             + ((m.Held & LoanHold.Frozen) != 0 ? "in an arrangement" : "at the penalty rate") + ": " + (LoanHolds.Sound(m) ? "PASS" : "FAIL"));
            }
            loan.dailyInterest = interest;
            loan.dailyPayment = payment;
        }

        private void BeginHold(LoanMark m, Loan loan, int hold, string when)
        {
            int interest = loan.dailyInterest, payment = loan.dailyPayment;
            LoanHolds.Begin(m, hold, ref interest, ref payment, Multiplier, AccountRules.DaysPerYear);
            LoanHolds.Apply(m, ref interest, ref payment, Multiplier, AccountRules.DaysPerYear);
            Rate(m, loan, interest, payment, when);
            m.Interest = interest;
            m.Payment = payment;
        }

        private void EndHold(LoanMark m, int hold, string when)
        {
            var loan = m.Ref as Loan;
            if ((m.Held & hold) == 0)
            {
                return;
            }
            if (loan == null)
            {
                m.Held &= ~hold;
                return;
            }
            int interest = loan.dailyInterest, payment = loan.dailyPayment;
            LoanHolds.End(m, hold, ref interest, ref payment, Multiplier, AccountRules.DaysPerYear);
            Rate(m, loan, interest, payment, when);
            m.Interest = interest;      // the file's own reading of the loan moves with it: the change is the mod's, not the borrower's
            m.Payment = payment;
        }

        // ---------------------------------------------------------------- the hour, the second, the save

        /// <summary>Every game hour, before the director reads the loans: what is held is in place (the night's run may follow at once).</summary>
        public void KeepHolds()
        {
            if (_file == null)
            {
                return;
            }
            if (!Settings.EconomyConsequences)
            {
                LiftPenalties();
            }
            ApplyHolds("the hour");
        }

        // "Consequences of not paying" is off: no debt is at a penalty rate and no step is waiting.
        private void LiftPenalties()
        {
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                if ((m.Held & LoanHold.Penalty) != 0)
                {
                    EndHold(m, LoanHold.Penalty, "the option is off");
                }
                Calm(m.Ladder);
            }
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                Calm(_file.Accounts[i].Ladder);
            }
            _repoWaiting = false;
        }

        private static void Calm(LadderState s)
        {
            int claim = s.ClaimNumber, repo = s.RepoPending ? 0 : s.RepoDay;
            bool planUsed = s.PlanUsed;
            if (s.Misses == 0 && !s.Penalty && !s.CallDue && !s.RepoPending && s.PlanLeft == 0)
            {
                return;
            }
            s.Clear();
            s.ClaimNumber = claim;
            s.RepoDay = repo;
            s.PlanUsed = planUsed;
        }

        /// <summary>
        /// The first hour event of a new day, before the game's run: if the cash is short of what leaves tonight, the
        /// overdraft pays the shortfall, up to what is free of its limit.
        /// </summary>
        public void BeforeRun(int day)
        {
            if (_file == null)
            {
                return;
            }
            CreditAccount overdraft = _file.Account(ProductKind.Overdraft);
            CreditRefusal refusal;
            if (overdraft == null || overdraft.Frozen || overdraft.Free <= 0 || !Settings.EconomyProducts || !CreditLedger.CanBorrow(out refusal))
            {
                return;
            }
            Obligations due = Books.Tomorrow(_file, day - 1);   // (the game has moved its day on already: tonight's run is yesterday's "tomorrow")
            int cash = _wallet.Cash;
            int shortfall = Mathf.CeilToInt(due.Total) - cash;
            int paid = AccountRules.Cover(overdraft, shortfall);
            if (paid <= 0)
            {
                return;
            }
            string name = Name(overdraft);
            _wallet.Give(paid, Money.Cover, name);
            Touch();
            Note(NotificationType.Info, "reallife_econ_note_cover", Data("amount", FinanceText.Money(paid), "owed", FinanceText.Money(overdraft.Owed), "limit", FinanceText.Money(overdraft.Agreed)));
            if (Verbose)
            {
                Logger?.Info("Economy check (overdraft, day " + day + "): " + Mathf.CeilToInt(due.Total) + " leaves tonight (loans " + due.Loans.ToString("0") + ", accounts " + due.Accounts.ToString("0")
                             + ", fines " + due.Other.ToString("0") + ", rent " + due.Rent.ToString("0") + ", wages " + due.Wages.ToString("0") + ") against " + cash + " in cash: the overdraft paid " + paid
                             + ", " + overdraft.Owed + " of " + overdraft.Agreed + " drawn: " + (paid <= shortfall && overdraft.Owed <= overdraft.Agreed ? "PASS" : "FAIL"));
            }
        }

        /// <summary>Every game hour, after the loans are read: a security that has gone, a vehicle to be taken, a purchase on finance, what the private lender is waiting to do.</summary>
        public void Hour(int day)
        {
            if (_file == null)
            {
                return;
            }
            Securities(day);
            Purchases(day);
            if (_repoWaiting)
            {
                TakeVehicles(day);
            }
            SharkHour(day);
        }

        /// <summary>Once a second: only what cannot wait for the hour, and only while there is such a thing.</summary>
        public void Slow()
        {
            if (_file == null)
            {
                return;
            }
            if (_afterSave)
            {
                ApplyHolds("after the save");
            }
            if (_file.Relief.HasApproval)
            {
                Purchases(Today);
            }
            if (_repoWaiting)
            {
                TakeVehicles(Today);
            }
        }

        /// <summary>The game is about to write a save: every loan has its own figures in it, so the save loads in the plain game as it would without the mod.</summary>
        public void BeforeSave()
        {
            if (_file != null && RestoreHolds("the game is saving") > 0)
            {
                _afterSave = true;
            }
        }

        private void Touch()
        {
            CreditLedger.Touch();
        }

        private void Opened(CreditAccount a)
        {
            _file.Add(a);
            if (!a.Test)
            {
                CreditLedger.Register(a);
                if (_file.FirstCreditDay <= 0)
                {
                    _file.FirstCreditDay = Mathf.Max(1, Today);   // holding credit is where a history begins
                }
            }
            Touch();
            if (Verbose)
            {
                Logger?.Info("Economy check (account opened): " + a.Kind + " #" + a.Id + " with " + LenderName(a) + ", " + a.Owed + " owed of " + a.Agreed + " at " + (a.RateBp / 100f).ToString("0.##")
                             + "% a year" + (a.Term > 0 ? ", " + a.Instalment + " a day for " + a.Term + " days" : string.Empty) + (a.Secured ? ", secured on '" + a.SecurityName + "' (" + a.Security + ") bought for " + a.SecurityValue : string.Empty)
                             + (a.Test ? " (test)" : string.Empty) + ": PASS");
            }
        }

        private void Closed(CreditAccount a)
        {
            a.Closed = true;
            CreditLedger.Unregister(a);
            Touch();
        }

        // ---------------------------------------------------------------- the day

        /// <summary>
        /// The game's day event, after its own run and after the director has read what became of each of the game's
        /// loans: the next step for a loan that was missed, each account's payment, takings and rents that go to a
        /// lender, the private lender, the ways out of debt. The mod's payments come after the game's own, always in
        /// this order, so what the cash is spent on first never changes.
        /// </summary>
        public void Day(int day, List<LoanFinding> findings)
        {
            if (_file == null)
            {
                return;
            }
            int level = Level;
            bool consequences = Settings.EconomyConsequences;
            _claimedToday = false;
            _wallet.BookDay = day - 1;   // booked on the day that has ended, as the game books its own run
            try
            {
                if (_file.Relief.Arranged)
                {
                    ArrangementDay(day);
                }
                LoansDay(day, findings, level, consequences);
                AccountsDay(day, level, consequences);
                Diversions(day);
                SharkDay(day, level);
                ReliefDay(day);
            }
            finally
            {
                _wallet.BookDay = null;
            }
            _file.Trim();
            Touch();
        }

        // A line on the credit file about a debt. One about a test account is a test entry, which "clear test entries" takes out.
        private static void Post(Debt debt, CreditKind kind, int amount, int day)
        {
            CreditLedger.Post(kind, amount, day, debt.Key, 1, debt.Account != null && debt.Account.Test);
        }

        // The game's loans: a payment made puts a loan back in order (and takes its plan's part); one missed takes the next step.
        private void LoansDay(int day, List<LoanFinding> findings, int level, bool consequences)
        {
            for (int i = 0; i < findings.Count; i++)
            {
                LoanFinding f = findings[i];
                LoanMark m = f.Live;
                var loan = m != null ? m.Ref as Loan : null;
                if (loan == null || (m.Held & LoanHold.Frozen) != 0 || (f.Note != LoanNote.Paid && f.Note != LoanNote.Missed))
                {
                    continue;
                }
                var debt = new Debt(m);
                if (f.Note == LoanNote.Missed)
                {
                    if (consequences)
                    {
                        Step(debt, LadderRules.Missed(m.Ladder, level, SecuredNow(m), m.Ladder.Overdue + f.Amount), f.Amount, 0, day, level);
                    }
                    continue;
                }
                string name = debt.Name;   // (taken now: a loan the plan's last part pays off leaves the game's list)
                int part = Mathf.Min(Mathf.Min(m.Ladder.PlanPerDay, m.Ladder.PlanLeft), debt.Owed);
                if (m.Ladder.PlanLeft > 0 && part > 0 && !_wallet.Take(part, Money.Payment, name))
                {
                    if (consequences)
                    {
                        Step(debt, LadderRules.Missed(m.Ladder, level, SecuredNow(m), m.Ladder.Overdue), part, 0, day, level);   // the plan is broken
                    }
                    continue;
                }
                if (m.Ladder.PlanLeft > 0)
                {
                    m.Ladder.PlanLeft = Mathf.Max(0, m.Ladder.PlanLeft - part);
                    m.Ladder.Overdue = Mathf.Max(0, m.Ladder.Overdue - part);
                    PayDown(m, loan, part, day);
                }
                if (LadderRules.Paid(m.Ladder))
                {
                    EndHold(m, LoanHold.Penalty, "three payments in a row");
                    Note(NotificationType.Success, "reallife_econ_note_penalty_over", Data("account", name));
                }
            }
        }

        private bool SecuredNow(LoanMark m)
        {
            return m.Purpose == LoanPurpose.Business && m.Security.Length > 0 && m.Ladder.RepoDay == 0 && Books.Business(m.Security) != null;
        }

        // Money off one of the game's loans outside its own run. The file's reading of the loan moves with it, so the
        // rating does not take it for a part paid early by the borrower. A loan with nothing left is removed, as the game removes one.
        private void PayDown(LoanMark m, Loan loan, int amount, int day)
        {
            if (amount <= 0)
            {
                return;
            }
            loan.remainingAmount = Mathf.Max(0f, loan.remainingAmount - amount);
            m.Remaining = loan.remainingAmount;
            if (loan.remainingAmount <= 0.5f)
            {
                RemoveLoan(m, loan);
                CreditLedger.Post(CreditKind.PaidOff, (int)m.Total, day, GameLoans.AccountKey);
            }
        }

        // One of the game's loans leaves the game's list for good (paid by the mod, written off, passed to the court).
        // Nothing else of the game's refers to a loan: the list is all there is (EconoView and the night's run read it).
        private void RemoveLoan(LoanMark m, Loan loan)
        {
            if (Save != null)
            {
                Save.Loans.Remove(loan);
            }
            _file.Loans.Remove(m);
            m.Held = LoanHold.None;
            m.Ref = null;
        }

        // The mod's own accounts, in the order they were opened.
        private void AccountsDay(int day, int level, bool consequences)
        {
            int payDay = day - 1, paid = 0, paidAmount = 0, missed = 0, missedAmount = 0;
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed || a.Test || (a.Owed == 0 && a.Ladder.RepoPending))
                {
                    continue;   // (a test account moves only when a test button moves it; a vehicle written off in a bankruptcy only waits to be taken back)
                }
                int before = a.Owed;
                string name = Name(a);
                DayOutcome o = AccountRules.RunDay(a, day, _wallet, level, consequences, name);
                if (o.WasDue && o.Missed > 0)
                {
                    missed++;
                    missedAmount += o.Missed;
                }
                else if (o.WasDue && o.Paid > 0)
                {
                    paid++;
                    paidAmount += o.Paid;
                }
                if (Verbose && (o.WasDue || o.Paid > 0 || o.Interest > 0 || o.Statement))
                {
                    Logger?.Info("Economy check (account, day " + day + "): " + a.Kind + " #" + a.Id + ": " + before + " owed, interest " + o.Interest + ", "
                                 + (o.Missed > 0 ? "a payment of " + o.Missed + " missed (" + (consequences ? a.Ladder.Misses + " in a row" : "consequences are off") + ")" : "paid " + o.Paid)
                                 + (o.Fee > 0 ? ", late fee " + o.Fee : string.Empty) + (o.Statement ? ", statement of " + a.StatementLeft + " with a minimum of " + a.MinimumLeft + " due day " + a.DueDay : string.Empty)
                                 + ", now " + a.Owed + " owed" + (a.Closed ? ", paid off" : string.Empty) + ": " + (a.Owed == Expected(a, before, o) ? "PASS" : "FAIL"));
                }
                if (o.PenaltyEnded)
                {
                    Note(NotificationType.Success, "reallife_econ_note_penalty_over", Data("account", name));
                    Check("rate restored: " + a.Kind + " #" + a.Id + " back at " + (a.RateNow / 100f).ToString("0.##") + "% after three payments in a row", a.RateNow == a.RateAgreedNow);
                }
                if (o.Step != LadderStep.None)
                {
                    Step(new Debt(a), o.Step, o.Missed, o.Fee, day, level);
                }
                if (o.Statement)
                {
                    Note(NotificationType.Info, "reallife_econ_note_statement", Data("amount", FinanceText.Money(a.StatementLeft), "minimum", FinanceText.Money(a.MinimumLeft), "day", a.DueDay.ToString()));
                }
                if (o.Closed)
                {
                    CreditLedger.Unregister(a);
                    CreditLedger.Post(CreditKind.PaidOff, a.Agreed, payDay, a.Key);
                    Note(NotificationType.Success, "reallife_econ_note_paid_off", Data("account", name));
                }
                else if (consequences && !a.Secured && LadderRules.UnsecuredClaimDue(a.Ladder, a.Owed, day, level))
                {
                    Claim(new Debt(a), day);
                }
            }
            if (paid > 0)
            {
                CreditLedger.Post(CreditKind.PaidOnTime, paidAmount, payDay, AccountsSource, paid);
            }
            if (missed > 0)
            {
                int effect = CreditLedger.Post(CreditKind.Missed, missedAmount, payDay, AccountsSource, missed);
                Note(NotificationType.Error, "reallife_econ_note_missed_account", Data("amount", FinanceText.Money(missedAmount), "points", Mathf.Abs(effect).ToString(),
                    "score", CreditLedger.Value.ToString(), "grade", FinanceText.Grade(CreditLedger.Grade)));
            }
        }

        // What an account should owe after its day: an instalment account pays the day's interest with the payment
        // (it is added to the balance only when the payment is missed); the others add it to the balance.
        private static int Expected(CreditAccount a, int before, in DayOutcome o)
        {
            bool withPayment = ProductCatalogue.Instalment(a.Kind) && o.Missed == 0;
            return before + (withPayment ? 0 : o.Interest) + o.Fee - (o.Paid - (withPayment ? o.Interest : 0));
        }

        // ---------------------------------------------------------------- the steps

        // One step of the ladder for one debt: what the rules decided, done.
        private void Step(Debt debt, LadderStep step, int missed, int feeAdded, int day, int level)
        {
            if (step == LadderStep.None)
            {
                return;
            }
            string name = debt.Name, lender = debt.Lender;
            LadderState s = debt.Ladder;
            string detail = string.Empty;
            switch (step)
            {
                case LadderStep.Reminder:
                    break;   // the notice that the payment was missed is the reminder (it names the amount and what it did to the rating)
                case LadderStep.LateFee:
                    {
                        int fee = feeAdded;
                        if (debt.Account == null)
                        {
                            // One of the game's loans: the fee is taken from cash if it is there, else it waits on an account of its own.
                            fee = AccountRules.LateFee(missed, level);
                            if (!_wallet.Take(fee, Money.LateFee, name))
                            {
                                Arrears(debt.Mark, fee, day);
                                detail = " (not in cash: added to the late fees owed)";
                            }
                        }
                        Post(debt, CreditKind.LateFee, fee, day);
                        Note(NotificationType.Warning, "reallife_econ_note_late_fee", Data("lender", lender, "account", name, "amount", FinanceText.Money(fee)));
                        detail = " of " + fee + detail;
                    }
                    break;
                case LadderStep.PenaltyRate:
                    if (debt.Account == null && debt.Loan != null && (debt.Mark.Held & LoanHold.Penalty) == 0)
                    {
                        BeginHold(debt.Mark, debt.Loan, LoanHold.Penalty, "the penalty rate");
                    }
                    Post(debt, CreditKind.PenaltyRate, 0, day);
                    Note(NotificationType.Warning, "reallife_econ_note_penalty", Data("lender", lender, "account", name, "points", ProductCatalogue.PenaltyPoints.ToString(), "run", ProductCatalogue.PenaltyClearRun.ToString()));
                    break;
                case LadderStep.Collections:
                    if (debt.Account == null && debt.Loan != null && (debt.Mark.Held & LoanHold.Penalty) == 0 && s.Penalty)
                    {
                        BeginHold(debt.Mark, debt.Loan, LoanHold.Penalty, "the penalty rate");   // (a count that passed the third miss by)
                    }
                    _file.Relief.CollectorKnown = true;
                    DebtPhone.Setup(this);
                    Post(debt, CreditKind.Collections, s.Overdue, day);
                    Note(NotificationType.Error, "reallife_econ_note_collections", Data("lender", lender, "account", name, "amount", FinanceText.Money(s.Overdue)), _openCollections, 16f);
                    break;
                case LadderStep.Repossession:
                    Repossession(debt, day);
                    break;
                case LadderStep.Claim:
                    Claim(debt, day);
                    break;
            }
            if (Verbose)
            {
                bool secured = debt.Account != null ? debt.Account.Secured : debt.Mark.Security.Length > 0;
                Logger?.Info("Economy check (step, day " + day + "): " + (debt.Account != null ? debt.Account.Kind + " #" + debt.Account.Id : "the loan at " + debt.Mark.Street + " " + debt.Mark.Number)
                             + ": " + s.Misses + " payment(s) missed in a row on level " + level + " (" + (secured ? "secured" : "unsecured") + "), " + s.Overdue + " overdue: " + step + detail + ": "
                             + (LadderRules.Takes(step, level, true) ? "PASS" : "FAIL"));
            }
        }

        // Late fees on one of the game's loans that were not in cash: one account holds them all, with the loan's bank.
        private void Arrears(LoanMark m, int fee, int day)
        {
            CreditAccount a = _file.Account(ProductKind.Arrears);
            if (a == null)
            {
                a = AccountRules.Open(ProductKind.Arrears, 0, 0, day);
                a.Street = m.Street;
                a.Number = m.Number;
                Opened(a);
            }
            a.Owed += fee;
            a.Agreed = Mathf.Max(a.Agreed, a.Owed);
        }

        // The fifth step, by what the debt is secured on: a vehicle is taken once it stands out of sight; a business's
        // takings or a building's rents go to the lender until the arrears are cleared. Nothing else is taken from the borrower.
        private void Repossession(Debt debt, int day)
        {
            LadderState s = debt.Ladder;
            if (debt.Account != null && debt.Account.Kind == ProductKind.CarFinance)
            {
                s.RepoPending = true;
                _repoWaiting = true;
                Note(NotificationType.Error, "reallife_econ_note_repo_due", Data("lender", debt.Lender, "thing", Thing(debt.Account)), null, 16f);
                return;
            }
            s.RepoDay = day;
            Post(debt, CreditKind.Repossessed, s.Overdue, day);
            if (debt.Account != null)
            {
                Note(NotificationType.Error, "reallife_econ_note_rents_taken", Data("lender", debt.Lender, "thing", Thing(debt.Account), "amount", FinanceText.Money(s.Overdue)), null, 16f);
                return;
            }
            BuildingRegistration shop = Books.Business(debt.Mark.Security);
            Note(NotificationType.Error, "reallife_econ_note_takings_taken", Data("lender", debt.Lender, "business", shop != null ? shop.BusinessName : string.Empty,
                "share", Mathf.RoundToInt(ProductCatalogue.BusinessDivertShare * 100f) + "%", "amount", FinanceText.Money(s.Overdue)), null, 16f);
        }

        // The last step: the lender takes the balance and its costs to court, and the debt is the court's from then on.
        private void Claim(Debt debt, int day)
        {
            int owed = debt.Owed;
            if (owed <= 0 || debt.Ladder.ClaimNumber != 0)
            {
                return;
            }
            int costs = LadderRules.ClaimCosts(owed);
            string name = debt.Name, lender = debt.Lender, key = debt.Key;
            if (debt.Account != null && debt.Account.Test)
            {
                Logger?.Info("Economy check (claim, day " + day + "): " + name + ": a claim for " + owed + " and " + costs + " in costs would be filed here; nothing is filed for a test account: PASS");
                return;
            }
            int number = EconomyBridges.FileClaim != null ? EconomyBridges.FileClaim(owed + costs) : 0;
            if (number <= 0)
            {
                if (Verbose)
                {
                    Logger?.Info("Economy check (claim, day " + day + "): " + name + ": " + owed + " would go to court, but there is no court to file with (its feature is off): the debt stays as it is: PASS");
                }
                return;
            }
            debt.Ladder.ClaimNumber = number;
            if (_file.Relief.Claims.Count < Relief.ClaimsMost)
            {
                _file.Relief.Claims.Add(number);
            }
            _claimedToday = true;
            if (debt.Account != null)
            {
                debt.Account.Owed = 0;
                AccountRules.Unsecure(debt.Account);
                Closed(debt.Account);
            }
            else
            {
                RemoveLoan(debt.Mark, debt.Loan);
            }
            CreditLedger.Post(CreditKind.ClaimFiled, owed + costs, day, key);
            Note(NotificationType.Error, "reallife_econ_note_claim", Data("lender", lender, "account", name, "amount", FinanceText.Money(owed + costs), "costs", FinanceText.Money(costs)), null, 18f);
            if (Verbose)
            {
                Logger?.Info("Economy check (claim, day " + day + "): " + name + ": " + owed + " and " + costs + " in costs filed as civil claim " + number + "; the debt is closed here and is the court's now: PASS");
            }
        }

        // ---------------------------------------------------------------- takings and rents that go to a lender

        private void Diversions(int day)
        {
            for (int i = _file.Loans.Count - 1; i >= 0; i--)
            {
                LoanMark m = _file.Loans[i];
                var loan = m.Ref as Loan;
                if (m.Ladder.RepoDay <= 0 || loan == null || (m.Held & LoanHold.Frozen) != 0)
                {
                    continue;
                }
                BuildingRegistration shop = Books.Business(m.Security);
                if (shop == null || m.Ladder.Overdue <= 0)
                {
                    m.Ladder.RepoDay = 0;   // the arrears are cleared, or the business has gone
                    continue;
                }
                int want = Mathf.Min(Mathf.Min(Mathf.FloorToInt(Books.SalesYesterday(shop.Address) * ProductCatalogue.BusinessDivertShare), m.Ladder.Overdue), Mathf.CeilToInt(loan.remainingAmount));
                int got = _wallet.TakeUpTo(want, Money.Diverted, shop.BusinessName);
                m.Ladder.Overdue -= got;
                PayDown(m, loan, got, day);
                if (Verbose)
                {
                    Logger?.Info("Economy check (takings, day " + day + "): " + shop.BusinessName + " took " + Books.SalesYesterday(shop.Address).ToString("0") + " yesterday: " + got + " of " + want
                                 + " went to the lender, " + m.Ladder.Overdue + " still overdue: " + (got <= want ? "PASS" : "FAIL"));
                }
                if (m.Ladder.Overdue <= 0)
                {
                    m.Ladder.RepoDay = 0;
                    if (LadderRules.Paid(m.Ladder))
                    {
                        EndHold(m, LoanHold.Penalty, "three payments in a row");
                    }
                    Note(NotificationType.Success, "reallife_econ_note_takings_over", Data("business", shop.BusinessName));
                }
            }
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed || a.Kind != ProductKind.Mortgage || a.Ladder.RepoDay <= 0)
                {
                    continue;
                }
                Entities.RealEstate estate = Books.Estate(a.Security);
                if (estate == null || a.Ladder.Overdue <= 0)
                {
                    a.Ladder.RepoDay = 0;
                    continue;
                }
                int want = Mathf.Min(Mathf.Min(Mathf.FloorToInt(estate.DailyIncome * ProductCatalogue.RentDivertShare), a.Ladder.Overdue), a.Owed);
                int got = _wallet.TakeUpTo(want, Money.Diverted, Thing(a));
                AccountRules.Pay(a, got);
                if (a.Ladder.Overdue <= 0)
                {
                    a.Ladder.RepoDay = 0;
                    Note(NotificationType.Success, "reallife_econ_note_rents_over", Data("thing", Thing(a)));
                }
                if (a.Closed)
                {
                    CreditLedger.Unregister(a);
                }
            }
        }
    }
}
