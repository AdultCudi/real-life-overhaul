using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// BizMan's Send now on a warehouse's route may be used once a game day for each route. This holds the day each route was
    /// last sent and answers whether it may be sent today; the game's own run at 02:00 and the trips' departures are not
    /// touched. Rivals and the city's shops have no early send (LogisticsDirector.Rivals charges them once a day), so there
    /// is nothing to cap on their side. Saved as "reallife:send_now": "v=1", then '^' and one record each, a route's id and
    /// the day, split by '|'. A save without the line counts every route as not sent today; entries of an earlier day are
    /// not written, as they no longer hold anything back. Game-free; a map lookup when the page is laid again, never per frame.
    /// </summary>
    public static class SendNowLimit
    {
        public const string Key = "reallife:send_now";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Dictionary<string, int> Days = new Dictionary<string, int>(8, StringComparer.Ordinal);
        private static readonly List<string> Order = new List<string>(8);

        /// <summary>True when the route was not sent on <paramref name="today"/> (the day it was sent, if ever, is another one).</summary>
        public static bool Allowed(string route, int today) => string.IsNullOrEmpty(route) || !Days.TryGetValue(route, out int day) || day != today;

        /// <summary>The route was sent on <paramref name="today"/>.</summary>
        public static void Sent(string route, int today)
        {
            if (!string.IsNullOrEmpty(route))
            {
                Days[route] = today;
            }
        }

        /// <summary>No save: every route may be sent.</summary>
        public static void Reset() => Days.Clear();

        /// <summary>The state as a line: only the routes sent on <paramref name="today"/>, sorted, so a state always writes the same line.</summary>
        public static string Write(int today)
        {
            Order.Clear();
            foreach (KeyValuePair<string, int> kv in Days)
            {
                if (kv.Value == today)
                {
                    Order.Add(kv.Key);
                }
            }
            Order.Sort(StringComparer.Ordinal);
            var t = new StringBuilder(64);
            t.Append("v=").Append(Version);
            for (int i = 0; i < Order.Count; i++)
            {
                t.Append("^").Append(TripStore.Escape(Order[i])).Append('|').Append(today.ToString(Inv));
            }
            return t.ToString();
        }

        /// <summary>Reads a line into the state (replacing it); an empty one for no line (a save from before). False for another version or a broken line.</summary>
        public static bool Read(string text)
        {
            Days.Clear();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            string[] records = text.Split('^');
            if (records[0] != "v=" + Version)
            {
                return false;
            }
            for (int i = 1; i < records.Length; i++)
            {
                string[] c = records[i].Split('|');
                if (c.Length != 2 || c[0].Length == 0 || !int.TryParse(c[1], NumberStyles.Integer, Inv, out int day))
                {
                    Days.Clear();
                    return false;
                }
                Days[TripStore.Unescape(c[0])] = day;
            }
            return true;
        }

        /// <summary>From the mod's load of a save: the line read; a line this version cannot read is kept as it was and every route may be sent.</summary>
        public static void ReadSave(Dictionary<string, string> data)
        {
            data.TryGetValue(Key, out string text);
            if (!Read(text))
            {
                SaveLines.Unreadable(data, Key, "its version is not one this version of the mod reads");
            }
        }

        /// <summary>At a save: the routes sent on <paramref name="today"/> written, unless the line was kept as it was read.</summary>
        public static void WriteSave(Dictionary<string, string> data, int today)
        {
            if (!SaveLines.IsKept(data, Key))
            {
                data[Key] = Write(today);
            }
        }
    }
}
