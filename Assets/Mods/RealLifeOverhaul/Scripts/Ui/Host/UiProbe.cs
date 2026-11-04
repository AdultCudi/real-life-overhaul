using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The load-time self-check of what a kit screen needs from the game's own screen: one line in Player.log per object,
    /// "found" or "MISSING <what>", and the reason a screen's Check returns (so the game's own view stays when it is missing).
    /// </summary>
    public static class UiProbe
    {
        /// <summary>Writes the line for one needed object and returns null when it is there, else the reason it is missing.</summary>
        public static string Need(string screen, string what, bool found)
        {
            UiDirector.Logger?.Info("UI: " + screen + ": " + (found ? "found " + what : "MISSING " + what));
            return found ? null : what + " is not there";
        }

        /// <summary>The same for an object at a hierarchy path under the game's screen (one a typed field cannot reach).</summary>
        public static string Path(string screen, Transform root, string path) => Need(screen, path, root != null && root.Find(path) != null);
    }
}
