using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How long a kind of goods keeps: none of these but Packaged spoils.</summary>
    public enum SpoilClass
    {
        Packaged = 0,   // tins, drinks, dry goods and everything that is not food: never spoils
        Produce = 1,    // fruit, vegetables and cut flowers
        Dairy = 2,
        Bakery = 3,
        Meat = 4,
        Prepared = 5,   // made food: salads, burgers, pizza
        Frozen = 6
    }

    /// <summary>A batch of one product at one place, stocked on one day.</summary>
    public struct Cohort
    {
        public int Day;
        public int Count;

        public Cohort(int day, int count)
        {
            Day = day;
            Count = count;
        }
    }

    /// <summary>
    /// Food spoilage's rules: each product's class by its name (the game's fruit and vegetables sold by weight are produce
    /// whatever their name), its shelf life in days out of and in cold storage, the batches a place holds of it oldest first,
    /// taking from the oldest, what is past its life at the end of a day, and the share a rival shop loses. Whole days and
    /// counts, so the same stock spoils the same on every machine. Game-free; checked offline (EconomyCheck, LogisticsChecks.cs).
    /// </summary>
    public static class SpoilRules
    {
        public const int CohortsMost = 6;            // batches kept of a product at a place; the two oldest are folded into one past it
        public const int ShortLife = 7;              // days: goods that keep no longer than this are short-lived (smaller, more frequent orders)
        public const int RivalStockDays = 4;         // days of stock a rival shop holds between its deliveries
        public const int RivalHandlingMille = 30;    // thousandths of fresh goods a rival loses to handling whatever its stock

        // Shelf life in days by SpoilClass: out of cold storage, and in a fridge or a freezer.
        private static readonly int[] Ambient = { 0, 6, 5, 4, 3, 4, 3 };
        private static readonly int[] Cold = { 0, 12, 14, 7, 8, 8, 90 };

        private static readonly Dictionary<string, SpoilClass> Named = new Dictionary<string, SpoilClass>(StringComparer.Ordinal)
        {
            { "ba:itemname_apple", SpoilClass.Produce }, { "ba:itemname_banana", SpoilClass.Produce }, { "ba:itemname_grapes", SpoilClass.Produce },
            { "ba:itemname_pear", SpoilClass.Produce }, { "ba:itemname_carrot", SpoilClass.Produce }, { "ba:itemname_lettuce", SpoilClass.Produce },
            { "ba:itemname_tomato", SpoilClass.Produce }, { "ba:itemname_onion", SpoilClass.Produce }, { "ba:itemname_slicedonions", SpoilClass.Produce },
            { "ba:itemname_russetpotatoes", SpoilClass.Produce }, { "ba:itemname_cheapflower", SpoilClass.Produce },
            { "ba:itemname_expensiveflower", SpoilClass.Produce },
            { "ba:itemname_milk", SpoilClass.Dairy }, { "ba:itemname_butter", SpoilClass.Dairy }, { "ba:itemname_cheese", SpoilClass.Dairy },
            { "ba:itemname_cheeseplatter", SpoilClass.Dairy },
            { "ba:itemname_croissant", SpoilClass.Bakery }, { "ba:itemname_cupcake", SpoilClass.Bakery }, { "ba:itemname_donut", SpoilClass.Bakery },
            { "ba:itemname_dough", SpoilClass.Bakery },
            { "ba:itemname_groundbeef", SpoilClass.Meat }, { "ba:itemname_chickenbreast", SpoilClass.Meat }, { "ba:itemname_rawsausage", SpoilClass.Meat },
            { "ba:itemname_salad", SpoilClass.Prepared }, { "ba:itemname_burger", SpoilClass.Prepared }, { "ba:itemname_hotdog", SpoilClass.Prepared },
            { "ba:itemname_pizza", SpoilClass.Prepared }, { "ba:itemname_kabob", SpoilClass.Prepared }, { "ba:itemname_frenchfries", SpoilClass.Prepared },
            { "ba:itemname_freshfood", SpoilClass.Prepared },
            { "ba:itemname_frozenfood", SpoilClass.Frozen }, { "ba:itemname_icecream", SpoilClass.Frozen },
        };

        // A rival shop's fresh share of its sales (thousandths) and the class its fresh goods keep like, by its kind of business.
        private static readonly Dictionary<string, int> FreshShare = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "ba:businesstype_fruitandvegetablestore", 900 }, { "ba:businesstype_florist", 900 }, { "ba:businesstype_fastfoodrestaurant", 650 },
            { "ba:businesstype_supermarket", 450 }, { "ba:businesstype_cafe", 400 }, { "ba:businesstype_coffeeshop", 250 },
        };

        private static readonly Dictionary<string, SpoilClass> FreshKind = new Dictionary<string, SpoilClass>(StringComparer.Ordinal)
        {
            { "ba:businesstype_fruitandvegetablestore", SpoilClass.Produce }, { "ba:businesstype_florist", SpoilClass.Produce },
            { "ba:businesstype_fastfoodrestaurant", SpoilClass.Prepared }, { "ba:businesstype_supermarket", SpoilClass.Dairy },
            { "ba:businesstype_cafe", SpoilClass.Bakery }, { "ba:businesstype_coffeeshop", SpoilClass.Dairy },
        };

        /// <summary>A product's class: by its name, else produce if the game weighs it at the till, else packaged.</summary>
        public static SpoilClass Of(string item, bool weighed)
        {
            if (item != null && Named.TryGetValue(item, out SpoilClass c))
            {
                return c;
            }
            return weighed ? SpoilClass.Produce : SpoilClass.Packaged;
        }

        /// <summary>Days a class keeps, out of or in cold storage; 0 for never spoils.</summary>
        public static int Life(SpoilClass c, bool cold) => (cold ? Cold : Ambient)[(int)c];

        /// <summary>Days a product keeps; 0 for never.</summary>
        public static int Life(string item, bool weighed, bool cold) => Life(Of(item, weighed), cold);

        /// <summary>Furniture that keeps goods cold: a fridge or a freezer of any kind.</summary>
        public static bool IsCold(string furniture) =>
            furniture != null && (furniture.IndexOf("fridge", StringComparison.Ordinal) >= 0 || furniture.IndexOf("freezer", StringComparison.Ordinal) >= 0);

        /// <summary>Pieces in all the batches.</summary>
        public static int Total(List<Cohort> batches)
        {
            int n = 0;
            for (int i = 0; i < batches.Count; i++)
            {
                n += batches[i].Count;
            }
            return n;
        }

        /// <summary>
        /// Stock come in on a day: a batch of that day, merged into the newest if it is the same day; past <see cref="CohortsMost"/> the
        /// two oldest are folded into one at their weighted mean day (rounded down, so never younger than they were).
        /// </summary>
        public static void Add(List<Cohort> batches, int day, int count)
        {
            if (count <= 0)
            {
                return;
            }
            int at = batches.Count;
            while (at > 0 && batches[at - 1].Day > day)
            {
                at--;
            }
            if (at > 0 && batches[at - 1].Day == day)
            {
                batches[at - 1] = new Cohort(day, batches[at - 1].Count + count);
            }
            else
            {
                batches.Insert(at, new Cohort(day, count));
            }
            while (batches.Count > CohortsMost)
            {
                Cohort a = batches[0], b = batches[1];
                long mean = ((long)a.Day * a.Count + (long)b.Day * b.Count) / Math.Max(1, a.Count + b.Count);
                batches[0] = new Cohort((int)Math.Min(mean, b.Day), a.Count + b.Count);
                batches.RemoveAt(1);
            }
        }

        /// <summary>Takes pieces out (sold, used or moved), oldest first; the batches taken from go into <paramref name="taken"/> if given. Returns the pieces taken.</summary>
        public static int Take(List<Cohort> batches, int count, List<Cohort> taken)
        {
            int left = Math.Max(0, count);
            while (left > 0 && batches.Count > 0)
            {
                Cohort c = batches[0];
                int n = Math.Min(left, c.Count);
                taken?.Add(new Cohort(c.Day, n));
                left -= n;
                if (n == c.Count)
                {
                    batches.RemoveAt(0);
                }
                else
                {
                    batches[0] = new Cohort(c.Day, c.Count - n);
                }
            }
            return Math.Max(0, count) - left;
        }

        /// <summary>
        /// Makes the batches hold what the place holds: less, and the difference is taken from the oldest; more, and it came in today.
        /// Returns the pieces that came in (0 when it went down).
        /// </summary>
        public static int Match(List<Cohort> batches, int onSite, int today)
        {
            int have = Total(batches);
            if (onSite < have)
            {
                Take(batches, have - onSite, null);
                return 0;
            }
            Add(batches, today, onSite - have);
            return onSite - have;
        }

        /// <summary>The pieces past their life at the turn into <paramref name="today"/> (stocked on or before today minus the life); 0 when it never spoils.</summary>
        public static int Expired(List<Cohort> batches, int life, int today)
        {
            if (life <= 0)
            {
                return 0;
            }
            int n = 0;
            for (int i = 0; i < batches.Count && batches[i].Day + life <= today; i++)
            {
                n += batches[i].Count;
            }
            return n;
        }

        /// <summary>Turns of the day until the oldest batch spoils (1: tonight; 0: due now); -1 when it never does or nothing is held.</summary>
        public static int SpoilsIn(List<Cohort> batches, int life, int today)
        {
            if (life <= 0 || batches == null || batches.Count == 0)
            {
                return -1;
            }
            return Math.Max(0, batches[0].Day + life - today);
        }

        /// <summary>A share of what came in that spoiled, whole per cent; -1 when nothing came in.</summary>
        public static int Percent(int spoiled, int came) => came <= 0 ? -1 : (int)Math.Min(100L, ((long)Math.Max(0, spoiled) * 100 + came / 2) / came);

        /// <summary>
        /// The most a manager orders of a short-lived product: what it sells in the days the goods keep (its use a day in
        /// thousandths); no cap for goods that keep longer than <see cref="ShortLife"/> or never spoil.
        /// </summary>
        public static int OrderCap(int usePerDayMille, int life)
        {
            if (life <= 0 || life > ShortLife || usePerDayMille < 0)
            {
                return int.MaxValue;
            }
            return (int)Math.Min(int.MaxValue, ((long)usePerDayMille * life + 999) / 1000);
        }

        /// <summary>A rival shop's fresh share of its sales, thousandths, by its kind of business; 0 for one that sells no fresh goods.</summary>
        public static int RivalFresh(string businessType) => businessType != null && FreshShare.TryGetValue(businessType, out int m) ? m : 0;

        /// <summary>The share of a rival shop's sales lost to spoiled stock, thousandths: handling, and what outlives its life between deliveries.</summary>
        public static int RivalLoss(string businessType)
        {
            int fresh = RivalFresh(businessType);
            if (fresh <= 0)
            {
                return 0;
            }
            int life = Life(FreshKind.TryGetValue(businessType, out SpoilClass c) ? c : SpoilClass.Produce, false);
            int over = life > 0 && RivalStockDays > life ? (RivalStockDays - life) * 1000 / RivalStockDays : 0;
            return fresh * (RivalHandlingMille + over) / 1000;
        }
    }
}
