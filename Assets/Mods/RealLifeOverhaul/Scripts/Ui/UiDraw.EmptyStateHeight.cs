using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The height the kit's empty state (UiDraw.EmptyState) takes, for a part that sizes its room to it.</summary>
    public static partial class UiDraw
    {
        /// <summary>
        /// The block UiDraw.EmptyState lays in a room of this width: the glyph's tile, the title on up to two lines, the
        /// sentence on up to three and the action's button when there is one, by the same sums, snapped to the grid. A part
        /// that gives the state just this room (and a step round it) leaves no hole above or below it.
        /// </summary>
        public static float EmptyStateHeight(float w, string title, string body, string action)
        {
            float bw = SnapDown(Math.Min(w - UiTokens.S8 * 2f, UiTokens.S16 * 8f));
            float th = string.IsNullOrEmpty(title) ? 0f : UiTextRules.WrappedHeight(title, UiStyle.Title, bw, 2);
            float lh = string.IsNullOrEmpty(body) ? 0f : UiTextRules.WrappedHeight(body, UiStyle.Body, bw, 3);
            float ah = string.IsNullOrEmpty(action) ? 0f : UiTokens.S5 + UiTokens.Control;
            return Snap(UiTokens.S12 + UiTokens.S4 + th + (lh > 0f ? UiTokens.S2 + lh : 0f) + ah);
        }
    }
}
