using System.Text;

namespace RealLifeOverhaul.DaySeries
{
    /// <summary>The player's pricing plans as the price cut sees them: by place in a list, so the rule needs no game. The company's director gives the real ones.</summary>
    public interface IPlanHolds
    {
        int Count { get; }

        string IdAt(int index);

        /// <summary>The plan is held now: its daily reprice is off.</summary>
        bool Held(int index);

        /// <summary>The plan has a price the plan manages (one the player did not set by hand), so a cut can move it.</summary>
        bool Cuttable(int index);

        /// <summary>Cuts the plan's managed prices by <paramref name="percent"/>; how many prices moved.</summary>
        int Cut(int index, int percent);

        void SetHeld(int index, bool held);
    }

    /// <summary>
    /// The 5 per cent price cut for 14 days, that the player makes and the rivals answer with, and what it does to the plans' holds.
    /// A cut holds the plans it moved so the game's daily reprice does not put the prices back. What was held before the cut is
    /// recorded; the end of the cut takes off only the holds the cut put on. The cut is on only if it moved a price. Game-free, so
    /// the checks run the very handlers the Finance page and the day turn call.
    /// </summary>
    public static class PriceCutRules
    {
        /// <summary>Starts a cut on <paramref name="day"/>. Returns how many prices moved; the book is changed only when that is above zero.</summary>
        public static int Start(DaySeriesBook book, IPlanHolds plans, int day)
        {
            if (book == null || plans == null || day <= 0 || DaySeriesRules.CutOn(book, day))
            {
                return 0;
            }
            var already = new StringBuilder(64);
            var byCut = new StringBuilder(64);
            int moved = 0;
            for (int i = 0; i < plans.Count; i++)
            {
                string id = plans.IdAt(i);
                if (string.IsNullOrEmpty(id) || !plans.Cuttable(i))
                {
                    continue;
                }
                bool held = plans.Held(i);
                int n = plans.Cut(i, DaySeriesRules.CutPercent);
                if (n <= 0)
                {
                    continue;
                }
                moved += n;
                if (held)
                {
                    Add(already, id);
                }
                else
                {
                    plans.SetHeld(i, true);
                    Add(byCut, id);
                }
            }
            if (moved <= 0)
            {
                return 0;
            }
            book.CutFrom = day;
            book.CutUntil = day + DaySeriesRules.CutDays;
            book.HeldAlready = already.ToString();
            book.HeldByCut = byCut.ToString();
            return moved;
        }

        /// <summary>
        /// Ends the cut: the holds the cut put on are taken off, and a plan that was already held stays held. A cut from an old save
        /// (no record of what it held) takes the hold off every plan it could have moved. Returns how many holds were taken off.
        /// </summary>
        public static int End(DaySeriesBook book, IPlanHolds plans)
        {
            if (book == null)
            {
                return 0;
            }
            int freed = 0;
            bool old = book.CutFrom > 0 && string.IsNullOrEmpty(book.HeldByCut) && string.IsNullOrEmpty(book.HeldAlready);
            for (int i = 0; plans != null && i < plans.Count; i++)
            {
                string id = plans.IdAt(i);
                if (string.IsNullOrEmpty(id) || !plans.Held(i) || Has(book.HeldAlready, id))
                {
                    continue;
                }
                if (old ? plans.Cuttable(i) : Has(book.HeldByCut, id))
                {
                    plans.SetHeld(i, false);
                    freed++;
                }
            }
            book.CutFrom = 0;
            book.CutUntil = 0;
            book.HeldAlready = string.Empty;
            book.HeldByCut = string.Empty;
            return freed;
        }

        /// <summary>Whether the day turn must end the cut: it is on and its last day has passed.</summary>
        public static bool Due(DaySeriesBook book, int day) => book != null && book.CutFrom > 0 && day >= book.CutUntil;

        /// <summary>The day turn's part: ends the cut when it is due. True if it ended.</summary>
        public static bool Turn(DaySeriesBook book, IPlanHolds plans, int day)
        {
            if (!Due(book, day))
            {
                return false;
            }
            End(book, plans);
            return true;
        }

        /// <summary>Whether any plan could be cut now, for the button to say so before it is pressed.</summary>
        public static bool Possible(IPlanHolds plans)
        {
            for (int i = 0; plans != null && i < plans.Count; i++)
            {
                if (!string.IsNullOrEmpty(plans.IdAt(i)) && plans.Cuttable(i))
                {
                    return true;
                }
            }
            return false;
        }

        private static void Add(StringBuilder list, string id)
        {
            if (list.Length > 0)
            {
                list.Append(' ');
            }
            list.Append(DaySeriesCatalogue.Clean(id).Replace(' ', '_'));
        }

        /// <summary>Whether a plan's id is in a list of ids, whole tokens only.</summary>
        public static bool Has(string list, string id)
        {
            if (string.IsNullOrEmpty(list) || string.IsNullOrEmpty(id))
            {
                return false;
            }
            string clean = DaySeriesCatalogue.Clean(id).Replace(' ', '_');
            int at = 0;
            while (at < list.Length)
            {
                int end = list.IndexOf(' ', at);
                end = end < 0 ? list.Length : end;
                if (end - at == clean.Length && string.CompareOrdinal(list, at, clean, 0, clean.Length) == 0)
                {
                    return true;
                }
                at = end + 1;
            }
            return false;
        }
    }
}
