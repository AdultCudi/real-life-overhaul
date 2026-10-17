using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// One business as the planner's arithmetic sees it: stations in groups, the customers the game would send each hour of
    /// the week, what a customer is worth and what an hour of somebody costs. Filled by RotaGame; money in hundredths.
    /// </summary>
    public sealed class RotaShop
    {
        public int Stations, Groups;
        public int[] Group = new int[0];            // [station] its group
        public int[] Cap = new int[0];              // [station] customers an hour it serves when staffed; 0: none itself
        public int[] StationWage = new int[0];      // [station] an hour of somebody at it, for hours nobody is rostered for yet
        public byte[] GroupKind = new byte[0];      // [group] RotaGroupKind
        public bool[] Taken = new bool[0];          // [cell * Stations + station] somebody the planner leaves alone stands there
        public int[] GroupRoom = new int[0];        // [group] the most customers an hour the rest of the building allows; 0: not known
        public readonly int[] Customers = new int[RotaCatalogue.Cells];
        public readonly int[] Wanting = new int[RotaCatalogue.Cells];   // customers were the stations no limit (the game's shop forecast stops at them)
        public readonly bool[] Open = new bool[RotaCatalogue.Cells];    // the hours it keeps now
        public readonly int[] DayFee = new int[RotaCatalogue.Days];     // the game's licensing fee, charged on a day it opens at all
        public int Margin;              // takings less the cost of the goods, a customer served
        public int Takings;             // a customer served
        public bool HasCustomers;       // false: nobody walks in (a factory, a headquarters): every work station is wanted every open hour
        public bool FixedHours;         // the game sets the hours (a headquarters): none are advised
        public int DirtEvents;          // hundredths of a dirtying, a customer served
        public int CleanHours;          // thousandths of a cleaner's hour to clean one dirtying away
        public int Version;             // raised on every filling, so that what was worked out for the last is not reused

        // What the ladder of plans counts beside customers and wages; nothing where the game gives no figure.
        public int[] GroupGoing = new int[0];   // [group] hundredths an hour for somebody new; 0: the station's wage is taken
        public int DirtPerCustomer;     // thousandths of a point of the game's mean dirt, a customer served
        public int CleanPerHour;        // thousandths of a point of it one cleaner's hour takes off
        public int Content = 75;        // the customers' satisfaction now, 0 to 100 (the game's overall figure)
        public int CleanNow = 100;      // the cleanliness the last days' customers found, the one Margin was earned at
        public int TheftRate;           // hundredths the game's hourly theft takes for each customer who comes in the hour...
        public int TheftLimit;          // ...while fewer than this come (from there its arithmetic takes nothing); 0: no such end
        public int NightRisk;           // hundredths a night it is shut at one in the morning (the mod's break-ins come only then)
        public bool Cleaned;            // the cleaning service cleans it: no cleaning shift is wanted and the floor stays as it is
        public int HireCost;            // hundredths paid once for each person taken on (the game asks nothing unless a signing bonus is offered)
        public readonly RotaStock Stock = new RotaStock();  // its goods, product by product; none where nothing is sold off a shelf

        /// <summary>Makes room for so many stations and groups and clears every figure.</summary>
        public void Size(int stations, int groups)
        {
            Version++;
            GroupGoing = RotaProblem.Room(GroupGoing, groups);
            DirtPerCustomer = CleanPerHour = TheftRate = TheftLimit = NightRisk = HireCost = 0;
            Content = 75;
            CleanNow = 100;
            Cleaned = false;
            Stations = stations;
            Groups = groups;
            Group = RotaProblem.Room(Group, stations);
            Cap = RotaProblem.Room(Cap, stations);
            StationWage = RotaProblem.Room(StationWage, stations);
            GroupKind = RotaProblem.Room(GroupKind, groups);
            Taken = RotaProblem.Room(Taken, RotaCatalogue.Cells * stations);
            GroupRoom = RotaProblem.Room(GroupRoom, groups);
            Array.Clear(Customers, 0, Customers.Length);
            Array.Clear(Wanting, 0, Wanting.Length);
            Array.Clear(Open, 0, Open.Length);
            Array.Clear(DayFee, 0, DayFee.Length);
            Margin = Takings = DirtEvents = CleanHours = 0;
            HasCustomers = FixedHours = false;
            Stock.Clear();
        }
    }
}
