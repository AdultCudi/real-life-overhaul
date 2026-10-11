using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The solver's improving pass, every move of which is counted against a fixed budget, and the rating of a rota:
    /// what it covers, what it costs, which demands it leaves unmet and why, which hours nobody covers and why.
    /// </summary>
    public sealed partial class RotaSolver
    {
        // ---------------------------------------------------------------- 5: improving

        // Uncovered hours, the most valuable day and station first: each is covered if a move that loses nothing does it.
        private bool Cover(int level)
        {
            bool ever = false;
            for (int pass = 0; pass < 2; pass++)
            {
                bool any = false;
                for (int i = 0; i < Days * _s && _steps > 0; i++)
                {
                    int lane = _lanes[i], s = lane % _s, d = lane / _s;
                    uint need = _need[lane];
                    for (int h = 0; need != 0 && !_p.Whole[s] && h < Hours && _steps > 0; h++)
                    {
                        if (((need >> h) & 1u) != 0)
                        {
                            any |= CoverHour(level, d, h, s);
                        }
                    }
                }
                ever |= any;
                if (!any)
                {
                    break;
                }
            }
            return ever;
        }

        private bool CoverHour(int level, int d, int h, int s)
        {
            int cell = d * Hours + h, value = _p.Value[cell * _s + s];
            int free = BestFree(level, d, h, s);
            if (free >= 0)
            {
                Assign(free, cell, s);
                return true;
            }
            for (int i = _canStart[s]; i < _canStart[s + 1] && _steps > 0; i++)
            {
                int w = _canList[i], at = level * _w + w;
                if (((_stationOk[at] >> s) & 1UL) == 0 || ((_dayOk[at] >> d) & 1) == 0 || ((_hourOk[at] >> h) & 1u) == 0)
                {
                    continue;
                }
                int now = _pos[w * Cells + cell];
                if (now >= 0)
                {
                    // They stand elsewhere this hour: they come over if it is the first or last hour of that shift (a shift is not
                    // cut in two), and the hour they leave is taken by another or is worth less.
                    if (Swap(level, w, cell, now, cell, s, value))
                    {
                        return true;
                    }
                    continue;
                }
                bool dayFull = _dayH[w * Days + d] >= RotaCatalogue.DayCap, weekFull = _weekH[w] >= _maxHours[at];
                int before = RunBack(w, d, s, h), after = RunOn(w, d, s, h + 1);
                if (!dayFull && !weekFull && before + after >= RotaCatalogue.ShiftCap)
                {
                    // Their shift here is as long as a shift may be: it moves along by an hour, the hour at its far end given up.
                    if ((before > 0 && Swap(level, w, cell - before, s, cell, s, value)) || (after > 0 && Swap(level, w, cell + after, s, cell, s, value)))
                    {
                        return true;
                    }
                    continue;
                }
                if (!dayFull && !weekFull)
                {
                    continue;   // (held back by their number of days: an hour given up elsewhere does not help)
                }
                // They are free this hour but at a limit of the day or the week: an hour at the edge of another shift is given up for it.
                for (int d2 = dayFull ? d : 0; d2 < (dayFull ? d + 1 : Days) && _steps > 0; d2++)
                {
                    uint busy = _busy[w * Days + d2];
                    for (int h2 = 0; busy != 0 && h2 < Hours; h2++)
                    {
                        if (((busy >> h2) & 1u) != 0 && Swap(level, w, d2 * Hours + h2, _pos[w * Cells + d2 * Hours + h2], cell, s, value))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // Somebody gives up one hour (at the edge of a shift) to take another that is uncovered. Kept if the hour given up is
        // taken by another, or is worth less than the hour gained; undone otherwise.
        private bool Swap(int level, int w, int from, int fromStation, int to, int toStation, int value)
        {
            int d = from / Hours, h = from % Hours, row = w * Cells + d * Hours;
            if (_p.Whole[fromStation] || !((h == 0 || _pos[row + h - 1] != fromStation) || (h == Hours - 1 || _pos[row + h + 1] != fromStation)))
            {
                return false;
            }
            _steps--;
            Unassign(from, fromStation);
            if (Free(level, w, to / Hours, to % Hours, toStation))
            {
                Assign(w, to, toStation);
                int other = BestFree(level, d, h, fromStation);
                if (other >= 0)
                {
                    Assign(other, from, fromStation);
                    return true;
                }
                if (_p.Value[from * _s + fromStation] < value)
                {
                    return true;
                }
                Unassign(to, toStation);
            }
            Assign(w, from, fromStation);
            return false;
        }

        // Who should take one free hour at a station: whoever already stands there the hour before or after, then whose demands it helps, then the cheapest.
        private int BestFree(int level, int d, int h, int s)
        {
            int best = -1, bestKey = 0;
            for (int i = _canStart[s]; i < _canStart[s + 1]; i++)
            {
                int w = _canList[i];
                _steps--;
                if (!Free(level, w, d, h, s))
                {
                    continue;
                }
                int row = w * Cells + d * Hours;
                bool joins = (h > 0 && _pos[row + h - 1] == s) || (h < Hours - 1 && _pos[row + h + 1] == s);
                int key = (joins ? 1000 : 0) + Gain(w, d, s, 1) - (level > 0 ? 100000 * Broken(w, d, s, h, h + 1) : 0);
                if (best < 0 || key > bestKey || (key == bestKey && Cheaper(w, best, s)))
                {
                    best = w;
                    bestKey = key;
                }
            }
            return best;
        }

        // Shift by shift: it goes to somebody else if that leaves fewer demands unmet, or the same and it costs less, or the same again and they do it better.
        private void Hand(int level)
        {
            for (int i = 0; i < Days * _s && _steps > 0; i++)
            {
                int lane = _lanes[i], s = lane % _s, d = lane / _s;
                if (_p.Whole[s])
                {
                    continue;
                }
                for (int h = 0; h < Hours && _steps > 0;)
                {
                    int holder = _at[(d * Hours + h) * _s + s];
                    if (holder < 0)
                    {
                        h++;
                        continue;
                    }
                    int x = h;
                    while (h < Hours && _at[(d * Hours + h) * _s + s] == holder)
                    {
                        h++;
                    }
                    int y = h, best = -1, bestUnmet = 0, bestWage = 0;
                    int unmetHolder = Unmet(holder);
                    for (int k = _canStart[s]; k < _canStart[s + 1]; k++)
                    {
                        int w = _canList[k];
                        _steps--;
                        if (w == holder || !CanTake(level, w, d, s, x, y))
                        {
                            continue;
                        }
                        int before = unmetHolder + Unmet(w);
                        Move(d, s, x, y, w);
                        int unmet = Unmet(holder) + Unmet(w) - before;
                        Move(d, s, x, y, holder);
                        int wage = (_p.Worker[w].Wage - _p.Worker[holder].Wage) * (y - x);
                        bool better = unmet < 0 || (unmet == 0 && (wage < 0 || (wage == 0 && _p.Can[w * _s + s] > _p.Can[holder * _s + s])));
                        if (better && (best < 0 || unmet < bestUnmet || (unmet == bestUnmet && (wage < bestWage || (wage == bestWage && _p.Can[w * _s + s] > _p.Can[best * _s + s])))))
                        {
                            best = w;
                            bestUnmet = unmet;
                            bestWage = wage;
                        }
                    }
                    if (best >= 0)
                    {
                        Move(d, s, x, y, best);
                    }
                }
            }
        }

        private void Move(int d, int s, int x, int y, int to)
        {
            for (int h = x; h < y; h++)
            {
                Unassign(d * Hours + h, s);
                Assign(to, d * Hours + h, s);
            }
        }

        // ---------------------------------------------------------------- what it comes to

        private void Rate()
        {
            RotaPlan plan = _plan;
            plan.WantedValue = plan.CoveredValue = plan.WantedHours = plan.CoveredHours = plan.UnmetCount = plan.UnmetWeight = plan.Shifts = 0;
            plan.Wage = 0;
            for (int d = 0; d < Days; d++)
            {
                for (int s = 0; s < _s; s++)
                {
                    int last = -1, gapFrom = -1;
                    for (int h = 0; h <= Hours; h++)
                    {
                        int cell = d * Hours + h;
                        int w = h < Hours ? _at[cell * _s + s] : -1;
                        int value = h < Hours ? _p.Value[cell * _s + s] : 0;
                        if (value > 0)
                        {
                            plan.WantedValue += value;
                            plan.WantedHours++;
                        }
                        if (w >= 0)
                        {
                            plan.Wage += _p.Worker[w].Wage;
                            plan.Shifts += w != last ? 1 : 0;
                            if (value > 0)
                            {
                                plan.CoveredValue += value;
                                plan.CoveredHours++;
                            }
                        }
                        last = w;
                        bool gap = value > 0 && w < 0;
                        if (gap && gapFrom < 0)
                        {
                            gapFrom = h;
                        }
                        else if (!gap && gapFrom >= 0)
                        {
                            if (plan.Gaps.Count < RotaCatalogue.ReasonsMost)
                            {
                                plan.Gaps.Add(new RotaGap { Day = d, From = gapFrom, To = h, Station = s, Reason = _canStart[s + 1] == _canStart[s] ? RotaReason.NoSkill : RotaReason.Stretched });
                            }
                            gapFrom = -1;
                        }
                    }
                }
            }
            for (int w = 0; w < _w; w++)
            {
                RotaDemand[] demands = _p.Worker[w].Demands;
                int count = Math.Min(demands.Length, RotaCatalogue.DemandsMost);
                for (int i = 0; i < count; i++)
                {
                    if (Met(w, ref demands[i]))
                    {
                        continue;
                    }
                    plan.UnmetCount++;
                    plan.UnmetWeight += Weight(demands[i].Priority);
                    if (plan.Unmet.Count < RotaCatalogue.ReasonsMost)
                    {
                        int day, station;
                        Where(w, ref demands[i], out day, out station);
                        plan.Unmet.Add(new RotaUnmet { Worker = w, Demand = i, Reason = Why(w, ref demands[i]), Day = day, Station = station });
                    }
                }
            }
        }

        // The first hour of the week in which a demand about days, hours or kinds of shift is not kept: the day, and the station they stand at then.
        private void Where(int w, ref RotaDemand q, out int day, out int station)
        {
            day = station = -1;
            if (q.Kind != RotaDemandKind.FreeDays && q.Kind != RotaDemandKind.NoHours && q.Kind != RotaDemandKind.NoShiftKind)
            {
                return;
            }
            for (int cell = 0; cell < Cells; cell++)
            {
                int s = _pos[w * Cells + cell];
                if (s < 0)
                {
                    continue;
                }
                bool here = q.Kind == RotaDemandKind.FreeDays ? ((q.Mask >> (cell / Hours)) & 1UL) != 0 : q.Kind == RotaDemandKind.NoHours ? ((q.Mask >> (cell % Hours)) & 1UL) != 0 : ((q.Mask >> _p.Kind[s]) & 1UL) != 0;
                if (here)
                {
                    day = cell / Hours;
                    station = s;
                    return;
                }
            }
        }

        private RotaReason Why(int w, ref RotaDemand q)
        {
            switch (q.Kind)
            {
                case RotaDemandKind.Hours:
                    return _weekH[w] < q.A ? RotaReason.Short : RotaReason.Needed;
                case RotaDemandKind.Days:
                    return _daysOn[w] < q.A ? RotaReason.Short : RotaReason.Needed;
                case RotaDemandKind.OnStation:
                    return (q.Mask & _canMask[w]) == 0 ? RotaReason.NoStation : RotaReason.Short;
                default:
                    return RotaReason.Needed;
            }
        }
    }
}
