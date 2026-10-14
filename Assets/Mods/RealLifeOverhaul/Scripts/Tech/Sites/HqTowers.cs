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
    /// Which of the city's office buildings house the tech companies, and on which floor each company is. The twelve
    /// companies share a few large towers, a floor each, so that the mod holds three office buildings where it held
    /// twelve and the rest stay on the market for the player (the owner, 14 Oct 2026: "every office is occupied by a
    /// company").
    ///
    ///   how many     <see cref="PerTower"/> companies to a tower: the game's panel at a door has four buttons, so every
    ///                floor of a tower is offered at once; twelve companies make three towers
    ///   which        the largest free office buildings: the candidates ranked by floor (ties by address), each tower
    ///                in a lane of its own of that ranking (<see cref="Pick"/>), so that no two compete for a building
    ///   the floors   company k is on floor k mod <see cref="PerTower"/> + 1 of tower k / <see cref="PerTower"/>; with
    ///                fewer buildings than towers the companies are spread over those there are, more floors to each,
    ///                so every company has a floor while one building is free
    ///
    /// Every machine that has the same city gets the same answer (no dice, nothing kept), and a building that is let moves
    /// only the tower that stood in it. Game-free: checked in NavCheck (TowerCheck.cs).
    /// </summary>
    public static class HqTowers
    {
        public const int PerTower = 4;   // companies to a tower, a floor each (the four buttons of the game's panel at a door)

        /// <summary>The towers wanted for so many companies.</summary>
        public static int Towers(int companies) => companies <= 0 ? 0 : (companies + PerTower - 1) / PerTower;

        public static string Key(string street, int number) => (street ?? string.Empty) + "#" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// The place key of one company's floor of a tower: the building's place key and the company's name; the building's
        /// alone where there is no company. The player's desk on a fitted floor is kept by it (FitOut/FitDesks), and the
        /// careers ask for the desk of a job by the same key, so a desk stands only on the employer's own floor.
        /// </summary>
        public static string FloorPlace(string place, string company) => string.IsNullOrEmpty(company) || string.IsNullOrEmpty(place) ? place : place + "#" + company;

        /// <summary>
        /// into[t] is the index among the candidates of tower t's building, or -1 when there are too few. Returns how many
        /// towers have a building. The candidates are ranked by floor, the largest first (ties by address), whatever is
        /// in them; tower t's lane is every rank r with r mod the towers equal to t, and the tower takes the largest free
        /// building of its own lane. So no tower ever competes with another for a building: one let moves only the tower
        /// that stood in it, and a tower never moves for a building let in another's lane. A tower whose lane has nothing
        /// free takes the largest free building left, the towers in turn.
        /// </summary>
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

        /// <summary>
        /// The tower (towerOf[k], an index into <paramref name="buildings"/>, or -1) and floor (floorOf[k], from 1, or 0) of
        /// each of so many companies, for the towers' buildings as <see cref="Pick"/> gave them (-1 for a tower without).
        /// </summary>
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

        /// <summary>
        /// The floor the player goes to when coming in by the game's own door: the floor of the company they work for if it is
        /// in this tower (<paramref name="employerFloor"/> above 0), else the first.
        /// </summary>
        public static int DoorFloor(int floors, int employerFloor)
        {
            return floors <= 0 ? 0 : employerFloor >= 1 && employerFloor <= floors ? employerFloor : 1;
        }

        /// <summary>The floors offered on a page of the game's panel (four buttons; three and "more floors" when they do not fit), leaving out the one the player is on.</summary>
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
