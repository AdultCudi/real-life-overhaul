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

        /// <summary>
        /// A business of the player's kept closed for at least some hours with the game's own switch, as the utilities'
        /// own closure (Business): closed, or one of the mod's closures already on it made to last. False if it is closed
        /// by someone else, or cannot be kept so.
        /// </summary>
        public static Func<BuildingRegistration, int, bool> CloseFor;

        /// <summary>A business that has what it lacked again: the utilities' closure on it ended now (Business). Another feature's may still keep it closed.</summary>
        public static Action<BuildingRegistration> Release;

        /// <summary>A line in the Business desk's messages (Business), to read back: why a business is closed. Its key and its names.</summary>
        public static Action<string, Dictionary<string, string>> Record;

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
            Release = null;
            Record = null;
        }
    }
}
