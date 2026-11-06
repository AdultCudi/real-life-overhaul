using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// The Economy app's history as one line of the save's modData, <c>v=1~l:{last day}~s:{figure}|{first day}|{readings};...</c>
    /// (parts by '~', figures by ';', fields by '|'). A figure's readings run from its first day in the window to its newest, one
    /// token each, split by ',': <c>=n</c> a reading, a bare number its change from the day before, nothing "the same as the day
    /// before" and <c>n</c> a day with none; after a gap the next reading is written whole. The figures change mostly once a
    /// month, so a year is a few hundred characters. A later version adds figures or fields at the end; this reader skips what it
    /// does not know. A save without the line starts an empty book. Game-free (EconomyCheck's OutlookChecks).
    /// </summary>
    public static class OutlookStore
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private const int DayMost = 10000000;          // a day past this is a damaged line, not a game
        private const long DeltaMost = 1000000000L;    // a change past this is written whole

        /// <summary>The book of this save; null if the save has none or it cannot be read (the caller starts afresh and the line is kept).</summary>
        public static OutlookBook Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(OutlookCatalogue.Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, OutlookCatalogue.Key, Read(text, out version), version, OutlookCatalogue.Version);
        }

        public static string Write(OutlookBook book)
        {
            var t = new StringBuilder(256 + book.Lines * 160);
            t.Append("v=").Append(OutlookCatalogue.Version).Append("~l:").Append(book.LastDay.ToString(Inv)).Append("~s:");
            bool first = true;
            foreach (string name in book.Names())
            {
                int from = FirstDay(book, name, out int last);
                if (from == 0)
                {
                    continue;   // nothing of it in the window: not kept
                }
                t.Append(first ? string.Empty : ";").Append(OutlookCatalogue.Clean(name)).Append('|').Append(from.ToString(Inv)).Append('|');
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
            return t.ToString();
        }

        // The first day of a figure in the window and its newest; 0 for none.
        private static int FirstDay(OutlookBook book, string name, out int last)
        {
            last = 0;
            int from = 0;
            for (int day = Math.Max(1, book.LastDay - OutlookCatalogue.HistoryDays + 1); day <= book.LastDay; day++)
            {
                if (book.Get(name, day, out _))
                {
                    from = from == 0 ? day : from;
                    last = day;
                }
            }
            return from;
        }

        /// <summary>Reads a line. Null for one that is not the Economy app's at all or is damaged.</summary>
        public static OutlookBook Read(string text, out int version)
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
                var book = new OutlookBook();
                int declared = 0;
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
                            if (!int.TryParse(body, NumberStyles.Integer, Inv, out declared) || declared < 0 || declared > DayMost)
                            {
                                return null;
                            }
                            header = true;
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
                    }
                }
                return header ? book : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // One figure: false for a record that is damaged, so the whole line is.
        private static bool Figure(OutlookBook book, string record)
        {
            string[] c = record.Split('|');
            if (c.Length < 3 || c[0].Length == 0 || !int.TryParse(c[1], NumberStyles.Integer, Inv, out int from) || from < 1 || from > DayMost)
            {
                return false;
            }
            string[] tokens = c[2].Split(',');
            if (tokens.Length > OutlookCatalogue.HistoryDays + 1)
            {
                return false;
            }
            bool known = false;
            int before = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                int value;
                if (token == "n")
                {
                    known = false;
                    continue;
                }
                if (token.Length > 0 && token[0] == '=')
                {
                    if (!int.TryParse(token.Substring(1), NumberStyles.Integer, Inv, out value) || value == OutlookCatalogue.NoValue)
                    {
                        return false;
                    }
                }
                else if (!known)
                {
                    return false;   // a change with nothing before it
                }
                else if (token.Length == 0)
                {
                    value = before;
                }
                else
                {
                    if (!long.TryParse(token, NumberStyles.Integer, Inv, out long change) || before + change > int.MaxValue || before + change <= int.MinValue)
                    {
                        return false;
                    }
                    value = (int)(before + change);
                }
                book.Set(c[0], from + i, value);
                known = true;
                before = value;
            }
            return true;
        }
    }
}
