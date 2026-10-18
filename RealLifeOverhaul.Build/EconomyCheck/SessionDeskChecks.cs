using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// The school's and the university's desks in a Going Public session (Scripts/Careers: ClassPlayRules, CareersDirector.OpenDesk,
// SchoolDesk.Own). Going Public's prefix on PlayerActivityUI.Show never shows a StudyActivity in a session: it puts up its
// own training window for the game's course ("Requires the MarketDemands course first"). So, first, the game-free rules of
// which way is taken in and out of a session, and where the panel in place offers a class: alone exactly as before, in a
// session the desk's own panel. Then, read from the source as the game's panel cannot be run here: in a session no class,
// exam or homework of the mod's hands a StudyActivity to the game's panel. The game's door is asked for its study panel in
// one place, behind the rule; every desk path goes through it; the desk's own activity is not a StudyActivity and is what
// the panel is given; a classroom door's click is the mod's only in a session; each desk's panel opened says its self-check.
// Then What's new's line in every language.
internal static partial class Program
{
    private static void SessionDesks()
    {
        SessionDeskRules();
        SessionDeskSources();
        SessionDeskWhatsNew();
    }

    // ---------------------------------------------------------------- the rules

    private static void SessionDeskRules()
    {
        Check(ClassPlayRules.WayToDesk(false) == DeskWay.GameStudy && ClassPlayRules.WayToDesk(true) == DeskWay.OwnPanel,
            "session desks: the game's study panel alone, the desk's own panel in a Going Public session (WayToDesk)");
        Check(ClassPlayRules.WayToDesk(true) == ClassPlayRules.WayToDesk(true) && ClassPlayRules.WayToDesk(false) == ClassPlayRules.WayToDesk(false),
            "session desks: the way is the same on every ask, on host and guest alike (both are InSession)");
        bool rooms = !ClassPlayRules.TakesRoomClick(true, 0) && !ClassPlayRules.TakesRoomClick(false, 0);
        for (int room = 1; room <= CampusRules.RoomsPerFloor; room++)
        {
            rooms &= ClassPlayRules.TakesRoomClick(true, room) && !ClassPlayRules.TakesRoomClick(false, room);
        }
        Check(rooms, "session desks: a click on a classroom door of the university is the mod's in a session only, and on none but its rooms 1 to " + CampusRules.RoomsPerFloor);
        Check(!ClassPlayRules.SchoolInPlace(false) && ClassPlayRules.SchoolInPlace(true),
            "session desks: the school's classes and exams at its desks alone, at the panel where the player stands in a session (its desks' click is Going Public's)");

        // Alone, the panel in place offers exactly what it offered before: skipped when (class at the school or online) and
        // exam at the school, or in a classroom; the class where neither the school's desks, the computer nor a room hold it.
        bool same = true;
        int combos = 0;
        foreach (bool a in new[] { false, true })
        {
            foreach (bool o in new[] { false, true })
            {
                foreach (bool x in new[] { false, true })
                {
                    foreach (bool r in new[] { false, true })
                    {
                        if (a && (o || !x))
                        {
                            continue;   // (a class held at the school is never online, and its exam is at the school too)
                        }
                        combos++;
                        ClassPlayRules.InPlace(a, o, x, r, false, out bool classHere, out bool examHere);
                        bool skippedBefore = (a || o) && x || r;
                        same &= (!classHere && !examHere) == skippedBefore;
                        same &= skippedBefore || (classHere == (!a && !o)) && (examHere == !x);
                    }
                }
            }
        }
        Check(same && combos == 10, "session desks: alone, the panel in place offers what it did before the session's way, in all " + combos + " cases");

        // Real courses, as the director reads them (AtTheSchool: a class at the school in person; its exam at the school, or from home).
        CourseDef fork = StudyCatalogue.Find(StudyCatalogue.Forklift);         // the school, in person
        CourseDef boot = StudyCatalogue.Find(StudyCatalogue.CodingBootcamp);   // online, from home
        CourseDef cs = StudyCatalogue.Find(StudyCatalogue.ComputerScience);    // the university
        Check(fork != null && boot != null && cs != null && fork.Campus == Campus.School && boot.Campus == Campus.Online && cs.Campus == Campus.University,
            "session desks: the courses the checks below use are where they say");
        if (fork == null || boot == null || cs == null)
        {
            return;
        }
        Check(Here(fork, StudyMode.FullTime, false, false) == "none" && Here(fork, StudyMode.FullTime, false, true) == "class and exam",
            "session desks: a course of the school is at its desks alone, and offered where the player stands in a session");
        Check(Here(boot, StudyMode.FullTime, false, false) == "none" && Here(boot, StudyMode.FullTime, false, true) == "exam",
            "session desks: an online course's class stays at the computer at home; its exam, at the school, is offered in place in a session");
        Check(Here(cs, StudyMode.FullTime, true, false) == "none" && Here(cs, StudyMode.FullTime, true, true) == "none",
            "session desks: a course of the university in its classroom is at its door in and out of a session, never in place");
        Check(Here(cs, StudyMode.FullTime, false, false) == "class and exam" && Here(cs, StudyMode.FullTime, false, true) == "class and exam",
            "session desks: a course of the university without its floors is offered in place, as before, in and out of a session");
        Check(Here(cs, StudyMode.Distance, false, false) == "none" && Here(cs, StudyMode.Distance, false, true) == "exam",
            "session desks: a course followed at a distance has its classes at home; its exam, at the school, in place in a session");
    }

    // What the panel in place offers for a course: "none", "class", "exam" or "class and exam".
    private static string Here(CourseDef c, StudyMode mode, bool inRooms, bool session)
    {
        bool home = mode == StudyMode.Distance || c.Campus == Campus.Online;
        bool classAtSchool = !home && c.Campus == Campus.School;
        bool examAtSchool = home || c.Campus == Campus.School;
        ClassPlayRules.InPlace(classAtSchool, ClassPlayRules.HeldOnline(c, mode), examAtSchool, inRooms, session, out bool classHere, out bool examHere);
        return classHere && examHere ? "class and exam" : classHere ? "class" : examHere ? "exam" : "none";
    }

    // ---------------------------------------------------------------- the source

    private static void SessionDeskSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        var learning = new List<string>();
        var studies = new List<string>();
        var shows = new List<string>();
        foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string text = System.IO.File.ReadAllText(file);
            string name = System.IO.Path.GetFileName(file);
            for (int i = 0; i < Occurrences(text, ".StartLearning()"); i++)
            {
                learning.Add(name);
            }
            if (text.Contains("new StudyActivity(") || text.Contains("new PlayerActivity.StudyActivity("))
            {
                studies.Add(name);
            }
            if (text.Contains("PlayerActivityUI.Show(") && System.IO.Path.GetDirectoryName(file).EndsWith("Careers", StringComparison.Ordinal))
            {
                shows.Add(name);
            }
        }
        Check(learning.Count == 1 && learning[0] == "CareersDirector.ClassPlay.cs",
            "session desks: the game's door is asked for its study panel in one place only, CareersDirector.OpenDesk (found in: " + string.Join(", ", learning) + ")");
        Check(studies.Count == 0,
            "session desks: no StudyActivity of the mod's making is handed to the game's panel (new StudyActivity in: " + string.Join(", ", studies) + ")");
        Check(shows.Count == 1 && shows[0] == "DeskGame.cs",
            "session desks: in Careers the game's panel is asked to show an activity in DeskGame.Show alone (in: " + string.Join(", ", shows) + ")");

        string careers = System.IO.Path.Combine(scripts, "Careers");
        string Read(string file)
        {
            string path = System.IO.Path.Combine(careers, file);
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : string.Empty;
        }
        string play = Read("CareersDirector.ClassPlay.cs"), school = Read("SchoolDesk.cs"), interior = Read("CampusInterior.cs");
        string floors = Read("CareersDirector.Floors.cs"), prompt = Read("CareersDirector.Prompt.cs"), game = Read("DeskGame.cs");
        Check(play.Length > 0 && school.Length > 0 && interior.Length > 0 && floors.Length > 0 && prompt.Length > 0 && game.Length > 0,
            "session desks: the director's desks, the desk, the university's floors, the panel in place and the desk's game side are read");

        string open = MethodText(play, "internal string OpenDesk(EducationDoorController door, int room, bool begin)");
        int session = open.IndexOf("bool session = GoingPublic.InSession;", StringComparison.Ordinal);
        int way = open.IndexOf("DeskWay way = ClassPlayRules.WayToDesk(session);", StringComparison.Ordinal);
        int alone = open.IndexOf("if (way == DeskWay.GameStudy)", StringComparison.Ordinal);
        int learn = open.IndexOf("door.StartLearning();", StringComparison.Ordinal);
        int back = alone < 0 ? -1 : open.IndexOf("return null;", alone, StringComparison.Ordinal);
        int own = open.IndexOf("SchoolDesk desk = SchoolDesk.Own(door, this, offer);", StringComparison.Ordinal);
        int shown = open.IndexOf("DeskGame.Show(desk)", StringComparison.Ordinal);
        Check(session >= 0 && way > session && alone > way && learn > alone && back > learn && own > back && shown > own,
            "session desks: OpenDesk asks the game's door for its study panel only on the way taken alone, and in a session shows the desk's own activity");
        Check(school.Contains("internal sealed class SchoolDesk : IPlayerActivity") && !school.Contains(": StudyActivity"),
            "session desks: the desk's own activity is the mod's, not a StudyActivity that Going Public would take");
        string made = MethodText(school, "public static SchoolDesk Own(EducationDoorController door, CareersDirector director, DeskOffer offer)");
        Check(made.Contains("place.CreateActivity(door) is StudyActivity game") && made.Contains("offer.Only = true;") && !made.Contains("Show(") && !made.Contains("StartLearning"),
            "session desks: SchoolDesk.Own makes the door's study activity as the game does but never shows it, and gives the panel the class's alone");
        string toSchool = MethodText(play, "private string ToSchoolDesk(Enrolment e, SittingKind kind)");
        string toRoom = MethodText(interior, "public string GoToRoom(int course, bool begin = false)");
        string clicked = MethodText(floors, "internal void RoomDoorClicked(EducationDoorController door)");
        Check(toSchool.Contains("OpenDesk(door, 0, true)") && toRoom.Contains("_director.OpenDesk(door, room, true)")
              && toRoom.Contains("_director.OpenDesk(door, room, false)") && clicked.Contains("OpenDesk(door, room, false)"),
            "session desks: the school's desk from the Campus app and the panel in place, the classroom from the Campus app and the floor's"
            + " buttons, and a classroom door's click all open the desk through OpenDesk");
        string takes = MethodText(floors, "internal bool TakesRoomClick(EducationDoorController door)");
        string offers = MethodText(game, "private static bool Offers(EntityController e)");
        string click = MethodText(game, "private static void Clicked()");
        Check(takes.Contains("bool session = GoingPublic.InSession;") && takes.Contains("ClassPlayRules.TakesRoomClick(session, room)")
              && offers.Contains("e is EducationDoorController door") && offers.Contains("TakesRoomClick(door)") && click.Contains("RoomDoorClicked(door)")
              && game.Contains("entityController is EducationDoorController ? \"click_to_learn\" : \"click_to_work\""),
            "session desks: a classroom door's click is the mod's (the game's own Click to learn) only as the rule says, and opens the room's own panel");
        string inPlace = MethodText(prompt, "private void ClassPrompt()");
        Check(inPlace.Contains("bool session = GoingPublic.InSession;")
              && inPlace.Contains("ClassPlayRules.InPlace(AtTheSchool(e, false), online, AtTheSchool(e, true), InRooms(e), session, out bool classHere, out bool examHere);")
              && prompt.Contains("AtTheSchool(e, false) && !ClassPlayRules.SchoolInPlace(GoingPublic.InSession) ? \"reallife_study_note_class_desk\""),
            "session desks: the panel in place offers the school's class in a session by the rule, and the class's notice says to press the key there, not Study at a desk");

        // One self-check line a desk's panel opened: every way out of OpenDesk says it first.
        int returns = Occurrences(open, "return ");
        int checks = Occurrences(open, "DeskCheck(");
        string line = MethodText(play, "private static void DeskCheck(bool session, DeskWay way, string where, bool begin, string fail, string said)");
        Check(returns > 0 && checks == returns && line.Contains("Meta.Dev.Verbose") && line.Contains("Careers check (desk panel): ")
              && line.Contains("\"in a Going Public session\" : \"alone\"") && line.Contains("\"PASS\"") && line.Contains("\"FAIL, \""),
            "session desks: with Developer diagnostics on, each desk's panel opened writes one self-check line, in a session or not, the way taken,"
            + " PASS or why not (" + checks + " lines for " + returns + " ways out)");
    }

    // ---------------------------------------------------------------- What's new

    private static void SessionDeskWhatsNew()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("session desks", langs, codes))
        {
            return;
        }
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en"] = "Fixed: in a Going Public session, classes, exams and homework at Manhattan Business School and the university",
            ["pt"] = "Corrigido: numa sessão do Going Public, aulas, provas e tarefas na Manhattan Business School",
            ["zh-cn"] = "已修复：在 Going Public 联机中，曼哈顿商学院和大学的课程、考试和作业",
            ["de"] = "Behoben: In einer Sitzung von Going Public bleiben Stunden, Prüfungen und Hausaufgaben",
            ["fr"] = "Corrigé : dans une session de Going Public, les séances, examens et devoirs",
            ["es"] = "Corregido: en una sesión de Going Public, las clases, los exámenes y los deberes",
            ["ru"] = "Исправлено: в сессии Going Public занятия, экзамены и домашние задания",
            ["nl"] = "Opgelost: in een Going Public-sessie stoppen lessen, examens en huiswerk",
            ["tr"] = "Düzeltildi: Bir Going Public oturumunda, Manhattan İşletme Okulu ve üniversitedeki dersler"
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
            "session desks: What's new has the fix's line, with its language's fix mark, in some reallife_changes_ row of every language (lacking: "
            + string.Join(", ", lacking) + ")");
    }
}
