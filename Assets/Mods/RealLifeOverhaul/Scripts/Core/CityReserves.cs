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
    /// police stations, the tech towers, City Utilities, the courthouse, the university, the offices made of shops, the
    /// brands' franchise sites), and they are not refused to the player. The game's own rivals and city businesses can
    /// still open in them (both are size K version 1, one of the rivals' office sizes, with the game's office layouts):
    /// that is the game's, and the mod does not stop it.
    ///
    /// THE COURTHOUSE. Only a sign, a door and a name, so it needs no office at all: the game's own moving company, 15 12th
    /// Street (2,623 sqm, Lower Manhattan, the financial and legal quarter), a building of the game's special services that
    /// is never to let, never for sale and never a rival's or a city business's, the same in every game. No feature of the
    /// mod uses that business's building (no career, polling place, licence desk or study is held there; movers are hired
    /// by phone). Before, it took the game's tax office, 70 5th Avenue, the largest building in the city.
    ///
    /// MOVES. A place the mod moved since a save was made (the courthouse, City Utilities) is told once: the save keeps
    /// where it was (<see cref="TellMoved"/>); a save from before it kept one had the old rule's building.
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

        public const string CourtStreet = "ba:street_twelfthstreet";
        public const int CourtNumber = 15;
        public const string CourtService = "ba:businesstype_movingservice";   // the game's special service of that building

        public const string OldCourt = "ba:street_fifthavenue|70";        // the tax office: where every save before this had the courthouse
        public const string OldUtilities = "ba:street_seventhstreet|17";  // the largest office: where every save before this had City Utilities

        public const string CourtKey = "reallife:place_courthouse";       // "street|number" of the courthouse, so that a move is told once
        public const string UtilitiesKey = "reallife:place_utilities";    // ...of City Utilities

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

        /// <summary>True for the courthouse's building.</summary>
        public static bool IsCourt(string street, int number) => number == CourtNumber && string.Equals(street, CourtStreet, StringComparison.Ordinal);

        /// <summary>
        /// The courthouse's building may serve: it is the game's moving company as chosen (its special service), and no
        /// player rents or owns it (a special service never is; a changed city could make it so).
        /// </summary>
        public static bool CourtServes(string serviceType, bool players) => !players && serviceType == CourtService;

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
