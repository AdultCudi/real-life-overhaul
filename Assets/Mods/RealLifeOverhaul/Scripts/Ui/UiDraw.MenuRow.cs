namespace RealLifeOverhaul.Ui
{
    /// <summary>A row of a menu: what it does in a word or two, its glyph, the key that presses it from anywhere, and its state.</summary>
    public struct UiMenuRowModel
    {
        public UiIcon Icon;
        public UiRole Tint;       // the glyph's colour, or None for the icon's own
        public string Label;
        public UiAction Key;      // pressed from anywhere on the screen (its cap at the row's right), or Count for none
        public bool Danger;       // it ends something the player may lose (quitting without a save): its words in the danger's ink
        public bool Disabled;     // the game holds it now: drawn, not pressed
        public bool Split;        // a hairline above it: the first of another group (leaving the game after the game's own)
    }

    public static partial class UiDraw
    {
        /// <summary>px: a menu's row, its glyph's tile with room round it.</summary>
        public const float MenuRowHeight = UiTokens.Row;

        /// <summary>
        /// A menu's row, as Cities: Skylines II's pause menu lists its actions: the glyph on its slate tile in the glyph's own
        /// colour, the words in the soft white (an action that loses something in the danger's ink, its glyph with them), and
        /// the key that presses it from anywhere on its cap at the right while the keys lead. Focus and hover are the host's
        /// still fill on the row, edge to edge across its panel. Returns the focus target.
        /// </summary>
        public static int MenuRow(UiScene s, UiAt at, float x, float y, float w, ref UiMenuRowModel row, int command, int arg, IUiKeys keys)
        {
            float h = MenuRowHeight, tile = UiTokens.ControlSmall;
            int face = Box(s, at, x, y, w, h, UiRole.None, UiRole.None, 0f, row.Disabled ? UiFlags.Disabled : UiFlags.None);
            UiAt inside = at.In(face);
            float tx = x + UiTokens.S4, ty = y + (h - tile) / 2f;
            int t = Tile(s, inside, tx, ty, tile, UiRole.None, false);
            float g = UiTokens.Icon;
            UiRole tint = row.Danger ? UiRole.DangerText : row.Tint != UiRole.None ? row.Tint : UiIconTints.Of(row.Icon);
            Icon(s, inside.In(t), tx + (tile - g) / 2f, ty + (tile - g) / 2f, g, row.Icon, tint, UiFlags.Middle | UiFlags.Centre, true);
            bool cap = row.Key != UiAction.Count && !s.Pointer && !s.Held && HasKey(row.Key, keys);
            float capW = cap ? KeyCapWidth(row.Key, keys) : 0f;
            float lx = tx + tile + UiTokens.S3;
            float lw = x + w - UiTokens.S4 - (cap ? capW + UiTokens.S3 : 0f) - lx;
            TextMiddle(s, inside, lx, y, h, lw, UiStyle.BodyStrong, row.Danger ? UiRole.DangerText : UiRole.Text, row.Label,
                UiFlags.Label | UiFlags.Truncate | (row.Disabled ? UiFlags.Disabled : UiFlags.None));
            if (cap)
            {
                KeyCap(s, inside, x + w - UiTokens.S4 - capW, y + (h - UiTokens.KeyCap) / 2f, row.Key, keys, UiRole.Track, UiRole.Text);
            }
            return Node(s, at, x, y, w, h, command, arg, face, row.Disabled, -1, 0, row.Key);
        }
    }
}
