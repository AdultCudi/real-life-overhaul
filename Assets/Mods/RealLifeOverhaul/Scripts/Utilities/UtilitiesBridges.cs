using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// What the utilities read from the rest of the mod, set by Wiring/UtilityLinks. Each is safe to leave null: the
    /// utilities then act as if that feature were off (a fair credit grade, nobody else at home, no business closed).
    /// </summary>
    public static class UtilitiesBridges
    {
        /// <summary>The player's credit grade (Economy.CreditGrade as a number, very poor 0 to excellent 5).</summary>
        public static Func<int> Grade;

        /// <summary>The people living at a home of the player's besides the player: a partner who moved in, children at home, a roommate.</summary>
        public static Func<BuildingRegistration, int> Household;

        /// <summary>A business of the player's closed for some hours with the game's own switch (Business); false if it was closed already.</summary>
        public static Func<BuildingRegistration, int, bool> CloseFor;

        /// <summary>A line in the Business desk's messages (Business), to read back: why a business is closed. Its key and its names.</summary>
        public static Action<string, Dictionary<string, string>> Record;

        /// <summary>True for a building another feature has made its own (the courthouse), which the office is not to be.</summary>
        public static Func<CityBuildingController, bool> Taken;

        /// <summary>The credit grade, fair while nothing says otherwise.</summary>
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
            Record = null;
            Taken = null;
        }
    }
}
