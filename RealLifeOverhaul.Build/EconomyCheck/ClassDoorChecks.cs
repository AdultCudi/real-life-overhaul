using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// The university's classroom doors (Scripts/Careers/Study/CampusRules: the course behind each door of each floor, the side
// its sign faces, the mark over it) and the Persona app's education box (Scripts/Careers/Study/EducationRules: its groups,
// the order of its rows and each row's standing and words).
internal static partial class Program
{
    private static void ClassDoors()
    {
        // ---------------------------------------------------------------- the course behind each door
        string[] game = { "BasicHr", "Headquarters", "MarketDemands", "OfficeBusinesses", "ProductManufacturing" };
        var seen = new int[StudyCatalogue.IdsMost];
        int free = 0, held = 0;
        bool freeWhere = true;
        for (int floor = 1; floor <= CampusRules.Floors; floor++)
        {
            for (int d = 0; d < game.Length; d++)
            {
                int course = CampusRules.CourseOfDoor(floor, game[d]);
                if (course < 0)
                {
                    free++;
                    freeWhere &= floor == CampusRules.Floors && CampusRules.RoomOfDoor(game[d]) >= 3;
                    continue;
                }
                held++;
                seen[course]++;
            }
        }
        bool once = true;
        for (int i = 0; i < CampusRules.Courses.Length; i++)
        {
            once &= seen[CampusRules.Courses[i]] == 1;
        }
        Check(held == CampusRules.Courses.Length && once, "class doors: over the four floors the school's five doors hold each of the university's 17 courses once");
        Check(free == 3 && freeWhere, "class doors: the only free rooms are floor 4's rooms 3 to 5");
        Check(CampusRules.CourseOfDoor(1, "OfficeBusinesses") == StudyCatalogue.Cybersecurity && CampusRules.CourseOfDoor(1, "BasicHr") == StudyCatalogue.ComputerScience
              && CampusRules.CourseOfDoor(1, "ProductManufacturing") == StudyCatalogue.Business && CampusRules.CourseOfDoor(4, "Headquarters") == StudyCatalogue.Pgce,
            "class doors: the game's Office Businesses door of floor 1 is room 4, BSc Cybersecurity; Basic HR room 1, computer science; floor 4 room 2 the PGCE");
        Check(CampusRules.CourseOfDoor(1, "FoodSafety") == -1 && CampusRules.CourseOfDoor(1, "AutoMechanics") == -1 && CampusRules.CourseOfDoor(1, null) == -1
              && CampusRules.CourseOfDoor(0, "BasicHr") == -1 && CampusRules.CourseOfDoor(5, "BasicHr") == -1,
            "class doors: a door of another of the game's courses, a door with no name, and floors that are none hold no course");
        bool same = true;
        for (int floor = 1; floor <= CampusRules.Floors; floor++)
        {
            for (int d = 0; d < game.Length; d++)
            {
                same &= CampusRules.CourseOfDoor(floor, game[d]) == CampusRules.CourseAt(floor, CampusRules.RoomOfDoor(game[d]));
            }
        }
        Check(same, "class doors: the door's course is its room's, the same as the sign, the Campus app and the floor's notice say");

        // ---------------------------------------------------------------- the side the sign faces
        // The game's school layout (StreamingAssets/BusinessLayouts/School/D1/GarmentDistrictSchool.json): each door's place,
        // its forward (from its rotation) and its room's first chair.
        float[,] doors =
        {
            // forward x, forward z, door x, door z, chair x, chair z
            { -1f, 0f, 999.5f, -145.5f, 1002.5f, -142.25f },        // BasicHr, turned -90
            { -1f, 0f, 999.419f, -147.4987f, 1002.75f, -147.5f },   // Headquarters, turned 270
            { 0f, -1f, 996.5f, -144.5f, 990.5f, -140.25f },         // MarketDemands, turned 180
            { 1f, 0f, 994.5f, -150f, 993.5f, -152.75f },            // OfficeBusinesses, turned 90
            { 1f, 0f, 994.5f, -147.5f, 989.971f, -147.493f }        // ProductManufacturing, turned 90
        };
        bool outward = true;
        for (int i = 0; i < doors.GetLength(0); i++)
        {
            outward &= CampusRules.SignSide(doors[i, 0], doors[i, 1], doors[i, 2], doors[i, 3], doors[i, 4], doors[i, 5], true) == 1;
        }
        Check(outward, "class doors: every room's sign of the game's school faces the door's forward, away from the room's chair: out of the room");
        Check(CampusRules.SignSide(-1f, 0f, 994.5f, -147.5f, 989.971f, -147.493f, true) == -1 && CampusRules.SignSide(0f, 1f, 996.5f, -144.5f, 990.5f, -140.25f, true) == -1,
            "class doors: a door turned the other way round still has its sign facing away from the chair (back along its forward)");
        Check(CampusRules.SignSide(1f, 0f, 0f, 0f, 0f, 0f, false) == 1 && CampusRules.SignSide(0f, 1f, 5f, 5f, 5f, 5f, true) == 1,
            "class doors: with no chair, or one level with the door, the sign faces the door's forward");

        // ---------------------------------------------------------------- the mark over the door
        Check(CampusRules.MarkOf(false, true, true, true, true, 0) == RoomMark.None, "class doors: a free room has no mark");
        Check(CampusRules.MarkOf(true, false, false, false, false, -1) == RoomMark.None, "class doors: a course not taken has no mark");
        Check(CampusRules.MarkOf(true, true, true, false, true, -1) == RoomMark.Soon && CampusRules.MarkOf(true, true, true, false, false, CampusRules.SoonMinutes) == RoomMark.Soon
              && CampusRules.MarkOf(true, true, true, false, false, 0) == RoomMark.Soon,
            "class doors: a class or exam to begin now, or the next class within the hour, is the game's action mark");
        Check(CampusRules.MarkOf(true, true, true, false, false, CampusRules.SoonMinutes + 1) == RoomMark.Enrolled && CampusRules.MarkOf(true, true, true, false, false, -1) == RoomMark.Enrolled,
            "class doors: enrolled with nothing within the hour is the plain mark");
        Check(CampusRules.MarkOf(true, true, false, false, true, 0) == RoomMark.None, "class doors: a course followed online from home has no mark at its room");
        Check(CampusRules.MarkOf(true, false, false, true, false, -1) == RoomMark.Completed && CampusRules.MarkOf(true, true, true, true, false, 600) == RoomMark.Enrolled,
            "class doors: a course passed is completed; taken again (a retake under way) it is marked as under way");

        EducationBox();
    }

    private static void EducationBox()
    {
        // ---------------------------------------------------------------- groups
        bool groups = true;
        var inGroup = new int[EducationRules.Order.Length];
        for (int i = 0; i < StudyCatalogue.All.Length; i++)
        {
            CourseDef c = StudyCatalogue.All[i];
            EducationGroup g = EducationRules.GroupOf(c);
            inGroup[(int)g]++;
            groups &= g == EducationGroup.Certificate ? c.Kind == CourseKind.Certificate
                : g == EducationGroup.Nursing ? c.Campus == Campus.Hospital
                : g == EducationGroup.Online ? c.Campus == Campus.Online && c.Kind != CourseKind.Certificate
                : g == EducationGroup.Degree ? c.Kind == CourseKind.Degree && c.Campus == Campus.University
                : g == EducationGroup.Postgrad ? c.Kind == CourseKind.Postgrad
                : g == EducationGroup.Diploma ? c.Kind == CourseKind.Vocational && c.Campus == Campus.School
                : c.Kind == CourseKind.Short && c.Campus == Campus.School;
        }
        Check(groups, "education box: each course is in the group of where it is taught and what it is");
        Check(inGroup[(int)EducationGroup.Degree] == 13 && inGroup[(int)EducationGroup.Postgrad] == 4 && inGroup[(int)EducationGroup.Nursing] == 1 && inGroup[(int)EducationGroup.Short] == 8
              && inGroup[(int)EducationGroup.Diploma] == 8 && inGroup[(int)EducationGroup.Online] == 2 && inGroup[(int)EducationGroup.Certificate] == 12,
            "education box: 13 degrees, 4 after a degree, nursing, 8 short courses, 8 diplomas, 2 online courses and 12 certificates");
        Check(EducationRules.Order.Length == 7 && EducationRules.Order[0] == EducationGroup.Degree && EducationRules.Order[6] == EducationGroup.Certificate
              && EducationRules.GroupKeys.Length == EducationRules.Order.Length,
            "education box: the university's degrees first, the certificates last, each group with its words");

        // ---------------------------------------------------------------- the box's height and a heading's width
        // The game's box: 690 high, its rows from 163 under its top, its five rows 438 high (80 each, 8 between, 3 above and below).
        float gameBox = EducationRules.BoxHeight(690f, 163f, 438f, 438f);
        Check(gameBox == 690f, "education box: with the game's rows alone, the game's own height");
        float full = EducationRules.BoxHeight(690f, 163f, 438f, 438f + 55 * 88f);
        Check(full == 163f + 438f + 55 * 88f + (690f - 163f - 438f), "education box: grown to hold every row added, with the game's white under the last");
        Check(EducationRules.BoxHeight(690f, 163f, 600f, 600f + 88f) == 163f + 600f + 88f + EducationRules.LeastMargin
              && EducationRules.BoxHeight(690f, 163f, 400f, 300f) == 690f,
            "education box: a game whose rows already fill its box keeps a little white under the last row; never smaller than the game's");
        float nameWidth = EducationRules.HeadingNameWidth(1141f, 15f, 26f);
        Check(nameWidth == 1141f - 15f - 26f - EducationRules.CountRoom && nameWidth > 587f && EducationRules.HeadingNameWidth(100f, 15f, 26f) == 0f,
            "education box: a heading's name takes the row's width but the count's room, wider than a course's name");

        // ---------------------------------------------------------------- rows, nothing begun
        var rows = new List<EducationRow>(64);
        var fresh = new StudyFile();
        int listed = EducationRules.Rows(fresh, c => true, false, 10, rows);
        bool everyOnce = listed == StudyCatalogue.All.Length && rows.Count == StudyCatalogue.All.Length + 7;
        var count = new int[StudyCatalogue.IdsMost];
        int lastGroup = -1;
        bool headed = true, ordered = true;
        EducationRow heading = default;
        int under = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Heading)
            {
                headed &= i == 0 || under == heading.Count;
                ordered &= (int)rows[i].Group > lastGroup;
                lastGroup = (int)rows[i].Group;
                heading = rows[i];
                under = 0;
                continue;
            }
            under++;
            count[rows[i].Course]++;
            headed &= rows[i].Group == heading.Group && rows[i].Status.State == EducationState.NotStarted;
        }
        headed &= under == heading.Count;
        for (int i = 0; i < StudyCatalogue.All.Length; i++)
        {
            everyOnce &= count[StudyCatalogue.All[i].Id] == 1;
        }
        Check(everyOnce, "education box: with every course offered, each is listed once, under one heading for each of the 7 groups");
        Check(headed && ordered, "education box: the groups come in their order, each heading counting the courses under it");
        Check(!rows[1].Heading && rows[1].Course == StudyCatalogue.ComputerScience && rows[0].Group == EducationGroup.Degree,
            "education box: nothing begun, a group keeps the catalogue's order (computer science first)");
        listed = EducationRules.Rows(fresh, c => c.Kind == CourseKind.Short || c.Kind == CourseKind.Vocational, false, 10, rows);
        Check(listed == 18 && rows.Count == 18 + 3 && rows[0].Group == EducationGroup.Short,
            "education box: the university switched off, only the school's courses and the two online ones are listed, with no empty group");
        listed = EducationRules.Rows(fresh, c => true, true, 10, rows);
        Check(listed == 0 && rows.Count == 0, "education box: a box that cannot scroll, nothing begun: nothing more than the game's own");
        listed = EducationRules.Rows(null, c => true, false, 10, rows);
        Check(listed == StudyCatalogue.All.Length, "education box: no study file yet: every course not started");

        // ---------------------------------------------------------------- standings
        var f = new StudyFile();
        CourseDef cyber = StudyCatalogue.Find(StudyCatalogue.Cybersecurity);
        var studying = new Enrolment { Course = cyber.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = 6, Done = 5, SettledDay = 10, Stage = (int)EnrolStage.Studying };
        f.Courses.Add(studying);
        EducationStatus s = EducationRules.Status(f, cyber, 10);
        Check(s.State == EducationState.Studying && s.Done == 5 && s.Of == cyber.Sessions && s.NextDay == StudyRules.Next(cyber, StudyMode.FullTime, 11)
              && s.NextHour == cyber.Hour && EducationRules.StatusKey(s) == "reallife_persona_edu_classes_next",
            "education box: under way, the classes attended of the timetable's and the next one, as the Campus app gives it");
        studying.FeeLate = true;
        s = EducationRules.Status(f, cyber, 10);
        Check(s.State == EducationState.FeeOwed && EducationRules.StatusKey(s) == "reallife_persona_edu_fee", "education box: an instalment owed is said");
        studying.FeeLate = false;
        studying.Stage = (int)EnrolStage.Exam;
        studying.ExamTo = 30;
        s = EducationRules.Status(f, cyber, 25);
        Check(s.State == EducationState.Exam && s.Day == 30 && EducationRules.StatusKey(s) == "reallife_persona_edu_exam", "education box: the exam to sit, by the window's last day");
        studying.Stage = (int)EnrolStage.Retake;
        studying.ExamTo = 44;
        s = EducationRules.Status(f, cyber, 40);
        Check(s.State == EducationState.Retake && s.Day == 44 && EducationRules.StatusKey(s) == "reallife_persona_edu_retake", "education box: a retake to sit");
        CourseDef cloud = StudyCatalogue.Find(StudyCatalogue.CloudAssociate);
        f.Courses.Add(new Enrolment { Course = cloud.Id, Start = 1, Homework = 2, Stage = (int)EnrolStage.Studying });
        s = EducationRules.Status(f, cloud, 10);
        Check(s.State == EducationState.Studying && s.Homework && s.Done == 2 && s.Of == cloud.Homework && s.NextDay == 0 && EducationRules.StatusKey(s) == "reallife_persona_edu_homework",
            "education box: a certificate under way counts its homework, with no class");
        CourseDef law = StudyCatalogue.Find(StudyCatalogue.Law);
        f.Results.Add(new Enrolment { Course = law.Id, Stage = (int)EnrolStage.Failed, Score = 31 });
        f.Results.Add(new Enrolment { Course = law.Id, Stage = (int)EnrolStage.Passed, Grade = 2 });
        f.Results.Add(new Enrolment { Course = law.Id, Stage = (int)EnrolStage.Passed, Grade = 3 });
        f.Results.Add(new Enrolment { Course = law.Id, Stage = (int)EnrolStage.Withdrawn });
        s = EducationRules.Status(f, law, 50);
        Check(s.State == EducationState.Passed && s.Grade == 3 && EducationRules.StatusKey(s) == "reallife_persona_edu_passed", "education box: passed, with its best grade, whatever came after");
        CourseDef aid = StudyCatalogue.Find(StudyCatalogue.FirstAid);
        f.Results.Add(new Enrolment { Course = aid.Id, Stage = (int)EnrolStage.Withdrawn });
        f.Results.Add(new Enrolment { Course = aid.Id, Stage = (int)EnrolStage.Failed, Score = 42 });
        s = EducationRules.Status(f, aid, 50);
        Check(s.State == EducationState.Failed && s.Score == 42 && EducationRules.StatusKey(s) == "reallife_persona_edu_failed", "education box: never passed, the latest result: failed with its mark");
        CourseDef bar = StudyCatalogue.Find(StudyCatalogue.Bartending);
        f.Results.Add(new Enrolment { Course = bar.Id, Stage = (int)EnrolStage.Barred });
        CourseDef fork = StudyCatalogue.Find(StudyCatalogue.Forklift);
        f.Results.Add(new Enrolment { Course = fork.Id, Stage = (int)EnrolStage.Withdrawn });
        Check(EducationRules.StatusKey(EducationRules.Status(f, bar, 50)) == "reallife_study_stage_barred" && EducationRules.StatusKey(EducationRules.Status(f, fork, 50)) == "reallife_study_stage_withdrawn",
            "education box: barred from the exam and withdrawn are said in the Campus app's words");
        CourseDef barista = StudyCatalogue.Find(StudyCatalogue.Barista);
        Check(EducationRules.StatusKey(EducationRules.Status(f, barista, 50)) == "persona_education_not_started", "education box: not started, in the game's own words");
        f.Courses.Add(new Enrolment { Course = StudyCatalogue.Business, Start = 1, Stage = (int)EnrolStage.Studying, SettledDay = 50 });
        s = EducationRules.Status(f, StudyCatalogue.Find(StudyCatalogue.Business), 50);
        Check(s.NextDay > 50, "education box: today's class settled, the next is on a later day");

        // ---------------------------------------------------------------- order within a group: begun first
        listed = EducationRules.Rows(f, c => true, false, 50, rows);
        int lawAt = IndexOf(rows, law.Id), cyberAt = IndexOf(rows, cyber.Id), businessAt = IndexOf(rows, StudyCatalogue.Business), csAt = IndexOf(rows, StudyCatalogue.ComputerScience);
        Check(cyberAt == 1 && businessAt == 2 && lawAt == 3 && csAt > lawAt, "education box: in a group the courses under way first (catalogue order), then those passed, then the rest");
        int aidAt = IndexOf(rows, aid.Id), barAt = IndexOf(rows, bar.Id), forkAt = IndexOf(rows, fork.Id), baristaAt = IndexOf(rows, barista.Id);
        Check(aidAt < barAt && barAt < forkAt && forkAt < baristaAt && rows[aidAt - 1].Heading, "education box: failed, barred and withdrawn before those not started, in the catalogue's order");
        Check(listed == StudyCatalogue.All.Length, "education box: begun or not, every course once");
        var again = new List<EducationRow>(64);
        EducationRules.Rows(f, c => true, false, 50, again);
        bool sameRows = again.Count == rows.Count;
        for (int i = 0; sameRows && i < rows.Count; i++)
        {
            sameRows = again[i].Heading == rows[i].Heading && again[i].Course == rows[i].Course && again[i].Status.State == rows[i].Status.State;
        }
        Check(sameRows, "education box: the same rows every time for the same studies");
        listed = EducationRules.Rows(f, c => false, true, 50, rows);
        bool begunOnly = listed == 7;
        for (int i = 0; i < rows.Count; i++)
        {
            begunOnly &= rows[i].Heading || EducationRules.Started(rows[i].Status.State);
        }
        Check(begunOnly, "education box: begun courses are listed even when no longer offered; a box that cannot scroll lists only those");

        // ---------------------------------------------------------------- words
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        var langs = new[] { LoadWords(System.IO.Path.Combine(folder, "en.json")), LoadWords(System.IO.Path.Combine(folder, "pt.json")), LoadWords(System.IO.Path.Combine(folder, "zh-cn.json")) };
        Check(langs[0] != null && langs[1] != null && langs[2] != null, "education box: en.json, pt.json and zh-cn.json are where the checks look for them (" + folder + ")");
        if (langs[0] == null || langs[1] == null || langs[2] == null)
        {
            return;
        }
        var need = new Dictionary<string, string[]>
        {
            { "reallife_persona_edu_count", new[] { "{count}" } },
            { "reallife_persona_edu_count_one", new[] { "{count}" } },
            { "reallife_persona_edu_classes", new[] { "{done}", "{of}" } },
            { "reallife_persona_edu_classes_next", new[] { "{done}", "{of}", "{day}", "{hour}" } },
            { "reallife_persona_edu_homework", new[] { "{done}", "{of}" } },
            { "reallife_persona_edu_fee", new[] { "{done}", "{of}" } },
            { "reallife_persona_edu_exam", new[] { "{day}" } },
            { "reallife_persona_edu_retake", new[] { "{day}" } },
            { "reallife_persona_edu_passed", new[] { "{grade}" } },
            { "reallife_persona_edu_failed", new[] { "{score}" } },
            { "reallife_study_stage_withdrawn", new string[0] },
            { "reallife_study_stage_barred", new string[0] },
            { "reallife_campus_room_done", new string[0] },
            { "reallife_campus_note_room_panel", new string[0] }
        };
        for (int g = 0; g < EducationRules.Order.Length; g++)
        {
            need[EducationRules.GroupKey(EducationRules.Order[g])] = new string[0];
        }
        for (int i = 0; i < StudyCatalogue.All.Length; i++)
        {
            need["reallife_study_course_" + StudyCatalogue.All[i].Key] = new string[0];
        }
        var missing = new List<string>();
        foreach (KeyValuePair<string, string[]> k in need)
        {
            for (int l = 0; l < langs.Length; l++)
            {
                bool ok = langs[l].TryGetValue(k.Key, out string words) && !string.IsNullOrWhiteSpace(words);
                for (int p = 0; ok && p < k.Value.Length; p++)
                {
                    ok = words.Contains(k.Value[p]);
                }
                if (!ok)
                {
                    missing.Add(k.Key + (l == 0 ? " (en)" : l == 1 ? " (pt)" : " (zh-cn)"));
                }
            }
        }
        Check(missing.Count == 0, "education box: every heading, standing and course name is in all three locales with the figures it is filled with ("
                                  + need.Count + " keys" + (missing.Count > 0 ? "; missing " + string.Join(", ", missing) : string.Empty) + ")");
        bool oneLine = true;
        string tooLong = string.Empty;
        float headingWidth = EducationRules.HeadingNameWidth(1141f, 15f, 26f);   // the game's row: 1141 wide, its name 15 in, its mark's edge 26 from the right
        for (int g = 0; g < EducationRules.Order.Length; g++)
        {
            for (int l = 0; l < langs.Length; l++)
            {
                if (langs[l].TryGetValue(EducationRules.GroupKey(EducationRules.Order[g]), out string words) && !EducationRules.HeadingFits(words, headingWidth))
                {
                    oneLine = false;
                    tooLong += " " + words;
                }
            }
        }
        Check(oneLine && !EducationRules.HeadingFits(new string('a', 60), headingWidth),
            "education box: every group's heading fits its row on one line in all three locales" + (oneLine ? string.Empty : ";" + tooLong));
        bool keyed = true;
        foreach (EducationState state in Enum.GetValues(typeof(EducationState)))
        {
            string key = EducationRules.StatusKey(new EducationStatus { State = state, NextDay = 3 });
            keyed &= key == "persona_education_not_started" ? state == EducationState.NotStarted : need.ContainsKey(key);
        }
        Check(keyed, "education box: every standing has words the checks know (the game's own for not started)");
        Check(langs[0].TryGetValue("reallife_campus_note_room_panel", out string note) && note.Length <= 240, "education box: the classroom panel's notice is a notification's length");
    }

    private static int IndexOf(List<EducationRow> rows, int course)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (!rows[i].Heading && rows[i].Course == course)
            {
                return i;
            }
        }
        return -1;
    }
}
