using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The delivery trips' line in the save: "v=1", then '^' and one record each, its fields split by '|': R a route's departures,
    /// T a trip waiting or on the road (its stops split by '/', a stop's goods by ';', an item and its pieces by '='), L a run
    /// of the log, D the day the rivals' books were last charged. Routes sorted by id, trips and runs in their order, so a
    /// state always writes the same line. A save from before has no line: no trips on the road. Game-free.
    /// </summary>
    public static class TripStore
    {
        public const string Key = "reallife:trips";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(TripState s)
        {
            var t = new StringBuilder(256);
            t.Append("v=").Append(Version);
            var ids = new List<string>(s.Routes.Keys);
            ids.Sort(StringComparer.Ordinal);
            for (int i = 0; i < ids.Count; i++)
            {
                RouteTimes r = s.Routes[ids[i]];
                t.Append("^R|").Append(Escape(ids[i])).Append('|').Append(N(r.First)).Append('|').Append(N(r.Runs)).Append('|').Append(N(r.Gap))
                    .Append('|').Append(N(r.Last)).Append('|').Append(r.Set ? '1' : '0');
            }
            for (int i = 0; i < s.Trips.Count; i++)
            {
                Trip p = s.Trips[i];
                t.Append("^T|").Append(Escape(p.Plan)).Append('|').Append(Escape(p.From)).Append('|').Append(N(p.Slot)).Append('|').Append(Escape(p.Vehicle))
                    .Append('|').Append(Escape(p.Driver)).Append('|').Append(N(p.Depart)).Append('|').Append(N(p.Back)).Append('|').Append(p.Out ? '1' : '0')
                    .Append('|').Append(N(p.Metres)).Append('|').Append(N(p.Boxes)).Append('|').Append(N(p.Room)).Append('|').Append(N(p.FuelCents))
                    .Append('|').Append(N(p.WageCents)).Append('|').Append(N((int)p.Kind)).Append('|');
                for (int k = 0; k < p.Stops.Count; k++)
                {
                    TripStop st = p.Stops[k];
                    t.Append(k > 0 ? "/" : string.Empty).Append(Escape(st.Place)).Append(',').Append(N(st.Arrive)).Append(',').Append(st.Done ? '1' : '0').Append(',');
                    for (int g = 0; g < st.Items.Count; g++)
                    {
                        t.Append(g > 0 ? ";" : string.Empty).Append(Escape(st.Items[g])).Append('=').Append(N(st.Pieces[g]));
                    }
                }
            }
            for (int i = 0; i < s.Log.Count; i++)
            {
                TripLog e = s.Log[i];
                t.Append("^L|").Append(N(e.Day)).Append('|').Append(N(e.Minute)).Append('|').Append(Escape(e.Plan)).Append('|').Append(N(e.Slot))
                    .Append('|').Append(N(e.Stops)).Append('|').Append(N(e.Pieces)).Append('|').Append(N(e.Boxes)).Append('|').Append(N(e.Room))
                    .Append('|').Append(N(e.Metres)).Append('|').Append(N(e.Minutes)).Append('|').Append(N(e.FuelCents)).Append('|').Append(N(e.WageCents));
            }
            t.Append("^D|").Append(N(s.RivalDay));
            return t.ToString();
        }

        /// <summary>The state in a line; an empty one for no line (a save from before); null for another version or a broken line.</summary>
        public static TripState Read(string text)
        {
            var s = new TripState();
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
                bool ok;
                switch (c[0])
                {
                    case "R": ok = Route(c, s); break;
                    case "T": ok = TripOf(c, s); break;
                    case "L": ok = LogOf(c, s); break;
                    case "D": ok = c.Length == 2 && I(c[1], out s.RivalDay); break;
                    default: ok = false; break;
                }
                if (!ok)
                {
                    return null;
                }
            }
            return s;
        }

        private static bool Route(string[] c, TripState s)
        {
            var r = new RouteTimes();
            if (c.Length != 7 || !I(c[2], out r.First) || !I(c[3], out r.Runs) || !I(c[4], out r.Gap) || !I(c[5], out r.Last) || (c[6] != "0" && c[6] != "1")
                || r.First < 0 || r.First > 23 || r.Runs < 1 || r.Runs > TripRules.RunChoices[TripRules.RunChoices.Length - 1] || r.Gap < 1 || r.Gap > 23)
            {
                return false;
            }
            r.Set = c[6] == "1";
            s.Routes[Unescape(c[1])] = r;
            return true;
        }

        private static bool TripOf(string[] c, TripState s)
        {
            var p = new Trip();
            int kind;
            if (c.Length != 16 || !I(c[3], out p.Slot) || !I(c[6], out p.Depart) || !I(c[7], out p.Back) || (c[8] != "0" && c[8] != "1") || !I(c[9], out p.Metres)
                || !I(c[10], out p.Boxes) || !I(c[11], out p.Room) || !I(c[12], out p.FuelCents) || !I(c[13], out p.WageCents) || !I(c[14], out kind)
                || kind < 0 || kind > (int)TripVehicle.Truck)
            {
                return false;
            }
            p.Plan = Unescape(c[1]);
            p.From = Unescape(c[2]);
            p.Vehicle = Unescape(c[4]);
            p.Driver = Unescape(c[5]);
            p.Out = c[8] == "1";
            p.Kind = (TripVehicle)kind;
            string[] stops = c[15].Length > 0 ? c[15].Split('/') : new string[0];
            for (int k = 0; k < stops.Length; k++)
            {
                if (!StopOf(stops[k], p))
                {
                    return false;
                }
            }
            s.Trips.Add(p);
            return true;
        }

        private static bool StopOf(string text, Trip p)
        {
            string[] f = text.Split(',');
            var st = new TripStop();
            if (f.Length != 4 || !I(f[1], out st.Arrive) || (f[2] != "0" && f[2] != "1"))
            {
                return false;
            }
            st.Place = Unescape(f[0]);
            st.Done = f[2] == "1";
            if (f[3].Length > 0)
            {
                string[] goods = f[3].Split(';');
                for (int g = 0; g < goods.Length; g++)
                {
                    int at = goods[g].IndexOf('=');
                    if (at <= 0 || !I(goods[g].Substring(at + 1), out int n) || n < 0)
                    {
                        return false;
                    }
                    st.Items.Add(Unescape(goods[g].Substring(0, at)));
                    st.Pieces.Add(n);
                }
            }
            p.Stops.Add(st);
            return true;
        }

        private static bool LogOf(string[] c, TripState s)
        {
            var e = new TripLog();
            if (c.Length != 13 || !I(c[1], out e.Day) || !I(c[2], out e.Minute) || !I(c[4], out e.Slot) || !I(c[5], out e.Stops) || !I(c[6], out e.Pieces)
                || !I(c[7], out e.Boxes) || !I(c[8], out e.Room) || !I(c[9], out e.Metres) || !I(c[10], out e.Minutes) || !I(c[11], out e.FuelCents)
                || !I(c[12], out e.WageCents))
            {
                return false;
            }
            e.Plan = Unescape(c[3]);
            s.Log.Add(e);
            return true;
        }

        private static string N(int v) => v.ToString(Inv);

        private static bool I(string s, out int v) => int.TryParse(s, NumberStyles.Integer, Inv, out v);

        internal static string Escape(string s) => (s ?? string.Empty).Replace("%", "%25").Replace("^", "%5E").Replace("|", "%7C").Replace(";", "%3B")
            .Replace("=", "%3D").Replace(",", "%2C").Replace("/", "%2F").Replace(":", "%3A");

        internal static string Unescape(string s) => s.Replace("%3A", ":").Replace("%2F", "/").Replace("%2C", ",").Replace("%3D", "=").Replace("%3B", ";")
            .Replace("%7C", "|").Replace("%5E", "^").Replace("%25", "%");
    }
}
