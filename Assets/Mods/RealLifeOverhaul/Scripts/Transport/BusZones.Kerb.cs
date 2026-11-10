using System;
using System.Collections.Generic;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The bus zones' part for every path that stops a car at a kerb. Each place the buses stop at has a zone (BusZoneRules), laid
    /// out once into a table of rectangles when the places are made; <see cref="Holds"/> reads it with no allocation and no
    /// physics, and is what every path of the mod asks before it stands a car there. The game's own cars come in two ways, both
    /// closed here whatever the lane beside the zone is: the ones standing there are taken in at once (<see cref="TakeIn"/>), and
    /// each lane beside it is gated, so a car it makes later is switched off as it is made (<see cref="Hush"/>, on the lane's own
    /// onGenerateVehicle, the event the game hides cars by itself). Queries on events and the director's timer only.
    /// </summary>
    public static partial class BusZones
    {
        private const float StripLow = 0.9f;      // metres above the lane the middle of the box a zone is looked at by stands
        private const float StripHalf = 1.25f;    // and half its height: a car's body, from under its sills to over its roof

        private static BusZoneRules.Zone[] _table = new BusZoneRules.Zone[128];
        private static TransitStop[] _tableStops = new TransitStop[128];
        private static int _tableCount;
        private static readonly Collider[] KerbFound = new Collider[48];
        private static readonly List<ParkingLaneGenerator> Gated = new List<ParkingLaneGenerator>(32);
        private static readonly List<ParkingLaneGenerator> Counted = new List<ParkingLaneGenerator>(8);
        private static readonly List<GameObject> Hushed = new List<GameObject>(16);
        private static readonly Action<GameObject> GateHandler = Guard.Wrap<GameObject>(Hush);   // made once: the lanes hold it, and take it off again by it

        /// <summary>Delivery job vans' spots a stop was refused for, since the city loaded: the stop avoids what the game keeps standing.</summary>
        public static int Avoided { get; private set; }

        /// <summary>A new layout of the places begins: what the last one avoided is forgotten.</summary>
        public static void NewLayout()
        {
            Avoided = 0;
        }

        private static int ParkedMask => 1 << LayerHelper.ParkedVehiclesLayerIndex;

        // Every place's zone, once the places are made: a standing zone where there is no bay, and the table Holds reads.
        private static void Lay()
        {
            _tableCount = 0;
            for (int i = 0; i < TransitMap.Places.Count; i++)
            {
                TransitStop stop = TransitMap.Places[i];
                if (stop == null)
                {
                    continue;
                }
                if (stop.Zone == null)
                {
                    BusZoneRules.Standing(stop.Stand, out float from, out float to);
                    stop.Zone = new BusZone { From = from, To = to, Kerb = stop.KerbAside, Standing = true };
                }
                if (_tableCount >= _table.Length)
                {
                    Array.Resize(ref _table, _table.Length * 2);
                    Array.Resize(ref _tableStops, _tableStops.Length * 2);
                }
                _table[_tableCount] = BusZoneRules.Zone.Make(stop.Lane.x, stop.Lane.y, stop.Lane.z, stop.Along.x, stop.Along.z, stop.Zone.From, stop.Zone.To, stop.Zone.Kerb);
                _tableStops[_tableCount] = stop;
                _tableCount++;
            }
        }

        /// <summary>How many places' zones the table holds.</summary>
        public static int Zones => _tableCount;

        /// <summary>
        /// True if a vehicle standing there, its middle, the way it points, half its length and half its width, reaches into the zone of
        /// any bus stop: the question every path that stops a car at a kerb asks. No allocation, no physics; false before the places are made.
        /// </summary>
        public static bool Holds(Vector3 centre, Vector3 forward, float halfLength, float halfWidth)
        {
            return HeldAt(centre, forward, halfLength, halfWidth) != null;
        }

        /// <summary>The place whose zone a vehicle's footprint reaches into, for a log line; null if none.</summary>
        public static TransitStop HeldAt(Vector3 centre, Vector3 forward, float halfLength, float halfWidth)
        {
            for (int i = 0; i < _tableCount; i++)
            {
                if (BusZoneRules.Holds(in _table[i], centre.x, centre.y, centre.z, forward.x, forward.z, halfLength, halfWidth))
                {
                    return _tableStops[i];
                }
            }
            return null;
        }

        // The box a zone is looked at by: its strip, margins included, from the lane's edge to the kerb, a car's height.
        private static void Strip(TransitStop stop, out Vector3 centre, out Vector3 half, out Quaternion turn)
        {
            BusZone zone = stop.Zone;
            float from = zone.From - Margin;
            float to = zone.To + Margin;
            float outer = Mathf.Max(BusZoneRules.Inner + 0.1f, zone.Kerb + BusZoneRules.KerbSlack);
            Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
            centre = stop.Lane + stop.Along * ((from + to) * 0.5f) + right * ((BusZoneRules.Inner + outer) * 0.5f) + Vector3.up * StripLow;
            half = new Vector3((outer - BusZoneRules.Inner) * 0.5f, StripHalf, (to - from) * 0.5f);
            turn = Quaternion.LookRotation(stop.Along, Vector3.up);
        }

        // The game's parked cars standing in a zone, handed back to the game's store at once, in view or not: the game does the same to the
        // parked cars a car is put among (VehicleHelper.DestroyBlockingVehicles). Whatever lane they stand in. With release off, only counted.
        private static int Scan(TransitStop stop, bool release, out int others)
        {
            BusZone zone = stop.Zone;
            Strip(stop, out Vector3 centre, out Vector3 half, out Quaternion turn);
            int found = Physics.OverlapBoxNonAlloc(centre, half, KerbFound, turn, ParkedMask, QueryTriggerInteraction.Ignore);
            int taken = 0;
            others = 0;
            for (int i = 0; i < found; i++)
            {
                Collider hit = KerbFound[i];
                KerbFound[i] = null;
                if (hit == null || !hit.gameObject.activeInHierarchy)
                {
                    continue;
                }
                ParkingLaneGenerator lane = hit.GetComponentInParent<ParkingLaneGenerator>();
                Transform car = hit.transform;
                while (lane != null && car.parent != null && car.parent != lane.transform)
                {
                    car = car.parent;
                }
                if (lane == null || car.parent != lane.transform || car.gameObject.layer != LayerHelper.ParkedVehiclesLayerIndex)
                {
                    others++;   // not one of a lane's cars: the mod's own parked patrol car, whose placing asks Holds first
                    continue;
                }
                if (release)
                {
                    Release(car.gameObject, lane);
                }
                taken++;
            }
            if (release)
            {
                zone.Others = others;
            }
            return taken;
        }

        private static int TakeIn(TransitStop stop)
        {
            return Scan(stop, true, out _);
        }

        // Every parking lane whose box reaches into a zone, of any kind (plain, angled, curved, kept for particular vehicles, disabled
        // bays), gated: the cars it makes in the zone are switched off as they are made.
        private static void GateLanes(TransitStop stop)
        {
            BusZone zone = stop.Zone;
            Strip(stop, out Vector3 centre, out Vector3 half, out Quaternion turn);
            int found = Physics.OverlapBoxNonAlloc(centre, half, KerbFound, turn, ParkingMask, QueryTriggerInteraction.Collide);
            Counted.Clear();
            for (int i = 0; i < found; i++)
            {
                Collider hit = KerbFound[i];
                KerbFound[i] = null;
                ParkingLaneGenerator lane = hit != null ? hit.GetComponentInParent<ParkingLaneGenerator>() : null;
                if (lane == null || Counted.Contains(lane))
                {
                    continue;
                }
                Counted.Add(lane);
                Join(lane);
            }
            zone.Gated = Counted.Count;
            Counted.Clear();
        }

        // The lane's own event for a car it has made: ours is combined with the game's own handlers, and taken off again by the same delegate.
        private static void Join(ParkingLaneGenerator lane)
        {
            if (lane == null || Gated.Contains(lane))
            {
                return;
            }
            if (Gated.Count >= 64)
            {
                Gated.RemoveAll(gone => gone == null);
            }
            lane.onGenerateVehicle = (Action<GameObject>)Delegate.Combine(lane.onGenerateVehicle, GateHandler);
            Gated.Add(lane);
        }

        /// <summary>
        /// A parking lane the mod has stood (new ground, an uptown module, a car park, a made building's own): gated if its box reaches into
        /// a zone, so the cars it parks never stand in one. Called before the lane wakes; safe on null and before the places are made.
        /// </summary>
        public static void Meet(ParkingLaneGenerator lane)
        {
            if (lane == null || _tableCount == 0)
            {
                return;
            }
            BoxCollider box = lane.GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }
            Transform frame = lane.transform;
            Vector3 scale = frame.lossyScale;
            float alongZ = box.size.z * Mathf.Abs(scale.z);
            float alongX = box.size.x * Mathf.Abs(scale.x);
            Vector3 way = alongZ >= alongX ? frame.forward : frame.right;
            if (Holds(frame.TransformPoint(box.center), way, Mathf.Max(alongZ, alongX) * 0.5f, Mathf.Min(alongZ, alongX) * 0.5f))
            {
                Join(lane);
            }
        }

        // A car the game has just made in a gated lane: switched off if it stands in a zone. Not released: the lane's own loop still holds it
        // (its minimum of free spots may hand it to the store itself), and the game takes it in with the lane's other cars when it cleans the lane.
        private static void Hush(GameObject car)
        {
            if (car == null || _tableCount == 0)
            {
                return;
            }
            Transform frame = car.transform;
            MeshCollider body = car.GetComponent<MeshCollider>();
            Bounds local = body != null && body.sharedMesh != null ? body.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(2f, 1.5f, 5f));
            TransitStop held = HeldAt(frame.TransformPoint(local.center), frame.forward, local.extents.z, local.extents.x);
            if (held == null)
            {
                return;
            }
            car.SetActive(false);
            if (Hushed.Count >= 256)
            {
                Hushed.RemoveRange(0, 128);
            }
            Hushed.Add(car);
            if (held.Zone != null)
            {
                held.Zone.Hushed++;
            }
        }

        // The gates off the lanes, and the cars they switched off back on while they still stand in their lanes (the game's store may have taken them since).
        private static void Ungate(bool unloading)
        {
            for (int i = 0; i < Gated.Count; i++)
            {
                if (Gated[i] != null)
                {
                    Gated[i].onGenerateVehicle = (Action<GameObject>)Delegate.Remove(Gated[i].onGenerateVehicle, GateHandler);
                }
            }
            Gated.Clear();
            for (int i = 0; !unloading && i < Hushed.Count; i++)
            {
                GameObject car = Hushed[i];
                if (car != null && !car.activeSelf && car.layer == LayerHelper.ParkedVehiclesLayerIndex && car.transform.parent != null
                    && car.transform.parent.GetComponent<ParkingLaneGenerator>() != null)
                {
                    car.SetActive(true);
                }
            }
            Hushed.Clear();
        }

        /// <summary>
        /// Why a place cannot keep its kerb clear and the stop should be somewhere else: a delivery job's van stands at the start of a lane in
        /// it, which the game keeps there and brings back (ParkingLaneGenerator.designatedDeliveryVanSpot). Null if it can. Once a stop laid
        /// out, one physics box; for a place without a bay, since a bay's own plan refuses such a lane (BusZones.Plan).
        /// </summary>
        public static string Unclearable(TransitStop stop, Vector3 right)
        {
            BusZoneRules.Standing(stop.Stand, out float from, out float to);
            from -= Margin;
            to += Margin;
            float outer = Mathf.Max(BusZoneRules.Inner + 0.1f, stop.KerbAside + BusZoneRules.KerbSlack);
            Vector3 centre = stop.Lane + stop.Along * ((from + to) * 0.5f) + right * ((BusZoneRules.Inner + outer) * 0.5f) + Vector3.up * StripLow;
            int found = Physics.OverlapBoxNonAlloc(centre, new Vector3((outer - BusZoneRules.Inner) * 0.5f, StripHalf, (to - from) * 0.5f), KerbFound,
                Quaternion.LookRotation(stop.Along, Vector3.up), ParkingMask, QueryTriggerInteraction.Collide);
            string why = null;
            for (int i = 0; i < found; i++)
            {
                Collider hit = KerbFound[i];
                KerbFound[i] = null;
                ParkingLaneGenerator lane = hit != null ? hit.GetComponentInParent<ParkingLaneGenerator>() : null;
                if (why == null && lane != null && HoldsVan(lane))
                {
                    why = "a delivery van's spot lies in its kerb";
                }
            }
            Avoided += why != null ? 1 : 0;
            return why;
        }

        // A lane the game keeps a delivery job's van at: its own flag, or a building's delivery start (the game reads the building's registration for the second).
        private static bool HoldsVan(ParkingLaneGenerator lane)
        {
            try
            {
                return lane.designatedDeliveryVanSpot || lane.ContainsDeliveryVehicle();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return lane.designatedDeliveryVanSpot;
            }
        }

        // Once, when every zone is made: what is still in a zone, counted by the same box the sweep uses. One line.
        private static void Audit()
        {
            int places = TransitMap.Places.Count, zones = 0, bays = 0, standing = 0, gated = 0, cars = 0, others = 0;
            for (int i = 0; i < TransitMap.Places.Count; i++)
            {
                TransitStop stop = TransitMap.Places[i];
                if (stop == null || stop.Zone == null)
                {
                    continue;
                }
                zones++;
                bays += stop.Zone.Standing ? 0 : 1;
                standing += stop.Zone.Standing ? 1 : 0;
                gated += stop.Zone.Gated;
                cars += Scan(stop, false, out int other);
                others += other;
            }
            bool pass = zones == places && cars == 0 && _tableCount == zones;
            Logger?.Info("Transit check (kerbs): " + places + " place(s), " + zones + " with a bus zone (" + bays + " bays, " + standing + " standing), " + _tableCount
                         + " in the table every path asks; " + gated + " parking lane(s) gated; " + cars + " of the game's parked car(s) in a zone after the sweep, "
                         + others + " other thing(s) on the parked-vehicle layer in one (the mod's own parked patrol cars, placed by Holds), " + Avoided
                         + " stop(s) refused for a delivery van's spot: " + (pass ? "PASS" : "FAIL") + ".");
        }
    }
}
