using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The officer's file in the save: one line under <see cref="Key"/>, apart from the careers' own, written the way the careers' line is
    /// (Tech/TechCodec). Fields a later version adds at the end of a record read as nothing here. Game-free.
    ///
    ///   h  arrests|tickets|commendations|aid|talked|backups|backed off|hurt|shifts|answered|day|rating|today|count|ring day
    ///   r  the ring: the ratings of the last shifts, oldest first
    /// </summary>
    public static class DutyStore
    {
        public const string Key = "reallife:work_duty";
        public const int Version = 1;

        public static DutyFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(DutyFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Arrests).Add(f.Tickets).Add(f.Commendations).Add(f.Aid).Add(f.Talked).Add(f.Backups).Add(f.BackedOff).Add(f.Hurt).Add(f.Shifts)
                .Add(f.Answered).Add(f.Day).Add(f.Rating).Add(f.Today).Add(f.Count).Add(f.RingDay);
            w.Part('r');
            for (int i = 0; i < f.Count && i < f.Ring.Length; i++)
            {
                w.Record().Add(f.Ring[i]);
            }
            return w.ToString();
        }

        public static DutyFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new DutyFile();
            Fields h = r.One('h');
            f.Arrests = More(h.Int());
            f.Tickets = More(h.Int());
            f.Commendations = More(h.Int());
            f.Aid = More(h.Int());
            f.Talked = More(h.Int());
            f.Backups = More(h.Int());
            f.BackedOff = More(h.Int());
            f.Hurt = More(h.Int());
            f.Shifts = More(h.Int());
            f.Answered = More(h.Int());
            f.Day = h.Int(-1);
            f.Rating = PoliceRules.Clamp(h.Int(PoliceRules.RatingStart));
            f.Today = More(h.Int());
            int count = TalentCatalogue.Clamp(h.Int(), 0, f.Ring.Length);
            f.RingDay = h.Int(-1);
            foreach (Fields x in r.Records('r'))
            {
                if (f.Count < count)
                {
                    f.Ring[f.Count++] = PoliceRules.Clamp(x.Int(PoliceRules.RatingStart));
                }
            }
            return f;
        }

        private static int More(int value) => value < 0 ? 0 : value;
    }
}
