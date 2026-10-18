using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How fast a patrol car after the player's car may go: a little under that car's own top speed (Driving/CarTuning), 130 to
    /// 200 km/h, or 83 km/h after a car without the mod's performance; and how long it keeps at it. For both pursuits.
    /// </summary>
    internal static class PursuitPace
    {
        public const float Stock = 23f;            // m/s, about 83 km/h: after a car with the game's own performance
        public const float Share = 0.95f;          // of the top speed of the car it is after
        public const float LeastKmh = 130f;        // km/h
        public const float MostKmh = 200f;         // km/h

        public const float StartGrace = 15f;       // seconds a fast patrol car has to get up to speed before it can be called left behind
        public const float BackupAfter = 45f;      // seconds of pursuit before a second patrol car joins, if one can be had
        public const float CloseEnough = 90f;      // metres: a patrol car this close, with the car in sight, does not give up when the time is up
        public const float Extension = 15f;        // ...but carries on this many seconds at a time

        /// <summary>The top speed, in m/s, for a patrol car that is after this car.</summary>
        public static float For(Rigidbody quarry)
        {
            CarTuning tuning = CarTuning.Instance;
            var tuned = tuning != null ? tuning.Tuned : null;
            if (quarry == null || tuned == null || tuned.gameObject != quarry.gameObject || tuning.TargetTopKmh <= 0f)
            {
                return Stock;
            }
            return Mathf.Clamp(tuning.TargetTopKmh * Share, LeastKmh, MostKmh) / 3.6f;
        }
    }
}
