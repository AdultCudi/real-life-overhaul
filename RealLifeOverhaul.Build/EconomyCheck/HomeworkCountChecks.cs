using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// Homework counts across sittings (Scripts/Careers: Study/StudyRules.Learning, Sitting, CareersDirector.Coursework). A sitting
// stopped before its two hours used to count nothing and say nothing, so a player who sat an hour and a half at the computer
// saw no piece done. Now the minutes sat are kept on the enrolment (written as they pass, so a save in the middle keeps them), the
// next sitting asks only for the rest, a stop says so, and a piece of two hours in two sittings is one piece. First the game-free
// rules, then the save, the skip, the source, the texts, then What's new.
internal static partial class Program
{
    private static void HomeworkCounts()
    {
        HomeworkCountRules();
        HomeworkCountSave();
        HomeworkCountSources();
        HomeworkCountTexts();
    }

    private static void HomeworkCountRules()
    {
        int full = StudyRules.HomeworkFull;
        Check(full == StudyCatalogue.HomeworkHours * 60 && full == 120, "homework count: a piece is the catalogue's hours in minutes (" + full + ")");
        var e = new Enrolment { Course = StudyCatalogue.Bartending, Stage = (int)EnrolStage.Studying };
        Check(StudyRules.HomeworkLeft(e) == full && StudyRules.HomeworkLeft(null) == full, "homework count: a fresh piece asks for all of its minutes");

        // Early stop: the minutes are carried, nothing counts yet.
        bool done = StudyRules.HomeworkSat(0, 45, false, out int kept);
        Check(!done && kept == 45, "homework count: a sitting stopped 45 minutes in keeps 45 minutes and the piece is not done");
        e.HomeworkMinutes = kept;
        Check(StudyRules.HomeworkLeft(e) == 75, "homework count: the next sitting asks only for the 75 minutes left");

        // Finished across two sittings.
        bool second = StudyRules.HomeworkSat(45, 75, true, out int after);
        Check(second && after == 0, "homework count: 45 minutes, then the 75 that were left, make one piece and leave nothing carried");
        Check(StudyRules.HomeworkSat(45, 75, false, out int exact) && exact == 0, "homework count: the minutes adding up to the piece finish it even when the panel did not say whole");
        Check(!StudyRules.HomeworkSat(45, 74, false, out int shy) && shy == 119, "homework count: one minute short of the piece does not finish it");
        Check(!StudyRules.HomeworkSat(0, 0, false, out int none) && none == 0 && !StudyRules.HomeworkSat(-5, -5, false, out int neg) && neg == 0,
            "homework count: no minutes, or nonsense ones, keep nothing and finish nothing");
        Check(StudyRules.HomeworkSat(0, full, true, out _) && StudyRules.HomeworkSat(0, 100000, false, out int huge) && huge == 0, "homework count: a whole sitting, or more minutes than a piece, finishes it");
        Check(StudyRules.HomeworkRunning(45, 30) == 75 && StudyRules.HomeworkRunning(45, 75) == full - 1 && StudyRules.HomeworkRunning(0, 0) == 0 && StudyRules.HomeworkRunning(-1, -1) == 0,
            "homework count: while a sitting runs the carried minutes follow it, never a whole piece, never below nothing");
        e.HomeworkMinutes = 5000;
        Check(StudyRules.HomeworkLeft(e) == 1, "homework count: carried minutes past a piece still leave a minute to sit");

        // Skip and fast forward: the clock's minutes are the sitting's, so a sitting skipped to its end is whole, also with carried minutes.
        var skipped = new ClassTally(full);
        skipped.Begin(40000);
        skipped.Add(3, 40003);
        TallyEnd end = skipped.Settle(40000 + full, out int minutes, out bool whole);
        Check(end == TallyEnd.Counted && minutes == full && whole, "homework count: a piece skipped or fast-forwarded to its end by the clock is whole");
        var rest = new ClassTally(75);
        rest.Begin(50000);
        rest.Settle(50075, out int restMinutes, out bool restWhole);
        Check(restMinutes == 75 && restWhole && StudyRules.HomeworkSat(45, restMinutes, restWhole, out _), "homework count: the rest of a piece skipped to its end finishes the piece");
    }

    private static void HomeworkCountSave()
    {
        var f = new StudyFile { Seed = 5 };
        f.Courses.Add(new Enrolment { Course = StudyCatalogue.Plumber, Start = 10, Stage = (int)EnrolStage.Studying, Homework = 1, HomeworkMinutes = 45 });
        string line = CareersStore.Write(f);
        StudyFile back = CareersStore.Read(line, out _);
        Check(back.Courses.Count == 1 && back.Courses[0].HomeworkMinutes == 45 && back.Courses[0].Homework == 1 && CareersStore.Write(back) == line,
            "homework count: the minutes sat on a piece are saved and loaded, and the line reads back the same");
        var older = new StudyFile { Seed = 5 };
        older.Courses.Add(new Enrolment { Course = StudyCatalogue.Plumber, Start = 10, Stage = (int)EnrolStage.Studying, Homework = 1 });
        Check(CareersStore.Read(CareersStore.Write(older), out _).Courses[0].HomeworkMinutes == 0, "homework count: a course with no minutes sat saves and loads as none");
        string without = line.Replace("|45;", ";").Replace("|45~", "~");
        StudyFile was = CareersStore.Read(without, out _);
        Check(without != line && was.Courses.Count == 1 && was.Courses[0].HomeworkMinutes == 0 && was.Courses[0].Homework == 1, "homework count: a save from before the minutes loads with none carried");
        f.Courses[0].HomeworkMinutes = 9999;
        Check(CareersStore.Read(CareersStore.Write(f), out _).Courses[0].HomeworkMinutes == StudyRules.HomeworkFull - 1, "homework count: a damaged number of minutes loads as less than a piece");
    }

    private static void HomeworkCountSources()
    {
        string course = ModSource(@"Careers\CareersDirector.Coursework.cs"), sitting = ModSource(@"Careers\Sitting.cs");
        string computer = ModSource(@"Careers\ComputerSitting.cs"), school = ModSource(@"Careers\SchoolDesk.cs"), campus = ModSource(@"Careers\CampusApp.Studying.cs");
        string director = ModSource(@"Careers\CareersDirector.cs");
        Check(course.Length > 0 && sitting.Length > 0 && computer.Length > 0 && school.Length > 0 && campus.Length > 0 && director.Length > 0, "homework count: the sources are read");
        string ending = MethodText(course, "private void StudyEnded(Sitting s, int minutes, bool whole, bool current)");
        Check(ending.Contains("StudyRules.HomeworkSat(s.Carried, minutes, whole, out int kept)") && ending.Contains("e.HomeworkMinutes = kept;") && ending.Contains("reallife_study_note_homework_early")
              && ending.Contains("s.Why") && ending.Contains("e.HomeworkMinutes = 0;"),
            "homework count: a homework sitting that ends early keeps its minutes, tells the player and logs who ended it; a finished piece clears them");
        string made = MethodText(course, "private Sitting HomeworkSitting(Enrolment e)");
        Check(made.Contains("StudyRules.HomeworkLeft(e)") && made.Contains("sitting.Carried"), "homework count: a sitting asks only for the rest of the piece and remembers what was carried");
        string worked = MethodText(sitting, "public void SkipWorked(int minutes)");
        Check(worked.Contains("SittingKind.Homework") && worked.Contains("_director.HomeworkWorked(this, minutes)"), "homework count: the minutes are written to the enrolment as they pass, so a save in the middle keeps them");
        Check(computer.Contains("sitting.Why = _why;") && school.Contains("sitting.Why = _why;") && computer.Contains("the player pressing Stop") && school.Contains("the player pressing Stop")
              && computer.Contains("the time being done") && computer.Contains("the game's panel") && computer.Contains("the mod stopping it"),
            "homework count: the computer and the desks say who ended a sitting: the player's Stop, the time, the game's panel (energy, night, event, save or load), the mod");
        string hint = MethodText(course, "private void HomeworkHint()");
        Check(hint.Contains("EntertainActivity") && hint.Contains("WorkActivity") && hint.Contains("StudyActivity") && hint.Contains("_homeworkHinted = true;") && hint.Contains("reallife_study_hint_homework")
              && director.Contains("HomeworkHint();"),
            "homework count: using the computer through the game's own activity while homework is due shows the hint once");
        Check(campus.Contains("reallife_ibox_homework_piece") && campus.Contains("reallife_ibox_revision_piece") && campus.Contains("e.HomeworkMinutes"), "homework count: the Studying tab shows the minutes done on the current piece");
    }

    private static void HomeworkCountTexts()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("homework count", langs, codes))
        {
            return;
        }
        var wanted = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["reallife_study_note_homework_early"] = new[] { "{course}", "{done}", "{total}" },
            ["reallife_study_hint_homework"] = new[] { "{course}" },
            ["reallife_ibox_homework_piece"] = new[] { "{number}", "{of}", "{done}", "{total}" },
            ["reallife_ibox_revision_piece"] = new[] { "{done}", "{total}" }
        };
        var bad = new List<string>();
        for (int l = 0; l < Math.Min(3, codes.Length); l++)
        {
            foreach (KeyValuePair<string, string[]> k in wanted)
            {
                if (!langs[l].TryGetValue(k.Key, out string text) || text.Length == 0)
                {
                    bad.Add(codes[l] + " lacks " + k.Key);
                    continue;
                }
                foreach (string name in k.Value)
                {
                    if (!text.Contains(name))
                    {
                        bad.Add(codes[l] + " " + k.Key + " lacks " + name);
                    }
                }
            }
            if (langs[l].TryGetValue("reallife_study_note_homework_early", out string note) && note.Length > 240)
            {
                bad.Add(codes[l] + " notice over 240");
            }
        }
        Check(bad.Count == 0, "homework count: the early-stop notice, the hint and the Studying tab's lines are in en, pt and zh-cn with their names, the notice within 240 characters (" + string.Join("; ", bad) + ")");
        Check(langs[0].TryGetValue("reallife_study_hint_homework", out string h) && h.Contains("Do {course} homework") && h.Contains("Campus > Do homework")
              && langs[0].TryGetValue("reallife_study_note_homework_early", out string n) && n.Contains("stopped early"),
            "homework count: the hint says where homework is started, and the notice says it stopped early");

        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en"] = "Fixed: homework stopped early used to count for nothing and said nothing.",
            ["pt"] = "Corrigido: a tarefa interrompida cedo não contava e não avisava nada.",
            ["zh-cn"] = "已修复：提前停止的作业过去不计入，也没有任何提示。"
        };
        var lacking = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            string want = lines.TryGetValue(codes[l], out string given) ? given : FixMarks[codes[l]];
            if (!WhatsNewHas(langs[l], want))
            {
                lacking.Add(codes[l]);
            }
        }
        Check(lacking.Count == 0, "homework count: What's new has the fix's line, with its fix mark, in some reallife_changes_ row of every language (lacking: " + string.Join(", ", lacking) + ")");
    }
}
