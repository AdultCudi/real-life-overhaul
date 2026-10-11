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
    /// They can also be made to wait at the kerb and set off at the moment that leaves the driver a
    /// chosen amount of road (Step.Close: a hard brake; Step.Late: none to speak of), measured from the
    /// car's speed as it comes, or to stop in the lane and walk on when the car that stands behind them
    /// sounds its horn (Step.Dawdle).
    /// One for the director's own use and ten more for a test that sends a crowd across; each owned and
    /// ticked (four times a second, a few subtractions; every frame only while one waits at the kerb for
    /// its moment) by RecklessDrivers.
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

        /// <summary>How somebody goes into the road.</summary>
        public enum Step
        {
            Across,   // straight over to the far pavement, now
            Stay,     // into the lane, and stand there until their time is up
            Close,    // wait at the kerb, then over at the moment that leaves the driver a hard stop
            Dawdle,   // into the lane and stand there; over when a car sounds its horn at them
            Late      // wait at the kerb, then over when the driver can no longer stop
        }

        private const int MostOut = 12;              // people of ours in the road at once: one, and a test's ten
        private const float KerbWait = 14f;          // seconds more somebody may wait at the kerb for their moment

        private static readonly Jaywalker[] Out = new Jaywalker[MostOut];   // whoever is in the road now

        public static IModLogger Logger;

        private readonly PedestrianTakeover _takeover = new PedestrianTakeover();
        private Pedestrian _walker;
        private VehicleComponent _car;
        private Vector3 _across;
        private Vector3 _lane;
        private float _toLane;
        private float _until;
        private bool _honked;
        private bool _stay;
        private bool _pending;           // waiting at the kerb for the car to be where it is wanted
        private Step _step;

        public bool Active => _walker != null || _takeover.Held;

        /// <summary>True while they wait at the kerb for their moment: the one state in which a tick every frame is worth having.</summary>
        public bool Pending => _pending && Active;

        /// <summary>The car they stepped out in front of, while they are in the road.</summary>
        public VehicleComponent Car => Active ? _car : null;

        /// <summary>True if this pedestrian is the one in the road: CarStrikes may hit it for the watched car although its own script is off.</summary>
        public static bool Holds(Pedestrian pedestrian)
        {
            return Having(pedestrian) != null;
        }

        // Whoever of ours has that pedestrian in the road, or null. Twelve places at the most.
        private static Jaywalker Having(Pedestrian pedestrian)
        {
            if (pedestrian == null)
            {
                return null;
            }
            for (int i = 0; i < Out.Length; i++)
            {
                Jaywalker one = Out[i];
                if (one != null && one._walker == pedestrian && one._takeover.Held)
                {
                    return one;
                }
            }
            return null;
        }

        /// <summary>
        /// A car that stands behind this pedestrian has sounded its horn at them: somebody who was dawdling in the
        /// lane walks on to the far pavement. True if they were ours and did.
        /// </summary>
        public static bool Hooted(Pedestrian pedestrian)
        {
            Jaywalker one = Having(pedestrian);
            if (one == null || one._step != Step.Dawdle || !one._stay)
            {
                return false;
            }
            one._stay = false;
            if (!one._takeover.WalkTo(one._across))
            {
                one.End("no way on to the far pavement");
                return false;
            }
            Logger?.Info("Jaywalker: the car behind them sounded its horn; they walk on.");
            return true;
        }

        /// <summary>
        /// The car has reached them: they are the game's again from this call on (ground and scripts as they were),
        /// so that whoever throws them down takes over an ordinary pedestrian. True if this was the one in the road.
        /// </summary>
        public static bool Yield(Pedestrian pedestrian)
        {
            Jaywalker current = null;
            for (int i = 0; i < Out.Length && pedestrian != null; i++)
            {
                if (Out[i] != null && Out[i]._walker == pedestrian)
                {
                    current = Out[i];
                    break;
                }
            }
            if (current == null)
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
                if (!Kerbside(candidate, player, nearest, furthest, out Vector3 at, out Vector3 over, out float toLane, out Vector3 runs))
                {
                    continue;
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

        /// <summary>
        /// The same without a car: anybody walking a pavement beside a lane, away from a crossing, with a pavement
        /// across the road, who is not in the road for us already. For a test that sends a crowd over.
        /// </summary>
        public static bool FindCrosser(Vector3 player, float nearest, float furthest, out Pedestrian walker, out Vector3 across, out Vector3 lane)
        {
            walker = null;
            across = default;
            lane = default;
            var city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers == null || walkers.Count == 0 || !TrafficTools.Ready || !RoadMap.Ensure())
            {
                return false;
            }
            Crossings.Ensure();
            int start = Random.Range(0, walkers.Count);
            for (int i = 0; i < walkers.Count; i++)
            {
                Pedestrian candidate = walkers[(start + i) % walkers.Count];
                if (Kerbside(candidate, player, nearest, furthest, out Vector3 at, out Vector3 over, out float toLane, out _)
                    && FarKerb(at, over, toLane, out Vector3 kerb))
                {
                    walker = candidate;
                    across = kerb;
                    lane = at + over * toLane;
                    return true;
                }
            }
            return false;
        }

        // True if that pedestrian is walking a pavement beside a lane, away from any crossing and at the distance wanted
        // from the player: where they are, the way straight across to the lane's middle, how far that is, and the way the lane runs.
        private static bool Kerbside(Pedestrian candidate, Vector3 player, float nearest, float furthest, out Vector3 at, out Vector3 over, out float toLane, out Vector3 runs)
        {
            at = default;
            over = default;
            toLane = 0f;
            runs = default;
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.tpc == null || candidate.tpc.isSittingOn != null
                || candidate.tpc.navmeshAgent == null || !candidate.tpc.navmeshAgent.isActiveAndEnabled || !candidate.tpc.navmeshAgent.isOnNavMesh
                || candidate.tpc.navmeshAgent.isStopped || RagdollSystem.Holds(candidate.tpc))
            {
                return false;
            }
            at = candidate.transform.position;
            float sqr = (at - player).sqrMagnitude;
            if (sqr < nearest * nearest || sqr > furthest * furthest || Crossings.At(at, out _))
            {
                return false;
            }
            if (!RoadMap.NearestLane(at, out float off, out runs, out _, out Vector3 point) || off < KerbLeast || off > KerbMost)
            {
                return false;
            }
            over = point - at;
            over.y = 0f;
            over -= runs * Vector3.Dot(over, runs);   // straight across, whatever point of the lane was nearest
            if (over.sqrMagnitude < 1f)
            {
                return false;
            }
            toLane = over.magnitude;
            over /= toLane;
            return !Crossings.At(at + over * toLane, out _);   // a lane that is a crossing's there: stepping out on it is not jaywalking
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
                // (A test in which they wait at the kerb for their moment, or stand in the lane, takes any car that is still well up the road.)
                if (mode >= 2 ? ahead > (mode == 2 || mode == 4 ? 25f : SolidRules.StepOutAt(speed, toLane, mode == 5) + 8f) && ahead < 120f
                        : ahead > wanted * 0.75f && ahead < wanted * (mode == 1 ? 2.2f : 1.3f))
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

        /// <summary>Takes the pedestrian and sends them into the road. False (and the pedestrian the game's, walking) if it cannot be done.</summary>
        /// <param name="car">The car they step out in front of; null only for somebody who simply crosses (Step.Across).</param>
        /// <param name="across">The pavement over the road.</param>
        /// <param name="lane">The middle of the lane, straight across from them.</param>
        public bool Begin(Pedestrian walker, VehicleComponent car, Vector3 across, Vector3 lane, Step step, float now)
        {
            if (Active || walker == null || (car == null && step != Step.Across))
            {
                return false;
            }
            int place = -1;
            for (int i = 0; i < Out.Length && place < 0; i++)
            {
                if (Out[i] == null)
                {
                    place = i;
                }
            }
            if (place < 0)
            {
                return false;
            }
            HornReactions.LetGo(walker);   // a pace raised or a stop made for a horn is undone before they are borrowed
            if (!_takeover.Take(walker))
            {
                return false;
            }
            Vector3 at = walker.transform.position;
            bool waits = step == Step.Close || step == Step.Late;
            bool stands = step == Step.Stay || step == Step.Dawdle;
            if (!_takeover.StandUp(at) || (!waits && !_takeover.WalkTo(stands ? lane : across)))
            {
                PutBack(at);
                return false;
            }
            _walker = walker;
            _car = car;
            _across = across;
            _lane = lane;
            _toLane = Vector3.Distance(new Vector3(at.x, 0f, at.z), new Vector3(lane.x, 0f, lane.z));
            _step = step;
            _stay = stands;
            _pending = waits;
            _until = now + Allowance + (waits ? KerbWait : 0f);
            _honked = false;
            Out[place] = this;
            Logger?.Info(car == null
                ? $"Jaywalker: '{walker.name}' crosses the road for the pavement {Vector3.Distance(at, across):F0} m across."
                : $"Jaywalker: '{walker.name}' {(waits ? "waits at the kerb to step" : "steps")} into the road {Vector3.Distance(at, car.transform.position):F0} m ahead of '{TrafficTools.Name(car)}' "
                  + $"doing {car.GetCurrentSpeed():F0} km/h ({step}), for the pavement {Vector3.Distance(at, across):F0} m across.");
            return true;
        }

        // At the kerb, waiting for their moment: the car's distance up the lane against what it must be for the road
        // the driver is to be left (SolidRules.StepOutAt, from its speed as it comes). A few subtractions.
        private void Wait()
        {
            VehicleComponent car = _car;
            if (car == null || !car.gameObject.activeInHierarchy)
            {
                PutBack(_takeover.Character != null ? _takeover.Character.transform.position : _lane);
                Over("the car they waited for left the road");
                return;
            }
            Transform body = car.transform;
            Vector3 to = _lane - body.position;
            Vector3 heading = body.forward;
            float ahead = to.x * heading.x + to.z * heading.z;
            float speed = car.GetCurrentSpeed() / 3.6f;
            if (ahead < 2f)
            {
                PutBack(_takeover.Character != null ? _takeover.Character.transform.position : _lane);
                Over("the car went by before they stepped out");
                return;
            }
            // (Their own pace, as the game gave it to them: the moment is measured with the walk they will really make.)
            ThirdPersonCharacter character = _takeover.Character;
            float pace = character != null && character.navmeshAgent != null ? character.navmeshAgent.speed : StreetRules.WalkPace;
            if (ahead > SolidRules.StepOutAt(speed, _toLane, _step == Step.Late, pace))
            {
                return;
            }
            _pending = false;
            if (!_takeover.WalkTo(_across))
            {
                PutBack(_takeover.Character != null ? _takeover.Character.transform.position : _lane);
                Over("no way across");
                return;
            }
            Logger?.Info($"Jaywalker: steps out now, {ahead:F0} m ahead of the car's middle at {speed * 3.6f:F0} km/h ({_step}).");
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
            if (_pending)
            {
                if (now >= _until)
                {
                    PutBack(at);
                    Over("out of time at the kerb");
                }
                else
                {
                    Wait();
                }
                return;
            }
            // (With "Traffic sees people in the road" on, the car's horn is that feature's: it is sounded there, once, for whoever is in its way.)
            if (!_honked && !SolidStreets.Working && car != null && car.gameObject.activeInHierarchy)
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
                        TrafficTools.Hoot(horn);
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
            _pending = false;
            for (int i = 0; i < Out.Length; i++)
            {
                if (Out[i] == this)
                {
                    Out[i] = null;
                }
            }
        }
    }
}
