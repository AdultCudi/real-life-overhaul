using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The campus's people in the save: one line under <see cref="Key"/>, apart from the careers' own so a save from before it keeps
    /// both as they were. Fields a later version adds at the end of a record read as nothing here. Game-free.
    ///
    ///   h  seed
    ///   i  each cohort: course|start|capacity|player in|students: name|gpa|employee, repeated, then each student's person id (an older save has none)
    ///   t  each tutor: course|name|years|rating|person id
    /// </summary>
    public static class RosterStore
    {
        public const string Key = "reallife:study_roster";
        public const int Version = 1;

        public static RosterFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(RosterFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed);
            w.Part('i');
            for (int k = 0; k < f.Intakes.Count && k < RosterRules.IntakesMost; k++)
            {
                Intake i = f.Intakes[k];
                if (!RosterRules.Keeps(f.Seed, i))
                {
                    continue;   // a cohort of strangers is made again as it was (RosterRules.NameOf), so only the ones the player or an employee of theirs sits in are kept
                }
                w.Record().Add(i.Course).Add(i.Start).Add(i.Capacity).Add(i.PlayerIn).Add(i.Students.Count);
                for (int s = 0; s < i.Students.Count; s++)
                {
                    w.Add(i.Students[s].Name).Add(i.Students[s].Gpa).Add(i.Students[s].Employee);
                }
                for (int s = 0; s < i.Students.Count; s++)
                {
                    w.Add(i.Students[s].PersonId);   // at the end of the record: an older save's has none
                }
            }
            w.Part('t');
            for (int k = 0; k < f.Tutors.Count && k < RosterRules.TutorsMost; k++)
            {
                w.Record().Add(f.Tutors[k].Course).Add(f.Tutors[k].Name).Add(f.Tutors[k].Years).Add(f.Tutors[k].Rating).Add(f.Tutors[k].PersonId);
            }
            return w.ToString();
        }

        public static RosterFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new RosterFile();
            f.Seed = r.One('h').Int(1);
            foreach (Fields x in r.Records('i'))
            {
                var i = new Intake { Course = x.Int(), Start = x.Int(), Capacity = TalentCatalogue.Clamp(x.Int(RosterRules.SeatsDefault), 1, 200),
                    PlayerIn = x.Bool() };
                int n = TalentCatalogue.Clamp(x.Int(), 0, i.Capacity);
                for (int s = 0; s < n; s++)
                {
                    i.Students.Add(new Student { Name = x.Text(), Gpa = TalentCatalogue.Clamp(x.Int(), 0, 400), Employee = x.Bool() });
                }
                for (int s = 0; s < n; s++)
                {
                    i.Students[s].PersonId = Math.Max(0, x.Int());
                }
                if (StudyCatalogue.Find(i.Course) != null && f.Intakes.Count < RosterRules.IntakesMost)
                {
                    f.Intakes.Add(i);
                }
            }
            foreach (Fields x in r.Records('t'))
            {
                var t = new Tutor { Course = x.Int(), Name = x.Text(), Years = TalentCatalogue.Clamp(x.Int(), 0, 60),
                    Rating = TalentCatalogue.Clamp(x.Int(30), 10, 50) };
                t.PersonId = Math.Max(0, x.Int());
                if (StudyCatalogue.Find(t.Course) != null && f.Tutors.Count < RosterRules.TutorsMost)
                {
                    f.Tutors.Add(t);
                }
            }
            return f;
        }
    }
}
