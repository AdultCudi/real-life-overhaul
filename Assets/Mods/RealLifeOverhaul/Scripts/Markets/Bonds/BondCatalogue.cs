using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Who stands behind a bond. Saved as a number: never renumber.</summary>
    public enum BondKind { Treasury = 0, City = 1, Deposit = 2 }

    /// <summary>One kind of bond the Stocks app offers: who issues it, for how long, and how often it pays. Never changed in play.</summary>
    public sealed class BondDef
    {
        public readonly int Id;                 // saved: never reuse or renumber
        public readonly BondKind Kind;
        public readonly int TermQuarters;       // quarters of the game's year until it is repaid
        public readonly int PaysPerYear;        // payments of interest a year

        public BondDef(int id, BondKind kind, int termQuarters, int paysPerYear)
        {
            Id = id;
            Kind = kind;
            TermQuarters = termQuarters;
            PaysPerYear = paysPerYear;
        }

        /// <summary>Its payments of interest over its whole term (the last with the repayment).</summary>
        public int Payments => Math.Max(1, TermQuarters * PaysPerYear / 4);
    }

    /// <summary>
    /// EVERY FIGURE OF THE BONDS (Markets/Bonds/BondRules), and the bonds there are. Game-free: the offline checks
    /// compile this file and the Guide prints from it.
    ///
    /// Three kinds, each paid in the game's money on a schedule and repaid in full at the end of its term:
    ///   the Treasury's bonds   the city's own debt: the yield of the city's rates for the term
    ///   city bonds             the council's: a little more for a little more risk
    ///   certificates of deposit at one of the city's three largest banks: the Treasury's yield less the bank's margin,
    ///                          a quarter point more at a bank whose loans are a point dearer (Economy/BankRules); never sold,
    ///                          only closed early for a few months' interest
    /// Terms and rates are a year of the game's (60 to 365 days, as the save was started). Rates in basis points (bp),
    /// hundredths of a per cent; prices for each 100 of what is repaid. One purchase is at most the larger of a flat sum and a
    /// share of what the player is worth, up to a cap (BondRules.Most); a ladder spreads one sum evenly over the Treasury's
    /// terms, a holding each (BondRules.Ladder); a bond set to roll over is bought again at its end, the same kind and term at
    /// that day's yield (BondRules.Rolled).
    /// </summary>
    public static class BondCatalogue
    {
        // ---------------------------------------------------------------- the yields

        public const int NeutralBp = 300;                // the bank rate the long run comes back to (as MarketValuation.NeutralRate)
        public const double ShortYears = 2.0;            // today's bank rate counts for a term less the longer it is: e^(-term / this)
        public const int TermPremiumBpYear = 25;         // a longer loan is paid a little more: this for each year of its term...
        public const int TermPremiumMostBp = 100;        // ...and no more than this
        public const int CitySpreadBp = 60;              // a city bond over the Treasury's yield
        public const int DepositUnderBp = 25;            // a certificate of deposit under the Treasury's yield (the bank's margin)...
        public const int DepositPerPointBp = 25;         // ...and this more for each point a bank's loans are dearer (BankRules' profile)
        public const int YieldLeastBp = 10;              // no bond is offered at less than this a year

        // ---------------------------------------------------------------- buying and selling

        public const int LeastAtStart = 500;             // the least that can be put into a bond, at the start's prices (Prices.At)
        public const int MostAtStart = 2000000;          // the most in one purchase, at the start's prices: for a player worth more, a share of it (BondRules.Most)...
        public const int MostWorthPct = 25;              // ...this share of what they are worth (the game's own figure with what they hold on the market), with "Worked orders and larger bond purchases" on...
        public const int MostCapAtStart = 250000000;     // ...and never more than this, at the start's prices: a hundredth of a large company's turnover, about the most a city's issue sells to one buyer
        public const int HoldingsMost = 24;              // bonds held at once...
        public const int HoldingsMostLarge = 48;         // ...and with "Worked orders and larger bond purchases" on: room for ladders of four (a version before it reads up to twice its own 24, so none is lost going back)
        public const int SellSpreadTreasuryBp = 10;      // a Treasury bond sold before its end goes for its price less this...
        public const int SellSpreadCityBp = 30;          // ...a city bond for this less
        public const int DepositPenaltyMonths = 3;       // a deposit closed early is repaid less this many months' interest on it
        public static readonly int[] Reinvest = { 0, 25, 50, 75, 100 };   // per cent of each payment's interest, after tax, put back into the bond

        // ---------------------------------------------------------------- the bonds

        /// <summary>Every bond there is. A certificate of deposit is offered at each of the city's three largest banks.</summary>
        public static readonly BondDef[] All =
        {
            new BondDef(1, BondKind.Treasury, 1, 4),     // three months, paid at the end
            new BondDef(2, BondKind.Treasury, 4, 4),     // a year, every quarter
            new BondDef(3, BondKind.Treasury, 8, 4),     // two years
            new BondDef(4, BondKind.Treasury, 20, 4),    // five years
            new BondDef(11, BondKind.City, 4, 12),       // a year, every month
            new BondDef(12, BondKind.City, 12, 12),      // three years
            new BondDef(21, BondKind.Deposit, 1, 12),    // three months at a bank, every month
            new BondDef(22, BondKind.Deposit, 4, 12)     // a year at a bank
        };

        public const int BanksMost = 3;                  // certificates of deposit at the city's three largest banks (BankRules.AppsMost)

        /// <summary>A bond by its id; null for none.</summary>
        public static BondDef Find(int id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }
    }
}
