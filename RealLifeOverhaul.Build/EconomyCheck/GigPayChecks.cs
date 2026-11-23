using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// Two players' suggestions about what gigs pay. One made over 3,000 in under 48 hours by spamming gigs where the game's own job
// would have given about 300; the owner answered that pay should differ by gig. The other found that the best day's pay came from
// letting the clock run and then finishing the tasks, because a rota shift's basic wage was counted in the hours it took.
// Pay now follows the work (Scripts/Jobs/JobCatalogue.cs: each kind's unit pay by effort and skill), the player's tiredness and
// newness (GigPayRules.cs: Wear and NewHandFactor), and a rota shift's whole work pays its whole basic wage (GigPayRules.WageHours).
// First the rules, then the table, then the earnings of three ways to play over 48 hours and a week against the old table, then the
// sources that call the rules and the texts.
internal static partial class Program
{
    private const double GigCashier = 25.0;   // what the game's own job pays an hour (PlayerActivity/WorkActivity)
    private const double GigVan = 100.0;      // the game's delivery van job, an hour

    private static void GigPay()
    {
        GigPayRulesHeld();
        GigPayTable();
        GigPayEarnings();
        GigPayEarlyFinish();
        GigPaySources();
        GigPayWords();
    }

    // ------------------------------------------------------------------------------------------------- the rules

    private static void GigPayRulesHeld()
    {
        float[] wear = GigPayRules.Wear;
        bool falling = wear[0] == 1f;
        for (int i = 1; i < wear.Length; i++)
        {
            falling &= wear[i] < wear[i - 1] && wear[i] > GigPayRules.WearLast;
        }
        Check(falling && GigPayRules.WearLast > 0f, "gig pay: a rested hand's first shift pays in full and each shift after pays less, never nothing");
        Check(GigPayRules.WearFactor(0f) == 1f && GigPayRules.WearFactor(-3f) == 1f && GigPayRules.WearFactor(0.4f) == 1f,
            "gig pay: no shift done, or under half of one, is not tired");
        Check(GigPayRules.WearFactor(0.5f) == wear[1] && GigPayRules.WearFactor(1f) == wear[1] && GigPayRules.WearFactor(2.4f) == wear[2],
            "gig pay: half a shift counts as one, so a part shift is not free");
        Check(GigPayRules.WearFactor(40f) == GigPayRules.WearLast && GigPayRules.WearFactor(wear.Length) == GigPayRules.WearLast, "gig pay: past the table every shift pays the last share");

        Check(GigPayRules.NewHandFactor(0) == GigPayRules.NewHandStart && GigPayRules.NewHandFactor(-5) == GigPayRules.NewHandStart,
            "gig pay: the very first shift pays the new hand's start share");
        Check(GigPayRules.NewHandFactor(GigPayRules.NewHandShifts) == 1f && GigPayRules.NewHandFactor(10000) == 1f,
            "gig pay: from the " + GigPayRules.NewHandShifts + "th shift on the pay is whole, and a save with a long record is never held back");
        bool climbs = true;
        for (int i = 1; i <= GigPayRules.NewHandShifts; i++)
        {
            climbs &= GigPayRules.NewHandFactor(i) > GigPayRules.NewHandFactor(i - 1);
        }
        Check(climbs, "gig pay: the new hand's share climbs with every shift");
        Check(GigPayRules.NewHandShifts >= 14 && GigPayRules.NewHandShifts <= 28, "gig pay: learning the ropes takes about a week of gigs (two to four shifts a day)");
        Check(GigPayRules.Strain(0f, 100) == 1f && Math.Abs(GigPayRules.Strain(1f, 0) - wear[1] * GigPayRules.NewHandStart) < 1e-6f,
            "gig pay: a veteran's first shift of the day is whole; tiredness and newness multiply");

        JobDef flyers = JobCatalogue.Find(JobCatalogue.Flyers), taxi = JobCatalogue.Find(JobCatalogue.Taxi);
        Check(GigPayRules.ShiftsOf(flyers, 24) == 1f && GigPayRules.ShiftsOf(flyers, 12) == 0.5f && GigPayRules.ShiftsOf(flyers, 0) == 0f,
            "gig pay: a unit count is a part of a shift of that gig");
        Check(GigPayRules.ShiftsOf(taxi, GigOffers.TaxiShift) == 1f, "gig pay: ride-hail, with no set size, counts " + GigOffers.TaxiShift + " fares as a shift");
        Check(GigPayRules.ShiftsLeft(flyers, flyers.DailyUnits) == JobCatalogue.DailyShifts && GigPayRules.ShiftsLeft(flyers, 1) == 1 && GigPayRules.ShiftsLeft(flyers, 0) == 0
              && GigPayRules.ShiftsLeft(taxi, taxi.DailyUnits) == JobCatalogue.DailyShifts, "gig pay: the Gig Line states the shifts left of the day's " + JobCatalogue.DailyShifts);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        float sum = 0f;
        for (int i = 0; i < 300000; i++)
        {
            sum += GigPayRules.Strain((i % 70) * 0.1f, i % 40);
        }
        watch.Stop();
        Check(sum > 0f && watch.Elapsed.TotalMilliseconds < 60.0, "gig pay: 300,000 pay factors take " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms (under 60)");
    }

    // ------------------------------------------------------------------------------------------------- the table

    // A game hour of a gig at its table pay: a shift's pay over its four game hours (a courier's round with its 800 m), ride-hail by the
    // fare, a 600 m fare being about 80 seconds of driving and a real minute a game hour.
    private static double GigTableHour(JobDef def)
    {
        if (def.Id == JobCatalogue.Taxi)
        {
            return (def.UnitPay + def.PayPerMetre * 600.0) / (80.0 / 60.0);
        }
        return (def.UnitPay * def.ShiftUnits + def.PayPerMetre * 800.0) / 4.0;
    }

    private static void GigPayTable()
    {
        var hour = new Dictionary<string, double>();
        foreach (JobDef def in JobCatalogue.All)
        {
            hour[def.Id] = GigTableHour(def);
            Check(def.DailyUnits == (def.ShiftUnits > 0 ? def.ShiftUnits : GigOffers.TaxiShift) * JobCatalogue.DailyShifts,
                "gig pay: " + def.Id + " has " + JobCatalogue.DailyShifts + " shifts a day, stated up front, not an open amount");
            Check(hour[def.Id] <= GigVan, "gig pay: " + def.Id + " at " + hour[def.Id].ToString("F1") + " an hour is not over the game's van job (" + GigVan + ")");
        }
        string line = "";
        foreach (JobDef def in JobCatalogue.All)
        {
            line += " " + def.Id + " " + hour[def.Id].ToString("F1");
        }
        Console.WriteLine("  an hour of each gig at the table:" + line + " (the cashier " + GigCashier + ")");

        // Idle work pays about the cashier's rate, involved work half as much again, work that needs a car, a licence and a reference twice and more.
        Check(hour[JobCatalogue.Flyers] >= GigCashier && hour[JobCatalogue.Busker] >= GigCashier,
            "gig pay: the idle gigs (flyers, busking) still pay the cashier's rate or a little over, so they are worth taking");
        Check(hour[JobCatalogue.Cleaner] >= GigCashier * 1.3 && hour[JobCatalogue.Stocker] >= GigCashier * 1.5 && hour[JobCatalogue.Vendor] >= GigCashier * 1.3,
            "gig pay: the involved gigs (cleaning, shelves, the cart) pay well over the cashier's");
        Check(hour[JobCatalogue.Courier] >= GigCashier * 2.0 && hour[JobCatalogue.Taxi] > hour[JobCatalogue.Courier],
            "gig pay: the gigs that need a car (the courier) pay twice the cashier's, and ride-hail, which also needs a licence, a reference and a clean record, most");
        Check(hour[JobCatalogue.Stocker] > hour[JobCatalogue.Cleaner] && hour[JobCatalogue.Cleaner] > hour[JobCatalogue.Flyers] && hour[JobCatalogue.Vendor] > hour[JobCatalogue.Busker],
            "gig pay: the heavier gig pays more an hour than the lighter (shelves over cleaning over flyers; the cart over busking)");
        double lowest = double.MaxValue, highest = 0.0;
        foreach (double h in hour.Values)
        {
            lowest = Math.Min(lowest, h);
            highest = Math.Max(highest, h);
        }
        Check(highest >= lowest * 2.5, "gig pay: the kinds are not alike: the best pays " + (highest / lowest).ToString("F1") + " times the lowest an hour (at least 2.5)");
        double onFoot = Math.Max(Math.Max(hour[JobCatalogue.Flyers], hour[JobCatalogue.Cleaner]), Math.Max(hour[JobCatalogue.Stocker], Math.Max(hour[JobCatalogue.Vendor], hour[JobCatalogue.Busker])));
        Check(onFoot <= GigCashier * 2.0, "gig pay: on foot no gig pays over twice the cashier an hour (" + onFoot.ToString("F1") + ")");

        Check(JobCatalogue.Find(JobCatalogue.Vendor).ByHour && JobCatalogue.Find(JobCatalogue.Taxi).ByHour && JobCatalogue.Find(JobCatalogue.Security).ByHour,
            "gig pay: a stall (the cart), a cab (ride-hail) and a watch (site security) are paid for being there");
        int byHour = 0;
        foreach (JobDef def in JobCatalogue.Recorded)
        {
            byHour += def.ByHour ? 1 : 0;
        }
        Check(byHour == 3, "gig pay: those three and no other kind is paid by the hour (" + byHour + ")");
        Check(JobCatalogue.LevelBonus <= 0.06f && JobCatalogue.PayFactor(JobCatalogue.MaxLevel) <= 1.2f, "gig pay: the top level adds at most a fifth, so a spammed gig's level is not a second raise");
    }

    // ------------------------------------------------------------------------------------------------- earnings

    // A player who works whenever energy allows (the spamming a player reported), picking the gig that pays most for its time among those open
    // that day, against the game's cashier (200 a day, eight to sixteen, six days) and the two together. One game hour is a minute of play, so a
    // gig shift is about four hours with the walk to it. Energy: awake the game spends 0.056 a minute, a bed gives 0.35, a day's consumables 30,
    // and a gig adds its unit energy; work starts at 8 above the shift's cost (40 above it when the cashier's shift is to be kept).
    private sealed class GigPlay
    {
        public double Money;
        public int GigShifts;
        public int CashShifts;
    }

    private static GigPlay GigPlayed(JobDef[] table, float levelBonus, bool strained, int fromHour, int hours, bool gigs, bool cashier, bool car)
    {
        const double awake = 3.36, workDrain = 3.0, regen = 21.0, coffee = 30.0;
        var play = new GigPlay();
        double t = 8 * 60.0, energy = 100.0, coffeeLeft = coffee;
        int n = table.Length;
        var units = new int[n];
        var shiftsDone = new int[n];
        var today = new int[n];
        int lastDay = 1, cashDay = 0;
        double start = t + fromHour * 60.0, end = start + hours * 60.0;
        double moneyAtStart = 0.0;
        bool marked = fromHour == 0;
        while (t < end)
        {
            if (!marked && t >= start)
            {
                moneyAtStart = play.Money;
                marked = true;
            }
            int day = (int)(t / 1440) + 1;
            if (day != lastDay)
            {
                Array.Clear(today, 0, n);
                lastDay = day;
                coffeeLeft = coffee;
            }
            double hour = (t % 1440) / 60;
            if (cashier && ((day - 1) % 7) < 6 && cashDay != day && hour >= 8 && hour < 12 && energy >= 25)
            {
                t += 8 * 60;
                energy -= 8 * workDrain;
                if (t - 8 * 60 >= start)
                {
                    play.Money += 200;
                    play.CashShifts++;
                }
                cashDay = day;
                continue;
            }
            double nextCash = double.MaxValue;
            if (cashier)
            {
                for (int d = day; d <= day + 2; d++)
                {
                    if (((d - 1) % 7) < 6 && cashDay != d && (d - 1) * 1440.0 + 8 * 60 >= t - 0.001)
                    {
                        nextCash = (d - 1) * 1440.0 + 8 * 60;
                        break;
                    }
                }
            }
            if (gigs)
            {
                int pick = -1, bestSize = 0;
                double best = 0, dur = 0, cost = 0, bestPay = 0;
                int shifts = 0;
                double sumToday = 0;
                for (int i = 0; i < n; i++)
                {
                    shifts += shiftsDone[i];
                    sumToday += GigPayRules.ShiftsOf(table[i], today[i]);
                }
                double strain = strained ? GigPayRules.Strain((float)sumToday, shifts) : 1.0;
                for (int i = 0; i < n; i++)
                {
                    JobDef def = table[i];
                    if ((def.Needs & JobNeed.Car) != 0 && !car)
                    {
                        continue;
                    }
                    if (GigOffers.DayOf(def.Id, day) == GigDay.Closed)
                    {
                        continue;
                    }
                    int left = def.DailyUnits - today[i];
                    if (left <= 0)
                    {
                        continue;
                    }
                    GigOffer o = GigOffers.Draw(def, day, (int)hour, GigWeather.Fair);
                    int size = Math.Min(o.Units > 0 ? o.Units : GigOffers.TaxiShift, left);
                    double pay = GigShiftPay(def, o, size, 1.0 + levelBonus * (JobCatalogue.LevelFor(def, units[i]) - 1), strain);
                    double minutes = def.Id == JobCatalogue.Taxi ? size * 80 : 240.0 * size / Math.Max(1, def.ShiftUnits);
                    double d2 = minutes + ((def.Needs & JobNeed.Car) != 0 ? 30 : 15);
                    double c = awake / 60 * d2 + def.UnitEnergy * size;
                    if (t + d2 > nextCash || energy < c + (cashier ? 40 : 8))
                    {
                        continue;
                    }
                    if (pay / d2 > best)
                    {
                        best = pay / d2;
                        pick = i;
                        dur = d2;
                        cost = c;
                        bestPay = pay;
                        bestSize = size;
                    }
                }
                if (pick >= 0)
                {
                    if (t >= start)
                    {
                        play.Money += bestPay;
                        play.GigShifts++;
                    }
                    units[pick] += bestSize;
                    today[pick] += bestSize;
                    energy -= cost;
                    t += dur;
                    if (GigPayRules.ShiftsOf(table[pick], bestSize) >= 0.99f)
                    {
                        shiftsDone[pick]++;
                    }
                    continue;
                }
            }
            if (energy < 40 && coffeeLeft > 0)
            {
                double add = Math.Min(coffeeLeft, 30);
                energy += add;
                coffeeLeft -= add;
                t += 5;
                continue;
            }
            if (energy < 55)
            {
                double rest = Math.Min((100 - energy) / regen * 60, nextCash - t > 0 ? nextCash - t : 1e9);
                t += rest;
                energy = Math.Min(100, energy + rest / 60 * regen);
                continue;
            }
            double wait = Math.Min(nextCash < double.MaxValue ? Math.Max(5, nextCash - t) : 60, 60);
            t += wait;
            energy = Math.Max(0, energy - awake / 60 * wait);
        }
        play.Money -= moneyAtStart;
        return play;
    }

    // A shift's pay as the offer drew it: the unit pay at the level and the fee, over the units, a big tipper and a speed bonus taken as won,
    // a courier's round with its 800 m, a ride-hail fare with its 600 m.
    private static double GigShiftPay(JobDef def, GigOffer o, int size, double level, double strain)
    {
        double rate = level * o.Fee * strain;
        if (def.Id == JobCatalogue.Taxi)
        {
            return rate * size * (def.UnitPay + def.PayPerMetre * 600) + rate * def.UnitPay * o.TipperUnits;
        }
        return rate * (def.UnitPay * (size + o.TipperUnits + o.SpeedUnits) + def.PayPerMetre * 800 * size / Math.Max(1, def.ShiftUnits));
    }

    // The table before this change: what the first rebalance paid, the unit pay and the day's units of each gig, the level step 8 %.
    private static JobDef[] GigOldTable()
    {
        const string c = "ba:skill_customerservice";
        return new[]
        {
            new JobDef(JobCatalogue.Flyers, "", "", "", JobNeed.Outdoors, 7f, 0f, 0.35f, 0.01f, c, 48, 40, 24, 6),
            new JobDef(JobCatalogue.Cleaner, "", "", "", JobNeed.Outdoors, 20f, 0f, 0.9f, 0.03f, "ba:skill_cleaning", 24, 30, 8, 2),
            new JobDef(JobCatalogue.Stocker, "", "", "", JobNeed.Outdoors, 33f, 0f, 1.3f, 0.02f, c, 18, 20, 6, 2),
            new JobDef(JobCatalogue.Vendor, "", "", "", JobNeed.Outdoors, 18f, 0f, 0.6f, 0.04f, c, 36, 30, 12, 3),
            new JobDef(JobCatalogue.Busker, "", "", "", JobNeed.Outdoors, 17f, 0f, 0.7f, 0f, null, 16, 20, 8, 2),
            new JobDef(JobCatalogue.Courier, "", "", "", JobNeed.Outdoors | JobNeed.Car, 50f, 0.085f, 1.6f, 0.04f, "ba:skill_deliverydriver", 15, 15, 5, 2),
            new JobDef(JobCatalogue.Taxi, "", "", "", JobNeed.Outdoors | JobNeed.Car | JobNeed.Reference | JobNeed.CleanRecord, 22f, 0.195f, 2.2f, 0.05f, c, 14, 12, 0)
        };
    }

    private static void GigPayEarnings()
    {
        JobDef[] old = GigOldTable();
        JobDef[] now = JobCatalogue.All;
        var rows = new List<string>();
        double[,] before = new double[5, 2], after = new double[5, 2];
        string[] ways = { "gigs only, on foot", "gigs only, own car", "starter job only", "both, on foot", "both, own car" };
        for (int w = 0; w < 5; w++)
        {
            bool gigs = w != 2, cashier = w >= 2, car = w == 1 || w == 4;
            for (int h = 0; h < 2; h++)
            {
                int hours = h == 0 ? 48 : 168;
                before[w, h] = GigPlayed(old, 0.08f, false, 0, hours, gigs, cashier, car).Money;
                after[w, h] = GigPlayed(now, JobCatalogue.LevelBonus, true, 0, hours, gigs, cashier, car).Money;
            }
            rows.Add(ways[w] + ": 48 h " + before[w, 0].ToString("F0") + " -> " + after[w, 0].ToString("F0") + ", a week " + before[w, 1].ToString("F0") + " -> " + after[w, 1].ToString("F0"));
        }
        Console.WriteLine("  earnings (before -> after), a player who works whenever energy allows:");
        foreach (string row in rows)
        {
            Console.WriteLine("    " + row);
        }
        double job48 = after[2, 0], job168 = after[2, 1];
        Check(job48 == 400.0 && job168 == 1200.0 && before[2, 0] == 400.0, "gig pay: the game's own job pays 400 in 48 hours and 1,200 in a week, as it always did (25 an hour, 8 to 16, six days)");

        // The report: over 3,000 in under 48 hours against about 300 for the game's job. Before, the same play gave several times the job.
        Check(before[0, 0] >= job48 * 5.5 && before[1, 0] >= job48 * 9.0, "gig pay: before, spamming gigs paid " + (before[0, 0] / job48).ToString("F1") + " times the game's job in 48 hours on foot and "
              + (before[1, 0] / job48).ToString("F1") + " with a car (the reported tenfold)");
        Check(after[0, 0] >= job48 * 1.8 && after[0, 0] <= job48 * 3.0, "gig pay: after, gigs on foot pay " + (after[0, 0] / job48).ToString("F1") + " times the game's job in 48 hours: worth doing, not tenfold");
        Check(after[0, 1] >= job168 * 2.5 && after[0, 1] <= job168 * 3.8, "gig pay: after, gigs on foot pay " + (after[0, 1] / job168).ToString("F1") + " times the game's job in a week");
        Check(after[1, 0] <= job48 * 4.8 && after[1, 1] <= job168 * 5.6 && after[1, 0] > after[0, 0],
            "gig pay: with a car the skilled gigs pay more but still only " + (after[1, 0] / job48).ToString("F1") + " and " + (after[1, 1] / job168).ToString("F1") + " times the game's job");
        Check(after[3, 0] > job48 && after[3, 0] < after[0, 0] && after[4, 1] > after[3, 1], "gig pay: doing both pays more than the job alone and less than gigs alone (the job takes its eight hours)");
        double[] shrink = { after[0, 0] / before[0, 0], after[0, 1] / before[0, 1], after[1, 0] / before[1, 0], after[1, 1] / before[1, 1] };
        Check(shrink[0] < 0.5 && shrink[1] < 0.55 && shrink[2] < 0.45 && shrink[3] < 0.45, "gig pay: gig earnings are about a third to a half of what they were (" + string.Join(", ", Array.ConvertAll(shrink, s => s.ToString("P0")))
              + ") and the job's are unchanged");

        // A first week that does not print money: the second week of the same play pays more than the first, by the new hand's share, and a
        // week in the middle of a long record is the same as the second.
        double first = GigPlayed(now, JobCatalogue.LevelBonus, true, 0, 168, true, false, false).Money;
        double second = GigPlayed(now, JobCatalogue.LevelBonus, true, 168, 168, true, false, false).Money;
        double third = GigPlayed(now, JobCatalogue.LevelBonus, true, 336, 168, true, false, false).Money;
        Console.WriteLine("  gigs on foot, week by week: " + first.ToString("F0") + ", " + second.ToString("F0") + ", " + third.ToString("F0"));
        Check(first < second * 0.9 && Math.Abs(third - second) < second * 0.2, "gig pay: the first week pays less than the second (" + first.ToString("F0") + " against " + second.ToString("F0") + "), the later weeks alike");

        // Diminishing returns in a day: each shift pays less than the one before, whatever the gig.
        double shiftOne = GigPayRules.Strain(0f, 100), shiftFour = GigPayRules.Strain(3f, 100);
        Check(shiftFour <= shiftOne * 0.6, "gig pay: the fourth shift of a day pays " + (shiftFour / shiftOne).ToString("P0") + " of the first");

        // A save in progress keeps its levels: the level is the units done (unchanged), and the pay factor at a level only gets a smaller step.
        JobDef flyers = JobCatalogue.Find(JobCatalogue.Flyers);
        Check(JobCatalogue.LevelFor(flyers, 0) == 1 && JobCatalogue.LevelFor(flyers, 40) == 2 && JobCatalogue.LevelFor(flyers, 80) == 3 && JobCatalogue.LevelFor(flyers, 120) == 4
              && JobCatalogue.LevelFor(flyers, 9999) == JobCatalogue.MaxLevel, "gig pay: levels still come from the units done at the same counts, so a save keeps every level it reached");
        Check(JobCatalogue.PayFactor(1) == 1f && JobCatalogue.PayFactor(2) > 1f, "gig pay: a level still adds to the pay");
    }

    // ------------------------------------------------------------------------------------------------- the end of a shift

    private static void GigPayEarlyFinish()
    {
        const int whole = CareerCatalogue.ShiftHours;
        bool same = true;
        for (int minutes = 1; minutes <= 900; minutes += 7)
        {
            same &= GigPayRules.WageHours(true, false, minutes, false) == whole && GigPayRules.WageHours(true, false, minutes, true) == whole - 1;
        }
        Check(same, "early finish: set work done in full pays the whole " + whole + " hours of basic wage whether it took 1 minute or 15 hours, and one hour less when the start was late");
        Check(GigPayRules.WageHours(true, false, 25f, false) > GigPayRules.WageHours(false, false, 25f, false), "early finish: finishing in half an hour pays more than being stopped at half an hour");
        Check(GigPayRules.WageHours(false, false, 61f, false) == 2 && GigPayRules.WageHours(false, false, 600f, false) == whole && GigPayRules.WageHours(false, false, 0f, false) == 1
              && GigPayRules.WageHours(false, false, 130f, true) == 2, "early finish: work stopped short is paid by the hours begun, one to " + whole + ", as before");
        Check(GigPayRules.WageHours(true, true, 61f, false) == 2 && GigPayRules.WageHours(true, true, 300f, false) == whole && GigPayRules.WageHours(true, true, 10f, false) == 1,
            "early finish: a stall, a cab or a watch is paid by the hour even when its work was done, and time past the shift adds nothing");
        // Waiting out the clock no longer pays more than doing the work.
        for (int h = 1; h <= whole; h++)
        {
            Check(GigPayRules.WageHours(true, false, 20f, false) >= GigPayRules.WageHours(false, false, h * 60f, false), "early finish: doing the work in 20 minutes pays at least what waiting out " + h + " hour(s) does");
        }
        float hourly = 14f;
        Check(CareerCatalogue.Wage(GigPayRules.WageHours(true, false, 90f, false), hourly) == 56, "early finish: a 14 an hour rota shift done in an hour and a half pays 56 in basic wage, not 28");

        // Which kinds count their work whole: every end of a gig that is Done by its set work says so.
        foreach (string file in new[]
                 {
                     @"Jobs\Kinds\FlyerJob.cs", @"Jobs\Kinds\CleanerJob.cs", @"Jobs\Kinds\StockerJob.cs", @"Jobs\Kinds\CourierJob.cs", @"Jobs\Kinds\BuskerJob.cs",
                     @"Jobs\Kinds\DispatchJob.cs", @"Jobs\Kinds\LeadJob.cs", @"Jobs\Kinds\DesignJob.cs", @"Jobs\Kinds\TenderJob.cs", @"Jobs\Kinds\TradeJob.cs"
                 })
        {
            string code = ModSource(file);
            Check(code.Contains("Finish(") && System.Text.RegularExpressions.Regex.IsMatch(code, @"Finish\([^;]*JobEnd\.Done[^;]*, [^;]+\);"), "early finish: " + file + " says whether its set work was done in full when it ends");
        }
        string flyers = ModSource(@"Jobs\Kinds\FlyerJob.cs");
        string timeUp = MemberText(flyers, "if (now >= _endsAt)");
        Check(flyers.Contains("Finish(JobEnd.Done, true);") && !timeUp.Contains("Finish(DoneCount * 3 < _bundle && DoneCount + _refusals < FairTry ? JobEnd.Failed : JobEnd.Done, true)"),
            "early finish: a bundle handed out is whole, a bundle the clock ran out on is not");
    }

    // ------------------------------------------------------------------------------------------------- the sources

    private static void GigPaySources()
    {
        string director = ModSource(@"Jobs\JobDirector.cs");
        string phone = ModSource(@"Jobs\JobPhone.cs");
        string career = ModSource(@"Jobs\CareerDirector.cs");
        string dialog = ModSource(@"Jobs\CareerDialog.cs");
        string state = ModSource(@"Jobs\JobState.cs");
        string job = ModSource(@"Jobs\Job.cs");
        Check(director.Contains("_context.Offer.Fee * (freelance ? JobState.Strain() : 1f);"), "gig pay: a freelance gig is paid at today's tiredness and newness; a rota shift, a booked set, a desk shift and a test are not");
        Check(phone.Contains("offer.Fee * JobState.Strain();"), "gig pay: the Gig Line quotes the pay the shift will be worked at, tiredness and newness included");
        Check(phone.Contains("GigWords.Rule(def)"), "gig pay: every Gig Line offer (and every board's, which asks the same list) states how it is paid");
        Check(career.Contains("GigPayRules.WageHours(job.WholeWork && why == JobEnd.Done, job.Def.ByHour,"), "early finish: a rota shift's wage is asked of the rule, with whether its work was done whole and whether the kind is paid by the hour");
        Check(career.Contains("public static string PayRuleKey(Employer employer, CareerRank rank)") && dialog.Contains("CareerDirector.PayRuleKey(employer, first)")
              && dialog.Contains("CareerDirector.PayRuleKey(employer, CareerState.CurrentRank)"), "gig pay: the Work contact's offer and its status both state how a shift is paid");
        Check(job.Contains("public bool WholeWork { get; private set; }") && job.Contains("WholeWork = wholeWork && outcome == JobEnd.Done;"), "early finish: only an end that is Done can be whole");
        Check(state.Contains("GigPayRules.ShiftsOf(JobCatalogue.All[i], Records[i].Today)") && state.Contains("shifts += Records[i].Shifts;") && state.Contains("private const string Version = \"1\";")
              && !state.Contains("Join(text, record.Units, record.Shifts, record.Strikes, record.SackedUntil, record.Today, 1"),
            "gig pay: tiredness and newness are read from the units and shifts a save already keeps; nothing new is saved and the line's version is the same");
        string pay = ModSource(@"Jobs\JobDirector.Pay.cs");
        Check(pay.Contains("JobState.Worked(def, slip.Units, total)"), "early finish: the level's experience is counted on the slip as before, at the moment it is paid");
    }

    // ------------------------------------------------------------------------------------------------- the texts

    private static void GigPayWords()
    {
        Dictionary<string, string> en = LocaleFile("en");
        foreach (string key in new[] { "reallife_gig_rule_whole", "reallife_gig_rule_hour", "reallife_gig_left", "reallife_gig_wear", "reallife_gig_newhand", "reallife_career_rule_whole", "reallife_career_rule_hour" })
        {
            Check(en.TryGetValue(key, out string text) && text.Length > 10 && !text.Contains("—") && !text.Contains("–") && !text.Contains("!"), "gig pay: the text " + key + " is there, in plain words with no dash or exclamation mark");
        }
        Check(en["reallife_gig_left"].Contains("{left}") && en["reallife_gig_left"].Contains("{most}") && en["reallife_gig_wear"].Contains("{done}") && en["reallife_gig_wear"].Contains("{share}")
              && en["reallife_gig_newhand"].Contains("{share}"), "gig pay: the offer's lines carry the figures they are filled with");
        Check(en["reallife_gig_rule_whole"].Contains("Waiting adds nothing") && en["reallife_gig_rule_hour"].Contains("for being there"), "gig pay: the two rules say what time does and does not pay");
        // Any row of What's new: the version step moves the next row's lines into the version's own row.
        Check(WhatsNewHas(en, "Gigs now pay by effort and skill"), "gig pay: What's new tells of the pay by effort and skill");
        string words = ModSource(@"Jobs\GigWords.cs");
        foreach (string key in new[] { "reallife_gig_rule_whole", "reallife_gig_rule_hour", "reallife_gig_left", "reallife_gig_wear", "reallife_gig_newhand" })
        {
            Check(words.Contains("\"" + key + "\""), "gig pay: " + key + " is written whole in GigWords, never built from a prefix");
        }
        Check(ModSource(@"Jobs\CareerDirector.cs").Contains("\"reallife_career_rule_hour\"") && ModSource(@"Jobs\CareerDirector.cs").Contains("\"reallife_career_rule_whole\""),
            "gig pay: the Work contact's two rules are written whole in CareerDirector");
    }
}
