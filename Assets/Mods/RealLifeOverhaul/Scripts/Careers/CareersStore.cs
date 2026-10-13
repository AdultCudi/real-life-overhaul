using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Education and careers in the save: one line of the save's modData under <see cref="Key"/>, written the way the tech
    /// sector writes its lines (Tech/TechCodec: parts by letter, records, fields; a later version's extra fields at the end
    /// of a record are read by this one as nothing). Game-free.
    ///
    ///     v=1~h:seed|first day|last day|loan owed|loan taken|loan repaid|week|week minutes|graduation day|graduation course|taught~s:...~c:...~r:...
    ///
    ///   s  each skill the player has or had: skill|thousandths|best|day last used
    ///   c  each course under way, r each one finished: course|mode|start|seen|done|settled day|homework|fee|instalments|loan|on loan|
    ///      grant|stage|exam from|exam to|retakes|score|grade|ended|graduated|fee late
    ///   w, o, a, j, y  the working life (Careers/Work/WorkStore)
    /// </summary>
    public static class CareersStore
    {
        public const string Key = "reallife:careers";
        public const string NewsKey = "reallife:careers_news";
        public const int Version = 1;

        public static StudyFile Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                return Read(text, out version);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string Write(StudyFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.FirstDay).Add(f.LastDay).Add(f.LoanCents).Add(f.LoanTaken).Add(f.LoanRepaid).Add(f.WeekOf).Add(f.WeekMinutes)
                .Add(f.GraduationDay).Add(f.GraduationCourse).Add(f.Taught);
            w.Part('s');
            for (int s = 0; s < Skills.Count; s++)
            {
                if (f.Skill[s] > 0 || f.Best[s] > 0)
                {
                    w.Record().Add(s).Add(f.Skill[s]).Add(f.Best[s]).Add(f.Used[s]);
                }
            }
            w.Part('c');
            for (int i = 0; i < f.Courses.Count; i++)
            {
                Course(w, f.Courses[i]);
            }
            w.Part('r');
            for (int i = 0; i < f.Results.Count; i++)
            {
                Course(w, f.Results[i]);
            }
            WorkStore.Write(w, f.Work);
            return w.ToString();
        }

        private static void Course(TechWriter w, Enrolment e)
        {
            w.Record().Add(e.Course).Add(e.Mode).Add(e.Start).Add(e.Seen).Add(e.Done).Add(e.SettledDay).Add(e.Homework).Add(e.Fee).Add(e.Instalments).Add(e.Loan).Add(e.OnLoan)
                .Add(e.Grant).Add(e.Stage).Add(e.ExamFrom).Add(e.ExamTo).Add(e.Retakes).Add(e.Score).Add(e.Grade).Add(e.Ended).Add(e.Graduated).Add(e.FeeLate);
        }

        public static StudyFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new StudyFile();
            Fields h = r.One('h');
            f.Seed = Math.Max(1, h.Int(1));
            f.FirstDay = h.Int();
            f.LastDay = h.Int(-1);
            f.LoanCents = Math.Max(0, h.Long());
            f.LoanTaken = Math.Max(0, h.Long());
            f.LoanRepaid = Math.Max(0, h.Long());
            f.WeekOf = h.Int(-1);
            f.WeekMinutes = Math.Max(0, h.Int());
            f.GraduationDay = h.Int();
            f.GraduationCourse = h.Int(-1);
            f.Taught = Math.Max(0, h.Int());
            foreach (Fields x in r.Records('s'))
            {
                int s = x.Int(-1);
                if (s >= 0 && s < Skills.Count)
                {
                    f.Skill[s] = TalentCatalogue.Clamp(x.Int(), 0, 100000);
                    f.Best[s] = TalentCatalogue.Clamp(x.Int(), f.Skill[s], 100000);
                    f.Used[s] = x.Int();
                }
            }
            foreach (Fields x in r.Records('c'))
            {
                Enrolment e = Course(x);
                if (e != null && e.Live && f.Live(e.Course) == null && f.Courses.Count < StudyRules.LiveMost)
                {
                    f.Courses.Add(e);
                }
            }
            foreach (Fields x in r.Records('r'))
            {
                Enrolment e = Course(x);
                if (e != null && !e.Live && f.Results.Count < StudyFile.ResultsMost)
                {
                    f.Results.Add(e);
                }
            }
            WorkStore.Read(r, f.Work);
            return f;
        }

        private static Enrolment Course(Fields x)
        {
            var e = new Enrolment
            {
                Course = x.Int(-1),
                Mode = TalentCatalogue.Clamp(x.Int(), 0, 2),
                Start = x.Int(),
                Seen = Math.Max(0, x.Int()),
                Done = Math.Max(0, x.Int()),
                SettledDay = x.Int(-1),
                Homework = Math.Max(0, x.Int()),
                Fee = Math.Max(0, x.Int()),
                Instalments = Math.Max(0, x.Int()),
                Loan = Math.Max(0, x.Int()),
                OnLoan = x.Bool(),
                Grant = x.Int(),
                Stage = TalentCatalogue.Clamp(x.Int(), 0, (int)EnrolStage.Retake),
                ExamFrom = x.Int(),
                ExamTo = x.Int(),
                Retakes = Math.Max(0, x.Int()),
                Score = x.Int(),
                Grade = x.Int(),
                Ended = x.Int(),
                Graduated = x.Bool(),
                FeeLate = x.Bool()
            };
            return e.Def != null ? e : null;
        }
    }
}
