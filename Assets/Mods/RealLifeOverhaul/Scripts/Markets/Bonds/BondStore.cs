using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The player's bonds in the market's line of the save (MarketStore, its part f). Game-free.
    ///
    ///     f:next;id|bond|bank|issuer|face|cost|rate|bought|end|payments|paid|reinvest|carry|received|reinvested|taxed|test|rollover;...
    ///
    /// Never in the city's line a Going Public host sends: each player's own. A newer version adds fields at the end of a
    /// record; this reader takes the fields it knows, keeps every figure inside its bounds, and drops a record of a bond
    /// it does not know. A line without the part (a save from before bonds) has none.
    /// </summary>
    public static class BondStore
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Write(StringBuilder t, BondBook book)
        {
            t.Append(book.NextId.ToString(Inv));
            for (int i = 0; i < book.Holdings.Count; i++)
            {
                BondHolding h = book.Holdings[i];
                t.Append(';').Append(h.Id.ToString(Inv)).Append('|').Append(h.Def.ToString(Inv)).Append('|').Append(h.Bank.ToString(Inv)).Append('|').Append(MarketStore.Clean(h.Issuer));
                L(t, h.Face, h.Cost);
                I(t, h.RateBp, h.BoughtDay, h.EndDay, h.Payments, h.Paid, h.Reinvest);
                L(t, h.Carry, h.Received, h.Reinvested, h.Taxed);
                I(t, h.Test ? 1 : 0, h.Rollover ? 1 : 0);   // (whether it rolls over: a version before it reads a bond repaid at its end)
            }
        }

        public static void Read(string body, BondBook into)
        {
            into.Holdings.Clear();
            into.NextId = 1;
            if (string.IsNullOrEmpty(body))
            {
                return;
            }
            string[] records = body.Split(';');
            int next = Int(records[0]);
            for (int r = 1; r < records.Length; r++)
            {
                string[] c = records[r].Split('|');
                if (c.Length < 17)
                {
                    continue;
                }
                int id = Int(c[0]);
                BondDef d = BondCatalogue.Find(Int(c[1]));
                if (id <= 0 || d == null || into.Holding(id) != null || into.Holdings.Count >= BondCatalogue.HoldingsMostLarge * 2)
                {
                    continue;
                }
                var h = new BondHolding
                {
                    Id = id, Def = d.Id, Bank = d.Kind == BondKind.Deposit ? Clamp(Int(c[2]), 0, BondCatalogue.BanksMost - 1) : -1, Issuer = c[3],
                    Face = Math.Max(0L, Long(c[4])), Cost = Math.Max(0L, Long(c[5])), RateBp = Clamp(Int(c[6]), 0, 5000), BoughtDay = Int(c[7]), EndDay = Int(c[8]),
                    Payments = Clamp(Int(c[9]), 1, 1000), Reinvest = BondRules.ReinvestOf(Clamp(Int(c[11]), 0, 100)), Carry = Clamp(Long(c[12]), 0L, 99L),
                    Received = Math.Max(0L, Long(c[13])), Reinvested = Math.Max(0L, Long(c[14])), Taxed = Math.Max(0L, Long(c[15])), Test = Int(c[16]) != 0,
                    Rollover = c.Length > 17 && Int(c[17]) != 0
                };
                h.Paid = Clamp(Int(c[10]), 0, h.Payments);
                if (h.EndDay <= h.BoughtDay || h.Face <= 0)
                {
                    continue;
                }
                into.Holdings.Add(h);
                next = Math.Max(next, id + 1);
            }
            into.NextId = Math.Max(1, next);
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

        private static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        private static long Clamp(long value, long least, long most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
