using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.ParkSpaces
{
    /// <summary>
    /// The parking spaces in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=1^h:seed|lastDay|lastHour|refunded|paid^s:park|slot|who|paid|rent|day|paidTo|ending|missing|priceSeen|ownSeen;...^p:day|park|vehicle|hours;...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'. h is the header,
    /// s the spaces held (the player's and the tycoons'), p the car-hours counted and not yet paid back (a vehicle's id is escaped). A newer version adds fields
    /// at the end of a record or a part with a new letter: this reader takes what it knows and puts every figure inside its
    /// bounds. A save from before the feature has no line. Numbers are written and read in the invariant culture.
    /// </summary>
    public static class SpaceStore
    {
        public const string Key = "reallife:parking_spaces";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static SpaceState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // a line that cannot be read is kept as it is (Core/SaveLines)
        }

        public static string Write(SpaceState s)
        {
            var t = new StringBuilder(96 + s.Held.Count * 48);
            t.Append("v=").Append(Version.ToString(Inv)).Append("^h:");
            Join(t, s.Seed, s.LastDay, s.LastHour, s.RefundedTotal, s.PaidTotal);
            if (s.Held.Count > 0)
            {
                t.Append("^s:");
                for (int i = 0; i < s.Held.Count; i++)
                {
                    SpaceHolding h = s.Held[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, h.Park, h.Slot, h.Who, h.Paid, h.Rent, h.Day, h.PaidTo, h.Ending ? 1 : 0, h.Missing, h.PriceSeen, h.OwnSeen ? 1 : 0);
                }
            }
            if (s.Pending.Count > 0)
            {
                t.Append("^p:");
                for (int i = 0; i < s.Pending.Count; i++)
                {
                    SpaceHours h = s.Pending[i];
                    t.Append(i > 0 ? ";" : string.Empty).Append(h.Day.ToString(Inv)).Append('|').Append(h.Park.ToString(Inv)).Append('|')
                        .Append(Uri.EscapeDataString(h.Vehicle ?? string.Empty)).Append('|').Append(h.Hours.ToString(Inv));
                }
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static SpaceState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            string[] parts = text.Split('^');
            if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version))
            {
                return null;
            }
            var s = new SpaceState();
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length < 2 || part[1] != ':')
                {
                    continue;
                }
                string body = part.Substring(2);
                switch (part[0])
                {
                    case 'h':
                        string[] f = body.Split('|');
                        s.Seed = Int(f, 0);
                        s.LastDay = Int(f, 1, -1);
                        s.LastHour = Int(f, 2, -1);
                        s.RefundedTotal = Num(f, 3);
                        s.PaidTotal = Num(f, 4);
                        break;
                    case 's':
                        foreach (string record in body.Split(';'))
                        {
                            string[] r = record.Split('|');
                            if (r.Length < 7)
                            {
                                continue;
                            }
                            s.Held.Add(new SpaceHolding
                            {
                                Park = Int(r, 0), Slot = Int(r, 1), Who = Int(r, 2, SpaceHolding.PlayerIs), Paid = Num(r, 3), Rent = Num(r, 4), Day = Int(r, 5),
                                PaidTo = Int(r, 6), Ending = Num(r, 7) != 0, Missing = Int(r, 8), PriceSeen = Num(r, 9), OwnSeen = Num(r, 10) != 0
                            });
                        }
                        break;
                    case 'p':
                        foreach (string record in body.Split(';'))
                        {
                            string[] r = record.Split('|');
                            if (r.Length >= 4 && r[2].Length > 0)
                            {
                                s.Pending.Add(new SpaceHours { Day = Int(r, 0, -1), Park = Int(r, 1), Vehicle = Uri.UnescapeDataString(r[2]), Hours = Int(r, 3) });
                            }
                        }
                        break;
                }
            }
            SpaceRules.Bound(s);
            return s;
        }

        private static void Join(StringBuilder t, params long[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static long Num(string[] fields, int at, long fallback = 0)
        {
            return at < fields.Length && long.TryParse(fields[at], NumberStyles.Integer, Inv, out long n) ? Math.Max(-(long.MaxValue / 4), Math.Min(n, long.MaxValue / 4)) : fallback;
        }

        private static int Int(string[] fields, int at, int fallback = 0)
        {
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, Num(fields, at, fallback)));
        }
    }
}
