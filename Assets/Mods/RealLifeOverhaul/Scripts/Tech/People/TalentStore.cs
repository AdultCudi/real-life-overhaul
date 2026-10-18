using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The professionals in the save: one line under <see cref="Key"/> in <see cref="TechWriter"/>'s format, a header (h) and
    /// the files (p), fields in the order <see cref="Write"/> writes them; at most <see cref="PeopleMost"/> professionals.
    /// </summary>
    public static class TalentStore
    {
        public const string Key = "reallife:tech_people";
        public const int Version = 1;
        public const int PeopleMost = 600;

        public static TalentFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(TalentFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.FirstDay).Add(f.LastDay).Add(f.NextPro);
            w.Part('p');
            for (int i = 0; i < f.People.Count && i < PeopleMost; i++)
            {
                Profile p = f.People[i];
                w.Record().Add(p.Id).Add(p.Role).Add(p.Level).Add(p.A).Add(p.B).Add(p.Main).Add(p.YearsDays).Add(p.School).Add(p.Certs).Add(p.Morale).Add(p.Burnout)
                    .Add(p.Joined).Add(p.LevelDay).Add(p.ReviewDay).Add(p.Rating).Add(p.LowRuns).Add(p.Work).Add(p.NonCompete).Add(p.NoticeEnd).Add(p.Leaving)
                    .Add(p.CourseSkill).Add(p.CourseEnd).Add(p.Exam).Add(p.RaiseAsk).Add(p.RecognisedDay).Add(p.PoachDay).Add(p.PoachBp).Add(p.PoachBy).Place(p.Place)
                    .Add(p.Options).Add(p.Record).Add(p.Name).Add(p.Wage).Add(p.Female).Add(p.Age).Add(p.Sick).Add(p.AskEnd)
                    .Add(p.WorkDay).Add(p.WorkHours).Add(p.PrevDay).Add(p.PrevHours);
            }
            return w.ToString();
        }

        public static TalentFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new TalentFile();
            Fields h = r.One('h');
            f.Seed = h.Int(1);
            f.FirstDay = h.Int();
            f.LastDay = h.Int(-1);
            f.NextPro = Math.Max(1, h.Int(1));
            foreach (Fields x in r.Records('p'))
            {
                var p = new Profile
                {
                    Id = x.Text(),
                    Role = TalentCatalogue.Clamp(x.Int(), 0, TalentCatalogue.RoleCount - 1),
                    Level = TalentCatalogue.Clamp(x.Int(), 0, TalentCatalogue.Levels - 1),
                    A = x.Int(),
                    B = x.Int(),
                    Main = x.Int(),
                    YearsDays = x.Int(),
                    School = x.Int(),
                    Certs = x.Int(),
                    Morale = x.Int(TalentCatalogue.MoraleStart),
                    Burnout = x.Int(),
                    Joined = x.Int(),
                    LevelDay = x.Int(),
                    ReviewDay = x.Int(),
                    Rating = x.Int(),
                    LowRuns = x.Int(),
                    Work = x.Int(),
                    NonCompete = x.Bool(),
                    NoticeEnd = x.Int(),
                    Leaving = x.Int(),
                    CourseSkill = x.Int(-1),
                    CourseEnd = x.Int(),
                    Exam = x.Int(-1),
                    RaiseAsk = x.Int(),
                    RecognisedDay = x.Int(-1000),
                    PoachDay = x.Int(),
                    PoachBp = x.Int(),
                    PoachBy = x.Text(),
                    Place = x.Place(),
                    Options = x.Int(),
                    Record = x.Bool(),
                    Name = x.Text(),
                    Wage = x.Int(),
                    Female = x.Bool(),
                    Age = x.Int(30),
                    Sick = x.Bool(),
                    AskEnd = x.Int(),
                    WorkDay = x.Int(-1),
                    WorkHours = Math.Max(0, x.Int()),
                    PrevDay = x.Int(-1),
                    PrevHours = Math.Max(0, x.Int())
                };
                if (p.Id.Length > 0 && f.Of(p.Id) == null && f.People.Count < PeopleMost)
                {
                    f.People.Add(p);
                }
            }
            return f;
        }
    }
}
