using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Notes when something of the mod's asked the game to cut the navigation mesh (a carving obstacle switched on, off or
    /// moved). The engine does the cut about half a second later, in a frame in which the mod itself is doing nothing, so
    /// the session record could not name it; with this it can. Two fields written on a change, read once per long frame.
    /// </summary>
    public static class MeshCuts
    {
        public static string Last { get; private set; }

        public static float At { get; private set; } = -100f;

        public static void Note(string what)
        {
            Last = what;
            At = Time.unscaledTime;
        }
    }
}
