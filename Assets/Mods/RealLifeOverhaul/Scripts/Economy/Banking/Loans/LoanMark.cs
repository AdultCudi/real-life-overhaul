using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One of the game's loans as the credit file last saw it, and what the mod keeps about it; the game's Loan has no id (<see cref="LoanBook"/>).</summary>
    public sealed class LoanMark
    {
        public string Street;
        public int Number;
        public float Total;
        public int FirstDay;
        public float Remaining;
        public int Payment, Interest;

        public readonly LadderState Ladder = new LadderState();
        public int Held;            // bits of LoanHold: what the mod has changed on the loan in memory
        public int OwnInterest;     // the loan's own daily interest, kept while Held is not nothing
        public int OwnPayment;      // and its own daily payment
        public int Purpose;         // LoanPurpose
        public string Security = string.Empty;   // a business loan: the business's street and number as "street#number"

        [NonSerialized] public object Ref;
        [NonSerialized] internal bool Matched;   // during one comparison: its loan is in the game's list
        [NonSerialized] internal bool Gone;      // during one comparison: its loan has left the game's list
    }

    /// <summary>What the mod has changed on one of the game's loans in memory. Every bit is undone before the game writes a save.</summary>
    public static class LoanHold
    {
        public const int None = 0;
        public const int Penalty = 1;   // its daily interest carries the penalty points
        public const int Frozen = 2;    // in a debt arrangement: no interest and no payment
    }

    /// <summary>What one of the game's loans was taken for, where the mod made it.</summary>
    public static class LoanPurpose
    {
        public const int Plain = 0, Business = 1, Consolidation = 2;
    }

    /// <summary>A loan as it stands in the game now, read for one comparison.</summary>
    public struct LoanView
    {
        public string Street;
        public int Number;
        public float Total, Remaining;
        public int Payment, Interest;
        public object Ref;
    }
}
