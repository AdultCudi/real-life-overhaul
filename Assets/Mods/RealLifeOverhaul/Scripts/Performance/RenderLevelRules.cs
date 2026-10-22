namespace RealLifeOverhaul
{
    /// <summary>
    /// The limits of the performance section's picture settings (Tweaks/ShadowReach.cs): how far the sun's shadows reach and
    /// in how many cascades, and how far away things keep their detailed models. Each is the game's own until the player
    /// moves it: a distance is a percentage of the game's (100 changes nothing), the cascades a choice whose first entry is
    /// the game's own. Nothing here makes the picture richer than the game's: the sliders go down from the game's value.
    /// The benchmark's trial rows use <see cref="TrialPercent"/> and <see cref="TrialCascades"/>. No Unity or game types:
    /// checked offline (NavCheck, RenderLevelCheck.cs).
    /// </summary>
    public static class RenderLevelRules
    {
        public const int Least = 25;           // per cent of the game's distance: the shortest a slider offers
        public const int Game = 100;           // per cent: the game's own distance, nothing changed
        public const int GameCascades = 0;     // the cascades choice that leaves the game's own
        public const int MostCascades = 4;     // the most cascades the render pipeline has
        public const int TrialPercent = 50;    // per cent: the benchmark's rows try half the game's distance
        public const int TrialCascades = 1;    // the benchmark's row tries one cascade

        // The pipeline's own splits for three and four cascades, as fractions of the shadow distance; with two the game's
        // own first split is kept, with one there is none.
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

        /// <summary>
        /// The splits to set for a number of cascades (fractions of the distance, rising, each under 1): none for one, none
        /// for two (the game's own first split is kept), the pipeline's own for three and four. Written into
        /// <paramref name="into"/> (three places); the count written.
        /// </summary>
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
