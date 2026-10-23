using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// What street talk keeps in a save, the word of mouth: v=1^c=countedDay|counted^t=address|day|percent. A line that cannot
    /// be read is kept as it was (Core/SaveLines). Game-free. COST: nothing unless a save is loaded or written.
    /// </summary>
    public static class StreetStore
    {
        public const string Key = "reallife:street";
        public const int Version = 1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(StreetMouth m)
        {
            var t = new StringBuilder(32 + m.Told.Count * 40);
            t.Append("v=").Append(Version).Append("^c=").Append(m.CountedDay.ToString(Inv)).Append('|').Append(m.Counted.ToString(Inv));
            for (int i = 0; i < m.Told.Count; i++)
            {
                StreetTold told = m.Told[i];
                t.Append("^t=").Append(Clean(told.Address)).Append('|').Append(told.Day.ToString(Inv)).Append('|').Append(told.Percent.ToString(Inv));
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null if it is of another version or cannot be read; an empty line gives nothing told.</summary>
        public static StreetMouth Read(string text)
        {
            var m = new StreetMouth();
            if (string.IsNullOrEmpty(text))
            {
                return m;
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
                string[] p = r.Substring(2).Split('|');
                switch (r[0])
                {
                    case 'c':
                        if (p.Length < 2 || !int.TryParse(p[0], NumberStyles.Integer, Inv, out m.CountedDay) || !int.TryParse(p[1], NumberStyles.Integer, Inv, out m.Counted))
                        {
                            return null;
                        }
                        break;
                    case 't':
                        var told = new StreetTold { Address = p[0] };
                        if (p.Length < 3 || p[0].Length == 0 || !int.TryParse(p[1], NumberStyles.Integer, Inv, out told.Day) || !int.TryParse(p[2], NumberStyles.Integer, Inv, out told.Percent)
                            || told.Percent < 0 || told.Percent > StreetRules.MouthMost)
                        {
                            return null;
                        }
                        m.Told.Add(told);
                        break;
                    default:
                        break;   // a later version's kind of record
                }
            }
            return m;
        }

        // Nothing a record is split on may be inside a field.
        private static string Clean(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.Replace('^', ' ').Replace('|', ' ');
        }
    }
}
