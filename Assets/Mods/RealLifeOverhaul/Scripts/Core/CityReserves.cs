using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's buildings the mod keeps fixed, from the game's own data; game-free, checked by EconomyCheck.
    /// The player's offices: the two largest office buildings a player can rent (17 7th Street, 12 Broadway), which no
    /// feature of the mod ever chooses; the game's rivals may still open in them. City Utilities: 13 7th Street, an
    /// ordinary residential building no game business can use; while a player holds it, the office stands in the
    /// smallest other free residential building of Midtown. A move since the save was made is told once
    /// (<see cref="TellMoved"/>).
    /// </summary>
    public static class CityReserves
    {
        public const string PlayerOfficeId = "player_office_";   // + 1 or 2: the reservations of the player's two offices

        /// <summary>The player's two offices, largest first: street ids and numbers.</summary>
        public static readonly string[] PlayerOfficeStreets = { "ba:street_seventhstreet", "ba:street_broadwaystreet" };
        public static readonly int[] PlayerOfficeNumbers = { 17, 12 };
        public static readonly string[] PlayerOfficeStreetNames = { "7th Street", "Broadway" };   // the game's own names, for when its streets are not loaded

        /// <summary>One of the player's offices as the game writes an address ("17 7th Street"), from the street's name.</summary>
        public static string PlayerOfficeAddress(int i, string streetName)
        {
            return PlayerOfficeNumbers[i].ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + (string.IsNullOrEmpty(streetName) ? PlayerOfficeStreetNames[i] : streetName);
        }

        public const string Residential = "ba:buildingtype_residential";
        public const string UtilitiesDistrict = "ba:neighborhood_midtown";   // where the office stands meanwhile, near 13 7th Street

        public const string UtilitiesId = "utilities_office";       // the book's id of City Utilities' building, and its reservation
        public const string UtilitiesStreet = "ba:street_seventhstreet";
        public const int UtilitiesNumber = 13;
        public const string UtilitiesStreetName = "7th Street";     // the game's own name, for when its streets are not loaded

        public const string OldUtilities = "ba:street_seventhstreet|17";  // the largest office: where every save before this had City Utilities
        public const string UtilitiesKey = "reallife:place_utilities";    // "street|number" of City Utilities, so that a move is told once

        public static bool IsPlayerOffice(string street, int number)
        {
            for (int i = 0; i < PlayerOfficeStreets.Length; i++)
            {
                if (PlayerOfficeNumbers[i] == number && string.Equals(PlayerOfficeStreets[i], street, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsUtilities(string street, int number) => number == UtilitiesNumber && string.Equals(street, UtilitiesStreet, StringComparison.Ordinal);

        /// <summary>True if a building may hold City Utilities: residential, no special service, nobody's, no other feature's.</summary>
        public static bool HostServes(string buildingType, bool specialService, bool players, bool heldByOther)
        {
            return buildingType == Residential && !specialService && !players && !heldByOther;
        }

        /// <summary>City Utilities' address as the game writes one ("13 7th Street"), from the street's name.</summary>
        public static string UtilitiesAddress(string streetName) => UtilitiesNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + (string.IsNullOrEmpty(streetName) ? UtilitiesStreetName : streetName);

        /// <summary>
        /// The smallest candidate by floor, ties broken by address in ordinal order, so every machine picks the same;
        /// -1 for none.
        /// </summary>
        public static int PickSmallest(int count, Func<int, bool> fits, Func<int, int> sqm, Func<int, string> street, Func<int, int> number, Func<string, int, bool> taken)
        {
            int best = -1, bestSqm = int.MaxValue;
            string bestName = null;
            for (int i = 0; i < count; i++)
            {
                if (fits != null && !fits(i))
                {
                    continue;
                }
                string s = street(i);
                int n = number(i);
                if (taken != null && taken(s, n))
                {
                    continue;
                }
                int size = Math.Max(0, Math.Min(999999, sqm(i)));
                string name = s + "#" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (size < bestSqm || (size == bestSqm && string.CompareOrdinal(name, bestName) < 0))
                {
                    best = i;
                    bestSqm = size;
                    bestName = name;
                }
            }
            return best;
        }

        /// <summary>The indexes of the two largest candidates, as SpecialNameBook.PickLargest picks; -1 for none.</summary>
        public static void TopTwo(int count, Func<int, bool> fits, Func<int, int> sqm, Func<int, string> street, Func<int, int> number, out int first, out int second)
        {
            first = SpecialNameBook.PickLargest(count, fits, sqm, street, number, null);
            int one = first;
            second = SpecialNameBook.PickLargest(count, i => i != one && (fits == null || fits(i)), sqm, street, number, null);
        }

        /// <summary>
        /// True if a place's move is told: where the save kept it (or <paramref name="oldRule"/> for an older save in
        /// which the feature ran) differs from now. A new game is told nothing.
        /// </summary>
        public static bool TellMoved(string kept, string now, bool featureRanBefore, string oldRule)
        {
            string was = !string.IsNullOrEmpty(kept) ? kept : featureRanBefore ? oldRule : null;
            return !string.IsNullOrEmpty(was) && !string.IsNullOrEmpty(now) && !string.Equals(was, now, StringComparison.Ordinal);
        }

        /// <summary>A place as a save keeps it: "street|number".</summary>
        public static string Mark(string street, int number) => (street ?? string.Empty) + "|" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
