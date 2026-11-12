using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The days of net worth in a save: one entry of the save's data, short lines. Game-free.
    ///
    ///   reallife:worth_days
    ///     WD|version
    ///     P|day:worth,day:worth,...           the player, oldest first (sixty days at most)
    ///     T|index|day:worth,day:worth,...     a rival tycoon by their index (thirty days at most)
    ///
    /// Numbers in the invariant culture. A newer version adds fields at the end or lines with a new letter: the reader takes what
    /// it knows and skips the rest. A save without the line starts with no days (the card says its line fills as days pass); a line
    /// of a later version, or one that cannot be read, gives nothing and is kept as it was read (Core/SaveLines).
    /// </summary>
    public static class WorthStore
    {
        public const string Key = "reallife:worth_days";
        public const int Version = 1;

        private const string Head = "WD";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(WorthBook b)
        {
            var s = new StringBuilder(64 + b.Player.Count * 18);
            s.Append(Head).Append('|').Append(Version.ToString(Inv));
            s.Append("\nP|");
            Ring(s, b.Player);
            for (int i = 0; i < b.Rivals.Length; i++)
            {
                if (b.Rivals[i].Count > 0)
                {
                    s.Append("\nT|").Append(i.ToString(Inv)).Append('|');
                    Ring(s, b.Rivals[i]);
                }
            }
            return s.ToString();
        }

        private static void Ring(StringBuilder s, NetWorthRing r)
        {
            for (int i = 0; i < r.Count; i++)
            {
                s.Append(i > 0 ? "," : string.Empty).Append(r.DayAt(i).ToString(Inv)).Append(':').Append(r.WorthAt(i).ToString(Inv));
            }
        }

        /// <summary>
        /// A line into a book. An empty or missing line is a save without one: true, an empty book. False (and an empty book) for another
        /// version's line or one that cannot be read, which the caller keeps as it was.
        /// </summary>
        public static bool Read(string text, out WorthBook book)
        {
            book = new WorthBook();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            try
            {
                string[] lines = text.Split('\n');
                string[] h = lines[0].TrimEnd('\r').Split('|');
                if (h.Length < 2 || h[0] != Head || !int.TryParse(h[1], NumberStyles.Integer, Inv, out int version) || version != Version)
                {
                    book = new WorthBook();
                    return false;
                }
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    if (c.Length >= 2 && c[0] == "P")
                    {
                        if (!Fill(book.Player, c[1]))
                        {
                            book = new WorthBook();
                            return false;
                        }
                    }
                    else if (c.Length >= 3 && c[0] == "T")
                    {
                        if (!int.TryParse(c[1], NumberStyles.Integer, Inv, out int who) || who < 0)
                        {
                            book = new WorthBook();
                            return false;
                        }
                        if (who < book.Rivals.Length && !Fill(book.Rivals[who], c[2]))
                        {
                            book = new WorthBook();
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                book = new WorthBook();
                return false;
            }
        }

        private static bool Fill(NetWorthRing ring, string text)
        {
            if (text.Length == 0)
            {
                return true;
            }
            string[] pairs = text.Split(',');
            int last = int.MinValue;
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] p = pairs[i].Split(':');
                if (p.Length != 2 || !int.TryParse(p[0], NumberStyles.Integer, Inv, out int day) || !long.TryParse(p[1], NumberStyles.Integer, Inv, out long worth) || day <= last)
                {
                    return false;   // days run forward: anything else is a damaged line
                }
                last = day;
                ring.Push(day, worth);
            }
            return true;
        }
    }
}
