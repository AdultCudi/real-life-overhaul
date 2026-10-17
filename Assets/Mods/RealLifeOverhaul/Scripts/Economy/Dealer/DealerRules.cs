using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>A seeded draw (xorshift): the same seed and salts give the same numbers, however often asked.</summary>
    public struct DealerDice
    {
        private uint _s;

        public DealerDice(int seed, int a, int b = 0, int c = 0)
        {
            unchecked
            {
                uint s = (uint)seed * 2654435761u ^ (uint)a * 2246822519u ^ (uint)b * 3266489917u ^ (uint)c * 668265263u;
                _s = s == 0 ? 0x9E3779B9u : s;
                Next();
                Next();
            }
        }

        public uint Next()
        {
            unchecked
            {
                uint x = _s;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _s = x;
                return x;
            }
        }

        /// <summary>A whole number from least to most, both in.</summary>
        public int Range(int least, int most)
        {
            if (most <= least)
            {
                return least;
            }
            return least + (int)(Next() % (uint)(most - least + 1));
        }

        /// <summary>True with this chance in thousandths.</summary>
        public bool Chance(int thousandths)
        {
            return thousandths > 0 && (thousandths >= 1000 || Range(0, 999) < thousandths);
        }

        /// <summary>One of several kinds by their shares (thousandths, or any weights).</summary>
        public int Pick(int[] shares)
        {
            int total = 0;
            for (int i = 0; i < shares.Length; i++)
            {
                total += Math.Max(0, shares[i]);
            }
            if (total <= 0)
            {
                return 0;
            }
            int at = Range(0, total - 1);
            for (int i = 0; i < shares.Length; i++)
            {
                at -= Math.Max(0, shares[i]);
                if (at < 0)
                {
                    return i;
                }
            }
            return shares.Length - 1;
        }
    }

    /// <summary>The dealership's rules, game-free and checked offline; whole money units and thousandths only.</summary>
    public static partial class DealerRules
    {
        /// <summary>A straight line through points (at, value), held at both ends.</summary>
        public static int Line(int[] at, int[] value, int x)
        {
            if (x <= at[0])
            {
                return value[0];
            }
            for (int i = 1; i < at.Length; i++)
            {
                if (x <= at[i])
                {
                    long span = at[i] - at[i - 1];
                    return value[i - 1] + (int)((value[i] - value[i - 1]) * (long)(x - at[i - 1]) / (span <= 0 ? 1 : span));
                }
            }
            return value[value.Length - 1];
        }

        /// <summary>The share of its worth new a car keeps at these kilometres and this condition (thousandths).</summary>
        public static int WorthShare(int km, int condition)
        {
            int share = Line(DealerCatalogue.KmAt, DealerCatalogue.KmWorth, Math.Max(0, km)) * Line(DealerCatalogue.ConditionAt, DealerCatalogue.ConditionWorth, condition) / 1000;
            return Math.Max(DealerCatalogue.WorthLeast, share);
        }

        /// <summary>A car's worth: the game's list price at the price <paramref name="level"/> (1000 at the start's), by km and condition.</summary>
        public static int Worth(float listPrice, int level, int km, int condition)
        {
            if (listPrice <= 0f)
            {
                return 0;
            }
            double atPrices = listPrice * (double)Math.Max(1, level) / 1000.0;
            return (int)Math.Round(atPrices * WorthShare(km, condition) / 1000.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>The asking price a car is put on the lot at, from its worth.</summary>
        public static int Asking(int worth)
        {
            return DealerCatalogue.Named(DealerCatalogue.Share(worth, DealerCatalogue.MarkUp));
        }

        /// <summary>How many asking prices the player may step through for a car.</summary>
        public static int AskSteps => (DealerCatalogue.AskMost - DealerCatalogue.AskLeast) / DealerCatalogue.AskStep + 1;

        public static int AskAt(int worth, int step)
        {
            int share = DealerCatalogue.AskLeast + DealerCatalogue.Clamp(step, 0, AskSteps - 1) * DealerCatalogue.AskStep;
            return DealerCatalogue.Named(DealerCatalogue.Share(worth, share));
        }

        /// <summary>The step nearest an asking price.</summary>
        public static int AskStepOf(int worth, int asking)
        {
            int best = 0, gap = int.MaxValue;
            for (int s = 0; s < AskSteps; s++)
            {
                int g = Math.Abs(AskAt(worth, s) - asking);
                if (g < gap)
                {
                    gap = g;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>What another dealer pays for a car at once.</summary>
        public static int TradePrice(int worth)
        {
            return DealerCatalogue.Named(DealerCatalogue.Share(worth, DealerCatalogue.TradeShare));
        }

        /// <summary>The condition a car's faults take from it, together.</summary>
        public static int FaultCondition(int faults)
        {
            int taken = 0;
            for (int f = 0; f < DealerCatalogue.Faults; f++)
            {
                taken += (faults & (1 << f)) != 0 ? DealerCatalogue.FaultCondition[f] : 0;
            }
            return taken;
        }

        public static int FaultCount(int faults)
        {
            int n = 0;
            for (int f = 0; f < DealerCatalogue.Faults; f++)
            {
                n += (faults & (1 << f)) != 0 ? 1 : 0;
            }
            return n;
        }

        /// <summary>Preparing a car: the base, a share of what its missing condition costs, and each known fault put right.</summary>
        public static int PrepareCost(int worthNew, int condition, int knownFaults, int baseAtPrices)
        {
            int missing = Math.Max(0, 1000 - condition);
            int cost = baseAtPrices + (int)((long)worthNew * missing * DealerCatalogue.PrepareShare / 1000000L);
            for (int f = 0; f < DealerCatalogue.Faults; f++)
            {
                cost += (knownFaults & (1 << f)) != 0 ? DealerCatalogue.Share(worthNew, DealerCatalogue.FaultCost[f]) : 0;
            }
            return DealerCatalogue.Named(cost);
        }

        /// <summary>The condition after preparing: the faults known put right, and most of what is still missing put back.</summary>
        public static int Prepared(int condition, int faults, int knownFaults)
        {
            int c = condition + FaultCondition(faults & knownFaults);
            int missing = Math.Max(0, 1000 - c);
            int back = Math.Max(Math.Min(missing, DealerCatalogue.PrepareLeast), DealerCatalogue.Share(missing, DealerCatalogue.PrepareRestore));
            return DealerCatalogue.Clamp(c + back, 0, 1000);
        }

        /// <summary>
        /// The places of a lot on a floor of this size (centimetres), in rows inside a gap along every wall, facing along the longer
        /// side. Fills <paramref name="into"/> with x, z offsets from the middle; returns how many.
        /// </summary>
        public static int Layout(int width, int depth, List<int> into, out bool across)
        {
            into.Clear();
            across = width > depth;    // cars stand nose to tail along the longer side
            int wide = across ? depth : width, deep = across ? width : depth;
            int usableWide = Math.Max(0, wide - 2 * DealerCatalogue.WallGap), usableDeep = Math.Max(0, deep - 2 * DealerCatalogue.WallGap);
            int perRow = Math.Max(1, usableWide / DealerCatalogue.SpotWidth);
            int rows = Math.Max(1, usableDeep / DealerCatalogue.SpotDepth);
            int count = DealerCatalogue.Clamp(perRow * rows, DealerCatalogue.SpotsLeast, DealerCatalogue.SpotsMost);
            if (perRow * rows < count)
            {
                rows = (count + perRow - 1) / perRow;   // a floor too small for the least: the places are put closer
            }
            int step = Math.Max(1, Math.Min(DealerCatalogue.SpotWidth, usableWide / Math.Max(1, perRow)));
            int stepDeep = Math.Max(1, Math.Min(DealerCatalogue.SpotDepth, Math.Max(usableDeep, rows) / Math.Max(1, rows)));
            for (int n = 0; n < count; n++)
            {
                int r = n / perRow, c = n % perRow;
                int a = (c * 2 + 1 - perRow) * step / 2;           // across the row, centred
                int b = (r * 2 + 1 - rows) * stepDeep / 2;         // along the floor, centred
                into.Add(across ? b : a);
                into.Add(across ? a : b);
            }
            return count;
        }

        public static string CustomerName(ref DealerDice dice)
        {
            return DealerCatalogue.FirstNames[dice.Range(0, DealerCatalogue.FirstNames.Length - 1)] + " " + DealerCatalogue.LastNames[dice.Range(0, DealerCatalogue.LastNames.Length - 1)];
        }

        /// <summary>A name as the store keeps it: within its length, with the store's separators made spaces.</summary>
        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            var t = new System.Text.StringBuilder(Math.Min(name.Length, DealerCatalogue.NameMost));
            for (int i = 0; i < name.Length && t.Length < DealerCatalogue.NameMost; i++)
            {
                char ch = name[i];
                t.Append(ch == '|' || ch == ';' || ch == '^' || ch == '\n' || ch == '\r' ? ' ' : ch);
            }
            return t.ToString();
        }
    }
}
