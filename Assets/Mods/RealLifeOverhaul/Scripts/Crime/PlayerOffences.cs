using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Notices what the player does wrong on the road: the stop line ten times a second, speed and position twice a second,
    /// and on foot twice a second, but only while the game shows nothing carrying the player (<see cref="PlayerRide"/>: a few
    /// field reads). A kind of offence whose switch is off (<see cref="OffenceSwitches"/>) is not looked for. Each look is a few
    /// subtractions against RoadMap; the one physics query (a crossing?) runs only on foot, with an officer near.
    /// </summary>
    internal sealed class PlayerOffences
    {
        private const float LineInterval = 0.1f;      // seconds
        private const float SlowInterval = 0.5f;      // seconds
        private const float MinSpeed = 4f;            // m/s: below this nobody is speeding or driving recklessly
        private const float CrawlSpeed = 1.5f;        // m/s: creeping over a stop line slower is not running the light
        private const float RedFor = 1f;              // seconds the light must have been red before the car crosses
        private const float PastLine = 1f;            // metres the car's middle must be past the stop line
        private const float PavementSeconds = 2.5f;   // seconds on the pavement at speed
        private const float WrongSideSeconds = 3f;    // seconds against the traffic
        private const float OnRoadSeconds = 1.2f;     // seconds walking in the road
        internal const float OfficerSight = 30f;       // metres

        private static readonly Collider[] Hits = new Collider[12];

        private float _nextLine;
        private float _nextSlow;
        private int _line = -1;
        private float _along;
        private float _redSince = -1f;
        private float _speedingSince = -1f;
        private bool _speedingBooked;
        private float _pavementSince = -1f;
        private float _wrongSince = -1f;
        private float _roadSince = -1f;
        private Vector3 _lastFoot;
        private float _footAt = -1f;     // when _lastFoot was taken; negative if there was no look since a reset
        private bool _carried;           // the last foot look found the player carried
        private readonly CallLedger _calls = new CallLedger();   // when something of the mod's last called the player to where they walked

        /// <summary>The officer who saw the last offence on foot, to be given a line.</summary>
        public StreetActor Witness { get; private set; }

        /// <summary>The signs of a ride at the last foot look, for the log line written when an offence on foot is found.</summary>
        public RideFacts Ride { get; private set; }

        public void Reset()
        {
            _line = -1;
            _redSince = -1f;
            _speedingSince = -1f;
            _speedingBooked = false;
            _pavementSince = -1f;
            _wrongSince = -1f;
            _roadSince = -1f;
            _footAt = -1f;
            _carried = false;
            _calls.Reset();
            Witness = null;
        }

        /// <summary>
        /// Looks at the player's car: an offence the moment one is complete, else None. A kind whose switch is off is not
        /// looked for.
        /// </summary>
        public Offence Driving(Rigidbody body, float now, OffenceSwitches on)
        {
            if (body == null)
            {
                return Offence.None;   // no car, or one the game destroyed: nothing to read
            }
            Vector3 velocity = body.velocity;
            float sqrSpeed = velocity.x * velocity.x + velocity.z * velocity.z;
            bool slow = sqrSpeed < MinSpeed * MinSpeed;
            if (slow)
            {
                _speedingSince = -1f;
                _pavementSince = -1f;
                _wrongSince = -1f;
            }
            Offence offence = Offence.None;
            if (now >= _nextLine)
            {
                _nextLine = now + LineInterval;
                if (on.RedLight)
                {
                    // Watched at any speed, so pulling away through a red light counts too.
                    Vector3 heading = slow ? Flatten(body.rotation * Vector3.forward) : velocity / Mathf.Sqrt(sqrSpeed);
                    if (RedLight(body.position, heading, now) && sqrSpeed > CrawlSpeed * CrawlSpeed)
                    {
                        offence = Offence.RedLight;
                    }
                }
                else
                {
                    _line = -1;   // the watch starts afresh from the line the car is at when the switch is turned on again
                    _redSince = -1f;
                }
            }
            if (!slow && offence == Offence.None && now >= _nextSlow)
            {
                _nextSlow = now + SlowInterval;
                offence = Slow(body.position, velocity, Mathf.Sqrt(sqrSpeed), now, on);
            }
            return offence;
        }

        private bool RedLight(Vector3 position, Vector3 heading, float now)
        {
            if (!RoadMap.LineAt(position, heading, out int line, out float along))
            {
                _line = -1;
                return false;
            }
            bool red = RoadMap.IsRed(RoadMap.Line(line));
            if (line != _line)
            {
                _line = line;
                _along = along;
                _redSince = red ? now : -1f;
                return false;
            }
            if (!red)
            {
                _redSince = -1f;
            }
            else if (_redSince < 0f)
            {
                _redSince = now;
            }
            bool crossed = _along < PastLine && along >= PastLine;
            _along = along;
            return crossed && red && now - _redSince >= RedFor;
        }

        private Offence Slow(Vector3 position, Vector3 velocity, float speed, float now, OffenceSwitches on)
        {
            if (!on.Speeding && !on.Pavement && !on.WrongWay)
            {
                _speedingSince = -1f;
                _speedingBooked = false;
                _pavementSince = -1f;
                _wrongSince = -1f;
                return Offence.None;
            }
            if (!RoadMap.NearestLane(position, out float distance, out Vector3 runs, out float limit))
            {
                _speedingSince = -1f;
                _pavementSince = -1f;
                _wrongSince = -1f;
                return Offence.None;
            }

            // Well over the limit for a few seconds; once until they slow down again.
            if (on.Speeding && speed * 3.6f > limit + CrimeLaw.SpeedingMarginKmh && distance < 8f)
            {
                if (_speedingSince < 0f)
                {
                    _speedingSince = now;
                }
                else if (!_speedingBooked && now - _speedingSince >= CrimeLaw.SpeedingSeconds)
                {
                    _speedingBooked = true;
                    return Offence.Speeding;
                }
            }
            else
            {
                _speedingSince = -1f;
                _speedingBooked = false;
            }

            // On the pavement at speed, well away from any lane.
            if (on.Pavement && speed > 8f && distance > 5f && NavMesh.SamplePosition(position, out NavMeshHit hit, 0.6f, StreetSpots.Pavement)
                && Flat(hit.position - position) < 0.5f * 0.5f)
            {
                if (_pavementSince < 0f)
                {
                    _pavementSince = now;
                }
                else if (now - _pavementSince >= PavementSeconds)
                {
                    _pavementSince = -1f;
                    return Offence.Reckless;
                }
            }
            else
            {
                _pavementSince = -1f;
            }

            // In a lane, fast, the wrong way along it.
            if (on.WrongWay && speed > 10f && distance < 1.8f && Vector3.Dot(runs, velocity) < -0.85f * speed)
            {
                if (_wrongSince < 0f)
                {
                    _wrongSince = now;
                }
                else if (now - _wrongSince >= WrongSideSeconds)
                {
                    _wrongSince = -1f;
                    return Offence.WrongSide;
                }
            }
            else
            {
                _wrongSince = -1f;
            }
            return Offence.None;
        }

        private static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
        }

        private static float Flat(Vector3 offset)
        {
            return offset.x * offset.x + offset.z * offset.z;
        }

        /// <summary>
        /// Looks at the player on foot with an officer close enough to see: against the lights, or in the road. Not while the
        /// game shows that something carries them (<see cref="PlayerRide"/>): the seat of a taxi, a private driver's car or a bus
        /// is not the road. A ride of another mod that shows none of the signs is taken for a walk. A kind whose switch is off is
        /// not looked for.
        /// </summary>
        public Offence OnFoot(PlayerController player, float now, OffenceSwitches on)
        {
            if (now < _nextSlow)
            {
                return Offence.None;
            }
            _nextSlow = now + SlowInterval;
            Vector3 position = player.transform.position;
            if (!on.OnFoot || Carried(player, position, now))
            {
                Rest(position, now);
                return Offence.None;
            }
            // Every look, officer or none, so that a light that has stood still while the player waited at the kerb is known for it.
            if (on.CrossRed && Crossings.Ensure())
            {
                Crossings.Watch(position, now);
            }
            Witness = OfficerNear(position, OfficerSight);
            // Waiting at the kerb or standing at a car door is no offence.
            bool walking = Flat(position - _lastFoot) > 0.3f * 0.3f;
            _lastFoot = position;
            _footAt = now;
            if (Witness == null || !walking || !RoadMap.Ensure())
            {
                _roadSince = -1f;
                return Offence.None;
            }

            // Walking where the mod's own scene, job or vehicle calls the player, or just up from a knockdown, is no offence, and the
            // officer who sees it has nothing to shout about. The excuse lasts a while after the call ends (CallRules.Linger).
            if (_calls.Hold(PlayerCalls.Within(position), now) || CallRules.JustRisen(PlayerKnockdown.RisenAt, now))
            {
                _roadSince = -1f;
                Witness = null;
                return Offence.None;
            }

            int count = Physics.OverlapSphereNonAlloc(position + Vector3.up * 0.5f, 0.6f, Hits, Physics.AllLayers, QueryTriggerInteraction.Collide);
            bool crossing = false;
            bool mayWalk = true;
            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i];
                Hits[i] = null;
                if (hit == null || !hit.CompareTag("Crosswalk"))
                {
                    continue;
                }
                crossing = true;
                var link = hit.GetComponent<CrosswalkToTrafficLightLink>();
                // The game's own rule for its pedestrians: walk while the cars' red lamp is lit. The game's junction holds its amber
                // until every car has left it, so a light that has not changed for longer than a cycle is stuck, and is no offence.
                mayWalk = link == null || link.redTrafficLight == null || link.redTrafficLight.activeSelf
                          || Crossings.LightStuck(link.redTrafficLight, now);
            }
            if (crossing)
            {
                bool offence = on.CrossRed && !mayWalk && _roadSince >= 0f && now - _roadSince >= OnRoadSeconds;
                if (mayWalk || !on.CrossRed)
                {
                    _roadSince = -1f;
                }
                else if (_roadSince < 0f)
                {
                    _roadSince = now;
                }
                if (offence)
                {
                    _roadSince = -1f;
                    return Offence.CrossOnRed;
                }
                return Offence.None;
            }

            // Off the pavement, with a lane close by.
            bool onRoad = on.Jaywalk && !NavMesh.SamplePosition(position, out _, 0.4f, StreetSpots.Pavement)
                          && RoadMap.Usable && RoadMap.NearestLane(position, out float distance, out _, out _) && distance < 5f;
            if (!onRoad)
            {
                _roadSince = -1f;
                return Offence.None;
            }
            if (_roadSince < 0f)
            {
                _roadSince = now;
                return Offence.None;
            }
            if (now - _roadSince < OnRoadSeconds)
            {
                return Offence.None;
            }
            _roadSince = -1f;
            return Offence.Jaywalk;
        }

        // True while something carries the player. The log says once, when a ride begins or ends, what showed it.
        private bool Carried(PlayerController player, Vector3 position, float now)
        {
            float seconds = _footAt < 0f ? 0f : now - _footAt;
            RideFacts ride = PlayerRide.Read(player, Flat(position - _lastFoot), seconds);
            Ride = ride;
            bool carried = OffenceRules.Carried(ride);
            if (carried != _carried)
            {
                _carried = carried;
                if (Meta.Dev.Verbose)
                {
                    PoliceAttention.Logger?.Info(carried
                        ? "Police attention check (ride): signs of a ride: " + OffenceRules.Signs(ride) + ": no offence on foot is looked for: PASS"
                        : "Police attention check (ride): no sign of a ride: offences on foot are looked for: PASS");
                }
            }
            return carried;
        }

        // Nothing is looked for: the road timer is forgotten, and the place and time of this look are kept, so that the speed is
        // still measured through a ride and the first look after it starts afresh.
        private void Rest(Vector3 position, float now)
        {
            _roadSince = -1f;
            _lastFoot = position;
            _footAt = now;
            Witness = null;
        }

        /// <summary>One of the mod's officers within sight of a point and being drawn, or null.</summary>
        public static StreetActor OfficerNear(Vector3 position, float within)
        {
            ActorPool pool = ActorPool.Instance;
            if (pool == null)
            {
                return null;
            }
            for (int i = pool.ActorCount - 1; i >= 0; i--)
            {
                StreetActor actor = pool.ActorAt(i);
                if (actor.InUse && actor.IsValid && actor.Look == ActorLook.Officer && actor.Visible && !actor.Limp
                    && (actor.Position - position).sqrMagnitude < within * within)
                {
                    return actor;
                }
            }
            return null;
        }
    }
}
