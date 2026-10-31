using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Sessions
{
    /// <summary>
    /// The sessions' line in a save: "v=1|seed|reminded" then one "^" part a record, its fields by "#": place, session,
    /// held, delegated, by, a, b, until, times. New fields go at the end of a record; a record with fewer reads the rest as
    /// they start. '%', '#', '^' and '|' in a place are %25, %23, %5E and %7C. A line of a later version, or unreadable,
    /// gives nothing and is kept as it was read (SaveLines). Game-free.
    /// </summary>
    public static class SessionStore
    {
        public const string Key = "reallife:sessions";
        public const int Version = 1;
        private const int RecordsMost = 512;   // a save keeps no more: far past every kind at every business a player holds
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(SessionFile f)
        {
            var t = new StringBuilder(32 + f.Records.Count * 64);
            t.Append("v=").Append(Version).Append('|').Append(f.Seed.ToString(Inv)).Append('|').Append(f.Reminded.ToString(Inv));
            for (int i = 0; i < f.Records.Count && i < RecordsMost; i++)
            {
                SessionRecord r = f.Records[i];
                t.Append('^').Append(Escape(r.Place)).Append('#').Append(Escape(r.Session)).Append('#').Append(r.Held.ToString(Inv)).Append('#').Append(r.Delegated ? '1' : '0')
                 .Append('#').Append(r.By.ToString(Inv)).Append('#').Append(r.A.ToString(Inv)).Append('#').Append(r.B.ToString(Inv)).Append('#').Append(r.Until.ToString(Inv))
                 .Append('#').Append(r.Times.ToString(Inv));
            }
            return t.ToString();
        }

        /// <summary>The file from a save's data: null with no line; null and <paramref name="version"/> -1 for a line that cannot be read.</summary>
        public static SessionFile Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            SessionFile f = Read(text, out version);
            if (f == null)
            {
                version = -1;
            }
            return f;
        }

        /// <summary>A line read: null if it is of another version or cannot be read.</summary>
        public static SessionFile Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            string[] parts = text.Split('^');
            string[] head = parts[0].Split('|');
            if (head.Length < 3 || head[0] != "v=" + Version || !int.TryParse(head[1], NumberStyles.Integer, Inv, out int seed) || !int.TryParse(head[2], NumberStyles.Integer, Inv, out int reminded))
            {
                return null;
            }
            var f = new SessionFile { Seed = seed, Reminded = reminded };
            for (int i = 1; i < parts.Length; i++)
            {
                string[] x = parts[i].Split('#');
                if (x.Length < 2 || x[0].Length == 0 || x[1].Length == 0)
                {
                    return null;
                }
                var r = new SessionRecord { Place = Unescape(x[0]), Session = Unescape(x[1]) };
                r.Held = Int(x, 2, SessionRules.Never);
                r.Delegated = Int(x, 3, 0) == 1;
                r.By = Int(x, 4, 0);
                r.A = Int(x, 5, 0);
                r.B = Int(x, 6, 0);
                r.Until = Int(x, 7, SessionRules.Never);
                r.Times = Int(x, 8, 0);
                f.Records.Add(r);
            }
            version = Version;
            return f;
        }

        private static int Int(string[] x, int i, int otherwise)
        {
            return i < x.Length && int.TryParse(x[i], NumberStyles.Integer, Inv, out int v) ? v : otherwise;
        }

        private static string Escape(string s)
        {
            return (s ?? string.Empty).Replace("%", "%25").Replace("#", "%23").Replace("^", "%5E").Replace("|", "%7C");
        }

        private static string Unescape(string s)
        {
            return s.Replace("%7C", "|").Replace("%5E", "^").Replace("%23", "#").Replace("%25", "%");
        }
    }
}
