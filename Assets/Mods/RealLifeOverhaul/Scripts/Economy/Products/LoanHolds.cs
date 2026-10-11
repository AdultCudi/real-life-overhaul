using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the mod changes on one of the game's loans, and how it is put back exactly. Game-free: it works on the
    /// loan's two figures (its daily interest and its daily payment) and the loan's mark in the credit file.
    ///
    /// The game keeps a loan's daily interest and daily payment as plain numbers and takes them each night
    /// (LoanHelper.RunDaily). Two things of the mod's are done by writing them: the PENALTY RATE (the interest carries
    /// the penalty points, <see cref="GameLoanMath.PenaltyInterest"/>) and a DEBT ARRANGEMENT (interest and payment
    /// are nothing while the arrangement pays the loan). The loan's own two figures are kept on its mark from the
    /// moment anything is held (<see cref="Begin"/>) and written back when it ends (<see cref="End"/>), when the
    /// economy stops, and every time the game writes a save (<see cref="Restore"/>), so a save never holds anything
    /// but the game's own figures and loads in the plain game as it would without the mod. After a save they are
    /// written again (<see cref="Apply"/>) before the next night's run.
    ///
    /// If the game or the player changes a held loan in the meantime (the game recalculates every loan when its
    /// interest multiplier is changed; EconoView changes a daily payment), the new figure is taken as the loan's own.
    /// </summary>
    public static class LoanHolds
    {
        /// <summary>
        /// A hold begins on a loan. The first hold remembers the loan's figures as its own; a further one first puts
        /// the loan's own figures back, so that what was written for the first is never taken for a change from
        /// outside. The caller writes what is held with <see cref="Apply"/>.
        /// </summary>
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

        /// <summary>What a held loan's figures should be in memory.</summary>
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

        // A figure that is neither the loan's own nor what the mod wrote was changed by the game or by the player: it is the loan's own now.
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

        /// <summary>Writes what is held onto the loan's figures. True if either changed. Does nothing for a loan with nothing held.</summary>
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

        /// <summary>The self-check's question: a loan with something held has its own figures remembered (a payment of nothing with interest of nothing is no loan the game makes).</summary>
        public static bool Sound(LoanMark mark)
        {
            return mark.Held == LoanHold.None || mark.OwnInterest > 0 || mark.OwnPayment > 0;
        }
    }
}
