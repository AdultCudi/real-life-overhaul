using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Anything that is credit: the game's loans now, and whatever later lends (a card, a mortgage, hire purchase).
    /// An account registers with <see cref="CreditLedger.Register"/>; from then on what it is owed counts in the
    /// rating and in the Finance app, and it reports each instalment with <see cref="CreditLedger.Post"/>
    /// (<see cref="CreditKind.PaidOnTime"/>, <see cref="CreditKind.Missed"/>, <see cref="CreditKind.PartPaid"/>).
    /// The members are read when the score is worked out and when the app opens, never per frame.
    /// </summary>
    public interface ICreditAccount
    {
        /// <summary>Short and never changed: it is the source written on the file's events ("loans").</summary>
        string Key { get; }
        /// <summary>The account's name for the Finance app, a locale key.</summary>
        string NameKey { get; }
        /// <summary>What is owed on it now.</summary>
        float Balance { get; }
        /// <summary>The most that could be owed on it on the lender's own terms (not the terms the rating has earned).</summary>
        float Limit { get; }
        /// <summary>What it takes at the next daily run, interest included.</summary>
        float PaymentDue { get; }
    }

    /// <summary>
    /// The credit rating's public face: the file of the loaded save, the score and grade, the gate every lender asks,
    /// and the one way to write on the file. Everything outside Scripts/Economy uses only this class (and
    /// <see cref="CreditCatalogue"/> for the figures). Game-free: the day, the income and the record reach it through
    /// the three delegates the Economy director sets.
    /// </summary>
    public static class CreditLedger
    {
        private static readonly List<ICreditAccount> AccountList = new List<ICreditAccount>(4);
        private static readonly CreditScore Now = new CreditScore();
        private static bool _fresh;

        /// <summary>The game's day.</summary>
        public static Func<int> Clock;
        /// <summary>The game's own figure for a day's income.</summary>
        public static Func<float> Income;
        /// <summary>The legal and public record (Wiring/EconomyLinks).</summary>
        public static Func<LegalStanding> Legal;

        /// <summary>The file of the loaded save; null while there is none or the economy is off.</summary>
        public static CreditFile File { get; private set; }

        public static bool Ready => File != null;

        public static IReadOnlyList<ICreditAccount> Accounts => AccountList;

        /// <summary>Raised when the grade has changed: the grade it was, the grade it is.</summary>
        public static event Action<CreditGrade, CreditGrade> GradeChanged;

        /// <summary>Raised after a line was written: the line (with its effect), the score before and after.</summary>
        public static event Action<CreditEvent, int, int> Posted;

        /// <summary>The rating as it stands. Worked out again only when something has changed (<see cref="Touch"/>).</summary>
        public static CreditScore Score
        {
            get
            {
                if (!_fresh)
                {
                    Refresh();
                }
                return Now;
            }
        }

        public static int Value => Ready ? Score.Value : CreditCatalogue.CleanStart;

        public static CreditGrade Grade => CreditCatalogue.GradeOf(Value);

        public static int Today => Clock != null ? Clock() : 0;

        public static void Open(CreditFile file)
        {
            File = file;
            _fresh = false;
        }

        public static void Close()
        {
            File = null;
            _fresh = false;
            AccountList.Clear();
        }

        public static void Register(ICreditAccount account)
        {
            if (account != null && !AccountList.Contains(account))
            {
                AccountList.Add(account);
                _fresh = false;
            }
        }

        public static void Unregister(ICreditAccount account)
        {
            if (AccountList.Remove(account))
            {
                _fresh = false;
            }
        }

        /// <summary>Something the score reads has changed outside the file (a balance, the record, the day): it is worked out again when next asked for.</summary>
        public static void Touch()
        {
            _fresh = false;
        }

        /// <summary>
        /// The gate every lender asks before lending anything. Under the threshold the answer is no, whatever the
        /// lender, the amount or the preset; and for a set number of days after a bankruptcy it is no whatever the score.
        /// </summary>
        public static bool CanBorrow(out CreditRefusal reason)
        {
            if (!Ready)
            {
                reason = CreditRefusal.NotReady;
                return false;
            }
            reason = ReliefRules.NoCredit(File.Relief, Today) ? CreditRefusal.Bankrupt : Score.Lends ? CreditRefusal.None : CreditRefusal.BelowThreshold;
            return reason == CreditRefusal.None;
        }

        /// <summary>What the scoring needs from outside the file, over every registered account.</summary>
        public static CreditInputs Gather()
        {
            var inputs = new CreditInputs();
            for (int i = 0; i < AccountList.Count; i++)
            {
                ICreditAccount a = AccountList[i];
                inputs.Owed += Math.Max(0f, a.Balance);
                inputs.Limit += Math.Max(0f, a.Limit);
                inputs.DailyService += Math.Max(0f, a.PaymentDue);
            }
            inputs.DailyIncome = Income != null ? Income() : 0f;
            inputs.Legal = Legal != null ? Legal() : default(LegalStanding);
            return inputs;
        }

        /// <summary>Works the score out now. Returns it; raises <see cref="GradeChanged"/> if the grade is not the one last recorded.</summary>
        public static int Refresh()
        {
            if (File == null)
            {
                return CreditCatalogue.CleanStart;
            }
            CreditScoring.Compute(File, Gather(), Today, Now);
            _fresh = true;
            int was = File.LastGrade;
            File.LastScore = Now.Value;
            File.LastGrade = (int)Now.Grade;
            if (was >= 0 && was != (int)Now.Grade)
            {
                File.LastGradeWas = was;
                File.LastGradeDay = Today;
                File.UnseenGrade = true;
                GradeChanged?.Invoke((CreditGrade)was, Now.Grade);
            }
            return Now.Value;
        }

        /// <summary>
        /// Writes one line on the credit file and returns what it did to the score, in points. An instalment
        /// (<see cref="CreditKind.PaidOnTime"/>, <see cref="CreditKind.Missed"/>, <see cref="CreditKind.PartPaid"/>)
        /// is also counted on its day, <paramref name="count"/> times; the first <see cref="CreditKind.NewCredit"/>
        /// starts the length of the history. <paramref name="sourceKey"/> is the reporting account's
        /// <see cref="ICreditAccount.Key"/>. Does nothing while no file is open (the economy is off).
        /// </summary>
        public static int Post(CreditKind kind, int amount, int day, string sourceKey, int count = 1, bool test = false)
        {
            CreditFile file = File;
            if (file == null || kind < 0 || kind >= CreditKind.Count)
            {
                return 0;
            }
            count = Math.Max(1, count);
            int before = Score.Value;
            switch (kind)
            {
                case CreditKind.PaidOnTime: file.CountPayments(day, count, 0, 0, test); break;
                case CreditKind.Missed: file.CountPayments(day, 0, count, 0, test); break;
                case CreditKind.PartPaid: file.CountPayments(day, 0, 0, test ? 0 : count); break;
                case CreditKind.NewCredit:
                    if (file.FirstCreditDay <= 0 && !test)
                    {
                        file.FirstCreditDay = Math.Max(1, day);
                    }
                    break;
            }
            if (kind == CreditKind.Missed && !test)
            {
                file.UnseenMissed += count;
                if (file.LastMissedDay != day)
                {
                    file.LastMissedDay = day;
                    file.LastMissedCount = 0;
                    file.LastMissedAmount = 0;
                }
                file.LastMissedCount += count;
                file.LastMissedAmount += amount;
            }
            var e = new CreditEvent { Kind = kind, Day = day, Amount = amount, Count = count, Test = test, Source = sourceKey ?? string.Empty };
            file.Add(e);                      // on file before the score is read: an application counts from its own line
            _fresh = false;
            int after = Refresh();
            e.Effect = after - before;
            int last = file.Events.Count - 1;
            if (last >= 0)
            {
                file.Events[last] = e;
            }
            Posted?.Invoke(e, before, after);
            return e.Effect;
        }

        /// <summary>
        /// For the test tools: moves the score to <paramref name="target"/> with an entry marked as a test, which
        /// "clear test entries" takes out again. Returns the score reached.
        /// </summary>
        public static int TestSet(int target, int day)
        {
            CreditFile file = File;
            if (file == null)
            {
                return CreditCatalogue.CleanStart;
            }
            int before = Score.Value;
            file.TestOffset += CreditCatalogue.Clamp(target) - before;
            _fresh = false;
            int after = Refresh();
            var e = new CreditEvent { Kind = CreditKind.TestAdjust, Day = day, Amount = after, Count = 1, Effect = after - before, Test = true, Source = "test" };
            file.Add(e);
            Posted?.Invoke(e, before, after);
            return after;
        }

        /// <summary>For the test tools: everything they wrote is taken out. True if there was anything.</summary>
        public static bool TestClear()
        {
            if (File == null || !File.ClearTests())
            {
                return false;
            }
            _fresh = false;
            Refresh();
            return true;
        }
    }
}
