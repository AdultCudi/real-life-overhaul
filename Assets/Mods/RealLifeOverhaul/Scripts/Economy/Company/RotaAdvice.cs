using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// One business as the planner's arithmetic sees it: its stations in groups, the customers the game would send in
    /// each hour of the week if it were open, what a customer is worth, and what an hour of somebody costs. Filled
    /// from the game by RotaGame; nothing here knows the game. Money is in hundredths.
    /// </summary>
    public sealed class RotaShop
    {
        public int Stations, Groups;
        public int[] Group = new int[0];            // [station] the group it belongs to
        public int[] Cap = new int[0];              // [station] customers an hour it serves when somebody stands at it; 0: none itself
        public int[] StationWage = new int[0];      // [station] what an hour of somebody at it costs, for hours nobody is rostered for yet
        public byte[] GroupKind = new byte[0];      // [group] RotaGroupKind
        public bool[] Taken = new bool[0];          // [cell * Stations + station] somebody the planner leaves alone stands there
        public int[] GroupRoom = new int[0];        // [group] the most customers an hour everything else in the building allows, were this group no limit; 0: not known
        public readonly int[] Customers = new int[RotaCatalogue.Cells];
        public readonly int[] Wanting = new int[RotaCatalogue.Cells];   // the customers who would come if the stations there are were no limit (the game's forecast for a shop stops at what they serve)
        public readonly bool[] Open = new bool[RotaCatalogue.Cells];    // the hours it keeps now
        public readonly int[] DayFee = new int[RotaCatalogue.Days];     // charged once on a day it opens at all (the game's licensing fees)
        public int Margin;              // takings less the cost of the goods, a customer served
        public int Takings;             // a customer served
        public bool HasCustomers;       // false: nobody walks in (a factory, a headquarters): every work station is wanted every open hour
        public bool FixedHours;         // the game sets the hours (a headquarters): none are advised
        public int DirtEvents;          // hundredths of a time the game dirties something, a customer served
        public int CleanHours;          // thousandths of a cleaner's hour it takes to clean one such time away
        public int Version;             // goes up each time it is filled again, so that what was worked out for the last filling is not used

        public void Size(int stations, int groups)
        {
            Version++;
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
        }
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

    /// <summary>What the planner advises for one business, and what each choice comes to in a week.</summary>
    public sealed class RotaOutlook
    {
        public readonly int[] Net = new int[RotaCatalogue.Cells];       // what opening the hour makes with the crew worth having
        public readonly int[] Crew = new int[RotaCatalogue.Cells];      // how many service stations that crew stands at
        public readonly bool[] Best = new bool[RotaCatalogue.Cells];    // the hours worth opening, with all the staff they need
        public readonly bool[] Staffed = new bool[RotaCatalogue.Cells]; // the hours worth opening that the present staff can keep
        public bool Advised;            // hours were worked out; false where the business keeps the hours it has (see RotaShop)
        public long ProfitNow, ProfitStaffed, ProfitBest;
        public int HoursNow, HoursStaffed, HoursBest;
        public bool Barred;             // hours that would pay were left out of the advice because the business may not open then (no late opening permit)...
        public long ProfitFree;         // ...and this is what a week with them would come to, with all the staff it takes...
        public int HoursFree;           // ...open for so many hours
        public int CustomersWeek;
        public readonly RotaPlan Plan = new RotaPlan();                 // the rota of the present staff for <see cref="Staffed"/>
        public readonly List<RotaRole> Roles = new List<RotaRole>(8);
        public readonly List<RotaMissing> Missing = new List<RotaMissing>(4);
    }

    /// <summary>
    /// The planner's advice, by rule: which stations an hour's customers are worth staffing, which hours are worth
    /// opening, how many people that takes, and what a week comes to. Game-free.
    ///
    /// THE GAME'S OWN ARITHMETIC, AS IT IS USED HERE
    ///   customers served   in an hour the game serves no more customers than the stations with somebody at them
    ///                      allow: stations of one group add up, and the smallest group's sum is the limit
    ///                      (ItemHelper.GetItemsSortedByCapacity with requireEmployee, as RetailBusinessSimulator
    ///                      calls it; an office serves one customer a staffed desk, OfficeBusinessSimulator). Here a
    ///                      group with nobody at all serves nobody, so the smallest crew is one at each group
    ///   cleaning           each customer served dirties the floor some, and an hour of a cleaner's shift cleans
    ///                      some of it away (BuildingCleanliness.ApplyDirt and SimulateCleaning); RotaGame turns the
    ///                      game's figures into the two rates kept in <see cref="RotaShop"/>
    ///
    /// AN HOUR IS WORTH OPENING when the customers its best crew serves bring more than that crew's wages. A day's
    /// hours are advised as blocks: at most <see cref="RotaCatalogue.BlocksMost"/> of at least
    /// <see cref="RotaCatalogue.BlockLeast"/> hours, inside the day (the game's own hours never run past midnight),
    /// and only if they bring more than the day's fee. Hours the business may not open (no late opening permit) are
    /// never advised; what they would add is worked out beside the advice.
    ///
    /// Cost: nothing unless asked. An advice is worked out in one go (<see cref="Advise"/>) or a few steps at a time
    /// (<see cref="Begin"/> and <see cref="Step"/>), with the same result; nothing is allocated either way.
    /// </summary>
    public sealed class RotaAdvisor
    {
        private const int Cells = RotaCatalogue.Cells, Hours = RotaCatalogue.Hours, Days = RotaCatalogue.Days;

        private int[] _groupStart = new int[0], _groupList = new int[0];   // each group's stations, the one that serves most first
        private int[] _taken = new int[0], _cap = new int[0], _taken2 = new int[0], _cap2 = new int[0];
        private bool[] _need = new bool[0];
        private readonly int[] _ideal = new int[Cells];       // customers the crew worth having serves, by the last Grid
        private readonly bool[] _blocked = new bool[Cells];
        private readonly int[] _pre = new int[Hours + 1], _bad = new int[Hours + 1];
        private readonly int[] _firstValue = new int[Hours + 1], _firstFrom = new int[Hours + 1], _firstTo = new int[Hours + 1];
        private readonly bool[] _free = new bool[Cells];      // the hours worth opening with no bar on any of them

        // Where the working out of an advice stands, so that it can stop after any step and go on from there.
        private const int StageHours = 0, StageFree = 1, StageBest = 2, StagePeople = 3, StageRota = 4, StageCut = 5, StageRotaAgain = 6, StageSums = 7, StageDone = 8;
        private int _stage = StageDone;
        private RotaShop _shop;
        private RotaProblem _problem;
        private RotaSolver _solver;
        private RotaOutlook _into;
        private short[] _present;
        private bool[] _barred;
        private RotaShop _sorted;
        private int _sortedVersion;

        // ---------------------------------------------------------------- an hour's crew

        private void Sort(RotaShop shop)
        {
            if (ReferenceEquals(_sorted, shop) && _sortedVersion == shop.Version)
            {
                return;
            }
            _sorted = shop;
            _sortedVersion = shop.Version;
            int s = shop.Stations, g = shop.Groups;
            _groupStart = RotaProblem.Room(_groupStart, g + 1);
            _groupList = RotaProblem.Room(_groupList, s);
            _taken = RotaProblem.Room(_taken, g);
            _cap = RotaProblem.Room(_cap, g);
            _taken2 = RotaProblem.Room(_taken2, g);
            _cap2 = RotaProblem.Room(_cap2, g);
            _need = RotaProblem.Room(_need, s);
            int n = 0;
            for (int group = 0; group < g; group++)
            {
                _groupStart[group] = n;
                for (int station = 0; station < s; station++)
                {
                    if (shop.Group[station] != group)
                    {
                        continue;
                    }
                    int j = n++;
                    while (j > _groupStart[group] && shop.Cap[_groupList[j - 1]] < shop.Cap[station])
                    {
                        _groupList[j] = _groupList[j - 1];
                        j--;
                    }
                    _groupList[j] = station;
                }
            }
            _groupStart[g] = n;
        }

        /// <summary>
        /// The crew worth having for an hour with so many customers: <paramref name="need"/> says which stations.
        /// One at each service group to begin with; a further station where the customers it adds bring more than
        /// it costs. Support stations are always wanted. Where nobody walks in, every work station is.
        /// </summary>
        public void Crew(RotaShop shop, int customers, bool[] need, out int served, out int wage, out int count)
        {
            Sort(shop);
            served = wage = count = 0;
            int stations = shop.Stations, groups = shop.Groups, services = 0;
            for (int s = 0; s < stations; s++)
            {
                need[s] = shop.GroupKind[shop.Group[s]] == RotaGroupKind.Support || (!shop.HasCustomers && shop.GroupKind[shop.Group[s]] == RotaGroupKind.Service);
            }
            if (!shop.HasCustomers)
            {
                for (int s = 0; s < stations; s++)
                {
                    if (need[s])
                    {
                        wage += shop.StationWage[s];
                        count++;
                    }
                }
                return;
            }
            for (int g = 0; g < groups; g++)
            {
                _taken[g] = _cap[g] = 0;
                if (shop.GroupKind[g] == RotaGroupKind.Service && _groupStart[g + 1] > _groupStart[g])
                {
                    int first = _groupList[_groupStart[g]];
                    _taken[g] = 1;
                    _cap[g] = shop.Cap[first];
                    wage += shop.StationWage[first];
                    services++;
                }
            }
            if (services == 0)
            {
                // No station here limits the customers: one person at work serves them all, the cheapest to have there.
                int one = -1;
                for (int s = 0; s < stations; s++)
                {
                    if (need[s] && (one < 0 || shop.StationWage[s] < shop.StationWage[one]))
                    {
                        one = s;
                    }
                }
                served = one >= 0 ? customers : 0;
                wage = one >= 0 ? shop.StationWage[one] : 0;
                count = one >= 0 ? 1 : 0;
                return;
            }
            int now = Least(shop, _cap, customers);
            while (now < customers)
            {
                // The groups that hold the customers back each get one more station until more are served; kept if that pays.
                int cost = 0, next = now;
                for (int g = 0; g < groups; g++)
                {
                    _taken2[g] = _taken[g];
                    _cap2[g] = _cap[g];
                }
                while (next <= now)
                {
                    int tight = -1;
                    for (int g = 0; g < groups; g++)
                    {
                        if (shop.GroupKind[g] == RotaGroupKind.Service && _taken2[g] > 0 && (tight < 0 || _cap2[g] < _cap2[tight]))
                        {
                            tight = g;
                        }
                    }
                    if (tight < 0 || _groupStart[tight] + _taken2[tight] >= _groupStart[tight + 1])
                    {
                        next = -1;
                        break;
                    }
                    int station = _groupList[_groupStart[tight] + _taken2[tight]];
                    if (shop.Cap[station] <= 0)
                    {
                        next = -1;
                        break;
                    }
                    _taken2[tight]++;
                    _cap2[tight] += shop.Cap[station];
                    cost += shop.StationWage[station];
                    next = Least(shop, _cap2, customers);
                }
                if (next < 0 || (long)(next - now) * shop.Margin <= cost)
                {
                    break;
                }
                for (int g = 0; g < groups; g++)
                {
                    _taken[g] = _taken2[g];
                    _cap[g] = _cap2[g];
                }
                wage += cost;
                now = next;
            }
            served = now;
            for (int g = 0; g < groups; g++)
            {
                for (int k = 0; k < _taken[g]; k++)
                {
                    need[_groupList[_groupStart[g] + k]] = true;
                    count++;
                }
            }
        }

        // The customers the staffed groups serve between them: the smallest sum, and no more than come.
        private int Least(RotaShop shop, int[] cap, int customers)
        {
            int least = customers;
            for (int g = 0; g < shop.Groups; g++)
            {
                if (shop.GroupKind[g] == RotaGroupKind.Service && _groupStart[g + 1] > _groupStart[g] && cap[g] < least)
                {
                    least = cap[g];
                }
            }
            return least;
        }

        // ---------------------------------------------------------------- the hours worth opening

        /// <summary>What opening each hour of the week makes with the crew worth having, and how many stand in that crew.</summary>
        public void Net(RotaShop shop, int[] net, int[] crew)
        {
            Sort(shop);
            for (int cell = 0; cell < Cells; cell++)
            {
                int served, wage, count;
                Crew(shop, shop.Customers[cell], _need, out served, out wage, out count);
                long made = (long)served * shop.Margin - wage;
                net[cell] = shop.HasCustomers ? (int)Math.Max(int.MinValue / 64, Math.Min(int.MaxValue / 64, made)) : 0;
                crew[cell] = count;
            }
        }

        /// <summary>
        /// The hours to open, day by day: the block or two blocks of hours that make the most, never one that holds
        /// a blocked hour, and none on a day that would not make more than its fee.
        /// </summary>
        public void Blocks(int[] net, bool[] blocked, int[] dayFee, bool[] open)
        {
            for (int d = 0; d < Days; d++)
            {
                int at = d * Hours;
                for (int h = 0; h < Hours; h++)
                {
                    open[at + h] = false;
                    _pre[h + 1] = _pre[h] + net[at + h];
                    _bad[h + 1] = _bad[h] + (blocked != null && blocked[at + h] ? 1 : 0);
                }
                // The best one block that ends by each hour.
                int best = 0, a1 = -1, b1 = -1, a2 = -1, b2 = -1;
                _firstValue[0] = 0;
                _firstFrom[0] = _firstTo[0] = -1;
                for (int b = 1; b <= Hours; b++)
                {
                    _firstValue[b] = _firstValue[b - 1];
                    _firstFrom[b] = _firstFrom[b - 1];
                    _firstTo[b] = _firstTo[b - 1];
                    for (int a = 0; a + RotaCatalogue.BlockLeast <= b; a++)
                    {
                        int value = _pre[b] - _pre[a];
                        if (_bad[b] - _bad[a] == 0 && value > _firstValue[b])
                        {
                            _firstValue[b] = value;
                            _firstFrom[b] = a;
                            _firstTo[b] = b;
                        }
                    }
                }
                if (_firstValue[Hours] > 0)
                {
                    best = _firstValue[Hours];
                    a1 = _firstFrom[Hours];
                    b1 = _firstTo[Hours];
                }
                // A second block after a gap of at least an hour, if the two make more than the best one.
                for (int a = 2; RotaCatalogue.BlocksMost > 1 && a + RotaCatalogue.BlockLeast <= Hours; a++)
                {
                    if (_firstValue[a - 1] <= 0)
                    {
                        continue;
                    }
                    for (int b = a + RotaCatalogue.BlockLeast; b <= Hours; b++)
                    {
                        int value = _pre[b] - _pre[a];
                        if (_bad[b] - _bad[a] == 0 && value > 0 && _firstValue[a - 1] + value > best)
                        {
                            best = _firstValue[a - 1] + value;
                            a1 = _firstFrom[a - 1];
                            b1 = _firstTo[a - 1];
                            a2 = a;
                            b2 = b;
                        }
                    }
                }
                if (best <= dayFee[d])
                {
                    continue;
                }
                for (int h = 0; h < Hours; h++)
                {
                    open[at + h] = (h >= a1 && h < b1) || (h >= a2 && h < b2);
                }
            }
        }

        // ---------------------------------------------------------------- what each hour at each station is worth

        /// <summary>
        /// Writes what an hour at each station is worth into a problem, for a week of opening hours: the stations of
        /// each hour's crew by the customers each adds, support stations and the day's cleaning at the least there is.
        /// </summary>
        public void Grid(RotaShop shop, bool[] open, RotaProblem into)
        {
            Sort(shop);
            int stations = shop.Stations;
            Array.Clear(into.Value, 0, Cells * stations);
            for (int d = 0; d < Days; d++)
            {
                long dirt = 0;   // hundredths of the game's dirtying
                int openHours = 0;
                for (int h = 0; h < Hours; h++)
                {
                    int cell = d * Hours + h;
                    _ideal[cell] = 0;
                    if (!open[cell])
                    {
                        continue;
                    }
                    openHours++;
                    int customers = shop.Customers[cell], served, wage, count;
                    Crew(shop, customers, _need, out served, out wage, out count);
                    _ideal[cell] = served;
                    dirt += shop.HasCustomers ? (long)served * shop.DirtEvents : count * 100L;
                    if (!shop.HasCustomers)
                    {
                        for (int s = 0; s < stations; s++)
                        {
                            into.Value[cell * stations + s] = _need[s] && !shop.Taken[cell * stations + s] ? RotaCatalogue.ValueWork : 0;
                        }
                        continue;
                    }
                    bool anyService = false;
                    for (int g = 0; g < shop.Groups; g++)
                    {
                        if (shop.GroupKind[g] != RotaGroupKind.Service)
                        {
                            continue;
                        }
                        int sum = 0;
                        for (int k = _groupStart[g]; k < _groupStart[g + 1]; k++)
                        {
                            int s = _groupList[k];
                            if (!_need[s])
                            {
                                break;
                            }
                            anyService = true;
                            int more = Math.Min(customers, sum + shop.Cap[s]) - Math.Min(customers, sum);
                            sum += shop.Cap[s];
                            into.Value[cell * stations + s] = shop.Taken[cell * stations + s] ? 0 : Worth(more, shop.Margin);
                        }
                    }
                    bool first = !anyService;   // (no station limits the customers: the one person of the smallest crew is worth all of them)
                    for (int s = 0; s < stations; s++)
                    {
                        if (shop.GroupKind[shop.Group[s]] != RotaGroupKind.Support || shop.Taken[cell * stations + s])
                        {
                            continue;
                        }
                        into.Value[cell * stations + s] = first ? Worth(customers, shop.Margin) : RotaCatalogue.ValueSupport;
                        first = false;
                    }
                }
                // The day's cleaning: the hours it takes, at the end of the day when the dirt is there to clean.
                long cleaning = dirt > 0 && shop.CleanHours > 0 ? Math.Max(1L, (dirt * shop.CleanHours + 99999L) / 100000L) : 0L;
                for (int s = 0; s < stations && cleaning > 0 && openHours > 0; s++)
                {
                    if (shop.GroupKind[shop.Group[s]] != RotaGroupKind.Cleaning)
                    {
                        continue;
                    }
                    for (int h = Hours - 1; h >= 0 && cleaning > 0; h--)
                    {
                        int cell = d * Hours + h;
                        if (open[cell] && !shop.Taken[cell * stations + s])
                        {
                            into.Value[cell * stations + s] = RotaCatalogue.ValueSupport;
                            cleaning--;
                        }
                    }
                }
            }
        }

        // What customers are worth to the planner, in whole money; never less than the floor, so that the smallest crew is always wanted.
        private static int Worth(int customers, int margin)
        {
            long worth = (long)customers * Math.Max(0, margin) / 100L;
            return (int)Math.Max(RotaCatalogue.ValueFloor, Math.Min(1000000L, worth));
        }

        // ---------------------------------------------------------------- what a week comes to

        /// <summary>
        /// The customers served in one hour by whoever stands at the stations then: those of a rota
        /// (<paramref name="at"/>, may be null), those the planner leaves alone, and with <paramref name="fill"/>
        /// everybody the last <see cref="Grid"/> wanted (<paramref name="value"/>).
        /// </summary>
        public int Served(RotaShop shop, int cell, short[] at, int[] value, bool fill)
        {
            Sort(shop);
            if (!shop.HasCustomers)
            {
                return 0;
            }
            int stations = shop.Stations, least = shop.Customers[cell], services = 0;
            bool anyone = false;
            for (int g = 0; g < shop.Groups; g++)
            {
                _cap[g] = 0;
            }
            for (int s = 0; s < stations; s++)
            {
                int i = cell * stations + s;
                if (shop.Taken[i] || (at != null && at[i] >= 0) || (fill && value[i] > 0))
                {
                    _cap[shop.Group[s]] += shop.Cap[s];
                    anyone |= shop.GroupKind[shop.Group[s]] != RotaGroupKind.Cleaning;
                }
            }
            for (int g = 0; g < shop.Groups; g++)
            {
                if (shop.GroupKind[g] == RotaGroupKind.Service && _groupStart[g + 1] > _groupStart[g])
                {
                    services++;
                    least = Math.Min(least, _cap[g]);
                }
            }
            return services > 0 ? least : anyone ? least : 0;
        }

        /// <summary>
        /// A week in money: what the customers served bring, less the wages of everybody at a station and the fee of
        /// each day it opens. Hours somebody is still to be hired for are counted at the station's wage.
        /// </summary>
        public long Profit(RotaShop shop, RotaProblem problem, bool[] open, short[] at, bool fill)
        {
            int stations = shop.Stations;
            long sum = 0;
            for (int d = 0; d < Days; d++)
            {
                bool any = false;
                for (int h = 0; h < Hours; h++)
                {
                    int cell = d * Hours + h;
                    if (open[cell])
                    {
                        any = true;
                        sum += (long)Served(shop, cell, at, problem.Value, fill) * shop.Margin;
                    }
                    for (int s = 0; s < stations; s++)
                    {
                        int i = cell * stations + s, w = at != null ? at[i] : -1;
                        if (w >= 0)
                        {
                            sum -= problem.Worker[w].Wage;
                        }
                        else if (shop.Taken[i] || (fill && open[cell] && problem.Value[i] > 0))
                        {
                            sum -= shop.StationWage[s];
                        }
                    }
                }
                sum -= any ? shop.DayFee[d] : 0;
            }
            return sum;
        }

        // ---------------------------------------------------------------- people and stations

        /// <summary>
        /// Group by group, the people the wanted hours take against the people there are. Somebody counts for the
        /// first group they can stand at. A person is counted as good for the hours a week their own demand allows,
        /// forty with none.
        /// </summary>
        public void Roles(RotaShop shop, RotaProblem problem, List<RotaRole> into)
        {
            into.Clear();
            int stations = shop.Stations;
            for (int g = 0; g < shop.Groups; g++)
            {
                var role = new RotaRole { Group = g };
                long wages = 0;
                for (int cell = 0; cell < Cells; cell++)
                {
                    int at = 0;
                    for (int s = 0; s < stations; s++)
                    {
                        if (shop.Group[s] == g && problem.Value[cell * stations + s] > 0)
                        {
                            at++;
                            wages += shop.StationWage[s];
                        }
                    }
                    role.Hours += at;
                    role.Peak = Math.Max(role.Peak, at);
                }
                int reach = 0, able = 0;
                for (int w = 0; w < problem.Workers; w++)
                {
                    int home = -1;
                    for (int s = 0; s < stations && home < 0; s++)
                    {
                        home = problem.Can[w * stations + s] != 0 ? shop.Group[s] : -1;
                    }
                    if (home != g)
                    {
                        continue;
                    }
                    role.Have++;
                    able++;
                    reach += WeekOf(problem.Worker[w]);
                }
                reach = able > 0 ? Math.Max(1, reach / able) : RotaCatalogue.FullWeek;
                role.Need = Math.Max(role.Peak, (role.Hours + reach - 1) / reach);
                role.Hire = Math.Max(0, role.Need - role.Have);
                role.Spare = Math.Max(0, role.Have - role.Need);
                role.Cost = role.Need > 0 ? wages * role.Hire / role.Need : 0;
                if (role.Hours > 0 || role.Have > 0)
                {
                    into.Add(role);
                }
            }
        }

        private static int WeekOf(RotaWorker worker)
        {
            int most = RotaCatalogue.FullWeek;
            for (int i = 0; i < worker.Demands.Length; i++)
            {
                if (worker.Demands[i].Kind == RotaDemandKind.Hours && worker.Demands[i].B > 0)
                {
                    most = Math.Min(most, worker.Demands[i].B);
                }
            }
            return most;
        }

        /// <summary>
        /// Where the customers who would come outnumber what every station of a group can serve: what one more
        /// station like its largest would serve on its best day, counting only hours in which that pays its wage,
        /// and no more than the other groups and the rest of the building allow.
        /// </summary>
        public void Missing(RotaShop shop, bool[] open, List<RotaMissing> into)
        {
            Sort(shop);
            into.Clear();
            if (!shop.HasCustomers)
            {
                return;
            }
            for (int g = 0; g < shop.Groups; g++)
            {
                _cap[g] = 0;
                for (int k = _groupStart[g]; k < _groupStart[g + 1]; k++)
                {
                    _cap[g] += shop.Cap[_groupList[k]];
                }
            }
            for (int g = 0; g < shop.Groups; g++)
            {
                if (shop.GroupKind[g] != RotaGroupKind.Service || _groupStart[g + 1] == _groupStart[g])
                {
                    continue;
                }
                int largest = _groupList[_groupStart[g]], others = shop.GroupRoom[g] > 0 ? shop.GroupRoom[g] : int.MaxValue, bestDay = -1, bestMore = 0;
                for (int k = 0; k < shop.Groups; k++)
                {
                    if (k != g && shop.GroupKind[k] == RotaGroupKind.Service && _groupStart[k + 1] > _groupStart[k])
                    {
                        others = Math.Min(others, _cap[k]);
                    }
                }
                for (int d = 0; d < Days; d++)
                {
                    int more = 0;
                    for (int h = 0; h < Hours; h++)
                    {
                        int cell = d * Hours + h, customers = Math.Min(Math.Max(shop.Wanting[cell], shop.Customers[cell]), others);
                        int extra = open[cell] ? Math.Min(customers, _cap[g] + shop.Cap[largest]) - Math.Min(customers, _cap[g]) : 0;
                        more += (long)extra * shop.Margin > shop.StationWage[largest] ? extra : 0;
                    }
                    if (more > bestMore)
                    {
                        bestMore = more;
                        bestDay = d;
                    }
                }
                if (bestDay >= 0)
                {
                    into.Add(new RotaMissing { Group = g, Day = bestDay, Customers = bestMore });
                }
            }
        }

        // ---------------------------------------------------------------- the whole advice

        /// <summary>
        /// Everything the page shows for one business, in one go. <paramref name="present"/> is the rota it has now;
        /// <paramref name="barred"/> (may be null) the hours it may not open, which are never advised.
        /// Afterwards <paramref name="problem"/> holds the worth of the hours of <see cref="RotaOutlook.Staffed"/>,
        /// and <see cref="RotaOutlook.Plan"/> the rota of the present staff for them.
        /// </summary>
        public void Advise(RotaShop shop, RotaProblem problem, RotaSolver solver, short[] present, bool[] barred, RotaOutlook into)
        {
            Begin(shop, problem, solver, present, barred, into);
            while (!Step(int.MaxValue))
            {
            }
        }

        /// <summary>
        /// Begins the same advice, to be worked out a few steps at a time by <see cref="Step"/> (the director spreads
        /// them over frames). The steps are the same in the same order, so the advice is the same. Nothing given
        /// here may change until the last step is done.
        /// </summary>
        public void Begin(RotaShop shop, RotaProblem problem, RotaSolver solver, short[] present, bool[] barred, RotaOutlook into)
        {
            _shop = shop;
            _problem = problem;
            _solver = solver;
            _present = present;
            _barred = barred;
            _into = into;
            _stage = StageHours;
        }

        /// <summary>Does so many steps more: a step is one stage of the arithmetic, or one step of a rota. True when the advice is finished.</summary>
        public bool Step(int steps)
        {
            while (steps-- > 0 && _stage != StageDone)
            {
                One();
            }
            return _stage == StageDone;
        }

        private void One()
        {
            RotaShop shop = _shop;
            RotaOutlook into = _into;
            switch (_stage)
            {
                case StageHours:
                    Net(shop, into.Net, into.Crew);
                    into.Advised = shop.HasCustomers && !shop.FixedHours && shop.Margin > 0 && shop.Stations > 0;
                    into.CustomersWeek = 0;
                    for (int cell = 0; cell < Cells; cell++)
                    {
                        into.CustomersWeek += shop.Customers[cell];
                    }
                    into.Barred = false;
                    into.ProfitFree = 0;
                    into.HoursFree = 0;
                    if (into.Advised)
                    {
                        Blocks(into.Net, _barred, shop.DayFee, into.Best);
                    }
                    else
                    {
                        Array.Copy(shop.Open, into.Best, Cells);
                    }
                    _stage = into.Advised && _barred != null ? StageFree : StageBest;
                    break;
                case StageFree:
                    // What the hours it may not open would add: the advice once more without the bar, if that opens any hour more.
                    Blocks(into.Net, null, shop.DayFee, _free);
                    for (int cell = 0; cell < Cells; cell++)
                    {
                        into.Barred |= _free[cell] != into.Best[cell];
                    }
                    if (into.Barred)
                    {
                        Grid(shop, _free, _problem);
                        into.ProfitFree = Profit(shop, _problem, _free, null, true);
                        into.HoursFree = Count(_free);
                    }
                    _stage = StageBest;
                    break;
                case StageBest:
                    // With everybody the hours need.
                    Grid(shop, into.Best, _problem);
                    into.ProfitBest = Profit(shop, _problem, into.Best, null, true);
                    _stage = StagePeople;
                    break;
                case StagePeople:
                    Roles(shop, _problem, into.Roles);
                    Missing(shop, into.Best, into.Missing);
                    _solver.Start(_problem, into.Plan);
                    _stage = StageRota;
                    break;
                case StageRota:
                    _stage = _solver.Step(1) ? StageCut : StageRota;
                    break;
                case StageCut:
                    // With the staff there are: an hour nobody can keep the smallest crew for is not opened, and the day's blocks are cut again without it.
                    Array.Copy(into.Best, into.Staffed, Cells);
                    bool cut = false;
                    for (int cell = 0; into.Advised && cell < Cells; cell++)
                    {
                        bool uncrewed = into.Best[cell] && !Crewed(shop, cell, into.Plan.At);
                        _blocked[cell] = uncrewed || (_barred != null && _barred[cell]);
                        cut |= uncrewed;
                    }
                    if (cut)
                    {
                        Blocks(into.Net, _blocked, shop.DayFee, into.Staffed);
                        Grid(shop, into.Staffed, _problem);
                        _solver.Start(_problem, into.Plan);
                    }
                    _stage = cut ? StageRotaAgain : StageSums;
                    break;
                case StageRotaAgain:
                    _stage = _solver.Step(1) ? StageSums : StageRotaAgain;
                    break;
                case StageSums:
                    into.ProfitStaffed = Profit(shop, _problem, into.Staffed, into.Plan.At, false);
                    into.ProfitNow = Profit(shop, _problem, shop.Open, _present, false);
                    into.HoursNow = Count(shop.Open);
                    into.HoursStaffed = Count(into.Staffed);
                    into.HoursBest = Count(into.Best);
                    _stage = StageDone;
                    break;
            }
        }

        /// <summary>
        /// The hours a business may not open without the mod's late opening permit: from
        /// <see cref="CompanyCatalogue.LateHour"/> on, and before <see cref="CompanyCatalogue.EarlyHour"/>. A block
        /// of hours that holds none of them is not late by <see cref="LicenceRules.IsLate"/>.
        /// </summary>
        public static void LateHours(bool[] barred)
        {
            for (int cell = 0; cell < Cells; cell++)
            {
                int h = cell % Hours;
                barred[cell] = h >= CompanyCatalogue.LateHour || h < CompanyCatalogue.EarlyHour;
            }
        }

        /// <summary>True if the smallest crew stands in an hour: somebody at each service group, or at any work station where no group limits the customers.</summary>
        public bool Crewed(RotaShop shop, int cell, short[] at)
        {
            Sort(shop);
            int stations = shop.Stations, services = 0, staffed = 0;
            bool anyone = false;
            for (int g = 0; g < shop.Groups; g++)
            {
                _taken2[g] = 0;
            }
            for (int s = 0; s < stations; s++)
            {
                int i = cell * stations + s;
                if (shop.Taken[i] || (at != null && at[i] >= 0))
                {
                    _taken2[shop.Group[s]]++;
                    anyone |= shop.GroupKind[shop.Group[s]] != RotaGroupKind.Cleaning;
                }
            }
            for (int g = 0; g < shop.Groups; g++)
            {
                if (shop.GroupKind[g] == RotaGroupKind.Service && _groupStart[g + 1] > _groupStart[g])
                {
                    services++;
                    staffed += _taken2[g] > 0 ? 1 : 0;
                }
            }
            return services > 0 ? staffed == services : anyone;
        }

        public static int Count(bool[] open)
        {
            int n = 0;
            for (int cell = 0; cell < Cells; cell++)
            {
                n += open[cell] ? 1 : 0;
            }
            return n;
        }
    }
}
