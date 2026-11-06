using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A month's lease of bays at a car park, to a business or a rival nearby, or to the player at a car park of the city's.</summary>
    public sealed class CarParkLease
    {
        public const int Business = 0, Rival = 1, Player = 2;   // who leases

        public int Id;
        public string Name = string.Empty;       // the lessee as the players read it (a business's name, a rival's, or empty for the player's own)
        public int Who = Business;
        public int Bays;
        public long Monthly;                     // cents a bay a month, fixed when the lease began
        public int Start;                        // the day it began
        public int PaidTo;                       // the last day paid for: the month's term ends on it
        public bool Ending;                      // not renewed at the end of the term
    }

    /// <summary>A lease asked for at a car park, to be accepted or declined until its last day.</summary>
    public sealed class CarParkOffer
    {
        public int Id;
        public string Name = string.Empty;
        public int Who = CarParkLease.Business;
        public int Bays;
        public long Monthly;                     // cents a bay a month
        public int Expires;                      // the last day it stands
    }

    /// <summary>
    /// A car park, the player's or a city builder's, from its plans to the years after (docs/features/construction.md, "Parking
    /// lots"): the land it takes (its first space and how many after it along the strip, the rows held), the plan's size, how it is
    /// let and at what price, the cars in it this hour, what it has taken, its leases and the offers waiting. The player's is one
    /// from its keys (its project before that). Saved in the line's "^j:" part; a line without it has none.
    /// </summary>
    public sealed class CarParkFile
    {
        public const int DailyKept = 14;

        public int Id;
        public CarParkOwner Owner;
        public int Rival;                            // the builder whose arm builds and runs it, when the city's
        public CarParkStage Stage = CarParkStage.Open;
        public string Lot = string.Empty;            // its first space's lot
        public int Spaces = 1;                       // spaces it takes along the strip from the first
        public string Rows = string.Empty;           // the rows held of each space, as digits (LotFile.Rows); empty: every row
        public int Width, Depth;                     // m: the ground it is laid out on
        public int Mouth = 1;                        // how it is laid out, 1 to 4 (CarParkRules.Codes): which way the aisles run and which end of the frontage the mouth is at
        public int Bays;
        public CarParkMode Mode = CarParkMode.Public;
        public int Price;                            // cents an hour, public pay parking
        public int Project;                          // the player's project that built it; 0 for the city's
        public int Day;                              // the day it was begun
        public int Until;                            // the day its stage ends (the city's)
        public int SiteId;                           // the city's site showing it going up; 0 for none
        public int Opened;                           // the day it opened; 0 before
        public int Cars;                             // cars in its bays at the last hour looked at
        public int Seen = -1;                        // the hour (day x 24 + hour) the cars were last worked out for
        public int Own;                              // car-hours of the player's own cars left in it today: the street rate the game charged them is paid back at night
        public long Taken;                           // cents taken so far today by the hour
        public long Earned, Spent;                   // cents taken and spent in all
        public int LastDay;                          // the day the daily list was last written for
        public readonly List<int> Daily = new List<int>(DailyKept);   // net dollars each of the last days, the latest last
        public int Others;                           // bays the city's other lessees hold at a car park of the city's that leases
        public readonly List<CarParkLease> Leases = new List<CarParkLease>(4);
        public readonly List<CarParkOffer> Offers = new List<CarParkOffer>(2);

        public bool UnderWay => Stage == CarParkStage.Plans || Stage == CarParkStage.Planning || Stage == CarParkStage.Building;

        public bool Standing => Stage == CarParkStage.Open;

        /// <summary>Bays under a lease.</summary>
        public int Leased
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Leases.Count; i++)
                {
                    n += Leases[i].Bays;
                }
                return n;
            }
        }

        /// <summary>Bays the player leases here.</summary>
        public int LeasedByPlayer
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Leases.Count; i++)
                {
                    n += Leases[i].Who == CarParkLease.Player ? Leases[i].Bays : 0;
                }
                return n;
            }
        }

        /// <summary>Bays still to lease: those no lease and none of the city's lessees hold.</summary>
        public int Free => Math.Max(0, Bays - Leased - (Owner == CarParkOwner.Builder ? Others : 0));

        /// <summary>Cents the leases bring in a month.</summary>
        public long LeaseIncome
        {
            get
            {
                long n = 0;
                for (int i = 0; i < Leases.Count; i++)
                {
                    n += Leases[i].Monthly * Leases[i].Bays;
                }
                return n;
            }
        }

        /// <summary>A day's net money (dollars) put at the end of the daily list, the oldest let go.</summary>
        public void AddDay(int day, int dollars)
        {
            Daily.Add(dollars);
            while (Daily.Count > DailyKept)
            {
                Daily.RemoveAt(0);
            }
            LastDay = day;
        }

        /// <summary>The net money (dollars) of the last so many days written.</summary>
        public long Net(int days)
        {
            long n = 0;
            for (int i = Math.Max(0, Daily.Count - days); i < Daily.Count; i++)
            {
                n += Daily[i];
            }
            return n;
        }
    }

    public sealed partial class BuildState
    {
        public const int CarParksMost = 160;         // car parks kept: every one standing, those closed let go first

        public readonly List<CarParkFile> CarParks = new List<CarParkFile>(8);
        public readonly int[] ParkNextDay = new int[BuildCatalogue.Districts];   // the day the city's builders may next begin a car park in each district; 0 before the first look
        public long ParkRefunded;                    // cents of the street rate paid back to the player for cars left in their own lots

        /// <summary>The car park with this id, or null.</summary>
        public CarParkFile CarPark(int id)
        {
            for (int i = 0; id > 0 && i < CarParks.Count; i++)
            {
                if (CarParks[i].Id == id)
                {
                    return CarParks[i];
                }
            }
            return null;
        }

        /// <summary>The car park (under way or standing) that takes a lot, its first or one after it, or null.</summary>
        public CarParkFile CarParkOn(string lot, Func<string, string, int, bool> within)
        {
            for (int i = 0; !string.IsNullOrEmpty(lot) && i < CarParks.Count; i++)
            {
                CarParkFile c = CarParks[i];
                if (c.Stage == CarParkStage.Closed)
                {
                    continue;
                }
                if (string.Equals(c.Lot, lot, StringComparison.Ordinal) || (c.Spaces > 1 && within != null && within(c.Lot, lot, c.Spaces)))
                {
                    return c;
                }
            }
            return null;
        }

        /// <summary>The city's car park under way whose site of the programme has this id, or null.</summary>
        public CarParkFile CarParkSite(int siteId)
        {
            for (int i = 0; siteId > 0 && i < CarParks.Count; i++)
            {
                if (CarParks[i].UnderWay && CarParks[i].SiteId == siteId)
                {
                    return CarParks[i];
                }
            }
            return null;
        }

        /// <summary>The player's car park the project built, or null.</summary>
        public CarParkFile CarParkOfProject(int project)
        {
            for (int i = 0; project > 0 && i < CarParks.Count; i++)
            {
                if (CarParks[i].Project == project && CarParks[i].Stage != CarParkStage.Closed)
                {
                    return CarParks[i];
                }
            }
            return null;
        }

        /// <summary>Car parks of the city's under way in a district (<paramref name="district"/> says which district a lot is in).</summary>
        public int CarParksUnderWay(Func<string, int> district, int which)
        {
            int n = 0;
            for (int i = 0; i < CarParks.Count; i++)
            {
                n += CarParks[i].Owner == CarParkOwner.Builder && CarParks[i].UnderWay && district(CarParks[i].Lot) == which ? 1 : 0;
            }
            return n;
        }
    }
}
