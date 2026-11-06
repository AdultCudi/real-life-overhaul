namespace RealLifeOverhaul
{
    /// <summary>
    /// The limits of the picture settings: a distance is a share of the game's (100 changes nothing), the cascades a choice
    /// whose first is the game's; neither goes above the game. Checked offline (NavCheck, RenderLevelCheck.cs).
    /// </summary>
    public static class RenderLevelRules
    {
        public const int Least = 25;           // per cent of the game's distance: the shortest a slider offers
        public const int Game = 100;           // per cent: the game's own distance, nothing changed
        public const int GameCascades = 0;     // the cascades choice that leaves the game's own
        public const int MostCascades = 4;     // the most cascades the render pipeline has
        public const int TrialPercent = 50;    // per cent: the benchmark's rows try half the game's distance
        public const int TrialCascades = 1;    // the benchmark's row tries one cascade

        // the pipeline's own splits for three and four cascades, fractions of the shadow distance
        private static readonly float[] ThreeSplits = { 0.1f, 0.3f };
        private static readonly float[] FourSplits = { 0.05f, 0.15f, 0.3f };

        /// <summary>A distance setting within its limits.</summary>
        public static int Percent(int value) => value < Least ? Least : value > Game ? Game : value;

        /// <summary>A cascades setting within its limits: 0 (the game's own) to <see cref="MostCascades"/>.</summary>
        public static int Cascades(int value) => value < GameCascades ? GameCascades : value > MostCascades ? MostCascades : value;

        /// <summary>True when a distance setting changes anything.</summary>
        public static bool Changes(int percent) => Percent(percent) < Game;

        /// <summary>The game's distance (metres, or a level-of-detail bias) taken to a setting's share of it.</summary>
        public static float Scaled(float game, int percent) => game <= 0f ? game : game * Percent(percent) / Game;

        /// <summary>Writes the pipeline's splits for three or four cascades into <paramref name="into"/>; how many (none for one or two).</summary>
        public static int Splits(int cascades, float[] into)
        {
            float[] splits = cascades == 3 ? ThreeSplits : cascades == 4 ? FourSplits : null;
            if (splits == null || into == null || into.Length < splits.Length)
            {
                return 0;
            }
            for (int i = 0; i < splits.Length; i++)
            {
                into[i] = splits[i];
            }
            return splits.Length;
        }
    }
}
