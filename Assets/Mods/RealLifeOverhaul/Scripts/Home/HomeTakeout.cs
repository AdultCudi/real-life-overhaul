using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Home cooking's portions packed to take (<see cref="HomeState.Takeout"/>), as Meals asks for them through its bridge
    /// (Wiring/MealsLinks): how many are carried, what they are, and one eaten at the seat the player sits on. The portion
    /// fills here, once, with home cooking's own fill, exactly as a dish eaten at the cooker does. Nothing runs per frame.
    /// </summary>
    public static class HomeTakeout
    {
        private const string TooFull = "itemhelper_notification_you_are_to_stuffed_to_eat_this";   // the game's own refusal (ItemHelper.TryToConsume)
        private const string TooFullId = "too_stuffed_to_eat";

        /// <summary>Portions packed and carried; 0 before the save is read.</summary>
        public static int Carried()
        {
            return HomeState.Loaded && HomeState.Takeout > 0 ? HomeState.Takeout : 0;
        }

        /// <summary>The name key of what is packed: the dish, or "leftovers" when it is not known.</summary>
        public static string NameKey()
        {
            Dish dish = HomeState.TakeoutDish > 0 ? Cooking.DishByNumber(HomeState.TakeoutDish) : null;
            return dish != null ? dish.Name : "reallife_home_dish_leftovers";
        }

        /// <summary>
        /// One packed portion eaten: false, nothing taken, while the player is too full (the game's own notice) or none is
        /// carried. The game's "food eaten" event comes back to the home director as a home-cooked meal.
        /// </summary>
        public static bool EatOne()
        {
            GameManager game = InstanceBehavior<GameManager>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (!HomeState.Loaded || HomeState.Takeout <= 0 || game == null || save == null)
            {
                return false;
            }
            if (Mathf.RoundToInt(save.Hunger) >= 100)
            {
                Notifications.ShowError(TooFull, TooFullId);
                return false;
            }
            int fill = HomeState.TakeOne();
            HomeDirector host = HomeDirector.Instance;
            if (host != null)
            {
                host.OwnMeal = Meal.Home;
            }
            game.ChangeHunger(fill);
            HomeDirector.Logger?.Info("Home: a packed portion eaten at a seat, filled " + fill + ", " + HomeState.Takeout + " still packed.");
            return true;
        }
    }
}
