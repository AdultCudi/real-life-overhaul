using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// Classes played as the game's own studying is (Scripts/Careers/Study/ClassPlayRules): what the computer at home offers
// beside the game's Pass Time for which enrolment at which minute (the class button to attend now, greyed with the next
// class, or none; the homework button), and the minutes of a class, homework or exam at a desk or the computer counted
// once whichever way it ends (ClassTally): the end of the time, Stop, a skip, the panel letting it go, a walk called off.
internal static partial class Program
{
    private static void ClassPlay()
    {
        CourseDef boot = StudyCatalogue.Find(StudyCatalogue.CodingBootcamp);      // online, from home
        CourseDef market = StudyCatalogue.Find(StudyCatalogue.DigitalMarketing);  // online, other days
        CourseDef fork = StudyCatalogue.Find(StudyCatalogue.Forklift);            // the school, in person
        CourseDef cs = StudyCatalogue.Find(StudyCatalogue.ComputerScience);       // the university
        CourseDef books = StudyCatalogue.Find(StudyCatalogue.Bookkeeping);        // the school, with homework
        Check(boot != null && market != null && fork != null && cs != null && books != null && books.Homework > 0 && boot.Campus == Campus.Online && market.Campus == Campus.Online && fork.Campus == Campus.School,
            "class play: the courses the checks below use are where they say");
        if (boot == null || market == null || fork == null || cs == null || books == null)
        {
            return;
        }
        int bootStart = StudyRules.Hour(boot, StudyMode.FullTime) * 60;
        int day = 8;   // a Monday (NextClassChecks)
        while (!StudyRules.On(StudyRules.Days(boot, StudyMode.FullTime), day))
        {
            day++;
        }

        // ---------------------------------------------------------------- held online
        Check(ClassPlayRules.HeldOnline(boot, StudyMode.FullTime) && ClassPlayRules.HeldOnline(cs, StudyMode.Distance) && !ClassPlayRules.HeldOnline(cs, StudyMode.FullTime)
              && !ClassPlayRules.HeldOnline(fork, StudyMode.FullTime) && !ClassPlayRules.HeldOnline(null, StudyMode.Distance),
            "class play: an online course, and any course followed at a distance, is held at the computer at home; one in person is not; no course is none");

        // ---------------------------------------------------------------- what the computer offers
        Check(!ClassPlayRules.AtComputer(new List<Enrolment>(), day, bootStart, -1).Any && !ClassPlayRules.AtComputer(null, day, bootStart, -1).Any,
            "class play: with no course the computer has only the game's own buttons");

        var online = new Enrolment { Course = boot.Id, Mode = (int)StudyMode.FullTime, Start = 1, SettledDay = day - 1, Stage = (int)EnrolStage.Studying };
        var list = new List<Enrolment> { online };
        ComputerOffer now = ClassPlayRules.AtComputer(list, day, bootStart, -1);
        Check(now.Class == boot.Id && now.ClassNow && now.NextAt == -1 && now.Homework == -1,
            "class play: at the class's hour the computer shows Attend for the online class, and no homework while none is set");
        ComputerOffer early = ClassPlayRules.AtComputer(list, day, bootStart - StudyCatalogue.ArriveEarlyMinutes, -1);
        ComputerOffer tooEarly = ClassPlayRules.AtComputer(list, day, bootStart - StudyCatalogue.ArriveEarlyMinutes - 1, -1);
        Check(early.ClassNow && !tooEarly.ClassNow && tooEarly.Class == boot.Id && tooEarly.NextAt == day * 1440 + bootStart,
            "class play: Attend from the first minute the class can be joined; a minute before, greyed with today's class");
        ComputerOffer lateIn = ClassPlayRules.AtComputer(list, day, bootStart + StudyCatalogue.LateMinutes, -1);
        ComputerOffer gone = ClassPlayRules.AtComputer(list, day, bootStart + StudyCatalogue.LateMinutes + 1, -1);
        Check(lateIn.ClassNow && !gone.ClassNow && gone.Class == boot.Id && gone.NextAt == StudyRules.NextClass(online, day, bootStart + StudyCatalogue.LateMinutes + 1, -1) && gone.NextAt > day * 1440 + bootStart,
            "class play: Attend until the last minute it can be joined; after, greyed with the next class the Campus app names");
        bool sameMinutes = ClassPlayRules.ClassNow(online, day, bootStart + 30, out int left) && StudyRules.Joinable(boot, StudyMode.FullTime, day, bootStart + 30, out int joinable) && left == joinable;
        Check(sameMinutes, "class play: a class attended from the computer runs the minutes the class still has, as from the Campus app");

        var owed = new Enrolment { Course = boot.Id, Mode = (int)StudyMode.FullTime, Start = 1, SettledDay = day - 1, Stage = (int)EnrolStage.Studying, FeeLate = true };
        var settled = new Enrolment { Course = boot.Id, Mode = (int)StudyMode.FullTime, Start = 1, SettledDay = day, Stage = (int)EnrolStage.Studying };
        ComputerOffer owedOffer = ClassPlayRules.AtComputer(new List<Enrolment> { owed }, day, bootStart, -1);
        ComputerOffer settledOffer = ClassPlayRules.AtComputer(new List<Enrolment> { settled }, day, bootStart, -1);
        Check(!owedOffer.ClassNow && owedOffer.Class == boot.Id && !settledOffer.ClassNow && settledOffer.Class == boot.Id && settledOffer.NextAt > day * 1440 + bootStart,
            "class play: with a fee owed, or today's class already counted, the class button is greyed, never Attend");
        ComputerOffer sat = ClassPlayRules.AtComputer(list, day, bootStart + 10, boot.Id);
        Check(!sat.ClassNow, "class play: the class being sat is not offered again");

        var school = new Enrolment { Course = fork.Id, Mode = (int)StudyMode.FullTime, Start = 1, SettledDay = 0, Stage = (int)EnrolStage.Studying };
        int forkStart = StudyRules.Hour(fork, StudyMode.FullTime) * 60;
        int forkDay = 8;
        while (!StudyRules.On(StudyRules.Days(fork, StudyMode.FullTime), forkDay))
        {
            forkDay++;
        }
        ComputerOffer inPerson = ClassPlayRules.AtComputer(new List<Enrolment> { school }, forkDay, forkStart, -1);
        Check(inPerson.Class == -1 && !inPerson.ClassNow, "class play: a class held in person (the school) never has a button at the computer, even at its hour");

        var distance = new Enrolment { Course = cs.Id, Mode = (int)StudyMode.Distance, Start = 1, SettledDay = 0, Stage = (int)EnrolStage.Studying };
        ComputerOffer far = ClassPlayRules.AtComputer(new List<Enrolment> { distance }, day, 0, -1);
        Check(far.Class == cs.Id && far.NextAt == StudyRules.NextClass(distance, day, 0, -1),
            "class play: a university course followed at a distance has its class button at the computer");

        // Two online courses: the one on now wins; else the one whose next class is soonest. In the order of the courses.
        var other = new Enrolment { Course = market.Id, Mode = (int)StudyMode.FullTime, Start = 1, SettledDay = day - 1, Stage = (int)EnrolStage.Studying };
        ComputerOffer both = ClassPlayRules.AtComputer(new List<Enrolment> { other, online }, day, bootStart, -1);
        Check(both.Class == boot.Id && both.ClassNow, "class play: of two online courses the one whose class is on now has the button");
        int otherNext = StudyRules.NextClass(other, day, 0, -1), bootNext = StudyRules.NextClass(online, day, 0, -1);
        ComputerOffer soonest = ClassPlayRules.AtComputer(new List<Enrolment> { other, online }, day, 0, -1);
        Check(soonest.Class == (otherNext >= 0 && (bootNext < 0 || otherNext < bootNext) ? market.Id : boot.Id) && soonest.NextAt == Math.Min(otherNext < 0 ? int.MaxValue : otherNext, bootNext < 0 ? int.MaxValue : bootNext),
            "class play: with none on now, the class button is greyed with the soonest next class");

        // Homework: open on the first course that has it, wherever its classes are; none when it is all done.
        var homework = new Enrolment { Course = books.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = books.Sessions / 2, Done = books.Sessions / 2, SettledDay = 3, Stage = (int)EnrolStage.Studying, Homework = 0 };
        bool open = StudyRules.HomeworkOpen(homework);
        ComputerOffer withHomework = ClassPlayRules.AtComputer(new List<Enrolment> { homework, online }, day, bootStart, -1);
        Check(open && withHomework.Homework == books.Id && withHomework.Class == boot.Id && withHomework.ClassNow,
            "class play: homework set and not done has its button at the computer (any course's), beside the online class's Attend");
        var allDone = new Enrolment { Course = books.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = books.Sessions / 2, Done = books.Sessions / 2, SettledDay = 3, Stage = (int)EnrolStage.Studying, Homework = books.Homework };
        Check(!StudyRules.HomeworkOpen(allDone) && ClassPlayRules.AtComputer(new List<Enrolment> { allDone }, day, bootStart, -1).Homework == -1,
            "class play: with every piece set done, no homework button");

        bool same = true;
        for (int m = 0; m < 24 * 60; m += 7)
        {
            ComputerOffer a = ClassPlayRules.AtComputer(new List<Enrolment> { other, online, homework }, day, m, -1);
            ComputerOffer b = ClassPlayRules.AtComputer(new List<Enrolment> { other, online, homework }, day, m, -1);
            same &= a.Class == b.Class && a.ClassNow == b.ClassNow && a.NextAt == b.NextAt && a.Homework == b.Homework
                    && (!a.ClassNow || a.NextAt == -1) && (a.ClassNow || a.Class < 0 || a.NextAt >= day * 1440 + m - StudyCatalogue.LateMinutes);
        }
        Check(same, "class play: the same courses at the same minute always give the same buttons; Attend has no next time, a greyed button one that is still to come");

        // ---------------------------------------------------------------- the minutes, counted once
        var off = new ClassTally(90);
        Check(!off.Add(30) && off.Done == 0 && off.Settle(out int m0, out bool w0) == TallyEnd.CalledOff && m0 == 0 && !w0 && off.Settle(out _, out _) == TallyEnd.Settled,
            "class play: a walk called off before sitting down counts nothing, once, and nothing more after");
        var whole = new ClassTally(90);
        whole.Begin();
        bool first = whole.Add(60), second = whole.Add(45);
        Check(!first && second && whole.Done == 90 && whole.Left == 0 && whole.Settle(out int m1, out bool w1) == TallyEnd.Counted && m1 == 90 && w1,
            "class play: fast forward or skip time past the end counts the class's minutes and no more, as a whole class");
        Check(whole.Settle(out int m2, out _) == TallyEnd.Settled && m2 == 0 && !whole.Add(30) && whole.Done == 90,
            "class play: a second end (the panel's Finish after the skip's) counts nothing again, and no minute is added after the end");
        var stop = new ClassTally(120);
        stop.Begin();
        stop.Add(40);
        bool leftRight = stop.Left == 80;
        TallyEnd stopped = stop.Settle(out int m3, out bool w3);
        Check(leftRight && stopped == TallyEnd.Counted && m3 == 40 && !w3,
            "class play: Stop, energy running out or the panel letting it go part way counts the minutes sat, not the whole");
        // The director counts a class sat for at least CountedShare of its length (CareersDirector.StudyEnded): the tally's
        // minutes and length are what it is given, so the class's last counted minute is the same as before.
        int need = (120 * StudyCatalogue.CountedShare + 999) / 1000;
        var justEnough = new ClassTally(120);
        justEnough.Begin();
        justEnough.Add(need);
        var justShort = new ClassTally(120);
        justShort.Begin();
        justShort.Add(need - 1);
        justEnough.Settle(out int enough, out _);
        justShort.Settle(out int shortBy, out _);
        Check(!(enough * 1000 < justEnough.Length * StudyCatalogue.CountedShare) && shortBy * 1000 < justShort.Length * StudyCatalogue.CountedShare && m3 * 1000 < stop.Length * StudyCatalogue.CountedShare,
            "class play: a class stopped early counts if " + need + " of its 120 minutes were sat and not if one fewer, as a partial class is judged today (StudyCatalogue.CountedShare)");
        var zero = new ClassTally(0);
        zero.Begin();
        Check(zero.Length == 1 && !zero.Add(0) && !zero.Add(-5) && zero.Add(1) && zero.Settle(out int m4, out bool w4) == TallyEnd.Counted && m4 == 1 && w4,
            "class play: a sitting of no length is one minute; no minutes or minutes taken away add nothing");
        var late = new ClassTally(60);
        late.Settle(out _, out _);
        late.Begin();
        Check(!late.Begun && !late.Add(60) && late.Done == 0, "class play: a sitting settled before it began cannot be begun afterwards");

        // ---------------------------------------------------------------- the texts
        var words = LanguageWords(out string[] langs);
        if (!LanguagesRead("class play", words, langs))
        {
            return;
        }
        var keys = new (string Key, string[] Names)[]
        {
            ("reallife_study_prompt_homework", new[] { "{course}" }), ("reallife_study_prompt_attend", new[] { "{course}" }),
            ("reallife_study_desk_next", new[] { "{course}", "{day}", "{hour}" }), ("reallife_study_why_no_computer", new string[0]),
            ("reallife_study_why_no_computer_homework", new string[0]), ("reallife_study_note_class_computer", new[] { "{course}" }),
            ("reallife_study_why_no_internet", new string[0])
        };
        var wrong = new List<string>();
        for (int k = 0; k < keys.Length; k++)
        {
            for (int l = 0; l < langs.Length; l++)
            {
                if (!words[l].TryGetValue(keys[k].Key, out string w) || string.IsNullOrWhiteSpace(w))
                {
                    wrong.Add(langs[l] + " " + keys[k].Key + " (missing)");
                    continue;
                }
                foreach (string name in keys[k].Names)
                {
                    if (!w.Contains(name))
                    {
                        wrong.Add(langs[l] + " " + keys[k].Key + " (no " + name + ")");
                    }
                }
            }
        }
        Check(wrong.Count == 0, "class play: the computer's buttons, its refusals and the class's notice are in every language with their names (" + string.Join(", ", wrong) + ")");
        Check(words[0].TryGetValue("reallife_study_why_no_computer", out string why) && why == "You need a computer at home for an online class.",
            "class play: a home with no computer says so, in the owner's words");
    }
}
