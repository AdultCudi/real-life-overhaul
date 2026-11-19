using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The agency's book of other owners' blocks in a line of the save's modData of its own, <see cref="Key"/>, so a save made before it loads as it was and a save made
    /// after it loads with the option off. Game-free. Parts divided by '^' (h the book's figures, b the blocks), records by ';', fields by '|', a block's calls by ',' and
    /// a rival pitch's fields by '.'; text fields are escaped. A later version only adds fields at a record's end; a field the reader does not know is ignored, a missing
    /// one is its default, and every figure is put inside its bounds on reading.
    /// </summary>
    public static class AgencyBookStore
    {
        public const string Key = "reallife:estate_agency_book";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(AgencyBook book)
        {
            var t = new StringBuilder(96 + book.Blocks.Count * 160);
            t.Append("v=").Append(Version).Append("^h:");
            Ints(t, book.NextId, book.LastOfferDay, book.MonthLettings, book.LastMonthFees, book.LastMonthPenalty, book.LastMonthLettings, book.Won, book.Lost, book.Month);
            t.Append("^b:");
            bool first = true;
            for (int i = 0; i < book.Blocks.Count; i++)
            {
                ManagedBlock b = book.Blocks[i];
                t.Append(first ? string.Empty : ";");
                first = false;
                t.Append(b.Id.ToString(Inv)).Append('|').Append(Esc(b.Owner)).Append('|').Append(Esc(b.District)).Append('|');
                Ints(t, b.Stage, b.Why, b.Units, b.Sqm, b.RentDay, b.FeeAsk, b.FeePct, b.Standard, b.Until, b.Pitched ? 1 : 0, b.Let, b.Happy, b.Cond, b.Strikes, b.NextInspect, b.Missed,
                    b.Collected, b.HappySum, b.HappyDays, b.Fees, b.Earned, b.Months, b.Penalised, b.LettingsDone);
                t.Append('|');
                for (int k = 0; k < b.Calls.Count; k++)
                {
                    t.Append(k > 0 ? "," : string.Empty).Append(b.Calls[k].ToString(Inv));
                }
                t.Append('|');
                for (int k = 0; k < b.Bids.Count; k++)
                {
                    AgencyBid bid = b.Bids[k];
                    t.Append(k > 0 ? "," : string.Empty).Append(Esc(bid.Rival).Replace(".", "%2E")).Append('.').Append(bid.Pct.ToString(Inv)).Append('.').Append(bid.Skill.ToString(Inv));
                }
                t.Append('|').Append(Esc(b.Rival));
            }
            return t.ToString();
        }

        /// <summary>The saved book put into <paramref name="book"/>; false, with the book as it was, when the line cannot be read. Nothing is thrown.</summary>
        public static bool Read(AgencyBook book, string text)
        {
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return false;
            }
            try
            {
                var blocks = new List<ManagedBlock>(8);
                int[] head = null;
                string[] parts = text.Split('^');
                for (int i = 1; i < parts.Length; i++)
                {
                    string part = parts[i];
                    if (part.Length < 2 || part[1] != ':')
                    {
                        continue;
                    }
                    string body = part.Substring(2);
                    if (part[0] == 'h')
                    {
                        string[] f = body.Split('|');
                        head = new int[9];
                        for (int k = 0; k < head.Length; k++)
                        {
                            head[k] = k < f.Length ? Int(f[k]) : 0;
                        }
                    }
                    else if (part[0] == 'b' && body.Length > 0)
                    {
                        string[] records = body.Split(';');
                        for (int r = 0; r < records.Length && blocks.Count < AgencyBookCatalogue.BlocksMost + AgencyBookCatalogue.OffersMost + AgencyBookCatalogue.LostKept; r++)
                        {
                            ManagedBlock b = ReadBlock(records[r]);
                            if (b != null)
                            {
                                blocks.Add(b);
                            }
                        }
                    }
                }
                if (head == null)
                {
                    return false;
                }
                book.Blocks.Clear();
                book.Blocks.AddRange(blocks);
                book.NextId = Math.Max(1, head[0]);
                for (int i = 0; i < book.Blocks.Count; i++)
                {
                    book.NextId = Math.Max(book.NextId, book.Blocks[i].Id + 1);
                }
                book.LastOfferDay = head[1];
                book.MonthLettings = Math.Max(0, head[2]);
                book.LastMonthFees = Math.Max(0, head[3]);
                book.LastMonthPenalty = Math.Max(0, head[4]);
                book.LastMonthLettings = Math.Max(0, head[5]);
                book.Won = Math.Max(0, head[6]);
                book.Lost = Math.Max(0, head[7]);
                book.Month = Math.Max(0, head[8]);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static ManagedBlock ReadBlock(string record)
        {
            string[] f = record.Split('|');
            if (f.Length < 28)
            {
                return null;
            }
            var b = new ManagedBlock { Id = Int(f[0]), Owner = Unesc(f[1]), District = Unesc(f[2]) };
            if (b.Id <= 0 || b.Owner.Length == 0)
            {
                return null;
            }
            b.Stage = EstateCatalogue.Clamp(Int(f[3]), 0, (int)BlockStage.Lost);
            b.Why = EstateCatalogue.Clamp(Int(f[4]), 0, (int)BlockWhy.Resigned);
            b.Units = EstateCatalogue.Clamp(Int(f[5]), 1, AgencyBookCatalogue.UnitsMost);
            b.Sqm = EstateCatalogue.Clamp(Int(f[6]), 1, 1000);
            b.RentDay = Math.Max(1, Int(f[7]));
            b.FeeAsk = EstateCatalogue.Clamp(Int(f[8]), 0, 1000);
            b.FeePct = EstateCatalogue.Clamp(Int(f[9]), 1, 200);
            b.Standard = EstateCatalogue.Clamp(Int(f[10]), 0, 1000);
            b.Until = Int(f[11]);
            b.Pitched = Int(f[12]) != 0;
            b.Let = EstateCatalogue.Clamp(Int(f[13]), 0, b.Units);
            b.Happy = EstateCatalogue.Clamp(Int(f[14]), -1, 1000);
            b.Cond = EstateCatalogue.Clamp(Int(f[15]), 0, 1000);
            b.Strikes = EstateCatalogue.Clamp(Int(f[16]), 0, 9);
            b.NextInspect = Int(f[17]);
            b.Missed = EstateCatalogue.Clamp(Int(f[18]), 0, 9);
            b.Collected = Math.Max(0, Int(f[19]));
            b.HappySum = Math.Max(0, Int(f[20]));
            b.HappyDays = Math.Max(0, Int(f[21]));
            b.Fees = Math.Max(0, Int(f[22]));
            b.Earned = Math.Max(0, Int(f[23]));
            b.Months = Math.Max(0, Int(f[24]));
            b.Penalised = Math.Max(0, Int(f[25]));
            b.LettingsDone = Math.Max(0, Int(f[26]));
            string[] calls = f[27].Length > 0 ? f[27].Split(',') : new string[0];
            for (int k = 0; k < calls.Length && b.Calls.Count < AgencyBookCatalogue.CallsMost; k++)
            {
                b.Calls.Add(Int(calls[k]));
            }
            string[] bids = f.Length > 28 && f[28].Length > 0 ? f[28].Split(',') : new string[0];
            for (int k = 0; k < bids.Length && b.Bids.Count < AgencyBookCatalogue.RivalBidsMost; k++)
            {
                string[] q = bids[k].Split('.');
                if (q.Length >= 3 && q[0].Length > 0)
                {
                    b.Bids.Add(new AgencyBid
                    {
                        Rival = Uri.UnescapeDataString(q[0]), Pct = EstateCatalogue.Clamp(Int(q[1]), 1, 200), Skill = EstateCatalogue.Clamp(Int(q[2]), 0, 100)
                    });
                }
            }
            b.Rival = f.Length > 29 ? Unesc(f[29]) : string.Empty;
            return b;
        }

        /// <summary>The line read into a state's book (a missing line leaves what the book holds).</summary>
        public static void Read(AgencyBook book, Dictionary<string, string> data)
        {
            string text;
            if (data != null && data.TryGetValue(Key, out text))
            {
                Read(book, text);
            }
        }

        private static string Esc(string text)
        {
            return Uri.EscapeDataString(text ?? string.Empty);
        }

        private static string Unesc(string text)
        {
            return Uri.UnescapeDataString(text ?? string.Empty);
        }

        private static void Ints(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static int Int(string text)
        {
            int n;
            return int.TryParse(text, NumberStyles.Integer, Inv, out n) ? n : 0;
        }
    }
}
