using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Giving
{
    /// <summary>How a loan's turn on a day ended.</summary>
    public enum LoanTurn
    {
        None = 0,         // not its day, or not open
        Part = 1,         // a part was paid back
        Repaid = 2,       // the last part was paid back
        Missed = 3,       // nothing could be paid this month
        WrittenOff = 4    // nothing could be paid for the months in a row that write it off
    }

    /// <summary>
    /// The rules of a loan to a person, with no game in it: who may be lent to and how much, the loan made, and a month's turn of it (the person pays back what their household can
    /// spare, or nothing, and after a few months of nothing it is written off, with no court). The director books the player's money and tells the person; the rest is here.
    /// COST: a few operations a loan a day.
    /// </summary>
    public static class GivingLoans
    {
        /// <summary>
        /// Whether a loan of <paramref name="amount"/> (today's whole money) may be made to the social cast's person: only a resident the population holds in a household that can be
        /// helped can pay it back out of their own money; at most the most, one at a time for a person, eight open. Done to go on; <paramref name="resident"/> and
        /// <paramref name="household"/> are the person's.
        /// </summary>
        public static GiveResult Check(GivingState st, PopulationState s, int socialIndex, int amount, int level, out int resident, out int household)
        {
            resident = GivingPeople.ResidentOf(s, socialIndex);
            household = resident >= 0 ? GivingPeople.HouseholdOf(s, resident) : -1;
            if (household < 0 || !GivingPeople.Helpable(s, household))
            {
                return GiveResult.NoPerson;
            }
            if (amount <= 0)
            {
                return GiveResult.Nothing;
            }
            if (amount > GivingRules.At(GivingCatalogue.LoanMost, level) || st.OpenLoans() >= GivingCatalogue.LoansMost || st.LoanOf(socialIndex) != null)
            {
                return GiveResult.Limit;
            }
            return GiveResult.Done;
        }

        /// <summary>The loan made, after the player's money has been taken: it lands in the household as a gift does (its debt first), and the loan is written down, the closed ones trimmed.</summary>
        public static LoanOut Make(GivingState st, PopulationState s, int household, int socialIndex, string name, int amount, int day, int monthDays)
        {
            GivingPeople.Help(s, household, amount, out _, out _);
            var loan = new LoanOut
            {
                Person = socialIndex, Name = name ?? string.Empty, Principal = amount, Remaining = amount, Made = day, Part = GivingRules.LoanPart(amount), Next = day + Math.Max(1, monthDays)
            };
            st.Loans.Add(loan);
            Trim(st);
            return loan;
        }

        /// <summary>The closed loans a page keeps, the oldest dropped.</summary>
        public static void Trim(GivingState st)
        {
            int closed = 0;
            for (int i = st.Loans.Count - 1; i >= 0; i--)
            {
                if (st.Loans[i].State != LoanState.Open && ++closed > GivingCatalogue.LoanKept)
                {
                    st.Loans.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// A loan's turn on a day: when a part falls due the person pays what their household can spare (never more than the part or what is owed), taken out of the household's
        /// savings, or nothing. <paramref name="took"/> is what was paid, for the director to put in the player's cash.
        /// </summary>
        public static LoanTurn Collect(LoanOut l, PopulationState s, int day, int monthDays, out long took)
        {
            took = 0;
            if (l.State != LoanState.Open || day < l.Next)
            {
                return LoanTurn.None;
            }
            int p = GivingPeople.ResidentOf(s, l.Person);
            int h = p >= 0 ? GivingPeople.HouseholdOf(s, p) : -1;
            long spare = h >= 0 && s.Households.Alive(h) ? GivingPeople.Spare(s, h) : 0L;
            int want = GivingRules.Repayment(l.Remaining, l.Part, spare);
            took = want > 0 ? GivingPeople.Take(s, h, want) : 0L;
            l.Next = day + Math.Max(1, monthDays);
            if (took > 0)
            {
                l.Remaining -= (int)took;
                l.Paid += (int)took;
                l.Misses = 0;
                if (l.Remaining > 0)
                {
                    return LoanTurn.Part;
                }
                l.State = LoanState.Repaid;
                l.Closed = day;
                return LoanTurn.Repaid;
            }
            l.Misses++;
            if (l.Misses < GivingCatalogue.LoanMisses)
            {
                return LoanTurn.Missed;
            }
            l.State = LoanState.WrittenOff;
            l.Closed = day;
            return LoanTurn.WrittenOff;
        }
    }
}
