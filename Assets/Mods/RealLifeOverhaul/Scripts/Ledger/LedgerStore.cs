using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The City Ledger in a save: one entry of the save's data, a few short lines.
    ///
    ///   LG1|subscribed|cancelled|paid until|since|weeks|last morning|copy day|delivered|bought|copies|kiosks for
    ///   K|x|y|z|heading|building                     a kiosk near the home
    ///   V|policies                                    the council's policies at the last edition
    ///   E|day|has market|market day|index|change|week|rate|inflation|cycle   the latest edition
    ///   P|section|weight|key|head|name|value...       a piece of it
    ///   S|key|level|change                            a sector
    ///   Q|name|ticker|sector|close|change|volume|marks a company's price
    ///   C|close,close,...                             the index's last closes
    ///
    /// Numbers are invariant; a line that does not read is left out, and an entry of another version is not read at all
    /// (a new state). Game-free.
    /// </summary>
    public static class LedgerStore
    {
        public const string Key = "reallife:ledger";
        private const string Version = "LG1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Save(Dictionary<string, string> data, LedgerState s)
        {
            if (data == null || s == null)
            {
                return;
            }
            data[Key] = Write(s);
        }

        public static string Write(LedgerState s)
        {
            var b = new StringBuilder(4096);
            b.Append(Version).Append('|').Append(Bit(s.Subscribed)).Append('|').Append(Bit(s.Cancelled)).Append('|').Append(s.PaidUntil).Append('|').Append(s.Since)
                .Append('|').Append(s.Weeks).Append('|').Append(s.LastMorning).Append('|').Append(s.CopyDay).Append('|').Append(Bit(s.Delivered)).Append('|').Append(Bit(s.Bought))
                .Append('|').Append(s.Copies).Append('|').Append(Clean(s.KiosksFor));
            for (int i = 0; i < s.Kiosks.Count; i++)
            {
                KioskSpot k = s.Kiosks[i];
                b.Append("\nK|").Append(k.X.ToString("R", Inv)).Append('|').Append(k.Y.ToString("R", Inv)).Append('|').Append(k.Z.ToString("R", Inv)).Append('|')
                    .Append(k.Heading.ToString("R", Inv)).Append('|').Append(Clean(k.Building));
            }
            if (!string.IsNullOrEmpty(s.Council))
            {
                b.Append("\nV|").Append(Clean(s.Council));
            }
            Edition(b, s.Edition);
            return b.ToString();
        }

        /// <summary>
        /// An edition as the store writes it: its line and those of its pieces, sectors, companies and closes (each
        /// after a line break).
        /// </summary>
        public static void Edition(StringBuilder b, LedgerEdition e)
        {
            if (e == null)
            {
                return;
            }
            b.Append("\nE|").Append(e.Day).Append('|').Append(Bit(e.HasMarket)).Append('|').Append(e.MarketDay).Append('|').Append(e.IndexLevel).Append('|').Append(e.IndexChangeBp)
                .Append('|').Append(e.IndexWeekBp).Append('|').Append(e.RateBp).Append('|').Append(e.InflationBp).Append('|').Append(Clean(e.CycleKey));
            for (int i = 0; i < e.Pieces.Count; i++)
            {
                Piece(b, "P", e.Pieces[i]);
            }
            for (int i = 0; i < e.Sectors.Count; i++)
            {
                LedgerSector x = e.Sectors[i];
                b.Append("\nS|").Append(Clean(x.Key)).Append('|').Append(x.Level).Append('|').Append(x.ChangeBp);
            }
            for (int i = 0; i < e.Quotes.Count; i++)
            {
                LedgerQuote q = e.Quotes[i];
                b.Append("\nQ|").Append(Clean(q.Name)).Append('|').Append(Clean(q.Ticker)).Append('|').Append(q.Sector).Append('|').Append(q.Close).Append('|').Append(q.ChangeBp)
                    .Append('|').Append(q.Volume).Append('|').Append(q.Flags);
            }
            if (e.IndexCloses.Count > 0)
            {
                b.Append("\nC|");
                for (int i = 0; i < e.IndexCloses.Count; i++)
                {
                    b.Append(i > 0 ? "," : string.Empty).Append(e.IndexCloses[i]);
                }
            }
        }

        private static void Piece(StringBuilder b, string tag, LedgerPiece p)
        {
            if (p == null)
            {
                return;
            }
            b.Append('\n').Append(tag).Append('|').Append((int)p.Section).Append('|').Append(p.Weight).Append('|').Append(Clean(p.Key)).Append('|').Append(Clean(p.Head));
            for (int i = 0; i + 1 < p.Data.Length; i += 2)
            {
                b.Append('|').Append(Clean(p.Data[i])).Append('|').Append(Clean(p.Data[i + 1]));
            }
        }

        /// <summary>The state of a save; a new one if the save has none, or one this version cannot read.</summary>
        public static LedgerState Load(Dictionary<string, string> data)
        {
            string text;
            if (data == null || !data.TryGetValue(Key, out text))
            {
                return new LedgerState();
            }
            if (!string.IsNullOrEmpty(text) && !HeadOk(text, Version, 12))
            {
                SaveLines.Unreadable(data, Key, "it could not be read");   // damaged, or a later version's: kept as it is (Core/SaveLines)
            }
            return Read(text);
        }

        /// <summary>True if an entry's first line is of this version, with as many fields as Read needs before it reads anything.</summary>
        internal static bool HeadOk(string text, string version, int fields)
        {
            int end = text.IndexOf('\n');
            string[] head = (end >= 0 ? text.Substring(0, end) : text).TrimEnd('\r').Split('|');
            return head.Length >= fields && head[0] == version;
        }

        public static LedgerState Read(string text)
        {
            var s = new LedgerState();
            if (string.IsNullOrEmpty(text))
            {
                return s;
            }
            string[] lines = text.Split('\n');
            string[] head = lines[0].TrimEnd('\r').Split('|');
            if (head.Length < 12 || head[0] != Version)
            {
                return s;
            }
            s.Subscribed = head[1] == "1";
            s.Cancelled = s.Subscribed && head[2] == "1";
            s.PaidUntil = Int(head[3]);
            s.Since = Int(head[4]);
            s.Weeks = Math.Max(0, Int(head[5]));
            s.LastMorning = Int(head[6]);
            s.CopyDay = Int(head[7]);
            s.Delivered = head[8] == "1";
            s.Bought = head[9] == "1";
            s.Copies = Math.Max(0, Int(head[10]));
            s.KiosksFor = head[11].Length > 0 ? head[11] : null;
            LedgerEdition e = null;
            for (int i = 1; i < lines.Length; i++)
            {
                string[] f = lines[i].TrimEnd('\r').Split('|');
                try
                {
                    switch (f[0])
                    {
                        case "K":
                            if (f.Length >= 6 && s.Kiosks.Count < LedgerCatalogue.KiosksWanted)
                            {
                                var k = new KioskSpot { X = Float(f[1]), Y = Float(f[2]), Z = Float(f[3]), Heading = Float(f[4]), Building = f[5].Length > 0 ? f[5] : null };
                                if (Finite(k.X) && Finite(k.Y) && Finite(k.Z) && Finite(k.Heading))
                                {
                                    s.Kiosks.Add(k);
                                }
                            }
                            break;
                        case "V":
                            s.Council = f.Length >= 2 && f[1].Length > 0 ? f[1] : null;
                            break;
                        default:
                            EditionLine(f, ref e);
                            break;
                    }
                }
                catch (Exception)
                {
                    // one line this version cannot read: left out
                }
            }
            s.Edition = Finish(e);
            return s;
        }

        /// <summary>
        /// One line of an edition: "E" begins one (given back in <paramref name="e"/>), the others add to it; any other
        /// line, or one before an "E", is left out.
        /// </summary>
        public static void EditionLine(string[] f, ref LedgerEdition e)
        {
            switch (f[0])
            {
                case "E":
                    if (f.Length >= 10)
                    {
                        e = new LedgerEdition
                        {
                            Day = Int(f[1]), HasMarket = f[2] == "1", MarketDay = Int(f[3]), IndexLevel = Int(f[4]), IndexChangeBp = Int(f[5]), IndexWeekBp = Int(f[6]),
                            RateBp = Int(f[7]), InflationBp = Int(f[8]), CycleKey = f[9].Length > 0 ? f[9] : null
                        };
                    }
                    break;
                case "P":
                    if (e != null && e.Pieces.Count < LedgerCatalogue.PiecesMost)
                    {
                        LedgerPiece p = ReadPiece(f);
                        if (p != null)
                        {
                            e.Pieces.Add(p);
                        }
                    }
                    break;
                case "S":
                    if (e != null && f.Length >= 4 && e.Sectors.Count < 64)
                    {
                        e.Sectors.Add(new LedgerSector { Key = f[1], Level = Int(f[2]), ChangeBp = Int(f[3]) });
                    }
                    break;
                case "Q":
                    if (e != null && f.Length >= 8 && e.Quotes.Count < 400)
                    {
                        var q = new LedgerQuote { Name = f[1], Ticker = f[2], Sector = Int(f[3]), Close = Int(f[4]), ChangeBp = Int(f[5]), Volume = Long(f[6]), Flags = Int(f[7]) };
                        if (q.Close > 0 && q.Sector >= 0 && q.Volume >= 0)
                        {
                            e.Quotes.Add(q);
                        }
                    }
                    break;
                case "C":
                    if (e != null && f.Length >= 2 && f[1].Length > 0)
                    {
                        string[] values = f[1].Split(',');
                        for (int v = 0; v < values.Length && e.IndexCloses.Count < LedgerCatalogue.ChartDays; v++)
                        {
                            int close = Int(values[v]);
                            if (close > 0)
                            {
                                e.IndexCloses.Add(close);
                            }
                        }
                    }
                    break;
            }
        }

        /// <summary>An edition read whole: a company whose sector is not in it is left out. Null for none, or one without a day.</summary>
        public static LedgerEdition Finish(LedgerEdition e)
        {
            if (e == null || e.Day <= 0)
            {
                return null;
            }
            for (int i = 0; i < e.Quotes.Count; i++)
            {
                if (e.Quotes[i].Sector >= e.Sectors.Count)
                {
                    e.Quotes.RemoveAt(i--);   // a company whose sector is not in the edition
                }
            }
            return e;
        }

        private static LedgerPiece ReadPiece(string[] f)
        {
            if (f.Length < 5 || f[3].Length == 0)
            {
                return null;
            }
            int section = Int(f[1]);
            if (section < 0 || section >= (int)LedgerSection.Count)
            {
                return null;
            }
            int pairs = (f.Length - 5) / 2;
            var data = pairs > 0 ? new string[pairs * 2] : LedgerPiece.NoData;
            for (int i = 0; i < pairs * 2; i++)
            {
                data[i] = f[5 + i];
            }
            return new LedgerPiece((LedgerSection)section, Int(f[2]), f[3], f[4].Length > 0 ? f[4] : null, data);
        }

        private static string Bit(bool on)
        {
            return on ? "1" : "0";
        }

        /// <summary>A value as the store writes it: no field or line breaks in it.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ');
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, Inv, out int value) ? value : 0;
        }

        private static long Long(string text)
        {
            return long.TryParse(text, NumberStyles.Integer, Inv, out long value) ? value : 0L;
        }

        private static float Float(string text)
        {
            return float.TryParse(text, NumberStyles.Float, Inv, out float value) ? value : float.NaN;
        }
    }
}
