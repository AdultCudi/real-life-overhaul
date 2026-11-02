using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The HUD's sums, game-free and checked offline (EconomyCheck, UiChecks.Hud.cs): the px scale the HUD lays out at under
    /// the game's UI zoom, where a box over a point of the world stands (beside its point, inside the screen above the
    /// status bar, clear of a docked panel, at an edge when the point is behind the camera), and how the money's change
    /// rises and fades. All in px of the layout, y downwards. Cost: a few sums; nothing allocated.
    /// </summary>
    public static class UiHudRules
    {
        public const float LeastWidth = 1280f;    // px: the narrowest the HUD lays out; a UI zoom past it holds there, so the bar still fits
        public const float Margin = UiTokens.S4;  // px a box over the world keeps from the screen's edges and the status bar
        public const float Lift = UiTokens.S4;    // px a box stands above its point, as the game's own box stands up from it
        public const float ChangeHold = 0.6f;     // seconds the money's change rests before it rises
        public const float ChangeTime = 4f;       // seconds from its arrival until it is gone, as the game's floating money lasts
        public const float ChangeRise = UiTokens.S12;   // px it rises in that time

        /// <summary>
        /// Canvas units in a px of the HUD's layout on a canvas of this width (the game's 3840, divided by its UI zoom): the
        /// kit's 2 while that leaves the layout at least <see cref="LeastWidth"/> wide, so a larger zoom draws the HUD larger,
        /// as the game's own HUD grows, until it would no longer fit.
        /// </summary>
        public static float Scale(float canvasWidth)
        {
            if (canvasWidth <= 0f)
            {
                return UiTokens.CanvasPerPx;
            }
            return Math.Min(UiTokens.CanvasPerPx, canvasWidth / LeastWidth);
        }

        /// <summary>
        /// Where a box of a size stands over a point of the world, given where the point lands on the screen (px of the layout)
        /// and whether it is in front of the camera: centred over its point and lifted from it, kept <see cref="Margin"/>
        /// inside the screen and above the status bar (<paramref name="floor"/>, the bar's top), and moved off a docked
        /// panel's box (<paramref name="avoidW"/> 0 for none) to its nearer side with room. A point behind the camera puts
        /// the box at the screen's foot on the side the point lies, as a game's marker waits at the edge. Always inside.
        /// </summary>
        public static void Beside(float pointX, float pointY, bool ahead, float w, float h, float screenW, float floor,
            float avoidX, float avoidY, float avoidW, float avoidH, out float x, out float y)
        {
            if (!ahead)
            {
                // Behind the lens the projection mirrors the point: the box waits at the foot on the side the point really is.
                pointX = screenW - pointX;
                pointY = floor;
            }
            x = pointX - w / 2f;
            y = pointY - Lift - h;
            Clamp(ref x, ref y, w, h, screenW, floor);
            if (avoidW > 0f && Overlaps(x, y, w, h, avoidX, avoidY, avoidW, avoidH))
            {
                float right = avoidX + avoidW + Margin, left = avoidX - Margin - w, below = avoidY + avoidH + Margin;
                bool rightFits = right + w <= screenW - Margin, leftFits = left >= Margin, belowFits = below + h <= floor - Margin;
                float dr = Math.Abs(right - x), dl = Math.Abs(left - x);
                if (rightFits && (!leftFits || dr <= dl))
                {
                    x = right;
                }
                else if (leftFits)
                {
                    x = left;
                }
                else if (belowFits)
                {
                    y = below;
                }
                Clamp(ref x, ref y, w, h, screenW, floor);
            }
        }

        /// <summary>Keeps a box inside the screen, <see cref="Margin"/> from its edges and above the floor; a box too big for the room keeps its top left in.</summary>
        public static void Clamp(ref float x, ref float y, float w, float h, float screenW, float floor)
        {
            x = Math.Max(Margin, Math.Min(x, screenW - Margin - w));
            y = Math.Max(Margin, Math.Min(y, floor - Margin - h));
        }

        /// <summary>The two boxes share any area.</summary>
        public static bool Overlaps(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh) =>
            ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;

        /// <summary>True when the box lies whole inside the screen above the floor, with its margin.</summary>
        public static bool Inside(float x, float y, float w, float h, float screenW, float floor) =>
            x >= Margin - 0.01f && y >= Margin - 0.01f && x + w <= screenW - Margin + 0.01f && y + h <= floor - Margin + 0.01f;

        /// <summary>A screen point (pixels from the bottom left, as the camera gives it) as px of the layout (from the top left).</summary>
        public static void ToLayout(float screenX, float screenY, float screenPixelsW, float screenPixelsH, float layoutW, float layoutH, out float x, out float y)
        {
            x = screenPixelsW > 0f ? screenX / screenPixelsW * layoutW : 0f;
            y = screenPixelsH > 0f ? (1f - screenY / screenPixelsH) * layoutH : 0f;
        }

        /// <summary>px the money's change has risen this long after it arrived: still while it rests, then easing up.</summary>
        public static float ChangeLift(float age)
        {
            if (age <= ChangeHold)
            {
                return 0f;
            }
            float t = Math.Min(1f, (age - ChangeHold) / (ChangeTime - ChangeHold));
            return ChangeRise * (1f - (1f - t) * (1f - t));
        }

        /// <summary>How much of the money's change shows this long after it arrived: whole while it rests, then fading out.</summary>
        public static float ChangeAlpha(float age)
        {
            if (age <= ChangeHold)
            {
                return 1f;
            }
            return Math.Max(0f, 1f - (age - ChangeHold) / (ChangeTime - ChangeHold));
        }

        /// <summary>True while the money's change still shows.</summary>
        public static bool ChangeShows(float age) => age >= 0f && age < ChangeTime;
    }
}
