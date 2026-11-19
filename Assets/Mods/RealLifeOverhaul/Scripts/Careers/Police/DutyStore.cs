using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The officer's file in the save: one line under <see cref="Key"/>, apart from the careers' own, written the way the careers' line is
    /// (Tech/TechCodec). Fields a later version adds at the end of a record read as nothing here. Game-free.
    ///
    ///   h  arrests|tickets|commendations|aid|talked|backups|backed off|hurt|shifts|answered|day|rating|today|count|ring day
    ///      then, appended: next case|actions today|notice day|pending opened|released|convicted|acquitted|commended
    ///   r  the ring: the ratings of the last shifts, oldest first
    ///   c  each case of the book: id|name|person|offence|district|day|hearing|quality|strength|worked day|done|witness|summoned|end|end day|
    ///      sentence
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
                .Add(f.Answered).Add(f.Day).Add(f.Rating).Add(f.Today).Add(f.Count).Add(f.RingDay).Add(f.NextCase).Add(f.ActionsToday).Add(f.NoticeDay).Add(f.PendOpened)
                .Add(f.PendReleased).Add(f.PendConvicted).Add(f.PendAcquitted).Add(f.PendCommended);
            w.Part('r');
            for (int i = 0; i < f.Count && i < f.Ring.Length; i++)
            {
                w.Record().Add(f.Ring[i]);
            }
            w.Part('c');
            for (int i = 0; i < f.Cases.Count && i < CaseRules.Most; i++)
            {
                OfficerCase c = f.Cases[i];
                w.Record().Add(c.Id).Add(c.Name).Add(c.PersonId).Add(c.Offence).Add(c.District).Add(c.Day).Add(c.Hearing).Add(c.Quality).Add(c.Strength).Add(c.WorkedDay).Add(c.Done)
                    .Add(c.Witness).Add(c.Summoned ? 1 : 0).Add((int)c.End).Add(c.EndDay).Add(c.Sentence);
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
            f.NextCase = Math.Max(1, h.Int(1));
            f.ActionsToday = More(h.Int());
            f.NoticeDay = h.Int(-1);
            f.PendOpened = More(h.Int());
            f.PendReleased = More(h.Int());
            f.PendConvicted = More(h.Int());
            f.PendAcquitted = More(h.Int());
            f.PendCommended = More(h.Int());
            foreach (Fields x in r.Records('r'))
            {
                if (f.Count < count)
                {
                    f.Ring[f.Count++] = PoliceRules.Clamp(x.Int(PoliceRules.RatingStart));
                }
            }
            foreach (Fields x in r.Records('c'))
            {
                if (f.Cases.Count >= CaseRules.Most)
                {
                    break;
                }
                var c = new OfficerCase { Id = x.Int(), Name = x.Text(), PersonId = More(x.Int()), Offence = x.Text(), District = x.Text(), Day = x.Int(), Hearing = More(x.Int()) };
                c.Quality = TalentCatalogue.Clamp(x.Int(), 0, 100);
                c.Strength = TalentCatalogue.Clamp(x.Int(), 0, 100);
                c.WorkedDay = x.Int(-1);
                c.Done = More(x.Int());
                c.Witness = x.Int();
                c.Summoned = x.Int() != 0;
                c.End = (CaseEnd)TalentCatalogue.Clamp(x.Int(), 0, (int)CaseEnd.Pleaded);
                c.EndDay = x.Int();
                c.Sentence = More(x.Int());
                if (c.Id > 0 && c.Offence.Length > 0)
                {
                    f.Cases.Add(c);
                }
            }
            return f;
        }

        private static int More(int value) => value < 0 ? 0 : value;
    }
}
