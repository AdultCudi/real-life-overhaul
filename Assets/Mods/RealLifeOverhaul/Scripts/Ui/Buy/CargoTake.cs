using System.Collections.Generic;
using BigAmbitions.Items;
using Buildings.BuildingTypes.Special.PrivateDriverService;
using Helpers;
using UI;
using UI.PlayerHUD;
using UnityEngine.UI;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A press on a stack's tile, on the item panel or in manage cargo: the game's own click on its tile (CargoItemUi's item
    /// button), which is the game's one way to take a stack out. Its click puts the stack in the hands, on the hand truck
    /// in use or across to the storage that is open, or takes a hand truck out of the boot. The holder is looked at before
    /// and after, so a press that takes nothing writes one line to Player.log with the reason, whatever the diagnostics
    /// switch says: a report of cargo that will not come out then answers itself. Never another player's vehicle in a
    /// Going Public session. Cost: two passes over the holder's stacks a press, nothing else.
    /// </summary>
    internal static class CargoTake
    {
        /// <summary>
        /// Presses the game's tile; true when anything was taken. <paramref name="fromUsedVehicle"/>: the tile stands on the
        /// panel of the vehicle the player uses (the game's PickCargoInstance), else it is manage cargo's or a held thing's.
        /// </summary>
        public static bool Press(CargoItemUi slot, ICargoHolder holder, bool mine, bool fromUsedVehicle, string where)
        {
            Button tile = slot != null ? BuyKits.Get<Button>(GameContract.SlotButton, slot) : null;
            string stack = slot != null ? slot.name : "a stack";   // the game names its tile after the goods
            CargoTakeRefusal why = CargoTakeRules.Why(tile != null && tile.gameObject.activeInHierarchy, tile != null && tile.interactable, mine, fromUsedVehicle, false,
                false, false, false, false);
            if (CargoTakeRules.Before(why))
            {
                Refused(stack, where, why, holder);
                return false;
            }
            ICargoHolder own = Own();
            ItemInstance hands = PlayerHelper.ItemInstanceInHands;
            string vehicle = ActiveVehicle();
            bool cart = Cart(stack), open = StorageOpen(), garage = PrivateDriverGarageTrigger.IsActive;
            Count(holder, out int stacks, out int pieces);
            UiWidgets.Hand(tile);
            Count(holder, out int stacksNow, out int piecesNow);
            if (CargoTakeRules.Took(stacks, pieces, stacksNow, piecesNow, !ReferenceEquals(hands, PlayerHelper.ItemInstanceInHands), vehicle != ActiveVehicle()))
            {
                if (Meta.Dev.Verbose)
                {
                    UiDirector.Logger?.Info("UI check (cargo tile): '" + stack + "' pressed on " + where + ", the holder went from " + stacks + " stacks and " + pieces
                                            + " pieces to " + stacksNow + " and " + piecesNow + ": PASS.");
                }
                return true;
            }
            Refused(stack, where, CargoTakeRules.Why(true, true, true, fromUsedVehicle, own != null && ReferenceEquals(own, holder), cart, own != null, open, garage), holder);
            return false;
        }

        /// <summary>True for a holder that is the player's to unload: always outside a Going Public session; in one, a vehicle only if the save has it.</summary>
        public static bool Mine(ICargoHolder holder)
        {
            if (!GoingPublic.InSession || !(holder is VehicleInstance vehicle))
            {
                return true;
            }
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && ReferenceEquals(all[i].vehicleInstance, vehicle))
                {
                    return GoingPublic.MineVehicle(all[i]);
                }
            }
            return true;   // a vehicle with no body here (a shelf's or a stored one's cargo) is read from the player's own save
        }

        /// <summary>What the player holds or pushes, as the game's click reads it (CargoItemUi.ClickItem): the vehicle in use, else the thing in the hands, else nothing.</summary>
        public static ICargoHolder Own()
        {
            if (PlayerHelper.IsUsingVehicle)
            {
                return VehicleHelper.GetCurrentVehicle();
            }
            return PlayerHelper.IsHoldingItem ? PlayerHelper.ItemInstanceInHands : null;
        }

        // One line a refusal, whatever the diagnostics switch says: what was pressed where, why nothing came out, and what the game held then.
        private static void Refused(string stack, string where, CargoTakeRefusal why, ICargoHolder holder)
        {
            Count(holder, out int stacks, out int pieces);
            string vehicle = ActiveVehicle();
            ItemInstance hands = PlayerHelper.ItemInstanceInHands;
            Meta.Dev.Always(UiDirector.Logger, "UI check (cargo tile): '" + stack + "' pressed on " + where + " took nothing: " + CargoTakeRules.Words(why) + " (the holder has "
                + stacks + " stacks of " + (holder != null ? holder.GetMaxCargoSize() : 0) + " and " + pieces + " pieces; the player holds "
                + (hands != null ? hands.itemName : "nothing") + " and uses " + (vehicle ?? "no vehicle") + "; manage cargo is " + (StorageOpen() ? "open" : "closed") + ").");
        }

        private static void Count(ICargoHolder holder, out int stacks, out int pieces)
        {
            List<CargoInstance> held = holder != null ? holder.GetCargoInstances() : null;
            stacks = held != null ? held.Count : 0;
            pieces = 0;
            for (int i = 0; i < stacks; i++)
            {
                pieces += held[i] != null ? held[i].amount : 0;
            }
        }

        private static string ActiveVehicle()
        {
            GameInstance save = SaveGameManager.Current;
            return save != null && !string.IsNullOrEmpty(save.ActiveVehicleId) ? save.ActiveVehicleId : null;
        }

        private static bool StorageOpen()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.playerHUD != null && uis.playerHUD.manageCargoUI != null && uis.playerHUD.manageCargoUI.isPanelOpen;
        }

        // A hand truck or a flatbed cart as goods: the game takes it out of the cargo as a vehicle to push (CargoItemUi.IsDeployableFromCargo).
        private static bool Cart(string itemName)
        {
            Item item = !string.IsNullOrEmpty(itemName) ? ItemsGetter.GetByName(itemName, true) : null;
            VehicleType type = item != null && !string.IsNullOrEmpty(item.vehicleType) ? VehicleTypeHelper.GetVehicleType(item.vehicleType) : null;
            return type != null && type.spawnInPlayerObject;
        }
    }
}
