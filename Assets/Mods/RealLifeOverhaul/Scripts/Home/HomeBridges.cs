using System;

namespace RealLifeOverhaul
{
    /// <summary>What home and health ask of the rest of the mod (Wiring/LifeLinks fills each in, and Wiring/MealsLinks <see cref="ServedAtTable"/>).</summary>
    public static class HomeBridges
    {
        /// <summary>
        /// Whether the player is under a roof the game does not know of: up in a hotel room, where the player stands at the
        /// hotel's door in the street while the room's list is open. Neither rain nor the street reaches them there.
        /// Default: no.
        /// </summary>
        public static Func<bool> Sheltered;

        /// <summary>
        /// The quality of the bed (0 to 100) the player sleeps in away from the game's own beds, such as a hotel room's
        /// through its time skip, or -1 when not. While it is 0 or more the hours count as sleep. Default: never.
        /// </summary>
        public static Func<int> SleepingAway;

        /// <summary>
        /// A dish cooked at home is ready and about to be eaten (its fill, of the game's 100): true when it is carried to a
        /// free seat and eaten there, so the cooker's bite is left out. The meal's effect is the cooking's own either way.
        /// Default: eaten at the cooker.
        /// </summary>
        public static Func<int, bool> ServedAtTable;

        /// <summary>
        /// An illness's chance drawn for a slot (which chance it is) from the save's seed and the day, 0 to just under 1, so the same day gives the same answer after
        /// every load (Health, through Wiring/Wiring.HealthLinks). Default: the game's own random number, as before.
        /// </summary>
        public static Func<int, float> Roll;

        /// <summary>True while an illness that has turned serious is held on: it does not clear up by itself until the hospital has seen it (Health). Default: never.</summary>
        public static Func<bool> Held;

        /// <summary>True while something Home does not hold rules out hard exercise: a serious case, a broken arm (Health). Default: nothing.</summary>
        public static Func<bool> TooIll;

        /// <summary>A line for beside the needs' bars that comes before Home's own: an injury, a serious case (Health). The same string while nothing changes. Default: none.</summary>
        public static Func<string> StatusNote;
    }
}
