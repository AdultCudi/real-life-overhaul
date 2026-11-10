using System.Collections.Generic;

namespace RealLifeOverhaul.Giving
{
    /// <summary>One charity's book: what it holds, what it was given and what it did with it. Money is whole money, in the dollars of the day it moved.</summary>
    public sealed class CharityBook
    {
        public long Pot;            // held, to be paid out under the month's cap
        public long PlayerMonth;    // given by the player this month
        public long OthersMonth;    // given by the rivals this month
        public long PlayerYear;     // given by the player this tax year
        public long OthersYear;     // ... by the rivals
        public long PaidMonth;      // paid out to the cause this month so far
        public long PaidYear;       // ... this tax year
        public int PeopleYear;      // households or people helped this tax year
        public long LastMoney;      // what the last month's pay-out was
        public int LastPeople;      // ... and how many it reached
        public int LastMonth = -1;  // the month it was
        public long PlayerTotal;    // given by the player in all
        public int StandingTold;    // the day a skipped standing gift was last said (0: none pending)
    }

    /// <summary>A standing monthly gift to a charity: its step at the start's prices, paid on the first day of each month.</summary>
    public sealed class StandingGift
    {
        public int Charity;
        public int Amount;       // whole money at the start's prices
        public int Since;        // the day it was set
        public bool Skipped;     // the last month could not be paid, and the player was told
        public long PaidTotal;
    }

    /// <summary>The state of a loan.</summary>
    public enum LoanState
    {
        Open = 0,
        Repaid = 1,
        WrittenOff = 2
    }

    /// <summary>Money lent to a person the player knows: no interest, paid back in parts from what their household can spare.</summary>
    public sealed class LoanOut
    {
        public int Person;         // the social cast's index
        public string Name = string.Empty;
        public int Principal;      // whole money, as lent
        public int Remaining;
        public int Made;           // the day
        public int Part;           // the most one month asks
        public int Misses;         // months in a row nothing was paid
        public int Next;           // the first day a part is next asked
        public int Paid;           // paid back so far
        public LoanState State;
        public int Closed;         // the day it was repaid or written off
    }

    /// <summary>What the player gave a person in one window, so many gifts earn no more than one: the gains applied so far are kept, and the curve's next value is paid as the difference.</summary>
    public sealed class PersonGiving
    {
        public int Person;
        public int Start;           // the first day of the window
        public int Given;           // at the start's prices, the window's gifts
        public int AffectionDone;   // tenths of a point applied
        public int TrustDone;
        public int AskDay;          // the last day they asked the player for help
        public int GivenTotal;      // whole money, in all
    }

    /// <summary>A rival's giving: their gifts in all and this tax year, and the latest.</summary>
    public sealed class RivalGiving
    {
        public long Total;
        public long Year;
        public int LastDay;
        public long Last;
        public int LastCharity;
        public int Month = -1;      // the last month a gift was made, so a gift is made once a month
    }

    /// <summary>
    /// Everything giving keeps for a save (reallife:giving). Game-free. Old saves have none of it and start with nothing given.
    /// </summary>
    public sealed class GivingState
    {
        public const int Rivals = 3;

        public int Seed;
        public int LastDay;             // the day last gone through
        public int Month = -1;          // the month last gone through (the day over the month's length)
        public int PeriodStart;         // the first day of the game's tax year the gifts below are for
        public long TaxGifts;           // gifts to charities this tax year, whole money
        public long TaxClaimed;         // ... of which the game's tax already deducts
        public int StandingMonth = -1;  // the month the standing points below were counted for
        public long MonthGiven;         // the player's gifts to charities this month, at the start's prices
        public int StandingDone;        // points of integrity applied this month
        public int PressDone;           // tenths of recognition applied this month
        public int LastAsk;             // the last day anybody asked the player for help
        public int NeighbourMonth;      // the last month the people helped one another
        public long NeighbourMoney;     // what they moved that month
        public long NeighbourYear;
        public int NeighbourHelped;     // households helped by neighbours this tax year
        public int PaperDay;            // the last day the paper was written for
        public readonly CharityBook[] Books = new CharityBook[(int)Charity.Count];
        public readonly List<StandingGift> Standing = new List<StandingGift>(GivingCatalogue.StandingMost);
        public readonly List<LoanOut> Loans = new List<LoanOut>(GivingCatalogue.LoansMost + GivingCatalogue.LoanKept);
        public readonly List<PersonGiving> People = new List<PersonGiving>(16);
        public readonly RivalGiving[] RivalBooks = new RivalGiving[Rivals];
        public readonly List<GivingNote> Notes = new List<GivingNote>(8);   // gifts the paper has still to print

        public GivingState()
        {
            for (int i = 0; i < Books.Length; i++)
            {
                Books[i] = new CharityBook();
            }
            for (int i = 0; i < RivalBooks.Length; i++)
            {
                RivalBooks[i] = new RivalGiving();
            }
        }

        public CharityBook Book(Charity c) => Books[GivingCatalogue.Clamp((int)c, 0, Books.Length - 1)];

        public StandingGift StandingOf(int charity)
        {
            for (int i = 0; i < Standing.Count; i++)
            {
                if (Standing[i].Charity == charity)
                {
                    return Standing[i];
                }
            }
            return null;
        }

        /// <summary>The person's giving record, made when asked to.</summary>
        public PersonGiving PersonOf(int person, bool make)
        {
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Person == person)
                {
                    return People[i];
                }
            }
            if (!make)
            {
                return null;
            }
            var g = new PersonGiving { Person = person };
            People.Add(g);
            return g;
        }

        public int OpenLoans()
        {
            int n = 0;
            for (int i = 0; i < Loans.Count; i++)
            {
                n += Loans[i].State == LoanState.Open ? 1 : 0;
            }
            return n;
        }

        public LoanOut LoanOf(int person)
        {
            for (int i = 0; i < Loans.Count; i++)
            {
                if (Loans[i].Person == person && Loans[i].State == LoanState.Open)
                {
                    return Loans[i];
                }
            }
            return null;
        }

        public long OutstandingLoans()
        {
            long sum = 0;
            for (int i = 0; i < Loans.Count; i++)
            {
                sum += Loans[i].State == LoanState.Open ? Loans[i].Remaining : 0;
            }
            return sum;
        }
    }

    /// <summary>A gift the paper may print: who gave, to which charity, how much, on which day.</summary>
    public sealed class GivingNote
    {
        public int Day;
        public int Charity;
        public long Amount;       // whole money
        public int Giver;         // -1 the player, 0.. a rival's index
    }
}
