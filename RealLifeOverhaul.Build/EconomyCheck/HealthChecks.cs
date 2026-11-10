using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Health;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Ui;
using HPlan = RealLifeOverhaul.Health.Plan;

// Health (Scripts/Health, run with --health): the seeded draws and that a load changes nothing, the flu's wave and its bound, what the player's way of living does, a
// case's days (rest against neglect, turning serious only after neglect, never ending a life), injuries, the doctor's appointment, plans and claims (never more than the
// bill, never for what began before the cover), bills and the medical debt (collected whatever the option says), sick leave and sick pay (never farmed), stays in hospital
// and a save in the middle of one, the people's day (every dollar counted, bounded cost, nothing allocated), the staff, the rivals, the save, old saves and the option off,
// the pages and every button, the sources and the texts in every language.
internal static partial class Program
{
    private static readonly string[] HealthLanguages = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };

    private static void Health()
    {
        HealthRuleChecks();
        HealthWaveChecks();
        HealthPlayerChecks();
        HealthCaseChecks();
        HealthAppointmentChecks();
        HealthCoverChecks();
        HealthBillChecks();
        HealthDebtChecks();
        HealthLeaveChecks();
        HealthStayChecks();
        HealthPeopleChecks();
        HealthStaffRivalChecks();
        HealthStoreChecks();
        HealthOptionChecks();
        HealthFixes();
        HealthPageChecks();
        HealthSourceChecks();
        HealthTextChecks();
    }

    // The older checks settle a whole shift of eight hours of which none was worked, and call in sick in the morning of a shift that ends at five.
    private static long HealthSettle(LeaveBook l, LeaveFacts f, int day, int seed, int dayPayCents, int hiredDay, int yearDays) =>
        HealthLeave.Settle(l, f, day, seed, dayPayCents, hiredDay, yearDays, 480, 0);

    private static bool HealthCallIn(LeaveBook l, int day, bool ill, int yearDays) => HealthLeave.CallIn(l, day, ill, yearDays, 8 * 60, 17 * 60);

    // Home's illness mirrored with no record of when it began (the day it is seen is its day).
    private static bool HealthMirror(HealthCase c, Condition home, bool treated, int day) => HealthRules.Mirror(c, home, treated, day, 0);

    private static readonly HealthFactors HealthCareful = new HealthFactors { Energy = 80, Hunger = 80, SleepDebt = 0, Diet = 2, Fitness = 80 };
    private static readonly HealthFactors HealthRunDown = new HealthFactors { Energy = 10, Hunger = 10, SleepDebt = 14, Diet = 0, Fitness = 10 };

    // ---- the draws

    private static void HealthRuleChecks()
    {
        Check(HealthRules.Hash(7, 1, 2, 3) == HealthRules.Hash(7, 1, 2, 3) && HealthRules.Hash(7, 1, 2, 3) != HealthRules.Hash(8, 1, 2, 3) && HealthRules.Hash(7, 1, 2, 3) != HealthRules.Hash(7, 1, 2, 4),
            "health draws: a draw is the same for the same seed, person and day, and another for another");
        bool inside = true;
        for (int i = 0; i < 5000; i++)
        {
            int r = HealthRules.Roll(31, i, 5, 9, 100000);
            float u = HealthRules.Unit(31, i, 3, 0);
            inside &= r >= 0 && r < 100000 && u >= 0f && u < 1f;
        }
        Check(inside && HealthRules.Roll(1, 2, 3, 4, 1) == 0 && HealthRules.Roll(1, 2, 3, 4, 0) == 0, "health draws: a draw is inside its range, and a range of one or none is nothing");
        Check(HealthRules.Unit(5, 40, 3, 0) == HealthRules.Unit(5, 40, 3, 0) && HealthRules.Unit(5, 40, 3, 0) != HealthRules.Unit(5, 41, 3, 0) && HealthRules.Unit(5, 40, 3, 0) != HealthRules.Unit(5, 40, 4, 0),
            "health draws: Home's chance for a day and a slot is the same however often it is asked (loading the day again changes nothing), and another the next day");
        Check(HealthRules.IdOf("emp-12") == HealthRules.IdOf("emp-12") && HealthRules.IdOf("emp-12") != HealthRules.IdOf("emp-13") && HealthRules.IdOf(null) >= 0 && HealthRules.IdOf("x") >= 0,
            "health draws: a member of staff's own number comes from their id, the same on every machine");
        int n = (int)Condition.Count;
        Check(HealthCatalogue.CourseLeast.Length == n && HealthCatalogue.CourseMost.Length == n && HealthCatalogue.Infectious.Length == n && HealthCatalogue.CanTurn.Length == n
              && HealthCatalogue.Injury.Length == n && HealthCatalogue.SeriousAtOnce.Length == n && HealthCatalogue.StayDays.Length == n && HealthCatalogue.HealDays.Length == n
              && HealthCatalogue.NameKeys.Length == n, "health catalogue: every table has a line for each condition");
        int p = (int)HPlan.Count;
        Check(HealthCatalogue.PremiumMonth.Length == p && HealthCatalogue.HospitalShare.Length == p && HealthCatalogue.Excess.Length == p && HealthCatalogue.DoctorShare.Length == p
              && HealthCatalogue.YearCap.Length == p && HealthCatalogue.PlanKeys.Length == p && HealthCatalogue.AgeFrom.Length == HealthCatalogue.AgeShare.Length,
            "health catalogue: every table has a line for each plan");
        bool courses = true;
        for (int c = 1; c < n; c++)
        {
            courses &= HealthCatalogue.CourseLeast[c] >= 1 && HealthCatalogue.CourseMost[c] >= HealthCatalogue.CourseLeast[c] && HealthCatalogue.CourseMost[c] <= HealthCatalogue.CourseLongest
                       && HealthCatalogue.StayDays[c] <= HealthCatalogue.StayMost;
            for (int who = 0; who < 40; who++)
            {
                int days = HealthRules.Course(3, who, 10 + who, (Condition)c);
                courses &= days >= HealthCatalogue.CourseLeast[c] && days <= HealthCatalogue.CourseMost[c];
            }
        }
        Check(courses, "health catalogue: every course is between its least and its most, no longer than the longest, and no stay is longer than " + HealthCatalogue.StayMost + " days");
        Check(HealthRules.MonthDays(60) == 5 && HealthRules.MonthDays(365) == 30 && HealthRules.MonthDays(8) == 3 && HealthRules.YearOf(60, 60) == 0 && HealthRules.YearOf(61, 60) == 1
              && HealthRules.MonthOf(5, 5) == 0 && HealthRules.MonthOf(6, 5) == 1, "health calendar: a month is a twelfth of the save's year (three days at least), and day 61 of a 60-day year is the second year");
        Check(HealthRules.At(80, 1000) == 80 && HealthRules.At(800, 2000) == 1600 && HealthRules.At(800, 500) == 400, "health prices: a fee follows the city's prices, as Prices.At does");
    }

    // ---- the wave

    private static void HealthWaveChecks()
    {
        Check(HealthRules.WaveDays(60) == 7 && HealthRules.WaveDays(365) == 43 && HealthRules.WaveDays(8) == HealthCatalogue.WaveDaysLeast, "health wave: it lasts 12% of the year, six days at least (7 of 60, 43 of 365)");
        bool once = true, winter = true, bounded = true, pure = true;
        foreach (int yearDays in new[] { 60, 120, 365 })
        {
            foreach (int seed in new[] { 3, 77, 9001 })
            {
                for (int year = 0; year < 10; year++)
                {
                    int top = HealthRules.WaveTop(seed, year, yearDays);
                    winter &= top >= year * yearDays + 1 && top <= year * yearDays + Math.Max(1, yearDays / 10) && CalendarRules.Season(top, yearDays) == CalendarRules.Winter;
                    int days = 0, best = 0;
                    for (int day = year * yearDays + 1 - yearDays / 4; day <= (year + 1) * yearDays - yearDays / 4; day++)
                    {
                        int w = day >= 1 ? HealthRules.Wave(seed, day, yearDays) : 0;
                        pure &= day < 1 || w == HealthRules.Wave(seed, day, yearDays);
                        days += w > 0 ? 1 : 0;
                        best = Math.Max(best, w);
                        bounded &= w >= 0 && w <= HealthCatalogue.WaveSeverityMost;
                    }
                    once &= days >= 1 && days <= HealthRules.WaveDays(yearDays) && best == HealthRules.WaveSeverity(seed, year);
                }
            }
        }
        Check(winter, "health wave: each year's wave tops in the first tenth of the year, which the calendar counts as winter");
        Check(once, "health wave: one wave a year, no longer than its days, reaching the year's own severity at its top and nothing outside it");
        Check(bounded && pure, "health wave: the wave is never over " + HealthCatalogue.WaveSeverityMost + " thousandths and is the same for a day however often it is asked");
        // A year's wave that begins before the year turns is read on the last days of the year before: across many seeds, find the waves that do (their top within a half wave of
        // the turn) and read one there. For the others the last day of the year is quiet.
        int half = HealthRules.WaveDays(60) / 2, across = 0, quietLast = 0;
        bool read = true;
        for (int seed = 1; seed <= 400; seed++)
        {
            int top = HealthRules.WaveTop(seed, 1, 60);
            int lastDay = HealthRules.Wave(seed, 60, 60), expected = top - 60 < half ? HealthRules.WaveSeverity(seed, 1) * (half - (top - 60)) / half : 0;
            bool leaks = top - 60 < half;
            across += leaks ? 1 : 0;
            quietLast += leaks ? 0 : 1;
            read &= leaks ? lastDay > 0 && lastDay == expected : lastDay == 0;
            read &= HealthRules.Wave(seed, top, 60) == HealthRules.WaveSeverity(seed, 1);
        }
        Check(across > 20 && quietLast > 20 && read, "health wave: a wave that begins before the year turns is read on the last days of the year before (" + across + " of 400 seeds do, and are read there; the other " + quietLast + " are quiet on the last day)");
    }

    // ---- the player's own chances

    private static void HealthPlayerChecks()
    {
        Check(HealthRules.Lifestyle(HealthFactors.Middling) == 1000 && HealthRules.Lifestyle(HealthCareful) == 562 && HealthRules.Lifestyle(HealthRunDown) == 3000
              && HealthRules.Lifestyle(new HealthFactors { SleepDebt = 9, Diet = 1, Fitness = 50 }) == 2000 && HealthRules.Lifestyle(new HealthFactors { Diet = 0, Fitness = 50 }) == 1500,
            "health living: sleep owed doubles a chance, a poor diet adds half, good food and fitness take a quarter off each, between a half and three times");
        Check(HealthRules.WaveChance(1000, HealthFactors.Middling) == HealthCatalogue.PlayerWavePeak && HealthRules.WaveChance(0, HealthRunDown) == 0
              && HealthRules.ContactChance(5, HealthFactors.Middling) == HealthCatalogue.PlayerContact * HealthCatalogue.PlayerContactMost && HealthRules.ContactChance(0, HealthRunDown) == 0,
            "health living: no wave and nobody ill at home is no chance at all, and at most " + HealthCatalogue.PlayerContactMost + " people at home are counted");
        bool safe = true, same = true;
        for (int seed = 1; seed <= 500; seed++)
        {
            for (int day = 0; day < HealthCatalogue.SafeDays; day++)
            {
                safe &= HealthRules.PlayerCatch(seed, day, 1400, 2, HealthRunDown) == Condition.None;
            }
        }
        int careful = 0, runDown = 0, quiet = 0;
        for (int day = 10; day < 20010; day++)
        {
            Condition a = HealthRules.PlayerCatch(41, day, 1000, 0, HealthCareful), b = HealthRules.PlayerCatch(41, day, 1000, 0, HealthRunDown);
            same &= a == HealthRules.PlayerCatch(41, day, 1000, 0, HealthCareful);
            careful += a != Condition.None ? 1 : 0;
            runDown += b != Condition.None ? 1 : 0;
            quiet += HealthRules.PlayerCatch(41, day, 0, 0, HealthRunDown) != Condition.None ? 1 : 0;
        }
        Check(safe, "health living: nothing is caught in a game's first " + HealthCatalogue.SafeDays + " days");
        Check(same, "health reload: the day's draw is the same however often the day is loaded");
        Check(quiet == 0, "health living: with no wave and nobody ill at home, twenty thousand days bring nothing: illness is never out of the blue");
        Check(careful > 0 && runDown > careful * 3 && careful < 20000 * 12 / 1000 && runDown < 20000 * 6 / 100,
            "health living: at the top of a wave a careful player catches it far less often than a run down one (" + careful + " against " + runDown + " in 20,000 days), so staying well is in the player's hands");
        Check(HealthRules.PlayerCatch(41, 50, 1000, 2, HealthFactors.Middling) == HealthRules.PlayerCatch(41, 50, 1000, 2, HealthFactors.Middling), "health reload: the same day, wave and home give the same answer");
    }

    // ---- a case's days

    private static HealthCase HealthCaseOf(Condition kind, int day)
    {
        var c = new HealthCase();
        HealthMirror(c, kind, false, day);
        return c;
    }

    private static void HealthCaseChecks()
    {
        HealthCase flu = HealthCaseOf(Condition.Flu, 10);
        Check(flu.Open && flu.Kind == Condition.Flu && flu.Began == 10 && !flu.Serious && !HealthMirror(flu, Condition.Flu, true, 12) && flu.Began == 10 && flu.Treated,
            "health case: Home's illness opens the case on its day, and the same illness later only brings its prescription");
        Check(HealthMirror(flu, Condition.Cold, false, 20) && flu.Kind == Condition.Cold && flu.Began == 20 && !flu.Treated && !HealthMirror(new HealthCase(), Condition.None, false, 5)
              && !HealthMirror(new HealthCase(), Condition.Sprain, false, 5), "health case: another illness is another case, and nothing or an injury is never mirrored from Home");
        // Rest against neglect.
        HealthCase c = HealthCaseOf(Condition.Flu, 10);
        Check(HealthRules.CaseDay(c, HealthRunDown, 5, 11) == CaseEvent.None && c.Neglect == 1 && HealthRules.CaseDay(c, HealthRunDown, 5, 11) == CaseEvent.None && c.Neglect == 1,
            "health case: a day without rest or care is a day of neglect, counted once however often the day is lived");
        Check(HealthRules.CaseDay(c, HealthCareful, 5, 12) == CaseEvent.None && c.Neglect == 0 && HealthRules.CaseDay(c, HealthCareful, 5, 13) == CaseEvent.None && c.Neglect == 0,
            "health case: a day of rest takes a day of neglect off, never under nothing");
        Check(HealthRules.Rested(c, HealthCareful) && !HealthRules.Rested(c, HealthRunDown) && HealthRules.Rested(c, new HealthFactors { Treated = true })
              && !HealthRules.Rested(c, new HealthFactors { Energy = 100, Hunger = 100, SleepDebt = HealthCatalogue.RestDebt + 1 })
              && !HealthRules.Rested(c, new HealthFactors { Energy = HealthCatalogue.RestEnergy - 1, Hunger = 100 }) && !HealthRules.Rested(c, new HealthFactors { Energy = 100, Hunger = HealthCatalogue.RestHunger - 1 }),
            "health case: rest is sleep, food and strength together, or a prescription");
        // Turning serious: never before the neglect days, surely by the certain day, never once treated, never for what cannot turn.
        bool neverEarly = true, alwaysBy = true, treatedNever = true, cannotNever = true, carefulNever = true, sticks = true;
        int between = 0;
        for (int seed = 1; seed <= 300; seed++)
        {
            HealthCase n = HealthCaseOf(Condition.Flu, 100);
            int turned = 0;
            for (int d = 1; d <= HealthCatalogue.NeglectCertain + 1 && turned == 0; d++)
            {
                CaseEvent ev = HealthRules.CaseDay(n, HealthRunDown, seed, 100 + d);
                turned = ev == CaseEvent.TurnedSerious ? d : 0;
                neverEarly &= ev != CaseEvent.TurnedSerious || d >= HealthCatalogue.NeglectDays;
            }
            alwaysBy &= turned > 0 && turned <= HealthCatalogue.NeglectCertain && n.Serious;
            between += turned > 0 && turned < HealthCatalogue.NeglectCertain ? 1 : 0;
            for (int d = 10; d < 60; d++)
            {
                sticks &= HealthRules.CaseDay(n, HealthCareful, seed, 100 + d) == CaseEvent.None && n.Serious && n.Open;
            }
            HealthCase t = HealthCaseOf(Condition.Flu, 100);
            HealthRules.Treat(t, 100);
            HealthCase cold = HealthCaseOf(Condition.Cold, 100), back = HealthCaseOf(Condition.BackPain, 100), ok = HealthCaseOf(Condition.FoodPoisoning, 100);
            for (int d = 1; d <= 40; d++)
            {
                treatedNever &= HealthRules.CaseDay(t, HealthRunDown, seed, 100 + d) != CaseEvent.TurnedSerious && !t.Serious;
                cannotNever &= HealthRules.CaseDay(cold, HealthRunDown, seed, 100 + d) != CaseEvent.TurnedSerious && HealthRules.CaseDay(back, HealthRunDown, seed, 100 + d) != CaseEvent.TurnedSerious
                               && !cold.Serious && !back.Serious;
                carefulNever &= HealthRules.CaseDay(ok, HealthCareful, seed, 100 + d) == CaseEvent.None && !ok.Serious;
            }
        }
        Check(neverEarly, "health case: nothing turns serious before " + HealthCatalogue.NeglectDays + " days of neglect, over 300 seeds");
        Check(alwaysBy && between > 30 && between < 300, "health case: neglected, the flu turns serious by day " + HealthCatalogue.NeglectCertain + " at the latest, some sooner (" + between + " of 300)");
        Check(treatedNever, "health case: a treated illness never turns serious, however the player lives");
        Check(cannotNever, "health case: a cold and a bad back never turn serious");
        Check(carefulNever, "health unwinnable: a player who rests and eats never has an illness turn serious, over 300 seeds and forty days each");
        Check(sticks, "health case: a serious case waits for the hospital: it neither ends by itself nor ends a life, however long it is left");
        // Injuries.
        Check(HealthRules.InjuryOf(3, 12, 0) == Condition.None && HealthRules.InjuryOf(3, 24, 0) == Condition.Sprain && HealthRules.InjuryOf(3, 47, 0) == Condition.Sprain
              && (HealthRules.InjuryOf(3, 48, 0) == Condition.BrokenArm || HealthRules.InjuryOf(3, 48, 0) == Condition.Concussion) && HealthRules.InjuryOf(3, 48, 4) == HealthRules.InjuryOf(3, 48, 4),
            "health injury: a hurt under a day is no injury, from a day a sprain, from two days a broken arm or a concussion, the same one for the same injury");
        var hurt = new HealthCase();
        Check(HealthRules.Injure(hurt, Condition.Sprain, 3, 50, 0, false) && hurt.Kind == Condition.Sprain && !hurt.Serious && hurt.Until >= 52 && hurt.Until <= 54 && hurt.Began == 50,
            "health injury: a sprain heals by itself in its two to four days");
        Check(HealthRules.CaseDay(hurt, HealthRunDown, 3, hurt.Until - 1) != CaseEvent.Healed && HealthRules.CaseDay(hurt, HealthRunDown, 3, hurt.Until) == CaseEvent.Healed, "health injury: it is healed on its day and not before");
        var arm = new HealthCase();
        Check(HealthRules.Injure(arm, Condition.BrokenArm, 3, 50, 1, true) && arm.Serious && arm.Work && arm.Until == 0 && !HealthRules.Injure(arm, Condition.Sprain, 3, 51, 2, false)
              && arm.Kind == Condition.BrokenArm && !HealthRules.Injure(arm, Condition.Concussion, 3, 51, 3, false), "health injury: a broken arm needs the hospital at once, and nothing takes a serious case's place");
        HealthCase ill = HealthCaseOf(Condition.Cold, 40);
        Check(HealthRules.Injure(ill, Condition.Sprain, 3, 41, 4, false) && ill.Kind == Condition.Sprain && !HealthMirror(ill, Condition.Flu, false, 42) && ill.Kind == Condition.Sprain
              && !HealthRules.Injure(ill, Condition.Cold, 3, 41, 5, false), "health injury: an injury takes the place of a mild illness, is kept while Home's illness runs, and an illness is never an injury");
        var half = new HealthCase();
        HealthRules.Injure(half, Condition.Sprain, 8, 50, 6, false);
        int until = half.Until;
        HealthRules.Treat(half, 50);
        Check(half.Treated && half.Until == 50 + (until - 50 + 1) / 2 && half.Until < until, "health injury: the doctor halves what is left of a sprain");
        HealthRules.Treat(half, 50);
        Check(half.Until == 50 + (until - 50 + 1) / 2, "health injury: a second visit halves nothing more");
        HealthRules.Discharged(arm, 60);
        Check(arm.Open && !arm.Serious && arm.Treated && arm.Until == 60 + HealthCatalogue.HealDays[(int)Condition.BrokenArm], "health hospital: a broken arm set in hospital heals at home in its days");
        HealthCase sick = HealthCaseOf(Condition.Flu, 10);
        sick.Serious = true;
        HealthRules.Discharged(sick, 20);
        Check(!sick.Open && !sick.Serious, "health hospital: an illness is over at the discharge");
        // An accident at work.
        int none = 0, some = 0, high = 0, tired = 0, arms = 0;
        bool sameDay = true;
        for (int day = 1; day <= 100000; day++)
        {
            none += HealthRules.WorkAccident(9, day, 0, HealthRunDown) != Condition.None ? 1 : 0;
            some += HealthRules.WorkAccident(9, day, 1, HealthCareful) != Condition.None ? 1 : 0;
            Condition h = HealthRules.WorkAccident(9, day, 2, HealthCareful);
            high += h != Condition.None ? 1 : 0;
            arms += h == Condition.BrokenArm ? 1 : 0;
            tired += HealthRules.WorkAccident(9, day, 2, HealthRunDown) != Condition.None ? 1 : 0;
            sameDay &= day > 200 || h == HealthRules.WorkAccident(9, day, 2, HealthCareful);
        }
        Check(none == 0 && some > 250 && some < 560 && high > 1000 && high < 1400 && tired > high * 17 / 10 && arms > 0 && arms < high / 2 && sameDay,
            "health work: no accident at a safe job, 0.4% a shift at a risky one and 1.2% at the riskiest, twice that worked without rest, one draw a day (" + some + ", " + high + ", " + tired + " in 100,000 shifts)");
    }

    // ---- the doctor's appointment

    private static void HealthAppointmentChecks()
    {
        HealthDay.Slot(10, 8, out int d1, out int h1);
        HealthDay.Slot(10, 14, out int d2, out int h2);
        HealthDay.Slot(10, 15, out int d3, out int h3);
        HealthDay.Slot(10, 2, out int d4, out int h4);
        Check(d1 == 10 && h1 == 10 && d2 == 10 && h2 == 16 && d3 == 11 && h3 == HealthCatalogue.AppointmentHour && d4 == 10 && h4 == HealthCatalogue.AppointmentHour,
            "health appointment: two hours on today while the surgery has hours, never before nine, else the first hour tomorrow");
        var a = new HealthAppointment();
        Check(!HealthDay.Book(a, 10, 8, string.Empty, 0) && !a.Booked && HealthDay.Book(a, 10, 8, "Park Avenue", 12) && a.Booked && a.Day == 10 && a.Hour == 10 && !HealthDay.Book(a, 10, 9, "Park Avenue", 12),
            "health appointment: one appointment at a time, and none without a hospital or clinic in the city");
        int at = 10 * 60;
        Check(HealthDay.InWindow(a, 10, at) && HealthDay.InWindow(a, 10, at - HealthCatalogue.AppointmentEarly) && HealthDay.InWindow(a, 10, at + HealthCatalogue.AppointmentLate)
              && !HealthDay.InWindow(a, 10, at - HealthCatalogue.AppointmentEarly - 1) && !HealthDay.InWindow(a, 10, at + HealthCatalogue.AppointmentLate + 1) && !HealthDay.InWindow(a, 11, at)
              && !HealthDay.InWindow(new HealthAppointment(), 10, at), "health appointment: kept from twenty minutes before its hour to forty five after, on its day");
        Check(!HealthDay.Missed(a, 10, at + HealthCatalogue.AppointmentLate) && HealthDay.Missed(a, 10, at + HealthCatalogue.AppointmentLate + 1) && HealthDay.Missed(a, 11, 0) && !HealthDay.Missed(a, 9, 1439),
            "health appointment: missed once its window is past, and not before");
        a.Kept = true;
        Check(!HealthDay.Missed(a, 11, 0), "health appointment: one that was kept is never missed");
        a.Clear();
        Check(!a.Booked && a.Street.Length == 0 && !HealthDay.Missed(a, 99, 0), "health appointment: cancelled, nothing is booked and nothing is owed");
    }

    // ---- plans and claims

    private static void HealthCoverChecks()
    {
        Check(HealthCover.AgeShare(20) == 800 && HealthCover.AgeShare(30) == 1000 && HealthCover.AgeShare(39) == 1000 && HealthCover.AgeShare(40) == 1200 && HealthCover.AgeShare(65) == 1900
              && HealthCover.AgeShare(90) == 2400 && HealthCover.AgeShare(-3) == 800, "health premium: priced by age, from four fifths under thirty to 2.4 times from seventy");
        Check(HealthCover.Premium(HPlan.None, 30, 0, 1000) == 0 && HealthCover.Premium(HPlan.Basic, 30, 0, 1000) == 60 && HealthCover.Premium(HPlan.Standard, 30, 0, 1000) == 140
              && HealthCover.Premium(HPlan.Full, 30, 0, 1000) == 290 && HealthCover.Premium(HPlan.Standard, 20, 0, 1000) == 112 && HealthCover.Premium(HPlan.Standard, 70, 0, 1000) == 336,
            "health premium: 60, 140 and 290 a month at thirty, by the age's share");
        Check(HealthCover.Premium(HPlan.Standard, 30, 1, 1000) == 161 && HealthCover.Premium(HPlan.Standard, 30, 4, 1000) == 224 && HealthCover.Premium(HPlan.Standard, 30, 40, 1000) == 224
              && HealthCover.Premium(HPlan.Standard, 30, -2, 1000) == 140 && HealthCover.Premium(HPlan.Standard, 30, 0, 2000) == 280,
            "health premium: priced by record, 15% more for each claim of the last year up to 60%, and following the city's prices");
        var policy = new HealthPolicy();
        policy.ClaimDays.AddRange(new[] { 10, 50, 69, 100 });
        Check(HealthCover.ClaimsLastYear(policy, 100, 60) == 3 && HealthCover.ClaimsLastYear(policy, 400, 60) == 0, "health premium: only the claims of the last year count");
        // What a plan pays.
        bool never = true, shares = true;
        long[] bills = { 0, 1, 79, 80, 125, 250, 499, 500, 501, 1700, 2000, 5300, 99999, 4000000 };
        foreach (HPlan plan in new[] { HPlan.None, HPlan.Basic, HPlan.Standard, HPlan.Full })
        {
            foreach (Care care in new[] { Care.Doctor, Care.Hospital })
            {
                foreach (long bill in bills)
                {
                    foreach (int level in new[] { 500, 1000, 2600 })
                    {
                        foreach (long paid in new long[] { 0, 5000, 19990, 1000000 })
                        {
                            long claim = HealthCover.Claim(plan, care, bill, paid, level);
                            never &= claim >= 0 && claim <= bill && (plan != HPlan.None || claim == 0);
                            never &= claim <= Math.Max(0, HealthRules.At(HealthCatalogue.YearCap[(int)plan], level) - paid);
                        }
                    }
                }
            }
        }
        Check(never, "health insurance: a claim is never over the bill, never under nothing, never over what is left of the year's cap, and nothing without a plan (every plan, bill, price level and year)");
        shares &= HealthCover.Claim(HPlan.Basic, Care.Doctor, 80, 0, 1000) == 0 && HealthCover.Claim(HPlan.Standard, Care.Doctor, 80, 0, 1000) == 40 && HealthCover.Claim(HPlan.Full, Care.Doctor, 80, 0, 1000) == 64;
        shares &= HealthCover.Claim(HPlan.Basic, Care.Hospital, 2600, 0, 1000) == 1470 && HealthCover.Claim(HPlan.Standard, Care.Hospital, 2600, 0, 1000) == 1997 && HealthCover.Claim(HPlan.Full, Care.Hospital, 2600, 0, 1000) == 2500;
        shares &= HealthCover.Claim(HPlan.Basic, Care.Hospital, 400, 0, 1000) == 0 && HealthCover.Claim(HPlan.Basic, Care.Hospital, 5000, 19000, 1000) == 1000 && HealthCover.Claim(HPlan.Basic, Care.Hospital, 5000, 20000, 1000) == 0;
        Check(shares, "health insurance: the basic plan pays 70% of a stay over 500 and nothing of the doctor, the standard 85% over 250 and half the doctor, the full all over 100 and four fifths, each up to its year's cap");
        Check(HealthCover.StayBill(1, false, 1000) == 1700 && HealthCover.StayBill(2, false, 1000) == 2600 && HealthCover.StayBill(2, true, 1000) == 900 && HealthCover.StayBill(1, true, 1000) == 0
              && HealthCover.StayBill(9, false, 1000) == HealthCatalogue.Admission + HealthCatalogue.StayMost * HealthCatalogue.StayDay && HealthCover.StayBill(2, false, 2000) == 5200,
            "health hospital: a stay costs 800 and 900 a day, five days at most, and the game's own first night is the game's bill");
        // Buying cover after falling ill does not cover that illness.
        Check(!HealthCover.Covers(HPlan.Full, 10, 9) && !HealthCover.Covers(HPlan.Full, 10, 10) && !HealthCover.Covers(HPlan.Full, 10, 12) && HealthCover.Covers(HPlan.Full, 10, 13)
              && !HealthCover.Covers(HPlan.None, 10, 50) && !HealthCover.Covers(HPlan.Full, 0, 50), "health insurance: nothing that began before the plan or in its first " + HealthCatalogue.WaitDays + " days is covered");
        Check(!HealthCover.EmployerCovers(HPlan.Standard, 10, 10) && HealthCover.EmployerCovers(HPlan.Standard, 10, 11) && !HealthCover.EmployerCovers(HPlan.None, 10, 50) && !HealthCover.EmployerCovers(HPlan.Standard, 0, 50),
            "health insurance: an employer's plan covers what begins after the job did");
        var s = new HealthState();
        s.Case.Kind = Condition.Flu;
        s.Case.Began = 20;
        s.Case.Serious = true;
        bool took = HealthDay.TakePlan(s, HPlan.Full, 21, 30, 60, 5, 1000, amount => true, out int first);
        BillResult late = HealthCover.Settle(s, Care.Hospital, 2600, s.Case.Began, false, HPlan.None, 0, 23, 60, 1000);
        Check(took && first == 290 && s.Policy.Plan == HPlan.Full && late.Claim == 0 && late.Bill == 2600 && s.Policy.PaidYear == 0 && s.Policy.ClaimDays.Count == 0,
            "health exploit: cover bought after falling ill pays nothing of that illness's stay, however long the player then waits to be admitted");
        BillResult later = HealthCover.Settle(s, Care.Hospital, 2600, 60, false, HPlan.None, 0, 62, 60, 1000);
        Check(later.Claim == 2500 && later.By == HPlan.Full && !later.Employer && s.Policy.PaidYear == 2500 && s.Policy.ClaimDays.Count == 1 && s.Policy.Year == 1,
            "health insurance: what begins after the first days is covered, counted against the year's cap and kept for the next premium");
        BillResult work = HealthCover.Settle(new HealthState(), Care.Hospital, 2600, 60, true, HPlan.None, 0, 62, 60, 1000);
        Check(work.Claim == 2600 && work.Employer, "health work: an accident at work is the employer's insurer's in full, and never more than the bill");
        var both = new HealthState();
        HealthCover.Take(both.Policy, HPlan.Basic, 10, 5);
        BillResult better = HealthCover.Settle(both, Care.Hospital, 2600, 60, false, HPlan.Standard, 20, 62, 60, 1000);
        Check(better.Claim == 1997 && better.Employer && better.By == HPlan.Standard && both.Policy.PaidYear == 0 && both.Policy.ClaimDays.Count == 0,
            "health insurance: the plan that pays more of a bill pays it, once: the two never add up");
        // A plan taken.
        var poor = new HealthState();
        Check(!HealthDay.TakePlan(poor, HPlan.Standard, 30, 30, 60, 5, 1000, amount => false, out int asked) && asked == 140 && poor.Policy.Plan == HPlan.None && poor.Policy.Since == 0,
            "health insurance: a player who cannot pay the first month has no cover");
        Check(!HealthDay.TakePlan(s, HPlan.Full, 30, 30, 60, 5, 1000, amount => true, out int _) && !HealthDay.TakePlan(s, HPlan.None, 30, 30, 60, 5, 1000, amount => true, out int _)
              && !HealthDay.TakePlan(s, HPlan.Count, 30, 30, 60, 5, 1000, amount => true, out int _), "health insurance: the plan held is not taken again, and nothing is not a plan");
        var change = new HealthState();
        HealthDay.TakePlan(change, HPlan.Basic, 10, 30, 60, 5, 1000, amount => true, out int _);
        HealthDay.TakePlan(change, HPlan.Full, 40, 30, 60, 5, 1000, amount => true, out int _);
        Check(change.Policy.Since == 40 && change.Policy.NextDue == 45 && !HealthCover.Covers(change.Policy.Plan, change.Policy.Since, 41),
            "health exploit: changing to a better plan is a new policy, so its first days cover nothing new and what is under way is never covered");
        // The premium's month, and the lapse.
        var m = new HealthState();
        HealthDay.TakePlan(m, HPlan.Standard, 10, 30, 60, 5, 1000, amount => true, out int _);
        int paid5 = 0;
        for (int day = 11; day <= 70; day++)
        {
            paid5 += HealthDay.Premium(m, day, 30, 60, 5, 1000, amount => true, out int _) == PremiumResult.Paid ? 1 : 0;
        }
        Check(paid5 == 12 && m.Policy.NextDue == 75 && m.Policy.Missed == 0, "health premium: one premium every month across the year's turn, 12 in 60 days of a 60-day year");
        Check(HealthDay.Premium(m, 200, 30, 60, 5, 1000, amount => true, out int once) == PremiumResult.Paid && once == 140 && m.Policy.NextDue > 200 && m.Policy.NextDue <= 205
              && HealthDay.Premium(m, 200, 30, 60, 5, 1000, amount => true, out int _) == PremiumResult.None, "health premium: days away are one premium, not one for each month missed, and never twice in a day");
        var lapse = new HealthState();
        HealthDay.TakePlan(lapse, HPlan.Standard, 10, 30, 60, 5, 1000, amount => true, out int _);
        Check(HealthDay.Premium(lapse, 15, 30, 60, 5, 1000, amount => false, out int _) == PremiumResult.Missed && lapse.Policy.Plan == HPlan.Standard && lapse.Policy.Missed == 1
              && HealthDay.Premium(lapse, 20, 30, 60, 5, 1000, amount => false, out int _) == PremiumResult.Lapsed && lapse.Policy.Plan == HPlan.None,
            "health premium: a premium missed is a warning, the second running ends the plan");
        var kept = new HealthState();
        HealthDay.TakePlan(kept, HPlan.Standard, 10, 30, 60, 5, 1000, amount => true, out int _);
        HealthDay.Premium(kept, 15, 30, 60, 5, 1000, amount => false, out int _);
        Check(HealthDay.Premium(kept, 20, 30, 60, 5, 1000, amount => true, out int _) == PremiumResult.Paid && kept.Policy.Missed == 0, "health premium: a premium paid clears the one missed");
    }

    // ---- a bill met

    private static void HealthBillChecks()
    {
        var s = new HealthState();
        long took = 0;
        BillResult r = HealthDay.Bill(s, Care.Hospital, 2600, false, HPlan.None, 0, 50, 60, 5, 1000, 100000, amount => { took += amount; return true; });
        Check(r.Bill == 2600 && r.Claim == 0 && r.Paid == 2600 && r.Owed == 0 && took == 2600 && s.Debt.Owed == 0 && s.BillsYear == 2600 && s.ClaimsYear == 0,
            "health bill: without a plan the whole bill is the player's");
        took = 0;
        var part = new HealthState();
        BillResult p = HealthDay.Bill(part, Care.Hospital, 2600, false, HPlan.None, 0, 50, 60, 5, 1000, 1000, amount => { took += amount; return true; });
        Check(p.Paid == 1000 && p.Owed == 1600 && took == 1000 && part.Debt.Owed == 1600 && part.Debt.PerDay == 160 && p.Claim + p.Paid + p.Owed == p.Bill,
            "health bill: a bill is paid from the cash that is there and the rest is owed, over two months with no interest (160 a day of 1,600)");
        took = 0;
        var broke = new HealthState();
        BillResult b = HealthDay.Bill(broke, Care.Hospital, 5300, false, HPlan.None, 0, 50, 60, 5, 1000, 0, amount => { took += amount; return true; });
        Check(b.Paid == 0 && b.Owed == 5300 && took == 0 && broke.Debt.Owed == 5300, "health unwinnable: with no cash at all nothing is taken: the dearest stay is a debt of 5,300, never a wallet under nothing");
        var refused = new HealthState();
        BillResult f = HealthDay.Bill(refused, Care.Hospital, 900, false, HPlan.None, 0, 50, 60, 5, 1000, 5000, amount => false);
        Check(f.Paid == 0 && f.Owed == 900 && refused.Debt.Owed == 900, "health bill: a payment the game refuses leaves the bill owed, never lost");
        took = 0;
        var pre = new HealthState();
        HealthCover.Take(pre.Policy, HPlan.Standard, 10, 5);
        BillResult d = HealthDay.Bill(pre, Care.Doctor, 80, true, HPlan.None, 0, 50, 60, 5, 1000, 100000, amount => { took += amount; return true; });
        Check(d.Claim == 40 && d.Paid == 40 && d.Owed == 0 && took == 0 && pre.Debt.Owed == 0, "health bill: the doctor's fee is paid at the desk, so only the plan's part comes back and nothing is taken twice");
        // Insurance is never a profit: over many bills what the plans pay is never more than the bills, and what the player keeps is never more than they had.
        bool noProfit = true;
        for (int seed = 1; seed <= 60; seed++)
        {
            var w = new HealthState();
            long wallet = 500000, start = wallet;
            HealthDay.TakePlan(w, (HPlan)(1 + seed % 3), 1, 20 + seed, 60, 5, 1000, amount => { wallet -= amount; return true; }, out int _);
            long billed = 0, claimed = 0;
            for (int day = 2; day < 300; day++)
            {
                HealthDay.Premium(w, day, 20 + seed, 60, 5, 1000, amount => { wallet -= amount; return true; }, out int _);
                if (HealthRules.Roll(seed, day, 1, 1, 6) == 0)
                {
                    bool hospital = HealthRules.Roll(seed, day, 2, 2, 2) == 0;
                    long bill = hospital ? HealthCover.StayBill(1 + HealthRules.Roll(seed, day, 3, 3, 5), false, 1000) : 80;
                    if (!hospital)
                    {
                        wallet -= bill;   // paid at the desk
                    }
                    BillResult x = HealthDay.Bill(w, hospital ? Care.Hospital : Care.Doctor, bill, !hospital, seed % 2 == 0 ? HPlan.Standard : HPlan.None, 1, day, 60, 5, 1000, wallet,
                        amount => { wallet -= amount; return true; });
                    wallet += hospital ? 0 : x.Claim;
                    billed += x.Bill;
                    claimed += x.Claim;
                    noProfit &= x.Claim <= x.Bill && x.Claim + x.Paid + x.Owed == x.Bill;
                }
            }
            noProfit &= claimed <= billed && wallet <= start && w.ClaimsYear <= w.BillsYear;
        }
        Check(noProfit, "health exploit: insurance is never a profit: over 60 lives of bills the plans never paid more than a bill, the parts always made the bill, and nobody ended with more than they began with");
    }

    // ---- the medical debt

    private static void HealthDebtChecks()
    {
        var s = new HealthState();
        HealthCover.Owe(s.Debt, 1600, 5, 50);
        Check(s.Debt.Owed == 1600 && s.Debt.PerDay == 160 && HealthCover.Instalment(s.Debt, 50) == 0 && HealthCover.Instalment(s.Debt, 51) == 160, "health debt: the first instalment is the day after the bill");
        Check(HealthDay.Debt(s, 51, amount => true, null, out long a1) == DebtResult.Paid && a1 == 160 && s.Debt.Owed == 1440 && HealthDay.Debt(s, 51, amount => true, null, out long a2) == DebtResult.None && a2 == 0,
            "health debt: one instalment a day, never two");
        int days = 0;
        for (int day = 52; day < 80 && s.Debt.Owed > 0; day++)
        {
            HealthDay.Debt(s, day, amount => true, null, out long _);
            days++;
        }
        Check(s.Debt.Owed == 0 && days == 9 && s.Debt.Paid == 1600 && s.Debt.PerDay == 0, "health debt: paid off in the ten days of two months, to the dollar and with no interest");
        HealthCover.Owe(s.Debt, 30, 5, 80);
        Check(s.Debt.PerDay == HealthCatalogue.DebtLeastADay, "health debt: an instalment is ten a day at least");
        // No hiding from the debt: late days lead to court, with the option on or off (the director runs this very step either way).
        var late = new HealthState();
        HealthCover.Owe(late.Debt, 2000, 5, 10);
        int filedFor = 0;
        DebtResult last = DebtResult.None;
        int courtDay = 0;
        for (int day = 11; day <= 30 && courtDay == 0; day++)
        {
            last = HealthDay.Debt(late, day, amount => false, amount => { filedFor = amount; return 77; }, out long _);
            courtDay = last == DebtResult.Court ? day : 0;
        }
        Check(courtDay == 10 + HealthCatalogue.DebtClaimAfter && filedFor == 2000 && late.Debt.Owed == 0 && late.Debt.Claim == 77 && late.Debt.Late == 0,
            "health exploit: nobody hides from a medical debt: after " + HealthCatalogue.DebtClaimAfter + " days running with nothing paid the whole of it goes to court, and is the court's from then on");
        var noCourt = new HealthState();
        HealthCover.Owe(noCourt.Debt, 2000, 5, 10);
        for (int day = 11; day <= 40; day++)
        {
            HealthDay.Debt(noCourt, day, amount => false, amount => 0, out long _);
        }
        Check(noCourt.Debt.Owed == 2000 && noCourt.Debt.Late == 30 && HealthDay.Debt(noCourt, 41, amount => true, amount => 0, out long back) == DebtResult.Paid && back == 200 && noCourt.Debt.Late == 0,
            "health debt: with no court to go to the debt stays owed and is tried again every day, and a day paid clears the late days");
        var all = new HealthState();
        HealthCover.Owe(all.Debt, 900, 5, 10);
        Check(!HealthDay.PayOff(all, 12, amount => false, out long _) && all.Debt.Owed == 900 && HealthDay.PayOff(all, 12, amount => true, out long whole) && whole == 900 && all.Debt.Owed == 0
              && !HealthDay.PayOff(all, 12, amount => true, out long _), "health debt: it can be paid off in one go, once, and only with the cash");
        var twice = new HealthState();
        HealthCover.Owe(twice.Debt, 1000, 5, 10);
        HealthCover.Owe(twice.Debt, 600, 5, 12);
        HealthCover.Owe(twice.Debt, -50, 5, 12);
        Check(twice.Debt.Owed == 1600 && twice.Debt.PerDay == 160, "health debt: a second bill owed is added to the first, and a negative one is nothing");
    }

    // ---- sick leave

    private static readonly LeaveFacts HealthIll = new LeaveFacts { Ill = true };
    private static readonly LeaveFacts HealthWell = new LeaveFacts();

    private static void HealthLeaveChecks()
    {
        Check(HealthLeave.PaidDays(60) == 2 && HealthLeave.PaidDays(120) == 3 && HealthLeave.PaidDays(365) == 10 && HealthLeave.PaidDays(8) == HealthCatalogue.PaidDaysLeast,
            "health leave: paid sick days are 3% of the year, two at least (2 of 60, 10 of 365)");
        var l = new LeaveBook();
        Check(HealthLeave.Covers(l, HealthIll, 100, 5) && !HealthLeave.Covers(l, HealthWell, 100, 5), "health leave: a day off ill is excused, a day off well is not");
        long pay1 = HealthSettle(l, HealthIll, 100, 5, 12000, 50, 60);
        long again = HealthSettle(l, HealthIll, 100, 5, 12000, 50, 60);
        Check(pay1 == 12000 && again == 0 && l.PaidUsed == 1 && l.SpellDays == 1 && HealthLeave.Covers(l, HealthWell, 100, 5), "health leave: a sick day is paid the day's pay, once, and stays excused as it was settled");
        long pay2 = HealthSettle(l, HealthIll, 101, 5, 12000, 50, 60);
        Check(pay2 == 12000 && l.PaidUsed == 2 && l.SpellDays == 2 && !HealthLeave.Covers(l, HealthIll, 102, 5), "health leave: after " + HealthCatalogue.SelfCertDays + " sick working days running a doctor's note is needed");
        long pay3 = HealthSettle(l, HealthIll, 102, 5, 12000, 50, 60);
        Check(pay3 == 0 && !l.LastCovered && l.SpellDays == 3, "health leave: the third day without a note is not excused and not paid");
        var noted = new LeaveBook();
        HealthSettle(noted, HealthIll, 100, 5, 12000, 50, 365);
        HealthSettle(noted, HealthIll, 101, 5, 12000, 50, 365);
        Check(HealthLeave.Covers(noted, new LeaveFacts { Ill = true, Note = true }, 102, 5) && HealthSettle(noted, new LeaveFacts { Ill = true, Note = true }, 102, 5, 12000, 50, 365) == 12000,
            "health leave: with a doctor's note the spell goes on excused and paid");
        Check(HealthLeave.Covers(new LeaveBook(), new LeaveFacts { InHospital = true }, 100, 5), "health leave: a day in hospital is excused");
        var hosp = new LeaveBook();
        HealthLeave.Note(hosp, 100, 104);
        Check(HealthLeave.Noted(hosp, 100) && HealthLeave.Noted(hosp, 104) && !HealthLeave.Noted(hosp, 99) && !HealthLeave.Noted(hosp, 105) && HealthLeave.Covers(hosp, HealthWell, 103, 5)
              && !HealthLeave.Covers(hosp, HealthWell, 105, 5), "health leave: a hospital's note covers its own days and no others");
        HealthLeave.Note(hosp, 103, 108);
        Check(hosp.NoteFrom == 100 && hosp.NoteUntil == 108, "health leave: a second stay's note joins the first's and never shortens it");
        var gap = new LeaveBook();
        HealthSettle(gap, HealthIll, 100, 5, 0, 50, 60);
        HealthSettle(gap, HealthIll, 101, 5, 0, 50, 60);
        Check(HealthLeave.SpellBefore(gap, 104) == 2 && HealthLeave.SpellBefore(gap, 105) == 0 && HealthLeave.Covers(gap, HealthIll, 105, 5), "health leave: a spell ends after " + HealthCatalogue.SpellGap + " days back at work, and a new one begins");
        // Sick pay is never farmed.
        var early = new LeaveBook();
        Check(HealthSettle(early, HealthIll, 52, 5, 12000, 50, 60) == 0 && early.LastCovered && early.PaidUsed == 0, "health exploit: no sick pay in a job's first " + HealthCatalogue.QualifyDays + " days (excused, unpaid)");
        var shut = new LeaveBook();
        Check(HealthSettle(shut, HealthWell, 100, 5, 12000, 50, 60) == 0 && shut.SpellLast == 0 && shut.SpellDays == 0 && shut.PaidUsed == 0,
            "health leave: a day missed for no sick reason (the place shut, the internet down) is nothing of the leave's: no spell, no pay");
        bool bounded = true;
        long most = 0;
        foreach (int yearDays in new[] { 60, 365 })
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var farm = new LeaveBook();
                long paid = 0;
                int excused = 0, year = -1, inYear = 0, paidInYear = 0;
                for (int day = yearDays + 1; day <= yearDays * 4; day++)   // three whole years
                {
                    if (HealthRules.YearOf(day, yearDays) != year)
                    {
                        year = HealthRules.YearOf(day, yearDays);
                        inYear = paidInYear = 0;
                    }
                    HealthCallIn(farm, day, false, yearDays);   // every single day, well
                    long cents = HealthSettle(farm, HealthWell, day, seed, 12000, 10, yearDays);
                    paid += cents;
                    paidInYear += cents > 0 ? 1 : 0;
                    excused += farm.LastCovered ? 1 : 0;
                    inYear++;
                    bounded &= cents <= 12000 && paidInYear <= HealthLeave.PaidDays(yearDays) && farm.PaidUsed <= HealthLeave.PaidDays(yearDays);
                }
                bounded &= farm.Noticed > 0 && excused < yearDays * 3 / 2;
                most = Math.Max(most, paid);
            }
        }
        Check(bounded && most <= 3L * HealthLeave.PaidDays(365) * 12000, "health exploit: calling in sick every day of three years is paid for the year's paid days and no more (" + most / 100 + " at most), and is seen through most days");
        Check(HealthLeave.NoticeChance(0) == 200 && HealthLeave.NoticeChance(1) == 400 && HealthLeave.NoticeChance(9) == HealthCatalogue.NoticeMost && HealthLeave.NoticeChance(-4) == 200,
            "health leave: a day called in while well is seen through one time in five, then two in five, up to four in five");
        Check(HealthLeave.SeenThrough(3, 100, 2) == HealthLeave.SeenThrough(3, 100, 2), "health reload: whether the employer sees through a day is the day's own draw, the same after every load");
        int seen = 0;
        for (int day = 1; day <= 4000; day++)
        {
            seen += HealthLeave.SeenThrough(3, day, 0) ? 1 : 0;
        }
        Check(seen > 650 && seen < 950, "health leave: the first such day of a year is seen through about one time in five (" + seen + " of 4,000)");
        var call = new LeaveBook();
        Check(HealthCallIn(call, 100, false, 60) && !HealthCallIn(call, 100, false, 60) && call.Claims == 1 && HealthCallIn(call, 101, true, 60) && call.Claims == 1,
            "health leave: calling in sick is once a day, and a day called in while really ill is no mark against the player");
        var seenBook = new LeaveBook();
        int markDay = 0;
        for (int day = 200; day < 260 && markDay == 0; day++)
        {
            markDay = HealthLeave.SeenThrough(5, day, 0) ? day : 0;
        }
        HealthCallIn(seenBook, markDay, false, 365);
        Check(markDay > 0 && !HealthLeave.Covers(seenBook, HealthWell, markDay, 5) && HealthSettle(seenBook, HealthWell, markDay, 5, 12000, 10, 365) == 0 && seenBook.Noticed == 1 && !seenBook.LastCovered,
            "health leave: a day seen through is not excused, not paid, and counted");
        var turn = new LeaveBook();
        HealthSettle(turn, HealthIll, 58, 5, 12000, 10, 60);
        HealthSettle(turn, HealthIll, 59, 5, 12000, 10, 60);
        Check(turn.PaidUsed == 2 && HealthSettle(turn, new LeaveFacts { Ill = true, Note = true }, 60, 5, 12000, 10, 60) == 0
              && HealthSettle(turn, new LeaveFacts { Ill = true, Note = true }, 61, 5, 12000, 10, 60) == 12000 && turn.PaidUsed == 1 && turn.Year == 1,
            "health leave: the paid days start again when the year turns, and not a day before");
        var work = new LeaveBook();
        long workPaid = 0;
        for (int day = 100; day < 106; day++)
        {
            workPaid += HealthSettle(work, new LeaveFacts { Ill = true, AtWorkInjury = true, InCare = true, WorkDays = day - 100 }, day, 5, 12000, 10, 60);
        }
        Check(workPaid == 6 * 12000 && work.PaidUsed == 0 && HealthCatalogue.CourseMost[(int)Condition.BrokenArm] <= HealthCatalogue.CourseLongest,
            "health work: the days off an accident at work are paid outside the year's paid days, for as long as the injury lasts and no longer");
    }

    // ---- stays in hospital

    private static void HealthStayChecks()
    {
        Check(HealthRules.StayUntil(10 * 1440 + 600, 1) == 11 * 1440 + 480 && HealthRules.StayUntil(10 * 1440 + 120, 1) == 10 * 1440 + 480 && HealthRules.StayUntil(10 * 1440 + 600, 2) == 12 * 1440 + 480
              && HealthRules.StayUntil(10 * 1440 + 480, 1) == 11 * 1440 + 480, "health hospital: a stay ends at eight in the morning, so many mornings on");
        var s = new HealthState();
        Check(!HealthDay.Admit(s, 10 * 1440, false), "health exploit: nobody is admitted without a serious case: the hospital is no place to hide");
        s.Case.Kind = Condition.Flu;
        s.Case.Began = 8;
        Check(!HealthDay.Admit(s, 10 * 1440, false), "health hospital: an illness that has not turned serious is not admitted");
        s.Case.Serious = true;
        int now = 10 * 1440 + 14 * 60;
        Check(HealthDay.Admit(s, now, false) && s.Stay.Active && s.Stay.Days == 2 && s.Stay.Until == 12 * 1440 + 480 && s.Stay.Kind == Condition.Flu && !HealthDay.Admit(s, now, false),
            "health hospital: a serious flu is two days in, to the second morning, and nobody is admitted twice");
        Check(HealthDay.StayBill(s.Stay, now, 1000) == 1700 && HealthDay.StayBill(s.Stay, now + 1440, 1000) == 2600 && HealthDay.StayBill(s.Stay, s.Stay.Until, 1000) == 2600
              && HealthDay.StayBill(new HealthStay(), now, 1000) == 0, "health hospital: a stay cut short is billed for the days begun, a whole one for all its days");
        // A save and a load in the middle of the stay.
        HealthState mid = HealthStore.Read(HealthStore.Write(s), out int _);
        Check(mid.Stay.Active && mid.Stay.From == now && mid.Stay.Until == s.Stay.Until && mid.Stay.Days == 2 && mid.Stay.Kind == Condition.Flu && mid.Case.Serious && mid.Case.Kind == Condition.Flu,
            "health save: a stay under way is in the save from the admission, so a load in the middle goes on to the same morning");
        HealthDay.Discharge(mid, 12);
        Check(!mid.Stay.Active && !mid.Case.Open && HealthLeave.Noted(mid.Leave, 10) && HealthLeave.Noted(mid.Leave, 12 + HealthCatalogue.NoteAfterStay) && !HealthLeave.Noted(mid.Leave, 13 + HealthCatalogue.NoteAfterStay),
            "health hospital: at the discharge the illness is over and a note covers the stay's days and two after");
        HealthDay.Discharge(mid, 12);
        Check(!mid.Stay.Active && mid.Leave.NoteUntil == 14, "health hospital: a discharge is once");
        var game = new HealthState();
        game.Case.Kind = Condition.Concussion;
        game.Case.Serious = true;
        Check(HealthDay.Admit(game, 20 * 1440 + 480, true) && game.Stay.GameNight && game.Stay.Until == 21 * 1440 + 480 && HealthDay.StayBill(game.Stay, game.Stay.Until, 1000) == 900,
            "health hospital: after the game's own night a serious case stays for the rest of its days, and that night is not billed twice");
        var one = new HealthState();
        one.Case.Kind = Condition.BrokenArm;
        one.Case.Serious = true;
        Check(HealthDay.Admit(one, 20 * 1440 + 480, true) && one.Stay.Until == 20 * 1440 + 480 && HealthDay.StayBill(one.Stay, one.Stay.Until, 1000) == 0,
            "health hospital: a one day case the game already kept overnight is discharged at once with nothing more to pay");
        HealthState longStay = HealthStore.Read("v=1^s:1|1000|9999999|50|2|0", out int _);
        Check(longStay.Stay.Days == HealthCatalogue.StayMost && longStay.Stay.Until <= 1000 + (HealthCatalogue.StayMost + 1) * 1440, "health save: a line that says a stay is endless is held to the longest stay");
    }

    // ---- the city's people

    private static PopulationState HealthCity(int households, int seed)
    {
        PopulationState s = GivingCity(households, seed);
        PersonTable pt = s.People;
        for (int p = 0; p < pt.Count; p++)
        {
            if (pt.Id[p] == 0)
            {
                continue;
            }
            bool works = pt.Status[p] == (byte)Status.Worker;
            pt.Job[p] = works ? 0 : -1;
            pt.Hours[p] = (byte)(works ? 20 + HealthRules.Roll(seed, p, 1, 1, 30) : 0);
            pt.Pay[p] = works ? 1200 + HealthRules.Roll(seed, p, 2, 2, 2400) : 0;
            pt.Birth[p] = 400 - 60 * (5 + HealthRules.Roll(seed, p, 3, 3, 75));   // five to eighty years old on day 400 of a 60-day year
        }
        return s;
    }

    private static long HealthMoneyOf(PopulationState s, bool players)
    {
        long sum = 0;
        for (int h = 0; h < s.Households.Count; h++)
        {
            bool own = (s.Households.Flags[h] & HouseholdFlags.Players) != 0;
            sum += s.Households.Alive(h) && own == players ? (long)s.Households.Savings[h] - s.Households.Debt[h] : 0L;
        }
        return sum;
    }

    private static void HealthPeopleChecks()
    {
        // The draws.
        int cold = 0, flu = 0, fluWave = 0, childCold = 0, arm = 0;
        bool sameDraw = true;
        for (int id = 1; id <= 200000; id++)
        {
            Condition c = HealthPeople.Primary(17, id, 40, 35, true, false, 0, CalendarRules.Winter);
            sameDraw &= id > 500 || c == HealthPeople.Primary(17, id, 40, 35, true, false, 0, CalendarRules.Winter);
            cold += c == Condition.Cold ? 1 : 0;
            flu += c == Condition.Flu ? 1 : 0;
            arm += c == Condition.BrokenArm ? 1 : 0;
            fluWave += HealthPeople.Primary(17, id, 40, 35, true, false, 1000, CalendarRules.Winter) == Condition.Flu ? 1 : 0;
            childCold += HealthPeople.Primary(17, id, 40, 8, false, false, 0, CalendarRules.Winter) == Condition.Cold ? 1 : 0;
        }
        Check(sameDraw && cold > 520 && cold < 680 && flu > 50 && flu < 115 && fluWave > flu * 8 && childCold > cold * 13 / 10 && arm > 0 && arm < 30,
            "health people: in winter 0.3% of adults catch a cold a day, children half as many again, the flu is rare out of its wave and sixteen times as common at its top (" + cold + ", " + flu + ", " + fluWave + " of 200,000)");
        int summer = 0, exhausted = 0, child = 0;
        for (int id = 1; id <= 200000; id++)
        {
            summer += HealthPeople.Primary(17, id, 40, 35, true, false, 0, CalendarRules.Summer) == Condition.Cold ? 1 : 0;
            exhausted += HealthPeople.Primary(17, id, 40, 35, true, true, 0, CalendarRules.Summer) == Condition.Exhaustion ? 1 : 0;
            Condition kid = HealthPeople.Primary(17, id, 40, 8, false, true, 0, CalendarRules.Summer);
            child += kid == Condition.BackPain || kid == Condition.Exhaustion ? 1 : 0;
        }
        Check(summer < cold / 2 && exhausted > 20 && child == 0, "health people: colds follow the season, exhaustion comes only of long hours, and children have neither a bad back nor exhaustion");
        bool seriousOk = HealthPeople.SeriousFor(3, 1, 1, Condition.BrokenArm, 30) && !HealthPeople.SeriousFor(3, 1, 1, Condition.Cold, 90) && !HealthPeople.SeriousFor(3, 1, 1, Condition.Sprain, 90);
        int young = 0, old = 0;
        for (int id = 1; id <= 20000; id++)
        {
            young += HealthPeople.SeriousFor(3, id, 9, Condition.Flu, 30) ? 1 : 0;
            old += HealthPeople.SeriousFor(3, id, 9, Condition.Flu, 70) ? 1 : 0;
        }
        Check(seriousOk && young > 600 && young < 1000 && old > young * 2, "health people: 4% of flu cases need the hospital, three times as many of the old, a broken arm always and a cold never");
        Check(HealthPeople.CareBill(Condition.Cold, false, 1000) == 0 && HealthPeople.CareBill(Condition.Flu, false, 1000) == 125 && HealthPeople.CareBill(Condition.Flu, true, 1000) == 125 + 2600
              && HealthPeople.CareBill(Condition.Flu, false, 2000) == 250 && HealthPeople.CareBill(Condition.None, true, 1000) == 0,
            "health people: a cold costs nothing, the rest the doctor and a prescription at Home's own fees, and the hospital on top when it is serious");
        // A day on a city: every dollar counted, the player's own household never touched, the same city twice gives the same day.
        PopulationState a = HealthCity(6000, 51), b = HealthCity(6000, 51);
        var oa = new PopHealth();
        var ob = new PopHealth();
        long before = HealthMoneyOf(a, false), ownBefore = HealthMoneyOf(a, true);
        int day = HealthRules.WaveTop(51, 7, 60);
        a.Day = b.Day = day;
        HealthPeople.Begin(oa, day, 3);
        HealthPeople.Begin(ob, day, 3);
        bool whole = HealthPeople.Slice(a, oa, 51, 1000, int.MaxValue / 2);
        // The same day in slices with a save and a load between each two.
        int slices = 0;
        while (ob.Cursor >= 0 && slices < 100)
        {
            HealthPeople.Slice(b, ob, 51, 1000, 1700);
            var carrier = new HealthState();
            CopyPop(ob, carrier.Pop);
            HealthState loaded = HealthStore.Read(HealthStore.Write(carrier), out int _);
            ob = loaded.Pop;
            slices++;
        }
        long after = HealthMoneyOf(a, false);
        Check(whole && oa.Cursor < 0 && oa.Cases > 20 && before - after == oa.LostWages + oa.Bills && oa.Debt <= oa.LostWages + oa.Bills,
            "health people: every dollar a case costs a household is counted: " + oa.Cases + " cases, " + (before - after) + " out of savings and onto debts, " + oa.LostWages + " of pay and " + oa.Bills + " of care");
        Check(HealthMoneyOf(a, true) == ownBefore, "health people: the player's own household's money is never touched");
        Check(slices > 3 && ob.Cursor < 0 && ob.Cases == oa.Cases && ob.Bills == oa.Bills && ob.LostWages == oa.LostWages && ob.Debt == oa.Debt && ob.IllNow == oa.IllNow && HealthMoneyOf(b, false) == after,
            "health save: the day's pass cut into " + slices + " slices with a save and a load between each gives the very same day (nobody drawn twice, nobody left out)");
        // The same day run again changes the people again only because it is run again: the draws themselves are the day's own.
        PopulationState c1 = HealthCity(3000, 9), c2 = HealthCity(3000, 9);
        var o1 = new PopHealth();
        var o2 = new PopHealth();
        HealthPeople.Begin(o1, 100, 1);
        HealthPeople.Begin(o2, 100, 1);
        HealthPeople.Slice(c1, o1, 9, 1000, int.MaxValue / 2);
        HealthPeople.Slice(c2, o2, 9, 1000, int.MaxValue / 2);
        Check(o1.Cases == o2.Cases && o1.Bills == o2.Bills && HealthMoneyOf(c1, false) == HealthMoneyOf(c2, false), "health reload: the same city and day give the same cases and the same money on every machine");
        // A case's own bounds.
        PopulationState one = HealthCity(40, 5);
        var oo = new PopHealth();
        HealthPeople.Begin(oo, 200, 1);
        int person = -1;
        for (int p = 0; p < one.People.Count && person < 0; p++)
        {
            int h = one.People.Household[p];
            person = one.People.Status[p] == (byte)Status.Worker && (one.Households.Flags[h] & HouseholdFlags.Players) == 0 ? p : -1;
        }
        int home = one.People.Household[person];
        one.Households.Savings[home] = 0;
        one.Households.Debt[home] = 0;
        one.People.Pay[person] = 900000;   // a pay so high that the lost days alone are far over the bound
        HealthPeople.Apply(one, oo, 5, person, 200, Condition.BrokenArm, 1000);
        one.People.Pay[person] = 1500;
        Check(one.Households.Debt[home] == HealthCatalogue.NpcDebtMost && one.Households.Savings[home] == 0 && oo.Serious == 1 && oo.Debt == one.Households.Debt[home]
              && oo.LostWages + oo.Bills == HealthCatalogue.NpcDebtMost,
            "health people: what a household cannot pay goes on its debt, by no more than " + HealthCatalogue.NpcDebtMost + " a case, and its savings never under nothing");
        one.Households.Savings[home] = 1000000;
        int debtWas = one.Households.Debt[home];
        HealthPeople.Apply(one, oo, 5, person, 200, Condition.Flu, 1000);
        Check(one.Households.Debt[home] == debtWas && one.Households.Savings[home] < 1000000 && one.Households.Savings[home] > 1000000 - 6000, "health people: a household with savings pays from them and takes on no debt");
        Check(HealthPeople.Insured(one, 5, home) == HealthPeople.Insured(one, 5, home), "health people: a household has its plan or has not, the same every day");
        // The ring of who is ill, a month's sums, and the wave's bound over a whole year.
        PopulationState city = HealthCity(8000, 23);
        var o = new PopHealth();
        int worst = 0, waveCases = 0, quietCases = 0, top = HealthRules.WaveTop(23, 7, 60), lastMonthCases = 0;
        bool monthTurned = false;
        for (int d = 7 * 60 + 1 - 10; d <= 8 * 60; d++)
        {
            int month = HealthRules.MonthOf(d, 5);
            int was = o.Cases, monthWas = o.Month;
            HealthPeople.Begin(o, d, month);
            if (monthWas >= 0 && month != monthWas)
            {
                monthTurned |= o.Cases == 0 && o.LastCases == was;
                lastMonthCases = was;
            }
            int casesBefore = o.Cases;
            HealthPeople.Slice(city, o, 23, 1000, int.MaxValue / 2);
            int today = o.Cases - casesBefore;
            waveCases += d == top ? today : 0;
            quietCases += d == top + 30 ? today : 0;
            worst = Math.Max(worst, o.IllNow);
        }
        int live = city.People.Live;
        Check(worst * 1000L <= (long)live * HealthCatalogue.WaveShareMost && worst > live / 200, "health wave: over a whole year never more than " + HealthCatalogue.WaveShareMost / 10 + "% of the city is ill at once (" + worst + " of " + live + " at the worst)");
        Check(waveCases > quietCases * 3 / 2 && quietCases > 0, "health wave: at the wave's top more than half as many again fall ill in a day as a month later (" + waveCases + " against " + quietCases + ")");
        Check(monthTurned && lastMonthCases > 0, "health people: a new month closes the month's sums and starts them again");
        for (int d = 8 * 60 + 1; d <= 8 * 60 + HealthCatalogue.CourseLongest + 2; d++)
        {
            HealthPeople.Begin(o, d, HealthRules.MonthOf(d, 5));
            o.Cursor = -1;   // nobody new: the pass does not run (a session, the living population off)
        }
        Check(o.IllNow == 0, "health people: everybody whose case has ended is well again, so with nobody new the count of the ill comes down to nothing");
        var skip = new PopHealth { Day = 100, IllNow = 5 };
        skip.Ends[103 % skip.Ends.Length] = 5;
        HealthPeople.Begin(skip, 300, 2);
        Check(skip.IllNow == 0 && skip.Ends.All(e => e == 0), "health people: days skipped (a long time away) still end the cases that ended in them");
        PopulationState home2 = HealthCity(400, 77);
        int illAtHome = 0;
        for (int d = 300; d < 420; d++)
        {
            int n = HealthPeople.IllAtHome(home2, 77, d);
            illAtHome += n;
            if (n != HealthPeople.IllAtHome(home2, 77, d))
            {
                illAtHome = int.MinValue;
            }
        }
        Check(illAtHome > 0, "health people: the people of the player's own home fall ill by the same draw, the same for a day however often it is asked (" + illAtHome + " person-days in 120)");
        // The cost: bounded, and nothing allocated.
        PopulationState big = HealthCity(21000, 31);
        var ot = new PopHealth();
        double best = double.MaxValue, bestSlice = double.MaxValue;
        long leastBytes = long.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            HealthPeople.Begin(ot, 500 + round, 1);
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            long began = Stopwatch.GetTimestamp();
            HealthPeople.Slice(big, ot, 31, 1000, HealthCatalogue.PassSlice);
            double sliceMs = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            HealthPeople.Slice(big, ot, 31, 1000, int.MaxValue / 2);
            double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            leastBytes = Math.Min(leastBytes, GC.GetAllocatedBytesForCurrentThread() - bytes);
            best = Math.Min(best, ms);
            bestSlice = Math.Min(bestSlice, sliceMs);
        }
        Check(big.People.Live >= 42000 && best < 120.0, "health costs: a day's pass over " + big.People.Live + " people takes " + best.ToString("F1", CultureInfo.InvariantCulture) + " ms in all, under 120 ms spread over its seconds");
        Check(bestSlice < 40.0, "health costs: a slice of " + HealthCatalogue.PassSlice + " people takes " + bestSlice.ToString("F2", CultureInfo.InvariantCulture) + " ms, under 40 ms once a second");
        Check(leastBytes == 0, "health costs: the people's pass allocates nothing (" + leastBytes + " bytes, the least of three rounds)");
    }

    private static void CopyPop(PopHealth from, PopHealth to)
    {
        to.Day = from.Day;
        to.Cursor = from.Cursor;
        to.IllNow = from.IllNow;
        to.Month = from.Month;
        to.Cases = from.Cases;
        to.Serious = from.Serious;
        to.LostDays = from.LostDays;
        to.LostWages = from.LostWages;
        to.Bills = from.Bills;
        to.Debt = from.Debt;
        to.LastCases = from.LastCases;
        to.LastSerious = from.LastSerious;
        to.LastLostDays = from.LastLostDays;
        to.LastLostWages = from.LastLostWages;
        to.LastBills = from.LastBills;
        to.LastDebt = from.LastDebt;
        Array.Copy(from.Ends, to.Ends, from.Ends.Length);
    }

    // ---- the staff and the rivals

    private static void HealthStaffRivalChecks()
    {
        int quiet = 0, wave = 0, contact = 0, gold = 0, longest = 0, shorter = 0, plain = 0;
        bool same = true;
        for (int who = 1; who <= 100000; who++)
        {
            quiet += HealthPeople.StaffCourse(7, who, 30, 0, 0, 0) > 0 ? 1 : 0;
            int w = HealthPeople.StaffCourse(7, who, 30, 1000, 0, 0);
            same &= who > 300 || w == HealthPeople.StaffCourse(7, who, 30, 1000, 0, 0);
            wave += w > 0 ? 1 : 0;
            longest = Math.Max(longest, w);
            plain += w;
            contact += HealthPeople.StaffCourse(7, who, 30, 0, 9, 0) > 0 ? 1 : 0;
            int g = HealthPeople.StaffCourse(7, who, 30, 1000, 0, 3);
            gold += g > 0 ? 1 : 0;
            shorter += w > 0 && g > 0 && g <= w ? 1 : 0;
        }
        Check(quiet == 0, "health staff: with no wave and no colleague off nobody is made ill: the game's own sick days are all there is");
        Check(same && wave > 850 && wave < 1150 && longest <= HealthCatalogue.StaffCourseMost && contact > 4000 && contact < 5000,
            "health staff: 1% a day at the top of a wave, 1.5% for each colleague off up to three, one to three days (" + wave + " and " + contact + " of 100,000)");
        Check(gold < wave * 65 / 100 && gold > wave * 45 / 100 && shorter == gold, "health staff: the game's gold health plan cuts the chance by 45% and takes a day off the course (" + gold + " against " + wave + ")");
        Check(HealthPeople.StaffFrom(30, 0) == 32 && HealthPeople.StaffFrom(30, 1) == 31 && HealthPeople.StaffFrom(30, 2) == 31, "health staff: with no sick pay they come in ill the first day, otherwise they are off from tomorrow");
        // The rivals.
        var r = new RivalHealth();
        int began = 0, serious = 0;
        bool once = true, inside = true, noOverlap = true;
        int until = 0;
        for (int day = 1; day <= 30000; day++)
        {
            bool busy = r.Kind != Condition.None && day <= r.Until;
            bool b = HealthRules.RivalDay(r, 13, 1, day, 0);
            once &= !HealthRules.RivalDay(r, 13, 1, day, 0);
            noOverlap &= !(b && busy);
            if (b)
            {
                began++;
                serious += r.Serious ? 1 : 0;
                until = r.Until;
                inside &= r.Until >= day + HealthCatalogue.RivalDaysLeast - 1 && r.Until <= day + HealthCatalogue.RivalDaysMost - 1 + HealthCatalogue.StayMost && r.Kind != Condition.None;
            }
        }
        Check(once && noOverlap && inside && began > 20 && began < 60 && serious < began / 3, "health rivals: a tycoon falls ill about once in 800 days out of the wave, for two to five days, once a day at most and never while ill already (" + began + " in 30,000 days)");
        var r1 = new RivalHealth();
        var r2 = new RivalHealth();
        bool alike = true;
        for (int day = 1; day <= 4000; day++)
        {
            alike &= HealthRules.RivalDay(r1, 99, 2, day, 500) == HealthRules.RivalDay(r2, 99, 2, day, 500) && r1.Kind == r2.Kind && r1.Until == r2.Until && r1.Serious == r2.Serious;
        }
        Check(alike, "health rivals: the same save gives every tycoon the same illnesses on every machine");
        Check(HealthRules.RivalPaid(2600, 1000000, 900000) == 2600 && HealthRules.RivalPaid(2600, 901000, 900000) == 1000 && HealthRules.RivalPaid(2600, 900000, 900000) == 0 && HealthRules.RivalPaid(2600, 5, 900000) == 0,
            "health rivals: a hospital's bill leaves a tycoon's worth from the part over their floor only, never under nothing");
    }

    // ---- the save

    private static HealthState HealthFull()
    {
        var s = new HealthState { Seed = 123456, LastDay = 88, LastHour = 14, GameStays = 3, Year = 1, BillsYear = 5200, ClaimsYear = 3100, LastBill = 2600, LastClaim = 1997, LastBillDay = 80, Injuries = 4, StaffDay = 88 };
        s.Case.Kind = Condition.BrokenArm;
        s.Case.Began = 85;
        s.Case.Until = 93;
        s.Case.Treated = true;
        s.Case.Work = true;
        s.Case.Neglect = 2;
        s.Case.LastDay = 88;
        HealthCover.Take(s.Policy, HPlan.Standard, 40, 5);
        s.Policy.Missed = 1;
        s.Policy.Year = 1;
        s.Policy.PaidYear = 1997;
        s.Policy.ClaimDays.AddRange(new[] { 61, 80 });
        s.Stay.Active = true;
        s.Stay.From = 88 * 1440 + 100;
        s.Stay.Until = 90 * 1440 + 480;
        s.Stay.Days = 2;
        s.Stay.Kind = Condition.BrokenArm;
        s.Stay.GameNight = true;
        HealthCover.Owe(s.Debt, 1500, 5, 80);
        s.Debt.Late = 2;
        s.Debt.Claim = 0;
        s.Debt.Paid = 300;
        s.Leave.Year = 1;
        s.Leave.PaidUsed = 1;
        s.Leave.SpellLast = 87;
        s.Leave.SpellDays = 2;
        s.Leave.LastCovered = true;
        s.Leave.ClaimDay = 70;
        s.Leave.Claims = 2;
        s.Leave.Noticed = 1;
        s.Leave.NoteFrom = 60;
        s.Leave.NoteUntil = 64;
        s.Leave.PaidDay = 87;
        s.Leave.PaidMoney = 120;
        s.Leave.PrevYear = 0;
        s.Leave.PrevPaidUsed = 2;
        s.Leave.CaseFrom = 84;
        s.Leave.CaseTo = 88;
        s.Leave.CaseWork = true;
        s.Leave.CareFrom = 86;
        s.Leave.WorkTold = 84;
        s.DrawDay = 88;
        s.Draws[1] = 3;
        s.Draws[8] = 7;
        HealthDay.Book(s.Appointment, 88, 9, "Park Avenue; 5th | Street ^ x", 12);
        s.Staff.Add(new StaffCase { Id = "emp|1;a^b", From = 89, Until = 90 });
        s.Staff.Add(new StaffCase { Id = "emp-2", From = 90, Until = 92 });
        s.RivalCases[1].Kind = Condition.Flu;
        s.RivalCases[1].Day = 86;
        s.RivalCases[1].Until = 91;
        s.RivalCases[1].Serious = true;
        s.RivalCases[1].Bill = 2600;
        s.RivalCases[1].LastDay = 88;
        s.Pop.Day = 88;
        s.Pop.Cursor = 24000;
        s.Pop.IllNow = 900;
        s.Pop.Month = 17;
        s.Pop.Cases = 300;
        s.Pop.Serious = 9;
        s.Pop.LostDays = 500;
        s.Pop.LostWages = 40000;
        s.Pop.Bills = 60000;
        s.Pop.Debt = 7000;
        s.Pop.Pending = 89;
        s.Pop.LastCases = 280;
        s.Pop.LastBills = 55000;
        for (int i = 0; i < s.Pop.Ends.Length; i++)
        {
            s.Pop.Ends[i] = i * 3;
        }
        s.Notes.Add(new HealthNote { Day = 86, Rival = 1, Kind = Condition.Flu });
        return s;
    }

    private static void HealthStoreChecks()
    {
        HealthState s = HealthFull();
        string line = HealthStore.Write(s);
        HealthState r = HealthStore.Read(line, out int version);
        Check(version == HealthStore.Version && r != null && HealthStore.Write(r) == line, "health save: a line read and written again is the same line");
        Check(r.Seed == 123456 && r.LastDay == 88 && r.LastHour == 14 && r.GameStays == 3 && r.BillsYear == 5200 && r.ClaimsYear == 3100 && r.Injuries == 4 && r.Case.Kind == Condition.BrokenArm
              && r.Case.Until == 93 && r.Case.Treated && r.Case.Work && !r.Case.Serious && r.Policy.Plan == HPlan.Standard && r.Policy.Since == 40 && r.Policy.NextDue == 45 && r.Policy.Missed == 1
              && r.Policy.PaidYear == 1997 && r.Policy.ClaimDays.Count == 2 && r.Policy.ClaimDays[1] == 80, "health save: the header, the case and the policy with its claims come back");
        Check(r.Stay.Active && r.Stay.Until == 90 * 1440 + 480 && r.Stay.GameNight && r.Debt.Owed == 1500 && r.Debt.PerDay == 150 && r.Debt.Late == 2 && r.Debt.Paid == 300 && r.Leave.PaidUsed == 1
              && r.Leave.SpellDays == 2 && r.Leave.LastCovered && r.Leave.Claims == 2 && r.Leave.Noticed == 1 && r.Leave.NoteFrom == 60 && r.Leave.NoteUntil == 64 && r.Leave.PaidMoney == 120,
            "health save: the stay, the debt and the sick leave come back");
        Check(r.Leave.PrevYear == 0 && r.Leave.PrevPaidUsed == 2 && r.Leave.CaseFrom == 84 && r.Leave.CaseTo == 88 && r.Leave.CaseWork && r.Leave.CareFrom == 86 && r.Leave.WorkTold == 84 && r.DrawDay == 88 && r.Draws[1] == 3
              && r.Draws[8] == 7 && r.Draws[0] == 0 && r.Pop.Pending == 89, "health save: the year before's paid days, the span of the last case, the accident said and the day's draws come back");
        Check(r.Appointment.Booked && r.Appointment.Street == "Park Avenue; 5th | Street ^ x" && r.Appointment.Number == 12 && r.Staff.Count == 2 && r.Staff[0].Id == "emp|1;a^b" && r.Staff[1].Until == 92
              && r.RivalCases[1].Kind == Condition.Flu && r.RivalCases[1].Serious && r.RivalCases[1].Bill == 2600 && r.RivalCases[0].Kind == Condition.None && r.Pop.Cursor == 24000 && r.Pop.IllNow == 900
              && r.Pop.Ends[5] == 15 && r.Pop.LastBills == 55000 && r.Notes.Count == 1 && r.Notes[0].Rival == 1,
            "health save: the appointment (whatever its street's name holds), the staff, the rivals, the people's pass with its cursor and the paper's notes come back");
        // Old saves, lines that are not ours, and a later version's line.
        var empty = new Dictionary<string, string>();
        Check(HealthStore.Load(empty, out int none) == null && none == 0 && HealthStore.Load(null, out int _) == null && HealthStore.Read(string.Empty, out int _) == null
              && HealthStore.Read("nonsense", out int _) == null && HealthStore.Read("v=x^h:1", out int _) == null, "health save: a save from before the feature loads with nothing in it, and a line that is not health's is not read");
        HealthState blank = HealthStore.Read("v=1", out int v1);
        Check(blank != null && v1 == 1 && !blank.Case.Open && blank.Policy.Plan == HPlan.None && !blank.Stay.Active && blank.Debt.Owed == 0 && blank.Pop.Cursor == -1 && blank.GameStays == -1,
            "health save: a line with nothing but its version is a fresh state");
        HealthState newer = HealthStore.Read(line.Replace("v=1^", "v=7^") + "|99|98^z:new|part^h9", out int v7);
        Check(newer != null && v7 == 7 && newer.Seed == 123456 && newer.Debt.Owed == 1500 && newer.Notes.Count == 1, "health save: a line from a newer version is read as far as this one knows it");
        HealthState wild = HealthStore.Read("v=1^h:5|-9|77|-8|-4|-100|900|-3|50|-1|-2|-6^c:99|-5|-5|1|1|1|-7|-2^p:9|-3|-3|50|-9|-80^d:-500|-3|-4|-5|-6|-7^l:-5|-5|-5|-5|1|-5|3|9|-5|-5|-5|77^a:-4|99|1|-8^o:-1|-9|-5|-7|-1|-1|-1|-1|-1|-1",
            out int _);
        Check(wild != null && wild.LastDay == 0 && wild.LastHour == 23 && wild.BillsYear == 0 && wild.ClaimsYear == 0 && wild.LastClaim == 0 && !wild.Case.Open && !wild.Case.Serious && wild.Policy.Plan == HPlan.None
              && wild.Policy.Missed == HealthCatalogue.LapseAfter && wild.Debt.Owed == 0 && wild.Debt.PerDay == 0 && wild.Leave.Noticed == 3 && wild.Leave.NoteFrom == 0 && !wild.Appointment.Booked
              && wild.Pop.Cursor == -1 && wild.Pop.IllNow == 0, "health save: every figure of a line is put inside its bounds, so a spoilt line can neither owe a negative debt nor claim more than a bill");
        // A culture with decimal commas and its own digits' grouping writes and reads the same line.
        CultureInfo was = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            string german = HealthStore.Write(s);
            HealthState back = HealthStore.Read(german, out int _);
            Check(german == line && back.Debt.Owed == 1500 && back.Pop.Bills == 60000, "health save: a language with a decimal comma writes and reads the same line");
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }

    // ---- the option off and on, and Going Public

    private static void HealthOptionChecks()
    {
        HealthState s = HealthFull();
        long bill = HealthDay.StayBill(s.Stay, s.Stay.From + 30, 1000);
        HealthDay.Clear(s);
        Check(bill == 0 && !s.Stay.Active && !s.Case.Open && !s.Appointment.Booked && s.Staff.Count == 0 && s.Pop.Cursor == -1,
            "health option: switched off, the case under way ends, the stay is closed, the appointment and the staff's cases are dropped and the people's pass stops");
        Check(s.Debt.Owed == 1500 && s.Policy.Plan == HPlan.Standard && s.Leave.PaidUsed == 1 && s.BillsYear == 5200, "health option: what is owed stays owed, and the policy's record and the leave stay in the line");
        Check(HealthDay.Debt(s, 89, amount => true, null, out long taken) == DebtResult.Paid && taken == 150 && s.Debt.Owed == 1350, "health option: the debt's day is the same step with the option off, so the instalments go on");
        var cut = new HealthState();
        cut.Case.Kind = Condition.Flu;
        cut.Case.Serious = true;
        HealthDay.Admit(cut, 10 * 1440 + 600, false);
        Check(HealthDay.StayBill(cut.Stay, 10 * 1440 + 700, 1000) == 1700, "health option: a stay cut short by the switch is still billed for the day begun: no free night");
        // Back on: no claim for a night the game gave while it was away, and no cover kept through a month unpaid.
        var back = new HealthState { GameStays = 2 };
        HealthDay.TakePlan(back, HPlan.Full, 10, 30, 60, 5, 1000, amount => true, out int _);
        Check(!HealthDay.BackOn(back, 16, 5, 5) && back.GameStays == 5 && back.Policy.Plan == HPlan.Full, "health option: back on within the month the plan is there (its premium falls due at once), and the game's nights while away are not claimed afterwards");
        Check(HealthDay.Premium(back, 16, 30, 60, 5, 1000, amount => true, out int due) == PremiumResult.Paid && due == 290, "health option: the premium missed while away is taken on the first day back");
        var away = new HealthState();
        HealthDay.TakePlan(away, HPlan.Full, 10, 30, 60, 5, 1000, amount => true, out int _);
        Check(HealthDay.BackOn(away, 20, 0, 5) && away.Policy.Plan == HPlan.None, "health exploit: a plan whose premiums were not taken for a month has ended, so cover is not kept by switching the option off between bills");
        // Going Public and the living population off, and the option off with a debt: the rules are driven in HealthFixChecks (HealthSessionChecks, HealthPeopleDayChecks,
        // HealthStayRunChecks). What is left here is the wiring they cannot run: the look with the option off hands over to the one step, and the people and the clock are asked
        // through the rules.
        string dir = ModSource(@"Health\HealthDirector.cs"), game = ModSource(@"Health\HealthGame.cs"), care = ModSource(@"Health\HealthDirector.Care.cs"), wiring = ModSource(@"Wiring\Wiring.HealthLinks.cs");
        Check(dir.Contains("OptionOff(day);") && dir.Contains("HealthDay.WhileOff(") && game.Contains("HealthPeople.ModeOf(Settings.LivingPopulation, CityPopulation.Running, CityPopulation.Following, GoingPublic.InSession)")
              && wiring.Contains("HealthRivals.WorthIn(GoingPublic.InSession,") && care.Contains("HealthDay.Release(_state, HealthGame.Minutes, day, GoingPublic.ClockShared,"),
            "health wiring: the option off, the city's people, the rivals and a shared clock are decided by the rules that are driven above");
    }

    // ---- the pages

    private static void HealthPageChecks()
    {
        Check(UiScreens.All.Any(sc => sc.Id == "health" && sc.Section == UiScreenSwitches.Ventures), "health pages: the app has its own switch in Mod settings");
        foreach (string state in UiSamples.HealthStates)
        {
            var stub = new UiSamples.HealthStub();
            VentModel m = UiSamples.Health(ModEnglish(), state, null, stub);
            bool off = state == "off";
            Check(off ? m.Shape == VentShape.Off : m.Shape == VentShape.Page && m.Nav.Length == 4, "health pages: " + state + " builds as " + (off ? "the off page" : "a page of four tabs"));
            if (off)
            {
                continue;
            }
            int blocks = m.Left.Length + m.Right.Length;
            Check(blocks >= 4 && m.HeroFigures.Length == 3, "health pages: " + state + " has its figures and blocks (" + blocks + ")");
            int primary = 0;
            foreach (VentBlock b in m.Right.Concat(m.Left))
            {
                if (b.Kind != VentBlockKind.Actions)
                {
                    continue;
                }
                foreach (UiActionModel a in b.Actions)
                {
                    primary += a.Kind == UiButtonKind.Primary ? 1 : 0;
                    if (a.Disabled)
                    {
                        continue;
                    }
                    int before = stub.Count;
                    bool ran = a.Arg >= 0 && a.Arg < m.Runs.Length && m.Runs[a.Arg] != null && m.Runs[a.Arg]();
                    Check(ran && stub.Count == before + 1, "health pages: " + state + ": the button '" + a.Label + "' reaches its call (" + stub.Last + ")");
                }
            }
            Check(primary <= 1, "health pages: " + state + " has one primary action at most (" + primary + ")");
        }
        var press = new UiSamples.HealthStub();
        UiActionModel Button(VentModel m, int at) => m.Right.Concat(m.Left).Where(b => b.Kind == VentBlockKind.Actions).SelectMany(b => b.Actions).ElementAt(at);
        VentModel you = UiSamples.Health(ModEnglish(), "you", null, press);
        you.Runs[Button(you, 0).Arg]();
        Check(press.Last == "book" && Button(you, 0).Kind == UiButtonKind.Primary, "health pages: Book an appointment books the doctor");
        VentModel booked = UiSamples.Health(ModEnglish(), "you_booked", null, press);
        booked.Runs[Button(booked, 0).Arg]();
        Check(press.Last == "cancel_booking" && booked.Right.Any(b => b.Kind == VentBlockKind.Row && b.Value != null && b.Value.Contains("65")), "health pages: with an appointment the page shows its day and offers to cancel it");
        Check(Button(UiSamples.Health(ModEnglish(), "you_hospital", null, press), 0).Disabled, "health pages: nobody books the doctor from a hospital bed");
        Check(UiSamples.Health(ModEnglish(), "you_serious", null, press).Left.Any(b => b.Kind == VentBlockKind.Note && b.Tone == UiRole.Warn) && UiSamples.Health(ModEnglish(), "you_serious", null, press).Nav[0].Count == "1",
            "health pages: a serious case is said as a warning and marked on the tab");
        var view = new VentView { Tab = HealthPages.TabCover, Pick = HealthPages.PlanKey + 2 };
        VentModel cover = UiSamples.Health(ModEnglish(), "cover", view, press);
        cover.Runs[Button(cover, 0).Arg]();
        Check(press.Last == "take" && press.Plan == (int)HPlan.Full && cover.Left.Any(b => b.Kind == VentBlockKind.Items && b.Items.Length == 3), "health pages: Take this plan takes the plan picked among the three");
        VentModel held = UiSamples.Health(ModEnglish(), "cover_held", null, press);
        Check(Button(held, 0).Kind == UiButtonKind.Secondary && Button(held, 1).Disabled, "health pages: the plan held is not taken again, and can be cancelled");
        held.Runs[Button(held, 0).Arg]();
        Check(press.Last == "cancel_plan" && held.Left.Any(b => b.Kind == VentBlockKind.Note && b.Tone == UiRole.Ok), "health pages: Cancel your plan cancels it, and an employer's plan is said");
        Check(Button(UiSamples.Health(ModEnglish(), "cover_broke", null, press), 0).Disabled, "health pages: a plan whose first month is more than the spare cash cannot be taken");
        VentModel debt = UiSamples.Health(ModEnglish(), "bills_debt", null, press);
        debt.Runs[Button(debt, 1).Arg]();
        Check(press.Last == "pay_debt" && Button(debt, 0).Disabled && Button(debt, 0).Kind == UiButtonKind.Secondary, "health pages: Pay it off now pays the debt, and nobody calls in sick without a shift");
        VentModel shift = UiSamples.Health(ModEnglish(), "bills_shift", null, press);
        shift.Runs[Button(shift, 0).Arg]();
        Check(press.Last == "call_in" && !shift.Left.Any(b => b.Kind == VentBlockKind.Actions), "health pages: Call in sick today calls in, and with nothing owed there is no debt to pay off");
        VentModel session = UiSamples.Health(ModEnglish(), "city_session", null, press);
        Check(session.Left.Any(b => b.Kind == VentBlockKind.Note && b.Tone == UiRole.Warn) && !session.Left.Any(b => b.Kind == VentBlockKind.Row), "health pages: in a session the city's page says the people are the host's and shows no figures of them");
        string app = ModSource(@"Health\HealthApp.cs"), live = ModSource(@"Ui\Ventures\HealthLive.cs");
        Check(app.Contains("HealthLive.Instance.Book()") && app.Contains("HealthLive.Instance.CancelBooking()") && app.Contains("HealthLive.Instance.TakePlan(plan)") && app.Contains("HealthLive.Instance.CancelPlan()")
              && app.Contains("HealthLive.Instance.PayDebt()") && app.Contains("HealthLive.Instance.CallInSick()") && live.Contains("d.Book()") && live.Contains("d.CancelBooking()") && live.Contains("d.TakePlan(wanted, out amount)")
              && live.Contains("d.CancelPlan()") && live.Contains("d.PayOffDebt(out amount)") && live.Contains("d.CallInSick()"),
            "health pages: the app in the game's frame and the kit's page press the same calls of the director");
    }

    // ---- the sources

    private static void HealthSourceChecks()
    {
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"Health\HealthDirector.cs"));
        string[] files = Directory.GetFiles(folder, "*.cs");
        Check(files.Length >= 20, "health sources: the feature's files are found (" + files.Length + ")");
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            string name = System.IO.Path.GetFileName(file);
            Check(!text.Contains("GameManager.ChangeMoney") && !text.Contains("ChangeMoneySafe") && !text.Contains("Update()") && !text.Contains("FindObjectsByType"),
                "health sources: " + name + " books no money outside the one call and has no per-frame code or scene search");
            Check(!text.Contains("ProjectSettings") && !text.Contains("TODO") && !text.Contains("NotImplemented"), "health sources: " + name + " has no stub and no note of work left");
            Check(!text.Contains("appearanceSetter") && !text.Contains("UpdateVisuals") && !text.Contains("animator"), "health sources: " + name + " touches nobody's looks or animations");
            Check(name == "HealthDirector.cs" || !text.Contains("UnityEngine.Random") && !text.Contains("Random.value") && !text.Contains("Random.Range"), "health sources: " + name + " draws nothing unseeded");
        }
        string dir = ModSource(@"Health\HealthDirector.cs"), care = ModSource(@"Health\HealthDirector.Care.cs"), leave = ModSource(@"Health\HealthDirector.Leave.cs"), game = ModSource(@"Health\HealthGame.cs");
        Check(CountOfText(dir, "UnityEngine.Random") == 1 && dir.Contains("_state.Seed = UnityEngine.Random.Range(1, int.MaxValue);"), "health sources: the one unseeded number is the save's seed, drawn once and kept");
        Check(dir.Contains("StartCoroutine(Guard.Routine(Tick()))") && dir.Contains("catch (Exception e)") && care.Contains("StartCoroutine(Guard.Routine(StayRoutine()))"),
            "health sources: the timer and the stay are guarded coroutines, and a fault switches the feature off for the session");
        Check(game.Contains("BizBooks.Book(") && game.Contains("amount > Spare") && game.Contains("force: false"), "health sources: money leaves only through the mod's one booking call, from the spare cash, never by force");
        Check(dir.Contains("HealthDay.Premium(") && dir.Contains("HealthDay.Debt(") && dir.Contains("HealthDay.BackOn(") && dir.Contains("HealthDay.WhileOff(") && dir.Contains("HealthRivals.Day(") && dir.Contains("HealthPeople.Slice(")
              && dir.Contains("HealthPeople.Turn(") && dir.Contains("HealthPeople.Resume(") && dir.Contains("HealthPeople.StandDown(") && dir.Contains("HealthPeople.IllAtHome(") && care.Contains("HealthRules.CaseDay(")
              && care.Contains("HealthRules.PlayerCatch(") && care.Contains("HealthRules.Mirror(") && care.Contains("HealthRules.InjuryOf(") && care.Contains("HealthRules.Injure(") && care.Contains("HealthRules.WorkAccident(")
              && care.Contains("HealthDay.Bill(") && care.Contains("HealthDay.Admit(") && care.Contains("HealthDay.GameNight(") && care.Contains("HealthDay.Release(") && care.Contains("HealthDay.NextSkip(")
              && care.Contains("HealthDay.StayOver(") && care.Contains("HealthDay.AppointmentDay(") && care.Contains("HealthDay.Book(") && care.Contains("HealthDay.InWindow(") && care.Contains("HealthLeave.ObserveCase(")
              && leave.Contains("HealthLeave.Covers(") && leave.Contains("HealthLeave.Settle(") && leave.Contains("HealthLeave.CallIn(") && leave.Contains("HealthLeave.FactsFor(") && leave.Contains("HealthDay.TakePlan(")
              && leave.Contains("HealthDay.PayOff(") && game.Contains("HealthPeople.StaffCourse(") && game.Contains("HealthPeople.StaffFrom(") && ModSource(@"Health\HealthDirector.Facts.cs").Contains("HealthRules.Draw("),
            "health wiring: the director calls the very rules these checks drive, every one of them (the rules themselves are run in this file and in HealthFixChecks)");
        Check(game.Contains("e.nextSickDay = Math.Min(e.nextSickDay, day + 1);") && !game.Contains("isAbsent = "), "health sources: a member of staff is off through the game's own sick day, never marked absent by hand");
        Check(care.Contains("new Timestamp((float)target), disableCancel: true") && care.Contains("finally") && care.Contains("EndStay();") && dir.Contains("EndStay();"),
            "health sources: a stay moves the clock by the game's own time skip, and whatever it changed is put back on every way out");
        string home = ModSource(@"Home\HomeHealth.cs"), cook = ModSource(@"Home\CookActivity.cs"), wiring = ModSource(@"Wiring\Wiring.HealthLinks.cs");
        Check(!home.Contains("Random.value <") && CountOfText(home, "Random.value") == 1 && CountOfText(home, "Draw(") == 8 && cook.Contains("HomeHealth.Draw(8)") && wiring.Contains("return d.HomeDraw(slot);"),
            "health reload: every one of Home's illness chances is drawn through the one door, which with Health on is the save's seed, the day, the chance and which draw of it that day (HealthRules.Draw, driven above)");
        Check(home.Contains("HomeBridges.Held != null && HomeBridges.Held()") && home.Contains("HomeBridges.TooIll != null && HomeBridges.TooIll()"), "health sources: a serious illness is held on in Home, and rules out hard exercise");
        string life = ModSource(@"Life\LifeEvents.cs"), doctor = ModSource(@"Home\DoctorVisit.cs"), phone = ModSource(@"Phone\PhoneContacts.cs"), work = ModSource(@"Careers\CareersDirector.Work.cs");
        Check(life.Contains("Hurt?.Invoke(hours);") && doctor.Contains("DoctorVisit.Admission != null ? DoctorVisit.Admission() : null") && doctor.Contains("DoctorVisit.Paid?.Invoke(HomeBalance.ConsultationFee)")
              && doctor.Contains("DoctorVisit.Paid?.Invoke(HomeBalance.PrescriptionFee)") && doctor.Contains("DoctorVisit.Treated?.Invoke()") && phone.Contains("PhoneContacts.DoctorFirst(Line)"),
            "health sources: every injury, the doctor's desk and the doctor's phone line reach health through their one door each");
        int missed = work.IndexOf("CareersBridges.DayMissed?.Invoke(", StringComparison.Ordinal), excused = work.IndexOf("if (Excused(day) || j.OffUntil >= day)", StringComparison.Ordinal);
        int worked = work.IndexOf("if (worked)", StringComparison.Ordinal);
        Check(missed > worked && missed < excused && worked > 0 && work.Contains("CareersBridges.ShiftWorked?.Invoke(j.Def.SkillA);"),
            "health wiring: health hears of a working day only once it is settled as not worked, before it is excused (what it pays for the day is driven in HealthPartDayChecks)");
        Check(wiring.Contains("CareerLinks.Health = new Leave();") && wiring.Contains("CareerLinks.Health = _noteBefore;") && wiring.Contains("return d.Covers(day);"),
            "health sources: with Health on the leave rules answer whether a day off is excused, and the answer as it was is put back with the option off");
        Check(wiring.Contains("CreditKind.Missed") && wiring.Contains("CreditKind.ClaimFiled") && wiring.Contains("CreditKind.PaidOff"),
            "health wiring: the credit file is told of the debt's missed instalments, its claim and its paying off (the account, the court and the score are run in HealthCreditChecks)");
        Check(!wiring.Contains("Justice.") && !ModSource(@"Health\HealthLeave.cs").Contains("Court") && !care.Contains("Custody"),
            "health exploit: being ill excuses a working day and nothing else: no debt, trial or deadline of the mod's asks health anything");
        Check(CountOfText(ModSource(@"Wiring\Wiring.cs"), "HealthLinks") == 2, "health sources: the wiring connects and disconnects health");
        Check(ModSource(@"Meta\FeatureList.cs").Contains("Health.HealthGuide.Register()") && ModSource(@"Core\FeatureOwners.cs").Contains("\"RealLifeOverhaul.Health\", \"health\"") && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"health\")")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"health\"") && ModSource(@"Core\Settings.cs").Contains("public static bool Health = true;"),
            "health sources: the feature has its option (on by default), its Guide page, its owner and its place in the cities list");
        // (Health never calls Permadeath, and what a flu held on can do under Permadeath's rules, are in HealthDeathChecks.)
    }

    // ---- the texts

    private static void HealthTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"Health\HealthDirector.cs"));
        var sources = new List<string>(Directory.GetFiles(folder, "*.cs").Select(File.ReadAllText));
        sources.Add(ModSource(@"Ui\Ventures\HealthPages.cs"));
        sources.Add(ModSource(@"Ui\Ventures\HealthLive.cs"));
        sources.Add(ModSource(@"Wiring\Wiring.HealthLinks.cs"));
        foreach (string source in sources)
        {
            foreach (Match m in Regex.Matches(source, "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string k in new[] { "reallife_opt_sec_health", "reallife_opt_tip_health", "reallife_ui_screen_health", "reallife_opt_tip_ui_screen_health", "reallife_transaction_hc_premium_label",
            "reallife_transaction_hc_bill_label", "reallife_transaction_hc_debt_paid_label", "reallife_transaction_hc_salary_sick_label", "reallife_transaction_hc_missed_fee_label" })
        {
            keys.Add(k);
        }
        Check(keys.Count >= 205, "health texts: its keys are found: " + keys.Count);
        string[] facts = { "{hc_neglect}", "{hc_admission}", "{hc_stay_day}", "{hc_stay_most}", "{hc_missed}", "{hc_basic}", "{hc_standard}", "{hc_full}", "{hc_wait}", "{hc_debt_months}", "{hc_debt_court}",
            "{hc_self_cert}", "{hc_note_after}" };
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        foreach (string code in HealthLanguages)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "health texts: " + code + ".json is read");
            if (words == null)
            {
                continue;
            }
            foreach (string key in keys)
            {
                bool there = words.TryGetValue(key, out string value) && value.Length > 0;
                Check(there, "health texts: " + key + " is in " + code + ".json");
                if (!there)
                {
                    continue;
                }
                Check(!value.Contains('!') && !value.Contains((char)0x2014) && !value.Contains((char)0x2013) && !value.Contains(" - ") && !value.Contains("..."), "health texts: " + key + " has no exclamation mark, dash or three dots in " + code);
                if (english != null && english.TryGetValue(key, out string en))
                {
                    var wanted = new SortedSet<string>(Regex.Matches(en, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    var got = new SortedSet<string>(Regex.Matches(value, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    Check(wanted.SetEquals(got), "health texts: " + key + " keeps its {names} in " + code);
                    Check(Regex.Matches(en, "<b>").Count == Regex.Matches(value, "<b>").Count && Regex.Matches(en, @"\n").Count == Regex.Matches(value, @"\n").Count, "health texts: " + key + " keeps its tags and lines in " + code);
                }
                if (key.Contains("_note_") || key.Contains("_test_"))
                {
                    Check(value.Length <= 240, "health texts: the notice " + key + " is at most 240 characters in " + code + " (" + value.Length + ")");
                }
            }
            if (words.TryGetValue("reallife_guide_health", out string page))
            {
                foreach (string fact in facts)
                {
                    Check(page.Contains(fact), "health texts: the Guide page has " + fact + " in " + code);
                }
            }
        }
        string guide = ModSource(@"Health\HealthGuide.cs");
        foreach (string fact in facts)
        {
            Check(guide.Contains(".Fact(\"" + fact.Trim('{', '}') + "\""), "health texts: the Guide's " + fact + " is filled from the catalogue");
        }
        // What's new: the feature's line in the next row of every language, before the first fix mark.
        string[] sentences =
        {
            "You can now fall seriously ill or be hurt and spend days in hospital while your businesses run on, book the doctor from the new Health app, take a health plan that pays part of the bills, pay a bill off as a medical debt and use sick leave at your job, while your staff, the rivals and the city's people fall ill by the same rules and the flu goes round in midwinter.",
            "Agora você pode adoecer gravemente ou se machucar e passar dias no hospital enquanto seus negócios continuam, marcar consulta pelo novo app Saúde, contratar um plano de saúde que paga parte das contas, quitar uma conta como dívida médica e usar a licença médica no trabalho, enquanto seus funcionários, os rivais e a população da cidade adoecem pelas mesmas regras e a gripe circula no meio do inverno.",
            "你现在可能会重病或受伤并住院好几天，而你的生意照常运转，你可以在新的“健康”应用中预约医生，购买一份能支付部分账单的健康方案，把一笔账单作为医疗欠款慢慢还清，并在工作中使用病假，而你的员工、对手和城里的居民也按同样的规则生病，隆冬时流感还会四处传播。",
            "Du kannst jetzt ernsthaft krank werden oder dich verletzen und Tage im Krankenhaus verbringen, während deine Geschäfte weiterlaufen, du den Arzt in der neuen App Gesundheit buchst, einen Gesundheitstarif nimmst, der einen Teil der Rechnungen zahlt, eine Rechnung als Arztschuld begleichst und bei der Arbeit Krankmeldung nutzt, während deine Mitarbeiter, die Rivalen und die Leute der Stadt nach denselben Regeln krank werden und im Winter die Grippe umgeht.",
            "Vous pouvez désormais tomber gravement malade ou être blessé et passer des jours à l'hôpital pendant que vos entreprises continuent, prendre rendez-vous depuis l'application Santé, souscrire un plan santé qui paie une partie des factures, régler une facture comme dette médicale et prendre un arrêt maladie au travail, tandis que votre personnel, vos rivaux et les habitants de la ville tombent malades selon les mêmes règles et que la grippe circule en plein hiver.",
            "Ahora puedes enfermar gravemente o lesionarte y pasar días en el hospital mientras tus negocios siguen funcionando, pedir cita con el médico desde la nueva app Salud, contratar un plan de salud que paga parte de las facturas, pagar una factura como deuda médica y usar la baja laboral en tu trabajo, mientras tu personal, tus rivales y la gente de la ciudad enferman con las mismas reglas y la gripe circula en pleno invierno.",
            "Теперь вы можете серьёзно заболеть или получить травму и провести дни в больнице, пока ваш бизнес продолжает работать, записаться к врачу в новом приложении Здоровье, оформить страховой план, который покрывает часть счетов, погасить счёт как медицинский долг и брать больничный на работе, а сотрудники, конкуренты и жители города заболевают по тем же правилам, и зимой по городу ходит грипп.",
            "Je kunt nu ernstig ziek worden of gewond raken en dagen in het ziekenhuis doorbrengen terwijl je bedrijven doordraaien, de dokter boeken in de nieuwe app Gezondheid, een zorgplan nemen dat een deel van de rekeningen betaalt, een rekening als medische schuld afbetalen en ziekteverlof gebruiken op je werk, terwijl je personeel, de rivalen en de mensen in de stad volgens dezelfde regels ziek worden en de griep in het midden van de winter rondgaat.",
            "Artık işletmeleriniz çalışmaya devam ederken ağır hastalanabilir ya da yaralanabilir ve hastanede günler geçirebilirsiniz; yeni Sağlık uygulamasından doktora randevu alabilir, faturaların bir kısmını ödeyen bir sağlık planı edinebilir, bir faturayı tıbbi borç olarak kapatabilir ve işinizde hastalık izni kullanabilirsiniz; personeliniz, rakipleriniz ve şehrin insanları da aynı kurallarla hastalanırken kışın ortasında grip de yayılır.",
            "Teraz możesz poważnie zachorować albo ulec wypadkowi i spędzić dni w szpitalu, podczas gdy twoje firmy działają dalej, umówić wizytę u lekarza w nowej aplikacji Zdrowie, wykupić plan zdrowotny, który płaci część rachunków, spłacić rachunek jako dług medyczny i wziąć zwolnienie chorobowe w pracy, a twoi pracownicy, rywale i mieszkańcy miasta chorują na tych samych zasadach, a zimą krąży grypa.",
            "이제 회사를 계속 운영하는 동안 중병에 걸리거나 다쳐 며칠씩 입원하실 수 있으며, 새 건강 앱에서 의사 진료를 예약하고, 청구서 일부를 대신 내주는 건강 플랜에 가입하고, 청구서를 의료 부채로 갚고, 직장에서 병가를 쓸 수 있으며, 직원, 경쟁자, 도시 주민도 같은 규칙으로 병에 걸리고 한겨울에는 독감이 유행합니다."
        };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: ", "Naprawiono: ", "수정: " };
        for (int i = 0; i < HealthLanguages.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, HealthLanguages[i] + ".json"));
            // (any row of What's new, the version step moves the next row into a numbered one) in its own language, as ONE sentence: one full stop, at the end
            bool there = words != null && WhatsNewHas(words, sentences[i]);
            int stops = sentences[i].Count(ch => ch == '.' || ch == '。');
            Check(there && stops == 1 && (sentences[i].EndsWith(".", StringComparison.Ordinal) || sentences[i].EndsWith("。", StringComparison.Ordinal)),
                "health texts: What's new has the feature's line as one sentence, in its own language, in " + HealthLanguages[i]);
            string next = words != null && words.TryGetValue("reallife_changes_next", out string value) ? value : string.Empty;
            int at = next.IndexOf(sentences[i], StringComparison.Ordinal), mark = next.IndexOf(marks[i], StringComparison.Ordinal);
            Check(next.Length == 0 || at < 0 || mark < 0 || at < mark, "health texts: in the next row the feature's line is before the first fix mark in " + HealthLanguages[i]);
        }
    }
}
