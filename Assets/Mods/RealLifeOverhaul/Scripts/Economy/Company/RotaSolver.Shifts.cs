using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The solver's placing steps: the stations that take one person for the whole week, the shifts given first to
    /// whoever asks for a least number of hours or a number of days, and then the shifts of every day at every station,
    /// the most valuable first. The last is used a second time when optional demands give way (see RotaSolver.cs).
    /// </summary>
    public sealed partial class RotaSolver
    {
        // ---------------------------------------------------------------- 2: stations with one person for the whole week

        private void Whole()
        {
            for (int s = 0; s < _s; s++)
            {
                if (!_p.Whole[s])
                {
                    continue;
                }
                int total = 0;
                for (int d = 0; d < Days; d++)
                {
                    total += Bits(_need[d * _s + s]);
                }
                if (total == 0)
                {
                    continue;
                }
                int best = -1, bestGain = 0;
                for (int i = _canStart[s]; i < _canStart[s + 1]; i++)
                {
                    int w = _canList[i];
                    if (!FitsWhole(w, s, total))
                    {
                        continue;
                    }
                    int gain = Gain(w, 0, s, total);
                    if (best < 0 || gain > bestGain || (gain == bestGain && Cheaper(w, best, s)))
                    {
                        best = w;
                        bestGain = gain;
                    }
                }
                for (int d = 0; best >= 0 && d < Days; d++)
                {
                    uint mask = _need[d * _s + s];
                    for (int h = 0; h < Hours; h++)
                    {
                        if (((mask >> h) & 1u) != 0)
                        {
                            Assign(best, d * Hours + h, s);
                        }
                    }
                }
            }
        }

        private bool FitsWhole(int w, int s, int total)
        {
            if (((_stationOk[w] >> s) & 1UL) == 0 || _weekH[w] + total > _maxHours[w])
            {
                return false;
            }
            int days = _daysOn[w];
            for (int d = 0; d < Days; d++)
            {
                uint mask = _need[d * _s + s];
                if (mask == 0)
                {
                    continue;
                }
                int wd = w * Days + d;
                if (((_dayOk[w] >> d) & 1) == 0 || (_hourOk[w] & mask) != mask || (_busy[wd] & mask) != 0 || _dayH[wd] + Bits(mask) > RotaCatalogue.DayCap || LongestRun(mask) > RotaCatalogue.ShiftCap)
                {
                    return false;
                }
                days += _dayH[wd] == 0 ? 1 : 0;
            }
            return days <= _maxDays[w];
        }

        private bool Cheaper(int w, int than, int s)
        {
            int a = _p.Worker[w].Wage, b = _p.Worker[than].Wage;
            return a < b || (a == b && _p.Can[w * _s + s] > _p.Can[than * _s + s]);
        }

        private static int Bits(uint mask)
        {
            int n = 0;
            for (; mask != 0; mask &= mask - 1)
            {
                n++;
            }
            return n;
        }

        private static int LongestRun(uint mask)
        {
            int best = 0, run = 0;
            for (int h = 0; h < Hours; h++)
            {
                run = ((mask >> h) & 1u) != 0 ? run + 1 : 0;
                best = run > best ? run : best;
            }
            return best;
        }

        // ---------------------------------------------------------------- 3: goals first

        // Whoever wants a least number of hours or a number of days gets shifts first, the heaviest demand first: this puts
        // them in that order and says how many they are.
        private int GoalsOrder()
        {
            int n = 0;
            for (int w = 0; w < _w; w++)
            {
                if (_minHours[w] <= 0 && _wantDays[w] <= 0)
                {
                    continue;
                }
                int weight = Math.Max(_minHours[w] > 0 ? _minWeight[w] : 0, _wantDays[w] > 0 ? _daysWeight[w] : 0), j = n++;
                while (j > 0 && Math.Max(_minHours[_order[j - 1]] > 0 ? _minWeight[_order[j - 1]] : 0, _wantDays[_order[j - 1]] > 0 ? _daysWeight[_order[j - 1]] : 0) < weight)
                {
                    _order[j] = _order[j - 1];
                    j--;
                }
                _order[j] = w;
            }
            Array.Clear(_rare, 0, _rare.Length);
            return n;
        }

        // One of them: the most valuable days at the stations they can stand at, until their least is reached.
        private void GoalsFor(int w)
        {
            for (int i = 0; i < Days * _s; i++)
            {
                bool hours = _weekH[w] < _minHours[w], days = _wantDays[w] > 0 && _daysOn[w] < _wantDays[w];
                if (!hours && !days)
                {
                    break;
                }
                int lane = _lanes[i], s = lane % _s, d = lane / _s, x, y;
                if (_p.Can[w * _s + s] == 0 || _need[lane] == 0 || _p.Whole[s])
                {
                    continue;
                }
                bool newDay = _dayH[w * Days + d] == 0;
                if (newDay ? (_wantDays[w] > 0 && !days) : !hours)
                {
                    continue;   // (their days are made up: hours only on days they work already; their hours are made up: only a day that is still wanted)
                }
                _pre[0] = 0;
                for (int h = 0; h < Hours; h++)
                {
                    _pre[h + 1] = _pre[h] + Math.Max(0, _p.Value[(d * Hours + h) * _s + s]);
                }
                if (Window(0, w, d, s, _need[lane], out x, out y))
                {
                    for (int h = x; h < y; h++)
                    {
                        Assign(w, d * Hours + h, s);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- 4 and 6: shifts

        // One day at one station, the i-th most valuable: its uncovered runs go out in shifts. True if anything was placed.
        private bool ShiftsLane(int level, bool relaxed, int i)
        {
            int lane = _lanes[i], s = lane % _s;
            return _need[lane] != 0 && !_p.Whole[s] && Lane(level, lane / _s, s, relaxed);
        }

        private bool Lane(int level, int d, int s, bool relaxed)
        {
            int lane = d * _s + s;
            _pre[0] = 0;
            for (int h = 0; h < Hours; h++)
            {
                _pre[h + 1] = _pre[h] + Math.Max(0, _p.Value[(d * Hours + h) * _s + s]);
            }
            bool any = false;
            while (_need[lane] != 0)
            {
                // How few could take each hour: an hour only one person can take is theirs before it is anybody's choice.
                Array.Clear(_could, 0, Hours);
                for (int i = _canStart[s]; i < _canStart[s + 1]; i++)
                {
                    uint open = Open(level, _canList[i], d, s, _need[lane]);
                    for (int h = 0; open != 0; h++, open >>= 1)
                    {
                        _could[h] += (int)(open & 1u);
                    }
                }
                _rare[0] = 0;
                for (int h = 0; h < Hours; h++)
                {
                    _rare[h + 1] = _rare[h] + (_could[h] > 0 ? 60 / Math.Min(_could[h], 6) : 0);
                }
                int best = -1, bx = 0, by = 0, bestValue = 0, bestRare = 0, bestGain = 0, bestCost = 0;
                for (int i = _canStart[s]; i < _canStart[s + 1]; i++)
                {
                    int w = _canList[i], x, y;
                    if (!Window(level, w, d, s, _need[lane], out x, out y))
                    {
                        continue;
                    }
                    int value = _pre[y] - _pre[x], rare = _rare[y] - _rare[x], cost = _p.Worker[w].Wage * (y - x);
                    int gain = relaxed ? -Broken(w, d, s, x, y) : Gain(w, d, s, y - x);
                    if (relaxed)
                    {
                        rare = 0;   // (who minds least comes first once demands give way)
                    }
                    if (best < 0 || value > bestValue || (value == bestValue && (rare > bestRare || (rare == bestRare && (gain > bestGain || (gain == bestGain && (cost < bestCost
                        || (cost == bestCost && _p.Can[w * _s + s] > _p.Can[best * _s + s]))))))))
                    {
                        best = w;
                        bx = x;
                        by = y;
                        bestValue = value;
                        bestRare = rare;
                        bestGain = gain;
                        bestCost = cost;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                for (int h = bx; h < by; h++)
                {
                    Assign(best, d * Hours + h, s);
                }
                any = true;
            }
            return any;
        }

        // The hours of a day somebody could take at a station out of those still wanted, by their limits; none if a limit of the day or the week is reached.
        private uint Open(int level, int w, int d, int s, uint need)
        {
            int at = level * _w + w, wd = w * Days + d;
            if (((_stationOk[at] >> s) & 1UL) == 0 || ((_dayOk[at] >> d) & 1) == 0 || (_dayH[wd] == 0 && _daysOn[w] >= _maxDays[at])
                || _dayH[wd] >= RotaCatalogue.DayCap || _weekH[w] >= _maxHours[at])
            {
                return 0;
            }
            return need & _hourOk[at] & ~_busy[wd];
        }

        // The best shift somebody could take out of the uncovered hours of a day at a station: hours x to y. False if they can take none.
        private bool Window(int level, int w, int d, int s, uint need, out int x, out int y)
        {
            x = y = 0;
            int at = level * _w + w, wd = w * Days + d;
            uint open = Open(level, w, d, s, need);
            if (open == 0)
            {
                return false;
            }
            int room = Math.Min(RotaCatalogue.DayCap - _dayH[wd], _maxHours[at] - _weekH[w]);
            if (_wantDays[w] > _daysOn[w] && _dayH[wd] == 0 && _maxHours[at] < Days * RotaCatalogue.DayCap)
            {
                // Somebody who wants so many days and has a most for the week: the hours they have left are spread over the days they have left.
                int daysLeft = _wantDays[w] - _daysOn[w];
                room = Math.Min(room, Math.Max(1, (_maxHours[at] - _weekH[w] + daysLeft - 1) / daysLeft));
            }
            int bestValue = -1, bestRare = -1;
            for (int h = 0; h < Hours;)
            {
                if (((open >> h) & 1u) == 0)
                {
                    h++;
                    continue;
                }
                int a = h;
                while (h < Hours && ((open >> h) & 1u) != 0)
                {
                    h++;
                }
                int b = h, before = RunBack(w, d, s, a), after = RunOn(w, d, s, b);
                int most = Math.Min(room, RotaCatalogue.ShiftCap - before - after);
                if (most <= 0)
                {
                    continue;
                }
                // A run longer than one shift is cut into equal shifts, so that no odd hour is left at its end.
                int run = b - a, len = run <= most ? run : (run + (run + most - 1) / most - 1) / ((run + most - 1) / most);
                len = len > most ? most : len;
                int from = a;
                if (len < run && before == 0)
                {
                    // Which end of the run: the one that joins a shift they already have, then the one worth more, then the one fewer others could take.
                    int ends = _pre[b] - _pre[b - len], starts = _pre[a + len] - _pre[a];
                    if (after > 0 || ends > starts || (ends == starts && _rare[b] - _rare[b - len] > _rare[a + len] - _rare[a]))
                    {
                        from = b - len;
                    }
                }
                int value = _pre[from + len] - _pre[from], rare = _rare[from + len] - _rare[from];
                if (value > bestValue || (value == bestValue && (rare > bestRare || (rare == bestRare && len > y - x))))
                {
                    bestValue = value;
                    bestRare = rare;
                    x = from;
                    y = from + len;
                }
            }
            return bestValue >= 0;
        }

        // What somebody's unmet demands would weigh more if they took a shift (the relaxed step asks before it places).
        private int Broken(int w, int d, int s, int x, int y)
        {
            int before = Unmet(w);
            for (int h = x; h < y; h++)
            {
                Assign(w, d * Hours + h, s);
            }
            int after = Unmet(w);
            for (int h = x; h < y; h++)
            {
                Unassign(d * Hours + h, s);
            }
            return after - before;
        }

        private bool AnyNeed()
        {
            for (int i = 0; i < Days * _s; i++)
            {
                if (_need[i] != 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
