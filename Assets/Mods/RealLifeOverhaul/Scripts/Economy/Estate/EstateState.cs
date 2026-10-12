using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Somebody who would take a building: what they offer a day, how good their record is, and until when they wait.</summary>
    public sealed class RentApplicant
    {
        public int Id;
        public int Kind;        // which description of its use's eight (EstateText)
        public int Offer;       // dollars a day
        public int Record;      // 20 to 95: how reliably they have paid before
        public int Term;        // days the lease would run
        public int Lapse;       // the last day the application stands
    }

    /// <summary>
    /// One building the player owns, as the property business keeps it. The fields from <see cref="Base"/> down to
    /// <see cref="Name"/> are read from the game each morning and are not saved.
    /// </summary>
    public sealed class EstateHolding
    {
        public string Place;            // the address, written as business pressure writes a place
        public int Paid, BoughtDay;     // as the game recorded the purchase
        public int Spent;               // refurbishment and conversion paid for: counted against a gain
        public int Use;                 // EstateUse: what it is let as
        public int Cond = EstateCatalogue.ConditionNew;
        public int Level;               // refurbishment, 0 to 3
        public int Ask = EstateCatalogue.AskStart;   // the asking rent against the market's
        public bool Managed;            // the player lets it himself; false: the game lets it, as it always did
        public bool ToLet = true;       // applicants are looked for while it stands empty
        public bool Cover;              // landlord's cover

        public int Tenant = -1;         // the description of who is in; -1 for nobody
        public int Record;
        public int Rent;                // dollars a day, as agreed
        public int Deposit;             // held
        public int Until;               // the lease's last day
        public int Arrears, ArrearsDays;
        public int NoticeEnd;           // the day notice runs out; 0 for none
        public int CourtDay;            // the day the possession order comes; 0 for none
        public int VoidDays;            // days empty in a row

        public int Work;                // EstateWork
        public int WorkEnd, WorkCost, WorkTo;
        public int PlanUse = -1;        // a use the council has allowed and that is not built yet

        public int List;                // the asking price; 0 while it is not for sale
        public int Offer, OfferLapse;   // a buyer's offer and the last day it stands
        public int SaleDay, SalePrice;  // an agreed sale and the day it completes
        public bool SaleFails;          // ...which was settled when it was agreed, and is found out on the day

        public int RentYear, CostYear;  // since the first day of the running year of the game
        public int NextApplicant = 1;
        public readonly List<RentApplicant> Applicants = new List<RentApplicant>(EstateCatalogue.ApplicantsMost);

        // read from the game, not saved
        public int Base;                // the game's own value of it, before anything of the mod's
        public int AsBuilt;             // the use its building type makes it
        public int Income = EstateCatalogue.IncomeMiddle;
        public string District;
        public string Name = string.Empty;
        public bool Self;               // the player uses its unit: the game lets the rest, as it always did
        public bool Seen;

        public bool Let => Tenant >= 0;
        public bool Busy => Work != (int)EstateWork.None;

        public RentApplicant ApplicantOf(int id)
        {
            for (int i = 0; i < Applicants.Count; i++)
            {
                if (Applicants[i].Id == id)
                {
                    return Applicants[i];
                }
            }
            return null;
        }
    }

    /// <summary>What a district thinks of the agency.</summary>
    public sealed class DistrictRep
    {
        public string Name;     // the game's own name for the neighbourhood
        public int Rep = EstateCatalogue.ReputationStart;
        public int SqmPrice;    // what a square metre goes for there by the game's own figures; read from the game, not saved
    }

    /// <summary>Another owner's building or unit that the agency sells or lets.</summary>
    public sealed class Instruction
    {
        public int Id;
        public string District;
        public bool Sale;       // false: a letting
        public int Sqm;
        public int Value;       // what it is worth
        public int Ask;         // what the owner wants, thousandths of that
        public int Stage;       // InstructionStage
        public int Hours;       // an agent's hours it takes, in tenths
        public int Done;        // ...spent so far
        public int Day;         // answer by; then lapses on; then completes on
        public int Price;       // the deal agreed
        public bool Fails;      // settled when the deal was agreed, found out on the day
    }

    /// <summary>The estate agency: where it is run from, what the districts think of it and what it has in hand.</summary>
    public sealed class AgencyFile
    {
        public string Office = string.Empty;   // the place of the office; empty while there is no agency
        public int NextId = 1;
        public int Commission, Deals, Lost;    // in all
        public readonly List<DistrictRep> Districts = new List<DistrictRep>(16);
        public readonly List<Instruction> Open = new List<Instruction>(EstateCatalogue.InstructionsMost);

        public int Capacity;                   // agent's hours a day, tenths; read from the game each morning, not saved
        public int Negotiators;

        public bool Running => Office.Length > 0;

        public DistrictRep District(string name)
        {
            for (int i = 0; name != null && i < Districts.Count; i++)
            {
                if (Districts[i].Name == name)
                {
                    return Districts[i];
                }
            }
            return null;
        }

        public Instruction Find(int id)
        {
            for (int i = 0; i < Open.Count; i++)
            {
                if (Open[i].Id == id)
                {
                    return Open[i];
                }
            }
            return null;
        }
    }

    /// <summary>A hotel of the city's that the player holds as a business.</summary>
    public sealed class HotelFile
    {
        public string Slot;             // the hotel's own id (Travel)
        public bool Leased;
        public int Paid, Day;           // the price or the lease's deposit, and when
        public int LeaseDaily;          // dollars a day while leased
        public readonly int[] Rate = { EstateCatalogue.Thousand, EstateCatalogue.Thousand };   // against the going rate
        public int Staff = 1;           // the staffing contract: 0 basic, 1 standard, 2 excellent
        public int Clean = 700, Rating = EstateCatalogue.RatingStart;
        public int PushEnd;             // the last day of a campaign; 0 for none
        public int GroupRooms, GroupNights, GroupRate;   // a group that is staying: rooms, nights left, dollars a room
        public int AskRooms, AskNights, AskRate, AskLapse;   // a group that asks
        public int OutRooms, OutEnd;    // rooms out of use and the last night they are
        public int InspectDay;          // the next inspection
        public readonly int[] Reviews = new int[EstateCatalogue.ReviewsKept];   // newest first; 0 for none
        public readonly int[] Sold = new int[EstateCatalogue.RoomTypes];        // last night
        public int Takings, Costs;      // last night
        public int TakingsYear, CostsYear;

        // read from the game's hotel data, not saved
        public int Tier;
        public int Income = EstateCatalogue.IncomeMiddle;
        public readonly int[] Going = new int[EstateCatalogue.RoomTypes];   // tonight's going rate of each type
        public string Name = string.Empty;
        public bool Seen;
    }

    /// <summary>A car of the player's that is on hire.</summary>
    public sealed class HireCar
    {
        public string Id;               // the game's vehicle instance id
        public string Type = string.Empty;   // the game's name for the kind of vehicle
        public int Price;               // its list price
        public int Rate = EstateCatalogue.Thousand;   // against the going rate
        public int State;               // HireState
        public int Day;                 // out: the day it is due back
        public int HireDays, HireRate, Deposit, HireSeed;
        public int X, Y, Z, Yaw;        // where it stood when it went out: hundredths of a metre, tenths of a degree
        public int Takings, Hires;
    }

    /// <summary>The hire business: its lot, its cars, what customers think of it.</summary>
    public sealed class HireFile
    {
        public string Lot = string.Empty;
        public int Rating = EstateCatalogue.HireRatingStart;
        public bool Cover;
        public int TakingsYear, CostsYear;
        public readonly List<HireCar> Cars = new List<HireCar>(EstateCatalogue.HireCarsMost);

        public int Spaces;              // read from the game, not saved

        public bool Running => Lot.Length > 0;

        public HireCar Car(string id)
        {
            for (int i = 0; id != null && i < Cars.Count; i++)
            {
                if (Cars[i].Id == id)
                {
                    return Cars[i];
                }
            }
            return null;
        }

        public int Count(HireState state)
        {
            int count = 0;
            for (int i = 0; i < Cars.Count; i++)
            {
                count += Cars[i].State == (int)state ? 1 : 0;
            }
            return count;
        }
    }

    /// <summary>Everything the property business keeps for one save. Game-free; written by <see cref="EstateStore"/>.</summary>
    public sealed class EstateState
    {
        public int Seed;
        public int FirstDay, LastDay;
        public int Real = EstateCatalogue.RealStart;   // the property market against prices
        public int Months;                              // months the market has moved
        public int YearFrom;                            // the day the running year's figures began
        public int TaxFrom;                             // the first day of the game's tax year being counted (0: not yet)
        public int TaxTakings, TaxCosts;                // the hotels' and car hire's takings and running costs in it: taxed when it turns
        public readonly List<EstateHolding> Holdings = new List<EstateHolding>(8);
        public readonly AgencyFile Agency = new AgencyFile();
        public readonly List<HotelFile> Hotels = new List<HotelFile>(2);
        public readonly HireFile Hire = new HireFile();

        public EstateHolding Holding(string place)
        {
            for (int i = 0; place != null && i < Holdings.Count; i++)
            {
                if (Holdings[i].Place == place)
                {
                    return Holdings[i];
                }
            }
            return null;
        }

        public HotelFile Hotel(string slot)
        {
            for (int i = 0; slot != null && i < Hotels.Count; i++)
            {
                if (Hotels[i].Slot == slot)
                {
                    return Hotels[i];
                }
            }
            return null;
        }
    }
}
