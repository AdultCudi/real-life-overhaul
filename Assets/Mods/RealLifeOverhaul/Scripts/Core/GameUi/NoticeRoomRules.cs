namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// How far the game's stack of notifications (top right, under the top bar) goes down so that it starts under the
    /// mod's info panel standing there: a copy of the game's job panel for a shift, a lesson or a test, an outing's plan
    /// (Jobs/ShiftCard). The game itself has no rule for the two: its own job panel and its notifications stand in the
    /// same corner, each placed by its own canvas. So the panel stays where the game's job panel stands, and the stack of
    /// notifications starts a gap under it while it shows, as a list under a heading; it goes back to the game's own place
    /// the moment no panel shows.
    ///
    /// Plain arithmetic on screen pixels, y upwards: no game types, nothing allocated. Checked offline (NavCheck, PolishCheck).
    /// </summary>
    public static class NoticeRoomRules
    {
        public const float Gap = 16f;   // canvas units between the panel's foot and the first notification: the gap ShiftCard keeps under the game's own job panel

        /// <summary>
        /// Canvas units the stack goes down from its own place: none while no panel shows (<paramref name="panelBottom"/>
        /// NaN), or while the stack's own top is a gap under the panel's foot already; otherwise the overlap and the gap,
        /// in the stack's canvas units (<paramref name="scale"/>: screen pixels to one of them, above 0).
        /// </summary>
        /// <param name="stackTop">Where the stack's top stands at its own place, in screen pixels.</param>
        /// <param name="panelBottom">The lowest foot of the mod's panels showing, in screen pixels; NaN for none.</param>
        public static float Drop(float stackTop, float panelBottom, float scale)
        {
            if (float.IsNaN(panelBottom) || float.IsNaN(stackTop) || !(scale > 0f))
            {
                return 0f;
            }
            float want = panelBottom - Gap * scale;   // the stack's top a gap under the panel's foot
            return stackTop > want ? (stackTop - want) / scale : 0f;
        }

        /// <summary>The lower of two feet in screen pixels, NaN meaning none: the panels showing are taken one by one.</summary>
        public static float Lowest(float lowest, float bottom)
        {
            return float.IsNaN(lowest) || bottom < lowest ? bottom : lowest;
        }

        /// <summary>True when the stack must be moved again: the drop differs from the one it has by a visible step.</summary>
        public static bool Moves(float now, float want)
        {
            return System.Math.Abs(now - want) > 0.5f;
        }
    }
}
