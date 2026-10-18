using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The second patrol car of a long pursuit of the player: it joins after PursuitPace.BackupAfter if the traffic system has
    /// a car, follows the trail a little further back, and says which of the two is nearer the player. No fine and no stop of
    /// its own; a time comparison until it is due.
    /// </summary>
    internal sealed class PursuitBackup
    {
        private const float RetryEvery = 6f;    // seconds between looks for a car to take
        private const int Tries = 3;
        private const float ExtraGap = 9f;      // metres further back than the first car it settles

        private readonly ResponseUnit _unit;
        private float _dueAt;
        private int _tried;

        public PursuitBackup(GameObject host)
        {
            _unit = new ResponseUnit(host);
        }

        /// <summary>True while the second car is on the road and still ours.</summary>
        public bool Out => _unit.InUse && !_unit.IsLost;

        /// <summary>A pursuit has begun: the second car is due some time from now.</summary>
        public void Begin(float now)
        {
            _dueAt = now + PursuitPace.BackupAfter;
            _tried = 0;
        }

        /// <summary>Brings the second car out when it is due; true at the moment it does. A time comparison until then.</summary>
        public bool Tick(float now, Rigidbody quarry, float pace, float gap)
        {
            if (_unit.InUse)
            {
                if (_unit.IsLost)
                {
                    _unit.Release(TrafficTools.Ready, remove: false, quiet: true);   // taken back by the game: no third car
                }
                return false;
            }
            if (now < _dueAt || _tried >= Tries || quarry == null || !TrafficTools.Ready)
            {
                return false;
            }
            _tried++;
            _dueAt = now + RetryEvery;
            Vector3 position = quarry.position;
            Waypoint anchor = TrafficTools.ClosestWaypointAlong(position, quarry.velocity, 40f);
            if (!_unit.Spawn(UnitKind.Police, anchor, Random.Range(90f, 140f), 40f))
            {
                return false;
            }
            _tried = Tries;   // one second car a pursuit
            EmergencyDriver driver = _unit.Driver;
            driver.Begin(_unit.Car, _unit.Path, position, gap + ExtraGap);
            driver.Follow(quarry, gap + ExtraGap);
            driver.CruiseSpeed = pace;
            return true;
        }

        /// <summary>One more point of the player's trail, for the second car to follow.</summary>
        public void Crumb(Vector3 position, int most)
        {
            if (_unit.InUse && _unit.Path.Count < most)
            {
                _unit.Path.Add(position);
            }
        }

        /// <summary>Where the patrol car nearer the quarry is: the first car's place, or the second car's if that is out and nearer.</summary>
        public Vector3 Nearer(Vector3 first, Vector3 quarry)
        {
            if (!Out || _unit.Car == null)
            {
                return first;
            }
            Vector3 second = _unit.Car.transform.position;
            return (second - quarry).sqrMagnitude < (first - quarry).sqrMagnitude ? second : first;
        }

        /// <summary>The pursuit is over: the second car goes back to being traffic. Safe at any time.</summary>
        public void Release(bool resumeTraffic)
        {
            _tried = Tries;
            _unit.Release(resumeTraffic, remove: false, quiet: true);
        }
    }
}
