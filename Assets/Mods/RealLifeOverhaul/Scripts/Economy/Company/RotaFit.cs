using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Why somebody cannot be given the least hours a week they ask for.</summary>
    public static class RotaClash
    {
        public const int None = 0;
        public const int Hours = 1;     // too few hours they may work here: open, on days and at hours they allow, a day within the game's
        public const int Shared = 2;    // those who can stand at the same stations ask for more hours between them than the stations hold
        public const int Service = 3;   // the cleaning service cleans the business: there is no cleaning for them to do
    }

    /// <summary>
    /// Keeps the hours demand on a rota the solver made, in place: whoever asks for hours is brought up to their least,
    /// the better at the job first (the most is a limit of the solver already); then one-hour shifts are mended and
    /// shifts slide towards the customers. Breaks nothing the solver keeps. Game-free; a step is one person or one pass.
    /// </summary>
    public sealed class RotaFit
    {
        private const int Cells = RotaCatalogue.Cells, Hours = RotaCatalogue.Hours, Days = RotaCatalogue.Days;

        private RotaShop _shop;
        private RotaProblem _p;
        private bool[] _open;
        private short[] _at;
        private int _s, _w, _next, _rescueAt, _given;
        private bool _slid, _moved, _mended;
        private int _slideDay, _rounds;
        private int[] _hours = new int[0], _day = new int[0], _least = new int[0], _most = new int[0], _daysMost = new int[0], _frag = new int[0];
        private int[] _order = new int[0];         // everybody, the better at the job first (the earlier of two as good)
        private ulong[] _free = new ulong[0], _not = new ulong[0], _kinds = new ulong[0];
        private short[] _before = new short[0];    // the rota before a last try, to put back if it comes to nothing
        private short[] _where = new short[0];     // [worker * Cells + cell] the station they are at, plus one; 0: nowhere

        /// <summary>People the last <see cref="Rescue"/> or run of <see cref="RescueStep"/> gave a week.</summary>
        public int Given => _given;

        public void RescueBegin()
        {
            _rescueAt = _given = 0;
        }

        /// <summary>The least and the most hours a week somebody asks for; 0 and int.MaxValue where they ask for none.</summary>
        public static void Range(RotaWorker worker, out int least, out int most)
        {
            least = 0;
            most = int.MaxValue;
            for (int i = 0; i < worker.Demands.Length; i++)
            {
                RotaDemand d = worker.Demands[i];
                if (d.Kind == RotaDemandKind.Hours)
                {
                    least = Math.Max(least, d.A);
                    most = d.B > 0 ? Math.Min(most, d.B) : most;
                }
            }
        }

        /// <summary>True if hours a week are inside what somebody asks for, or they do not work at all.</summary>
        public static bool Inside(RotaWorker worker, int hours)
        {
            int least, most;
            Range(worker, out least, out most);
            return hours == 0 || (hours >= least && hours <= most);
        }

        public int HoursOf(int worker)
        {
            return _hours[worker];
        }

        /// <summary>The game's skill, a percentage, at the station somebody is best at; 0 for somebody who can stand nowhere.</summary>
        public static int Skill(RotaProblem problem, int worker)
        {
            int skill = 0;
            for (int s = 0; s < problem.Stations; s++)
            {
                skill = Math.Max(skill, problem.Can[worker * problem.Stations + s]);
            }
            return skill;
        }

        /// <summary>Takes a rota and what it was made for: <paramref name="problem"/> holds the worth of the hours and everybody's demands.</summary>
        public void Begin(RotaShop shop, RotaProblem problem, bool[] open, RotaPlan plan)
        {
            _shop = shop;
            _p = problem;
            _open = open;
            _at = plan.At;
            _s = problem.Stations;
            _w = problem.Workers;
            _next = 0;
            _slid = _moved = _mended = false;
            _slideDay = _rounds = 0;
            if (_hours.Length < _w)
            {
                _hours = new int[_w];
                _day = new int[_w * Days];
                _least = new int[_w];
                _most = new int[_w];
                _daysMost = new int[_w];
                _frag = new int[_w];
                _order = new int[_w];
                _free = new ulong[_w];
                _not = new ulong[_w];
                _kinds = new ulong[_w];
                _where = new short[_w * Cells];
            }
            for (int w = 0; w < _w; w++)
            {
                Range(problem.Worker[w], out _least[w], out _most[w]);
                _daysMost[w] = Days;
                _free[w] = _not[w] = _kinds[w] = 0;
                _frag[w] = -1;
                RotaDemand[] demands = problem.Worker[w].Demands;
                for (int i = 0; i < demands.Length; i++)
                {
                    RotaDemand d = demands[i];
                    _daysMost[w] = d.Kind == RotaDemandKind.Days ? Math.Min(_daysMost[w], d.A) : _daysMost[w];
                    _free[w] |= d.Kind == RotaDemandKind.FreeDays ? d.Mask : 0UL;
                    _not[w] |= d.Kind == RotaDemandKind.NoHours ? d.Mask : 0UL;
                    _kinds[w] |= d.Kind == RotaDemandKind.NoShiftKind ? d.Mask : 0UL;
                }
                // _hours holds the skill for the sort until Count fills it with the hours.
                int skill = _hours[w] = Skill(problem, w), j = w;
                while (j > 0 && _hours[_order[j - 1]] < skill)
                {
                    _order[j] = _order[j - 1];
                    j--;
                }
                _order[j] = w;
            }
            Count();
        }

        private void Count()
        {
            Array.Clear(_hours, 0, _hours.Length);
            Array.Clear(_day, 0, _day.Length);
            Array.Clear(_where, 0, _where.Length);
            for (int cell = 0; cell < Cells; cell++)
            {
                for (int s = 0; s < _s; s++)
                {
                    int w = _at[cell * _s + s];
                    if (w >= 0 && w < _w)
                    {
                        _hours[w]++;
                        _day[w * Days + cell / Hours]++;
                        _where[w * Cells + cell] = (short)(s + 1);
                    }
                }
            }
        }

        /// <summary>One step more: one person brought up to their least, then the mending, then the passes towards the customers. True when done.</summary>
        public bool Step()
        {
            if (_next < _w)
            {
                Reach(_order[_next++]);
                return false;
            }
            if (!_mended)
            {
                _mended = true;
                Mend();
                return false;
            }
            if (_slid || !_shop.HasCustomers)
            {
                return true;
            }
            // One station of one day a step; the week is gone over until a whole round moves nothing.
            _moved |= SlideAt(_slideDay / Math.Max(1, _s), _slideDay % Math.Max(1, _s));
            if (++_slideDay >= Days * _s)
            {
                _slid = !_moved || ++_rounds >= Hours * 8;
                _slideDay = 0;
                _moved = false;
            }
            return _slid;
        }

        // True if somebody may be given an hour at a station by every rule; who stands there now is the caller's matter.
        private bool May(int w, int cell, int s)
        {
            int d = cell / Hours, h = cell % Hours;
            if (_shop.Cleaned && _shop.GroupKind[_shop.Group[s]] == RotaGroupKind.Cleaning)
            {
                return false;   // the cleaning service cleans here
            }
            if (!_open[cell] || _p.Can[w * _s + s] == 0 || _p.Whole[s] || _shop.Taken[cell * _s + s] || _where[w * Cells + cell] != 0 || _hours[w] >= _most[w])
            {
                return false;
            }
            if (((_free[w] >> d) & 1UL) != 0 || ((_not[w] >> h) & 1UL) != 0 || ((_kinds[w] >> _p.Kind[s]) & 1UL) != 0 || _day[w * Days + d] >= RotaCatalogue.DayCap)
            {
                return false;
            }
            if (_day[w * Days + d] == 0)
            {
                int days = 0;
                for (int k = 0; k < Days; k++)
                {
                    days += _day[w * Days + k] > 0 ? 1 : 0;
                }
                if (days >= _daysMost[w])
                {
                    return false;
                }
            }
            int run = 1;
            for (int k = h - 1; k >= 0 && _at[(d * Hours + k) * _s + s] == w; k--)
            {
                run++;
            }
            for (int k = h + 1; k < Hours && _at[(d * Hours + k) * _s + s] == w; k++)
            {
                run++;
            }
            return run <= RotaCatalogue.ShiftCap;
        }

        private bool Mine(int w, int cell, int s)
        {
            return cell >= 0 && cell < Cells && _at[cell * _s + s] == w;
        }

        private bool Beside(int w, int cell, int s)
        {
            int h = cell % Hours;
            return (h > 0 && Mine(w, cell - 1, s)) || (h < Hours - 1 && Mine(w, cell + 1, s));
        }

        private void Put(int w, int cell, int s)
        {
            _at[cell * _s + s] = (short)w;
            _where[w * Cells + cell] = (short)(s + 1);
            _hours[w]++;
            _day[w * Days + cell / Hours]++;
        }

        private void Take(int w, int cell, int s)
        {
            _at[cell * _s + s] = -1;
            _where[w * Cells + cell] = 0;
            _hours[w]--;
            _day[w * Days + cell / Hours]--;
        }

        /// <summary>Brings one person up to the least hours they ask for, as far as the rules allow.</summary>
        public void Reach(int w)
        {
            while (_hours[w] < _least[w])
            {
                int need = _least[w] - _hours[w], best = -1, bestStation = -1;
                long most = long.MinValue;
                bool takes = false;
                for (int pass = 0; pass < 2 && best < 0; pass++)
                {
                    // An empty place first (a wanted hour, then the busiest); with none, an hour a colleague can spare.
                    for (int cell = 0; cell < Cells; cell++)
                    {
                        for (int s = 0; s < _s; s++)
                        {
                            int v = _at[cell * _s + s];
                            if ((pass == 0) != (v < 0) || !May(w, cell, s) || (pass == 1 && !Spares(v, cell, s)))
                            {
                                continue;
                            }
                            bool beside = Beside(w, cell, s);
                            if (_frag[w] >= 0 ? !(beside && (cell == _frag[w] - 1 || cell == _frag[w] + 1)) : !beside && (_day[w * Days + cell / Hours] > 0 || (need >= 2 && !Grows(w, cell, s))))
                            {
                                continue;   // an hour alone is no shift: the hour that began one is lengthened first, and no second shift is begun on a day
                            }
                            long score = (_p.Value[cell * _s + s] > 0 ? 1L << 40 : 0L) + ((long)_shop.Customers[cell] << 16) + (beside ? 1L << 8 : 0L) - cell;
                            if (score > most)
                            {
                                most = score;
                                best = cell;
                                bestStation = s;
                                takes = pass == 1;
                            }
                        }
                    }
                }
                if (best < 0 && _frag[w] >= 0)
                {
                    _frag[w] = -1;      // the hour that began a shift cannot be lengthened after all: it stays, and the search goes on elsewhere
                    continue;
                }
                if (best < 0)
                {
                    return;
                }
                bool starts = !Beside(w, best, bestStation);
                if (takes)
                {
                    Take(_at[best * _s + bestStation], best, bestStation);
                }
                Put(w, best, bestStation);
                _frag[w] = starts && need >= 2 ? best : -1;
            }
        }

        public void Drop(int w)
        {
            for (int cell = 0; cell < Cells && _hours[w] > 0; cell++)
            {
                int s = _where[w * Cells + cell] - 1;
                if (s >= 0)
                {
                    Take(w, cell, s);
                }
            }
            _frag[w] = -1;
        }

        /// <summary>
        /// Tries once more each of the first <paramref name="people"/> who has no hours and asks for some, the better first;
        /// a try that falls short of their least is undone. Returns how many were given a week.
        /// </summary>
        public int Rescue(int people)
        {
            RescueBegin();
            while (!RescueStep(people))
            {
            }
            return _given;
        }

        /// <summary>The same a person at a time, after <see cref="RescueBegin"/>. True when everybody has been looked at.</summary>
        public bool RescueStep(int people)
        {
            int given = 0;
            for (; _rescueAt < _w && given == 0; _rescueAt++)
            {
                int w = _order[_rescueAt];
                if (w >= people || _hours[w] > 0 || _least[w] <= 0)
                {
                    continue;
                }
                if (_before.Length < Cells * _s)
                {
                    _before = new short[Cells * _s];
                }
                Array.Copy(_at, _before, Cells * _s);
                _frag[w] = -1;
                Reach(w);
                if (_hours[w] < _least[w])
                {
                    // The hours colleagues gave up for nothing go back to them.
                    Array.Copy(_before, _at, Cells * _s);
                    Count();
                    _frag[w] = -1;
                    _rescueAt++;
                    break;      // a try that came to nothing is a step of its own
                }
                given++;
            }
            _given += given;
            if (_rescueAt >= _w && _given > 0)
            {
                _mended = _slid = _moved = false;
                _slideDay = _rounds = 0;
            }
            return _rescueAt >= _w;
        }

        // True if a new shift begun at an hour could be lengthened by the hour before or after it.
        private bool Grows(int w, int cell, int s)
        {
            int h = cell % Hours;
            bool grows = false;
            short there = _at[cell * _s + s];     // a colleague, where the hour would be taken over: put back below
            Put(w, cell, s);
            grows |= h > 0 && Free(cell - 1, s, there) && May(w, cell - 1, s);
            grows |= h < Hours - 1 && Free(cell + 1, s, there) && May(w, cell + 1, s);
            Take(w, cell, s);
            _at[cell * _s + s] = there;
            return grows;
        }

        // True if an hour at a station is empty, or the colleague an hour is being taken from holds it and can spare it too.
        private bool Free(int cell, int s, int from)
        {
            int v = _at[cell * _s + s];
            return v < 0 || (v == from && from < _w && _hours[from] - 2 >= _least[from]);
        }

        // True if a colleague can give up an hour: they keep their least, it ends their shift, and no single hour is left.
        private bool Spares(int v, int cell, int s)
        {
            if (v < 0 || v >= _w || _hours[v] - 1 < _least[v] || _p.Whole[s])
            {
                return false;
            }
            int h = cell % Hours, before = 0, after = 0;
            for (int k = h - 1; k >= 0 && Mine(v, cell - (h - k), s); k--)
            {
                before++;
            }
            for (int k = h + 1; k < Hours && Mine(v, cell + (k - h), s); k++)
            {
                after++;
            }
            return (before == 0 || after == 0) && before + after != 1;
        }

        // One-hour shifts: each is lengthened by the busier hour beside it where allowed, else given up if its holder keeps
        // their least without it or has no other hour.
        private void Mend()
        {
            for (int cell = 0; cell < Cells; cell++)
            {
                int h = cell % Hours;
                for (int s = 0; s < _s; s++)
                {
                    int w = _at[cell * _s + s];
                    if (w < 0 || w >= _w || _p.Whole[s] || Beside(w, cell, s))
                    {
                        continue;
                    }
                    int before = h > 0 && _at[(cell - 1) * _s + s] < 0 && May(w, cell - 1, s) ? cell - 1 : -1;
                    int after = h < Hours - 1 && _at[(cell + 1) * _s + s] < 0 && May(w, cell + 1, s) ? cell + 1 : -1;
                    int to = before >= 0 && (after < 0 || _shop.Customers[before] > _shop.Customers[after]) ? before : after;
                    if (to >= 0)
                    {
                        Put(w, to, s);
                    }
                    else if (_hours[w] - 1 >= _least[w] || _hours[w] == 1)
                    {
                        Take(w, cell, s);
                    }
                }
            }
        }

        // The people at a group of stations in an hour.
        private int Level(int cell, int group)
        {
            int n = 0;
            for (int s = 0; s < _s; s++)
            {
                n += _shop.Group[s] == group && (_at[cell * _s + s] >= 0 || _shop.Taken[cell * _s + s]) ? 1 : 0;
            }
            return n;
        }

        /// <summary>Moves shifts an hour at a time towards the busier end, until no move is left.</summary>
        public void Slide()
        {
            bool moved = _shop.HasCustomers;
            for (int round = 0; moved && round < Hours * 8; round++)
            {
                moved = false;
                for (int i = 0; i < Days * _s; i++)
                {
                    moved |= SlideAt(i / _s, i % _s);
                }
            }
        }

        // One day at a station that serves customers: each shift an hour later, earlier, or an end hour lent to a busier hour.
        private bool SlideAt(int d, int s)
        {
            bool moved = false;
            if (_shop.GroupKind[_shop.Group[s]] == RotaGroupKind.Service && !_p.Whole[s])
            {
                for (int h = 1; h < Hours - 1; h++)
                {
                    moved |= Bridge(d * Hours + h, s);
                }
                for (int h = 0; h < Hours; h++)
                {
                    int cell = d * Hours + h, w = _at[cell * _s + s];
                    if (w < 0 || w >= _w || (h > 0 && _at[(cell - 1) * _s + s] == w))
                    {
                        continue;   // not the first hour of a shift
                    }
                    int end = h;
                    while (end < Hours && _at[(d * Hours + end) * _s + s] == w)
                    {
                        end++;
                    }
                    bool here = end < Hours && Shift(w, s, cell, d * Hours + end);
                    here = here || (h > 0 && Shift(w, s, d * Hours + end - 1, cell - 1));
                    here = here || (end - h >= 3 && (Lend(w, s, cell) || Lend(w, s, d * Hours + end - 1)));
                    moved |= here;
                    h = here ? Hours : end - 1;   // the next shift may begin in the very next hour; after a move the next round looks again
                }
            }
            return moved;
        }

        // An end hour of a shift of three or more moves to a busier, thinner staffed hour beside another of theirs, any day.
        private bool Lend(int w, int s, int from)
        {
            int group = _shop.Group[s], level = Level(from, group), best = -1, bestStation = -1;
            Take(w, from, s);
            for (int to = 0; to < Cells; to++)
            {
                if (to == from || !_open[to] || _shop.Customers[to] <= _shop.Customers[from] || (best >= 0 && _shop.Customers[to] <= _shop.Customers[best]) || Level(to, group) >= level)
                {
                    continue;
                }
                for (int t = 0; t < _s; t++)
                {
                    if (_shop.Group[t] == group && _at[to * _s + t] < 0 && Beside(w, to, t) && May(w, to, t))
                    {
                        best = to;
                        bestStation = t;
                        break;
                    }
                }
            }
            if (best < 0)
            {
                Put(w, from, s);
                return false;
            }
            Put(w, best, bestStation);
            return true;
        }

        // An hour left empty between two shifts of one person (one shift may not be so long): the break moves to the
        // quietest hour that leaves both shifts two to twelve hours long.
        private bool Bridge(int to, int s)
        {
            int w = _at[(to - 1) * _s + s];
            if (!_open[to] || _at[to * _s + s] >= 0 || w < 0 || w >= _w || _at[(to + 1) * _s + s] != w || _shop.Taken[to * _s + s])
            {
                return false;
            }
            int d = to / Hours, from = to - 1, end = to + 1, best = -1;
            while (from > d * Hours && _at[(from - 1) * _s + s] == w)
            {
                from--;
            }
            while (end < (d + 1) * Hours && _at[end * _s + s] == w)
            {
                end++;
            }
            for (int g = from; g < end; g++)
            {
                int left = g - from, right = end - g - 1;
                bool whole = (left == 0 || (left >= 2 && left <= RotaCatalogue.ShiftCap)) && (right == 0 || (right >= 2 && right <= RotaCatalogue.ShiftCap));
                if (g != to && whole && _shop.Customers[g] < _shop.Customers[to] && (best < 0 || _shop.Customers[g] < _shop.Customers[best]))
                {
                    best = g;
                }
            }
            if (best < 0)
            {
                return false;
            }
            Take(w, best, s);
            if (!May(w, to, s))
            {
                Put(w, best, s);
                return false;
            }
            Put(w, to, s);
            return true;
        }

        // Gives up one end hour of a shift for the hour beyond its other end, if that is busier and thinner staffed and allowed.
        private bool Shift(int w, int s, int from, int to)
        {
            int group = _shop.Group[s];
            if (!_open[to] || _at[to * _s + s] >= 0 || _shop.Customers[to] <= _shop.Customers[from] || Level(to, group) >= Level(from, group))
            {
                return false;
            }
            Take(w, from, s);
            if (!May(w, to, s))
            {
                Put(w, from, s);
                return false;
            }
            Put(w, to, s);
            return true;
        }

        /// <summary>The most hours a week somebody could be given in these opening hours were nobody else there.</summary>
        public static int Room(RotaShop shop, RotaProblem problem, bool[] open, int worker)
        {
            int stations = problem.Stations, least, most, daysMost = Days, sum = 0, used = 0;
            ulong free = 0, not = 0, kinds = 0;
            long rooms = 0;
            RotaWorker who = problem.Worker[worker];
            Range(who, out least, out most);
            for (int i = 0; i < who.Demands.Length; i++)
            {
                RotaDemand d = who.Demands[i];
                daysMost = d.Kind == RotaDemandKind.Days ? Math.Min(daysMost, d.A) : daysMost;
                free |= d.Kind == RotaDemandKind.FreeDays ? d.Mask : 0UL;
                not |= d.Kind == RotaDemandKind.NoHours ? d.Mask : 0UL;
                kinds |= d.Kind == RotaDemandKind.NoShiftKind ? d.Mask : 0UL;
            }
            // Each day's room, six bits a day (a day holds no more than fourteen), then the days with the most, as many as they work.
            for (int d = 0; d < Days; d++)
            {
                int room = 0;
                for (int h = 0; h < Hours && ((free >> d) & 1UL) == 0; h++)
                {
                    bool can = false;
                    for (int s = 0; s < stations && !can && open[d * Hours + h] && ((not >> h) & 1UL) == 0; s++)
                    {
                        can = !(shop.Cleaned && shop.GroupKind[shop.Group[s]] == RotaGroupKind.Cleaning) && problem.Can[worker * stations + s] != 0 && !problem.Whole[s]
                              && !shop.Taken[(d * Hours + h) * stations + s] && ((kinds >> problem.Kind[s]) & 1UL) == 0;
                    }
                    room += can ? 1 : 0;
                }
                room = Math.Min(room, RotaCatalogue.DayCap);
                rooms |= (long)room << (d * 6);
            }
            for (int taken = 0; taken < daysMost; taken++)
            {
                int best = -1, bestRoom = 0;
                for (int d = 0; d < Days; d++)
                {
                    int room = (int)((rooms >> (d * 6)) & 63);
                    if (((used >> d) & 1) == 0 && room > bestRoom)
                    {
                        bestRoom = room;
                        best = d;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                used |= 1 << best;
                sum += bestRoom;
            }
            return Math.Min(sum, most);
        }
    }
}
