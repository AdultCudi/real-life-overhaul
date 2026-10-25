using System;
using System.Collections.Generic;
using BAModAPI;
using Buildings;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A building registration's building, district and type, read so that one whose building the city does not have gives
    /// nothing instead of throwing. The game's own BuildingRegistration.Neighborhood, GetBuildingType and Alerts read
    /// BuildingCached without a test, and a save can hold a registration whose street and number match no building (a
    /// player's save left one by another mod: the game's own ItemHelper.GetLowestMarketPrice threw on it 857 times, and the
    /// tech sector's hiring switched itself off on it at every load). Every walker of the save's registrations asks here
    /// first and skips such a one; nothing in the save is changed or mended, so the game and the other mod see it as before.
    /// Such a registration is named once a session in a warning, by its street and number.
    /// COST: a field read and a null test for a registration whose building is found; for one that is not, the game's own
    /// address lookup (BuildingCached tries again at every read, as the game does) and a set test.
    /// </summary>
    internal static class GameRegistrations
    {
        public static IModLogger Logger;

        private static readonly HashSet<string> Told = new HashSet<string>(StringComparer.Ordinal);   // addresses already named this session

        /// <summary>The registration's building; null, with the one warning, if the city has none at its address.</summary>
        public static Building BuildingOf(BuildingRegistration reg)
        {
            if (reg == null)
            {
                return null;
            }
            Building building = reg.BuildingCached;
            if (building == null)
            {
                Tell(reg);
            }
            return building;
        }

        /// <summary>True if the registration's building is found, so its district, address and type can be read.</summary>
        public static bool Found(BuildingRegistration reg) => BuildingOf(reg) != null;

        /// <summary>The district the registration's building stands in, as the game names it; null if its building is not found.</summary>
        public static string District(BuildingRegistration reg)
        {
            Building building = BuildingOf(reg);
            return building != null ? building.Neighbourhood : null;
        }

        /// <summary>The type of the registration's building (ba:buildingtype_...); null if its building is not found.</summary>
        public static string BuildingType(BuildingRegistration reg)
        {
            Building building = BuildingOf(reg);
            return building != null ? building.BuildingType : null;
        }

        /// <summary>The game's to-do alerts at the registration; none if its building is not found.</summary>
        public static int Alerts(BuildingRegistration reg) => Found(reg) ? reg.Alerts : 0;

        /// <summary>For the test entry: whether the registration was named in this session's warning.</summary>
        public static bool WasTold(BuildingRegistration reg) => reg != null && Told.Contains(Where(reg));

        private static void Tell(BuildingRegistration reg)
        {
            if (!reg.HasValidAddress)
            {
                return;   // a registration with no street is one the game itself allows for (HasValidAddress): no address to name, nothing lost
            }
            string where = Where(reg);
            if (Told.Add(where))
            {
                Logger?.Warn("Buildings: the save holds a registration at '" + where + "' whose building the city does not have (likely left by another mod"
                             + " or an older game); the mod leaves it out wherever it reads the city's businesses and changes nothing of it.");
            }
        }

        private static string Where(BuildingRegistration reg) => reg.StreetNumber + " " + reg.StreetName;
    }
}
