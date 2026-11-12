using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How a price stands against the rivals' lowest price in the neighbourhood.</summary>
    public enum PriceStance : byte
    {
        Manager = 0,    // the manager's own suggestion, its highest: what the game's pricing manager sets each day
        Undercut = 1,   // a step under the rivals' lowest price
        Match = 2,      // the rivals' lowest price
        Premium = 3     // a step over it
    }

    /// <summary>
    /// A pricing strategy: how to stand against the rivals, the least margin over cost, prices ending in .90, never under
    /// cost, the most a price moves in a day, and whether the daily reprice runs at all. Kept for each of the game's
    /// pricing plans (BizMan's pricing page) and for each place the mod's own manager prices, by the same rule. The
    /// default leaves the game's own daily reprice exactly as it is. Game-free; checked offline (EconomyCheck).
    /// </summary>
    public struct PriceStrategy
    {
        public PriceStance Stance;
        public int Margin;      // per cent: the least margin a product keeps over its cost; 0 for none
        public bool Ninety;     // prices end in .90 where one fits inside the other rules
        public bool OverCost;   // never under cost: at least the cost and five per cent
        public int Step;        // per cent: the most a price moves from yesterday's in a day; 0 for no limit
        public bool Hold;       // the daily reprice is off: the plan's prices stay where they stood

        public bool IsDefault => Stance == PriceStance.Manager && Margin == 0 && !Ninety && !OverCost && Step == 0 && !Hold;

        /// <summary>Whether the price rule changes anything the game's own reprice sets.</summary>
        public bool Prices => Stance != PriceStance.Manager || Margin > 0 || Ninety || OverCost || Step > 0;
    }

    /// <summary>The strategy's rule, in cents. Game-free.</summary>
    public static class PriceStrategyRules
    {
        public static readonly int[] Margins = { 0, 10, 20, 30, 40, 50, 60 };    // per cent, the choices of the least margin
        public static readonly int[] Steps = { 0, 5, 10, 15, 20, 25 };           // per cent, the choices of the most a price moves a day
        public const int UndercutPerMille = 950;    // five per cent under the rivals' lowest
        public const int PremiumPerMille = 1100;    // ten per cent over it
        public const int CostFloorPerMille = 1050;  // never under cost: the cost and five per cent

        /// <summary>Each stance's words, in the order of <see cref="PriceStance"/>.</summary>
        public static readonly string[] StanceKeys =
        {
            "reallife_ui_bizops_stance_manager", "reallife_ui_bizops_stance_undercut", "reallife_ui_bizops_stance_match", "reallife_ui_bizops_stance_premium"
        };

        /// <summary>
        /// The price a product takes, cents: the stance's start (the suggestion; under, at or over the rivals' lowest where a
        /// rival sells it), raised to the least margin, held under the ceiling, moved at most the day's step from yesterday's,
        /// never under the cost floor where that is asked, and ended in .90 inside those bounds where one fits. A figure of 0
        /// or less is not known: no rival, no cost, no ceiling, no yesterday. Returns the suggestion where nothing is known.
        /// </summary>
        public static int Price(in PriceStrategy s, int suggested, int rival, int cost, int ceiling, int yesterday)
        {
            int p = suggested;
            if (rival > 0)
            {
                switch (s.Stance)
                {
                    case PriceStance.Undercut: p = Share(rival, UndercutPerMille); break;
                    case PriceStance.Match: p = rival; break;
                    case PriceStance.Premium: p = Share(rival, PremiumPerMille); break;
                }
            }
            if (p <= 0)
            {
                return suggested;
            }
            int hard = s.OverCost && cost > 0 ? ShareUp(cost, CostFloorPerMille) : 1;
            int floor = Math.Max(hard, MarginFloor(cost, s.Margin));
            int top = ceiling > 0 ? Math.Max(ceiling, floor) : int.MaxValue;   // the floor the player asked for wins over the ceiling
            p = Math.Min(Math.Max(p, floor), top);
            int lo = 1, hi = int.MaxValue;
            if (s.Step > 0 && yesterday > 0)
            {
                lo = Math.Max(1, yesterday - (int)((long)yesterday * s.Step / 100));
                hi = yesterday + (int)((long)yesterday * s.Step / 100);
                p = Math.Min(Math.Max(p, lo), hi);
            }
            p = Math.Max(p, hard);   // never under cost holds over the day's step
            if (s.Ninety)
            {
                p = Ninety(p, Math.Max(Math.Max(lo, floor), hard), Math.Min(hi, top));
            }
            return p;
        }

        /// <summary>The least price that keeps a margin over a cost, cents; 1 with no cost or no margin.</summary>
        public static int MarginFloor(int cost, int margin)
        {
            if (cost <= 0 || margin <= 0 || margin >= 100)
            {
                return 1;
            }
            return (int)((cost * 100L + (100 - margin) - 1) / (100 - margin));
        }

        /// <summary>The nearest price ending in .90 within lo to hi, the lower on a tie; the price itself where none fits.</summary>
        public static int Ninety(int p, int lo, int hi)
        {
            if (p % 100 == 90)
            {
                return p;
            }
            int below = p / 100 * 100 - 10, above = below + 100;
            bool b = below >= Math.Max(1, lo) && below <= hi, a = above >= Math.Max(1, lo) && above <= hi;
            if (b && a)
            {
                return p - below <= above - p ? below : above;
            }
            return b ? below : a ? above : p;
        }

        /// <summary>A margin over cost, per cent of the price; int.MinValue with no price or no cost.</summary>
        public static int MarginOf(int price, int cost)
        {
            return price <= 0 || cost <= 0 ? int.MinValue : (int)Math.Round((price - cost) * 100.0 / price);
        }

        /// <summary>How far a price stands from the rivals' lowest, tenths of a per cent; int.MinValue with no rival.</summary>
        public static int AgainstRivals(int price, int rival)
        {
            return price <= 0 || rival <= 0 ? int.MinValue : (int)Math.Round((price - rival) * 1000.0 / rival);
        }

        /// <summary>
        /// A strategy as one number, for a save line of numbers (the mod's own managers keep theirs so): the stance in two
        /// bits, the margin's and the day's step's places in their lists, the .90, the cost floor and the hold a bit each.
        /// The default is 0, so a line from before the strategy reads as the default.
        /// </summary>
        public static int Pack(in PriceStrategy s)
        {
            int margin = Math.Max(0, Array.IndexOf(Margins, s.Margin)), step = Math.Max(0, Array.IndexOf(Steps, s.Step));
            return ((int)s.Stance & 3) | (margin & 7) << 2 | (s.Ninety ? 1 << 5 : 0) | (s.OverCost ? 1 << 6 : 0) | (step & 7) << 7 | (s.Hold ? 1 << 10 : 0);
        }

        /// <summary>A strategy from its number (see <see cref="Pack"/>); a place past a list's end is its last choice.</summary>
        public static PriceStrategy Unpack(int v)
        {
            v = Math.Max(0, v);
            return new PriceStrategy
            {
                Stance = (PriceStance)(v & 3), Margin = Margins[Math.Min(v >> 2 & 7, Margins.Length - 1)], Ninety = (v & 1 << 5) != 0, OverCost = (v & 1 << 6) != 0,
                Step = Steps[Math.Min(v >> 7 & 7, Steps.Length - 1)], Hold = (v & 1 << 10) != 0
            };
        }

        private static int Share(int cents, int perMille) => (int)((long)cents * perMille / 1000);

        private static int ShareUp(int cents, int perMille) => (int)(((long)cents * perMille + 999) / 1000);
    }

    /// <summary>What the mod keeps for one of the game's pricing plans: its strategy and its prices before its last reprice and now.</summary>
    public sealed class PricePlanFile
    {
        public PriceStrategy Strategy;
        public int Day = -1, Hour = -1;                                          // the reprice last seen: the plan's next one as it stood then
        public readonly Dictionary<string, int> Yesterday = new Dictionary<string, int>();   // product: cents, as the plan stood just before its last reprice
        public readonly Dictionary<string, int> Today = new Dictionary<string, int>();       // product: cents, as it stands, looked at each hour

        public bool Worth => !Strategy.IsDefault || Yesterday.Count > 0 || Today.Count > 0;
    }

    /// <summary>
    /// The pricing plans' save line: "v=1^id|stance|margin|ninety|overcost|step|hold|day|hour|yesterday|today", a list of
    /// prices written "product=cents;product=cents". '%', '^', '|', ';' and '=' in an id or a product are written %25, %5E,
    /// %7C, %3B and %3D. Sorted by plan and by product, so the same plans write the same line. Game-free.
    /// </summary>
    public static class PricePlanStore
    {
        public const string Key = "reallife:economy_price_plans";
        public const int Version = 1;
        public const int MostProducts = 400;   // a plan's products kept at the most: a line that cannot grow without end

        public static string Write(Dictionary<string, PricePlanFile> plans)
        {
            var t = new StringBuilder(64 + plans.Count * 256);
            t.Append("v=").Append(Version);
            var ids = new List<string>(plans.Keys);
            ids.Sort(string.CompareOrdinal);
            for (int i = 0; i < ids.Count; i++)
            {
                PricePlanFile f = plans[ids[i]];
                if (f == null || !f.Worth)
                {
                    continue;
                }
                PriceStrategy s = f.Strategy;
                t.Append('^').Append(Escape(ids[i])).Append('|').Append((int)s.Stance).Append('|').Append(s.Margin).Append('|').Append(s.Ninety ? 1 : 0)
                    .Append('|').Append(s.OverCost ? 1 : 0).Append('|').Append(s.Step).Append('|').Append(s.Hold ? 1 : 0).Append('|').Append(f.Day).Append('|').Append(f.Hour)
                    .Append('|');
                Prices(t, f.Yesterday);
                t.Append('|');
                Prices(t, f.Today);
            }
            return t.ToString();
        }

        /// <summary>Reads a line into the plans; false, with none, for another version or a bad line.</summary>
        public static bool Read(string text, Dictionary<string, PricePlanFile> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            string[] parts = text.Split('^');
            if (parts[0] != "v=" + Version)
            {
                return false;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                string[] c = parts[i].Split('|');
                int stance, margin, ninety, over, step, hold, day, hour;
                if (c.Length != 11 || c[0].Length == 0 || !I(c[1], out stance) || stance < 0 || stance > (int)PriceStance.Premium || !I(c[2], out margin)
                    || Array.IndexOf(PriceStrategyRules.Margins, margin) < 0 || !I(c[3], out ninety) || !I(c[4], out over) || !I(c[5], out step)
                    || Array.IndexOf(PriceStrategyRules.Steps, step) < 0 || !I(c[6], out hold) || !I(c[7], out day) || !I(c[8], out hour))
                {
                    into.Clear();
                    return false;
                }
                var f = new PricePlanFile
                {
                    Strategy = new PriceStrategy { Stance = (PriceStance)stance, Margin = margin, Ninety = ninety == 1, OverCost = over == 1, Step = step, Hold = hold == 1 },
                    Day = day, Hour = hour
                };
                if (!Prices(c[9], f.Yesterday) || !Prices(c[10], f.Today))
                {
                    into.Clear();
                    return false;
                }
                into[Unescape(c[0])] = f;
            }
            return true;
        }

        private static void Prices(StringBuilder t, Dictionary<string, int> prices)
        {
            var names = new List<string>(prices.Keys);
            names.Sort(string.CompareOrdinal);
            for (int i = 0; i < names.Count && i < MostProducts; i++)
            {
                t.Append(i > 0 ? ";" : string.Empty).Append(Escape(names[i])).Append('=').Append(prices[names[i]].ToString(CultureInfo.InvariantCulture));
            }
        }

        private static bool Prices(string text, Dictionary<string, int> into)
        {
            into.Clear();
            if (text.Length == 0)
            {
                return true;
            }
            string[] items = text.Split(';');
            for (int i = 0; i < items.Length; i++)
            {
                int at = items[i].IndexOf('='), cents;
                if (at <= 0 || !I(items[i].Substring(at + 1), out cents) || cents < 0)
                {
                    return false;
                }
                into[Unescape(items[i].Substring(0, at))] = cents;
            }
            return true;
        }

        private static bool I(string s, out int v) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);

        private static string Escape(string s) => s.Replace("%", "%25").Replace("^", "%5E").Replace("|", "%7C").Replace(";", "%3B").Replace("=", "%3D");

        private static string Unescape(string s) => s.Replace("%3D", "=").Replace("%3B", ";").Replace("%7C", "|").Replace("%5E", "^").Replace("%25", "%");
    }
}
