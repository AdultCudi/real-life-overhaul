using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The five levels of the management ladder, the same for the player's own companies and for a career at the city's
    /// companies. Saved as a number by both: never renumber.
    /// </summary>
    public enum HierarchyLevel
    {
        Frontline = 0,      // L0: one station or role, no management authority
        Unit = 1,           // L1: one store, or one department of a headquarters
        Region = 2,         // L2: several stores
        Executive = 3,      // L3: the company, one area of it
        Owner = 4           // L4: the owner and chief executive
    }

    /// <summary>The three areas an executive owns. Saved as a number: never renumber.</summary>
    public enum ExecutiveArea
    {
        Operations = 0,     // the chief operating officer: staffing, stock, the stores' figures
        People = 1,         // the chief people officer: hiring, pay, staff demands, satisfaction
        Money = 2,          // the chief financial officer: budgets and spending over them
        Count = 3
    }

    /// <summary>One rank on the ladder: its name's key, its level, its step on the way up and, for an executive, its area.</summary>
    public sealed class HierarchyRank
    {
        public readonly int Id;
        public readonly string Key;             // reallife_hier_rank_{Key}
        public readonly HierarchyLevel Level;
        public readonly int Step;               // the order of promotion, 0 at the bottom; two ranks of one step are the same rung
        public readonly int Area;               // ExecutiveArea for an executive, -1 for every other rank

        public HierarchyRank(int id, string key, HierarchyLevel level, int step, int area)
        {
            Id = id;
            Key = key;
            Level = level;
            Step = step;
            Area = area;
        }

        public string TextKey => HierarchyCatalogue.Mod + "_hier_rank_" + Key;
    }

    /// <summary>
    /// The ranks and levels both ladders share: the player's companies (Scripts/Economy/Company, the managers, regional
    /// managers and executives) and the player's own career at the city's companies (Scripts/Careers). One table, so a
    /// rank is called the same and sits at the same level wherever it is shown. Game-free: EconomyCheck compiles it.
    /// </summary>
    public static class HierarchyCatalogue
    {
        public const int Levels = 5;
        public const string Mod = "reallife";       // the start of every key of the mod's, put before a rank's own name

        public const int Frontline = 0, ShiftSupervisor = 1, StoreManager = 2, DepartmentSupervisor = 3, RegionalManager = 4,
            OperationsChief = 5, PeopleChief = 6, FinanceChief = 7, OwnerChief = 8, RankCount = 9;

        /// <summary>Every rank, by its id. The careers' way up: frontline, shift supervisor, store manager, regional manager, executive.</summary>
        public static readonly HierarchyRank[] Ranks =
        {
            new HierarchyRank(Frontline, "frontline", HierarchyLevel.Frontline, 0, -1),
            new HierarchyRank(ShiftSupervisor, "shift_supervisor", HierarchyLevel.Frontline, 1, -1),
            new HierarchyRank(StoreManager, "store_manager", HierarchyLevel.Unit, 2, -1),
            new HierarchyRank(DepartmentSupervisor, "department_supervisor", HierarchyLevel.Unit, 2, -1),
            new HierarchyRank(RegionalManager, "regional_manager", HierarchyLevel.Region, 3, -1),
            new HierarchyRank(OperationsChief, "coo", HierarchyLevel.Executive, 4, (int)ExecutiveArea.Operations),
            new HierarchyRank(PeopleChief, "chro", HierarchyLevel.Executive, 4, (int)ExecutiveArea.People),
            new HierarchyRank(FinanceChief, "cfo", HierarchyLevel.Executive, 4, (int)ExecutiveArea.Money),
            new HierarchyRank(OwnerChief, "owner", HierarchyLevel.Owner, 5, -1)
        };

        /// <summary>reallife_hier_level_{n}: each level's name, L0 to L4.</summary>
        public static readonly string[] LevelKeys = { "reallife_hier_level_0", "reallife_hier_level_1", "reallife_hier_level_2", "reallife_hier_level_3", "reallife_hier_level_4" };

        /// <summary>The executive rank of each area, by ExecutiveArea.</summary>
        public static readonly int[] AreaRanks = { OperationsChief, PeopleChief, FinanceChief };

        /// <summary>
        /// The professions of the talent catalogue (Tech/People/TalentCatalogue) an executive of each area is hired from, by
        /// ExecutiveArea: operations from those who run delivery, people from HR and recruiting, money from accounting.
        /// </summary>
        public static readonly int[][] AreaRoles =
        {
            new[] { TalentCatalogue.ChiefExecutive, TalentCatalogue.EngineeringManager, TalentCatalogue.ProductManager, 30 /* business_analyst */ },
            new[] { 25 /* hr_manager */, 26 /* recruiter */ },
            new[] { TalentCatalogue.Accountant, TalentCatalogue.FinancialAnalyst }
        };

        /// <summary>The least level of the talent catalogue an executive is hired at (TalentCatalogue.Senior).</summary>
        public const int ExecutiveLeastLevel = TalentCatalogue.Senior;

        public static HierarchyRank Rank(int id)
        {
            return id >= 0 && id < Ranks.Length ? Ranks[id] : Ranks[0];
        }

        /// <summary>The level's code as both ladders show it, "L0" to "L4".</summary>
        public static string Code(HierarchyLevel level)
        {
            return "L" + ((int)level).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The ranks one step above, into <paramref name="into"/>; how many. The owner has none.</summary>
        public static int Next(int rank, int[] into)
        {
            int step = Rank(rank).Step + 1, n = 0;
            for (int i = 0; i < Ranks.Length && n < into.Length; i++)
            {
                if (Ranks[i].Step == step)
                {
                    into[n++] = i;
                }
            }
            return n;
        }

        /// <summary>Whether a profession of the talent catalogue may be an executive of an area.</summary>
        public static bool Fits(ExecutiveArea area, int role, int level)
        {
            if ((int)area < 0 || (int)area >= AreaRoles.Length || level < ExecutiveLeastLevel)
            {
                return false;
            }
            int[] roles = AreaRoles[(int)area];
            for (int i = 0; i < roles.Length; i++)
            {
                if (roles[i] == role)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
