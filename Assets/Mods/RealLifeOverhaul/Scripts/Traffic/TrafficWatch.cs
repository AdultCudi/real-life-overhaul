using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A self-check on the traffic: finds a vehicle that is hopping up and down on the spot and stops it.
    /// The traffic system's springs have no damper; what steadies a car is the traffic system replacing
    /// its whole velocity every physics step, which it does not do for a car it has been told to ignore,
    /// nor for one whose wheels have lost the road (standing on something). So a car the mod holds and
    /// neither drives nor freezes, or a car that has ended up on top of something solid, bounces for as
    /// long as it is left. The mod is written never to leave one like that; this is what notices if it
    /// does. Five times a second it reads the height of every vehicle on the road (one position each, no
    /// physics queries, nothing allocated); a vehicle that has gone up and down without going anywhere
    /// for two windows running (three seconds) is reported once, with what the mod last did to it and
    /// what it is standing on, and taken off the road if nobody is looking that way, or else frozen where
    /// it stands until nobody is. Ticked from TrafficAccidents.Update.
    /// It also watches the response vehicles it is told must not stand (Guard): an ambulance or a patrol car
    /// with its crew aboard, or one whose scene has ended. One of those that does not move a metre in 25 s is
    /// reported once and got off the road. One position a second per vehicle, and only while there is one.
    /// </summary>
    public static class TrafficWatch
    {
        private const float Interval = 0.2f;
        private const float Window = 1.5f;       // seconds a vehicle is watched before it is judged
        private const int Windows = 2;           // ...and how many in a row must say "bouncing"
        private const float MinTravel = 0.06f;   // metres up and down within a window
        private const float MinStep = 0.008f;    // a change of height smaller than this between two looks is no movement
        private const float MaxDrift = 1f;       // metres along the ground within a window: further than this it is driving, not hopping
        private const int MinTurns = 2;          // changes between rising and falling within a window

        private struct Track
        {
            public float Since;      // when this window began; 0 if the vehicle is not being watched
            public Vector3 Start;
            public float Height, Low, High;
            public sbyte Way;
            public byte Turns;
            public byte Bouncing;    // windows in a row
        }

        private struct Held
        {
            public VehicleComponent Car;
            public bool Ours;        // a scene of the mod's holds it: that scene lets it go, we only undo the freeze if it is left
        }

        public static IModLogger Logger => TrafficTools.Logger;

        private static Track[] _tracks = new Track[0];
        private static readonly List<Held> Frozen = new List<Held>(4);
        private static readonly Collider[] Under = new Collider[8];
        private static float _next;
        private static int _reports;
        private static int _underMask;

        public static void Tick(float now)
        {
            if (now < _next)
            {
                return;
            }
            _next = now + Interval;
            try
            {
                if (!TrafficTools.Ready)
                {
                    Thaw(all: true);
                    return;
                }
                Thaw(all: false);
                Look(now);
                if (_guards > 0 && now >= _nextGuard)
                {
                    _nextGuard = now + 1f;
                    LookAtGuarded(now);
                }
            }
            catch (Exception e)
            {
                // A self-check must never be the fault: it says so and waits a good while before looking again.
                Logger?.Error(e);
                _next = now + 30f;
            }
        }

        // ---------------------------------------------------------------- response vehicles that must not stand

        private const float StandsFor = 25f;     // seconds without moving a metre after which a guarded vehicle is reported
        private const float GuardFor = 90f;      // seconds a vehicle stays guarded after it was last named (it is named again on every transition)

        private struct Guarded
        {
            public VehicleComponent Car;
            public string What;
            public Vector3 At;
            public float Since;      // when it was last seen to have moved
            public float Until;
            public bool Said;
        }

        private static readonly Guarded[] Guards = new Guarded[4];
        private static int _guards;
        private static float _nextGuard;

        /// <summary>
        /// What is done about a guarded vehicle that a scene of the mod's still holds and that has stood too long:
        /// the scene's own way of getting it off the road (TrafficAccidents.Unstick). Called once a second until it has gone.
        /// </summary>
        public static Action<VehicleComponent> Stuck;

        /// <summary>
        /// Names a response vehicle of the mod's (an ambulance, a patrol car) that from now on must not stand: its
        /// crew is aboard, or its scene has ended. Once a second it is looked at (one position); if it has not
        /// moved a metre in 25 s that is reported once and put right: a vehicle the mod still holds through its
        /// scene (Stuck), one the traffic system has back by taking it off the road as soon as nobody is looking
        /// that way. Naming it again starts the count afresh. It is forgotten when it leaves the road, and 90 s
        /// after it was last named.
        /// </summary>
        public static void Guard(VehicleComponent car, string what)
        {
            if (car == null)
            {
                return;
            }
            int slot = -1;
            for (int i = 0; i < Guards.Length; i++)
            {
                if (Guards[i].Car == car)
                {
                    slot = i;
                    break;
                }
                if (slot < 0 && (object)Guards[i].Car == null)
                {
                    slot = i;
                }
            }
            if (slot < 0)
            {
                return;
            }
            if ((object)Guards[slot].Car == null)
            {
                _guards++;
            }
            float now = Time.time;
            Guards[slot] = new Guarded { Car = car, What = what, At = car.transform.position, Since = now, Until = now + GuardFor };
        }

        private static void LookAtGuarded(float now)
        {
            for (int i = 0; i < Guards.Length; i++)
            {
                ref Guarded guarded = ref Guards[i];
                VehicleComponent car = guarded.Car;
                if ((object)car == null)
                {
                    continue;
                }
                bool ours = car != null && TrafficTools.IsHeld(car);
                if (car == null || !car.gameObject.activeInHierarchy || (!ours && now >= guarded.Until))
                {
                    guarded = default;
                    _guards--;
                    continue;
                }
                Vector3 at = car.transform.position;
                float dx = at.x - guarded.At.x;
                float dz = at.z - guarded.At.z;
                if (dx * dx + dz * dz > 1f)
                {
                    guarded.At = at;
                    guarded.Since = now;
                    guarded.Said = false;
                    continue;
                }
                if (now - guarded.Since < StandsFor)
                {
                    continue;
                }
                bool seen = StreetSpots.IsInView(at);
                if (!guarded.Said)
                {
                    guarded.Said = true;
                    string touch = TrafficTools.LastTouch(car, out float ago);
                    Logger?.Warn("Traffic watch: the " + guarded.What + " ('" + TrafficTools.Name(car) + "') has stood for " + (now - guarded.Since).ToString("F0")
                                 + " s with its crew aboard or after its scene ended, " + TrafficTools.State(car) + "; "
                                 + (touch != null ? "the mod last touched it " + ago.ToString("F0") + " s ago: " + touch : "the mod has not touched it")
                                 + "; " + (ours ? "its scene is told to get it off the road." : seen ? "it is taken off the road as soon as it is out of view." : "out of view: taken off the road."));
                }
                if (ours)
                {
                    Stuck?.Invoke(car);
                }
                else if (!seen)
                {
                    TrafficTools.Remove(car, "the traffic watch: a response vehicle left standing");
                    guarded = default;
                    _guards--;
                }
            }
        }

        private static void Look(float now)
        {
            List<VehicleComponent> pool = Manager.GetVehicleList();
            if (_tracks.Length < pool.Count)
            {
                _tracks = new Track[pool.Count];
            }
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                Rigidbody body = car != null && car.gameObject.activeInHierarchy ? car.rb : null;
                if (body == null || body.isKinematic)
                {
                    _tracks[i].Since = 0f;   // off the road, or frozen: nothing to watch
                    _tracks[i].Bouncing = 0;
                    continue;
                }
                Vector3 at = body.position;
                ref Track track = ref _tracks[i];
                if (track.Since <= 0f)
                {
                    Begin(ref track, at, now);
                    continue;
                }
                float step = at.y - track.Height;
                if (step > MinStep || step < -MinStep)
                {
                    sbyte way = (sbyte)(step > 0f ? 1 : -1);
                    if (track.Way != 0 && way != track.Way && track.Turns < byte.MaxValue)
                    {
                        track.Turns++;
                    }
                    track.Way = way;
                }
                track.Height = at.y;
                track.Low = Mathf.Min(track.Low, at.y);
                track.High = Mathf.Max(track.High, at.y);
                if (now - track.Since < Window)
                {
                    continue;
                }
                float travel = track.High - track.Low;
                float dx = at.x - track.Start.x;
                float dz = at.z - track.Start.z;
                bool bouncing = track.Turns >= MinTurns && travel >= MinTravel && dx * dx + dz * dz < MaxDrift * MaxDrift;
                int running = bouncing ? track.Bouncing + 1 : 0;
                float middle = (track.Low + track.High) * 0.5f;
                Begin(ref track, at, now);
                track.Bouncing = (byte)running;
                if (running >= Windows)
                {
                    track.Bouncing = 0;
                    Settle(car, body, travel, middle);
                }
            }
        }

        private static void Begin(ref Track track, Vector3 at, float now)
        {
            track.Since = now;
            track.Start = at;
            track.Height = track.Low = track.High = at.y;
            track.Way = 0;
            track.Turns = 0;
        }

        // Reports the vehicle and stops it: off the road if it cannot be seen, frozen at the middle of its bounce if it can.
        private static void Settle(VehicleComponent car, Rigidbody body, float travel, float middle)
        {
            Vector3 at = body.position;
            bool ours = TrafficTools.IsHeld(car);
            bool seen = StreetSpots.IsInView(at);
            if (_reports < 12)
            {
                _reports++;
                string touch = TrafficTools.LastTouch(car, out float ago);
                Logger?.Warn("Traffic watch: '" + TrafficTools.Name(car) + "' was bouncing on the spot (" + travel.ToString("F2", CultureInfo.InvariantCulture) + " m up and down for "
                             + (Window * Windows).ToString("F0") + " s), " + TrafficTools.State(car) + "; "
                             + (touch != null ? "the mod last touched it " + ago.ToString("F0") + " s ago: " + touch : "the mod has not touched it (ordinary traffic)")
                             + "; " + Standing(car, body) + "; "
                             + (seen ? "frozen where it stands until it is out of view." : "out of view: taken off the road.")
                             + (_reports == 12 ? " (No more of these are logged this session.)" : string.Empty));
            }
            if (!seen && !ours)
            {
                TrafficTools.Remove(car, "the traffic watch: bouncing on the spot");
                return;
            }
            // In view, or a car one of the mod's scenes still counts on (that scene takes it off the road itself):
            // held still at the middle of its bounce, which is where its springs carry it.
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            at.y = middle;
            body.position = at;
            TrafficTools.Touch(car, "frozen by the traffic watch");
            Frozen.Add(new Held { Car = car, Ours = ours });
        }

        // What is under a bouncing vehicle besides the road: a person or a body, a parked vehicle, another car.
        private static string Standing(VehicleComponent car, Rigidbody body)
        {
            if (_underMask == 0)
            {
                _underMask = TrafficTools.VehicleMask | TrafficTools.PeopleMask;
            }
            float reach = Mathf.Max(1.2f, car.length * 0.5f);
            int count = Physics.OverlapSphereNonAlloc(body.worldCenterOfMass, reach, Under, _underMask, QueryTriggerInteraction.Ignore);
            string found = null;
            for (int i = 0; i < count; i++)
            {
                Collider other = Under[i];
                if (found == null && other != null && other.attachedRigidbody != body)
                {
                    found = "touching '" + other.name + "' (layer " + LayerMask.LayerToName(other.gameObject.layer) + ")";
                }
                Under[i] = null;
            }
            return found ?? "nothing but the road under it";
        }

        // A frozen vehicle is given its physics back the moment it is no longer where we left it to be seen:
        // gone from the road (the pool does not reset a body), let go by its scene, or out of view (then it is
        // taken off the road as well, unless a scene still holds it).
        private static void Thaw(bool all)
        {
            for (int i = Frozen.Count - 1; i >= 0; i--)
            {
                Held held = Frozen[i];
                VehicleComponent car = held.Car;
                if (car == null)
                {
                    Frozen.RemoveAt(i);
                    continue;
                }
                Rigidbody body = car.rb;
                bool onRoad = car.gameObject.activeInHierarchy;
                bool moved = body == null || !body.isKinematic;                 // somebody else has taken it over since
                bool letGo = held.Ours && !TrafficTools.IsHeld(car);
                bool unseen = onRoad && !held.Ours && !StreetSpots.IsInView(car.transform.position);
                if (!all && onRoad && !moved && !letGo && !unseen)
                {
                    continue;
                }
                Frozen.RemoveAt(i);
                if (moved)
                {
                    continue;
                }
                if (onRoad && unseen && !all)
                {
                    TrafficTools.Remove(car, "the traffic watch: frozen after bouncing, now out of view");   // also makes it movable again
                }
                else if (!held.Ours || !onRoad || letGo)
                {
                    body.isKinematic = false;
                }
            }
        }
    }
}
