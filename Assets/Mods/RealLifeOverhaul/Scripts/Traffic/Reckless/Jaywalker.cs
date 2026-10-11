using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using GleyTrafficSystem;
using Helpers;
using Streets.Pedestrians;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Somebody who steps into the road away from a crossing, in front of a car: one of the game's own
    /// pedestrians, borrowed the way a casualty borrows one (PedestrianTakeover: its own scripts off, the
    /// ground it may walk on widened to the road), walked across to the far pavement and given back
    /// walking. The car is ordinary traffic and is not touched: its own sensor sees somebody in the road
    /// and its driver brakes. Mostly that is all: a horn, a cross look, and they are across. Only if the
    /// car really reaches them while it is still moving (CarStrikes watches that one car) are they hit,
    /// at whatever speed it still had.
    /// One at a time. Owned and ticked (four times a second, a few subtractions) by RecklessDrivers.
    ///
    /// GIVING THEM BACK. The one thing changed on the pedestrian is the kind of ground it may walk on, and
    /// PedestrianTakeover puts that back on each of its three ways out, all of which are used here:
    /// across, or out of time: HandBack (on the pavement again, walking on as the game's);
    /// put away by the game meanwhile (the player went indoors, the pool took it): Forget;
    /// hit by the car: Yield (Forget), and the casualty takes it over from there as it would any pedestrian.
    /// The option switched off, traffic stopped, the city unloaded: End, which is one of the first two.
    /// </summary>
    internal sealed class Jaywalker
    {
        private const float Allowance = 16f;         // seconds to get across before they are simply put back on a pavement
        private const float HornWithin = 13f;        // metres: the car this near, and still coming, sounds its horn
        private const float KerbLeast = 2.5f;        // metres from the lane's middle: nearer than this they are not on a pavement
        private const float KerbMost = 7f;           // ...and further than this the road is not theirs to cross
        private const float LaneKeeps = 1.8f;        // metres a car may be off the lane's line and still be in that lane
        private const float AngryFor = 3f;

        private static readonly float[] Widths = { 3f, 6.5f, 10f, 13.5f };   // metres beyond the first lane's middle at which the far pavement is looked for

        private static Jaywalker _current;

        public static IModLogger Logger;

        private readonly PedestrianTakeover _takeover = new PedestrianTakeover();
        private Pedestrian _walker;
        private VehicleComponent _car;
        private float _until;
        private bool _honked;
        private bool _stay;

        public bool Active => _walker != null || _takeover.Held;

        /// <summary>The car they stepped out in front of, while they are in the road.</summary>
        public VehicleComponent Car => Active ? _car : null;

        /// <summary>True if this pedestrian is the one in the road: CarStrikes may hit it for the watched car although its own script is off.</summary>
        public static bool Holds(Pedestrian pedestrian)
        {
            Jaywalker current = _current;
            return current != null && pedestrian != null && current._walker == pedestrian && current._takeover.Held;
        }

        /// <summary>
        /// The car has reached them: they are the game's again from this call on (ground and scripts as they were),
        /// so that whoever throws them down takes over an ordinary pedestrian. True if this was the one in the road.
        /// </summary>
        public static bool Yield(Pedestrian pedestrian)
        {
            Jaywalker current = _current;
            if (current == null || pedestrian == null || current._walker != pedestrian)
            {
                return false;
            }
            current._takeover.Forget();
            current.Over("hit by the car");
            return true;
        }

        /// <summary>
        /// Looks for somebody walking a pavement beside a lane, away from any crossing, with a car coming up that
        /// lane at the distance wanted (StreetRules.JaywalkCarDistance), and a pavement across the road for them to
        /// make for. One pass over the pedestrians out and, for each who stands right, one over the traffic: on a
        /// 20 s look or a test button only.
        /// </summary>
        /// <param name="late">They are due to be hit: the car is as near as it can be.</param>
        /// <param name="mode">0: as it comes. 1: a test, a car up to twice as far off will do. 2: a test in which they stand in the lane: any car coming up it within 120 m.</param>
        /// <param name="across">The pavement over the road they make for.</param>
        /// <param name="lane">The middle of the lane the car is coming up, straight across from them.</param>
        public static bool Find(Vector3 player, float nearest, float furthest, bool late, int mode, out Pedestrian walker, out VehicleComponent car, out Vector3 across, out Vector3 lane)
        {
            walker = null;
            car = null;
            across = default;
            lane = default;
            var city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers == null || walkers.Count == 0 || !TrafficTools.Ready || !RoadMap.Ensure())
            {
                return false;
            }
            Crossings.Ensure();
            List<VehicleComponent> pool = Manager.GetVehicleList();
            int start = Random.Range(0, walkers.Count);
            for (int i = 0; i < walkers.Count; i++)
            {
                Pedestrian candidate = walkers[(start + i) % walkers.Count];
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.tpc == null || candidate.tpc.isSittingOn != null
                    || candidate.tpc.navmeshAgent == null || !candidate.tpc.navmeshAgent.isActiveAndEnabled || !candidate.tpc.navmeshAgent.isOnNavMesh
                    || candidate.tpc.navmeshAgent.isStopped || RagdollSystem.Holds(candidate.tpc))
                {
                    continue;
                }
                Vector3 at = candidate.transform.position;
                float sqr = (at - player).sqrMagnitude;
                if (sqr < nearest * nearest || sqr > furthest * furthest || Crossings.At(at, out _))
                {
                    continue;
                }
                if (!RoadMap.NearestLane(at, out float off, out Vector3 runs, out _, out Vector3 point) || off < KerbLeast || off > KerbMost)
                {
                    continue;
                }
                Vector3 over = point - at;
                over.y = 0f;
                over -= runs * Vector3.Dot(over, runs);   // straight across, whatever point of the lane was nearest
                if (over.sqrMagnitude < 1f)
                {
                    continue;
                }
                float toLane = over.magnitude;
                over /= toLane;
                if (Crossings.At(at + over * toLane, out _))
                {
                    continue;   // the lane there is a crossing's: stepping out on it is not jaywalking
                }
                VehicleComponent coming = Coming(pool, at + over * toLane, runs, toLane, late, mode);
                if (coming == null || !FarKerb(at, over, toLane, out Vector3 kerb))
                {
                    continue;
                }
                walker = candidate;
                car = coming;
                across = kerb;
                lane = at + over * toLane;
                return true;
            }
            return false;
        }

        // An ordinary car in that lane, coming, at about the distance that leaves the driver the room wanted.
        private static VehicleComponent Coming(List<VehicleComponent> pool, Vector3 lane, Vector3 runs, float toLane, bool late, int mode)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (!CrimeCars.IsOrdinary(car))
                {
                    continue;
                }
                float speed = car.GetCurrentSpeed() / 3.6f;
                if (speed < 4.5f || speed > 17f)
                {
                    continue;
                }
                Transform body = car.transform;
                Vector3 offset = lane - body.position;
                if (offset.y > 4f || offset.y < -4f)
                {
                    continue;
                }
                float ahead = offset.x * runs.x + offset.z * runs.z;                 // metres of lane between the car and where they step in
                float beside = offset.x * runs.z - offset.z * runs.x;
                Vector3 heading = body.forward;
                if (ahead <= 0f || beside > LaneKeeps || beside < -LaneKeeps || heading.x * runs.x + heading.z * runs.z < 0.8f)
                {
                    continue;
                }
                float wanted = StreetRules.JaywalkCarDistance(speed, toLane, late);
                if (mode == 2 ? ahead > 25f && ahead < 120f : ahead > wanted * 0.75f && ahead < wanted * (mode == 1 ? 2.2f : 1.3f))
                {
                    return car;
                }
            }
            return null;
        }

        // A pavement on the other side of the road, straight across: tried one, two, three and four lanes over.
        private static bool FarKerb(Vector3 at, Vector3 over, float toLane, out Vector3 kerb)
        {
            for (int i = 0; i < Widths.Length; i++)
            {
                Vector3 probe = at + over * (toLane + Widths[i]);
                if (StreetSpots.OnPavement(probe, 2.2f, out kerb) && Vector3.Dot(kerb - at, over) > toLane + 1.5f)
                {
                    return true;
                }
            }
            kerb = default;
            return false;
        }

        /// <summary>Takes the pedestrian and sends them across. False (and the pedestrian the game's, walking) if it cannot be done.</summary>
        /// <param name="stay">A test: they stop where they were sent (the middle of the lane) and stand there until their time is up.</param>
        public bool Begin(Pedestrian walker, VehicleComponent car, Vector3 across, bool stay, float now)
        {
            if (Active || walker == null || car == null)
            {
                return false;
            }
            HornReactions.LetGo(walker);   // a pace raised or a stop made for a horn is undone before they are borrowed
            if (!_takeover.Take(walker))
            {
                return false;
            }
            Vector3 at = walker.transform.position;
            if (!_takeover.StandUp(at) || !_takeover.WalkTo(across))
            {
                PutBack(at);
                return false;
            }
            _walker = walker;
            _car = car;
            _stay = stay;
            _until = now + Allowance;
            _honked = false;
            _current = this;
            Logger?.Info($"Jaywalker: '{walker.name}' steps into the road {Vector3.Distance(at, car.transform.position):F0} m ahead of '{TrafficTools.Name(car)}' "
                         + $"doing {car.GetCurrentSpeed():F0} km/h, for the pavement {Vector3.Distance(at, across):F0} m across.");
            return true;
        }

        public void Tick(float now)
        {
            if (!Active)
            {
                return;
            }
            if (_takeover.PutAway)
            {
                // The game has it back already (indoors, the pool): only our own hold is dropped.
                _takeover.Forget();
                Over("put away by the game");
                return;
            }
            ThirdPersonCharacter character = _takeover.Character;
            if (character == null)
            {
                _takeover.Forget();
                Over("gone");
                return;
            }
            Vector3 at = character.transform.position;
            VehicleComponent car = _car;
            if (!_honked && car != null && car.gameObject.activeInHierarchy)
            {
                Vector3 to = at - car.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < HornWithin * HornWithin && Vector3.Dot(to, car.transform.forward) > 0f && car.GetCurrentSpeed() > 4f)
                {
                    // A near miss as far as anybody can tell yet: the driver leans on the horn, and gets a look for it.
                    _honked = true;
                    var horn = car.GetComponentInChildren<AiCarHorn>();
                    if (horn != null)
                    {
                        horn.Hail(false);
                    }
                    ActorPool pool = ActorPool.Instance;
                    if (pool != null)
                    {
                        pool.ShowEmoji(character, CharacterEmojiName.CommonRedAngry, AngryFor);
                    }
                    Logger?.Info($"Jaywalker: the car is {Mathf.Sqrt(to.sqrMagnitude):F0} m off at {car.GetCurrentSpeed():F0} km/h and sounds its horn.");
                }
            }
            if ((_takeover.Arrived && !_stay) || now >= _until)
            {
                bool across = _takeover.Arrived && !_stay;
                PutBack(at);
                Over(across ? "across" : "out of time: put back on a pavement");
            }
        }

        /// <summary>Ends it now, whatever state it is in: they are the game's again, on a pavement. Safe at any time.</summary>
        public void End(string why)
        {
            if (!Active)
            {
                return;
            }
            if (_takeover.PutAway || _takeover.Character == null)
            {
                _takeover.Forget();
            }
            else
            {
                PutBack(_takeover.Character.transform.position);
            }
            Over(why);
        }

        // Back to the game as an ordinary walker heading somewhere along the pavements (or, standing where a pedestrian
        // cannot walk, put away through the pool, which is PedestrianTakeover's own rule).
        private void PutBack(Vector3 here)
        {
            try
            {
                if (!StreetSpots.PavementAround(here, 15f, 30f, false, out Vector3 onward))
                {
                    onward = here;
                }
                _takeover.HandBack(onward);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _takeover.Remove();
            }
        }

        private void Over(string why)
        {
            VehicleComponent car = _car;
            CarStrikes strikes = CarStrikes.Instance;
            if (strikes != null && car != null && strikes.Watching(car))
            {
                strikes.Unwatch(car);
            }
            Logger?.Info("Jaywalker: over (" + why + ").");
            _walker = null;
            _car = null;
            if (_current == this)
            {
                _current = null;
            }
        }
    }
}
