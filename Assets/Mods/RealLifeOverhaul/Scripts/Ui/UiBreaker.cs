using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The kit's circuit breaker, one per screen: a screen the kit faulted on (another mod's piece made it throw) is the
    /// game's own for the rest of the session, and the rest of the interface goes on. Game-free; the screen switches ask it
    /// (<see cref="UiScreenSwitches.On"/>), so every path that draws a screen sees the same answer. Cost: a set look-up,
    /// and none at all while no breaker is open.
    /// </summary>
    public static class UiBreaker
    {
        private static readonly HashSet<string> Open = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Opens the breaker of a screen; true the first time (the caller logs once).</summary>
        public static bool Trip(string screenId) => !string.IsNullOrEmpty(screenId) && Open.Add(screenId);

        /// <summary>Closes a screen's breaker again (the kit's own fault, in a city that is gone: the screen is asked once more).</summary>
        public static bool Untrip(string screenId) => screenId != null && Open.Remove(screenId);

        /// <summary>True once the screen is the game's own for the session.</summary>
        public static bool Tripped(string screenId) => Open.Count > 0 && screenId != null && Open.Contains(screenId);

        /// <summary>How many screens are the game's own because of a fault of another mod's piece.</summary>
        public static int Count => Open.Count;

        /// <summary>The breakers closed: a new session, and the checks.</summary>
        public static void Reset() => Open.Clear();
    }
}
