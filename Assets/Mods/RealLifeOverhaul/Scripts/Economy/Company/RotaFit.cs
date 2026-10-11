using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Why somebody cannot be given the least hours a week they ask for.</summary>
    public static class RotaClash
    {
        public const int None = 0;
        public const int Hours = 1;     // the hours they may work here (opening hours less their free days and the hours they do not work, a day no longer than the game allows) are fewer than they ask for
        public const int Shared = 2;    // those who can stand at the same stations ask for more hours between them than the stations hold
        public const int Service = 3;   // the cleaning service cleans the business: there is no cleaning for them to do
    }

    /// <summary>
    /// The two compulsory ends of an hours demand, and shifts that follow the customers. Game-free; works on a rota
    /// the solver has made, in place.
    ///
    /// THE RULE: whoever asks for a number of hours a week (the game's HoursWorkingPerWeek, part-time or full-time)
    /// works no fewer than its least and no more than its most, or does not work in this rota at all and is named.
    /// The most is a limit of the solver already (RotaLadder gives every hours demand the game's high priority, which
    /// never gives way). This class brings everybody up to their least (<see cref="Reach"/>):
    ///   1  an hour at a station they can stand at that is open and empty: a wanted one first, then the busiest;
    ///      next to a shift they have, or as the start of a new one that the next hour then lengthens (a shift of
    ///      one hour is left only when nothing else keeps the rules)
    ///   2  an hour taken over from a colleague who has more than their own least, at an end of that colleague's shift
    /// and never breaks what the solver never breaks: skill, one place at a time, a shift of at most
    /// <see cref="RotaCatalogue.ShiftCap"/> hours, a day of at most <see cref="RotaCatalogue.DayCap"/>, and the
    /// person's own limits (most hours, number of days, free days, hours not worked, kinds of shift).
    ///
    /// SHIFTS FOLLOW THE CUSTOMERS (<see cref="Slide"/>): a shift at a station that serves customers is moved an
    /// hour at a time towards the busier end while the hour it would gain has more customers and fewer people at
    /// that group of stations than the hour it would give up. So within a day a busier hour is never staffed thinner
    /// than a quieter one unless a rule above forbids the move.
    ///
    /// Cost: nothing unless asked; a step is one person, or one pass of moves. Nothing is allocated once grown.
    /// </summary>
    public sealed class RotaFit
    {
        private const int Cells = RotaCatalogue.Cells, Hours = RotaCatalogue.Hours, Days = RotaCatalogue.Days;

        private RotaShop _shop;
        private RotaProblem _p;
        private bool[] _open;
        private short[] _at;
        private int _s, _w, _next;
        private bool _slid, _moved, _mended;
        private int _slideDay, _rounds;
        private int[] _hours = new int[0], _day = new int[0], _least = new int[0], _most = new int[0], _daysMost = new int[0], _frag = new int[0];
        private ulong[] _free = new ulong[0], _not = new ulong[0], _kinds = new ulong[0];
        private short[] _where = new short[0];     // [worker * Cells + cell] the station they are at, plus one; 0: nowhere

        public int Moves;       // hours added, taken over or slid (the self-check)

        /// <summary>The least and the most hours a week somebody asks for; 0 and a very large number where they ask for none.</summary>
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

        /// <summary>Takes a rota and what it was made for. <paramref name="problem"/> holds the worth of the hours (a wanted hour has one) and everybody's demands.</summary>
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
            Moves = 0;
            if (_hours.Length < _w)
            {
                _hours = new int[_w];
                _day = new int[_w * Days];
                _least = new int[_w];
                _most = new int[_w];
                _daysMost = new int[_w];
                _frag = new int[_w];
                _free = new ulong[_w];
                _not = new ulong[_w];
                _kinds = new ulong[_w];
                _where = new short[_w * Cells];
            }
            Array.Clear(_hours, 0, _hours.Length);
            Array.Clear(_day, 0, _day.Length);
            Array.Clear(_where, 0, _where.Length);
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
            }
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

        /// <summary>One step more: one person brought up to their least, then the passes that move shifts towards the customers. True when done.</summary>
        public bool Step()
        {
            if (_next < _w)
            {
                Reach(_next++);
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
            // One station's shifts of one day a step; the week is gone over until a whole round moves nothing.
            _moved |= SlideAt(_slideDay / Math.Max(1, _s), _slideDay % Math.Max(1, _s));
            if (++_slideDay >= Days * _s)
            {
                _slid = !_moved || ++_rounds >= Hours * 8;
                _slideDay = 0;
                _moved = false;
            }
            return _slid;
        }

        // ---------------------------------------------------------------- the rules nobody's rota breaks

        // True if somebody may be given an hour at a station, the station being empty or not (who stands there is the caller's matter).
        private bool May(int w, int cell, int s)
        {
            int d = cell / Hours, h = cell % Hours;
            if (_shop.Cleaned && _shop.GroupKind[_shop.Group[s]] == RotaGroupKind.Cleaning)
            {
                return false;   // (the cleaning service cleans here: nobody is given a cleaning hour)
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

        // True if an hour at a station is next to one this person works there on the same day.
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

        // ---------------------------------------------------------------- the least hours

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
                    // First an empty place; with none, an hour a colleague can spare.
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
                                continue;   // (an hour alone is no shift: the hour that started one is lengthened first, and a second shift on a day is not begun)
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
                    _frag[w] = -1;      // (the hour that began a shift cannot be lengthened after all: it stays, and the search goes on elsewhere)
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
                Moves++;
            }
        }

        // True if a new shift begun at an hour could be lengthened by the hour before or after it.
        private bool Grows(int w, int cell, int s)
        {
            int h = cell % Hours;
            bool grows = false;
            short there = _at[cell * _s + s];     // (a colleague, where the hour would be taken over: put back below)
            Put(w, cell, s);
            grows |= h > 0 && Free(cell - 1, s, there) && May(w, cell - 1, s);
            grows |= h < Hours - 1 && Free(cell + 1, s, there) && May(w, cell + 1, s);
            Take(w, cell, s);
            _at[cell * _s + s] = there;
            return grows;
        }

        // True if an hour at a station is empty, or is the next hour of the colleague an hour is being taken over from and they can spare that too.
        private bool Free(int cell, int s, int from)
        {
            int v = _at[cell * _s + s];
            return v < 0 || (v == from && from < _w && _hours[from] - 2 >= _least[from]);
        }

        // True if a colleague can give up an hour: they stay at or above their own least, it is an end of their shift, and what is left of that shift is no single hour.
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

        /// <summary>
        /// Shifts of a single hour: each is lengthened by the busier hour beside it where the rules allow, and
        /// otherwise given up if its holder stays at or above their least hours without it (or has no other hour).
        /// One is left only where neither can be done.
        /// </summary>
        public void Mend()
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
                        Moves++;
                    }
                    else if (_hours[w] - 1 >= _least[w] || _hours[w] == 1)
                    {
                        Take(w, cell, s);
                        Moves++;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- towards the customers

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

        // One station of one day: each shift at a station that serves customers, later by an hour, earlier by an hour, or an end hour lent to a busier hour.
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
                        continue;   // (not the first hour of a shift)
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
                    h = here ? Hours : end - 1;   // (the next shift may begin in the very next hour; after a move the day is looked at again in the next round)
                }
            }
            return moved;
        }

        // Gives up an end hour of a shift of three hours or more for an hour beside another hour the same person works (on any
        // day) that is busier and thinner staffed: the week's hours go where the customers are, and no shift is left a single hour.
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
            Moves++;
            return true;
        }

        // An empty hour between two shifts of the same person, left empty because one shift may not be that long: the break is
        // moved to the quietest hour of the two shifts that leaves both whole (two hours or more, twelve at most), if that is quieter.
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
            Moves++;
            return true;
        }

        // Gives up one end hour of a shift for the hour beyond its other end, if that hour is busier and thinner staffed and the rules allow.
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
            Moves++;
            return true;
        }

        // ---------------------------------------------------------------- why somebody cannot be given their hours

        /// <summary>
        /// The most hours a week somebody could be given in these opening hours were nobody else there: each day
        /// they may work, the open hours they may work at a station they can stand at, no more than the game's day.
        /// </summary>
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
            // Each day's room, then the days with the most room, as many as they work.
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
                // (kept in the bits of one number, six to a day: a day holds no more than fourteen)
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
