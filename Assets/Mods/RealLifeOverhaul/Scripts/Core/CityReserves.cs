using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's buildings the mod keeps fixed, read from the game's own data (its Buildings definitions, its rivals'
    /// settings and StreamingAssets/BusinessLayouts), game-free and checked offline (EconomyCheck, CityReserveChecks.cs).
    ///
    /// THE PLAYER'S OFFICES. The two largest office buildings a player can rent or buy, 17 7th Street (102,502 sqm, Murray
    /// Hill) and 12 Broadway (58,989 sqm, Lower Manhattan), are reserved for the player from the start (Wiring/CoreLinks
    /// reserves them in the book of the mod's buildings, Core/SpecialNameBook): no feature of the mod ever chooses them (the
    /// police stations, the tech towers, City Utilities, the courthouse's fallback, the university, the offices made of shops, the
    /// brands' franchise sites), and they are not refused to the player. The game's own rivals and city businesses can
    /// still open in them (both are size K version 1, one of the rivals' office sizes, with the game's office layouts):
    /// that is the game's, and the mod does not stop it.
    ///
    /// CITY UTILITIES (the owner, 15 Oct 2026: "Just move the City Utilities"). At 13 7th Street, Midtown, the same in
    /// every game, its desk at the door. By the game's data it is an ordinary residential building of size F version 1,
    /// 6,980 sqm, traffic 40, with no special service: none of the game's own buildings (no game business, no game jobs).
    /// No business of any kind, the city's or a rival's, is ever opened in a residential building: the generator only
    /// marks homes to let or not, the rivals hold offices, shops, cinemas, theatres and warehouses, and no AI business has
    /// a residential layout. The game itself may let it as a home or put it up for sale (as it may any residential
    /// building); the mod reserves it and refuses it to the player with its other buildings, so nobody rents or buys it
    /// while the mod runs. In an older game where a player already rents or owns it, the office stands meanwhile in the
    /// smallest other free residential building of Midtown, never the player's, and moves back for good once it is free.
    /// Before, it took the city's largest office building, 17 7th Street, which is now kept for the player. The courthouse
    /// stays where it always was, the game's tax office at 70 5th Avenue.
    ///
    /// MOVES. City Utilities moved since a save was made is told once: the save keeps where it was
    /// (<see cref="TellMoved"/>); a save from before it kept one had the old rule's building, 17 7th Street.
    /// </summary>
    public static class CityReserves
    {
        public const string PlayerOfficeId = "player_office_";   // + 1 or 2: the reservations of the player's two offices

        /// <summary>The player's two offices, largest first: street ids and numbers.</summary>
        public static readonly string[] PlayerOfficeStreets = { "ba:street_seventhstreet", "ba:street_broadwaystreet" };
        public static readonly int[] PlayerOfficeNumbers = { 17, 12 };
        public static readonly int[] PlayerOfficeSqm = { 102502, 58989 };   // the game's data: to say in the log if the city changes
        public static readonly string[] PlayerOfficeStreetNames = { "7th Street", "Broadway" };   // the game's own street names, for a page made where the game's streets are not loaded

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
        public const string UtilitiesStreetName = "7th Street";     // the game's own street name, for a page made where the game's streets are not loaded

        public const string OldUtilities = "ba:street_seventhstreet|17";  // the largest office: where every save before this had City Utilities
        public const string UtilitiesKey = "reallife:place_utilities";    // "street|number" of City Utilities, so that a move is told once

        /// <summary>True for one of the player's two offices.</summary>
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

        /// <summary>True for City Utilities' building, 13 7th Street.</summary>
        public static bool IsUtilities(string street, int number) => number == UtilitiesNumber && string.Equals(street, UtilitiesStreet, StringComparison.Ordinal);

        /// <summary>
        /// A building may hold City Utilities: an ordinary residential building (never one of the game's own special
        /// services), that no player rents or owns, with nothing of another feature's in it.
        /// </summary>
        public static bool HostServes(string buildingType, bool specialService, bool players, bool heldByOther)
        {
            return buildingType == Residential && !specialService && !players && !heldByOther;
        }

        /// <summary>City Utilities' address as the game writes one ("13 7th Street"), from the street's name.</summary>
        public static string UtilitiesAddress(string streetName) => UtilitiesNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + (string.IsNullOrEmpty(streetName) ? UtilitiesStreetName : streetName);

        /// <summary>
        /// The smallest of the candidates <paramref name="fits"/> allows, by floor, ties broken by address ("street#number"
        /// in ordinal order), leaving out what <paramref name="taken"/> holds: the building meanwhile while 13 7th Street is
        /// a player's. The same city gives the same pick on every machine. -1 for none.
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

        /// <summary>
        /// The two largest of the candidates <paramref name="fits"/> says a player could rent or buy (offices of the city's
        /// own: no special service), by floor, ties broken by address, as SpecialNameBook.PickLargest: the indexes, -1 for
        /// none. For the check that the game's city still has the two kept for the player where they were.
        /// </summary>
        public static void TopTwo(int count, Func<int, bool> fits, Func<int, int> sqm, Func<int, string> street, Func<int, int> number, out int first, out int second)
        {
            first = SpecialNameBook.PickLargest(count, fits, sqm, street, number, null);
            int one = first;
            second = SpecialNameBook.PickLargest(count, i => i != one && (fits == null || fits(i)), sqm, street, number, null);
        }

        /// <summary>
        /// True if a place's move is told: where it was (the save's <paramref name="kept"/>, or for a save from before it
        /// kept one in which the feature ran, <paramref name="oldRule"/>) is somewhere else than now. A new game is told nothing.
        /// </summary>
        public static bool TellMoved(string kept, string now, bool featureRanBefore, string oldRule)
        {
            string was = !string.IsNullOrEmpty(kept) ? kept : featureRanBefore ? oldRule : null;
            return !string.IsNullOrEmpty(was) && !string.IsNullOrEmpty(now) && !string.Equals(was, now, StringComparison.Ordinal);
        }

        /// <summary>A place as a save keeps it.</summary>
        public static string Mark(string street, int number) => (street ?? string.Empty) + "|" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
