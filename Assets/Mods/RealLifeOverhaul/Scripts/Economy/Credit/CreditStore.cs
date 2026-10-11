using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The credit file in the save: one line of the save's modData, under <see cref="Key"/>. Game-free.
    ///
    ///     v=1~h:...~d:...~e:...~f:...~l:...
    ///
    /// Parts are divided by '~' and begin with a letter and a colon; records inside a part by ';', fields by '|'.
    ///   h  the header: the numbers of <see cref="CreditFile"/>, in the order of <see cref="Save"/>
    ///   d  the instalments of each of the last sixty days: day|on time|missed|part|test on time|test missed
    ///   e  the last 120 events: kind|day|amount|count|effect|test|source
    ///   f  the folded totals, one record per kind: count|amount
    ///   l  the loans as last seen: street|number|total|first day|remaining|payment|interest
    ///
    /// A NEWER VERSION adds fields at the end of a record or a part with a new letter; this reader takes the fields it
    /// knows and skips parts it does not, so a save written by a later version still loads (and what it does not know
    /// is not written back). A change that cannot be read that way raises <see cref="Version"/> and gets a step in
    /// <see cref="Upgrade"/>. Nothing here is the game's: the loans themselves are the game's own records, and a save
    /// loads in the plain game with this line ignored.
    /// </summary>
    public static class CreditStore
    {
        public const string Key = "reallife:economy";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The file of this save; null (and <paramref name="fresh"/> true) if the save has none or it cannot be read.</summary>
        public static CreditFile Load(Dictionary<string, string> data, out bool fresh, out int version)
        {
            fresh = true;
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                CreditFile file = Read(text, out version);
                if (file == null)
                {
                    return null;
                }
                Upgrade(file, version);
                fresh = false;
                return file;
            }
            catch (Exception)
            {
                return null;   // a damaged line: the file is opened afresh from what the save shows
            }
        }

        public static void Save(Dictionary<string, string> data, CreditFile file)
        {
            if (data != null && file != null)
            {
                data[Key] = Write(file);
            }
        }

        /// <summary>What an older version's file needs before this version uses it. Version 1 is the first.</summary>
        private static void Upgrade(CreditFile file, int from)
        {
        }

        public static string Write(CreditFile f)
        {
            var t = new StringBuilder(512 + f.Events.Count * 28 + f.Days.Count * 16);
            t.Append("v=").Append(Version).Append("~h:");
            Join(t, f.StartDay, f.FirstCreditDay, f.OldOnTime, f.OldMissed, f.OldPart, f.TestOffset, f.LastScore, f.LastGrade, f.SettledDay,
                f.UnseenMissed, f.UnseenGrade ? 1 : 0, f.LastMissedDay, f.LastMissedCount, f.LastMissedAmount, f.LastGradeDay, f.LastGradeWas,
                f.LegalSeen.Convictions, f.LegalSeen.Judgments, f.LegalSeen.LateDays, f.LegalSeen.Outstanding, f.LegalSeen.RecentOffences);
            t.Append("~d:");
            for (int i = 0; i < f.Days.Count; i++)
            {
                PayDay d = f.Days[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, d.Day, d.OnTime, d.Missed, d.Part, d.TestOnTime, d.TestMissed);
            }
            t.Append("~e:");
            for (int i = 0; i < f.Events.Count; i++)
            {
                CreditEvent e = f.Events[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, (int)e.Kind, e.Day, e.Amount, e.Count, e.Effect, e.Test ? 1 : 0);
                t.Append('|').Append(Clean(e.Source));
            }
            t.Append("~f:");
            for (int k = 0; k < f.FoldedCount.Length; k++)
            {
                t.Append(k > 0 ? ";" : string.Empty).Append(f.FoldedCount[k].ToString(Inv)).Append('|').Append(f.FoldedAmount[k].ToString(Inv));
            }
            t.Append("~l:");
            for (int i = 0; i < f.Loans.Count; i++)
            {
                LoanMark m = f.Loans[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(m.Street)).Append('|').Append(m.Number.ToString(Inv)).Append('|')
                    .Append(m.Total.ToString("R", Inv)).Append('|').Append(m.FirstDay.ToString(Inv)).Append('|')
                    .Append(m.Remaining.ToString("R", Inv)).Append('|').Append(m.Payment.ToString(Inv)).Append('|').Append(m.Interest.ToString(Inv));
            }
            return t.ToString();
        }

        public static CreditFile Read(string text, out int version)
        {
            version = 0;
            string[] parts = text.Split('~');
            if (parts.Length == 0 || !parts[0].StartsWith("v=", StringComparison.Ordinal) || !int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var f = new CreditFile();
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
                    case 'h': Header(f, body.Split('|')); break;
                    case 'd':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length >= 4 && I(c, 0) > 0)
                            {
                                f.Days.Add(new PayDay { Day = I(c, 0), OnTime = Count(c, 1), Missed = Count(c, 2), Part = Count(c, 3), TestOnTime = Count(c, 4), TestMissed = Count(c, 5) });
                            }
                        }
                        f.Days.Sort((a, b) => a.Day.CompareTo(b.Day));
                        break;
                    case 'e':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            int kind = I(c, 0, -1);
                            if (c.Length >= 3 && kind >= 0 && kind < (int)CreditKind.Count && f.Events.Count < CreditCatalogue.LedgerMost)
                            {
                                f.Events.Add(new CreditEvent
                                {
                                    Kind = (CreditKind)kind, Day = I(c, 1), Amount = I(c, 2), Count = Math.Max(1, I(c, 3, 1)), Effect = I(c, 4),
                                    Test = I(c, 5) != 0, Source = c.Length > 6 ? c[6] : string.Empty
                                });
                            }
                        }
                        break;
                    case 'f':
                        {
                            string[] records = body.Split(';');
                            for (int k = 0; k < records.Length && k < f.FoldedCount.Length; k++)
                            {
                                string[] c = records[k].Split('|');
                                f.FoldedCount[k] = Count(c, 0);
                                long amount;
                                f.FoldedAmount[k] = c.Length > 1 && long.TryParse(c[1], NumberStyles.Integer, Inv, out amount) ? amount : 0L;
                            }
                        }
                        break;
                    case 'l':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length >= 7)
                            {
                                f.Loans.Add(new LoanMark
                                {
                                    Street = c[0], Number = I(c, 1), Total = F(c, 2), FirstDay = I(c, 3), Remaining = F(c, 4), Payment = I(c, 5), Interest = I(c, 6)
                                });
                            }
                        }
                        break;
                }
            }
            return f;
        }

        private static void Header(CreditFile f, string[] c)
        {
            f.StartDay = I(c, 0);
            f.FirstCreditDay = Math.Max(0, I(c, 1));
            f.OldOnTime = Count(c, 2);
            f.OldMissed = Count(c, 3);
            f.OldPart = Count(c, 4);
            f.TestOffset = I(c, 5);
            f.LastScore = I(c, 6);
            f.LastGrade = I(c, 7, -1);
            f.SettledDay = I(c, 8);
            f.UnseenMissed = Count(c, 9);
            f.UnseenGrade = I(c, 10) != 0;
            f.LastMissedDay = I(c, 11);
            f.LastMissedCount = Count(c, 12);
            f.LastMissedAmount = Count(c, 13);
            f.LastGradeDay = I(c, 14);
            f.LastGradeWas = I(c, 15, -1);
            f.LegalSeen = new LegalStanding { Convictions = Count(c, 16), Judgments = Count(c, 17), LateDays = Count(c, 18), Outstanding = Count(c, 19), RecentOffences = Count(c, 20) };
            if (f.LastScore != 0)
            {
                f.LastScore = CreditCatalogue.Clamp(f.LastScore);
            }
            if (f.LastGrade < -1 || f.LastGrade > (int)CreditGrade.Excellent)
            {
                f.LastGrade = -1;
            }
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

        private static int Count(string[] c, int at)
        {
            return Math.Max(0, I(c, at));
        }

        private static float F(string[] c, int at)
        {
            float value;
            return at < c.Length && float.TryParse(c[at], NumberStyles.Float, Inv, out value) && !float.IsNaN(value) && !float.IsInfinity(value) ? value : 0f;
        }

        /// <summary>A text with the three dividers taken out, so that it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('~', ' ');
        }
    }
}
