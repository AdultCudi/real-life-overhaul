using System;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// One of Uptown's streets: an id of the mod's own (never one of the game's), its name in the texts, the short name
    /// the game's help system shows, whether it is an avenue (west to east) or a street (south to north), its place in the
    /// grid, and its first house number. A street that continues one of Manhattan's across 1st Avenue shows that street's
    /// name, with numbers from 1001, which the game's (at most 82 on any street) never reach.
    /// </summary>
    public sealed class LayoutStreet
    {
        public readonly string Id;            // "reallife:street_uptown_<name>", under the mod's own street prefix
        public readonly string NameKey;       // its name in the texts, under the prefix the mod's street names share
        public readonly string Abbreviation;  // three letters
        public readonly bool Avenue;          // an avenue runs west to east, a street south to north
        public readonly int Index;            // a street's place west to east (Manhattan's 9th Street is 0); an avenue's row north of 1st Avenue (1 to 3)
        public readonly int FirstNumber;      // its first house number: 1001 on a street that continues one of Manhattan's, 1 otherwise
        public readonly string Continues;     // the game's street it continues across 1st Avenue ("ba:street_secondstreet"), or null

        public LayoutStreet(string name, string abbreviation, bool avenue, int index, string continues)
        {
            Id = LayoutStreets.Prefix + name;
            NameKey = LayoutStreets.KeyPrefix + name;
            Abbreviation = abbreviation;
            Avenue = avenue;
            Index = index;
            Continues = continues;
            FirstNumber = continues != null ? LayoutStreets.ContinuedFirst : 1;
        }
    }

    /// <summary>
    /// Uptown's streets, the whole band's: nineteen streets on the game's own grid (Manhattan's nine continued north, five
    /// west of them and five east) and three avenues north of 1st Avenue. They are the game's StreetData at every load
    /// through the mod's own streets (NewBuildings' MadeStreets), so a made building there has an address the game writes
    /// and finds, and the district save names every one. Fixed data: the same on every machine and in every version, since
    /// saves keep records by address. No Unity or game types.
    /// </summary>
    public static class LayoutStreets
    {
        private const string Mod = "reallife";                                // apart, so that the text checks do not take the names' prefix for a key
        public const string Prefix = "reallife:street_uptown_";               // under the mod's own street prefix ("reallife:street_")
        public const string KeyPrefix = Mod + "_newbuildings_street_uptown_"; // the mod's street names share the new buildings' key prefix
        public const int ContinuedFirst = 1001;   // the first number on a continued street: the game's highest on any street is 82
        public const int WestMost = -5;           // the westmost street's index: five west of 9th Street
        public const int EastMost = 13;           // the eastmost: five east of 1st Street
        public const int Avenues = 3;             // Uptown's avenues north of 1st Avenue

        /// <summary>Every street of the band, the streets west to east, then the avenues south to north.</summary>
        public static readonly LayoutStreet[] All =
        {
            new LayoutStreet("willow", "UWI", false, -5, null),
            new LayoutStreet("birch", "UBI", false, -4, null),
            new LayoutStreet("elm", "UEL", false, -3, null),
            new LayoutStreet("maple", "UMA", false, -2, null),
            new LayoutStreet("cedar", "UCE", false, -1, null),
            new LayoutStreet("ninth", "U9S", false, 0, "ba:street_ninthstreet"),
            new LayoutStreet("eighth", "U8S", false, 1, "ba:street_eighthstreet"),
            new LayoutStreet("seventh", "U7S", false, 2, "ba:street_seventhstreet"),
            new LayoutStreet("sixth", "U6S", false, 3, "ba:street_sixthstreet"),
            new LayoutStreet("fifth", "U5S", false, 4, "ba:street_fifthstreet"),
            new LayoutStreet("fourth", "U4S", false, 5, "ba:street_fourthstreet"),
            new LayoutStreet("third", "U3S", false, 6, "ba:street_thirdstreet"),
            new LayoutStreet("second", "U2S", false, 7, "ba:street_secondstreet"),
            new LayoutStreet("first", "U1S", false, 8, "ba:street_firststreet"),
            new LayoutStreet("hill", "UHI", false, 9, null),
            new LayoutStreet("ridge", "URI", false, 10, null),
            new LayoutStreet("grove", "UGR", false, 11, null),
            new LayoutStreet("park", "UPA", false, 12, null),
            new LayoutStreet("lake", "ULA", false, 13, null),
            new LayoutStreet("uptownavenue", "UAV", true, 1, null),
            new LayoutStreet("summitavenue", "USU", true, 2, null),
            new LayoutStreet("crestavenue", "UCR", true, 3, null),
        };

        /// <summary>The street at a place west to east (9th Street is 0), or null outside the band.</summary>
        public static LayoutStreet Street(int index)
        {
            int at = index - WestMost;
            return at >= 0 && index <= EastMost ? All[at] : null;
        }

        /// <summary>The avenue of a row north of 1st Avenue (1 to 3), or null.</summary>
        public static LayoutStreet Avenue(int row)
        {
            return row >= 1 && row <= Avenues ? All[EastMost - WestMost + row] : null;
        }

        /// <summary>The street of that id, or null. A string comparison each.</summary>
        public static LayoutStreet Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.Ordinal))
                {
                    return All[i];
                }
            }
            return null;
        }
    }
}
