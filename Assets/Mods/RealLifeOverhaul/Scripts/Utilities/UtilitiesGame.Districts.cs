using System.Collections.Generic;
using Helpers;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>The utilities' reading of the city's districts for the player's own network (BroadbandCompany.Discover). Once a morning; nothing per frame.</summary>
    public static partial class UtilitiesGame
    {
        /// <summary>
        /// The districts of the loaded New York as the game lists them (the seven the mod knows first, the mod's own, then any a free update adds), into
        /// <paramref name="into"/>; false while the game lists none. A district of the game's data that is in another city is left out (CityCheck.Here).
        /// </summary>
        public static bool CityDistricts(List<string> into)
        {
            CityCheck.NewYorkDistricts(NeighborhoodHelper.Neighborhoods, into);
            return into.Count > 0;
        }
    }
}
