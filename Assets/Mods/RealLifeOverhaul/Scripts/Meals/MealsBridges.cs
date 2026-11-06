using System;

namespace RealLifeOverhaul.Meals
{
    /// <summary>
    /// What meals asks of the rest of the mod (Wiring/MealsLinks fills each in): home cooking's portions packed to take,
    /// eaten at the seat the player sits on. Each is null while home life is not there, and then nothing packed is offered.
    /// </summary>
    public static class MealsBridges
    {
        /// <summary>Home portions packed to take and carried (Home/HomeTakeout.Carried). Default: none.</summary>
        public static Func<int> Carried;

        /// <summary>The name key of what is packed (Home/HomeTakeout.NameKey).</summary>
        public static Func<string> CarriedName;

        /// <summary>
        /// One packed portion eaten, filling once with home cooking's own fill and raising the game's one "food eaten" event;
        /// false, nothing taken, when the player is too full or nothing is carried (Home/HomeTakeout.EatOne).
        /// </summary>
        public static Func<bool> EatCarried;
    }
}
