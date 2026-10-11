using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a comparison of the file's loans with the game's found.</summary>
    public enum LoanNote
    {
        New,            // a loan the file had not seen
        Gone,           // a loan no longer in the game outside the daily run: paid off by the borrower
        PaymentChanged, // its daily payment was changed
        ExtraPaid,      // its balance fell outside the daily run: part paid off by the borrower
        Paid,           // the daily run took its instalment
        PaidOff,        // the daily run took its last instalment
        Missed          // the daily run could not take its instalment
    }

    public struct LoanFinding
    {
        public LoanNote Note;
        public LoanMark Mark;       // as the file had it before the comparison
        public int Amount;          // the money the note is about
    }

    /// <summary>
    /// Knows the game's loans apart and reads what happened to them, without touching the game's code. Game-free.
    ///
    /// IDENTITY. The game's Loan has no id and its list changes (a paid-off loan is removed, a new one is added at the
    /// end). While a save is loaded a loan is the object itself (LoanMark.Ref). After a load the objects are new, so a
    /// mark is matched to a loan by its fingerprint: the bank's address and the amount borrowed, which never change.
    /// Two loans of one amount from one bank are told apart by their balance: the mark whose saved balance equals the
    /// loan's is taken first, else the first whose saved balance is not below it (a balance only falls), in the
    /// list's order, which the game keeps.
    ///
    /// THE DAILY PAYMENT. The game's NewDay raises its hour event first, then runs the day (LoanHelper.RunDaily takes
    /// each instalment, or only the interest when the money is short), then raises its day event. <see cref="Sync"/> on
    /// the hour event therefore leaves every mark exactly as its loan stood before the run, and <see cref="Settle"/> on
    /// the day event compares after it: a balance that fell was paid, a loan that is gone was paid off, a balance that
    /// did not move while a payment was due was missed. Nothing the player does can fall between the two.
    /// </summary>
    public static class LoanBook
    {
        private const float Cent = 0.5f;   // balances are whole in practice; this only absorbs float noise

        /// <summary>
        /// Brings the marks up to date with the game's loans and says what changed since the last time. With
        /// <paramref name="quiet"/> nothing is said (the first look after a load, or after the feature was off).
        /// </summary>
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
                        findings.Add(new LoanFinding { Note = LoanNote.New, Mark = Copy(mark), Amount = (int)Math.Round(loan.Total) });
                    }
                    continue;
                }
                if (!quiet && findings != null)
                {
                    if (loan.Remaining < mark.Remaining - Cent)
                    {
                        findings.Add(new LoanFinding { Note = LoanNote.ExtraPaid, Mark = Copy(mark), Amount = (int)Math.Round(mark.Remaining - loan.Remaining) });
                    }
                    if (loan.Payment != mark.Payment)
                    {
                        findings.Add(new LoanFinding { Note = LoanNote.PaymentChanged, Mark = Copy(mark), Amount = loan.Payment });
                    }
                }
                Take(mark, loan);
            }
        }

        /// <summary>
        /// After the game's daily run: what became of each instalment. The marks are as <see cref="Sync"/> left them
        /// before the run and are brought up to date here.
        /// </summary>
        public static void Settle(List<LoanMark> marks, List<LoanView> live, List<LoanFinding> findings)
        {
            Match(marks, live);
            for (int m = marks.Count - 1; m >= 0; m--)
            {
                LoanMark mark = marks[m];
                int due = (int)Math.Round(Math.Min(mark.Payment, mark.Remaining));
                if (!mark.Matched)
                {
                    findings.Add(new LoanFinding { Note = LoanNote.PaidOff, Mark = Copy(mark), Amount = due + mark.Interest });
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
                    findings.Add(new LoanFinding { Note = LoanNote.Paid, Mark = Copy(mark), Amount = due + mark.Interest });
                }
                else if (mark.Payment > 0 && mark.Remaining > Cent)
                {
                    findings.Add(new LoanFinding { Note = LoanNote.Missed, Mark = Copy(mark), Amount = due });
                }
                Take(mark, loan);
            }
            // A loan made during the run itself cannot be (the run makes none), but a list is never left half read.
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

        // Sets Matched and Ref on every mark that has a loan: by the object first, by the fingerprint for the rest.
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
                    mark.Ref = null;   // its loan has left the game's list: it is not to be taken for another loan of the same print
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

        private static LoanMark Copy(LoanMark mark)
        {
            return new LoanMark
            {
                Street = mark.Street, Number = mark.Number, Total = mark.Total, FirstDay = mark.FirstDay,
                Remaining = mark.Remaining, Payment = mark.Payment, Interest = mark.Interest
            };
        }
    }
}
