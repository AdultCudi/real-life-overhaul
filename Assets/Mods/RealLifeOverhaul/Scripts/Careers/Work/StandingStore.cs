using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The player's standing at work in the save: one line under <see cref="Key"/>, apart from the careers' own so a save from before it,
    /// or one loaded by an older build, keeps both as they were. Written the way the careers' line is (Tech/TechCodec); fields a later
    /// version adds at the end of a record read as nothing here. Game-free.
    ///
    ///   h  hired|street|number|employer|seed|week|count|ring (12 whole numbers)
    ///   p  each colleague: name|rank|skill|day|post|role|score|punctuality|reliability|teamwork|count|ring (12)
    ///   o  each other applicant: the same
    ///   r  each recent shift: day|minutes|score|pay
    /// </summary>
    public static class StandingStore
    {
        public const string Key = "reallife:work_standing";
        public const int Version = 1;

        public static StandingFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(StandingFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h');
            w.Record().Add(f.Hired).Add(f.Street).Add(f.Number).Add(f.Employer).Add(f.Seed).Add(f.WeekOf).Add(f.Count);
            Ring(w, f.Ring);
            w.Part('p');
            for (int i = 0; i < f.Peers.Count && i < StandingRules.PeersMost; i++)
            {
                One(w, f.Peers[i]);
            }
            w.Part('o');
            for (int i = 0; i < f.Others.Count && i < StandingRules.OthersMost; i++)
            {
                One(w, f.Others[i]);
            }
            w.Part('r');
            for (int i = 0; i < f.Shifts.Count && i < StandingRules.ShiftsMost; i++)
            {
                w.Record().Add(f.Shifts[i].Day).Add(f.Shifts[i].Minutes).Add(f.Shifts[i].Score).Add(f.Shifts[i].Pay);
            }
            return w.ToString();
        }

        private static void Ring(TechWriter w, int[] ring)
        {
            for (int i = 0; i < ring.Length; i++)
            {
                w.Add(ring[i]);
            }
        }

        private static void One(TechWriter w, Peer p)
        {
            w.Record().Add(p.Name).Add(p.Rank).Add(p.Skill).Add(p.Day).Add(p.Post).Add(p.Role).Add(p.Score).Add(p.Punctuality).Add(p.Reliability).Add(p.Teamwork).Add(p.Count);
            Ring(w, p.Ring);
        }

        public static StandingFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new StandingFile();
            Fields h = r.One('h');
            f.Hired = h.Int();
            f.Street = h.Text();
            f.Number = h.Int();
            f.Employer = h.Text();
            f.Seed = h.Int(1);
            f.WeekOf = h.Int(-1);
            f.Count = TalentCatalogue.Clamp(h.Int(), 0, Peer.RingSize);
            for (int i = 0; i < Peer.RingSize; i++)
            {
                f.Ring[i] = Score(h.Int());
            }
            foreach (Fields x in r.Records('p'))
            {
                if (f.Peers.Count < StandingRules.PeersMost)
                {
                    f.Peers.Add(ReadPeer(x));
                }
            }
            foreach (Fields x in r.Records('o'))
            {
                if (f.Others.Count < StandingRules.OthersMost)
                {
                    f.Others.Add(ReadPeer(x));
                }
            }
            foreach (Fields x in r.Records('r'))
            {
                if (f.Shifts.Count < StandingRules.ShiftsMost)
                {
                    f.Shifts.Add(new ShiftRecord { Day = x.Int(), Minutes = System.Math.Max(0, x.Int()), Score = Score(x.Int()), Pay = System.Math.Max(0, x.Int()) });
                }
            }
            return f;
        }

        private static int Score(int value) => TalentCatalogue.Clamp(value, 0, 100);

        private static Peer ReadPeer(Fields x)
        {
            var p = new Peer
            {
                Name = x.Text(), Rank = TalentCatalogue.Clamp(x.Int(), 0, 20), Skill = Score(x.Int()), Day = x.Int(), Post = x.Int(),
                Role = TalentCatalogue.Clamp(x.Int(), 0, (int)PeerRole.Applicant), Score = Score(x.Int()), Punctuality = Score(x.Int()),
                    Reliability = Score(x.Int()),
                Teamwork = Score(x.Int())
            };
            p.Count = TalentCatalogue.Clamp(x.Int(), 0, Peer.RingSize);
            for (int i = 0; i < Peer.RingSize; i++)
            {
                p.Ring[i] = Score(x.Int());
            }
            return p;
        }
    }
}
