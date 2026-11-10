using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Parties
{
    /// <summary>
    /// The one line parties and company events keep in a save: "v=1|seed|next id|day settled|last invitation|off since",
    /// then one "^" part a record, led by a letter and its fields by "#": P a party (its guests as "person:answer:came"
    /// joined by ","), E a company event, S a manager's schedule, I an invitation, L a lift, R a rival's event. New fields
    /// go at the end of a record and a record with fewer reads the rest as they start, so an older line loads; a part led
    /// by a letter this version does not know, and fields past those it knows, are passed over, so a line from a newer
    /// version is read as far as it goes. Numbers are written and read with the invariant culture. '%', '#', '^', '|',
    /// ',' and ':' in a name are written as %25, %23, %5E, %7C, %2C and %3A. A save with no line starts with nothing; a
    /// line whose head cannot be read gives nothing and is kept as it was read (SaveLines). Game-free.
    /// </summary>
    public static class PartyStore
    {
        public const string Key = "reallife:parties";
        public const int Version = 1;
        private const int RecordsMost = 600;   // parts a line holds: far past what the rules keep
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(PartyFile f)
        {
            var t = new StringBuilder(96 + f.Parties.Count * 96 + f.Events.Count * 64 + f.Lifts.Count * 24);
            t.Append("v=").Append(Version).Append('|').Append(N(f.Seed)).Append('|').Append(N(f.NextId)).Append('|').Append(N(f.Settled)).Append('|').Append(N(f.LastInvite))
             .Append('|').Append(N(f.OffSince));
            for (int i = 0; i < f.Parties.Count; i++)
            {
                Party p = f.Parties[i];
                t.Append("^P#").Append(N(p.Id)).Append('#').Append(N((int)p.Place)).Append('#').Append(N((int)p.Tier)).Append('#').Append(N((int)p.State)).Append('#').Append(N(p.Day))
                 .Append('#').Append(N(p.Hour)).Append('#').Append(Escape(p.Street)).Append('#').Append(N(p.Number)).Append('#').Append(Escape(p.Name)).Append('#').Append(B(p.Own))
                 .Append('#').Append(N(p.Paid)).Append('#').Append(N(p.Began)).Append('#').Append(N(p.Quality)).Append('#').Append(B(p.Late)).Append('#').Append(B(p.Left))
                 .Append('#').Append(B(p.Reminded)).Append('#').Append(N(p.Sqm)).Append('#');
                for (int g = 0; g < p.Guests.Count; g++)
                {
                    t.Append(g > 0 ? "," : string.Empty).Append(N(p.Guests[g].Person)).Append(':').Append(N((int)p.Guests[g].Answer)).Append(':').Append(B(p.Guests[g].Came));
                }
            }
            for (int i = 0; i < f.Events.Count; i++)
            {
                StaffEvent e = f.Events[i];
                t.Append("^E#").Append(N(e.Id)).Append('#').Append(Escape(e.Place)).Append('#').Append(Escape(e.Name)).Append('#').Append(N((int)e.Kind)).Append('#').Append(N((int)e.State))
                 .Append('#').Append(N(e.Day)).Append('#').Append(N(e.Hour)).Append('#').Append(N(e.Paid)).Append('#').Append(N(e.Heads)).Append('#').Append(N(e.Came))
                 .Append('#').Append(N(e.Shut)).Append('#').Append(B(e.ByManager));
            }
            for (int i = 0; i < f.Plans.Count; i++)
            {
                StaffPlan s = f.Plans[i];
                t.Append("^S#").Append(Escape(s.Place)).Append('#').Append(N(s.Every)).Append('#').Append(N((int)s.Kind)).Append('#').Append(N(s.Since)).Append('#').Append(N(s.Tried));
            }
            for (int i = 0; i < f.Invites.Count; i++)
            {
                Invite v = f.Invites[i];
                t.Append("^I#").Append(N(v.Id)).Append('#').Append(N(v.Person)).Append('#').Append(N((int)v.State)).Append('#').Append(N(v.Asked)).Append('#').Append(N(v.Day))
                 .Append('#').Append(N(v.Hour)).Append('#').Append(N((int)v.Place)).Append('#').Append(Escape(v.Street)).Append('#').Append(N(v.Number)).Append('#').Append(Escape(v.Name))
                 .Append('#').Append(N(v.Began)).Append('#').Append(B(v.Reminded));
            }
            for (int i = 0; i < f.Lifts.Count; i++)
            {
                Lift l = f.Lifts[i];
                t.Append("^L#").Append(Escape(l.Id)).Append('#').Append(N(l.Person)).Append('#').Append(N(l.Used)).Append('#').Append(N(l.Day)).Append('#').Append(N(l.Stand));
            }
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                RivalEvent r = f.Rivals[i];
                t.Append("^R#").Append(N(r.Day)).Append('#').Append(N(r.Tycoon)).Append('#').Append(Escape(r.Outlet)).Append('#').Append(Escape(r.District)).Append('#').Append(N((int)r.Kind))
                 .Append('#').Append(N(r.Heads)).Append('#').Append(N(r.Cost));
            }
            return t.ToString();
        }

        /// <summary>The file from a save's data: null with no line; null and <paramref name="version"/> -1 for a line whose head cannot be read.</summary>
        public static PartyFile Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            PartyFile f = Read(text, out version);
            if (f == null)
            {
                version = -1;
            }
            return f;
        }

        /// <summary>
        /// A line read as far as it goes: null only when its head cannot be read. <paramref name="version"/> is the line's own,
        /// which may be later than this one.
        /// </summary>
        public static PartyFile Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            string[] parts = text.Split('^');
            string[] head = parts[0].Split('|');
            if (head.Length < 2 || !head[0].StartsWith("v=", System.StringComparison.Ordinal)
                || !int.TryParse(head[0].Substring(2), NumberStyles.Integer, Inv, out int v) || v < 1 || !int.TryParse(head[1], NumberStyles.Integer, Inv, out int seed))
            {
                return null;
            }
            var f = new PartyFile { Seed = seed };
            f.NextId = System.Math.Max(1, Int(head, 2, 1));
            f.Settled = Int(head, 3, -1);
            f.LastInvite = Int(head, 4, -1000);
            f.OffSince = Int(head, 5, -1);
            for (int i = 1; i < parts.Length && i <= RecordsMost; i++)
            {
                string[] x = parts[i].Split('#');
                if (x.Length < 2 || x[0].Length != 1)
                {
                    continue;   // not a part this version knows how to read
                }
                switch (x[0][0])
                {
                    case 'P': ReadParty(f, x); break;
                    case 'E': ReadEvent(f, x); break;
                    case 'S': ReadPlan(f, x); break;
                    case 'I': ReadInvite(f, x); break;
                    case 'L': ReadLift(f, x); break;
                    case 'R': ReadRival(f, x); break;
                }
            }
            for (int i = 0; i < f.Parties.Count; i++)
            {
                f.NextId = System.Math.Max(f.NextId, f.Parties[i].Id + 1);
            }
            for (int i = 0; i < f.Events.Count; i++)
            {
                f.NextId = System.Math.Max(f.NextId, f.Events[i].Id + 1);
            }
            for (int i = 0; i < f.Invites.Count; i++)
            {
                f.NextId = System.Math.Max(f.NextId, f.Invites[i].Id + 1);
            }
            version = v;
            return f;
        }

        private static void ReadParty(PartyFile f, string[] x)
        {
            int id = Int(x, 1, 0);
            if (id <= 0 || f.Find(id) != null)
            {
                return;
            }
            var p = new Party
            {
                Id = id, Place = (PartyPlace)PartyCatalogue.Clamp(Int(x, 2, 0), 0, 2), Tier = (PartyTier)PartyCatalogue.Clamp(Int(x, 3, 0), 0, 2), State = (PartyState)Int(x, 4, 0),
                Day = Int(x, 5, 0), Hour = PartyCatalogue.Clamp(Int(x, 6, 0), 0, 23), Street = Text(x, 7), Number = Int(x, 8, 0), Name = Text(x, 9), Own = Int(x, 10, 0) == 1,
                Paid = System.Math.Max(0, Int(x, 11, 0)), Began = Int(x, 12, -1), Quality = PartyCatalogue.Clamp(Int(x, 13, 0), 0, 100), Late = Int(x, 14, 0) == 1,
                Left = Int(x, 15, 0) == 1, Reminded = Int(x, 16, 0) == 1, Sqm = Int(x, 17, 0)
            };
            string[] guests = x.Length > 18 && x[18].Length > 0 ? x[18].Split(',') : new string[0];
            for (int g = 0; g < guests.Length; g++)
            {
                string[] one = guests[g].Split(':');
                if (one.Length >= 2 && int.TryParse(one[0], NumberStyles.Integer, Inv, out int person) && person >= 0)
                {
                    p.Guests.Add(new PartyGuest { Person = person, Answer = (PartyAnswer)Int(one, 1, 0), Came = Int(one, 2, 0) == 1 });
                }
            }
            f.Parties.Add(p);
        }

        private static void ReadEvent(PartyFile f, string[] x)
        {
            int id = Int(x, 1, 0);
            string place = Text(x, 2);
            if (id <= 0 || place.Length == 0 || f.Event(id) != null)
            {
                return;
            }
            f.Events.Add(new StaffEvent
            {
                Id = id, Place = place, Name = Text(x, 3), Kind = (StaffEventKind)PartyCatalogue.Clamp(Int(x, 4, 0), 0, 2), State = (StaffEventState)Int(x, 5, 0), Day = Int(x, 6, 0),
                Hour = PartyCatalogue.Clamp(Int(x, 7, 0), 0, 23), Paid = System.Math.Max(0, Int(x, 8, 0)), Heads = Int(x, 9, 0), Came = Int(x, 10, 0), Shut = Int(x, 11, 0),
                ByManager = Int(x, 12, 0) == 1
            });
        }

        private static void ReadPlan(PartyFile f, string[] x)
        {
            string place = Text(x, 1);
            if (place.Length == 0 || f.Plan(place, false) != null)
            {
                return;
            }
            StaffPlan s = f.Plan(place, true);
            s.Every = System.Math.Max(0, Int(x, 2, 0));
            s.Kind = (StaffEventKind)PartyCatalogue.Clamp(Int(x, 3, 0), 0, 2);
            s.Since = Int(x, 4, 0);
            s.Tried = Int(x, 5, -1000);
        }

        private static void ReadInvite(PartyFile f, string[] x)
        {
            int id = Int(x, 1, 0), person = Int(x, 2, -1);
            if (id <= 0 || person < 0 || f.Invitation(id) != null)
            {
                return;
            }
            f.Invites.Add(new Invite
            {
                Id = id, Person = person, State = (InviteState)Int(x, 3, 0), Asked = Int(x, 4, 0), Day = Int(x, 5, 0), Hour = PartyCatalogue.Clamp(Int(x, 6, 0), 0, 23),
                Place = (PartyPlace)PartyCatalogue.Clamp(Int(x, 7, 1), 0, 2), Street = Text(x, 8), Number = Int(x, 9, 0), Name = Text(x, 10), Began = Int(x, 11, -1),
                Reminded = Int(x, 12, 0) == 1
            });
        }

        private static void ReadLift(PartyFile f, string[] x)
        {
            string id = Text(x, 1);
            int person = Int(x, 2, -1), used = PartyCatalogue.Clamp(Int(x, 3, 0), 0, 1000);
            if ((id.Length == 0) == (person < 0))
            {
                return;   // neither a person nor an employee, or both
            }
            Lift lift = id.Length > 0 ? f.Of(id, true) : f.Of(person, true);
            lift.Used = used;
            lift.Day = Int(x, 4, 0);
            lift.Stand = PartyCatalogue.Clamp(Int(x, 5, 0), 0, PartyCatalogue.CapStanding);
        }

        private static void ReadRival(PartyFile f, string[] x)
        {
            string outlet = Text(x, 3);
            if (outlet.Length == 0)
            {
                return;
            }
            f.Rivals.Add(new RivalEvent
            {
                Day = Int(x, 1, 0), Tycoon = Int(x, 2, 0), Outlet = outlet, District = Text(x, 4), Kind = (StaffEventKind)PartyCatalogue.Clamp(Int(x, 5, 0), 0, 2), Heads = Int(x, 6, 0),
                Cost = System.Math.Max(0, Int(x, 7, 0))
            });
        }

        private static string N(int value) => value.ToString(Inv);

        private static char B(bool value) => value ? '1' : '0';

        private static int Int(string[] x, int i, int otherwise)
        {
            return i < x.Length && int.TryParse(x[i], NumberStyles.Integer, Inv, out int v) ? v : otherwise;
        }

        private static string Text(string[] x, int i) => i < x.Length ? Unescape(x[i]) : string.Empty;

        private static string Escape(string s)
        {
            return (s ?? string.Empty).Replace("%", "%25").Replace("#", "%23").Replace("^", "%5E").Replace("|", "%7C").Replace(",", "%2C").Replace(":", "%3A");
        }

        private static string Unescape(string s)
        {
            return s.Replace("%3A", ":").Replace("%2C", ",").Replace("%7C", "|").Replace("%5E", "^").Replace("%23", "#").Replace("%25", "%");
        }
    }
}
