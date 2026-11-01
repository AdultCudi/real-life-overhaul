using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The population's work, in steps a frame can hold (POPULATION.md section 11): the generation of a city, and each game day's turn (the month's
    /// rents, moves and arrivals on the month's first day; then life, the labour market, every household's money and the day's pools, the
    /// businesses' takings and the districts' figures). Each step does what fits in the slice it is given and says when the job is done; the
    /// same job run whole (<see cref="RunAll"/>, for the offline checks and behind a loading screen) is the same work in one go. Game-free.
    /// COST: nothing per frame while idle; at work the slice given (Core/DayPump's 2 ms, of which the population takes about one).
    /// </summary>
    public sealed class PopulationSim
    {
        /// <summary>Where a job stands.</summary>
        public enum Phase
        {
            Idle, Generate, Setup, FillStart, Fill, FillEnd, Finish, Median,
            MonthRoll, MonthWages, MonthRents, Moves, Arrivals,
            LifeBegin, Life, LifeEnd, LabourBegin, Labour, Commuters,
            MoneyBegin, Money, Outsiders, Route, Allocate, Close, Employers,
            FiguresBegin, FiguresHouseholds, FiguresRest, Done
        }

        public PopulationState State;
        public readonly PoolRules.DemandDay Demand = new PoolRules.DemandDay();
        public readonly LifeRules.LifeDay Life = new LifeRules.LifeDay();
        public DistrictMeasure[] Measure = new DistrictMeasure[0];
        public int[] Pulls = new int[0];                 // by district and use (homes, shops, offices, warehouses): -100 to 100, -1000 none
        public long TakingsLast, TakingsBefore;          // the city's takings in the last two closed months, for the offices' pull

        private readonly List<int>[] _open = new List<int>[PopulationCatalogue.Fields];
        private readonly int[] _shares = new int[PopulationCatalogue.Categories];
        private readonly int[] _budget = new int[PopulationCatalogue.Categories];
        private readonly int[] _vacancies = new int[PopulationCatalogue.Fields];
        private readonly int[] _slots = new int[PopulationCatalogue.Fields];
        private readonly int[] _seekers = new int[PopulationCatalogue.Fields];
        private readonly int[] _workers = new int[PopulationCatalogue.Fields];
        private long[] _disposable = new long[0];
        private long[] _gross = new long[0];
        private int _disposableCount;
        private IList<BusinessSeed> _seeds;
        private DemandInputs _inputs;
        private Phase _phase = Phase.Idle;
        private int _cursor, _day, _tightness;
        private int _residentOut;   // thousandths of the city's own labour force out of work today, which slows the region's fill
        private int[] _games = new int[GameRules.Cells], _gamesByDistrict = new int[0], _gamesByClass = new int[PopulationCatalogue.Classes * GameRules.Cells];   // the day's counts as made
        private bool _gamesMade;   // the figures' pass of this job counted them
        private bool _generating;

        public bool Busy => _phase != Phase.Idle;
        public Phase Now => _phase;
        public int Day => _day;

        /// <summary>A new city: its buildings laid out with the seed and the day; the registrations for its employers and businesses.</summary>
        public void BeginGenerate(PopulationState s, int seed, int day, int yearDays, IList<BusinessSeed> seeds)
        {
            State = s;
            GenerateRules.Begin(s, seed, day, yearDays);
            Measure = MeasureRules.Measure(s.Buildings, s.Districts, null);
            _seeds = seeds;
            _day = day;
            _generating = true;
            _cursor = 0;
            _phase = Phase.Generate;
        }

        /// <summary>
        /// A game day's turn: the registrations as they stand (null keeps the last), and what the day reads from the national economy. The month's
        /// work comes first on a month's first day.
        /// </summary>
        public void BeginDay(int day, in DemandInputs inputs, IList<BusinessSeed> seeds)
        {
            PopulationState s = State;
            _day = day;
            _inputs = inputs;
            if (seeds != null)
            {
                _seeds = seeds;
                LabourRules.Setup(s, seeds);
                Measure = MeasureRules.Measure(s.Buildings, s.Districts, null);
            }
            s.Day = day;
            _generating = false;
            _cursor = 0;
            int month = GenerateRules.MonthOf(day, s.YearDays);
            _phase = month != s.MonthNumber ? Phase.MonthRoll : Phase.LifeBegin;
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

        /// <summary>Does what fits in the slice; true when the job is done (the sim idle again).</summary>
        public bool Step(ref WorkSlice slice)
        {
            PopulationState s = State;
            while (_phase != Phase.Idle && slice.More())
            {
                switch (_phase)
                {
                    case Phase.Generate:
                        if (_cursor < s.Buildings.Count)
                        {
                            GenerateRules.Building(s, _cursor++);
                        }
                        else
                        {
                            Next(Phase.Setup);
                        }
                        break;
                    case Phase.Setup:
                        LabourRules.Setup(s, _seeds);
                        Next(Phase.FillStart);
                        break;
                    case Phase.FillStart:
                        LabourRules.Openings(s, _open);
                        Next(Phase.Fill);
                        break;
                    case Phase.Fill:
                        for (int to = Until(s.People.Count, PopulationCatalogue.PeopleStep); _cursor < to; _cursor++)
                        {
                            LabourRules.FillPerson(s, _cursor, _open);
                        }
                        Through(s.People.Count, Phase.FillEnd);
                        break;
                    case Phase.FillEnd:
                        LabourRules.FillCommuters(s);
                        Next(Phase.Finish);
                        break;
                    case Phase.Finish:
                        for (int to = Until(s.Households.Count, PopulationCatalogue.HouseholdStep); _cursor < to; _cursor++)
                        {
                            if (s.Households.Alive(_cursor))
                            {
                                GenerateRules.Finish(s, _cursor);
                            }
                        }
                        Through(s.Households.Count, Phase.Median);
                        break;
                    case Phase.Median:
                        s.Median = MedianNow(s);
                        if (_generating)
                        {
                            Next(Phase.LifeBegin);   // the first day is lived at once, so every figure and the businesses' anchors are there from the start
                            _generating = false;
                        }
                        else
                        {
                            Next(Phase.Done);
                        }
                        break;

                    case Phase.MonthRoll:
                        MonthRoll(s);
                        Next(Phase.MonthWages);
                        break;
                    case Phase.MonthWages:
                        LabourRules.Month(s, _vacancies, _slots, _seekers, _workers);
                        Next(Phase.MonthRents);
                        break;
                    case Phase.MonthRents:
                        HousingRules.Month(s);
                        Life.Begin(s);
                        LabourRules.Openings(s, _open);
                        Next(Phase.Moves);
                        break;
                    case Phase.Moves:
                        for (int to = Until(s.Households.Count, PopulationCatalogue.HouseholdStep); _cursor < to; _cursor++)
                        {
                            if (s.Households.Alive(_cursor))
                            {
                                RegionRules.Household(s, Life, _cursor, _day);
                            }
                        }
                        Through(s.Households.Count, Phase.Arrivals);
                        break;
                    case Phase.Arrivals:
                        RegionRules.Arrivals(s, Life, _day, VacanciesNow(s), _open);
                        Next(Phase.LifeBegin);
                        break;

                    case Phase.LifeBegin:
                        Life.Begin(s);
                        Next(Phase.Life);
                        break;
                    case Phase.Life:
                        for (int to = Until(s.People.Count, PopulationCatalogue.PeopleStep); _cursor < to; _cursor++)
                        {
                            LifeRules.Person(s, Life, _cursor, _day);
                        }
                        Through(s.People.Count, Phase.LifeEnd);
                        break;
                    case Phase.LifeEnd:
                        LifeRules.EndOfDay(s, Life, _day);
                        Next(Phase.LabourBegin);
                        break;
                    case Phase.LabourBegin:
                        LabourRules.Openings(s, _open);
                        _tightness = Tightness(s, out _residentOut);
                        Next(Phase.Labour);
                        break;
                    case Phase.Labour:
                        for (int to = Until(s.People.Count, PopulationCatalogue.PeopleStep); _cursor < to; _cursor++)
                        {
                            if (s.People.Id[_cursor] != 0)
                            {
                                LabourRules.Day(s, _cursor, _day, _open, _tightness);
                            }
                        }
                        Through(s.People.Count, Phase.Commuters);
                        break;
                    case Phase.Commuters:
                        LabourRules.Commuters(s, _day, _residentOut);
                        Next(Phase.MoneyBegin);
                        break;

                    case Phase.MoneyBegin:
                        PoolRules.Prepare(s, Demand, _inputs, _day);
                        if (s.Median <= 0)
                        {
                            s.Median = MedianNow(s);   // a population from before the middle was kept: once
                        }
                        SizeScratch(s);
                        _disposableCount = 0;
                        Next(Phase.Money);
                        break;
                    case Phase.Money:
                        for (int to = Until(s.Households.Count, PopulationCatalogue.HouseholdStep); _cursor < to; _cursor++)
                        {
                            MoneyOf(_cursor);
                        }
                        Through(s.Households.Count, Phase.Outsiders);
                        break;
                    case Phase.Outsiders:
                        PoolRules.Outsiders(s, Demand, _inputs);
                        Next(Phase.Route);
                        break;
                    case Phase.Route:
                        PoolRules.Route(s, Demand);
                        Next(Phase.Allocate);
                        break;
                    case Phase.Allocate:
                        PoolRules.Allocate(s, Demand);
                        Next(Phase.Close);
                        break;
                    case Phase.Close:
                        PoolRules.Close(s, Demand, _day);
                        Next(Phase.Employers);
                        break;
                    case Phase.Employers:
                        EmployersDay(s);
                        Next(Phase.FiguresBegin);
                        break;
                    case Phase.FiguresBegin:
                        for (int d = 0; d < s.Figures.Length; d++)
                        {
                            s.Figures[d].Clear();
                        }
                        Array.Clear(_games, 0, _games.Length);
                        if (_gamesByDistrict.Length != s.Districts.Count * GameRules.Cells)
                        {
                            _gamesByDistrict = new int[s.Districts.Count * GameRules.Cells];
                        }
                        Array.Clear(_gamesByDistrict, 0, _gamesByDistrict.Length);
                        Array.Clear(_gamesByClass, 0, _gamesByClass.Length);
                        _gamesMade = true;
                        Next(Phase.FiguresHouseholds);
                        break;
                    case Phase.FiguresHouseholds:
                        for (int to = Until(s.Households.Count, PopulationCatalogue.HouseholdStep); _cursor < to; _cursor++)
                        {
                            FigureHousehold(s, _cursor);
                        }
                        Through(s.Households.Count, Phase.FiguresRest);
                        break;
                    case Phase.FiguresRest:
                        FiguresRest(s);
                        if (_disposableCount > 0)
                        {
                            Array.Sort(_disposable, 0, _disposableCount);
                            s.Median = Math.Max(1, _disposable[_disposableCount / 2]);   // tomorrow's levels are measured by today's middle
                        }
                        Next(Phase.Done);
                        break;
                    case Phase.Done:
                        s.Day = _day;
                        if (_gamesMade)
                        {
                            _gamesMade = false;
                            PublishGames(s);
                        }
                        for (int d = 0; d < s.IncomeAnchor.Length && d < s.Figures.Length; d++)
                        {
                            if (s.IncomeAnchor[d] <= 0)
                            {
                                s.IncomeAnchor[d] = s.Figures[d].MeanIncome;   // a district's first day with people: the city economy's income moves from here
                            }
                        }
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

        // The end of a phase's next items from its cursor: as many as one step holds.
        private int Until(int count, int step)
        {
            return Math.Min(count, _cursor + step);
        }

        // The next phase once the cursor is through a phase's items.
        private void Through(int count, Phase next)
        {
            if (_cursor >= count)
            {
                Next(next);
            }
        }

        private void SizeScratch(PopulationState s)
        {
            if (_disposable.Length < s.Households.Capacity)
            {
                _disposable = new long[s.Households.Capacity];
                _gross = new long[s.Households.Capacity];
            }
        }

        // One household's money: its day booked, its spending into the pools, its people's pay into their employers' wage bills.
        private void MoneyOf(int h)
        {
            PopulationState s = State;
            if (!s.Households.Alive(h))
            {
                return;
            }
            PoolRules.Household(s, Demand, h, _day, _inputs.BaseBp, _inputs.Mood, _shares, _budget, out DayIncome inc);
            _gross[h] = inc.Gross;
            _disposable[_disposableCount++] = inc.Disposable;
            for (int p = s.Households.First[h]; p >= 0; p = s.People.Next[p])
            {
                int e = s.People.Job[p];
                if (e >= 0 && s.People.Status[p] == (byte)Status.Worker)
                {
                    s.Employers.Bill[e] += LabourRules.DailyPay(s, p);
                }
            }
            int d = s.Households.District[h];
            if (d < s.Figures.Length)
            {
                long spent = 0;
                for (int k = (int)Spend.Utilities; k <= (int)Spend.Digital; k++)
                {
                    spent += _budget[k];
                }
                DistrictFigures f = s.Figures[d];
                f.Spending += spent;
                f.Taxes += inc.IncomeTax + (long)s.Households.Rent[h] * PopulationCatalogue.PropertyTaxShare / 1000;
            }
        }

        // The employers' day: the commuters' pay into their wage bills (the going rate of the field), and the output of those that sell nothing
        // to people (offices' other floors, the public sector, a building's staff, special services): their wage bill and a firm's margin.
        private static void EmployersDay(PopulationState s)
        {
            EmployerTable et = s.Employers;
            for (int e = 0; e < et.Count; e++)
            {
                if (et.Commuters[e] > 0)
                {
                    et.Bill[e] += (long)et.Commuters[e] * LabourRules.CommuterPay(s, e);
                }
            }
        }

        // A household's part of its district's figures: its people by what they do, its income and rent.
        private void FigureHousehold(PopulationState s, int h)
        {
            if (!s.Households.Alive(h))
            {
                return;
            }
            int d = s.Households.District[h];
            if (d >= s.Figures.Length || (s.Households.Flags[h] & HouseholdFlags.Away) != 0)
            {
                return;
            }
            DistrictFigures f = s.Figures[d];
            f.Households++;
            f.MeanIncome += (int)Math.Min(int.MaxValue / 4, h < _gross.Length ? _gross[h] : 0);
            if (s.Households.Landlord[h] != Landlords.Own)
            {
                f.MeanRent += s.Households.Rent[h];
            }
            int c = Math.Min((int)s.Households.Class[h], PopulationCatalogue.Classes - 1);
            for (int p = s.Households.First[h]; p >= 0; p = s.People.Next[p])
            {
                ushort games = s.People.Games[p];
                if (games != 0)
                {
                    GameRules.Add(games, _games, 0);
                    GameRules.Add(games, _gamesByDistrict, d * GameRules.Cells);
                    GameRules.Add(games, _gamesByClass, c * GameRules.Cells);
                }
                f.People++;
                switch ((Status)s.People.Status[p])
                {
                    case Status.Child: f.Children++; break;
                    case Status.Pupil: f.Pupils++; break;
                    case Status.Student: f.Students++; break;
                    case Status.Worker: f.Workers++; break;
                    case Status.Unemployed: f.Unemployed++; break;
                    case Status.Retired: f.Retired++; break;
                    default: f.Home++; break;
                }
            }
        }

        // The day's games counts in use: the city's, each district's and each class's; the population's start taken from the first.
        private void PublishGames(PopulationState s)
        {
            Array.Copy(_games, s.GameCounts, GameRules.Cells);
            if (s.GameByDistrict.Length != _gamesByDistrict.Length)
            {
                s.GameByDistrict = new int[_gamesByDistrict.Length];
            }
            Array.Copy(_gamesByDistrict, s.GameByDistrict, _gamesByDistrict.Length);
            Array.Copy(_gamesByClass, s.GameByClass, _gamesByClass.Length);
            GameRules.Start(s);
        }

        // The rest of the districts' figures: their flats and free flats, jobs, vacancies and commuters, and the means.
        private static void FiguresRest(PopulationState s)
        {
            var renters = new int[s.Figures.Length];
            for (int h = 0; h < s.Households.Count; h++)
            {
                if (s.Households.Alive(h) && s.Households.Landlord[h] != Landlords.Own && s.Households.District[h] < renters.Length)
                {
                    renters[s.Households.District[h]]++;
                }
            }
            for (int b = 0; b < s.Buildings.Count; b++)
            {
                int d = s.Buildings[b].District;
                if (d < 0 || d >= s.Figures.Length)
                {
                    continue;
                }
                int first = s.FlatFirst[b];
                for (int u = 0; u < s.FlatCount[b]; u++)
                {
                    s.Figures[d].Flats++;
                    s.Figures[d].Vacant += s.Occupant[first + u] == -1 ? 1 : 0;
                }
            }
            EmployerTable et = s.Employers;
            for (int e = 0; e < et.Count; e++)
            {
                int b = et.Building[e];
                if (b < 0)
                {
                    continue;
                }
                int d = s.Buildings[b].District;
                if (d < 0 || d >= s.Figures.Length)
                {
                    continue;
                }
                s.Figures[d].Jobs += et.Slots[e];
                s.Figures[d].Vacancies += et.Vacancies(e);
                s.Figures[d].Commuters += et.Commuters[e];
            }
            for (int d = 0; d < s.Figures.Length; d++)
            {
                DistrictFigures f = s.Figures[d];
                f.MeanIncome = f.Households > 0 ? f.MeanIncome / f.Households : 0;
                f.MeanRent = renters[d] > 0 ? f.MeanRent / renters[d] : 0;
                f.RentIndex = d < s.RentIndex.Length ? s.RentIndex[d] : 1000;
            }
        }

        /// <summary>The city's middle household's income after tax today, cents a day (a sort of the households' incomes, at a month's or a city's start).</summary>
        public static long MedianNow(PopulationState s)
        {
            var all = new List<long>(s.Households.Live);
            for (int h = 0; h < s.Households.Count; h++)
            {
                if (s.Households.Alive(h))
                {
                    SpendRules.Income(s, h, s.Day, 0, out DayIncome inc);
                    all.Add(inc.Disposable);
                }
            }
            if (all.Count == 0)
            {
                return 1;
            }
            all.Sort();
            return Math.Max(1, all[all.Count / 2]);
        }

        // The month turns: every ledger and book rolled, the districts' pull worked out from the month that ended, its counters started again.
        private void MonthRoll(PopulationState s)
        {
            for (int d = 0; d < s.Figures.Length; d++)
            {
                s.Figures[d].WantFlats = d < s.WantFlats.Length ? s.WantFlats[d] : 0;
            }
            TakingsBefore = TakingsLast;
            long takings = 0;
            for (int b = 0; b < s.Businesses.Count; b++)
            {
                takings += s.Businesses.MonthTakings[b];
            }
            TakingsLast = takings;
            if (Pulls.Length != s.Districts.Count * 4)
            {
                Pulls = new int[s.Districts.Count * 4];
            }
            for (int b = 0; b < s.Businesses.Count; b++)
            {
                s.Businesses.LastTakings[b] = s.Businesses.MonthTakings[b];
            }
            HousingRules.Pulls(s, Measure, TakingsLast, TakingsBefore, Pulls);
            for (int h = 0; h < s.Households.Count; h++)
            {
                if (s.Households.Alive(h))
                {
                    SpendRules.Roll(s, h);
                }
            }
            for (int b = 0; b < s.Businesses.Count; b++)
            {
                s.Businesses.MonthTakings[b] = 0;
            }
            EmployerTable et = s.Employers;
            for (int e = 0; e < et.Count; e++)
            {
                et.BillLast[e] = et.Bill[e];
                long output = et.Output[e];
                if (et.Business[e] < 0)
                {
                    // public output at its cost, as national accounts value it
                    output = et.Kind[e] == (byte)EmployerKind.Public ? et.Bill[e] : et.Bill[e] * (1000 + PopulationCatalogue.FirmMarkup) / 1000;
                }
                et.OutputLast[e] = output;
                et.Bill[e] = 0;
                et.Output[e] = 0;
            }
            s.Books.Roll();
            s.BirthsLast = s.Births;
            s.DeathsLast = s.Deaths;
            s.Births = s.Deaths = 0;
            s.Region.MovedInLast = s.Region.MovedIn;
            s.Region.MovedOutLast = s.Region.MovedOut;
            s.Region.MovedIn = s.Region.MovedOut = s.Region.Wanting = 0;
            for (int d = 0; d < s.Figures.Length; d++)
            {
                DistrictFigures f = s.Figures[d];
                f.Births = f.Deaths = f.MovedIn = f.MovedOut = 0;
                f.Spending = f.Takings = f.Taxes = 0;
                f.MigrantsWanting = 0;
            }
            Array.Clear(s.WantFlats, 0, s.WantFlats.Length);
            Array.Clear(_vacancies, 0, _vacancies.Length);
            Array.Clear(_slots, 0, _slots.Length);
            for (int e = 0; e < et.Count; e++)
            {
                if (et.Players[e])
                {
                    continue;   // the player staffs those
                }
                int f = PopulationCatalogue.Clamp(et.Field[e], 0, PopulationCatalogue.Fields - 1);
                _vacancies[f] += et.Vacancies(e);
                _slots[f] += et.Slots[e];
            }
            Array.Clear(_seekers, 0, _seekers.Length);
            Array.Clear(_workers, 0, _workers.Length);
            for (int p = 0; p < s.People.Count; p++)
            {
                if (s.People.Id[p] == 0)
                {
                    continue;
                }
                int f = PopulationCatalogue.Clamp(s.People.Field[p], 0, PopulationCatalogue.Fields - 1);
                _seekers[f] += s.People.Status[p] == (byte)Status.Unemployed ? 1 : 0;
                _workers[f] += s.People.Status[p] == (byte)Status.Worker ? 1 : 0;
            }
            s.MonthNumber = GenerateRules.MonthOf(_day, s.YearDays);
        }

        // Thousandths of open vacancies over jobseekers in the city, a thousand at most: how easily the out of work find work today.
        // Vacancies over the city's jobseekers, thousandths (at most a thousand), and the share of its labour force out of work.
        private static int Tightness(PopulationState s, out int residentOut)
        {
            int vacancies = VacanciesNow(s), seekers = 0, workers = 0;
            for (int p = 0; p < s.People.Count; p++)
            {
                if (s.People.Id[p] != 0)
                {
                    seekers += s.People.Status[p] == (byte)Status.Unemployed ? 1 : 0;
                    workers += s.People.Status[p] == (byte)Status.Worker ? 1 : 0;
                }
            }
            residentOut = (int)(1000L * seekers / Math.Max(1, seekers + workers));
            return seekers <= 0 ? 1000 : (int)Math.Min(1000L, 1000L * vacancies / seekers);
        }

        /// <summary>The city's open vacancies now (the players' left out: the player hires there).</summary>
        public static int VacanciesNow(PopulationState s)
        {
            int v = 0;
            for (int e = 0; e < s.Employers.Count; e++)
            {
                v += s.Employers.Players[e] ? 0 : s.Employers.Vacancies(e);
            }
            return v;
        }
    }
}
