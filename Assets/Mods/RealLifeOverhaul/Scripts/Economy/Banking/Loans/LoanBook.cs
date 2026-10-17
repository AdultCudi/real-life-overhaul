using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a comparison of the file's loans with the game's found.</summary>
    public enum LoanNote
    {
        New,            // a loan the file had not seen
        Gone,           // no longer in the game outside the daily run: paid off by the borrower
        PaymentChanged, // its daily payment was changed
        ExtraPaid,      // its balance fell outside the daily run: part paid off by the borrower
        Paid,           // the daily run took its instalment
        PaidOff,        // the daily run took its last instalment
        Missed          // the daily run could not take its instalment
    }

    /// <summary>One thing a comparison found about one loan.</summary>
    public struct LoanFinding
    {
        public LoanNote Note;
        public LoanMark Mark;       // a copy of the file's mark as it was before the comparison
        public LoanMark Live;       // the file's own mark, where the loan is still there
        public int Amount;          // the money the note is about
    }

    /// <summary>
    /// Tells the game's loans apart and reads what happened to them. The game's NewDay raises its hour event, runs the day
    /// (LoanHelper.RunDaily) and then raises its day event: <see cref="Sync"/> on the hour leaves every mark as its loan
    /// stood before the run, and <see cref="Settle"/> on the day reads the run. Game-free.
    /// </summary>
    public static class LoanBook
    {
        private const float Cent = 0.5f;   // balances are whole in practice: this only absorbs float noise

        /// <summary>Brings the marks up to date and says what changed since the last look; nothing with <paramref name="quiet"/>.</summary>
        public static void Sync(List<LoanMark> marks, List<LoanView> live, int today, List<LoanFinding> findings, bool quiet = false)
        {
            Match(marks, live);
            for (int m = marks.Count - 1; m >= 0; m--)
            {
                LoanMark mark = marks[m];
                if (mark.Matched)
                {
                    continue;
                }
                if (!quiet && findings != null)
                {
                    findings.Add(new LoanFinding { Note = LoanNote.Gone, Mark = Copy(mark), Amount = (int)Math.Round(mark.Remaining) });
                }
                marks.RemoveAt(m);
            }
            for (int i = 0; i < live.Count; i++)
            {
                LoanView loan = live[i];
                LoanMark mark = Find(marks, loan.Ref);
                if (mark == null)
                {
                    mark = new LoanMark { Street = loan.Street, Number = loan.Number, Total = loan.Total, FirstDay = today, Ref = loan.Ref };
                    Take(mark, loan);
                    marks.Add(mark);
                    if (!quiet && findings != null)
                    {
                        findings.Add(new LoanFinding { Note = LoanNote.New, Mark = Copy(mark), Live = mark, Amount = (int)Math.Round(loan.Total) });
                    }
                    continue;
                }
                if (!quiet && findings != null)
                {
                    if (loan.Remaining < mark.Remaining - Cent)
                    {
                        findings.Add(new LoanFinding { Note = LoanNote.ExtraPaid, Mark = Copy(mark), Amount = (int)Math.Round(mark.Remaining - loan.Remaining) });
                    }
                    if (loan.Payment != mark.Payment && (mark.Held & LoanHold.Frozen) == 0)
                    {
                        findings.Add(new LoanFinding { Note = LoanNote.PaymentChanged, Mark = Copy(mark), Amount = loan.Payment });
                    }
                }
                Take(mark, loan);
            }
        }

        /// <summary>After the game's daily run: what became of each instalment, against the marks as the last hour left them.</summary>
        public static void Settle(List<LoanMark> marks, List<LoanView> live, List<LoanFinding> findings)
        {
            Match(marks, live);
            for (int m = marks.Count - 1; m >= 0; m--)
            {
                LoanMark mark = marks[m];
                int due = (int)Math.Round(Math.Min(mark.Payment, mark.Remaining));
                if (!mark.Matched)
                {
                    findings.Add(new LoanFinding { Note = LoanNote.PaidOff, Mark = Copy(mark), Live = mark, Amount = due + mark.Interest });
                    marks.RemoveAt(m);
                    continue;
                }
                LoanView loan = default(LoanView);
                for (int i = 0; i < live.Count; i++)
                {
                    if (ReferenceEquals(live[i].Ref, mark.Ref))
                    {
                        loan = live[i];
                        break;
                    }
                }
                if (loan.Remaining < mark.Remaining - Cent)
                {
                    findings.Add(new LoanFinding { Note = LoanNote.Paid, Mark = Copy(mark), Live = mark, Amount = due + mark.Interest });
                }
                else if (mark.Payment > 0 && mark.Remaining > Cent)
                {
                    findings.Add(new LoanFinding { Note = LoanNote.Missed, Mark = Copy(mark), Live = mark, Amount = due });
                }
                Take(mark, loan);
            }
            // the run makes no loan, but a list is never left half read
            for (int i = 0; i < live.Count; i++)
            {
                if (Find(marks, live[i].Ref) == null)
                {
                    var mark = new LoanMark { Street = live[i].Street, Number = live[i].Number, Total = live[i].Total, Ref = live[i].Ref };
                    Take(mark, live[i]);
                    marks.Add(mark);
                }
            }
        }

        // Sets Matched and Ref on every mark that has a loan: by the object first, then by the fingerprint (the bank's address
        // and the amount, which never change), two alike told apart by balance, which only falls, in the game's list order.
        private static void Match(List<LoanMark> marks, List<LoanView> live)
        {
            for (int m = 0; m < marks.Count; m++)
            {
                LoanMark mark = marks[m];
                mark.Matched = false;
                mark.Gone = false;
                if (mark.Ref == null)
                {
                    continue;
                }
                for (int i = 0; i < live.Count; i++)
                {
                    if (ReferenceEquals(live[i].Ref, mark.Ref))
                    {
                        mark.Matched = true;
                        break;
                    }
                }
                if (!mark.Matched)
                {
                    mark.Ref = null;   // its loan has left the game's list: never to be taken for another loan of the same print
                    mark.Gone = true;
                }
            }
            for (int i = 0; i < live.Count; i++)
            {
                LoanView loan = live[i];
                if (loan.Ref == null || Find(marks, loan.Ref) != null)
                {
                    continue;
                }
                LoanMark pick = null;
                for (int m = 0; m < marks.Count; m++)
                {
                    LoanMark mark = marks[m];
                    if (mark.Matched || mark.Gone || mark.Ref != null || !SamePrint(mark, loan) || loan.Remaining > mark.Remaining + Cent)
                    {
                        continue;
                    }
                    if (Math.Abs(mark.Remaining - loan.Remaining) <= Cent)
                    {
                        pick = mark;
                        break;
                    }
                    pick = pick ?? mark;
                }
                if (pick != null)
                {
                    pick.Ref = loan.Ref;
                    pick.Matched = true;
                }
            }
        }

        private static bool SamePrint(LoanMark mark, in LoanView loan)
        {
            return mark.Number == loan.Number && Math.Abs(mark.Total - loan.Total) <= Cent && string.Equals(mark.Street, loan.Street, StringComparison.Ordinal);
        }

        private static LoanMark Find(List<LoanMark> marks, object reference)
        {
            if (reference == null)
            {
                return null;
            }
            for (int m = 0; m < marks.Count; m++)
            {
                if (ReferenceEquals(marks[m].Ref, reference))
                {
                    return marks[m];
                }
            }
            return null;
        }

        private static void Take(LoanMark mark, in LoanView loan)
        {
            mark.Remaining = loan.Remaining;
            mark.Payment = loan.Payment;
            mark.Interest = loan.Interest;
        }

        // Held is copied too: it says whether a debt arrangement held the loan.
        private static LoanMark Copy(LoanMark mark)
        {
            return new LoanMark
            {
                Street = mark.Street, Number = mark.Number, Total = mark.Total, FirstDay = mark.FirstDay,
                Remaining = mark.Remaining, Payment = mark.Payment, Interest = mark.Interest, Held = mark.Held
            };
        }
    }
}
