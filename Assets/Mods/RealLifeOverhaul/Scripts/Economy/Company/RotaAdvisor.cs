using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The planner's advice, game-free: which stations an hour's customers are worth staffing, which hours are worth opening
    /// and what a week comes to. Cost: nothing unless asked; nothing allocated once its arrays have grown.
    /// </summary>
    public sealed partial class RotaAdvisor
    {
        private const int Cells = RotaCatalogue.Cells, Hours = RotaCatalogue.Hours, Days = RotaCatalogue.Days;

        private int[] _groupStart = new int[0], _groupList = new int[0];   // each group's stations, the one that serves most first
        private int[] _taken = new int[0], _cap = new int[0], _taken2 = new int[0], _cap2 = new int[0];
        private bool[] _need = new bool[0];
        private readonly int[] _pre = new int[Hours + 1], _bad = new int[Hours + 1];
        private readonly int[] _firstValue = new int[Hours + 1], _firstFrom = new int[Hours + 1], _firstTo = new int[Hours + 1];
        private RotaShop _sorted;
        private int _sortedVersion;

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
        /// The crew worth having for an hour, marked in <paramref name="need"/>: one at each service group, a further station
        /// where its customers pay for it, and every support station (left out of its wage and count beside a service crew).
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
            // A group's stations add up and the smallest group's sum is the limit (RetailBusinessSimulator.GetMaxCustomerCapacityThisHour).
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
                // No station limits the customers: one person at work serves them all, the cheapest to have there.
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
                // The groups that hold the customers back each get a station more until more are served; kept if that pays.
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

        /// <summary>What opening each hour makes with the crew worth having, and the service stations of that crew.</summary>
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

        /// <summary>The hours to open: each day's one or two blocks that make the most, none holding a blocked hour, none below the day's fee.</summary>
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
                // The best single block ending by each hour.
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
                // A second block after a gap of an hour or more, if the two make more than the best one.
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

        /// <summary>Writes into a problem what an hour at each station is worth in these opening hours, the day's cleaning included.</summary>
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
                    if (!open[cell])
                    {
                        continue;
                    }
                    openHours++;
                    int customers = shop.Customers[cell], served, wage, count;
                    Crew(shop, customers, _need, out served, out wage, out count);
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
                    bool first = !anyService;   // no station limits the customers: the one person of the smallest crew is worth all of them
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
                // The day's cleaning, at its end when the dirt is there (BuildingCleanliness.ApplyDirt, SimulateCleaning).
                long cleaning = !shop.Cleaned && dirt > 0 && shop.CleanHours > 0 ? Math.Max(1L, (dirt * shop.CleanHours + 99999L) / 100000L) : 0L;
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

        // In whole money, never under the floor, so the smallest crew is always wanted.
        private static int Worth(int customers, int margin)
        {
            long worth = (long)customers * Math.Max(0, margin) / 100L;
            return (int)Math.Max(RotaCatalogue.ValueFloor, Math.Min(1000000L, worth));
        }

        /// <summary>Customers served in an hour by a rota (<paramref name="at"/>, may be null), those left alone, and with fill every wanted station.</summary>
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

        /// <summary>A week in money: sales less wages and day fees. Hours nobody is rostered for yet are counted at the station's wage.</summary>
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

        /// <summary>Group by group, the people the wanted hours take against those there, each counted for their first group.</summary>
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
                    RotaFit.Range(problem.Worker[w], out _, out int most);
                    reach += Math.Min(RotaCatalogue.FullWeek, most);
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

        /// <summary>What one more station like a group's largest would serve on its best day, in hours it pays, within the building's room.</summary>
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

        /// <summary>The hours that need the late opening permit, the same as <see cref="LicenceRules.IsLate"/> counts.</summary>
        public static void LateHours(bool[] barred)
        {
            for (int cell = 0; cell < Cells; cell++)
            {
                int h = cell % Hours;
                barred[cell] = h >= CompanyCatalogue.LateHour || h < CompanyCatalogue.EarlyHour;
            }
        }

        /// <summary>True if the smallest crew stands in an hour: somebody at each service group, or anyone where no group limits.</summary>
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
    }
}
