namespace RealLifeOverhaul
{
    /// <summary>
    /// Police patrols stepped up in the district the player stands in: a crime reported on Flare that the City Office
    /// acted on (Social/Flare, through Wiring/FlareLinks, which sets this once a game hour from where the player is). While
    /// it holds, crime in the street there is rarer (CrimeDirector) and an offence of the player's is likelier to be seen
    /// (PoliceAttention). Two field reads where those already roll their chances; nothing per frame of its own.
    /// </summary>
    public static class CityWatch
    {
        public const float PatrolCrime = 0.6f;   // of the street crime there while the police patrol it
        public const float PatrolSeen = 1.3f;    // ...and how much likelier an offence is to be seen

        /// <summary>What street crime is scaled by here and now: 1 without patrols.</summary>
        public static float CrimeScale { get; private set; } = 1f;

        /// <summary>What the chance of an offence being seen is scaled by here and now: 1 without patrols.</summary>
        public static float SeenScale { get; private set; } = 1f;

        /// <summary>Whether the police patrol the district the player stands in, as of this hour.</summary>
        public static void Set(bool patrol)
        {
            CrimeScale = patrol ? PatrolCrime : 1f;
            SeenScale = patrol ? PatrolSeen : 1f;
        }
    }
}
