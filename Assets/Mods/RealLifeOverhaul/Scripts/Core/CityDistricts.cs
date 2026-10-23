using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The New York the mod is made for, by the game's own district ids: the seven of Build 3682 (NeighborhoodData's
    /// neighbourhood) and the game's district of nowhere in particular. A free update may add districts (the Hamptons came
    /// that way in 1.0) and a DLC may bring another city's; a table of the mod's that names districts treats any other id
    /// as unknown and gives it a default or leaves it out, never an index or a key it does not have. Game-free, checked by
    /// EconomyCheck (the tables) and ContractCheck (against the game's own list).
    /// </summary>
    public static class CityDistricts
    {
        public const string Global = "ba:neighborhood_global";   // the game's district of nowhere in particular: no city's

        /// <summary>The districts of the New York the mod knows, in the game's ordinal order of their ids.</summary>
        public static readonly string[] NewYork =
        {
            "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_industrycity", "ba:neighborhood_lowermanhattan",
            "ba:neighborhood_midtown", "ba:neighborhood_murrayhill", "ba:neighborhood_thehamptons"
        };

        /// <summary>The order that puts the districts the mod knows before any other, each part in ordinal order.</summary>
        public static readonly IComparer<string> KnownFirst = new KnownFirstOrder();

        /// <summary>True for one of the seven districts of the New York the mod knows.</summary>
        public static bool Known(string district)
        {
            return IndexOf(district) >= 0;
        }

        /// <summary>
        /// True for a district of the mod's own (Uptown, Scripts/Uptown): its id has the mod's prefix, which no district of
        /// the game's, a free update's or a DLC's can have. Never one a free update added, never another city's; each table
        /// by district decides for itself whether it takes part (docs/features/uptown.md, "The mod's own tables").
        /// </summary>
        public static bool Mine(string district)
        {
            return DistrictSave.DistrictSaveRules.IsModId(district);
        }

        /// <summary>The district's place in <see cref="NewYork"/>; -1 for any other id, null and the global one included.</summary>
        public static int IndexOf(string district)
        {
            if (string.IsNullOrEmpty(district))
            {
                return -1;
            }
            for (int i = 0; i < NewYork.Length; i++)
            {
                if (string.Equals(NewYork[i], district, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// The districts of <paramref name="source"/> in <paramref name="into"/> (cleared first): those the mod knows in the
        /// order given, then the others in the order given, each once, the global one and empty ids left out, and any for
        /// which <paramref name="here"/> is false left out (a district of the game's data that is not in the loaded city).
        /// So a district a free update adds comes after the seven, and their places, which saves keep by index, stay as
        /// they were. Allocates nothing but <paramref name="into"/>'s growth.
        /// </summary>
        public static List<string> Ordered(IList<string> source, Func<string, bool> here, List<string> into)
        {
            into.Clear();
            if (source == null)
            {
                return into;
            }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    string id = source[i];
                    if (string.IsNullOrEmpty(id) || id == Global || Known(id) != (pass == 0) || into.Contains(id) || (here != null && !here(id)))
                    {
                        continue;
                    }
                    into.Add(id);
                }
            }
            return into;
        }

        private sealed class KnownFirstOrder : IComparer<string>
        {
            public int Compare(string a, string b)
            {
                bool knownA = Known(a), knownB = Known(b);
                if (knownA != knownB)
                {
                    return knownA ? -1 : 1;
                }
                return string.CompareOrdinal(a, b);
            }
        }
    }
}
