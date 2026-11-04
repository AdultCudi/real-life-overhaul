using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The city's population as other features read it (the interface of the living economy's later workstreams: tech, Flare, every business
    /// type, construction, civic, the Economy app). Game-free and safe at any time: while the population is not running, every member answers as a
    /// city without one (no figure, the game's own counts). Read on the game's main thread only; nothing here is per frame (each answer is a field
    /// read or a walk of the districts, about a dozen). Money is cents unless a member says whole money; a share is thousandths.
    /// </summary>
    public static class CityPopulation
    {
        /// <summary>The population in use, or null. Set by the population's director only.</summary>
        internal static PopulationState Live { get; set; }

        /// <summary>The day's pools in use (for a household's or a business's part), or null.</summary>
        internal static PoolRules.DemandDay Pools { get; set; }

        /// <summary>True while the population runs (Living population on, a city it knows, the people generated or loaded).</summary>
        public static bool Running => Live != null;

        /// <summary>
        /// The construction demand: homes a year the city wants started on the mod's land (POPULATION.md section 14), 0 with none or no population. The city's
        /// developers answer it through the mod's land's homes pull (<see cref="Pull"/>); the player's construction company through its tenders.
        /// </summary>
        public static int HomesWanted => Live != null ? Live.Growth.Wanted : 0;

        /// <summary>Homes finished in the city in the running year of the population (any builder's).</summary>
        public static int HomesBuilt => Live != null ? Live.Growth.Built : 0;

        /// <summary>Firms closed in the running year of the population.</summary>
        public static int FirmsClosed => Live != null ? Live.Growth.Closed : 0;

        /// <summary>True on a Going Public guest that follows the host's population: the figures are the host's, the people the host's to keep.</summary>
        public static bool Following { get; internal set; }

        /// <summary>The city's people and households now.</summary>
        public static int People => Live == null ? 0 : Live.People.Live > 0 ? Live.People.Live : Live.HostPeople;

        public static int Households => Live == null ? 0 : Live.Households.Live > 0 ? Live.Households.Live : Live.HostHouseholds;

        /// <summary>A district's figures at the last day (its people, jobs, rents, income, the month's births, deaths and moves), by the game's id; null for none.</summary>
        public static DistrictFigures District(string id)
        {
            PopulationState s = Live;
            int d = s != null ? s.DistrictOf(id) : -1;
            return d >= 0 && d < s.Figures.Length ? s.Figures[d] : null;
        }

        /// <summary>Unemployment in the city, thousandths of the labour force; -1 while not running.</summary>
        public static int Unemployment
        {
            get
            {
                PopulationState s = Live;
                if (s == null)
                {
                    return -1;
                }
                int workers = 0, unemployed = 0;
                for (int d = 0; d < s.Figures.Length; d++)
                {
                    workers += s.Figures[d].Workers;
                    unemployed += s.Figures[d].Unemployed;
                }
                return workers + unemployed > 0 ? (int)(1000L * unemployed / (workers + unemployed)) : -1;
            }
        }

        /// <summary>Open vacancies, jobseekers and the chance a job ends a day (thousandths of a thousand: per million), as measured; false while not running.</summary>
        public static bool Jobs(out int vacancies, out int seekers, out int endsPerMillion)
        {
            vacancies = seekers = endsPerMillion = 0;
            PopulationState s = Live;
            if (s == null)
            {
                return false;
            }
            for (int d = 0; d < s.Figures.Length; d++)
            {
                vacancies += s.Figures[d].Vacancies;
                seekers += s.Figures[d].Unemployed;
            }
            endsPerMillion = PopulationCatalogue.Turnover * 1000 / Math.Max(1, s.YearDays);
            return true;
        }

        /// <summary>
        /// A district's income level on the city economy's scale, tenths of a point (600 to 1400): its start moved by its households' mean income
        /// against what it was when the population began, so the readers of CityEconomy.DistrictIncome move with the people from where they stood;
        /// 0 for no figure (the city economy's own then).
        /// </summary>
        public static int IncomeLevel(string id, int start)
        {
            PopulationState s = Live;
            int d = s != null ? s.DistrictOf(id) : -1;
            if (d < 0 || d >= s.Figures.Length || d >= s.IncomeAnchor.Length || s.IncomeAnchor[d] <= 0 || s.Figures[d].MeanIncome <= 0 || start <= 0)
            {
                return 0;
            }
            long level = (long)start * s.Figures[d].MeanIncome / s.IncomeAnchor[d];
            return (int)Math.Max(PopulationCatalogue.IncomeScaleLeast, Math.Min(PopulationCatalogue.IncomeScaleMost, level));
        }

        /// <summary>A business's takings today, cents, its customers and its factor on the game's own count (thousandths), by its address key ("street|number"); false for none.</summary>
        public static bool Business(string key, out long takings, out int customers, out int factor)
        {
            takings = 0;
            customers = 0;
            factor = 1000;
            PopulationState s = Live;
            int b = s != null ? s.BusinessOfKey(key) : -1;
            if (b < 0 || !s.Businesses.Seen[b])
            {
                return false;
            }
            takings = s.Businesses.Takings[b];
            customers = s.Businesses.Visits[b];
            factor = CustomerRules.Factor(takings, s.Businesses.Anchor[b], s.Businesses.AnchorDay[b]);
            return true;
        }

        /// <summary>
        /// The city's spending of a kind today, cents (households, commuters and visitors in the pools; Digital and Utilities as households spent them), for
        /// a market sized from the population (the tech sector's digital market, a business type's demand); 0 while not running.
        /// </summary>
        public static long Spending(Spend kind)
        {
            PopulationState s = Live;
            PoolRules.DemandDay dd = Pools;
            if (s == null || dd == null || dd.Day < 0)
            {
                return 0;
            }
            int k = (int)kind;
            long sum = 0;
            if (k >= PopulationCatalogue.ShopCategoryFirst && k <= PopulationCatalogue.ShopCategoryLast)
            {
                for (int i = 0; i < dd.LineMoney.Length; i++)
                {
                    sum += i % PopulationCatalogue.Categories == k ? dd.LineMoney[i] : 0;
                }
                return sum;
            }
            for (int h = 0; h < s.Households.Count && (h + 1) * PopulationCatalogue.Categories <= dd.Budget.Length; h++)
            {
                sum += s.Households.Alive(h) ? dd.Budget[h * PopulationCatalogue.Categories + k] : 0;
            }
            return sum;
        }

        /// <summary>The council's taxes from the population: the running month's and the last closed month's, cents (income, sales and property tax together).</summary>
        public static long TaxesMonth => Live != null ? Live.Books.Taxes : 0;

        public static long TaxesLastMonth => Live != null ? Live.Books.TaxesLast : 0;

        /// <summary>A district's pull for homes, shops, offices and warehouses (use 0 to 3), -100 to 100, for its construction; -1000 for none counted.</summary>
        public static int Pull(string id, int use)
        {
            PopulationState s = Live;
            int[] pulls = PullTable;
            int d = s != null ? s.DistrictOf(id) : -1;
            return d >= 0 && use >= 0 && use < 4 && pulls != null && d * 4 + use < pulls.Length ? pulls[d * 4 + use] : -1000;
        }

        /// <summary>The districts' pulls, set by the director after each month.</summary>
        internal static int[] PullTable { get; set; }

        /// <summary>The person a person of another system's is (a game employee's id, a professional's...), their id; 0 for none linked.</summary>
        public static int PersonOf(LinkKind kind, string key)
        {
            PopulationState s = Live;
            if (s == null || string.IsNullOrEmpty(key))
            {
                return 0;
            }
            for (int i = 0; i < s.Links.Count; i++)
            {
                if (s.Links[i].Kind == kind && string.Equals(s.Links[i].Key, key, StringComparison.Ordinal))
                {
                    return s.Links[i].Person;
                }
            }
            return 0;
        }

        /// <summary>
        /// The residents who like a genre (0 to 11, the studio's Genre) on a platform they play on (0 PC, 1 console, 2 phone) at the last day's figures,
        /// and at the population's start, whose ratio to the world's people then is the game studio's world factor (Wiring/PopulationLinks); false while
        /// the population does not run. A guest's are the host's. COST: two array reads.
        /// </summary>
        public static bool Players(int genre, int platform, out long now, out long start)
        {
            now = start = 0;
            PopulationState s = Live;
            if (s == null || genre < 0 || genre >= GameRules.Genres || platform < 0 || platform >= GameRules.Platforms)
            {
                return false;
            }
            int cell = genre * GameRules.Platforms + platform;
            now = GameRules.Share(s.GameCounts[cell], s.People.Live > 0 ? s.People.Live : s.HostPeople, s.Growth.PeopleStart);
            start = s.GameStart[cell];
            return true;
        }

        /// <summary>The residents of a district (its id) who like a genre on a platform they play on, at the last day's figures; -1 for none known.</summary>
        public static int PlayersIn(string district, int genre, int platform)
        {
            PopulationState s = Live;
            int d = s != null ? s.DistrictOf(district) : -1;
            int at = d * GameRules.Cells + genre * GameRules.Platforms + platform;
            return d >= 0 && genre >= 0 && genre < GameRules.Genres && platform >= 0 && platform < GameRules.Platforms && at < s.GameByDistrict.Length ? s.GameByDistrict[at] : -1;
        }

        /// <summary>The residents of a class (0 working, 1 middle, 2 upper) who like a genre on a platform they play on, at the last day's figures; -1 for none known.</summary>
        public static int PlayersOfClass(int cls, int genre, int platform)
        {
            PopulationState s = Live;
            bool known = s != null && cls >= 0 && cls < PopulationCatalogue.Classes && genre >= 0 && genre < GameRules.Genres && platform >= 0 && platform < GameRules.Platforms;
            return known ? s.GameByClass[cls * GameRules.Cells + genre * GameRules.Platforms + platform] : -1;
        }

        /// <summary>A day of the population has been lived (its figures and every business's takings are in place): raised on the main thread, once a game day.</summary>
        public static event Action DayLived;

        internal static void RaiseDayLived(Action<Exception> fault)
        {
            Action handlers = DayLived;
            if (handlers == null)
            {
                return;
            }
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    fault?.Invoke(e);
                }
            }
        }
    }
}
