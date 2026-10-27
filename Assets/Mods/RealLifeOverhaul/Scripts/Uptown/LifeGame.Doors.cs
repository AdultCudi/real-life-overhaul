using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's warehouses on the game's side, for the checks and the test that drives a car in (docs/features/uptown.md,
    /// "Garage doors"): whether a warehouse's garage doors are whole as the game's own are, what stands in the mouth of one, the car the
    /// player drives, the warehouse nearest it, its vehicle slots and how many are taken. Nothing here runs a frame: asked at a
    /// load, on a stand and by the test.
    /// </summary>
    internal static partial class LifeGame
    {
        private const float GateStep = 9f;      // m in front of a garage door the test puts the car, facing it
        private const float MouthTall = 1.2f;   // m above the ground a mouth is looked through
        private static int _mouthMask;

        /// <summary>The whole-building facts of a warehouse's vehicle entrances alone (the rest of the facts are left unset).</summary>
        public static CompleteFacts EntranceFacts(MadeBuilding made)
        {
            CompleteFacts f = default;
            Entrances(made, ref f);
            return f;
        }

        /// <summary>Why a made building's garage doors are not whole as the game's own warehouse's are, or null when they are (English, for a check line).</summary>
        public static string DoorsWhy(MadeBuilding made)
        {
            if (made == null || made.Controller == null || made.Building == null || made.Registration == null)
            {
                return "it does not stand at this load";
            }
            CompleteFacts f = EntranceFacts(made);
            if (!f.Warehouse)
            {
                return "it is no warehouse";
            }
            if (!f.WarehouseRegistration)
            {
                return "its registration is no warehouse's, so it has no vehicle slots";
            }
            if (f.GatesWanted == 0)
            {
                return "its structure has no vehicle slot";
            }
            if (f.GatesWhole != f.GatesWanted)
            {
                return "only " + f.GatesWhole + " of its structure's " + f.GatesWanted + " vehicle slots have a garage door whole (a component, its trigger and the closed door)";
            }
            if (!f.GatesServe)
            {
                return "a garage door of its look looks out on a wall, not on its street";
            }
            string blocker = Blocker(made);
            return blocker != null ? "the mouth of a garage door is blocked by " + blocker : null;
        }

        /// <summary>The name of the first thing that stands in the mouth of one of a made building's garage doors (a lamp post, a piece of furniture, a parked car), or null.</summary>
        public static string Blocker(MadeBuilding made)
        {
            DriveInEntrance[] doors = made != null && made.Controller != null ? made.Controller.driveInEntrances : null;
            if (_mouthMask == 0)
            {
                _mouthMask = LayerMask.GetMask("StreetProps", "InteractiveItems", "InteractiveItemsOutlined", "ParkedVehicles");   // what stays; the traffic passing by does not count
            }
            var rects = new List<GroundRect>(2);
            var hits = new Collider[8];
            for (int i = 0; doors != null && i < doors.Length; i++)
            {
                if (doors[i] == null)
                {
                    continue;
                }
                rects.Clear();
                Transform t = doors[i].transform;
                MouthRules.Gate(t.position.x, t.position.z, WayOfDoor(t.forward), BoxWidth(doors[i]), rects);
                GroundRect m = rects[0];
                var centre = new Vector3((m.X1 + m.X2) * 0.5f, t.position.y + MouthTall, (m.Z1 + m.Z2) * 0.5f);
                int found = Physics.OverlapBoxNonAlloc(centre, new Vector3((m.X2 - m.X1) * 0.5f, 1f, (m.Z2 - m.Z1) * 0.5f), hits, Quaternion.identity, _mouthMask, QueryTriggerInteraction.Ignore);
                if (found > 0 && hits[0] != null)
                {
                    return hits[0].name;
                }
            }
            return null;
        }

        private static int WayOfDoor(Vector3 forward)
        {
            return Mathf.Abs(forward.z) >= Mathf.Abs(forward.x) ? (forward.z >= 0f ? 0 : 2) : (forward.x >= 0f ? 1 : 3);
        }

        private static float BoxWidth(DriveInEntrance door)
        {
            foreach (BoxCollider box in door.GetComponents<BoxCollider>())
            {
                if (!box.isTrigger)
                {
                    return box.size.x * Mathf.Abs(door.transform.lossyScale.x);
                }
            }
            return 3.25f;
        }

        /// <summary>True when the player is at the wheel of a car (not on a bike or a scooter, which have no garage to be taken into).</summary>
        public static bool DrivesCar(out CarController car)
        {
            GameManager game = InstanceBehavior<GameManager>.IsInitialized ? InstanceBehavior<GameManager>.Instance : null;
            car = PlayerHelper.IsUsingVehicle && game != null ? game.selectedVehicle as CarController : null;
            return car != null && car.vehicleType != null && !car.vehicleType.spawnInPlayerObject && car.vehicleCollider != null;
        }

        /// <summary>
        /// The district's warehouse nearest a point with a garage door whole: one the player rents first (the game takes a car into no
        /// other), else the nearest of any; its first garage door. Null where none stands.
        /// </summary>
        public static MadeBuilding NearestWarehouse(IList<MadeBuilding> standing, Vector3 from, out DriveInEntrance gate)
        {
            gate = null;
            MadeBuilding best = null;
            float bestAway = float.MaxValue;
            bool bestMine = false;
            for (int i = 0; standing != null && i < standing.Count; i++)
            {
                MadeBuilding made = standing[i];
                DriveInEntrance[] doors = made != null && made.Controller != null && made.Registration is Warehouse ? made.Controller.driveInEntrances : null;
                if (doors == null || doors.Length == 0 || doors[0] == null)
                {
                    continue;
                }
                bool mine = made.Registration.RentedByPlayer && !GoingPublic.Foreign(made.Registration);
                float dx = doors[0].transform.position.x - from.x, dz = doors[0].transform.position.z - from.z, away = dx * dx + dz * dz;
                if ((mine && !bestMine) || (mine == bestMine && away < bestAway))
                {
                    best = made;
                    gate = doors[0];
                    bestAway = away;
                    bestMine = mine;
                }
            }
            return best;
        }

        /// <summary>A warehouse's vehicle slots as its registration has them, and how many hold a vehicle that is at its address; (0, 0) before it is rented.</summary>
        public static void SlotsOf(MadeBuilding made, out int slots, out int taken)
        {
            slots = taken = 0;
            if (!(made != null && made.Registration is Warehouse warehouse) || warehouse.vehicleSlots == null)
            {
                return;
            }
            slots = warehouse.vehicleSlots.Count;
            for (int i = 0; i < slots; i++)
            {
                string id = warehouse.vehicleSlots[i] != null ? warehouse.vehicleSlots[i].vehicleInstanceId : null;
                taken += !string.IsNullOrEmpty(id) && BuildingHelper.IsVehicleAtAddress(id, made.Building.Address) ? 1 : 0;
            }
        }

        /// <summary>The vehicle slots the game gives the warehouse's structure (BuildingSizeData.numberOfVehicleSlots).</summary>
        public static int StructureSlots(MadeBuilding made)
        {
            BuildingSizeData data = made != null && made.Building != null ? BuildingSizeHelper.GetData(made.Building) : null;
            return data != null ? data.numberOfVehicleSlots : 0;
        }

        /// <summary>The player's car put in front of a garage door, facing it, on the ground, the distance a car comes in from.</summary>
        public static void DrawUpAt(CarController car, DriveInEntrance gate)
        {
            Vector3 outward = Vector3.ProjectOnPlane(gate.transform.forward, Vector3.up).normalized;
            VehicleHelper.TeleportVehicleToGround(car, gate.transform.position + outward * GateStep, Quaternion.LookRotation(-outward));
        }

        /// <summary>The metres between a car and a garage door.</summary>
        public static float Away(CarController car, DriveInEntrance gate)
        {
            return car != null && gate != null ? Vector3.Distance(car.transform.position, gate.transform.position) : float.MaxValue;
        }
    }
}
