namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// When a bar of the game's is written again: the game redraws its needs bars only for a visible step
    /// (Topbar.UpdateStatsBars), and <see cref="GameBar"/> keeps to its finer one. Game-free; checked offline (NavCheck).
    /// </summary>
    public static class BarRule
    {
        public const float Step = 0.005f;   // of the bar's length: the game's own step for its energy bar

        /// <summary>A value for the bar: 0 to 1, and 0 for anything that is no number.</summary>
        public static float Clamp(float value)
        {
            if (!(value > 0f))
            {
                return 0f;   // also what is no number
            }
            return value > 1f ? 1f : value;
        }

        /// <summary>The bar is to be written: nothing shown yet (below zero), the value at an end not yet shown, or more than a step off.</summary>
        public static bool Due(float shown, float value)
        {
            value = Clamp(value);
            if (shown < 0f)
            {
                return true;
            }
            if ((value == 0f || value == 1f) && shown != value)
            {
                return true;
            }
            float off = value - shown;
            return off > Step || off < -Step;
        }

        /// <summary>A stretch of the bar from its centre and width, cut at the bar's ends. False when nothing of it is on the bar.</summary>
        public static bool Span(float centre, float width, out float from, out float to)
        {
            from = Clamp(centre - width * 0.5f);
            to = Clamp(centre + width * 0.5f);
            return width > 0f && to > from;
        }
    }
}
