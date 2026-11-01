using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The kit's empty state with a picture and a way out: a part that holds nothing says what would fill it and offers the fix.</summary>
    public static partial class UiDraw
    {
        /// <summary>
        /// An empty state for a part's room (a table that finds no one, a statement with no money moved, empty shelves): the
        /// glyph of what would be there on its slate tile in its own colour, the title and the sentence under it, both
        /// wrapped to a short measure, and the action that fills it (Clear search) as a button when there is one. Centred
        /// in the room it is given. Returns the button's target, or -1.
        /// </summary>
        public static int EmptyState(UiScene s, UiAt at, float x, float y, float w, float h, UiIcon icon, string title, string body, string action, int command, IUiKeys keys)
        {
            at = at.In(Box(s, at, x, y, w, h, UiRole.None, UiRole.None, 0f));   // the room is the state's own
            float bw = SnapDown(Math.Min(w - UiTokens.S8 * 2f, UiTokens.S16 * 8f));
            float tile = UiTokens.S12;
            float th = string.IsNullOrEmpty(title) ? 0f : UiTextRules.WrappedHeight(title, UiStyle.Title, bw, 2);
            float lh = string.IsNullOrEmpty(body) ? 0f : UiTextRules.WrappedHeight(body, UiStyle.Body, bw, 3);
            float ah = string.IsNullOrEmpty(action) ? 0f : UiTokens.S5 + UiTokens.Control;
            float block = tile + UiTokens.S4 + th + (lh > 0f ? UiTokens.S2 + lh : 0f) + ah;
            float top = Snap(y + Math.Max(0f, (h - block) / 2f));
            float cx = x + SnapDown((w - tile) / 2f);
            int t = Tile(s, at, cx, top, tile, UiRole.None, false);
            float g = UiTokens.S8 - UiTokens.S1;
            Icon(s, at.In(t), cx + (tile - g) / 2f, top + (tile - g) / 2f, g, icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float ty = top + tile + UiTokens.S4, bx = x + (w - bw) / 2f;
            if (th > 0f)
            {
                Text(s, at, bx, ty, bw, UiStyle.Title, UiRole.Text, title, UiFlags.Centre | UiFlags.Middle | UiFlags.Wrap | UiFlags.Label, 2);
                ty += th + UiTokens.S2;
            }
            if (lh > 0f)
            {
                Text(s, at, bx, ty, bw, UiStyle.Body, UiRole.Text2, body, UiFlags.Centre | UiFlags.Middle | UiFlags.Wrap | UiFlags.Label, 3);
                ty += lh;
            }
            if (ah <= 0f)
            {
                return -1;
            }
            float btn = Math.Min(w - UiTokens.S4, ButtonWidth(UiButtonKind.Secondary, action, UiIcon.None, UiAction.Count, keys));
            float by = Snap(ty + UiTokens.S5);
            return Button(s, at, x + SnapDown((w - btn) / 2f), by, btn, UiTokens.Control, UiButtonKind.Secondary, action, UiIcon.None, UiAction.Count, keys, command, 0);
        }
    }
}
