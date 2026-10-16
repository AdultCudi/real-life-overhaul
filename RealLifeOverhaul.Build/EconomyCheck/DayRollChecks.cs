using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy.Brands;

// The game's night kept whole (Scripts/Core/DayRollRules.cs, DayRollGuard.cs). A player, 16 Oct 2026: three gyms in three
// districts, "I hire employees and within a few days the salaries are higher than the daily income ... daily payrolls
// reaching over 8k a day", firing everyone put it right, staff "scheduled but showing they do not have the 48 hours", and
// in a new save no brand recognition in 40 days. Every night of their logs (days 37 to 62, 0.27.0 to 0.28.0) the game's
// new day threw in its rivals' step: "Item with name it-services:itemname_hourlycloudservicesfee not found in
// ItemsDictionary", then a NullReferenceException in CompetitionHelper.GetSuitableBuildingTypeForItem from
// CompetitionHelper.RunDaily from GameManager.NewDay. The goods of a mod that was not loaded sat in the city's market.
// NewDay had paid the wages (each employee's hours of the day times the wage) and stopped there: the day event this
// mod's brands wait for, and the employees' day that clears the hours, never ran. Reproduced here with the game's
// night as steps, three gyms of eight paid 37 an hour for 32 hours a week; then the guard's two answers.
internal static partial class Program
{
    private const int RollStaff = 24;          // three gyms of eight
    private const int RollWage = 3700;         // cents an hour: the player's rota paid 9,570 a week for 256 hours at one gym
    private const string RollBadGood = "it-services:itemname_hourlycloudservicesfee";

    private static void DayRoll()
    {
        DayRollCreep();
        DayRollGoods();
        DayRollOtherFault();
        DayRollFirstNight();
        DayRollBrand();
        DayRollEdges();
        DayRollSources();
        DayRollWords();
    }

    private sealed class RollPerson
    {
        public readonly int[] Shift = new int[7];   // hours on each day of the week, Monday first
        public int Worked, Week;
        public int WeekShifts => SinceMonday(6);

        // Shifts from Monday to a day of the week (0 Monday), as DayRollGuard.Shifts reads them from the schedule.
        public int SinceMonday(int weekday)
        {
            int sum = 0;
            for (int d = 0; d <= weekday; d++)
            {
                sum += Shift[d];
            }
            return sum;
        }
    }

    private sealed class RollRun
    {
        public readonly List<RollPerson> Staff = new List<RollPerson>();
        public readonly List<string> Market = new List<string> { "ba:itemname_gymentrance", "ba:itemname_coffee", RollBadGood };
        public readonly List<long> Payroll = new List<long>();   // cents paid on each night
        public int DayEvents, MostWeek, RollDay, DoneDay;
        public BrandsState Brands;
        public Brand Brand;
    }

    private static int Weekday(int day)
    {
        return (day - 1) % 7;   // 0 Monday, as TimeHelper.GetDayOfWeek
    }

    // Twenty-four staff, four days of eight hours each, the days spread over the week.
    private static RollRun RollSetup(bool brand)
    {
        var run = new RollRun();
        for (int i = 0; i < RollStaff; i++)
        {
            var p = new RollPerson();
            for (int k = 0; k < 4; k++)
            {
                p.Shift[(i + k * 2) % 7] += 8;
            }
            run.Staff.Add(p);
        }
        if (brand)
        {
            run.Brands = Spread(new[] { 1, 1, 1 }, 100, 75, out run.Brand);
        }
        return run;
    }

    private static bool RollKnown(string good)
    {
        return good != RollBadGood;
    }

    // Days 1 to 'days' of the game: each day's shifts counted an hour at a time, then the night of the next day in the
    // order GameManager.NewDay takes its steps. 'guard' is DayRollGuard's first hour; 'goods' whether the market still
    // holds the unknown good at the start; 'otherFault' a night that stops at the rivals' step for another reason.
    private static RollRun RollDays(int days, bool guard, bool goods, bool otherFault, bool brand)
    {
        RollRun run = RollSetup(brand);
        if (!goods)
        {
            run.Market.Remove(RollBadGood);
        }
        for (int day = 1; day <= days; day++)
        {
            foreach (RollPerson p in run.Staff)
            {
                int hours = p.Shift[Weekday(day)];
                p.Worked += hours;   // (EmployeeInstance.RunHourly: one an hour on shift)
                p.Week += hours;
                run.MostWeek = Math.Max(run.MostWeek, p.Week);
            }
            RollNight(run, day + 1, guard, otherFault);
        }
        return run;
    }

    private static void RollNight(RollRun run, int night, bool guard, bool otherFault)
    {
        // the hour event, first thing in NewDay (the guard)
        if (guard)
        {
            bool missed = DayRollRules.Missed(run.RollDay, run.DoneDay, night);
            run.RollDay = night;
            run.Market.RemoveAll(g => !RollKnown(g));
            foreach (RollPerson p in run.Staff)
            {
                if (missed || p.Worked > DayRollRules.DayHours)
                {
                    p.Worked = DayRollRules.Mend(p.Worked, p.Shift[Weekday(night - 1)], true);
                    p.Week = DayRollRules.Week(p.Week, p.SinceMonday(Weekday(night - 1)));
                }
            }
        }
        // EmployeeHelper.PayDailyWages: the hours counted times the wage
        long paid = 0;
        foreach (RollPerson p in run.Staff)
        {
            paid += (long)p.Worked * RollWage;
        }
        run.Payroll.Add(paid);
        // CompetitionHelper.RunDaily: throws on a good the game cannot find (or another mod's fault); the rest of the night is lost
        bool throws = otherFault || run.Market.Exists(g => !RollKnown(g));
        if (throws)
        {
            // (the throw leaves the game's tick: the first hour is not counted.) The next hour's event: the guard does the employees' day.
            if (guard && DayRollRules.Stopped(run.RollDay, run.DoneDay, night))
            {
                foreach (RollPerson p in run.Staff)
                {
                    p.Worked = DayRollRules.Cleared(p.Worked);
                    p.Week = DayRollRules.ClearedWeek(p.Week, Weekday(night) == 0);
                }
            }
            return;
        }
        // GlobalEvents.onNewDay: the mod's day (the brands' step among it)
        run.DayEvents++;
        run.DoneDay = night;
        if (run.Brand != null)
        {
            BrandRules.Day(run.Brands, run.Brand);
        }
        // EmployeeHelper.RunDaily: the day's hours cleared, and the week's on a Monday
        foreach (RollPerson p in run.Staff)
        {
            p.Worked = 0;
            if (Weekday(night) == 0)
            {
                p.Week = 0;
            }
        }
    }

    private static void DayRollCreep()
    {
        RollRun before = RollDays(14, false, true, false, false);
        RollRun clean = RollDays(14, false, false, false, false);
        long first = before.Payroll[0], third = before.Payroll[2], last = before.Payroll[13];
        bool grows = true;
        for (int i = 1; i < before.Payroll.Count; i++)
        {
            grows &= before.Payroll[i] > before.Payroll[i - 1];
        }
        long cleanMost = 0;
        foreach (long p in clean.Payroll)
        {
            cleanMost = Math.Max(cleanMost, p);
        }
        Check(grows && third > 800000 && last > 6 * cleanMost && before.DayEvents == 0,
            "day roll: the report reproduced: with the unknown good in the market every night stops at the rivals' step, the hours are never cleared and the payroll grows every night: "
            + first / 100 + ", then " + third / 100 + " on the third night (past 8,000), " + last / 100 + " on the fourteenth; a city without the good pays at most " + cleanMost / 100 + " a night; day events " + before.DayEvents);
        Check(before.MostWeek > 48 && clean.MostWeek <= 32,
            "day roll: and the week's hours are never cleared either: " + before.MostWeek + " counted in a week against shifts of 32 (an hours-a-week demand of up to 48 shows unmet), " + clean.MostWeek + " without the good");
    }

    private static void DayRollGoods()
    {
        RollRun guarded = RollDays(30, true, true, false, false);
        RollRun clean = RollDays(30, false, false, false, false);
        bool same = guarded.Payroll.Count == clean.Payroll.Count;
        for (int i = 0; same && i < clean.Payroll.Count; i++)
        {
            same = guarded.Payroll[i] == clean.Payroll[i];
        }
        Check(same && guarded.DayEvents == 30 && !guarded.Market.Contains(RollBadGood) && guarded.Market.Count == 2,
            "day roll: with the guard the unknown good is taken out at the first night's first hour, before the rivals' step: every night runs to its end (" + guarded.DayEvents
            + " of 30 day events), the other goods are kept, and every night's payroll is the clean city's to the cent");
    }

    private static void DayRollOtherFault()
    {
        RollRun guarded = RollDays(30, true, false, true, false);
        RollRun clean = RollDays(30, false, false, false, false);
        RollRun bare = RollDays(30, false, false, true, false);
        bool same = true;
        for (int i = 0; i < clean.Payroll.Count; i++)
        {
            same &= guarded.Payroll[i] == clean.Payroll[i];
        }
        Check(same && guarded.MostWeek <= 32 && bare.Payroll[29] > 10 * clean.Payroll[29] / 4,
            "day roll: a night that stops for another mod's fault (nothing in the market to take out): the hour after it clears the hours as the game's employees' day would, so the payroll is the clean city's every night and the week's hours stay within the shifts ("
            + guarded.MostWeek + "); without the guard the thirtieth night pays " + bare.Payroll[29] / 100);
    }

    // A save that ran with the fault for weeks, loaded with the guard: nothing seen before the first night, but the hours prove it.
    private static void DayRollFirstNight()
    {
        RollRun run = RollSetup(false);
        foreach (RollPerson p in run.Staff)
        {
            p.Worked = 40 * 5;
            p.Week = 120;
        }
        int day = 61;   // the player's day 61 to 62
        RollNight(run, day + 1, true, false);
        long most = 0;
        foreach (RollPerson p in run.Staff)
        {
            most = Math.Max(most, p.Worked);
        }
        long expected = 0;
        foreach (RollPerson p in RollSetup(false).Staff)
        {
            expected += (long)p.Shift[Weekday(day)] * RollWage;
        }
        bool weeks = true;
        foreach (RollPerson p in run.Staff)
        {
            weeks &= p.Week <= p.SinceMonday(Weekday(day)) && p.Week <= 32;   // (the night is not a Monday's: the week runs on from the shifts so far)
        }
        Check(run.Payroll[0] == expected && weeks && run.DayEvents == 1,
            "day roll: the first night after the update, in a save whose staff carry 200 hours each: they are paid for their shifts on the day that ended (" + expected / 100
            + " in all, not " + (long)RollStaff * 200 * RollWage / 100 + "), their week is brought to their shifts since Monday, and the night runs to its end");
    }

    private static void DayRollBrand()
    {
        RollRun stuck = RollDays(40, false, true, false, true);
        RollRun guarded = RollDays(40, true, true, false, true);
        int stuckMost = 0;
        foreach (DistrictFile d in stuck.Brand.Districts)
        {
            stuckMost = Math.Max(stuckMost, d.Rec);
        }
        int lowest = int.MaxValue;
        foreach (DistrictFile d in guarded.Brand.Districts)
        {
            lowest = Math.Min(lowest, d.Rec);
        }
        Check(stuckMost == 0 && stuck.Brand.City == 0,
            "day roll: the brand reproduced: three gyms in three districts for 40 days with every night stopped before the day event: recognition " + stuckMost + " in every district and " + stuck.Brand.City + " city-wide (the player's 0.0)");
        Check(lowest >= 800 && guarded.Brand.City >= 800,
            "day roll: with the guard the same brand's day runs every night: " + lowest + " hundredths in each district and " + guarded.Brand.City + " city-wide by day 40, as BrandPaceSpread expects");
    }

    private static void DayRollEdges()
    {
        Check(!DayRollRules.Missed(0, 0, 62) && !DayRollRules.Missed(61, 61, 62) && DayRollRules.Missed(61, 60, 62) && !DayRollRules.Missed(59, 58, 62) && !DayRollRules.Missed(62, 0, 62),
            "day roll: a night counts as stopped only when its first hour was seen the day before and its day event was not; a first night after a load, a night seen long ago or the same night again does not");
        Check(DayRollRules.Mend(8, 8, false) == 8 && DayRollRules.Mend(24, 0, false) == 24 && DayRollRules.Mend(25, 8, false) == 8 && DayRollRules.Mend(16, 8, true) == 8
              && DayRollRules.Mend(5, 8, true) == 5 && DayRollRules.Mend(300, 30, false) == 24 && DayRollRules.Mend(300, -4, true) == 0 && DayRollRules.Mend(-3, 8, false) == 0
              && DayRollRules.Mend(0, 0, true) == 0 && DayRollRules.Mend(int.MaxValue, 8, false) == 8,
            "day roll: the hours paid: a day as counted is kept up to 24; more than a day, or a stopped night, is brought to the shifts of the day that ended, never above 24 or below nothing; fewer than the shifts are never raised");
        Check(DayRollRules.Week(120, 32) == 32 && DayRollRules.Week(20, 32) == 20 && DayRollRules.Week(-1, 32) == 0 && DayRollRules.Week(50, -5) == 0 && DayRollRules.Week(0, 0) == 0,
            "day roll: the week's hours of somebody mended: no more than their shifts from Monday to the day that ended, never below nothing");
        Check(DayRollRules.Stopped(62, 61, 62) && !DayRollRules.Stopped(62, 62, 62) && !DayRollRules.Stopped(0, 0, 62) && !DayRollRules.Stopped(61, 60, 62)
              && DayRollRules.Cleared(17) == 0 && DayRollRules.ClearedWeek(40, true) == 0 && DayRollRules.ClearedWeek(40, false) == 40 && DayRollRules.ClearedWeek(-2, false) == 0,
            "day roll: the hour after a night counts it stopped only when that night's first hour was seen and its day event was not; then the day's hours are cleared, and the week's on a Monday only");
        Check(!DayRollRules.Needs(8, 8, true) && DayRollRules.Needs(30, 8, false) && !DayRollRules.Needs(20, 8, false) && DayRollRules.Needs(16, 8, true),
            "day roll: something needs mending only where the hours would change");
        RollRun a = RollDays(21, true, true, true, true), b = RollDays(21, true, true, true, true);
        bool same = a.Payroll.Count == b.Payroll.Count && a.Brand.City == b.Brand.City;
        for (int i = 0; same && i < a.Payroll.Count; i++)
        {
            same = a.Payroll[i] == b.Payroll[i];
        }
        Check(same, "day roll: the same nights give the same pay and the same recognition (nothing random: the same on every machine of a Going Public session)");
    }

    private static void DayRollSources()
    {
        string core = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Core");
        string guard = System.IO.File.ReadAllText(System.IO.Path.Combine(core, "DayRollGuard.cs"));
        string start = System.IO.File.ReadAllText(System.IO.Path.Combine(core, "RealLifeOverhaulMod.cs"));
        Check(guard.Contains("save.Hour == 1") && guard.Contains("AfterNight(save)") && guard.Contains("DayRollRules.Stopped(") && guard.Contains("EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen)") && guard.Contains("EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen)"),
            "day roll: the guard works in the hour event with the hour at nothing, the one GameManager.NewDay calls before the wages and the rivals' step, and watches the day event to know a night that stopped");
        Check(guard.Contains("ItemsGetter.GetByName(entry.itemName, true)") && guard.Contains("market.RemoveAt(i)") && guard.Contains("DayRollRules.Missed(") && guard.Contains("DayRollRules.Mend(")
              && guard.Contains("DayRollRules.Week(") && guard.Contains("GoingPublic.Foreign(reg)"),
            "day roll: the guard asks the game for each good quietly, takes out only the ones it cannot find, mends hours by the rules, and leaves another player's employees alone");
        Check(start.Contains("Guard.Add<DayRollGuard>(_root)") && start.Contains("DayRollGuard.Logger = context.Logger"), "day roll: the guard is started with the city, with its logger");
    }

    private static void DayRollWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("day roll", langs, codes))
        {
            return;
        }
        bool texts = true, plain = true;
        string[] keys = { "reallife_dayroll_note_goods", "reallife_dayroll_note_hours" };
        for (int l = 0; l < 3 && l < langs.Length; l++)
        {
            foreach (string key in keys)
            {
                bool has = langs[l].TryGetValue(key, out string text) && text.Length > 20;
                texts &= has && (l == 0 || text != langs[0][key]);
                plain &= has && !text.Contains("!") && !text.Contains("—") && !text.Contains("–") && !text.Contains(" - ") && text.Length <= 240;
            }
        }
        Check(texts && plain, "day roll: the two notices are in English, Portuguese and Chinese, each within 240 characters, with no dash and no exclamation mark");
        string[] wanted = { "Fixed: when the city's market held goods of a mod that is no longer loaded", "Corrigido: quando o mercado da cidade tinha produtos de um mod", "已修复：当城市市场里有已不再加载的模组的商品时" };
        bool line = true;
        for (int l = 0; l < 3 && l < langs.Length; l++)
        {
            bool found = false;
            foreach (var row in langs[l])
            {
                found |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(wanted[l]);
            }
            line &= found;
        }
        Check(line, "day roll: What's new has the fix's line in English, Portuguese and Chinese, in some reallife_changes_ row");
    }
}
