using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The penalty rate and a debt arrangement as written on one of the game's loans' daily interest and payment, and put back
    /// exactly: the loan's own figures are kept on its mark and written back before every save, so a save holds only the
    /// game's. A figure changed from outside meanwhile (EconoView) is taken as the loan's own; the game's pricing again for a
    /// new interest multiplier is undone first (<see cref="Repriced"/>). Game-free.
    /// </summary>
    public static class LoanHolds
    {
        /// <summary>A hold begins: the first remembers the loan's figures, a further one puts them back first. The caller then calls <see cref="Apply"/>.</summary>
        public static void Begin(LoanMark mark, int hold, ref int interest, ref int payment, float multiplier, int daysPerYear)
        {
            if (mark.Held == LoanHold.None)
            {
                mark.OwnInterest = Math.Max(0, interest);
                mark.OwnPayment = Math.Max(0, payment);
            }
            else
            {
                Restore(mark, ref interest, ref payment, multiplier, daysPerYear);
            }
            mark.Held |= hold;
        }

        public static void Wanted(LoanMark mark, float multiplier, int daysPerYear, out int interest, out int payment)
        {
            if ((mark.Held & LoanHold.Frozen) != 0)
            {
                interest = 0;
                payment = 0;
                return;
            }
            payment = mark.OwnPayment;
            interest = (mark.Held & LoanHold.Penalty) != 0
                ? GameLoanMath.PenaltyInterest(mark.OwnInterest, mark.Total, ProductCatalogue.PenaltyPoints, multiplier, daysPerYear)
                : mark.OwnInterest;
        }

        // A figure that is neither the loan's own nor what the mod wrote was changed by the game or the player: it is the loan's own now.
        private static void Adopt(LoanMark mark, int interest, int payment, float multiplier, int daysPerYear)
        {
            int wantInterest, wantPayment;
            Wanted(mark, multiplier, daysPerYear, out wantInterest, out wantPayment);
            if (interest != wantInterest && interest != mark.OwnInterest)
            {
                mark.OwnInterest = Math.Max(0, interest);
            }
            if (payment != wantPayment && payment != mark.OwnPayment)
            {
                mark.OwnPayment = Math.Max(0, payment);
            }
        }

        /// <summary>Writes what is held onto the loan's figures. True if either changed; nothing for a loan with nothing held.</summary>
        public static bool Apply(LoanMark mark, ref int interest, ref int payment, float multiplier, int daysPerYear)
        {
            if (mark.Held == LoanHold.None)
            {
                return false;
            }
            Adopt(mark, interest, payment, multiplier, daysPerYear);
            int wantInterest, wantPayment;
            Wanted(mark, multiplier, daysPerYear, out wantInterest, out wantPayment);
            bool changed = interest != wantInterest || payment != wantPayment;
            interest = wantInterest;
            payment = wantPayment;
            return changed;
        }

        /// <summary>Writes the loan's own figures back; what is held stays noted, to be applied again. True if either changed.</summary>
        public static bool Restore(LoanMark mark, ref int interest, ref int payment, float multiplier, int daysPerYear)
        {
            if (mark.Held == LoanHold.None)
            {
                return false;
            }
            Adopt(mark, interest, payment, multiplier, daysPerYear);
            bool changed = interest != mark.OwnInterest || payment != mark.OwnPayment;
            interest = mark.OwnInterest;
            payment = mark.OwnPayment;
            return changed;
        }

        /// <summary>A hold ends: the loan's own figures are written back, and whatever else is still held is applied again.</summary>
        public static void End(LoanMark mark, int hold, ref int interest, ref int payment, float multiplier, int daysPerYear)
        {
            if ((mark.Held & hold) == 0)
            {
                return;
            }
            Restore(mark, ref interest, ref payment, multiplier, daysPerYear);
            mark.Held &= ~hold;
            if (mark.Held != LoanHold.None)
            {
                Apply(mark, ref interest, ref payment, multiplier, daysPerYear);
            }
            else
            {
                mark.OwnInterest = 0;
                mark.OwnPayment = 0;
            }
        }

        /// <summary>
        /// The game has priced its loans again for a new interest multiplier, at the rate each bank offers today. The loan keeps
        /// its own: its payment as last read, and the interest of the rate it was made at under the new multiplier (the game's
        /// figure stands where no rate can be read). With something held those become its own figures, and the held ones are
        /// what it carries while <paramref name="holds"/> (the desk is running). The mark's reading moves with it.
        /// </summary>
        public static void Repriced(LoanMark mark, ref int interest, ref int payment, float was, float now, int years, int daysPerYear, bool holds)
        {
            bool held = mark.Held != LoanHold.None;
            int own = GameLoanMath.Repriced(held ? mark.OwnInterest : mark.Interest, mark.Total, was, now, years, daysPerYear);
            interest = own >= 0 ? own : interest;
            payment = held ? mark.OwnPayment : mark.Payment;
            if (held)
            {
                mark.OwnInterest = interest;
                if (holds)
                {
                    Wanted(mark, now, daysPerYear, out interest, out payment);
                }
            }
            mark.Interest = interest;
            mark.Payment = payment;
        }

        /// <summary>The self-check: a loan with something held has its own figures remembered (no game loan has a payment and interest of nothing).</summary>
        public static bool Sound(LoanMark mark)
        {
            return mark.Held == LoanHold.None || mark.OwnInterest > 0 || mark.OwnPayment > 0;
        }
    }
}
