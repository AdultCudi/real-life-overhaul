using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The second patrol car of a long pursuit of the player. It joins once the pursuit has gone on for
    /// a while (PursuitPace.BackupAfter), if the traffic system has a car to give: put on the road the
    /// player has just driven, some way behind them, it follows their trail like the first and keeps a
    /// little further back. Whoever runs the pursuit (PolicePursuit, Crime/PoliceAttention) owns one of
    /// these, tells it of every trail point, asks it which of the two cars is nearer the player (that
    /// one is the one that must be lost or that makes the stop) and lets it go when the pursuit ends.
    /// It writes no fine and makes no stop of its own. Nothing here runs unless a pursuit is on, and then
    /// it is a time comparison until the car is due.
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

        /// <summary>
        /// Brings the second car out when it is due. Call while the first car is in pursuit; a time
        /// comparison until then, and nothing once it is out or cannot be had.
        /// </summary>
        /// <returns>True at the moment it comes out, for the owner's log line.</returns>
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
