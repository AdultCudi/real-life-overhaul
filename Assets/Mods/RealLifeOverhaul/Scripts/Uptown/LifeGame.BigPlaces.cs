using System.Collections.Generic;
using Buildings;
using Buildings.BuildingTypes.Special.GasStation;
using Entities;
using Helpers;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.NewGround;
using UI;
using UI.Overlays;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's big special places on the game's side, for their tests: whether each stands as one of the game's own (its
    /// service, its registration, the game's lists of special places and wholesalers, its drive-ins or its pumps and their
    /// triggers wired to its own controller), the player taken there, one order delivered by the game's own delivery of a
    /// wholesale contract, and a fill-up at its pumps by the game's own overlay. Nothing here runs a frame: a button's press
    /// and the fill-up test's few steps.
    /// </summary>
    internal static partial class LifeGame
    {
        private const string WholesaleType = "ba:businesstype_wholesalestore";
        private const string StationType = "ba:businesstype_gasstation";
        private const float DoorStep = 1.6f;      // m in front of a door the player is put, on foot
        private const float DriveStep = 9f;       // m in front of a garage door or a forecourt a car is put, facing it
        private const float SampleReach = 3f;     // m the walking mesh is looked for round a spot

        /// <summary>Why a big place does not stand as one of the game's own, or null when every part of it does (English, for the check line).</summary>
        public static string BigWhy(MadeBuilding made, BigPlace place)
        {
            if (made == null || made.Controller == null || made.Building == null || made.Registration == null)
            {
                return "it does not stand at this load";
            }
            Building b = made.Building;
            BuildingRegistration reg = made.Registration;
            string type = place.Kind == BigKind.Wholesale ? WholesaleType : StationType;
            if (b.SpecialService == null || b.SpecialService.businessTypeName != type)
            {
                return "its record carries no " + type + " service";
            }
            if (reg.businessTypeName != type || reg.BusinessName != place.Name)
            {
                return "its registration is " + (reg.businessTypeName ?? "(none)") + " named " + (reg.BusinessName ?? "(none)");
            }
            if (!BuildingHelper.SpecialServiceBuildings.TryGetValue(b.Address, out Building listed) || listed != b)
            {
                return "it is not in the game's list of special buildings";
            }
            if (!MadeGame.InCityList(made, out _))
            {
                return "it is not in the city's list of buildings";
            }
            if (!Walkable(DoorSpot(made.Controller)))
            {
                return "its door is not on the walking ground";
            }
            return place.Kind == BigKind.Wholesale ? StoreWhy(made, reg) : StationWhy(made);
        }

        // A drive-in store: among the game's wholesalers, goods to sell, and garage doors of its own controller.
        private static string StoreWhy(MadeBuilding made, BuildingRegistration reg)
        {
            if (!(made.Building.SpecialService.settings is WholesaleStoreSettings))
            {
                return "its service has no wholesale store's settings (the delivery fee)";
            }
            IReadOnlyList<BuildingRegistration> sellers = BuildingHelper.GetWholesaleBuildingRegistrations();
            bool among = false;
            for (int i = 0; sellers != null && i < sellers.Count; i++)
            {
                among |= sellers[i] == reg;
            }
            if (!among)
            {
                return "it is not among the game's wholesale stores";
            }
            if (reg.GetListOfItemsForSale().Count == 0)
            {
                return "its layout sells nothing";
            }
            DriveInEntrance[] doors = made.Controller.driveInEntrances;
            if (doors == null || doors.Length == 0)
            {
                return "it has no garage doors";
            }
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] == null || !doors[i].transform.IsChildOf(made.Controller.transform))
                {
                    return "a garage door of it is not its own";
                }
            }
            return null;
        }

        // A filling station: its pumps' and repair bays' triggers its own controller's, as the towing and the overlay find them.
        private static string StationWhy(MadeBuilding made)
        {
            if (!(made.Building.SpecialService.settings is GasStationSettings settings) || settings.truckOnly)
            {
                return "its service has no filling station's settings for cars";
            }
            GasStationController station = made.Controller.GetComponent<GasStationController>();
            if (station == null || station.gasStationTriggers == null || station.gasStationTriggers.Length == 0)
            {
                return "it has no pumps";
            }
            int refuel = 0;
            for (int i = 0; i < station.gasStationTriggers.Length; i++)
            {
                GasStationTrigger t = station.gasStationTriggers[i];
                if (t == null || t.cbc != made.Controller || !t.transform.IsChildOf(made.Controller.transform))
                {
                    return "a pump of it answers to another building";
                }
                refuel += t.isRefuelStation ? 1 : 0;
            }
            for (int i = 0; station.repairStationTriggers != null && i < station.repairStationTriggers.Length; i++)
            {
                if (station.repairStationTriggers[i] == null || station.repairStationTriggers[i].cbc != made.Controller)
                {
                    return "a repair bay of it answers to another building";
                }
            }
            GasStationPartController[] parts = made.Controller.GetComponentsInChildren<GasStationPartController>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].gasStationCbc != made.Controller)
                {
                    return "a part of its forecourt answers to another building";
                }
            }
            return refuel > 0 ? null : "none of its pumps fills a tank";
        }

        /// <summary>
        /// A store among the game's wholesale stores, or out of them: the game keeps its list once made (from its list of all
        /// buildings, until the next load: BuildingHelper.GetWholesaleBuildingRegistrations), so one made before the district's
        /// buildings were in that list lacks it, and one made before it stood down would keep it. True when it is in the list
        /// (or, <paramref name="listed"/> false, out of it); true for anything but a wholesale store.
        /// </summary>
        public static bool Wholesaler(Building record, BuildingRegistration registration, bool listed)
        {
            if (record == null || registration == null || record.SpecialService == null || record.SpecialService.businessTypeName != WholesaleType)
            {
                return true;
            }
            if (!(BuildingHelper.GetWholesaleBuildingRegistrations() is List<BuildingRegistration> all))
            {
                return false;
            }
            bool there = all.Contains(registration);
            if (listed && !there)
            {
                all.Add(registration);
            }
            else if (!listed && there)
            {
                all.Remove(registration);
            }
            return true;
        }

        /// <summary>True when the player drives a vehicle of their own that the game's pumps serve (a motor vehicle with room for cargo).</summary>
        public static bool DrivesFillable(out VehicleController vehicle)
        {
            GameManager game = InstanceBehavior<GameManager>.IsInitialized ? InstanceBehavior<GameManager>.Instance : null;
            vehicle = PlayerHelper.IsUsingVehicle && game != null ? game.selectedVehicle : null;
            return vehicle != null && vehicle.vehicleType != null && vehicle.vehicleType.IsMotorVehicle && vehicle.vehicleType.maxCargoCapacity > 0
                   && vehicle.vehicleCollider != null && !vehicle.vehicleType.spawnInPlayerObject;
        }

        /// <summary>True when the player is out of doors and on foot or at the wheel, where the game can put them somewhere else.</summary>
        public static bool CanMove => !Indoors && !UI.Load.LoadScene.isLoading;

        /// <summary>
        /// The player taken to a big place, the game's own way: on foot to its door, at the wheel to in front of the store's
        /// first garage door or onto the station's forecourt, facing it. False when the player cannot be moved now.
        /// </summary>
        public static bool TakeTo(MadeBuilding made, BigKind kind)
        {
            if (made == null || made.Controller == null || !CanMove)
            {
                return false;
            }
            GameManager game = InstanceBehavior<GameManager>.Instance;
            VehicleController vehicle = PlayerHelper.IsUsingVehicle && game != null ? game.selectedVehicle : null;
            if (vehicle == null)
            {
                WalkGame.Warp(DoorSpot(made.Controller));
                return true;
            }
            Transform at = kind == BigKind.Wholesale && made.Controller.driveInEntrances != null && made.Controller.driveInEntrances.Length > 0
                ? made.Controller.driveInEntrances[0].transform : made.Controller.entranceDoors[0].doorTransform;
            Vector3 outward = Vector3.ProjectOnPlane(at.forward, Vector3.up).normalized;
            VehicleHelper.TeleportVehicleToGround(vehicle, at.position + outward * DriveStep, Quaternion.LookRotation(-outward));
            return true;
        }

        // A metre and a half out of the first door, on the ground.
        private static Vector3 DoorSpot(CityBuildingController c)
        {
            Transform door = c.entranceDoors[0].doorTransform;
            return door.position + Vector3.ProjectOnPlane(door.forward, Vector3.up).normalized * DoorStep;
        }

        private static bool Walkable(Vector3 at)
        {
            var walk = new NavMeshQueryFilter { agentTypeID = BuildingManager.PlayerAgentTypeId, areaMask = NavMesh.AllAreas };
            return NavMesh.SamplePosition(at, out NavMeshHit _, SampleReach, walk);
        }

        // ---------------------------------------------------------------- an order delivered now

        /// <summary>
        /// The player's business the order goes to: of their own units the game delivers wholesale orders to (a business
        /// whose building type takes them, as the order dialog lists them), the first by address; null when there is none.
        /// </summary>
        public static BuildingRegistration OrderingBusiness()
        {
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            BuildingRegistration best = null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                if (r == null || !r.RentedByPlayer || r.businessTypeName == BuildingsRules.EmptyBusinessType || GoingPublic.Foreign(r)
                    || !BuildingTypeHelper.GetData(r).HasTag(BigAmbitions.Tags.TagRef.Buildingtypetag.acceptswholesaledeliveries))
                {
                    continue;
                }
                if (best == null || string.CompareOrdinal(r.StreetName, best.StreetName) < 0 || (r.StreetName == best.StreetName && r.StreetNumber < best.StreetNumber))
                {
                    best = r;
                }
            }
            return best;
        }

        /// <summary>
        /// One unit of one of the store's goods ordered for <paramref name="business"/> and delivered now by the game's own
        /// delivery of a wholesale contract (what its 8 o'clock pass runs for each contract due): paid from the player's money
        /// with the store's delivery fee, into the business's shelves or storage, the store's contact told, the game's own
        /// notice. The contract is the order's alone, never in the save's list. What it measured, and false with why when it
        /// could not run.
        /// </summary>
        public static bool DeliverNow(MadeBuilding store, BuildingRegistration business, out string item, out int arrived, out float paid, out string why)
        {
            item = null;
            arrived = 0;
            paid = 0f;
            why = null;
            var deliver = GameContract.WholesaleDeliver.Method;
            if (deliver == null)
            {
                why = "the game's delivery of a wholesale contract is not in this build";
                return false;
            }
            List<string> goods = store.Registration.GetListOfItemsForSale();
            List<string> sold = business.GetListOfItemsForSale();
            for (int i = 0; i < goods.Count && item == null; i++)
            {
                item = sold != null && sold.Contains(goods[i]) ? goods[i] : null;
            }
            item = item ?? (goods.Count > 0 ? goods[0] : null);
            if (item == null)
            {
                why = "the store sells nothing";
                return false;
            }
            var settings = (WholesaleStoreSettings)store.Building.SpecialService.settings;
            var order = new DeliveryContract
            {
                enabled = true, isUrgentOrder = false, nextDeliveryDay = TimeHelper.CurrentDay, repeatingOrder = false, wholesaleAddress = store.Building.Address,
                businessAddress = business.Address, deliveryFee = settings.deliveryFee,
                items = new List<DeliveryContractItem> { new DeliveryContractItem { itemName = item, amount = 1 } }
            };
            int before = BuildingHelper.CountTotalResourcesInStock(business.Address, item);
            float money = SaveGameManager.Current.Money;
            object done = deliver.Invoke(null, new object[] { order });
            arrived = BuildingHelper.CountTotalResourcesInStock(business.Address, item) - before;
            paid = money - SaveGameManager.Current.Money;
            if (!(done is bool delivered) || !delivered)
            {
                why = "the game did not deliver it (not enough money, or the item on backorder: the store's contact says which)";
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- a fill-up at the pumps

        /// <summary>The station's first pump that fills a tank, or null.</summary>
        public static GasStationTrigger Pump(MadeBuilding station)
        {
            GasStationController c = station != null && station.Controller != null ? station.Controller.GetComponent<GasStationController>() : null;
            for (int i = 0; c != null && c.gasStationTriggers != null && i < c.gasStationTriggers.Length; i++)
            {
                if (c.gasStationTriggers[i] != null && c.gasStationTriggers[i].isRefuelStation)
                {
                    return c.gasStationTriggers[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The player's car drawn up at a pump, stopped, along the pumps' lane, its tank half full where it was fuller (what
        /// the fill-up then pays for), and the game's pumps asked which of them it stands at, as the game asks when the
        /// player gets in a car. The tank as it was before.
        /// </summary>
        public static float DrawUp(VehicleController car, GasStationTrigger pump, Transform station)
        {
            float was = car.GetCurrentFuel();
            float half = car.vehicleType.maxFuel * 0.5f;
            if (was > half)
            {
                car.SetFuel(half);
            }
            VehicleHelper.TeleportVehicleToGround(car, pump.transform.position, Quaternion.LookRotation(-Vector3.ProjectOnPlane(station.forward, Vector3.up).normalized));
            GasStationController.RefreshActiveTriggers(car.vehicleCollider);
            return was;
        }

        /// <summary>The pump the game's overlay is open for now, or null (or when the contract's piece is missing).</summary>
        public static GasStationTrigger OverlayPump()
        {
            GasStationOverlay overlay = Overlay();
            return overlay != null && GameContract.StationTrigger.Field != null ? GameContract.StationTrigger.Field.GetValue(overlay) as GasStationTrigger : null;
        }

        /// <summary>The overlay's own fill-up, as its button does: false when the contract's piece is missing.</summary>
        public static bool FillUp()
        {
            GasStationOverlay overlay = Overlay();
            if (overlay == null || GameContract.StationRefuel.Method == null)
            {
                return false;
            }
            GameContract.StationRefuel.Method.Invoke(overlay, null);
            return true;
        }

        private static GasStationOverlay Overlay()
        {
            UIs ui = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            return ui != null && ui.overlayUI != null ? ui.overlayUI.gasStation : null;
        }
    }
}
