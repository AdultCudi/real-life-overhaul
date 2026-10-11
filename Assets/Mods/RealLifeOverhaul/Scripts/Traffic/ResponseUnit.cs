using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealLifeOverhaul
{
    public enum UnitKind
    {
        Plain,      // an ordinary car driven by the mod: no lights, no siren (a fleeing suspect)
        Ambulance,
        Police
    }

    /// <summary>
    /// One traffic car taken out of the traffic AI and driven by the mod: an ambulance, a patrol
    /// car, or a suspect's car. It is either spawned from the pool at the start of a route or
    /// taken over where it stands, gets the lights, siren and body its kind calls for, has its
    /// own EmergencyDriver, and is handed back or removed cleanly when released. The object is
    /// created once and reused; while idle it holds nothing and costs nothing.
    /// The unit is only the vehicle. Its crew are put on the road by whoever owns it, at the
    /// moment it parks (see AccidentCast and RoadsideStop).
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

        /// <summary>
        /// Puts a vehicle of the given kind on the road some way back from the anchor waypoint
        /// and fills Path with the road from there to the anchor. Does not start driving.
        /// </summary>
        /// <returns>False if there was no road, no free vehicle, or the spawn point was occupied.</returns>
        public bool Spawn(UnitKind kind, Waypoint anchor, float routeLength, float minimumLength)
        {
            Refusal = null;
            if (InUse || anchor == null || !TrafficTools.Ready)
            {
                Refusal = InUse ? "the unit is still out" : anchor == null ? "there is no road point to head for" : "traffic is not running";
                return false;
            }

            // Walk the road graph backwards from the anchor, so the route there is guaranteed drivable. The walk
            // takes a turn at random at every junction and gives up at a dead end, so a short one is tried again.
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
                car = TrafficManager.Instance.LoadVehicle(prefab, RouteScratch[i]);
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

        // We drive it ourselves. A scripted route (even an empty one) also stops the game
        // despawning it by distance and sweeping it away with ordinary traffic.
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

        // Lights, siren and (police) the body. False if the vehicle cannot be made to look its part.
        private bool Dress()
        {
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

        /// <summary>
        /// The unit has stopped where it will stay for a while: the car is pinned in place (so
        /// holding it costs nothing) and the siren goes quiet; the lights stay on.
        /// This is the moment its crew steps out; the owner sees to that.
        /// </summary>
        /// <param name="keepSiren">Standing only for a moment with its work not done (backed away from the tape, waiting to pull out): the siren stays on.</param>
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

        /// <summary>
        /// Lets the car go and takes off everything that was put on it. Safe to call in any
        /// state, including when the car has already been removed or reused by the game.
        /// </summary>
        /// <param name="resumeTraffic">False when traffic itself has been shut down (indoors, fast-forward).</param>
        /// <param name="remove">Take the car off the road instead of letting it drive away.</param>
        /// <param name="quiet">Drive away with the lights on but the siren off (a chase called off).</param>
        /// <param name="rolling">It goes back to traffic at the speed it has, instead of from rest (TrafficTools.ReturnToTraffic says when that is sound).</param>
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
                    // Towed, no longer needed, or traffic has shut down around it: a car left
                    // behind here would sit on the road for good.
                    TrafficTools.Remove(car, Label());
                }
            }

            Car = null;
            _kit = null;
            _body = null;
            _siren = null;
            _rescue = null;
            InUse = false;
            Parked = false;
            Path.Clear();
        }

        /// <summary>
        /// Lets go of the car where it stands for another holder to take in the same moment (an accident scene
        /// takes a car that has just crashed as one of its wrecks). The driver is stopped, what this unit put on the
        /// car comes off, and the car is left exactly as a holder finds any car it takes: still out of the traffic
        /// system's hands, able to move, with no scripted route of ours. Nothing is handed back to traffic and
        /// nothing is taken off the road: whoever is given the car must do one or the other.
        /// </summary>
        /// <returns>The car, or null if it was no longer ours to give (the pool had reused it).</returns>
        public VehicleComponent HandOver()
        {
            if (!InUse)
            {
                return null;
            }
            VehicleComponent car = Car;
            bool held = TrafficTools.IsHeld(car);
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
            Car = null;
            _kit = null;
            _body = null;
            _siren = null;
            _rescue = null;
            InUse = false;
            Parked = false;
            Path.Clear();
            return held ? car : null;
        }

        private string Label()
        {
            return Kind == UnitKind.Ambulance ? "an ambulance released" : Kind == UnitKind.Police ? "a patrol car released" : "a car the mod was driving released";
        }

        // The game's traffic includes an ambulance, and nothing else will do: a van with a beacon is not one.
        // LoadVehicle borrows a busy instance if no idle one exists, so the ambulance may be in use as traffic.
        private static GameObject PickAmbulancePrefab()
        {
            List<VehicleComponent> pool = Manager.GetVehicleList();
            LogPool(pool);
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (TrafficTools.IsAmbulance(car) && !TrafficManager.Instance.IsVehicleIgnored(car.GetIndex()))
                {
                    return car.prefab;
                }
            }
            return null;
        }

        // There is no police car in the traffic pool. The Crown-Victoria-style sedan stands in,
        // then any other idle car that is not a taxi, ambulance, limo or truck. Only idle
        // instances are considered, so a second patrol car never steals the first one's vehicle.
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
