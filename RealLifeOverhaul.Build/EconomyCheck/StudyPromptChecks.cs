using RealLifeOverhaul.Careers;

// Where a class or an exam is offered in place (Scripts/Careers/Study/StudyRules: AtClass, AtExam, Prompt, ClassNotice,
// NextClass): the panel at the university and on the wards, the school's desks, the notice at class time.
internal static partial class Program
{
    private static void StudyPrompts()
    {
        CourseDef cs = StudyCatalogue.Find(StudyCatalogue.ComputerScience);   // the university, Mon Wed Fri at 10
        CourseDef aid = StudyCatalogue.Find(StudyCatalogue.FirstAid);         // the school
        CourseDef nursing = StudyCatalogue.Find(StudyCatalogue.Nursing);      // the wards
        CourseDef boot = StudyCatalogue.Find(StudyCatalogue.CodingBootcamp);  // online
        var nowhere = new StudyHere();
        var uni = new StudyHere { University = true };
        var school = new StudyHere { School = true };
        var ward = new StudyHere { Hospital = true };
        var home = new StudyHere { Home = true, Online = true };
        var homeOff = new StudyHere { Home = true };

        // ---------------------------------------------------------------- where a class is joined
        Check(StudyRules.AtClass(cs, StudyMode.FullTime, uni) == null && StudyRules.AtClass(cs, StudyMode.FullTime, nowhere) == "reallife_study_why_not_university"
              && StudyRules.AtClass(cs, StudyMode.FullTime, school) == "reallife_study_why_not_university",
            "study prompt: a degree's class is offered at the university only (its sign, door or inside), and says where to go elsewhere");
        Check(StudyRules.AtClass(aid, StudyMode.FullTime, school) == null && StudyRules.AtClass(aid, StudyMode.FullTime, uni) == "reallife_study_why_not_school",
            "study prompt: a short course's class is at the school, not at the university next door");
        Check(StudyRules.AtClass(nursing, StudyMode.FullTime, ward) == null && StudyRules.AtClass(nursing, StudyMode.FullTime, school) == "reallife_study_why_not_hospital",
            "study prompt: nursing on a ward of a hospital or clinic");
        Check(StudyRules.AtClass(boot, StudyMode.FullTime, home) == null && StudyRules.AtClass(boot, StudyMode.FullTime, homeOff) == "reallife_study_why_no_internet"
              && StudyRules.AtClass(boot, StudyMode.FullTime, school) == "reallife_study_why_not_home"
              && StudyRules.AtClass(cs, StudyMode.Distance, home) == null && StudyRules.AtClass(cs, StudyMode.Distance, uni) == "reallife_study_why_not_home",
            "study prompt: an online class, or one followed from home, at a home of yours with working internet");

        // ---------------------------------------------------------------- where an exam is sat
        Check(StudyRules.AtExam(cs, StudyMode.FullTime, uni) == null && StudyRules.AtExam(boot, StudyMode.FullTime, school) == null
              && StudyRules.AtExam(boot, StudyMode.FullTime, home) == "reallife_study_why_exam_school" && StudyRules.AtExam(cs, StudyMode.Distance, school) == null,
            "study prompt: exams where the classes are; online and from-home courses at the school's desks");

        // ---------------------------------------------------------------- what the panel offers
        Check(StudyRules.Prompt(null, null, false) == StudyPrompt.Class && StudyRules.Prompt("x", null, false) == StudyPrompt.Exam
              && StudyRules.Prompt("x", "y", false) == StudyPrompt.None && StudyRules.Prompt(null, null, true) == StudyPrompt.None,
            "study prompt: a class first, then an exam, nothing while the player is busy");

        // ---------------------------------------------------------------- the notice at class time
        Check(StudyRules.ClassNotice(null) == 1 && StudyRules.ClassNotice("reallife_study_why_no_class") == 0
              && StudyRules.ClassNotice("reallife_study_why_not_university") == 2 && StudyRules.ClassNotice("reallife_study_why_not_school") == 2
              && StudyRules.ClassNotice("reallife_study_why_fee_owed") == 2,
            "study prompt: at class time, 'press the key' where it can be joined, 'go there' (with why) where not, nothing without a class");

        // ---------------------------------------------------------------- the next class, for a desk outside the window
        int day = 1;
        while (!StudyRules.On(StudyRules.Days(cs, StudyMode.FullTime), day))
        {
            day++;
        }
        int start = StudyRules.Hour(cs, StudyMode.FullTime) * 60;
        Check(StudyRules.NextClass(cs, StudyMode.FullTime, day, start - 120, day) == day * 1440 + start
              && StudyRules.NextClass(cs, StudyMode.FullTime, day, start + StudyCatalogue.LateMinutes, day) == day * 1440 + start,
            "study prompt: today's class is the next one until it can no longer be joined");
        int after = StudyRules.NextClass(cs, StudyMode.FullTime, day, start + StudyCatalogue.LateMinutes + 1, day);
        Check(after > (day + 1) * 1440 - 1 && StudyRules.On(StudyRules.Days(cs, StudyMode.FullTime), after / 1440) && after % 1440 == start,
            "study prompt: after the window, the next class day at its hour");
        Check(StudyRules.NextClass(cs, StudyMode.FullTime, day, 0, day + 10) >= (day + 10) * 1440 && StudyRules.NextClass(null, StudyMode.FullTime, day, 0, day) == -1,
            "study prompt: not before the course starts; nothing for no course");
        int joinMinutes;
        Check(StudyRules.Joinable(cs, StudyMode.FullTime, day, start - StudyCatalogue.ArriveEarlyMinutes, out joinMinutes) && joinMinutes == cs.Hours * 60
              && !StudyRules.Joinable(cs, StudyMode.FullTime, day, start - StudyCatalogue.ArriveEarlyMinutes - 1, out joinMinutes),
            "study prompt: the panel and the desk's button appear half an hour before the class and the whole class is left");
    }
}
