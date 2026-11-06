namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Where the game's panel box is drawn for a place out of the picture (<see cref="WorldPanel.KeepInView"/>): at the
    /// picture's edge on the place's true side, as the game's guider keeps its arrow at the edge for a place off the
    /// screen. The game's indoor camera (13 m from the player, lens 49) sees the floor from about 5 m behind the player
    /// to 10 m ahead, a box standing up from a place 5 m ahead is already cut off by the top of the screen, and indoors
    /// the game hides its guider: a panel left over a place further off is drawn off the screen and nothing at all says
    /// where to go. Measured in the camera's own space, so a place behind the lens is never mirrored. Game-free, a few
    /// multiplications; checked offline (NavCheck, PanelEdgeCheck.cs).
    /// </summary>
    public static class PanelEdgeRules
    {
        public const float SideMargin = 0.16f;   // of the picture's width kept clear left and right: about half the box's width at 1080p, and a gap
        public const float LowMargin = 0.14f;    // of its height at the bottom: the box stands up from its point (pivot 0.5, 0), over the game's item panel
        public const float HighMargin = 0.34f;   // at the top: the box's own height above its point, and the game's top bar
        public const float Near = 0.05f;         // metres ahead of the lens: at or behind it a place has no point in the picture
        public const float LeastDepth = 1f;      // metres ahead of the lens the edge point is put at for a place behind it

        /// <summary>
        /// The place's point in the picture (0 to 1 across, 0 to 1 up) from where it is in the camera's space (metres:
        /// x right, y up, z ahead) and the lens (the tangent of half its height's angle; its width over its height).
        /// True when the place is out of the picture, or too near its edge for the box, and the point was moved onto
        /// the edge toward it; false with the place's own point. A few multiplications, nothing allocated.
        /// </summary>
        public static bool Edge(float x, float y, float z, float tanHalf, float aspect, out float across, out float up)
        {
            float dx;
            float dy;
            bool ahead = z > Near && tanHalf > 0f && aspect > 0f;
            if (ahead)
            {
                dx = x / (z * tanHalf * aspect);   // -1 to 1 across the picture
                dy = y / (z * tanHalf);
                across = 0.5f + dx * 0.5f;
                up = 0.5f + dy * 0.5f;
                if (across >= SideMargin && across <= 1f - SideMargin && up >= LowMargin && up <= 1f - HighMargin)
                {
                    return false;
                }
            }
            else
            {
                // Behind the lens the picture has no point for it: its side is the side of the camera it is on.
                dx = aspect > 0f ? x / aspect : x;
                dy = y;
                if (dx * dx + dy * dy < 0.000001f)
                {
                    dy = -1f;   // straight behind: below, the way the player turns to it
                }
            }
            float scale = float.MaxValue;
            float right = 1f - 2f * SideMargin;
            float top = 1f - 2f * HighMargin;
            float bottom = 1f - 2f * LowMargin;
            if (dx > 0f)
            {
                scale = right / dx;
            }
            else if (dx < 0f)
            {
                scale = right / -dx;
            }
            if (dy > 0f && top / dy < scale)
            {
                scale = top / dy;
            }
            else if (dy < 0f && bottom / -dy < scale)
            {
                scale = bottom / -dy;
            }
            if (scale == float.MaxValue)
            {
                scale = 0f;
            }
            across = 0.5f + dx * scale * 0.5f;
            up = 0.5f + dy * scale * 0.5f;
            return true;
        }
    }
}
