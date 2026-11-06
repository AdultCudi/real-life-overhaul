using System.Collections.Generic;
using BigAmbitions.Items;
using Helpers;
using Localizor;
using PlayerActivity;
using PlayerActivity.Activities.Rest;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Meals
{
    /// <summary>
    /// Where takeout and plates touch the game, one small method per read: the game's own rest on a seat, the meal in the
    /// bag in the player's hand, and its one bite, taken exactly as the game's inventory takes it (ItemHelper.TryToConsume,
    /// then one less in the bag), so the hunger, the energy, the sound and the one "food eaten" event are the game's own.
    /// Nothing here runs per frame: each read answers a sit, an opening of the panel or a press.
    /// </summary>
    internal static class TakeoutGame
    {
        /// <summary>The game's activity panel, or null outside a city.</summary>
        public static PlayerActivityUI Panel()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null ? uis.playerActivityUI : null;
        }

        /// <summary>
        /// True while the local player rests on a seat through the game's own rest (clicked a chair or a bench and pressed
        /// Rest): its activity is in the panel, under way, and the character sits; <paramref name="place"/> is where.
        /// </summary>
        public static bool Resting(PlayerController player, out Transform place)
        {
            place = null;
            PlayerActivityUI panel = Panel();
            if (panel == null || player == null || player.Character == null || !(panel.GetCurrentActivity is RestActivity rest))
            {
                return false;
            }
            PlayerActivityState state = rest.GetState();
            if (state != PlayerActivityState.Started && state != PlayerActivityState.Running)
            {
                return false;
            }
            place = player.Character.isSittingOn;
            return place != null;
        }

        /// <summary>The first meal in the bag in the player's hand that the game's inventory would let them eat (<see cref="MealsRules.Eatable"/>), or null.</summary>
        public static CargoInstance BagMeal()
        {
            if (!PlayerHelper.IsHoldingItem || !PlayerHelper.IsHoldingBag)
            {
                return null;
            }
            ItemInstance bag = PlayerHelper.ItemInstanceInHands;
            List<CargoInstance> cargo = bag != null ? bag.cargoInstances : null;
            for (int i = 0; cargo != null && i < cargo.Count; i++)
            {
                CargoInstance food = cargo[i];
                Item item = food != null ? food.ItemCached : null;
                if (item != null && MealsRules.Eatable(food.paid, food.IsSealed, item.isConsumable, item.saturation))
                {
                    return food;
                }
            }
            return null;
        }

        /// <summary>The game's own name for what is in the bag.</summary>
        public static string Name(CargoInstance food)
        {
            return food != null ? food.itemName.GetLocalization() : string.Empty;
        }

        /// <summary>The fill of a meal in the bag, of the game's 100.</summary>
        public static float Fill(CargoInstance food)
        {
            Item item = food != null ? food.ItemCached : null;
            return item != null ? item.saturation : 0f;
        }

        /// <summary>
        /// One of the meal in the bag eaten, as the game's own inventory does it: its consume, then one less in the bag (the
        /// game itself drops an emptied paper bag). False, nothing changed, when the game refuses (too full, a bite already
        /// playing) or the meal is no longer in the bag in hand.
        /// </summary>
        public static bool EatFromBag(CargoInstance food)
        {
            ItemInstance bag = PlayerHelper.ItemInstanceInHands;
            if (food == null || bag == null || food.ItemCached == null || !bag.cargoInstances.Contains(food))
            {
                return false;
            }
            if (!food.ItemCached.TryToConsume())
            {
                return false;
            }
            bag.ReduceFromCargo(food, 1);
            return true;
        }

        /// <summary>The panel's button template (its private buttonTemplate, Core/GameContract), or null where the game has none.</summary>
        public static Button Template(PlayerActivityUI panel)
        {
            var field = GameContract.ActivityButtons.Field;
            return panel != null && field != null ? field.GetValue(panel) as Button : null;
        }

        /// <summary>The blue of the game's own Start buttons in this panel (PlayerActivityUI.SetButton, GlobalReferences).</summary>
        public static Sprite Blue()
        {
            return GlobalReferences.GetButtonImageByName("blue");
        }
    }
}
