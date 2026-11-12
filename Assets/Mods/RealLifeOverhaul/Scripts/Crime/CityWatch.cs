namespace RealLifeOverhaul
{
    /// <summary>
    /// How the player's district scales crime: patrols after a Flare report make street crime rarer and offences likelier
    /// seen; a dark street lamp makes street crime likelier. Set by Wiring; nothing per frame.
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

        /// <summary>The district the player stands in against the city's crime, from its residents (the living population: income, unemployment, the young); set by Wiring, null for none.</summary>
        public static System.Func<float> Residents;

        /// <summary>What street crime is scaled by for the residents where the player stands: 1 with none. Asked at each look for a scene, a few times a minute.</summary>
        public static float ResidentsScale
        {
            get
            {
                try
                {
                    float scale = Residents != null ? Residents() : 1f;
                    return scale > 0f && !float.IsNaN(scale) && !float.IsInfinity(scale) ? scale : 1f;
                }
                catch (System.Exception)
                {
                    return 1f;   // the street's own crime stands
                }
            }
        }

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
