using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// The next class and the classes held so far (Scripts/Careers/Study/StudyRules: Held, NextClass on an enrolment), as the
// Campus app, Persona, the desks, the doors and the notices all read them: before, during and after a class's window, a
// class gone today but not yet counted (the hour after its window counts it), a missed day, the week wrapping round, a
// class being sat, and a course whose last class has gone. Also the dates of the Campus and Career apps' texts, which are
// filled with the game's "Day 8" and must not say "day" again.
internal static partial class Program
{
    private static void NextClasses()
    {
        CourseDef fork = StudyCatalogue.Find(StudyCatalogue.Forklift);       // the school, Mon and Wed at 09:00, 2 classes
        CourseDef cs = StudyCatalogue.Find(StudyCatalogue.ComputerScience);  // the university, Mon Wed Fri at 10:00, 24 classes
        int forkStart = StudyRules.Hour(fork, StudyMode.FullTime) * 60, csStart = StudyRules.Hour(cs, StudyMode.FullTime) * 60;
        int late = StudyCatalogue.LateMinutes;
        Check(StudyRules.On(StudyRules.Days(fork, StudyMode.FullTime), 8) && StudyRules.On(StudyRules.Days(cs, StudyMode.FullTime), 8) && !StudyRules.On(StudyRules.Days(cs, StudyMode.FullTime), 9),
            "next class: day 8 is a Monday with both courses' classes, day 9 a Tuesday without (the checks below rely on it)");

        // ---------------------------------------------------------------- the owner's case: the last class gone, unattended
        // Enrolled on day 2 (a Tuesday), attended day 3 (Wednesday), away from the school at Monday day 8's 09:00 class.
        var owner = new Enrolment { Course = fork.Id, Mode = (int)StudyMode.FullTime, Start = 2, Seen = 1, Done = 1, SettledDay = 3, Stage = (int)EnrolStage.Studying };
        Check(StudyRules.NextClass(owner, 8, 9 * 60 + 0, -1) == 8 * 1440 + forkStart && StudyRules.Held(owner, 8, 9 * 60, -1) == 1,
            "next class: at 09:00 today's class is the next one, and only the one attended is held");
        Check(StudyRules.NextClass(owner, 8, forkStart + late, -1) == 8 * 1440 + forkStart,
            "next class: today's class until the last minute it can be joined");
        Check(StudyRules.Held(owner, 8, 10 * 60 + 12, -1) == 2 && StudyRules.NextClass(owner, 8, 10 * 60 + 12, -1) == -1,
            "next class: at 10:12 the 09:00 class is gone and held (1 attended of 2 so far), and with both of its classes held the course has no next class");
        Check(StudyRules.NextClass(fork, StudyMode.FullTime, 8, 10 * 60 + 12, 4) == 10 * 1440 + forkStart,
            "next class: the timetable alone would say Wednesday, which the course no longer has (why the enrolment's rule is the one read)");

        // ---------------------------------------------------------------- a long course: before, during, after the window
        var degree = new Enrolment { Course = cs.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = 3, Done = 3, SettledDay = 5, Stage = (int)EnrolStage.Studying };
        Check(StudyRules.NextClass(degree, 8, csStart - 60, -1) == 8 * 1440 + csStart && StudyRules.Held(degree, 8, csStart - 60, -1) == 3,
            "next class: before the window, today's class");
        Check(StudyRules.NextClass(degree, 8, csStart + 30, -1) == 8 * 1440 + csStart && StudyRules.Held(degree, 8, csStart + 30, -1) == 3,
            "next class: during the window, today's class, not yet held");
        Check(StudyRules.NextClass(degree, 8, csStart + late + 1, -1) == 10 * 1440 + csStart && StudyRules.Held(degree, 8, csStart + late + 1, -1) == 4,
            "next class: after the window, Wednesday's, and today's counts as held");
        Check(StudyRules.NextClass(degree, 8, csStart + 30, cs.Id) == 10 * 1440 + csStart && StudyRules.Held(degree, 8, csStart + 30, cs.Id) == 4,
            "next class: while today's is being sat it is held, and the next is Wednesday's");
        Check(StudyRules.NextClass(degree, 8, csStart + 30, fork.Id) == 8 * 1440 + csStart,
            "next class: another course's class being sat changes nothing");

        // ---------------------------------------------------------------- a missed day, the week round, a course not begun
        var missedDay = new Enrolment { Course = cs.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = 2, Done = 2, SettledDay = 3, Stage = (int)EnrolStage.Studying };
        Check(StudyRules.Held(missedDay, 8, csStart - 60, -1) == 3 && StudyRules.NextClass(missedDay, 8, csStart - 60, -1) == 8 * 1440 + csStart,
            "next class: Friday's class not yet counted is held all the same, and Monday's is next");
        var friday = new Enrolment { Course = cs.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = 5, Done = 5, SettledDay = 10, Stage = (int)EnrolStage.Studying };
        Check(StudyRules.NextClass(friday, 12, csStart + late + 1, -1) == 15 * 1440 + csStart,
            "next class: after Friday's window the next is Monday's, over the weekend");
        Check(StudyRules.NextClass(friday, 13, 12 * 60, -1) == 15 * 1440 + csStart && StudyRules.Held(friday, 13, 12 * 60, -1) == 6,
            "next class: on Saturday, Monday's; Friday's (gone, not yet counted) is held");
        var later = new Enrolment { Course = cs.Id, Mode = (int)StudyMode.FullTime, Start = 15, SettledDay = -1, Stage = (int)EnrolStage.Studying };
        Check(StudyRules.NextClass(later, 8, csStart + late + 1, -1) == 15 * 1440 + csStart && StudyRules.Held(later, 8, csStart + late + 1, -1) == 0,
            "next class: a course that starts later begins with its first day; nothing before it is held");
        var last = new Enrolment { Course = cs.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = cs.Sessions - 1, Done = cs.Sessions - 1, SettledDay = 5, Stage = (int)EnrolStage.Studying };
        Check(StudyRules.NextClass(last, 8, csStart - 60, -1) == 8 * 1440 + csStart && StudyRules.NextClass(last, 8, csStart + late + 1, -1) == -1
              && StudyRules.Held(last, 8, csStart + late + 1, -1) == cs.Sessions,
            "next class: the last class is next until its window shuts; then none, and never more held than the course has");
        var exam = new Enrolment { Course = cs.Id, Mode = (int)StudyMode.FullTime, Start = 1, Seen = cs.Sessions, Done = cs.Sessions, SettledDay = 5, Stage = (int)EnrolStage.Exam };
        Check(StudyRules.NextClass(exam, 8, 0, -1) == -1 && StudyRules.Held(exam, 8, 0, -1) == cs.Sessions && StudyRules.NextClass((Enrolment)null, 8, 0, -1) == -1
              && StudyRules.Held(null, 8, 0, -1) == 0,
            "next class: none at the exam, none for no enrolment");

        // ---------------------------------------------------------------- Persona reads the same rule
        var f = new StudyFile();
        f.Courses.Add(degree);
        EducationStatus s = EducationRules.Status(f, cs, 8, csStart + late + 1, -1);
        Check(s.NextDay == 10 && s.NextHour == csStart / 60, "next class: Persona's education box names Wednesday's class after Monday's window, as the Campus app does");
        s = EducationRules.Status(f, cs, 8, csStart, -1);
        Check(s.NextDay == 8 && s.NextHour == csStart / 60, "next class: Persona's education box names today's class while it can be joined");

        // ---------------------------------------------------------------- the texts
        var words = LanguageWords(out string[] langs);
        if (!LanguagesRead("next class", words, langs))
        {
            return;
        }
        bool next = true;
        for (int l = 0; l < langs.Length; l++)
        {
            next &= words[l].TryGetValue("reallife_campus_on_next", out string w) && w.Contains("{day}") && w.Contains("{hour}");
        }
        Check(next, "next class: the Campus app's line for it is in every language, with its day and hour");
        // Filled with the game's own day ("Day 8", "Dia 8", "第 8 天", "Tag 8", "Jour 8", "Día 8", "День 8"): the word for day is not said again before it.
        string[,] dated =
        {
            { "reallife_campus_on_mode", "start" }, { "reallife_campus_on_exam", "from" }, { "reallife_campus_on_exam", "to" },
            { "reallife_campus_on_retake", "from" }, { "reallife_campus_row_exam", "from" }, { "reallife_campus_ceremony", "day" },
            { "reallife_campus_child_reading", "day" }, { "reallife_campus_result_passed", "day" }, { "reallife_campus_result_other", "day" },
            { "reallife_campus_skill_line", "day" }, { "reallife_career_job_at", "since" }, { "reallife_career_app_line", "day" },
            { "reallife_career_app_interview", "day" }, { "reallife_career_app_offer", "day" }, { "reallife_career_post_terms", "closes" },
            { "reallife_life_job_review", "next" }, { "reallife_own_practice_line", "since" }, { "reallife_own_contract_line", "due" },
            { "reallife_planner_exam_what", "to" }, { "reallife_study_note_enrolled", "day" }, { "reallife_study_note_exam_open", "day" },
            { "reallife_study_note_graduation", "day" }, { "reallife_work_note_notice", "day" }, { "reallife_family_study_started", "day" }
        };
        var doubled = new List<string>();
        for (int i = 0; i < dated.GetLength(0); i++)
        {
            string key = dated[i, 0], slot = "{" + dated[i, 1] + "}";
            for (int l = 0; l < langs.Length; l++)
            {
                if (!words[l].TryGetValue(key, out string w) || !w.Contains(slot))
                {
                    doubled.Add(langs[l] + " " + key + " (missing, or without " + slot + ")");
                    continue;
                }
                if (Doubled(w, slot, langs[l]))
                {
                    doubled.Add(langs[l] + " " + key);
                }
            }
        }
        Check(doubled.Count == 0, "next class: dates filled with the game's own day say no second word for day (" + string.Join(", ", doubled) + ")");
        Check(Doubled("since day {start}.", "{start}", "en") && Doubled("desde o dia {start}.", "{start}", "pt") && Doubled("自第{start}天起。", "{start}", "zh-cn")
              && Doubled("seit Tag {start}.", "{start}", "de") && Doubled("depuis le jour {start}.", "{start}", "fr") && Doubled("desde el día {start}.", "{start}", "es")
              && Doubled("с дня {start}.", "{start}", "ru")
              && !Doubled("since {start}.", "{start}", "en") && !Doubled("desde o {start}.", "{start}", "pt") && !Doubled("自{start}起。", "{start}", "zh-cn")
              && !Doubled("seit {start}.", "{start}", "de") && !Doubled("depuis {start}.", "{start}", "fr") && !Doubled("desde el {start}.", "{start}", "es")
              && !Doubled("с {start}.", "{start}", "ru"),
            "next class: the doubled day is found in each language, and the fixed wording passes");
    }

    // The word for day in each Latin or Cyrillic language, as it may stand straight before a day (Chinese is "第...天" around it).
    private static readonly Dictionary<string, string[]> DayWords = new Dictionary<string, string[]>
    {
        { "en", new[] { "day", "days" } }, { "pt", new[] { "dia", "dias" } }, { "de", new[] { "tag", "tage", "tages", "tagen" } },
        { "fr", new[] { "jour", "jours" } }, { "es", new[] { "día", "días", "dia" } }, { "ru", new[] { "день", "дня", "дню", "днём", "дни", "дней" } }
    };

    // True if the word for day stands straight before (or, in Chinese, around) a slot filled with an already written day.
    private static bool Doubled(string words, string slot, string lang)
    {
        int at = words.IndexOf(slot, StringComparison.Ordinal);
        while (at >= 0)
        {
            string before = words.Substring(0, at).TrimEnd();
            string after = words.Substring(at + slot.Length).TrimStart();
            if (DayWords.TryGetValue(lang, out string[] day))
            {
                for (int d = 0; d < day.Length; d++)
                {
                    if (before.EndsWith(" " + day[d], StringComparison.OrdinalIgnoreCase) || before.Equals(day[d], StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            if (lang == "zh-cn" && before.EndsWith("第", StringComparison.Ordinal) && after.StartsWith("天", StringComparison.Ordinal))
            {
                return true;
            }
            at = words.IndexOf(slot, at + slot.Length, StringComparison.Ordinal);
        }
        return false;
    }
}
