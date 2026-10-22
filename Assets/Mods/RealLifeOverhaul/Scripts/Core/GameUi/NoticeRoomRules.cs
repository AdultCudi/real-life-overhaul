namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// How far the game's stack of notifications (top right, under the top bar) goes down to start under the mod's info panel
    /// there (ShiftCard, a copy of the game's job panel). The game places its job panel and its notifications each by its own
    /// canvas, with no rule for the two: so the panel stays where the game's job panel stands, and the stack starts a gap under
    /// it while it shows. Game-free arithmetic on screen pixels, y upwards, nothing allocated; checked offline (NavCheck PolishCheck).
    /// </summary>
    public static class NoticeRoomRules
    {
        public const float Gap = 16f;   // canvas units between the panel's foot and the first notification: the gap ShiftCard keeps under the game's job panel

        /// <summary>
        /// Canvas units the stack goes down from its own place (<paramref name="stackTop"/>, screen pixels): none while no panel
        /// shows (<paramref name="panelBottom"/>, the lowest panel foot in screen pixels, NaN) or the stack is already a gap under
        /// it; else the overlap and the gap, in the stack's units (<paramref name="scale"/>: screen pixels to one, above 0).
        /// </summary>
        public static float Drop(float stackTop, float panelBottom, float scale)
        {
            if (float.IsNaN(panelBottom) || float.IsNaN(stackTop) || !(scale > 0f))
            {
                return 0f;
            }
            float want = panelBottom - Gap * scale;   // the stack's top a gap under the panel's foot
            return stackTop > want ? (stackTop - want) / scale : 0f;
        }

        /// <summary>The lower of two feet in screen pixels, NaN meaning none.</summary>
        public static float Lowest(float lowest, float bottom)
        {
            return float.IsNaN(lowest) || bottom < lowest ? bottom : lowest;
        }

        /// <summary>The stack must move again: the drop differs from the one it has by a visible step.</summary>
        public static bool Moves(float now, float want)
        {
            return System.Math.Abs(now - want) > 0.5f;
        }
    }
}
