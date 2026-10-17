using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What the planner advises for one business, and what each choice comes to in a week.</summary>
    public sealed class RotaOutlook
    {
        public readonly int[] Net = new int[RotaCatalogue.Cells];       // what opening the hour makes with the crew worth having
        public readonly int[] Crew = new int[RotaCatalogue.Cells];      // service stations that crew stands at
        public readonly bool[] Best = new bool[RotaCatalogue.Cells];    // the hours worth opening, with all the staff they need
        public readonly bool[] Staffed = new bool[RotaCatalogue.Cells]; // of those, the hours the present staff can keep
        public bool Advised;            // hours were worked out; false where the business keeps the hours it has
        public long ProfitNow, ProfitStaffed, ProfitBest;
        public int HoursNow, HoursStaffed, HoursBest;
        public bool Barred;             // paying hours were left out for want of a late opening permit...
        public long ProfitFree;         // ...what a week with them would come to, with all the staff it takes...
        public int HoursFree;           // ...open for so many hours
        public int CustomersWeek;
        public readonly RotaPlan Plan = new RotaPlan();                 // the present staff's rota for Staffed
        public readonly List<RotaRole> Roles = new List<RotaRole>(8);
        public readonly List<RotaMissing> Missing = new List<RotaMissing>(4);
    }

    /// <summary>How many people a group of stations needs for the advised hours, against how many the business has.</summary>
    public struct RotaRole
    {
        public int Group;
        public int Hours;       // station hours a week
        public int Peak;        // stations at once
        public int Need, Have, Hire, Spare;
        public long Cost;       // a week, for those to hire
    }

    /// <summary>What one more station of a group would bring on its best day.</summary>
    public struct RotaMissing
    {
        public int Group, Day, Customers;
    }
}
