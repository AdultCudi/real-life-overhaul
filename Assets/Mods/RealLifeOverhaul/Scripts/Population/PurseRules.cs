using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// Money another feature takes from, or gives back to, the savings of a district's households: the one way in, so
    /// that what leaves a household is what the taker gets and what goes back is what the households gain, to the unit.
    /// Called on a press or a day's turn on the main thread, never while the population's own day is being worked on a
    /// save's workers. Game-free.
    /// </summary>
    public static class PurseRules
    {
        /// <summary>
        /// Takes a stake from the households of a district that a draw picks: of those whose stake is more than nothing,
        /// the <paramref name="households"/> with the lowest draw (ties by their ids), each paying what
        /// <paramref name="stake"/> says of its savings, never more than it has. Returns what was taken in all.
        /// </summary>
        /// <param name="draw">A household's draw by its id: the same households for the same asking.</param>
        /// <param name="stake">What a household with these savings pays; 0 for one left alone.</param>
        public static long Take(PopulationState s, int district, int households, Func<int, int> draw, Func<long, int> stake, out int joined)
        {
            joined = 0;
            if (s == null || draw == null || stake == null || households <= 0)
            {
                return 0;
            }
            HouseholdTable hh = s.Households;
            int candidates = 0;
            for (int h = 0; h < hh.Count; h++)
            {
                candidates += hh.Id[h] != 0 && hh.District[h] == district && stake(hh.Savings[h]) > 0 ? 1 : 0;
            }
            if (candidates == 0)
            {
                return 0;
            }
            var keys = new long[candidates];
            var slots = new int[candidates];
            for (int h = 0, n = 0; h < hh.Count; h++)
            {
                if (hh.Id[h] != 0 && hh.District[h] == district && stake(hh.Savings[h]) > 0)
                {
                    keys[n] = ((long)Math.Max(0, draw(hh.Id[h])) << 32) | (uint)hh.Id[h];
                    slots[n++] = h;
                }
            }
            Array.Sort(keys, slots);
            long taken = 0;
            for (int i = 0; i < candidates && joined < households; i++)
            {
                int h = slots[i];
                int pays = Math.Min(Math.Max(0, stake(hh.Savings[h])), Math.Max(0, hh.Savings[h]));
                if (pays <= 0)
                {
                    continue;
                }
                hh.Savings[h] -= pays;
                taken += pays;
                joined++;
            }
            return taken;
        }

        /// <summary>
        /// Gives money back to the households of a district, in even parts (the first ones a unit more where it does not
        /// divide); to every household of the city when the district has none. Returns what they took: all of it while
        /// there is a household at all.
        /// </summary>
        public static long Give(PopulationState s, int district, long amount)
        {
            if (s == null || amount <= 0)
            {
                return 0;
            }
            HouseholdTable hh = s.Households;
            int there = 0, all = 0;
            for (int h = 0; h < hh.Count; h++)
            {
                all += hh.Id[h] != 0 ? 1 : 0;
                there += hh.Id[h] != 0 && hh.District[h] == district ? 1 : 0;
            }
            bool anywhere = there == 0;
            int count = anywhere ? all : there;
            if (count == 0)
            {
                return 0;
            }
            long each = amount / count, odd = amount % count, given = 0;
            for (int h = 0; h < hh.Count; h++)
            {
                if (hh.Id[h] == 0 || (!anywhere && hh.District[h] != district))
                {
                    continue;
                }
                long part = each + (odd-- > 0 ? 1 : 0);
                long room = int.MaxValue / 2 - (long)hh.Savings[h];
                part = Math.Min(part, Math.Max(0L, room));
                hh.Savings[h] += (int)part;
                given += part;
            }
            return given;
        }
    }
}
