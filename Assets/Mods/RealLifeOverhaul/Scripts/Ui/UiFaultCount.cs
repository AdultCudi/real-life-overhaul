using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Which screens have already had a fault counted against the interface's guard. The guard switches the whole interface off after a handful of
    /// faults in half a minute; one screen throwing again and again is not that, it is one screen, and it is the game's own after the first. Game-free.
    /// </summary>
    public static class UiFaultCount
    {
        private static readonly HashSet<string> Counted = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>True the first time a screen faults in the session (count it), false after (log it only). A fault with no screen always counts.</summary>
        public static bool Counts(string screenId) => string.IsNullOrEmpty(screenId) || Counted.Add(screenId);

        /// <summary>A new session, and the checks.</summary>
        public static void Reset() => Counted.Clear();
    }
}
