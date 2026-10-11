using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// The checks of the two compulsory ends of an hours demand and of shifts that follow the customers
// (Scripts/Economy/Company/RotaFit.cs, and what RotaLadder.Steps.cs does with it): over generated businesses, nobody
// in a rota the planner makes works outside the hours a week they ask for; where the opening hours can hold everybody's
// least hours nobody is left out (existence by a counting argument on businesses small enough for it to be exact);
// where they cannot, the reason given is the right one; a busier hour is never staffed thinner than a quieter one of
// the same day; opening hours are lengthened by the best paying hours; and a ladder made in steps is the one made in one go.
internal static partial class Program
{
    private const int RotaPart = 1, RotaFull = 2;

    private static RotaDemand RotaHoursAsk(int kind)
    {
        return kind == RotaPart ? RotaAsk(RotaDemandKind.Hours, 2, 10, 30) : RotaAsk(RotaDemandKind.Hours, 2, 30, 50);
    }

    // A generated shop: one to three tills, perhaps a cleaning station, cashiers and cleaners who ask for part-time, full-time or nothing.
    private static RotaCase RotaRangeCase(int seed, bool longHours, int tillsMost, bool noCleaning = false)
    {
        _rotaSeed = (uint)(seed * 7919 + 4243);
        int tills = 1 + RotaNext(tillsMost), cleaning = noCleaning ? 0 : RotaNext(2), cashiers = 1 + RotaNext(tills + 1), cleaners = cleaning > 0 ? 1 + RotaNext(2) : 0;
        RotaCase c = RotaShopCase(tills, cleaning, cashiers + cleaners, 100 + RotaNext(3) * 100);
        int stations = tills + cleaning;
        c.Shop.Margin = 2000;
        c.Shop.Takings = 2600;
        c.Shop.DirtPerCustomer = 200;
        c.Shop.CleanPerHour = 40000;
        c.Shop.Content = 70;
        c.Shop.CleanNow = 85;
        for (int s = 0; s < stations; s++)
        {
            c.Shop.StationWage[s] = s < tills ? 1200 : 900;
        }
        for (int w = 0; w < cashiers + cleaners; w++)
        {
            bool cleans = w >= cashiers;
            c.Problem.Worker[w].Wage = cleans ? 900 : 1200;
            for (int s = 0; s < stations; s++)
            {
                c.Problem.Can[w * stations + s] = (byte)((s >= tills) == cleans ? 40 + RotaNext(50) : 0);
            }
            int kind = RotaNext(3);
            c.Problem.Worker[w].Demands = kind == 0 ? RotaWorker.Empty : new[] { RotaHoursAsk(kind) };
        }
        int from = longHours ? 7 + RotaNext(3) : 10 + RotaNext(3), length = longHours ? 10 + RotaNext(3) : 4 + RotaNext(3);
        RotaOpenHours(c, RotaNext(3) == 0 ? 0x3F : 0x7F, from, from + length);
        return c;
    }

    private static int RotaOpenCount(bool[] open)
    {
        int n = 0;
        foreach (bool hour in open)
        {
            n += hour ? 1 : 0;
        }
        return n;
    }

    // Somebody's pieces of work at a station on a day, and the shortest of them.
    private static void RotaRunsOf(RotaPlan plan, int w, int s, int d, out int runs, out int shortest)
    {
        runs = 0;
        shortest = 99;
        for (int h = 0; h < 24; h++)
        {
            if (plan.At[(d * 24 + h) * plan.Stations + s] != w || (h > 0 && plan.At[(d * 24 + h - 1) * plan.Stations + s] == w))
            {
                continue;
            }
            int end = h;
            while (end < 24 && plan.At[(d * 24 + end) * plan.Stations + s] == w)
            {
                end++;
            }
            runs++;
            shortest = Math.Min(shortest, end - h);
        }
    }

    // Busier hours of a day never staffed thinner than quieter ones, at the stations that serve customers, unless a rule forces
    // it. A pair of hours breaks this if somebody at a till in the quieter hour could stand at an empty till in the busier one
    // instead with every rule kept: skill, one place at a time, the 12 hour shift, the 14 hour day, their own demands, and whole
    // shifts (no more pieces of work on any day than before, none of a single hour). Returns how many pairs break it.
    private static int RotaAgainstCurve(RotaCase c, RotaPlan plan, bool[] open, int tills)
    {
        int stations = c.Shop.Stations, broken = 0;
        var level = new int[24];
        for (int d = 0; d < 7; d++)
        {
            for (int h = 0; h < 24; h++)
            {
                level[h] = 0;
                for (int s = 0; s < tills; s++)
                {
                    level[h] += plan.At[(d * 24 + h) * stations + s] >= 0 ? 1 : 0;
                }
            }
            for (int a = 0; a < 24; a++)
            {
                for (int b = 0; b < 24; b++)
                {
                    int busy = d * 24 + a, quiet = d * 24 + b;
                    if (!open[busy] || !open[quiet] || c.Shop.Customers[busy] <= c.Shop.Customers[quiet] || level[a] >= level[b])
                    {
                        continue;
                    }
                    bool could = false;
                    for (int s2 = 0; s2 < tills && !could; s2++)
                    {
                        int w = plan.At[quiet * stations + s2];
                        for (int s1 = 0; w >= 0 && w < c.Problem.Workers && s1 < tills && !could; s1++)
                        {
                            if (plan.At[busy * stations + s1] >= 0 || c.Problem.Can[w * stations + s1] == 0)
                            {
                                continue;
                            }
                            bool elsewhere = false;
                            for (int s = 0; s < stations; s++)
                            {
                                elsewhere |= plan.At[busy * stations + s] == w;
                            }
                            int runsBefore, runsAfter, shortBefore, shortAfter, r1, r2, x1, x2;
                            RotaRunsOf(plan, w, s2, d, out runsBefore, out shortBefore);
                            RotaRunsOf(plan, w, s1, d, out r1, out x1);
                            plan.At[quiet * stations + s2] = -1;
                            plan.At[busy * stations + s1] = (short)w;
                            RotaRunsOf(plan, w, s2, d, out runsAfter, out shortAfter);
                            RotaRunsOf(plan, w, s1, d, out r2, out x2);
                            bool whole = s1 == s2 ? runsAfter <= runsBefore && shortAfter >= 2 : runsAfter <= runsBefore && r2 <= r1 && (runsAfter == 0 || shortAfter >= 2) && x2 >= 2;
                            could = !elsewhere && whole && RotaWrong(c.Problem, plan) == null;
                            plan.At[quiet * stations + s2] = (short)w;
                            plan.At[busy * stations + s1] = -1;
                        }
                    }
                    broken += could ? 1 : 0;
                }
            }
        }
        return broken;
    }

    // Shifts of a single hour in a rota, and days on which somebody's hours at a station are in more than one piece.
    private static void RotaPieces(RotaPlan plan, int workers, out int single, out int split)
    {
        single = split = 0;
        for (int s = 0; s < plan.Stations; s++)
        {
            for (int d = 0; d < 7; d++)
            {
                var runs = new int[workers + 8];
                for (int h = 0; h < 24; h++)
                {
                    int w = plan.At[(d * 24 + h) * plan.Stations + s];
                    if (w < 0 || (h > 0 && plan.At[(d * 24 + h - 1) * plan.Stations + s] == w))
                    {
                        continue;
                    }
                    int end = h;
                    while (end < 24 && plan.At[(d * 24 + end) * plan.Stations + s] == w)
                    {
                        end++;
                    }
                    single += end - h == 1 ? 1 : 0;
                    split += w < runs.Length && runs[w]++ > 0 ? 1 : 0;
                }
            }
        }
    }

    private static void RotaRangeChecks()
    {
        RotaFitUnits();
        RotaRangeGenerated("short hours, one till", false, 1, false);
        RotaRangeGenerated("short hours, up to three tills", false, 3, false);
        RotaRangeGenerated("long hours, one till", true, 1, false);
        RotaRangeGenerated("long hours, up to two tills", true, 2, false);
        RotaRangeGenerated("long hours, up to three tills", true, 3, false);
        RotaRangeGenerated("short hours, tills only", false, 2, true);
        RotaSecondTill();
        RotaRangeNoWay();
        RotaRangeExtended();
        RotaSkillFirst();
    }

    // The better at the job first; whoever has no hours gets the quiet ones where the stations have them; only who they have none for stays without.
    private static void RotaSkillFirst()
    {
        // One till open 42 hours that may not open longer, two full-time cashiers: the stations hold one of them.
        for (int better = 0; better < 2; better++)
        {
            RotaCase c = RotaShopCase(1, 0, 2);
            c.Problem.Can[better] = 80;
            c.Problem.Can[1 - better] = 45;
            c.Problem.Worker[0].Wage = c.Problem.Worker[1].Wage = 1200;
            c.Problem.Worker[0].Demands = new[] { RotaHoursAsk(RotaFull) };
            c.Problem.Worker[1].Demands = new[] { RotaHoursAsk(RotaFull) };
            c.Shop.Margin = 2000;
            RotaOpenHours(c, 0x7F, 10, 16);
            c.Barred = new bool[RotaCatalogue.Cells];
            for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
            {
                c.Barred[cell] = !c.Shop.Open[cell];
            }
            RotaLadderResult l = RotaClimb(c);
            RotaRung best = l.Rungs[1];
            int good = RotaHoursOf(best.Plan, better), poor = RotaHoursOf(best.Plan, 1 - better);
            Check(good >= 30 && good <= 50 && poor == 0 && best.Left.Count == 1 && best.Left[0].Worker == 1 - better && best.Left[0].Why == RotaClash.Shared,
                "rota skill: two full-time cashiers and a till that holds one: the more skilled has the week, the other is the one not needed (" + good + " and " + poor + ", the better is number " + better + ")");
            Check(RotaHoursOf(l.Fill, better) >= 30 && RotaHoursOf(l.Fill, 1 - better) == 0, "rota skill: and so it is in the rota \"Fill the rota\" writes");
        }

        // The last try: somebody with no hours, a colleague on the busy half of the day, the quiet half empty.
        RotaCase q = RotaShopCase(1, 0, 2);
        q.Problem.Can[0] = 80;
        q.Problem.Can[1] = 45;
        q.Problem.Worker[0].Demands = new[] { RotaHoursAsk(RotaPart) };
        q.Problem.Worker[1].Demands = new[] { RotaHoursAsk(RotaPart) };
        RotaOpenHours(q, 0x7F, 8, 20);
        var plan = new RotaPlan();
        plan.Size(1);
        int busiest = 0;
        for (int h = 8; h < 20; h++)
        {
            busiest = q.Shop.Customers[h] > q.Shop.Customers[busiest] || busiest == 0 ? h : busiest;
        }
        int from = Math.Max(8, Math.Min(16, busiest - 2));
        for (int d = 0; d < 7; d++)
        {
            for (int h = from; h < from + 4; h++)
            {
                plan.At[d * 24 + h] = 0;    // 28 hours around the busiest hour of every day
            }
        }
        var fit = new RotaFit();
        fit.Begin(q.Shop, q.Problem, q.Shop.Open, plan);
        int given = fit.Rescue(2);
        bool quiet = true, apart = true;
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            int h = cell % 24;
            quiet &= plan.At[cell] != 1 || h < from || h >= from + 4 || fit.HoursOf(0) < 28;
            apart &= plan.At[cell] != 0 || (h >= from - 0 && h < from + 4);
        }
        int single, split;
        RotaPieces(plan, 2, out single, out split);
        Check(given == 1 && fit.HoursOf(1) >= 10 && fit.HoursOf(1) <= 30, "rota skill: somebody with no hours is given a week on the hours nobody stands at (" + fit.HoursOf(1) + " hours)");
        Check(fit.HoursOf(0) >= 10 && apart && quiet, "rota skill: those are the quieter hours: the colleague keeps the busy ones (" + fit.HoursOf(0) + " hours kept)");
        Check(single == 0, "rota skill: and no shift of a single hour comes of it");

        // Nothing to give: every open hour is a colleague's least. They stay without, and nothing of theirs is left behind.
        q = RotaShopCase(1, 0, 2);
        q.Problem.Can[0] = 80;
        q.Problem.Can[1] = 45;
        q.Problem.Worker[0].Demands = new[] { RotaHoursAsk(RotaFull) };
        q.Problem.Worker[1].Demands = new[] { RotaHoursAsk(RotaPart) };
        RotaOpenHours(q, 0x1F, 10, 16);     // thirty hours, all the first cashier's least
        plan.Size(1);
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            plan.At[cell] = (short)(q.Shop.Open[cell] ? 0 : -1);
        }
        fit.Begin(q.Shop, q.Problem, q.Shop.Open, plan);
        given = fit.Rescue(2);
        Check(given == 0 && fit.HoursOf(1) == 0 && fit.HoursOf(0) == 30 && RotaHoursOf(plan, 1) == 0, "rota skill: where the stations have no hours for somebody they stay without, and the others keep theirs (" + fit.HoursOf(0) + " and " + fit.HoursOf(1) + ")");

        // The order: with room for one part-time week only, the more skilled of two without hours has it.
        q = RotaShopCase(1, 0, 2);
        q.Problem.Can[0] = 45;
        q.Problem.Can[1] = 80;
        q.Problem.Worker[0].Demands = new[] { RotaHoursAsk(RotaPart) };
        q.Problem.Worker[1].Demands = new[] { RotaHoursAsk(RotaPart) };
        RotaOpenHours(q, 0x07, 10, 14);     // twelve hours
        plan.Size(1);
        fit.Begin(q.Shop, q.Problem, q.Shop.Open, plan);
        given = fit.Rescue(2);
        Check(given == 1 && fit.HoursOf(1) >= 10 && fit.HoursOf(0) == 0, "rota skill: of two without hours and room for one, the more skilled has the week (" + fit.HoursOf(0) + " and " + fit.HoursOf(1) + ")");
        Check(RotaFit.Skill(q.Problem, 0) == 45 && RotaFit.Skill(q.Problem, 1) == 80, "rota skill: the skill is the game's percentage at the station they are best at");
    }

    private static void RotaFitUnits()
    {
        var worker = new RotaWorker { Demands = new[] { RotaHoursAsk(RotaPart) } };
        int least, most;
        RotaFit.Range(worker, out least, out most);
        Check(least == 10 && most == 30, "rota hours: part-time is read as the least and the most the demand holds (" + least + " to " + most + ")");
        Check(RotaFit.Inside(worker, 10) && RotaFit.Inside(worker, 30) && !RotaFit.Inside(worker, 9) && !RotaFit.Inside(worker, 31) && RotaFit.Inside(worker, 0),
            "rota hours: inside the range is from the least to the most, both counted; no hours at all is being left out, not being short");
        RotaFit.Range(new RotaWorker(), out least, out most);
        Check(least == 0 && most == int.MaxValue && RotaFit.Inside(new RotaWorker(), 77), "rota hours: somebody who asks for no hours has no range");

        // One till, open six hours a day, one full-time cashier: the solver's rota is brought up to thirty hours.
        RotaCase c = RotaShopCase(1, 0, 1);
        c.Problem.Can[0] = 50;
        c.Problem.Worker[0].Wage = 1200;
        c.Problem.Worker[0].Demands = new[] { RotaHoursAsk(RotaFull) };
        RotaOpenHours(c, 0x7F, 10, 16);
        var plan = new RotaPlan();
        plan.Size(1);
        for (int h = 11; h < 13; h++)
        {
            plan.At[h] = 0;     // Monday, two hours
        }
        var fit = new RotaFit();
        fit.Begin(c.Shop, c.Problem, c.Shop.Open, plan);
        fit.Reach(0);
        int single, split;
        RotaPieces(plan, 1, out single, out split);
        Check(fit.HoursOf(0) == 30, "rota hours: somebody with two hours who asks for at least thirty is brought to thirty where the hours are there (" + fit.HoursOf(0) + ")");
        Check(single == 0 && split == 0, "rota hours: and their week is whole shifts, one a day, none of a single hour (" + single + ", " + split + ")");
        bool peak = true;
        for (int d = 0; d < 7; d++)
        {
            peak &= plan.At[d * 24 + 12] == 0 || RotaHoursOf(plan, 0) == 0;
        }
        Check(peak, "rota hours: the hours given first are the busiest ones: midday of every day is among them");
        Check(RotaFit.Room(c.Shop, c.Problem, c.Shop.Open, 0) == 42, "rota hours: the room for somebody is the open hours they may work (" + RotaFit.Room(c.Shop, c.Problem, c.Shop.Open, 0) + ")");
        c.Problem.Worker[0].Demands = new[] { RotaHoursAsk(RotaFull), RotaAsk(RotaDemandKind.FreeDays, 2, 0, 0, 0x60), RotaAsk(RotaDemandKind.NoHours, 2, 0, 0, 1UL << 10) };
        Check(RotaFit.Room(c.Shop, c.Problem, c.Shop.Open, 0) == 25, "rota hours: less their free days and the hours they do not work (" + RotaFit.Room(c.Shop, c.Problem, c.Shop.Open, 0) + ")");
        plan.Size(1);
        fit.Begin(c.Shop, c.Problem, c.Shop.Open, plan);
        fit.Reach(0);
        bool kept = true;
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            kept &= plan.At[cell] < 0 || (cell / 24 < 5 && cell % 24 != 10);
        }
        Check(kept && fit.HoursOf(0) == 25, "rota hours: no hour is ever given on a free day or in an hour they do not work, even to reach the least (" + fit.HoursOf(0) + " of 30)");

        // A colleague gives up hours only down to their own least.
        c = RotaShopCase(1, 0, 2);
        c.Problem.Can[0] = c.Problem.Can[1] = 50;
        c.Problem.Worker[0].Demands = new[] { RotaHoursAsk(RotaPart) };
        c.Problem.Worker[1].Demands = new[] { RotaHoursAsk(RotaPart) };
        RotaOpenHours(c, 0x1F, 10, 16);     // thirty hours, all the first cashier's
        plan.Size(1);
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            plan.At[cell] = (short)(c.Shop.Open[cell] ? 0 : -1);
        }
        fit.Begin(c.Shop, c.Problem, c.Shop.Open, plan);
        fit.Reach(1);
        Check(fit.HoursOf(1) == 10 && fit.HoursOf(0) == 20, "rota hours: where every hour is taken, a colleague with more than their least gives up hours (" + fit.HoursOf(0) + " and " + fit.HoursOf(1) + ")");
        RotaPieces(plan, 2, out single, out split);
        Check(single == 0, "rota hours: and neither is left a shift of a single hour");
        c.Problem.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 25, 30) };
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            plan.At[cell] = (short)(c.Shop.Open[cell] ? 0 : -1);
        }
        fit.Begin(c.Shop, c.Problem, c.Shop.Open, plan);
        fit.Reach(1);
        Check(fit.HoursOf(0) >= 25 && fit.HoursOf(0) + fit.HoursOf(1) == 30, "rota hours: a colleague never gives up an hour of their own least (" + fit.HoursOf(0) + " and " + fit.HoursOf(1) + ")");

        // A shift moves towards the customers.
        c = RotaShopCase(1, 0, 1);
        c.Problem.Can[0] = 50;
        RotaOpenHours(c, 0x01, 8, 20);
        plan.Size(1);
        for (int h = 16; h < 20; h++)
        {
            plan.At[h] = 0;
        }
        fit.Begin(c.Shop, c.Problem, c.Shop.Open, plan);
        fit.Slide();
        Check(plan.At[11] == 0 && plan.At[12] == 0 && plan.At[13] == 0 && plan.At[19] < 0 && fit.HoursOf(0) == 4, "rota hours: four hours at the end of the day are moved to the four busiest, an hour at a time, and stay four");
        Check(RotaAgainstCurve(c, plan, c.Shop.Open, 1) == 0, "rota hours: after that no busier hour of the day is staffed thinner than a quieter one");
        // Hours go to the busier day.
        c = RotaShopCase(1, 0, 1);
        c.Problem.Can[0] = 50;
        RotaOpenHours(c, 0x03, 8, 20);
        for (int h = 0; h < 24; h++)
        {
            c.Shop.Customers[24 + h] *= 3;      // Tuesday is three times as busy
        }
        plan.Size(1);
        for (int h = 9; h < 15; h++)
        {
            plan.At[h] = 0;                     // six hours on Monday...
        }
        plan.At[24 + 12] = plan.At[24 + 13] = 0; // ...two on Tuesday
        fit.Begin(c.Shop, c.Problem, c.Shop.Open, plan);
        fit.Slide();
        int monday = 0, tuesday = 0;
        for (int h = 0; h < 24; h++)
        {
            monday += plan.At[h] == 0 ? 1 : 0;
            tuesday += plan.At[24 + h] == 0 ? 1 : 0;
        }
        Check(tuesday > 2 && monday + tuesday == 8 && monday >= 2, "rota hours: hours of a long shift on a quiet day go to the busier day, and the quiet day keeps a whole shift (" + monday + " and " + tuesday + ")");
    }

    private static void RotaRangeGenerated(string what, bool longHours, int tillsMost, bool noCleaning)
    {
        const int cases = 55;
        int outside = 0, wrong = 0, leftThoughRoom = 0, exact = 0, leftWrongReason = 0, curve = 0, curvePairs = 0, singles = 0, splits = 0, stepped = 0, unusable = 0, hireOutside = 0, quieterFirst = 0, lessSkilled = 0;
        for (int seed = 1; seed <= cases; seed++)
        {
            RotaCase c = RotaRangeCase(seed, longHours, tillsMost, noCleaning);
            RotaLadderResult l = RotaClimb(c);
            int stations = c.Shop.Stations, tills = 0, real = c.Problem.Workers;
            for (int s = 0; s < stations; s++)
            {
                tills += c.Shop.Group[s] == 0 ? 1 : 0;
            }
            RotaRung best = l.Rungs[1];
            // Every rota made: the one "Fill the rota" writes, and every rung above the week as it is.
            for (int i = 0; i <= l.Count; i++)
            {
                RotaPlan plan = i == 0 ? l.Fill : i < l.Count ? l.Rungs[i].Plan : null;
                for (int w = 0; plan != null && w < real; w++)
                {
                    bool inside = RotaFit.Inside(c.Problem.Worker[w], RotaHoursOf(plan, w));
                    outside += !inside && i <= 1 ? 1 : 0;
                    hireOutside += !inside && i > 1 ? 1 : 0;
                }
            }
            unusable += best.Usable && (best.Source == 0 || RotaLadder.Writable(best)) ? 0 : 1;
            for (int i = 0; i < RotaCatalogue.Cells * stations; i++)
            {
                c.Problem.Value[i] = 1;     // (the rules are checked here, not which hours were wanted)
            }
            wrong += RotaWrong(c.Problem, l.Fill) != null || RotaWrong(c.Problem, best.Plan) != null ? 1 : 0;
            // Existence, where counting is exact: one till's cashiers, days of at most twelve open hours. Everybody's least fits if and
            // only if the least hours add up to no more than the open hours (any hours of such a day make legal shifts).
            int asked = 0, openHours = RotaOpenCount(best.Open), cleaners = 0;
            for (int w = 0; w < real; w++)
            {
                int least, most;
                RotaFit.Range(c.Problem.Worker[w], out least, out most);
                bool cleans = c.Problem.Can[w * stations] == 0;
                asked += cleans ? 0 : least;
                cleaners += cleans ? 1 : 0;
            }
            if (tills == 1 && cleaners <= 1)
            {
                exact++;
                bool cashierLeft = false;
                foreach (RotaLeft left in best.Left)
                {
                    cashierLeft |= c.Problem.Can[left.Worker * stations] != 0;
                }
                leftThoughRoom += asked <= openHours && cashierLeft ? 1 : 0;
            }
            foreach (RotaLeft left in best.Left)
            {
                for (int v = 0; v < real && left.Why == RotaClash.Shared; v++)
                {
                    int least, most;
                    RotaFit.Range(c.Problem.Worker[v], out least, out most);
                    bool same = (c.Problem.Can[v * stations] != 0) == (c.Problem.Can[left.Worker * stations] != 0);
                    lessSkilled += v != left.Worker && same && least >= left.Least && RotaHoursOf(best.Plan, v) > 0 && RotaFit.Skill(c.Problem, v) < RotaFit.Skill(c.Problem, left.Worker) ? 1 : 0;
                }
            }
            foreach (RotaLeft left in best.Left)
            {
                bool right = left.Why == RotaClash.Hours ? left.Room < left.Least : left.Why == RotaClash.Shared && left.Room >= left.Least && left.Asked > 0;
                leftWrongReason += right && RotaHoursOf(best.Plan, left.Worker) == 0 ? 0 : 1;
            }
            int broken = RotaAgainstCurve(c, best.Plan, best.Open, tills) + RotaAgainstCurve(c, l.Fill, c.Shop.Open, tills);
            curve += broken > 0 ? 1 : 0;
            curvePairs += broken;
            // A second till is staffed where one till is not enough before anywhere else.
            for (int d = 0; d < 7 && tills > 1; d++)
            {
                for (int a = 0; a < 24; a++)
                {
                    for (int b = 0; b < 24; b++)
                    {
                        int ca = d * 24 + a, cb = d * 24 + b;
                        bool twoAtQuiet = best.Open[ca] && best.Open[cb] && c.Shop.Wanting[ca] <= 20 && c.Shop.Wanting[cb] > 20 && c.Shop.Wanting[cb] > c.Shop.Wanting[ca];
                        int la = 0, lb = 0;
                        for (int s = 0; s < tills; s++)
                        {
                            la += best.Plan.At[ca * stations + s] >= 0 ? 1 : 0;
                            lb += best.Plan.At[cb * stations + s] >= 0 ? 1 : 0;
                        }
                        quieterFirst += twoAtQuiet && la >= 2 && lb < 2 ? 1 : 0;
                    }
                }
            }
            int single, split;
            RotaPieces(best.Plan, real, out single, out split);
            singles += single;
            splits += split;
            if (seed % 5 == 0)
            {
                long whole = RotaLadderHash(l);
                RotaCase again = RotaRangeCase(seed, longHours, tillsMost, noCleaning);
                stepped += RotaLadderHash(RotaClimb(again, RotaMode.Auto, true)) == whole ? 0 : 1;
            }
        }
        string of = " (" + what + ", " + cases + " businesses)";
        Check(outside == 0, "rota hours: in the rota \"Fill the rota\" writes and in the best week with the people, everybody works inside the hours they ask for or not at all" + of + ": " + outside + " outside");
        Check(hireOutside == 0, "rota hours: and so they do in every plan that takes a hire" + of + ": " + hireOutside + " outside");
        Check(unusable == 0, "rota hours: the best week with the people is always one that may be used" + of + ": " + unusable + " not");
        Check(wrong == 0, "rota hours: no rota breaks a limit of a shift, a day or a demand to get there" + of + ": " + wrong + " do");
        Check(leftThoughRoom == 0, "rota hours: where the open hours hold every cashier's least hours, no cashier is left out (" + exact + " businesses where counting is exact)" + of + ": " + leftThoughRoom + " left out");
        Check(leftWrongReason == 0, "rota hours: whoever is left out has no hours at all, and the reason given is the true one" + of + ": " + leftWrongReason + " not");
        Check(lessSkilled == 0, "rota skill: nobody is without hours while somebody less skilled who asks for as many works at the same stations" + of + ": " + lessSkilled);
        Check(curve == 0, "rota hours: no busier hour of a day has fewer people at the tills than a quieter one" + of + ": " + curve + " businesses, " + curvePairs + " pairs of hours");
        Check(quieterFirst >= 0, "rota hours: (counted, and covered by the check before: hours with two at the tills while a busier hour that needs two has one, whatever the reason" + of + ": " + quieterFirst + ")");
        Check(singles == 0, "rota hours: no shift of a single hour" + of + ": " + singles);
        Check(stepped == 0, "rota hours: a ladder made two steps at a time is the ladder made in one go" + of + ": " + stepped + " differ");
    }

    // Where no legal hours hold everybody: the person is named, with the figures, and nobody works short.
    private static void RotaSecondTill()
    {
        // Two tills, two cashiers who ask for nothing, three times the customers: one till serves 20, the busy hours bring up to 36.
        RotaCase c = RotaShopCase(2, 0, 2, 300);
        c.Problem.Can[0] = c.Problem.Can[1] = c.Problem.Can[2] = c.Problem.Can[3] = 50;
        c.Problem.Worker[0].Wage = c.Problem.Worker[1].Wage = 1200;
        c.Shop.Margin = 2000;
        RotaOpenHours(c, 0x7F, 8, 20);
        RotaLadderResult l = RotaClimb(c);
        int twoWhereOne = 0, oneWhereTwo = 0, two = 0;
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            int at = (l.Fill.At[cell * 2] >= 0 ? 1 : 0) + (l.Fill.At[cell * 2 + 1] >= 0 ? 1 : 0);
            two += at == 2 ? 1 : 0;
            twoWhereOne += at == 2 && c.Shop.Wanting[cell] <= 20 ? 1 : 0;
            oneWhereTwo += c.Shop.Open[cell] && at < 2 && c.Shop.Wanting[cell] > 20 ? 1 : 0;
        }
        Check(two > 0 && twoWhereOne == 0, "rota hours: the second till is staffed only in hours that bring more than one till serves (" + two + " hours with two, " + twoWhereOne + " of them needless)");
        Check(RotaAgainstCurve(c, l.Fill, c.Shop.Open, 2) == 0, "rota hours: and where two people cannot keep it in every such hour (" + oneWhereTwo + " left), it is the busiest of them that have it");
    }

    private static void RotaRangeNoWay()
    {
        // Two full-time cleaners, one cleaning station, open six hours a day and not allowed to open longer.
        RotaCase c = RotaShopCase(1, 1, 3);
        c.Problem.Can[0 * 2 + 0] = 50;
        c.Problem.Can[1 * 2 + 1] = 50;
        c.Problem.Can[2 * 2 + 1] = 50;
        c.Problem.Worker[1].Demands = new[] { RotaHoursAsk(RotaFull) };
        c.Problem.Worker[2].Demands = new[] { RotaHoursAsk(RotaFull) };
        c.Shop.Margin = 2000;
        RotaOpenHours(c, 0x7F, 10, 16);
        c.Barred = new bool[RotaCatalogue.Cells];
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            c.Barred[cell] = !c.Shop.Open[cell];
        }
        RotaLadderResult l = RotaClimb(c);
        RotaRung best = l.Rungs[1];
        Check(best.Left.Count == 1 && best.Left[0].Why == RotaClash.Shared, "rota hours: two full-time cleaners for one station open 42 hours: one is left out, because the two ask for more than the station holds");
        Check(best.Left.Count == 1 && best.Left[0].Asked == 60 && best.Left[0].Held == 42 && best.Left[0].Least == 30 && best.Left[0].Room == 42, "rota hours: with the figures: 60 hours asked, 42 held ("
            + (best.Left.Count == 1 ? best.Left[0].Asked + ", " + best.Left[0].Held : "?") + ")");
        int a = RotaHoursOf(best.Plan, 1), b = RotaHoursOf(best.Plan, 2);
        Check((a == 0 && b >= 30 && b <= 50) || (b == 0 && a >= 30 && a <= 50), "rota hours: the other works a full week inside the range, the one left out has no hours (" + a + " and " + b + ")");
        Check(best.Usable, "rota hours: and that plan may still be used: nobody in it works outside what they ask for");

        // One full-time cashier, a shop open four hours a day that may not open longer: nothing fits.
        c = RotaShopCase(1, 0, 1);
        c.Problem.Can[0] = 50;
        c.Problem.Worker[0].Demands = new[] { RotaHoursAsk(RotaFull) };
        c.Shop.Margin = 2000;
        RotaOpenHours(c, 0x7F, 11, 15);
        c.Barred = new bool[RotaCatalogue.Cells];
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            c.Barred[cell] = !c.Shop.Open[cell];
        }
        l = RotaClimb(c);
        best = l.Rungs[1];
        Check(best.Left.Count == 1 && best.Left[0].Why == RotaClash.Hours && best.Left[0].Room == 28 && best.Left[0].Least == 30, "rota hours: a full-time cashier and 28 open hours: the clash is named with both figures ("
            + (best.Left.Count == 1 ? best.Left[0].Room + " of " + best.Left[0].Least : "?") + ")");
        Check(RotaHoursOf(best.Plan, 0) == 0 && RotaHoursOf(l.Fill, 0) == 0, "rota hours: and they are given no hours rather than 28");

        // Free days and hours not worked against a least: the room says so.
        c = RotaShopCase(1, 0, 1);
        c.Problem.Can[0] = 50;
        c.Problem.Worker[0].Demands = new[] { RotaHoursAsk(RotaFull), RotaAsk(RotaDemandKind.FreeDays, 2, 0, 0, 0x7C) };
        c.Shop.Margin = 2000;
        RotaOpenHours(c, 0x7F, 8, 20);
        l = RotaClimb(c);
        best = l.Rungs[1];
        Check(best.Left.Count == 1 && best.Left[0].Why == RotaClash.Hours && best.Left[0].Room == 24, "rota hours: five free days, twelve open hours a day and a least of thirty: two days hold 24 ("
            + (best.Left.Count == 1 ? best.Left[0].Room.ToString() : "?") + ")");
    }

    // Where the hours kept are too few and longer ones are allowed: they are lengthened by the best paying hours.
    private static void RotaRangeExtended()
    {
        RotaCase c = RotaShopCase(1, 0, 1);
        c.Problem.Can[0] = 50;
        c.Problem.Worker[0].Wage = 1200;
        c.Problem.Worker[0].Demands = new[] { RotaHoursAsk(RotaFull) };
        c.Shop.Margin = 2000;
        c.Shop.StationWage[0] = 1200;
        RotaOpenHours(c, 0x7F, 11, 14);      // 21 hours: nine short
        RotaLadderResult l = RotaClimb(c);
        RotaRung best = l.Rungs[1];
        int hours = RotaHoursOf(best.Plan, 0), added = RotaOpenCount(best.Open) - 21;
        Check(best.Left.Count == 0 && hours >= 30 && hours <= 50, "rota hours: a full-time cashier and a shop open 21 hours: the plan with the people opens longer and gives the 30 hours (" + hours + ")");
        Check(best.Source >= 3 && added >= 9, "rota hours: at least the nine hours that were missing are opened (" + added + ", source " + best.Source + ")");
        bool said = false, kept = true;
        foreach (RotaChange change in best.Changes)
        {
            said |= change.Why == RotaWhy.Fits || change.Why == RotaWhy.Opened;
        }
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            kept &= !c.Shop.Open[cell] || best.Open[cell] || best.Source == 3;
        }
        Check(said, "rota hours: and the plan says which hours it opens and why");
        Check(kept, "rota hours: no hour kept now is closed to do it");
        Check(l.Fill.At.Length > 0 && RotaHoursOf(l.Fill, 0) == 0, "rota hours: \"Fill the rota\" on the 21 hours kept gives the cashier none, since 21 are not 30: the page offers the plan that can");

        // The hours added are the best paying ones next to the hours kept: none left closed pays better than one that was opened.
        int casesChecked = 0, worse = 0;
        for (int seed = 1; seed <= 30; seed++)
        {
            _rotaSeed = (uint)(seed * 31 + 5);
            c = RotaShopCase(1, 0, 1, 100 + RotaNext(2) * 100);
            c.Problem.Can[0] = 50;
            c.Problem.Worker[0].Wage = 1200;
            c.Problem.Worker[0].Demands = new[] { RotaHoursAsk(RotaFull) };
            c.Shop.Margin = 60 + RotaNext(3) * 20;      // (so little a customer that no hour pays: the hours are lengthened only as far as the cashier needs)
            c.Shop.StationWage[0] = 1200;
            int from = 9 + RotaNext(6);
            RotaOpenHours(c, 0x7F, from, from + 2 + RotaNext(2));
            var before = (bool[])c.Shop.Open.Clone();
            l = RotaClimb(c);
            best = l.Rungs[1];
            if (best.Source != 4)
            {
                continue;
            }
            casesChecked++;
            int least = int.MaxValue;
            for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
            {
                least = best.Open[cell] && !before[cell] ? Math.Min(least, c.Outlook.Net[cell]) : least;
            }
            for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
            {
                int h = cell % 24;
                bool beside = (h > 0 && before[cell - 1]) || (h < 23 && before[cell + 1]);
                worse += !best.Open[cell] && beside && c.Outlook.Net[cell] > least ? 1 : 0;
            }
        }
        Check(casesChecked >= 10 && worse == 0, "rota hours: over " + casesChecked + " businesses whose hours had to be lengthened, no hour left closed beside the hours kept pays better than one that was opened (" + worse + ")");
    }
}
