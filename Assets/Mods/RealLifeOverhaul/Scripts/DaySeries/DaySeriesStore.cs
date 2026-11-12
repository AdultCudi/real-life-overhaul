using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.DaySeries
{
    /// <summary>
    /// The day series as one line of the save, <c>v=1~l:{day}~d:{seed}~c:{from}|{until}~h:{plans already held}~k:{plans the cut held}~s:{figures}~n:{loans}</c>.
    /// A figure's readings run from its first day in the window, one token each: <c>=n</c> a reading, a bare number its change
    /// from the day before, nothing "the same", and <c>n</c> a day with none. A later version adds a section at the end; this
    /// reader skips a letter it does not know. A line that is not the series, or is damaged, is kept as it was read.
    /// </summary>
    public static class DaySeriesStore
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int DayMost = 10000000;
        private const long DeltaMost = 1000000000L;

        public static DaySeriesBook Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(DaySeriesCatalogue.Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, DaySeriesCatalogue.Key, Read(text, out version), version, DaySeriesCatalogue.Version);
        }

        public static string Write(DaySeriesBook book)
        {
            var t = new StringBuilder(256 + book.Lines * 80);
            t.Append("v=").Append(DaySeriesCatalogue.Version.ToString(Inv));
            t.Append("~l:").Append(book.LastDay.ToString(Inv));
            t.Append("~d:").Append(book.Seed.ToString(Inv));
            t.Append("~c:").Append(book.CutFrom.ToString(Inv)).Append('|').Append(book.CutUntil.ToString(Inv));
            if (!string.IsNullOrEmpty(book.HeldAlready))
            {
                t.Append("~h:").Append(DaySeriesCatalogue.Clean(book.HeldAlready));
            }
            if (!string.IsNullOrEmpty(book.HeldByCut))
            {
                t.Append("~k:").Append(DaySeriesCatalogue.Clean(book.HeldByCut));
            }
            t.Append("~s:");
            bool first = true;
            foreach (string name in book.Names())
            {
                int from = FirstDay(book, name, out int last);
                if (from == 0)
                {
                    continue;
                }
                t.Append(first ? string.Empty : ";").Append(DaySeriesCatalogue.Clean(name)).Append('|').Append(from.ToString(Inv)).Append('|');
                first = false;
                bool known = false;
                int before = 0;
                for (int day = from; day <= last; day++)
                {
                    if (day > from)
                    {
                        t.Append(',');
                    }
                    if (!book.Get(name, day, out int value))
                    {
                        t.Append('n');
                        known = false;
                        continue;
                    }
                    long change = (long)value - before;
                    if (!known || change > DeltaMost || change < -DeltaMost)
                    {
                        t.Append('=').Append(value.ToString(Inv));
                    }
                    else if (change != 0)
                    {
                        t.Append(change.ToString(Inv));
                    }
                    known = true;
                    before = value;
                }
            }
            t.Append("~n:");
            var loans = new List<DayLoan>(book.LoanCount);
            book.CopyLoans(loans);
            for (int i = 0; i < loans.Count; i++)
            {
                DayLoan loan = loans[i];
                if (i > 0)
                {
                    t.Append(';');
                }
                t.Append(DaySeriesCatalogue.Clean(loan.Who)).Append('|').Append(loan.Rank.ToString(Inv)).Append('|')
                    .Append(loan.Day.ToString(Inv)).Append('|').Append(loan.Amount.ToString(Inv));
            }
            return t.ToString();
        }

        private static int FirstDay(DaySeriesBook book, string name, out int last)
        {
            last = 0;
            int from = 0;
            for (int day = Math.Max(1, book.LastDay - DaySeriesCatalogue.Days + 1); day <= book.LastDay; day++)
            {
                if (book.Get(name, day, out _))
                {
                    from = from == 0 ? day : from;
                    last = day;
                }
            }
            return from;
        }

        public static DaySeriesBook Read(string text, out int version)
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
                var book = new DaySeriesBook();
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
                        case 'l':
                            if (!int.TryParse(body, NumberStyles.Integer, Inv, out int declared) || declared < 0 || declared > DayMost)
                            {
                                return null;
                            }
                            header = true;
                            break;
                        case 'd':
                            if (!int.TryParse(body, NumberStyles.Integer, Inv, out int seed))
                            {
                                return null;
                            }
                            book.Seed = seed;
                            break;
                        case 'c':
                            string[] cut = body.Split('|');
                            if (cut.Length < 2 || !int.TryParse(cut[0], NumberStyles.Integer, Inv, out int fromDay) || !int.TryParse(cut[1], NumberStyles.Integer, Inv, out int until)
                                || fromDay < 0 || until < 0 || fromDay > DayMost || until > DayMost)
                            {
                                return null;
                            }
                            book.CutFrom = fromDay;
                            book.CutUntil = until;
                            break;
                        case 'h':
                            book.HeldAlready = body;
                            break;
                        case 'k':
                            book.HeldByCut = body;
                            break;
                        case 's':
                            if (body.Length > 0)
                            {
                                foreach (string record in body.Split(';'))
                                {
                                    if (!Figure(book, record))
                                    {
                                        return null;
                                    }
                                }
                            }
                            break;
                        case 'n':
                            if (body.Length > 0 && !Loans(book, body))
                            {
                                return null;
                            }
                            break;
                    }
                }
                return header ? book : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Figure(DaySeriesBook book, string record)
        {
            string[] c = record.Split('|');
            if (c.Length < 3 || c[0].Length == 0 || !int.TryParse(c[1], NumberStyles.Integer, Inv, out int from) || from < 1 || from > DayMost)
            {
                return false;
            }
            string[] tokens = c[2].Split(',');
            if (tokens.Length > DaySeriesCatalogue.Days + 1)
            {
                return false;
            }
            bool known = false;
            int before = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (token == "n")
                {
                    known = false;
                    continue;
                }
                int value;
                if (token.Length > 0 && token[0] == '=')
                {
                    if (!int.TryParse(token.Substring(1), NumberStyles.Integer, Inv, out value) || value == DaySeriesCatalogue.NoValue)
                    {
                        return false;
                    }
                }
                else if (token.Length == 0)
                {
                    if (!known)
                    {
                        return false;
                    }
                    value = before;
                }
                else
                {
                    if (!known || !int.TryParse(token, NumberStyles.Integer, Inv, out int delta))
                    {
                        return false;
                    }
                    long next = (long)before + delta;
                    if (next > int.MaxValue || next < int.MinValue || next == DaySeriesCatalogue.NoValue)
                    {
                        return false;
                    }
                    value = (int)next;
                }
                if (!book.Set(c[0], from + i, value))
                {
                    return false;
                }
                known = true;
                before = value;
            }
            return true;
        }

        private static bool Loans(DaySeriesBook book, string body)
        {
            string[] records = body.Split(';');
            if (records.Length > DaySeriesCatalogue.LoansMost + 1)
            {
                return false;
            }
            for (int i = 0; i < records.Length; i++)
            {
                string[] c = records[i].Split('|');
                if (c.Length < 4 || c[0].Length == 0 || !int.TryParse(c[1], NumberStyles.Integer, Inv, out int rank) || rank < 0
                    || !int.TryParse(c[2], NumberStyles.Integer, Inv, out int day) || day < 1 || day > DayMost
                    || !int.TryParse(c[3], NumberStyles.Integer, Inv, out int amount) || amount <= 0)
                {
                    return false;
                }
                book.AddLoan(c[0], rank, day, amount);
            }
            return true;
        }
    }
}
