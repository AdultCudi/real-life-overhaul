using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Why a bond cannot be bought, sold or changed now. Each has a sentence (reallife_mkt_bond_why_...).</summary>
    public enum BondWhy { None = 0, Closed, Held, Behind, Off, TooLittle, TooMuch, NoCash, Full, NotHeld, NoBank, Count }

    /// <summary>A bond the player holds. Money in cents; its payments fall on fixed days from the day it was bought.</summary>
    public sealed class BondHolding
    {
        public int Id;               // the player's own number for it (BondBook.NextId)
        public int Def;              // BondCatalogue's id
        public int Bank = -1;        // a deposit's bank by its rank among the city's banks; -1 for none
        public string Issuer = string.Empty;   // that bank's name when it was opened, never translated
        public long Face;            // repaid at the end; grows as interest is put back
        public long Cost;            // paid for it, interest put back included
        public int RateBp;           // a year, on the face, fixed when bought
        public int BoughtDay, EndDay;
        public int Payments, Paid;   // payments of interest over its term, and how many are made
        public int Reinvest;         // per cent of each payment's interest after tax put back into it
        public long Carry;           // paid out, not yet a whole unit of money
        public long Received;
        public long Reinvested;
        public long Taxed;
        public bool Test;            // bought by the test tools
        public bool Rollover;        // bought again at its end, same kind and term, at that day's yield
    }

    /// <summary>Every bond the player holds. Each player's own: never in the city's shared line.</summary>
    public sealed class BondBook
    {
        public readonly List<BondHolding> Holdings = new List<BondHolding>(8);
        public int NextId = 1;

        public BondHolding Holding(int id)
        {
            for (int i = 0; i < Holdings.Count; i++)
            {
                if (Holdings[i].Id == id)
                {
                    return Holdings[i];
                }
            }
            return null;
        }

        /// <summary>The player's own bonds brought into another market (a Going Public guest takes the host's).</summary>
        public void Take(BondBook from)
        {
            if (from == null || ReferenceEquals(from, this))
            {
                return;
            }
            Holdings.AddRange(from.Holdings);
            NextId = from.NextId > NextId ? from.NextId : NextId;
        }
    }

    /// <summary>What a <see cref="BondNote"/> tells.</summary>
    public enum BondNoteKind { Interest = 0, Repaid = 1, Rolled = 2 }

    /// <summary>
    /// What a bond did, for the director to tell, money in cents. Interest: A paid, B put back, C the tax. Repaid: A the
    /// face. Rolled: Holding is the new bond, A the money rolled into it, B its rate in bp, C its end day.
    /// </summary>
    public struct BondNote
    {
        public BondNoteKind Kind;
        public int Holding;
        public int Def;
        public long A, B, C;
    }

    /// <summary>A bond on offer today: what it is, at which bank (a deposit), and its yield a year.</summary>
    public struct BondOffer
    {
        public BondDef Def;
        public int Bank;             // -1 for none
        public string Issuer;
        public int YieldBp;
    }
}
