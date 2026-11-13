using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One filter over a table: the words of "all", and the kinds of row it picks among (each row names its kind in <see cref="UiDataRow.Kinds"/>).</summary>
    public sealed class TableChoice
    {
        public string All;
        public string[] Options = new string[0];

        /// <summary>The words the filter's field shows for a choice: "all" for none (-1).</summary>
        public string Words(int chosen) => chosen >= 0 && chosen < Options.Length ? Options[chosen] : All;
    }

    /// <summary>
    /// What the Property and Services tables do with rows the screen has written, as the old app's tables did (TableSpec): order them by a column, keep the rows a filter's
    /// choice lets through, and add up a column over the rows kept. Game-free, so a page and the checks sort the same way. COST: one sort of the rows when a page is written.
    /// </summary>
    public static class TableRules
    {
        /// <summary>A sort key that says the row has none: it comes last in either direction, as an empty cell did.</summary>
        public const long None = long.MinValue;

        /// <summary>The choice after this one: each kind in turn, then all again (-1).</summary>
        public static int Next(int chosen, int options) => chosen + 1 >= options ? -1 : chosen + 1;

        /// <summary>A choice kept from before, held to the options there are now: none when it is gone.</summary>
        public static int Held(int chosen, int options) => chosen >= 0 && chosen < options ? chosen : -1;

        /// <summary>True when the row passes every filter that has a choice.</summary>
        public static bool Passes(in UiDataRow row, int[] chosen)
        {
            for (int f = 0; chosen != null && f < chosen.Length; f++)
            {
                if (chosen[f] >= 0 && row.Kinds != null && f < row.Kinds.Length && row.Kinds[f] != chosen[f])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The rows to show, as indices into <paramref name="rows"/>: those that pass the filters, ordered by the column (a figure by its sort key, words without one, an
        /// empty one last), the written order kept among equals and where no column is sorted.
        /// </summary>
        public static int[] Order(UiDataRow[] rows, int column, bool down, int[] chosen)
        {
            var keep = new List<int>(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                if (Passes(rows[i], chosen))
                {
                    keep.Add(i);
                }
            }
            if (column >= 0)
            {
                keep.Sort((a, b) =>
                {
                    int c = Compare(rows[a], rows[b], column, down);
                    return c != 0 ? c : a.CompareTo(b);
                });
            }
            return keep.ToArray();
        }

        private static bool Key(in UiDataRow r, int column, out long key)
        {
            key = None;
            if (r.SortKeys != null && column < r.SortKeys.Length)
            {
                key = r.SortKeys[column];
            }
            return key != None;
        }

        private static int Compare(in UiDataRow a, in UiDataRow b, int column, bool down)
        {
            bool hasA = Key(a, column, out long ka), hasB = Key(b, column, out long kb);
            if (hasA || hasB)
            {
                if (hasA != hasB)
                {
                    return hasA ? -1 : 1;   // the row with nothing there is last whichever way the column runs
                }
                int c = ka.CompareTo(kb);
                return down ? -c : c;
            }
            string wa = column < (a.Cells?.Length ?? 0) ? a.Cells[column] : null, wb = column < (b.Cells?.Length ?? 0) ? b.Cells[column] : null;
            bool emptyA = string.IsNullOrEmpty(wa), emptyB = string.IsNullOrEmpty(wb);
            if (emptyA != emptyB)
            {
                return emptyA ? 1 : -1;
            }
            int t = string.Compare(wa, wb, StringComparison.OrdinalIgnoreCase);
            return down ? -t : t;
        }

        /// <summary>The sum of a column's sort keys over the rows shown; rows with none add nothing.</summary>
        public static long Sum(UiDataRow[] rows, int[] order, int column)
        {
            long sum = 0;
            for (int i = 0; i < order.Length; i++)
            {
                if (Key(rows[order[i]], column, out long k))
                {
                    sum += k;
                }
            }
            return sum;
        }

        /// <summary>The items in the order given: the rows' own lists (keys, locate runs) follow the rows through the sort.</summary>
        public static T[] Take<T>(IList<T> items, int[] order)
        {
            var taken = new T[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                taken[i] = order[i] < items.Count ? items[order[i]] : default(T);
            }
            return taken;
        }
    }

    /// <summary>One unit of the rent roll as the figures see it: let or not, what it pays a day, what its tenant owes, and what it asks while it stands empty (a day).</summary>
    public struct RollLine
    {
        public bool Let;
        public int RentDay, Arrears, AskDay;
    }

    /// <summary>The head of the rent roll: what is due in a month, what is owed, what that leaves collected, and what the empty units ask in a month.</summary>
    public struct RollTotals
    {
        public long Due, Owed, Collected, EmptyAsking;
        public int Units, Let;
    }

    /// <summary>
    /// The sums the Property app puts in its heads, worked out in one place so the live pages and the samples cannot add them up two ways: the roll's totals, a mean satisfaction, a day's
    /// rent as a month's, and how a guest's review is rated. Game-free. COST: one pass over the units, when a page is written.
    /// </summary>
    public static class EstateFigures
    {
        /// <summary>A rent a day as the roll shows it, a month's.</summary>
        public static long Month(long perDay, int monthDays) => perDay * Math.Max(1, monthDays);

        /// <summary>The roll's totals from its units. A unit that is let adds its month's rent to what is due and its arrears to what is owed; one that stands empty adds what it asks.</summary>
        public static RollTotals Roll(IList<RollLine> lines, int monthDays)
        {
            var t = new RollTotals();
            for (int i = 0; i < lines.Count; i++)
            {
                RollLine l = lines[i];
                t.Units++;
                if (l.Let)
                {
                    t.Let++;
                    t.Due += Month(l.RentDay, monthDays);
                    t.Owed += l.Arrears;
                }
                else
                {
                    t.EmptyAsking += Month(l.AskDay, monthDays);
                }
            }
            t.Collected = Math.Max(0, t.Due - t.Owed);
            return t;
        }

        /// <summary>The mean of the tenants' satisfaction (0 to 1000 each), or -1 with none judged yet. Whole thousandths, as a building's own mean is.</summary>
        public static int MeanHappy(IList<int> happy)
        {
            long sum = 0;
            int count = 0;
            for (int i = 0; i < happy.Count; i++)
            {
                if (happy[i] >= 0)
                {
                    sum += happy[i];
                    count++;
                }
            }
            return count > 0 ? (int)(sum / count) : -1;
        }

        /// <summary>How many halves of a star a guest gives for a review the hotel was written (HotelRules.ReviewClean to ReviewDear): praise is four or five stars, a complaint one or two.</summary>
        public static int ReviewHalves(int review)
        {
            switch (review)
            {
                case 1: return 10;   // clean
                case 3: return 8;    // the staff
                case 5: return 8;    // good value
                case 2: return 2;    // dirty
                case 4: return 4;    // nobody at the desk
                case 6: return 4;    // too dear
                default: return -1;
            }
        }

        /// <summary>A yield in hundredths of a per cent from a rent a day, a year of days and a price.</summary>
        public static int YieldHundredths(long rentDay, int daysPerYear, long price) => price > 0 ? (int)(rentDay * daysPerYear * 10000L / price) : 0;
    }
}
