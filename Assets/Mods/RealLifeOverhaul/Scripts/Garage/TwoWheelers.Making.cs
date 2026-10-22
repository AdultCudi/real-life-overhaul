using System.Collections.Generic;
using System.Globalization;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>The making part of <see cref="TwoWheelers"/>: spawning, putting back, collecting and removing them in the save.</summary>
    public sealed partial class TwoWheelers
    {
        /// <summary>A new two-wheeler at a place, made the way the game makes a scooter. Null if it could not be made.</summary>
        public BikeMount Spawn(BikeKind kind, Vector3 position, Quaternion rotation, bool owned, int colour = -1)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.VehicleInstances == null || VehicleTypeHelper.GetVehicleType(ScooterType) == null)
            {
                return null;
            }
            // The game bills and tickets a parked vehicle in a public space; a bicycle on the pavement owes nobody anything.
            var instance = new VehicleInstance(ScooterType) { parkingState = ParkingState.Undefined };
            var scooter = VehicleHelper.CreateAndSpawnVehicle(instance, position, rotation) as ScooterController;
            if (scooter == null)
            {
                save.VehicleInstances.Remove(instance);
                Logger?.Warn("Two-wheelers: the game's scooter prefab has no ScooterController; no two-wheeler was made.");
                return null;
            }
            var record = new BikeRecord
            {
                Id = instance.id, Kind = kind, Owned = owned, Position = position, Heading = rotation.eulerAngles.y, Day = TimeHelper.CurrentDay, Colour = colour
            };
            GarageState.Bikes[instance.id] = record;
            GarageState.Touch();
            if (scooter.vehicleCollider != null)
            {
                scooter.vehicleCollider.gameObject.layer = LayerHelper.InteractiveItemsLayerIndex;   // as the game leaves a parked scooter, to be walked up to and used
            }
            BikeMount mount = BikeMount.Attach(scooter, record);
            if (mount != null)
            {
                Logger?.Info("Two-wheelers: made" + (owned ? " (owned): " : " (hired): ") + mount.Report());
                Watch(mount, position, rotation);
            }
            else
            {
                Logger?.Info("Two-wheelers: " + kind + (owned ? " (owned)" : " (hired)") + " made at " + position.ToString("F2", CultureInfo.InvariantCulture)
                    + ", but it could not be dressed: it is a scooter.");
            }
            return mount;
        }

        /// <summary>Takes a two-wheeler out of the world and the save, the game's own way.</summary>
        public void Remove(string id)
        {
            GameInstance save = SaveGameManager.Current;
            GarageState.Bikes.Remove(id);
            GarageState.Touch();
            if (save == null || save.VehicleInstances == null)
            {
                return;
            }
            VehicleInstance instance = null;
            for (int i = 0; i < save.VehicleInstances.Count; i++)
            {
                if (save.VehicleInstances[i] != null && save.VehicleInstances[i].id == id)
                {
                    instance = save.VehicleInstances[i];
                    break;
                }
            }
            if (instance == null)
            {
                return;
            }
            VehicleController controller = VehicleHelper.GetVehicleController(instance);
            if (controller != null && save.ActiveVehicleId == id)
            {
                controller.ExitVehicle();
            }
            OverlayGuard.Release(controller);
            instance.Delete(controller);
        }

        // An owned one whose scooter the game took away (played without the mod): back where it stood.
        private void RestoreLost(GameInstance save)
        {
            _scratch.Clear();
            foreach (KeyValuePair<string, BikeRecord> pair in GarageState.Bikes)
            {
                if (pair.Value.Owned && !Has(save, pair.Key))
                {
                    _scratch.Add(pair.Key);
                }
            }
            for (int i = 0; i < _scratch.Count && i < 6; i++)
            {
                BikeRecord old = GarageState.Bikes[_scratch[i]];
                GarageState.Bikes.Remove(_scratch[i]);
                if (old.Position.sqrMagnitude < 1f)
                {
                    continue;   // never placed
                }
                BikeMount mount = Spawn(old.Kind, old.Position, Quaternion.Euler(0f, old.Heading, 0f), true, old.Colour);
                if (mount != null)
                {
                    WarnIfWalled(mount);
                }
                Logger?.Info("Two-wheelers: an owned " + old.Kind + " was missing from the save and " + (mount != null ? "has been stood back where it was left." : "could not be put back."));
            }
            _scratch.Clear();
        }

        // An owned one standing behind a building's wall as a save loads: said once, with the way out.
        private void WarnIfWalled(BikeMount mount)
        {
            Vector3 at = mount.transform.position;
            if (mount.Record == null || !mount.Record.Owned || BuildingManager.IsInsideBuilding || !BikePlaces.Walled(at))
            {
                return;
            }
            Logger?.Warn("Two-wheelers: an owned one stands behind a building's wall, where it cannot be walked up to: " + mount.Report());
            Notifications.Show(NotificationType.Warning, "reallife_bike_walled", new Dictionary<string, string>(2)
            {
                { "name", mount.Def.Name.GetLocalization() }, { "place", BikePlaces.Whereabouts(at) }
            }, 14f);
        }

        // A hired bicycle is the player's until the end of the next day; then it is collected, with a late fee.
        private void CollectHires(GameInstance save, int day)
        {
            _scratch.Clear();
            foreach (KeyValuePair<string, BikeRecord> pair in GarageState.Bikes)
            {
                if (!pair.Value.Owned && day > pair.Value.Day + 1 && save.ActiveVehicleId != pair.Key)
                {
                    _scratch.Add(pair.Key);
                }
            }
            for (int i = 0; i < _scratch.Count; i++)
            {
                Remove(_scratch[i]);
                Crime.Pay(BikeCatalogue.LateFee, "reallife_transaction_bike");
                Notifications.Show(NotificationType.Warning, "reallife_bike_collected",
                    new Dictionary<string, string>(1) { { "amount", GarageText.Money(BikeCatalogue.LateFee) } }, 9f);
            }
            _scratch.Clear();
        }

        private static bool Has(GameInstance save, string id)
        {
            for (int i = 0; i < save.VehicleInstances.Count; i++)
            {
                if (save.VehicleInstances[i] != null && save.VehicleInstances[i].id == id)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Where every two-wheeler stands now, written into its record; called while the game saves.</summary>
        public void RememberPositions()
        {
            // Saved as in view: the game takes a scooter away four hours after it was last seen.
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.VehicleInstances != null)
            {
                for (int i = 0; i < save.VehicleInstances.Count; i++)
                {
                    VehicleInstance instance = save.VehicleInstances[i];
                    if (instance != null && GarageState.Bike(instance.id) != null)
                    {
                        instance.lastSeen = null;
                    }
                }
            }
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is ScooterController scooter && scooter.vehicleInstance != null)
                {
                    BikeRecord record = GarageState.Bike(scooter.vehicleInstance.id);
                    if (record != null)
                    {
                        record.Position = scooter.transform.position;
                        record.Heading = scooter.transform.eulerAngles.y;
                    }
                }
            }
        }
    }
}
