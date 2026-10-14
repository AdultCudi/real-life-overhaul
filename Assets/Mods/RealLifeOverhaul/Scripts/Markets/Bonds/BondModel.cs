using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Why a bond cannot be bought, sold or changed now. Each has a sentence (reallife_mkt_bond_why_...).</summary>
    public enum BondWhy { None = 0, Closed, Held, Behind, Off, TooLittle, TooMuch, NoCash, Full, NotHeld, NoBank, Count }

    /// <summary>
    /// A bond the player holds (Markets/Bonds/BondRules). Money in cents: what it repays, what it cost, what it has paid.
    /// Its payments fall on fixed days from the day it was bought (BondRules.PayDay); what it pays each time is its face
    /// times its rate for the part of the year, less the tax withheld, and the part the player chose is put back into it.
    /// </summary>
    public sealed class BondHolding
    {
        public int Id;               // the player's own number for it (BondBook.NextId)
        public int Def;              // BondCatalogue's id
        public int Bank = -1;        // a certificate of deposit's bank, by its rank among the city's banks (BankRules); -1 for none
        public string Issuer = string.Empty;   // that bank's name when it was opened (a name, never translated)
        public long Face;            // cents repaid at the end of its term (grows as interest is put back into it)
        public long Cost;            // cents paid for it, interest put back included
        public int RateBp;           // its rate a year, on the face, fixed when it was bought
        public int BoughtDay, EndDay;
        public int Payments, Paid;   // payments of interest over its term, and how many are made
        public int Reinvest;         // per cent of each payment's interest after tax put back into it (BondCatalogue.Reinvest)
        public long Carry;           // cents paid out not yet a whole unit of money
        public long Received;        // cents of interest paid to the player
        public long Reinvested;      // cents of interest put back into it
        public long Taxed;           // cents of tax withheld
        public bool Test;            // bought by the test tools
        public bool Rollover;        // at its end it rolls over into a new one of the same kind and term at that day's yield (BondRules.Rolled), while bonds are on
    }

    /// <summary>Every bond the player holds, and the number the next one takes. Never in the city's line (MarketStore): each player's own.</summary>
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

        /// <summary>The player's own bonds brought into another market (a Going Public guest takes the host's: MarketShare.Adopt).</summary>
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

    /// <summary>
    /// What a bond did, for the director to tell: interest paid (A to the player, B put back, C the tax), the face repaid (A),
    /// or the bond rolled over at its end (Holding the new one: A the money rolled into it, B its rate in bp, C its end day).
    /// </summary>
    public enum BondNoteKind { Interest = 0, Repaid = 1, Rolled = 2 }

    public struct BondNote
    {
        public BondNoteKind Kind;
        public int Holding;
        public int Def;
        public long A, B, C;         // money
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
