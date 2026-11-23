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
        public int StandingTag = -1;   // the district a standing gift to upkeep is for, else -1
    }

    /// <summary>One loan as a page shows it.</summary>
    public sealed class GiveLoanFact
    {
        public string Name = string.Empty;
        public int Principal, Remaining, Paid, Part, Made, Next, Closed;
        public LoanState State;
        public int Misses;
        public int Parts = GivingCatalogue.LoanParts;
        public int Extra;           // what a business loan adds to what is owed
    }

    /// <summary>One person the player has given to, lent to, helped or said no to, as the People tab shows them.</summary>
    public sealed class GivePersonFact
    {
        public string Name = string.Empty;
        public GivingNeed Need;     // what they are short of now, as the population holds it
        public long Given;          // whole money given in all
        public int Loan;            // what they still owe, 0 for none
        public int Dependence;      // how much they lean on the player's help, 0 to the most
        public int CoveredDays;     // days left of the rent the player paid for them, 0 for none
        public bool Cooling;        // helped lately, so they do not ask
        public bool Proud;          // they refused the first offer out of pride and may take it later
        public int Nos;             // times the player said no
    }

    /// <summary>A hall, a wing or a scholarship that carries the player's name: its words, the sum and the day.</summary>
    public sealed class GiveNamingFact
    {
        public string Words = string.Empty;
        public long Amount;
        public int Day;
    }

    /// <summary>Everything the Giving pages read, in plain data: the live source fills it from the director and the samples and checks fill it by hand.</summary>
    public sealed class GiveFacts
    {
        public bool Ready = true;            // giving runs for this save
        public int Why;                      // why a charity cannot work now: 0 it can, 1 the city's people are not running, 2 a Going Public session
        public long Cash, Spare;
        public int Level = 1000;
        public int PersonMost = GivingCatalogue.GiftsMost;   // the most a gift to a person may be today, whole money: it doubles with each wealth step the player has passed
        public GiveCharityFact[] Charities = new GiveCharityFact[0];
        public readonly List<GiveLoanFact> Loans = new List<GiveLoanFact>(12);
        public readonly List<GivePersonFact> People = new List<GivePersonFact>(12);
        public readonly List<GiveNamingFact> Namings = new List<GiveNamingFact>(4);
        public string[] Districts = new string[0];   // the districts a gift to upkeep can name, by the number the director takes
        public long[] DistrictTotals = new long[0];  // what the player has given to each district's upkeep in all
        public long TaxGifts, TaxClaimed, TaxCap, Income;
        public long Carried;                 // gifts over the cap of earlier years, still to be deducted
        public int DeductionShare = GivingCatalogue.DeductionShare;
        public long NeighbourYear;
        public int NeighbourHelped;
        public long RivalsYear;
        public string RivalLine = string.Empty;   // the biggest rival gift this year, in words
        public int Month;
    }
}
