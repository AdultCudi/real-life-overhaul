using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One candidate week of the ladder: its hours' worth, its cleaning, the fit to everybody's hours, the hours cut.</summary>
    public sealed partial class RotaLadder
    {
        private const int BenchMost = 8;    // people one week may leave out, one after another, before whoever is still short gets no hours

        private readonly bool[] _blocked = new bool[Cells];
        private readonly int[] _servedEst = new int[Cells], _clean = new int[Cells], _cleanFree = new int[Cells], _cellOrder = new int[Cells];
        private readonly int[] _dayClean = new int[Days];
        private readonly bool[] _moved = new bool[Days];
        private int _moveAt, _moveWarm, _moveBest;
        private long _moveNow, _moveMost;
        private int _cleanCount, _cleanTarget, _cleanWage;
        private bool _cleanOn, _cleanPays;
        private readonly RotaFit _fit = new RotaFit();
        private bool[] _benched = new bool[0];
        private bool _rescuing;             // whoever has no hours is having the last try, a person a step
        private bool _rescued;              // the last try gave somebody a week: the fit runs once more
        private readonly bool[] _ext = new bool[Cells], _added = new bool[Cells];
        private int _benchRounds, _extFor;
        private int[] _groupAsked = new int[0], _groupHeld = new int[0];

        private void Grid()
        {
            RotaShop shop = _shop;
            int stations = shop.Stations;
            _advisor.Grid(shop, _open, _work);
            int cleaning = -1;
            for (int s = 0; s < stations && cleaning < 0; s++)
            {
                cleaning = shop.GroupKind[shop.Group[s]] == RotaGroupKind.Cleaning ? s : -1;
            }
            _cleanOn = shop.HasCustomers && !shop.Cleaned && cleaning >= 0 && shop.DirtPerCustomer > 0 && shop.CleanPerHour > 0;
            _cleanCount = _cleanTarget = _moveAt = 0;
            _cleanPays = true;
            Array.Clear(_clean, 0, Cells);
            Array.Clear(_dayClean, 0, Days);
            if (!_cleanOn)
            {
                _phase = PhaseLay;
                return;
            }
            _cleanWage = shop.StationWage[cleaning];
            for (int cell = 0; cell < Cells; cell++)
            {
                int served = 0, wage, count, free = 0;
                if (_open[cell])
                {
                    _advisor.Crew(shop, shop.Customers[cell], _need, out served, out wage, out count);
                }
                for (int s = 0; s < stations && _open[cell]; s++)
                {
                    free += shop.GroupKind[shop.Group[s]] == RotaGroupKind.Cleaning && !shop.Taken[cell * stations + s] ? 1 : 0;
                }
                _servedEst[cell] = served;
                _cleanFree[cell] = free;
            }
            for (int w = 0; w < _base.Workers; w++)
            {
                int home = Home(_base, w);
                _cleanTarget += !_benched[w] && home >= 0 && shop.GroupKind[home] == RotaGroupKind.Cleaning ? Least(_base.Worker[w]) : 0;
            }
            _phase = PhaseClean;
        }

        // One more cleaning hour, the one adding most to sales while that beats a cleaner's wage; then the cleaners' own least hours.
        private void CleanOne()
        {
            RotaShop shop = _shop;
            if (_cleanPays && _cleanCount < CleanMost)
            {
                // A move is one hour more, or the same hour more on every day (one hour alone may change nothing where the
                // floor is dirtier than an hour cleans). A step tries a day's worth of moves, so no step is long.
                int end;
                if (_moveAt == 0)
                {
                    CleanSales(shop, _open, _servedEst, _clean, 0, out _moveWarm);
                    _moveNow = CleanSales(shop, _open, _servedEst, _clean, _moveWarm, out end);
                    _moveMost = 0;
                    _moveBest = -1;
                }
                for (int n = 0; n < Hours && _moveAt < Cells + Hours; n++, _moveAt++)
                {
                    int added = CleanMove(_moveAt, 1);
                    if (added > 0)
                    {
                        long left = CleanSales(shop, _open, _servedEst, _clean, _moveWarm, out end) - _moveNow - (long)added * _cleanWage;
                        if (left > _moveMost)
                        {
                            _moveMost = left;
                            _moveBest = _moveAt;
                        }
                    }
                    CleanMove(_moveAt, -1);
                }
                if (_moveAt < Cells + Hours)
                {
                    return;
                }
                _moveAt = 0;
                int best = _moveBest;
                if (best >= 0)
                {
                    _cleanCount += CleanMove(best, 1);
                    for (int d = 0; d < Days; d++)
                    {
                        _dayClean[d] = 0;
                        for (int h = 0; h < Hours; h++)
                        {
                            _dayClean[d] += _clean[d * Hours + h];
                        }
                    }
                    return;
                }
            }
            _cleanPays = false;
            // The day with the fewest cleaning hours first, its busiest hour beside the cleaning it has, so a shift grows round them.
            while (_cleanCount < _cleanTarget)
            {
                int last = -1;
                bool lastBeside = false;
                for (int cell = 0; cell < Cells; cell++)
                {
                    if (_clean[cell] >= _cleanFree[cell])
                    {
                        continue;
                    }
                    int h = cell % Hours;
                    bool beside = (h > 0 && _clean[cell - 1] > 0) || (h < Hours - 1 && _clean[cell + 1] > 0);
                    if (last < 0 || _dayClean[cell / Hours] < _dayClean[last / Hours]
                        || (_dayClean[cell / Hours] == _dayClean[last / Hours] && (beside && !lastBeside || (beside == lastBeside && shop.Customers[cell] > shop.Customers[last]))))
                    {
                        last = cell;
                        lastBeside = beside;
                    }
                }
                if (last < 0)
                {
                    break;
                }
                _clean[last]++;
                _dayClean[last / Hours]++;
                _cleanCount++;
            }
            CleanBlocks();
            _phase = PhaseLay;
        }

        // A day's cleaning as one shift, two hours or more where there is room: the run holding most hours chosen, then most customers.
        // A second cleaner at once stands in its busiest hours.
        private void CleanBlocks()
        {
            RotaShop shop = _shop;
            for (int d = 0; d < Days; d++)
            {
                int at = d * Hours, length = 0, extra = 0, room = 0;
                for (int h = 0; h < Hours; h++)
                {
                    length += _clean[at + h] > 0 ? 1 : 0;
                    extra += Math.Max(0, _clean[at + h] - 1);
                    room += _cleanFree[at + h] > 0 ? 1 : 0;
                }
                if (length == 0)
                {
                    continue;
                }
                length = Math.Min(room, Math.Max(length, 2));
                int best = -1;
                long most = -1;
                for (int from = 0; from + length <= Hours; from++)
                {
                    long score = 0;
                    bool whole = true;
                    for (int h = from; h < from + length && whole; h++)
                    {
                        whole = _cleanFree[at + h] > 0;
                        score += (_clean[at + h] > 0 ? 1L << 20 : 0L) + shop.Customers[at + h];
                    }
                    if (whole && score > most)
                    {
                        most = score;
                        best = from;
                    }
                }
                if (best < 0)
                {
                    continue;   // no run of open hours is that long: the hours stay where they were chosen
                }
                _cleanCount += length - _dayClean[d] + extra;
                for (int h = 0; h < Hours; h++)
                {
                    _clean[at + h] = h >= best && h < best + length ? 1 : 0;
                }
                for (; extra > 0; extra--)
                {
                    int busiest = -1;
                    for (int h = best; h < best + length; h++)
                    {
                        if (_clean[at + h] < _cleanFree[at + h] && (busiest < 0 || _clean[at + h] < _clean[at + busiest]
                            || (_clean[at + h] == _clean[at + busiest] && shop.Customers[at + h] > shop.Customers[at + busiest])))
                        {
                            busiest = h;
                        }
                    }
                    if (busiest < 0)
                    {
                        _cleanCount -= extra;
                        break;
                    }
                    _clean[at + busiest]++;
                }
                _dayClean[d] = 0;
                for (int h = 0; h < Hours; h++)
                {
                    _dayClean[d] += _clean[at + h];
                }
            }
        }

        // A move of the cleaning hours, done (1) or undone (-1): below Cells one hour of the week, from there the same hour of
        // every day. Returns the hours it adds; undoing takes back the cells it marked.
        private int CleanMove(int move, int way)
        {
            int added = 0;
            if (move < Cells)
            {
                if (way > 0)
                {
                    // Set either way: the round's best move, done for good, leaves it set, and a later try that finds no room must not undo it.
                    _moved[0] = _clean[move] < _cleanFree[move];
                    _clean[move] += _moved[0] ? 1 : 0;
                    return _moved[0] ? 1 : 0;
                }
                if (_moved[0])
                {
                    _clean[move]--;
                    _moved[0] = false;
                }
                return 0;
            }
            for (int d = 0; d < Days; d++)
            {
                int cell = d * Hours + (move - Cells);
                if (way > 0)
                {
                    _moved[d] = _clean[cell] < _cleanFree[cell];
                    _clean[cell] += _moved[d] ? 1 : 0;
                    added += _moved[d] ? 1 : 0;
                }
                else if (_moved[d])
                {
                    _clean[cell]--;
                    _moved[d] = false;
                }
            }
            return added;
        }

        // The cleaning hours chosen, and the least hours people ask for at stations otherwise empty, go into the hours' worth.
        private void Lay()
        {
            RotaShop shop = _shop;
            int stations = shop.Stations;
            for (int cell = 0; _cleanOn && cell < Cells; cell++)
            {
                int n = _clean[cell];
                for (int s = 0; s < stations; s++)
                {
                    if (shop.GroupKind[shop.Group[s]] != RotaGroupKind.Cleaning)
                    {
                        continue;
                    }
                    int i = cell * stations + s;
                    _work.Value[i] = 0;
                    if (n > 0 && !shop.Taken[i])
                    {
                        _work.Value[i] = RotaCatalogue.ValueSupport;
                        n--;
                    }
                }
            }
            // The busiest first, the earlier of two as busy.
            for (int cell = 0; cell < Cells; cell++)
            {
                int j = cell;
                while (j > 0 && shop.Customers[_cellOrder[j - 1]] < shop.Customers[cell])
                {
                    _cellOrder[j] = _cellOrder[j - 1];
                    j--;
                }
                _cellOrder[j] = cell;
            }
            for (int g = 0; g < shop.Groups; g++)
            {
                if (shop.GroupKind[g] == RotaGroupKind.Cleaning)
                {
                    continue;
                }
                int asked = 0, wanted = 0;
                for (int w = 0; w < _base.Workers; w++)
                {
                    asked += Home(_base, w) == g && !_benched[w] ? Least(_base.Worker[w]) : 0;
                }
                for (int i = 0; asked > 0 && i < Cells * stations; i++)
                {
                    wanted += shop.Group[i % stations] == g && _work.Value[i] > 0 ? 1 : 0;
                }
                for (int k = 0; k < Cells && wanted < asked; k++)
                {
                    int cell = _cellOrder[k];
                    for (int s = 0; s < stations && _open[cell] && wanted < asked; s++)
                    {
                        int i = cell * stations + s;
                        if (shop.Group[s] == g && _work.Value[i] == 0 && !shop.Taken[i] && !_work.Whole[s])
                        {
                            _work.Value[i] = RotaCatalogue.ValueSupport + Math.Min(1000, shop.Customers[cell]);   // the busier hour the more wanted
                            wanted++;
                        }
                    }
                }
            }
        }

        // Whoever is still outside their range is left out and the week made again: the least skilled of them, or somebody less
        // skilled at the same stations when there is room. At the end whoever has no hours has a last try, the better first.
        private void Fitted()
        {
            int stations = _work.Stations, real = _base.Workers;
            int worst = -1, low = int.MaxValue;
            if (_rescued)
            {
                _rescued = false;
                _solver.Rate(_work, _plan);
                _phase = PhaseCut;
                return;
            }
            for (int w = 0; w < real; w++)
            {
                int hours = _fit.HoursOf(w), skill = RotaFit.Skill(_base, w);
                if (hours != 0 && !RotaFit.Inside(_base.Worker[w], hours) && !_benched[w] && skill <= low)
                {
                    low = skill;
                    worst = w;
                }
            }
            if (worst >= 0 && _benchRounds++ < BenchMost)
            {
                int home = Home(_base, worst), least = Least(_base.Worker[worst]);
                bool room = _fit.HoursOf(worst) < least && RotaFit.Room(_shop, _base, _open, worst) >= least;
                for (int v = 0; room && v < real; v++)
                {
                    int skill = RotaFit.Skill(_base, v);
                    if (v != worst && !_benched[v] && skill < low && _fit.HoursOf(v) > 0 && Least(_base.Worker[v]) > 0 && Home(_base, v) == home)
                    {
                        low = skill;
                        worst = v;
                    }
                }
                _benched[worst] = true;
                Build(_hires, _runExtra, _runStrict);
                _phase = PhaseGrid;
                return;
            }
            bool tried = false;
            for (int w = 0; w < real; w++)
            {
                if (!RotaFit.Inside(_base.Worker[w], _fit.HoursOf(w)))
                {
                    _fit.Drop(w);
                }
                // Whoever was left out may stand at their stations again for the last try.
                for (int s = 0; _benched[w] && s < stations; s++)
                {
                    _work.Can[w * stations + s] = _base.Can[w * stations + s];
                }
                tried |= _fit.HoursOf(w) == 0;
            }
            if (tried)
            {
                _fit.RescueBegin();
                _rescuing = true;
                return;
            }
            _solver.Rate(_work, _plan);
            _phase = PhaseCut;
        }

        // After the last try, whoever was left out and still has no hours stands nowhere again; if it gave anybody a week, the fit runs once more.
        private void Rescued()
        {
            int stations = _work.Stations;
            _rescuing = false;
            for (int w = 0; w < _base.Workers; w++)
            {
                for (int s = 0; _benched[w] && _fit.HoursOf(w) == 0 && s < stations; s++)
                {
                    _work.Can[w * stations + s] = 0;
                }
            }
            if (_fit.Given > 0)
            {
                _rescued = true;
                return;
            }
            _solver.Rate(_work, _plan);
            _phase = PhaseCut;
        }

        // The hours nobody keeps the smallest crew for are closed, and the week is made once more without them.
        private void Cut()
        {
            if (_cut == CutNone)
            {
                _phase = PhaseSum;
                return;
            }
            bool any = false;
            for (int cell = 0; cell < Cells; cell++)
            {
                bool unmanned = _open[cell] && !_advisor.Crewed(_shop, cell, _plan.At);
                _blocked[cell] = unmanned || (_cut == CutBlocks && _barred != null && _barred[cell]);
                any |= unmanned;
            }
            if (!any)
            {
                _phase = PhaseSum;
                return;
            }
            if (_cut == CutPlain)
            {
                for (int cell = 0; cell < Cells; cell++)
                {
                    _open[cell] &= !_blocked[cell];
                }
            }
            else
            {
                _advisor.Blocks(_outlook.Net, _blocked, _shop.DayFee, _open);
            }
            _cut = CutNone;
            _phase = PhaseGrid;
        }

        // Who a week gives no hours although they ask for some, and why (RotaClash).
        private void Leftover(RotaRung rung, bool[] open)
        {
            RotaShop shop = _shop;
            int stations = shop.Stations;
            rung.Left.Clear();
            if (_groupAsked.Length < shop.Groups)
            {
                _groupAsked = new int[shop.Groups];
                _groupHeld = new int[shop.Groups];
            }
            bool counted = false;
            for (int w = 0; w < _base.Workers; w++)
            {
                int least, most, home = Home(_base, w);
                RotaFit.Range(_base.Worker[w], out least, out most);
                if (least <= 0 || rung.HoursOf[w] > 0 || home < 0)
                {
                    continue;
                }
                if (!counted)
                {
                    counted = true;
                    Array.Clear(_groupAsked, 0, _groupAsked.Length);
                    Array.Clear(_groupHeld, 0, _groupHeld.Length);
                    for (int v = 0; v < _base.Workers; v++)
                    {
                        int group = Home(_base, v);
                        _groupAsked[group < 0 ? 0 : group] += group < 0 ? 0 : Least(_base.Worker[v]);
                    }
                    for (int i = 0; i < Cells * stations; i++)
                    {
                        _groupHeld[shop.Group[i % stations]] += open[i / stations] && !shop.Taken[i] ? 1 : 0;
                    }
                }
                var left = new RotaLeft { Worker = w, Least = least, Room = RotaFit.Room(shop, _base, open, w), Why = RotaClash.Hours };
                if (shop.Cleaned && shop.GroupKind[home] == RotaGroupKind.Cleaning && left.Room < least)
                {
                    left.Why = RotaClash.Service;
                }
                else if (left.Room >= least)
                {
                    left.Why = RotaClash.Shared;
                    left.Asked = _groupAsked[home];
                    left.Held = _groupHeld[home];
                }
                rung.Left.Add(left);
            }
        }

        // The hours kept and more: the best paying hour beside an open one (or the best of a shut day), one at a time, until
        // the opening hours hold the least hours of everybody the week left out. False if no hour was added.
        private bool Extend(RotaRung from)
        {
            RotaShop shop = _shop;
            int stations = shop.Stations, added = 0;
            Array.Copy(shop.Open, _ext, Cells);
            Array.Clear(_added, 0, Cells);
            _extFor = from.Left.Count > 0 ? from.Left[0].Worker : -1;
            for (int round = 0; round < Cells; round++)
            {
                bool holds = true;
                for (int i = 0; i < from.Left.Count && holds; i++)
                {
                    RotaLeft left = from.Left[i];
                    int home = Home(_base, left.Worker), asked = 0, held = 0;
                    holds &= RotaFit.Room(shop, _base, _ext, left.Worker) >= left.Least;
                    for (int v = 0; v < _base.Workers; v++)
                    {
                        asked += Home(_base, v) == home ? Least(_base.Worker[v]) : 0;
                    }
                    for (int k = 0; k < Cells * stations; k++)
                    {
                        held += _ext[k / stations] && shop.Group[k % stations] == home && !shop.Taken[k] ? 1 : 0;
                    }
                    holds &= held >= asked;
                }
                if (holds)
                {
                    break;
                }
                int best = -1;
                for (int cell = 0; cell < Cells; cell++)
                {
                    if (_ext[cell] || (_barred != null && _barred[cell]))
                    {
                        continue;
                    }
                    int d = cell / Hours, h = cell % Hours;
                    bool beside = (h > 0 && _ext[cell - 1]) || (h < Hours - 1 && _ext[cell + 1]), any = false;
                    for (int k = 0; k < Hours && !beside; k++)
                    {
                        any |= _ext[d * Hours + k];
                    }
                    if ((beside || !any) && (best < 0 || _outlook.Net[cell] > _outlook.Net[best]))
                    {
                        best = cell;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                _ext[best] = _added[best] = true;
                added++;
            }
            return added > 0;
        }
    }
}
