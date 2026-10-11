namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// When a bar of the game's is written again, and where a mark on it stands. The game writes its own needs bars
    /// only when what they show would differ visibly: by more than half a hundredth for energy, a hundredth for the
    /// others, and always when the bar runs empty (UI.Topbar.Topbar.UpdateStatsBars). <see cref="GameBar"/> keeps to
    /// the finer of the two for everything it draws, so a marker that crosses the bar in a second moves two units of
    /// the game's 412 at a time and is written at most two hundred times a pass, and a bar that stands still is
    /// never written at all.
    ///
    /// Plain arithmetic: no game types, nothing allocated. Checked offline (NavCheck).
    /// </summary>
    public static class BarRule
    {
        public const float Step = 0.005f;   // of the bar's length: the game's own step for its energy bar (Topbar.UpdateStatsBars)

        /// <summary>A value brought into the bar: 0 to 1, and 0 for anything that is not a number.</summary>
        public static float Clamp(float value)
        {
            if (!(value > 0f))
            {
                return 0f;   // (also what is not a number)
            }
            return value > 1f ? 1f : value;
        }

        /// <summary>
        /// Whether the bar is to be written: nothing is shown yet (<paramref name="shown"/> below zero), the value has
        /// reached an end the bar does not show yet, or it differs from what is shown by more than a step.
        /// </summary>
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

        /// <summary>
        /// A stretch of the bar from its centre and width, cut off at the bar's ends. False when nothing of it is on
        /// the bar (no width, or wholly outside).
        /// </summary>
        public static bool Span(float centre, float width, out float from, out float to)
        {
            from = Clamp(centre - width * 0.5f);
            to = Clamp(centre + width * 0.5f);
            return width > 0f && to > from;
        }
    }
}
