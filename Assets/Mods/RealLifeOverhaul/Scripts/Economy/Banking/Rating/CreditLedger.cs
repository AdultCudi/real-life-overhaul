using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Anything that is credit (the game's loans, the mod's accounts, the student loan): once registered, what it owes counts
    /// in the rating, and it reports each instalment with <see cref="CreditLedger.Post"/>. Read when the score is worked out.
    /// </summary>
    public interface ICreditAccount
    {
        /// <summary>Short and never changed: the source written on the file's events ("loans").</summary>
        string Key { get; }
        string NameKey { get; }   // a locale key
        float Balance { get; }
        /// <summary>The most that could be owed on the lender's own terms (not the terms the rating has earned).</summary>
        float Limit { get; }
        /// <summary>What it takes at the next daily run, interest included.</summary>
        float PaymentDue { get; }
    }

    /// <summary>
    /// The credit rating's public face: the loaded save's file, the score, the gate every lender asks and the one way to write
    /// on the file. Game-free: the day, the income and the record come through delegates the director sets.
    /// </summary>
    public static class CreditLedger
    {
        private static readonly List<ICreditAccount> AccountList = new List<ICreditAccount>(4);
        private static readonly CreditScore Now = new CreditScore();
        private static bool _fresh;

        public static Func<int> Clock;
        public static Func<float> Income;   // the game's own figure for a day's income
        public static Func<LegalStanding> Legal;

        /// <summary>The loaded save's file; null while there is none or the economy is off.</summary>
        public static CreditFile File { get; private set; }

        public static bool Ready => File != null;

        public static IReadOnlyList<ICreditAccount> Accounts => AccountList;

        public static event Action<CreditGrade, CreditGrade> GradeChanged;   // the grade it was, the grade it is
        public static event Action<CreditEvent, int, int> Posted;            // the line with its effect, the score before and after

        /// <summary>The rating as it stands, worked out again only after <see cref="Touch"/>.</summary>
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

        /// <summary>Something the score reads has changed outside the file (a balance, the record, the day).</summary>
        public static void Touch()
        {
            _fresh = false;
        }

        /// <summary>The gate every lender asks: no under the threshold, whatever the lender, amount or preset, and no for a while after a bankruptcy.</summary>
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

        /// <summary>Works the score out now and returns it; raises <see cref="GradeChanged"/> if the grade moved.</summary>
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
        /// Writes one line on the file; an instalment kind is also counted on its day, <paramref name="count"/> times. Returns
        /// what it did to the score, in points; nothing while no file is open.
        /// </summary>
        public static int Post(CreditKind kind, int amount, int day, string sourceKey, int count = 1, bool test = false)
        {
            CreditFile file = File;
            if (file == null)
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
            file.Add(e);   // on file before the score is read: an application counts from its own line
            _fresh = false;
            int after = Refresh();
            e.Effect = after - before;
            file.Events[file.Events.Count - 1] = e;
            Posted?.Invoke(e, before, after);
            return e.Effect;
        }

        /// <summary>For the test tools: moves the score to <paramref name="target"/> with a test entry. Returns the score reached.</summary>
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
