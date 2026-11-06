using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// One of the game's building structures (a size and a version) as Uptown's grid of spaces takes it: its floor, the
    /// frontage and depth the game's own buildings of it have along their streets, its structure's own floor, and the
    /// whole spaces it stands on.
    /// </summary>
    public sealed class SpaceSize
    {
        public readonly string Size;        // the game's size id ("ba:buildingsize_a")
        public readonly int Version;        // its version (the game's A1, A2 and so on)
        public readonly int Sqm;            // BuildingSizeData.squareMeters
        public readonly float Front;        // m: the median frontage of the game's own buildings of it that the copy engine can copy
        public readonly float Deep;         // m: their median depth back from the street
        public readonly float InnerFront;   // m: its structure prefab's floor along its way in (the interior the game loads)
        public readonly float InnerDeep;    // m: and back from it
        public readonly LotClass Class;     // the plan's class of it, for Manhattan's mix
        public readonly int Weight;         // the game's own buildings of it, Build 3682: its share within its class
        public readonly int Spaces;         // whole spaces it stands on: its frontage rounded up
        public readonly int DepthClass;     // m: the shallowest strip it fits (SpacesRules' depth classes)

        public SpaceSize(char size, int version, int sqm, float front, float deep, float innerFront, float innerDeep, LotClass kind, int weight)
        {
            Size = "ba:buildingsize_" + size;
            Version = version;
            Sqm = sqm;
            Front = front;
            Deep = deep;
            InnerFront = innerFront;
            InnerDeep = innerDeep;
            Class = kind;
            Weight = weight;
            Spaces = SpacesRules.SpacesFor(front);
            DepthClass = SpacesRules.DepthClassFor(deep);
        }

        /// <summary>"A1", "K1": as the game's blueprints name it.</summary>
        public string Name => char.ToUpperInvariant(Size[Size.Length - 1]) + Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Uptown's grid of spaces (the owner, 25 Oct 2026: "A lot must be split properly, so a bigger lot must take 2 spaces,
    /// 3 spaces, like that. 1 space is the standard lowest one", and "there are multiple building sizes ranging from A1 to K
    /// something. Based on that make the lot split"): every frontage cut into spaces of the game's narrowest building, A1,
    /// and every building of the district standing on the whole spaces its size and version take. The figures are the game's
    /// own, Build 3682 (not-in-repo filllab/facts/size_spaces.py; EconomyCheck's UptownSpacesChecks holds the table to the
    /// game's designs). Game-free and fixed, the same on every machine. COST: reads of a fixed table.
    /// </summary>
    public static class SpacesRules
    {
        // A1's frontage (6.3 m, the narrowest of every size the grid takes) to the whole metre: the plan's widths are whole
        // metres, as its plots and land prices are, and a space never narrower than the building it is for.
        public const int SpaceWidth = 7;
        public const float FillSliver = 1f;   // m of a building's spaces its look may leave: no gap to the eye (FrontageRules.SliverMost)

        // The depth classes, the plan's strip depths: a building of the shallowest stands on any strip, of the middle on a
        // street's or an avenue's, of the deepest on an avenue's or the first block's two deep ones.
        public const int ShallowDepth = 32;
        public const int StreetDepth = 35;
        public const int AvenueDepth = 39;

        /// <summary>
        /// Every size and version the grid takes, in the game's order. Out of it: the boat's and parking's (no frontage of a
        /// building), the Hamptons' T (no building of the city's streets), the versions kept for the game's special services
        /// (D1, D3, E1, G1, I4, O1), and the warehouses deeper than any strip (P2 39.6 m, P3 48.3, Q2 71.0, Q3 50.8; Q1 has no
        /// building of the city). The theatres and cinemas wear the game's one theatre front that can be copied (R1's).
        /// </summary>
        public static readonly SpaceSize[] Grid =
        {
            new SpaceSize('a', 1, 75, 6.3f, 22.5f, 5f, 15f, LotClass.Small, 41),
            new SpaceSize('a', 2, 75, 10.0f, 25.4f, 8f, 15f, LotClass.Small, 28),
            new SpaceSize('a', 3, 75, 10.0f, 24.3f, 5f, 15f, LotClass.Small, 11),
            new SpaceSize('b', 1, 54, 10.0f, 24.2f, 9f, 10f, LotClass.Small, 119),
            new SpaceSize('c', 1, 225, 16.2f, 24.4f, 15f, 15f, LotClass.Small, 50),
            new SpaceSize('c', 2, 225, 16.1f, 22.3f, 15f, 15f, LotClass.Small, 40),
            new SpaceSize('d', 2, 285, 21.6f, 26.9f, 23f, 15f, LotClass.Office, 24),
            new SpaceSize('f', 1, 96, 15.1f, 24.2f, 12f, 12f, LotClass.Small, 69),
            new SpaceSize('h', 1, 690, 28.3f, 29.8f, 23f, 37f, LotClass.Warehouse, 13),
            new SpaceSize('h', 2, 690, 16.1f, 15.3f, 30f, 37f, LotClass.Warehouse, 1),
            new SpaceSize('h', 3, 690, 24.9f, 34.1f, 44f, 44f, LotClass.Warehouse, 2),
            new SpaceSize('i', 1, 1292, 24.1f, 36.6f, 37f, 44f, LotClass.Warehouse, 7),
            new SpaceSize('i', 2, 1292, 37.9f, 29.5f, 37f, 37f, LotClass.Warehouse, 2),
            new SpaceSize('i', 3, 1292, 30.0f, 29.4f, 37f, 51f, LotClass.Warehouse, 7),
            new SpaceSize('j', 1, 285, 29.9f, 29.8f, 24f, 15f, LotClass.Office, 15),
            new SpaceSize('k', 1, 660, 29.5f, 28.4f, 30f, 22f, LotClass.Office, 22),
            new SpaceSize('l', 1, 204, 23.1f, 29.7f, 17f, 12f, LotClass.Big, 23),
            new SpaceSize('m', 1, 1000, 32.4f, 26.1f, 29f, 39f, LotClass.Big, 25),
            new SpaceSize('n', 1, 589, 19.7f, 27.8f, 19f, 35f, LotClass.Big, 17),
            new SpaceSize('p', 1, 2184, 43.2f, 36.8f, 44f, 58f, LotClass.Warehouse, 1),
            new SpaceSize('r', 1, 2000, 32.7f, 25.5f, 28f, 56f, LotClass.Theatre, 1),
            new SpaceSize('r', 2, 2000, 32.7f, 25.5f, 28f, 51f, LotClass.Theatre, 1),
            new SpaceSize('r', 3, 2000, 32.7f, 25.5f, 28f, 46f, LotClass.Theatre, 1),
            new SpaceSize('s', 1, 2000, 32.7f, 25.5f, 35f, 39f, LotClass.Cinema, 1),
            new SpaceSize('s', 2, 2000, 32.7f, 25.5f, 35f, 39f, LotClass.Cinema, 1),
            new SpaceSize('s', 3, 2000, 32.7f, 25.5f, 35f, 39f, LotClass.Cinema, 1),
        };

        /// <summary>
        /// The ways in a size's interior has (BuildingSizeData.numberOfEntrances, Build 3682): two for M, R and S, one for
        /// the rest. A building wears a look of another size only with as many doors.
        /// </summary>
        public static int Doors(string size)
        {
            return size == "ba:buildingsize_m" || size == "ba:buildingsize_r" || size == "ba:buildingsize_s" ? 2 : 1;
        }

        /// <summary>Whole spaces a frontage takes: rounded up, never fewer than one.</summary>
        public static int SpacesFor(float front)
        {
            return Math.Max(1, (int)Math.Ceiling((front - 0.001f) / SpaceWidth));
        }

        /// <summary>The shallowest depth class that holds a building this deep (m); past the deepest, a depth no strip has.</summary>
        public static int DepthClassFor(float deep)
        {
            return deep <= ShallowDepth ? ShallowDepth : deep <= StreetDepth ? StreetDepth : deep <= AvenueDepth ? AvenueDepth : (int)Math.Ceiling(deep);
        }

        /// <summary>The grid's entry for a size and version (a version below 1: the size's first), or null for one out of the grid.</summary>
        public static SpaceSize Of(string size, int version)
        {
            for (int i = 0; size != null && i < Grid.Length; i++)
            {
                if (string.Equals(Grid[i].Size, size, StringComparison.Ordinal) && (version < 1 || Grid[i].Version == version))
                {
                    return Grid[i];
                }
            }
            return null;
        }

        /// <summary>Spaces a size and version takes, or 0 for one out of the grid.</summary>
        public static int SpacesOf(string size, int version)
        {
            SpaceSize s = Of(size, version);
            return s != null ? s.Spaces : 0;
        }

        /// <summary>
        /// True when a site of <paramref name="siteSpaces"/> joined spaces offers a building of a size taking
        /// <paramref name="sizeSpaces"/>: one of exactly the site's spaces, or of fewer when the design chosen fills the
        /// site's frontage alone (<paramref name="fills"/>: within <see cref="FillSliver"/>), so a site is never left with
        /// ground inside it beside its building.
        /// </summary>
        public static bool Offered(int siteSpaces, int sizeSpaces, bool fills)
        {
            return sizeSpaces > 0 && siteSpaces > 0 && (sizeSpaces == siteSpaces || (sizeSpaces < siteSpaces && fills));
        }

        /// <summary>True when a design's frontage fills ground this wide: no wider, and no more than <see cref="FillSliver"/> short.</summary>
        public static bool Fills(float front, float width)
        {
            return front <= width + 0.001f && front >= width - FillSliver;
        }

        /// <summary>
        /// A strip's spaces along a frontage of <paramref name="length"/> m, into <paramref name="into"/> (cleared): as many
        /// of <see cref="SpaceWidth"/> as it holds, the whole metres over given one each to its spaces from its two ends in
        /// turn (the corners first), so every space is the standard or a metre wider. Returns the part of a metre left, shared
        /// at the strip's two ends.
        /// </summary>
        public static float Cut(float length, List<int> into)
        {
            into.Clear();
            int n = (int)Math.Floor((length + 0.01f) / SpaceWidth);
            if (n <= 0)
            {
                return Math.Max(0f, length);
            }
            for (int i = 0; i < n; i++)
            {
                into.Add(SpaceWidth);
            }
            int spare = Math.Max(0, (int)Math.Floor(length - n * SpaceWidth + 0.01f));   // a strip of 70 m is 69.99999 in floats
            for (int k = 0; k < spare; k++)
            {
                int at = k % 2 == 0 ? k / 2 : n - 1 - k / 2;
                into[((at % n) + n) % n]++;
            }
            return Math.Max(0f, length - n * SpaceWidth - spare);
        }
    }
}
