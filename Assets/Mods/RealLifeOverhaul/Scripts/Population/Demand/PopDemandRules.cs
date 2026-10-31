using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The rules of the population's demand for the mod's own businesses (the brief's first step: each business keeps its own mechanics and reads the people
    /// through a factor on its old figure): a household's credit band and satisfaction, a district's crime against the city's, the leans a district's businesses
    /// read, when a population is settled enough to be anchored, the anchor itself, and each demand's factor from its recipe. Game-free.
    /// COST: nothing per frame; a household's band and satisfaction a dozen comparisons, the day's leans and factors a few hundred operations.
    /// </summary>
    public static class PopDemandRules
    {
        public const int Poor = 0, Fair = 1, Good = 2;   // the bands, in the order of the own bank's segments Poor, Fair and Good
        public const int Default = -1;                   // past every band: in default, out of the borrowing market
        public const int LeanLeast = 250, LeanMost = 4000;   // thousandths a district's lean keeps within against the city's

        /// <summary>
        /// A household's credit band from its income against the city's middle (<paramref name="level"/>, thousandths) and its debt against its income after
        /// tax (cents a day; debt whole money). The population keeps debt only where a household spent past its income (it models no car loans or cards, and the
        /// debt it generates a city with is paid off over the first years), so debt marks distress here and income carries the rest: poorly rated past a third
        /// of a year's income in debt, with no income and some debt, or on half the city's middle income or less; well rated at the city's middle income or
        /// over; fairly otherwise; <see cref="Default"/> past a year's income in debt. Income is read against the city's middle, so the bands keep their shares
        /// as the city grows richer or poorer and move as its households' circumstances do.
        /// </summary>
        public static int Band(long disposable, long debt, int level)
        {
            if (disposable <= 0)
            {
                return debt > 0 ? Poor : Fair;
            }
            long debtDays = Math.Max(0, debt) * 100 / disposable;
            if (debtDays > PopDemandCatalogue.DefaultDebtDays)
            {
                return Default;
            }
            if (debtDays > PopDemandCatalogue.PoorDebtDays || level < PopDemandCatalogue.PoorLevel)
            {
                return Poor;
            }
            return level >= PopDemandCatalogue.GoodLevel ? Good : Fair;
        }

        /// <summary>
        /// A household's satisfaction, thousandths: what its rent takes of its income, its adults out of work, its savings and debt against its income, and
        /// room in its flat. No record keeps it: it is the household's circumstances read each day.
        /// </summary>
        public static int Content(long rent, long disposable, long savings, long debt, int outOfWork, int flatSqm, int size)
        {
            int c = PopDemandCatalogue.ContentBase;
            if (disposable > 0)
            {
                long share = Math.Max(0, rent) * 1000 / disposable;
                c += (int)Math.Max(PopDemandCatalogue.ContentRentLeast, Math.Min(PopDemandCatalogue.ContentRentMost, -(share - PopDemandCatalogue.ContentRentFair) / 2));
                long days = Math.Max(0, savings) * 100 / disposable;
                c += days < PopDemandCatalogue.ContentThinDays ? -PopDemandCatalogue.ContentThin : days > PopDemandCatalogue.ContentCushionDays ? PopDemandCatalogue.ContentCushion : 0;
                c -= Math.Max(0, debt) * 100 / disposable > PopDemandCatalogue.ContentDebtDays ? PopDemandCatalogue.ContentDebt : 0;
            }
            else
            {
                c += PopDemandCatalogue.ContentRentLeast - PopDemandCatalogue.ContentThin;   // nothing coming in: the rent and the savings both weigh at their worst
            }
            c -= Math.Min(PopDemandCatalogue.ContentOutOfWorkMost, Math.Max(0, outOfWork) * PopDemandCatalogue.ContentOutOfWork);
            if (flatSqm > 0 && flatSqm < Math.Max(1, size) * PopulationCatalogue.PersonsPerRoom)
            {
                c -= PopDemandCatalogue.ContentCrowded;
            }
            return PopulationCatalogue.Clamp(c, 0, 1000);
        }

        /// <summary>
        /// True once a population has lived through a month's turn since it was generated: its moves, arrivals and the region's wishes are then in its
        /// figures, so the anchor is taken from a city in its ordinary run, not from its first morning.
        /// </summary>
        public static bool Settled(PopulationState s)
        {
            return s != null && s.MonthNumber > GenerateRules.MonthOf(s.Generated, s.YearDays);
        }

        /// <summary>A car's worth of a household's income for its car buying, thousandths: richer households replace sooner, within bounds.</summary>
        public static int CarLean(int level)
        {
            return PopulationCatalogue.Clamp(level, PopDemandCatalogue.CarLeanLeast, PopDemandCatalogue.CarLeanMost);
        }

        /// <summary>
        /// A district's crime against the city's, thousandths: its households' income against the city's (the poorer, the more), its unemployment against
        /// the city's and its share of young people against the city's, weighed; a thousand for a district with nobody in it.
        /// </summary>
        public static int CrimeIndex(long income, int households, int unemployed, int workers, int youth, int people,
                                     long cityIncome, int cityHouseholds, int cityUnemployed, int cityWorkers, int cityYouth, int cityPeople)
        {
            if (households <= 0 || people <= 0 || cityHouseholds <= 0 || cityPeople <= 0)
            {
                return PopDemandCatalogue.Neutral;
            }
            long mine = Math.Max(1, income / households), city = Math.Max(1, cityIncome / cityHouseholds);
            int incomeTerm = Term(city * 1000 / mine);
            long rate = workers + unemployed > 0 ? 1000L * unemployed / (workers + unemployed) : 0;
            long cityRate = cityWorkers + cityUnemployed > 0 ? 1000L * cityUnemployed / (cityWorkers + cityUnemployed) : 0;
            int unemploymentTerm = cityRate > 0 ? Term(rate * 1000 / cityRate) : 1000;
            long share = 1000L * youth / people, cityShare = 1000L * cityYouth / cityPeople;
            int youthTerm = cityShare > 0 ? Term(share * 1000 / cityShare) : 1000;
            long index = ((long)PopDemandCatalogue.CrimeIncome * incomeTerm + (long)PopDemandCatalogue.CrimeUnemployment * unemploymentTerm + (long)PopDemandCatalogue.CrimeYouth * youthTerm) / 1000;
            return (int)Math.Max(PopDemandCatalogue.CrimeLeast, Math.Min(PopDemandCatalogue.CrimeMost, index));
        }

        private static int Term(long thousandths)
        {
            return (int)Math.Max(PopDemandCatalogue.CrimeTermLeast, Math.Min(PopDemandCatalogue.CrimeTermMost, thousandths));
        }

        /// <summary>A district's figure per household against the city's, thousandths, within the leans' bounds; a thousand where either has none.</summary>
        public static int Lean(long part, int households, long cityPart, int cityHouseholds)
        {
            if (households <= 0 || cityHouseholds <= 0 || cityPart <= 0)
            {
                return PopDemandCatalogue.Neutral;
            }
            long mine = Math.Max(0, part) * 1000 / households, city = cityPart * 1000 / cityHouseholds;
            if (city <= 0)
            {
                return PopDemandCatalogue.Neutral;
            }
            return (int)Math.Max(LeanLeast, Math.Min(LeanMost, mine * 1000 / city));
        }

        /// <summary>A district's saving against the city's, thousandths: the share of its income after tax it puts by (which may be less than nothing), offset by a whole so a district that draws on its savings leans below the city.</summary>
        public static int SavingLean(long saved, long disposable, long citySaved, long cityDisposable)
        {
            if (disposable <= 0 || cityDisposable <= 0)
            {
                return PopDemandCatalogue.Neutral;
            }
            long rate = Math.Max(-900, Math.Min(900, saved * 1000 / disposable)), cityRate = Math.Max(-900, Math.Min(900, citySaved * 1000 / cityDisposable));
            return (int)Math.Max(LeanLeast, Math.Min(LeanMost, (1000 + rate) * 1000 / (1000 + cityRate)));
        }

        /// <summary>
        /// The day's leans and the district figures that hang on them, from the day's tallies: income, saving, borrowing and the housing market against the city's,
        /// the bands of each district's borrowers, its crime, and its homes and businesses against the anchor day's.
        /// </summary>
        public static void Leans(PopDemandState m)
        {
            int n = m.Districts;
            long income = 0, disposable = 0, saved = 0, housing = 0;
            int households = 0, unemployed = 0, workers = 0, youth = 0, people = 0;
            long debt = 0, borrow0 = 0, borrow1 = 0, borrow2 = 0;
            for (int d = 0; d < n; d++)
            {
                income += m.Income[d];
                disposable += m.Disposable[d];
                saved += m.Saved[d];
                housing += m.Housing[d];
                households += m.Households[d];
                unemployed += m.Unemployed[d];
                workers += m.Workers[d];
                youth += m.Youth[d];
                people += m.People[d];
                debt += m.Debt[d];
                borrow0 += m.Borrowing[d * PopDemandCatalogue.Bands];
                borrow1 += m.Borrowing[d * PopDemandCatalogue.Bands + 1];
                borrow2 += m.Borrowing[d * PopDemandCatalogue.Bands + 2];
            }
            long cityBorrow = borrow0 + borrow1 + borrow2;
            for (int d = 0; d < n; d++)
            {
                int hh = m.Households[d];
                long mine = m.Borrowing[d * PopDemandCatalogue.Bands] + m.Borrowing[d * PopDemandCatalogue.Bands + 1] + m.Borrowing[d * PopDemandCatalogue.Bands + 2];
                m.IncomeLean[d] = Lean(m.Income[d], hh, income, households);
                m.SavingLean[d] = SavingLean(m.Saved[d], m.Disposable[d], saved, disposable);
                m.BorrowLean[d] = Lean(m.Debt[d], hh, debt, households);
                m.HousingLean[d] = Lean(m.Housing[d], hh, housing, households);
                for (int b = 0; b < PopDemandCatalogue.Bands; b++)
                {
                    long part = mine > 0 ? m.Borrowing[d * PopDemandCatalogue.Bands + b] : b == Poor ? borrow0 : b == Fair ? borrow1 : borrow2;
                    long all = mine > 0 ? mine : cityBorrow;
                    m.Ask[d * PopDemandCatalogue.Bands + b] = all > 0 ? (int)(part * 1000 / all) : (b == Fair ? 1000 : 0);
                }
                m.Crime[d] = CrimeIndex(m.Income[d], hh, m.Unemployed[d], m.Workers[d], m.Youth[d], m.People[d], income, households, unemployed, workers, youth, people);
                long homes = (long)hh + m.Businesses[d];
                m.ConnectionFactor[d] = m.Connections[d] > 0 ? (int)Math.Min(PopDemandCatalogue.FactorMost, homes * 1000 / m.Connections[d]) : PopDemandCatalogue.Neutral;
            }
        }

        /// <summary>
        /// The anchor: on the first settled day every source's figure is taken as the anchor (so every demand reads a thousand that day); after it, a source,
        /// a kind of online spending or a district's homes first seen above nothing (a district the city has just peopled) are anchored where they are first seen.
        /// Returns true on the day the whole anchor was taken.
        /// </summary>
        public static bool Anchor(PopDemandState m, PopulationState s, int day)
        {
            bool whole = false;
            if (m.AnchorDay < 0)
            {
                if (!Settled(s))
                {
                    return false;
                }
                m.AnchorDay = day;
                whole = true;
            }
            for (int i = 0; i < m.Now.Length; i++)
            {
                if (m.Anchor[i] <= 0 && m.Now[i] > 0)
                {
                    m.Anchor[i] = m.Now[i];
                }
            }
            for (int k = 0; k < m.OnlineNow.Length; k++)
            {
                if (m.OnlineAnchor[k] <= 0 && m.OnlineNow[k] > 0)
                {
                    m.OnlineAnchor[k] = m.OnlineNow[k];
                }
            }
            for (int d = 0; d < m.Districts; d++)
            {
                long homes = (long)m.Households[d] + m.Businesses[d];
                if (m.Connections[d] <= 0 && homes > 0)
                {
                    m.Connections[d] = homes;
                }
            }
            return whole;
        }

        /// <summary>Every demand's factor from its recipe (each source's figure against its anchor, weighed), and each kind of online spending's.</summary>
        public static void Refactor(PopDemandState m)
        {
            for (int i = 0; i < m.Factors.Length; i++)
            {
                m.Factors[i] = Factor(m, (PopDemand)i);
            }
            for (int k = 0; k < m.OnlineFactors.Length; k++)
            {
                m.OnlineFactors[k] = m.AnchorDay < 0 ? PopDemandCatalogue.Neutral : PopDemandCatalogue.Ratio(m.OnlineNow[k], m.OnlineAnchor[k]);
            }
        }

        /// <summary>A demand's factor now, thousandths of the anchor day's: a thousand before the anchor.</summary>
        public static int Factor(PopDemandState m, PopDemand d)
        {
            if (m.AnchorDay < 0)
            {
                return PopDemandCatalogue.Neutral;
            }
            (PopSource Source, int Weight)[] recipe = PopDemandCatalogue.Recipe(d);
            long sum = 0;
            int weights = 0;
            for (int i = 0; i < recipe.Length; i++)
            {
                int src = (int)recipe[i].Source;
                sum += (long)recipe[i].Weight * PopDemandCatalogue.Ratio(m.Now[src], m.Anchor[src]);
                weights += recipe[i].Weight;
            }
            return weights > 0 ? (int)Math.Max(0, Math.Min(PopDemandCatalogue.FactorMost, sum / weights)) : PopDemandCatalogue.Neutral;
        }

        /// <summary>
        /// A source's part of a demand now, thousandths of the demand's anchor-day figure (its weight times its figure against its anchor): the parts of a
        /// demand sum to its factor. For the Economy app's "where it comes from".
        /// </summary>
        public static int Part(PopDemandState m, PopDemand d, int index)
        {
            (PopSource Source, int Weight)[] recipe = PopDemandCatalogue.Recipe(d);
            if (index < 0 || index >= recipe.Length)
            {
                return 0;
            }
            int src = (int)recipe[index].Source;
            int ratio = m.AnchorDay < 0 ? PopDemandCatalogue.Neutral : PopDemandCatalogue.Ratio(m.Now[src], m.Anchor[src]);
            return (int)((long)recipe[index].Weight * ratio / 1000);
        }
    }
}
