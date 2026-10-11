namespace RealLifeOverhaul.GameUi
{
    /// <summary>Where one of the mod's panels stands in relation to the game's building box.</summary>
    public enum PanelSide
    {
        Home = 0,   // over its own place: the building box is not up, or is clear of it
        Left,
        Right,
        Above,
        Below,
        Wait        // no side of the building box has room on the screen: the panel is not shown until one has
    }

    /// <summary>
    /// The one rule for two boxes on the screen: the game's building box (the card that comes up over a shop's door
    /// while the pointer is on the shop) and a panel of the mod's over a place in the world (<see cref="WorldPanel"/>).
    ///
    /// The game has no such rule of its own to join. Its building box and its panel over a place are placed each by
    /// its own script from a point in the world and neither knows of the other (BuildingResume.LateUpdate,
    /// OverlayUI.Update); they do not meet in the game because the game's own panels stand at filling stations and
    /// lifts, where no building box comes up. Where the game does have two boxes for one thing (the small and the
    /// large card over an item indoors) it shows one and holds the other back (OverlayManager.ShowDetailedOverlay).
    /// So: the building box is the game's and never moves; the mod's panel steps to the side of it that has room,
    /// left or right first, then above or below, and where no side has room it waits, as the game's small card does.
    ///
    /// Once to a side the panel stays on that side for as long as that side has room (no hopping from one side to the
    /// other as the camera turns), and it goes home only when its home is clear of the box by a margin wider than the
    /// one that sent it away (no flicker at the edge).
    ///
    /// Plain arithmetic on eight numbers: no game types, nothing allocated. Checked offline (NavCheck).
    /// All figures are pixels of the screen, y upwards.
    /// </summary>
    public static class PanelPlacement
    {
        public const float Gap = 14f;        // pixels kept between the two boxes: the width of the game's own gap between HUD panels at 1080p
        public const float Keep = 28f;       // pixels more than Gap the panel's home must be clear of the box before it goes back: twice the gap, so a camera that sways does not send it to and fro
        public const float Moved = 6f;       // pixels a box must have moved on the screen before the two are measured again: less than this is not seen as an overlap
        public const float MovedSqr = Moved * Moved;

        /// <summary>True when the two rectangles, the first grown by <paramref name="margin"/> on every side, share any area.</summary>
        public static bool Overlap(float aLeft, float aBottom, float aRight, float aTop, float bLeft, float bBottom, float bRight, float bTop, float margin)
        {
            return bLeft < aRight + margin && bRight > aLeft - margin && bBottom < aTop + margin && bTop > aBottom - margin;
        }

        /// <summary>
        /// Where the panel goes. The box is the game's building box, the panel is the mod's as it would stand at home.
        /// </summary>
        /// <param name="boxUp">Whether the building box is on the screen at all.</param>
        /// <param name="now">Where the panel stands at this moment.</param>
        /// <param name="dx">Pixels to move the panel from its home, to the right.</param>
        /// <param name="dy">Pixels to move the panel from its home, upwards.</param>
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
            // Left or right, whichever is the shorter step; then above or below, whichever is the shorter.
            PanelSide side = Nearer(PanelSide.Left, PanelSide.Right, boxLeft, boxBottom, boxRight, boxTop, panelLeft, panelBottom, panelRight, panelTop, screenWidth, screenHeight, out dx, out dy);
            if (side == PanelSide.Wait)
            {
                side = Nearer(PanelSide.Above, PanelSide.Below, boxLeft, boxBottom, boxRight, boxTop, panelLeft, panelBottom, panelRight, panelTop, screenWidth, screenHeight, out dx, out dy);
            }
            return side;
        }

        // Of two sides the one with room and the shorter step; the first if they are equal; Wait if neither has room.
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
        /// The step that puts the panel on one side of the box, the gap away from it, and whether the whole panel is
        /// on the screen there. A panel whose home is already further out on that side than the gap is not drawn in
        /// towards the box: its step is nothing.
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
        /// Whether the two boxes are to be measured again: the building box came or went, or either has moved on the
        /// screen by more than <see cref="Moved"/> since they were last measured (which is what a camera that moved
        /// does to them). Squared distances, so nothing is rooted.
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
