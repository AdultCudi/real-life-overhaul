using System;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The minimap's destination block in plain numbers: how the minutes left are rounded and what the 2D and 3D button says. Nothing of the game in it, so
    /// the checks run every rule. Cost: a few comparisons, on the slow tick.
    /// </summary>
    public static class NavDestRules
    {
        public const int LeastMinutes = 1;      // a route with any of it left is at least one minute: "0 min" reads as arrived
        public const int MostMinutes = 999;     // the block's right edge holds three figures

        /// <summary>
        /// The minutes of the game's clock a route has left, rounded up so the figure never promises more than it takes: <paramref name="seconds"/> is the time
        /// the way takes by the pace the player makes, <paramref name="minutesPerSecond"/> how many minutes the game's clock runs in one of its seconds.
        /// </summary>
        public static int Minutes(float seconds, float minutesPerSecond)
        {
            if (!(seconds > 0f) || !(minutesPerSecond > 0f))
            {
                return LeastMinutes;
            }
            double minutes = Math.Ceiling(seconds * (double)minutesPerSecond - 0.001);
            return (int)Math.Max(LeastMinutes, Math.Min(MostMinutes, minutes));
        }

        /// <summary>
        /// The minutes as the block says them: whole minutes under <see cref="HoursFrom"/>, hours and the minutes over them past that (the game's clock runs a
        /// minute a second, so a long walk is hours of it). The hours are zero for the first kind.
        /// </summary>
        public static void Split(int minutes, out int hours, out int rest)
        {
            minutes = Math.Max(LeastMinutes, Math.Min(MostMinutes, minutes));
            hours = minutes >= HoursFrom ? minutes / 60 : 0;
            rest = minutes - hours * 60;
        }

        public const int HoursFrom = 100;       // minutes from which the block says hours

        /// <summary>The words on the minimap's picture switch: what the picture is now, so the player reads the state, not the action.</summary>
        public static string Switch(bool tilted) => tilted ? "3D" : "2D";

        /// <summary>The saved choice as the switch keeps it: tilted unless the player turned it flat (0). A value that is not 0 or 1 is the default.</summary>
        public static bool TiltedFrom(int saved) => saved != 0;
    }
}
