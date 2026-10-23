using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>The player's construction company: its office, yard, licence, standing, speciality and cover, and its books.</summary>
    public sealed class FirmFile
    {
        public int Founded;                          // the day it was founded; 0 for none
        public string Office = string.Empty;         // the office business's place, "street#number"
        public string Yard = string.Empty;           // a warehouse of the player's kept as the yard; empty for none
        public LicenceState Licence;
        public int LicenceDay;                       // the day it was asked for, granted or refused
        public int LicenceUntil;                     // the last day it holds
        public bool LicenceReminded;
        public int Reputation = BuildCatalogue.ReputationStart;
        public Speciality Speciality;
        public int SpecialSince;
        public CoverLevel Cover;
        public int Finished, OnTime, Late, Defects, TendersWon, TendersLost;
        public long Earned, Spent;                   // money from and into its works
    }

    /// <summary>A lot of the district the player owns.</summary>
    public sealed class LotFile
    {
        public string Id = string.Empty;
        public int Bought;
        public long Paid;
        public int Zones;           // the uses it is zoned for, bits of BuildUse
        public int ZoneAsk;         // a use asked of the council (its bit), 0 for none
        public int ZoneDay;         // the day the council hears it
        public int TaxedTo;         // the last day its land tax was charged for
        public int EmptySince;      // the day it last had no works under way; 0 while works are
    }

    /// <summary>A crew the company employs: a foreman and the people of a gang, their skill, morale, day rate and where they work.</summary>
    public sealed class CrewFile
    {
        public int Id;
        public int Gang;
        public int Foreman;
        public int Skill;
        public int Morale = BuildCatalogue.MoraleStart;
        public int Rate;            // money a working day, fixed when hired, raised when matched
        public int Hired;
        public int Project;         // the project it works on; 0 for none
        public int IdleDays;
        public int LowDays;         // days running at or under the strike line
        public int StrikeUntil;     // the last day of a strike; 0 for none
        public int PoachRival = -1; // a rival's offer, with its pay in thousandths more and the last day to match it
        public int PoachPay;
        public int PoachUntil;
        public int LastWeekLooked;  // the week a rival's offer was last drawn for it

        public int[] People => BuildCatalogue.Gangs[BuildCatalogue.Clamp(Gang, 0, BuildCatalogue.GangCount - 1)];
    }

    /// <summary>A piece of plant the company owns, kept at its yard or on a site.</summary>
    public sealed class PlantFile
    {
        public int Id;
        public Plant Kind;
        public int Bought;
        public int Price;
        public int Project;         // the project it is on; 0 at the yard
    }

    /// <summary>An employee of the office and the role the company gives them (the game's own id for the employee).</summary>
    public sealed class StaffFile
    {
        public string Employee = string.Empty;
        public StaffRole Role;
        public int Project;         // a site manager's project; 0 for none
    }

    /// <summary>One project: a building on a lot, an old building rebuilt, or works won in a tender, from the plans to the keys.</summary>
    public sealed class ProjectFile
    {
        public int Id;
        public ProjectKind Kind;
        public ProjectStage Stage;
        public int Created;
        public string Lot = string.Empty;            // a lot's id (a plot, a public building)
        public string Street = string.Empty;         // an old building's address (a rebuild, a client's)
        public int Number;
        public string District = string.Empty;       // the game's district id the building is in
        public BuildUse Use;
        public string Design = string.Empty;         // the copy engine's design name; empty for one picked to fit
        public string Size = string.Empty;           // the game's size id of the design's building
        public int Version;
        public int Area;                             // m2 of floor
        public int Storeys;
        public int Width, Depth, Height;             // m: the ground's, and the design's height
        public ArchitectKind Architect;
        public int Firm;
        public string ArchitectId = string.Empty;    // the employee, for an architect of the staff
        public int PlansDay;                         // the day the plans are drawn; 0 for not asked
        public bool PlansDone;
        public int Quality;                          // the plans', 0 to 100
        public long Estimate;                        // money: the build's estimate as shown
        public int Error;                            // thousandths the estimate is out by
        public int Exits, ExitsNeeded, ParkingNeeded;
        public bool ParkingUnder;
        public int EngineerDay;                      // the day the sign off is ready; 0 for not asked
        public bool Engineered;
        public bool EngineerStaff;
        public PlanningState Planning;
        public int Applications;                     // planning applications made
        public int PlanningDay, DecideDay;
        public int Objections;
        public bool Consulted;                       // the objections counted and in the paper
        public int Reasons;                          // bits of Refusal
        public bool Conditions;
        public int AppealDay;
        public int SiteId;                           // the city's site while it goes up
        public int Hours;                            // its working hours
        public int StartDay, DueDay;
        public int Rework;                           // tenths of an hour still to put right before the next inspection
        public readonly int[] Have = new int[(int)Material.Count];      // tenths of a load on site for the stage
        public readonly int[] Need = new int[(int)Material.Count];      // tenths of a load the stage needs
        public readonly int[] OnOrder = new int[(int)Material.Count];   // tenths of a load ordered
        public int OrderDay;                         // the day an order comes; 0 for none
        public int Merchant;
        public int Orders;                           // orders made, for each delivery's own draw
        public readonly int[] Plant = { -1, -1, -1, -1 };   // by plant: -1 none, 0 hired, else an owned piece's id
        public readonly InspectionState[] Insp = new InspectionState[(int)Inspection.Count];
        public readonly int[] InspDay = new int[(int)Inspection.Count];
        public readonly int[] InspTries = new int[(int)Inspection.Count];
        public int StageSeen = -1;                   // the stage the materials and plant were last set for
        public bool Visited;                         // the player walked the site with the foreman in this stage
        public int PaceSum, PaceDays;                // thousandths: the stage's pace each working day, for the inspector
        public int BribeDay;                         // the day a bribe was paid; 0 for none
        public int BribeAmount;
        public long Spent;
        public long LoanAgreed, LoanDrawn, LoanOwed, LoanCarry;    // the carry in hundredths
        public int LoanRateBp;
        public int LoanDraws;                        // bits: the draws taken
        public int LoanFrom;                         // the day repayments began; 0 before completion
        public int Investor = -1;
        public long InvestorPut, InvestorOwed;
        public int InvestorDue;                      // the last day to pay them
        public int Flats, FlatsSold, FlatsOffPlan;
        public long Deposits;                        // money of flats sold before completion, still due at it
        public long OffPlanBalance;                  // what those buyers pay at completion
        public int Handed;                           // the day it was handed over
        public int Opening;                          // the opening day
        public bool Ribbon;                          // the player cut the ribbon
        public bool Opened;
        public AfterUse After;
        public long Value;                           // the building's value at handover
        public int Ask = 1;                          // the asking price of the flats, 0 to 2
        public int DefectDay;                        // a fault due to come to light; 0 for none
        public long DefectClaim;
        public TenderSource Source;
        public int CivicKind = -1;                   // the city's kind of site, for works won
        public long ClientValue, ClientPaid;
        public int Client;
        public int RainHours, WaitHours, StrikeDays, Fails;   // hours lost to rain and for want of materials, days on strike, inspections failed
        public bool InDistrict;                      // handed to the district as one of its buildings
        public int Tender;                           // the tender it came from; 0 for none
        public int CostPermille = 1000;              // the design's cost against the plans' budget: the player's own design sets it
        public int Units;                            // the flats the player's own design gives a block of homes; 0 for the usual
        public int HedgeDay;                         // the day its materials were last hedged on the exchange; 0 never
        public long HedgeEnded;                      // cents: what its hedge's futures that have ended made, their commission in it
        public bool HedgeTest;                       // the test tools' project, put back by their put back
        public readonly List<HedgeHold> Hedges = new List<HedgeHold>(4);   // its futures still on the exchange's book

        public bool Live => Stage != ProjectStage.Finished && Stage != ProjectStage.Abandoned;

        /// <summary>Cents: what the hedge has saved (over nothing) or cost, its ended futures and its open ones as they stand.</summary>
        public long HedgeCents
        {
            get
            {
                long cents = HedgeEnded;
                for (int i = 0; i < Hedges.Count; i++)
                {
                    cents += Hedges[i].Result;
                }
                return cents;
            }
        }

        /// <summary>The rules' model of its work: a contract on a lot is built as a plot, a client's as a rebuild, the city's works as works.</summary>
        public ProjectKind Model
        {
            get
            {
                if (Kind != ProjectKind.Contract)
                {
                    return Kind;
                }
                return CivicKind == 5 ? ProjectKind.Plot : CivicKind == 4 ? ProjectKind.Rebuild : ProjectKind.Contract;
            }
        }
    }

    /// <summary>Works put out to tender, the player's bid and the award.</summary>
    public sealed class TenderFile
    {
        public int Id;
        public TenderSource Source;
        public int SiteId;                           // the city's site, for the city's works
        public int CivicKind;                        // the city's kind of site the works are
        public string Street = string.Empty;
        public int Number;
        public string Lot = string.Empty;
        public BuildUse Use;
        public int Area;
        public int Hours;
        public long Value;
        public int Opened;                           // the hour it opened: day x 24 + hour
        public int Closes;                           // the hour bids close
        public int BidPermille;                      // the player's price against the value; 0 for no bid
        public bool Fast;
        public TenderState State;
        public int Winner = -2;                      // -1 the player, a rival's index, -2 none yet
        public long WinningPrice;
        public int WinningDays;
        public int Client;
        public int Project;                          // the project the player's win became
    }

    /// <summary>One of the city's rival builders: its standing and what it has done in this save.</summary>
    public sealed class RivalFile
    {
        public int Reputation;
        public int Won, Lost, Built;
    }

    /// <summary>A development of the city's developers already handed to the district, or one shown going up, by its lot.</summary>
    public sealed class DevFile
    {
        public string Lot = string.Empty;
        public int SiteId;                           // the city's site showing it going up; 0 for none
        public bool Handed;                          // the district has it as one of its buildings
        public string Design = string.Empty;
        public bool Public;                          // a public building a rival won the tender for: finished when its site is handed over
    }

    /// <summary>One day of the construction company's books: what its works brought in and what they cost, in money.</summary>
    public struct DayBook
    {
        public int Day;
        public long In, Out;
    }

    /// <summary>A future of a project's hedge still on the exchange's book, and what it had made at the last look, in cents.</summary>
    public struct HedgeHold
    {
        public int Contract;
        public long Result;
    }

    /// <summary>The construction game in one save. Game-free: the director tells it the day and the game's figures.</summary>
    public sealed class BuildState
    {
        public int Seed;
        public int FirstDay, LastDay;
        public int NextId = 1;
        public int NextClientDay, NextPublicDay;     // the days the next client's and public tenders open
        public readonly FirmFile Firm = new FirmFile();
        public readonly List<LotFile> Lots = new List<LotFile>(8);
        public readonly List<CrewFile> Crews = new List<CrewFile>(8);
        public readonly List<PlantFile> Plant = new List<PlantFile>(4);
        public readonly List<StaffFile> Staff = new List<StaffFile>(8);
        public readonly List<ProjectFile> Projects = new List<ProjectFile>(8);
        public readonly List<TenderFile> Tenders = new List<TenderFile>(8);
        public readonly RivalFile[] Rivals = NewRivals();
        public readonly List<DevFile> Developments = new List<DevFile>(16);
        public readonly List<int> HiredOffers = new List<int>(4);   // this week's offers already taken, by slot
        public int OffersWeek = -1;
        public readonly List<DayBook> Books = new List<DayBook>(64);   // the company's takings and costs by day, the newest last

        /// <summary>Money a work brought in today, in the company's books and its total.</summary>
        public void TakeIn(long amount, int day)
        {
            if (amount > 0)
            {
                Firm.Earned += amount;
                Book(day, amount, 0L);
            }
        }

        /// <summary>Money a work cost today, in the company's books and its total.</summary>
        public void PayOut(long amount, int day)
        {
            if (amount > 0)
            {
                Firm.Spent += amount;
                Book(day, 0L, amount);
            }
        }

        // A day's line, kept as long as a listed company's year of books reaches back (ExposureRules.BooksDays).
        private void Book(int day, long earned, long spent)
        {
            int last = Books.Count - 1;
            if (last >= 0 && Books[last].Day == day)
            {
                DayBook b = Books[last];
                b.In += earned;
                b.Out += spent;
                Books[last] = b;
            }
            else
            {
                Books.Add(new DayBook { Day = day, In = earned, Out = spent });
            }
            while (Books.Count > 0 && (Books[0].Day < day - ExposureRules.BooksDays || Books.Count > ExposureRules.BooksDays))
            {
                Books.RemoveAt(0);
            }
        }

        /// <summary>The company's takings and costs over the days from one day up to, not with, another; how many days had any.</summary>
        public int BooksOver(int from, int to, out long earned, out long spent)
        {
            earned = 0L;
            spent = 0L;
            int days = 0;
            for (int i = Books.Count - 1; i >= 0; i--)
            {
                DayBook b = Books[i];
                if (b.Day < from)
                {
                    break;
                }
                if (b.Day < to)
                {
                    earned += b.In;
                    spent += b.Out;
                    days++;
                }
            }
            return days;
        }

        private static RivalFile[] NewRivals()
        {
            var rivals = new RivalFile[BuildCatalogue.RivalCount];
            for (int i = 0; i < rivals.Length; i++)
            {
                rivals[i] = new RivalFile { Reputation = BuildCatalogue.RivalReputation[i] };
            }
            return rivals;
        }

        public int NewId() => NextId++;

        public LotFile Lot(string id)
        {
            for (int i = 0; i < Lots.Count; i++)
            {
                if (string.Equals(Lots[i].Id, id, StringComparison.Ordinal))
                {
                    return Lots[i];
                }
            }
            return null;
        }

        public ProjectFile Project(int id)
        {
            for (int i = 0; id > 0 && i < Projects.Count; i++)
            {
                if (Projects[i].Id == id)
                {
                    return Projects[i];
                }
            }
            return null;
        }

        /// <summary>The live project going up on a site of the city's programme, or null.</summary>
        public ProjectFile OnSite(int siteId)
        {
            for (int i = 0; siteId > 0 && i < Projects.Count; i++)
            {
                if (Projects[i].SiteId == siteId && Projects[i].Stage == ProjectStage.Building)
                {
                    return Projects[i];
                }
            }
            return null;
        }

        /// <summary>The live project on a lot, or null.</summary>
        public ProjectFile OnLot(string lot)
        {
            for (int i = 0; lot != null && i < Projects.Count; i++)
            {
                if (Projects[i].Live && string.Equals(Projects[i].Lot, lot, StringComparison.Ordinal))
                {
                    return Projects[i];
                }
            }
            return null;
        }

        public CrewFile Crew(int id)
        {
            for (int i = 0; i < Crews.Count; i++)
            {
                if (Crews[i].Id == id)
                {
                    return Crews[i];
                }
            }
            return null;
        }

        public PlantFile PlantOf(int id)
        {
            for (int i = 0; id > 0 && i < Plant.Count; i++)
            {
                if (Plant[i].Id == id)
                {
                    return Plant[i];
                }
            }
            return null;
        }

        public StaffFile StaffOf(string employee)
        {
            for (int i = 0; employee != null && i < Staff.Count; i++)
            {
                if (string.Equals(Staff[i].Employee, employee, StringComparison.Ordinal))
                {
                    return Staff[i];
                }
            }
            return null;
        }

        public TenderFile Tender(int id)
        {
            for (int i = 0; i < Tenders.Count; i++)
            {
                if (Tenders[i].Id == id)
                {
                    return Tenders[i];
                }
            }
            return null;
        }

        /// <summary>The open tender for a site of the city's programme, or null.</summary>
        public TenderFile TenderFor(int siteId)
        {
            for (int i = 0; siteId > 0 && i < Tenders.Count; i++)
            {
                if (Tenders[i].SiteId == siteId && Tenders[i].Source == TenderSource.CityWorks)
                {
                    return Tenders[i];
                }
            }
            return null;
        }

        public DevFile Development(string lot)
        {
            for (int i = 0; lot != null && i < Developments.Count; i++)
            {
                if (string.Equals(Developments[i].Lot, lot, StringComparison.Ordinal))
                {
                    return Developments[i];
                }
            }
            return null;
        }

        /// <summary>The crews on a project.</summary>
        public int CrewsOn(int project)
        {
            int n = 0;
            for (int i = 0; i < Crews.Count; i++)
            {
                n += Crews[i].Project == project && project > 0 ? 1 : 0;
            }
            return n;
        }

        /// <summary>The player's projects going up now.</summary>
        public int Building()
        {
            int n = 0;
            for (int i = 0; i < Projects.Count; i++)
            {
                n += Projects[i].Stage == ProjectStage.Building ? 1 : 0;
            }
            return n;
        }
    }
}
