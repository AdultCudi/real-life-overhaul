using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in the city by rule, what the kit face reads of the residents that no other part of the rules gave: the districts the followers of
    /// an account live in (the player's own, or an influencer's the player may hire) and the business of the same kind nearest to one of the
    /// player's. Game-free. COST: a walk over one account's followers (the day's index), once a game day for the page that shows it.
    /// </summary>
    public static partial class FlareCityRules
    {
        /// <summary>
        /// The followers of an account by the district they live in, into <paramref name="into"/> (one place a district): the residents the
        /// day's index lists for it. Returns how many were counted; zero for an account the city has no followers of.
        /// </summary>
        public static int FollowersByDistrict(FlareCityState c, PopulationState s, int account, int[] into)
        {
            Array.Clear(into, 0, into.Length);
            int n = account >= 0 ? c.FollowersOf(account, out int start) : 0;
            int counted = 0;
            if (n <= 0)
            {
                return 0;
            }
            c.FollowersOf(account, out start);
            for (int i = 0; i < n && start + i < c.FollowerList.Length; i++)
            {
                int p = c.FollowerList[start + i];
                if (p < 0 || p >= s.People.Count || s.People.Id[p] == 0)
                {
                    continue;
                }
                int d = DistrictOf(s, p);
                if (d >= 0 && d < into.Length)
                {
                    into[d]++;
                    counted++;
                }
            }
            return counted;
        }

        /// <summary>
        /// The business of the same kind in the same district that the residents rate best, for one of the player's: its slot, or -1 when there
        /// is none (or no rival of that kind has been rated). Never the player's own businesses or a session guest's.
        /// </summary>
        public static int Competitor(FlareCityState c, PopulationState s, int b)
        {
            if (b < 0 || b >= s.Businesses.Count || b >= c.BusinessCount)
            {
                return -1;
            }
            BusinessTable bt = s.Businesses;
            int best = -1, bestRating = 0;
            for (int i = 0; i < bt.Count && i < c.BusinessCount; i++)
            {
                bool theirs = bt.Owner[i] != (byte)Population.Owner.Player && bt.Owner[i] != (byte)Population.Owner.Guest;
                if (i == b || bt.Kind[i] != bt.Kind[b] || bt.District[i] != bt.District[b] || !theirs || bt.Key[i] == null)
                {
                    continue;
                }
                int rating = c.Rating(i);
                if (rating > bestRating)
                {
                    best = i;
                    bestRating = rating;
                }
            }
            return best;
        }
    }
}
