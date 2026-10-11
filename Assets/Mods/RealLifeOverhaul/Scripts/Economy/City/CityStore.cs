using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The city's economy in the save: one line of the save's modData, under <see cref="Key"/>. Game-free.
    ///
    ///     v=1~h:...~d:...~s:...~n:...
    ///
    /// Parts are divided by '~' and begin with a letter and a colon; records inside a part by ';', fields by '|'.
    ///   h  the header: the numbers of <see cref="CityState"/>, in the order of <see cref="Write"/>
    ///   d  the districts: name|start|income|a month ago
    ///   s  supply, one record per kind of goods in the catalogue's order: factor|shock|from|days|size
    ///   n  the pieces for the paper that are not yet out of date: day|kind|a|b|c|d|name
    ///
    /// Every figure is a whole number, so what is read back is exactly what was written. A NEWER VERSION adds fields at
    /// the end of a record or a part with a new letter; this reader takes the fields it knows and skips parts it does
    /// not, so a save written by a later version still loads. A line that cannot be read is left alone and the economy
    /// is opened afresh. Nothing here is the game's: a save loads in the plain game with this line ignored.
    ///
    /// <see cref="TestKey"/> holds the same line as it was before the first test tool changed it, for as long as test
    /// changes are in place.
    /// </summary>
    public static class CityStore
    {
        public const string Key = "reallife:economy_city";
        public const string TestKey = "reallife:economy_city_before_tests";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; null if the save has none or it cannot be read.</summary>
        public static CityState Load(Dictionary<string, string> data, string key, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return Read(text, out version);
        }

        public static string Write(CityState s)
        {
            var t = new StringBuilder(256 + s.Districts.Count * 40);
            t.Append("v=").Append(Version).Append("~h:");
            Join(t, s.Seed, s.Age, s.LastDay, (int)s.Cycle, s.MonthsLeft, s.MonthsIn, s.Base, s.BaseBefore, s.Pending ? 1 : 0, s.PendingMove, (int)s.PendingReason,
                s.LastMove, (int)s.LastReason, s.Index, s.Inflation, s.Pressure, s.MonthChange, s.CarryRent, s.CarryWages, s.CarryTakings, s.LastCopycatDay, s.Buys);
            t.Append("~d:");
            for (int i = 0; i < s.Districts.Count; i++)
            {
                CityDistrict d = s.Districts[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(d.Name)).Append('|');
                Join(t, d.Start, d.Income, d.Before);
            }
            t.Append("~s:");
            for (int k = 0; k < s.Supply.Length; k++)
            {
                t.Append(k > 0 ? ";" : string.Empty);
                Join(t, s.Supply[k].Factor, (int)s.Supply[k].Shock, s.Supply[k].From, s.Supply[k].Days, s.Supply[k].Size);
            }
            t.Append("~n:");
            for (int i = 0; i < s.News.Count; i++)
            {
                CityNews n = s.News[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, n.Day, (int)n.Kind, n.A, n.B, n.C, n.D);
                t.Append('|').Append(Clean(n.Name));
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not the city's economy at all; a figure out of its bounds is put inside them.</summary>
        public static CityState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            try
            {
                string[] parts = text.Split('~');
                if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
                {
                    return null;
                }
                var s = new CityState();
                bool header = false;
                for (int p = 1; p < parts.Length; p++)
                {
                    string part = parts[p];
                    if (part.Length < 2 || part[1] != ':')
                    {
                        continue;
                    }
                    string body = part.Substring(2);
                    switch (part[0])
                    {
                        case 'h':
                            Header(s, body.Split('|'));
                            header = true;
                            break;
                        case 'd':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 4 && c[0].Length > 0 && s.District(c[0]) == null)
                                {
                                    int start = MacroCatalogue.Clamp(I(c, 1, MacroCatalogue.IncomeMiddle), MacroCatalogue.IncomeLeast, MacroCatalogue.IncomeMost);
                                    s.Districts.Add(new CityDistrict
                                    {
                                        Name = c[0], Start = start,
                                        Income = MacroCatalogue.Clamp(I(c, 2, start), MacroCatalogue.IncomeLeast, MacroCatalogue.IncomeMost),
                                        Before = MacroCatalogue.Clamp(I(c, 3, start), MacroCatalogue.IncomeLeast, MacroCatalogue.IncomeMost)
                                    });
                                }
                            }
                            break;
                        case 's':
                        {
                            string[] records = Records(body);
                            for (int k = 0; k < records.Length && k < s.Supply.Length; k++)
                            {
                                string[] c = records[k].Split('|');
                                int shock = I(c, 1);
                                s.Supply[k].Factor = MacroCatalogue.Clamp(I(c, 0, 1000), MacroCatalogue.FactorLeast, MacroCatalogue.FactorMost);
                                s.Supply[k].Shock = shock == (int)SupplyShock.Shortage || shock == (int)SupplyShock.Glut ? (SupplyShock)shock : SupplyShock.None;
                                s.Supply[k].From = Math.Max(0, I(c, 2));
                                s.Supply[k].Days = MacroCatalogue.Clamp(I(c, 3), 0, MacroCatalogue.ShockDaysMost);
                                s.Supply[k].Size = MacroCatalogue.Clamp(I(c, 4), 0, MacroCatalogue.ShortageMost);
                                if (s.Supply[k].Shock == SupplyShock.None || s.Supply[k].Days == 0)
                                {
                                    s.Supply[k].Shock = SupplyShock.None;
                                    s.Supply[k].From = s.Supply[k].Days = s.Supply[k].Size = 0;
                                }
                            }
                            break;
                        }
                        case 'n':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                int kind = I(c, 1, -1);
                                if (c.Length >= 6 && kind >= 0 && kind < MacroCatalogue.NewsKinds && s.News.Count < 16)
                                {
                                    s.News.Add(new CityNews { Day = I(c, 0), Kind = (CityNewsKind)kind, A = I(c, 2), B = I(c, 3), C = I(c, 4), D = I(c, 5), Name = c.Length > 6 ? c[6] : string.Empty });
                                }
                            }
                            break;
                    }
                }
                return header ? s : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Header(CityState s, string[] c)
        {
            s.Seed = I(c, 0);
            s.Age = Math.Max(0, I(c, 1));
            s.LastDay = Math.Max(0, I(c, 2));
            s.Cycle = (CycleState)MacroCatalogue.Clamp(I(c, 3, (int)CycleState.Steady), 0, MacroCatalogue.StateCount - 1);
            s.MonthsLeft = MacroCatalogue.Clamp(I(c, 4, 1), 1, MacroCatalogue.StateMonthsMost);
            s.MonthsIn = Math.Max(1, I(c, 5, 1));
            s.Base = MacroCatalogue.Clamp(I(c, 6), MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost);
            s.BaseBefore = MacroCatalogue.Clamp(I(c, 7), MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost);
            s.Pending = I(c, 8) != 0;
            s.PendingMove = s.Pending ? MacroCatalogue.Clamp(I(c, 9), -MacroCatalogue.MoveMost, MacroCatalogue.MoveMost) : 0;
            s.PendingReason = s.Pending ? Reason(I(c, 10)) : RateReason.Hold;
            s.LastMove = MacroCatalogue.Clamp(I(c, 11), -MacroCatalogue.MoveMost, MacroCatalogue.MoveMost);
            s.LastReason = Reason(I(c, 12));
            s.Index = MacroCatalogue.Clamp(I(c, 13, MacroCatalogue.IndexStart), MacroCatalogue.IndexLeast, MacroCatalogue.IndexMost);
            s.Inflation = MacroCatalogue.Clamp(I(c, 14, MacroCatalogue.InflationTarget), MacroCatalogue.InflationLeast, MacroCatalogue.InflationMost);
            s.Pressure = MacroCatalogue.Clamp(I(c, 15), MacroCatalogue.PressureLeast, MacroCatalogue.PressureMost);
            s.MonthChange = I(c, 16);
            s.CarryRent = Math.Max(0, I(c, 17));
            s.CarryWages = Math.Max(0, I(c, 18));
            s.CarryTakings = Math.Max(0, I(c, 19));
            s.LastCopycatDay = Math.Max(0, I(c, 20));
            s.Buys = I(c, 21) & ((1 << MacroCatalogue.SupplyKinds) - 1);
        }

        private static RateReason Reason(int value)
        {
            return value >= (int)RateReason.Hold && value <= (int)RateReason.Normal ? (RateReason)value : RateReason.Hold;
        }

        private static string[] Records(string body)
        {
            return body.Length == 0 ? Array.Empty<string>() : body.Split(';');
        }

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static int I(string[] c, int at, int fallback = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : fallback;
        }

        /// <summary>A text with the three dividers taken out, so that it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('~', ' ');
        }
    }
}
