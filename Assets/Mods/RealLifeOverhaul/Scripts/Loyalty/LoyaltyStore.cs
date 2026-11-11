using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Loyalty
{
    /// <summary>
    /// The loyalty schemes in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=1^f:place|on|want|rate|nextRate|push|offer|nextOffer|offerAsked|offerFrom|since|members|base|lastDay|lift|basket|open|bad|added|carried|paid;...
    ///        ^w:place|day|visits|signups|lapsed|rewards|costs|extra|margin;...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'. f is a scheme, w a
    /// day of its week (a place is escaped). A newer version adds fields at the end of a record or a part with a new letter: this
    /// reader takes what it knows and puts every figure inside its bounds. A save from before the feature has no line. Numbers
    /// are written and read in the invariant culture.
    /// </summary>
    public static class LoyaltyStore
    {
        public const string Key = "reallife:loyalty";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static LoyaltyState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // a line that cannot be read is kept as it is (Core/SaveLines)
        }

        public static string Write(LoyaltyState s)
        {
            var t = new StringBuilder(32 + s.Files.Count * 160);
            t.Append("v=").Append(Version.ToString(Inv));
            if (s.Files.Count > 0)
            {
                t.Append("^f:");
                for (int i = 0; i < s.Files.Count; i++)
                {
                    LoyaltyFile f = s.Files[i];
                    t.Append(i > 0 ? ";" : string.Empty).Append(Uri.EscapeDataString(f.Place ?? string.Empty)).Append('|');
                    Join(t, f.On ? 1 : 0, f.Want ? 1 : 0, f.Rate, f.NextRate, f.Push, f.OfferDay, f.NextOffer, f.OfferAsked, f.OfferFrom, f.Since, f.Members, f.Base, f.LastDay,
                        f.Lift, f.Basket, f.OpenHours, f.BadHours, f.Added, f.Carried, f.Paid);
                }
            }
            bool any = false;
            for (int i = 0; i < s.Files.Count; i++)
            {
                LoyaltyFile f = s.Files[i];
                for (int k = 0; k < f.Week.Length; k++)
                {
                    LoyaltyDay d = f.Week[k];
                    if (d.Day <= 0)
                    {
                        continue;
                    }
                    t.Append(any ? ";" : "^w:").Append(Uri.EscapeDataString(f.Place ?? string.Empty)).Append('|');
                    Join(t, d.Day, d.Visits, d.Signups, d.Lapsed, d.Rewards, d.Costs, d.Extra, d.Margin);
                    any = true;
                }
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static LoyaltyState Read(string text, out int version)
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
            var s = new LoyaltyState();
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
                    case 'f':
                        foreach (string record in body.Split(';'))
                        {
                            string[] r = record.Split('|');
                            string place = r.Length >= 14 ? Uri.UnescapeDataString(r[0]) : string.Empty;
                            if (place.Length == 0 || s.File(place) != null || s.Files.Count >= LoyaltyRules.FilesMost)
                            {
                                continue;
                            }
                            var f = new LoyaltyFile
                            {
                                Place = place, On = Num(r, 1) != 0, Want = Num(r, 2) != 0, Rate = Int(r, 3, LoyaltyRules.RateStart), NextRate = Int(r, 4, LoyaltyRules.RateStart),
                                Push = Int(r, 5), OfferDay = Int(r, 6, LoyaltyFile.NoDay), NextOffer = Int(r, 7, LoyaltyFile.NoDay), OfferAsked = Int(r, 8, LoyaltyFile.NoDay),
                                OfferFrom = Int(r, 9), Since = Int(r, 10), Members = Int(r, 11), Base = Int(r, 12), LastDay = Int(r, 13), Lift = Int(r, 14), Basket = Int(r, 15),
                                OpenHours = Int(r, 16), BadHours = Int(r, 17), Added = Int(r, 18), Carried = Int(r, 19), Paid = Num(r, 20)
                            };
                            LoyaltyRules.Bound(f);
                            s.Files.Add(f);
                        }
                        break;
                    case 'w':
                        foreach (string record in body.Split(';'))
                        {
                            string[] r = record.Split('|');
                            LoyaltyFile f = r.Length >= 9 ? s.File(Uri.UnescapeDataString(r[0])) : null;
                            int day = f != null ? Int(r, 1) : 0;
                            if (day <= 0)
                            {
                                continue;
                            }
                            f.Week[day % LoyaltyRules.WeekDays] = new LoyaltyDay
                            {
                                Day = day, Visits = Math.Max(0, Int(r, 2)), Signups = Math.Max(0, Int(r, 3)), Lapsed = Math.Max(0, Int(r, 4)), Rewards = Math.Max(0L, Num(r, 5)),
                                Costs = Math.Max(0L, Num(r, 6)), Extra = Math.Max(0L, Num(r, 7)), Margin = Math.Max(0L, Num(r, 8))
                            };
                        }
                        break;
                }
            }
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
