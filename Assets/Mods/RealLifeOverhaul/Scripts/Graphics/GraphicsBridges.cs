using System;

namespace RealLifeOverhaul.Gfx
{
    /// <summary>
    /// What the picture asks of the rest of the mod; each optional with a default, filled in by Wiring/Wiring.GraphicsLinks.cs.
    /// Asked when the picture is worked out again (once a second of game time at most), never per frame.
    /// </summary>
    public static class GraphicsBridges
    {
        /// <summary>The season the city shows (CalendarRules.Winter to Autumn), or -1 while the seasons are off or stand down. Default: -1.</summary>
        public static Func<int> Season;

        /// <summary>How hard snow falls here now, 0 to 1 (Seasons). Default: 0.</summary>
        public static Func<float> SnowFalling;

        /// <summary>How much snow lies, 0 to 1 (Seasons). Default: 0.</summary>
        public static Func<float> SnowCover;

        /// <summary>Every bridge back to its default.</summary>
        public static void Reset()
        {
            Season = null;
            SnowFalling = null;
            SnowCover = null;
        }
    }
}
