using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Food spoilage's line in the save: "v=1", then '^' and one record each, its fields split by '|': P a place (its key, the last
    /// day turned, the seven days' stamps, pieces spoiled, pieces come in and the spoiled cost, each seven split by ',', then
    /// its products split by ';', each a name, '=' and its batches day:count split by ','), N the last day a notice was given.
    /// Places and products sorted by name, so a state always writes the same line. A save from before has no line: everything
    /// held then is taken as stocked on the day it loads. Game-free.
    /// </summary>
    public static class SpoilStore
    {
        public const string Key = "reallife:spoilage";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(SpoilState s)
        {
            var t = new StringBuilder(256);
            t.Append("v=").Append(Version);
            var keys = new List<string>(s.Places.Keys);
            keys.Sort(StringComparer.Ordinal);
            var items = new List<string>(8);
            for (int i = 0; i < keys.Count; i++)
            {
                SpoilPlace p = s.Places[keys[i]];
                t.Append("^P|").Append(TripStore.Escape(keys[i])).Append('|').Append(N(p.Turned));
                Ring(t, p.Stamp);
                Ring(t, p.Spoiled);
                Ring(t, p.Came);
                Ring(t, p.Cents);
                t.Append('|');
                items.Clear();
                foreach (KeyValuePair<string, List<Cohort>> kv in p.Batches)
                {
                    if (kv.Value.Count > 0)
                    {
                        items.Add(kv.Key);
                    }
                }
                items.Sort(StringComparer.Ordinal);
                for (int k = 0; k < items.Count; k++)
                {
                    List<Cohort> b = p.Batches[items[k]];
                    t.Append(k > 0 ? ";" : string.Empty).Append(TripStore.Escape(items[k])).Append('=');
                    for (int c = 0; c < b.Count; c++)
                    {
                        t.Append(c > 0 ? "," : string.Empty).Append(N(b[c].Day)).Append(':').Append(N(b[c].Count));
                    }
                }
            }
            t.Append("^N|").Append(N(s.Told));
            return t.ToString();
        }

        /// <summary>The state in a line; an empty one for no line (a save from before); null for another version or a broken line.</summary>
        public static SpoilState Read(string text)
        {
            var s = new SpoilState();
            if (string.IsNullOrEmpty(text))
            {
                return s;
            }
            string[] records = text.Split('^');
            if (records[0] != "v=" + Version)
            {
                return null;
            }
            for (int i = 1; i < records.Length; i++)
            {
                string[] c = records[i].Split('|');
                bool ok = c[0] == "P" ? PlaceOf(c, s) : c[0] == "N" && c.Length == 2 && I(c[1], out s.Told);
                if (!ok)
                {
                    return null;
                }
            }
            return s;
        }

        private static bool PlaceOf(string[] c, SpoilState s)
        {
            var p = new SpoilPlace();
            if (c.Length != 8 || !I(c[2], out p.Turned) || !Ring(c[3], p.Stamp) || !Ring(c[4], p.Spoiled) || !Ring(c[5], p.Came) || !Ring(c[6], p.Cents))
            {
                return false;
            }
            if (c[7].Length > 0)
            {
                string[] goods = c[7].Split(';');
                for (int g = 0; g < goods.Length; g++)
                {
                    int at = goods[g].IndexOf('=');
                    if (at <= 0)
                    {
                        return false;
                    }
                    List<Cohort> b = p.Of(TripStore.Unescape(goods[g].Substring(0, at)));
                    string[] batches = goods[g].Substring(at + 1).Split(',');
                    for (int k = 0; k < batches.Length; k++)
                    {
                        string[] dc = batches[k].Split(':');
                        if (dc.Length != 2 || !I(dc[0], out int day) || !I(dc[1], out int count) || count <= 0 || (b.Count > 0 && b[b.Count - 1].Day >= day)
                            || b.Count >= SpoilRules.CohortsMost)
                        {
                            return false;
                        }
                        b.Add(new Cohort(day, count));
                    }
                }
            }
            s.Places[TripStore.Unescape(c[1])] = p;
            return true;
        }

        private static void Ring(StringBuilder t, int[] ring)
        {
            t.Append('|');
            for (int i = 0; i < ring.Length; i++)
            {
                t.Append(i > 0 ? "," : string.Empty).Append(N(ring[i]));
            }
        }

        private static bool Ring(string text, int[] into)
        {
            string[] f = text.Split(',');
            if (f.Length != into.Length)
            {
                return false;
            }
            for (int i = 0; i < f.Length; i++)
            {
                if (!I(f[i], out into[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static string N(int v) => v.ToString(Inv);

        private static bool I(string s, out int v) => int.TryParse(s, NumberStyles.Integer, Inv, out v);
    }
}
