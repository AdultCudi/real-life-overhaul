using System;

namespace RealLifeOverhaul
{
    /// <summary>What life events ask of other features, filled in by Wiring. Null: no home.</summary>
    public static class LifeBridges
    {
        /// <summary>A residential building the player rents or owns, wherever they are now.</summary>
        public static Func<bool> HasHome;
    }
}
