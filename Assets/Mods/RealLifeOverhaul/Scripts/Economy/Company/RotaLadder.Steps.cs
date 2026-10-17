using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The ladder made a step at a time: the order of its candidate weeks, which become rungs, and what a rung changes.</summary>
    public sealed partial class RotaLadder
    {
        private const int PhaseIdle = 0, PhaseGrid = 1, PhaseClean = 2, PhaseLay = 3, PhaseSolve = 4, PhaseCut = 5, PhaseSum = 6, PhaseFit = 7;
        private const int SeqStart = 0, SeqLean = 1, SeqStaff = 2, SeqCut = 3, SeqAdvised = 4, SeqHire = 5, SeqDone = 6, SeqExtend = 7;
        private const int CutNone = 0, CutPlain = 1, CutBlocks = 2;

        private RotaShop _shop;
        private RotaProblem _base;
        private readonly RotaProblem _work = new RotaProblem();
        private RotaSolver _solver;
        private RotaAdvisor _advisor;
        private RotaOutlook _outlook;
        private RotaPlan _now;
        private bool[] _barred;
        private int _wanted;
        private RotaLadderResult _into;
        private int _seq = SeqDone, _phase = PhaseIdle, _cut, _mode;
        private readonly bool[] _open = new bool[Cells];
        private bool[] _need = new bool[0];
        private readonly RotaPlan _plan = new RotaPlan();
        private readonly RotaRung _cand = new RotaRung(), _best = new RotaRung(), _a = new RotaRung(), _hireBest = new RotaRung();
        private readonly List<RotaRole> _roles = new List<RotaRole>(8);
        private readonly int[] _hired = new int[HiresMost], _tryGroups = new int[GroupsTried];
        private int _hires, _tryCount, _tryAt, _hireGroup;
        private bool _hireAny;
        private int _runExtra;
        private bool _runStrict;
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

        /// <summary>Begins a ladder for <see cref="Step"/>: nothing given may change, nor the solver and advisor be used elsewhere, until done.</summary>
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

        /// <summary>The piece the next step is: ten times the candidate week, plus its phase (for the self-check's timing).</summary>
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
                    if (_solver.Step(1))
                    {
                        _fit.Begin(_shop, _work, _open, _plan);
                        _phase = PhaseFit;
                    }
                    break;
                case PhaseFit:
                    if (_rescuing)
                    {
                        if (_fit.RescueStep(_base.Workers))
                        {
                            Rescued();
                        }
                    }
                    else if (_fit.Step())
                    {
                        Fitted();
                    }
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
                    Leftover(_cand, _open);
                    _phase = PhaseIdle;
                    Arrived();
                    break;
            }
        }

        // Customers come, the hours are the business's own and a customer's worth is known (RotaAdvisor).
        private bool Advised => _outlook.Advised;

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
            for (int w = 0; w < _base.Workers; w++)
            {
                first.Out += RotaFit.Inside(_base.Worker[w], first.HoursOf[w]) ? 0 : 1;
            }
            Leftover(first, shop.Open);
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
                r.ModeMatters |= Home(_base, w) >= 0 && (Least(_base.Worker[w]) > 0 || Gives(_base.Worker[w]));
            }
            r.LeanNet = r.StaffNet = 0;
            _seq = SeqLean;
            Run(shop.Open, CutNone, RotaMode.Lean);
        }

        // Starts a candidate week: these hours, cut or not, in this mode, with one more person for the group extra (-1: none).
        private void Run(bool[] open, int cut, int mode, int extra = -1)
        {
            if (_benched.Length < _base.Workers)
            {
                _benched = new bool[_base.Workers];
            }
            Array.Clear(_benched, 0, _benched.Length);
            _benchRounds = 0;
            _rescued = _rescuing = false;
            _runExtra = extra;
            _runStrict = mode == RotaMode.StaffFirst;
            Build(_hires, extra, _runStrict);
            Array.Copy(open, _open, Cells);
            _cut = cut;
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
                case SeqExtend:
                    _cand.FitsFor = _extFor;
                    Consider(4);
                    AfterFill();
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

        // The mode is settled: the rota for the hours kept now is what "Fill the rota" writes, and the first candidate for the best week.
        private void Moded(int mode)
        {
            RotaLadderResult r = _into;
            _mode = r.Mode = mode;
            CopyPlan(_a.Plan, r.Fill);
            r.FillSum = _a.Sum;
            r.FillLeft.Clear();
            r.FillLeft.AddRange(_a.Left);
            CopyRung(r.Rungs[0], _best);
            _best.Source = 0;
            if (Better(_a, _best))
            {
                CopyRung(_a, _best);
                _best.Source = 1;
            }
            // Where the hours kept cannot hold somebody's least hours: the same hours and more, the best paying first.
            if (Advised && _a.Left.Count > 0 && _a.Left[0].Why != RotaClash.Service && Extend(_a))
            {
                _seq = SeqExtend;
                Run(_ext, CutNone, _mode);
                return;
            }
            AfterFill();
        }

        private void AfterFill()
        {
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

        private void NextHire()
        {
            RotaLadderResult r = _into;
            if (_hires >= HiresMost || _base.Workers + _hires + 1 > RotaCatalogue.WorkersMost)
            {
                r.Stop = RotaEnd.Most;
                _seq = SeqDone;
                return;
            }
            // Who the hours worth opening still lack, with everybody there is and everybody advised so far: nobody left out
            // as the last candidate week left them.
            Array.Clear(_benched, 0, _benched.Length);
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
                // Where the goods ran out more with that person than without, the stock holds the plans back.
                r.Stop = _hireBest.Sum.StockShort > 0 && _hireBest.Sum.StockLost > before.Sum.StockLost ? RotaEnd.Stock : RotaEnd.TooLittle;
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
            // Paid once for everybody the plan takes on, against what they add together over the best week without them.
            long together = rung.Sum.Net - r.Rungs[1].Sum.Net;
            rung.OneOff = (long)_shop.HireCost * _hires;
            rung.WeeksBack = rung.OneOff <= 0 ? 0 : together <= 0 ? 999 : (int)Math.Min(999, (rung.OneOff + together - 1) / together);
            Changes(rung);
            r.Count++;
            r.Recommended = r.Count - 1;
            NextHire();
        }

        // The problem the candidates are solved in: the people there are, those advised so far, and one more for a group tried.
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
            // The hours a week somebody asks for never give way, in either mode; staff first, nothing they ask for does (the
            // solver lets only demands below the game's high priority give way). Whoever is left out of a week stands nowhere.
            if (_strict.Length < real)
            {
                _strict = new RotaDemand[real][];
            }
            for (int w = 0; w < real; w++)
            {
                RotaDemand[] asked = from.Worker[w].Demands;
                if (_strict[w] == null || _strict[w].Length != asked.Length)
                {
                    _strict[w] = new RotaDemand[asked.Length];
                }
                for (int k = 0; k < asked.Length; k++)
                {
                    _strict[w][k] = asked[k];
                    _strict[w][k].Priority = strict || asked[k].Kind == RotaDemandKind.Hours ? RotaCatalogue.PriorityHigh : asked[k].Priority;
                }
                to.Worker[w].Demands = _strict[w];
                for (int s = 0; w < _benched.Length && _benched[w] && s < stations; s++)
                {
                    to.Can[w * stations + s] = 0;
                }
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
            // First the one that keeps the hours rule for more people: nobody outside their range, and fewer left without hours.
            int wrongA = a.Out * 1000 + a.Left.Count, wrongB = b.Out * 1000 + b.Left.Count;
            if (wrongA != wrongB)
            {
                return wrongA < wrongB;
            }
            if (_shop.HasCustomers)
            {
                return a.Sum.Net > b.Sum.Net;
            }
            return a.Plan.CoveredValue > b.Plan.CoveredValue || (a.Plan.CoveredValue == b.Plan.CoveredValue && a.Sum.Wages + a.Sum.Demands < b.Sum.Wages + b.Sum.Demands);
        }

        private static int Least(RotaWorker worker)
        {
            RotaFit.Range(worker, out int least, out _);
            return least;
        }

        // A demand below the game's high priority beside the hours a week: lean lets it give way, staff first never (Build).
        private static bool Gives(RotaWorker worker)
        {
            for (int k = 0; k < worker.Demands.Length; k++)
            {
                if (worker.Demands[k].Kind != RotaDemandKind.Hours && worker.Demands[k].Priority < RotaCatalogue.PriorityHigh)
                {
                    return true;
                }
            }
            return false;
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

        // The first service group nobody stands at in an hour of a rung; -1 if each has somebody.
        private int Unmanned(RotaRung rung, int cell)
        {
            RotaShop shop = _shop;
            int stations = shop.Stations;
            for (int g = 0; g < shop.Groups; g++)
            {
                if (shop.GroupKind[g] != RotaGroupKind.Service)
                {
                    continue;
                }
                bool any = false, has = false;
                for (int s = 0; s < stations && !has; s++)
                {
                    if (shop.Group[s] == g)
                    {
                        any = true;
                        has = shop.Taken[cell * stations + s] || (rung.Plan.At.Length > cell * stations + s && rung.Plan.At[cell * stations + s] >= 0);
                    }
                }
                if (any && !has)
                {
                    return g;
                }
            }
            return -1;
        }

        // What a rung changes against the hours kept now, run by run of hours, day by day, each with its reason.
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
                        why = rung.Open[cell] ? (rung.Source == 4 && _added[cell] ? RotaWhy.Fits : RotaWhy.Opened)
                            : _barred != null && _barred[cell] ? RotaWhy.Barred
                            : rung.Source == 2 || (first.Served[cell] == 0 && first.Come[cell] > 0) ? RotaWhy.NoCrew
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
                        int missing = last == RotaWhy.NoCrew ? Unmanned(first, d * Hours + from) : -1;
                        var change = new RotaChange
                        {
                            Day = d, From = from, To = h, Why = last, Group = last == RotaWhy.NoCrew ? (missing >= 0 ? missing : service) : last == RotaWhy.Fits ? rung.FitsFor : -1
                        };
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
