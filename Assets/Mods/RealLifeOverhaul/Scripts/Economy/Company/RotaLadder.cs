using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How a business weighs what its people ask for against what an hour brings.</summary>
    public static class RotaMode
    {
        public const int Auto = 0;          // whichever of the two comes to more in a week
        public const int StaffFirst = 1;    // the least hours people ask for are given wherever the opening hours hold them, as the game's own auto fill does
        public const int Lean = 2;          // only hours that bring more than they cost
    }

    public static class RotaRungKind
    {
        public const int AsItIs = 0, Best = 1, Hire = 2;
    }

    /// <summary>Why a plan changes an hour the business keeps now.</summary>
    public static class RotaWhy
    {
        public const byte Opened = 0;   // the plan opens it
        public const byte NoCrew = 1;   // nobody can stand at a station the hour needs: it sells nothing as it is
        public const byte NoPay = 2;    // its customers bring less than the smallest crew is paid
        public const byte Barred = 3;   // no late opening permit
        public const byte Day = 4;      // the day's blocks of hours come to more without it
        public const byte Fits = 5;     // opened so that somebody can be given the least hours they ask for
    }

    /// <summary>Why the ladder ends where it does.</summary>
    public static class RotaEnd
    {
        public const int Covered = 0;   // the people there are (and those advised) keep every hour that pays
        public const int TooLittle = 1; // the next person would add less than the least a rung must
        public const int Most = 2;      // as many people as one ladder advises
        public const int NoAdvice = 3;  // nothing is advised here (no customers, hours set by the game, nothing known of a customer's worth)
        public const int Stock = 4;     // the next person would add too little because the goods run out first
    }

    /// <summary>What a week of a plan comes to, part by part. Money in hundredths.</summary>
    public struct RotaWeekSum
    {
        public int Hours, Manned;           // hours open, and those of them with the smallest crew there
        public int Come, Served;            // customers who come while it is open, and those who are sold something
        public int LostUnmanned, LostFull;  // those who find nobody at a station, and those more than the stations manned can serve
        public long Sales;                  // what the customers served leave after the cost of the goods, at the cleanliness they find
        public long Clean;                  // what a spotless floor would have added to that
        public long Wages, Fees, Theft, Night, Demands;
        public long Net;
        public int StaffHours, CleanHours;
        public long StockLost;              // what the customers would have left for goods that had run out (not in Sales: told, not taken off again)
        public int StockShort;              // products that ran out in the week
    }

    /// <summary>A run of hours on one day that a plan opens or closes against the hours kept now.</summary>
    public struct RotaChange
    {
        public int Day, From, To;
        public byte Why;
        public int Group;       // the stations nobody can be at (NoCrew); -1 otherwise
        public long Was;        // what those hours come to as things are
        public long Will;       // and in the plan
    }

    /// <summary>Somebody a plan gives no hours, because the least they ask for cannot be had there.</summary>
    public struct RotaLeft
    {
        public int Worker, Least;
        public int Room;        // the most hours they could be given in the plan's opening hours were nobody else there
        public int Why;         // RotaClash
        public int Asked, Held; // Shared: what those at the same stations ask for between them, and the hours those stations hold
    }

    /// <summary>One plan of the ladder: its hours, its rota, and what its week comes to.</summary>
    public sealed class RotaRung
    {
        public int Kind, Source;        // Source: 0 the week as it is, 1 the hours kept with a new rota, 2 those hours less the ones nobody can keep, 3 the hours worth opening, 4 the hours kept and more, so that everybody's least hours fit
        public int Hires;
        public readonly int[] Hire = new int[RotaLadder.HiresMost];     // the group each person to hire is for
        public int Workers, Staff;      // in the rota, and of them the people there are now
        public readonly bool[] Open = new bool[RotaCatalogue.Cells];
        public readonly RotaPlan Plan = new RotaPlan();
        public RotaWeekSum Sum;
        public long Gain;               // against the rung before
        public long OneOff;             // paid once for it
        public int WeeksBack;           // weeks until the gain has paid that; 0: at once
        public readonly int[] Come = new int[RotaCatalogue.Cells], Served = new int[RotaCatalogue.Cells];
        public readonly int[] Earn = new int[RotaCatalogue.Cells], Cost = new int[RotaCatalogue.Cells];
        public int[] HoursOf = new int[0];      // [worker] hours a week
        public long[] DemandCost = new long[0]; // [worker] what their unmet demands cost a week
        public int[] QuitHours = new int[0];    // [worker] hours until their satisfaction is at nothing; 0: it does not fall there
        public readonly List<RotaChange> Changes = new List<RotaChange>(RotaLadder.ChangesMost);
        public readonly List<RotaLeft> Left = new List<RotaLeft>(4);    // whoever gets no hours because their least cannot be had
        public readonly RotaStockWeek Stock = new RotaStockWeek();      // the goods in the week counted, product by product
        public int Out;                 // people at work whose hours are outside what they ask for (only the week as it is can have any)
        public int FitsFor = -1;        // the employee the hours were lengthened for (Source 4)

        /// <summary>True if everybody the plan gives hours works inside the range they ask for.</summary>
        public bool Usable => Out == 0;

        internal void Size(int workers)
        {
            if (HoursOf.Length < workers)
            {
                HoursOf = new int[workers];
                DemandCost = new long[workers];
                QuitHours = new int[workers];
            }
            Array.Clear(HoursOf, 0, HoursOf.Length);
            Array.Clear(DemandCost, 0, DemandCost.Length);
            Array.Clear(QuitHours, 0, QuitHours.Length);
            Array.Clear(Come, 0, Come.Length);
            Array.Clear(Served, 0, Served.Length);
            Array.Clear(Earn, 0, Earn.Length);
            Array.Clear(Cost, 0, Cost.Length);
            Workers = workers;
            Sum = new RotaWeekSum();
            Left.Clear();
            Stock.Clear(0);
            Out = 0;
            FitsFor = -1;
        }
    }

    /// <summary>The ladder of one business, from the week as it is to the last person worth hiring.</summary>
    public sealed class RotaLadderResult
    {
        public readonly RotaRung[] Rungs = new RotaRung[RotaLadder.RungsMost];
        public int Count;
        public int Recommended;
        public int Mode;                    // the one the plans are made in: RotaMode.StaffFirst or RotaMode.Lean
        public bool ModeAuto;               // it was chosen by what a week comes to, not by the player
        public bool ModeMatters;            // somebody asks for a least number of hours, so the two differ
        public long LeanNet, StaffNet;      // the hours kept now with a new rota, in each
        public int Stop, StopGroup;
        public long StopGain, StopNeed;
        public readonly RotaPlan Fill = new RotaPlan();     // the rota for the hours kept now ("Fill the rota")
        public RotaWeekSum FillSum;
        public readonly List<RotaLeft> FillLeft = new List<RotaLeft>(4);   // whoever that rota gives no hours, because the hours kept cannot hold their least
        public readonly int[] Hourly = new int[RotaCatalogue.Hours];   // the week's customers by the hour of the day
        public int Steps;                   // steps it took (the self-check)

        public RotaLadderResult()
        {
            for (int i = 0; i < Rungs.Length; i++)
            {
                Rungs[i] = new RotaRung();
            }
        }
    }

    /// <summary>
    /// The ladder of plans of one business, by rule and game-free: the week as it is, the best week the people there
    /// are can keep, and one rung for each further person worth hiring. Every rung is a set of opening hours and a
    /// rota (made by <see cref="RotaSolver"/>), valued the same way (<see cref="Evaluate"/>).
    ///
    /// WHAT A WEEK IS VALUED AT, each line the game's own arithmetic as read from its code (docs/features/economy-rota.md lists where):
    ///   sales       only customers who find the smallest crew buy: an hour open with nobody at a till sells nothing
    ///               (RetailBusinessSimulator.ProcessCustomer leaves the order unpaid). The stations manned serve
    ///               their customers an hour and no more; whoever comes beyond that is lost. Skill does not change
    ///               how many a till serves
    ///   goods       where a delivery or a logistics manager's round brings a product, the customers buy it only while
    ///               it is on site (RotaStock.cs: the shelves are filled again from storage the moment they run empty,
    ///               the round comes at 2, the delivery at 8 on a Monday, what finds no room goes back). The goods are
    ///               run over two weeks, as the cleanliness is, and the second is the one counted
    ///   cleanliness each customer served dirties the floor, each hour of a cleaner's shift cleans, and the
    ///               cleanliness a customer finds is a quarter of the satisfaction that scales what customers buy
    ///               (BuildingCleanliness, BusinessHelper.UpdateSatisfaction). The week is run hour by hour, twice,
    ///               so that it starts as dirty as it ends
    ///   wages       of everybody at a station
    ///   fees        the game's licensing fee of each day it opens
    ///   theft       the game's hourly theft follows the customers who came in the hour, served or not
    ///               (BusinessSecurityHelper.SimulateTheft)
    ///   night       the mod's break-ins come only to a shop that is shut at one in the morning (Crime/OffscreenCrimes)
    ///   demands     an unmet demand takes its weight off an employee's satisfaction every hour until they resign
    ///               at nothing (EmployeeInstance.UpdateSatisfaction, LowSatisfactionComplaint); counted as the
    ///               sales lost while the post stands empty for <see cref="ReplaceDays"/> days, spread over the weeks
    ///               it takes, and the worse work in the meantime (an order's service is skill times satisfaction)
    ///
    /// THE RUNGS
    ///   as it is    the hours and the rota it has
    ///   best        the better of: the week as it is; the hours kept with a new rota; those hours less the ones
    ///               nobody can keep the smallest crew for; the hours worth opening, cut to what the people can keep.
    ///               So it is never worse than the week as it is
    ///   hire        one more person of the group that adds most, at the going wage, with the hours worth opening
    ///               cut to what everybody can then keep. A rung is kept only if it adds at least
    ///               <see cref="ShareLeast"/> per cent and <see cref="GainLeast"/>; the ladder ends at the first
    ///               that does not, so a week open round the clock is shown only if every step to it pays
    ///
    /// Cost: nothing unless asked. A ladder is made in one go (<see cref="Make"/>) or a few steps at a time
    /// (<see cref="Begin"/> and <see cref="Step"/>), with the same result: the steps are the same in the same order.
    /// Nothing is allocated once its arrays have grown to the largest business seen. No chance, no clock.
    /// </summary>
    public sealed partial class RotaLadder
    {
        public const int HiresMost = 4;             // people one ladder advises hiring
        public const int RungsMost = 2 + HiresMost;
        public const int ShareLeast = 5;            // per cent of the week before that one more person must add...
        public static long GainLeast => 5000L * Prices.Level / 1000;   // (at today's prices, as the wages the rota reads)         // ...and never less than this, hundredths a week
        public const int ReplaceDays = 7;           // days a post stands empty when somebody resigns (the planner's own figure: the game sends candidates over days, and the player has to see it, choose and agree a wage)
        public const int CleanMost = 56;            // cleaning hours a week chosen one at a time by what each brings
        public const int ChangesMost = 28;
        public const int GroupsTried = 3;           // groups one more person is tried for, a rung
        public const int HireSkill = 50;            // how well somebody new is taken to do the job (it changes no figure but who is preferred)

        private const int Cells = RotaCatalogue.Cells, Hours = RotaCatalogue.Hours, Days = RotaCatalogue.Days;

        private RotaAdvisor _advisor;
        private readonly bool[] _dayOpen = new bool[Days];
        private int[] _unmetWeight = new int[0], _unmetCount = new int[0];
        private long[] _value = new long[0];
        private readonly RotaStockRun _stockRun = new RotaStockRun();

        /// <summary>
        /// True for a rung that may be written into the game: the best week of the people there are, when it is not
        /// the week as it is. A rung that takes somebody still to be hired is never written.
        /// </summary>
        public static bool Writable(RotaRung rung)
        {
            return rung != null && rung.Hires == 0 && rung.Kind == RotaRungKind.Best && rung.Source != 0 && rung.Usable;
        }

        // ---------------------------------------------------------------- the game's arithmetic, a figure at a time

        /// <summary>The game's cleanliness for a mean dirt, both in thousandths of a point: dirt that shows (five points and more) counts twice.</summary>
        public static int CleanOf(int dirt)
        {
            int clean = 100000 - dirt - (dirt >= 5000 ? dirt : 0);
            return clean < 0 ? 0 : clean;
        }

        /// <summary>
        /// What a customer leaves at a mean dirt, in thousandths of the shop's margin: cleanliness is a quarter of
        /// the satisfaction, and what customers buy goes with (satisfaction + 50) / 100.
        /// </summary>
        public static int Factor(RotaShop shop, int dirt)
        {
            if (shop.DirtPerCustomer <= 0)
            {
                return 1000;
            }
            int factor = 1000 + (CleanOf(dirt) - shop.CleanNow * 1000) / (4 * (50 + Math.Max(0, shop.Content)));
            return factor < 0 ? 0 : factor;
        }

        /// <summary>What the game's theft takes in an hour so many customers come.</summary>
        public static long Theft(RotaShop shop, int come)
        {
            return shop.TheftRate <= 0 || come <= 0 || (shop.TheftLimit > 0 && come >= shop.TheftLimit) ? 0L : (long)come * shop.TheftRate;
        }

        /// <summary>
        /// What one employee's unmet demands cost in a week. The weights are the game's (tenths of a point of
        /// satisfaction an hour); <paramref name="value"/> is what the customers they serve leave in a week,
        /// <paramref name="wages"/> what they are paid for it. <paramref name="quitHours"/>: the hours until their
        /// satisfaction is at nothing and they resign; 0 if it settles above that.
        /// </summary>
        public static long DemandCost(int unmetWeight, int metWeight, int unmet, int demands, int mood, long value, long wages, int skill, bool serves, int content, out int quitHours)
        {
            quitHours = 0;
            if (unmet <= 0 || demands <= 0)
            {
                return 0;
            }
            int rate = Math.Min(6, unmetWeight - metWeight);    // tenths of a point an hour; the game moves it by no more than 0.6
            int share = (demands - unmet) * 100 / demands;
            int cap = share <= 34 ? 60 : share <= 67 ? 80 : 100;
            mood = mood < 1 ? 1 : mood > 100 ? 100 : mood;
            long quit = 0;
            int lost;
            if (rate > 0)
            {
                quitHours = Math.Max(1, mood * 10 / rate);
                long replace = ReplaceDays * Math.Max(value - wages, wages) / 7;
                quit = replace * 168 / Math.Max(168, quitHours);
                lost = 100 - mood / 2;      // on the way down, half way on average
            }
            else
            {
                lost = 100 - cap;           // it settles at the most the game allows with so many demands unmet
            }
            long worse = serves ? value * Math.Max(0, skill) * lost / (400L * (50 + Math.Max(0, content))) : wages * lost / 100;
            return quit + worse;
        }

        // ---------------------------------------------------------------- what a week comes to

        /// <summary>
        /// Values a week: the hours <paramref name="open"/> with the rota <paramref name="plan"/> (rated or made by
        /// the solver against <paramref name="problem"/>, so that it says which demands it leaves unmet).
        /// </summary>
        public void Evaluate(RotaAdvisor advisor, RotaShop shop, RotaProblem problem, bool[] open, RotaPlan plan, RotaRung into, RotaProblem asked = null)
        {
            int stations = shop.Stations, workers = problem.Workers;
            short[] at = plan.At;
            into.Size(workers);
            if (_value.Length < workers)
            {
                _value = new long[workers];
                _unmetWeight = new int[workers];
                _unmetCount = new int[workers];
            }
            Array.Clear(_value, 0, _value.Length);
            Array.Clear(_unmetWeight, 0, _unmetWeight.Length);
            Array.Clear(_unmetCount, 0, _unmetCount.Length);
            var sum = new RotaWeekSum();
            for (int d = 0; d < Days; d++)
            {
                _dayOpen[d] = false;
                for (int h = 0; h < Hours; h++)
                {
                    _dayOpen[d] |= open[d * Hours + h];
                }
            }
            bool model = shop.HasCustomers && shop.DirtPerCustomer > 0 && !shop.Cleaned;
            RotaStock stock = shop.Stock;
            bool goods = shop.HasCustomers && stock.Any;    // some goods sell only while they are there
            into.Stock.Clear(goods ? stock.Products : 0);
            if (goods)
            {
                _stockRun.Start(stock);
            }
            int dirt = 0, spotless = Factor(shop, 0);
            for (int pass = model || goods ? 0 : 1; pass < 2; pass++)
            {
                for (int cell = 0; cell < Cells; cell++)
                {
                    bool isOpen = open[cell];
                    int cleaners = 0, crew = 0;
                    long wage = 0;
                    for (int s = 0; s < stations; s++)
                    {
                        int i = cell * stations + s, w = at[i];
                        if (w < 0 && !shop.Taken[i])
                        {
                            continue;
                        }
                        bool cleans = shop.GroupKind[shop.Group[s]] == RotaGroupKind.Cleaning;
                        cleaners += cleans ? 1 : 0;
                        crew += !cleans && w >= 0 && w < workers ? 1 : 0;
                        wage += w >= 0 && w < workers ? problem.Worker[w].Wage : shop.StationWage[s];
                    }
                    if (_dayOpen[cell / Hours])
                    {
                        dirt = Math.Max(0, dirt - cleaners * shop.CleanPerHour);   // (the game cleans on a day the business opens: BuildingCleanlinessHelper.RunHourly)
                    }
                    int come = isOpen ? Math.Max(shop.Customers[cell], shop.Wanting[cell]) : 0;
                    int served = isOpen ? advisor.Served(shop, cell, at, null, false) : 0;
                    int factor = model ? Factor(shop, dirt) : 1000;
                    dirt = (int)Math.Min(100000L, dirt + (long)served * shop.DirtPerCustomer);
                    long sales;
                    bool shortHour = false;
                    if (goods)
                    {
                        // What the round and the delivery bring at the start of the hour, then what its customers find.
                        RotaStockWeek counted = pass == 1 ? into.Stock : null;
                        _stockRun.Arrive(cell, counted);
                        sales = _stockRun.Sell(cell, served, factor, counted);
                        shortHour = _stockRun.Short;
                    }
                    else
                    {
                        sales = (long)served * shop.Margin * factor / 1000;
                    }
                    if (pass == 0)
                    {
                        continue;
                    }
                    long theft = isOpen ? Theft(shop, come) : 0;
                    bool manned = isOpen && advisor.Crewed(shop, cell, at);
                    sum.Hours += isOpen ? 1 : 0;
                    sum.Manned += manned ? 1 : 0;
                    sum.Come += come;
                    sum.Served += served;
                    sum.LostUnmanned += served == 0 ? come : 0;
                    sum.LostFull += served > 0 ? come - served : 0;
                    sum.Sales += sales;
                    // What a spotless floor would add: nothing in an hour some goods ran out in (there was no more of them to sell).
                    sum.Clean += !model || shortHour ? 0 : !goods ? (long)served * shop.Margin * (spotless - factor) / 1000
                        : factor > 0 ? sales * (spotless - factor) / factor : (long)served * shop.Margin * spotless / 1000;
                    sum.Wages += wage;
                    sum.Theft += theft;
                    into.Come[cell] = come;
                    into.Served[cell] = served;
                    into.Earn[cell] = (int)Math.Min(int.MaxValue / 2, sales);
                    into.Cost[cell] = (int)Math.Min(int.MaxValue / 2, wage + theft);
                    for (int s = 0; s < stations; s++)
                    {
                        int w = at[cell * stations + s];
                        if (w < 0 || w >= workers)
                        {
                            continue;
                        }
                        into.HoursOf[w]++;
                        sum.StaffHours++;
                        if (shop.GroupKind[shop.Group[s]] == RotaGroupKind.Cleaning)
                        {
                            sum.CleanHours++;
                        }
                        else if (crew > 0)
                        {
                            _value[w] += sales / crew;
                        }
                    }
                }
            }
            for (int d = 0; d < Days; d++)
            {
                sum.Fees += _dayOpen[d] ? shop.DayFee[d] : 0;
                sum.Night += shop.NightRisk > 0 && !open[d * Hours + 1] ? shop.NightRisk : 0;
            }
            for (int i = 0; i < plan.Unmet.Count; i++)
            {
                RotaUnmet unmet = plan.Unmet[i];
                if (unmet.Worker >= 0 && unmet.Worker < workers && unmet.Demand >= 0 && unmet.Demand < Asked(problem, asked, unmet.Worker).Length)
                {
                    _unmetWeight[unmet.Worker] += Weight(Asked(problem, asked, unmet.Worker)[unmet.Demand].Priority);
                    _unmetCount[unmet.Worker]++;
                }
            }
            for (int w = 0; w < workers; w++)
            {
                RotaWorker worker = problem.Worker[w];
                if (_unmetCount[w] == 0)
                {
                    continue;
                }
                int all = 0, skill = 0, quit;
                RotaDemand[] demands = Asked(problem, asked, w);
                for (int k = 0; k < demands.Length; k++)
                {
                    all += Weight(demands[k].Priority);
                }
                for (int s = 0; s < stations; s++)
                {
                    skill = Math.Max(skill, problem.Can[w * stations + s]);
                }
                long cost = DemandCost(_unmetWeight[w], all - _unmetWeight[w], _unmetCount[w], demands.Length, worker.Mood, _value[w], (long)into.HoursOf[w] * worker.Wage, skill, _value[w] > 0,
                    shop.Content, out quit);
                into.DemandCost[w] = cost;
                into.QuitHours[w] = quit;
                sum.Demands += cost;
            }
            if (goods)
            {
                sum.StockLost = into.Stock.LostAll;
                sum.StockShort = into.Stock.Short;
            }
            sum.Net = sum.Sales - sum.Wages - sum.Fees - sum.Theft - sum.Night - sum.Demands;
            into.Sum = sum;
        }

        // What somebody asks for with the weights the game gives it: a problem made stricter for a plan keeps the same demands in the same order.
        private static RotaDemand[] Asked(RotaProblem problem, RotaProblem asked, int worker)
        {
            return asked != null && worker < asked.Workers ? asked.Worker[worker].Demands : problem.Worker[worker].Demands;
        }

        private static int Weight(int priority)
        {
            return RotaCatalogue.DemandWeight[priority < 0 ? 0 : priority >= RotaCatalogue.DemandWeight.Length ? RotaCatalogue.DemandWeight.Length - 1 : priority];
        }

        // ---------------------------------------------------------------- cleaning hours, by what each brings

        /// <summary>
        /// What the customers of a week leave with so many cleaners in each hour, starting at a mean dirt:
        /// <paramref name="served"/> customers an hour, <paramref name="clean"/> cleaners an hour.
        /// </summary>
        public static long CleanSales(RotaShop shop, bool[] open, int[] served, int[] clean, int dirt, out int end)
        {
            long sales = 0;
            for (int d = 0; d < Days; d++)
            {
                bool any = false;
                for (int h = 0; h < Hours; h++)
                {
                    any |= open[d * Hours + h];
                }
                for (int h = 0; h < Hours; h++)
                {
                    int cell = d * Hours + h;
                    if (any && clean[cell] > 0)
                    {
                        dirt = Math.Max(0, dirt - clean[cell] * shop.CleanPerHour);
                    }
                    if (served[cell] > 0)
                    {
                        sales += (long)served[cell] * shop.Margin * Factor(shop, dirt) / 1000;
                        dirt = (int)Math.Min(100000L, dirt + (long)served[cell] * shop.DirtPerCustomer);
                    }
                }
            }
            end = dirt;
            return sales;
        }

        /// <summary>The same week as it settles: run once to find how dirty it starts, then counted.</summary>
        public static long CleanWeek(RotaShop shop, bool[] open, int[] served, int[] clean)
        {
            int warm, end;
            CleanSales(shop, open, served, clean, 0, out warm);
            return CleanSales(shop, open, served, clean, warm, out end);
        }

        internal static void CopyPlan(RotaPlan from, RotaPlan to)
        {
            to.Size(from.Stations);
            Array.Copy(from.At, to.At, Cells * from.Stations);
            to.Level = from.Level;
            to.WantedValue = from.WantedValue;
            to.CoveredValue = from.CoveredValue;
            to.WantedHours = from.WantedHours;
            to.CoveredHours = from.CoveredHours;
            to.UnmetCount = from.UnmetCount;
            to.UnmetWeight = from.UnmetWeight;
            to.Wage = from.Wage;
            to.Shifts = from.Shifts;
            to.Unmet.AddRange(from.Unmet);
            to.Gaps.AddRange(from.Gaps);
        }

        internal static void CopyRung(RotaRung from, RotaRung to)
        {
            to.Size(from.Workers);
            to.Kind = from.Kind;
            to.Source = from.Source;
            to.Hires = from.Hires;
            to.Staff = from.Staff;
            Array.Copy(from.Hire, to.Hire, from.Hire.Length);
            Array.Copy(from.Open, to.Open, Cells);
            CopyPlan(from.Plan, to.Plan);
            to.Sum = from.Sum;
            to.Gain = from.Gain;
            to.OneOff = from.OneOff;
            to.WeeksBack = from.WeeksBack;
            Array.Copy(from.Come, to.Come, Cells);
            Array.Copy(from.Served, to.Served, Cells);
            Array.Copy(from.Earn, to.Earn, Cells);
            Array.Copy(from.Cost, to.Cost, Cells);
            Array.Copy(from.HoursOf, to.HoursOf, from.Workers);
            Array.Copy(from.DemandCost, to.DemandCost, from.Workers);
            Array.Copy(from.QuitHours, to.QuitHours, from.Workers);
            to.Changes.Clear();
            to.Changes.AddRange(from.Changes);
            to.Left.AddRange(from.Left);
            to.Stock.CopyFrom(from.Stock);
            to.Out = from.Out;
            to.FitsFor = from.FitsFor;
        }
    }
}
