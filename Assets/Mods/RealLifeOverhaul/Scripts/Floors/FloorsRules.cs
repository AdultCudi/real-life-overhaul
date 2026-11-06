using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Floors
{
    /// <summary>A building the mod holds, as the floors' rules see it: its address, its holder's turn for spare floors, and its floors.</summary>
    public struct FloorHostRow
    {
        public string Key;       // "street#number": the same on every machine of a session
        public int Rank;         // its holder's turn for spare floors (FloorsRules.RankTowers, RankCampus, RankClaimed)
        public int Used;         // floors its holder's own places take, from floor 1
        public int Capacity;     // floors it holds in all (FloorsRules.Capacity)
    }

    /// <summary>A place a feature asked the floors for, with the floors it takes.</summary>
    public struct FloorGuestRow
    {
        public string Id;        // the place's own id, unique
        public int Floors;       // floors it takes, one at least
    }

    /// <summary>
    /// The floors' rules, game-free: which floors a building holds, which place goes on which spare floor and when a building
    /// is taken for places that found none, the floors on a page of the game's four-button panel, and the floor the game's own
    /// door takes the player to. Every answer is a function of the buildings and places alone, never of the order they came in,
    /// so every machine of a Going Public session gives the same one; nothing of it is saved, it is worked out again at each
    /// load. Checked offline by EconomyCheck (FloorsChecks.cs).
    /// </summary>
    public static class FloorsRules
    {
        public const int MostFloors = 12;     // floors a building holds at most: four pages of the game's panel, three floors to a page
        public const int Slots = 4;           // buttons of the game's panel over a place (GameUi/WorldPanel.Slots), one per key it is sent
        public const int RankTowers = 0;      // spare floors go first to the tech companies' towers,
        public const int RankCampus = 1;      // then to the university's building,
        public const int RankClaimed = 2;     // then to a building taken for places that found no room
        public const int MostClaims = 4;      // buildings ever taken for places: a city holds a dozen offices to let, the mod never takes them all

        /// <summary>
        /// The floors a building holds: its floor area over one storey's (the game's interior for its size), never fewer than its
        /// holder's own floors and never more than <see cref="MostFloors"/> unless the holder's own need them.
        /// </summary>
        public static int Capacity(int totalSqm, int storeySqm, int own)
        {
            int bySize = storeySqm > 0 && totalSqm > 0 ? totalSqm / storeySqm : 1;
            return Math.Max(Math.Max(1, own), Math.Min(MostFloors, Math.Max(1, bySize)));
        }

        /// <summary>The floor the game's own door takes the player to: the one wanted while there is such a floor, else the first.</summary>
        public static int DoorFloor(int floors, int wanted)
        {
            return floors <= 0 ? 0 : wanted >= 1 && wanted <= floors ? wanted : 1;
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

        /// <summary>How many pages the floors take on the panel (one when they fit its four buttons).</summary>
        public static int Pages(int floors, int skip, int slots)
        {
            int count = Math.Max(0, floors) - (skip >= 1 && skip <= floors ? 1 : 0);
            if (count <= slots)
            {
                return 1;
            }
            int per = slots - 1;
            return (count + per - 1) / per;
        }

        /// <summary>The page after <paramref name="page"/>, back to the first after the last.</summary>
        public static int NextPage(int page, int floors, int skip, int slots)
        {
            int pages = Pages(floors, skip, slots);
            return pages <= 1 ? 0 : (Math.Max(0, page) + 1) % pages;
        }

        /// <summary>
        /// Spare floors for the places asked for. The buildings in their holders' turn, then by address; the places by id; each place
        /// the first run of free floors that holds it, above its holder's own. hostOf[g] is the building's index in
        /// <paramref name="hosts"/> (-1: no room anywhere), firstFloor[g] its first floor (0 with none). The same answer for the same
        /// buildings and places in any order. Returns how many found room. COST: run when a building or a place changes, not per frame.
        /// </summary>
        public static int Assign(IList<FloorHostRow> hosts, IList<FloorGuestRow> guests, int[] hostOf, int[] firstFloor)
        {
            int guestCount = guests != null ? guests.Count : 0;
            for (int g = 0; g < guestCount && g < hostOf.Length && g < firstFloor.Length; g++)
            {
                hostOf[g] = -1;
                firstFloor[g] = 0;
            }
            int hostCount = hosts != null ? hosts.Count : 0;
            if (guestCount == 0 || hostCount == 0)
            {
                return 0;
            }
            int[] hostOrder = HostOrder(hosts);
            int[] guestOrder = GuestOrder(guests);
            var next = new int[hostCount];   // each building's lowest free floor
            for (int h = 0; h < hostCount; h++)
            {
                next[h] = Math.Max(0, hosts[h].Used) + 1;
            }
            int placed = 0;
            for (int k = 0; k < guestOrder.Length; k++)
            {
                int g = guestOrder[k];
                if (g >= hostOf.Length || g >= firstFloor.Length)
                {
                    continue;
                }
                int wants = Math.Max(1, guests[g].Floors);
                for (int r = 0; r < hostOrder.Length; r++)
                {
                    int h = hostOrder[r];
                    if (next[h] + wants - 1 <= hosts[h].Capacity)
                    {
                        hostOf[g] = h;
                        firstFloor[g] = next[h];
                        next[h] += wants;
                        placed++;
                        break;
                    }
                }
            }
            return placed;
        }

        /// <summary>
        /// The buildings to take for the places that found no room: one for every <see cref="MostFloors"/> floors they take
        /// (never one a place), at most <see cref="MostClaims"/>. A place larger than a whole building is never given one.
        /// </summary>
        public static int ClaimsFor(IList<FloorGuestRow> guests, int[] hostOf)
        {
            int floors = 0;
            for (int g = 0; guests != null && g < guests.Count && g < hostOf.Length; g++)
            {
                int wants = Math.Max(1, guests[g].Floors);
                if (hostOf[g] < 0 && wants <= MostFloors)
                {
                    floors += wants;
                }
            }
            return Math.Min(MostClaims, (floors + MostFloors - 1) / MostFloors);
        }

        /// <summary>True for a building taken for places that no place has a floor of now: it is given back.</summary>
        public static bool ClaimIdle(int host, int[] hostOf, int guests)
        {
            for (int g = 0; g < guests && g < hostOf.Length; g++)
            {
                if (hostOf[g] == host)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True if a floor's structure is the building's own: by size and version, a structure asked for as none being the building's own.</summary>
        public static bool Own(string floorSize, int floorVersion, string ownSize, int ownVersion)
        {
            return string.IsNullOrEmpty(floorSize) || (floorVersion == ownVersion && string.Equals(floorSize, ownSize, StringComparison.Ordinal));
        }

        /// <summary>A building's key for the floors: its street and number, as every machine names it.</summary>
        public static string Key(string street, int number) => (street ?? string.Empty) + "#" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // The buildings in their holders' turn, then by address (ordinal): the order every machine walks them in.
        private static int[] HostOrder(IList<FloorHostRow> hosts)
        {
            var order = new int[hosts.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }
            Array.Sort(order, (a, b) =>
            {
                int byRank = hosts[a].Rank.CompareTo(hosts[b].Rank);
                if (byRank != 0)
                {
                    return byRank;
                }
                int byKey = string.CompareOrdinal(hosts[a].Key, hosts[b].Key);
                return byKey != 0 ? byKey : a.CompareTo(b);
            });
            return order;
        }

        // The places by id (ordinal), whatever order they asked in.
        private static int[] GuestOrder(IList<FloorGuestRow> guests)
        {
            var order = new int[guests.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }
            Array.Sort(order, (a, b) =>
            {
                int byId = string.CompareOrdinal(guests[a].Id, guests[b].Id);
                return byId != 0 ? byId : a.CompareTo(b);
            });
            return order;
        }
    }
}
