using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Notes when the mod changed a carving obstacle of the navigation mesh. The engine cuts about half a second later,
    /// in a frame where the mod does nothing, so the session record needs this to name it. Everything of the mod's that
    /// carves tells this class. COST: a handful of writes into a fixed ring a note; read once a long frame and a minute.
    /// </summary>
    public static class MeshCuts
    {
        private const int Kept = 16;   // notes remembered: more than the mod makes in the seconds a long frame looks back over

        private static readonly CountRing Ring = new CountRing(Kept);

        public static string Last { get; private set; }

        public static float At { get; private set; } = -100f;

        /// <summary>Obstacles changed since the mod started.</summary>
        public static int Total => Ring.Total;

        /// <summary><paramref name="what"/> is a constant text; <paramref name="obstacles"/>, how many obstacles the change touched.</summary>
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

        /// <summary>How many obstacles were changed between two unscaled times, both ends counted.</summary>
        public static int Between(float from, float to) => Ring.Between(from, to);
    }
}
