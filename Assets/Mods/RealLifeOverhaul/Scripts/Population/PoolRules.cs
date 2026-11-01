using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>What the day's demand reads from outside the population: the national economy and the levers other features hold.</summary>
    public struct DemandInputs
    {
        public int BaseBp;          // hundredths of a per cent: the central bank's rate over the banks' own (savings' returns follow it)
        public int Mood;            // thousandths: what customers spend against the ordinary in this state of the economy (CityModel.Spend)
        public int Visitors;        // thousandths: the region's visitors against the ordinary (the season, the economy)
        public int[] Bears;         // thousandths by district: what its customers bear against the game's own prices (CityEconomy.Bears); null for 1000
    }

    /// <summary>
    /// The day's money as pools (POPULATION.md section 6; the brief's sixth step): each household's spending of each kind goes into its line, at
    /// home or near work; commuters and visitors are lines of their own; each line is routed to the districts in reach of its kind by their
    /// pull and the distance; each district's money of a class and kind is shared among the businesses there that sell it by their pull, price
    /// against what the class bears, interior, satisfaction, attention and habit. Pools against businesses, never person by business, so it is
    /// cheap; yet every cent is placed by cumulative whole-number shares, so it is exact: a household's spending at a business can be worked out
    /// afterwards (<see cref="Attribute"/>), and summed over households it is that business's takings from residents to the cent; what is spent
    /// into the pools is what businesses take plus what goes beyond the city. Game-free. COST: nothing per frame; a household's part some twenty
    /// additions, the routing and sharing a few hundred thousand operations a day, all sliced by the caller.
    /// </summary>
    public static class PoolRules
    {
        public const int Home = 0, Work = 1, Commuter = 2, Visitor = 3, LineKinds = 4;
        public const int Classes = PopulationCatalogue.Classes;
        public const int Kinds = PopulationCatalogue.Categories;
        public const int WeightScale = 1000000;   // every set of weights is brought under this, so a share of a pool never overflows

        private const int HomeLean = 1500;        // thousandths: people favour their own district's shops
        private const int NearFactor = 500;       // thousandths of a kind's reach someone near work or visiting goes
        private static readonly int[] ClassBears = { 900, 1000, 1250 };      // thousandths of a district's own figure each class bears
        private static readonly int[] QualityLean = { 600, 1000, 1400 };    // thousandths: how much each class cares for an interior
        private static readonly int[] CommuterKinds = { 0, 0, 90, 420, 220, 60, 20, 0, 50, 10, 0, 0, 0, 0, 70, 0, 0, 40, 20, 0, 0 };   // thousandths of a commuter's spending by kind

        /// <summary>The day's pools, kept until the next day's (not saved: a load works the day out again).</summary>
        public sealed class DemandDay
        {
            public int Districts;
            public int Day = -1;
            public long[] LineMoney = new long[0];    // by line: kind, origin district, class, kind of spending
            public long[] SubPool = new long[0];      // by line kind, shopping district, class, kind: the money routed there
            public long[] Offsets = new long[0];      // by pool and origin: where a line's money starts in a district's pool
            public long[] RouteCum = new long[0];     // by line kind, origin and kind of spending: the routing weights over the districts, cumulative
            public long[] Pull = new long[0];         // by district and kind: the pull of its businesses that sell it
            public List<int>[] PoolBusinesses = new List<int>[0];   // by district and kind: its businesses, in the order of their slots
            public long[] Weight = new long[0];       // by business and class: its weight in its pool today
            public long[] WeightTotal = new long[0];  // by district, class and kind
            public int[] Budget = new int[0];         // by household and kind: the day's spending, cents
            public byte[] WorkDistrict = new byte[0]; // by household: the district its main earner works in, or 255
            public long ResidentSpend;                // what households spent into the pools today (their spending of the kinds businesses sell)
            public long OutsiderSpend;                // ... commuters and visitors
            public long Takings;                      // businesses' takings today
            public long Outside;                      // pool money no business in reach sells: spent beyond the city
            public long[] Scratch = new long[0];

            public int LineIndex(int kind, int origin, int cls, int k) => ((kind * Districts + origin) * Classes + cls) * Kinds + k;
            public int PoolIndex(int d, int k) => d * Kinds + k;
            public int TotalIndex(int d, int cls, int k) => (d * Classes + cls) * Kinds + k;
            public int RouteIndex(int kind, int origin, int k) => ((kind * Districts + origin) * Kinds + k) * (Districts + 1);

            /// <summary>Everything sized for the city as it stands; the day's figures cleared.</summary>
            public void Size(PopulationState s)
            {
                Districts = Math.Max(1, s.Districts.Count);
                int lines = LineKinds * Districts * Classes * Kinds;
                if (LineMoney.Length != lines)
                {
                    LineMoney = new long[lines];
                    SubPool = new long[lines];
                    Offsets = new long[lines * Districts];
                    RouteCum = new long[LineKinds * Districts * Kinds * (Districts + 1)];
                    WeightTotal = new long[Districts * Classes * Kinds];
                    Pull = new long[Districts * Kinds];
                    PoolBusinesses = new List<int>[Districts * Kinds];
                    Scratch = new long[Districts + 1];
                }
                Array.Clear(LineMoney, 0, LineMoney.Length);
                Array.Clear(SubPool, 0, SubPool.Length);
                Array.Clear(Offsets, 0, Offsets.Length);
                Array.Clear(RouteCum, 0, RouteCum.Length);
                Array.Clear(WeightTotal, 0, WeightTotal.Length);
                Array.Clear(Pull, 0, Pull.Length);
                for (int i = 0; i < PoolBusinesses.Length; i++)
                {
                    if (PoolBusinesses[i] == null)
                    {
                        PoolBusinesses[i] = new List<int>(8);
                    }
                    PoolBusinesses[i].Clear();
                }
                int businesses = s.Businesses.Key.Length;
                if (Weight.Length < businesses * Classes)
                {
                    Weight = new long[businesses * Classes];
                }
                int households = s.Households.Capacity;
                if (Budget.Length < households * Kinds)
                {
                    Budget = new int[households * Kinds];
                    WorkDistrict = new byte[households];
                }
                ResidentSpend = OutsiderSpend = Takings = Outside = 0;
            }
        }

        /// <summary>
        /// The day begins: the pools sized and empty, each business's weight in its pool by class (blended with yesterday's share by its kind's
        /// habit), each district's pull for each kind, and every business's takings for the day set to nothing.
        /// </summary>
        public static void Prepare(PopulationState s, DemandDay dd, in DemandInputs inputs, int day)
        {
            dd.Size(s);
            dd.Day = day;
            BusinessTable bt = s.Businesses;
            for (int b = 0; b < bt.Count; b++)
            {
                bt.Takings[b] = 0;
                bt.Visits[b] = 0;
                Array.Clear(bt.ByClass, b * Classes, Classes);
                Array.Clear(bt.ByLine, b * BusinessTable.Lines, BusinessTable.Lines);
                for (int c = 0; c < Classes; c++)
                {
                    dd.Weight[b * Classes + c] = 0;
                }
                int k = bt.Kind[b];
                if (!bt.Seen[b] || k < PopulationCatalogue.ShopCategoryFirst || k > PopulationCatalogue.ShopCategoryLast || bt.District[b] >= dd.Districts)
                {
                    continue;
                }
                dd.PoolBusinesses[dd.PoolIndex(bt.District[b], k)].Add(b);
                dd.Pull[dd.PoolIndex(bt.District[b], k)] += (long)bt.Room[b] * HoursFactor(bt.Hours[b]) / 1000 * AttentionTerm(bt.Attention[b]) / 1000;
            }
            for (int d = 0; d < dd.Districts; d++)
            {
                int bears = inputs.Bears != null && d < inputs.Bears.Length && inputs.Bears[d] > 0 ? inputs.Bears[d] : 1000;
                for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
                {
                    List<int> list = dd.PoolBusinesses[dd.PoolIndex(d, k)];
                    for (int c = 0; c < Classes; c++)
                    {
                        Weigh(s, dd, list, d, c, k, bears);
                    }
                }
            }
        }

        // A pool's weights for a class: each business's own, as a share, blended with the share it took yesterday by the kind's habit.
        private static void Weigh(PopulationState s, DemandDay dd, List<int> list, int d, int c, int k, int bears)
        {
            if (list.Count == 0)
            {
                return;
            }
            BusinessTable bt = s.Businesses;
            long raw = 0, past = 0;
            for (int i = 0; i < list.Count; i++)
            {
                int b = list[i];
                long v = OwnWeight(bt, b, c, bears);
                dd.Weight[b * Classes + c] = v;
                raw += v;
                past += bt.Habit[b * Classes + c];
            }
            int habit = past > 0 ? PopulationCatalogue.Habit[k] : 0;
            long total = 0;
            for (int i = 0; i < list.Count; i++)
            {
                int b = list[i];
                long share = raw > 0 ? dd.Weight[b * Classes + c] * WeightScale / raw : 0;
                long before = past > 0 ? (long)bt.Habit[b * Classes + c] * WeightScale / past : 0;
                long w = (share * (1000 - habit) + before * habit) / 1000;
                dd.Weight[b * Classes + c] = w;
                total += w;
            }
            dd.WeightTotal[dd.TotalIndex(d, c, k)] = total;
        }

        /// <summary>
        /// A business's own pull on a class: its room for customers and hours, its prices against what the class bears, its interior, its customers'
        /// satisfaction and what people hear of it.
        /// </summary>
        public static long OwnWeight(BusinessTable bt, int b, int c, int bears)
        {
            long v = (long)bt.Room[b] * 1000;
            v = v * HoursFactor(bt.Hours[b]) / 1000;
            v = v * PriceTerm(bt.Price[b], c, bears) / 1000;
            v = v * QualityTerm(bt.Quality[b], c) / 1000;
            v = v * ContentTerm(bt.Content[b]) / 1000;
            v = v * AttentionTerm(bt.Attention[b]) / 1000;
            return Math.Max(0, v);
        }

        public static int HoursFactor(int hours) => PopulationCatalogue.Clamp(hours * 1000 / 12, 200, 2000);

        /// <summary>Thousandths of pull a price keeps with a class: lost the further it is over what the class bears (the working class the most), gained under it.</summary>
        public static int PriceTerm(int price, int c, int bears)
        {
            int borne = (int)((long)Math.Max(1, bears) * ClassBears[c] / 1000);
            int over = (price > 0 ? price : 1000) - borne;
            return PopulationCatalogue.Clamp(1000 - (int)((long)PopulationCatalogue.PriceElasticity[c] * over / 1000), 150, 1600);
        }

        public static int QualityTerm(int quality, int c) => PopulationCatalogue.Clamp(1000 + PopulationCatalogue.QualityWeight * (quality - 50) / 50 * QualityLean[c] / 1000, 300, 2200);

        public static int ContentTerm(int content) => PopulationCatalogue.Clamp(1000 + PopulationCatalogue.SatisfactionWeight * (content - 50) / 50, 300, 1800);

        public static int AttentionTerm(int attention) => 1000 + PopulationCatalogue.Clamp(attention, PopulationCatalogue.AttentionLeast, PopulationCatalogue.AttentionMost);

        /// <summary>
        /// One household's day: its income, its budget, its book, and its spending at businesses into its lines, at home and near its main earner's
        /// work. Its spending by kind is kept for the day, so its part of every business's takings can be worked out afterwards.
        /// </summary>
        public static void Household(PopulationState s, DemandDay dd, int h, int day, int baseBp, int mood, int[] shares, int[] budget, out DayIncome inc)
        {
            HouseholdTable hh = s.Households;
            SpendRules.Income(s, h, day, baseBp, out inc);
            long saving = SpendRules.Budget(s, h, inc, s.Median, shares, budget);
            if (mood != 1000 && mood > 0)
            {
                // A downturn holds spending back (and an upturn lets it go): what is not spent is saved.
                for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
                {
                    int was = budget[k];
                    budget[k] = (int)((long)was * mood / 1000);
                    saving += was - budget[k];
                }
            }
            SpendRules.Book(s, h, inc, budget, saving);
            int work = WorkDistrict(s, h);
            dd.WorkDistrict[h] = (byte)(work < 0 ? 255 : work);
            int o = hh.District[h], c = hh.Class[h];
            int row = h * Kinds;
            for (int k = 0; k < Kinds; k++)
            {
                dd.Budget[row + k] = budget[k];
                if (k < PopulationCatalogue.ShopCategoryFirst || k > PopulationCatalogue.ShopCategoryLast || budget[k] <= 0)
                {
                    continue;
                }
                long near = work >= 0 ? (long)budget[k] * PopulationCatalogue.NearWork[k] / 1000 : 0;
                dd.LineMoney[dd.LineIndex(Home, o, c, k)] += budget[k] - near;
                if (near > 0)
                {
                    dd.LineMoney[dd.LineIndex(Work, work, c, k)] += near;
                }
                dd.ResidentSpend += budget[k];
            }
        }

        /// <summary>The district the household's main earner works in, or -1 (nobody in a job in the city).</summary>
        public static int WorkDistrict(PopulationState s, int h)
        {
            PersonTable pt = s.People;
            int best = -1, bestPay = -1;
            for (int p = s.Households.First[h]; p >= 0; p = pt.Next[p])
            {
                int e = pt.Job[p];
                if (e >= 0 && pt.Status[p] == (byte)Status.Worker && pt.Pay[p] > bestPay)
                {
                    int b = s.Employers.Building[e];
                    if (b >= 0 && b < s.Buildings.Count)
                    {
                        best = s.Buildings[b].District;
                        bestPay = pt.Pay[p];
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// The commuters' and the visitors' lines: commuters spend near work by the district's jobs filled from the region, their class by the
        /// fields of the jobs they hold; visitors by each district's pull, their class by the district's people.
        /// </summary>
        public static void Outsiders(PopulationState s, DemandDay dd, in DemandInputs inputs)
        {
            int districts = dd.Districts;
            long[] commuters = new long[districts * Classes];
            EmployerTable et = s.Employers;
            for (int e = 0; e < et.Count; e++)
            {
                int b = et.Building[e];
                if (b < 0 || et.Commuters[e] <= 0)
                {
                    continue;
                }
                int d = s.Buildings[b].District;
                CommuterClasses((Field)et.Field[e], out int w, out int m, out int u);
                commuters[d * Classes] += (long)et.Commuters[e] * w;
                commuters[d * Classes + 1] += (long)et.Commuters[e] * m;
                commuters[d * Classes + 2] += (long)et.Commuters[e] * u;
            }
            int mood = inputs.Mood > 0 ? inputs.Mood : 1000;
            for (int d = 0; d < districts; d++)
            {
                for (int c = 0; c < Classes; c++)
                {
                    long people = commuters[d * Classes + c];   // thousandths of commuters
                    if (people <= 0)
                    {
                        continue;
                    }
                    long money = people * PopulationCatalogue.CommuterSpend / 1000 * mood / 1000;
                    for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
                    {
                        long part = money * CommuterKinds[k] / 1000;
                        dd.LineMoney[dd.LineIndex(Commuter, d, c, k)] += part;
                        dd.OutsiderSpend += part;
                        s.Books.CommuterSpend += part;
                    }
                }
                PopDistrict pd = s.Districts[d];
                long visitors = (long)pd.Pull * PopulationCatalogue.VisitorsPerPull / 1000 * (inputs.Visitors > 0 ? inputs.Visitors : 1000) / 1000;
                int all = Math.Max(1, pd.Working + pd.Middle + pd.Upper);
                for (int c = 0; c < Classes; c++)
                {
                    int share = c == 0 ? pd.Working : c == 1 ? pd.Middle : pd.Upper;
                    long people = visitors * share / all;
                    if (people <= 0)
                    {
                        continue;
                    }
                    for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
                    {
                        long part = people * PopulationCatalogue.VisitorSpend[k] * mood / 1000;
                        dd.LineMoney[dd.LineIndex(Visitor, d, c, k)] += part;
                        dd.OutsiderSpend += part;
                        s.Books.VisitorSpend += part;
                    }
                }
            }
        }

        // Thousandths of the commuters in a field's jobs by class: office and the professions middle and upper, shops and trades mostly working.
        private static void CommuterClasses(Field f, out int working, out int middle, out int upper)
        {
            switch (f)
            {
                case Field.Professional:
                case Field.Tech:
                case Field.Management:
                    working = 0; middle = 600; upper = 400;
                    return;
                case Field.Office:
                case Field.Creative:
                case Field.Public:
                    working = 200; middle = 750; upper = 50;
                    return;
                default:
                    working = 700; middle = 300; upper = 0;
                    return;
            }
        }

        /// <summary>
        /// The routing weights of a line (kind, origin, kind of spending) over the districts: each district's pull for the kind, falling with the
        /// distance over the kind's reach (shorter near work or visiting), its own district favoured; brought under the weight scale. Returns the total;
        /// the weights in <paramref name="into"/>, one a district. The same on every call for the same day.
        /// </summary>
        public static long RouteWeights(PopulationState s, DemandDay dd, int kind, int origin, int k, long[] into)
        {
            int reach = PopulationCatalogue.Reach[k];
            if (kind != Home)
            {
                reach = reach * NearFactor / 1000;
            }
            reach = Math.Max(50, reach);
            long total = 0;
            for (int d = 0; d < dd.Districts; d++)
            {
                long pull = dd.Pull[dd.PoolIndex(d, k)];
                if (pull <= 0)
                {
                    into[d] = 0;
                    continue;
                }
                long q = (long)CityTable.Distance(s, origin, d) * 1000 / reach;   // thousandths of the reach
                long decay = 1000000000L / (1000000 + 2 * q * q);                  // thousandths: a third at the reach, a ninth at twice it
                long w = pull * decay / 1000;
                if (d == origin)
                {
                    w = w * HomeLean / 1000;
                }
                into[d] = w;
                total += w;
            }
            if (total > WeightScale)
            {
                long scaled = 0;
                for (int d = 0; d < dd.Districts; d++)
                {
                    into[d] = into[d] * WeightScale / total;
                    scaled += into[d];
                }
                total = scaled;
            }
            return total;
        }

        /// <summary>The part of the first <paramref name="x"/> cents of a set of weights that falls between two cumulative weights, by cumulative whole shares.</summary>
        public static long CumShare(long x, long cumBefore, long cumAt, long total)
        {
            return total <= 0 ? 0 : x * cumAt / total - x * cumBefore / total;
        }

        /// <summary>
        /// Every line routed into the districts' pools, origin by origin, each line's start in each pool kept for the households' parts afterwards;
        /// what no district in reach sells goes beyond the city.
        /// </summary>
        public static void Route(PopulationState s, DemandDay dd)
        {
            long[] w = dd.Scratch;
            int n = dd.Districts;
            for (int kind = 0; kind < LineKinds; kind++)
            {
                for (int o = 0; o < n; o++)
                {
                    for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
                    {
                        int r = dd.RouteIndex(kind, o, k);
                        long total = RouteWeights(s, dd, kind, o, k, w);
                        long cum = 0;
                        dd.RouteCum[r] = 0;
                        for (int d = 0; d < n; d++)
                        {
                            cum += w[d];
                            dd.RouteCum[r + d + 1] = cum;
                        }
                        for (int c = 0; c < Classes; c++)
                        {
                            long money = dd.LineMoney[dd.LineIndex(kind, o, c, k)];
                            if (money <= 0)
                            {
                                continue;
                            }
                            if (total <= 0)
                            {
                                dd.Outside += money;
                                continue;
                            }
                            for (int d = 0; d < n; d++)
                            {
                                int pool = dd.LineIndex(kind, d, c, k);
                                dd.Offsets[pool * n + o] = dd.SubPool[pool];
                                dd.SubPool[pool] += CumShare(money, dd.RouteCum[r + d], dd.RouteCum[r + d + 1], total);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Every district's pools shared among its businesses: each business's takings by class and by the line's kind, to the cent.</summary>
        public static void Allocate(PopulationState s, DemandDay dd)
        {
            BusinessTable bt = s.Businesses;
            for (int kind = 0; kind < LineKinds; kind++)
            {
                for (int d = 0; d < dd.Districts; d++)
                {
                    for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
                    {
                        List<int> list = dd.PoolBusinesses[dd.PoolIndex(d, k)];
                        for (int c = 0; c < Classes; c++)
                        {
                            long money = dd.SubPool[dd.LineIndex(kind, d, c, k)];
                            if (money == 0)
                            {
                                continue;
                            }
                            long total = dd.WeightTotal[dd.TotalIndex(d, c, k)];
                            if (total <= 0 || list.Count == 0)
                            {
                                dd.Outside += money;   // nobody there it would buy from after all (every business's pull at nothing)
                                continue;
                            }
                            long cum = 0;
                            for (int i = 0; i < list.Count; i++)
                            {
                                int b = list[i];
                                long next = cum + dd.Weight[b * Classes + c];
                                long part = CumShare(money, cum, next, total);
                                cum = next;
                                bt.Takings[b] += part;
                                bt.ByClass[b * Classes + c] += part;
                                bt.ByLine[b * BusinessTable.Lines + kind] += part;
                                dd.Takings += part;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The day closes for the businesses: their customers (takings over a customer's basket), the sales tax in their takings to the council,
        /// their value added to their employers' output, their month, today's share kept for tomorrow's habit, and the anchor of a business
        /// seen with takings for the first time. The pools' money spent beyond the city goes to the council's books.
        /// </summary>
        public static void Close(PopulationState s, DemandDay dd, int day)
        {
            BusinessTable bt = s.Businesses;
            for (int b = 0; b < bt.Count; b++)
            {
                long t = bt.Takings[b];
                int basket = PopulationCatalogue.BasketOfType(bt.Type[b]) * 100;
                bt.Visits[b] = (int)Math.Min(int.MaxValue, (t + basket / 2) / Math.Max(1, basket));
                long tax = t * PopulationCatalogue.SalesTax / 1000;
                s.Books.SalesTax += tax;
                bt.MonthTakings[b] += t;
                int k = bt.Kind[b];
                int e = bt.Employer[b];
                if (e >= 0 && k >= 0 && k < Kinds)
                {
                    s.Employers.Output[e] += (t - tax) * PopulationCatalogue.ValueAdded[k] / 1000;
                }
                int d = bt.District[b];
                for (int c = 0; c < Classes; c++)
                {
                    long total = d < dd.Districts && k >= 0 && k < Kinds ? dd.WeightTotal[dd.TotalIndex(d, c, k)] : 0;
                    bt.Habit[b * Classes + c] = total > 0 ? (int)(dd.Weight[b * Classes + c] * WeightScale / total) : 0;
                }
                if (bt.AnchorDay[b] < 0 && t > 0 && bt.Seen[b])
                {
                    bt.Anchor[b] = t;
                    bt.AnchorDay[b] = day;
                }
                if (d < s.Figures.Length)
                {
                    s.Figures[d].Takings += t;
                }
            }
            s.Books.Outside += dd.Outside;
        }

        // ------------------------------------------------------------------ a household's part, worked out afterwards

        /// <summary>
        /// A household's spending at each business today, cents, from the day's pools (<paramref name="perBusiness"/>, one a business slot, added
        /// to): its part of each line it spent into, routed and shared exactly as the day's money was, so that over every household it is each
        /// business's takings from residents. A walk over the households before it: asked by the Economy app's drill-down, never per frame.
        /// </summary>
        public static void Attribute(PopulationState s, DemandDay dd, int h, long[] perBusiness)
        {
            if (dd == null || dd.Day < 0 || !s.Households.Alive(h) || (h + 1) * Kinds > dd.Budget.Length)
            {
                return;
            }
            int o = s.Households.District[h], c = s.Households.Class[h], w = dd.WorkDistrict[h];
            var homeBefore = new long[Kinds];
            var workBefore = new long[Kinds];
            for (int x = 0; x < h; x++)
            {
                if (!s.Households.Alive(x) || s.Households.Class[x] != c)
                {
                    continue;
                }
                bool sameHome = s.Households.District[x] == o, sameWork = w != 255 && dd.WorkDistrict[x] == w;
                if (!sameHome && !sameWork)
                {
                    continue;
                }
                for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
                {
                    Split(dd, x, k, out long home, out long near);
                    if (sameHome)
                    {
                        homeBefore[k] += home;
                    }
                    if (sameWork)
                    {
                        workBefore[k] += near;
                    }
                }
            }
            for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
            {
                Split(dd, h, k, out long home, out long near);
                if (home != 0)
                {
                    Piece(dd, Home, o, c, k, homeBefore[k], homeBefore[k] + home, perBusiness);
                }
                if (near != 0 && w != 255)
                {
                    Piece(dd, Work, w, c, k, workBefore[k], workBefore[k] + near, perBusiness);
                }
            }
        }

        /// <summary>
        /// Every household's spending at every business today at once (<paramref name="perBusiness"/>, summed over households; <paramref name="perHousehold"/>,
        /// when given, each household's spending at businesses): the same arithmetic as <see cref="Attribute"/>, line by line in one pass. For the
        /// offline checks of the conservation and for the Economy app's "who contributes".
        /// </summary>
        public static void AttributeAll(PopulationState s, DemandDay dd, long[] perBusiness, long[] perHousehold)
        {
            if (dd == null || dd.Day < 0)
            {
                return;
            }
            var running = new long[dd.LineMoney.Length];
            long[] mine = perHousehold != null ? new long[perBusiness.Length] : null;
            for (int h = 0; h < s.Households.Count && (h + 1) * Kinds <= dd.Budget.Length; h++)
            {
                if (!s.Households.Alive(h))
                {
                    continue;
                }
                int o = s.Households.District[h], c = s.Households.Class[h], w = dd.WorkDistrict[h];
                long[] into = mine ?? perBusiness;
                if (mine != null)
                {
                    Array.Clear(mine, 0, mine.Length);
                }
                for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
                {
                    Split(dd, h, k, out long home, out long near);
                    if (home != 0)
                    {
                        int line = dd.LineIndex(Home, o, c, k);
                        Piece(dd, Home, o, c, k, running[line], running[line] + home, into);
                        running[line] += home;
                    }
                    if (near != 0 && w != 255)
                    {
                        int line = dd.LineIndex(Work, w, c, k);
                        Piece(dd, Work, w, c, k, running[line], running[line] + near, into);
                        running[line] += near;
                    }
                }
                if (mine != null)
                {
                    long sum = 0;
                    for (int b = 0; b < mine.Length; b++)
                    {
                        perBusiness[b] += mine[b];
                        sum += mine[b];
                    }
                    if (h < perHousehold.Length)
                    {
                        perHousehold[h] = sum;
                    }
                }
            }
        }

        // A household's spending of a kind split between home and near work, as the day put it into its lines.
        private static void Split(DemandDay dd, int h, int k, out long home, out long near)
        {
            long b = dd.Budget[h * Kinds + k];
            near = b > 0 && dd.WorkDistrict[h] != 255 ? b * PopulationCatalogue.NearWork[k] / 1000 : 0;
            home = Math.Max(0, b) - near;
        }

        // The cents from x0 to x1 of a line, routed and shared as the day's money was, added to the businesses' sums.
        private static void Piece(DemandDay dd, int kind, int origin, int c, int k, long x0, long x1, long[] perBusiness)
        {
            int n = dd.Districts;
            int r = dd.RouteIndex(kind, origin, k);
            long total = dd.RouteCum[r + n];
            if (total <= 0)
            {
                return;   // beyond the city: no business's
            }
            for (int d = 0; d < n; d++)
            {
                long before = dd.RouteCum[r + d], at = dd.RouteCum[r + d + 1];
                if (at == before)
                {
                    continue;
                }
                long r0 = CumShare(x0, before, at, total), r1 = CumShare(x1, before, at, total);
                long offset = dd.Offsets[dd.LineIndex(kind, d, c, k) * n + origin];
                Share(dd, kind, d, c, k, offset + r0, offset + r1, perBusiness);
            }
        }

        // The cents from y0 to y1 of a district's pool, shared among its businesses as the day's were.
        private static void Share(DemandDay dd, int kind, int d, int c, int k, long y0, long y1, long[] perBusiness)
        {
            List<int> list = dd.PoolBusinesses[dd.PoolIndex(d, k)];
            long total = dd.WeightTotal[dd.TotalIndex(d, c, k)];
            if (total <= 0 || list.Count == 0 || dd.SubPool[dd.LineIndex(kind, d, c, k)] == 0)
            {
                return;
            }
            long cum = 0;
            for (int i = 0; i < list.Count; i++)
            {
                int b = list[i];
                long next = cum + dd.Weight[b * Classes + c];
                long part = CumShare(y1, cum, next, total) - CumShare(y0, cum, next, total);
                cum = next;
                if (b < perBusiness.Length)
                {
                    perBusiness[b] += part;
                }
            }
        }
    }
}
