using System;
using System.Diagnostics;
using RealLifeOverhaul.Economy;

// The staff rota planner's checks (Scripts/Economy/Company/RotaModel.cs, RotaSolver.cs, RotaAdvice.cs): every kind of
// demand, the game's limits on a shift and a day, skills, one place at a time, the headquarters rule, what gives way
// when a full rota is impossible and what is said about it, the empty cases, the hours worth opening, an hour's crew,
// the people a week takes, that the same business always gets the same rota, how long the largest business takes,
// and generated businesses against a plain "fill in order" rota. The rules of a rota are checked here a second
// time, by code of this file that shares nothing with the solver.
internal static partial class Program
{
    private const int RotaCells = RotaCatalogue.Cells;
    private static readonly RotaSolver RotaSolverUnderTest = new RotaSolver();

    private static void Rota()
    {
        RotaCatalogueChecks();
        RotaShifts();
        RotaDemands();
        RotaGivingWay();
        RotaEmptyCases();
        RotaWholeWeek();
        RotaChoices();
        RotaCrew();
        RotaOpeningHours();
        RotaWeekAndPeople();
        RotaAdvice();
        RotaGenerated();
        RotaTiming();
    }

    // ---------------------------------------------------------------- making a problem by hand

    private static RotaProblem RotaMake(int stations, int workers)
    {
        var p = new RotaProblem();
        p.Size(stations, workers);
        for (int w = 0; w < workers; w++)
        {
            p.Worker[w].Wage = 1500;
            for (int s = 0; s < stations; s++)
            {
                p.Can[w * stations + s] = 50;
            }
        }
        return p;
    }

    // Wants a station for hours from to to on the days of a mask (bit 0 Monday), each hour worth so much.
    private static void RotaWant(RotaProblem p, int station, int days, int from, int to, int value = 100)
    {
        for (int d = 0; d < 7; d++)
        {
            for (int h = from; h < to && ((days >> d) & 1) != 0; h++)
            {
                p.Value[(d * 24 + h) * p.Stations + station] = value;
            }
        }
    }

    private static RotaDemand RotaAsk(RotaDemandKind kind, int priority, int a = 0, int b = 0, ulong mask = 0)
    {
        return new RotaDemand { Kind = kind, Priority = priority, A = a, B = b, Mask = mask };
    }

    private static int RotaHoursOf(RotaPlan plan, int worker, int day = -1, int station = -1)
    {
        int n = 0;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            for (int s = 0; s < plan.Stations; s++)
            {
                n += plan.At[cell * plan.Stations + s] == worker && (day < 0 || cell / 24 == day) && (station < 0 || s == station) ? 1 : 0;
            }
        }
        return n;
    }

    private static int RotaDaysOf(RotaPlan plan, int worker)
    {
        int n = 0;
        for (int d = 0; d < 7; d++)
        {
            n += RotaHoursOf(plan, worker, d) > 0 ? 1 : 0;
        }
        return n;
    }

    private static int RotaLongestShift(RotaPlan plan)
    {
        int longest = 0;
        for (int d = 0; d < 7; d++)
        {
            for (int s = 0; s < plan.Stations; s++)
            {
                int run = 0, last = -1;
                for (int h = 0; h < 24; h++)
                {
                    int w = plan.At[(d * 24 + h) * plan.Stations + s];
                    run = w >= 0 && w == last ? run + 1 : w >= 0 ? 1 : 0;
                    last = w;
                    longest = Math.Max(longest, run);
                }
            }
        }
        return longest;
    }

    private static int RotaShortestShift(RotaPlan plan)
    {
        int shortest = 99;
        for (int d = 0; d < 7; d++)
        {
            for (int s = 0; s < plan.Stations; s++)
            {
                int run = 0, last = -1;
                for (int h = 0; h <= 24; h++)
                {
                    int w = h < 24 ? plan.At[(d * 24 + h) * plan.Stations + s] : -1;
                    if (w != last && last >= 0)
                    {
                        shortest = Math.Min(shortest, run);
                    }
                    run = w >= 0 && w == last ? run + 1 : 1;
                    last = w;
                }
            }
        }
        return shortest;
    }

    // ---------------------------------------------------------------- the rules, checked a second time

    /// <summary>
    /// What is wrong with a rota, by this file's own reading of the rules; null if nothing is. Limits of demands
    /// are checked for every demand at level 0 and for those of high priority at level 1.
    /// </summary>
    private static string RotaWrong(RotaProblem p, RotaPlan plan)
    {
        int S = p.Stations;
        for (int w = 0; w < p.Workers; w++)
        {
            int week = 0, days = 0;
            var hoursAt = new int[S];
            for (int d = 0; d < 7; d++)
            {
                int day = 0;
                for (int h = 0; h < 24; h++)
                {
                    int here = 0;
                    for (int s = 0; s < S; s++)
                    {
                        if (plan.At[(d * 24 + h) * S + s] != w)
                        {
                            continue;
                        }
                        here++;
                        hoursAt[s]++;
                        if (p.Can[w * S + s] == 0)
                        {
                            return "worker " + w + " stands at station " + s + " without a skill for it";
                        }
                        if (p.Value[(d * 24 + h) * S + s] <= 0)
                        {
                            return "worker " + w + " stands at station " + s + " in an hour nobody is wanted there (day " + d + ", " + h + ")";
                        }
                    }
                    if (here > 1)
                    {
                        return "worker " + w + " is in " + here + " places at once (day " + d + ", " + h + ")";
                    }
                    day += here;
                }
                if (day > RotaCatalogue.DayCap)
                {
                    return "worker " + w + " works " + day + " hours on day " + d;
                }
                week += day;
                days += day > 0 ? 1 : 0;
            }
            foreach (RotaDemand q in p.Worker[w].Demands)
            {
                if (plan.Level > 0 && q.Priority < RotaCatalogue.PriorityHigh)
                {
                    continue;
                }
                ulong can = 0;
                for (int s = 0; s < S; s++)
                {
                    can |= p.Can[w * S + s] != 0 ? 1UL << s : 0;
                }
                for (int cell = 0; cell < RotaCells; cell++)
                {
                    for (int s = 0; s < S; s++)
                    {
                        if (plan.At[cell * S + s] != w)
                        {
                            continue;
                        }
                        bool broken = (q.Kind == RotaDemandKind.FreeDays && ((q.Mask >> (cell / 24)) & 1) != 0) || (q.Kind == RotaDemandKind.NoHours && ((q.Mask >> (cell % 24)) & 1) != 0)
                                      || (q.Kind == RotaDemandKind.NoShiftKind && ((q.Mask >> p.Kind[s]) & 1) != 0) || (q.Kind == RotaDemandKind.OnStation && (q.Mask & can) != 0 && ((q.Mask >> s) & 1) == 0);
                        if (broken)
                        {
                            return "worker " + w + "'s demand " + q.Kind + " (priority " + q.Priority + ") is broken at level " + plan.Level;
                        }
                    }
                }
                if ((q.Kind == RotaDemandKind.Hours && q.B > 0 && week > q.B) || (q.Kind == RotaDemandKind.Days && q.A > 0 && days > q.A))
                {
                    return "worker " + w + " is over their " + q.Kind + " (" + week + " hours, " + days + " days) at level " + plan.Level;
                }
            }
        }
        if (RotaLongestShift(plan) > RotaCatalogue.ShiftCap)
        {
            return "a shift of " + RotaLongestShift(plan) + " hours";
        }
        for (int s = 0; s < S; s++)
        {
            int who = -2, wanted = 0, held = 0;
            for (int cell = 0; cell < RotaCells && p.Whole[s]; cell++)
            {
                if (p.Value[cell * S + s] <= 0)
                {
                    continue;
                }
                wanted++;
                int w = plan.At[cell * S + s];
                held += w >= 0 ? 1 : 0;
                if (who == -2)
                {
                    who = w;
                }
                else if (who != w)
                {
                    return "station " + s + " is for one person all week and has more than one, or one for part of it";
                }
            }
            if (held != 0 && held != wanted)
            {
                return "station " + s + " is for one person all week and is held for part of it";
            }
        }
        return null;
    }

    private static RotaPlan RotaSolve(RotaProblem p, string what)
    {
        RotaPlan plan = RotaSolverUnderTest.Solve(p, new RotaPlan());
        string wrong = RotaWrong(p, plan);
        Check(wrong == null, "rota, " + what + ": the rules hold (" + wrong + ")");
        return plan;
    }

    // ---------------------------------------------------------------- the checks

    private static void RotaCatalogueChecks()
    {
        Check(RotaCatalogue.ShiftCap == 12 && RotaCatalogue.DayCap == 14, "rota: the game's limits, a shift of twelve hours and a day of fourteen");
        Check(RotaCatalogue.DemandWeight[0] == 2 && RotaCatalogue.DemandWeight[1] == 3 && RotaCatalogue.DemandWeight[2] == 5, "rota: an unmet demand weighs as the game's own impact, two, three and five tenths");
        Check(RotaCatalogue.BlocksMost >= 1 && RotaCatalogue.BlockLeast >= 1 && RotaCatalogue.BlockLeast * RotaCatalogue.BlocksMost <= 24, "rota: the advised blocks fit a day");
        Check(RotaCatalogue.ValueSupport > 0 && RotaCatalogue.ValueFloor > RotaCatalogue.ValueSupport, "rota: the smallest crew is worth more than a support station, and both are wanted");
    }

    private static void RotaShifts()
    {
        RotaProblem p = RotaMake(1, 1);
        RotaWant(p, 0, 0x7F, 9, 17);
        RotaPlan plan = RotaSolve(p, "one person, one station, 9 to 17");
        Check(plan.Full && plan.CoveredHours == 56 && plan.Shifts == 7, "rota: one person covers 9 to 17 all week in seven shifts (" + plan.CoveredHours + " hours, " + plan.Shifts + " shifts)");
        Check(plan.Wage == 56L * 1500, "rota: the week's wages are the hours at the hourly wage (" + plan.Wage + ")");
        Check(plan.UnmetCount == 0 && plan.Gaps.Count == 0 && plan.Level == 0, "rota: nothing unmet and nothing uncovered");

        p = RotaMake(1, 1);
        RotaWant(p, 0, 0x01, 6, 22);
        plan = RotaSolve(p, "one person, sixteen hours");
        Check(RotaLongestShift(plan) <= 12 && RotaHoursOf(plan, 0, 0) <= 14, "rota: a shift is never longer than twelve hours and a day never longer than fourteen (" + RotaLongestShift(plan) + ", " + RotaHoursOf(plan, 0, 0) + ")");
        Check(plan.CoveredHours == 14 && plan.Gaps.Count >= 1 && plan.Gaps[0].Reason == RotaReason.Stretched, "rota: one person at one station covers fourteen of sixteen hours in two shifts, and the rest is told as a gap (" + plan.CoveredHours + ")");

        p = RotaMake(2, 1);
        RotaWant(p, 0, 0x01, 6, 22);
        RotaWant(p, 1, 0x01, 6, 22);
        plan = RotaSolve(p, "one person, two stations");
        Check(RotaHoursOf(plan, 0, 0) == 14, "rota: with a second station to move to, one person works the fourteen hours a day allows and no more (" + RotaHoursOf(plan, 0, 0) + ")");

        p = RotaMake(1, 2);
        RotaWant(p, 0, 0x01, 6, 22);
        plan = RotaSolve(p, "two people, sixteen hours");
        Check(plan.Full && plan.Shifts == 2 && RotaShortestShift(plan) == 8, "rota: two people share sixteen hours as two shifts of eight, not twelve and four (" + plan.Shifts + " shifts, shortest " + RotaShortestShift(plan) + ")");

        p = RotaMake(1, 2);
        RotaWant(p, 0, 0x01, 8, 21);
        plan = RotaSolve(p, "two people, thirteen hours");
        Check(plan.Full && RotaShortestShift(plan) >= 6, "rota: thirteen hours are cut seven and six, no odd hour left over (shortest " + RotaShortestShift(plan) + ")");

        p = RotaMake(2, 1);
        RotaWant(p, 0, 0x7F, 9, 17);
        RotaWant(p, 1, 0x7F, 9, 17);
        plan = RotaSolve(p, "one person, two stations at the same hours");
        Check(plan.CoveredHours == 56, "rota: nobody is in two places at once (56 of 112 hours covered, got " + plan.CoveredHours + ")");

        p = RotaMake(2, 2);
        p.Can[0 * 2 + 1] = 0;
        p.Can[1 * 2 + 0] = 0;
        RotaWant(p, 0, 0x7F, 9, 17);
        RotaWant(p, 1, 0x7F, 9, 17);
        plan = RotaSolve(p, "skills");
        Check(plan.Full && RotaHoursOf(plan, 0, -1, 1) == 0 && RotaHoursOf(plan, 1, -1, 0) == 0, "rota: each stands only at the station they have the skill for");

        p = RotaMake(1, 1);
        RotaWant(p, 0, 0x01, 9, 13);
        RotaWant(p, 0, 0x01, 15, 19);
        plan = RotaSolve(p, "two blocks of opening hours");
        Check(plan.Full && plan.Shifts == 2, "rota: a day in two blocks is two shifts for the same person");
    }

    private static void RotaDemands()
    {
        // Hours a week: the most is a limit, the least a goal.
        RotaProblem p = RotaMake(1, 2);
        RotaWant(p, 0, 0x7F, 8, 20);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 10, 20) };
        RotaPlan plan = RotaSolve(p, "hours a week");
        Check(RotaHoursOf(plan, 0) <= 20 && RotaHoursOf(plan, 0) >= 10, "rota: between 10 and 20 hours a week is kept (" + RotaHoursOf(plan, 0) + ")");
        Check(plan.UnmetCount == 0, "rota: and so the demand is met");

        p = RotaMake(1, 2);
        RotaWant(p, 0, 0x1F, 9, 17);
        p.Worker[1].Wage = 3000;
        p.Worker[1].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 1, 24, 40) };
        plan = RotaSolve(p, "least hours against a cheaper colleague");
        Check(RotaHoursOf(plan, 1) >= 24, "rota: somebody who wants at least 24 hours gets them although a colleague is cheaper (" + RotaHoursOf(plan, 1) + ")");
        Check(RotaHoursOf(plan, 0) == 40 - RotaHoursOf(plan, 1) && RotaHoursOf(plan, 1) <= 32, "rota: and the cheaper colleague has the rest (" + RotaHoursOf(plan, 0) + ")");

        p = RotaMake(1, 1);
        RotaWant(p, 0, 0x03, 9, 13);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 30, 40) };
        plan = RotaSolve(p, "least hours that are not there");
        Check(plan.UnmetCount == 1 && plan.Unmet[0].Reason == RotaReason.Short && plan.UnmetWeight == 5, "rota: a least number of hours the business does not need is told as short, at the weight of a high demand");

        // Days a week.
        p = RotaMake(1, 2);
        RotaWant(p, 0, 0x7F, 9, 17);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Days, 2, 3) };
        plan = RotaSolve(p, "days a week");
        Check(RotaDaysOf(plan, 0) == 3 && plan.Full && plan.UnmetCount == 0, "rota: three days a week is three days, and the other covers the rest (" + RotaDaysOf(plan, 0) + ")");

        p = RotaMake(1, 1);
        RotaWant(p, 0, 0x7F, 9, 17);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Days, 2, 5) };
        plan = RotaSolve(p, "days a week, alone");
        Check(RotaDaysOf(plan, 0) == 5 && plan.CoveredHours == 40, "rota: five days a week of high priority leaves two days uncovered rather than be broken");

        // Free days.
        p = RotaMake(1, 2);
        RotaWant(p, 0, 0x7F, 9, 17);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.FreeDays, 1, 0, 0, 0x60) };
        plan = RotaSolve(p, "free days");
        Check(RotaHoursOf(plan, 0, 5) == 0 && RotaHoursOf(plan, 0, 6) == 0 && plan.Full && plan.Level == 0, "rota: Saturday and Sunday free are free, with the other at work then");

        // Hours not worked.
        p = RotaMake(1, 2);
        RotaWant(p, 0, 0x7F, 6, 22);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.NoHours, 0, 0, 0, 0xFFF000) };   // nothing from noon on
        plan = RotaSolve(p, "hours not worked");
        bool none = true;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            none &= plan.At[cell] != 0 || cell % 24 < 12;
        }
        Check(none && plan.Full && plan.Level == 0, "rota: no shift after noon means mornings only");

        // No shift of a kind.
        p = RotaMake(2, 2);
        p.Kind[1] = RotaKind.Cleaning;
        RotaWant(p, 0, 0x7F, 9, 17);
        RotaWant(p, 1, 0x7F, 9, 17);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.NoShiftKind, 1, 0, 0, 1UL << RotaKind.Cleaning) };
        plan = RotaSolve(p, "no cleaning shift");
        Check(RotaHoursOf(plan, 0, -1, 1) == 0 && plan.Full, "rota: no cleaning shifts means the other one cleans");

        // A station asked for.
        p = RotaMake(2, 2);
        RotaWant(p, 0, 0x7F, 9, 17);
        RotaWant(p, 1, 0x7F, 9, 17);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.OnStation, 1, 0, 0, 1UL << 1) };
        plan = RotaSolve(p, "a station asked for");
        Check(RotaHoursOf(plan, 0, -1, 1) == 56 && RotaHoursOf(plan, 0, -1, 0) == 0 && plan.UnmetCount == 0, "rota: somebody who asks for a station works at it and nowhere else");

        p = RotaMake(1, 1);
        RotaWant(p, 0, 0x7F, 9, 17);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.OnStation, 1, 0, 0, 1UL << 5) };
        plan = RotaSolve(p, "a station the business does not have");
        Check(plan.Full && plan.UnmetCount == 1 && plan.Unmet[0].Reason == RotaReason.NoStation, "rota: a station that is not there cannot be had: they work where they can, and it is told");

        // Several at once.
        p = RotaMake(1, 3);
        RotaWant(p, 0, 0x7F, 8, 20);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 20, 30), RotaAsk(RotaDemandKind.Days, 1, 4), RotaAsk(RotaDemandKind.FreeDays, 0, 0, 0, 0x40) };
        p.Worker[1].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 30, 40), RotaAsk(RotaDemandKind.NoHours, 1, 0, 0, 0x0000FF) };
        plan = RotaSolve(p, "several demands");
        Check(RotaHoursOf(plan, 0) >= 20 && RotaHoursOf(plan, 0) <= 30 && RotaDaysOf(plan, 0) == 4 && RotaHoursOf(plan, 0, 6) == 0, "rota: hours, days and a free day together are all kept (" + RotaHoursOf(plan, 0) + " hours, " + RotaDaysOf(plan, 0) + " days)");
        Check(RotaHoursOf(plan, 1) >= 30 && RotaHoursOf(plan, 1) <= 40 && plan.UnmetCount == 0, "rota: and the colleague's (" + RotaHoursOf(plan, 1) + " hours, unmet " + plan.UnmetCount + ")");
    }

    private static void RotaGivingWay()
    {
        // An optional demand gives way where nobody else can cover the hour, and only there.
        RotaProblem p = RotaMake(1, 1);
        RotaWant(p, 0, 0x7F, 9, 17);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.FreeDays, 1, 0, 0, 0x40) };
        RotaPlan plan = RotaSolve(p, "a free day nobody else can cover");
        Check(plan.Level == 1 && plan.Full, "rota: with nobody else for Sunday the optional free day gives way and the week is covered");
        Check(plan.UnmetCount == 1 && plan.Unmet[0].Reason == RotaReason.Needed && plan.Unmet[0].Day == 6 && plan.Unmet[0].Station == 0 && plan.Unmet[0].Worker == 0 && plan.UnmetWeight == 3,
            "rota: and it is told whose, why, on which day and at which station");

        p = RotaMake(1, 2);
        RotaWant(p, 0, 0x7F, 9, 17);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.FreeDays, 1, 0, 0, 0x40) };
        plan = RotaSolve(p, "a free day somebody else can cover");
        Check(plan.Level == 0 && plan.Full && plan.UnmetCount == 0, "rota: with a colleague for Sunday nothing gives way: the strict rota is tried first and kept");

        p = RotaMake(1, 1);
        RotaWant(p, 0, 0x7F, 9, 17);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.FreeDays, 2, 0, 0, 0x40) };
        plan = RotaSolve(p, "a free day of high priority");
        Check(plan.Level == 0 && plan.CoveredHours == 48 && plan.UnmetCount == 0, "rota: a demand of high priority never gives way: Sunday stays uncovered");
        Check(plan.Gaps.Count == 1 && plan.Gaps[0].Day == 6 && plan.Gaps[0].From == 9 && plan.Gaps[0].To == 17 && plan.Gaps[0].Reason == RotaReason.Stretched, "rota: and the gap is told with its day and hours");

        // Low and medium give way together (the game's own step), high does not.
        p = RotaMake(1, 2);
        RotaWant(p, 0, 0x7F, 6, 20);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.NoHours, 0, 0, 0, 0xFFE000) };   // low: nothing from 13:00
        p.Worker[1].Demands = new[] { RotaAsk(RotaDemandKind.NoHours, 2, 0, 0, 0xFFE000) };   // high: the same
        plan = RotaSolve(p, "the same demand at low and at high priority");
        bool highKept = true;
        int lowLate = 0;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            highKept &= plan.At[cell] != 1 || cell % 24 < 13;
            lowLate += plan.At[cell] == 0 && cell % 24 >= 13 ? 1 : 0;
        }
        Check(plan.Level == 1 && highKept && lowLate > 0 && plan.Full, "rota: of two who want their afternoons, the one of low priority works them and the one of high priority never does");
        Check(plan.UnmetCount == 1 && plan.Unmet[0].Worker == 0 && plan.UnmetWeight == 2, "rota: one demand unmet, the low one");

        // The most hours of a week, when optional, gives way too.
        p = RotaMake(1, 1);
        RotaWant(p, 0, 0x1F, 9, 17);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 0, 10, 20) };
        plan = RotaSolve(p, "the most hours, optional");
        Check(plan.Level == 1 && plan.Full && plan.UnmetCount == 1 && plan.Unmet[0].Reason == RotaReason.Needed, "rota: an optional most of 20 hours gives way to cover 40 with nobody else there");

        // What gives way costs least: of two who could cover, the one whose demands stay whole.
        p = RotaMake(1, 2);
        RotaWant(p, 0, 0x40, 9, 17);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.FreeDays, 1, 0, 0, 0x40) };
        p.Worker[1].Demands = new[] { RotaAsk(RotaDemandKind.FreeDays, 0, 0, 0, 0x40) };
        plan = RotaSolve(p, "two who want Sunday free");
        Check(plan.Level == 1 && RotaHoursOf(plan, 1) == 8 && RotaHoursOf(plan, 0) == 0 && plan.UnmetWeight == 2, "rota: of two who want Sunday free the one who minds less works it");

        // Nobody has the skill.
        p = RotaMake(2, 1);
        p.Can[1] = 0;
        RotaWant(p, 0, 0x01, 9, 17);
        RotaWant(p, 1, 0x01, 9, 17);
        plan = RotaSolve(p, "a station nobody has the skill for");
        Check(plan.Gaps.Count == 1 && plan.Gaps[0].Station == 1 && plan.Gaps[0].Reason == RotaReason.NoSkill, "rota: a station nobody can stand at is told as that");
    }

    private static void RotaEmptyCases()
    {
        RotaProblem p = RotaMake(2, 0);
        RotaWant(p, 0, 0x7F, 9, 17);
        RotaPlan plan = RotaSolve(p, "no employees");
        Check(plan.CoveredHours == 0 && plan.WantedHours == 56 && plan.Gaps.Count == 7 && plan.Gaps[0].Reason == RotaReason.NoSkill, "rota: with no employees nothing is covered and every day is a gap");

        p = RotaMake(0, 3);
        plan = RotaSolve(p, "no stations");
        Check(plan.WantedHours == 0 && plan.Shifts == 0 && plan.Full, "rota: with no stations there is nothing to do");

        p = RotaMake(2, 3);
        plan = RotaSolve(p, "closed all week");
        Check(plan.WantedHours == 0 && plan.Shifts == 0 && plan.Wage == 0, "rota: closed all week, nobody is rostered");

        p = RotaMake(0, 0);
        plan = RotaSolve(p, "nothing at all");
        Check(plan.Full && plan.UnmetCount == 0, "rota: an empty business is an empty rota");

        // A business with no customers: every work station every open hour.
        var shop = new RotaShop();
        shop.Size(3, 2);
        shop.GroupKind[0] = RotaGroupKind.Service;
        shop.GroupKind[1] = RotaGroupKind.Cleaning;
        shop.Group[2] = 1;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            shop.Open[cell] = cell / 24 < 5 && cell % 24 >= 8 && cell % 24 < 16;
        }
        p = RotaMake(3, 3);
        p.Kind[2] = RotaKind.Cleaning;
        var advisor = new RotaAdvisor();
        advisor.Grid(shop, shop.Open, p);
        plan = RotaSolve(p, "a business with no customers");
        int work = 0;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            work += (p.Value[cell * 3] == RotaCatalogue.ValueWork ? 1 : 0) + (p.Value[cell * 3 + 1] == RotaCatalogue.ValueWork ? 1 : 0);
        }
        Check(work == 80 && plan.Full, "rota: where no customers come, both work stations are wanted for all 40 open hours and covered (" + work + ")");
        var outlook = new RotaOutlook();
        advisor.Advise(shop, p, RotaSolverUnderTest, null, outlook);
        bool same = true;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            same &= outlook.Best[cell] == shop.Open[cell] && outlook.Staffed[cell] == shop.Open[cell];
        }
        Check(!outlook.Advised && same, "rota: and no hours are advised: it keeps the hours it has");
    }

    private static void RotaWholeWeek()
    {
        RotaProblem p = RotaMake(2, 3);
        p.Whole[0] = p.Whole[1] = true;
        RotaWant(p, 0, 0x1F, 8, 16, RotaCatalogue.ValueWork);
        RotaWant(p, 1, 0x1F, 8, 16, RotaCatalogue.ValueWork);
        RotaPlan plan = RotaSolve(p, "headquarters desks");
        int holder0 = plan.At[8 * 2], holder1 = plan.At[8 * 2 + 1];
        Check(plan.Full && holder0 >= 0 && holder1 >= 0 && holder0 != holder1 && RotaHoursOf(plan, holder0) == 40 && RotaHoursOf(plan, holder1) == 40, "rota: a headquarters desk has one person for the whole week");
        Check(plan.Shifts == 10, "rota: two desks, five days: ten shifts");

        p = RotaMake(1, 1);
        p.Whole[0] = true;
        RotaWant(p, 0, 0x1F, 8, 16, RotaCatalogue.ValueWork);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.FreeDays, 2, 0, 0, 0x10) };
        plan = RotaSolve(p, "a desk nobody can hold all week");
        Check(plan.CoveredHours == 0 && plan.Gaps.Count == 5, "rota: somebody who cannot hold a desk every day does not hold it at all");

        p = RotaMake(2, 2);
        p.Whole[0] = true;
        p.Kind[1] = RotaKind.Cleaning;
        p.Can[0 * 2 + 1] = 0;
        p.Can[1 * 2 + 0] = 0;
        RotaWant(p, 0, 0x1F, 8, 16, RotaCatalogue.ValueWork);
        RotaWant(p, 1, 0x1F, 14, 16, RotaCatalogue.ValueSupport);
        plan = RotaSolve(p, "a desk and a cleaner");
        Check(plan.Full && RotaHoursOf(plan, 1) == 10, "rota: the cleaner of a headquarters is rostered like anybody else");
    }

    private static void RotaChoices()
    {
        // The scarce hours of the one person go where they are worth most.
        RotaProblem p = RotaMake(1, 1);
        RotaWant(p, 0, 0x7F, 9, 17, 10);
        RotaWant(p, 0, 0x20, 9, 17, 500);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 0, 8) };
        RotaPlan plan = RotaSolve(p, "eight hours to give");
        Check(RotaHoursOf(plan, 0, 5) == 8 && plan.CoveredValue == 4000, "rota: eight hours to give go to Saturday, where an hour is worth most");

        p = RotaMake(2, 1);
        RotaWant(p, 0, 0x01, 9, 17, 50);
        RotaWant(p, 1, 0x01, 9, 17, 300);
        plan = RotaSolve(p, "two stations, one person");
        Check(RotaHoursOf(plan, 0, -1, 1) == 8, "rota: of two stations wanted at once, the one worth more is staffed");

        // The cheaper of two; the better of two at one wage.
        p = RotaMake(1, 2);
        RotaWant(p, 0, 0x01, 9, 17);
        p.Worker[0].Wage = 2400;
        plan = RotaSolve(p, "a dearer and a cheaper");
        Check(RotaHoursOf(plan, 1) == 8 && plan.Wage == 8L * 1500, "rota: the cheaper of two takes the shift");

        p = RotaMake(1, 2);
        RotaWant(p, 0, 0x01, 9, 17);
        p.Can[1] = 90;
        plan = RotaSolve(p, "a better hand at the same wage");
        Check(RotaHoursOf(plan, 1) == 8, "rota: at the same wage the better hand takes the shift");

        // The same problem, the same rota.
        p = RotaMake(4, 7);
        for (int s = 0; s < 4; s++)
        {
            RotaWant(p, s, 0x7F, 7 + s, 23 - s, 40 + s * 30);
        }
        p.Worker[2].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 20, 30), RotaAsk(RotaDemandKind.FreeDays, 1, 0, 0, 0x03) };
        p.Worker[5].Demands = new[] { RotaAsk(RotaDemandKind.Days, 1, 4), RotaAsk(RotaDemandKind.NoHours, 0, 0, 0, 0xFC0000) };
        RotaPlan first = RotaSolverUnderTest.Solve(p, new RotaPlan());
        RotaPlan second = RotaSolverUnderTest.Solve(p, new RotaPlan());
        RotaPlan third = new RotaSolver().Solve(p, new RotaPlan());
        bool equal = true;
        for (int i = 0; i < RotaCells * 4; i++)
        {
            equal &= first.At[i] == second.At[i] && first.At[i] == third.At[i];
        }
        Check(equal && first.Wage == second.Wage && first.UnmetWeight == third.UnmetWeight, "rota: the same business gives the same rota, twice from one solver and once from a new one");

        // A rota made elsewhere is rated by the same count.
        var given = new RotaPlan();
        given.Size(4);
        Array.Copy(first.At, given.At, RotaCells * 4);
        RotaSolverUnderTest.Rate(p, given);
        Check(given.CoveredHours == first.CoveredHours && given.Wage == first.Wage && given.Shifts == first.Shifts && given.UnmetCount == first.UnmetCount && given.CoveredValue == first.CoveredValue,
            "rota: a rota handed in is rated as the one that was made");
        var none = new RotaPlan();
        none.Size(4);
        RotaSolverUnderTest.Rate(p, none);
        Check(none.CoveredHours == 0 && none.WantedHours == first.WantedHours && none.Wage == 0 && none.Gaps.Count > 0, "rota: an empty rota covers nothing and costs nothing");
    }

    // A shop of tills that serve 20 customers an hour each, with a margin of 4.00 a customer and 15.00 an hour wages.
    private static RotaShop RotaTills(int tills, int cleaners = 0)
    {
        var shop = new RotaShop();
        shop.Size(tills + cleaners, cleaners > 0 ? 2 : 1);
        shop.GroupKind[0] = RotaGroupKind.Service;
        if (cleaners > 0)
        {
            shop.GroupKind[1] = RotaGroupKind.Cleaning;
        }
        for (int s = 0; s < tills + cleaners; s++)
        {
            shop.Group[s] = s < tills ? 0 : 1;
            shop.Cap[s] = s < tills ? 20 : 0;
            shop.StationWage[s] = 1500;
        }
        shop.Margin = 400;
        shop.Takings = 900;
        shop.HasCustomers = true;
        return shop;
    }

    private static void RotaCrew()
    {
        var advisor = new RotaAdvisor();
        RotaShop shop = RotaTills(3);
        var need = new bool[3];
        int served, wage, count;
        advisor.Crew(shop, 0, need, out served, out wage, out count);
        Check(count == 1 && served == 0 && need[0] && !need[1], "rota crew: with no customers the smallest crew is one till");
        advisor.Crew(shop, 15, need, out served, out wage, out count);
        Check(count == 1 && served == 15 && wage == 1500, "rota crew: 15 customers, a till that serves 20: one till");
        advisor.Crew(shop, 30, need, out served, out wage, out count);
        Check(count == 2 && served == 30 && wage == 3000, "rota crew: 30 customers: a second till serves 10 more, worth 40.00 against 15.00 wages");
        advisor.Crew(shop, 23, need, out served, out wage, out count);
        Check(count == 1 && served == 20, "rota crew: 23 customers: a second till would serve 3 more, worth 12.00 against 15.00 wages, so it is not staffed");
        advisor.Crew(shop, 100, need, out served, out wage, out count);
        Check(count == 3 && served == 60 && need[2], "rota crew: 100 customers: all three tills, and 60 served");

        // Two groups: both are needed, and the smaller sum is the limit.
        var two = new RotaShop();
        two.Size(3, 2);
        two.GroupKind[0] = two.GroupKind[1] = RotaGroupKind.Service;
        two.Group[2] = 1;
        two.Cap[0] = two.Cap[1] = 20;
        two.Cap[2] = 30;
        two.StationWage[0] = two.StationWage[1] = two.StationWage[2] = 1500;
        two.Margin = 400;
        two.HasCustomers = true;
        advisor.Crew(two, 50, need, out served, out wage, out count);
        Check(count == 3 && served == 30, "rota crew: two tills of 20 and a counter of 30: 50 customers, 30 served");
        var p = RotaMake(3, 0);
        var at = new short[RotaCells * 3];
        for (int i = 0; i < at.Length; i++)
        {
            at[i] = -1;
        }
        two.Customers[10] = 50;
        Check(advisor.Served(two, 10, at, p.Value, false) == 0, "rota crew: nobody at work, nobody served");
        at[10 * 3] = 0;
        Check(advisor.Served(two, 10, at, p.Value, false) == 0 && !advisor.Crewed(two, 10, at), "rota crew: a till without the counter serves nobody: the smallest crew is one at each");
        at[10 * 3 + 2] = 0;
        Check(advisor.Served(two, 10, at, p.Value, false) == 20 && advisor.Crewed(two, 10, at), "rota crew: one till and the counter serve 20 of 50");

        // A support station is always wanted and serves nobody.
        var guard = RotaTills(1);
        guard.Size(2, 2);
        guard.GroupKind[0] = RotaGroupKind.Service;
        guard.GroupKind[1] = RotaGroupKind.Support;
        guard.Group[1] = 1;
        guard.Cap[0] = 20;
        guard.StationWage[0] = guard.StationWage[1] = 1500;
        guard.Margin = 400;
        guard.HasCustomers = true;
        var need2 = new bool[2];
        advisor.Crew(guard, 10, need2, out served, out wage, out count);
        Check(need2[0] && need2[1] && served == 10 && wage == 1500, "rota crew: a guard is wanted while it is open, and is not part of the smallest crew's wages");
    }

    private static void RotaOpeningHours()
    {
        var advisor = new RotaAdvisor();
        var net = new int[RotaCells];
        var open = new bool[RotaCells];
        var fee = new int[7];
        for (int cell = 0; cell < RotaCells; cell++)
        {
            net[cell] = -500;
        }
        for (int h = 9; h < 18; h++)
        {
            net[h] = 1000;
        }
        advisor.Blocks(net, null, fee, open);
        bool right = true;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            right &= open[cell] == (cell >= 9 && cell < 18);
        }
        Check(right, "rota hours: the hours that make money are advised, and no hour that loses money");

        // A dip in the middle: kept if the block still makes more whole than cut, cut otherwise.
        net[12] = -300;
        advisor.Blocks(net, null, fee, open);
        Check(!open[12] && open[11] && open[13], "rota hours: an hour that loses money in the middle of the day is closed, as a break between two blocks");
        net[12] = 1000;
        net[13] = -300;
        net[15] = -300;
        advisor.Blocks(net, null, fee, open);
        int blocks = 0, hours = 0;
        for (int h = 0; h < 24; h++)
        {
            blocks += open[h] && (h == 0 || !open[h - 1]) ? 1 : 0;
            hours += open[h] ? 1 : 0;
        }
        Check(blocks == 2 && open[9] && open[17] && (open[13] != open[15]), "rota hours: no more than two blocks a day: of two losing hours one is a break and one is kept open (" + blocks + " blocks, " + hours + " hours)");

        // One good hour alone is not a block.
        for (int cell = 0; cell < RotaCells; cell++)
        {
            net[cell] = -500;
        }
        net[24 + 10] = 5000;
        net[24 + 11] = -100;
        advisor.Blocks(net, null, fee, open);
        Check(open[24 + 10] && (open[24 + 11] || open[24 + 9]) && !open[10], "rota hours: a block is at least two hours, so a good hour takes a neighbour with it if the two still make money");
        net[24 + 10] = 80;
        advisor.Blocks(net, null, fee, open);
        Check(!open[24 + 10], "rota hours: and stays closed if they do not");

        // The day's fee.
        for (int cell = 0; cell < RotaCells; cell++)
        {
            net[cell] = cell % 24 >= 18 && cell % 24 < 23 ? 40000 : -1000;
        }
        fee[2] = 500000;
        advisor.Blocks(net, null, fee, open);
        Check(open[18] && open[24 + 22] && !open[2 * 24 + 18] && !open[2 * 24 + 20], "rota hours: a day that would not make more than its fee stays closed (five hours at 400.00 against a fee of 5,000.00)");
        fee[2] = 150000;
        advisor.Blocks(net, null, fee, open);
        Check(open[2 * 24 + 18], "rota hours: and opens when it does");
        fee[2] = 0;

        // Blocked hours, the edges of the day, and the shape of what is advised.
        var blocked = new bool[RotaCells];
        blocked[20] = true;
        advisor.Blocks(net, blocked, fee, open);
        Check(open[18] && open[19] && !open[20] && open[21] && open[22], "rota hours: an hour nobody can keep is closed and the block is cut around it");
        for (int cell = 0; cell < RotaCells; cell++)
        {
            net[cell] = 700;
            blocked[cell] = false;
        }
        advisor.Blocks(net, null, fee, open);
        Check(open[0] && open[23] && open[6 * 24 + 23], "rota hours: a business that makes money every hour is advised to open from midnight to midnight, each day by itself");
        var random = new Random(4);
        bool shape = true;
        for (int round = 0; round < 200; round++)
        {
            for (int cell = 0; cell < RotaCells; cell++)
            {
                net[cell] = random.Next(-3000, 3000);
            }
            advisor.Blocks(net, null, fee, open);
            for (int d = 0; d < 7; d++)
            {
                int n = 0, run = 0, sum = 0;
                for (int h = 0; h <= 24; h++)
                {
                    bool on = h < 24 && open[d * 24 + h];
                    sum += on ? net[d * 24 + h] : 0;
                    if (on)
                    {
                        run++;
                    }
                    else if (run > 0)
                    {
                        n++;
                        shape &= run >= RotaCatalogue.BlockLeast;
                        run = 0;
                    }
                }
                shape &= n <= RotaCatalogue.BlocksMost && sum >= 0;
            }
        }
        Check(shape, "rota hours: on 200 weeks of chance every day's advice is at most two blocks of at least two hours that together make money");
    }

    private static void RotaWeekAndPeople()
    {
        var advisor = new RotaAdvisor();
        RotaShop shop = RotaTills(2, 1);
        for (int cell = 0; cell < RotaCells; cell++)
        {
            shop.Open[cell] = cell % 24 >= 9 && cell % 24 < 19;
            shop.Customers[cell] = shop.Open[cell] ? (cell / 24 >= 5 ? 35 : 12) : 0;
        }
        shop.DirtEvents = 250;
        shop.CleanHours = 8;      // a hundred and twenty-five dirtyings an hour of cleaning
        RotaProblem p = RotaMake(3, 2);
        p.Kind[2] = RotaKind.Cleaning;
        p.Can[0 * 3 + 2] = 0;
        p.Can[1 * 3 + 2] = 0;
        advisor.Grid(shop, shop.Open, p);
        int first = 0, second = 0, cleaning = 0;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            first += p.Value[cell * 3] > 0 ? 1 : 0;
            second += p.Value[cell * 3 + 1] > 0 ? 1 : 0;
            cleaning += p.Value[cell * 3 + 2] > 0 ? 1 : 0;
        }
        Check(first == 70 && second == 20, "rota week: the first till is wanted for all 70 open hours, the second for the 20 weekend hours with 35 customers (" + first + ", " + second + ")");
        Check(p.Value[(5 * 24 + 10) * 3] == 80 && p.Value[(5 * 24 + 10) * 3 + 1] == 60 && p.Value[10 * 3] == 48, "rota week: an hour at a till is worth the customers it adds: 20 and 15 at 4.00 on Saturday, 12 on Monday");
        // Monday: 120 customers, 300 dirtyings, 2.4 hours of cleaning, so 3; Saturday: 350 customers, 875 dirtyings, 7 hours.
        int monday = 0, saturday = 0;
        for (int h = 0; h < 24; h++)
        {
            monday += p.Value[h * 3 + 2] > 0 ? 1 : 0;
            saturday += p.Value[(5 * 24 + h) * 3 + 2] > 0 ? 1 : 0;
        }
        Check(monday == 3 && saturday == 7 && p.Value[18 * 3 + 2] > 0 && p.Value[15 * 3 + 2] == 0, "rota week: cleaning by the dirt the customers bring, at the end of the day (" + monday + " hours on Monday, " + saturday + " on Saturday)");
        Check(cleaning == 5 * 3 + 2 * 7, "rota week: " + cleaning + " hours of cleaning in the week");

        // People: 70 + 20 hours at the tills, two people good for 40 each: three needed, two there.
        var roles = new System.Collections.Generic.List<RotaRole>();
        advisor.Roles(shop, p, roles);
        Check(roles.Count == 2 && roles[0].Hours == 90 && roles[0].Peak == 2 && roles[0].Need == 3 && roles[0].Have == 2 && roles[0].Hire == 1 && roles[0].Spare == 0,
            "rota people: 90 till hours a week take three people at 40 hours each; two are there, so one to hire (" + (roles.Count > 0 ? roles[0].Need + " needed, " + roles[0].Have + " there" : "none") + ")");
        Check(roles.Count == 2 && roles[0].Cost == 90L * 1500 / 3, "rota people: who is hired costs a third of the till hours' wages");
        Check(roles.Count == 2 && roles[1].Hours == 29 && roles[1].Need == 1 && roles[1].Have == 0 && roles[1].Hire == 1, "rota people: 29 hours of cleaning take one cleaner, and there is none");
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 10, 20) };
        p.Worker[1].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 10, 20) };
        advisor.Roles(shop, p, roles);
        Check(roles[0].Need == 5 && roles[0].Hire == 3, "rota people: the same hours with people who work 20 a week at most take five");
        RotaProblem many = RotaMake(3, 6);
        many.Kind[2] = RotaKind.Cleaning;
        advisor.Grid(shop, shop.Open, many);
        advisor.Roles(shop, many, roles);
        Check(roles[0].Have == 6 && roles[0].Spare == 3 && roles[0].Hire == 0, "rota people: six who can stand at a till where three are needed: three to spare");

        // A week in money.
        RotaPlan plan = RotaSolverUnderTest.Solve(p, new RotaPlan());
        long profit = advisor.Profit(shop, p, shop.Open, plan.At, false);
        long served = 0;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            served += advisor.Served(shop, cell, plan.At, p.Value, false);
        }
        Check(profit == served * 400 - plan.Wage, "rota money: a week is the customers served at their margin less the wages (" + profit + ")");
        Check(plan.CoveredHours == 40 && served > 0, "rota money: two people of 20 hours cover 40 hours between them");
        long full = advisor.Profit(shop, p, shop.Open, null, true);
        Check(full == (5 * 10 * 12 + 2 * 10 * 35) * 400L - (90 + 29) * 1500L, "rota money: with everybody the hours need, 1,300 customers at 4.00 less 119 hours at 15.00 (" + full + ")");
        shop.DayFee[5] = 100000;
        Check(advisor.Profit(shop, p, shop.Open, null, true) == full - 100000, "rota money: a day's fee comes off a day it opens");
        shop.DayFee[5] = 0;

        // One more till.
        var missing = new System.Collections.Generic.List<RotaMissing>();
        for (int h = 9; h < 19; h++)
        {
            shop.Wanting[5 * 24 + h] = 70;   // (the game's forecast for a shop stops at the 40 its tills serve: 70 would come)
            shop.Customers[5 * 24 + h] = 40;
        }
        advisor.Missing(shop, shop.Open, missing);
        Check(missing.Count == 1 && missing[0].Day == 5 && missing[0].Customers == 200 && missing[0].Group == 0, "rota stations: with 70 customers an hour wanting to come on Saturday and two tills of 20, a third would serve 200 more that day");
        shop.GroupRoom[0] = 50;
        advisor.Missing(shop, shop.Open, missing);
        Check(missing.Count == 1 && missing[0].Customers == 100, "rota stations: and no more than the rest of the building allows: with shelves for 50 customers an hour, 100 more");
        shop.GroupRoom[0] = 40;
        advisor.Missing(shop, shop.Open, missing);
        Check(missing.Count == 0, "rota stations: where the shelves are the limit, no till is missing");
        shop.GroupRoom[0] = 0;
        for (int h = 9; h < 19; h++)
        {
            shop.Customers[5 * 24 + h] = shop.Wanting[5 * 24 + h] = 35;
        }
        advisor.Missing(shop, shop.Open, missing);
        Check(missing.Count == 0, "rota stations: with customers the tills can serve, no till is missing");
    }

    private static void RotaAdvice()
    {
        var advisor = new RotaAdvisor();
        RotaShop shop = RotaTills(2);
        for (int cell = 0; cell < RotaCells; cell++)
        {
            int h = cell % 24;
            shop.Open[cell] = h >= 8 && h < 12;                                                 // it keeps four hours in the morning now
            shop.Customers[cell] = h >= 10 && h < 20 ? 30 : h >= 6 && h < 22 ? 3 : 0;         // worth opening 10 to 20: 30 customers at 4.00 against wages
        }
        RotaProblem p = RotaMake(2, 4);
        var present = new short[RotaCells * 2];
        for (int i = 0; i < present.Length; i++)
        {
            present[i] = -1;
        }
        for (int d = 0; d < 7; d++)
        {
            for (int h = 8; h < 12; h++)
            {
                present[(d * 24 + h) * 2] = 0;
            }
        }
        var outlook = new RotaOutlook();
        advisor.Advise(shop, p, RotaSolverUnderTest, present, outlook);
        bool hours = true;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            hours &= outlook.Best[cell] == (cell % 24 >= 10 && cell % 24 < 20);
        }
        Check(outlook.Advised && hours && outlook.HoursBest == 70, "rota advice: 10 to 20 is advised, the hours in which the customers pay for the crew (" + outlook.HoursBest + " hours)");
        Check(outlook.Net[10] == 30 * 400 - 3000 && outlook.Crew[10] == 2 && outlook.Net[8] == 3 * 400 - 1500 && outlook.Crew[8] == 1, "rota advice: an hour's worth is its customers' margin less its crew's wages");
        Check(outlook.HoursNow == 28 && outlook.ProfitNow == 7 * (2 * 3 + 2 * 20) * 400L - 28 * 1500L, "rota advice: as it stands, 8 to 12 with one at a till (" + outlook.ProfitNow + ")");
        Check(outlook.ProfitBest == 70 * (30 * 400L - 3000), "rota advice: best with all the staff it takes: 70 hours of 30 customers and two tills (" + outlook.ProfitBest + ")");
        Check(outlook.HoursStaffed == 70 && outlook.ProfitStaffed == outlook.ProfitBest && outlook.Plan.Full, "rota advice: four people keep those hours, so the present staff's best is the same");
        Check(outlook.ProfitBest > outlook.ProfitNow && outlook.CustomersWeek == 7 * (10 * 30 + 6 * 3), "rota advice: and better than now; the week's customers are counted");
        string wrong = RotaWrong(p, outlook.Plan);
        Check(wrong == null, "rota advice: the rota that comes with it keeps the rules (" + wrong + ")");

        // How long the staff there are can keep it open.
        p = RotaMake(2, 1);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 20, 40) };
        advisor.Advise(shop, p, RotaSolverUnderTest, present, outlook);
        Check(outlook.HoursBest == 70 && outlook.HoursStaffed <= 40 && outlook.HoursStaffed >= 30, "rota advice: one person of 40 hours a week keeps it open for no more than 40 of the 70 advised hours (" + outlook.HoursStaffed + ")");
        bool inside = true, crewed = true;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            inside &= !outlook.Staffed[cell] || outlook.Best[cell];
            crewed &= !outlook.Staffed[cell] || outlook.Plan.At[cell * 2] >= 0 || outlook.Plan.At[cell * 2 + 1] >= 0;
        }
        Check(inside && crewed, "rota advice: those hours are among the advised ones, and somebody is at a till in each");
        Check(outlook.ProfitStaffed < outlook.ProfitBest && outlook.ProfitStaffed > outlook.ProfitNow, "rota advice: which makes less than hiring would and more than now (" + outlook.ProfitStaffed + ")");
        Check(outlook.Roles.Count == 1 && outlook.Roles[0].Hire >= 3, "rota advice: and the people to hire are counted (" + (outlook.Roles.Count > 0 ? outlook.Roles[0].Hire : -1) + ")");

        // Nobody at all.
        p = RotaMake(2, 0);
        advisor.Advise(shop, p, RotaSolverUnderTest, null, outlook);
        Check(outlook.HoursBest == 70 && outlook.HoursStaffed == 0 && outlook.ProfitStaffed == 0 && outlook.Roles[0].Hire == 4, "rota advice: with no staff the advice is still there, nothing can be kept open, and four are to hire");

        // Nothing is worth opening; no worth known; hours that are the game's.
        for (int cell = 0; cell < RotaCells; cell++)
        {
            shop.Customers[cell] = 2;
        }
        p = RotaMake(2, 4);
        advisor.Advise(shop, p, RotaSolverUnderTest, present, outlook);
        Check(outlook.Advised && outlook.HoursBest == 0 && outlook.ProfitBest == 0, "rota advice: two customers an hour never pay a wage: no hour is advised");
        shop.Margin = 0;
        advisor.Advise(shop, p, RotaSolverUnderTest, present, outlook);
        Check(!outlook.Advised && outlook.HoursBest == 28, "rota advice: with no worth known for a customer no hours are advised and it keeps the ones it has");
        shop.Margin = 400;
        shop.FixedHours = true;
        advisor.Advise(shop, p, RotaSolverUnderTest, present, outlook);
        Check(!outlook.Advised && outlook.HoursBest == 28 && outlook.Plan.Full, "rota advice: where the game sets the hours none are advised, and the rota fills the hours there are");
    }

    // ---------------------------------------------------------------- generated businesses

    private static uint _rotaSeed;

    private static int RotaNext(int below)
    {
        _rotaSeed = _rotaSeed * 1664525u + 1013904223u;
        return (int)((_rotaSeed >> 8) % (uint)below);
    }

    private static RotaProblem RotaGenerate(int seed, int stations, int workers, bool demands)
    {
        _rotaSeed = (uint)(seed * 7919 + 17);
        RotaProblem p = RotaMake(stations, workers);
        int skills = 1 + RotaNext(3);
        var skillOf = new int[stations];
        for (int s = 0; s < stations; s++)
        {
            skillOf[s] = RotaNext(skills);
            p.Kind[s] = skillOf[s] == skills - 1 && skills > 1 && RotaNext(2) == 0 ? RotaKind.Cleaning : RotaKind.Work;
            int opens = 6 + RotaNext(5), closes = 16 + RotaNext(8), peak = 100 + RotaNext(400);
            for (int d = 0; d < 7; d++)
            {
                bool shut = RotaNext(9) == 0;
                for (int h = opens; h < closes && !shut; h++)
                {
                    bool busy = h >= 11 && h < 19;
                    if (s == 0 || busy || RotaNext(3) != 0)
                    {
                        p.Value[(d * 24 + h) * stations + s] = (busy ? peak : peak / 4) / (1 + s) + (d >= 5 ? 50 : 0) + 2;
                    }
                }
            }
        }
        for (int w = 0; w < workers; w++)
        {
            int skill = RotaNext(skills), second = RotaNext(4) == 0 ? RotaNext(skills) : -1;
            p.Worker[w].Wage = 1200 + RotaNext(12) * 100;
            for (int s = 0; s < stations; s++)
            {
                p.Can[w * stations + s] = (byte)(skillOf[s] == skill || skillOf[s] == second ? 30 + RotaNext(70) : 0);
            }
            if (!demands)
            {
                continue;
            }
            var list = new System.Collections.Generic.List<RotaDemand>();
            if (RotaNext(3) != 0)
            {
                int least = 8 + RotaNext(4) * 8;
                list.Add(RotaAsk(RotaDemandKind.Hours, 2, least, least + 8 + RotaNext(2) * 8));
            }
            if (RotaNext(4) == 0)
            {
                list.Add(RotaAsk(RotaDemandKind.Days, RotaNext(3), 3 + RotaNext(3)));
            }
            if (RotaNext(3) == 0)
            {
                list.Add(RotaAsk(RotaDemandKind.FreeDays, RotaNext(3), 0, 0, 1UL << RotaNext(7) | 1UL << RotaNext(7)));
            }
            if (RotaNext(4) == 0)
            {
                list.Add(RotaAsk(RotaDemandKind.NoHours, RotaNext(3), 0, 0, RotaNext(2) == 0 ? 0x0003FFUL : 0xFC0000UL));
            }
            if (RotaNext(6) == 0)
            {
                list.Add(RotaAsk(RotaDemandKind.NoShiftKind, RotaNext(3), 0, 0, 1UL << RotaKind.Cleaning));
            }
            if (RotaNext(8) == 0)
            {
                list.Add(RotaAsk(RotaDemandKind.OnStation, RotaNext(3), 0, 0, 1UL << RotaNext(stations)));
            }
            p.Worker[w].Demands = list.ToArray();
        }
        return p;
    }

    /// <summary>
    /// The plain rota this is measured against: day by day, hour by hour, station by station, the first employee in
    /// the list who may stand there by the same rules gets the hour. First with every demand as a limit; then, as
    /// the game does, once more over what is left with only the demands of high priority as limits.
    /// </summary>
    private static RotaPlan RotaInOrder(RotaProblem p)
    {
        int S = p.Stations, W = p.Workers;
        var plan = new RotaPlan();
        plan.Size(S);
        var busy = new bool[W * RotaCells];
        var dayHours = new int[W * 7];
        var week = new int[W];
        for (int level = 0; level < 2; level++)
        {
            for (int cell = 0; cell < RotaCells; cell++)
            {
                int d = cell / 24, h = cell % 24;
                for (int s = 0; s < S; s++)
                {
                    if (p.Value[cell * S + s] <= 0 || plan.At[cell * S + s] >= 0 || p.Whole[s])
                    {
                        continue;
                    }
                    for (int w = 0; w < W; w++)
                    {
                        if (p.Can[w * S + s] == 0 || busy[w * RotaCells + cell] || dayHours[w * 7 + d] >= RotaCatalogue.DayCap)
                        {
                            continue;
                        }
                        int run = 1;
                        for (int k = h - 1; k >= 0 && plan.At[(d * 24 + k) * S + s] == w; k--)
                        {
                            run++;
                        }
                        for (int k = h + 1; k < 24 && plan.At[(d * 24 + k) * S + s] == w; k++)
                        {
                            run++;
                        }
                        if (run > RotaCatalogue.ShiftCap)
                        {
                            continue;
                        }
                        ulong can = 0;
                        for (int k = 0; k < S; k++)
                        {
                            can |= p.Can[w * S + k] != 0 ? 1UL << k : 0;
                        }
                        int days = 0;
                        for (int k = 0; k < 7; k++)
                        {
                            days += dayHours[w * 7 + k] > 0 ? 1 : 0;
                        }
                        bool allowed = true;
                        foreach (RotaDemand q in p.Worker[w].Demands)
                        {
                            if (level > 0 && q.Priority < RotaCatalogue.PriorityHigh)
                            {
                                continue;
                            }
                            allowed &= !(q.Kind == RotaDemandKind.Hours && q.B > 0 && week[w] >= q.B) && !(q.Kind == RotaDemandKind.Days && q.A > 0 && dayHours[w * 7 + d] == 0 && days >= q.A)
                                       && !(q.Kind == RotaDemandKind.FreeDays && ((q.Mask >> d) & 1) != 0) && !(q.Kind == RotaDemandKind.NoHours && ((q.Mask >> h) & 1) != 0)
                                       && !(q.Kind == RotaDemandKind.NoShiftKind && ((q.Mask >> p.Kind[s]) & 1) != 0) && !(q.Kind == RotaDemandKind.OnStation && (q.Mask & can) != 0 && ((q.Mask >> s) & 1) == 0);
                        }
                        if (!allowed)
                        {
                            continue;
                        }
                        plan.At[cell * S + s] = (short)w;
                        busy[w * RotaCells + cell] = true;
                        dayHours[w * 7 + d]++;
                        week[w]++;
                        plan.Level = level;
                        break;
                    }
                }
            }
        }
        int kept = plan.Level;
        RotaSolverUnderTest.Rate(p, plan);
        plan.Level = kept;
        return plan;
    }

    private static void RotaGenerated()
    {
        // The same businesses twice: with demands of every priority, and with every demand of high priority, so that
        // none gives way in either rota and the two are measured on the same footing.
        RotaAgainstPlain(false);
        RotaAgainstPlain(true);
    }

    private static void RotaAgainstPlain(bool allHigh)
    {
        const int Cases = 120;
        int wrong = 0, moreHours = 0, sameHours = 0, fewerHours = 0, fewerUnmet = 0, moreUnmet = 0, baselineWrong = 0, differ = 0, lessValue = 0, atLeast = 0, fewerForNothing = 0, unmetForNothing = 0;
        long hoursOurs = 0, hoursBase = 0, valueOurs = 0, valueBase = 0, wageOurs = 0, wageBase = 0, shiftsOurs = 0, shiftsBase = 0;
        int unmetOurs = 0, unmetBase = 0;
        string firstWrong = null;
        for (int seed = 1; seed <= Cases; seed++)
        {
            int stations = 2 + seed % 9, workers = 2 + (seed * 5) % 17;
            RotaProblem p = RotaGenerate(seed, stations, workers, seed % 4 != 0);
            for (int w = 0; allHigh && w < p.Workers; w++)
            {
                for (int i = 0; i < p.Worker[w].Demands.Length; i++)
                {
                    p.Worker[w].Demands[i].Priority = RotaCatalogue.PriorityHigh;
                }
            }
            RotaPlan ours = RotaSolverUnderTest.Solve(p, new RotaPlan());
            RotaPlan again = new RotaSolver().Solve(p, new RotaPlan());
            string bad = RotaWrong(p, ours);
            if (bad != null)
            {
                wrong++;
                firstWrong = firstWrong ?? "seed " + seed + ": " + bad;
            }
            for (int i = 0; i < RotaCells * stations; i++)
            {
                if (ours.At[i] != again.At[i])
                {
                    differ++;
                    break;
                }
            }
            RotaPlan plain = RotaInOrder(p);
            baselineWrong += RotaWrong(p, plain) != null ? 1 : 0;
            moreHours += ours.CoveredHours > plain.CoveredHours ? 1 : 0;
            sameHours += ours.CoveredHours == plain.CoveredHours ? 1 : 0;
            fewerHours += ours.CoveredHours < plain.CoveredHours ? 1 : 0;
            lessValue += ours.CoveredValue < plain.CoveredValue ? 1 : 0;
            fewerUnmet += ours.UnmetCount < plain.UnmetCount ? 1 : 0;
            moreUnmet += ours.UnmetCount > plain.UnmetCount ? 1 : 0;
            atLeast += ours.CoveredHours >= plain.CoveredHours && ours.UnmetCount <= plain.UnmetCount ? 1 : 0;
            fewerForNothing += ours.CoveredHours < plain.CoveredHours && ours.CoveredValue <= plain.CoveredValue ? 1 : 0;
            unmetForNothing += ours.UnmetCount > plain.UnmetCount && ours.CoveredValue <= plain.CoveredValue ? 1 : 0;
            hoursOurs += ours.CoveredHours;
            hoursBase += plain.CoveredHours;
            valueOurs += ours.CoveredValue;
            valueBase += plain.CoveredValue;
            unmetOurs += ours.UnmetCount;
            unmetBase += plain.UnmetCount;
            wageOurs += ours.Wage;
            wageBase += plain.Wage;
            shiftsOurs += ours.Shifts;
            shiftsBase += plain.Shifts;
        }
        string set = allHigh ? "generated, every demand of high priority" : "generated";
        Check(wrong == 0, "rota, " + set + ", " + Cases + " businesses: every rota keeps the rules (" + wrong + " do not; " + firstWrong + ")");
        Check(differ == 0, "rota, " + set + ": a second solver gives the same rota every time (" + differ + " differ)");
        Check(baselineWrong == 0, "rota, " + set + ": the plain rota it is measured against keeps the rules too (" + baselineWrong + " do not)");
        Check(lessValue == 0, "rota, " + set + ": never less value covered than the plain rota (" + lessValue + " cases)");
        Check(fewerForNothing == 0, "rota, " + set + ": never fewer wanted hours covered than the plain rota, unless the hours it covers are worth more (" + fewerForNothing + " cases)");
        Check(unmetForNothing == 0, "rota, " + set + ": never more demands unmet than the plain rota, unless more value is covered for it (" + unmetForNothing + " cases)");
        Check(atLeast >= 10 && atLeast * 10 >= Cases * 8, "rota, " + set + ": at least as many wanted hours covered with no more demands unmet in " + atLeast + " of " + Cases + " businesses");
        Check(hoursOurs > hoursBase && valueOurs > valueBase && unmetOurs < unmetBase && shiftsOurs < shiftsBase && wageOurs * hoursBase <= wageBase * hoursOurs,
            "rota, " + set + ": in all, more hours, more value, fewer unmet demands, fewer shifts and no more wages an hour");
        if (allHigh)
        {
            Check(moreUnmet == 0, "rota, " + set + ": never more demands unmet than the plain rota (" + moreUnmet + " cases)");
        }
        Console.WriteLine("  rota against a plain fill in order, " + Cases + " " + (allHigh ? "businesses with every demand of high priority" : "generated businesses") + ": hours covered " + hoursOurs + " against " + hoursBase
                          + " (more in " + moreHours + ", the same in " + sameHours + ", fewer in " + fewerHours + ", each of those with more value); value " + valueOurs + " against " + valueBase + " (less in " + lessValue
                          + "); demands unmet " + unmetOurs + " against " + unmetBase + " (fewer in " + fewerUnmet + ", more in " + moreUnmet + "); at least as many hours with no more unmet in " + atLeast
                          + "; shifts " + shiftsOurs + " against " + shiftsBase + "; wages " + wageOurs / 100 + " against " + wageBase / 100);
    }

    private static void RotaTiming()
    {
        // The largest business: 40 stations and 60 employees, every station wanted for 16 hours of every day.
        RotaProblem p = RotaGenerate(99, 40, 60, true);
        for (int cell = 0; cell < RotaCells; cell++)
        {
            for (int s = 0; s < 40; s++)
            {
                p.Value[cell * 40 + s] = cell % 24 >= 6 && cell % 24 < 22 ? 50 + (s * 37 + cell * 11) % 400 : 0;
            }
        }
        var solver = new RotaSolver();
        var plan = new RotaPlan();
        for (int i = 0; i < 30; i++)
        {
            solver.Solve(p, plan);   // (the runtime compiles it, and the arrays grow, before the clock starts)
        }
        string wrong = RotaWrong(p, plan);
        Check(wrong == null, "rota, the largest business: the rules hold (" + wrong + ")");
        double worst = 0, sum = 0;
        var watch = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        const int Runs = 40;
        for (int i = 0; i < Runs; i++)
        {
            watch.Restart();
            solver.Solve(p, plan);
            watch.Stop();
            double ms = watch.Elapsed.TotalMilliseconds;
            worst = Math.Max(worst, ms);
            sum += ms;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(worst < 8.0, "rota, the largest business (40 stations, 60 employees, 7 days): a rota in under 8 ms (the slowest of " + Runs + " took " + worst.ToString("0.00") + " ms)");
        Check(allocated < 4096, "rota, the largest business: nothing is allocated while it is solved again (" + allocated + " bytes over " + Runs + " runs)");
        Console.WriteLine("  rota timing, 40 stations and 60 employees over 7 days (" + plan.WantedHours + " wanted hours, " + plan.CoveredHours + " covered, " + plan.Shifts + " shifts): mean "
                          + (sum / Runs).ToString("0.00") + " ms, slowest " + worst.ToString("0.00") + " ms");

        // And the whole advice for a large shop: the hours, the crew of every hour, and up to two rotas.
        var shop = new RotaShop();
        shop.Size(40, 4);
        shop.GroupKind[0] = shop.GroupKind[1] = RotaGroupKind.Service;
        shop.GroupKind[2] = RotaGroupKind.Support;
        shop.GroupKind[3] = RotaGroupKind.Cleaning;
        for (int s = 0; s < 40; s++)
        {
            shop.Group[s] = s < 20 ? 0 : s < 32 ? 1 : s < 36 ? 2 : 3;
            shop.Cap[s] = s < 20 ? 15 : s < 32 ? 25 : 0;
            shop.StationWage[s] = 1500;
            p.Kind[s] = s >= 36 ? RotaKind.Cleaning : RotaKind.Work;
        }
        shop.Margin = 500;
        shop.HasCustomers = true;
        shop.DirtEvents = 250;
        shop.CleanHours = 8;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            int h = cell % 24;
            shop.Open[cell] = h >= 8 && h < 20;
            shop.Customers[cell] = h >= 7 && h < 23 ? 60 + (h >= 11 && h < 19 ? 200 : 0) + (cell / 24) * 10 : 0;
        }
        var advisor = new RotaAdvisor();
        var outlook = new RotaOutlook();
        for (int i = 0; i < 10; i++)
        {
            advisor.Advise(shop, p, solver, null, outlook);
        }
        worst = 0;
        for (int i = 0; i < 20; i++)
        {
            watch.Restart();
            advisor.Advise(shop, p, solver, null, outlook);
            watch.Stop();
            worst = Math.Max(worst, watch.Elapsed.TotalMilliseconds);
        }
        Check(worst < 16.0, "rota, the largest business: the whole advice, with its rotas, in under 16 ms (the slowest of 20 took " + worst.ToString("0.00") + " ms)");
        Console.WriteLine("  rota timing, the whole advice for it (" + outlook.HoursBest + " hours advised, " + outlook.HoursStaffed + " with the staff there are): slowest " + worst.ToString("0.00") + " ms");
    }
}
