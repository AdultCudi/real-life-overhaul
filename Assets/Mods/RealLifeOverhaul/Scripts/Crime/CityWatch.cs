namespace RealLifeOverhaul
{
    /// <summary>
    /// Police patrols stepped up in the district the player stands in: a crime reported on Flare that the City Office
    /// acted on (Social/Flare, through Wiring/FlareLinks, which sets this once a game hour from where the player is). While
    /// it holds, crime in the street there is rarer (CrimeDirector) and an offence of the player's is likelier to be seen
    /// (PoliceAttention). A dark street lamp of the city's faults near the player after dark (Scripts/Faults, through
    /// Wiring/FaultLinks, when it changes) makes street crime there likelier. Two field reads where those already roll
    /// their chances; nothing per frame of its own.
    /// </summary>
    public static class CityWatch
    {
        public const float PatrolCrime = 0.6f;   // of the street crime there while the police patrol it
        public const float PatrolSeen = 1.3f;    // ...and how much likelier an offence is to be seen

        private static bool _patrol;
        private static float _dark = 1f;

        /// <summary>What street crime is scaled by here and now: 1 without patrols or a dark street.</summary>
        public static float CrimeScale { get; private set; } = 1f;

        /// <summary>What the chance of an offence being seen is scaled by here and now: 1 without patrols.</summary>
        public static float SeenScale { get; private set; } = 1f;

        /// <summary>Whether the police patrol the district the player stands in, as of this hour.</summary>
        public static void Set(bool patrol)
        {
            _patrol = patrol;
            CrimeScale = (patrol ? PatrolCrime : 1f) * _dark;
            SeenScale = patrol ? PatrolSeen : 1f;
        }

        /// <summary>Street crime where the player stands scaled by a dark street (1 for none).</summary>
        public static void Dark(float scale)
        {
            _dark = scale > 0f ? scale : 1f;
            CrimeScale = (_patrol ? PatrolCrime : 1f) * _dark;
        }
    }
}
