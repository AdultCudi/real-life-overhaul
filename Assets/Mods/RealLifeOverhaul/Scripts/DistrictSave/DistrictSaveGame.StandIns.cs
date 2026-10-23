using System;
using System.Collections.Generic;
using System.Globalization;
using AI.Employees.SalaryNegotiation;
using Buildings.Office.Headquarters;
using Entities;
using Helpers;
using Player.PlayerMissions;
using UnityEngine;
using Vehicles.DeliveryDriverJob;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// The fields written as a stand-in, because what they belong to is never lost: the player, the player's cars and
    /// contacts, the map's destination, the job under way, and the references of the game's own records to someone or
    /// a plan that left. Each is written naming the game's own places (outside a building's door, the way out nearest, a
    /// district of the game's, nothing) and its real value goes back after the write and at a load where its place is
    /// made; the player in a building of the mod's is the one stand-in that is not kept, as the save loads with the
    /// player outside its door with or without the mod. Never another player's things: the save's cars are this
    /// player's alone (GoingPublic.MineVehicle). COST: one pass at a save.
    /// </summary>
    internal static partial class DistrictSaveGame
    {
        private const string Inside = "inside";   // the player or a car was in a building of the mod's
        private const string Ground = "ground";   // the player or a car was out of doors on the mod's ground

        private static void StandIns(GameInstance save, PlaceSet places, Strip s)
        {
            Step(s, "the player", () => Player(save, places, s));
            Step(s, "the map's destination", () => Guider(save, places, s));
            Step(s, "the job under way", () => Mission(save, places, s));
            // save path: GameInstance.VehicleInstances[].streetName
            // save path: GameInstance.VehicleInstances[].streetNumber
            // save path: GameInstance.VehicleInstances[].parkingNeighbourhood
            // save path: GameInstance.VehicleInstances[].position
            Step(s, "the player's cars", () => Cars(save.VehicleInstances, SaveKind.Vehicle, places, s));
            // save path: GameInstance.privateDriverVehicleInstances[].streetName
            // save path: GameInstance.privateDriverVehicleInstances[].streetNumber
            // save path: GameInstance.privateDriverVehicleInstances[].parkingNeighbourhood
            // save path: GameInstance.privateDriverVehicleInstances[].position
            Step(s, "the private driver's cars", () => Cars(save.privateDriverVehicleInstances, SaveKind.DriverVehicle, places, s));
            Step(s, "the contacts", () => Contacts(save, places, s));
            Step(s, "the staff that stay", () => Staying(save, places, s));
            Step(s, "the vans' drivers and the headhunters' plans", () => Slots(save, s));
        }

        // The player in a building of the mod's is written outside its door, facing out (a made building's door, as new
        // buildings has it), and the save loads with the player there with or without the mod: nothing of it is kept. A
        // door on the mod's own ground is kept as a spot there, the save naming the way out nearest. The player out of
        // doors on the mod's ground is written at the way out nearest, and goes back at a load where that ground is made.
        // save path: GameInstance.CurrentStreetName
        // save path: GameInstance.CurrentStreetNumber
        // save path: GameInstance.LastPlayerPosition
        private static void Player(GameInstance save, PlaceSet places, Strip s)
        {
            string street = save.CurrentStreetName;
            int number = save.CurrentStreetNumber;
            SerializableVector3 at = save.LastPlayerPosition;
            SerializableQuaternion turn = save.LastPlayerRotation;
            bool inside = places.Names(street, number);
            bool ground = !inside && string.IsNullOrEmpty(street) && places.OnGround(at.x, at.z);
            if (!inside && !ground)
            {
                return;
            }
            bool hasWay = Leaving(places, inside, street, number, at, out PlaceExit exit, out bool doorOnGround);   // chosen before anything is written
            if (ground || doorOnGround)
            {
                var record = new StoredRecord(SaveKind.Player, 0, null);
                if (ground)
                {
                    Spot(record.Values, street, number, at, turn, Ground);
                }
                else if (places.DoorOf(street, number, out PlaceExit door))
                {
                    Spot(record.Values, string.Empty, 0, new SerializableVector3(door.X, door.Y, door.Z), Facing(door.Heading), Ground);
                }
                s.Records.Add(record);
            }
            s.StoodIn++;
            save.CurrentStreetName = string.Empty;   // out of doors, as a new game starts
            save.CurrentStreetNumber = 0;
            if (hasWay)
            {
                save.LastPlayerPosition = new SerializableVector3(exit.X, exit.Y, exit.Z);
                save.LastPlayerRotation = Facing(exit.Heading);
            }
            s.PutBacks.Add(() =>
            {
                save.CurrentStreetName = street;
                save.CurrentStreetNumber = number;
                save.LastPlayerPosition = at;
                save.LastPlayerRotation = turn;
            });
        }

        // The map's destination at one of the mod's places is written as none.
        // save path: GameInstance.customDestination
        private static void Guider(GameInstance save, PlaceSet places, Strip s)
        {
            Address to = save.customDestination;
            if (!Names(places, to))
            {
                return;
            }
            var record = new StoredRecord(SaveKind.Guider, 0, null);
            record.Addresses.Add(new PlaceKey(to.streetName, to.streetNumber));
            record.Values.Add(to.streetName);
            record.Values.Add(to.streetNumber.ToString(CultureInfo.InvariantCulture));
            s.Records.Add(record);
            s.StoodIn++;
            save.customDestination = null;
            s.PutBacks.Add(() => save.customDestination = to);
        }

        // A delivery job under way to or from one of the mod's places is written as none, and kept whole.
        // save path: GameInstance.currentPlayerMission<Player.PlayerMissions.DeliveryDriverMission>.startAddress
        // save path: GameInstance.currentPlayerMission<Player.PlayerMissions.DeliveryDriverMission>.destinations[].address
        // save path: GameInstance.currentPlayerMission<Player.PlayerMissions.DeliveryDriverMission>.pinnedAddress
        // save path: GameInstance.currentPlayerMission<Player.PlayerMissions.FoodDeliveryMission>.destinationAddress
        private static void Mission(GameInstance save, PlaceSet places, Strip s)
        {
            PlayerMission mission = save.currentPlayerMission;
            if (mission == null)
            {
                return;
            }
            var record = new StoredRecord(SaveKind.Mission, 0, null);
            if (mission is DeliveryDriverMission drive)
            {
                Mark(record, places, drive.startAddress);
                Mark(record, places, drive.pinnedAddress);
                List<DeliveryJobDestination> stops = drive.destinations;
                for (int i = 0; stops != null && i < stops.Count; i++)
                {
                    if (stops[i] != null)
                    {
                        Mark(record, places, stops[i].address);
                    }
                }
            }
            else if (mission is FoodDeliveryMission food)
            {
                Mark(record, places, food.destinationAddress);
            }
            if (record.Addresses.Count == 0)
            {
                return;
            }
            record.Slot = s.Slot(mission);
            s.Records.Add(record);
            s.StoodIn++;
            save.currentPlayerMission = null;
            s.PutBacks.Add(() => save.currentPlayerMission = mission);
        }

        // A car in a building of the mod's, parked in its district, or out of doors on its ground, is written at the way
        // out nearest, parked in that way out's district of the game's.
        private static void Cars(List<VehicleInstance> cars, SaveKind kind, PlaceSet places, Strip s)
        {
            for (int i = 0; cars != null && i < cars.Count; i++)
            {
                try
                {
                    Car(cars[i], i, kind, places, s);
                }
                catch (Exception e)
                {
                    s.Faults.Add("a car (" + (cars[i] != null ? cars[i].id : "none") + "): " + e.GetType().Name + ": " + e.Message);   // the others go on
                }
            }
        }

        private static void Car(VehicleInstance car, int i, SaveKind kind, PlaceSet places, Strip s)
        {
            if (car == null)
            {
                return;
            }
            string street = car.streetName;
            int number = car.streetNumber;
            string district = car.parkingNeighbourhood;
            SerializableVector3 at = car.position;
            SerializableQuaternion turn = car.rotation;
            bool inside = places.Names(street, number);
            bool parked = places.NamesDistrict(district);
            bool ground = !inside && string.IsNullOrEmpty(street) && places.OnGround(at.x, at.z);
            if (!inside && !parked && !ground)
            {
                return;
            }
            bool hasWay = Leaving(places, inside, street, number, at, out PlaceExit exit, out bool _);   // chosen before anything is written
            var record = new StoredRecord(kind, i, car.id);
            if (inside)
            {
                record.Addresses.Add(new PlaceKey(street, number));
            }
            if (parked)
            {
                record.Districts.Add(district);
            }
            Spot(record.Values, street, number, at, turn, inside ? Inside : ground ? Ground : string.Empty);
            record.Values.Add(district);
            s.Records.Add(record);
            s.StoodIn++;
            if (inside)
            {
                car.streetName = string.Empty;   // out of doors, as the game leaves a car it drives out
                car.streetNumber = 0;
            }
            if ((inside || ground) && hasWay)
            {
                car.position = new SerializableVector3(exit.X, exit.Y, exit.Z);
                car.rotation = Facing(exit.Heading);
            }
            if (parked)
            {
                car.parkingNeighbourhood = hasWay && !string.IsNullOrEmpty(exit.District) ? exit.District : string.Empty;   // the game reads none as its whole city
            }
            s.PutBacks.Add(() =>
            {
                car.streetName = street;
                car.streetNumber = number;
                car.parkingNeighbourhood = district;
                car.position = at;
                car.rotation = turn;
            });
        }

        // A contact's address at one of the mod's places is written as none, as a contact made without one.
        // save path: GameInstance.Contacts[].streetName
        // save path: GameInstance.Contacts[].streetNumber
        private static void Contacts(GameInstance save, PlaceSet places, Strip s)
        {
            List<Contact> contacts = save.Contacts;
            for (int i = 0; contacts != null && i < contacts.Count; i++)
            {
                Contact c = contacts[i];
                if (c == null || !places.Names(c.streetName, c.streetNumber))
                {
                    continue;
                }
                string street = c.streetName;
                int number = c.streetNumber;
                var record = new StoredRecord(SaveKind.ContactAddress, i, c.id);
                record.Addresses.Add(new PlaceKey(street, number));
                record.Values.Add(street);
                record.Values.Add(number.ToString(CultureInfo.InvariantCulture));
                s.Records.Add(record);
                s.StoodIn++;
                c.streetName = null;
                c.streetNumber = 0;
                s.PutBacks.Add(() =>
                {
                    c.streetName = street;
                    c.streetNumber = number;
                });
            }
        }

        // Of the staff, candidates and negotiations that stay: where one was found, if that was one of the mod's places
        // (the game names the finder's business from it), and an HR manager's plan that left.
        // save path: GameInstance.EmployeeInstances[].candidateInfo.sourceAddress
        // save path: GameInstance.EmployeeInstances[].assignedHrManagerPlanId
        // save path: GameInstance.CandidateEmployeeInstances[].assignedHrManagerPlanId
        // save path: GameInstance.candidateSalaryNegotiations[].employeeInstance.assignedHrManagerPlanId
        private static void Staying(GameInstance save, PlaceSet places, Strip s)
        {
            List<EmployeeInstance> staff = save.EmployeeInstances;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                Source(staff[i], "emp", places, s);
                HrPlan(staff[i], "emp", s);
            }
            List<EmployeeInstance> candidates = save.CandidateEmployeeInstances;
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                HrPlan(candidates[i], "cand", s);
            }
            List<CandidateSalaryNegotiation> talks = save.candidateSalaryNegotiations;
            for (int i = 0; s.HrPlans.Count > 0 && talks != null && i < talks.Count; i++)
            {
                if (talks[i] != null)
                {
                    HrPlan(talks[i].employeeInstance, "nego:" + talks[i].id, s);
                }
            }
        }

        private static void Source(EmployeeInstance e, string where, PlaceSet places, Strip s)
        {
            CandidateInfo info = e != null ? e.candidateInfo : null;
            Address from = info != null ? info.sourceAddress : null;
            if (!Names(places, from))
            {
                return;
            }
            var record = new StoredRecord(SaveKind.EmployeeSource, 0, where + "/" + e.id);
            record.Addresses.Add(new PlaceKey(from.streetName, from.streetNumber));
            record.Values.Add(from.streetName);
            record.Values.Add(from.streetNumber.ToString(CultureInfo.InvariantCulture));
            s.Records.Add(record);
            s.StoodIn++;
            info.sourceAddress = null;   // the game names no finder then (CandidateInfo.GetSource)
            s.PutBacks.Add(() => info.sourceAddress = from);
        }

        private static void HrPlan(EmployeeInstance e, string where, Strip s)
        {
            if (e == null || !In(s.HrPlans, e.assignedHrManagerPlanId))
            {
                return;
            }
            string plan = e.assignedHrManagerPlanId;
            var record = new StoredRecord(SaveKind.EmployeeHrPlan, 0, where + "/" + e.id);
            Carry(record, s.PlanPlaces, plan);
            record.Values.Add(plan);
            s.Records.Add(record);
            s.StoodIn++;
            e.assignedHrManagerPlanId = null;
            s.PutBacks.Add(() => e.assignedHrManagerPlanId = plan);
        }

        // A warehouse's van whose driver left, and a headhunter's place for an HR manager's plan that left, are written empty.
        // save path: GameInstance.BuildingRegistrations[]<Entities.Warehouse>.vehicleSlots[].employeeDriverId
        // save path: GameInstance.headhunterPlans[].assignedHrPlans[]
        private static void Slots(GameInstance save, Strip s)
        {
            List<BuildingRegistration> regs = save.BuildingRegistrations;
            for (int i = 0; s.Employees.Count > 0 && regs != null && i < regs.Count; i++)
            {
                if (!(regs[i] is Warehouse warehouse) || warehouse.vehicleSlots == null)
                {
                    continue;
                }
                for (int k = 0; k < warehouse.vehicleSlots.Count; k++)
                {
                    VehicleSlot slot = warehouse.vehicleSlots[k];
                    if (slot == null || !In(s.Employees, slot.employeeDriverId))
                    {
                        continue;
                    }
                    string driver = slot.employeeDriverId;
                    var record = new StoredRecord(SaveKind.DriverSlot, k, KeyOf(warehouse.StreetName, warehouse.StreetNumber));
                    Depends(record, s, driver);
                    record.Values.Add(driver);
                    s.Records.Add(record);
                    s.StoodIn++;
                    slot.employeeDriverId = null;
                    s.PutBacks.Add(() => slot.employeeDriverId = driver);
                }
            }
            List<HeadhunterPlan> headhunters = save.headhunterPlans;
            for (int i = 0; s.HrPlans.Count > 0 && headhunters != null && i < headhunters.Count; i++)
            {
                HeadhunterPlan p = headhunters[i];
                string[] hr = p != null ? p.assignedHrPlans : null;
                for (int k = 0; hr != null && k < hr.Length; k++)
                {
                    if (!In(s.HrPlans, hr[k]))
                    {
                        continue;
                    }
                    string plan = hr[k];
                    int at = k;
                    var record = new StoredRecord(SaveKind.HeadhunterHrPlan, k, p.id);
                    Carry(record, s.PlanPlaces, plan);
                    record.Values.Add(plan);
                    s.Records.Add(record);
                    s.StoodIn++;
                    hr[k] = null;
                    s.PutBacks.Add(() => hr[at] = plan);
                }
            }
        }

        // A spot as the store keeps it: the street and number, the position and turn, and what the place was.
        private static void Spot(List<string> values, string street, int number, SerializableVector3 at, SerializableQuaternion turn, string what)
        {
            values.Add(street);
            values.Add(number.ToString(CultureInfo.InvariantCulture));
            values.Add(DistrictSaveRules.Number(at.x));
            values.Add(DistrictSaveRules.Number(at.y));
            values.Add(DistrictSaveRules.Number(at.z));
            values.Add(DistrictSaveRules.Number(turn.x));
            values.Add(DistrictSaveRules.Number(turn.y));
            values.Add(DistrictSaveRules.Number(turn.z));
            values.Add(DistrictSaveRules.Number(turn.w));
            values.Add(what);
        }

        // Where the player or a car in a building of the mod's, or on its ground, is written: outside the building's own
        // door when that gives onto the game's streets; else the way out nearest to the door, or to where it stands.
        private static bool Leaving(PlaceSet places, bool inside, string street, int number, SerializableVector3 at, out PlaceExit spot, out bool doorOnGround)
        {
            doorOnGround = false;
            if (inside && places.DoorOf(street, number, out PlaceExit door))
            {
                if (!places.OnGround(door.X, door.Z))
                {
                    spot = door;
                    return true;
                }
                doorOnGround = true;
                if (!places.NearestExit(door.X, door.Z, out spot))
                {
                    spot = door;   // no way out named: outside the door, on the mod's ground (the self-check says so)
                }
                return true;
            }
            return Way(places, inside ? Door(street, number) : (Vector3?)at, out spot);
        }

        // The way out nearest to a point, or the first way out when the point is not known.
        private static bool Way(PlaceSet places, Vector3? from, out PlaceExit exit)
        {
            if (from.HasValue && places.NearestExit(from.Value.x, from.Value.z, out exit))
            {
                return true;
            }
            return places.FirstExit(out exit);
        }

        // The door of a building of the mod's that no feature gave a door, where the city has it; none when it cannot be read.
        private static Vector3? Door(string street, int number)
        {
            try
            {
                Transform door = BuildingHelper.GetAddressEntranceTransform(new Address(street, number));
                return door != null ? door.position : (Vector3?)null;
            }
            catch (Exception)
            {
                return null;   // (a city without its scene objects, as the offline checks run it)
            }
        }

        // A turn about the up axis, as the game's rotations read (Quaternion.Euler(0, heading, 0)).
        private static SerializableQuaternion Facing(float heading)
        {
            DistrictSaveRules.Yaw(heading, out float y, out float w);
            return new SerializableQuaternion(0f, y, 0f, w);
        }
    }
}
