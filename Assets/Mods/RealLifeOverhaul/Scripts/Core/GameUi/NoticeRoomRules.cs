namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// How far the game's stack of notifications goes down to start under the mod's info panel in the top right, which
    /// stands where the game's job panel does. Game-free, screen pixels y upwards; checked offline (NavCheck PolishCheck).
    /// </summary>
    public static class NoticeRoomRules
    {
        public const float Gap = 16f;   // canvas units between the panel's foot and the first notification: the gap ShiftCard keeps under the game's job panel

        /// <summary>Canvas units the stack goes down: none with no panel (NaN) or room already; else the overlap and the gap.</summary>
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
