using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How fast a patrol car after the player's car may go, and how long it keeps at it. Shared by both
    /// pursuits of the player (Crime/PoliceAttention after a traffic stop, PolicePursuit after a
    /// hit-and-run). The player's cars reach 130 to 290 km/h since Driving/CarTuning; a patrol car held to
    /// 83 km/h lost every one of them on the first straight. Its top speed is now set once, when it
    /// comes out, from the car it is after: a little under that car's own top speed, never under 130 and
    /// never over 200 km/h. A van or a saloon cannot simply drive away from it; the fastest cars still can,
    /// given a long clear road, and anyone can by putting buildings between them (PursuitSight). It is not
    /// tied to the player's speed in any other way: nothing speeds it up when it falls behind. Corners,
    /// traffic and how quickly it gathers speed are the driver's business (EmergencyDriver, a fast drive).
    /// A car that does not carry the mod's performance (the option is off) is as slow as the game made it,
    /// and is chased at the old speed.
    /// </summary>
    internal static class PursuitPace
    {
        public const float Stock = 23f;            // m/s, about 83 km/h: after a car with the game's own performance
        public const float Share = 0.95f;          // of the top speed of the car it is after
        public const float LeastKmh = 130f;
        public const float MostKmh = 200f;

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
