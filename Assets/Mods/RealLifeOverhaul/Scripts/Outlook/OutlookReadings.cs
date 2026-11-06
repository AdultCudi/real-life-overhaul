using System.Collections.Generic;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>A change over some days: known or not, and its size in the unit of the figure it belongs to (basis points of a per cent, or the figure's own points).</summary>
    public struct OutlookDelta
    {
        public bool Known;
        public int Value;

        public static OutlookDelta None => default(OutlookDelta);

        public static OutlookDelta Of(int value) => new OutlookDelta { Known = true, Value = value };
    }

    /// <summary>One kind of goods' supply today: its wholesale factor, and a shortage or a glut running or announced.</summary>
    public sealed class OutlookSupply
    {
        public int Factor = 1000;     // thousandths: supply and the price index
        public int Shock;             // 0 none, 1 a shortage, 2 a glut
        public bool Announced;        // it starts tomorrow
        public int Days;              // days it lasts, or has left
        public int Percent;           // what it does to the kind's prices, whole per cent (a glut's is negative)
        public bool Stocked;          // one of the player's businesses stocks the kind

        public void Clear()
        {
            Factor = 1000;
            Shock = 0;
            Announced = false;
            Days = 0;
            Percent = 0;
            Stocked = false;
        }
    }

    /// <summary>A sector of the stock market: its index and the day's move.</summary>
    public sealed class OutlookSector
    {
        public int Id;
        public int Last;              // hundredths
        public int DayBp;             // basis points today
    }

    /// <summary>One district as the Districts tab shows it. Every figure is one the mod keeps; a use or a level the mod keeps for the district alone is -1 or 0 elsewhere.</summary>
    public sealed class OutlookDistrict
    {
        public string Id;             // the game's name for the neighbourhood
        public int Colour;            // 0xRRGGBB: the colour the city map shows it in
        public int Start, Income, Before;   // tenths of a point: where it started, where it stands, where it stood a month ago
        public int Working, Middle, Upper;  // per cent of its people (the game's)
        public int Weight;            // thousandths: the game's weight of its customers
        public int Traffic;           // per cent: the game's density of vehicle traffic
        public int Footfall;          // per cent of customers its income adds or takes
        public int Property;          // thousandths: what a property there is worth against the game's figure
        public int Bears;             // thousandths: what its customers bear against the game's own price
        public int Land;              // whole money: a square metre of land today
        public int Level;             // thousandths: its market level (the mod's own districts); 0 where there is none
        public int Growing = -1;      // buildings going up (the mod's own districts); -1 where none are counted
        public bool Mine;             // the player has a business or a building there
        public readonly int[] Demand = { -1, -1, -1, -1 };   // homes, shops, offices, warehouses, 0 to 100; -1: not kept here
        public int Pull;              // customer pull, 100 the city's mean (set by OutlookRules.Rank)
        public int Rank;              // 1 for the strongest
        public OutlookDelta IncomeLong;      // its income's change over the long window, tenths
        public OutlookDelta WorthLong;       // its property's change over the long window, basis points

        public bool HasDemand => Demand[0] >= 0 || Demand[1] >= 0 || Demand[2] >= 0 || Demand[3] >= 0;

        public void Clear()
        {
            Id = null;
            Colour = 0;
            Start = Income = Before = 1000;
            Working = Middle = Upper = 0;
            Weight = 1000;
            Traffic = 100;
            Footfall = 0;
            Property = Bears = 1000;
            Land = 0;
            Level = 0;
            Growing = -1;
            Mine = false;
            for (int i = 0; i < Demand.Length; i++)
            {
                Demand[i] = -1;
            }
            Pull = 100;
            Rank = 0;
            IncomeLong = OutlookDelta.None;
            WorthLong = OutlookDelta.None;
        }
    }

    /// <summary>One of the player's businesses or buildings, as the Districts tab lists it under the districts.</summary>
    public sealed class OutlookPlace
    {
        public string Name;           // the business's name, or the building's address
        public string District;       // the game's name for the neighbourhood; empty where it is not known
        public int Colour;            // the district's colour on the city map, 0xRRGGBB
        public bool Known = true;     // a business: false where the game sets its customers and the city's economy has no figure for it yet
        public int Customers;         // a business: whole per cent of customers its district, the economy and its prices add or take
        public int Prices;            // a business: 0 no measure, 1 its prices suit what its district bears, 2 they are over it
        public long Value, Today;     // a building: the game's own value, and what it is worth today
        public int Factor = 1000;     // ...today's against the game's, thousandths

        public void Clear()
        {
            Name = null;
            District = null;
            Colour = 0;
            Known = true;
            Customers = 0;
            Prices = 0;
            Value = Today = 0;
            Factor = 1000;
        }
    }

    /// <summary>
    /// Everything the Economy app reads of the game on one look, plain data filled by <see cref="OutlookGame"/>: the city's economy,
    /// the jobs it moves, the districts, the market, the council and the player. Reused from look to look; a figure the mod does
    /// not keep, because its feature is off, is left at the mark that says so.
    /// </summary>
    public sealed class OutlookReadings
    {
        public const int Kinds = 6;
        public const int MostDistricts = 16;
        public const int MostSectors = 12;
        public const int MostPlaces = 12;   // of the player's businesses, and of their buildings, listed

        public int Day, Hour;

        // The city's economy.
        public bool CityOn;
        public int Cycle = 1;               // 0 boom, 1 steady, 2 slowdown, 3 recession
        public int MonthsIn = 1;
        public int Level = 1;               // the strength: 0 relaxed to 3 hardcore
        public int Prices = 100000;         // thousandths
        public int Inflation = 200;         // hundredths of a per cent a year
        public int Rate;                    // the bank rate on top of the banks' own, quarter points
        public int RateBp = 300;            // the central bank's own rate, basis points
        public int MonthChange;             // the last month's rise in prices, hundredths of a per cent
        public int LastMove;                // the last decision, quarter points
        public bool Pending;                // a decision is announced for tomorrow
        public int PendingMove;             // ...quarter points
        public int DaysToDecision = 1;
        public int Income = 1000;           // the districts' mean income level, tenths of a point
        public int Footfall;                // the cycle's change to customers, per cent
        public int Spend = 1000;            // what a customer spends against the usual, thousandths
        public int Takings = 1000;          // takings against the game's own figure, thousandths
        public int Costs = 1000;            // rents and wages against the game's own figures, thousandths
        public int Property = 1000;         // property's worth against the game's figure, mean of the districts, thousandths

        // The jobs it moves.
        public bool JobsOn;
        public int Unemployment = 50;       // per mille, among professionals
        public int Vacancies;               // open at once in the city
        public int Seekers;                 // professionals looking for work at once
        public int Layoffs;                 // per mille a day that a job is lost (in a slowdown or a recession)
        public int Hiring;                  // a candidate's score against the usual (points)

        public readonly OutlookSupply[] Supply = new OutlookSupply[Kinds];
        public readonly List<OutlookDistrict> Districts = new List<OutlookDistrict>(MostDistricts);
        private int _districts;

        /// <summary>How many districts this look has filled.</summary>
        public int DistrictCount => _districts;

        // The stock market.
        public bool MarketOn;
        public bool MarketOpen;
        public int Stocks;                  // the index, hundredths
        public int StocksDayBp;
        public readonly List<OutlookSector> Sectors = new List<OutlookSector>(MostSectors);
        private int _sectors;

        // The council's books.
        public bool CouncilOn;
        public long Reserve;                // money at the start's prices
        public int Revenue, Spending;       // a week's, money
        public readonly int[] Departments = new int[4];   // -2 to 2 steps
        public int TaxPercent;              // the game's income tax rate
        public int TaxDue;                  // the bill now owing, money; 0 for none
        public int TaxDay;                  // ...and the day it is due

        // The player.
        public long Debt;                   // everything owed
        public int WagesTomorrow, RentTomorrow;   // money
        public int Shops, Buildings;
        public int Carried;                 // adjustments of the city's economy carried over to the next day, money
        public readonly List<OutlookPlace> Businesses = new List<OutlookPlace>(MostPlaces);
        public readonly List<OutlookPlace> Estates = new List<OutlookPlace>(MostPlaces);
        public int MoreBusinesses, MoreEstates;   // those beyond the ones listed
        private int _businesses, _estates;

        // Changes, by tile (OutlookCatalogue.Tiles), and the districts' own.
        public readonly OutlookDelta[] Short = new OutlookDelta[OutlookCatalogue.Tiles.Length];
        public readonly OutlookDelta[] Long = new OutlookDelta[OutlookCatalogue.Tiles.Length];

        public OutlookReadings()
        {
            for (int k = 0; k < Kinds; k++)
            {
                Supply[k] = new OutlookSupply();
            }
        }

        /// <summary>A district's record to fill, new or kept from the last look; call <see cref="Done"/> after the last.</summary>
        public OutlookDistrict NextDistrict()
        {
            OutlookDistrict d;
            if (_districts < Districts.Count)
            {
                d = Districts[_districts];
            }
            else
            {
                d = new OutlookDistrict();
                Districts.Add(d);
            }
            d.Clear();
            _districts++;
            return d;
        }

        /// <summary>A business's record to fill, new or kept from the last look.</summary>
        public OutlookPlace NextBusiness()
        {
            return Next(Businesses, ref _businesses);
        }

        /// <summary>A building's record to fill, new or kept from the last look.</summary>
        public OutlookPlace NextEstate()
        {
            return Next(Estates, ref _estates);
        }

        private static OutlookPlace Next(List<OutlookPlace> list, ref int used)
        {
            OutlookPlace p;
            if (used < list.Count)
            {
                p = list[used];
            }
            else
            {
                p = new OutlookPlace();
                list.Add(p);
            }
            p.Clear();
            used++;
            return p;
        }

        public OutlookSector NextSector()
        {
            OutlookSector s;
            if (_sectors < Sectors.Count)
            {
                s = Sectors[_sectors];
            }
            else
            {
                s = new OutlookSector();
                Sectors.Add(s);
            }
            _sectors++;
            return s;
        }

        /// <summary>Begins a look: no districts and no sectors yet.</summary>
        public void Begin()
        {
            _districts = 0;
            _sectors = 0;
            _businesses = 0;
            _estates = 0;
            Carried = MoreBusinesses = MoreEstates = 0;
            CityOn = JobsOn = MarketOn = CouncilOn = false;
            for (int k = 0; k < Kinds; k++)
            {
                Supply[k].Clear();
            }
            for (int i = 0; i < Short.Length; i++)
            {
                Short[i] = Long[i] = OutlookDelta.None;
            }
        }

        /// <summary>Ends a look: the records beyond those filled are dropped.</summary>
        public void Done()
        {
            if (Districts.Count > _districts)
            {
                Districts.RemoveRange(_districts, Districts.Count - _districts);
            }
            if (Sectors.Count > _sectors)
            {
                Sectors.RemoveRange(_sectors, Sectors.Count - _sectors);
            }
            if (Businesses.Count > _businesses)
            {
                Businesses.RemoveRange(_businesses, Businesses.Count - _businesses);
            }
            if (Estates.Count > _estates)
            {
                Estates.RemoveRange(_estates, Estates.Count - _estates);
            }
        }
    }
}
