using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the contacts' talk history keeps in a save: v=1^l=contact|minute|m or t|words, one record a line, oldest first for each
    /// contact (m the player's own line, t the contact's). A save without the line has no history; a line that cannot be read is
    /// kept as it was (Core/SaveLines). Game-free. COST: nothing unless a save is loaded or written.
    /// </summary>
    public static class TalkLogStore
    {
        public const string Key = "reallife:contactstalk";
        public const int Version = 1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(TalkLog log)
        {
            var t = new StringBuilder(16 + log.PeopleCount * 64);
            t.Append("v=").Append(Version);
            foreach (string id in log.People)
            {
                System.Collections.Generic.List<TalkLine> lines = log.Of(id);
                for (int i = 0; lines != null && i < lines.Count; i++)
                {
                    TalkLine l = lines[i];
                    t.Append("^l=").Append(id).Append('|').Append(l.Minute.ToString(Inv)).Append('|').Append(l.Mine ? 'm' : 't').Append('|').Append(l.Text);
                }
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null if it is of another version or cannot be read; no line, or an empty one, gives an empty history.</summary>
        public static TalkLog Read(string text)
        {
            var log = new TalkLog();
            if (string.IsNullOrEmpty(text))
            {
                return log;
            }
            string[] records = text.Split('^');
            if (records[0] != "v=" + Version)
            {
                return null;
            }
            for (int i = 1; i < records.Length; i++)
            {
                string r = records[i];
                if (r.Length < 2 || r[1] != '=')
                {
                    return null;
                }
                if (r[0] != 'l')
                {
                    continue;   // a later version's kind of record
                }
                string[] p = r.Substring(2).Split(new[] { '|' }, 4);
                if (p.Length < 4 || p[0].Length == 0 || !int.TryParse(p[1], NumberStyles.Integer, Inv, out int minute) || minute < 0 || (p[2] != "m" && p[2] != "t"))
                {
                    return null;
                }
                log.Add(p[0], minute, p[2] == "m", p[3]);
            }
            return log;
        }
    }
}
