using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// One account of the mod's own: an overdraft, a card, a vehicle or a building on finance, or a game loan's late fees.
    /// Saved in the credit file's line (part "a"); <see cref="AccountRules"/> has its arithmetic.
    /// </summary>
    public sealed class CreditAccount : ICreditAccount
    {
        public int Id;                  // unique in its file, never reused
        public ProductKind Kind;
        public int Owed;
        public int Agreed;              // the limit (overdraft, card) or the amount financed (car, building)
        public int RateBp;              // hundredths of a per cent a year, as agreed (the penalty rate is not in it)
        public long Carry;              // interest not yet a whole unit of money: nothing is lost to rounding
        public int OpenedDay;
        public int Term;                // days, of an instalment account
        public int Instalment;          // its daily repayment, before interest
        public int NextDay;             // card: the next statement day
        public int DueDay;              // card: the day the statement's minimum is taken; 0 for no statement open
        public int StatementLeft;       // card: of the last statement, still unpaid
        public int MinimumLeft;         // card: of its minimum, still unpaid
        public bool Charged;            // card: this statement's interest has been charged
        public string Security = string.Empty;      // a vehicle's id, or a building's street and number as "street#number"
        public string SecurityName = string.Empty;  // the vehicle's type or the building's address, for the app
        public int SecurityValue;       // the price it was bought for
        public bool Secured;            // false once the security is sold, lost or taken
        public string Street = string.Empty;        // the lending bank's address, as the credit file writes one
        public int Number;
        public bool Frozen;             // in a debt arrangement: no interest, no payment due, no steps; it can still be paid by hand
        public bool Closed;
        public bool Test;               // made by a test tool: "Test: put everything back" removes it
        public readonly LadderState Ladder = new LadderState();

        public string Key => ProductCatalogue.KindSources[(int)Kind];
        public string NameKey => ProductCatalogue.KindKeys[(int)Kind];
        public float Balance => Closed ? 0f : Owed;
        public float Limit => Closed ? 0f : Agreed;
        public float PaymentDue => Closed || Frozen ? 0f : AccountRules.DueTomorrow(this, CreditLedger.Today);

        public int Free => Closed || !ProductCatalogue.Revolving(Kind) ? 0 : Math.Max(0, Agreed - Owed);

        /// <summary>The rate as agreed, with the central bank's of the day on an overdraft or a card.</summary>
        public int RateAgreedNow => ProductCatalogue.Revolving(Kind) ? ProductTerms.WithBase(RateBp, AccountRules.BaseBp, AccountRules.BaseMultiplier) : RateBp;

        /// <summary>The rate charged now, the penalty rate included.</summary>
        public int RateNow => RateAgreedNow + (Ladder.Penalty ? ProductCatalogue.PenaltyPoints * 100 : 0);
    }
}
