using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>A kind of offence by the player. DrunkDriving is not in the road data: it is found at a stop or reported by Leisure.</summary>
    internal enum Offence { None, RedLight, Speeding, Reckless, WrongSide, Jaywalk, CrossOnRed, DrunkDriving }

    /// <summary>
    /// Notices what the player does wrong on the road: the stop line ten times a second, speed and position twice a second,
    /// on foot only with an officer near. Each look is a few subtractions against RoadMap; the one physics query (a
    /// crossing?) runs only with an officer near.
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

        /// <summary>The officer who saw the last offence on foot, to be given a line.</summary>
        public StreetActor Witness { get; private set; }

        public void Reset()
        {
            _line = -1;
            _redSince = -1f;
            _speedingSince = -1f;
            _speedingBooked = false;
            _pavementSince = -1f;
            _wrongSince = -1f;
            _roadSince = -1f;
            Witness = null;
        }

        /// <summary>Looks at the player's car: an offence the moment one is complete, else None.</summary>
        public Offence Driving(Rigidbody body, float now)
        {
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
                // Watched at any speed, so pulling away through a red light counts too.
                Vector3 heading = slow ? Flatten(body.rotation * Vector3.forward) : velocity / Mathf.Sqrt(sqrSpeed);
                if (RedLight(body.position, heading, now) && sqrSpeed > CrawlSpeed * CrawlSpeed)
                {
                    offence = Offence.RedLight;
                }
            }
            if (!slow && offence == Offence.None && now >= _nextSlow)
            {
                _nextSlow = now + SlowInterval;
                offence = Slow(body.position, velocity, Mathf.Sqrt(sqrSpeed), now);
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

        private Offence Slow(Vector3 position, Vector3 velocity, float speed, float now)
        {
            if (!RoadMap.NearestLane(position, out float distance, out Vector3 runs, out float limit))
            {
                _speedingSince = -1f;
                _pavementSince = -1f;
                _wrongSince = -1f;
                return Offence.None;
            }

            // Well over the limit for a few seconds; once until they slow down again.
            if (speed * 3.6f > limit + CrimeLaw.SpeedingMarginKmh && distance < 8f)
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
            if (speed > 8f && distance > 5f && NavMesh.SamplePosition(position, out NavMeshHit hit, 0.6f, StreetSpots.Pavement)
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
            if (speed > 10f && distance < 1.8f && Vector3.Dot(runs, velocity) < -0.85f * speed)
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

        /// <summary>Looks at the player on foot with an officer close enough to see: against the lights, or in the road.</summary>
        public Offence OnFoot(Vector3 position, float now)
        {
            if (now < _nextSlow)
            {
                return Offence.None;
            }
            _nextSlow = now + SlowInterval;
            Witness = OfficerNear(position, OfficerSight);
            // Waiting at the kerb or standing at a car door is no offence.
            bool walking = Flat(position - _lastFoot) > 0.3f * 0.3f;
            _lastFoot = position;
            if (Witness == null || !walking || !RoadMap.Ensure())
            {
                _roadSince = -1f;
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
                // The game's own rule for its pedestrians: walk while the cars' red lamp is lit.
                mayWalk = link == null || link.redTrafficLight == null || link.redTrafficLight.activeSelf;
            }
            if (crossing)
            {
                bool offence = !mayWalk && _roadSince >= 0f && now - _roadSince >= OnRoadSeconds;
                if (mayWalk)
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
            bool onRoad = !NavMesh.SamplePosition(position, out _, 0.4f, StreetSpots.Pavement)
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
