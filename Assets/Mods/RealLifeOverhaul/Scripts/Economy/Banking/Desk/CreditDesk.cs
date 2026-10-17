using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Localizor;
using SpecialServices.Bank;
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
    /// The credit desk: the game side of the mod's own accounts, missed payments, the private lender and the ways out of debt,
    /// whose rules are game-free classes. <see cref="EconomyDirector"/> owns it and calls it on the game's hour, day and save
    /// and when an app asks; once a second only while a purchase or a repossession waits. Cost: nothing per frame.
    /// </summary>
    public sealed partial class CreditDesk
    {
        public const string AccountsSource = "accounts";
        private const float RepossessionKeepAway = 30f;   // metres: no vehicle is taken with the player nearer than this

        private readonly EconomyDirector _director;
        private readonly Wallet _wallet = new Wallet();
        private readonly Action _openAccounts, _openCollections, _openAdvice, _openLender;
        private CreditFile _file;
        private bool _afterSave;       // the loans' own figures were written back for a save: what is held is written again
        private bool _repoWaiting;     // a vehicle is to be taken as soon as it stands out of sight
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

        public static int Level => ProductCatalogue.Level(Settings.EconomyLevel);

        private static IModLogger Logger => EconomyDirector.Logger;
        private static bool Verbose => Meta.Dev.Verbose;
        private static GameInstance Save => SaveGameManager.Current;
        private static float Multiplier => GameLoans.Multiplier;
        private static int Today => Save != null ? Save.Day : 0;

        /// <summary>An account's name for the player: its kind, and what it is secured on if anything.</summary>
        public static string Name(CreditAccount a)
        {
            string kind = FinanceText.T(a.NameKey);
            string name = a.SecurityName.Length > 0 ? FinanceText.T("reallife_econ_account_named", "kind", kind, "name", Thing(a)) : kind;
            return a.Test ? FinanceText.T("reallife_econ_account_test", "name", name) : name;
        }

        /// <summary>What an account is or was secured on: the vehicle's name, or the building's address.</summary>
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
            Notifications.Show(type, key, data, seconds, null, Guard.Wrap(click ?? _openAccounts), true, false);
        }

        // A dictionary each: the game keeps a notification's data for as long as it lists the notification.
        private static Dictionary<string, string> Data(string a, string av, string b = null, string bv = null, string c = null, string cv = null, string d = null, string dv = null)
        {
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

        private static void Check(string what, bool ok)
        {
            if (Verbose)
            {
                Logger?.Info("Economy check (" + what + "): " + (ok ? "PASS" : "FAIL"));
            }
        }

        /// <summary>The file is open and the loans have their marks: the accounts join the rating, and what is held on a loan is written.</summary>
        public void Start(CreditFile file, int today)
        {
            _file = file;
            AccountRules.DaysPerYear = GameCalendar.DaysPerYear;
            BankTerms.Secured = SecuredAt;
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                if (!file.Accounts[i].Closed && !file.Accounts[i].Test)
                {
                    CreditLedger.Register(file.Accounts[i]);   // a test account is never part of the rating
                }
            }
            SelfCheck(today);
            ApplyHolds("the start");
            RememberOwned();
            DebtPhone.Setup(this);
        }

        /// <summary>The economy stops: every loan has its own figures again; what was held stays noted in the file.</summary>
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

        // At load: every loan with something held has its own figures remembered, and every security is still there.
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
                        // nothing to write back: the loan keeps its figures, which a save always holds as the game's own
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
            if (Verbose || unsound + orphans + unsecured > 0)
            {
                Logger?.Info("Economy check (accounts at load, day " + today + "): " + _file.Accounts.Count + " account(s), " + held + " loan(s) with something held ("
                             + unsound + " without its own figures: cleared), " + orphans + " business loan(s) whose business has gone (now unsecured), " + unsecured
                             + " account(s) whose security has gone (now unsecured)" + (_file.Relief.Arranged ? ", a debt arrangement running" : string.Empty)
                             + (_file.Shark.Active ? ", " + _file.Shark.Owed + " owed to the private lender" : string.Empty) + ": " + (unsound == 0 ? "PASS" : "FAIL"));
            }
        }

        private static bool SecurityThere(CreditAccount a)
        {
            return a.Kind == ProductKind.Mortgage ? Books.Estate(a.Security) != null : Books.Vehicle(a.Security) != null;
        }

        private float SecuredAt(BankSettings settings)
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

        /// <summary>Writes what is held onto the game's loans in memory, where a figure differs. Returns how many loans were written to.</summary>
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
                Logger?.Info("Economy check (rate, " + when + "): the loan of " + m.Total.ToString("0") + " at " + m.Street + " " + m.Number + ": daily interest " + loan.dailyInterest + " to "
                             + interest
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
            m.Interest = interest;      // the file's reading moves with it: the change is the mod's, not the borrower's
            m.Payment = payment;
        }

        /// <summary>Every game hour, before the director reads the loans: what is held is in place, as the night's run may follow.</summary>
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

        /// <summary>The first hour of a new day, before the game's run: if cash is short of what leaves tonight, the overdraft pays the shortfall.</summary>
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
            Obligations due = Books.Tomorrow(_file, day - 1);   // the game has moved its day on already: tonight's run is yesterday's "tomorrow"
            int cash = _wallet.Cash;
            int shortfall = Mathf.CeilToInt(due.Total) - cash;
            int paid = AccountRules.Cover(overdraft, shortfall);
            if (paid <= 0)
            {
                return;
            }
            _wallet.Give(paid, Money.Cover, Name(overdraft));
            CreditLedger.Touch();
            Note(NotificationType.Info, "reallife_econ_note_cover", Data("amount", FinanceText.Money(paid), "owed", FinanceText.Money(overdraft.Owed), "limit", FinanceText.Money(overdraft.Agreed)));
            if (Verbose)
            {
                Logger?.Info("Economy check (overdraft, day " + day + "): " + Mathf.CeilToInt(due.Total) + " leaves tonight (loans " + due.Loans.ToString("0") + ", accounts "
                             + due.Accounts.ToString("0")
                             + ", fines " + due.Other.ToString("0") + ", rent " + due.Rent.ToString("0") + ", wages " + due.Wages.ToString("0") + ") against " + cash
                                                               + " in cash: the overdraft paid " + paid
                             + ", " + overdraft.Owed + " of " + overdraft.Agreed + " drawn: " + (paid <= shortfall && overdraft.Owed <= overdraft.Agreed ? "PASS" : "FAIL"));
            }
        }

        /// <summary>Every game hour, after the loans are read: a security that has gone, a vehicle to take, a purchase on finance, what the private lender waits to do.</summary>
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

        /// <summary>The game is about to write a save: every loan has its own figures in it, so the save loads in the plain game.</summary>
        public void BeforeSave()
        {
            if (_file != null && RestoreHolds("the game is saving") > 0)
            {
                _afterSave = true;
            }
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
            CreditLedger.Touch();
            if (Verbose)
            {
                Logger?.Info("Economy check (account opened): " + a.Kind + " #" + a.Id + " with " + LenderName(a) + ", " + a.Owed + " owed of " + a.Agreed + " at "
                             + (a.RateBp / 100f).ToString("0.##", CultureInfo.InvariantCulture)
                             + "% a year" + (a.Term > 0 ? ", " + a.Instalment + " a day for " + a.Term + " days" : string.Empty) + (a.Secured ? ", secured on '" + a.SecurityName + "' ("
                                             + a.Security + ") bought for " + a.SecurityValue : string.Empty)
                             + (a.Test ? " (test)" : string.Empty) + ": PASS");
            }
        }

        private void Closed(CreditAccount a)
        {
            a.Closed = true;
            CreditLedger.Unregister(a);
            CreditLedger.Touch();
        }
    }
}
