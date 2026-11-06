using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Offices
{
    /// <summary>One building as the conversion sees it: an office of the game's own, or a shop that may be one.</summary>
    public struct ConversionRow
    {
        public string Street;
        public int Number;
        public string District;
        public bool Office;   // an office building as the game made it, or a shop whose structure the game shares with its offices
        public bool Free;     // empty and to let, nobody's and no other feature's: the player could rent it now
        public bool Held;     // a shop that is an office now (or held an office business at load) and somebody has: it stays an office
    }

    /// <summary>
    /// Which of the city's empty shops are offices while the mod runs: at least <see cref="DistrictFloor"/> offices to let
    /// in each district with offices and <see cref="CityFloor"/> in the city, the game's own free offices first, never more
    /// than <see cref="Cap"/> of a district's shops. Shops are picked by a hash of their address (<see cref="Rank"/>), so
    /// every machine picks the same; a held one stays an office. Game-free: checked in NavCheck.
    /// </summary>
    public static class ConversionRules
    {
        public const int DistrictFloor = 2;   // offices to let in a district with offices: a choice, not a single one
        public const int CityFloor = 8;       // offices to let in the city; holds when a district has no shop to spare

        /// <summary>The most shops of a district that may be offices at once: half of them, and at least the floor.</summary>
        public static int Cap(int shops) => Math.Max(DistrictFloor, shops / 2);

        public static string Key(string street, int number) => (street ?? string.Empty) + "#" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A shop's place in the ranking: FNV-1a over its street's id and its number, the same on every machine.</summary>
        public static uint Rank(string street, int number)
        {
            uint h = 2166136261u;
            string s = street ?? string.Empty;
            for (int i = 0; i < s.Length; i++)
            {
                h = (h ^ s[i]) * 16777619u;
            }
            uint n = unchecked((uint)number);
            for (int i = 0; i < 4; i++)
            {
                h = (h ^ (n & 0xFFu)) * 16777619u;
                n >>= 8;
            }
            return h;
        }

        /// <summary>
        /// Whether a building's type reads the same in every copy of the building the game keeps: the city scene's (what the map card,
        /// the filters and the marker read) and the game's own list (what BizMan, the rent, the businesses that may start and the
        /// customers read by address). Null when every copy reads <paramref name="wanted"/>; otherwise what differs. A missing copy
        /// (null) is not a copy: a building the game's list does not hold has one place to read.
        /// </summary>
        public static string Disagreement(string wanted, string sceneType, string gameType)
        {
            if (!string.Equals(sceneType, wanted, StringComparison.Ordinal))
            {
                return "the city's copy reads " + (sceneType ?? "(none)") + ", not " + wanted;
            }
            if (gameType != null && !string.Equals(gameType, wanted, StringComparison.Ordinal))
            {
                return "the game's list reads " + gameType + ", not " + wanted + " (the map says one thing and BizMan another)";
            }
            return null;
        }

        /// <summary>
        /// The first business type a building type offers whose own suitable building type is another, or a type the game does not
        /// know: null when every offered business suits the type. <paramref name="suitableOf"/> gives a business type's suitable
        /// building type, null for none.
        /// </summary>
        public static string WrongOffer(string buildingType, IList<string> offered, Func<string, string> suitableOf)
        {
            for (int i = 0; offered != null && i < offered.Count; i++)
            {
                string suits = suitableOf != null ? suitableOf(offered[i]) : null;
                if (!string.Equals(suits, buildingType, StringComparison.Ordinal))
                {
                    return offered[i] + " (suits " + (suits ?? "nothing") + ")";
                }
            }
            return null;
        }

        /// <summary>The districts with offices, most offices first (ties by name): the order the city's floor is filled in.</summary>
        public static List<string> Districts(IList<ConversionRow> rows)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                if (rows[i].Office && rows[i].District != null)
                {
                    counts.TryGetValue(rows[i].District, out int n);
                    counts[rows[i].District] = n + 1;
                }
            }
            var names = new List<string>(counts.Keys);
            names.Sort((a, b) =>
            {
                int byCount = counts[b].CompareTo(counts[a]);
                return byCount != 0 ? byCount : string.CompareOrdinal(a, b);
            });
            return names;
        }

        /// <summary>Sets into[i] for each shop to be an office now; returns the offices to let across the office districts.</summary>
        public static int Decide(IList<ConversionRow> rows, bool[] into)
        {
            int count = rows != null ? rows.Count : 0;
            for (int i = 0; i < into.Length; i++)
            {
                into[i] = i < count && !rows[i].Office && rows[i].Held;
            }
            if (count == 0)
            {
                return 0;
            }
            List<string> districts = Districts(rows);
            int d = districts.Count;
            var free = new int[d];
            var shops = new int[d];
            var held = new int[d];
            var take = new int[d];
            var eligible = new List<int>[d];
            var at = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int k = 0; k < d; k++)
            {
                at[districts[k]] = k;
                eligible[k] = new List<int>();
            }
            for (int i = 0; i < count; i++)
            {
                ConversionRow r = rows[i];
                if (r.District == null || !at.TryGetValue(r.District, out int k))
                {
                    continue;   // a district without offices keeps its shops
                }
                if (r.Office)
                {
                    free[k] += r.Free ? 1 : 0;
                    continue;
                }
                shops[k]++;
                if (r.Held)
                {
                    held[k]++;
                }
                else if (r.Free)
                {
                    eligible[k].Add(i);
                }
            }
            var ranks = new uint[count];
            var keys = new string[count];
            for (int i = 0; i < count; i++)
            {
                ranks[i] = Rank(rows[i].Street, rows[i].Number);
                keys[i] = Key(rows[i].Street, rows[i].Number);
            }
            int total = 0;
            var room = new int[d];
            for (int k = 0; k < d; k++)
            {
                eligible[k].Sort((a, b) =>
                {
                    int byRank = ranks[a].CompareTo(ranks[b]);
                    return byRank != 0 ? byRank : string.CompareOrdinal(keys[a], keys[b]);
                });
                room[k] = Math.Min(Math.Max(0, Cap(shops[k]) - held[k]), eligible[k].Count);
                take[k] = Math.Min(Math.Max(0, DistrictFloor - free[k]), room[k]);
                total += free[k] + take[k];
            }
            while (total < CityFloor)
            {
                bool more = false;
                for (int k = 0; k < d && total < CityFloor; k++)
                {
                    if (take[k] < room[k])
                    {
                        take[k]++;
                        total++;
                        more = true;
                    }
                }
                if (!more)
                {
                    break;
                }
            }
            for (int k = 0; k < d; k++)
            {
                for (int j = 0; j < take[k]; j++)
                {
                    into[eligible[k][j]] = true;
                }
            }
            return total;
        }
    }
}
