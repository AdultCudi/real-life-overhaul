using RealLifeOverhaul.Careers;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>
    /// The heir's learning in the mod's own education (Careers' study file), with no game in it. Qualifications are a person's own: the founder's passes do not pass on, and the
    /// heir holds the degree the family put them through (a grown child's university, with the class they graduated with). Skills are taught at home and at work, so the heir
    /// starts with their share of the founder's (<see cref="DynastyRules.Upbringing"/>). The founder's courses under way end with them. The student loan is a debt and stays
    /// owed. COST: a pass over the file once a handover.
    /// </summary>
    public static class DynastyStudy
    {
        /// <summary>Makes the study file the heir's. Returns how many of the founder's qualifications did not pass on.</summary>
        public static int HandOver(StudyFile f, string heir, int share, int day)
        {
            if (f == null)
            {
                return 0;
            }
            for (int i = 0; i < f.Skill.Length; i++)
            {
                f.Skill[i] = DynastyRules.Passed(f.Skill[i], share);
                f.Best[i] = DynastyRules.Passed(f.Best[i], share);
            }
            for (int i = f.Courses.Count - 1; i >= 0; i--)
            {
                Enrolment e = f.Courses[i];
                e.Stage = (int)EnrolStage.Withdrawn;
                e.Ended = day;
                f.Finish(e);
            }
            int gone = 0;
            for (int i = f.Results.Count - 1; i >= 0; i--)
            {
                if (f.Results[i].Stage == (int)EnrolStage.Passed)
                {
                    f.Results.RemoveAt(i);
                    gone++;
                }
            }
            f.GraduationDay = 0;
            f.GraduationCourse = -1;
            ChildStudy own = null;
            for (int i = 0; i < f.Children.Count; i++)
            {
                if (f.Children[i].Name == heir)
                {
                    own = f.Children[i];
                }
            }
            if (own != null && own.At == ChildStep.Graduated)
            {
                f.Results.Add(new Enrolment
                {
                    Course = own.Course, Start = own.Start, Ended = own.End, Stage = (int)EnrolStage.Passed, Grade = own.Grade, Graduated = true, SettledDay = own.End
                });
            }
            // The children at university were the founder's: the heir is one of them, and the others are the heir's brothers and sisters, not theirs to put through.
            f.Children.Clear();
            return gone;
        }
    }
}
