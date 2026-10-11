using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Fills a week's rota. Game-free and over plain arrays: the director copies a business into a
    /// <see cref="RotaProblem"/>, this answers with a <see cref="RotaPlan"/>, and the same problem always gives the
    /// same plan (no chance, no clock: the improving pass counts its moves).
    ///
    /// WHAT IS NEVER BROKEN (the game enforces or punishes each): nobody stands at a station they have no skill for,
    /// nobody is in two places at once, no shift is longer than <see cref="RotaCatalogue.ShiftCap"/> hours, nobody
    /// works more than <see cref="RotaCatalogue.DayCap"/> hours in a day, and a station with the headquarters rule
    /// has one person for the whole week or nobody.
    ///
    /// WHAT IS WANTED, in this order: the most value covered; the most demands met (weighed as the game weighs them
    /// against satisfaction); the lowest wages, with the better hand where wages are equal; few, long shifts.
    ///
    /// HOW
    ///   1  limits     every demand that sets an upper end (most hours, most days, free days, hours not worked, kinds
    ///                 of shift, stations asked for) is a limit; a lower end (least hours, a number of days, a station
    ///                 at least once) is a goal
    ///   2  whole      stations with the headquarters rule get the person who fits every hour of them
    ///   3  goals      as the game's own first pass gives everybody their least hours: whoever asks for a least
    ///                 number of hours or for a number of days is given shifts first, on the most valuable days
    ///   4  shifts     day and station by day and station, the most valuable first: the uncovered run goes to whoever
    ///                 covers the most value of it in one shift, then who is hardest to place elsewhere, then whose
    ///                 demands it helps, then who costs least
    ///   5  improve    an uncovered hour is covered by having somebody give up an hour worth less, or one that
    ///                 another can take over; a shift goes to somebody whose demands it helps, or who is cheaper
    ///   6  relax      if hours are still uncovered, the demands the game calls optional (not of high priority) stop
    ///                 being limits, as in the game's own auto fill, and give way only where they cover an hour;
    ///                 the plan says which gave way and where. Demands of high priority never give way; the least
    ///                 hours of a week (the game's "semi-critical" step) are a goal throughout, reported if missed
    ///   7  again      unless that rota covers everything and meets every demand, a second one is made from a plain
    ///                 start (hour by hour in the order of the week, then steps 5 and 6) and the better is kept
    ///
    /// Nothing is allocated while it runs once its arrays have grown to the largest business seen. The steps that
    /// place shifts are in RotaSolver.Shifts.cs, the improving pass and the rating in RotaSolver.Improve.cs.
    /// </summary>
    public sealed partial class RotaSolver
    {
        private const int Cells = RotaCatalogue.Cells, Hours = RotaCatalogue.Hours, Days = RotaCatalogue.Days;
        private const uint AllHours = 0xFFFFFFu;

        private RotaProblem _p;
        private RotaPlan _plan;
        private int _s, _w, _steps;
        private short[] _at;

        private short[] _pos = new short[0];        // [worker * Cells + cell] the station they stand at, or -1
        private uint[] _busy = new uint[0];         // [worker * 7 + day] the hours they work, a bit each
        private int[] _dayH = new int[0];           // [worker * 7 + day]
        private int[] _weekH = new int[0], _daysOn = new int[0];
        private int[] _kindH = new int[0];          // [worker * 2 + kind]
        private int[] _hoursAt = new int[0];        // [worker * Stations + station]
        private uint[] _need = new uint[0];         // [day * Stations + station] hours wanted and not covered

        // The limits, twice: [worker] with every demand, [Workers + worker] with those of high priority only.
        private int[] _maxHours = new int[0], _maxDays = new int[0], _dayOk = new int[0];
        private uint[] _hourOk = new uint[0];
        private ulong[] _stationOk = new ulong[0];

        // The goals.
        private int[] _minHours = new int[0], _minWeight = new int[0], _wantDays = new int[0], _daysWeight = new int[0], _onWeight = new int[0];
        private ulong[] _onMask = new ulong[0], _canMask = new ulong[0];
        private bool _optional;                     // some demand that is a limit is not of high priority

        private int[] _canStart = new int[0], _canList = new int[0];
        private int[] _lanes = new int[0], _laneValue = new int[0];
        private int[] _order = new int[0];          // the workers with a goal, the heaviest demand first
        private readonly int[] _could = new int[Hours];
        private readonly RotaPlan _other = new RotaPlan();     // the second start's rota
        private readonly int[] _pre = new int[Hours + 1];
        private readonly int[] _rare = new int[Hours + 1];      // like _pre, of how few could take each hour of the day at hand

        /// <summary>The rota for a problem, written into <paramref name="plan"/> (made if null) and rated.</summary>
        public RotaPlan Solve(RotaProblem problem, RotaPlan plan = null)
        {
            // Two starts, and the better rota is kept: shifts by value, and hour by hour in the order of the week.
            plan = Begin(problem, plan);
            Whole();
            Goals();
            Shifts(0, false);
            Finish();
            if (plan.Full && plan.UnmetCount == 0)
            {
                return plan;   // (nothing a second start could do better but wages, and the first start is the one that looks at wages)
            }
            RotaPlan first = plan;
            Begin(problem, _other);
            Whole();
            InOrder();
            Finish();
            if (Better(_other, first))
            {
                Copy(_other, first);
            }
            return first;
        }

        // Steps 5 and 6, and the rating.
        private void Finish()
        {
            _steps = RotaCatalogue.ImproveSteps;
            Cover(0);
            _steps = RotaCatalogue.ImproveSteps;
            Hand(0);
            if (_optional && AnyNeed())
            {
                // The game's own second step: only demands of high priority are still limits.
                bool placed = Shifts(1, true);
                _steps = RotaCatalogue.ImproveSteps;
                placed |= Cover(1);
                _plan.Level = placed ? 1 : 0;
            }
            Rate();
        }

        // The second start: hour by hour through the week, station by station, the hour goes to whoever stands there the hour
        // before, else to the first who may take it.
        private void InOrder()
        {
            for (int cell = 0; cell < Cells; cell++)
            {
                int d = cell / Hours, h = cell % Hours;
                for (int s = 0; s < _s; s++)
                {
                    if (((_need[d * _s + s] >> h) & 1u) == 0 || _p.Whole[s])
                    {
                        continue;
                    }
                    int last = h > 0 ? _at[(cell - 1) * _s + s] : -1;
                    if (last >= 0 && Free(0, last, d, h, s))
                    {
                        Assign(last, cell, s);
                        continue;
                    }
                    for (int i = _canStart[s]; i < _canStart[s + 1]; i++)
                    {
                        if (Free(0, _canList[i], d, h, s))
                        {
                            Assign(_canList[i], cell, s);
                            break;
                        }
                    }
                }
            }
        }

        // The order of what is wanted: value, demands, wages, shifts.
        private static bool Better(RotaPlan a, RotaPlan b)
        {
            if (a.CoveredValue != b.CoveredValue)
            {
                return a.CoveredValue > b.CoveredValue;
            }
            if (a.UnmetWeight != b.UnmetWeight)
            {
                return a.UnmetWeight < b.UnmetWeight;
            }
            return a.Wage != b.Wage ? a.Wage < b.Wage : a.Shifts < b.Shifts;
        }

        private static void Copy(RotaPlan from, RotaPlan to)
        {
            Array.Copy(from.At, to.At, RotaCatalogue.Cells * from.Stations);
            to.Level = from.Level;
            to.WantedValue = from.WantedValue;
            to.CoveredValue = from.CoveredValue;
            to.WantedHours = from.WantedHours;
            to.CoveredHours = from.CoveredHours;
            to.UnmetCount = from.UnmetCount;
            to.UnmetWeight = from.UnmetWeight;
            to.Wage = from.Wage;
            to.Shifts = from.Shifts;
            to.Unmet.Clear();
            to.Unmet.AddRange(from.Unmet);
            to.Gaps.Clear();
            to.Gaps.AddRange(from.Gaps);
        }

        /// <summary>
        /// Rates a rota that was made elsewhere (the one a business has now): what it covers, what it costs, which
        /// demands it leaves unmet. <paramref name="plan"/>.At is read and its figures are written.
        /// </summary>
        public RotaPlan Rate(RotaProblem problem, RotaPlan plan)
        {
            short[] given = plan.At;
            int level = plan.Level;
            _p = problem;
            _s = problem.Stations;
            _w = problem.Workers;
            Room();
            _plan = plan;
            _at = given;
            Limits();
            for (int i = 0; i < Days * _s; i++)
            {
                _need[i] = 0;
            }
            for (int cell = 0; cell < Cells; cell++)
            {
                for (int s = 0; s < _s; s++)
                {
                    int w = given[cell * _s + s];
                    if (w >= 0 && w < _w)
                    {
                        Count(w, cell, s, 1);
                    }
                    else
                    {
                        given[cell * _s + s] = -1;
                        if (problem.Value[cell * _s + s] > 0)
                        {
                            _need[(cell / Hours) * _s + s] |= 1u << (cell % Hours);
                        }
                    }
                }
            }
            plan.Unmet.Clear();
            plan.Gaps.Clear();
            Rate();
            plan.Level = level;
            return plan;
        }

        // ---------------------------------------------------------------- the start

        private RotaPlan Begin(RotaProblem problem, RotaPlan plan)
        {
            _p = problem;
            _s = problem.Stations;
            _w = problem.Workers;
            if (_s > RotaCatalogue.StationsMost || _w > short.MaxValue)
            {
                throw new ArgumentException("a rota of " + _s + " stations and " + _w + " workers is more than the planner keeps");
            }
            plan = plan ?? new RotaPlan();
            plan.Size(_s);
            _plan = plan;
            _at = plan.At;
            Room();
            Limits();
            for (int d = 0; d < Days; d++)
            {
                for (int s = 0; s < _s; s++)
                {
                    uint mask = 0;
                    int sum = 0;
                    for (int h = 0; h < Hours; h++)
                    {
                        int v = problem.Value[(d * Hours + h) * _s + s];
                        if (v > 0)
                        {
                            mask |= 1u << h;
                            sum += v;
                        }
                    }
                    _need[d * _s + s] = mask;
                    _laneValue[d * _s + s] = sum;
                    _lanes[d * _s + s] = d * _s + s;
                }
            }
            SortLanes();
            return plan;
        }

        private void Room()
        {
            _pos = Fit(_pos, _w * Cells);
            for (int i = 0; i < _w * Cells; i++)
            {
                _pos[i] = -1;
            }
            _busy = RotaProblem.Room(_busy, _w * Days);
            _dayH = RotaProblem.Room(_dayH, _w * Days);
            _weekH = RotaProblem.Room(_weekH, _w);
            _daysOn = RotaProblem.Room(_daysOn, _w);
            _kindH = RotaProblem.Room(_kindH, _w * 2);
            _hoursAt = RotaProblem.Room(_hoursAt, _w * _s);
            _need = RotaProblem.Room(_need, Days * _s);
            _maxHours = RotaProblem.Room(_maxHours, _w * 2);
            _maxDays = RotaProblem.Room(_maxDays, _w * 2);
            _dayOk = RotaProblem.Room(_dayOk, _w * 2);
            _hourOk = RotaProblem.Room(_hourOk, _w * 2);
            _stationOk = RotaProblem.Room(_stationOk, _w * 2);
            _minHours = RotaProblem.Room(_minHours, _w);
            _minWeight = RotaProblem.Room(_minWeight, _w);
            _wantDays = RotaProblem.Room(_wantDays, _w);
            _daysWeight = RotaProblem.Room(_daysWeight, _w);
            _onWeight = RotaProblem.Room(_onWeight, _w);
            _onMask = RotaProblem.Room(_onMask, _w);
            _canMask = RotaProblem.Room(_canMask, _w);
            _canStart = RotaProblem.Room(_canStart, _s + 1);
            _canList = RotaProblem.Room(_canList, _w * _s);
            _lanes = RotaProblem.Room(_lanes, Days * _s);
            _order = RotaProblem.Room(_order, _w);
            _laneValue = RotaProblem.Room(_laneValue, Days * _s);
        }

        private static short[] Fit(short[] have, int count)
        {
            return have.Length < count ? new short[count] : have;
        }

        // Each employee's limits and goals from their demands, and who can stand at each station.
        private void Limits()
        {
            ulong every = _s >= 64 ? ulong.MaxValue : (1UL << _s) - 1UL;
            _optional = false;
            for (int w = 0; w < _w; w++)
            {
                ulong can = 0;
                for (int s = 0; s < _s; s++)
                {
                    if (_p.Can[w * _s + s] != 0)
                    {
                        can |= 1UL << s;
                    }
                }
                _canMask[w] = can;
                _onMask[w] = 0;
                for (int level = 0; level < 2; level++)
                {
                    int at = level * _w + w;
                    _maxHours[at] = Days * RotaCatalogue.DayCap;
                    _maxDays[at] = Days;
                    _dayOk[at] = (1 << Days) - 1;
                    _hourOk[at] = AllHours;
                    _stationOk[at] = every;
                }
                RotaDemand[] demands = _p.Worker[w].Demands;
                int count = Math.Min(demands.Length, RotaCatalogue.DemandsMost);
                for (int i = 0; i < count; i++)
                {
                    RotaDemand q = demands[i];
                    int weight = Weight(q.Priority);
                    bool high = q.Priority >= RotaCatalogue.PriorityHigh;
                    bool limit = true;
                    for (int level = high ? 1 : 0; level >= 0; level--)   // (a demand of high priority is a limit at both levels, any other at the first only)
                    {
                        int at = level * _w + w;
                        switch (q.Kind)
                        {
                            case RotaDemandKind.Hours:
                                if (q.B > 0)
                                {
                                    _maxHours[at] = Math.Min(_maxHours[at], q.B);
                                }
                                else
                                {
                                    limit = false;
                                }
                                break;
                            case RotaDemandKind.Days:
                                if (q.A > 0)
                                {
                                    _maxDays[at] = Math.Min(_maxDays[at], q.A);
                                }
                                else
                                {
                                    limit = false;
                                }
                                break;
                            case RotaDemandKind.FreeDays:
                                _dayOk[at] &= ~(int)q.Mask;
                                break;
                            case RotaDemandKind.NoHours:
                                _hourOk[at] &= ~(uint)q.Mask;
                                break;
                            case RotaDemandKind.NoShiftKind:
                                for (int s = 0; s < _s; s++)
                                {
                                    if (((q.Mask >> _p.Kind[s]) & 1UL) != 0)
                                    {
                                        _stationOk[at] &= ~(1UL << s);
                                    }
                                }
                                break;
                            case RotaDemandKind.OnStation:
                                // As the game's own first sorting of people to stations: only the stations asked for. With none of them
                                // here that they could stand at, the demand cannot be met at all and limits nothing.
                                if ((q.Mask & can) != 0)
                                {
                                    _stationOk[at] &= q.Mask;
                                }
                                else
                                {
                                    limit = false;
                                }
                                break;
                        }
                    }
                    _optional |= limit && !high;
                    switch (q.Kind)
                    {
                        case RotaDemandKind.Hours:
                            if (q.A > _minHours[w])
                            {
                                _minHours[w] = q.A;
                                _minWeight[w] = weight;
                            }
                            break;
                        case RotaDemandKind.Days:
                            if (q.A > 0)
                            {
                                _wantDays[w] = q.A;
                                _daysWeight[w] = weight;
                            }
                            break;
                        case RotaDemandKind.OnStation:
                            _onMask[w] |= q.Mask;
                            _onWeight[w] = Math.Max(_onWeight[w], weight);
                            break;
                    }
                }
            }
            int n = 0;
            for (int s = 0; s < _s; s++)
            {
                _canStart[s] = n;
                for (int w = 0; w < _w; w++)
                {
                    if (_p.Can[w * _s + s] != 0)
                    {
                        _canList[n++] = w;
                    }
                }
            }
            _canStart[_s] = n;
        }

        private static int Weight(int priority)
        {
            return RotaCatalogue.DemandWeight[priority < 0 ? 0 : priority > 2 ? 2 : priority];
        }

        // The most valuable day at a station first; equal ones in the order of the week. An insertion sort: a few hundred at most, nearly sorted by nothing.
        private void SortLanes()
        {
            int n = Days * _s;
            for (int i = 1; i < n; i++)
            {
                int lane = _lanes[i], value = _laneValue[lane], j = i - 1;
                while (j >= 0 && (_laneValue[_lanes[j]] < value || (_laneValue[_lanes[j]] == value && _lanes[j] > lane)))
                {
                    _lanes[j + 1] = _lanes[j];
                    j--;
                }
                _lanes[j + 1] = lane;
            }
        }

        // ---------------------------------------------------------------- placing and taking away

        private void Count(int w, int cell, int s, int by)
        {
            int d = cell / Hours, h = cell % Hours, wd = w * Days + d;
            if (by > 0)
            {
                _pos[w * Cells + cell] = (short)s;
                _busy[wd] |= 1u << h;
                if (_dayH[wd]++ == 0)
                {
                    _daysOn[w]++;
                }
            }
            else
            {
                _pos[w * Cells + cell] = -1;
                _busy[wd] &= ~(1u << h);
                if (--_dayH[wd] == 0)
                {
                    _daysOn[w]--;
                }
            }
            _weekH[w] += by;
            _kindH[w * 2 + (_p.Kind[s] == RotaKind.Cleaning ? 1 : 0)] += by;
            _hoursAt[w * _s + s] += by;
        }

        private void Assign(int w, int cell, int s)
        {
            _at[cell * _s + s] = (short)w;
            _need[(cell / Hours) * _s + s] &= ~(1u << (cell % Hours));
            Count(w, cell, s, 1);
        }

        private void Unassign(int cell, int s)
        {
            int w = _at[cell * _s + s];
            _at[cell * _s + s] = -1;
            if (_p.Value[cell * _s + s] > 0)
            {
                _need[(cell / Hours) * _s + s] |= 1u << (cell % Hours);
            }
            Count(w, cell, s, -1);
        }

        // How many hours they stand at this station straight before an hour, and straight from one.
        private int RunBack(int w, int d, int s, int h)
        {
            int at = w * Cells + d * Hours, n = 0;
            for (int k = h - 1; k >= 0 && _pos[at + k] == s; k--)
            {
                n++;
            }
            return n;
        }

        private int RunOn(int w, int d, int s, int h)
        {
            int at = w * Cells + d * Hours, n = 0;
            for (int k = h; k < Hours && _pos[at + k] == s; k++)
            {
                n++;
            }
            return n;
        }

        // Whether somebody may take one more hour at a station, by every limit of the level.
        private bool Free(int level, int w, int d, int h, int s)
        {
            int at = level * _w + w, wd = w * Days + d;
            return _p.Can[w * _s + s] != 0 && _at[(d * Hours + h) * _s + s] < 0
                   && ((_stationOk[at] >> s) & 1UL) != 0 && ((_dayOk[at] >> d) & 1) != 0 && ((_hourOk[at] >> h) & 1u) != 0 && ((_busy[wd] >> h) & 1u) == 0
                   && _dayH[wd] < RotaCatalogue.DayCap && _weekH[w] < _maxHours[at] && (_dayH[wd] > 0 || _daysOn[w] < _maxDays[at])
                   && RunBack(w, d, s, h) + RunOn(w, d, s, h + 1) < RotaCatalogue.ShiftCap;
        }

        // Whether somebody may take a whole shift, hours x to y of a day at a station.
        private bool CanTake(int level, int w, int d, int s, int x, int y)
        {
            int at = level * _w + w, wd = w * Days + d, len = y - x;
            uint window = ((1u << len) - 1u) << x;
            return _p.Can[w * _s + s] != 0 && ((_stationOk[at] >> s) & 1UL) != 0 && ((_dayOk[at] >> d) & 1) != 0
                   && (_hourOk[at] & window) == window && (_busy[wd] & window) == 0
                   && _dayH[wd] + len <= RotaCatalogue.DayCap && _weekH[w] + len <= _maxHours[at] && (_dayH[wd] > 0 || _daysOn[w] < _maxDays[at])
                   && RunBack(w, d, s, x) + RunOn(w, d, s, y) + len <= RotaCatalogue.ShiftCap;
        }

        // ---------------------------------------------------------------- demands

        private bool Met(int w, ref RotaDemand q)
        {
            switch (q.Kind)
            {
                case RotaDemandKind.Hours:
                    return _weekH[w] >= q.A && (q.B <= 0 || _weekH[w] <= q.B);
                case RotaDemandKind.Days:
                    return q.A <= 0 || _daysOn[w] == q.A;
                case RotaDemandKind.FreeDays:
                    for (int d = 0; d < Days; d++)
                    {
                        if (((q.Mask >> d) & 1UL) != 0 && _dayH[w * Days + d] > 0)
                        {
                            return false;
                        }
                    }
                    return true;
                case RotaDemandKind.NoHours:
                    for (int d = 0; d < Days; d++)
                    {
                        if ((_busy[w * Days + d] & (uint)q.Mask) != 0)
                        {
                            return false;
                        }
                    }
                    return true;
                case RotaDemandKind.NoShiftKind:
                    return !(((q.Mask & 1UL) != 0 && _kindH[w * 2] > 0) || ((q.Mask & 2UL) != 0 && _kindH[w * 2 + 1] > 0));
                default:
                    for (int s = 0; s < _s; s++)
                    {
                        if (((q.Mask >> s) & 1UL) != 0 && _hoursAt[w * _s + s] > 0)
                        {
                            return true;
                        }
                    }
                    return false;
            }
        }

        // What somebody's unmet demands weigh, as the game weighs them against satisfaction.
        private int Unmet(int w)
        {
            RotaDemand[] demands = _p.Worker[w].Demands;
            int sum = 0, count = Math.Min(demands.Length, RotaCatalogue.DemandsMost);
            for (int i = 0; i < count; i++)
            {
                if (!Met(w, ref demands[i]))
                {
                    sum += Weight(demands[i].Priority);
                }
            }
            return sum;
        }

        // How much a shift of so many hours helps somebody's goals: hours towards their least, a day towards their number of days, a station they ask for.
        private int Gain(int w, int d, int s, int len)
        {
            int gain = 0, lacking = _minHours[w] - _weekH[w];
            if (lacking > 0)
            {
                gain += _minWeight[w] * Math.Min(len, lacking) * 4;
            }
            if (_wantDays[w] > 0 && _dayH[w * Days + d] == 0)
            {
                gain += _daysOn[w] < _wantDays[w] ? _daysWeight[w] * 8 : -_daysWeight[w] * 8;
            }
            if (_onWeight[w] > 0 && ((_onMask[w] >> s) & 1UL) != 0)
            {
                bool has = false;
                for (int k = 0; k < _s && !has; k++)
                {
                    has = ((_onMask[w] >> k) & 1UL) != 0 && _hoursAt[w * _s + k] > 0;
                }
                gain += has ? 0 : _onWeight[w] * 8;
            }
            return gain;
        }
    }
}
