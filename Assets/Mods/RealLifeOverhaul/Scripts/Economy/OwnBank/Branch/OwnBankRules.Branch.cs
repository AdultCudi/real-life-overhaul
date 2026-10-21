using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What one visit at a branch's counter came to.</summary>
    public enum VisitResult
    {
        None = 0,       // an enquiry, or nothing the bank could do
        PaidIn = 1,     // an account opened or a deposit: the bank's people's deposits and its reserves grew
        TakenOut = 2,   // a withdrawal: both fell
        Lent = 3,       // a loan arranged: the bank's loans grew and its reserves fell
        Refused = 4     // a loan the bank would not make now
    }

    /// <summary>
    /// A branch's counter in the player's bank's own books: every visit moves the same balances the bank's day moves, so
    /// <see cref="Balanced"/> stays true and there is no second set of books. A visit's fee waits outside the balance sheet
    /// (<see cref="OwnBank.CounterFees"/>) until the next day books it as income. The branches' reach is the bank's hold of
    /// the city's people (<see cref="BankCity.Reach"/>, read by <see cref="SaversWanted"/> and <see cref="LoansWanted"/>).
    /// A visit is a few sums; nothing is allocated.
    /// </summary>
    public static partial class OwnBankRules
    {
        /// <summary>A kind of customer's hold, raised by the branches' reach: in full for people, a part of it for businesses.</summary>
        public static double Held(int hold, int reach, bool people)
        {
            int r = Math.Max(0, reach);
            return hold * (1000.0 + (people ? r : (long)r * BranchCatalogue.FirmsReach / 1000)) / 1000.0;
        }

        /// <summary>
        /// True while a loan of this many cents to a kind of borrower can be made at a counter now: the bank lends to them, is
        /// not in a run or a breach, and its cash over the reserve and its capital over the lending line both cover it.
        /// </summary>
        public static bool CounterLends(OwnBank b, int segment, long cents)
        {
            if (!b.Running || b.Run || b.Stage == Regulator.Breach || segment < 0 || segment >= OwnBankCatalogue.Segments || !b.Lending[segment] || cents <= 0)
            {
                return false;
            }
            long cash = b.Reserves - ReserveNeed(b);
            long risk = RiskWeighted(b) + cents * OwnBankCatalogue.LoanWeight[segment] / 1000;
            return cash >= cents && CapitalNow(b) * 1000 >= risk * OwnBankCatalogue.LendRatio;
        }

        /// <summary>
        /// One visit at a counter, in the bank's books, at today's price level: an account opened or a deposit into the city's
        /// people's savings with the bank, a withdrawal from them (never more than they hold or the bank's cash), or a loan to a
        /// kind of borrower if <see cref="CounterLends"/>. Its fee (at today's prices) is added to the counter's takings and
        /// given back in <paramref name="fee"/>, the sum that moved in <paramref name="moved"/>. Nothing happens at a bank that is not running.
        /// </summary>
        public static VisitResult Visit(OwnBank b, int kind, int segment, long cents, int level, out long fee, out long moved)
        {
            fee = 0;
            moved = 0;
            if (!b.Running)
            {
                return VisitResult.None;
            }
            moved = Math.Max(0, cents);
            switch (kind)
            {
                case BranchCatalogue.Open:
                case BranchCatalogue.Deposit:
                    if (moved <= 0)
                    {
                        moved = 0;
                        return VisitResult.None;
                    }
                    b.Deposits[OwnBankCatalogue.People] += moved;
                    b.Reserves += moved;
                    fee = At(kind == BranchCatalogue.Open ? BranchCatalogue.OpenFeeCents : BranchCatalogue.CounterFeeCents, level);
                    b.CounterFees += fee;
                    return VisitResult.PaidIn;
                case BranchCatalogue.Withdraw:
                    moved = Math.Min(moved, Math.Min(b.Deposits[OwnBankCatalogue.People], Math.Max(0, b.Reserves)));
                    if (moved <= 0)
                    {
                        moved = 0;
                        return VisitResult.None;
                    }
                    b.Deposits[OwnBankCatalogue.People] -= moved;
                    b.Reserves -= moved;
                    fee = At(BranchCatalogue.CounterFeeCents, level);
                    b.CounterFees += fee;
                    return VisitResult.TakenOut;
                case BranchCatalogue.Loan:
                    if (!CounterLends(b, segment, moved))
                    {
                        moved = 0;
                        return VisitResult.Refused;
                    }
                    b.Loans[segment] += moved;
                    b.Reserves -= moved;
                    fee = Math.Max(At(BranchCatalogue.LoanFeeLeastCents, level), moved * BranchCatalogue.LoanFee / 1000);
                    b.CounterFees += fee;
                    return VisitResult.Lent;
                default:
                    moved = 0;
                    return VisitResult.None;
            }
        }
    }
}
