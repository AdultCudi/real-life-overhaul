using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>What the utilities read from the rest of the mod (Wiring/UtilityLinks); each left null acts as that feature off.</summary>
    public static class UtilitiesBridges
    {
        /// <summary>The player's credit grade, very poor 0 to excellent 5.</summary>
        public static Func<int> Grade;

        /// <summary>The people living at a home of the player's besides the player.</summary>
        public static Func<BuildingRegistration, int> Household;

        /// <summary>Keeps a business closed some hours as the utilities' closure; false if someone else closed it or it cannot be kept.</summary>
        public static Func<BuildingRegistration, int, bool> CloseFor;

        /// <summary>A business has what it lacked again: the utilities' closure on it ends (another may still hold it).</summary>
        public static Action<BuildingRegistration> Release;

        /// <summary>A line in the Business desk's messages saying why a business is closed: its key and names.</summary>
        public static Action<string, Dictionary<string, string>> Record;

        /// <summary>Thousandths of a day's work the tech firm at a place would get at a speed (Mbps); -1 for a place with no tech firm.</summary>
        public static Func<BuildingRegistration, int, int> FirmWork;

        /// <summary>The orders a day the online shop at a place could take at a speed (Mbps); -1 for a place with no online shop.</summary>
        public static Func<BuildingRegistration, int, int> ShopMost;

        /// <summary>Fills in the buildings of the player's online shops; false while the online shops are not running.</summary>
        public static Func<List<BuildingRegistration>, bool> Shops;

        /// <summary>The credit grade, fair (2) while nothing says otherwise.</summary>
        public static int GradeNow
        {
            get
            {
                Func<int> grade = Grade;
                return grade != null ? grade() : 2;
            }
        }

        public static void Reset()
        {
            Grade = null;
            Household = null;
            CloseFor = null;
            Release = null;
            Record = null;
            FirmWork = null;
            ShopMost = null;
            Shops = null;
        }
    }
}
