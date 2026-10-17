using System;
using System.Collections.Generic;
using Buildings;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The hotels and car hire as the game has them; a hired car's object goes and is made again, its save entry stays.</summary>
    internal static partial class EstateGame
    {
        private const float KeepAway = 40f;        // metres: a car does not leave or come back with the player nearer
        private const float SpaceClear = 3f;       // metres: a space is taken while another of the player's vehicles is this near

        public static HotelSlot Slot(string id)
        {
            return TravelCatalogue.Hotel(id);
        }

        public static string HotelName(HotelSlot slot)
        {
            return slot != null ? slot.NameKey.GetLocalization() : string.Empty;
        }

        /// <summary>A hotel's tier, district and tonight's going rates, from Travel's own figures. False if Travel no longer has it.</summary>
        public static bool ReadHotel(HotelFile f, int day)
        {
            HotelSlot slot = Slot(f.Slot);
            if (slot == null)
            {
                return false;
            }
            RoomType[] rooms = TravelCatalogue.Rooms(slot.Tier);
            f.Tier = Mathf.Clamp((int)slot.Tier, 0, EstateCatalogue.HotelTiers - 1);
            f.Income = Income(slot.Neighbourhood);
            f.Name = HotelName(slot);
            for (int type = 0; type < EstateCatalogue.RoomTypes && type < rooms.Length; type++)
            {
                f.Going[type] = TravelCatalogue.NightRate(slot, rooms[type], day, (int)TimeHelper.GetDayOfWeek(day), 1f);
            }
            return true;
        }

        public static int HotelPrice(HotelSlot slot, int real, int daysPerYear)
        {
            RoomType[] rooms = TravelCatalogue.Rooms(slot.Tier);
            return HotelRules.Price((int)slot.Tier, Mathf.RoundToInt(rooms[0].Rate * slot.RateFactor), Mathf.RoundToInt(rooms[rooms.Length > 1 ? 1 : 0].Rate * slot.RateFactor), real, daysPerYear);
        }

        public static string RoomName(int tier, int type)
        {
            RoomType[] rooms = TravelCatalogue.Rooms((HotelTier)Mathf.Clamp(tier, 0, EstateCatalogue.HotelTiers - 1));
            return rooms[Mathf.Clamp(type, 0, rooms.Length - 1)].NameKey.GetLocalization();
        }

        /// <summary>The buildings the player holds, for the hire business to be run from.</summary>
        public static void Lots(List<BuildingRegistration> into)
        {
            into.Clear();
            GameInstance save = Save;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && reg.HasValidAddress && (GoingPublic.Mine(reg) || reg.BuildingOwnedByPlayer) && !reg.BuildingCached.IsHamptonsHouse())
                {
                    into.Add(reg);
                }
            }
        }

        /// <summary>A lot's vehicle spaces, or the kerb in front of a building with none; 0 if it is no longer held.</summary>
        public static int Spaces(string lot)
        {
            Address address = AddressOf(lot);
            BuildingRegistration reg = address != null ? BuildingHelper.GetBuildingRegistration(address) : null;
            if (reg == null || (!GoingPublic.Mine(reg) && !reg.BuildingOwnedByPlayer))
            {
                return 0;
            }
            BuildingSizeData size = BuildingSizeHelper.GetData(reg);
            return Math.Min(EstateCatalogue.HireCarsMost, Math.Max(EstateCatalogue.HireSpacesAnyBuilding, size != null ? size.numberOfVehicleSlots : 0));
        }

        private static bool Door(string lot, out Vector3 door)
        {
            door = default(Vector3);
            Address address = AddressOf(lot);
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController controller = address != null && city != null ? city.FindCityBuildingController(address) : null;
            if (controller == null || controller.entranceDoors == null)
            {
                return false;
            }
            for (int i = 0; i < controller.entranceDoors.Length; i++)
            {
                if (controller.entranceDoors[i] != null && controller.entranceDoors[i].doorTransform != null)
                {
                    door = controller.entranceDoors[i].doorTransform.position;
                    return true;
                }
            }
            return false;
        }

        public static VehicleInstance Vehicle(string id)
        {
            GameInstance save = Save;
            List<VehicleInstance> all = save != null ? save.VehicleInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].id == id)
                {
                    return all[i];
                }
            }
            return null;
        }

        private static VehicleController Controller(string id)
        {
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].vehicleInstance != null && all[i].vehicleInstance.id == id)
                {
                    return all[i];
                }
            }
            return null;
        }

        /// <summary>Whether a vehicle can go on hire from a lot; with false, <paramref name="why"/> is the key of the reason.</summary>
        public static bool MayHire(VehicleInstance v, string lot, out string why)
        {
            why = null;
            GameInstance save = Save;
            VehicleController controller = v != null ? Controller(v.id) : null;
            Vector3 door;
            if (GoingPublic.InSession)
            {
                why = "reallife_gp_estate_hire";   // a car made again would not be the same car to Going Public's other players
            }
            else if (v == null || save == null || v.VehicleType == null || !v.VehicleType.IsMotorVehicle || v.zeroSellingPrice || v.VehicleType.spawnInPlayerObject)
            {
                why = "reallife_estate_hire_no_kind";
            }
            else if (Dealerships.Stock(v))
            {
                why = "reallife_estate_hire_no_stock";   // a hire would take it out of the dealership's showroom and its records
            }
            else if (controller == null || controller.controlledByPlayer || save.ActiveVehicleId == v.id)
            {
                why = "reallife_estate_hire_no_driven";
            }
            else if (v.cargoInstances != null && v.cargoInstances.Count > 0)
            {
                why = "reallife_estate_hire_no_cargo";
            }
            else if (v.parkingState == ParkingState.Illegal || v.unpaidParkingAmount > 0f || !v.Address.IsUndefined())
            {
                why = "reallife_estate_hire_no_parked";
            }
            else if (!Door(lot, out door) || (controller.transform.position - door).sqrMagnitude > EstateCatalogue.HireLotMetres * EstateCatalogue.HireLotMetres)
            {
                why = "reallife_estate_hire_no_lot";
            }
            return why == null;
        }

        public static void Hireable(HireFile hire, List<VehicleInstance> into)
        {
            into.Clear();
            GameInstance save = Save;
            List<VehicleInstance> all = save != null ? save.VehicleInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                string why;
                if (all[i] != null && hire.Car(all[i].id) == null && MayHire(all[i], hire.Lot, out why))
                {
                    into.Add(all[i]);
                }
            }
        }

        public static string CarName(string type)
        {
            return string.IsNullOrEmpty(type) ? string.Empty : LocalizorManager.IsLocalizedKey(type) ? type.GetLocalization() : type;
        }

        public static int CarPrice(VehicleInstance v)
        {
            return v != null && v.VehicleType != null ? Mathf.RoundToInt(Mathf.Max(0f, v.VehicleType.price)) : 0;
        }

        private static bool PlayerNear(Vector3 at)
        {
            PlayerController player = PlayerHelper.PlayerController;
            return (player != null && (player.transform.position - at).sqrMagnitude < KeepAway * KeepAway) || (EconomyBridges.InView != null && EconomyBridges.InView(at));
        }

        /// <summary>A car whose service is not overdue and whose tyres and brakes are not worn out, by the mod's own car records.</summary>
        public static bool GoodOrder(string id, int day)
        {
            CarRecord r = CarGarage.Loaded ? CarGarage.For(id, day) : null;
            return r == null || (!r.Broken && r.Tyres >= CarRules.WarnBelow && r.Brakes >= CarRules.WarnBelow);
        }

        /// <summary>
        /// A hired car leaves: its object goes (the first half of VehicleHelper.Delete) and where it stood is noted. False,
        /// with nothing done, if it cannot go now or, unless <paramref name="force"/>, the player is by it or looking.
        /// </summary>
        public static bool TakeOut(HireCar car, string lot, bool force)
        {
            VehicleInstance v = Vehicle(car.Id);
            VehicleController controller = Controller(car.Id);
            string why;
            if (v == null || controller == null || !MayHire(v, lot, out why) || (!force && PlayerNear(controller.transform.position)))
            {
                return false;
            }
            Vector3 at = controller.transform.position;
            car.X = Mathf.RoundToInt(at.x * 100f);
            car.Y = Mathf.RoundToInt(at.y * 100f);
            car.Z = Mathf.RoundToInt(at.z * 100f);
            car.Yaw = Mathf.RoundToInt(controller.transform.eulerAngles.y * 10f);
            v.UnAssignFromWarehouse(true);
            Hide(controller);
            v.parkingState = ParkingState.Legal;
            return true;
        }

        // The traffic is told the collider is gone, as the game tells it.
        private static void Hide(VehicleController controller)
        {
            if (controller.vehicleCollider != null)
            {
                Manager.TriggerColliderRemovedEvent(controller.vehicleCollider);
            }
            if (controller.additionalCollider != null)
            {
                Manager.TriggerColliderRemovedEvent(controller.additionalCollider);
            }
            VehicleHelper.onVehicleDestroyed?.Invoke(controller);
            UnityEngine.Object.Destroy(controller.gameObject);
        }

        /// <summary>A car out on hire that has an object again after a load is taken out of the city again. True if it was.</summary>
        public static bool HideAgain(HireCar car)
        {
            VehicleController controller = Controller(car.Id);
            if (controller == null || controller.controlledByPlayer)
            {
                return false;
            }
            Hide(controller);
            return true;
        }

        /// <summary>True while a car that is out has no object in the city, as it should.</summary>
        public static bool IsOut(HireCar car)
        {
            return Vehicle(car.Id) != null && Controller(car.Id) == null;
        }

        /// <summary>
        /// A hired car comes back where it stood with what the hire did to it. False, with nothing done, while its space is
        /// taken or, unless <paramref name="force"/>, the player is there or looking.
        /// </summary>
        public static bool BringBack(HireCar car, HireReturn r, int day, bool force)
        {
            GameInstance save = Save;
            VehicleInstance v = Vehicle(car.Id);
            if (save == null || v == null)
            {
                return false;
            }
            if (Controller(car.Id) != null)
            {
                return true;   // in the city already (the feature was off for a while)
            }
            var at = new Vector3(car.X / 100f, car.Y / 100f, car.Z / 100f);
            if (!force && PlayerNear(at))
            {
                return false;
            }
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && (all[i].transform.position - at).sqrMagnitude < SpaceClear * SpaceClear)
                {
                    return false;
                }
            }
            Wear(v, r, day);
            save.VehicleInstances.Remove(v);   // CreateAndSpawnVehicle adds it to the list again
            try
            {
                VehicleHelper.CreateAndSpawnVehicle(v, at, Quaternion.Euler(0f, car.Yaw / 10f, 0f));
            }
            catch (Exception)
            {
                if (!save.VehicleInstances.Contains(v))
                {
                    save.VehicleInstances.Add(v);   // whatever happened, the car stays the player's
                }
                throw;
            }
            return true;
        }

        // The fuel used, the dirt and the damage, and on the mod's car record the distance and the wear of tyres and brakes.
        private static void Wear(VehicleInstance v, HireReturn r, int day)
        {
            if (r.Km <= 0)
            {
                return;   // called back, not the end of a hire
            }
            float tank = v.VehicleType != null ? v.VehicleType.maxFuel : 0f;
            v.fuel = Mathf.Min(v.fuel, tank * r.Fuel / 1000f);
            v.dirtiness = Mathf.Clamp01(v.dirtiness + 0.1f * r.Days);
            v.damage = Mathf.Clamp(v.damage + r.Damage / 1000f, 0f, 1f);
            CarRecord record = CarGarage.Loaded ? CarGarage.For(v.id, day) : null;
            if (record != null && v.VehicleType != null)
            {
                // The kind by price: the mod's own kinds need the car's object, which is not made yet.
                int cls = HireRules.Class(Mathf.RoundToInt(v.VehicleType.price));
                CarKind kind = cls == 0 ? CarKind.Economy : cls == 1 ? CarKind.Saloon : CarKind.Luxury;
                record.Km += r.Km;
                record.Tyres = Mathf.Clamp01(record.Tyres - r.Km / Mathf.Max(1f, CarRules.TyreLife(kind)));
                record.Brakes = Mathf.Clamp01(record.Brakes - r.Km / Mathf.Max(1f, CarRules.BrakeLife(kind)));
            }
        }

        /// <summary>A car that never came back is taken off the save's list the game's own way (VehicleHelper.Delete with no object).</summary>
        public static void Lose(HireCar car)
        {
            VehicleInstance v = Vehicle(car.Id);
            if (v != null)
            {
                VehicleController controller = Controller(car.Id);
                OverlayGuard.Release(controller);
                v.Delete(controller);
            }
        }

        /// <summary>While a car is out the game's hourly parking round has nothing to say about it.</summary>
        public static void KeepLegal(HireCar car)
        {
            VehicleInstance v = Vehicle(car.Id);
            if (v != null && v.parkingState != ParkingState.Legal)
            {
                v.parkingState = ParkingState.Legal;
            }
        }
    }
}
