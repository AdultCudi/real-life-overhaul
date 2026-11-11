using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Tech;

// Programmers hired at IT companies and game studios were not placed in the roles and seats their firms need, and data centre crews never arrived
// (a player's report, 9 Nov 2026, from a save of some six thousand employees). Two causes, both held here. The professionals' files stopped at 600
// people, so a hire after the 600th got no role, was left out of every crew and the Team tab, and a payroll start (a data centre's crew) was
// refused until somebody left: the file is now kept for every professional the game holds, with no bound to be met as a save grows. And no hire was
// ever given a shift, the rota planner refusing a business of over 64 stations or 120 staff: a professional at a tech firm is now seated at a free desk
// with a week of shifts that keeps to their demands (Tech/Firms/SeatPlan, SeatShifts, SeatRound), whenever they were hired and for every way of
// hiring, as the hour's rounds find them, newest first, a business with no room asked once a day for a kind of hire and not for each hire waiting.
// What that work answers for is held too: whether there is a seat never depends on a hire's demands, a business's answer holds for a kind of hire and
// no other (a part-timer is not told what a full week was), a professional the game moves to another business or assigns later is seated there, a
// second hire of a business open round the clock has another desk's day and not the night of the first, the studio's genre experience that stopped at
// 1,200 people, the rounds and the files at a save's scale, the candidate files that dropped a hire's role, and the texts, the Guide's included, that
// promised a seat the pass did not give or denied one it does. The rules, the sources and the texts.
internal static partial class Program
{
    private static void TechStaff()
    {
        TechStaffFiles();
        TechStaffSeatRules();
        TechStaffSeatRandom();
        TechStaffSeatRounds();
        TechStaffPlaces();
        TechStaffDayRules();
        TechStaffStudio();
        TechStaffCandidates();
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
        // a save past the old 600 and past the 10,000 that followed it (a save of a player's own grows by some forty employees a day): written and read
        // whole, the last of them there, whatever their number
        foreach (int people in new[] { 601, 2000, 6100, 10001, 30000 })
        {
            TalentFile file = TechStaffFile(people);
            string line = TalentStore.Write(file);
            TalentFile back = TalentStore.Read(line, out int version);
            Check(back.People.Count == people && back.People[people - 1].Id == "emp_" + (people - 1) && version == TalentStore.Version && TalentStore.Write(back) == line,
                "tech staff: a save of " + people + " professionals is written and read back whole, the last of them there (" + back.People.Count + " read)");
        }

        // the mod's own payroll is the one thing bounded, and only by what the player adds: the game's employees, however many, never fill it
        TalentFile staff = TechStaffFile(20000);
        TalentFile mixed = TechStaffFile(20000);
        mixed.People.Add(new Profile { Id = "pro:1", Name = "Rack Hand" });
        mixed.People.Add(new Profile { Id = "pro:2", Name = "Rack Hand" });
        Check(staff.PayrollCount() == 0 && mixed.PayrollCount() == 2 && TalentStore.PayrollMost > 2 && TalentStore.PayrollMost >= 10000,
            "tech staff: 20,000 of the game's employees on file leave the mod's payroll empty, and its own files are counted alone (" + mixed.PayrollCount() + ")");

        // the rules a read keeps: the first of an id is the one, and a record with no id is no file
        TalentFile dup = TechStaffFile(5);
        dup.People[3].Id = dup.People[1].Id;
        dup.People[4].Id = string.Empty;
        TalentFile kept = TalentStore.Read(TalentStore.Write(dup), out int v2);
        Check(v2 == TalentStore.Version && kept.People.Count == 3 && kept.People[0].Id == "emp_0" && kept.People[1].Id == "emp_1" && kept.People[2].Id == "emp_2",
            "tech staff: a file's id twice leaves the first, and a record with no id is dropped, as before");

        // A read is a pass over the line with a set of the ids, not a search of the files read so far for each (that is a square of the number of files).
        // A limit on the time cannot tell the two apart at ten thousand short ids (the square read takes well under any limit a machine under load allows),
        // so the read is timed at two sizes of ids that share a long stem, where each compare of the square costs most: four times the files is about
        // four times the time for a pass over the line, and sixteen times for the square.
        TalentStore.Read(TalentStore.Write(TechStaffStemmed(2000)), out _);   // (the code compiled and the allocator warm)
        string small = TalentStore.Write(TechStaffStemmed(5000)), large = TalentStore.Write(TechStaffStemmed(20000));
        double fast = TechStaffReadMs(small), slow = TechStaffReadMs(large);
        TalentFile readBack = TalentStore.Read(large, out int v3);
        Check(readBack.People.Count == 20000 && v3 == TalentStore.Version && slow < fast * 9.0 + 10.0,
            "tech staff: a line of 20,000 professionals (" + large.Length + " characters) is read in " + slow.ToString("0") + " ms, against " + fast.ToString("0") + " ms for 5,000: four times the files, under nine times the time (a square would be sixteen)");

        // the people's read finds each employee's file the same way: the index of the files by id, made once for a read, is a pass over them
        var index = new Dictionary<string, Profile>();
        TalentFile fewFiles = TechStaffStemmed(5000), manyFiles = TechStaffStemmed(20000);
        double indexFew = double.MaxValue, indexMany = double.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var timer = Stopwatch.StartNew();
            fewFiles.Index(index);
            indexFew = Math.Min(indexFew, timer.Elapsed.TotalMilliseconds);
            timer.Restart();
            manyFiles.Index(index);
            indexMany = Math.Min(indexMany, timer.Elapsed.TotalMilliseconds);
        }
        Check(index.Count == 20000 && ReferenceEquals(index[manyFiles.People[19999].Id], manyFiles.People[19999]) && indexMany < indexFew * 9.0 + 10.0,
            "tech staff: the index of 20,000 files by id is made in " + indexMany.ToString("0.0") + " ms, against " + indexFew.ToString("0.0") + " ms for 5,000: four times the files, under nine times the time");
    }

    // Files whose ids share a long stem, as a save's guids share their length: every compare of one id with another runs the stem through.
    private static TalentFile TechStaffStemmed(int people)
    {
        string stem = new string('x', 120) + ":";
        var f = new TalentFile { Seed = 11, FirstDay = 1, LastDay = 300, NextPro = 3 };
        for (int i = 0; i < people; i++)
        {
            f.People.Add(new Profile { Id = stem + i, Role = i % TalentCatalogue.RoleCount, Level = i % TalentCatalogue.Levels, Main = 40000 + i, Place = PlaceRules.Key("Main Street", 1 + i % 60), Name = "Staff " + i });
        }
        return f;
    }

    // The fastest of three reads of a line, in milliseconds.
    private static double TechStaffReadMs(string line)
    {
        double best = double.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var watch = Stopwatch.StartNew();
            TalentStore.Read(line, out _);
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }
        return best;
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

    // A demand to be free on a weekday (0 Monday): a week with a shift on it breaks the demand, by the weight given.
    private static SeatJudge TechStaffFreeOn(int day, int weight)
    {
        return (w, n) =>
        {
            for (int i = 0; i < n; i++)
            {
                if (w[i].Day == day)
                {
                    return weight;
                }
            }
            return 0;
        };
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

        // a business open round the clock with several desks: the first hires each have a day (8 to 16) on a desk of their own, the same desk and hours every day, and not the
        // night or the evening of the first desk while another is free for a day; the next, with every day taken, has a night
        SeatSpace clock = TechStaffRoom(3, 0, 6, 0, 24);
        var clockDesks = new int[4];
        var clockStarts = new int[4];
        bool eachDay = true;
        for (int hire = 0; hire < 4; hire++)
        {
            int got = TechStaffSeat(clock, 40, any, week, out int count, out int broke);
            eachDay &= got == SeatPlan.Seated && count == 5 && broke == 0 && TechStaffHours(week, count) == 40;
            clockDesks[hire] = week[0].Station;
            clockStarts[hire] = week[0].From;
            for (int i = 1; i < count; i++)
            {
                eachDay &= week[i].Station == week[0].Station && week[i].From == week[0].From;
            }
        }
        Check(eachDay && clockStarts[0] == 8 && clockStarts[1] == 8 && clockStarts[2] == 8 && clockDesks[0] == 0 && clockDesks[1] == 1 && clockDesks[2] == 2 && clockStarts[3] != 8
              && TechStaffTaken(clock) == 4 * 5 * 8,
            "tech staff, seat rule: at three desks open round the clock the first three hires each have the day (8 to 16) on a desk of their own, desks " + clockDesks[0] + ", " + clockDesks[1] + " and "
            + clockDesks[2] + " from " + clockStarts[0] + ", " + clockStarts[1] + " and " + clockStarts[2] + ", and only the fourth has a night (from " + clockStarts[3] + "), nothing doubled");

        // a seat never depends on the hire's demands: one desk open Monday to Friday from 8 to 16 with Monday and Friday taken. The hire who must be free on Tuesdays cannot have
        // the week that suits the desk best, and is seated on the three days left, breaking the demand by its weight; one with no demands is seated on the same days
        SeatSpace gap = TechStaffRoom(1, 0, 4, 8, 16);
        gap.Take(0, 0, 8, 16);
        gap.Take(0, 4, 8, 16);
        r = TechStaffSeat(gap, 40, TechStaffFreeOn(1, 5), week, out n, out unmet);
        bool gapDays = r == SeatPlan.Seated && n == 3 && unmet == 5 && TechStaffHours(week, n) == 24;
        for (int i = 0; i < n; i++)
        {
            gapDays &= week[i].Day >= 1 && week[i].Day <= 3 && week[i].Station == 0 && week[i].From == 8 && week[i].To == 16;
        }
        SeatSpace gapPlain = TechStaffRoom(1, 0, 4, 8, 16);
        gapPlain.Take(0, 0, 8, 16);
        gapPlain.Take(0, 4, 8, 16);
        int plainResult = SeatPlan.Plan(gapPlain, 40, any, week, out int plainCount, out int plainUnmet);
        Check(gapDays && plainResult == SeatPlan.Seated && plainCount == 3 && plainUnmet == 0,
            "tech staff, seat rule: where the week that keeps a hire's demand has no free desk, the hire is seated on the days that are free, breaking it (" + unmet + "), and is not told that every desk is taken");

        // a part-timer fits where a full week does not: sixteen hours are free at the desk, a week of forty needs twenty of them, a week of twenty-four needs twelve
        SeatSpace few = TechStaffRoom(1, 0, 4, 8, 16);
        for (int d = 0; d < 3; d++)
        {
            few.Take(0, d, 8, 16);
        }
        int fullTime = SeatPlan.Plan(few, 40, any, week, out int fullCount, out int fullUnmet);
        int partResult = TechStaffSeat(few, 24, any, week, out int partCount, out int partUnmet);
        Check(fullTime == SeatPlan.Full && fullCount == 0 && partResult == SeatPlan.Seated && partCount == 2 && TechStaffHours(week, partCount) == 16 && partUnmet == 0,
            "tech staff, seat rule: sixteen free hours seat a hire who wants twenty-four a week and not one who wants forty (" + TechStaffHours(week, partCount) + " hours given)");

        // a seat never depends on the demands, not even where every week planned to keep a demand breaks it once it is placed at a desk: a business open on two days and a demand that only a
        // week at desks breaks. Every week that fits is such a week; the hire is seated all the same, breaking the demand
        SeatSpace twoDays = TechStaffRoom(1, 5, 6, 8, 16);
        SeatJudge atDesks = (w, c) =>
        {
            for (int i = 0; i < c; i++)
            {
                if (w[i].Station >= 0)
                {
                    return 3;
                }
            }
            return 0;
        };
        r = SeatPlan.Plan(twoDays, 24, atDesks, week, out n, out unmet);
        int twoPlain = SeatPlan.Plan(twoDays, 24, any, week, out int twoCount, out int twoUnmet);
        Check(r == SeatPlan.Seated && n == 2 && unmet == 3 && twoPlain == SeatPlan.Seated && twoCount == 2 && twoUnmet == 0,
            "tech staff, seat rule: where every week planned to keep a demand breaks it once placed at a desk, the hire is still seated, breaking it (" + unmet + "), and is not told that every desk is taken");

        // what the hour's pass seats, the one question the start note, the hire form and the pass itself ask: a hire under a skill the tech firm hires under
        // (a lawyer, an HR manager, an account manager at the game's web development agency, and anyone at a firm that is no tech firm, are left to the player)
        int software = (int)FirmKind.Software, games = (int)FirmKind.Games, web = (int)FirmKind.WebAgency, design = (int)FirmKind.Design;
        Check(SeatRules.Seats(software, TalentCatalogue.Programmer) && SeatRules.Seats(software, TalentCatalogue.CustomerService) && SeatRules.Seats(games, TalentCatalogue.GraphicDesigner)
              && SeatRules.Seats(web, TalentCatalogue.Programmer) && SeatRules.Seats(design, TalentCatalogue.GraphicDesigner),
            "tech staff, seat rule: a programmer, a designer or a support hire is seated at a tech firm that hires under the skill");
        Check(!SeatRules.Seats(software, TalentCatalogue.Lawyer) && !SeatRules.Seats(software, TalentCatalogue.HrManager) && !SeatRules.Seats(web, TalentCatalogue.CustomerService)
              && !SeatRules.Seats(design, TalentCatalogue.CustomerService) && !SeatRules.Seats(web, TalentCatalogue.GraphicDesigner) && !SeatRules.Seats(-1, TalentCatalogue.Programmer)
              && !SeatRules.Seats(software, null) && !SeatRules.Seats(software, string.Empty),
            "tech staff, seat rule: a lawyer or an HR manager at a software studio, an account manager at the web agency or the design studio, and anyone at a law firm, headquarters or bank are not seated");
        bool every = true;
        for (int kind = 0; kind < (int)FirmKind.Count; kind++)
        {
            foreach (string skill in SeatRules.PrimaryOf(kind))
            {
                every &= SeatRules.Seats(kind, skill);
            }
        }
        Check(every, "tech staff, seat rule: every skill a kind of tech firm is hired under is seated there");

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
        int seated = 0, other = 0, demandsAsked = 0;
        string fault = string.Empty;
        var week = new SeatShift[SeatSpace.Days];
        var again = new SeatShift[SeatSpace.Days];
        var third = new SeatShift[SeatSpace.Days];
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
            for (int kind = 0; kind < 4; kind++)
            {
                int whatever = SeatPlan.Plan(room, wanted, TechStaffJudge(kind), third, out int thirdCount, out int thirdUnmet);
                if (whatever != result)
                {
                    fault = "round " + round + ": the answer is " + whatever + " for one demand and " + result + " for another";
                    break;
                }
                demandsAsked++;
            }
            if (fault.Length > 0)
            {
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
        Check(fault.Length == 0 && seated >= 20 && other >= 1 && demandsAsked == 4 * 300,
            "tech staff, seat rule, 300 random businesses: every week is inside the open hours, on a free desk the hire may use, one shift a day of 2 to 12 hours, at least half the hours wanted, "
            + "the same twice, and whether there is a seat the same whatever the hire's demand (" + seated + " seated, " + other + " not, " + demandsAsked + " demands asked; "
            + (fault.Length == 0 ? "no fault" : fault) + ")");
    }

    // ---------------------------------------------------------------- the hour's rounds

    // The professionals of a pretend save as the rounds look at them, oldest first: where each is assigned (null: nowhere), their skill, the hours a week they want, whether they
    // hold a shift, what the seating remembers of them (the real SeatMemory, asked the way the game side asks it), and what each business has room for. A business with no room
    // answers that every desk is taken; one with room gives a seat each and loses the room. A business in Leave is one the seating leaves its people at to the player.
    private sealed class SeatFake : ISeatBoard
    {
        public readonly List<string> Places = new List<string>(), Skills = new List<string>();
        public readonly List<bool> Shift = new List<bool>();
        public readonly List<int> Needs = new List<int>();
        public readonly List<SeatMemory> Memory = new List<SeatMemory>();
        public readonly Dictionary<string, int> Room = new Dictionary<string, int>(), Tries = new Dictionary<string, int>(), Given = new Dictionary<string, int>();
        public readonly HashSet<string> Leave = new HashSet<string>();
        public int Begun, Ended, Opened;
        private string _place;

        public void Add(string place, string skill, int hires, bool shift, int need = 40)
        {
            for (int i = 0; i < hires; i++)
            {
                Places.Add(place);
                Skills.Add(skill);
                Shift.Add(shift);
                Needs.Add(need);
                Memory.Add(new SeatMemory());
            }
        }

        // What the game does when the player moves somebody to another business (or to none: null): every shift of theirs is taken and the new address is set.
        public void Move(int i, string place)
        {
            Shift[i] = false;
            Places[i] = place;
        }

        public int Count => Places.Count;

        public bool Open(int i, bool everyone)
        {
            Opened++;
            if (Memory[i].LeftAt(Places[i]) && !Leave.Contains(Places[i]))
            {
                Memory[i].Reopen();   // the business is one the seating seats at now
            }
            return !Memory[i].Settled(Places[i], everyone);
        }

        public string Place(int i) => Places[i];

        public string Skill(int i) => Skills[i];

        public int Need(int i) => Needs[i];

        public bool Idle(int i, out string place)
        {
            place = null;
            if (Leave.Contains(Places[i]))
            {
                Memory[i].Left(Places[i]);
                return false;
            }
            if (Shift[i])
            {
                Memory[i].Seated(Places[i]);
                return false;
            }
            place = Places[i];
            return true;
        }

        public void Begin(string place)
        {
            _place = place;
            Begun++;
        }

        public int Seat(int i)
        {
            Tries[_place] = (Tries.TryGetValue(_place, out int tried) ? tried : 0) + 1;
            if (Room.TryGetValue(_place, out int room) && room > 0)
            {
                Room[_place] = room - 1;
                Shift[i] = true;
                Memory[i].Seated(Places[i]);
                Given[_place] = (Given.TryGetValue(_place, out int given) ? given : 0) + 1;
                return SeatPlan.Seated;
            }
            return SeatPlan.Full;
        }

        public void End(string place)
        {
            Ended++;
        }

        public int TriesAt(string place) => Tries.TryGetValue(place, out int t) ? t : 0;

        public int GivenAt(string place) => Given.TryGetValue(place, out int g) ? g : 0;
    }

    // One business with real desks, the seat rule itself behind the rounds: each hire is planned by SeatPlan.Plan against the desks, with the hours a week they want and the demands
    // they hold, and the hours given are taken. Nothing of the game, nothing copied of the rule.
    private sealed class SeatDesks : ISeatBoard
    {
        private const string Here = "ba:street_desks~9";
        public readonly SeatSpace Space;
        public readonly List<int> Hours = new List<int>(), Broken = new List<int>();
        public readonly List<SeatJudge> Judges = new List<SeatJudge>();
        public readonly List<bool> Shift = new List<bool>();
        public readonly List<SeatMemory> Memory = new List<SeatMemory>();
        public readonly List<SeatShift[]> Weeks = new List<SeatShift[]>();
        private readonly SeatShift[] _week = new SeatShift[SeatSpace.Days];
        public int Asked;

        public SeatDesks(SeatSpace space)
        {
            Space = space;
        }

        public void Add(int hours, SeatJudge judge)
        {
            Hours.Add(hours);
            Judges.Add(judge);
            Shift.Add(false);
            Memory.Add(new SeatMemory());
            Weeks.Add(new SeatShift[0]);
            Broken.Add(0);
        }

        public int Count => Hours.Count;

        public bool Open(int i, bool everyone) => !Memory[i].Settled(Here, everyone);

        public string Place(int i) => Here;

        public string Skill(int i) => TalentCatalogue.Programmer;

        public int Need(int i) => Hours[i];

        public bool Idle(int i, out string place)
        {
            place = Shift[i] ? null : Here;
            if (Shift[i])
            {
                Memory[i].Seated(Here);
            }
            return !Shift[i];
        }

        public void Begin(string place)
        {
        }

        public int Seat(int i)
        {
            Asked++;
            int result = SeatPlan.Plan(Space, Hours[i], Judges[i], _week, out int count, out int unmet);
            if (result == SeatPlan.Seated)
            {
                var kept = new SeatShift[count];
                Array.Copy(_week, kept, count);
                for (int k = 0; k < count; k++)
                {
                    Space.Take(_week[k].Station, _week[k].Day, _week[k].From, _week[k].To);
                }
                Shift[i] = true;
                Memory[i].Seated(Here);
                Weeks[i] = kept;
                Broken[i] = unmet;
            }
            return result;
        }

        public void End(string place)
        {
        }
    }

    // A large firm with hundreds of hires waiting and not a desk free, and a firm beside it with desks, both ways round; a hire the game moves; a business that answers one kind of hire no.
    private static void TechStaffSeatRounds()
    {
        const string A = "ba:street_first~1", B = "ba:street_second~2";
        string skill = TalentCatalogue.Programmer;
        int day = 700;

        foreach (bool aNewer in new[] { true, false })
        {
            var fake = new SeatFake();
            if (aNewer)
            {
                fake.Add(B, skill, 100, false);
                fake.Add(A, skill, 500, false);
            }
            else
            {
                fake.Add(A, skill, 500, false);
                fake.Add(B, skill, 100, false);
            }
            fake.Room[B] = 100;
            var ledger = new SeatLedger();
            int reached = -1;
            for (int hour = 0; hour < 24; hour++)
            {
                SeatRound.Run(fake, ledger, day, SeatPlan.Batch, SeatPlan.Look, false);
                reached = reached < 0 && fake.GivenAt(B) == 100 ? hour + 1 : reached;
            }
            int onDayOne = fake.TriesAt(A);
            SeatRound.Run(fake, ledger, day + 1, SeatPlan.Batch, SeatPlan.Look, false);
            Check(reached == 5 && onDayOne == 1 && fake.GivenAt(B) == 100 && fake.GivenAt(A) == 0 && fake.TriesAt(A) == 2,
                "tech staff, rounds: with 500 hires at a firm with no desk free " + (aNewer ? "newer" : "older") + " than the 100 at one with desks, those 100 are all seated in the fifth hour (" + reached
                + "), and the full firm is asked once a day, not for each hire (" + onDayOne + " on the first, " + fake.TriesAt(A) + " after the second)");
        }

        // the batch is the hires seated: twenty hires of a firm with no room do not use it up
        var batch = new SeatFake();
        batch.Add(B, skill, 30, false);
        batch.Add(A, skill, 600, false);
        batch.Room[B] = 30;
        var led = new SeatLedger();
        SeatTally first = SeatRound.Run(batch, led, day, SeatPlan.Batch, SeatPlan.Look, false);
        SeatTally second = SeatRound.Run(batch, led, day, SeatPlan.Batch, SeatPlan.Look, false);
        Check(first.Seated == SeatPlan.Batch && second.Seated == 10 && batch.TriesAt(A) == 1 && batch.GivenAt(B) == 30,
            "tech staff, rounds: the batch of " + SeatPlan.Batch + " is the hires seated (" + first.Seated + " in the first hour, " + second.Seated + " in the next), though 600 waited at a firm with no room");

        // the test: every one found without a seat is counted, those of a business with no room too, and each business is still asked once
        var test = new SeatFake();
        test.Add(A, skill, 50, false);
        test.Add(B, skill, 10, false);
        test.Room[B] = 10;
        SeatTally all = SeatRound.Run(test, new SeatLedger(), day, SeatPlan.TestMost, int.MaxValue, true);
        Check(all.Seen == 60 && all.Seated == 10 && all.Full == 50 && all.Seen == all.Seated + all.Unseated && test.TriesAt(A) == 1 && test.TriesAt(B) == 10,
            "tech staff, rounds: the test finds 60 without a seat, seats the 10 who can be and counts the 50 of the full firm, asking it once (" + all.Seated + " seated, " + all.Full + " full, " + test.TriesAt(A) + " asked)");

        // a hire is looked at once: those found with a shift are not looked at again, and an hour looks at a thousand at most
        var seated = new SeatFake();
        seated.Add(A, skill, 5000, true);
        var ledgerSeated = new SeatLedger();
        int looks = 0, passes = 0;
        for (; passes < 8; passes++)
        {
            SeatTally t = SeatRound.Run(seated, ledgerSeated, day, SeatPlan.Batch, SeatPlan.Look, false);
            looks += t.Looked;
            if (t.Looked == 0)
            {
                break;
            }
        }
        seated.Opened = 0;
        SeatRound.Run(seated, ledgerSeated, day, SeatPlan.Batch, SeatPlan.Look, false);
        Check(looks == 5000 && passes == 5 && seated.Begun == 0 && seated.Opened == seated.Count,
            "tech staff, rounds: 5,000 professionals with shifts are looked at 1,000 an hour, once each (" + looks + " looks in " + passes + " hours), then an hour costs one cheap question each (" + seated.Opened + ")");

        // many firms with no room, a hire waiting at each: every firm is asked once and the rounds end
        var many = new SeatFake();
        for (int i = 0; i < 300; i++)
        {
            many.Add("ba:street_many~" + i, skill, 1 + i % 3, false);
        }
        SeatTally manyTally = SeatRound.Run(many, new SeatLedger(), day, SeatPlan.Batch, SeatPlan.Look, false);
        int asked = 0;
        foreach (KeyValuePair<string, int> t in many.Tries)
        {
            asked += t.Value;
        }
        Check(manyTally.Seated == 0 && asked == 300 && many.Begun == 300 && many.Ended == 300,
            "tech staff, rounds: 300 firms with no room are each asked once, every one read is let go, and the hour ends (" + asked + " asked)");

        // a different skill at a full firm is its own question: the customer service hires are asked though the programmers were told no
        var skills = new SeatFake();
        skills.Add(A, TalentCatalogue.CustomerService, 5, false);
        skills.Add(A, skill, 5, false);
        skills.Room[A] = 0;
        SeatRound.Run(skills, new SeatLedger(), day, SeatPlan.Batch, SeatPlan.Look, false);
        Check(skills.TriesAt(A) == 2, "tech staff, rounds: a firm with no desk for one skill is asked again for another (" + skills.TriesAt(A) + " asked)");

        // a different number of hours a week is its own question too: a business that has no week of forty for the programmers is still asked for one who wants twenty-four
        var hours = new SeatFake();
        hours.Add(A, skill, 5, false, 24);
        hours.Add(A, skill, 5, false, 40);
        hours.Room[A] = 0;
        SeatRound.Run(hours, new SeatLedger(), day, SeatPlan.Batch, SeatPlan.Look, false);
        Check(hours.TriesAt(A) == 2, "tech staff, rounds: a firm with no week of forty for a programmer is asked again for one who wants twenty-four, once for each (" + hours.TriesAt(A) + " asked)");

        // what is told: once for a business until it seats somebody, and what a business answered today is not today's tomorrow
        var l = new SeatLedger();
        bool toldFirst = l.Tell(A, SeatPlan.Full), toldAgain = l.Tell(A, SeatPlan.Full), toldOther = l.Tell(A, SeatPlan.NoDesk), toldElse = l.Tell(B, SeatPlan.Full);
        l.Seated(A);
        bool toldAfter = l.Tell(A, SeatPlan.Full);
        l.Answer(A, skill, 40, 5, SeatPlan.Full);
        Check(toldFirst && !toldAgain && toldOther && toldElse && toldAfter && l.Answered(A, skill, 40, 5) == SeatPlan.Full && l.Answered(A, skill, 40, 6) == SeatPlan.Seated
              && l.Answered(B, skill, 40, 5) == SeatPlan.Seated && l.Answered(A, TalentCatalogue.CustomerService, 40, 5) == SeatPlan.Seated && l.Answered(A, skill, 24, 5) == SeatPlan.Seated,
            "tech staff, rounds: the player is told once for a business (not for every twenty hires), told again only after it has seated somebody, and its answer holds for the day and one kind of hire, a skill and the hours a week");

        // a professional the game moves is looked at where they are now, whatever the seating knew of them at the first business: seated at one firm and moved to another with desks (the
        // game takes every shift of theirs and sets the new address), left alone at a law firm and moved to a firm, and assigned nowhere and assigned later. The real SeatMemory is what asks.
        const string L = "ba:street_law~3";
        var moved = new SeatFake();
        moved.Leave.Add(L);
        moved.Add(A, skill, 3, false);      // 0, 1 and 2 at A
        moved.Add(L, skill, 1, false);      // 3 at the law firm, which the seating leaves to the player
        moved.Add(null, skill, 1, false);   // 4 assigned to no business
        moved.Room[A] = 3;
        moved.Room[B] = 5;
        var ledgerMoved = new SeatLedger();
        SeatTally hourOne = SeatRound.Run(moved, ledgerMoved, day, SeatPlan.Batch, SeatPlan.Look, false);
        bool seatedAtA = hourOne.Seated == 3 && moved.GivenAt(A) == 3 && moved.Shift[0] && moved.Shift[1] && moved.Shift[2] && !moved.Shift[3] && !moved.Shift[4] && hourOne.Looked == 4;
        moved.Move(1, B);   // the player moves one of them to B with My Employees
        moved.Move(3, B);   // and the one at the law firm
        moved.Move(4, B);   // and assigns the one with no business
        SeatTally hourTwo = SeatRound.Run(moved, ledgerMoved, day, SeatPlan.Batch, SeatPlan.Look, false);
        Check(seatedAtA && hourTwo.Seated == 3 && hourTwo.Looked == 3 && moved.GivenAt(B) == 3 && moved.Shift[1] && moved.Shift[3] && moved.Shift[4] && moved.GivenAt(A) == 3,
            "tech staff, rounds: a professional the game moves to another firm, one left alone at a law firm and moved, and one assigned later are each seated where they are now, and the two still at the first firm are not looked at ("
            + hourTwo.Seated + " seated, " + hourTwo.Looked + " looked at)");

        // while they stay where they are, a shift the player takes away is not given back, and one left alone stays left; the test asks about the first again and not the second
        var stays = new SeatFake();
        stays.Leave.Add(L);
        stays.Add(A, skill, 2, false);
        stays.Add(L, skill, 1, false);
        stays.Room[A] = 5;
        var ledgerStays = new SeatLedger();
        SeatRound.Run(stays, ledgerStays, day, SeatPlan.Batch, SeatPlan.Look, false);
        stays.Shift[0] = false;   // the player takes away their shifts in BizMan, without moving them
        stays.Shift[1] = false;
        SeatTally later = SeatRound.Run(stays, ledgerStays, day + 1, SeatPlan.Batch, SeatPlan.Look, false);
        SeatTally again = SeatRound.Run(stays, ledgerStays, day + 1, SeatPlan.TestMost, int.MaxValue, true);
        Check(later.Seated == 0 && later.Looked == 0 && stays.GivenAt(A) == 4 && again.Seen == 2 && again.Seated == 2 && again.Looked == 2 && !stays.Shift[2],
            "tech staff, rounds: shifts the player takes away from professionals who stay where they are are not given back by the hour, are by the test, and the one left alone at a law firm is left alone by both ("
            + later.Seated + " by the hour, " + again.Seated + " by the test)");

        // one left alone at a business is looked at again when the business has become one the seating seats at, with no move: the same address, a business of another type
        var typed = new SeatFake();
        typed.Leave.Add(L);
        typed.Add(L, skill, 1, false);
        typed.Room[L] = 1;
        var ledgerTyped = new SeatLedger();
        SeatTally untyped = SeatRound.Run(typed, ledgerTyped, day, SeatPlan.Batch, SeatPlan.Look, false);
        SeatTally untypedAgain = SeatRound.Run(typed, ledgerTyped, day, SeatPlan.Batch, SeatPlan.Look, false);
        typed.Leave.Remove(L);   // the player makes it a tech firm
        SeatTally retyped = SeatRound.Run(typed, ledgerTyped, day, SeatPlan.Batch, SeatPlan.Look, false);
        Check(untyped.Seated == 0 && untyped.Looked == 1 && untypedAgain.Looked == 0 && retyped.Seated == 1 && typed.Shift[0] && typed.GivenAt(L) == 1,
            "tech staff, rounds: a professional left alone at a business is not looked at again while it stays one the seating leaves alone, and is seated when it becomes a tech firm ("
            + untypedAgain.Looked + " looks while it stayed, " + retyped.Seated + " seated after)");

        // the rounds and the seat rule together, on one business with real desks, open Monday to Friday from 8 to 16. Monday and Friday are taken; the newer hire must be free on Tuesdays and
        // the older has no demands. The week that suits the newer hire best cannot be placed: they are seated on the three days that are free, breaking the demand, and the older hire is then
        // asked too (and finds no day left), not skipped for the newer hire's answer
        SeatSpace gapSpace = TechStaffRoom(1, 0, 4, 8, 16);
        gapSpace.Take(0, 0, 8, 16);
        gapSpace.Take(0, 4, 8, 16);
        var gap = new SeatDesks(gapSpace);
        gap.Add(40, TechStaffJudge(0));
        gap.Add(40, TechStaffFreeOn(1, 5));
        SeatTally gapDay = SeatRound.Run(gap, new SeatLedger(), day, SeatPlan.Batch, SeatPlan.Look, false);
        Check(gapDay.Seated == 1 && gap.Shift[1] && gap.Broken[1] == 5 && gap.Weeks[1].Length == 3 && !gap.Shift[0] && gap.Asked == 2 && gapDay.Full == 1,
            "tech staff, rounds: with the days that suit a hire's demand taken, the hire who must be free on Tuesdays is seated on the three days that are free, and the older hire is asked and told that every desk is taken, not skipped ("
            + gapDay.Seated + " seated, " + gap.Asked + " asked)");

        // a part-timer behind a full week: sixteen free hours seat the hire who wants twenty-four a week, and the hires who want forty are told no once between them
        SeatSpace fewSpace = TechStaffRoom(1, 0, 4, 8, 16);
        for (int d = 0; d < 3; d++)
        {
            fewSpace.Take(0, d, 8, 16);
        }
        var few = new SeatDesks(fewSpace);
        few.Add(24, TechStaffJudge(0));
        few.Add(40, TechStaffJudge(0));
        few.Add(40, TechStaffJudge(0));
        SeatTally fewDay = SeatRound.Run(few, new SeatLedger(), day, SeatPlan.Batch, SeatPlan.Look, false);
        Check(fewDay.Seated == 1 && few.Shift[0] && !few.Shift[1] && !few.Shift[2] && fewDay.Full == 2 && few.Asked == 2 && few.Weeks[0].Length == 2,
            "tech staff, rounds: the hire who wants twenty-four hours a week is seated at the desk with sixteen free hours behind two who want forty, who are asked once between them ("
            + fewDay.Seated + " seated, " + few.Asked + " asked)");

        // a save's worth of hours: 6,000 professionals, an hour with nobody to seat
        var big = new SeatFake();
        big.Add(A, skill, 6000, true);
        var ledgerBig = new SeatLedger();
        for (int i = 0; i < 6; i++)
        {
            SeatRound.Run(big, ledgerBig, day, SeatPlan.Batch, SeatPlan.Look, false);
        }
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 24; i++)
        {
            SeatRound.Run(big, ledgerBig, day, SeatPlan.Batch, SeatPlan.Look, false);
        }
        double ms = watch.Elapsed.TotalMilliseconds;
        Check(ms < 200.0, "tech staff, rounds: 24 hours with nobody to seat among 6,000 professionals take " + ms.ToString("0.0") + " ms in all (wanted under 200)");
    }

    // ---------------------------------------------------------------- what the day asks of the rest of the mod

    private static void TechStaffPlaces()
    {
        int comfort = 0, team = 0, pull = 0;
        var days = new PlaceDays(place => { comfort++; return place.Length; },
            (string place, int day, out int output, out int burnout) => { team++; output = 0; burnout = day; },
            (place, day) => { pull++; return 1000 + place.Length; });
        long sum = 0;
        for (int i = 0; i < 6000; i++)
        {
            string place = "ba:street_" + i % 40 + "~" + i % 40;
            PlaceDays.Entry e = days.Of(place, 700);
            sum += e.Comfort + e.Pushed;
            if (i % 7 == 0)
            {
                sum += days.PullAt(e, place, 700);
            }
        }
        Check(comfort == 40 && team == 40 && pull == 40 && days.Asked == 120 && sum > 0,
            "tech staff, the day: 6,000 professionals at 40 places ask the rest of the mod 40 times for the comfort, 40 for the team week and 40 for the rivals' pull, not once each (" + comfort + ", " + team + ", " + pull + ")");
        for (int i = 0; i < 6000; i++)
        {
            string place = "ba:street_" + i % 40 + "~" + i % 40;
            days.Of(place, 701);
        }
        Check(comfort == 80 && team == 80 && pull == 40, "tech staff, the day: the next day asks for each place again, and the pull only where somebody can be approached (" + comfort + ", " + team + ", " + pull + ")");

        // an approach is asked about only for those a rival could come for, and the answer is the old one's for them
        var junior = new Profile { Id = "emp_1", Level = TalentCatalogue.Junior };
        var senior = new Profile { Id = "emp_2", Level = TalentCatalogue.Senior };
        var noticed = new Profile { Id = "emp_3", Level = TalentCatalogue.Senior, NoticeEnd = 710 };
        var resting = new Profile { Id = "emp_4", Level = TalentCatalogue.Senior, PoachDay = 750 };
        bool same = true;
        foreach (Profile p in new[] { junior, senior, noticed, resting })
        {
            for (int day = 700; day < 760; day += 3)
            {
                bool approachable = TalentRules.Approachable(p, day);
                same &= approachable == (p.Level >= TalentCatalogue.PoachLevel && p.NoticeEnd <= 0 && p.PoachDay <= day);
                same &= approachable || !TalentRules.Approached(11, p, day, 1000, 0, 1000, 1000, 1000000);
            }
        }
        Check(same && !TalentRules.Approachable(junior, 700) && TalentRules.Approachable(senior, 700) && !TalentRules.Approachable(noticed, 700) && !TalentRules.Approachable(resting, 700)
              && TalentRules.Approachable(resting, 750),
            "tech staff, the day: only a senior or above, not on notice and with no offer resting, can be approached, and nobody else ever is");
    }

    // The rules the day runs for each professional (the director's loop without the game's calls: learning, mood, burnout, sickness, a course's end, a review, a
    // rival's approach), over a save of 6,000 for ten days. The day itself cannot run here (it holds the game's employees and its notifications), so this is
    // the share that can: it is held to half the time the whole day is wanted under, the other half being the game's calls and the reads.
    private static void TechStaffDayRules()
    {
        const int people = 6000, days = 10, seed = 11, year = 365;
        TalentFile file = TechStaffFile(people);
        for (int i = 0; i < people; i++)
        {
            Profile p = file.People[i];
            p.ReviewDay = 700 + i % 40;
            p.Morale = 300 + i % 600;
            p.Burnout = i % 11 * 90;
            p.GameWage = 2000 + i % 500;
            p.Going = 2300;
        }
        var places = new PlaceDays(place => place.Length % 40, (string place, int day, out int output, out int burnout) => { output = 0; burnout = place.Length % 3; }, (place, day) => 1000);
        int reviews = 0, approached = 0, sick = 0, noticed = 0;
        var watch = Stopwatch.StartNew();
        for (int day = 700; day < 700 + days; day++)
        {
            for (int i = 0; i < people; i++)
            {
                Profile p = file.People[i];
                PlaceDays.Entry here = places.Of(p.Place, day);
                int hours = 8;
                TalentRules.Learn(p, hours, i % 5 == 0);
                var d = new WorkDay { Day = day, Year = year, Hours = hours, WeekHours = 40, MainPoints = 50, Mentored = false, Paid = p.GameWage, Going = p.Going, Esteem = 0, OnCall = false,
                                      Wanted = TalentRules.Content(p, seed), Comfort = here.Comfort };
                TalentRules.Mood(p, d);
                p.Burnout = MeetingRules.Day(p.Burnout, hours, here.Pushed);
                p.Sick = false;
                sick += TechRandom.Mille(seed, TechRandom.Of(p.Id), day, 91) < TalentRules.SickChance(p) ? 1 : 0;
                TalentRules.EndCourse(p, seed, day);
                if (p.NoticeEnd == 0 && day >= p.ReviewDay)
                {
                    reviews++;
                    TalentRules.Review(p, seed, day, year, 50);
                    TalentRules.CanPromote(p, day, year, 50);
                }
                if (TalentRules.BurnsOut(seed, p, day))
                {
                    TalentRules.Notice(p, day, year, LeaveKind.BurntOut);
                    noticed++;
                }
                else if (TalentRules.Approachable(p, day) && TalentRules.Approached(seed, p, day, 1000, 0, p.GameWage, p.Going, places.PullAt(here, p.Place, day)))
                {
                    approached++;
                    TalentRules.Offer(p, "Rival", day);
                }
            }
        }
        double ms = watch.Elapsed.TotalMilliseconds, perDay = ms / days;
        int wanted = TalentCatalogue.DayMs(people);
        Console.WriteLine("  tech staff: the rules of a day for " + people + " professionals take " + perDay.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms (" + reviews + " reviews, "
                          + approached + " approaches, " + sick + " sick days, " + noticed + " notices in " + days + " days; the day is wanted under " + wanted + " ms)");
        Check(reviews > 0 && approached > 0 && perDay < wanted / 2.0 && places.Asked <= days * (2 * 60 + 60),   // (sixty places: the bridges are asked of them, not of the professionals)
            "tech staff, the day: the rules of a day for 6,000 professionals take " + perDay.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms, under half of the " + wanted
            + " ms the day is wanted under, and ask the rest of the mod " + places.Asked + " times in " + days + " days");
    }

    // ---------------------------------------------------------------- the game studio's people

    private static double TechStaffLookupMs(int people)
    {
        string stem = new string('x', 120) + ":";
        var file = new StudioFile();
        for (int i = 0; i < people; i++)
        {
            file.AddExperience(new GenreExperience { Id = stem + i });
        }
        double best = double.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var watch = Stopwatch.StartNew();
            int found = 0;
            for (int i = 0; i < people; i++)
            {
                found += file.ExperienceOf(stem + i) != null ? 1 : 0;
            }
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
            if (found != people)
            {
                return double.MaxValue;
            }
        }
        return best;
    }

    // A game studio's programmers beyond the 1,200th kept no experience days (the list stopped there and was never pruned): each gives up to twelve points of craft.
    private static void TechStaffStudio()
    {
        StudioFile f = StudioFileFor(5);
        for (int i = 0; i < 6000; i++)
        {
            for (int d = 0; d < 100 && (i == 0 || i == 1500 || i == 5999); d++)
            {
                StudioRules.Learn(f, "emp_" + i, (int)Genre.Rpg, 8);
            }
            StudioRules.Learn(f, "emp_" + i, (int)Genre.Action, 8);
        }
        int firstDays = StudioRules.ExperienceDays(f, "emp_0", (int)Genre.Rpg), middleDays = StudioRules.ExperienceDays(f, "emp_1500", (int)Genre.Rpg), lastDays = StudioRules.ExperienceDays(f, "emp_5999", (int)Genre.Rpg);
        Check(f.People.Count == 6000 && firstDays == 100 && middleDays == 100 && lastDays == 100 && StudioRules.ExperienceDays(f, "emp_5999", (int)Genre.Action) == 1
              && StudioRules.Craft(50, 1, lastDays) == StudioRules.Craft(50, 1, firstDays) && StudioRules.Craft(50, 1, lastDays) > StudioRules.Craft(50, 1, 0),
            "tech staff, studio: 6,000 professionals who worked on games all have their days in a genre recorded, the last as the first (" + f.People.Count + " recorded, " + lastDays + " days, craft "
            + StudioRules.Craft(50, 1, lastDays) + " against " + StudioRules.Craft(50, 1, 0) + " with none)");

        string line = StudioStore.Write(f);
        StudioFile back = StudioStore.Read(line, out _);
        Check(back.People.Count == 6000 && StudioRules.ExperienceDays(back, "emp_5999", (int)Genre.Rpg) == 100 && StudioStore.Write(back) == line,
            "tech staff, studio: the save reads all 6,000 back with their days (" + back.People.Count + " read)");

        var files = new Dictionary<string, Profile>();
        for (int i = 0; i < 6000; i += 2)
        {
            files["emp_" + i] = new Profile { Id = "emp_" + i };
        }
        int gone = StudioRules.Prune(back, files);
        Check(gone == 3000 && back.People.Count == 3000 && StudioRules.ExperienceDays(back, "emp_1500", (int)Genre.Rpg) == 100 && StudioRules.ExperienceDays(back, "emp_5999", (int)Genre.Rpg) == 0
              && back.People[0].Id == "emp_0" && back.People[1].Id == "emp_2" && StudioRules.Prune(back, files) == 0,
            "tech staff, studio: the experience of 3,000 professionals who have gone is let go with their files, the order kept (" + gone + " let go, " + back.People.Count + " left)");

        var direct = new StudioFile();
        var once = new GenreExperience { Id = "pro:7" };
        direct.People.Add(once);
        var twice = new GenreExperience { Id = "pro:7" };
        direct.AddExperience(twice);
        Check(ReferenceEquals(direct.ExperienceOf("pro:7"), once) && direct.ExperienceOf("pro:8") == null && direct.ExperienceOf(null) == null,
            "tech staff, studio: a list added to from outside is found all the same, and the first of an id stands");

        TechStaffLookupMs(1000);   // (the code compiled and the allocator warm)
        double few = TechStaffLookupMs(2000), many = TechStaffLookupMs(8000);
        Check(many < few * 9.0 + 10.0, "tech staff, studio: finding 8,000 professionals' experience takes " + many.ToString("0") + " ms against " + few.ToString("0")
                                       + " ms for 2,000: four times the people, under nine times the time (a pass over the list for each would be sixteen)");
    }

    // ---------------------------------------------------------------- the files made ahead for hires

    private static void TechStaffCandidates()
    {
        var h = new HiringFile { Seed = 3, FirstDay = 1, LastDay = 10, NextId = 5 };
        for (int i = 0; i < HiringCatalogue.CandidatesMost + 40; i++)
        {
            h.Candidates.Add(new CandidateFile { Id = "cand_" + i, Day = i % 30, P = new Profile { Id = "cand_" + i, Role = i % TalentCatalogue.RoleCount, Level = i % TalentCatalogue.Levels } });
        }
        HiringFile back = HiringStore.Read(HiringStore.Write(h), out _);
        Check(HiringCatalogue.CandidatesMost > 64 && back.Candidates.Count == HiringCatalogue.CandidatesMost && back.Candidates[HiringCatalogue.CandidatesMost - 1].Id == "cand_" + (HiringCatalogue.CandidatesMost - 1)
              && back.Candidates[7].P.Role == 7 % TalentCatalogue.RoleCount,
            "tech staff, hiring: " + HiringCatalogue.CandidatesMost + " files made ahead for candidates are kept and read back, not the 64 of before (" + back.Candidates.Count + " read)");
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

        string foundingSource = ModSource(@"Tech\TechDirector.Founding.cs"), moneySource = ModSource(@"Tech\TechDirector.Money.cs"), ventureSource = ModSource(@"Tech\TechDirector.Venture.cs");
        string teamSource = ModSource(@"Tech\People\RolesApp.Team.cs"), deskSource = ModSource(@"Tech\Firms\SeatGame.cs"), talentGame = ModSource(@"Tech\People\TalentGame.cs");
        string roleForm = ModSource(@"Tech\People\RolesApp.Hiring.cs"), workForm = ModSource(@"Ui\Work\RolesLive.cs"), techText = ModSource(@"Tech\TechText.cs");

        // the files: no bound for the game's employees to meet as a save grows (a bound met is a defect that comes back with the save's growth, and one
        // met in silence at that), only the mod's own payroll bounded, counted alone; a read that is one pass, the shortages asked once for it
        string read = MethodText(people, "private int ReadPeople(int day)");
        Check(!store.Contains("PeopleMost") && !people.Contains("PeopleMost") && !hiring.Contains("PeopleMost") && !foundingSource.Contains("PeopleMost") && !moneySource.Contains("PeopleMost")
              && !ventureSource.Contains("PeopleMost") && !people.Contains("_peopleFullTold") && read.Contains("p = NewFile(e, day);") && read.Contains("ReadShortages();") && !read.Contains("TalentStore."),
            "tech staff sources: no file is refused for want of room, and no bound is left to be met in silence: a professional of the game's always has a file");
        Check(store.Contains("seen.Add(p.Id)") && !store.Contains("f.Of(p.Id)") && store.Contains("HashSet<string>") && !store.Contains("i < PeopleMost") && !store.Contains("i < PayrollMost"),
            "tech staff sources: the people's line is read with a set of the ids, not a search of the files already read for each, and written whole");
        string payroll = MethodText(hiring, "private bool StartPayroll(");
        string join = MethodText(people, "internal int JoinPayroll(");
        Check(payroll.Contains("_talent.PayrollCount() >= TalentStore.PayrollMost") && !payroll.Contains("_talent.People.Count") && join.Contains("payroll + joined < TalentStore.PayrollMost")
              && foundingSource.Contains("_talent.PayrollCount() >= TalentStore.PayrollMost") && moneySource.Contains("_talent.PayrollCount() < TalentStore.PayrollMost")
              && ventureSource.Contains("_talent.PayrollCount() >= TalentStore.PayrollMost"),
            "tech staff sources: a hire on the payroll (a data centre's crew, a co-founder, a startup's chief) is refused only when the mod's own payroll is full, counted without the game's employees");

        // the hour: the people are read, a hire the game's own hiring made is found, who is on a shift is asked of a set read once for the business and not of the
        // game's scan of the day's shifts for each of thousands, and the professionals without a seat are seated, whichever way they were hired
        string hour = MethodText(people, "private void PeopleHour()");
        string onShift = MethodText(people, "private bool OnShiftNow(EmployeeInstance e)");
        Check(hour.Contains("TalentGame.Employees(_employees)") && hour.Contains("ReadPeople(day);") && hour.Contains("SeatHour(day);")
              && CodeAt(hour, "ReadPeople(day);") >= 0 && CodeAt(hour, "SeatHour(day);") > CodeAt(hour, "ReadPeople(day);"),
            "tech staff sources: each hour a hire the game's recruitment office, job board or headhunter made, or the mod's own jobs board or search firm, is read as a new professional and then seated");
        Check(hour.Contains("OnShiftNow(e)") && !hour.Contains("TalentGame.Working(") && hour.Contains("ReleaseCrews();") && hour.Contains("HourTimed(day, started);") && onShift.Contains("TalentGame.WorkingNow(reg, crew);")
              && onShift.Contains("crew.Contains(e.id)") && onShift.Contains("TalentGame.Available(e)") && talentGame.Contains("public static void WorkingNow(") && talentGame.Contains("TimeHelper.GetDayOfWeek(save.Day)")
              && talentGame.Contains("e.IsEmployeeAvailable()") && !talentGame.Contains("IsWorking()"),
            "tech staff sources: who is on a shift this hour is asked of a set read once for the business, as the game's IsWorking finds it, and the hour's time is written to the log when over its share");
        string starts = MethodText(hiring, "private int Starts(int day)");
        Check(starts.Contains("seating = SeatsNew(candidate, at);") && starts.Contains("seating ? \"reallife_hire_note_started_seat\" : \"reallife_hire_note_started\"")
              && starts.Contains("HiringGame.Arrived(candidate, at, s.P.Name, day)"),
            "tech staff sources: a hire the mod's jobs board starts is noted as being seated, so its start note says a desk follows");
        string file = MethodText(hiring, "private void File(string candidateId, Profile from, int day, int work, bool hire = false)");
        Check(file.Contains("HiringCatalogue.CandidatesMost") && file.Contains("if (!hire)") && file.Contains("_hiring.Candidates.RemoveAt(0);") && !file.Contains(">= 64") && starts.Contains("ad.Work, true);"),
            "tech staff sources: the file that carries the role a hire was made for is always kept: where the office's waiting candidates fill the table, the oldest of them gives way");

        // the day: what is asked of the rest of the mod is asked once for a place and not for each professional, an approach's lookups only for those a rival could come for,
        // the notices beyond the first few of a kind told together, the studio's experience of those who have gone let go, and the time written as PASS or FAIL
        string day = MethodText(people, "private void PeopleDay(int day)");
        Check(day.Contains("_places.Of(p.Place, day)") && !day.Contains("TechBridges.ComfortAt(") && !day.Contains("TechBridges.TeamWeekAt(") && !day.Contains("Shortage(") && !day.Contains("StudioPull(") && !day.Contains("SectorPull(")
              && day.Contains("_shortageRead[(int)p.Def.Family]") && day.Contains("TalentRules.Approachable(p, day)") && day.Contains("_places.PullAt(here, p.Place, day)")
              && CodeAt(day, "TalentRules.Approachable(p, day)") >= 0 && CodeAt(day, "_places.PullAt(here, p.Place, day)") > CodeAt(day, "TalentRules.Approachable(p, day)"),
            "tech staff sources: the day asks the comfort and the team week of a place once, the market's shortage once for the read, and the rivals' pull only for one who can be approached");
        Check(day.Contains("reviews <= TalentCatalogue.NoticesMost") && day.Contains("approached <= TalentCatalogue.NoticesMost") && day.Contains("reallife_talent_note_reviews_many") && day.Contains("reallife_talent_note_poach_many")
              && day.Contains("StudioRules.Prune(_studio, _fileOf)") && day.Contains("TalentCatalogue.DayMs(_people.Count)") && day.Contains("ms <= wanted ? \"PASS\" : \"FAIL\""),
            "tech staff sources: only the first " + TalentCatalogue.NoticesMost + " reviews and offers of a day are told by name and the rest together, the studio's experience of those who left is let go, and the day's time is written as PASS or FAIL against its share");
        Check(ModSource(@"Tech\TechDirector.cs").Contains("_places = new PlaceDays(TechBridges.ComfortAt, TechBridges.TeamWeekAt, PullOfPlace);")
              && people.Contains("private int PullOfPlace(string place, int day) => Math.Max(StudioPull(place, day), SectorPull(place, day));"),
            "tech staff sources: the place's cache asks the same bridges the day asked for each professional");

        // the Team tab: whether a professional has a desk and a shift is asked of the business's set of who has one, read once for a page, and its desks counted once
        Check(teamSource.Contains("TalentGame.StationHolders(person.At, read.Holders)") && teamSource.Contains("read.Desks.For(person.Game)") && !deskSource.Contains("DesksFor(")
              && !teamSource.Contains("TalentGame.StationShift(") && !deskSource.Contains("Array.Exists(") && deskSource.Contains("_lists.IndexOf(item.suitableSkills)") && talentGame.Contains("public static void StationHolders("),
            "tech staff sources: the Team tab reads a business's shifts and desks once for a page and asks them of each row, with no delegate made for a desk of a professional, and the pass over a business's items for one professional is gone");

        // the game studio's people have no bound either, and the experience of those who left is let go
        string catalogue = ModSource(@"Tech\Studio\StudioCatalogue.cs"), rules = ModSource(@"Tech\Studio\StudioRules.cs"), studioStore = ModSource(@"Tech\Studio\StudioStore.cs");
        Check(!catalogue.Contains("PeopleStored") && !rules.Contains("PeopleStored") && !studioStore.Contains("PeopleStored") && rules.Contains("f.AddExperience(e);") && studioStore.Contains("f.AddExperience(e);")
              && rules.Contains("public static int Prune(StudioFile f, Dictionary<string, Profile> files)"),
            "tech staff sources: the studio keeps every professional's experience of a genre, however many, and lets go that of those who have gone");

        // the seating part
        string hourPass = MethodText(seating, "private void SeatHour(int day)");
        string pass = MethodText(seating, "private SeatReport SeatPass(int day, bool all)");
        string test = MethodText(seating, "public void TestSeatHires()");
        Check(hourPass.Contains("Settings.TechSeatHires") && hourPass.Contains("_seatBroken") && hourPass.Contains("catch (Exception e)") && hourPass.Contains("SeatPass(day, false)"),
            "tech staff sources: the hour's seating is behind its option and its own fault guard, which switches seating off and leaves the sector running");
        Check(pass.Contains("SeatRound.Run(board, _seatLedger, day, all ? SeatPlan.TestMost : SeatPlan.Batch, all ? int.MaxValue : SeatPlan.Look, all)") && !seating.Contains("Newcomer") && !seating.Contains("dayHired")
              && !seating.Contains("_seatTried") && !seating.Contains("_seatHad") && !seating.Contains("SeatPlan.Window") && !ModSource(@"Tech\Firms\SeatPlan.cs").Contains("Newcomer")
              && seating.Contains("TalentGame.StationShift(e, at)") && seating.Contains("TalentGame.Business(e)") && seating.Contains("e.IsTraining") && seating.Contains("holders.Contains(e.id)")
              && seating.Contains("TalentGame.StationHolders(at, set)") && seating.Contains("SeatShifts.Begin(_reg);") && seating.Contains("SeatShifts.End();") && seating.Contains("_d._seatLedger.Tell(place, reason)")
              && pass.Contains("finally"),
            "tech staff sources: the pass seats from the newest back whenever they were hired, takes its batch as hires seated, asks a business with no room once a day, tells the player once for it, and reads each business's shifts once");
        // what the pass remembers of a professional is asked of the address the game has on the employee now, kept for that business and no other (SeatMemory, held above by the
        // rounds); the people's last read, which a move does not make again, is not what the hour looks at, and the file carries no flag for it
        string openText = TechStaffNested(seating, "public bool Open(int i, bool everyone)");
        string idleText = TechStaffNested(seating, "public bool Idle(int i, out string place)");
        string seatText = TechStaffNested(seating, "public int Seat(int i)");
        string needText = TechStaffNested(seating, "public int Need(int i)");
        Check(openText.Contains("person.Remembered.Settled(e.assignedAddress, everyone)") && !openText.Contains("person.At") && !openText.Contains(".Place")
              && openText.Contains("person.Remembered.LeftAt(e.assignedAddress) && SeatsAt(TalentGame.Business(e), person.Skill)") && openText.Contains("person.Remembered.Reopen();")
              && Occurs(idleText, "person.Remembered.Seated(e.assignedAddress)") == 2 && idleText.Contains("person.Remembered.Left(e.assignedAddress)")
              && seatText.Contains("person.Remembered.Seated(e.assignedAddress)") && seatText.Contains("person.SeatNeed = week;") && needText.Contains("SeatShifts.WeekHoursFor(person.Game)")
              && seating.Contains("public string Place(int i) => PlaceOf(_d._people[i]);") && seating.Contains("object.Equals(e.assignedAddress, person.Assigned)")
              && !seating.Contains("SeatKnown") && !seating.Contains("SeatNoAt") && !ModSource(@"Tech\People\TalentModel.cs").Contains("SeatKnown") && people.Contains("person.Assigned = e.assignedAddress;"),
            "tech staff sources: the hour asks the address the game has on each professional now, not the last read of the people, and what it remembers of them holds for that business only");
        Check(game.Contains("public static int WeekHoursFor(EmployeeInstance e)") && Occurs(game, "return HoursOf(least, most);") == 2 && Occurs(game, "BoundHours(hoursWanted, ref least, ref most);") == 2
              && Occurs(game, "DemandOf(") == 3 && game.Contains("week = ReadDemands(e);"),
            "tech staff sources: the hours a week a hire's week is planned for and the hours the rounds remember a business's answer by are read from the demands by one rule");
        Check(seating.Contains("SeatRules.Seats(FirmsGame.KindOf(TalentGame.TypeOf(at)), skill)") && seating.Contains("SeatsHire(at, TalentGame.Skill(e))") && seating.Contains("SeatsAt(at, person.Skill)")
              && !seating.Contains("Array.IndexOf(SeatRules.PrimaryOf(kind)") && techText.Contains("director.SeatsHire(place, role.GameSkill)")
              && Occurs(roleForm, "TechText.SeatDesk(_places[_adPlace], r)") == 2 && workForm.Contains("TechText.SeatDesk(_places[place], r)") && !roleForm.Contains("TechText.SeatDesk()") && !workForm.Contains("TechText.SeatDesk()"),
            "tech staff sources: the start note, the hire form (the ad, the search, the work form) and the pass ask one question, SeatsHire, so what they promise at a place for a role is what the pass does");
        Check(seating.Contains("GoingPublic.Mine(at)") && people.Contains("TalentGame.Employees(_employees);") && CodeAt(seating, "SeatShifts.Begin(_reg);") > CodeAt(seating, "GoingPublic.Mine(at)"),
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
            "reallife_hire_note_seat_full", "reallife_hire_test_seat_pass", "reallife_hire_test_seat_fail", "reallife_hire_test_seat_none",
            "reallife_talent_note_reviews_many", "reallife_talent_note_poach_many"
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
        string reviews = en.TryGetValue("reallife_talent_note_reviews_many", out string s6) ? s6 : string.Empty;
        string offers = en.TryGetValue("reallife_talent_note_poach_many", out string s7) ? s7 : string.Empty;
        holes &= reviews.Contains("{count}") && offers.Contains("{count}") && offers.Contains("{days}") && reviews.Contains("Team tab") && offers.Contains("Team tab");
        Check(holes, "tech staff texts: each notice keeps the {names} the code fills");
        Check(ModSource(@"Tech\TechText.cs").Contains("director.SeatsHire(place, role.GameSkill) ? \"reallife_hire_seat_desk_auto\" : \"reallife_hire_seat_desk\"")
              && en.TryGetValue("reallife_hire_seat_desk", out string byHand) && byHand.Contains("give the hire a shift")
              && en.TryGetValue("reallife_hire_seat_desk_auto", out string byRule) && !byRule.Contains("give the hire a shift") && byRule.Contains("shifts"),
            "tech staff texts: the hire form says a free desk and shifts are found for the hire only where the pass seats a hire of that role at that place, and that the player gives the shift where it does not");

        // a language that has the texts keeps their names too (one that lacks them reads them in English until translated, and is not held to them)
        bool translated = true;
        foreach (string code in new[] { "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" })
        {
            Dictionary<string, string> texts = LocaleFile(code);
            foreach (string key in notices)
            {
                if (texts.TryGetValue(key, out string v) && en.TryGetValue(key, out string english))
                {
                    foreach (string hole in new[] { "{name}", "{role}", "{business}", "{count}", "{seated}", "{left}", "{short}", "{days}" })
                    {
                        translated &= english.Contains(hole) == v.Contains(hole);
                    }
                }
            }
        }
        Check(translated, "tech staff texts: a language that has the notices keeps the same {names} as the English");

        // What's new tells of the fix in one sentence with its fix mark, in some row, and no row still holds the three sentences it was first told in
        const string told = "Fixed: programmers and other professionals hired at your tech firms are now given a role, a free desk and a week of shifts automatically, in a firm of any size, "
                            + "and a data centre crew found through a search firm now starts, however many professionals your save holds.";
        bool clean = true, once = false, old = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (!row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal))
            {
                continue;
            }
            old |= row.Value.Contains("had no role once your save held 600 of them") || row.Value.Contains("never started once your save held 600 professionals")
                   || row.Value.Contains("tech staff hired before the update who still have no shift");
            if (row.Value.Contains(told))
            {
                once = Occurs(row.Value, told) == 1;
                clean &= !row.Value.Contains("  ") && !row.Value.Contains("!") && !row.Value.Contains("—") && !row.Value.Contains("–");
            }
        }
        Check(WhatsNewHas(en, told) && once && !old, "tech staff texts: What's new tells of the work in one sentence with its fix mark, said once, in some row of the English");
        Check(clean, "tech staff texts: the row that tells of it has no double spaces, exclamation marks or dashes");

        // the Guide's three pages say that new tech hires are seated automatically and name the option as the options page spells it, and none still says that nobody works until the
        // player gives a shift by hand
        bool guided = true;
        foreach (string key in new[] { "reallife_guide_tech_sector", "reallife_guide_tech_people", "reallife_guide_tech_jobs" })
        {
            guided &= en.TryGetValue(key, out string page) && Occurs(page, "<b>" + label + "</b>") == 1 && page.Contains("seated automatically");
        }
        string sector = en.TryGetValue("reallife_guide_tech_sector", out string sectorPage) ? sectorPage : string.Empty;
        Check(guided && label.Length > 0 && sector.Contains("nobody works without a shift") && !sector.Contains("nobody works until they have a shift"),
            "tech staff texts: the Guide's tech sector, people and jobs pages each say that a tech hire is seated automatically and name the option \"" + label + "\", and the sector page no longer says that nobody works until a shift is given by hand");
    }

    // The text of a method of a class nested in the director (its closing brace on a line of its own, indented twelve spaces), from its signature; empty if it is not there.
    private static string TechStaffNested(string source, string signature)
    {
        int at = source.IndexOf(signature, StringComparison.Ordinal);
        int end = at < 0 ? -1 : source.IndexOf("\n            }", at, StringComparison.Ordinal);
        return end < 0 ? string.Empty : source.Substring(at, end - at);
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
