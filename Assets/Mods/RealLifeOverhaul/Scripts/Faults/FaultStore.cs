using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// What the city's faults keep in a save: one line under its own key, records separated by '^':
    ///
    ///     v=1^n=next|lastRoll|wetUntil|stormUntil|frostUntil^f=(a fault, as FaultShare.Line)^s=reporter|count|day|barredUntil|warnedDay^m=kind|district|day
    ///
    /// New fields go at the end of a record. A line that cannot be read gives no faults and is kept as it was
    /// (Core/SaveLines). Game-free.
    /// </summary>
    public static class FaultStore
    {
        public const string Key = "reallife:faults";
        public const int Version = 1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(FaultFile file)
        {
            var t = new StringBuilder(64 + file.Faults.Count * 100 + file.Mended.Count * 40);
            t.Append("v=").Append(Version);
            t.Append("^n=").Append(file.NextId.ToString(Inv)).Append('|').Append(file.LastRoll.ToString(Inv)).Append('|').Append(file.WetUntil.ToString(Inv)).Append('|')
             .Append(file.StormUntil.ToString(Inv)).Append('|').Append(file.FrostUntil.ToString(Inv));
            for (int i = 0; i < file.Faults.Count; i++)
            {
                t.Append("^f=");
                FaultShare.Line(file.Faults[i], t);
            }
            for (int i = 0; i < file.Strikes.Count; i++)
            {
                FaultStrike s = file.Strikes[i];
                t.Append("^s=").Append(FaultShare.Escape(s.Reporter)).Append('|').Append(s.Count.ToString(Inv)).Append('|').Append(s.Day.ToString(Inv)).Append('|')
                 .Append(s.BarredUntil.ToString(Inv)).Append('|').Append(s.WarnedDay.ToString(Inv));
            }
            for (int i = 0; i < file.Mended.Count; i++)
            {
                FaultMended m = file.Mended[i];
                t.Append("^m=").Append(((int)m.Kind).ToString(Inv)).Append('|').Append(FaultShare.Escape(m.District)).Append('|').Append(m.Day.ToString(Inv));
            }
            return t.ToString();
        }

        /// <summary>A line into a new file; null if of another version or unreadable. An empty line gives an empty file.</summary>
        public static FaultFile Read(string text)
        {
            var file = new FaultFile();
            if (string.IsNullOrEmpty(text))
            {
                return file;
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
                string body = r.Substring(2);
                string[] p = body.Split('|');
                switch (r[0])
                {
                    case 'n':
                        if (p.Length < 5 || !int.TryParse(p[0], NumberStyles.Integer, Inv, out file.NextId) || !long.TryParse(p[1], NumberStyles.Integer, Inv, out file.LastRoll)
                            || !long.TryParse(p[2], NumberStyles.Integer, Inv, out file.WetUntil) || !long.TryParse(p[3], NumberStyles.Integer, Inv, out file.StormUntil)
                            || !long.TryParse(p[4], NumberStyles.Integer, Inv, out file.FrostUntil))
                        {
                            return null;
                        }
                        break;
                    case 'f':
                        Fault f = FaultShare.Read(body);
                        if (f == null)
                        {
                            return null;
                        }
                        file.Faults.Add(f);
                        file.NextId = Math.Max(file.NextId, f.Id + 1);
                        break;
                    case 's':
                        var s = new FaultStrike { Reporter = FaultShare.Unescape(p[0]) };
                        if (p.Length < 5 || !int.TryParse(p[1], NumberStyles.Integer, Inv, out s.Count) || !int.TryParse(p[2], NumberStyles.Integer, Inv, out s.Day)
                            || !int.TryParse(p[3], NumberStyles.Integer, Inv, out s.BarredUntil) || !int.TryParse(p[4], NumberStyles.Integer, Inv, out s.WarnedDay))
                        {
                            return null;
                        }
                        file.Strikes.Add(s);
                        break;
                    case 'm':
                        int kind, day;
                        if (p.Length < 3 || !int.TryParse(p[0], NumberStyles.Integer, Inv, out kind) || kind < 0 || kind >= FaultRules.Kinds || !int.TryParse(p[2], NumberStyles.Integer, Inv, out day))
                        {
                            return null;
                        }
                        file.Mended.Add(new FaultMended { Kind = (FaultKind)kind, District = FaultShare.Unescape(p[1]), Day = day });
                        break;
                    default:
                        break;   // (a record of a later version's kind: passed over)
                }
            }
            return file;
        }
    }
}
