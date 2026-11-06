using RealLifeOverhaul.Meals;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Meals and music with home cooking (Home/HomeBridges):
        ///
        ///   ServedAtTable   a dish cooked at home is ready: meals finds a free seat in the room and the plate is carried
        ///                   there and eaten (Meals/MealsDirector.ServeCooked); no seat, or the option off, and it is
        ///                   eaten at the cooker as before. The food, its fill and its leftovers stay the cooking's own.
        ///   Carried, CarriedName, EatCarried
        ///                   home portions packed to take, offered as Eat in the game's rest panel once seated
        ///                   (Meals/TakeoutButton); one eaten fills with home cooking's own fill (Home/HomeTakeout).
        ///
        /// Called once, as a dish is served; a fault is said once (<see cref="Fault"/>) and the dish is eaten at the cooker.
        /// </summary>
        public static class MealsLinks
        {
            internal static void Connect()
            {
                HomeBridges.ServedAtTable = ServedAtTable;
                MealsBridges.Carried = HomeTakeout.Carried;
                MealsBridges.CarriedName = HomeTakeout.NameKey;
                MealsBridges.EatCarried = HomeTakeout.EatOne;
            }

            internal static void Disconnect()
            {
                HomeBridges.ServedAtTable = null;
                MealsBridges.Carried = null;
                MealsBridges.CarriedName = null;
                MealsBridges.EatCarried = null;
            }

            private static bool ServedAtTable(int fill)
            {
                try
                {
                    MealsDirector meals = MealsDirector.Instance;
                    return meals != null && meals.ServeCooked(fill);
                }
                catch (System.Exception e)
                {
                    Fault("MealsLinks.ServedAtTable", e);
                    return false;
                }
            }
        }
    }
}
