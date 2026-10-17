using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Somebody who would take a unit: what they offer, how good their record is, and until when they wait.</summary>
    public sealed class RentApplicant
    {
        public int Id;
        public int Kind;        // one of the use's eight descriptions (EstateText)
        public int Offer;       // dollars a day
        public int Record;      // 20 to 95: how reliably they have paid before
        public int Term;        // days the lease would run
        public int Lapse;       // the last day the application stands
    }

    /// <summary>One unit of a building the player lets himself; condition, level, cover and works are the building's.</summary>
    public sealed class EstateUnit
    {
        public int Ask = EstateCatalogue.AskStart;   // thousandths of the market's rent
        public bool ToLet = true;       // applicants are looked for while it stands empty

        public int Tenant = -1;         // the tenant's description; -1 for nobody
        public int Record;
        public int Rent;                // dollars a day, as agreed
        public int Deposit;             // held
        public int Until;               // the lease's last day
        public int Arrears, ArrearsDays;
        public int NoticeEnd;           // the day notice runs out; 0 for none
        public int CourtDay;            // the day the possession order comes; 0 for none
        public int VoidDays;            // days empty in a row
        public readonly List<RentApplicant> Applicants = new List<RentApplicant>(EstateCatalogue.ApplicantsMost);

        public bool Let => Tenant >= 0;
        public bool Paying => Tenant >= 0 && Arrears == 0;

        /// <summary>Nobody is in: every figure of the lease is nothing.</summary>
        public void Vacant()
        {
            Tenant = -1;
            Record = Rent = Deposit = Until = Arrears = ArrearsDays = NoticeEnd = CourtDay = 0;
        }
    }

    /// <summary>
    /// One building the player owns. The fields from <see cref="Base"/> on are read from the game, not saved; the first
    /// of <see cref="Units"/> holds what a save from before units kept as the building's lease.
    /// </summary>
    public sealed class EstateHolding
    {
        public string Place;            // street and number (EstateGame.Place)
        public int Paid, BoughtDay;     // as the game recorded the purchase
        public int Spent;               // refurbishment and conversion paid for: counted against a gain
        public int Use;                 // EstateUse
        public int Cond = EstateCatalogue.ConditionNew;
        public int Level;               // refurbishment, 0 to 3
        public bool Managed;            // the player lets it himself; false: the game lets it
        public bool Cover;              // landlord's cover
        public readonly List<EstateUnit> Units = new List<EstateUnit>(4) { new EstateUnit() };

        public int Work;                // EstateWork
        public int WorkEnd, WorkCost, WorkTo;
        public int PlanUse = -1;        // a use the council has allowed that is not built yet

        public int List;                // the asking price; 0 while it is not for sale
        public int Offer, OfferLapse;   // a buyer's offer and the last day it stands
        public int SaleDay, SalePrice;  // an agreed sale and the day it completes
        public bool SaleFails;          // settled when the sale was agreed, found out on the day

        public int RentYear, CostYear;  // since EstateState.YearFrom
        public int TopUpDay;            // the last night the rental income option was settled; not saved
        public int NextApplicant = 1;   // applicants are numbered across the building's units

        // read from the game, not saved
        public int Base;                // the game's own value of it
        public int Income = EstateCatalogue.IncomeMiddle;
        public string District;
        public string Name = string.Empty;
        public bool Self;               // the player uses a unit himself: the game lets the rest
        public bool Seen;

        public bool Busy => Work != (int)EstateWork.None;

        public bool AnyLet
        {
            get
            {
                for (int i = 0; i < Units.Count; i++)
                {
                    if (Units[i].Let)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>How many of its units are let, and how many of those owe rent.</summary>
        public int LetUnits(out int behind)
        {
            int let = 0;
            behind = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                let += Units[i].Let ? 1 : 0;
                behind += Units[i].Let && Units[i].Arrears > 0 ? 1 : 0;
            }
            return let;
        }

        public int ApplicantCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Units.Count; i++)
                {
                    count += Units[i].Applicants.Count;
                }
                return count;
            }
        }

        /// <summary>An applicant by number and the unit applied for; null and -1 for none.</summary>
        public RentApplicant ApplicantOf(int id, out int unit)
        {
            for (unit = 0; unit < Units.Count; unit++)
            {
                List<RentApplicant> list = Units[unit].Applicants;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Id == id)
                    {
                        return list[i];
                    }
                }
            }
            unit = -1;
            return null;
        }

        public RentApplicant ApplicantOf(int id)
        {
            int unit;
            return ApplicantOf(id, out unit);
        }

        /// <summary>A unit by its place; null for one the building does not have.</summary>
        public EstateUnit Unit(int unit)
        {
            return unit >= 0 && unit < Units.Count ? Units[unit] : null;
        }

        public void ClearApplicants()
        {
            for (int i = 0; i < Units.Count; i++)
            {
                Units[i].Applicants.Clear();
            }
        }
    }

    /// <summary>What a district thinks of the agency.</summary>
    public sealed class DistrictRep
    {
        public string Name;     // the game's neighbourhood
        public int Rep = EstateCatalogue.ReputationStart;
        public int SqmPrice;    // dollars a square metre there by the game's figures; not saved
    }

    /// <summary>Another owner's building or unit that the agency sells or lets.</summary>
    public sealed class Instruction
    {
        public int Id;
        public string District;
        public bool Sale;       // false: a letting
        public int Sqm;
        public int Value;
        public int Ask;         // what the owner wants, thousandths of the value
        public int Stage;       // InstructionStage
        public int Hours;       // an agent's hours it takes, tenths
        public int Done;        // tenths spent so far
        public int Day;         // answer by; then lapses on; then completes on
        public int Price;       // the deal agreed
        public bool Fails;      // settled when the deal was agreed, found out on the day
    }

    /// <summary>The estate agency: its office, what the districts think of it and what it has in hand.</summary>
    public sealed class AgencyFile
    {
        public string Office = string.Empty;   // the office's place; empty while there is no agency
        public int NextId = 1;
        public int Commission, Deals, Lost;    // in all
        public readonly List<DistrictRep> Districts = new List<DistrictRep>(16);
        public readonly List<Instruction> Open = new List<Instruction>(EstateCatalogue.InstructionsMost);

        public int Capacity;                   // an agent's hours a day, tenths; read each morning, not saved
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
        public string Slot;             // Travel's id of the hotel
        public bool Leased;
        public int Paid, Day;           // the price or the lease's deposit, and when
        public int LeaseDaily;          // dollars a day while leased
        public readonly int[] Rate = { EstateCatalogue.Thousand, EstateCatalogue.Thousand };   // thousandths of the going rate, by room type
        public int Staff = 1;           // 0 basic, 1 standard, 2 excellent
        public int Clean = 700, Rating = EstateCatalogue.RatingStart;
        public int PushEnd;             // a campaign's last day; 0 for none
        public int GroupRooms, GroupNights, GroupRate;   // a group staying: rooms, nights left, dollars a room
        public int AskRooms, AskNights, AskRate, AskLapse;   // a group that asks, and the last day to answer
        public int OutRooms, OutEnd;    // rooms out of use and their last night
        public int InspectDay;          // the next inspection
        public readonly int[] Reviews = new int[EstateCatalogue.ReviewsKept];   // review codes, newest first; 0 for none
        public readonly int[] Sold = new int[EstateCatalogue.RoomTypes];        // last night
        public int Takings, Costs;      // last night
        public int TakingsYear, CostsYear;

        // read from Travel's hotel data, not saved
        public int Tier;
        public int Income = EstateCatalogue.IncomeMiddle;
        public readonly int[] Going = new int[EstateCatalogue.RoomTypes];   // tonight's going rate of each type
        public string Name = string.Empty;
    }

    /// <summary>A car of the player's on the hire business's books.</summary>
    public sealed class HireCar
    {
        public string Id;               // the game's vehicle instance id
        public string Type = string.Empty;   // the game's vehicle type name
        public int Price;               // list price
        public int Rate = EstateCatalogue.Thousand;   // thousandths of the going rate
        public int State;               // HireState
        public int Day;                 // while out: the day it is due back
        public int HireDays, HireRate, Deposit, HireSeed;
        public int X, Y, Z, Yaw;        // where it stood when it went out: hundredths of a metre, tenths of a degree
        public int Takings, Hires;
        public int HireRating = -1;     // while out: the hire business's rating when it went out, which decides how it ends; -1: not known (an older save)
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
        public int Real = EstateCatalogue.RealStart;   // the property market against prices: 1000 is its long-run level
        public int Months;                              // months the market has moved
        public int YearFrom;                            // the day the running year's figures began
        public int TaxFrom;                             // the first day of the game's tax year being counted; 0: not yet
        public int TaxTakings, TaxCosts;                // the hotels' and car hire's in that year: taxed when it turns
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
