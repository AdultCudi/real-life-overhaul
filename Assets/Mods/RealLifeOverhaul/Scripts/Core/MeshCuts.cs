using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Notes when something of the mod's asked the game to cut the navigation mesh (a carving obstacle made, switched
    /// on or off, or moved). The engine does the cut about half a second later, in a frame in which the mod itself is
    /// doing nothing, so the session record could not name it; with this it can, and it can say how many obstacles
    /// were changed in the second before a long frame. Everything of the mod's that carves tells this class, so the
    /// record's count is the whole of the mod's share (the game's own 3,000-odd obstacles stand still and are not
    /// counted). A note is a handful of writes into a fixed ring; nothing is read except once per long frame and once
    /// a minute.
    /// </summary>
    public static class MeshCuts
    {
        private const int Kept = 16;   // notes remembered: more than the mod makes in the seconds a long frame looks back over

        private static readonly CountRing Ring = new CountRing(Kept);

        public static string Last { get; private set; }

        public static float At { get; private set; } = -100f;

        /// <summary>Obstacles changed since the mod started: the session record's line a minute shows the minute's share.</summary>
        public static int Total => Ring.Total;

        /// <summary>A constant text only; <paramref name="obstacles"/> is how many carving obstacles the one change touched.</summary>
        public static void Note(string what, int obstacles = 1)
        {
            if (obstacles <= 0)
            {
                return;
            }
            Last = what;
            At = Time.unscaledTime;
            Ring.Note(At, obstacles);
        }

        /// <summary>How many obstacles were changed between two times (unscaled seconds, both ends counted).</summary>
        public static int Between(float from, float to) => Ring.Between(from, to);
    }
}
