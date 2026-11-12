using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One business the cleaning service looks after: a shop of the player's, or somebody else's under contract.</summary>
    public sealed class CleaningClient
    {
        public string Place = string.Empty;      // its address, as business pressure writes a place
        public string Name = string.Empty;
        public string District = string.Empty;
        public bool Outside;                     // somebody else's business, under contract
        public int X, Z;                         // its door, whole metres
        public int Mode;                         // 0 as needed, 1 to 3 that many visits a day
        public readonly int[] Hours = { ServicesCatalogue.HoursDefault[0], ServicesCatalogue.HoursDefault[1], ServicesCatalogue.HoursDefault[2] };
        public int Threshold = ServicesCatalogue.ThresholdDefault;
        public int NeedDay = -1;                 // hundredths of dirt it makes a day at its worst tile; -1 until measured
        public int WorstStart;                   // its worst tile at the last day event
        public int CleanedToday;                 // hundredths the service has taken off every tile today
        public int MinutesToday, MinutesBefore;  // minutes of cleaning it got today and the day before
        public int CleanBefore = -1;             // its cleanliness at the last day event, for today's trend
        public int LastDay, LastMinute;          // when a cleaner last came in
        public string LastCleaner = string.Empty;

        // Under contract only: the floor as the mod keeps it, and the terms.
        public int Dirt;                         // hundredths, the whole floor evenly
        public int DirtDay;                      // hundredths it gains a day
        public int Sqm, Capacity;
        public int Fee;                          // a week
        public int Standard;                     // the cleanliness it asks for
        public int WeekFrom;                     // the day the running week began
        public int DaysUnder;                    // mornings of this week under the standard
        public int Missed;                       // weeks missed running
        public int Earned;                       // fees paid so far
        public int Crew = -1;                    // the named crew that serves it (0 to 2), or -1: whoever is free

        // Read from the game at each look; never saved.
        public int Worst;                        // its dirtiest tile now, hundredths
        public int Clean = 100;
        public int Force = 1;                    // the cleaners its floor needs
        public bool Open;                        // the game would clean it today: rented, not shut, open this weekday
        public bool Gone;                        // no longer there to be cleaned
        public int DoneMask;                     // today's fixed visits made or begun, a bit each
        public bool Pending;                     // a visit is under way or planned
        public int NextStart = -1, NextEnd;      // the next visit planned today, minutes; -1: none
        public string NextBy;
        public int Unserved;                     // why a due visit is not planned (RoundRules.NoCleaner and its neighbours); 0: it is

        /// <summary>The cleanliness that counts: the game's for a shop of the player's, the mod's figure under contract.</summary>
        public int Now => Outside ? CleaningRules.Even(Dirt) : Clean;

        /// <summary>The dirt a visit has to take off now.</summary>
        public int Load => Outside ? Dirt : Worst;
    }

    /// <summary>A contract another business offers the cleaning service.</summary>
    public sealed class CleaningOffer
    {
        public string Place = string.Empty;
        public string Name = string.Empty;
        public string District = string.Empty;
        public int Sqm, Capacity, X, Z;
        public int Fee, Standard;
        public int Until;                        // the last day the offer stands
        public readonly List<RivalBid> Rivals = new List<RivalBid>(3);   // rival cleaning firms bidding for the same contract
    }

    /// <summary>A rival cleaning firm's bid for an open contract: the share of the asking fee it works for, and how well its crew cleans.</summary>
    public sealed class RivalBid
    {
        public int Firm;                         // its place in ServicesCatalogue.RivalFirms
        public int Pct;                          // of the asking fee
        public int Skill;                        // 0 to 100
    }

    /// <summary>A member of the player's crew placed in one of the named crews.</summary>
    public sealed class CrewPlace
    {
        public string Id = string.Empty;
        public int Group;                        // 0 to ServicesCatalogue.CrewGroups - 1
    }

    /// <summary>One business's worth by day, a ring of the last days, for the change on the month and the line under the figure.</summary>
    public sealed class WorthRing
    {
        public string Place = string.Empty;
        public readonly int[] Value = new int[ServicesCatalogue.WorthDays];
        public readonly int[] Day = new int[ServicesCatalogue.WorthDays];   // the day each slot was noted; 0: none
    }

    /// <summary>A business the player has put on the market.</summary>
    public sealed class SaleListing
    {
        public string Place = string.Empty;
        public string Name = string.Empty;
        public int ListedDay;
        public int Step = ServicesCatalogue.AskingDefault;   // the asking price, as an index of ServicesCatalogue.AskingPct
        public int Asking;
        public int Value;                        // what it was worth when last valued
        public int Lift;                         // thousandths the business's books showed over the truth when it was last valued (the schemes' dressed books); an offer carries it
        public int NextOfferDay;
        public int OffersMade;
    }

    /// <summary>An offer for a business on the market.</summary>
    public sealed class SaleOffer
    {
        public int Id;
        public string Place = string.Empty;
        public int Buyer;                        // BuyerKind
        public int Price;
        public int Ceiling;                      // the most this buyer would pay: never shown
        public bool KeepsStaff;
        public int Parts;                        // 0 or 1: at once; more: that many parts, the first on the day, one a week after
        public int ClauseDays;                   // days the seller stays out of that trade in the district; 0: no clause
        public int Deadline;                     // the last day the offer stands
        public bool Countered;
        public int Face;                         // which of the buyer kind's names, for an offer made before named buyers
        public string Who = string.Empty;        // the buyer: a rival's id of the game's, an employee's id, a listed company's id, an investor's name; empty before named buyers
        public string WhoName = string.Empty;    // their name when the offer was made
        public int WhoCount;                     // a rival's businesses, a company's id, the staff's number; 0 for an investor
        public int Lift;                         // thousandths the books showed over the truth when this price was made: the buyer's look and the schemes' act go by it, never by the books now

        /// <summary>True for an offer from somebody by name; one made before named buyers has only its kind.</summary>
        public bool Named => WhoName.Length > 0;
    }

    /// <summary>A business of a rival of the game's put up for sale: the player may buy it first, then another rival takes it.</summary>
    public sealed class RivalSale
    {
        public int Id;
        public string Place = string.Empty;      // its building, as a place is written
        public string Business = string.Empty;   // its name
        public string Seller = string.Empty;     // the rival's id of the game's
        public string SellerName = string.Empty;
        public string Buyer = string.Empty;      // the rival who takes it if the player does not; empty: the seller keeps it
        public string BuyerName = string.Empty;
        public int Price;
        public int Weekly;                       // its takings a week when put up
        public int Day;                          // the day it was put up
        public int Deadline;                     // the last day the player may buy it
    }

    /// <summary>Money a buyer still owes for a business, in weekly parts.</summary>
    public sealed class SaleDue
    {
        public string Name = string.Empty;       // the business that was sold
        public int Buyer;
        public int Part;
        public int Left;                         // parts still to come
        public int NextDay;
    }

    /// <summary>A promise made with a sale: no business of that kind in that district until a day.</summary>
    public sealed class SaleClause
    {
        public string Type = string.Empty;       // the game's business type
        public string District = string.Empty;
        public string Name = string.Empty;       // the business that was sold
        public int FromDay, Until;               // one of the kind taken on after FromDay, until Until, breaks it
        public int Price;                        // the sale's price: the penalty is a share of it
    }

    /// <summary>A completed sale, as the tax return needs it.</summary>
    public sealed class SaleRecord
    {
        public int Day;
        public string Name = string.Empty;
        public int Price;
        public int Gain;                         // the price above the worth of the stock and furniture
    }

    /// <summary>Everything the cleaning service and selling a business keep of a save: one line of its data (<see cref="ServicesStore"/>).</summary>
    public sealed class ServicesState
    {
        public int Seed;
        public int FirstDay, LastDay;
        public int NextId = 1;

        public bool Van;                         // the crew drives
        public int LastOfferDay;                 // the last day a contract offer was looked for
        public int VanMetres;                    // driven today
        public readonly int[] VanCost = new int[7];       // the van's cost on each of the last seven days, by day of the week
        public readonly int[] FeeIncome = new int[7];     // contract fees less penalties on each of the last seven days
        public readonly List<string> Crew = new List<string>(ServicesCatalogue.CrewMost);
        public readonly List<CleaningClient> Clients = new List<CleaningClient>(ServicesCatalogue.ClientsMost);
        public readonly List<CleaningOffer> Offers = new List<CleaningOffer>(ServicesCatalogue.OffersMost);

        public readonly List<SaleListing> Listings = new List<SaleListing>(4);
        public readonly List<SaleOffer> SaleOffers = new List<SaleOffer>(ServicesCatalogue.OffersKept);
        public readonly List<SaleDue> Dues = new List<SaleDue>(4);
        public readonly List<SaleClause> Clauses = new List<SaleClause>(4);
        public readonly List<SaleRecord> Records = new List<SaleRecord>(ServicesCatalogue.RecordsKept);
        public readonly List<RivalSale> RivalSales = new List<RivalSale>(ServicesCatalogue.RivalSalesMost);
        public int RivalSaleDay;                 // the day the last rival's sale was put up

        public readonly List<CrewPlace> Places = new List<CrewPlace>(ServicesCatalogue.CrewMost);   // who stands in which named crew
        public readonly int[] FeeMonths = new int[ServicesCatalogue.FeeMonths];   // the fees contracted a month, oldest first, 0 before the first month noted
        public int FeeNoted;                     // months noted so far, up to the ring's size
        public int FeeMonth = -1;                // the month the last figure was noted for
        public int RivalWon;                     // contracts a rival firm took from the player's offers
        public readonly List<WorthRing> Worths = new List<WorthRing>(ServicesCatalogue.ListingsMost);

        /// <summary>The named crew a member stands in; -1 for none.</summary>
        public int GroupOf(string id)
        {
            for (int i = 0; i < Places.Count; i++)
            {
                if (string.Equals(Places[i].Id, id, StringComparison.Ordinal))
                {
                    return Places[i].Group;
                }
            }
            return -1;
        }

        public RivalSale RivalSale(int id)
        {
            for (int i = 0; i < RivalSales.Count; i++)
            {
                if (RivalSales[i].Id == id)
                {
                    return RivalSales[i];
                }
            }
            return null;
        }

        /// <summary>True if a rival's business at this place is up for sale now.</summary>
        public bool RivalSelling(string place)
        {
            for (int i = 0; i < RivalSales.Count; i++)
            {
                if (string.Equals(RivalSales[i].Place, place, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if this buyer already has an offer standing for the business at this place.</summary>
        public bool Offering(string place, string who)
        {
            for (int i = 0; !string.IsNullOrEmpty(who) && i < SaleOffers.Count; i++)
            {
                if (string.Equals(SaleOffers[i].Place, place, StringComparison.Ordinal) && string.Equals(SaleOffers[i].Who, who, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public CleaningClient Client(string place)
        {
            for (int i = 0; i < Clients.Count; i++)
            {
                if (string.Equals(Clients[i].Place, place, StringComparison.Ordinal))
                {
                    return Clients[i];
                }
            }
            return null;
        }

        public CleaningOffer Offer(string place)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                if (string.Equals(Offers[i].Place, place, StringComparison.Ordinal))
                {
                    return Offers[i];
                }
            }
            return null;
        }

        public bool InCrew(string id) => !string.IsNullOrEmpty(id) && Crew.Contains(id);

        public SaleListing Listing(string place)
        {
            for (int i = 0; i < Listings.Count; i++)
            {
                if (string.Equals(Listings[i].Place, place, StringComparison.Ordinal))
                {
                    return Listings[i];
                }
            }
            return null;
        }

        public SaleOffer SaleOffer(int id)
        {
            for (int i = 0; i < SaleOffers.Count; i++)
            {
                if (SaleOffers[i].Id == id)
                {
                    return SaleOffers[i];
                }
            }
            return null;
        }

        public int OffersFor(string place)
        {
            int count = 0;
            for (int i = 0; i < SaleOffers.Count; i++)
            {
                count += string.Equals(SaleOffers[i].Place, place, StringComparison.Ordinal) ? 1 : 0;
            }
            return count;
        }

        /// <summary>Takes a business off the market with its offers: it was sold, withdrawn or is no longer the player's.</summary>
        public void Unlist(string place)
        {
            for (int i = Listings.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Listings[i].Place, place, StringComparison.Ordinal))
                {
                    Listings.RemoveAt(i);
                }
            }
            for (int i = SaleOffers.Count - 1; i >= 0; i--)
            {
                if (string.Equals(SaleOffers[i].Place, place, StringComparison.Ordinal))
                {
                    SaleOffers.RemoveAt(i);
                }
            }
        }

        /// <summary>Empties the days from..to of a figure kept by day of the week (a week of them at most): no sum holds a day from a week before.</summary>
        public static void ClearDays(int[] days, int from, int to)
        {
            for (int d = Math.Max(from, to - 6); d <= to; d++)
            {
                days[((d % 7) + 7) % 7] = 0;
            }
        }

        /// <summary>The sum of a week's seven daily figures.</summary>
        public static int Week(int[] days)
        {
            int sum = 0;
            for (int i = 0; i < days.Length; i++)
            {
                sum += days[i];
            }
            return sum;
        }
    }
}
