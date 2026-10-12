using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>One of the save's investors. Made once per save from its seed (<see cref="Cast"/>); only the two days change.</summary>
    public sealed class Investor
    {
        public int Id;
        public InvestorKind Kind;
        public Temperament Temper;
        public int First, Last;      // a person's name: indices into the catalogue's lists; a fund's name is First in its list
        public int Likes;            // the sectors they like: one bit per Sector
        public int ChequeLeast, ChequeMost;
        public int Diligence;        // how hard they check a figure, 0 to 100
        public int ClosedUntil;      // they see nobody before this day (an untrue figure was caught)
        public int PitchedDay;       // the day they were last approached; 0: never

        public string Name => Kind == InvestorKind.Firm
            ? VenturesCatalogue.FirmNames[First % VenturesCatalogue.FirmNames.Length]
            : VenturesCatalogue.FirstNames[First % VenturesCatalogue.FirstNames.Length] + " " + VenturesCatalogue.LastNames[Last % VenturesCatalogue.LastNames.Length];

        public bool LikesSector(Sector sector)
        {
            return (Likes & (1 << (int)sector)) != 0;
        }

        /// <summary>True if they may be approached today.</summary>
        public bool Open(int day)
        {
            return day >= ClosedUntil && (PitchedDay <= 0 || day - PitchedDay >= VenturesCatalogue.PitchCooldownDays);
        }

        /// <summary>The first day they may be approached again.</summary>
        public int OpenDay => PitchedDay > 0 && PitchedDay + VenturesCatalogue.PitchCooldownDays > ClosedUntil ? PitchedDay + VenturesCatalogue.PitchCooldownDays : ClosedUntil;
    }

    public static class OfferSource
    {
        public const int Inbound = 0, Pitch = 1, FollowOn = 2;
    }

    /// <summary>Terms on the table: money for a share of one business's profit. For a business not yet open, Place is empty and TypeName says which kind.</summary>
    public sealed class Offer
    {
        public int Id;
        public int InvestorId;
        public int Source;
        public string Place = string.Empty;      // the business: Books.Key of its address
        public string Business = string.Empty;   // its name when the offer was made
        public string TypeName = string.Empty;   // a new business: the game's business type
        public int Budget;                       // ...and what the player said it would cost
        public int Amount;
        public int ShareBp;
        public int TargetPct, TargetDays;
        public int MadeDay, LapseDay;
        public bool Countered;
        public bool Test;

        public bool ForNew => string.IsNullOrEmpty(Place);
    }

    /// <summary>Money promised for a business of one kind, paid when the player opens one.</summary>
    public sealed class Pledge
    {
        public int Id;
        public int InvestorId;
        public string TypeName = string.Empty;
        public int Budget;
        public int Amount;
        public int ShareBp;
        public int TargetPct, TargetDays;
        public int MadeDay, OpenBy;
        public int Had;              // how many businesses of that kind the player ran when the promise was made: one more opens the deal
        public bool Test;
    }

    /// <summary>Where a deal stands. The numbers are in saves.</summary>
    public enum DealStage
    {
        Running = 0,   // a target is set
        Demand = 1,    // the investor has made a demand and waits for an answer
        Cold = 2,      // the investor has lost interest: they take their share and set no more targets
        Called = 3,    // they want their money back: the share has stopped and a daily sum is owed
        Quiet = 4      // they have had their money back and a target was met: they take their share and set no more targets
    }

    /// <summary>An investor's stake in one business of the player's.</summary>
    public sealed class Deal
    {
        public int Id;
        public int InvestorId;
        public string Place = string.Empty;
        public string Business = string.Empty;
        public int Amount;           // what they put in
        public int ShareBp;          // of each day's profit
        public int StartDay;
        public int PaidOut;          // their share so far
        public int Arrears;          // share that could not be taken: taken first the next day
        public int BaseProfit;       // the recent daily profit the target was set from
        public int TargetProfit;     // the recent daily profit they expect...
        public int TargetDay;        // ...by this day; 0: none set
        public int Misses;           // targets missed running
        public int TargetsMet;
        public int Mood;             // 0 to 100
        public DealStage Stage;
        public int DemandDay;        // a demand must be answered by this day
        public int BoardUntil;       // a seat on the board: the larger share until this day
        public int CostTarget;       // a promise to cut costs: the average daily cost to be at or under...
        public int CostDay;          // ...on this day; 0: no promise
        public int CallOwed, CallPerDay, CallMisses;
        public int Claim;            // the court's number of a claim for what was called; 0: none
        public bool FollowOn;        // a second round is due at the next look
        public bool Test;

        /// <summary>The share taken today: more while the investor sits on the board.</summary>
        public int ShareNow(int day)
        {
            return BoardUntil > day ? ShareBp + VenturesCatalogue.BoardExtraBp : ShareBp;
        }
    }

    /// <summary>A company on the city's market.</summary>
    public sealed class Company
    {
        public int Id;
        public MarketSector Sector;
        public bool Rival;                       // one of the game's own rivals
        public int NameIndex;                    // an invented firm: which of its sector's two names
        public string RivalName = string.Empty;  // a rival: the name the game gives it
        public int PriceCents;
        public double FairCents;                 // what the company is worth: the price drifts about it
        public int VolBp;                        // its ordinary daily move
        public int YieldBp;                      // its dividend, of the price, each payout
        public int LastBp;                       // yesterday's move
        public int LastCause;                    // what moved it most (MarketCause), or 0
        public readonly int[] History = new int[VenturesCatalogue.HistoryDays];   // closing prices, oldest first
        public int HistoryCount;

        public string Name => Rival ? RivalName : VenturesCatalogue.CompanyNames[(int)Sector][NameIndex & 1];

        public void Remember(int priceCents)
        {
            if (HistoryCount < History.Length)
            {
                History[HistoryCount++] = priceCents;
                return;
            }
            for (int i = 1; i < History.Length; i++)
            {
                History[i - 1] = History[i];
            }
            History[History.Length - 1] = priceCents;
        }

        /// <summary>The move over the days on record, in basis points; 0 with fewer than two.</summary>
        public int TrendBp => HistoryCount < 2 || History[0] <= 0 ? 0 : (int)((long)(History[HistoryCount - 1] - History[0]) * 10000L / History[0]);
    }

    /// <summary>The player's shares in one company.</summary>
    public sealed class Holding
    {
        public int CompanyId;
        public int Shares;
        public int Cost;         // what they cost, commission included
        public int Dividends;    // received so far
        public bool Test;
    }

    /// <summary>A business as the rules see it: figures from the game's own daily statements, read by the director.</summary>
    public struct BusinessFigures
    {
        public bool Exists;          // the player still runs it
        public int DaysOnRecord;     // of the last fourteen, the days it has a statement for
        public bool HasYesterday;
        public float ProfitYesterday, SalesYesterday, SalesWeek;
        public float AvgProfit7, AvgProfit14, AvgCost14;
        public int ProfitableDays;   // of the last fourteen
        public int AgeDays;
        public Sector Sector;
    }

    /// <summary>
    /// Everything Ventures keeps of a save: one line of the save's data (<see cref="VenturesStore"/>). Nothing of it is
    /// ever written into the game's own business or property records.
    /// </summary>
    public sealed class VenturesFile
    {
        public int Seed;
        public int StartDay;
        public int Reputation = VenturesCatalogue.ReputationStart;   // with investors, 0 to 100
        public int NextId = 1;
        public int Pitches;              // meetings held: each one's checks are its own
        public int OfferDay;             // the last day the businesses were looked at for an offer
        public int SettledDay;           // the last day whose payouts were made
        public int MarketSteps;          // days the market has moved
        public int MarketDay;            // the last day it moved on
        public int DividendsTotal, PaidOutTotal, InvestedTotal;
        public int TargetsMet;
        public int LeversSeen = -1;      // the council's policies as last seen, packed; -1: not yet
        public int ElectionSeen = -1;    // the number of elections counted as last seen
        public float RateSeen = -1f;     // the bank rate as last seen
        public int Unseen;               // offers and demands the app has not shown yet
        public bool MarketMoved;         // the shares held and the companies have gone to the city's stock market (Scripts/Markets): nothing of a market is kept here any more
        public MarketBackup Backup;      // the market before the test tools moved it; never saved (the save keeps this, not a test's prices)

        public readonly List<Investor> Investors = new List<Investor>(VenturesCatalogue.InvestorsMost);
        public readonly List<Offer> Offers = new List<Offer>(VenturesCatalogue.OffersMost + 2);
        public readonly List<Pledge> Pledges = new List<Pledge>(VenturesCatalogue.PledgesMost);
        public readonly List<Deal> Deals = new List<Deal>(VenturesCatalogue.DealsMost);
        public readonly List<Company> Companies = new List<Company>(VenturesCatalogue.Companies);
        public readonly List<Holding> Holdings = new List<Holding>(VenturesCatalogue.Companies);

        public Investor Investor(int id)
        {
            for (int i = 0; i < Investors.Count; i++)
            {
                if (Investors[i].Id == id)
                {
                    return Investors[i];
                }
            }
            return null;
        }

        public Offer Offer(int id)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                if (Offers[i].Id == id)
                {
                    return Offers[i];
                }
            }
            return null;
        }

        public Deal Deal(int id)
        {
            for (int i = 0; i < Deals.Count; i++)
            {
                if (Deals[i].Id == id)
                {
                    return Deals[i];
                }
            }
            return null;
        }

        public Company Company(int id)
        {
            for (int i = 0; i < Companies.Count; i++)
            {
                if (Companies[i].Id == id)
                {
                    return Companies[i];
                }
            }
            return null;
        }

        public Holding Holding(int companyId)
        {
            for (int i = 0; i < Holdings.Count; i++)
            {
                if (Holdings[i].CompanyId == companyId)
                {
                    return Holdings[i];
                }
            }
            return null;
        }

        /// <summary>The share of a business's profit already promised, and how many deals are on it.</summary>
        public int ShareOn(string place, int day, out int deals)
        {
            int share = 0;
            deals = 0;
            for (int i = 0; i < Deals.Count; i++)
            {
                if (Deals[i].Place == place && Deals[i].Stage != DealStage.Called)
                {
                    share += Deals[i].ShareNow(day);
                    deals++;
                }
            }
            return share;
        }

        public bool HasDeal(string place, int investorId)
        {
            for (int i = 0; i < Deals.Count; i++)
            {
                if (Deals[i].Place == place && Deals[i].InvestorId == investorId)
                {
                    return true;
                }
            }
            return false;
        }

        public bool HasOffer(string place)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                if (Offers[i].Place == place)
                {
                    return true;
                }
            }
            return false;
        }

        public bool HasOfferFrom(int investorId)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                if (Offers[i].InvestorId == investorId)
                {
                    return true;
                }
            }
            return false;
        }

        public void MoveReputation(int by)
        {
            Reputation = Reputation + by < 0 ? 0 : Reputation + by > 100 ? 100 : Reputation + by;
        }
    }

    /// <summary>
    /// The city's economy as investors and the market feel it. The default says "steady"; the city's own economy
    /// (Scripts/Economy/City) is joined on in Scripts/Wiring with one line (see the README).
    /// </summary>
    public interface IVenturesClimate
    {
        /// <summary>-1 a slowdown, 0 steady, 1 a boom.</summary>
        int Phase { get; }
        /// <summary>The bank rate, in per cent a year.</summary>
        float BankRate { get; }
    }

    /// <summary>A steady city with a bank rate that never moves.</summary>
    public sealed class SteadyClimate : IVenturesClimate
    {
        public const float Rate = 5f;
        public static readonly SteadyClimate Instance = new SteadyClimate();

        public int Phase => 0;
        public float BankRate => Rate;
    }

    /// <summary>A climate read from two questions, so that whoever keeps the city's economy joins it on with one line.</summary>
    public sealed class ClimateFrom : IVenturesClimate
    {
        private readonly System.Func<int> _phase;
        private readonly System.Func<float> _rate;

        public ClimateFrom(System.Func<int> phase, System.Func<float> rate)
        {
            _phase = phase;
            _rate = rate;
        }

        public int Phase
        {
            get
            {
                int phase = _phase != null ? _phase() : 0;
                return phase < 0 ? -1 : phase > 0 ? 1 : 0;
            }
        }

        public float BankRate => _rate != null ? _rate() : SteadyClimate.Rate;
    }
}
