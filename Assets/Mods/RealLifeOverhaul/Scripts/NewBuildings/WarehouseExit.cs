using System;
using System.Collections.Generic;
using BAModAPI;
using Buildings;
using UnityEngine;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// A car put out of one of the mod's warehouses (docs/features/new-buildings.md, "Vehicle entrances"; a beta player, 28 Oct 2026: "now i
    /// can enter warehouses in a car but can't leave only on foot"). The game puts a car out at the door's spawn point inside the tunnel, 4.55 m in,
    /// with the middle of its mesh half a length from it (<c>DriveInEntrance.GetSpawnPositionInsideOfGarageDoor</c>), 0.32 m clear of the
    /// door's entrance trigger (<c>DriveInEntranceEnterTrigger</c>, which takes any car that touches it into the building). That holds for a car
    /// whose mesh is centred on its origin; a car whose mesh centre is behind its origin (a body modelled round its front axle, as some of
    /// the cars other mods add are) is put out with its tail in the trigger, and the trigger takes it straight back in: it can enter and never
    /// leave. This moves a car just put out of a made building along the door's way, out, by just the distance its body stands into the trigger
    /// (<see cref="GateRules.PushOut"/>) and no more: a car the game put out clear is not touched. The player's own car only, the moment the
    /// game's exit is over, once for each exit. COST: nothing between exits; one pass over the building's doors and their triggers an exit.
    /// </summary>
    internal static class WarehouseExit
    {
        public static IModLogger Logger;

        private static readonly List<DriveInEntranceEnterTrigger> Triggers = new List<DriveInEntranceEnterTrigger>(4);
        private static readonly List<Collider> Boxes = new List<Collider>(4);

        /// <summary>Cars put out so far this session that stood into a trigger and were moved.</summary>
        public static int Moved { get; private set; }

        /// <summary>The game has left a building (its onExitBuilding): if it is one of the mod's and the player drove out of it, the car is settled clear of the door's trigger.</summary>
        public static void Left(Address address)
        {
            MadeBuilding made = address != null ? MadeBuildings.At(address.streetName, address.streetNumber) : null;
            GameManager game = InstanceBehavior<GameManager>.IsInitialized ? InstanceBehavior<GameManager>.Instance : null;
            VehicleController vehicle = game != null ? game.selectedVehicle : null;
            if (made == null || made.Controller == null || vehicle == null || vehicle.vehicleType == null || vehicle.vehicleType.spawnInPlayerObject || vehicle.vehicleCollider == null)
            {
                return;
            }
            DriveInEntrance[] doors = made.Controller.driveInEntrances;
            if (doors == null || doors.Length == 0)
            {
                return;
            }
            Physics.SyncTransforms();   // the car was put here this frame: its collider's bounds are those of its new place
            Bounds body = vehicle.vehicleCollider.bounds;
            for (int d = 0; d < doors.Length; d++)
            {
                if (doors[d] == null)
                {
                    continue;
                }
                Transform door = doors[d].transform;
                Vector3 way = Vector3.ProjectOnPlane(door.forward, Vector3.up);
                if (way.sqrMagnitude < 0.0001f)
                {
                    continue;
                }
                way.Normalize();
                if (Mathf.Abs(way.x) < 0.9998f && Mathf.Abs(way.z) < 0.9998f)
                {
                    continue;   // a door not along the world's axes: a box's bounds are not its body, and the district's doors all are
                }
                Triggers.Clear();
                doors[d].GetComponentsInChildren(true, Triggers);
                for (int t = 0; t < Triggers.Count; t++)
                {
                    Boxes.Clear();
                    Triggers[t].GetComponents(Boxes);
                    for (int b = 0; b < Boxes.Count; b++)
                    {
                        if (!Boxes[b].isTrigger || !Boxes[b].enabled || !Boxes[b].bounds.Intersects(body))
                        {
                            continue;
                        }
                        float far = Along(Boxes[b].bounds, door.position, way, true), near = Along(body, door.position, way, false);
                        float push = GateRules.PushOut(near, far);
                        if (push > 0f)
                        {
                            vehicle.transform.position += way * push;
                            Physics.SyncTransforms();
                            body = vehicle.vehicleCollider.bounds;
                            Moved++;
                            if (Meta.Dev.Verbose)
                            {
                                string line = FormattableString.Invariant($"the car put out of {made.Address} stood {push:F2} m into the door's trigger");
                                Logger?.Info("Uptown check (warehouse exit): " + line + " (its mesh is centred behind its origin) and was moved clear: PASS");
                            }
                        }
                    }
                }
                Triggers.Clear();
                Boxes.Clear();
            }
        }

        // Where a box stands along the way out from the door: its door-ward edge (the nearest to the outside, <paramref name="outer"/>) or its inner edge.
        private static float Along(Bounds box, Vector3 from, Vector3 way, bool outer)
        {
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3((c & 1) == 0 ? box.min.x : box.max.x, (c & 2) == 0 ? box.min.y : box.max.y, (c & 4) == 0 ? box.min.z : box.max.z);
                float s = Vector3.Dot(corner - from, way);
                low = Mathf.Min(low, s);
                high = Mathf.Max(high, s);
            }
            return outer ? high : low;
        }
    }
}
