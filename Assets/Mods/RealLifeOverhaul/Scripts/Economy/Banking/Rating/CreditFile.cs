using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One dated line of the credit file.</summary>
    public struct CreditEvent
    {
        public CreditKind Kind;
        public int Day;
        public int Amount;      // money, whole
        public int Count;       // how many things the line stands for (three instalments in one day): at least 1
        public int Effect;      // what posting it did to the score, in points
        public bool Test;       // written by a test tool: taken out by "clear test entries", never folded into the totals
        public string Source;   // the reporting account's key ("loans", "phone", "legal", "test"); never shown
    }

    /// <summary>The instalments of one game day. The test tools' are kept apart so they can be taken out exactly.</summary>
    public struct PayDay
    {
        public int Day;
        public int OnTime, Missed, Part;
        public int TestOnTime, TestMissed;
    }

    /// <summary>The legal and public record as the rating reads it, given by a bridge (Wiring/EconomyLinks).</summary>
    public struct LegalStanding
    {
        public int Convictions;     // by a court, still counting
        public int Judgments;       // civil judgments being paid off
        public int LateDays;        // days running on which a fine or court debt could not be taken, over all debts
        public int Outstanding;     // fines and court debts still owed, in money
        public int RecentOffences;  // lines on the police record in the last CreditCatalogue.OffenceDays
    }

    /// <summary>What the scoring needs from outside the file, gathered when the score is worked out.</summary>
    public struct CreditInputs
    {
        public float Owed;          // over every credit account
        public float Limit;         // what the lenders' own terms would let the borrower owe
        public float DailyService;  // what the accounts take tomorrow
        public float DailyIncome;   // the game's own figure for a day's income
        public LegalStanding Legal;
    }

    /// <summary>The credit file of one save: what the rating is worked out from, the loans as last seen, the mod's accounts and debts. Game-free.</summary>
    public sealed class CreditFile
    {
        public const int AccountsMost = 24;   // accounts the file keeps at most

        public int StartDay;            // the day the file was opened on this save
        public int FirstCreditDay;      // the day credit was first held; 0 for never
        public int OldOnTime, OldMissed, OldPart;   // instalments older than the fade, folded
        public int TestOffset;          // points a test tool has put on the score
        public int LastScore;           // as last worked out; 0 before the first time
        public int LastGrade = -1;
        public int SettledDay;          // the last day whose instalments were read
        public int UnseenMissed;        // for the app's badge: missed since the app was last opened
        public bool UnseenGrade;        // and a change of grade since then
        public int LastMissedDay, LastMissedCount, LastMissedAmount;
        public int LastGradeDay, LastGradeWas;
        public LegalStanding LegalSeen; // the record as it was when it was last compared

        public readonly List<PayDay> Days = new List<PayDay>(CreditCatalogue.PaymentFadeDays + 2);
        public readonly List<CreditEvent> Events = new List<CreditEvent>(CreditCatalogue.LedgerMost + 1);
        public readonly int[] FoldedCount = new int[(int)CreditKind.Count];
        public readonly long[] FoldedAmount = new long[(int)CreditKind.Count];
        public readonly List<LoanMark> Loans = new List<LoanMark>(4);

        public readonly List<CreditAccount> Accounts = new List<CreditAccount>(6);
        public readonly SharkDebt Shark = new SharkDebt();
        public readonly Relief Relief = new Relief();
        public int NextAccountId = 1;

        /// <summary>The open account of a kind, or null; never a test tool's. Of several finance accounts, the first.</summary>
        public CreditAccount Account(ProductKind kind)
        {
            for (int i = 0; i < Accounts.Count; i++)
            {
                if (Accounts[i].Kind == kind && !Accounts[i].Closed && !Accounts[i].Test)
                {
                    return Accounts[i];
                }
            }
            return null;
        }

        public CreditAccount AccountById(int id)
        {
            for (int i = 0; i < Accounts.Count; i++)
            {
                if (Accounts[i].Id == id)
                {
                    return Accounts[i];
                }
            }
            return null;
        }

        /// <summary>Puts an account in the file with an id of its own. Never shortens the list: it may be being walked.</summary>
        public CreditAccount Add(CreditAccount account)
        {
            account.Id = NextAccountId++;
            Accounts.Add(account);
            return account;
        }

        /// <summary>Closed accounts are forgotten (their history is on the file's events).</summary>
        public void Trim()
        {
            for (int i = Accounts.Count - 1; i >= 0; i--)
            {
                if (Accounts[i].Closed)
                {
                    Accounts.RemoveAt(i);
                }
            }
        }

        /// <summary>Adds a line, folding the oldest into the totals beyond <see cref="CreditCatalogue.LedgerMost"/>.</summary>
        public void Add(CreditEvent e)
        {
            if (e.Count < 1)
            {
                e.Count = 1;
            }
            Events.Add(e);
            while (Events.Count > CreditCatalogue.LedgerMost)
            {
                CreditEvent old = Events[0];
                Events.RemoveAt(0);
                int kind = (int)old.Kind;
                if (!old.Test && kind >= 0 && kind < FoldedCount.Length)
                {
                    FoldedCount[kind] += old.Count;
                    FoldedAmount[kind] += old.Amount;
                }
            }
        }

        /// <summary>How many of a kind are on file, folded ones included, test entries not.</summary>
        public int Total(CreditKind kind, out long amount)
        {
            int count = FoldedCount[(int)kind];
            amount = FoldedAmount[(int)kind];
            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i].Kind == kind && !Events[i].Test)
                {
                    count += Events[i].Count;
                    amount += Events[i].Amount;
                }
            }
            return count;
        }

        /// <summary>Counts instalments on a day, adding to a day already on file; the days stay in order.</summary>
        public void CountPayments(int day, int onTime, int missed, int part, bool test = false)
        {
            int at = -1;
            for (int i = Days.Count - 1; i >= 0; i--)
            {
                if (Days[i].Day == day)
                {
                    at = i;
                    break;
                }
            }
            PayDay d = at >= 0 ? Days[at] : new PayDay { Day = day };
            if (test)
            {
                d.TestOnTime += onTime;
                d.TestMissed += missed;
            }
            else
            {
                d.OnTime += onTime;
                d.Missed += missed;
                d.Part += part;
            }
            if (at >= 0)
            {
                Days[at] = d;
                return;
            }
            int before = Days.Count;
            while (before > 0 && Days[before - 1].Day > day)
            {
                before--;
            }
            Days.Insert(before, d);
        }

        /// <summary>Days past the fade no longer weigh by their date: their real instalments go into the lasting totals.</summary>
        public void Roll(int today)
        {
            int i = 0;
            while (i < Days.Count)
            {
                PayDay d = Days[i];
                if (today - d.Day < CreditCatalogue.PaymentFadeDays)
                {
                    i++;
                    continue;
                }
                OldOnTime += d.OnTime;
                OldMissed += d.Missed;
                OldPart += d.Part;
                Days.RemoveAt(i);
            }
        }

        /// <summary>Takes out everything a test tool wrote. True if there was anything.</summary>
        public bool ClearTests()
        {
            bool any = TestOffset != 0;
            TestOffset = 0;
            for (int i = Events.Count - 1; i >= 0; i--)
            {
                if (Events[i].Test)
                {
                    Events.RemoveAt(i);
                    any = true;
                }
            }
            for (int i = Days.Count - 1; i >= 0; i--)
            {
                PayDay d = Days[i];
                if (d.TestOnTime == 0 && d.TestMissed == 0)
                {
                    continue;
                }
                any = true;
                d.TestOnTime = 0;
                d.TestMissed = 0;
                if (d.OnTime == 0 && d.Missed == 0 && d.Part == 0)
                {
                    Days.RemoveAt(i);
                }
                else
                {
                    Days[i] = d;
                }
            }
            return any;
        }
    }
}
