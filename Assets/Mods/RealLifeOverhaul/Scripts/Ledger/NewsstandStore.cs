using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// Newsstand in a save: one entry of the save's data beside the City Ledger's own, a few short lines.
    ///
    ///   LN1|noted day|waiting day
    ///   U|subscription|subscribed|cancelled|paid until|since|weeks|last day      a subscription (0 to 3 a title's, 4 the pass)
    ///   L|title|day,day,...                                                      the mornings a title's edition came
    ///   E|... P|... S|... Q|... C|...                                            an earlier edition, as LedgerStore writes one
    ///
    /// Numbers are invariant; a line that does not read is left out, and an entry of another version is not read at all
    /// (a new state, no subscriptions). Game-free.
    /// </summary>
    public static class NewsstandStore
    {
        public const string Key = "reallife:ledger_online";
        private const string Version = "LN1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Save(Dictionary<string, string> data, NewsstandState s)
        {
            if (data == null || s == null)
            {
                return;
            }
            data[Key] = Write(s);
        }

        public static string Write(NewsstandState s)
        {
            var b = new StringBuilder(8192);
            b.Append(Version).Append('|').Append(s.NotedDay).Append('|').Append(s.WaitingDay);
            for (int i = 0; i < s.Subs.Length; i++)
            {
                OnlineSub x = s.Subs[i];
                b.Append("\nU|").Append(i).Append('|').Append(x.Subscribed ? '1' : '0').Append('|').Append(x.Cancelled ? '1' : '0').Append('|').Append(x.PaidUntil)
                    .Append('|').Append(x.Since).Append('|').Append(x.Weeks).Append('|').Append(x.LastDay);
            }
            for (int t = 0; t < s.Loaded.Length; t++)
            {
                if (s.Loaded[t].Count == 0)
                {
                    continue;
                }
                b.Append("\nL|").Append(t).Append('|');
                for (int i = 0; i < s.Loaded[t].Count; i++)
                {
                    b.Append(i > 0 ? "," : string.Empty).Append(s.Loaded[t][i]);
                }
            }
            for (int i = 0; i < s.Earlier.Count; i++)
            {
                LedgerStore.Edition(b, s.Earlier[i]);
            }
            return b.ToString();
        }

        /// <summary>The state of a save; a new one if the save has none, or one this version cannot read.</summary>
        public static NewsstandState Load(Dictionary<string, string> data)
        {
            string text;
            if (data == null || !data.TryGetValue(Key, out text))
            {
                return new NewsstandState();
            }
            if (!string.IsNullOrEmpty(text) && !LedgerStore.HeadOk(text, Version, 3))
            {
                SaveLines.Unreadable(data, Key, "it could not be read");   // damaged, or a later version's: kept as it is (Core/SaveLines)
            }
            return Read(text);
        }


        public static NewsstandState Read(string text)
        {
            var s = new NewsstandState();
            if (string.IsNullOrEmpty(text))
            {
                return s;
            }
            string[] lines = text.Split('\n');
            string[] head = lines[0].TrimEnd('\r').Split('|');
            if (head.Length < 3 || head[0] != Version)
            {
                return s;
            }
            s.NotedDay = Int(head[1]);
            s.WaitingDay = Int(head[2]);
            LedgerEdition e = null;
            for (int i = 1; i < lines.Length; i++)
            {
                string[] f = lines[i].TrimEnd('\r').Split('|');
                try
                {
                    switch (f[0])
                    {
                        case "U":
                            int sub = f.Length >= 8 ? Int(f[1]) : -1;
                            if (sub >= 0 && sub < s.Subs.Length)
                            {
                                OnlineSub x = s.Subs[sub];
                                x.Subscribed = f[2] == "1";
                                x.Cancelled = x.Subscribed && f[3] == "1";
                                x.PaidUntil = Int(f[4]);
                                x.Since = Int(f[5]);
                                x.Weeks = Math.Max(0, Int(f[6]));
                                x.LastDay = Int(f[7]);
                            }
                            break;
                        case "L":
                            int title = f.Length >= 3 ? Int(f[1]) : -1;
                            if (title >= 0 && title < s.Loaded.Length && f[2].Length > 0)
                            {
                                string[] days = f[2].Split(',');
                                for (int d = 0; d < days.Length && s.Loaded[title].Count < NewsstandCatalogue.KeepDays + 1; d++)
                                {
                                    int day = Int(days[d]);
                                    if (day > 0 && !s.Loaded[title].Contains(day))
                                    {
                                        s.Loaded[title].Add(day);
                                    }
                                }
                                s.Loaded[title].Sort();
                            }
                            break;
                        case "E":
                            Kept(s, e);
                            e = null;
                            LedgerStore.EditionLine(f, ref e);
                            break;
                        default:
                            LedgerStore.EditionLine(f, ref e);
                            break;
                    }
                }
                catch (Exception)
                {
                    // one line this version cannot read: left out
                }
            }
            Kept(s, e);
            s.Earlier.Sort((a, b) => a.Day.CompareTo(b.Day));
            return s;
        }

        private static void Kept(NewsstandState s, LedgerEdition e)
        {
            e = LedgerStore.Finish(e);
            if (e == null || s.Earlier.Count >= NewsstandCatalogue.KeepDays)
            {
                return;
            }
            for (int i = 0; i < s.Earlier.Count; i++)
            {
                if (s.Earlier[i].Day == e.Day)
                {
                    return;
                }
            }
            s.Earlier.Add(e);
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, Inv, out int value) ? value : 0;
        }
    }
}
