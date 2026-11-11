using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Tech;

// Programmers hired at IT companies and game studios were not placed in the roles and seats their firms need, and data centre crews never arrived
// (a player's report, 9 Nov 2026, from a save of some six thousand employees). Two causes, both held here. The professionals' files stopped at 600
// people, so a hire after the 600th got no role, was left out of every crew and the Team tab, and a payroll start (a data centre's crew) was
// refused until somebody left: the file is now kept for every professional the game holds. And no hire was ever given a shift, the rota planner
// refusing a business of over 64 stations or 120 staff: a hire at a tech firm is now seated at a free desk with a week of shifts that keeps to
// their demands (Tech/Firms/SeatPlan, SeatShifts), for every way of hiring, as the hour's pass finds them. The rules, the sources and the texts.
internal static partial class Program
{
    private static void TechStaff()
    {
        TechStaffFiles();
        TechStaffSeatRules();
        TechStaffSeatRandom();
        TechStaffSources();
        TechStaffTexts();
    }

    // ---------------------------------------------------------------- the professionals' files

    private static TalentFile TechStaffFile(int people)
    {
        var f = new TalentFile { Seed = 11, FirstDay = 1, LastDay = 300, NextPro = 3 };
        for (int i = 0; i < people; i++)
        {
            f.People.Add(new Profile
            {
                Id = "emp_" + i, Role = i % TalentCatalogue.RoleCount, Level = i % TalentCatalogue.Levels, Main = 40000 + i, Joined = 5 + i % 200,
                Place = PlaceRules.Key("Main Street", 1 + i % 60), Name = "Staff " + i
            });
        }
        return f;
    }

    private static void TechStaffFiles()
    {
        // a save past the old 600: written and read whole, the last of them there (the line held 600 and dropped the rest)
        foreach (int people in new[] { 601, 2000, 6100 })
        {
            TalentFile file = TechStaffFile(people);
            string line = TalentStore.Write(file);
            TalentFile back = TalentStore.Read(line, out int version);
            Check(back.People.Count == people && back.People[people - 1].Id == "emp_" + (people - 1) && version == TalentStore.Version && TalentStore.Write(back) == line,
                "tech staff: a save of " + people + " professionals is written and read back whole, the last of them there (" + back.People.Count + " read)");
        }
        Check(TalentStore.PeopleMost >= 6100 + 24 && TalentStore.PeopleMost > 600,
            "tech staff: the bound on files (" + TalentStore.PeopleMost + ") is above the employees of the largest save seen, some 6,024 in all, and no longer the old 600");

        // the rules a read keeps: the first of an id is the one, and a record with no id is no file
        TalentFile dup = TechStaffFile(5);
        dup.People[3].Id = dup.People[1].Id;
        dup.People[4].Id = string.Empty;
        TalentFile kept = TalentStore.Read(TalentStore.Write(dup), out int v2);
        Check(v2 == TalentStore.Version && kept.People.Count == 3 && kept.People[0].Id == "emp_0" && kept.People[1].Id == "emp_1" && kept.People[2].Id == "emp_2",
            "tech staff: a file's id twice leaves the first, and a record with no id is dropped, as before");

        // a read is a pass over the line with a set of the ids, not a search of the files read so far for each (it was a square of the number of files)
        var watch = Stopwatch.StartNew();
        TalentFile big = TechStaffFile(TalentStore.PeopleMost);
        string bigLine = TalentStore.Write(big);
        TalentFile bigBack = TalentStore.Read(bigLine, out int v3);
        double ms = watch.Elapsed.TotalMilliseconds;
        Check(bigBack.People.Count == TalentStore.PeopleMost && v3 == TalentStore.Version && ms < 4000.0,
            "tech staff: a line of " + TalentStore.PeopleMost + " professionals (" + bigLine.Length + " characters) is written and read in " + ms.ToString("0") + " ms (wanted under 4,000)");
    }

    // ---------------------------------------------------------------- the seat rule

    private static SeatSpace TechStaffRoom(int desks, int firstDay, int lastDay, int from, int to)
    {
        var room = new SeatSpace();
        room.Size(desks);
        for (int d = firstDay; d <= lastDay; d++)
        {
            for (int h = from; h < to; h++)
            {
                room.Open[d * SeatSpace.Hours + h] = true;
            }
        }
        for (int s = 0; s < desks; s++)
        {
            room.Usable[s] = true;
        }
        room.Seal();
        return room;
    }

    private static int TechStaffHours(SeatShift[] week, int count)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
        {
            total += week[i].To - week[i].From;
        }
        return total;
    }

    // What the game side does once a plan is made: the week's hours are taken at their desks.
    private static int TechStaffSeat(SeatSpace room, int weekHours, SeatJudge judge, SeatShift[] week, out int count, out int unmet)
    {
        int result = SeatPlan.Plan(room, weekHours, judge, week, out count, out unmet);
        for (int i = 0; result == SeatPlan.Seated && i < count; i++)
        {
            room.Take(week[i].Station, week[i].Day, week[i].From, week[i].To);
        }
        return result;
    }

    private static int TechStaffTaken(SeatSpace room)
    {
        int taken = 0;
        for (int i = 0; i < room.Stations * SeatSpace.Cells; i++)
        {
            taken += room.Taken[i] ? 1 : 0;
        }
        return taken;
    }

    private static SeatJudge TechStaffJudge(int kind)
    {
        switch (kind)
        {
            case 1:   // free on Mondays
                return (w, n) =>
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (w[i].Day == 0)
                        {
                            return 5;
                        }
                    }
                    return 0;
                };
            case 2:   // exactly four days a week
                return (w, n) => n == 4 ? 0 : 3;
            case 3:   // nothing before noon
                return (w, n) =>
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (w[i].From < 12)
                        {
                            return 3;
                        }
                    }
                    return 0;
                };
            default:
                return (w, n) => 0;
        }
    }

    private static void TechStaffSeatRules()
    {
        SeatJudge any = TechStaffJudge(0);
        var week = new SeatShift[SeatSpace.Days];

        // the usual week: one desk, open Monday to Saturday from 8 to 16: five days of eight hours on that desk, from the day's start
        SeatSpace one = TechStaffRoom(1, 0, 5, 8, 16);
        int r = TechStaffSeat(one, 40, any, week, out int n, out int unmet);
        bool usual = r == SeatPlan.Seated && n == 5 && unmet == 0 && TechStaffHours(week, n) == 40;
        for (int i = 0; usual && i < n; i++)
        {
            usual = week[i].Station == 0 && week[i].Day == i && week[i].From == 8 && week[i].To == 16;
        }
        Check(usual, "tech staff, seat rule: a hire at a desk open Monday to Saturday from 8 to 16 gets five days of eight hours (40 a week), Monday to Friday, on that desk");
        int secondResult = TechStaffSeat(one, 40, any, week, out int secondCount, out int secondUnmet);
        Check(secondResult == SeatPlan.Full && secondCount == 0 && TechStaffTaken(one) == 40,
            "tech staff, seat rule: a second hire at the same one desk is not given a Saturday and no more (under half a week is not a seat): the answer is that every desk is taken, and nothing is taken");

        // desks are shared and never doubled: three hires on three desks, a fourth finds none free
        SeatSpace three = TechStaffRoom(3, 0, 5, 8, 16);
        bool shared = true;
        for (int hire = 0; hire < 3; hire++)
        {
            int got = TechStaffSeat(three, 40, any, week, out int count, out int broke);
            shared &= got == SeatPlan.Seated && count == 5 && broke == 0;
            for (int i = 0; shared && i < count; i++)
            {
                shared &= week[i].Station == hire;
            }
        }
        int fourth = TechStaffSeat(three, 40, any, week, out int fourthCount, out int fourthUnmet);
        Check(shared && fourth == SeatPlan.Full && TechStaffTaken(three) == 3 * 5 * 8,
            "tech staff, seat rule: three hires take three desks one each, a fourth finds every desk taken, and no hour of a desk is taken twice (" + TechStaffTaken(three) + " of 120)");

        // a business open all day: the day shift first, then the night, then the evening, on the one desk
        SeatSpace round = TechStaffRoom(1, 0, 6, 0, 24);
        int[] starts = new int[3];
        bool roundTheClock = true;
        for (int hire = 0; hire < 3; hire++)
        {
            int got = TechStaffSeat(round, 40, any, week, out int count, out int broke);
            roundTheClock &= got == SeatPlan.Seated && count == 5 && TechStaffHours(week, count) == 40;
            starts[hire] = week[0].From;
        }
        Check(roundTheClock && starts[0] == 8 && starts[1] == 0 && starts[2] == 16 && TechStaffTaken(round) == 3 * 5 * 8,
            "tech staff, seat rule: at a desk open round the clock the first hire has the day (8 to 16), the next the night (0 to 8), the next the evening (16 to 24), nothing doubled (" + starts[0] + ", " + starts[1] + ", " + starts[2] + ")");

        // the hire's demands, asked through the judge: no Monday, exactly four days, nothing before noon, hours between 20 and 24, and one that cannot be met
        SeatSpace room = TechStaffRoom(2, 0, 6, 8, 20);
        r = TechStaffSeat(room, 40, TechStaffJudge(1), week, out n, out unmet);
        bool noMonday = r == SeatPlan.Seated && unmet == 0 && n == 5;
        for (int i = 0; i < n; i++)
        {
            noMonday &= week[i].Day != 0;
        }
        room = TechStaffRoom(2, 0, 6, 8, 20);
        r = TechStaffSeat(room, 40, TechStaffJudge(2), week, out n, out unmet);
        bool fourDays = r == SeatPlan.Seated && unmet == 0 && n == 4 && TechStaffHours(week, n) == 40;
        room = TechStaffRoom(2, 0, 6, 8, 20);
        r = TechStaffSeat(room, 40, TechStaffJudge(3), week, out n, out unmet);
        bool afternoon = r == SeatPlan.Seated && unmet == 0 && n > 0;
        for (int i = 0; i < n; i++)
        {
            afternoon &= week[i].From >= 12;
        }
        room = TechStaffRoom(2, 0, 6, 8, 20);
        r = TechStaffSeat(room, 24, (w, c) => TechStaffHours(w, c) >= 20 && TechStaffHours(w, c) <= 24 ? 0 : 5, week, out n, out unmet);
        bool partTime = r == SeatPlan.Seated && unmet == 0 && TechStaffHours(week, n) >= 20 && TechStaffHours(week, n) <= 24;
        room = TechStaffRoom(2, 0, 6, 8, 20);
        r = TechStaffSeat(room, 40, (w, c) => 7, week, out n, out unmet);
        bool cannot = r == SeatPlan.Seated && unmet == 7 && n > 0 && TechStaffHours(week, n) * 2 >= 40;
        Check(noMonday, "tech staff, seat rule: a hire who must be free on Mondays is seated on five other days");
        Check(fourDays, "tech staff, seat rule: a hire who works exactly four days a week is seated for four days of ten hours");
        Check(afternoon, "tech staff, seat rule: a hire who works nothing before noon is seated from noon on");
        Check(partTime, "tech staff, seat rule: a hire who works 20 to 24 hours a week is seated for hours in that range (" + TechStaffHours(week, n) + ")");
        Check(cannot, "tech staff, seat rule: where no week keeps a demand the hire is still seated, with the week that breaks the least, and the weight broken is told (" + unmet + ")");

        // a desk that suits the hire's wishes is preferred, and a desk they may not use never is
        SeatSpace ranked = TechStaffRoom(3, 0, 5, 8, 16);
        ranked.Rank[0] = 1;
        ranked.Rank[2] = 1;
        r = TechStaffSeat(ranked, 40, any, week, out n, out unmet);
        bool onBest = r == SeatPlan.Seated;
        for (int i = 0; i < n; i++)
        {
            onBest &= week[i].Station == 1;
        }
        SeatSpace barred = TechStaffRoom(2, 0, 5, 8, 16);
        barred.Usable[0] = false;
        r = TechStaffSeat(barred, 40, any, week, out n, out unmet);
        bool notBarred = r == SeatPlan.Seated;
        for (int i = 0; i < n; i++)
        {
            notBarred &= week[i].Station == 1;
        }
        Check(onBest && notBarred, "tech staff, seat rule: the desk every wish of the hire accepts is chosen first, and a desk they may not use is never chosen");

        // why not: closed, no desk, every desk taken
        SeatSpace shut = TechStaffRoom(2, 0, 5, 8, 16);
        Array.Clear(shut.Open, 0, shut.Open.Length);
        SeatSpace none = TechStaffRoom(2, 0, 5, 8, 16);
        none.Usable[0] = false;
        none.Usable[1] = false;
        SeatSpace taken = TechStaffRoom(1, 0, 5, 8, 16);
        for (int d = 0; d < 6; d++)
        {
            taken.Take(0, d, 8, 16);
        }
        SeatSpace empty = TechStaffRoom(0, 0, 5, 8, 16);
        Check(SeatPlan.Plan(shut, 40, any, week, out n, out unmet) == SeatPlan.Closed && n == 0
              && SeatPlan.Plan(none, 40, any, week, out n, out unmet) == SeatPlan.NoDesk && n == 0
              && SeatPlan.Plan(empty, 40, any, week, out n, out unmet) == SeatPlan.NoDesk
              && SeatPlan.Plan(taken, 40, any, week, out n, out unmet) == SeatPlan.Full && n == 0,
            "tech staff, seat rule: a business with no open hour, no desk the hire may use, or every desk taken is told apart, and nothing is written for it");

        // the same answer for the same business and hire
        SeatSpace twin = TechStaffRoom(3, 0, 5, 8, 16);
        twin.Take(0, 0, 8, 12);
        twin.Take(1, 2, 8, 16);
        var firstWeek = new SeatShift[SeatSpace.Days];
        var secondWeek = new SeatShift[SeatSpace.Days];
        int firstResult = SeatPlan.Plan(twin, 40, TechStaffJudge(2), firstWeek, out int firstN, out int firstUnmet);
        int secondTime = SeatPlan.Plan(twin, 40, TechStaffJudge(2), secondWeek, out int secondN, out int secondUnmetAgain);
        bool same = firstResult == secondTime && firstN == secondN && firstUnmet == secondUnmetAgain;
        for (int i = 0; same && i < firstN; i++)
        {
            same &= firstWeek[i].Day == secondWeek[i].Day && firstWeek[i].Station == secondWeek[i].Station && firstWeek[i].From == secondWeek[i].From && firstWeek[i].To == secondWeek[i].To;
        }
        Check(same && firstResult == SeatPlan.Seated, "tech staff, seat rule: the same business and hire give the same week every time");

        // the window the hour's pass looks after a hire
        Check(!SeatPlan.Newcomer(0, 10) && !SeatPlan.Newcomer(-5, 10) && SeatPlan.Newcomer(10, 10) && SeatPlan.Newcomer(10, 10 + SeatPlan.Window) && !SeatPlan.Newcomer(10, 11 + SeatPlan.Window)
              && !SeatPlan.Newcomer(12, 10),
            "tech staff, seat rule: a hire is looked after from the day they were hired for " + SeatPlan.Window + " days; one with no day hired, or hired in the future, is not");

        // a hundred and twenty hires' worth of planning on a large firm's desks stays cheap
        SeatSpace large = TechStaffRoom(300, 0, 5, 8, 16);
        var watch = Stopwatch.StartNew();
        int placed = 0;
        for (int hire = 0; hire < 120; hire++)
        {
            placed += TechStaffSeat(large, 40, TechStaffJudge(1), week, out int count, out int broke) == SeatPlan.Seated ? 1 : 0;
        }
        double ms = watch.Elapsed.TotalMilliseconds;
        Check(placed == 120 && ms < 3000.0, "tech staff, seat rule: 120 hires seated on 300 desks, each asked of a judge, take " + ms.ToString("0") + " ms (wanted under 3,000), " + placed + " placed");
    }

    // A few hundred businesses and hires from a seed: whatever the rule writes is inside the open hours, on a free desk the hire may use, in
    // shifts of two to twelve hours, one a day, at least half the hours wanted, and the weight told is the judge's own.
    private static void TechStaffSeatRandom()
    {
        var rng = new Random(2026);
        int seated = 0, other = 0;
        string fault = string.Empty;
        var week = new SeatShift[SeatSpace.Days];
        var again = new SeatShift[SeatSpace.Days];
        for (int round = 0; round < 300 && fault.Length == 0; round++)
        {
            int desks = 1 + rng.Next(6);
            var room = new SeatSpace();
            room.Size(desks);
            for (int d = 0; d < SeatSpace.Days; d++)
            {
                if (rng.Next(7) == 0)
                {
                    continue;
                }
                int a = rng.Next(0, 12), b = Math.Min(24, a + 2 + rng.Next(0, 16));
                for (int h = a; h < b; h++)
                {
                    room.Open[d * SeatSpace.Hours + h] = true;
                }
            }
            for (int s = 0; s < desks; s++)
            {
                room.Usable[s] = rng.Next(5) != 0;
                room.Rank[s] = rng.Next(2);
                for (int c = 0; c < SeatSpace.Cells; c++)
                {
                    room.Taken[s * SeatSpace.Cells + c] = rng.Next(4) == 0;
                }
            }
            int wanted = new[] { 40, 24, 30, 20 }[rng.Next(4)];
            SeatJudge judge = TechStaffJudge(rng.Next(4));
            int result = SeatPlan.Plan(room, wanted, judge, week, out int count, out int unmet);
            int twice = SeatPlan.Plan(room, wanted, judge, again, out int countAgain, out int unmetAgain);
            if (result != twice || count != countAgain || unmet != unmetAgain)
            {
                fault = "round " + round + ": not the same twice";
                break;
            }
            if (result != SeatPlan.Seated)
            {
                other++;
                continue;
            }
            seated++;
            if (count < 1 || count > SeatSpace.Days || TechStaffHours(week, count) * 2 < Math.Max(SeatPlan.ShiftLeast, wanted) || unmet != judge(week, count))
            {
                fault = "round " + round + ": " + count + " shift(s), " + TechStaffHours(week, count) + " hour(s) for " + wanted + ", weight " + unmet;
                break;
            }
            for (int i = 0; i < count && fault.Length == 0; i++)
            {
                SeatShift w = week[i];
                bool ok = w.Station >= 0 && w.Station < desks && room.Usable[w.Station] && w.From >= 0 && w.To <= SeatSpace.Hours && w.To - w.From >= SeatPlan.ShiftLeast
                          && w.To - w.From <= SeatPlan.ShiftMost && (i == 0 || w.Day > week[i - 1].Day);
                for (int h = w.From; ok && h < w.To; h++)
                {
                    int cell = w.Day * SeatSpace.Hours + h;
                    ok = room.Open[cell] && !room.Taken[w.Station * SeatSpace.Cells + cell];
                }
                fault = ok ? string.Empty : "round " + round + ": shift " + i + " on day " + w.Day + " from " + w.From + " to " + w.To + " at desk " + w.Station;
            }
        }
        Check(fault.Length == 0 && seated >= 20 && other >= 1,
            "tech staff, seat rule, 300 random businesses: every week is inside the open hours, on a free desk the hire may use, one shift a day of 2 to 12 hours, at least half the hours wanted, "
            + "and the same twice (" + seated + " seated, " + other + " not; " + (fault.Length == 0 ? "no fault" : fault) + ")");
    }

    // ---------------------------------------------------------------- the sources

    private static void TechStaffSources()
    {
        string people = ModSource(@"Tech\TechDirector.People.cs");
        string hiring = ModSource(@"Tech\TechDirector.Hiring.cs");
        string seating = ModSource(@"Tech\TechDirector.Seating.cs");
        string game = ModSource(@"Tech\Firms\SeatShifts.cs");
        string store = ModSource(@"Tech\People\TalentStore.cs");
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string list = ModSource(@"Meta\FeatureList.Economy.cs");

        // the files: no silent stop at a number a player reaches, the bound told when it is met, a read that is one pass
        string read = MethodText(people, "private int ReadPeople(int day)");
        Check(read.Contains("_peopleFullTold") && read.Contains("Logger?.Warn(") && read.Contains("ReadShortages();") && read.Contains("continue;"),
            "tech staff sources: a professional past the files' bound is told in the log, once, and not dropped in silence; the shortages are asked once for the read");
        Check(store.Contains("seen.Add(p.Id)") && !store.Contains("f.Of(p.Id)") && store.Contains("HashSet<string>"),
            "tech staff sources: the people's line is read with a set of the ids, not a search of the files already read for each");
        string payroll = MethodText(hiring, "private bool StartPayroll(");
        Check(payroll.Contains("_talent.People.Count >= TalentStore.PeopleMost") && TalentStore.PeopleMost > 6024,
            "tech staff sources: a hire on the payroll (a data centre's crew) is refused only at the bound, which no save reaches");

        // the hour: the people are read, a hire the game's own hiring made is found, and the lately hired are seated, whichever way they were hired
        string hour = MethodText(people, "private void PeopleHour()");
        Check(hour.Contains("TalentGame.Employees(_employees)") && hour.Contains("ReadPeople(day);") && hour.Contains("SeatHour(day);")
              && CodeAt(hour, "ReadPeople(day);") >= 0 && CodeAt(hour, "SeatHour(day);") > CodeAt(hour, "ReadPeople(day);"),
            "tech staff sources: each hour a hire the game's recruitment office, job board or headhunter made, or the mod's own jobs board or search firm, is read as a new professional and then seated");
        string starts = MethodText(hiring, "private int Starts(int day)");
        Check(starts.Contains("seating = SeatsNew(candidate, at);") && starts.Contains("seating ? \"reallife_hire_note_started_seat\" : \"reallife_hire_note_started\"")
              && starts.Contains("HiringGame.Arrived(candidate, at, s.P.Name, day)"),
            "tech staff sources: a hire the mod's jobs board starts is noted as being seated, so its start note says a desk follows");

        // the seating part
        string hourPass = MethodText(seating, "private void SeatHour(int day)");
        string pass = MethodText(seating, "private SeatReport SeatPass(int day, bool all)");
        string test = MethodText(seating, "public void TestSeatHires()");
        Check(hourPass.Contains("Settings.TechSeatHires") && hourPass.Contains("_seatBroken") && hourPass.Contains("catch (Exception e)") && hourPass.Contains("SeatPass(day, false)"),
            "tech staff sources: the hour's seating is behind its option and its own fault guard, which switches seating off and leaves the sector running");
        Check(pass.Contains("SeatPlan.Batch") && pass.Contains("SeatPlan.TestMost") && pass.Contains("SeatPlan.Newcomer(e.dayHired, day)") && pass.Contains("TalentGame.StationShift(e, at)")
              && pass.Contains("TalentGame.Business(e)") && pass.Contains("FirmsGame.KindOf(TalentGame.TypeOf(at))") && pass.Contains("!TalentGame.Shifts(at)") && pass.Contains("e.IsTraining")
              && pass.Contains("Array.IndexOf(SeatRules.PrimaryOf(kind), TalentGame.Skill(e)) < 0")
              && pass.Contains("SeatShifts.Begin(reg);") && pass.Contains("finally") && pass.Contains("SeatShifts.End();") && pass.Contains("_seatTried") && pass.Contains("_seatTold")
              && pass.Contains("_seatHad.Add(e.id)"),
            "tech staff sources: the pass takes a batch of the lately hired at the business they are assigned to now, a tech firm with shifts, with no shift at a desk, not those in training, one business read at a time, and keeps what it told");
        Check(pass.Contains("GoingPublic.Mine(at)") && people.Contains("TalentGame.Employees(_employees);") && CodeAt(pass, "SeatShifts.Begin(reg);") > CodeAt(pass, "GoingPublic.Mine(at)"),
            "tech staff sources: the pass reads only the professionals the people part holds for the player and only a business that is the player's, never another player's in a Going Public session");
        Check(test.Contains("SeatPass(Save.Day, true)") && test.Contains("Tech check (seat test)") && test.Contains("\"PASS\" : \"FAIL\"") && test.Contains("reallife_hire_test_seat_none")
              && test.Contains("reallife_talent_test_off"),
            "tech staff sources: the test seats the idle at once, reads each back for a shift at a placed desk, writes PASS or FAIL to the log, and says why when it cannot start");
        Check(!seating.Contains("Update()") && !seating.Contains("LateUpdate()") && !seating.Contains("StartCoroutine") && !game.Contains("Update()"),
            "tech staff sources: nothing runs per frame");

        // the game side: shifts added the way the game's own schedule does, nobody else's touched, the game told once
        Check(game.Contains("day.AddWorkShift(new WorkShift") && game.Contains("e.UpdateWeeklyHoursAndDays();") && game.Contains("e.UpdateAssignedWorkStationItems();")
              && game.Contains("BizManSchedule.AbortAutoFillForBusiness(_reg);") && game.Contains("ItemHelper.HasAnyMissingRequirements(Stations[s])") && game.Contains("Kept[i].Fulfilled(_who, Mini)")
              && game.Contains("forceCheckForCompletedTodoTasks = true") && game.Contains("SaveGameManager.MarkChange();") && game.Contains("ScheduleHelper.OnWorkShiftChanged"),
            "tech staff sources: shifts are added to the business's days as the game's schedule does, the employee's figures brought up to date, the game's own test of each demand asked, and the game told once");
        Check(!game.Contains("RemoveWorkShift") && !game.Contains("ClearWorkShifts") && !game.Contains("RemoveAllWorkShiftsThatMatchPredicate") && !game.Contains(".workShifts.Remove")
              && !game.Contains("AutoFillSchedule(") && !game.Contains("new ScheduleAutoFiller"),
            "tech staff sources: the seat rule removes no shift and starts no auto fill, so nobody else's week is moved");

        // the option
        Check(settings.Contains("TechSeatHiresId = \"tech_seat_hires\"") && settings.Contains("public static bool TechSeatHires = true;") && settings.Contains("TechSeatHires = Bool(modId, TechSeatHiresId, TechSeatHires);")
              && mod.Contains("AddToggle(Settings.TechSeatHiresId, \"reallife_hire_opt_seat\", Settings.TechSeatHires,")
              && mod.Contains("AddButton(\"reallife_hire_test_seat\", () => Tech.TechDirector.Instance?.TestSeatHires())")
              && tree.Contains(".Opt(\"tech_seat_hires\", \"tech_people\")") && list.Contains("Settings.TechPeopleId, Settings.TechSeatHiresId") && list.Contains("\"reallife_names_test\", \"reallife_hire_test_seat\""),
            "tech staff sources: the option is on by default, has its switch and its test on the options page, stands in the professionals' section needing them, and belongs to their feature");
    }

    // ---------------------------------------------------------------- the texts

    private static void TechStaffTexts()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string[] notices =
        {
            "reallife_hire_note_started_seat", "reallife_hire_note_seated", "reallife_hire_note_seated_one", "reallife_hire_note_seat_closed", "reallife_hire_note_seat_no_desk",
            "reallife_hire_note_seat_full", "reallife_hire_test_seat_pass", "reallife_hire_test_seat_fail", "reallife_hire_test_seat_none"
        };
        string[] others = { "reallife_hire_opt_seat", "reallife_opt_tip_tech_seat_hires", "reallife_hire_test_seat", "reallife_hire_seat_desk_auto" };
        string missing = "none missing";
        foreach (string key in notices)
        {
            missing = !en.TryGetValue(key, out string v) || string.IsNullOrEmpty(v) ? key : missing;
        }
        foreach (string key in others)
        {
            missing = !en.TryGetValue(key, out string v) || string.IsNullOrEmpty(v) ? key : missing;
        }
        Check(missing == "none missing", "tech staff texts: every key is in en.json (" + missing + ")");
        bool fits = true, plain = true;
        foreach (string key in notices)
        {
            string text = en.TryGetValue(key, out string v) ? v : string.Empty;
            fits &= text.Length > 0 && text.Length <= 240;
            plain &= !text.Contains("  ") && !text.Contains("!") && !text.Contains("—") && !text.Contains("–");
        }
        foreach (string key in others)
        {
            string text = en.TryGetValue(key, out string v) ? v : string.Empty;
            plain &= !text.Contains("  ") && !text.Contains("!") && !text.Contains("—") && !text.Contains("–");
        }
        Check(fits, "tech staff texts: every notice is 240 characters or fewer");
        Check(plain, "tech staff texts: no double spaces, exclamation marks or dashes");

        // what the options page holds a label, a sentence and a test button to
        string label = en.TryGetValue("reallife_hire_opt_seat", out string l) ? l : string.Empty;
        string tip = en.TryGetValue("reallife_opt_tip_tech_seat_hires", out string t) ? t : string.Empty;
        string button = en.TryGetValue("reallife_hire_test_seat", out string b) ? b : string.Empty;
        Check(TechStaffWords(label) <= 9 && label.Length <= 60 && !label.EndsWith(".") && TechStaffWords(tip) <= 32 && tip.Length <= 190 && tip.EndsWith(".")
              && TechStaffWords(button) <= 13 && button.Length > 0 && button.Length <= 80 && !button.StartsWith("Test", StringComparison.OrdinalIgnoreCase) && char.IsUpper(button[0]),
            "tech staff texts: the switch's label (" + TechStaffWords(label) + " words), its sentence (" + TechStaffWords(tip) + " words, " + tip.Length + " characters) and the test button keep to the options page's limits");

        // each notice keeps the names the code fills
        bool holes = true;
        string started = en.TryGetValue("reallife_hire_note_started_seat", out string s1) ? s1 : string.Empty;
        string many = en.TryGetValue("reallife_hire_note_seated", out string s2) ? s2 : string.Empty;
        string single = en.TryGetValue("reallife_hire_note_seated_one", out string s3) ? s3 : string.Empty;
        holes &= started.Contains("{name}") && started.Contains("{role}") && started.Contains("{business}");
        holes &= many.Contains("{count}") && many.Contains("{business}") && single.Contains("{name}") && single.Contains("{business}");
        foreach (string key in new[] { "reallife_hire_note_seat_closed", "reallife_hire_note_seat_no_desk", "reallife_hire_note_seat_full" })
        {
            string text = en.TryGetValue(key, out string v) ? v : string.Empty;
            holes &= text.Contains("{count}") && text.Contains("{business}");
        }
        string pass = en.TryGetValue("reallife_hire_test_seat_pass", out string s4) ? s4 : string.Empty;
        string fail = en.TryGetValue("reallife_hire_test_seat_fail", out string s5) ? s5 : string.Empty;
        holes &= pass.Contains("{seated}") && pass.Contains("{count}") && pass.Contains("{left}") && fail.Contains("{short}") && fail.Contains("{count}");
        Check(holes, "tech staff texts: each notice keeps the {names} the code fills");
        Check(ModSource(@"Tech\TechText.cs").Contains("Settings.TechSeatHires ? \"reallife_hire_seat_desk_auto\" : \"reallife_hire_seat_desk\"")
              && en.TryGetValue("reallife_hire_seat_desk", out string byHand) && byHand.Contains("give the hire a shift")
              && en.TryGetValue("reallife_hire_seat_desk_auto", out string byRule) && !byRule.Contains("give the hire a shift") && byRule.Contains("shifts"),
            "tech staff texts: the hire form says the player gives the shift only with the option off, and that one is found for the hire with it on");

        // a language that has the texts keeps their names too (one that lacks them reads them in English until translated, and is not held to them)
        bool translated = true;
        foreach (string code in new[] { "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" })
        {
            Dictionary<string, string> texts = LocaleFile(code);
            foreach (string key in notices)
            {
                if (texts.TryGetValue(key, out string v) && en.TryGetValue(key, out string english))
                {
                    foreach (string hole in new[] { "{name}", "{role}", "{business}", "{count}", "{seated}", "{left}", "{short}" })
                    {
                        translated &= english.Contains(hole) == v.Contains(hole);
                    }
                }
            }
        }
        Check(translated, "tech staff texts: a language that has the notices keeps the same {names} as the English");

        // What's new tells of both fixes, with their fix marks, in some row
        string[] lines =
        {
            "Fixed: programmers and other professionals hired at your tech firms had no role once your save held 600 of them",
            "Fixed: a data centre crew found through a search firm never started once your save held 600 professionals"
        };
        bool every = true, clean = true;
        foreach (string line in lines)
        {
            every &= WhatsNewHas(en, line);
            foreach (KeyValuePair<string, string> row in en)
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains(line))
                {
                    clean &= !row.Value.Contains("  ") && !row.Value.Contains("!") && !row.Value.Contains("—") && !row.Value.Contains("–") && Occurs(row.Value, line) == 1;
                }
            }
        }
        Check(every, "tech staff texts: What's new tells of the two fixes, with their fix marks, in some row of the English");
        Check(clean, "tech staff texts: each fix line is said once in its row, with no double spaces, exclamation marks or dashes");
    }

    private static int TechStaffWords(string text)
    {
        int count = 0;
        bool inWord = false;
        for (int i = 0; text != null && i < text.Length; i++)
        {
            bool letter = !char.IsWhiteSpace(text[i]);
            count += letter && !inWord ? 1 : 0;
            inWord = letter;
        }
        return count;
    }
}
