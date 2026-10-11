using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The making of a ladder (see RotaLadder.cs), a step at a time. A candidate week is made the same way each time:
    /// the worth of its hours is laid out, its cleaning hours are chosen by what each brings, a rota is made, the
    /// hours nobody can keep are cut and the rota made again, and the week is valued. One step is one of those
    /// pieces, one cleaning hour, or one step of the rota.
    /// </summary>
    public sealed partial class RotaLadder
    {
        private const int PhaseIdle = 0, PhaseGrid = 1, PhaseClean = 2, PhaseLay = 3, PhaseSolve = 4, PhaseCut = 5, PhaseSum = 6;
        private const int SeqStart = 0, SeqLean = 1, SeqStaff = 2, SeqCut = 3, SeqAdvised = 4, SeqHire = 5, SeqDone = 6;
        private const int CutNone = 0, CutPlain = 1, CutBlocks = 2;

        private RotaShop _shop;
        private RotaProblem _base;
        private readonly RotaProblem _work = new RotaProblem();
        private RotaSolver _solver;
        private RotaOutlook _outlook;
        private RotaPlan _now;
        private bool[] _barred;
        private int _wanted;
        private RotaLadderResult _into;
        private int _seq = SeqDone, _phase = PhaseIdle, _cut, _candMode, _mode;
        private readonly bool[] _open = new bool[Cells], _blocked = new bool[Cells];
        private readonly int[] _servedEst = new int[Cells], _clean = new int[Cells], _cleanFree = new int[Cells], _cellOrder = new int[Cells];
        private readonly int[] _dayClean = new int[Days];
        private readonly bool[] _moved = new bool[Days];
        private int _moveAt, _moveWarm, _moveBest;
        private long _moveNow, _moveMost;
        private bool[] _need = new bool[0];
        private int _cleanCount, _cleanTarget, _cleanWage;
        private bool _cleanOn, _cleanPays;
        private readonly RotaPlan _plan = new RotaPlan();
        private readonly RotaRung _cand = new RotaRung(), _best = new RotaRung(), _a = new RotaRung(), _hireBest = new RotaRung();
        private readonly List<RotaRole> _roles = new List<RotaRole>(8);
        private readonly int[] _hired = new int[HiresMost], _tryGroups = new int[GroupsTried];
        private int _hires, _tryCount, _tryAt, _hireGroup;
        private bool _hireAny;
        private RotaDemand[][] _strict = new RotaDemand[0][];
        private static readonly RotaDemand[] HireDemands = { new RotaDemand { Kind = RotaDemandKind.Hours, Priority = RotaCatalogue.PriorityHigh, A = 0, B = RotaCatalogue.FullWeek } };

        /// <summary>The whole ladder in one go.</summary>
        public void Make(RotaShop shop, RotaProblem problem, RotaSolver solver, RotaAdvisor advisor, RotaOutlook outlook, RotaPlan now, bool[] barred, int mode, RotaLadderResult into)
        {
            Begin(shop, problem, solver, advisor, outlook, now, barred, mode, into);
            while (!Step(int.MaxValue))
            {
            }
        }

        /// <summary>
        /// Begins a ladder, to be made a few steps at a time by <see cref="Step"/>. <paramref name="outlook"/> is the
        /// advice already worked out for the shop (<see cref="RotaAdvisor.Advise"/>), <paramref name="now"/> the rota
        /// it has, rated; <paramref name="barred"/> (may be null) the hours it may not open. Nothing given here may
        /// change until the last step is done, and the solver and the advisor are not to be used for anything else.
        /// </summary>
        public void Begin(RotaShop shop, RotaProblem problem, RotaSolver solver, RotaAdvisor advisor, RotaOutlook outlook, RotaPlan now, bool[] barred, int mode, RotaLadderResult into)
        {
            _shop = shop;
            _base = problem;
            _solver = solver;
            _advisor = advisor;
            _outlook = outlook;
            _now = now;
            _barred = barred;
            _wanted = mode;
            _into = into;
            _seq = SeqStart;
            _phase = PhaseIdle;
            _hires = 0;
            into.Count = 0;
            into.Steps = 0;
        }

        /// <summary>Which piece the next step is: ten times the candidate week being made, plus the piece of it (for the self-checks' timing).</summary>
        public int Doing => _seq * 10 + _phase;

        /// <summary>Does so many steps more. True when the ladder is finished.</summary>
        public bool Step(int steps)
        {
            while (steps-- > 0 && _seq != SeqDone)
            {
                _into.Steps++;
                One();
            }
            return _seq == SeqDone;
        }

        private void One()
        {
            switch (_phase)
            {
                case PhaseIdle:
                    Start();
                    break;
                case PhaseGrid:
                    Grid();
                    break;
                case PhaseClean:
                    CleanOne();
                    break;
                case PhaseLay:
                    Lay();
                    _solver.Start(_work, _plan);
                    _phase = PhaseSolve;
                    break;
                case PhaseSolve:
                    _phase = _solver.Step(1) ? PhaseCut : PhaseSolve;
                    break;
                case PhaseCut:
                    Cut();
                    break;
                default:
                    Evaluate(_advisor, _shop, _work, _open, _plan, _cand, _base);
                    Array.Copy(_open, _cand.Open, Cells);
                    CopyPlan(_plan, _cand.Plan);
                    _cand.Staff = _base.Workers;
                    _cand.Hires = 0;
                    _phase = PhaseIdle;
                    Arrived();
                    break;
            }
        }

        // ---------------------------------------------------------------- the order of the candidates

        private bool Advised => _outlook.Advised && _shop.HasCustomers;

        private void Start()
        {
            RotaLadderResult r = _into;
            RotaShop shop = _shop;
            Build(0, -1, false);
            Array.Clear(r.Hourly, 0, r.Hourly.Length);
            for (int cell = 0; cell < Cells; cell++)
            {
                r.Hourly[cell % Hours] += shop.Customers[cell];
            }
            RotaRung first = r.Rungs[0];
            Evaluate(_advisor, shop, _base, shop.Open, _now, first);
            first.Kind = RotaRungKind.AsItIs;
            first.Source = 0;
            first.Hires = 0;
            first.Staff = _base.Workers;
            first.Gain = first.OneOff = 0;
            first.WeeksBack = 0;
            first.Changes.Clear();
            Array.Copy(shop.Open, first.Open, Cells);
            CopyPlan(_now, first.Plan);
            r.Count = 1;
            r.Recommended = 0;
            r.Stop = RotaEnd.NoAdvice;
            r.StopGroup = -1;
            r.StopGain = r.StopNeed = 0;
            r.ModeAuto = _wanted != RotaMode.StaffFirst && _wanted != RotaMode.Lean;
            r.ModeMatters = false;
            for (int w = 0; w < _base.Workers; w++)
            {
                r.ModeMatters |= Least(_base.Worker[w]) > 0 && Home(_base, w) >= 0;
            }
            r.LeanNet = r.StaffNet = 0;
            _seq = SeqLean;
            Run(shop.Open, CutNone, RotaMode.Lean);
        }

        private void Run(bool[] open, int cut, int mode, int extra = -1)
        {
            Build(_hires, extra, mode == RotaMode.StaffFirst);
            Array.Copy(open, _open, Cells);
            _cut = cut;
            _candMode = mode;
            _phase = PhaseGrid;
        }

        private void Arrived()
        {
            RotaLadderResult r = _into;
            switch (_seq)
            {
                case SeqLean:
                    CopyRung(_cand, _a);
                    r.LeanNet = r.StaffNet = _cand.Sum.Net;
                    if (r.ModeMatters && _wanted != RotaMode.Lean)
                    {
                        _seq = SeqStaff;
                        Run(_shop.Open, CutNone, RotaMode.StaffFirst);
                        break;
                    }
                    Moded(RotaMode.Lean);
                    break;
                case SeqStaff:
                    r.StaffNet = _cand.Sum.Net;
                    bool staff = _wanted == RotaMode.StaffFirst || (r.ModeAuto && Better(_cand, _a));
                    if (staff)
                    {
                        CopyRung(_cand, _a);
                    }
                    Moded(staff ? RotaMode.StaffFirst : RotaMode.Lean);
                    break;
                case SeqCut:
                    Consider(2);
                    TryAdvised();
                    break;
                case SeqAdvised:
                    Consider(3);
                    BestDone();
                    break;
                default:
                    if (!_hireAny || Better(_cand, _hireBest))
                    {
                        CopyRung(_cand, _hireBest);
                        _hireGroup = _tryGroups[_tryAt];
                        _hireAny = true;
                    }
                    if (++_tryAt < _tryCount)
                    {
                        Hire();
                        break;
                    }
                    Hired();
                    break;
            }
        }

        // The mode is settled: the rota for the hours kept now is the one "Fill the rota" writes, and the first candidate for the best week.
        private void Moded(int mode)
        {
            RotaLadderResult r = _into;
            _mode = r.Mode = mode;
            CopyPlan(_a.Plan, r.Fill);
            r.FillSum = _a.Sum;
            CopyRung(r.Rungs[0], _best);
            _best.Source = 0;
            if (Better(_a, _best))
            {
                CopyRung(_a, _best);
                _best.Source = 1;
            }
            if (Advised && _a.Sum.Hours > _a.Sum.Manned)
            {
                _seq = SeqCut;
                Run(_shop.Open, CutPlain, _mode);
                return;
            }
            TryAdvised();
        }

        private void Consider(int source)
        {
            if (Better(_cand, _best))
            {
                CopyRung(_cand, _best);
                _best.Source = source;
            }
        }

        private void TryAdvised()
        {
            bool differs = false;
            for (int cell = 0; cell < Cells; cell++)
            {
                differs |= _outlook.Best[cell] != _shop.Open[cell];
            }
            if (Advised && differs)
            {
                _seq = SeqAdvised;
                Run(_outlook.Best, CutBlocks, _mode);
                return;
            }
            BestDone();
        }

        private void BestDone()
        {
            RotaLadderResult r = _into;
            RotaRung rung = r.Rungs[1];
            CopyRung(_best, rung);
            rung.Kind = RotaRungKind.Best;
            rung.Hires = 0;
            rung.Gain = rung.Sum.Net - r.Rungs[0].Sum.Net;
            rung.OneOff = 0;
            rung.WeeksBack = 0;
            Changes(rung);
            r.Count = 2;
            r.Recommended = rung.Source == 0 ? 0 : 1;
            if (!Advised)
            {
                r.Stop = RotaEnd.NoAdvice;
                _seq = SeqDone;
                return;
            }
            NextHire();
        }

        // ---------------------------------------------------------------- one more person

        private void NextHire()
        {
            RotaLadderResult r = _into;
            if (_hires >= HiresMost || _base.Workers + _hires + 1 > RotaCatalogue.WorkersMost)
            {
                r.Stop = RotaEnd.Most;
                _seq = SeqDone;
                return;
            }
            // Who the hours worth opening still lack, with everybody there is and everybody advised so far.
            Build(_hires, -1, false);
            _advisor.Grid(_shop, _outlook.Best, _work);
            _advisor.Roles(_shop, _work, _roles);
            _tryCount = 0;
            for (int i = 0; i < _roles.Count && _tryCount < GroupsTried; i++)
            {
                if (_roles[i].Hire > 0)
                {
                    _tryGroups[_tryCount++] = _roles[i].Group;
                }
            }
            if (_tryCount == 0)
            {
                r.Stop = RotaEnd.Covered;
                _seq = SeqDone;
                return;
            }
            _tryAt = 0;
            _hireAny = false;
            _seq = SeqHire;
            Hire();
        }

        private void Hire()
        {
            Run(_outlook.Best, CutBlocks, _mode, _tryGroups[_tryAt]);
        }

        // Every group has been tried for the next person: the best of them is a rung if it adds enough.
        private void Hired()
        {
            RotaLadderResult r = _into;
            RotaRung before = r.Rungs[r.Count - 1];
            long gain = _hireBest.Sum.Net - before.Sum.Net;
            long need = Math.Max(GainLeast, before.Sum.Net > 0 ? before.Sum.Net * ShareLeast / 100 : 0);
            if (gain < need)
            {
                r.Stop = RotaEnd.TooLittle;
                r.StopGroup = _hireGroup;
                r.StopGain = gain;
                r.StopNeed = need;
                _seq = SeqDone;
                return;
            }
            RotaRung rung = r.Rungs[r.Count];
            CopyRung(_hireBest, rung);
            _hired[_hires++] = _hireGroup;
            rung.Kind = RotaRungKind.Hire;
            rung.Source = 3;
            rung.Hires = _hires;
            rung.Staff = _base.Workers;
            Array.Copy(_hired, rung.Hire, HiresMost);
            rung.Gain = gain;
            rung.OneOff = (long)_shop.HireCost * _hires;
            rung.WeeksBack = rung.OneOff <= 0 ? 0 : (int)Math.Min(999, (rung.OneOff + gain - 1) / gain);
            Changes(rung);
            r.Count++;
            r.Recommended = r.Count - 1;
            NextHire();
        }

        /// <summary>The going wage of somebody new at a group: the figure given, else what an hour at its first station is counted at.</summary>
        public static int Going(RotaShop shop, int group)
        {
            if (group >= 0 && group < shop.Groups && shop.GroupGoing[group] > 0)
            {
                return shop.GroupGoing[group];
            }
            for (int s = 0; s < shop.Stations; s++)
            {
                if (shop.Group[s] == group)
                {
                    return shop.StationWage[s];
                }
            }
            return 0;
        }

        // The problem the candidates are solved in: the people there are, the people advised so far, and one more for a group being tried.
        private void Build(int hires, int extra, bool strict)
        {
            RotaProblem from = _base, to = _work;
            int stations = from.Stations, real = from.Workers, all = real + hires + (extra >= 0 ? 1 : 0);
            to.Size(stations, all);
            Array.Copy(from.Kind, to.Kind, stations);
            Array.Copy(from.Whole, to.Whole, stations);
            Array.Copy(from.Can, to.Can, real * stations);
            for (int w = 0; w < real; w++)
            {
                to.Worker[w].Wage = from.Worker[w].Wage;
                to.Worker[w].Mood = from.Worker[w].Mood;
                to.Worker[w].Demands = from.Worker[w].Demands;
            }
            // Staff first: nothing anybody asks for gives way to an hour (the solver lets only demands below the game's high priority give way).
            if (_strict.Length < real)
            {
                _strict = new RotaDemand[real][];
            }
            for (int w = 0; strict && w < real; w++)
            {
                RotaDemand[] asked = from.Worker[w].Demands;
                if (_strict[w] == null || _strict[w].Length != asked.Length)
                {
                    _strict[w] = new RotaDemand[asked.Length];
                }
                for (int k = 0; k < asked.Length; k++)
                {
                    _strict[w][k] = asked[k];
                    _strict[w][k].Priority = RotaCatalogue.PriorityHigh;
                }
                to.Worker[w].Demands = _strict[w];
            }
            for (int v = real; v < all; v++)
            {
                int group = v - real < hires ? _hired[v - real] : extra;
                to.Worker[v].Wage = Going(_shop, group);
                to.Worker[v].Demands = HireDemands;   // a full week and no more
                for (int s = 0; s < stations; s++)
                {
                    to.Can[v * stations + s] = (byte)(_shop.Group[s] == group && !from.Whole[s] ? HireSkill : 0);
                }
            }
            if (_need.Length < stations)
            {
                _need = new bool[stations];
            }
        }

        private bool Better(RotaRung a, RotaRung b)
        {
            if (_shop.HasCustomers)
            {
                return a.Sum.Net > b.Sum.Net;
            }
            return a.Plan.CoveredValue > b.Plan.CoveredValue || (a.Plan.CoveredValue == b.Plan.CoveredValue && a.Sum.Wages + a.Sum.Demands < b.Sum.Wages + b.Sum.Demands);
        }

        // The least hours a week somebody asks for; 0 if they ask for none.
        private static int Least(RotaWorker worker)
        {
            int least = 0;
            for (int i = 0; i < worker.Demands.Length; i++)
            {
                if (worker.Demands[i].Kind == RotaDemandKind.Hours && worker.Demands[i].A > least)
                {
                    least = worker.Demands[i].A;
                }
            }
            return least;
        }

        // The group of the first station somebody can stand at; -1 for somebody the planner leaves alone.
        private int Home(RotaProblem problem, int worker)
        {
            for (int s = 0; s < problem.Stations; s++)
            {
                if (problem.Can[worker * problem.Stations + s] != 0)
                {
                    return _shop.Group[s];
                }
            }
            return -1;
        }

        // ---------------------------------------------------------------- one candidate week

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
            for (int w = 0; _candMode == RotaMode.StaffFirst && w < _base.Workers; w++)
            {
                int home = Home(_base, w);
                _cleanTarget += home >= 0 && shop.GroupKind[home] == RotaGroupKind.Cleaning ? Least(_base.Worker[w]) : 0;
            }
            _phase = PhaseClean;
        }

        // One more cleaning hour: the one that adds most to what the week's customers leave, for as long as that is more than a cleaner's hour costs.
        private void CleanOne()
        {
            RotaShop shop = _shop;
            if (_cleanPays && _cleanCount < CleanMost)
            {
                // The move that leaves most after the cleaner's wage: one hour more, or the same hour more on every day (one
                // hour alone may change nothing where the floor is dirtier than a single hour cleans).
                // A step looks at a day's worth of moves, so that no step is long; the week as it stands is counted before the first.
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
            // Staff first: the hours a cleaner asks for beyond those, day by day from closing time backwards.
            while (_cleanCount < _cleanTarget)
            {
                int last = -1;
                for (int cell = Cells - 1; cell >= 0; cell--)
                {
                    if (_clean[cell] < _cleanFree[cell] && (last < 0 || _dayClean[cell / Hours] < _dayClean[last / Hours]
                        || (_dayClean[cell / Hours] == _dayClean[last / Hours] && cell % Hours > last % Hours)))
                    {
                        last = cell;
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
            _phase = PhaseLay;
        }

        // A move of the cleaning hours, done (1) or undone (-1): below Cells one hour of the week, from there the same hour of
        // every day. Returns the hours it adds. Undoing takes back exactly what doing added (the cells it marked).
        private int CleanMove(int move, int way)
        {
            int added = 0;
            if (move < Cells)
            {
                if (way > 0 && _clean[move] < _cleanFree[move])
                {
                    _clean[move]++;
                    _moved[0] = true;
                    return 1;
                }
                if (way < 0 && _moved[0])
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

        // The cleaning hours chosen go into the worth of the hours; staff first, so do the hours people ask for at stations that would stand empty.
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
            if (_candMode != RotaMode.StaffFirst)
            {
                return;
            }
            // The open hours, the busiest first (and the earlier of two as busy).
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
                    asked += Home(_base, w) == g ? Least(_base.Worker[w]) : 0;
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
                            _work.Value[i] = RotaCatalogue.ValueSupport;
                            wanted++;
                        }
                    }
                }
            }
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
                _blocked[cell] = unmanned || (_barred != null && _barred[cell]);
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
                    _open[cell] &= !_blocked[cell] || (_barred != null && _barred[cell] && _advisor.Crewed(_shop, cell, _plan.At));
                }
            }
            else
            {
                _advisor.Blocks(_outlook.Net, _blocked, _shop.DayFee, _open);
            }
            _cut = CutNone;
            _phase = PhaseGrid;
        }

        // ---------------------------------------------------------------- what a rung changes, and why

        // Run by run of hours, day by day, against the hours kept now.
        private void Changes(RotaRung rung)
        {
            rung.Changes.Clear();
            RotaShop shop = _shop;
            RotaRung first = _into.Rungs[0];
            int service = -1;
            for (int g = 0; g < shop.Groups && service < 0; g++)
            {
                service = shop.GroupKind[g] == RotaGroupKind.Service ? g : -1;
            }
            for (int d = 0; d < Days; d++)
            {
                int from = -1;
                byte last = 255;
                for (int h = 0; h <= Hours; h++)
                {
                    int cell = d * Hours + h;
                    byte why = 255;
                    if (h < Hours && rung.Open[cell] != shop.Open[cell])
                    {
                        why = rung.Open[cell] ? RotaWhy.Opened
                            : _barred != null && _barred[cell] ? RotaWhy.Barred
                            : rung.Source == 2 || first.Served[cell] == 0 ? RotaWhy.NoCrew
                            : _outlook.Net[cell] <= 0 ? RotaWhy.NoPay
                            : _outlook.Best[cell] ? RotaWhy.NoCrew
                            : RotaWhy.Day;
                    }
                    if (why == last)
                    {
                        continue;
                    }
                    if (last != 255 && rung.Changes.Count < ChangesMost)
                    {
                        var change = new RotaChange { Day = d, From = from, To = h, Why = last, Group = last == RotaWhy.NoCrew ? service : -1 };
                        for (int k = from; k < h; k++)
                        {
                            change.Was += (long)first.Earn[d * Hours + k] - first.Cost[d * Hours + k];
                            change.Will += (long)rung.Earn[d * Hours + k] - rung.Cost[d * Hours + k];
                        }
                        rung.Changes.Add(change);
                    }
                    last = why;
                    from = h;
                }
            }
        }
    }
}
