using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// Standing in a save: one entry of the save's data, short lines.
    ///
    ///   reallife:standing_file
    ///     ST1|seed|start|settled|wealth|recognition|press|net worth|weekly|followers|businesses|properties|staff|
    ///         customers,workers,neighbourhood,integrity|moved,moved,moved,moved|moved day|street day|street today|
    ///         street minute|shop day|shop today|visit day|place;place|notice day
    ///     D|district|opinion                                    a district's opinion of the player
    ///     N|day|part|cause|delta|district                       a change the Standing page lists
    ///
    /// Every number is written in the invariant culture. What is not understood is left out a line at a time: a note of
    /// a part or a cause this version does not know (from a later one) is dropped, never read as something else, so an
    /// entry from a later version or a damaged one never stops a load. A first line that is not this version's is not
    /// read at all (a new file: everybody's opinion where a newcomer's is). Game-free.
    /// </summary>
    public static class FameStore
    {
        public const string FileKey = "reallife:standing_file";
        public const string Keys = "reallife:standing_*";
        private const string Version = "ST1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The kinds this version knows: anything past them is from a later one.
        private const int Parts = (int)FamePart.Integrity + 1;
        private const int Causes = (int)FameCause.Press + 1;

        public static void Save(Dictionary<string, string> data, FameFile f)
        {
            if (data == null || f == null)
            {
                return;
            }
            data[FileKey] = Write(f);
        }

        /// <summary>The save's file, or null if it has none this version can read (a new one is opened then).</summary>
        public static FameFile Load(Dictionary<string, string> data)
        {
            string text;
            return data != null && data.TryGetValue(FileKey, out text) ? Read(text) : null;
        }

        public static string Write(FameFile f)
        {
            var b = new StringBuilder(320 + f.Districts.Count * 24 + f.Notes.Count * 28);
            b.Append(Version);
            Join(b, f.Seed, f.StartDay, f.SettledDay, f.Wealth, f.Recognition, f.Press);
            b.Append('|').Append(f.NetWorth.ToString(Inv)).Append('|').Append(f.Weekly.ToString(Inv));
            Join(b, f.Followers, f.Businesses, f.Properties, f.Staff);
            b.Append('|');
            List(b, f.Rep);
            b.Append('|');
            List(b, f.Moved);
            Join(b, f.MovedDay, f.StreetDay, f.StreetToday, f.StreetMinute, f.ShopDay, f.ShopToday, f.VisitDay);
            b.Append('|');
            for (int i = 0; i < f.Visited.Count; i++)
            {
                b.Append(i > 0 ? ";" : string.Empty).Append(Clean(f.Visited[i]).Replace(";", string.Empty));
            }
            Join(b, f.NoticeDay);
            for (int i = 0; i < f.Districts.Count; i++)
            {
                FameDistrict d = f.Districts[i];
                b.Append("\nD|").Append(Clean(d.Name));
                Join(b, d.Opinion);
            }
            for (int i = 0; i < f.Notes.Count; i++)
            {
                FameNote n = f.Notes[i];
                b.Append("\nN");
                Join(b, n.Day, n.Part, (int)n.Cause, n.Delta);
                b.Append('|').Append(Clean(n.District));
            }
            return b.ToString();
        }

        /// <summary>A file from the save's line; null if it is not this version's or cannot be read at all.</summary>
        public static FameFile Read(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                string[] lines = text.Split('\n');
                string[] h = lines[0].TrimEnd('\r').Split('|');
                if (h.Length < 24 || h[0] != Version)
                {
                    return null;
                }
                var f = new FameFile
                {
                    Seed = I(h, 1), StartDay = I(h, 2), SettledDay = I(h, 3, -1), Wealth = FameRules.Clamp(I(h, 4), 0, 100), Recognition = FameRules.Clamp(I(h, 5), 0, 100),
                    Press = FameRules.Clamp(I(h, 6), 0, FameRules.PressMost), NetWorth = L(h, 7), Weekly = L(h, 8), Followers = Math.Max(0, I(h, 9)), Businesses = Math.Max(0, I(h, 10)),
                    Properties = Math.Max(0, I(h, 11)), Staff = Math.Max(0, I(h, 12)), MovedDay = I(h, 15, -1), StreetDay = I(h, 16, -1), StreetToday = Math.Max(0, I(h, 17)),
                    StreetMinute = I(h, 18, -10000), ShopDay = I(h, 19, -1), ShopToday = Math.Max(0, I(h, 20)), VisitDay = I(h, 21, -1), NoticeDay = I(h, 23, -1)
                };
                Fill(h[13], f.Rep, FameRules.Start, 0, 100);
                Fill(h[14], f.Moved, 0, -FameRules.DayMost, FameRules.DayMost);
                string[] visited = h[22].Split(';');
                for (int i = 0; i < visited.Length && f.Visited.Count < FameRules.VisitsMost; i++)
                {
                    if (visited[i].Length > 0)
                    {
                        f.Visited.Add(visited[i]);
                    }
                }
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    if (c[0] == "D" && c.Length >= 3 && c[1].Length > 0 && f.Districts.Count < FameRules.DistrictsMost && f.District(c[1]) == null)
                    {
                        f.Districts.Add(new FameDistrict { Name = c[1], Opinion = FameRules.Clamp(I(c, 2, FameRules.Start), 0, 100) });
                    }
                    else if (c[0] == "N" && c.Length >= 6 && f.Notes.Count < FameRules.NotesMost)
                    {
                        int part = I(c, 2, -9), cause = I(c, 3, -1);
                        if (part < FameNote.Recognition || part >= Parts || cause < 0 || cause >= Causes)
                        {
                            continue;   // a kind of a later version's
                        }
                        f.Notes.Add(new FameNote { Day = I(c, 1), Part = part, Cause = (FameCause)cause, Delta = I(c, 4), District = Text(c[5]) });
                    }
                }
                return f;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- the pieces of a line

        internal static void Join(StringBuilder b, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append('|').Append(values[i].ToString(Inv));
            }
        }

        private static void List(StringBuilder b, int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append(i > 0 ? "," : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static void Fill(string text, int[] into, int otherwise, int least, int most)
        {
            string[] parts = (text ?? string.Empty).Split(',');
            for (int i = 0; i < into.Length; i++)
            {
                int value;
                into[i] = i < parts.Length && int.TryParse(parts[i], NumberStyles.Integer, Inv, out value) ? FameRules.Clamp(value, least, most) : otherwise;
            }
        }

        /// <summary>Text as it goes into a line whose fields are parted by '|' and whose lines by a line break.</summary>
        internal static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("|", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
        }

        internal static string Text(string field)
        {
            return string.IsNullOrEmpty(field) ? null : field;
        }

        internal static int I(string[] c, int at, int otherwise = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : otherwise;
        }

        internal static long L(string[] c, int at)
        {
            long value;
            return at < c.Length && long.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : 0L;
        }
    }
}
