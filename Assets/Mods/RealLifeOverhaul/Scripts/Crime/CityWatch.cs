namespace RealLifeOverhaul
{
    /// <summary>
    /// How the district the player stands in scales crime: patrols stepped up after a Flare report make street crime rarer
    /// and an offence likelier to be seen; a dark street lamp makes street crime likelier. Set by Wiring once an hour or on a
    /// change, read where the chances are rolled; nothing per frame.
    /// </summary>
    public static class CityWatch
    {
        public const float PatrolCrime = 0.6f;   // times the street crime while the police patrol
        public const float PatrolSeen = 1.3f;    // times the chance an offence is seen while they do

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
