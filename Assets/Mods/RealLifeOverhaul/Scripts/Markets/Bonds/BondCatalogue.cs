using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Who stands behind a bond. Saved as a number: never renumber.</summary>
    public enum BondKind { Treasury = 0, City = 1, Deposit = 2 }

    /// <summary>One kind of bond the Stocks app offers: who issues it, for how long, and how often it pays.</summary>
    public sealed class BondDef
    {
        public readonly int Id;                 // saved: never reuse or renumber
        public readonly BondKind Kind;
        public readonly int TermQuarters;       // quarters of the game's year until it is repaid
        public readonly int PaysPerYear;

        public BondDef(int id, BondKind kind, int termQuarters, int paysPerYear)
        {
            Id = id;
            Kind = kind;
            TermQuarters = termQuarters;
            PaysPerYear = paysPerYear;
        }

        /// <summary>Its payments of interest over its whole term, the last with the repayment.</summary>
        public int Payments => Math.Max(1, TermQuarters * PaysPerYear / 4);
    }

    /// <summary>
    /// Every figure of the bonds, and the bonds there are. Game-free: the offline checks compile it and the Guide prints
    /// from it. Rates in basis points (hundredths of a per cent), prices for each 100 repaid, terms in the game's year.
    /// </summary>
    public static class BondCatalogue
    {
        public const int NeutralBp = 300;                // the bank rate the long run comes back to (MarketValuation.NeutralRate)
        public const double ShortYears = 2.0;            // years: today's bank rate counts for a term by e^(-term / this)
        public const int TermPremiumBpYear = 25;         // bp more for each year of term...
        public const int TermPremiumMostBp = 100;        // ...up to this
        public const int CitySpreadBp = 60;              // bp of a city bond over the Treasury
        public const int DepositUnderBp = 25;            // bp of a deposit under the Treasury (the bank's margin)...
        public const int DepositPerPointBp = 25;         // ...and this more for each point a bank's loans are dearer
        public const int YieldLeastBp = 10;              // bp a year

        public const int LeastAtStart = 500;             // money at the start's prices (Prices.At)
        public const int MostAtStart = 2000000;          // the most in one purchase, at the start's prices...
        public const int MostWorthPct = 25;              // ...or this per cent of the player's worth with the large option on...
        public const int MostCapAtStart = 250000000;     // ...never over this, at the start's prices
        public const int HoldingsMost = 24;              // bonds held at once...
        public const int HoldingsMostLarge = 48;         // ...with the large option on: room for ladders of four
        public const int SellSpreadTreasuryBp = 10;      // bp off the price of a Treasury bond sold early
        public const int SellSpreadCityBp = 30;          // bp off the price of a city bond sold early
        public const int DepositPenaltyMonths = 3;       // months of interest kept when a deposit is closed early
        public static readonly int[] Reinvest = { 0, 25, 50, 75, 100 };   // per cent of each payment's interest after tax

        /// <summary>Every bond there is. A certificate of deposit is offered at each of the city's three largest banks.</summary>
        public static readonly BondDef[] All =
        {
            new BondDef(1, BondKind.Treasury, 1, 4),     // three months, paid at the end
            new BondDef(2, BondKind.Treasury, 4, 4),
            new BondDef(3, BondKind.Treasury, 8, 4),
            new BondDef(4, BondKind.Treasury, 20, 4),
            new BondDef(11, BondKind.City, 4, 12),
            new BondDef(12, BondKind.City, 12, 12),
            new BondDef(21, BondKind.Deposit, 1, 12),
            new BondDef(22, BondKind.Deposit, 4, 12)
        };

        public const int BanksMost = 3;                  // as BankRules.AppsMost

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
