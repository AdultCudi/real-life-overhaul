using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// A classroom door and the Campus app decide what can begin by one rule (Scripts/Careers: Study/ClassPlayRules.AtDesk, read
// through CareersDirector.DeskStartOf by the university's classroom doors, the school's desks and the app's Attend and Sit).
// The game's activity panel writes a button's key alone (PlayerActivityUI.SetButton reads neither its arguments nor whether it
// may be pressed), so a desk's button names its sitting with no {placeholder}, and a desk that cannot begin anything shows no
// button at all: its lines say why, and when the next class is and from when it can be begun, every value filled. First the
// rule, then the door against the app minute by minute for every course, then the source, read as the game's panel cannot be
// run here, then the texts and What's new.
internal static partial class Program
{
    private static void DeskStarts()
    {
        DeskStartRule();
        DeskStartAgree();
        DeskStartSources();
        DeskStartTexts();
    }

    private const string DsNoClass = "reallife_study_why_no_class", DsNoExam = "reallife_study_why_no_exam", DsFeeOwed = "reallife_study_why_fee_owed";
    private const string DsTired = "reallife_study_why_tired", DsBusy = "reallife_study_why_busy", DsNotUniversity = "reallife_study_why_not_university";

    // ---------------------------------------------------------------- the rule

    private static void DeskStartRule()
    {
        DeskStart cls = ClassPlayRules.AtDesk(null, DsNoExam, false, null, false);
        Check(cls.On && cls.Can && cls.Kind == SittingKind.Class && cls.Why == null, "desk start: a class in its window is begun at its door (the class, no reason)");
        DeskStart exam = ClassPlayRules.AtDesk(DsNoClass, null, false, null, false);
        Check(exam.On && exam.Can && exam.Kind == SittingKind.Exam && exam.Why == null, "desk start: on an exam day in its hours the door begins the exam");
        DeskStart both = ClassPlayRules.AtDesk(null, null, false, null, false);
        Check(both.Kind == SittingKind.Class && both.Can, "desk start: the class comes before the exam when both could be begun");
        DeskStart none = ClassPlayRules.AtDesk(DsNoClass, DsNoExam, false, null, false);
        Check(!none.On && !none.Can && none.Why == DsNoClass, "desk start: with no class or exam on, the door says there is no class to join now");
        DeskStart hours = ClassPlayRules.AtDesk(DsNoClass, "reallife_study_why_exam_hours", false, null, false);
        Check(!hours.Can && hours.Why == "reallife_study_why_exam_hours", "desk start: an exam day outside its hours says the exam's hours");
        DeskStart owed = ClassPlayRules.AtDesk(DsFeeOwed, DsNoExam, true, null, false);
        Check(!owed.On && !owed.Can && owed.Why == DsFeeOwed, "desk start: an instalment owed says to pay it first");
        DeskStart tired = ClassPlayRules.AtDesk(null, DsNoExam, false, DsTired, false);
        Check(tired.On && !tired.Can && tired.Why == DsTired, "desk start: too tired while the class is on: it is on, cannot be begun, and the door says rest first");
        DeskStart under = ClassPlayRules.AtDesk(null, DsNoExam, false, DsBusy, false);
        DeskStart idle = ClassPlayRules.AtDesk(DsNoClass, DsNoExam, false, DsBusy, false);
        Check(!under.Can && under.Why == DsBusy && !idle.On && idle.Why == DsNoClass,
            "desk start: a sitting under way stops a class that is on, and is no reason given while nothing is on");
        DeskStart there = ClassPlayRules.AtDesk(DsNotUniversity, DsNoExam, false, null, false);
        DeskStart walked = ClassPlayRules.AtDesk(DsNotUniversity, DsNoExam, false, null, true);
        Check(!there.Can && there.Why == DsNotUniversity && walked.Can && walked.Kind == SittingKind.Class,
            "desk start: away from the classroom only the Campus app's press, which walks the player there, excuses being elsewhere");
        Check(!ClassPlayRules.AtDesk(DsNoClass, DsNoExam, false, null, true).Can && !ClassPlayRules.AtDesk(DsFeeOwed, DsNoExam, true, null, true).Can,
            "desk start: from the app too, no class on and an instalment owed are reasons");
        Check(ClassPlayRules.BeginWhy(cls, SittingKind.Class, null) == null && ClassPlayRules.BeginWhy(tired, SittingKind.Class, null) == DsTired
              && ClassPlayRules.BeginWhy(exam, SittingKind.Class, DsNoClass) == DsNoClass && ClassPlayRules.BeginWhy(none, SittingKind.Exam, DsNoExam) == DsNoExam
              && ClassPlayRules.BeginWhy(none, SittingKind.Class, null) == DsNoClass,
            "desk start: a press begins its kind only when the rule offers that kind, else says that kind's own reason");
        DeskStart again = ClassPlayRules.AtDesk(null, DsNoExam, false, DsTired, false);
        Check(again.Kind == tired.Kind && again.On == tired.On && again.Can == tired.Can && again.Why == tired.Why, "desk start: the same answer on every ask");
    }

    // ---------------------------------------------------------------- the door against the app, minute by minute

    // A class's reason at its own classroom, as CanAttend gives it there: the place is right, so only the course and the hour count.
    private static string DsClassWhy(Enrolment e, int day, int minute)
    {
        CourseDef c = e.Def;
        if (c == null || e.Stage != (int)EnrolStage.Studying || !c.Timetabled)
        {
            return DsNoClass;
        }
        if (e.FeeLate)
        {
            return DsFeeOwed;
        }
        return e.SettledDay < day && StudyRules.Joinable(c, (StudyMode)e.Mode, day, minute, out _) ? null : DsNoClass;
    }

    private static void DeskStartAgree()
    {
        int courses = 0, cases = 0, apart = 0, wrongWindow = 0, wrongSpan = 0;
        string first = null;
        string[] seats = { null, DsTired };
        bool[] owing = { false, true };
        foreach (CourseDef c in StudyCatalogue.All)
        {
            if (!c.Timetabled || c.Campus == Campus.Online)
            {
                continue;
            }
            courses++;
            int day = StudyRules.Next(c, StudyMode.FullTime, 1);
            int start = c.Hour * 60;
            var e = new Enrolment { Course = c.Id, Mode = (int)StudyMode.FullTime, Start = 1, Stage = (int)EnrolStage.Studying, SettledDay = 0 };
            for (int m = start - 120; m <= start + c.Hours * 60; m++)
            {
                foreach (bool late in owing)
                {
                    foreach (string seat in seats)
                    {
                        e.FeeLate = late;
                        string why = DsClassWhy(e, day, m);
                        DeskStart door = ClassPlayRules.AtDesk(why, DsNoExam, late, seat, false);
                        bool app = ClassPlayRules.BeginWhy(ClassPlayRules.AtDesk(why, DsNoExam, late, seat, true), SittingKind.Class, why) == null;
                        cases++;
                        if (app != (door.Can && door.Kind == SittingKind.Class))
                        {
                            apart++;
                            first = first ?? c.Key + " at minute " + m;
                        }
                        bool open = m >= start - StudyCatalogue.ArriveEarlyMinutes && m <= start + StudyCatalogue.LateMinutes && !late && seat == null;
                        wrongWindow += door.Can == open ? 0 : 1;
                    }
                }
            }
            e.FeeLate = false;
            int next = StudyRules.NextClass(e, day, start - 120, -1);
            ClassPlayRules.BeginSpan(next, out int from, out int until);
            bool window = StudyRules.JoinWindow(c, StudyMode.FullTime, day, out int wFrom, out int wTo);
            wrongSpan += next == day * 24 * 60 + start && window && from == wFrom && until == wTo ? 0 : 1;
        }
        Check(courses > 10 && apart == 0,
            "desk start: for every course held in person (" + courses + "), at every minute around its class, owed or not, tired or not,"
            + " the door offers the class exactly when the Campus app's Attend begins it ("
            + cases + " cases, " + apart + " apart" + (first != null ? ", first " + first : string.Empty) + ")");
        Check(wrongWindow == 0, "desk start: the door begins a class from " + StudyCatalogue.ArriveEarlyMinutes + " minutes before it starts to " + StudyCatalogue.LateMinutes
                                + " after, never owed or too tired (" + wrongWindow + " minutes otherwise)");
        Check(wrongSpan == 0, "desk start: the door's next class line gives the class's start and the span it can be begun in, the join window's own (" + wrongSpan + " courses otherwise)");

        // The report: law, Mondays at 10, its door clicked at 09:22 and the Campus app pressed after 09:30 on day 8 (a Monday).
        CourseDef law = StudyCatalogue.Find(StudyCatalogue.Law);
        var reported = new Enrolment { Course = StudyCatalogue.Law, Mode = (int)StudyMode.FullTime, Start = 8, Stage = (int)EnrolStage.Studying, SettledDay = 0 };
        DeskStart at922 = ClassPlayRules.AtDesk(DsClassWhy(reported, 8, 9 * 60 + 22), DsNoExam, false, null, false);
        DeskStart at930 = ClassPlayRules.AtDesk(DsClassWhy(reported, 8, 9 * 60 + 30), DsNoExam, false, null, false);
        int lawNext = StudyRules.NextClass(reported, 8, 9 * 60 + 22, -1);
        ClassPlayRules.BeginSpan(lawNext, out int lawFrom, out int lawUntil);
        Check(law != null && law.Hour == 10 && !at922.Can && at922.Why == DsNoClass && at930.Can && lawNext == 8 * 24 * 60 + 600 && lawFrom % 1440 == 570 && lawUntil % 1440 == 660,
            "desk start: law's door at 09:22 on its Monday says no class yet, its next class at 10:00 and that it can be begun there from 09:30 to 11:00; at 09:30 it offers the class");
    }

    // ---------------------------------------------------------------- the source

    private static void DeskStartSources()
    {
        string play = ModSource(@"Careers\CareersDirector.ClassPlay.cs"), floors = ModSource(@"Careers\CareersDirector.Floors.cs");
        string prompt = ModSource(@"Careers\CareersDirector.Prompt.cs"), school = ModSource(@"Careers\SchoolDesk.cs");
        Check(play.Length > 0 && floors.Length > 0 && prompt.Length > 0 && school.Length > 0, "desk start: the director's desks, floors and watch and the desk are read");

        string of = MethodText(play, "private DeskStart DeskStartOf(Enrolment e, bool school, bool away, bool game)");
        Check(of.Contains("ClassPlayRules.AtDesk(classWhy, examWhy, e.FeeLate, SeatWhy(game), away && !school)") && of.Contains("CanAttend(e, out _)") && of.Contains("CanSit(e)"),
            "desk start: the director's one decision reads the rule with the course's own reasons and the player's seat");
        string room = MethodText(floors, "private SchoolDesk.DeskOffer RoomOffer(int room)");
        string desk = MethodText(prompt, "private SchoolDesk.DeskOffer DeskOfferOf()");
        string toRoom = MethodText(floors, "private string ToRoom(Enrolment e, string why, SittingKind kind)");
        string toSchool = MethodText(play, "private string ToSchoolDesk(Enrolment e, SittingKind kind)");
        Check(room.Contains("DeskStartOf(e, false, false, false)") && desk.Contains("DeskStartOf(e, true, false, false)")
              && toRoom.Contains("ClassPlayRules.BeginWhy(DeskStartOf(e, false, true, true), kind, why)") && toSchool.Contains("ClassPlayRules.BeginWhy(DeskStartOf(e, true, false, true), kind, null)")
              && !toRoom.Contains("reallife_study_why_not_university") && !room.Contains("CanAttend(") && !desk.Contains("CanAttend("),
            "desk start: the classroom door, the school's desk and the Campus app's Attend and Sit (to the classroom and to the school's desk) all decide by the one decision, none by a copy");
        string mark = MethodText(floors, "internal RoomMark RoomMarkOf(int course)");
        Check(mark.Contains("can = DeskStartOf(e, false, false, false).On;") && !mark.Contains("CanAttend("),
            "desk start: the mark over a classroom door says a class or exam is on by the door's own decision");
        string take = MethodText(play, "private bool TakeBegin(int course, SchoolDesk.DeskOffer offer)");
        Check(take.Length > 0 && !take.Contains("offer.Can = true;") && take.Contains("if (!offer.Can || offer.Kind != kind)") && take.Contains("offer.Due = true;"),
            "desk start: a sitting begun for the player from the Campus app is begun only when the desk's own offer says it can be, never forced");
        Check(!school.Contains("Arguments") && !floors.Contains("offer.Arguments") && !prompt.Contains("Arguments =") && !play.Contains("offer.Arguments"),
            "desk start: no desk's button carries arguments, which the game's activity panel never reads");
        string buttons = MethodText(school, "public ButtonInfo[] GetButtons()");
        Check(buttons.Contains("_buttons = _offer.Can") && buttons.Contains(": new[] { cancel };") && buttons.Contains("new ButtonInfo[n + (_offer.Can ? 1 : 0)]")
              && buttons.Contains("if (_offer.Can)"),
            "desk start: a desk that cannot begin anything shows no button of its own, as the game's own course panel once its course is done (the game's panel never dims one)");
        string labels = MethodText(school, "private LabelInfo[] WithLines(LabelInfo[] game)");
        Check(labels.Contains("ReferenceEquals(game, _gameLabels)") && MethodText(school, "public LabelInfo[] GetLabels()").Contains("return WithLines(_inner.GetLabels());"),
            "desk start: at the school's desk the class's lines follow the game's own course's, made again only when the game gives new ones");
        string keys = MethodText(play, "private static string StartKey(SittingKind kind)");
        string fill = MethodText(play, "private void FillOffer(SchoolDesk.DeskOffer offer, Enrolment e, DeskStart start, string first)");
        string next = MethodText(play, "private string NextLine(Enrolment e)");
        Check(keys.Contains("\"reallife_campus_button_sit\"") && keys.Contains("\"reallife_campus_button_homework\"") && keys.Contains("\"reallife_campus_button_attend\"")
              && fill.Contains("offer.Key = start.Can ? StartKey(start.Kind) : null;") && fill.Contains("string next = start.On ? null : NextLine(e);")
              && next.Contains("ClassPlayRules.BeginSpan(at, out int from, out int until);") && next.Contains("CareersText.Fill(\"reallife_study_desk_next_from\""),
            "desk start: the button's words are the sitting's own, and while nothing is on the lines say when the next class is and from when it can be begun");
        string line = MethodText(floors, "private void DoorAndApp(Enrolment e, DeskStart door, int floor, int room)");
        Check(room.Contains("DoorAndApp(e, start, floor, room);") && line.Contains("if (!Verbose)") && line.Contains("DeskStartOf(e, false, true, false)")
              && line.Contains("Careers check (door and app): ") && line.Contains("\"PASS\"") && line.Contains("\"FAIL, "),
            "desk start: with Developer diagnostics on, one self-check line a classroom door: what it offers against what the Campus app would begin from the same place");
        string watch = MethodText(prompt, "private void OnLateUpdate()");
        Check(watch.Contains("offer.Course >= 0 && TakeBegin(offer.Course, offer)") && watch.Contains("\", nothing to begin now (\" + offer.Why + \").\""),
            "desk start: a free classroom begins nothing sent from the app, and the door's line names the reason");
    }

    // ---------------------------------------------------------------- the texts

    private static void DeskStartTexts()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("desk start", langs, codes))
        {
            return;
        }
        bool plain = true;
        foreach (string key in new[] { "reallife_campus_button_attend", "reallife_campus_button_sit", "reallife_campus_button_homework" })
        {
            for (int l = 0; l < langs.Length; l++)
            {
                plain &= langs[l].TryGetValue(key, out string words) && words.Length > 0 && words.IndexOf('{') < 0;
            }
        }
        Check(plain, "desk start: every desk button's words, in every language, have no {placeholder} the game's panel would leave unfilled");
        Check(langs[0].TryGetValue("reallife_study_desk_next_from", out string nextFrom) && NamesOf(nextFrom) == "{day},{from},{hour},{until}"
              && LanguagesLacking(langs, codes, "reallife_study_desk_next_from").Length == 0,
            "desk start: the next class line is in every language with its day, hour, from and until");
        var lacking = new List<string>();
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            if (own == null || !own.ContainsKey("reallife_study_desk_next_from"))
            {
                lacking.Add(code);
            }
        }
        Check(lacking.Count == 0, "desk start: the next class line is written in en, pt and zh-cn themselves (lacking: " + string.Join(", ", lacking) + ")");
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en"] = "Fixed: a classroom door at the university and a desk at the school offer the class or exam whenever the Campus app could begin it",
            ["pt"] = "Corrigido: a porta de uma sala da universidade e uma mesa da escola oferecem a aula ou a prova sempre que o app Campus poderia começá-la",
            ["zh-cn"] = "已修复：只要校园应用能开始课程或考试，大学教室的门和学校的课桌就会提供它",
            ["de"] = "Behoben: Die Tür eines Hörsaals der Universität und ein Tisch der Schule bieten die Stunde oder Prüfung an",
            ["fr"] = "Corrigé : la porte d'une salle de l'université et un bureau de l'école proposent la séance ou l'examen",
            ["es"] = "Corregido: la puerta de un aula de la universidad y un pupitre de la escuela ofrecen la clase o el examen",
            ["ru"] = "Исправлено: дверь аудитории университета и парта школы предлагают занятие или экзамен",
            ["nl"] = "Opgelost: de deur van een lokaal op de universiteit en een tafel op de school bieden de les of het examen aan",
            ["tr"] = "Düzeltildi: üniversitedeki bir dersliğin kapısı ve okuldaki bir sıra"
        };
        var missing = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            if (!lines.TryGetValue(codes[l], out string wanted) || !WhatsNewHas(langs[l], wanted))
            {
                missing.Add(codes[l]);
            }
        }
        Check(missing.Count == 0 && codes.Length == lines.Count,
            "desk start: What's new has the fix's line, with its language's fix mark, in some reallife_changes_ row of every language (lacking: " + string.Join(", ", missing) + ")");
    }
}
