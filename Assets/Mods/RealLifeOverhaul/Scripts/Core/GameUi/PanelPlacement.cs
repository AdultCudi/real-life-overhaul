namespace RealLifeOverhaul.GameUi
{
    /// <summary>Where one of the mod's panels stands in relation to the game's building box.</summary>
    public enum PanelSide
    {
        Home = 0,   // over its own place: the building box is not up, or clear of it
        Left,
        Right,
        Above,
        Below,
        Wait        // no side of the building box has room: not shown until one has
    }

    /// <summary>
    /// The one rule for two boxes on the screen: the game's building box (the card over a shop's door while the pointer is
    /// on the shop) and a panel of the mod's over a place (<see cref="WorldPanel"/>). The game has no such rule: each is
    /// placed by its own script and they never meet there, since its own panels stand where no building box comes up. Where
    /// the game does have two boxes for one thing it shows one and holds the other back (OverlayManager.ShowDetailedOverlay).
    /// So the building box never moves; the panel steps to a side with room, left or right first, then above or below, and
    /// waits where none has. It keeps its side while that has room, and goes home only once its home is clear by a wider
    /// margin than sent it away, so it never flickers. Game-free arithmetic, nothing allocated; checked offline (NavCheck).
    /// All figures are screen pixels, y upwards.
    /// </summary>
    public static class PanelPlacement
    {
        public const float Gap = 14f;        // pixels between the two boxes: the game's own gap between HUD panels at 1080p
        public const float Keep = 28f;       // pixels more than Gap the home must be clear before the panel goes back, so a swaying camera does not send it to and fro
        public const float Moved = 6f;       // pixels a box must move on the screen before the two are measured again
        public const float MovedSqr = Moved * Moved;

        /// <summary>The two rectangles share any area, the first grown by <paramref name="margin"/> on every side.</summary>
        public static bool Overlap(float aLeft, float aBottom, float aRight, float aTop, float bLeft, float bBottom, float bRight, float bTop, float margin)
        {
            return bLeft < aRight + margin && bRight > aLeft - margin && bBottom < aTop + margin && bTop > aBottom - margin;
        }

        /// <summary>
        /// Where the panel goes: <paramref name="boxUp"/> whether the building box is on the screen, the panel's rectangle as at
        /// home, <paramref name="now"/> its side at this moment; <paramref name="dx"/> and <paramref name="dy"/> the pixels from
        /// home, right and up.
        /// </summary>
        public static PanelSide Place(bool boxUp, float boxLeft, float boxBottom, float boxRight, float boxTop,
                                      float panelLeft, float panelBottom, float panelRight, float panelTop,
                                      float screenWidth, float screenHeight, PanelSide now, out float dx, out float dy)
        {
            dx = 0f;
            dy = 0f;
            if (!boxUp || boxRight <= boxLeft || boxTop <= boxBottom || panelRight <= panelLeft || panelTop <= panelBottom)
            {
                return PanelSide.Home;
            }
            float margin = now == PanelSide.Home ? Gap : Gap + Keep;
            if (!Overlap(boxLeft, boxBottom, boxRight, boxTop, panelLeft, panelBottom, panelRight, panelTop, margin))
            {
                return PanelSide.Home;
            }
            // The side it is on, while that side still has room.
            if (now != PanelSide.Home && now != PanelSide.Wait
                && Fits(now, boxLeft, boxBottom, boxRight, boxTop, panelLeft, panelBottom, panelRight, panelTop, screenWidth, screenHeight, out dx, out dy))
            {
                return now;
            }
            // Left or right, whichever is the shorter step; then above or below.
            PanelSide side = Nearer(PanelSide.Left, PanelSide.Right, boxLeft, boxBottom, boxRight, boxTop, panelLeft, panelBottom, panelRight, panelTop, screenWidth, screenHeight, out dx, out dy);
            if (side == PanelSide.Wait)
            {
                side = Nearer(PanelSide.Above, PanelSide.Below, boxLeft, boxBottom, boxRight, boxTop, panelLeft, panelBottom, panelRight, panelTop, screenWidth, screenHeight, out dx, out dy);
            }
            return side;
        }

        // Of two sides the one with room and the shorter step, the first if equal; Wait if neither has room.
        private static PanelSide Nearer(PanelSide first, PanelSide second, float boxLeft, float boxBottom, float boxRight, float boxTop,
                                        float panelLeft, float panelBottom, float panelRight, float panelTop,
                                        float screenWidth, float screenHeight, out float dx, out float dy)
        {
            bool a = Fits(first, boxLeft, boxBottom, boxRight, boxTop, panelLeft, panelBottom, panelRight, panelTop, screenWidth, screenHeight, out float ax, out float ay);
            bool b = Fits(second, boxLeft, boxBottom, boxRight, boxTop, panelLeft, panelBottom, panelRight, panelTop, screenWidth, screenHeight, out float bx, out float by);
            if (a && (!b || ax * ax + ay * ay <= bx * bx + by * by))
            {
                dx = ax;
                dy = ay;
                return first;
            }
            if (b)
            {
                dx = bx;
                dy = by;
                return second;
            }
            dx = 0f;
            dy = 0f;
            return PanelSide.Wait;
        }

        /// <summary>
        /// The step that puts the panel on one side of the box, the gap away, and whether the whole panel is then on the
        /// screen. A panel already further out on that side is not drawn in: its step is nothing.
        /// </summary>
        public static bool Fits(PanelSide side, float boxLeft, float boxBottom, float boxRight, float boxTop,
                                float panelLeft, float panelBottom, float panelRight, float panelTop,
                                float screenWidth, float screenHeight, out float dx, out float dy)
        {
            dx = 0f;
            dy = 0f;
            switch (side)
            {
                case PanelSide.Left:
                    dx = boxLeft - Gap - panelRight;
                    dx = dx < 0f ? dx : 0f;
                    break;
                case PanelSide.Right:
                    dx = boxRight + Gap - panelLeft;
                    dx = dx > 0f ? dx : 0f;
                    break;
                case PanelSide.Above:
                    dy = boxTop + Gap - panelBottom;
                    dy = dy > 0f ? dy : 0f;
                    break;
                case PanelSide.Below:
                    dy = boxBottom - Gap - panelTop;
                    dy = dy < 0f ? dy : 0f;
                    break;
                default:
                    return false;
            }
            return panelLeft + dx >= 0f && panelRight + dx <= screenWidth && panelBottom + dy >= 0f && panelTop + dy <= screenHeight;
        }

        /// <summary>
        /// The two boxes are to be measured again: the building box came or went, or either moved more than
        /// <see cref="Moved"/> on the screen since last measured. Squared distances, so nothing is rooted.
        /// </summary>
        public static bool Due(bool boxUp, bool boxWasUp, float panelMovedSqr, float boxMovedSqr)
        {
            if (boxUp != boxWasUp)
            {
                return true;
            }
            return boxUp && (panelMovedSqr > MovedSqr || boxMovedSqr > MovedSqr);
        }

        /// <summary>The side as a word for the log.</summary>
        public static string Word(PanelSide side)
        {
            switch (side)
            {
                case PanelSide.Left: return "moved to the left of it";
                case PanelSide.Right: return "moved to the right of it";
                case PanelSide.Above: return "moved above it";
                case PanelSide.Below: return "moved below it";
                case PanelSide.Wait: return "no side of it has room, so the panel waits";
                default: return "back over its own place";
            }
        }
    }
}
