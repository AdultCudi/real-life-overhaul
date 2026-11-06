using System;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>What the seasons ask of the rest of the mod; each optional with a default, filled in by Wiring/Wiring.SeasonLinks.cs.</summary>
    public static class SeasonBridges
    {
        /// <summary>This save's own number, the one the street events' calendar is rolled from and Going Public shares (Events). Default: 0.</summary>
        public static Func<int> Salt;

        /// <summary>Every bridge back to its default.</summary>
        public static void Reset()
        {
            Salt = null;
        }
    }
}
