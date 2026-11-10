using System.Collections.Generic;

namespace RealLifeOverhaul.Giving
{
    /// <summary>One charity as a page shows it: what it holds and has done, with every sum in today's whole money.</summary>
    public sealed class GiveCharityFact
    {
        public int Id;
        public long Pot, Cap, PaidMonth, PlayerMonth, PlayerYear, OthersYear, PaidYear, LastMoney, PlayerTotal;
        public int PeopleYear, LastPeople;
        public int Standing;        // today's whole money a month, 0 for none
        public bool StandingSkipped;
    }

    /// <summary>One loan as a page shows it.</summary>
    public sealed class GiveLoanFact
    {
        public string Name = string.Empty;
        public int Principal, Remaining, Paid, Part, Made, Next, Closed;
        public LoanState State;
        public int Misses;
    }

    /// <summary>Everything the Giving pages read, in plain data: the live source fills it from the director and the samples and checks fill it by hand.</summary>
    public sealed class GiveFacts
    {
        public bool Ready = true;            // giving runs for this save
        public int Why;                      // why a charity cannot work now: 0 it can, 1 the city's people are not running, 2 a Going Public session
        public long Cash, Spare;
        public int Level = 1000;
        public GiveCharityFact[] Charities = new GiveCharityFact[0];
        public readonly List<GiveLoanFact> Loans = new List<GiveLoanFact>(12);
        public long TaxGifts, TaxClaimed, TaxCap, Income;
        public int DeductionShare = GivingCatalogue.DeductionShare;
        public long NeighbourYear;
        public int NeighbourHelped;
        public long RivalsYear;
        public string RivalLine = string.Empty;   // the biggest rival gift this year, in words
        public int Month;
    }
}
