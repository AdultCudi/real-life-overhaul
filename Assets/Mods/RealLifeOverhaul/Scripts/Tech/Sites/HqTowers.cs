using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One of the city's office buildings that may be a tower of the tech companies.</summary>
    public struct HqCandidate
    {
        public string Street;
        public int Number;
        public int Sqm;
    }

    /// <summary>
    /// Which office buildings house the tech companies, and each company's floor: the same city gives the same answer on every
    /// machine, and a building let moves only its own tower. Game-free.
    /// </summary>
    public static class HqTowers
    {
        public const int PerTower = 4;   // companies to a tower, a floor each (the four buttons of the game's panel at a door)

        /// <summary>The towers wanted for so many companies.</summary>
        public static int Towers(int companies) => companies <= 0 ? 0 : (companies + PerTower - 1) / PerTower;

        public static string Key(string street, int number) => (street ?? string.Empty) + "#" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The place key of a company's floor of a tower, which keeps a job's desk on the employer's own floor.</summary>
        public static string FloorPlace(string place, string company) => string.IsNullOrEmpty(company) || string.IsNullOrEmpty(place) ? place : place + "#" + company;

        /// <summary>into[t] is the candidate index of tower t's building (-1 with too few), each tower in its lane of the ranking. Returns how many have one.</summary>
        public static int Pick(IList<HqCandidate> all, int towers, Func<int, bool> taken, int[] into)
        {
            for (int t = 0; t < into.Length; t++)
            {
                into[t] = -1;
            }
            towers = Math.Min(towers, into.Length);
            if (all == null || all.Count == 0 || towers <= 0)
            {
                return 0;
            }
            var order = new int[all.Count];
            var keys = new string[all.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
                keys[i] = Key(all[i].Street, all[i].Number);
            }
            Array.Sort(order, (a, b) =>
            {
                int bySize = all[b].Sqm.CompareTo(all[a].Sqm);
                return bySize != 0 ? bySize : string.CompareOrdinal(keys[a], keys[b]);
            });
            var used = new bool[all.Count];
            int have = 0;
            for (int t = 0; t < towers; t++)
            {
                for (int r = t; r < order.Length; r += towers)
                {
                    int i = order[r];
                    if (taken == null || !taken(i))
                    {
                        into[t] = i;
                        used[i] = true;
                        have++;
                        break;
                    }
                }
            }
            for (int t = 0; t < towers && have < towers; t++)
            {
                for (int r = 0; into[t] < 0 && r < order.Length; r++)
                {
                    int i = order[r];
                    if (!used[i] && (taken == null || !taken(i)))
                    {
                        into[t] = i;
                        used[i] = true;
                        have++;
                    }
                }
            }
            return have;
        }

        /// <summary>Each company's tower (towerOf[k], -1 for none) and floor (floorOf[k], from 1, 0 for none).</summary>
        public static void Floors(int companies, int[] buildings, int[] towerOf, int[] floorOf)
        {
            int have = 0;
            for (int t = 0; buildings != null && t < buildings.Length; t++)
            {
                have += buildings[t] >= 0 ? 1 : 0;
            }
            int per = have > 0 ? Math.Max(PerTower, (companies + have - 1) / have) : 0;
            for (int k = 0; k < companies && k < towerOf.Length && k < floorOf.Length; k++)
            {
                towerOf[k] = -1;
                floorOf[k] = 0;
                if (have == 0)
                {
                    continue;
                }
                int nth = k / per, seen = -1;
                for (int t = 0; t < buildings.Length; t++)
                {
                    if (buildings[t] >= 0 && ++seen == nth)
                    {
                        towerOf[k] = t;
                        floorOf[k] = k % per + 1;
                        break;
                    }
                }
            }
        }

        /// <summary>The floors of a tower: how many companies are on it.</summary>
        public static int FloorsOf(int tower, int[] towerOf, int companies)
        {
            int n = 0;
            for (int k = 0; k < companies && k < towerOf.Length; k++)
            {
                n += towerOf[k] == tower ? 1 : 0;
            }
            return n;
        }

        /// <summary>The floor the game's door takes the player to: their employer's (<paramref name="employerFloor"/> above 0), else the first.</summary>
        public static int DoorFloor(int floors, int employerFloor)
        {
            return floors <= 0 ? 0 : employerFloor >= 1 && employerFloor <= floors ? employerFloor : 1;
        }

        /// <summary>The floors on a page of the game's four-button panel (three and "more floors" when they do not fit), less the player's own.</summary>
        public static int Page(int floors, int skip, int page, int[] into)
        {
            int count = Math.Max(0, floors) - (skip >= 1 && skip <= floors ? 1 : 0);
            bool paged = count > into.Length;
            int per = paged ? into.Length - 1 : into.Length;
            int start = paged && count > 0 ? (Math.Max(0, page) * per) % count : 0;
            for (int slot = 0; slot < into.Length; slot++)
            {
                if (paged && slot == into.Length - 1)
                {
                    into[slot] = -1;   // "more floors"
                    continue;
                }
                int i = start + slot;   // the i-th floor offered, the one the player is on left out
                into[slot] = slot < per && i < count ? i + 1 + (skip >= 1 && i + 1 >= skip ? 1 : 0) : 0;
            }
            return count;
        }
    }
}
