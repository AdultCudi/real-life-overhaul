using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// One business the cleaning service looks after: a shop of the player's, or a business of somebody else's under
    /// contract. What is kept of it in the save, and (the fields at the end) what is read from the game each look.
    /// </summary>
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
        public int NeedDay = -1;                 // the dirt it makes in a day, hundredths at its worst tile; -1 until measured
        public int WorstStart;                   // its worst tile at the last day event
        public int CleanedToday;                 // hundredths the service has taken off every tile today
        public int MinutesToday, MinutesBefore;  // minutes of cleaning it got today and on the day before
        public int CleanBefore = -1;             // its cleanliness at the last day event: the trend is today's against it
        public int LastDay, LastMinute;          // when a cleaner last came in
        public string LastCleaner = string.Empty;

        // A client under contract only: its floor as the mod keeps it, and the terms.
        public int Dirt;                         // hundredths, the whole floor evenly
        public int DirtDay;                      // hundredths it gains a day
        public int Sqm, Capacity;
        public int Fee;                          // a week
        public int Standard;                     // the cleanliness it asks for
        public int WeekFrom;                     // the day the running week began
        public int DaysUnder;                    // days of this week under the standard
        public int Missed;                       // weeks missed running
        public int Earned;                       // fees paid so far

        // Read from the game each look; never saved.
        [NonSerialized] public int Worst;        // its dirtiest tile now, hundredths
        [NonSerialized] public int Clean = 100;  // its cleanliness now
        [NonSerialized] public int Force = 1;    // the cleaners its floor needs
        [NonSerialized] public bool Open;        // the game would clean it today: rented, not shut, open on this day of the week
        [NonSerialized] public bool Gone;        // no longer there to be cleaned
        [NonSerialized] public int DoneMask;     // the fixed visits of today that have been made or begun, a bit each
        [NonSerialized] public bool Pending;     // a visit is under way or planned
        [NonSerialized] public int NextStart = -1, NextEnd;   // the next visit planned today, minutes; -1: none
        [NonSerialized] public string NextBy;    // ...and who makes it
        [NonSerialized] public int Unserved;     // why a visit that is due is not planned (RoundRules.NoCleaner and its neighbours); 0: nothing is left over

        /// <summary>The cleanliness that counts for this client: the game's for a shop of the player's, the mod's own figure for one under contract.</summary>
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
        public int Until;                        // the offer stands up to and with this day
    }

    /// <summary>A business the player has put on the market.</summary>
    public sealed class SaleListing
    {
        public string Place = string.Empty;
        public string Name = string.Empty;
        public int ListedDay;
        public int Step = ServicesCatalogue.AskingDefault;   // the asking price as an index of ServicesCatalogue.AskingPct
        public int Asking;
        public int Value;                        // what it was worth on the day it was listed
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
        public int Parts;                        // 0 or 1: at once; more: that many parts, the first on the day and one a week after
        public int ClauseDays;                   // days the seller must stay out of that trade in the district; 0: no such clause
        public int Deadline;                     // the offer stands up to and with this day
        public bool Countered;
        public int Face;                         // which of the buyer kind's names
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
        public int FromDay, Until;
        public int Price;                        // what the business was sold for: the penalty is a share of it
    }

    /// <summary>A sale that was completed: what the tax return needs to know of it.</summary>
    public sealed class SaleRecord
    {
        public int Day;
        public string Name = string.Empty;
        public int Price;
        public int Gain;                         // the price above the worth of the stock and furniture
    }

    /// <summary>
    /// Everything the cleaning service and selling a business keep of a save: one line of the save's data
    /// (<see cref="ServicesStore"/>). Nothing of it is written into the game's own records.
    /// </summary>
    public sealed class ServicesState
    {
        public int Seed;
        public int FirstDay, LastDay;
        public int NextId = 1;

        // The cleaning service.
        public bool Van;                         // the crew drives
        public int LastOfferDay;                 // the last day a contract offer was looked for
        public int VanMetres;                    // driven today
        public readonly int[] VanCost = new int[7];       // what the van cost on each of the last seven days, by day of the week
        public readonly int[] FeeIncome = new int[7];     // contract fees less penalties booked on each of the last seven days
        public readonly List<string> Crew = new List<string>(ServicesCatalogue.CrewMost);
        public readonly List<CleaningClient> Clients = new List<CleaningClient>(ServicesCatalogue.ClientsMost);
        public readonly List<CleaningOffer> Offers = new List<CleaningOffer>(ServicesCatalogue.OffersMost);

        // Selling a business.
        public readonly List<SaleListing> Listings = new List<SaleListing>(4);
        public readonly List<SaleOffer> SaleOffers = new List<SaleOffer>(ServicesCatalogue.OffersKept);
        public readonly List<SaleDue> Dues = new List<SaleDue>(4);
        public readonly List<SaleClause> Clauses = new List<SaleClause>(4);
        public readonly List<SaleRecord> Records = new List<SaleRecord>(ServicesCatalogue.RecordsKept);

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

        public bool InCrew(string id)
        {
            return !string.IsNullOrEmpty(id) && Crew.Contains(id);
        }

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

        /// <summary>How many offers stand for a business.</summary>
        public int OffersFor(string place)
        {
            int count = 0;
            for (int i = 0; i < SaleOffers.Count; i++)
            {
                count += string.Equals(SaleOffers[i].Place, place, StringComparison.Ordinal) ? 1 : 0;
            }
            return count;
        }

        /// <summary>Everything about a business on the market is taken away: it was sold, withdrawn or is no longer the player's.</summary>
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

        /// <summary>The sum of the last seven days of a week's figures.</summary>
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
