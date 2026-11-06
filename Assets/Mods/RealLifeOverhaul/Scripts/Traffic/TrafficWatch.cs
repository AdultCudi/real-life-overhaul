using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A self-check on the traffic: a car the mod holds without driving or freezing it bounces on the spot (the springs have no
    /// damper; the traffic system steadies only its own cars). Every vehicle of the pool is looked at once a second, and one
    /// that stands is watched five times a second (TrafficWatchRules); one bouncing in place for 3 s is reported and taken
    /// off the road unseen, or frozen until it is. It also watches the response vehicles that must not stand (Guard).
    /// Cost: five times a second, a position for a fifth of the pool and one for each vehicle standing; nothing allocated.
    /// </summary>
    public static class TrafficWatch
    {
        private const float Interval = TrafficWatchRules.Interval;   // seconds
        private const float Window = TrafficWatchRules.Window;       // seconds, for the report

        // The pool as the rules read it: one vehicle's place, or false if it is off the road or frozen.
        private sealed class PoolSource : IBounceSource
        {
            public List<VehicleComponent> Pool;

            public bool Read(int index, out float x, out float y, out float z)
            {
                VehicleComponent car = Pool[index];
                Rigidbody body = car != null && car.gameObject.activeInHierarchy ? car.rb : null;
                if (body == null || body.isKinematic)
                {
                    x = y = z = 0f;
                    return false;
                }
                Vector3 at = body.position;
                x = at.x;
                y = at.y;
                z = at.z;
                return true;
            }
        }

        private struct Held
        {
            public VehicleComponent Car;
            public bool Ours;        // a scene of the mod's holds it: that scene lets it go, we only undo the freeze if it is left
        }

        public static IModLogger Logger => TrafficTools.Logger;

        private static BounceTrack[] _tracks = new BounceTrack[0];
        private static readonly PoolSource Source = new PoolSource();
        private static readonly int[] Found = new int[4];
        private static readonly float[] Travels = new float[4];
        private static readonly float[] Middles = new float[4];
        private static int _cursor;
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

        /// <summary>The scene's own way of getting a guarded vehicle it still holds off the road (TrafficAccidents.Unstick), once a second until gone.</summary>
        public static Action<VehicleComponent> Stuck;

        /// <summary>Names a response vehicle that must not stand: if it does not move a metre in 25 s it is reported once and got off the road.</summary>
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
                System.Array.Resize(ref _tracks, pool.Count);
            }
            Source.Pool = pool;
            int found = TrafficWatchRules.Look(_tracks, pool.Count, ref _cursor, Source, now, Found, Travels, Middles);
            Source.Pool = null;
            for (int i = 0; i < found; i++)
            {
                VehicleComponent car = pool[Found[i]];
                Rigidbody body = car != null ? car.rb : null;
                if (body != null)
                {
                    Settle(car, body, Travels[i], Middles[i]);
                }
            }
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
                             + (Window * TrafficWatchRules.Windows).ToString("F0") + " s), " + TrafficTools.State(car) + "; "
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
            // In view, or still a scene's: held still at the middle of its bounce, where its springs carry it.
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            at.y = middle;
            body.position = at;
            TrafficTools.Touch(car, "frozen by the traffic watch");
            Frozen.Add(new Held { Car = car, Ours = ours });
        }

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
