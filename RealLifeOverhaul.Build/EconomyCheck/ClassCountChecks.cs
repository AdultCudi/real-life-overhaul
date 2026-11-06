using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// A class, homework or exam the player is really sitting is never lost (Scripts/Careers: Study/SittingRules, ClassPlayRules,
// Sitting, CareersDirector). Its end used to count only while it was the director's one current sitting, and the hourly pass
// marked a class gone by whenever that one reference was not its class, so anything that cleared or replaced the reference
// while the class ran (the in-place panel's reset, the let-go, a desk's sitting handed out over another) lost the class, and
// the panel's Attend afterwards found the day settled. Now every sitting has its own record, settled once, kept in a book of
// the sittings open; the minutes at a desk or computer are the game's clock's, a skip's included. First the game-free rules,
// then the source, read as the game's panel cannot be run here, then the school desk's keys while a class is due, then What's new.
internal static partial class Program
{
    private static void ClassCounts()
    {
        ClassCountRules();
        ClassCountClock();
        ClassCountKeys();
        ClassCountSources();
        ClassCountWhatsNew();
    }

    // ---------------------------------------------------------------- the record and the book

    private static void ClassCountRules()
    {
        const int course = StudyCatalogue.Bartending, other = StudyCatalogue.FirstAid;
        var book = new SittingBook();
        var desk = new SittingRecord(SittingKind.Class, course, 180, true);
        book.Hold(desk);
        // The director's reference moves on: an in-place sitting, then homework at a computer, handed out over the class.
        var inPlace = new SittingRecord(SittingKind.Class, other, 120, true);
        var homework = new SittingRecord(SittingKind.Homework, course, 120, true);
        book.Hold(inPlace);
        book.Hold(homework);
        inPlace.Settle(SittingEnd.LetGo);
        book.Release(inPlace);
        Check(book.InClass(course) && !book.InClass(other) && book.ClassOf() == course && book.Holds(desk),
            "class count: a class's sitting stays open in the book while other sittings are handed out and let go over it, so the hourly pass never marks it missed");
        bool first = desk.Begin() && desk.Settle(SittingEnd.Ended);
        Check(first && book.Holds(desk), "class count: its end is counted by its own record while it is in the book, whatever the director's current sitting is");
        Check(!desk.Settle(SittingEnd.Ended) && !desk.Settle(SittingEnd.LetGo) && !desk.Settle(SittingEnd.CalledOff) && desk.End == SittingEnd.Ended,
            "class count: a sitting is settled once: a second end, a let-go or a walk called off after it counts nothing");
        book.Release(desk);
        Check(!book.InClass(course) && book.ClassOf() == -1 && book.Studying && !book.Working && book.Count == 1,
            "class count: once its end is counted it leaves the book; the homework still open keeps the player studying");
        var lateGo = new SittingRecord(SittingKind.Class, course, 60, false);
        Check(lateGo.Settle(SittingEnd.LetGo) && !lateGo.Begin() && !lateGo.Begun, "class count: a sitting let go before it began can never begin afterwards");
        book.Hold(lateGo);
        Check(!book.Holds(lateGo) && !book.InClass(course), "class count: a settled sitting is never taken into the book");
        book.Hold(homework);
        Check(book.Count == 1, "class count: holding a sitting again (its activity made after it, at the computer) keeps one entry");
        var none = new SittingBook();
        Check(!none.InClass(course) && !none.InClass(-1) && none.ClassOf() == -1 && !none.Has(SittingKind.Interview) && none.Count == 0 && !none.Holds(null),
            "class count: an empty book holds no class, no course -1 and nothing at all");
        var day = new SittingRecord(SittingKind.Work, 3, 480, false);
        var interview = new SittingRecord(SittingKind.Interview, 77, 60, false);
        none.Hold(day);
        none.Hold(interview);
        Check(none.Working && none.Has(SittingKind.Interview, 77) && !none.Has(SittingKind.Interview, 78) && !none.Studying,
            "class count: a working day and an interview are known by kind and post in the book too, so the day is never marked missed while worked");
        Check(new SittingRecord(SittingKind.Exam, course, 0, false).Length == 1 && desk.Day == -1, "class count: a sitting of no length is one minute; the day is set as it is handed out");

        // The once-a-second look: kept while its activity is on the game's panel, else that activity is finished, and dropped once settled.
        Check(SittingRules.Look(false, true) == SittingLook.Keep && SittingRules.Look(false, false) == SittingLook.Finish
              && SittingRules.Look(true, true) == SittingLook.Drop && SittingRules.Look(true, false) == SittingLook.Drop,
            "class count: the look keeps a sitting its activity still holds on the panel, finishes one the panel let go, and drops one settled");

        // The share that counts, the one rule the class's end reads.
        bool share = true;
        foreach (int length in new[] { 1, 59, 60, 119, 120, 180, 240 })
        {
            int need = SittingRules.Needed(length);
            share &= SittingRules.Counts(need, length) && !SittingRules.Counts(need - 1, length) && need * 1000 >= length * StudyCatalogue.CountedShare
                     && (need - 1) * 1000 < length * StudyCatalogue.CountedShare;
        }
        Check(share && SittingRules.Counts(240, 240) && !SittingRules.Counts(0, 240) && SittingRules.Needed(0) == 1,
            "class count: a class counts from " + SittingRules.Needed(180) + " of 180 minutes (" + StudyCatalogue.CountedShare + " thousandths), not one fewer, at every length");
    }

    // ---------------------------------------------------------------- the minutes by the clock

    private static void ClassCountClock()
    {
        int need = SittingRules.Needed(180);
        // The game's skip time: the panel's Update performs little or nothing under it, and the skip's end performs its minutes.
        var skipped = new ClassTally(180);
        skipped.Begin(10000);
        skipped.Add(10, 10010);
        bool notYet = skipped.Left == 170;
        TallyEnd end = skipped.Settle(10180, out int minutes, out bool whole);
        Check(notYet && end == TallyEnd.Counted && minutes == 180 && whole && SittingRules.Counts(minutes, 180),
            "class count: a class sat through the game's skip counts the clock's 180 minutes, though the panel performed only 10 of them");
        // The panel performs a skip's minutes as it goes and again at its end: counted once by the clock.
        var twice = new ClassTally(180);
        twice.Begin(20000);
        twice.Add(60, 20060);
        twice.Add(60, 20060);
        twice.Settle(20060, out int sat, out bool all);
        Check(sat == 60 && !all && !SittingRules.Counts(sat, 180),
            "class count: a skip stopped an hour in counts its 60 minutes once, not twice, and the class left early does not count");
        var reached = new ClassTally(120);
        reached.Begin(500);
        bool over = reached.Add(1, 500 + 119) == false && reached.Add(1, 500 + 120);
        Check(over && reached.Done == 120 && reached.Left == 0 && reached.Add(5, 900) && reached.Done == 120,
            "class count: the class is over as the clock reaches its length, and never counts more than its length");
        var early = new ClassTally(180);
        early.Begin(3000);
        early.Settle(3000 + need - 1, out int shortBy, out _);
        var enough = new ClassTally(180);
        enough.Begin(3000);
        enough.Settle(3000 + need, out int justEnough, out _);
        Check(!SittingRules.Counts(shortBy, 180) && SittingRules.Counts(justEnough, 180),
            "class count: a class left by the clock " + (need - 1) + " minutes in does not count and " + need + " minutes in does: only a real leave is too early");
        var back = new ClassTally(90);
        back.Begin(7000);
        back.Add(1, 7040);
        back.Add(1, 7030);
        Check(back.Done == 40, "class count: the clock going back (a shared city's clock set) never takes minutes away");
        var called = new ClassTally(90);
        Check(called.Settle(9999, out int none, out _) == TallyEnd.CalledOff && none == 0 && called.Settle(9999, out _, out _) == TallyEnd.Settled,
            "class count: a walk called off before sitting down counts nothing by the clock either, once");
        var deltas = new ClassTally(90);
        deltas.Begin();
        deltas.Add(30, 999999);
        Check(deltas.Done == 30, "class count: a tally begun without the clock counts what the panel performs, as before");
    }

    // ---------------------------------------------------------------- the school desk's keys

    private static void ClassCountKeys()
    {
        CourseDef bar = StudyCatalogue.Find(StudyCatalogue.Bartending);   // the school, Tuesdays and Fridays at 14
        CourseDef boot = StudyCatalogue.Find(StudyCatalogue.CodingBootcamp);
        CourseDef cs = StudyCatalogue.Find(StudyCatalogue.ComputerScience);
        Check(bar != null && boot != null && cs != null && bar.Campus == Campus.School && bar.Hour == 14 && (bar.Days & StudyCatalogue.Tue) != 0,
            "class keys: the courses the checks below use are where and when they say");
        if (bar == null || boot == null || cs == null)
        {
            return;
        }
        const int tuesday = 2, wednesday = 3;   // day 1 is a Monday
        var e = new Enrolment { Course = bar.Id, Mode = (int)StudyMode.FullTime, Start = 1, Stage = (int)EnrolStage.Studying, SettledDay = 0 };
        var courses = new List<Enrolment> { e };
        int start = 14 * 60;
        Check(ClassPlayRules.ClassOnStart(courses, tuesday, start - StudyCatalogue.ArriveEarlyMinutes) && ClassPlayRules.ClassOnStart(courses, tuesday, start)
              && ClassPlayRules.ClassOnStart(courses, tuesday, start + StudyCatalogue.LateMinutes),
            "class keys: from " + StudyCatalogue.ArriveEarlyMinutes + " minutes before the school's class to " + StudyCatalogue.LateMinutes + " after, its button is on the game's Start key");
        Check(!ClassPlayRules.ClassOnStart(courses, tuesday, start - StudyCatalogue.ArriveEarlyMinutes - 1) && !ClassPlayRules.ClassOnStart(courses, tuesday, start + StudyCatalogue.LateMinutes + 1)
              && !ClassPlayRules.ClassOnStart(courses, wednesday, start),
            "class keys: outside the class's window, and on a day without it, the game's own course keeps the Start key");
        e.SettledDay = tuesday;
        Check(!ClassPlayRules.ClassOnStart(courses, tuesday, start), "class keys: a class attended (or gone by) today gives the Start key back to the game's course");
        e.SettledDay = 0;
        e.Start = wednesday;
        Check(!ClassPlayRules.ClassOnStart(courses, tuesday, start), "class keys: a course begun later has no class due yet");
        e.Start = 1;
        var exam = new Enrolment { Course = bar.Id, Mode = (int)StudyMode.FullTime, Start = 1, Stage = (int)EnrolStage.Exam, ExamFrom = wednesday, ExamTo = wednesday + 6 };
        var exams = new List<Enrolment> { exam };
        bool examDue = StudyRules.ExamDay(exam, wednesday);
        Check(examDue && ClassPlayRules.ClassOnStart(exams, wednesday, StudyCatalogue.ExamOpens * 60) && !ClassPlayRules.ClassOnStart(exams, wednesday, StudyCatalogue.ExamCloses * 60)
              && !ClassPlayRules.ClassOnStart(exams, wednesday, StudyCatalogue.ExamOpens * 60 - 1),
            "class keys: an exam at the school puts its button on the Start key in its hours (" + StudyCatalogue.ExamOpens + " to " + StudyCatalogue.ExamCloses + ") on its days, and not outside them");
        var online = new Enrolment { Course = boot.Id, Mode = (int)StudyMode.FullTime, Start = 1, Stage = (int)EnrolStage.Studying };
        var uni = new Enrolment { Course = cs.Id, Mode = (int)StudyMode.FullTime, Start = 1, Stage = (int)EnrolStage.Studying };
        int bootDay = StudyRules.Next(boot, StudyMode.FullTime, 1), csDay = StudyRules.Next(cs, StudyMode.FullTime, 1);
        Check(!ClassPlayRules.ClassOnStart(new List<Enrolment> { online, uni }, bootDay, boot.Hour * 60) && !ClassPlayRules.ClassOnStart(new List<Enrolment> { online, uni }, csDay, cs.Hour * 60),
            "class keys: an online class or one of the university is no reason to take the school desk's Start key from the game's course");
        Check(!ClassPlayRules.ClassOnStart(null, tuesday, start) && !ClassPlayRules.ClassOnStart(new List<Enrolment>(), tuesday, start)
              && !ClassPlayRules.ClassOnStart(new List<Enrolment> { null, new Enrolment { Course = 9999 } }, tuesday, start),
            "class keys: no courses, an empty list or a course the catalogue lacks leave the panel as the game has it");
        Check(ClassPlayRules.ClassOnStart(courses, tuesday, start) == ClassPlayRules.ClassOnStart(courses, tuesday, start), "class keys: the same answer on every ask");
    }

    // ---------------------------------------------------------------- the source

    private static void ClassCountSources()
    {
        string director = ModSource(@"Careers\CareersDirector.cs"), study = ModSource(@"Careers\CareersDirector.Study.cs");
        string prompt = ModSource(@"Careers\CareersDirector.Prompt.cs"), play = ModSource(@"Careers\CareersDirector.ClassPlay.cs");
        string day = ModSource(@"Careers\CareersDirector.DeskDay.cs"), work = ModSource(@"Careers\CareersDirector.Work.cs");
        string course = ModSource(@"Careers\CareersDirector.Coursework.cs"), sitting = ModSource(@"Careers\Sitting.cs");
        string school = ModSource(@"Careers\SchoolDesk.cs"), computer = ModSource(@"Careers\ComputerSitting.cs"), desk = ModSource(@"Careers\DeskWork.cs");
        Check(director.Length > 0 && study.Length > 0 && prompt.Length > 0 && play.Length > 0 && day.Length > 0 && work.Length > 0 && course.Length > 0
              && sitting.Length > 0 && school.Length > 0 && computer.Length > 0 && desk.Length > 0,
            "class count: the director's parts, the sitting and the desk, computer and work desk activities are read");

        // Its end is settled by its own record, never only while it is the director's current sitting.
        string ended = MethodText(director, "internal void Ended(Sitting sitting, int minutes, bool whole)");
        Check(ended.Contains("bool open = _book.Holds(sitting.Record);") && !ended.Contains("if (!ReferenceEquals(sitting, _sitting))")
              && ended.Contains("StudyEnded(sitting, minutes, whole, current);"),
            "class count: a sitting's end is counted when the director's current sitting changed under it (by the book, not by ReferenceEquals with _sitting)");
        string ends = MethodText(sitting, "public void SkipEnded(int minutes, bool whole)");
        string off = MethodText(sitting, "public void CalledOff()");
        int once = ends.IndexOf("if (!Record.Settle(SittingEnd.Ended))", StringComparison.Ordinal);
        Check(once > 0 && ends.IndexOf("_director.Ended(this, minutes, whole);", once, StringComparison.Ordinal) > once
              && off.Contains("if (!Record.Settle(SittingEnd.CalledOff))") && MethodText(sitting, "internal bool LetGo()").Contains("Record.Settle(SittingEnd.LetGo)"),
            "class count: one settlement a sitting: its end, its calling off and its let-go each pass the record's one gate before the director hears of it");

        // The hourly pass asks the book.
        string hour = MethodText(study, "private void StudyHour(int day, int hour)");
        Check(hour.Contains("e.SettledDay < day && day >= e.Start && !InClass(c.Id)") && study.Contains("private bool InClass(int course) => _book.InClass(course);")
              && study.Contains("internal int SittingClass => _book.ClassOf();") && !study.Contains("_sitting.Kind == SittingKind.Class"),
            "class count: the hourly pass never marks a class missed while a sitting of it is open in the book (a desk, the computer or in place), whatever _sitting holds");
        Check(work.Contains("private bool Working => _book.Working;") && work.Contains("_book.Has(SittingKind.Interview, post)") && !work.Contains("_sitting.Kind"),
            "class count: the working day and the interview are asked of the book too, so a day being worked is never marked missed");

        // Nothing clears or replaces a running sitting: every hand-out is Hold, with its activity.
        int assigned = Occurrences(director + study + prompt + play + day + work + course, "_sitting = sitting;");
        string hold = MethodText(director, "internal void Hold(Sitting sitting, PlayerActivity.IPlayerActivity holder)");
        Check(assigned == 1 && hold.Contains("_sitting = sitting;") && hold.Contains("_book.Hold(sitting.Record);") && hold.Contains("sitting.Record.Holder = holder;"),
            "class count: every sitting handed out goes through Hold, which books it with its activity (" + assigned + " assignment of _sitting)");
        Check(MethodText(director, "internal string Offer(Sitting sitting)").Contains("Hold(sitting, skip);")
              && MethodText(prompt, "internal string DeskSitting(int course, SittingKind kind, IPlayerActivity holder, out Sitting sitting)").Contains("Hold(sitting, holder);")
              && school.Contains("_director.DeskSitting(_offer.Course, _offer.Kind, this, out sitting)")
              && MethodText(play, "private string StartAtComputer(ComputerController computer, Sitting sitting, string what)").Contains("Hold(sitting, activity);")
              && day.Contains("Hold(sitting, holder);") && desk.Contains("_director.DeskBegin(late, this, out Sitting sitting)"),
            "class count: the in-place panel, the school's and the university's desks, the computer at home and the work desk each hand their sitting out with themselves as its activity");
        string slow = MethodText(director, "private void Slow()");
        int look = slow.IndexOf("LookAtSittings();", StringComparison.Ordinal);
        int reset = slow.IndexOf("if (_skip != null && !_skip.Open)", StringComparison.Ordinal);
        string resetBody = reset > 0 ? slow.Substring(reset, Math.Min(200, slow.Length - reset)) : string.Empty;
        Check(look > 0 && reset > look && !resetBody.Contains("_sitting = null;"),
            "class count: the in-place panel's reset in the timer no longer clears whatever sitting is current; the look at the open sittings comes first");
        string tick = MethodText(prompt, "private void DeskTick()");
        Check(!tick.Contains("_sitting = null;") && !tick.Contains("_activityUi.GetCurrentActivity") && tick.Contains("!DeskGame.Shows(_desk)") && tick.Contains("!DeskGame.Shows(_computer)"),
            "class count: the desk watch lets no sitting go by the director's reference, and reads the game's panel afresh, never a cached one");
        string lookAt = MethodText(director, "private void LookAtSittings()");
        int finish = lookAt.IndexOf("holder.Finish();", StringComparison.Ordinal);
        int letGo = lookAt.IndexOf("r.Settle(SittingEnd.LetGo)", StringComparison.Ordinal);
        Check(lookAt.Contains("SittingRules.Look(r.Settled, holder != null && DeskGame.Shows(holder))") && finish > 0 && letGo > finish,
            "class count: a sitting whose activity the panel let go is finished by that activity first (its minutes so far counted once), and only what it leaves unsettled is let go");
        string close = MethodText(director, "private void CloseSitting()");
        Check(close.Contains("_book[i].Settle(SittingEnd.LetGo);") && close.Contains("_book.Release(_book[i]);"),
            "class count: when the director lets every sitting go (switched off, a fault, the city going) their later ends count nothing");

        // The minutes at a desk or the computer are the clock's, a skip's included.
        bool clocked = true;
        foreach (string activity in new[] { school, computer })
        {
            clocked &= activity.Contains("_tally.Begin(CareersGame.Clock);") && activity.Contains("_tally.Add(minutes, CareersGame.Clock)") && activity.Contains("_tally.Settle(CareersGame.Clock, out int minutes, out bool whole)");
        }
        Check(clocked, "class count: at the school's and the university's desks and at the computer the minutes are counted by the game's clock, so a skip's count toward the share");

        // The class's end reads the share's one rule, and says what it came to once a class.
        string ending = MethodText(course, "private void StudyEnded(Sitting s, int minutes, bool whole, bool current)");
        Check(ending.Contains("if (!SittingRules.Counts(minutes, s.Length))") && ending.Contains("StudyRules.Attend(e, on)") && Occurrences(ending, "ClassSettled(") == 2,
            "class count: a class's end counts it from the share that counts (SittingRules.Counts), on the day it was joined, with its self-check line on both ways");
        string line = MethodText(course, "private void ClassSettled(CourseDef c, int day, int minutes, int length, string what, bool current, bool right)");
        string gone = MethodText(study, "private bool GoneBy(Enrolment e, int day)");
        Check(line.Contains("if (!Verbose)") && line.Contains("Careers check (class settled): ") && line.Contains("\"PASS\"") && line.Contains("\"FAIL, ")
              && gone.Contains("Careers check (class settled): ") && gone.Contains("missed, settled by the "),
            "class count: with Developer diagnostics on, one self-check line a class settled: attended, left early or missed, and how it was settled");

        // The school desk's keys: the class's button on the Start key while it is due.
        string keys = MethodText(school, "public ButtonInfo[] GetButtons()");
        Check(keys.Contains("bool first = _offer.Due;") && keys.Contains("first && b != null && b.playerAction == PlayerAction.Confirm")
              && keys.Contains("PlayerAction.SecondaryInteract, b.interactable)") && keys.Contains("first ? PlayerAction.Confirm : PlayerAction.SecondaryInteract, _offer.Can"),
            "class keys: at a school desk, while a class or exam is due, the class's button is on the Start key and the game's course on the secondary key; otherwise as before");
        string now = MethodText(prompt, "private SchoolDesk.DeskOffer DeskOfferNow()");
        Check(now.Contains("offer.Due = offer.Can && ClassPlayRules.ClassOnStart(_study.Courses, Save.Day, CareersGame.MinuteOfDay);")
              && MethodText(play, "private bool TakeBegin(int course, SchoolDesk.DeskOffer offer)").Contains("offer.Due = true;"),
            "class keys: the desk's offer is due by the one rule, and a class begun for the player from the Campus app is on the Start key");
        string own = MethodText(play, "internal string OpenDesk(EducationDoorController door, int room, bool begin)");
        Check(own.Contains("return \"reallife_study_why_no_class\";   // a school desk is opened only to begin: its own click is Going Public's"),
            "class keys: in a Going Public session a school desk's click is still Going Public's own");
    }

    // ---------------------------------------------------------------- What's new

    private static void ClassCountWhatsNew()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("class count", langs, codes))
        {
            return;
        }
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en"] = "Fixed: a class you sit through now always counts, also after skipping time",
            ["pt"] = "Corrigido: uma aula assistida até o fim agora sempre conta",
            ["zh-cn"] = "已修复：坚持上完的课现在总会计入",
            ["de"] = "Behoben: Eine Stunde, die du bis zum Ende besuchst, zählt jetzt immer",
            ["fr"] = "Corrigé : une séance suivie jusqu'au bout compte désormais toujours",
            ["es"] = "Corregido: una clase a la que asistes hasta el final ahora siempre cuenta",
            ["ru"] = "Исправлено: занятие, которое вы досидели до конца, теперь всегда засчитывается",
            ["nl"] = "Opgelost: een les die je volgt telt nu altijd, ook na het versnellen van de tijd",
            ["tr"] = "Düzeltildi: katıldığınız bir ders, zamanı atladıktan sonra da artık her zaman sayılıyor"
        };
        var lacking = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            if (!lines.TryGetValue(codes[l], out string wanted) || !WhatsNewHas(langs[l], wanted))
            {
                lacking.Add(codes[l]);
            }
        }
        Check(lacking.Count == 0 && codes.Length == lines.Count,
            "class count: What's new has the fix's line, with its language's fix mark, in some reallife_changes_ row of every language (lacking: " + string.Join(", ", lacking) + ")");
    }
}
