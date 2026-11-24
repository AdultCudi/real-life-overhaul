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
    /// Cold storage works two ways. A product some fridge or freezer of the game lists as its own (a cold shelf shows it) keeps its
    /// cold life only while it sits inside one. Any other product, which the game lets into no cold shelf, is kept cold by the
    /// fridges and freezers standing at the place: each covers up to its own room, the newest pieces first, and the rest keeps
    /// its ordinary life (<see cref="Chilled"/>, <see cref="Expired(List{Cohort},int,int,int,int)"/>).
    /// </summary>
    public static class SpoilRules
    {
        public const int CohortsMost = 6;            // batches kept of a product at a place; the two oldest are folded into one past it
        public const int ShortLife = 7;              // days: goods that keep no longer than this are short-lived (smaller, more frequent orders)
        public const int RivalStockDays = 4;         // days of stock a rival shop holds between its deliveries
        public const int RivalHandlingMille = 30;    // thousandths of fresh goods a rival loses to handling whatever its stock
        public const int PaceLeast = 25;             // per cent of the usual shelf life: a quarter of the time
        public const int PaceUsual = 100;            // the shelf lives above, as a save from before kept them
        public const int PaceMost = 400;             // four times as long

        /// <summary>
        /// Where the shelf life's pace comes from (the option, the usual in a Going Public session); set once by Settings. Null reads as the
        /// usual, so a check or a page that runs before it still gets the figures above.
        /// </summary>
        public static Func<int> PaceSource;

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

        public static int ClampPace(int percent) => Math.Max(PaceLeast, Math.Min(PaceMost, percent));

        /// <summary>The pace now, per cent of the usual shelf life.</summary>
        public static int Pace => PaceSource != null ? ClampPace(PaceSource()) : PaceUsual;

        /// <summary>
        /// A shelf life at a pace: whole days, rounded to the nearest and never under one while the goods spoil at all; the usual
        /// pace gives the life as it is. The one place the option is applied, so every reader of a life (the places, the managers'
        /// orders, the trips, the rivals' loss, the pages) agrees.
        /// </summary>
        public static int Scale(int usual, int pace) => usual <= 0 ? 0 : Math.Max(1, (int)(((long)usual * ClampPace(pace) + 50) / 100));

        /// <summary>Days a class keeps at the usual pace, out of or in cold storage; 0 for never spoils.</summary>
        public static int UsualLife(SpoilClass c, bool cold) => (cold ? Cold : Ambient)[(int)c];

        /// <summary>Days a product keeps at the usual pace; 0 for never.</summary>
        public static int UsualLife(string item, bool weighed, bool cold) => UsualLife(Of(item, weighed), cold);

        /// <summary>Days a class keeps now, out of or in cold storage; 0 for never spoils.</summary>
        public static int Life(SpoilClass c, bool cold) => Scale(UsualLife(c, cold), Pace);

        /// <summary>Days a product keeps now; 0 for never.</summary>
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

        /// <summary>
        /// Makes batches kept at one pace read at another, for goods whose usual life is <paramref name="usual"/> days: each batch keeps the
        /// share of its life it has used (so a change never throws stock out at once and never makes it fresh again), and from now on spoils
        /// at the new pace. A batch not yet due stays not due until the next day at the soonest; one already due stays due.
        /// </summary>
        public static void Rebase(List<Cohort> batches, int usual, int from, int to, int today)
        {
            from = ClampPace(from);
            to = ClampPace(to);
            if (from == to || usual <= 0 || batches.Count == 0)
            {
                return;
            }
            int was = Scale(usual, from), now = Scale(usual, to);
            Cohort[] old = batches.ToArray();
            batches.Clear();
            for (int i = 0; i < old.Length; i++)
            {
                long age = Math.Max(0, today - old[i].Day);
                long aged = (2 * age * now + was) / (2L * was);
                aged = age < was ? Math.Min(aged, now - 1) : Math.Max(aged, now);
                Add(batches, today - (int)aged, old[i].Count);
            }
        }

        /// <summary>
        /// Goods moved between two places that last read their batches at different paces: each batch of <paramref name="taken"/> read as
        /// the place it comes to reads age, by that place's own usual life of the product (<paramref name="usual"/>), so the guard of
        /// <see cref="Rebase"/> holds at the life the place will judge it by: goods not yet due where they were are not due on arrival.
        /// Every piece is kept. The batches come back in the same list.
        /// </summary>
        public static void Arrive(List<Cohort> taken, int usual, int from, int to, int today)
        {
            if (taken.Count == 0 || ClampPace(from) == ClampPace(to))
            {
                return;
            }
            var one = new List<Cohort>(1);
            Cohort[] arriving = taken.ToArray();
            taken.Clear();
            for (int i = 0; i < arriving.Length; i++)
            {
                one.Clear();
                one.Add(arriving[i]);
                Rebase(one, usual, from, to, today);
                taken.Add(new Cohort(one[0].Day, arriving[i].Count));
            }
        }

        /// <summary>The pieces past their life at the turn into <paramref name="today"/> (stocked on or before today minus the life); 0 when it never spoils.</summary>
        public static int Expired(List<Cohort> batches, int life, int today) => Expired(batches, life, 0, 0, today);

        /// <summary>
        /// The pieces past their life at the turn into <paramref name="today"/> when <paramref name="chilled"/> of the pieces are kept cold:
        /// the newest ones, since the oldest are sold first, so a batch that straddles the line is split by pieces. The rest keep
        /// <paramref name="life"/> and the cold pieces <paramref name="coldLife"/> (0 for never). With none chilled it is the one life above.
        /// What is due is always the oldest pieces (a cold piece is due only when everything older is), so taking it from the oldest batch
        /// first (<see cref="Take"/>) removes exactly these.
        /// </summary>
        public static int Expired(List<Cohort> batches, int life, int coldLife, int chilled, int today)
        {
            int warm = Math.Max(0, Total(batches) - Math.Max(0, chilled)), seen = 0, n = 0;
            for (int i = 0; i < batches.Count; i++)
            {
                Cohort c = batches[i];
                int inWarm = Math.Max(0, Math.Min(c.Count, warm - seen));
                if (life > 0 && c.Day + life <= today)
                {
                    n += inWarm;
                }
                if (coldLife > 0 && c.Day + coldLife <= today)
                {
                    n += c.Count - inWarm;
                }
                seen += c.Count;
            }
            return n;
        }

        /// <summary>Turns of the day until the oldest batch spoils (1: tonight; 0: due now); -1 when it never does or nothing is held.</summary>
        public static int SpoilsIn(List<Cohort> batches, int life, int today) => SpoilsIn(batches, life, 0, 0, today);

        /// <summary>
        /// The same with <paramref name="chilled"/> pieces kept cold: the oldest piece is an ordinary one while any ordinary piece is held,
        /// and a cold one only when all of it is cold.
        /// </summary>
        public static int SpoilsIn(List<Cohort> batches, int life, int coldLife, int chilled, int today)
        {
            if (batches == null || batches.Count == 0)
            {
                return -1;
            }
            int use = Total(batches) - Math.Max(0, chilled) > 0 ? life : coldLife;
            if (use <= 0)
            {
                return -1;
            }
            return Math.Max(0, batches[0].Day + use - today);
        }

        /// <summary>
        /// The room one fridge or freezer gives a product, in pieces: its slots times the pieces one slot holds (the game's own figures,
        /// Item.cargoCapacity and CargoInstance.GetMaxStockCapacity). Whole numbers, never negative.
        /// </summary>
        public static int Room(int slots, int stack) => (int)Math.Min(int.MaxValue, Math.Max(0L, slots) * Math.Max(0L, stack));

        /// <summary>
        /// The pieces of a product one fridge or freezer keeps cold, from the game's own figures for the furniture and the product: a
        /// shelf (a showcase) gives the room of its one load, a fridge with slots gives its slots times that and takes only food that
        /// fills (<c>saturation</c> above 0, FridgeController.CanStore); a whitelist that leaves the product out gives none. One load is
        /// the product's box size times the furniture's capacity multiplier, rounded up (CargoInstance.GetMaxStockCapacity).
        /// </summary>
        public static int RoomOf(bool shelf, int slots, float multiplier, int boxSize, int saturation, bool allowed)
        {
            if (!allowed || (!shelf && (slots <= 0 || saturation <= 0)))
            {
                return 0;
            }
            return Room(shelf ? 1 : slots, (int)Math.Ceiling((float)((float)Math.Max(0, boxSize) * multiplier)));
        }

        /// <summary>The pieces of a stock the cold furniture at a place keeps cold: all of it up to the room, the rest none.</summary>
        public static int Chilled(int stock, int room) => Math.Max(0, Math.Min(stock, room));

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
