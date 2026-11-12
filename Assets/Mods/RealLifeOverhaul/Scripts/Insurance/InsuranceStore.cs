using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>
    /// Insurance in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=1^h:...^c:...;...^m:...;...^k:...;...^x:...^r:...;...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'. h is the header, c the cars' policies, m the homes'
    /// policies, k the claims paid, x the days of the crashes that still count, r the rival tycoons' cover. A newer version adds fields at the end of a record or a
    /// part with a new letter: this reader takes what it knows and puts every figure inside its bounds. A line from before the feature is none at all. Every number is
    /// written and read in the invariant culture, and every name is escaped, so no name can break a record.
    /// </summary>
    public static class InsuranceStore
    {
        public const string Key = "reallife:insurance";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static InsuranceState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
        }

        public static string Write(InsuranceState s)
        {
            var t = new StringBuilder(128 + s.Cars.Count * 48 + s.Homes.Count * 64 + s.Claims.Count * 48);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.LastDay);
            if (s.Cars.Count > 0)
            {
                t.Append("^c:");
                for (int i = 0; i < s.Cars.Count; i++)
                {
                    CarPolicy p = s.Cars[i];
                    t.Append(i > 0 ? ";" : string.Empty).Append(Uri.EscapeDataString(p.Id ?? string.Empty)).Append('|');
                    Join(t, p.Tier, p.Since, p.Pre, p.NoClaimMonths, p.GraceFrom, p.Year, p.PaidYear, p.ClaimsYear, p.LastPremium, p.Live ? 1 : 0);
                }
            }
            if (s.Homes.Count > 0)
            {
                t.Append("^m:");
                for (int i = 0; i < s.Homes.Count; i++)
                {
                    HomePolicy p = s.Homes[i];
                    t.Append(i > 0 ? ";" : string.Empty).Append(Uri.EscapeDataString(p.Place ?? string.Empty)).Append('|');
                    Join(t, p.Tier, p.Active ? 1 : 0, p.Since, p.NextDue, p.GraceFrom, p.NoClaimMonths, p.Year, p.PaidYear, p.ClaimsYear, p.Premium);
                    t.Append('|').Append(Uri.EscapeDataString(p.Name ?? string.Empty)).Append('|');
                    for (int d = 0; d < p.ClaimDays.Count; d++)
                    {
                        t.Append(d > 0 ? "," : string.Empty).Append(p.ClaimDays[d].ToString(Inv));
                    }
                }
            }
            if (s.Claims.Count > 0)
            {
                t.Append("^k:");
                for (int i = 0; i < s.Claims.Count; i++)
                {
                    ClaimNote n = s.Claims[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, n.Day, n.Line, n.Kind, n.Loss, n.Excess, n.Payout);
                    t.Append('|').Append(Uri.EscapeDataString(n.What ?? string.Empty));
                }
            }
            if (s.CrashDays.Count > 0)
            {
                t.Append("^x:");
                for (int i = 0; i < s.CrashDays.Count; i++)
                {
                    t.Append(i > 0 ? "|" : string.Empty).Append(s.CrashDays[i].ToString(Inv));
                }
            }
            t.Append("^r:");
            for (int i = 0; i < s.Rivals.Length; i++)
            {
                RivalCover r = s.Rivals[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, r.Plan, r.NextDue, r.Missed, r.Year, r.PaidYear, r.PremiumsYear);
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static InsuranceState Read(string text, out int version)
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
            var s = new InsuranceState();
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
                    case 'h': s.LastDay = Math.Max(0, I(body.Split('|'), 0)); break;
                    case 'c': Cars(s, body); break;
                    case 'm': Homes(s, body); break;
                    case 'k': Claims(s, body); break;
                    case 'x': Crashes(s, body); break;
                    case 'r': Rivals(s, body); break;
                }
            }
            return s;
        }

        private static void Cars(InsuranceState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length; i++)
            {
                string[] f = records[i].Split('|');
                string id = Unescape(f[0]);
                if (f.Length < 11 || id.Length == 0 || s.Car(id) != null)
                {
                    continue;
                }
                s.Cars.Add(new CarPolicy
                {
                    Id = id, Tier = InsuranceCatalogue.Tier(I(f, 1, InsuranceCatalogue.StandardTier)), Since = Math.Max(0, I(f, 2)), Pre = InsuranceCatalogue.Clamp(I(f, 3), 0, 1000),
                    NoClaimMonths = InsuranceCatalogue.Clamp(I(f, 4), 0, InsuranceCatalogue.NoClaimMost / InsuranceCatalogue.NoClaimStep), GraceFrom = Math.Max(0, I(f, 5)),
                    Year = I(f, 6, -1), PaidYear = Math.Max(0L, L(f, 7)), ClaimsYear = Math.Max(0, I(f, 8)), LastPremium = Math.Max(0, I(f, 9)), Live = I(f, 10) == 1
                });
            }
        }

        private static void Homes(InsuranceState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length; i++)
            {
                string[] f = records[i].Split('|');
                string place = Unescape(f[0]);
                if (f.Length < 12 || place.Length == 0 || s.Home(place) != null)
                {
                    continue;
                }
                var p = new HomePolicy
                {
                    Place = place, Tier = InsuranceCatalogue.Tier(I(f, 1, InsuranceCatalogue.StandardTier)), Active = I(f, 2) == 1, Since = Math.Max(0, I(f, 3)),
                    NextDue = Math.Max(0, I(f, 4)), GraceFrom = Math.Max(0, I(f, 5)),
                    NoClaimMonths = InsuranceCatalogue.Clamp(I(f, 6), 0, InsuranceCatalogue.NoClaimMost / InsuranceCatalogue.NoClaimStep), Year = I(f, 7, -1),
                    PaidYear = Math.Max(0L, L(f, 8)), ClaimsYear = Math.Max(0, I(f, 9)), Premium = Math.Max(0, I(f, 10)), Name = Unescape(f[11])
                };
                if (p.Active && p.NextDue <= 0)
                {
                    p.Active = false;   // a policy with no day its premium is due on cannot be kept: read as ended, its record kept
                }
                string[] days = f.Length > 12 ? f[12].Split(',') : new string[0];
                for (int d = 0; d < days.Length && p.ClaimDays.Count < 8; d++)
                {
                    if (int.TryParse(days[d], NumberStyles.Integer, Inv, out int day) && day > 0)
                    {
                        p.ClaimDays.Add(day);
                    }
                }
                s.Homes.Add(p);
            }
        }

        private static void Claims(InsuranceState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && s.Claims.Count < InsuranceCatalogue.ClaimsKept; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length < 7 || L(f, 5) <= 0)
                {
                    continue;
                }
                long loss = Math.Max(0L, L(f, 3));
                s.Claims.Add(new ClaimNote
                {
                    Day = Math.Max(0, I(f, 0)), Line = InsuranceCatalogue.Clamp(I(f, 1), 0, (int)CoverLine.Count - 1), Kind = InsuranceCatalogue.Clamp(I(f, 2), 0, (int)ClaimKind.Count - 1),
                    Loss = loss, Excess = InsuranceCatalogue.Clamp(L(f, 4), 0L, loss), Payout = InsuranceCatalogue.Clamp(L(f, 5), 0L, loss), What = Unescape(f[6])
                });
            }
        }

        private static void Crashes(InsuranceState s, string body)
        {
            string[] f = body.Split('|');
            for (int i = 0; i < f.Length && s.CrashDays.Count < InsuranceCatalogue.CrashesMost; i++)
            {
                if (int.TryParse(f[i], NumberStyles.Integer, Inv, out int day) && day > 0)
                {
                    s.CrashDays.Add(day);
                }
            }
        }

        private static void Rivals(InsuranceState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && i < s.Rivals.Length; i++)
            {
                string[] f = records[i].Split('|');
                RivalCover r = s.Rivals[i];
                r.Plan = InsuranceCatalogue.Clamp(I(f, 0), 0, 3);
                r.NextDue = Math.Max(0, I(f, 1));
                r.Missed = Math.Max(0, I(f, 2));
                r.Year = I(f, 3, -1);
                r.PaidYear = Math.Max(0L, L(f, 4));
                r.PremiumsYear = Math.Max(0L, L(f, 5));
            }
        }

        private static void Join(StringBuilder t, params long[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static int I(string[] f, int at, int fallback = 0) => at < f.Length && int.TryParse(f[at], NumberStyles.Integer, Inv, out int v) ? v : fallback;

        private static long L(string[] f, int at) => at < f.Length && long.TryParse(f[at], NumberStyles.Integer, Inv, out long v) ? v : 0L;

        private static string Unescape(string text)
        {
            try
            {
                return string.IsNullOrEmpty(text) ? string.Empty : Uri.UnescapeDataString(text);
            }
            catch (UriFormatException)
            {
                return string.Empty;
            }
        }
    }
}
