using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Whether a patrol car still has the car it is after in sight, and for how long it has not; shared by both pursuits of the
    /// player, so the same driving loses both (buildings in between, or far behind). One line test a look, none when close.
    /// </summary>
    internal struct PursuitSight
    {
        public const float OutOfSight = 10f;   // seconds without line of sight to lose them
        public const float FarAway = 150f;     // metres behind that count as out of sight
        private const float Near = 30f;        // metres: closer than this nothing is asked, they are in sight

        private static int _buildings;
        private static bool _maskRead;

        private float _unseenSince;

        /// <summary>Starts counting afresh: for the start of a pursuit.</summary>
        public void Reset()
        {
            _unseenSince = 0f;
        }

        /// <summary>True if a building stands between two points a little above the road. False if the game's layers cannot be told.</summary>
        public static bool Blocked(Vector3 from, Vector3 to)
        {
            if (!_maskRead)
            {
                _maskRead = true;   // asked once, whether or not the layers exist
                _buildings = LayerMask.GetMask("Buildings", "BuildingsOutlined");
            }
            return _buildings != 0 && Physics.Linecast(from + Vector3.up * 1.5f, to + Vector3.up * 1.5f, _buildings, QueryTriggerInteraction.Ignore);
        }

        /// <summary>True once the pursuer has been without sight of its quarry for <see cref="OutOfSight"/> seconds. Call a few times a second.</summary>
        public bool Lost(Vector3 pursuer, Vector3 quarry, float now)
        {
            float gapSqr = (pursuer - quarry).sqrMagnitude;
            float far = FarAway * Events.Weather.SightFactor;   // rain, mist and the dark shorten it (1 in fair weather)
            bool hidden = gapSqr > far * far || (gapSqr > Near * Near && Blocked(pursuer, quarry));
            if (!hidden)
            {
                _unseenSince = 0f;
                return false;
            }
            if (_unseenSince <= 0f)
            {
                _unseenSince = now;
                return false;
            }
            return now - _unseenSince >= OutOfSight;
        }
    }
}
