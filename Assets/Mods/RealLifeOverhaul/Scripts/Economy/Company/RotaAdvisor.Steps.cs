using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The advice for one business worked out in one go or a step at a time, with the same steps in the same order.</summary>
    public sealed partial class RotaAdvisor
    {
        private const int StageHours = 0, StageFree = 1, StageBest = 2, StagePeople = 3, StageRota = 4, StageCut = 5, StageRotaAgain = 6, StageSums = 7, StageDone = 8;

        private int _stage = StageDone;
        private RotaShop _shop;
        private RotaProblem _problem;
        private RotaSolver _solver;
        private RotaOutlook _into;
        private short[] _present;
        private bool[] _barred;
        private readonly bool[] _blocked = new bool[Cells];
        private readonly bool[] _free = new bool[Cells];      // the hours worth opening with no hour barred

        /// <summary>
        /// Everything the page shows for one business; <paramref name="barred"/> may be null. Afterwards <paramref name="problem"/>
        /// holds the worth of the <see cref="RotaOutlook.Staffed"/> hours, and the outlook's plan the present staff's rota for them.
        /// </summary>
        public void Advise(RotaShop shop, RotaProblem problem, RotaSolver solver, short[] present, bool[] barred, RotaOutlook into)
        {
            Begin(shop, problem, solver, present, barred, into);
            while (!Step(int.MaxValue))
            {
            }
        }

        /// <summary>Begins the same advice for <see cref="Step"/>. Nothing given here may change until the last step.</summary>
        public void Begin(RotaShop shop, RotaProblem problem, RotaSolver solver, short[] present, bool[] barred, RotaOutlook into)
        {
            _shop = shop;
            _problem = problem;
            _solver = solver;
            _present = present;
            _barred = barred;
            _into = into;
            _stage = StageHours;
        }

        /// <summary>Does so many steps more: a stage of the arithmetic, or a step of a rota. True when the advice is finished.</summary>
        public bool Step(int steps)
        {
            while (steps-- > 0 && _stage != StageDone)
            {
                One();
            }
            return _stage == StageDone;
        }

        private void One()
        {
            RotaShop shop = _shop;
            RotaOutlook into = _into;
            switch (_stage)
            {
                case StageHours:
                    Net(shop, into.Net, into.Crew);
                    into.Advised = shop.HasCustomers && !shop.FixedHours && shop.Margin > 0 && shop.Stations > 0;
                    into.CustomersWeek = 0;
                    for (int cell = 0; cell < Cells; cell++)
                    {
                        into.CustomersWeek += shop.Customers[cell];
                    }
                    into.Barred = false;
                    into.ProfitFree = 0;
                    into.HoursFree = 0;
                    if (into.Advised)
                    {
                        Blocks(into.Net, _barred, shop.DayFee, into.Best);
                    }
                    else
                    {
                        Array.Copy(shop.Open, into.Best, Cells);
                    }
                    _stage = into.Advised && _barred != null ? StageFree : StageBest;
                    break;
                case StageFree:
                    // What the barred hours would add: the advice once more without the bar, if that opens any hour more.
                    Blocks(into.Net, null, shop.DayFee, _free);
                    for (int cell = 0; cell < Cells; cell++)
                    {
                        into.Barred |= _free[cell] != into.Best[cell];
                    }
                    if (into.Barred)
                    {
                        Grid(shop, _free, _problem);
                        into.ProfitFree = Profit(shop, _problem, _free, null, true);
                        into.HoursFree = Count(_free);
                    }
                    _stage = StageBest;
                    break;
                case StageBest:
                    Grid(shop, into.Best, _problem);
                    into.ProfitBest = Profit(shop, _problem, into.Best, null, true);
                    _stage = StagePeople;
                    break;
                case StagePeople:
                    Roles(shop, _problem, into.Roles);
                    Missing(shop, into.Best, into.Missing);
                    _solver.Start(_problem, into.Plan);
                    _stage = StageRota;
                    break;
                case StageRota:
                    _stage = _solver.Step(1) ? StageCut : StageRota;
                    break;
                case StageCut:
                    // With the staff there are: hours nobody keeps the smallest crew for are cut and the day's blocks made again.
                    Array.Copy(into.Best, into.Staffed, Cells);
                    bool cut = false;
                    for (int cell = 0; into.Advised && cell < Cells; cell++)
                    {
                        bool uncrewed = into.Best[cell] && !Crewed(shop, cell, into.Plan.At);
                        _blocked[cell] = uncrewed || (_barred != null && _barred[cell]);
                        cut |= uncrewed;
                    }
                    if (cut)
                    {
                        Blocks(into.Net, _blocked, shop.DayFee, into.Staffed);
                        Grid(shop, into.Staffed, _problem);
                        _solver.Start(_problem, into.Plan);
                    }
                    _stage = cut ? StageRotaAgain : StageSums;
                    break;
                case StageRotaAgain:
                    _stage = _solver.Step(1) ? StageSums : StageRotaAgain;
                    break;
                case StageSums:
                    into.ProfitStaffed = Profit(shop, _problem, into.Staffed, into.Plan.At, false);
                    into.ProfitNow = Profit(shop, _problem, shop.Open, _present, false);
                    into.HoursNow = Count(shop.Open);
                    into.HoursStaffed = Count(into.Staffed);
                    into.HoursBest = Count(into.Best);
                    _stage = StageDone;
                    break;
            }
        }

        private static int Count(bool[] open)
        {
            int n = 0;
            for (int cell = 0; cell < Cells; cell++)
            {
                n += open[cell] ? 1 : 0;
            }
            return n;
        }
    }
}
