using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Permadeath;
using RealLifeOverhaul.Tech;

// Checks for the family and the others (Scripts/Careers): a child's degree and its cost, tutoring's rate and subject,
// an academy's students, a degree sponsored for a professional, the save's line for the children, and the obituary's
// degree and career kept in a life's line.
internal static partial class Program
{
    private static void FamilyStudy()
    {
        CourseDef law = StudyCatalogue.Find(StudyCatalogue.Law);
        Check(StudyRules.ChildCost(law, 0) == law.Fee && StudyRules.ChildCost(law, law.Fee * 2) == 0 && StudyRules.ChildCost(law, 1000) == law.Fee - 1000, "family: the college fund pays first");
        Check(StudyRules.ChildGrade(100f, true, 15) == 4 && StudyRules.ChildGrade(0f, false, -15) == 1 && StudyRules.ChildGrade(80f, false, 0) > StudyRules.ChildGrade(20f, false, 0),
            "family: a child's class follows closeness, school and luck");
        Check(StudyRules.TutorRate(StudyCatalogue.TutorLeast - 1) == 0 && StudyRules.TutorRate(50) == StudyCatalogue.TutorBase + 50 * StudyCatalogue.TutorPerPoint && StudyRules.TutorRate(100) > StudyRules.TutorRate(50),
            "family: tutoring pays by the subject's points, nothing under the least");
        var f = new StudyFile();
        StudyRules.Learn(f, TalentCatalogue.Law, 60000, 1);
        StudyRules.Learn(f, Skills.Hair, 90000, 1);
        Check(StudyRules.TutorSkill(f) == TalentCatalogue.Law, "family: the subject tutored is the best of the professions' and teaching, never a trade");
        Check(StudyRules.AcademyStudents(0, 0) == 0 && StudyRules.AcademyStudents(1, 0) == StudyCatalogue.AcademyPerStaff && StudyRules.AcademyStudents(10, 0) == StudyCatalogue.AcademyDemand + 40
              && StudyRules.AcademyStudents(10, 1) > StudyRules.AcademyStudents(10, -1), "family: an academy teaches what its staff can take of those who come");
        var p = new Profile { Role = TalentCatalogue.Accountant, A = 40000, B = 30000, School = TalentCatalogue.SelfTaught };
        TalentRules.StartDegree(p, 10, 30);
        Check(p.OnCourse(20) && !p.OnCourse(40) && TalentRules.EndCourse(p, 1, 39) == 0, "family: a sponsored degree runs its days");
        Check(TalentRules.EndCourse(p, 1, 40) == 4 && p.A == 40000 + TalentCatalogue.DegreeGain * 1000 && p.B == 30000 + TalentCatalogue.DegreeGain * 1000 && p.School == TalentCatalogue.Bachelor && p.CourseSkill == -1,
            "family: a sponsored degree adds to both skills and makes a bachelor");
        var s = new StudyFile();
        s.Children.Add(new ChildStudy { Name = "Maya", Course = StudyCatalogue.Nursing, Stage = (int)ChildStep.Studying, Start = 3, End = 60, Paid = 4000, Grade = 0 });
        s.TutorDay = 12;
        s.TutorToday = 2;
        int version;
        StudyFile back = CareersStore.Read(CareersStore.Write(s), out version);
        Check(back.Children.Count == 1 && back.Children[0].Name == "Maya" && back.Children[0].End == 60 && back.Children[0].At == ChildStep.Studying && back.TutorToday == 2,
            "family: a child at university and the day's tutoring come back");
        var life = new LifeStory { Name = "Sam", Age = 40, Day = 100, Degree = "LLB Law, First", Career = "Partner at Elm Street Law" };
        var again = new LifeStory();
        Check(again.Read(life.Write()) && again.Degree == "LLB Law, First" && again.Career == "Partner at Elm Street Law", "family: a life's line keeps the degree and the career");
        string old = life.Write();
        old = old.Substring(0, old.Length - ("LLB Law, First|Partner at Elm Street Law|").Length);
        Check(again.Read(old) && again.Degree.Length == 0 && again.Career.Length == 0, "family: a life's line from before the careers reads with neither");
    }
}
