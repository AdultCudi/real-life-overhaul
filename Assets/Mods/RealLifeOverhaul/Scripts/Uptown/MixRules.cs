using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The rules of Uptown's mix, game-free (EconomyCheck's UptownMixChecks): the share of its shops, offices and homes kept
    /// free for the player to rent (the owner, 25 Oct 2026: "Increase renting more in Uptown", a third), how many units of
    /// a type the mix pass opens or frees to stand at exactly that share, the kind of business a unit takes so a street
    /// holds many kinds and few of one, the most of one kind a street keeps, and the names of the mod's own shops run by the
    /// city. Every answer comes from the arguments alone, picks by FNV-1a hashes of the lot's id and the save's seed: the
    /// same on every machine of a save, in any order the units come.
    /// COST: nothing per frame; a few sorts and counts over the district's units once a save.
    /// </summary>
    public static class MixRules
    {
        public const int ToLetShare = 33;          // % of Uptown's shops, offices and homes kept free to rent: the owner's third (25 Oct 2026), against Manhattan's 6 to 20 %
        public const int SameStreetMost = 2;       // businesses of one kind a street keeps from the first day: a third makes the mix pass give it another kind
        public const string HomeType = "ba:buildingtype_residential";

        /// <summary>How many of a type's units stand free to rent at the share: rounded as the game's MarkBuildingsForRent rounds it, halves to even.</summary>
        public static int Kept(int units, int sharePercent)
        {
            if (units <= 0)
            {
                return 0;
            }
            int share = Math.Max(0, Math.Min(100, sharePercent));
            return (int)Math.Round(units * share / 100.0, MidpointRounding.ToEven);
        }

        /// <summary>
        /// How many units to change so exactly the share stands free to rent: above 0, that many empty ones are given a
        /// business (homes, residents); below 0, that many of the city's are freed to rent; 0 when the share stands.
        /// </summary>
        public static int Change(int units, int free, int sharePercent)
        {
            if (units <= 0)
            {
                return 0;
            }
            int kept = Kept(units, sharePercent);
            return Math.Max(0, Math.Min(units, free)) - kept;
        }

        /// <summary>The order units are changed in: by the hash of each lot's id, the step and the save's seed.</summary>
        public static uint Order(string lotId, string step, string seed)
        {
            return NewBuildingsRules.Hash((lotId ?? string.Empty) + "|" + (step ?? string.Empty) + "|" + (seed ?? string.Empty));
        }

        /// <summary>
        /// The kind of business a unit takes, as an index into <paramref name="kinds"/> (the types that fit its structure),
        /// or -1 of none: the kind the unit's street holds fewest of (<paramref name="onStreet"/>: how many of each it holds
        /// now), ties by the hash of the lot, the kind and the seed. So a street fills with many kinds before it has two of one.
        /// </summary>
        public static int Kind(IList<string> kinds, IList<int> onStreet, string lotId, string seed)
        {
            int best = -1;
            uint bestHash = 0;
            for (int i = 0; kinds != null && i < kinds.Count; i++)
            {
                int count = onStreet != null && i < onStreet.Count ? onStreet[i] : 0;
                uint hash = Order(lotId, "kind|" + kinds[i], seed);
                int bestCount = best >= 0 && onStreet != null && best < onStreet.Count ? onStreet[best] : 0;
                if (best < 0 || count < bestCount || (count == bestCount && hash < bestHash))
                {
                    best = i;
                    bestHash = hash;
                }
            }
            return best;
        }

        /// <summary>True when a street holds more of a kind than it keeps: the mix pass gives the unit another.</summary>
        public static bool TooMany(int ofKindOnStreet)
        {
            return ofKindOnStreet > SameStreetMost;
        }

        // The names the city's shops of the mod's own kinds trade under: proper names, the same in every language.
        private static readonly string[] IceCreamNames = { "Scoops on Summit", "Crest Creamery", "Willow Gelato", "Two Cones", "Birch and Berry", "Lakeside Scoop" };
        private static readonly string[] TeaNames = { "Leaf and Kettle", "Steep Street Tea", "Birch Tea House", "Oolong Corner", "Ridge Tea Rooms", "Maple Leaf Tea" };
        private static readonly string[] TobaccoNames = { "Ridge Tobacco", "The Cigar Box", "Elm Smoke Shop", "Hill and Pipe", "Grove Tobacconist", "Cedar Cigars" };

        /// <summary>The name a city's shop of one of the mod's kinds trades under (by its type's word), picked by the lot's hash; null for a type with none.</summary>
        public static string ShopName(string type, string lotId, string seed)
        {
            string[] names = type == null ? null
                             : type.EndsWith("icecreamparlour", StringComparison.Ordinal) ? IceCreamNames
                             : type.EndsWith("teabar", StringComparison.Ordinal) ? TeaNames
                             : type.EndsWith("tobacconist", StringComparison.Ordinal) ? TobaccoNames : null;
            if (names == null)
            {
                return null;
            }
            return names[(int)(Order(lotId, "name", seed) % (uint)names.Length)];
        }

        public const int SharePoints = 5;          // points a type's share free to rent may stand from the third once the game's own days have let and filled units

        /// <summary>Why a type's units do not stand at the share free to rent (English, for the log), or null: within <see cref="SharePoints"/> of it, or one unit.</summary>
        public static string ShareWhy(string what, int units, int free, int sharePercent)
        {
            if (units <= 0)
            {
                return null;
            }
            int kept = Kept(units, sharePercent);
            int slack = Math.Max(1, (int)Math.Ceiling(units * SharePoints / 100.0));
            return Math.Abs(free - kept) <= slack ? null : what + ": " + free + " of " + units + " free to rent, wanted " + kept;
        }

        /// <summary>A census line's part: each kind with its count, the most first, ties by name, ordinal.</summary>
        public static string Spread(Dictionary<string, int> kinds)
        {
            if (kinds == null || kinds.Count == 0)
            {
                return "none";
            }
            var list = new List<KeyValuePair<string, int>>(kinds);
            list.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            var t = new System.Text.StringBuilder(256);
            for (int i = 0; i < list.Count; i++)
            {
                t.Append(i > 0 ? ", " : string.Empty).Append(OfficesRules.Word(list[i].Key)).Append(' ').Append(list[i].Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return t.ToString();
        }
    }
}
