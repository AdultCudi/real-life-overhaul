using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The raw materials exchange and the player's contracts in the market's save line. Game-free. A reader takes the fields
    /// it knows, keeps each in bounds and drops a record it cannot read; an older reader skips both parts whole.
    ///     m:name|group|base|x|fix|fixday|level|close,close,...;...                   the city's: in the line a host sends
    ///     q:next|balance|callday|callamount|commission|realised;id|kind|material|stock|qty|price|premium|mark|margin|
    ///       opened|settle|warehouse|place|test|end|endday|endprice|result|delivered|fees;...      the player's only
    /// </summary>
    public static class DerivStore
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public const int MaterialsMost = 400;
        public const int ContractsKeptMost = 400;

        public static void WriteMaterials(StringBuilder t, List<RawMaterial> materials)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                RawMaterial m = materials[i];
                if (i > 0)
                {
                    t.Append(';');
                }
                t.Append(Clean(m.Name)).Append('|').Append(((int)m.Group).ToString(Inv)).Append('|').Append(m.Base.ToString(Inv)).Append('|')
                    .Append(double.IsNaN(m.X) || double.IsInfinity(m.X) ? "0" : m.X.ToString("R", Inv)).Append('|').Append(m.Fix.ToString(Inv)).Append('|')
                    .Append(m.FixDay.ToString(Inv)).Append('|').Append(m.Level.ToString(Inv)).Append('|');
                for (int k = 0; k < m.Closes.Count; k++)
                {
                    t.Append(k > 0 ? "," : string.Empty).Append(m.Closes[k].ToString(Inv));
                }
            }
        }

        public static void ReadMaterials(string body, List<RawMaterial> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(body))
            {
                return;
            }
            string[] records = body.Split(';');
            for (int r = 0; r < records.Length && into.Count < MaterialsMost; r++)
            {
                string[] c = records[r].Split('|');
                if (c.Length < 7 || string.IsNullOrEmpty(c[0]) || MaterialRules.Find(into, c[0]) != null)
                {
                    continue;
                }
                long baseMicros = Long(c[2]);
                if (baseMicros <= 0)
                {
                    continue;
                }
                var m = new RawMaterial
                {
                    Name = c[0], Group = (MaterialGroup)Clamp(Int(c[1]), 0, (int)MaterialGroup.Count - 1), Base = baseMicros,
                    X = Math.Max(-DerivCatalogue.XMost, Math.Min(DerivCatalogue.XMost, Double(c[3]))), FixDay = Math.Max(0, Int(c[5])), Level = Clamp(Int(c[6]), 1, 100000)
                };
                long fix = Long(c[4]);
                m.Fix = fix > 0 ? fix : MaterialRules.Price(m.Base, m.Level, m.X);
                if (c.Length > 7 && c[7].Length > 0)
                {
                    string[] closes = c[7].Split(',');
                    for (int k = Math.Max(0, closes.Length - DerivCatalogue.ClosesKept); k < closes.Length; k++)
                    {
                        long close = Long(closes[k]);
                        if (close > 0)
                        {
                            m.Closes.Add(close);
                        }
                    }
                }
                MaterialRules.Insert(into, m);
            }
        }

        public static void WriteBook(StringBuilder t, DerivBook book)
        {
            t.Append(book.NextId.ToString(Inv));
            L(t, book.Balance);
            I(t, book.CallDay);
            L(t, book.CallAmount, book.Commission, book.Realised);
            for (int i = 0; i < book.Contracts.Count; i++)
            {
                DerivContract c = book.Contracts[i];
                t.Append(';').Append(c.Id.ToString(Inv));
                I(t, (int)c.Kind);
                t.Append('|').Append(Clean(c.Material));
                I(t, c.StockId);
                L(t, c.Qty, c.Price, c.Premium, c.Mark, c.Margin);
                I(t, c.OpenedDay, c.SettleDay);
                t.Append('|').Append(Clean(c.Warehouse)).Append('|').Append(Clean(c.PlaceName));
                I(t, c.Test ? 1 : 0, (int)c.End, c.EndDay);
                L(t, c.EndPrice, c.Result, c.Delivered, c.Fees);
            }
        }

        public static void ReadBook(string body, DerivBook into)
        {
            into.Contracts.Clear();
            into.NextId = 1;
            into.Balance = into.CallAmount = into.Commission = into.Realised = 0;
            into.CallDay = 0;
            if (string.IsNullOrEmpty(body))
            {
                return;
            }
            string[] records = body.Split(';');
            string[] h = records[0].Split('|');
            int next = Math.Max(1, Int(At(h, 0)));
            into.Balance = Long(At(h, 1));
            into.CallDay = Math.Max(0, Int(At(h, 2)));
            into.CallAmount = Math.Max(0L, Long(At(h, 3)));
            into.Commission = Math.Max(0L, Long(At(h, 4)));
            into.Realised = Long(At(h, 5));
            for (int r = 1; r < records.Length && into.Contracts.Count < ContractsKeptMost; r++)
            {
                string[] c = records[r].Split('|');
                if (c.Length < 20)
                {
                    continue;
                }
                int id = Int(c[0]);
                int kind = Int(c[1]);
                if (id <= 0 || kind < 0 || kind > (int)ContractKind.Put || into.Contract(id) != null)
                {
                    continue;
                }
                var k = new DerivContract
                {
                    Id = id, Kind = (ContractKind)kind, Material = c[2], StockId = Math.Max(0, Int(c[3])), Qty = Math.Max(0L, Long(c[4])), Price = Math.Max(1L, Long(c[5])),
                    Premium = Math.Max(0L, Long(c[6])), Mark = Math.Max(1L, Long(c[7])), Margin = Math.Max(0L, Long(c[8])), OpenedDay = Int(c[9]), SettleDay = Int(c[10]),
                    Warehouse = c[11], PlaceName = c[12], Test = Int(c[13]) != 0, End = (ContractEnd)Clamp(Int(c[14]), 0, (int)ContractEnd.Gone), EndDay = Int(c[15]),
                    EndPrice = Math.Max(0L, Long(c[16])), Result = Long(c[17]), Delivered = Math.Max(0L, Long(c[18])), Fees = Math.Max(0L, Long(c[19]))
                };
                bool onShares = k.StockId > 0, onMaterial = k.Material.Length > 0;
                if (k.Qty <= 0 || onShares == onMaterial || (onShares && k.IsFuture) || k.SettleDay <= 0)
                {
                    continue;   // a future is always on a material, an option on one or the other, never both
                }
                into.Contracts.Add(k);
                next = Math.Max(next, id + 1);
            }
            into.NextId = next;
        }

        /// <summary>A text with the line's dividers taken out; the colon of the game's names stays.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('~', ' ').Replace(',', ' ').Replace('\n', ' ');
        }

        private static string At(string[] c, int i)
        {
            return i < c.Length ? c[i] : string.Empty;
        }

        private static void L(StringBuilder t, params long[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append('|').Append(values[i].ToString(Inv));
            }
        }

        private static void I(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append('|').Append(values[i].ToString(Inv));
            }
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, Inv, out int value) ? value : 0;
        }

        private static long Long(string text)
        {
            return long.TryParse(text, NumberStyles.Integer, Inv, out long value) ? value : 0L;
        }

        private static double Double(string text)
        {
            return double.TryParse(text, NumberStyles.Float, Inv, out double value) && !double.IsNaN(value) && !double.IsInfinity(value) ? value : 0.0;
        }

        private static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
