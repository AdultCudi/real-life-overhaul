using System;

namespace RealLifeOverhaul
{
    /// <summary>What home and health ask of the rest of the mod (Wiring/LifeLinks fills each in).</summary>
    public static class HomeBridges
    {
        /// <summary>
        /// Whether the player is under a roof the game does not know of: up in a hotel room, where the player stands at the
        /// hotel's door in the street while the room's list is open. Neither rain nor the street reaches them there.
        /// Default: no.
        /// </summary>
        public static Func<bool> Sheltered;
    }
}
