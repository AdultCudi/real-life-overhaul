using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The day's measure of what the people bring the mod's own businesses, in steps a frame can hold, after the population's own day (its households' money
    /// and the day's pools are in place): one pass over the households (savings and debt by credit band, spending on leisure, goods and online, cars, the
    /// housing market, satisfaction, taxes), one over the people (adults, the young, work, who wants a course, the professions' seekers, the private wage
    /// bill), one over the employers (commuters' pay, the professions' vacancies) and one over the businesses (takings, sales tax); then each source's figure,
    /// each district's leans, the anchor and the factors. The same job run whole (<see cref="RunAll"/>) for the offline checks and a test. Game-free.
    /// COST: nothing per frame while idle; at work the slice given (the population's millisecond), about a fifth of the population's own day in all.
    /// </summary>
    public sealed class PopDemandJob
    {
        public enum Phase { Idle, Households, People, Employers, Businesses, Finish }

        private PopulationState _s;
        private PoolRules.DemandDay _dd;
        private PopDemandState _m;
        private int[] _pulls;
        private Phase _phase = Phase.Idle;
        private int _cursor, _day, _monthDays;
        private string[] _ids = new string[0];

        // The day's tallies, as the sources are made of them.
        private long _savings, _wealth, _leisure, _bargains, _carWant, _sellers, _budget, _rent, _taxes, _wages, _turnover, _proSeekers, _content;
        private readonly long[] _borrow = new long[PopDemandCatalogue.Bands];
        private readonly long[] _online = new long[PopDemandCatalogue.Kinds];
        private int _carless, _housing, _starters, _listings, _learners, _contentCount, _proOpenings;

        public bool Busy => _phase != Phase.Idle;
        public Phase Now => _phase;

        /// <summary>A day's measure begins: the demand's districts sized for the city, its identity checked (another population is anchored again), the tallies at nothing.</summary>
        public void Begin(PopulationState s, PoolRules.DemandDay dd, int[] pulls, PopDemandState m)
        {
            _s = s;
            _dd = dd;
            _m = m;
            _pulls = pulls;
            _day = s.Day;
            _monthDays = GenerateRules.MonthDays(s.YearDays);
            if (m.Seed != s.Seed || m.Generated != s.Generated)
            {
                m.Forget();
                m.Seed = s.Seed;
                m.Generated = s.Generated;
            }
            if (_ids.Length != s.Districts.Count)
            {
                _ids = new string[s.Districts.Count];
            }
            for (int d = 0; d < _ids.Length; d++)
            {
                _ids[d] = s.Districts[d].Id;
            }
            m.Size(_ids);
            m.ClearDay();
            _savings = _wealth = _leisure = _bargains = _carWant = _sellers = _budget = _rent = _taxes = _wages = _turnover = _proSeekers = _content = 0;
            Array.Clear(_borrow, 0, _borrow.Length);
            Array.Clear(_online, 0, _online.Length);
            _carless = _housing = _starters = _listings = _learners = _contentCount = _proOpenings = 0;
            _cursor = 0;
            _phase = Phase.Households;
        }

        /// <summary>Runs the job to its end at once.</summary>
        public void RunAll()
        {
            WorkSlice whole = WorkSlice.Whole;
            while (!Step(ref whole))
            {
                whole = WorkSlice.Whole;
            }
        }

        /// <summary>Does what fits in the slice; true when the job is done.</summary>
        public bool Step(ref WorkSlice slice)
        {
            while (_phase != Phase.Idle && slice.More())
            {
                switch (_phase)
                {
                    case Phase.Households:
                        for (int to = Math.Min(_s.Households.Count, _cursor + PopulationCatalogue.HouseholdStep); _cursor < to; _cursor++)
                        {
                            Household(_cursor);
                        }
                        Through(_s.Households.Count, Phase.People);
                        break;
                    case Phase.People:
                        for (int to = Math.Min(_s.People.Count, _cursor + PopulationCatalogue.PeopleStep); _cursor < to; _cursor++)
                        {
                            Person(_cursor);
                        }
                        Through(_s.People.Count, Phase.Employers);
                        break;
                    case Phase.Employers:
                        Employers();
                        Next(Phase.Businesses);
                        break;
                    case Phase.Businesses:
                        Businesses();
                        Next(Phase.Finish);
                        break;
                    case Phase.Finish:
                        Finish();
                        _phase = Phase.Idle;
                        return true;
                }
            }
            return _phase == Phase.Idle;
        }

        private void Next(Phase phase)
        {
            _phase = phase;
            _cursor = 0;
        }

        private void Through(int count, Phase next)
        {
            if (_cursor >= count)
            {
                Next(next);
            }
        }

        // One household: its money, its band, its spending of the kinds the sources count, its car, its place in the housing market, its satisfaction and
        // its taxes; its district's figures. The player's household is the player's: none of its money is the city's.
        private void Household(int h)
        {
            PopulationState s = _s;
            HouseholdTable hh = s.Households;
            if (!hh.Alive(h) || (hh.Flags[h] & HouseholdFlags.Players) != 0)
            {
                return;
            }
            SpendRules.Income(s, h, _day, 0, out DayIncome inc);
            long disposable = inc.Disposable, savings = hh.Savings[h], debt = hh.Debt[h];
            int level = SpendRules.Level(disposable, s.Median);
            int band = PopDemandRules.Band(disposable, debt, level);
            int cls = hh.Class[h];
            bool away = (hh.Flags[h] & HouseholdFlags.Away) != 0;
            _savings += savings;
            if (band >= 0)
            {
                _borrow[band] += Math.Max(0, disposable);
            }
            _wealth += cls >= (int)PopClass.Middle ? savings : 0;
            long spent = 0;
            bool pools = _dd != null && _dd.Day == _day && (h + 1) * PopDemandCatalogue.Kinds <= _dd.Budget.Length;
            if (pools)
            {
                int row = h * PopDemandCatalogue.Kinds;
                int[] budget = _dd.Budget;
                _leisure += (long)budget[row + (int)Spend.EatingOut] + budget[row + (int)Spend.Cinema] + budget[row + (int)Spend.Theatre] + budget[row + (int)Spend.NightLife]
                            + budget[row + (int)Spend.Casino];
                if (cls <= (int)PopClass.Middle)
                {
                    _bargains += (long)budget[row + (int)Spend.Electronics] + budget[row + (int)Spend.Furniture] + budget[row + (int)Spend.Clothing] + budget[row + (int)Spend.BooksGifts];
                }
                for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
                {
                    _online[k] += (long)budget[row + k] * PopDemandCatalogue.OnlineShare[k] / 1000;
                }
                for (int k = (int)Spend.Utilities; k <= (int)Spend.Digital; k++)
                {
                    spent += budget[row + k];
                }
            }
            if (hh.Cars[h] == 0)
            {
                _carless += level >= PopDemandCatalogue.HireLevelLeast ? 1 : 0;
                _carWant += level >= PopDemandCatalogue.FirstCarLevel ? (long)PopDemandCatalogue.FirstCarPerMille * PopDemandRules.CarLean(level) / 1000 : 0;
            }
            else
            {
                _carWant += 1000L / PopDemandCatalogue.CarLifeYears * PopDemandRules.CarLean(level) / 1000;
            }

            // Its people: the adults out of work, and the head's age.
            int outOfWork = 0, headAge = -1;
            for (int p = hh.First[h]; p >= 0; p = s.People.Next[p])
            {
                if (headAge < 0)
                {
                    headAge = s.Age(p, _day);
                }
                outOfWork += s.People.Status[p] == (byte)Status.Unemployed ? 1 : 0;
            }

            int building = hh.Building[h];
            bool recent = !away && hh.Since[h] <= _day && _day - hh.Since[h] < _monthDays;
            bool looking = away || (hh.Flags[h] & HouseholdFlags.WantsMove) != 0;
            int d = hh.District[h];
            if (recent || looking)
            {
                _housing++;
                if (!away && d < _m.Districts)
                {
                    _m.Housing[d]++;
                }
                if (building >= 0 && disposable > 0)
                {
                    _budget += disposable * PopulationCatalogue.AffordableShare / 1000;
                    _rent += CityTable.FlatRentNow(s, building, hh.Unit[h]);
                }
            }
            _sellers += (recent ? 1000 : 0) + (hh.Landlord[h] == Landlords.Own ? 1000 / (PopDemandCatalogue.OwnerYears * 12) : 0);
            _starters += recent && headAge >= PopDemandCatalogue.StarterFrom && headAge <= PopDemandCatalogue.StarterTo ? 1 : 0;
            bool short_ = debt > 0 && (disposable <= 0 || savings * 100 < (long)PopDemandCatalogue.ShortSavingsDays * disposable);
            _listings += recent || short_ ? 1 : 0;
            int flatSqm = building >= 0 ? CityTable.FlatSqmOf(s, building, hh.Unit[h]) : 0;
            _content += PopDemandRules.Content(hh.Rent[h], disposable, savings, debt, outOfWork, flatSqm, hh.Size[h]);
            _contentCount++;
            _taxes += inc.IncomeTax + (long)hh.Rent[h] * PopulationCatalogue.PropertyTaxShare / 1000;
            if (away || d >= _m.Districts)
            {
                return;   // no flat to count it in (its building is not standing at this load)
            }
            _m.Households[d]++;
            _m.Income[d] += inc.Gross;
            _m.Disposable[d] += disposable;
            _m.Saved[d] += pools ? disposable - hh.Rent[h] - spent : 0;
            _m.Debt[d] += disposable > 0 ? Math.Min(Math.Max(0, debt), (long)PopDemandCatalogue.PoorDebtDays * disposable / 100) : 0;   // past the poor band's line a lender counts no more
            if (band >= 0)
            {
                _m.Borrowing[d * PopDemandCatalogue.Bands + band] += Math.Max(0, disposable);
            }
        }

        // One person: their district's adults, young people and work; whether they want a course; the professions' seekers; their pay in the private wage bill.
        private void Person(int p)
        {
            PopulationState s = _s;
            PersonTable pt = s.People;
            if (pt.Id[p] == 0 || (pt.Flags[p] & PersonFlags.Frozen) != 0)
            {
                return;   // the player is a resident, but none of the city's figures
            }
            int h = pt.Household[p];
            if (h < 0 || !s.Households.Alive(h) || (s.Households.Flags[h] & HouseholdFlags.Away) != 0)
            {
                return;
            }
            int d = s.Households.District[h];
            int age = s.Age(p, _day);
            var status = (Status)pt.Status[p];
            if (d < _m.Districts)
            {
                _m.People[d]++;
                _m.Adults[d] += age >= PopulationCatalogue.AdultLeast ? 1 : 0;
                _m.Youth[d] += age >= PopDemandCatalogue.YouthFrom && age <= PopDemandCatalogue.YouthTo ? 1 : 0;
                _m.Workers[d] += status == Status.Worker ? 1 : 0;
                _m.Unemployed[d] += status == Status.Unemployed ? 1 : 0;
            }
            int e = pt.Job[p];
            if (status == Status.Worker && e >= 0 && e < s.Employers.Count && s.Employers.Kind[e] != (byte)EmployerKind.Public)
            {
                _wages += LabourRules.DailyPay(s, p);
            }
            if (age >= PopDemandCatalogue.LearnerFrom && age <= PopDemandCatalogue.LearnerTo
                && (status == Status.Unemployed || (status == Status.Worker && pt.Education[p] <= (byte)Education.College && pt.Skill[p] < PopDemandCatalogue.LearnerSkillUnder)))
            {
                _learners++;
            }
            if (PopDemandCatalogue.Profession(pt.Field[p]))
            {
                _proSeekers += status == Status.Unemployed ? 1000 : status == Status.Worker ? PopDemandCatalogue.SwitchPerMille : 0;
            }
        }

        // The employers: the commuters' pay in the private wage bill, and the professions' vacancies the player does not fill.
        private void Employers()
        {
            EmployerTable et = _s.Employers;
            for (int e = 0; e < et.Count; e++)
            {
                if (et.Kind[e] != (byte)EmployerKind.Public && et.Commuters[e] > 0)
                {
                    _wages += (long)et.Commuters[e] * LabourRules.CommuterPay(_s, e);
                }
                if (!et.Players[e] && PopDemandCatalogue.Profession(et.Field[e]))
                {
                    _proOpenings += et.Vacancies(e);
                }
            }
        }

        // The businesses: their takings and the sales tax in them, and each district's businesses (homes and businesses are broadband's market).
        private void Businesses()
        {
            BusinessTable bt = _s.Businesses;
            for (int b = 0; b < bt.Count; b++)
            {
                if (!bt.Seen[b])
                {
                    continue;
                }
                _turnover += bt.Takings[b];
                _taxes += bt.Takings[b] * PopulationCatalogue.SalesTax / 1000;
                if (bt.District[b] < _m.Districts)
                {
                    _m.Businesses[bt.District[b]]++;
                }
            }
        }

        // The day's sources from the tallies, the districts' leans, the anchor and the factors.
        private void Finish()
        {
            PopulationState s = _s;
            PopDemandState m = _m;
            long visitors = 0, pull = 0;
            int migrants = 0;
            for (int d = 0; d < s.Districts.Count; d++)
            {
                visitors += (long)s.Districts[d].Pull * PopulationCatalogue.VisitorsPerPull;
                migrants += d < s.Figures.Length ? s.Figures[d].MigrantsWanting : 0;
            }
            for (int i = 0; _pulls != null && i < _pulls.Length; i++)
            {
                pull += _pulls[i] > -1000 ? _pulls[i] + HousingRules.PullScale : 0;
            }
            long[] now = m.Now;
            now[(int)PopSource.Savings] = _savings;
            now[(int)PopSource.BorrowPoor] = _borrow[PopDemandRules.Poor];
            now[(int)PopSource.BorrowFair] = _borrow[PopDemandRules.Fair];
            now[(int)PopSource.BorrowGood] = _borrow[PopDemandRules.Good];
            now[(int)PopSource.Turnover] = _turnover;
            now[(int)PopSource.Wages] = _wages;
            now[(int)PopSource.Visitors] = visitors;
            now[(int)PopSource.Leisure] = _leisure;
            now[(int)PopSource.Carless] = _carless;
            now[(int)PopSource.Housing] = _housing + migrants;
            now[(int)PopSource.Budget] = _rent > 0 ? _budget * 1000 / _rent : 0;
            now[(int)PopSource.Sellers] = _sellers;
            now[(int)PopSource.Starters] = _starters;
            now[(int)PopSource.Wealth] = _wealth;
            now[(int)PopSource.Taxes] = _taxes;
            now[(int)PopSource.Pull] = pull;
            now[(int)PopSource.CarWant] = _carWant;
            long online = 0;
            for (int k = 0; k < _online.Length; k++)
            {
                m.OnlineNow[k] = _online[k];
                online += _online[k];
            }
            now[(int)PopSource.Online] = online;
            now[(int)PopSource.Listings] = _listings;
            now[(int)PopSource.Bargains] = _bargains;
            now[(int)PopSource.Learners] = _learners;
            now[(int)PopSource.Content] = _contentCount > 0 ? _content / _contentCount : 0;
            now[(int)PopSource.ProSeekers] = _proSeekers / 1000;
            now[(int)PopSource.ProOpenings] = _proOpenings;
            long adults = 0, households = 0;
            for (int d = 0; d < m.Districts; d++)
            {
                adults += m.Adults[d];
                households += m.Households[d];
            }
            now[(int)PopSource.Adults] = adults;
            now[(int)PopSource.Households] = households;
            PopDemandRules.Anchor(m, s, _day);
            PopDemandRules.Leans(m);
            PopDemandRules.Refactor(m);
            m.Day = _day;
        }
    }
}
