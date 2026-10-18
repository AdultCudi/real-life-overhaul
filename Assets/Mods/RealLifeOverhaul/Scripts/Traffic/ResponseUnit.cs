using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealLifeOverhaul
{
    /// <summary>What a unit is dressed as.</summary>
    public enum UnitKind
    {
        Plain,      // an ordinary car driven by the mod: no lights, no siren (a fleeing suspect)
        Ambulance,
        Police
    }

    /// <summary>
    /// One traffic car taken from the traffic AI and driven by the mod (an ambulance, a patrol car, a suspect's car): put on the
    /// road from the pool at the start of a route, or taken over where it stands; dressed for its kind; driven by its own
    /// EmergencyDriver; handed back or removed cleanly. Made once and reused; idle it holds nothing. Its crew is its owner's.
    /// </summary>
    public sealed class ResponseUnit
    {
        private const float KitLinger = 9f;   // seconds the lights stay on a car that drives off
        private const int RouteTries = 4;     // random walks back from the place before it is said to have no road leading to it
        private const int StartTries = 3;     // road points at the start of a route tried for a free one

        private static readonly List<Waypoint> RouteScratch = new List<Waypoint>();
        private static bool _poolLogged;
        private static bool _bodyLogged;

        public static IModLogger Logger;

        private readonly List<int> _preset = new List<int>();   // marks the car as ours; see Hold

        private GameObject _kit;
        private GameObject _body;
        private AudioSource _siren;
        private AiCarRescueCheck _rescue;
        private bool _wasKinematic;

        public ResponseUnit(GameObject host)
        {
            Driver = host.AddComponent<EmergencyDriver>();
        }

        public EmergencyDriver Driver { get; }
        public VehicleComponent Car { get; private set; }
        public UnitKind Kind { get; private set; }
        public bool Parked { get; private set; }

        /// <summary>Why the last Spawn put nothing on the road, for the log; null if it did.</summary>
        public string Refusal { get; private set; }

        /// <summary>The road points the driver follows. Filled by Spawn; the owner may add to it at any time.</summary>
        public List<Vector3> Path { get; } = new List<Vector3>(128);

        public bool InUse { get; private set; }

        /// <summary>True if the unit's car was taken away: removed by the game, or already reused by the pool.</summary>
        public bool IsLost => InUse && !TrafficTools.IsHeld(Car);

        /// <summary>Puts a vehicle of the kind on the road some way back from the anchor and fills Path to it. False with no road, no car, or no free start.</summary>
        public bool Spawn(UnitKind kind, Waypoint anchor, float routeLength, float minimumLength)
        {
            Refusal = null;
            if (InUse || anchor == null || !TrafficTools.Ready)
            {
                Refusal = InUse ? "the unit is still out" : anchor == null ? "there is no road point to head for" : "traffic is not running";
                return false;
            }

            // Walked back from the anchor so the route is drivable; a walk turns at random and may hit a dead end, so it is tried again.
            float walked = 0f;
            for (int attempt = 0; attempt < RouteTries && walked < minimumLength; attempt++)
            {
                walked = TrafficTools.WalkBack(anchor, routeLength, 160, RouteScratch);
            }
            if (walked < minimumLength || RouteScratch.Count == 0)
            {
                Refusal = "the road leading there is only " + walked.ToString("F0") + " m long in " + RouteTries + " tries (" + minimumLength.ToString("F0") + " m needed)";
                RouteScratch.Clear();
                return false;
            }
            GameObject prefab = kind == UnitKind.Ambulance ? PickAmbulancePrefab() : PickPatrolPrefab();
            if (prefab == null)
            {
                Refusal = kind == UnitKind.Ambulance ? "the traffic pool has no ambulance free" : "the traffic pool has no idle car to make a patrol car of";
                RouteScratch.Clear();
                return false;
            }
            // The start of the route may be taken (often by the other unit): the next points along it are tried too.
            VehicleComponent car = null;
            int start = 0;
            for (int i = 0; i < StartTries && i < RouteScratch.Count - 2 && car == null; i++)
            {
                start = i;
                car = TrafficTools.Load(prefab, RouteScratch[i]);   // an idle car of that prefab, never one taken by its model
            }
            if (car == null)
            {
                Refusal = "the first " + StartTries + " road points of its route are all taken";
                RouteScratch.Clear();
                return false;
            }

            Path.Clear();
            for (int i = start + 1; i < RouteScratch.Count; i++)
            {
                Path.Add(RouteScratch[i].position);
            }
            RouteScratch.Clear();

            Hold(car, kind);
            if (!Dress())
            {
                // A patrol car that cannot be given its police body would be a civilian car with a siren: not sent.
                if (!_bodyLogged)
                {
                    _bodyLogged = true;
                    Logger?.Warn($"Police: '{car.prefab.name}' could not be given the police car body; no patrol car is sent. (Logged once.)");
                }
                Release(resumeTraffic: true, remove: true);
                Refusal = "the car could not be given the police car body";
                return false;
            }
            return true;
        }

        /// <summary>Takes over a traffic car where it stands. It keeps its looks; Path starts empty.</summary>
        public bool Take(VehicleComponent car)
        {
            if (InUse || car == null || !car.gameObject.activeInHierarchy)
            {
                return false;
            }
            Path.Clear();
            Hold(car, UnitKind.Plain);
            return true;
        }

        // A scripted route, even an empty one, stops the game despawning it by distance or sweeping it away with traffic.
        private void Hold(VehicleComponent car, UnitKind kind)
        {
            Car = car;
            Kind = kind;
            InUse = true;
            Parked = false;
            Manager.SetVehicleIgnored(car.GetIndex(), true);
            _preset.Clear();
            car.presetPath = _preset;
            _rescue = TrafficTools.SuspendRescue(car);
            TrafficTools.Touch(car, "taken out of traffic as a unit");
        }

        private static readonly int HotDress = HotSpots.Slot("Traffic: ResponseUnit.Dress");
        private bool Dress()
        {
            using var hotSpot = HotSpots.Time(HotDress);
            Driver.Lit = true;   // its lights are on wherever it drives: an accident's officer on traffic waves it through
            if (Kind == UnitKind.Ambulance)
            {
                _kit = TrafficEffects.CreateFlasher(Car, "RealLifeOverhaul Emergency Lights", TrafficEffects.BeaconHeight(Car),
                    new Color(1f, 0.1f, 0.1f), new Color(0.15f, 0.3f, 1f), 0.18f);
                if (Settings.Siren)
                {
                    _siren = TrafficEffects.AddSiren(_kit, Car, SirenTone.Wail);
                }
                return true;
            }

            // Police: always the game's own police car body, fitted over the sedan.
            _body = TrafficEffects.CreatePoliceBody(Car);
            if (_body == null)
            {
                return false;
            }
            float roof = TrafficEffects.RoofHeight(Car);   // measured from the police body
            _kit = TrafficEffects.CreateFlasher(Car, "RealLifeOverhaul Police Lights", roof + 0.15f,
                new Color(1f, 0.08f, 0.08f), new Color(0.1f, 0.25f, 1f), 0.14f);
            if (Settings.Siren)
            {
                _siren = TrafficEffects.AddSiren(_kit, Car, SirenTone.Yelp);
            }
            return true;
        }

        /// <summary>The unit stops where it will stay a while: pinned (kinematic), siren quiet, lights on; the moment its crew steps out.</summary>
        /// <param name="keepSiren">Standing only for a moment with its work not done: the siren stays on.</param>
        public void Park(bool keepSiren = false)
        {
            if (!InUse)
            {
                return;
            }
            if (Parked)
            {
                if (_siren != null && !keepSiren)
                {
                    _siren.Stop();   // it was only standing for a moment; now it stays
                }
                return;
            }
            if (IsLost)
            {
                Driver.Forget();
                return;
            }
            Driver.Stop();
            Rigidbody body = Car.rb;
            _wasKinematic = body.isKinematic;
            body.isKinematic = true;
            Parked = true;
            TrafficTools.Touch(Car, "parked by the mod (kinematic)");
            if (_siren != null && !keepSiren)
            {
                _siren.Stop();
            }
        }

        /// <summary>Undoes Park so the unit can be driven again; the siren comes back on unless it is to leave quietly.</summary>
        public void Unpark(bool quiet = false)
        {
            if (!InUse || !Parked)
            {
                return;
            }
            Parked = false;
            if (Car != null)
            {
                Car.rb.isKinematic = _wasKinematic;
                TrafficTools.Touch(Car, "unparked by the mod");
            }
            if (_siren != null && !quiet && !_siren.isPlaying)
            {
                _siren.Play();
            }
        }

        /// <summary>Lets the car go and takes off everything put on it; safe in any state, even with the car removed or reused by the game.</summary>
        /// <param name="resumeTraffic">False when traffic itself has been shut down (indoors, fast-forward).</param>
        /// <param name="remove">Take the car off the road instead of letting it drive away.</param>
        /// <param name="quiet">Drive away with the lights on but the siren off.</param>
        /// <param name="rolling">Back to traffic at the speed it has (TrafficTools.ReturnToTraffic says when that is sound).</param>
        public void Release(bool resumeTraffic, bool remove, bool quiet = false, bool rolling = false)
        {
            if (!InUse)
            {
                return;
            }
            VehicleComponent car = Car;
            bool held = TrafficTools.IsHeld(car);
            bool drivesOff = held && resumeTraffic && !remove && TrafficTools.Ready;
            // (Read before the driver is stopped, which leaves the body with no motion at all.)
            Vector3 carry = rolling && drivesOff && car != null && car.rb != null && !car.rb.isKinematic ? car.rb.velocity : Vector3.zero;

            Unhold(car, held);

            // A unit that drives away keeps its lights and siren for a few seconds, and a patrol
            // car keeps its police body until the game takes the car off the road (the body
            // removes itself then). In every other case it all comes off now.
            if (_body != null && !drivesOff)
            {
                Object.Destroy(_body);
            }
            if (_kit != null)
            {
                if (drivesOff)
                {
                    if (_siren != null)
                    {
                        if (quiet)
                        {
                            _siren.Stop();
                        }
                        else if (!_siren.isPlaying)
                        {
                            _siren.Play();
                        }
                    }
                    Object.Destroy(_kit, KitLinger);
                }
                else
                {
                    Object.Destroy(_kit);
                }
            }

            if (held && TrafficManager.HasInstance && TrafficManager.IsInitialized)
            {
                if (drivesOff)
                {
                    TrafficTools.ReturnToTraffic(car, Label(), carry);
                }
                else
                {
                    // Towed, not needed or traffic shut down: a car left here would sit on the road for good.
                    TrafficTools.Remove(car, Label());
                }
            }

            Clear();
        }

        /// <summary>Lets go of the car where it stands for another holder to take at once, still out of traffic; null if it was no longer ours.</summary>
        public VehicleComponent HandOver()
        {
            if (!InUse)
            {
                return null;
            }
            VehicleComponent car = Car;
            bool held = TrafficTools.IsHeld(car);
            Unhold(car, held);
            if (_body != null)
            {
                Object.Destroy(_body);
            }
            if (_kit != null)
            {
                Object.Destroy(_kit);
            }
            if (held)
            {
                TrafficTools.Touch(car, "handed from a unit to a scene");
            }
            Clear();
            return held ? car : null;
        }

        private void Unhold(VehicleComponent car, bool held)
        {
            if (held)
            {
                Driver.Stop();
            }
            else
            {
                Driver.Forget();
            }
            if (car != null)
            {
                if (Parked)
                {
                    car.rb.isKinematic = _wasKinematic;
                }
                if (car.presetPath == _preset)
                {
                    car.presetPath = null;
                }
            }
            if (_rescue != null)
            {
                _rescue.enabled = true;
            }
        }

        private void Clear()
        {
            Driver.Lit = false;
            Car = null;
            _kit = null;
            _body = null;
            _siren = null;
            _rescue = null;
            InUse = false;
            Parked = false;
            Path.Clear();
        }

        private string Label()
        {
            return Kind == UnitKind.Ambulance ? "an ambulance released" : Kind == UnitKind.Police ? "a patrol car released" : "a car the mod was driving released";
        }

        // The game's traffic has an ambulance, and nothing else will do: an idle one of the pool, else one driving as plain
        // traffic out of sight, that very car taken off the road first. Never by its model: LoadVehicle would take any
        // ambulance of the pool, whatever it is doing (TrafficTools.Load).
        private static GameObject PickAmbulancePrefab()
        {
            List<VehicleComponent> pool = Manager.GetVehicleList();
            LogPool(pool);
            VehicleComponent driving = null;
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (!TrafficTools.IsAmbulance(car))
                {
                    continue;
                }
                if (!car.gameObject.activeInHierarchy)
                {
                    return car.prefab;
                }
                if (driving == null && car.presetPath == null && !TrafficManager.Instance.IsVehicleIgnored(car.GetIndex())
                    && car.visibilityScript != null && car.visibilityScript.IsNotInView() && !CitySpots.InView(car.transform.position))
                {
                    driving = car;
                }
            }
            if (driving == null)
            {
                return null;
            }
            TrafficTools.Remove(driving, "an ambulance taken from out of sight to answer a call");
            return driving.gameObject.activeInHierarchy ? null : driving.prefab;
        }

        // No police car in the traffic pool: the TiaraVic sedan stands in, else another idle car, so two patrol cars never share one.
        private static GameObject PickPatrolPrefab()
        {
            List<VehicleComponent> pool = Manager.GetVehicleList();
            LogPool(pool);

            GameObject fallback = null;
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (car == null || car.prefab == null || car.gameObject.activeInHierarchy
                    || car.vehicleType != VehicleTypes.Car || car.GetComponent<TaxiController>() != null)
                {
                    continue;
                }
                string name = car.prefab.name;
                if (name.IndexOf("TiaraVic", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return car.prefab;
                }
                if (fallback == null && name.IndexOf("Taxi", StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf("Ambulance", StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf("Limo", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    fallback = car.prefab;
                }
            }
            return fallback;
        }

        private static void LogPool(List<VehicleComponent> pool)
        {
            if (_poolLogged)
            {
                return;
            }
            _poolLogged = true;
            var names = new HashSet<string>();
            foreach (VehicleComponent car in pool)
            {
                if (car != null && car.prefab != null)
                {
                    names.Add(car.prefab.name);
                }
            }
            Logger?.Info("Accident: traffic pool vehicles: " + string.Join(", ", names));
        }
    }
}
