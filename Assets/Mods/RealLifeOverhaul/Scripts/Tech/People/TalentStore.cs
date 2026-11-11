using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The professionals in the save: one line under <see cref="Key"/> in <see cref="TechWriter"/>'s format, a header (h) and
    /// the files (p) and the moves followed (m, which an older version of the mod passes over), fields in the order
    /// <see cref="Write"/> writes them. A file is kept for every professional the game holds, however many (a save of a player's own grows by some forty
    /// employees a day, so any number would be one a game reached in the end: it was 600, then 10,000), and a read of the line stays linear in its
    /// size. Only the mod's own payroll, which the player adds to one decision at a time, is bounded, by <see cref="PayrollMost"/>.
    /// </summary>
    public static class TalentStore
    {
        public const string Key = "reallife:tech_people";
        public const int Version = 1;
        public const int PayrollMost = 10000;   // files of the mod's own payroll (rack crews, remote staff, a startup's chief), each paid by a booking a day; the game's employees have no bound

        public static TalentFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(TalentFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.FirstDay).Add(f.LastDay).Add(f.NextPro);
            w.Part('p');
            for (int i = 0; i < f.People.Count; i++)
            {
                Profile p = f.People[i];
                w.Record().Add(p.Id).Add(p.Role).Add(p.Level).Add(p.A).Add(p.B).Add(p.Main).Add(p.YearsDays).Add(p.School).Add(p.Certs).Add(p.Morale).Add(p.Burnout)
                    .Add(p.Joined).Add(p.LevelDay).Add(p.ReviewDay).Add(p.Rating).Add(p.LowRuns).Add(p.Work).Add(p.NonCompete).Add(p.NoticeEnd).Add(p.Leaving)
                    .Add(p.CourseSkill).Add(p.CourseEnd).Add(p.Exam).Add(p.RaiseAsk).Add(p.RecognisedDay).Add(p.PoachDay).Add(p.PoachBp).Add(p.PoachBy).Place(p.Place)
                    .Add(p.Options).Add(p.Record).Add(p.Name).Add(p.Wage).Add(p.Female).Add(p.Age).Add(p.Sick).Add(p.AskEnd)
                    .Add(p.WorkDay).Add(p.WorkHours).Add(p.PrevDay).Add(p.PrevHours);
            }
            w.Part('m');
            for (int i = 0; i < f.Moves.Count && i < MoveRules.RecordsMost; i++)
            {
                MoveRecord m = f.Moves[i];
                w.Record().Place(m.From).Place(m.To).Add(m.Day).Add(m.Hour).Add(m.Parts).Add(m.Whole).Add(m.Replaced);
            }
            return w.ToString();
        }

        public static TalentFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new TalentFile();
            var seen = new HashSet<string>(StringComparer.Ordinal);   // the ids read, so a file's id is looked up in one step, not by a pass over the files
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
                if (p.Id.Length > 0 && seen.Add(p.Id))
                {
                    f.People.Add(p);
                }
            }
            foreach (Fields x in r.Records('m'))
            {
                var m = new MoveRecord { From = x.Place(), To = x.Place(), Day = x.Int(), Hour = x.Int(), Parts = x.Int() & MoveRules.PartsAll, Whole = x.Bool(), Replaced = x.Bool() };
                if (m.From.Length > 0 && m.To.Length > 0 && m.From != m.To && f.Moves.Count < MoveRules.RecordsMost)
                {
                    f.Moves.Add(m);
                }
            }
            return f;
        }
    }
}
