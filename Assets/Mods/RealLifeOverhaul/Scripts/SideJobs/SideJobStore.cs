using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.SideJobs
{
    /// <summary>
    /// The taxi and delivery shifts in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=1^h:seed|day^c:unitsToday|grossToday|keptToday|tipsToday|netToday|fuelToday|unitsBefore|netBefore|shifts|units|net|kept^p:(the same)
    ///
    /// Parts are divided by '^' and begin with a letter and a colon, fields by '|'. h is the header, c the cab office's figures
    /// for the driver, p the parcel depot's. A newer version adds fields at the end of a part or a part with a new letter: this
    /// reader takes what it knows and puts every figure inside its bounds. A save from before the feature has no line. A shift
    /// that is on is not here: the jobs' own line settles it (JobSave). Numbers are in the invariant culture.
    /// </summary>
    public static class SideJobStore
    {
        public const string Key = "reallife:side_jobs";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static SideJobState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // a line that cannot be read is kept as it is (Core/SaveLines)
        }

        public static string Write(SideJobState s)
        {
            var t = new StringBuilder(160);
            t.Append("v=").Append(Version.ToString(Inv)).Append("^h:").Append(s.Seed.ToString(Inv)).Append('|').Append(s.Day.ToString(Inv));
            Tally(t, 'c', s.At(SideKind.Cab));
            Tally(t, 'p', s.At(SideKind.Parcels));
            return t.ToString();
        }

        private static void Tally(StringBuilder t, char letter, SideTally a)
        {
            t.Append('^').Append(letter).Append(':')
                .Append(a.UnitsToday.ToString(Inv)).Append('|').Append(a.GrossToday.ToString(Inv)).Append('|').Append(a.KeptToday.ToString(Inv)).Append('|')
                .Append(a.TipsToday.ToString(Inv)).Append('|').Append(a.NetToday.ToString(Inv)).Append('|').Append(a.FuelToday.ToString(Inv)).Append('|')
                .Append(a.UnitsBefore.ToString(Inv)).Append('|').Append(a.NetBefore.ToString(Inv)).Append('|').Append(a.Shifts.ToString(Inv)).Append('|')
                .Append(a.Units.ToString(Inv)).Append('|').Append(a.Net.ToString(Inv)).Append('|').Append(a.Kept.ToString(Inv));
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static SideJobState Read(string text, out int version)
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
            var s = new SideJobState();
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length < 2 || part[1] != ':')
                {
                    continue;
                }
                string[] f = part.Substring(2).Split('|');
                switch (part[0])
                {
                    case 'h':
                        s.Seed = Int(f, 0);
                        s.Day = Int(f, 1, -1);
                        break;
                    case 'c':
                        Tally(f, s.At(SideKind.Cab));
                        break;
                    case 'p':
                        Tally(f, s.At(SideKind.Parcels));
                        break;
                }
            }
            s.Bound();
            return s;
        }

        private static void Tally(string[] f, SideTally a)
        {
            a.UnitsToday = Int(f, 0);
            a.GrossToday = Int(f, 1);
            a.KeptToday = Int(f, 2);
            a.TipsToday = Int(f, 3);
            a.NetToday = Int(f, 4);
            a.FuelToday = Int(f, 5);
            a.UnitsBefore = Int(f, 6);
            a.NetBefore = Int(f, 7);
            a.Shifts = Int(f, 8);
            a.Units = Num(f, 9);
            a.Net = Num(f, 10);
            a.Kept = Num(f, 11);
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
