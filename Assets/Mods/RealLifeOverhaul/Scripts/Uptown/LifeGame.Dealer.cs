using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Characters;
using Buildings;
using Controllers;
using Dialogs;
using Entities;
using PlayerActivity;
using RealLifeOverhaul.NewBuildings;
using UI;
using UI.Dialog;
using UnityEngine;
using Vehicles;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The player at a special place's desk, on the game's side: the look at a desk's dialog gone wrong (DeskWatchRules) and what
    /// the dealership's tests open and read. The game seats the player at a special employee's desk, pauses a second later and
    /// opens the dialog its building's service answers with; a panel up on no dialog, or a player seated with nothing holding
    /// them, never ends of itself. Once a second, while the player is in one of the district's buildings, the game's own way out
    /// is taken: DialogUI.FinishDialog (it stands the player up, takes the pause off and lifts the hold) and Character.Reset.
    /// COST: nothing per frame; two field reads a second, and only while the player is inside a district building.
    /// </summary>
    internal static partial class LifeGame
    {
        private static readonly FieldInfo BlockersField = GameContract.NavBlockers.Field;   // PlayerController._activeNavigationBlockers

        private static float _brokenSince = -1f;   // when the dialog panel was first seen up on no dialog (Time.unscaledTime), or -1
        private static int _seatedLooks;           // looks in a row at the player seated with nothing holding them

        private static PlayerController Player => InstanceBehavior<GameManager>.IsInitialized ? InstanceBehavior<GameManager>.Instance.playerController : null;

        private static DialogUI Panel
        {
            get
            {
                UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
                return uis != null && uis.playerHUD != null ? uis.playerHUD.dialogUI : null;
            }
        }

        /// <summary>Nothing carried over to the next look: a new load, or the player out of the district's buildings.</summary>
        public static void WatchReset()
        {
            _brokenSince = -1f;
            _seatedLooks = 0;
        }

        /// <summary>
        /// One look: a panel up on no dialog for the grace is closed the game's way, and a player seated with nothing holding them
        /// for a few looks is stood up. What it did, for Player.log, or null for nothing.
        /// </summary>
        public static string Watch(float now)
        {
            DialogUI ui = Panel;
            bool panel = ui != null && ui.isPanelOpen;
            if (DeskWatchRules.Broken(panel, panel && ui.dialog != null, ref _brokenSince, now, DeskWatchRules.Grace))
            {
                _brokenSince = -1f;
                ui.FinishDialog();
                return "a dialog panel stood open on no dialog (the desk's service answers with none): closed the game's way, the player stood up and the pause lifted";
            }
            PlayerController player = Player;
            ThirdPersonCharacter body = player != null ? player.Character : null;
            bool seated = body != null && body.isSittingOn != null;
            bool held = panel || PlayerActivityUI.IsPanelOpen || Holding(player);
            if (DeskWatchRules.Stuck(seated, held, ref _seatedLooks, DeskWatchRules.SeatedLooks))
            {
                _seatedLooks = 0;
                body.Reset();
                return "the player sat with nothing holding them for " + DeskWatchRules.SeatedLooks + " looks: stood up the game's way";
            }
            return null;
        }

        // True when the game holds the player's walking (any navigation blocker), or when that cannot be read: then nothing is touched.
        private static bool Holding(PlayerController player)
        {
            if (player == null || BlockersField == null)
            {
                return true;
            }
            return !(BlockersField.GetValue(player) is HashSet<NavigationBlocker> blockers) || blockers.Count > 0;
        }

        // ---------------------------------------------------------------- what the dealership's tests open and read

        /// <summary>True while the game's dialog panel is up.</summary>
        public static bool DialogOpen
        {
            get
            {
                DialogUI ui = Panel;
                return ui != null && ui.isPanelOpen;
            }
        }

        /// <summary>
        /// The dealership's desk panel as its desk would open it: the game's dialog of the building's service, with the business's
        /// contact. False, with why, when it could not be opened.
        /// </summary>
        public static bool OpenStore(MadeBuilding made, out string why)
        {
            why = null;
            DialogUI ui = Panel;
            BuildingRegistration reg = made != null ? made.Registration : null;
            SpecialService service = made != null && made.Building != null ? made.Building.SpecialService : null;
            if (ui == null || reg == null || service == null)
            {
                why = "the game's dialog panel or the place's service is not there";
                return false;
            }
            Contact contact = reg.GetOrAddBusinessContact(true);
            if (contact == null)
            {
                why = "the game gave the place no contact";
                return false;
            }
            ui.ShowDialog(service.dialogType, NavigationBlocker.SpecialEmployeeDialog, contact);
            return true;
        }

        /// <summary>
        /// What the open panel holds, read through the game's own members: the dealership's cars the panel lists (the game's list
        /// of a store's cars), the car it has selected, that car's price, the delivery fee of the store's settings and how many of
        /// the player's warehouses it can deliver to. False until the panel's own page has started (it selects its first car).
        /// </summary>
        public static bool ReadStore(MadeBuilding made, out int cars, out string first, out float price, out float fee, out int warehouses)
        {
            cars = 0;
            first = null;
            price = 0f;
            fee = 0f;
            warehouses = 0;
            DialogUI ui = Panel;
            VehicleContractSettings page = ui != null ? ui.GetInputTransformSafe<VehicleContractSettings>() : null;
            if (page == null || page.selectedVehicleForSale == null)
            {
                return false;
            }
            Address address = made.Building.Address;
            cars = MadeGame.Listed(address);
            first = page.selectedVehicleForSale.VehicleName;
            VehicleType type = string.IsNullOrEmpty(first) ? null : VehicleTypeHelper.GetVehicleType(first);
            price = type != null ? type.price : 0f;
            VehicleStoreSettings store = VehicleDeliveryHelper.GetVehicleStoreSettings(address);
            fee = store != null ? store.deliveryPrice : 0f;
            warehouses = VehicleDeliveryHelper.GetAvailableWarehousesToDeliver().Count;
            return true;
        }

        /// <summary>The game's own close of its dialog panel (the player stood up, the pause lifted), if one is up.</summary>
        public static void CloseDialog()
        {
            DialogUI ui = Panel;
            if (ui != null && ui.isPanelOpen)
            {
                ui.FinishDialog();
            }
        }

        /// <summary>The nearest free seat in the building the player is in, within the room, or null: the chair a desk's customer sits on.</summary>
        public static Transform NearestSeat()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.IsInitialized ? InstanceBehavior<BuildingManager>.Instance : null;
            PlayerController player = Player;
            if (manager == null || player == null || !BuildingManager.IsInsideBuilding || manager.allItemControllers == null)
            {
                return null;
            }
            Vector3 at = player.transform.position;
            Transform best = null;
            float near = float.MaxValue;
            for (int i = 0; i < manager.allItemControllers.Count; i++)
            {
                Transform seat = Seating.Free(manager.allItemControllers[i]);
                if (seat == null)
                {
                    continue;
                }
                float d = (seat.position - at).sqrMagnitude;
                if (d < near)
                {
                    near = d;
                    best = seat;
                }
            }
            return best;
        }

        /// <summary>True when the player is seated (the game's own mark of it).</summary>
        public static bool Seated
        {
            get
            {
                PlayerController player = Player;
                return player != null && player.Character != null && player.Character.isSittingOn != null;
            }
        }

        /// <summary>The player seated as a special employee's desk seats them: the game's own SitOnChair, nothing else held.</summary>
        public static bool SitAtDesk(Transform seat)
        {
            PlayerController player = Player;
            if (player == null || player.Character == null || seat == null || !player.Character.navmeshAgent.enabled)
            {
                return false;
            }
            player.ResetNavigation();
            player.Character.SitOnChair(seat);
            return true;
        }

        /// <summary>Whatever held the player at a desk undone, as the game's own way out does it: the panel closed, the player stood, the hold off.</summary>
        public static void StandFromDesk()
        {
            CloseDialog();
            PlayerController player = Player;
            if (player != null && player.Character != null && player.Character.isSittingOn != null)
            {
                player.Character.Reset();
            }
            if (player != null && BlockersField?.GetValue(player) is HashSet<NavigationBlocker> blockers && blockers.Contains(NavigationBlocker.SpecialEmployeeDialog))
            {
                player.UnsetNavigationBlocker(NavigationBlocker.SpecialEmployeeDialog);
            }
        }

        /// <summary>
        /// The game's dialog panel opened on a dialog type that has no dialog (CallDialogFactory.GetDialog answers null for it), as a desk
        /// whose service carries none opens it: the panel up, the hold set, nothing to talk to. True when the panel is up.
        /// </summary>
        public static bool OpenNothing()
        {
            DialogUI ui = Panel;
            if (ui == null || ui.isPanelOpen)
            {
                return false;
            }
            ui.ShowDialog(CallDialogType.NotImplemented, NavigationBlocker.SpecialEmployeeDialog);
            return ui.isPanelOpen;
        }
    }
}
