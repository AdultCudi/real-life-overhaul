using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>One of the save's investors, made once from its seed (<see cref="Cast"/>); only the two days change.</summary>
    public sealed class Investor
    {
        public int Id;
        public InvestorKind Kind;
        public Temperament Temper;
        public int First, Last;      // indices into the catalogue's name lists; a fund's name is First in its list
        public int Likes;            // a bit per Sector
        public int ChequeLeast, ChequeMost;
        public int Diligence;        // how hard they check a figure, 0 to 100
        public int ClosedUntil;      // they see nobody before this day
        public int PitchedDay;       // last approached; 0: never

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

    /// <summary>How an offer came about. Saved as a number.</summary>
    public static class OfferSource
    {
        public const int Inbound = 0, Pitch = 1, FollowOn = 2;
    }

    /// <summary>Money on the table for a share of one business's profit. For a business not yet open, Place is empty and TypeName says which kind.</summary>
    public sealed class Offer
    {
        public int Id;
        public int InvestorId;
        public int Source;
        public string Place = string.Empty;      // Books.Key of the business's address
        public string Business = string.Empty;   // its name when the offer was made
        public string TypeName = string.Empty;   // a new business: the game's business type...
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
        public int Had;              // businesses of the kind the player ran at the promise: one more opens the deal
        public bool Test;
    }

    /// <summary>Where a deal stands. Saved as a number.</summary>
    public enum DealStage
    {
        Running = 0,   // a target is set
        Demand = 1,    // the investor waits for an answer to a demand
        Cold = 2,      // lost interest: they take their share and set no more targets
        Called = 3,    // they want their money back: no share, a daily sum owed
        Quiet = 4      // paid back and a target met: they take their share and set no more targets
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
        public int PaidOut;
        public int Arrears;          // share that could not be taken: taken first the next day
        public int BaseProfit;       // the recent daily profit the target was set from
        public int TargetProfit;     // the recent daily profit they expect...
        public int TargetDay;        // ...by this day; 0: none
        public int Misses;           // targets missed running
        public int TargetsMet;
        public int Mood;             // 0 to 100
        public DealStage Stage;
        public int DemandDay;        // a demand must be answered by this day
        public int BoardUntil;       // the larger share of a board seat until this day
        public int CostTarget;       // a promise: average daily costs at or under this...
        public int CostDay;          // ...on this day; 0: no promise
        public int CallOwed, CallPerDay, CallMisses;
        public int Claim;            // the court's number of a claim for what was called; 0: none
        public bool FollowOn;        // a second round is due at the next look
        public bool Test;

        /// <summary>The share taken of a day's profit: more while the investor sits on the board.</summary>
        public int ShareNow(int day)
        {
            return BoardUntil > day ? ShareBp + VenturesCatalogue.BoardExtraBp : ShareBp;
        }
    }

    /// <summary>A company on the old market, kept for the save and its carry-over.</summary>
    public sealed class Company
    {
        public int Id;
        public MarketSector Sector;
        public bool Rival;                       // one of the game's own rivals
        public int NameIndex;                    // an invented firm: which of its sector's two names
        public string RivalName = string.Empty;
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
    }

    /// <summary>The player's shares in one company.</summary>
    public sealed class Holding
    {
        public int CompanyId;
        public int Shares;
        public int Cost;         // commission included
        public int Dividends;    // received so far
        public bool Test;
    }

    /// <summary>A business as the rules see it, from the game's own daily statements.</summary>
    public struct BusinessFigures
    {
        public bool Exists;          // the player still runs it
        public int DaysOnRecord;     // of the last fourteen, days with a statement
        public bool HasYesterday;
        public float ProfitYesterday, SalesYesterday, SalesWeek;
        public float AvgProfit7, AvgProfit14, AvgCost14;
        public int ProfitableDays;   // of the last fourteen
        public int AgeDays;
        public Sector Sector;
    }

    /// <summary>Everything Ventures keeps of a save, in one line of its data (<see cref="VenturesStore"/>).</summary>
    public sealed class VenturesFile
    {
        public int Seed;
        public int StartDay;
        public int Reputation = VenturesCatalogue.ReputationStart;   // with investors, 0 to 100
        public int NextId = 1;
        public int Pitches;              // meetings held: each one's rolls are its own
        public int OfferDay;             // the last look at the businesses for an offer
        public int SettledDay;           // the last day whose payouts were made
        public int MarketSteps;          // days the market has moved
        public int MarketDay;            // the last day it moved on
        public int DividendsTotal, PaidOutTotal, InvestedTotal;
        public int TargetsMet;
        public int LeversSeen = -1;      // the council's policies as last seen, packed; -1: not yet
        public int ElectionSeen = -1;    // elections counted as last seen
        public float RateSeen = -1f;     // the bank rate as last seen
        public int Unseen;               // offers and demands the app has not shown yet
        public bool MarketMoved;         // the shares and companies have gone to the city's stock market (Scripts/Markets)
        public MarketBackup Backup;      // the market before a test moved it; the save keeps this instead

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

        /// <summary>The share of a business's profit already promised (a called deal takes none), and how many deals take it.</summary>
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

    /// <summary>The city's economy as investors feel it. Wiring/VenturesLinks joins the city's own on.</summary>
    public interface IVenturesClimate
    {
        /// <summary>-1 a slowdown, 0 steady, 1 a boom.</summary>
        int Phase { get; }
        /// <summary>The bank rate, in per cent a year.</summary>
        float BankRate { get; }
    }

    /// <summary>A steady city with a bank rate that never moves: the climate while nothing is joined on.</summary>
    public sealed class SteadyClimate : IVenturesClimate
    {
        public const float Rate = 5f;   // per cent a year
        public static readonly SteadyClimate Instance = new SteadyClimate();

        public int Phase => 0;
        public float BankRate => Rate;
    }

    /// <summary>A climate read from two questions, so the city's economy is joined on with one line.</summary>
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
