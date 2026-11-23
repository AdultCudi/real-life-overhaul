using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the save line: the size segments the quarterly review set (MarketEngine.ExtrasSegments), "~s:" a record each, "id,size,quarter,day" with ";" between. A company
    /// with none keeps the segment its catalogue row gives it; an older reader has no such part and skips it, so a save made with segments loads without them and the
    /// companies are as the catalogue has them.
    /// </summary>
    public static partial class MarketStore
    {
        private static void Segments(StringBuilder t, MarketState s)
        {
            bool first = true;
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                Stock k = s.Stocks[i];
                if (k.SizeQuarter < 0)
                {
                    continue;   // never reviewed: the catalogue's segment, which a line without this part gives it too
                }
                t.Append(first ? "~s:" : ";").Append(k.Id.ToString(Inv)).Append(',').Append((int)k.Size).Append(',').Append(k.SizeQuarter.ToString(Inv)).Append(',').Append(k.SizeDay.ToString(Inv));
                first = false;
            }
        }

        private static void ReadSegments(MarketState s, string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return;   // a save made before the reviews, or by a build that skips the part: every company has its catalogue segment
            }
            foreach (string record in Records(body))
            {
                string[] c = record.Split(',');
                Stock k = c.Length >= 4 ? s.Stock(Int(c[0])) : null;
                if (k == null)
                {
                    continue;
                }
                k.Size = (MarketSize)Clamp(Int(c[1]), (int)MarketSize.Large, (int)MarketSize.Small);
                k.SizeQuarter = Int(c[2]);
                k.SizeDay = Math.Max(0, Int(c[3]));
            }
        }
    }
}
