using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The groups of the Managers tab's directory, in the order its first page lists them. Not saved.</summary>
    public enum ManagerGroup
    {
        Needs = 0,          // what the managers have left for the player to look at
        Regional = 1,       // a regional manager per district
        Shop = 2,           // the managers of businesses customers walk into
        Works = 3,          // of factories and offices
        Warehouse = 4,      // of warehouses
        Vacant = 5,         // places with no manager
        Count = 6
    }

    /// <summary>How a post stands today. Not saved: read from the game's staff each time a page is written.</summary>
    public enum PostState
    {
        InPost = 0,         // the manager is at work and decides
        Covered = 1,        // off sick, and the district's regional manager stands in
        Uncovered = 2,      // off sick, and nobody stands in: today's decisions wait
        Empty = 3           // nobody holds the post
    }

    /// <summary>One place's post as the directory counts it, read once for each page written.</summary>
    public struct PostLine
    {
        public ManagedKind Kind;
        public bool Has;            // the place has a manager
        public bool Sick;           // the manager is off sick today
        public bool RegionThere;    // the district has a regional manager
        public bool RegionSick;     // who is off sick today
        public int Open;            // decisions at the place that can still be undone
        public int OpenMistakes;    // mistakes among them
        public int Spent, Budget;   // of the month's budget for overtime and hires, at prices
        public int Cost;            // the manager's allowance a month, at prices
        public int Decided;         // decisions the manager made this month
    }

    /// <summary>
    /// The directory's counts: per group the managers in post, the places, what they cost a month, the decisions still
    /// open, those off sick and those that need the player; and for the company the districts, the places nobody decides
    /// for today and the regional managers. Cleared and filled again for each page written: no allocation after it is made.
    /// </summary>
    public sealed class DirectoryCounts
    {
        public readonly int[] Managers = new int[(int)ManagerGroup.Count];
        public readonly int[] Places = new int[(int)ManagerGroup.Count];
        public readonly int[] Cost = new int[(int)ManagerGroup.Count];
        public readonly int[] Open = new int[(int)ManagerGroup.Count];
        public readonly int[] Sick = new int[(int)ManagerGroup.Count];
        public readonly int[] Needs = new int[(int)ManagerGroup.Count];
        public int Districts;       // districts the player has places in
        public int Uncovered;       // places whose manager is off sick with nobody standing in
        public int Covered;         // places whose manager is off sick with the regional manager standing in

        public void Clear()
        {
            Array.Clear(Managers, 0, Managers.Length);
            Array.Clear(Places, 0, Places.Length);
            Array.Clear(Cost, 0, Cost.Length);
            Array.Clear(Open, 0, Open.Length);
            Array.Clear(Sick, 0, Sick.Length);
            Array.Clear(Needs, 0, Needs.Length);
            Districts = Uncovered = Covered = 0;
        }

        /// <summary>A place: counted under its kind's group if it has a manager, under the vacancies if not.</summary>
        public void Add(PostLine line)
        {
            int g = (int)ManagerDirectoryRules.GroupOf(line.Kind, line.Has);
            Places[(int)ManagerDirectoryRules.KindGroup(line.Kind)]++;
            Places[g] += g == (int)ManagerGroup.Vacant ? 1 : 0;
            int needs = ManagerDirectoryRules.Reasons(ManagerDirectoryRules.NeedsOf(line));
            Needs[g] += needs;
            Needs[(int)ManagerGroup.Needs] += needs;
            Open[g] += Math.Max(0, line.Open);
            if (!line.Has)
            {
                return;
            }
            PostState state = ManagerDirectoryRules.State(true, line.Sick, line.RegionThere, line.RegionSick);
            Managers[g]++;
            Cost[g] = Sum(Cost[g], line.Cost);
            Sick[g] += line.Sick ? 1 : 0;
            Uncovered += state == PostState.Uncovered ? 1 : 0;
            Covered += state == PostState.Covered ? 1 : 0;
        }

        /// <summary>A district: its regional manager if it has one, off sick or not, and what they cost a month.</summary>
        public void AddRegion(bool has, bool sick, int cost)
        {
            Districts++;
            Places[(int)ManagerGroup.Regional]++;
            if (has)
            {
                Managers[(int)ManagerGroup.Regional]++;
                Cost[(int)ManagerGroup.Regional] = Sum(Cost[(int)ManagerGroup.Regional], cost);
                Sick[(int)ManagerGroup.Regional] += sick ? 1 : 0;
            }
        }

        /// <summary>Every manager in post, regional ones too.</summary>
        public int AllManagers => Managers[(int)ManagerGroup.Regional] + PlaceManagers;

        /// <summary>The managers of places, of every kind.</summary>
        public int PlaceManagers => Managers[(int)ManagerGroup.Shop] + Managers[(int)ManagerGroup.Works] + Managers[(int)ManagerGroup.Warehouse];

        /// <summary>Every place, with a manager or not.</summary>
        public int AllPlaces => Places[(int)ManagerGroup.Shop] + Places[(int)ManagerGroup.Works] + Places[(int)ManagerGroup.Warehouse];

        public int AllCost => Sum(Sum(Cost[(int)ManagerGroup.Regional], Cost[(int)ManagerGroup.Shop]), Sum(Cost[(int)ManagerGroup.Works], Cost[(int)ManagerGroup.Warehouse]));

        public int AllOpen => Open[(int)ManagerGroup.Shop] + Open[(int)ManagerGroup.Works] + Open[(int)ManagerGroup.Warehouse] + Open[(int)ManagerGroup.Vacant];

        public int AllSick => Sick[(int)ManagerGroup.Regional] + Sick[(int)ManagerGroup.Shop] + Sick[(int)ManagerGroup.Works] + Sick[(int)ManagerGroup.Warehouse];

        private static int Sum(int a, int b)
        {
            return (int)Math.Min(int.MaxValue / 4, (long)Math.Max(0, a) + Math.Max(0, b));
        }
    }

    /// <summary>
    /// The rules of the Managers tab's directory, game-free (EconomyCheck compiles them): which group a place is listed in,
    /// how its post stands today and who stands in, what needs the player, what a manager costs a month, who a decision is
    /// counted to, and the order a group's places are first listed in. The state agrees with ManagerRules.Decider, which the
    /// decisions themselves ask, so the tab never says somebody decides who does not.
    /// </summary>
    public static class ManagerDirectoryRules
    {
        public const int BudgetNearShare = 800;     // thousandths of the month's budget used: from here it needs the player
        public const int NeedsShown = 3;            // lines of what needs the player on the directory's first page

        // What needs the player at a place, a bit each.
        public const int NeedMistake = 1, NeedUncovered = 2, NeedBudget = 4;

        // A place's state as the groups' second filter has it.
        public const int FilterNeeds = 0, FilterSick = 1, FilterFine = 2;

        /// <summary>
        /// How a post stands today: a manager off sick is covered only by a regional manager who is at work; anybody else
        /// off sick leaves the day's decisions waiting (ManagerRules.Decider gives 0 then).
        /// </summary>
        public static PostState State(bool has, bool sick, bool regionThere, bool regionSick)
        {
            if (!has)
            {
                return PostState.Empty;
            }
            if (!sick)
            {
                return PostState.InPost;
            }
            return regionThere && !regionSick ? PostState.Covered : PostState.Uncovered;
        }

        /// <summary>The group a place is listed in: its kind's with a manager, the vacancies without one.</summary>
        public static ManagerGroup GroupOf(ManagedKind kind, bool has)
        {
            return has ? KindGroup(kind) : ManagerGroup.Vacant;
        }

        /// <summary>The group of a kind of place's managers.</summary>
        public static ManagerGroup KindGroup(ManagedKind kind)
        {
            switch (kind)
            {
                case ManagedKind.Shop: return ManagerGroup.Shop;
                case ManagedKind.Works: return ManagerGroup.Works;
                default: return ManagerGroup.Warehouse;
            }
        }

        /// <summary>The kind of place a group lists; false for the groups that are not one kind's.</summary>
        public static bool KindOf(ManagerGroup group, out ManagedKind kind)
        {
            kind = group == ManagerGroup.Works ? ManagedKind.Works : group == ManagerGroup.Warehouse ? ManagedKind.Warehouse : ManagedKind.Shop;
            return group == ManagerGroup.Shop || group == ManagerGroup.Works || group == ManagerGroup.Warehouse;
        }

        /// <summary>Whether a month's budget is nearly used: from <see cref="BudgetNearShare"/> of it. A budget of nothing never is.</summary>
        public static bool BudgetNear(int spent, int budget)
        {
            return budget > 0 && (long)Math.Max(0, spent) * 1000 >= (long)budget * BudgetNearShare;
        }

        /// <summary>
        /// What needs the player at a place, a bit each: a mistake that can still be undone (also one a manager stood down
        /// since made), and with a manager in post one off sick with nobody standing in, or a budget nearly used.
        /// </summary>
        public static int NeedsOf(PostLine line)
        {
            int needs = line.OpenMistakes > 0 ? NeedMistake : 0;
            if (!line.Has)
            {
                return needs;
            }
            needs |= State(true, line.Sick, line.RegionThere, line.RegionSick) == PostState.Uncovered ? NeedUncovered : 0;
            needs |= BudgetNear(line.Spent, line.Budget) ? NeedBudget : 0;
            return needs;
        }

        /// <summary>How many reasons a set of needs holds: each is one line of the Needs you page.</summary>
        public static int Reasons(int needs)
        {
            return ((needs & NeedMistake) != 0 ? 1 : 0) + ((needs & NeedUncovered) != 0 ? 1 : 0) + ((needs & NeedBudget) != 0 ? 1 : 0);
        }

        /// <summary>The kind a place is of for the groups' second filter: needs the player, off sick (covered), or in order.</summary>
        public static int FilterOf(PostState state, int needs)
        {
            return needs != 0 ? FilterNeeds : state == PostState.Covered || state == PostState.Uncovered ? FilterSick : FilterFine;
        }

        /// <summary>What the state column is ordered by: what needs the player first, then off sick, then in order, then empty.</summary>
        public static int StateOrder(PostState state, int needs)
        {
            return needs != 0 ? 0 : state == PostState.Uncovered ? 1 : state == PostState.Covered ? 2 : state == PostState.InPost ? 3 : 4;
        }

        /// <summary>A manager's allowance over a month of the game's days, at prices.</summary>
        public static int MonthCost(int perDay, int monthDays)
        {
            return (int)Math.Min(int.MaxValue / 4, (long)Math.Max(0, perDay) * Math.Max(1, monthDays));
        }

        /// <summary>
        /// Whether a decision at a place was the regional manager's, standing in for its manager off sick, by the name it
        /// was made under: only when that is the regional manager's and not the place manager's own (two of one name are
        /// counted to the place's). The decision is counted to whoever made it, so each one's month says what they did.
        /// </summary>
        public static bool ByRegion(string who, string own, string region)
        {
            return !string.IsNullOrEmpty(who) && !string.IsNullOrEmpty(region) && string.Equals(who, region, StringComparison.Ordinal)
                   && !string.Equals(who, own, StringComparison.Ordinal);
        }

        /// <summary>
        /// The order a group's places are first listed in: by district, then by name, ignoring case, the place's own id
        /// last so that two of one name always come the same way round; <paramref name="order"/> gets the rows' numbers.
        /// An insertion sort over the few dozen places a company has: no allocation, and the same order on every machine.
        /// </summary>
        public static void Order(string[] districts, string[] names, string[] ids, int count, int[] order)
        {
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }
            for (int i = 1; i < count; i++)
            {
                int row = order[i], at = i - 1;
                while (at >= 0 && Compare(districts, names, ids, order[at], row) > 0)
                {
                    order[at + 1] = order[at];
                    at--;
                }
                order[at + 1] = row;
            }
        }

        private static int Compare(string[] districts, string[] names, string[] ids, int a, int b)
        {
            int c = string.Compare(districts[a] ?? string.Empty, districts[b] ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (c == 0)
            {
                c = string.Compare(names[a] ?? string.Empty, names[b] ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }
            if (c == 0)
            {
                c = string.CompareOrdinal(ids[a] ?? string.Empty, ids[b] ?? string.Empty);
            }
            return c;
        }
    }
}
