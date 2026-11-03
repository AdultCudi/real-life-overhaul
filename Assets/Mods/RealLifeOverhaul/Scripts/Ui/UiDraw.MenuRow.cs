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
        public bool Primary;      // the menu's one main action (saving): the cyan fill with dark ink, as a game marks its first choice
    }

    public static partial class UiDraw
    {
        /// <summary>px: a menu's row, its glyph's tile with room round it.</summary>
        public const float MenuRowHeight = UiTokens.Row;

        /// <summary>px: a menu's row on a screen of its own (the pause menu): a larger tile, glyph and words, as a game's pause screen.</summary>
        public const float MenuRowLarge = UiTokens.S16;

        /// <summary>
        /// A menu's row, as Cities: Skylines II's pause menu lists its actions: the glyph on its slate tile in the glyph's own
        /// colour, the words in the soft white (an action that loses something in the danger's ink, its glyph with them; the
        /// main one on the cyan with dark ink), and the key that presses it from anywhere on its cap at the right, as a
        /// button shows its key. At <see cref="MenuRowLarge"/> the tile, glyph and words step up a size. Focus and hover are
        /// the host's still fill on the row, edge to edge across its panel. Returns the focus target.
        /// </summary>
        public static int MenuRow(UiScene s, UiAt at, float x, float y, float w, ref UiMenuRowModel row, int command, int arg, IUiKeys keys, float h = MenuRowHeight)
        {
            bool large = h >= MenuRowLarge;
            float tile = large ? UiTokens.IconTile : UiTokens.ControlSmall;
            float g = large ? Snap(UiTokens.IconTile * 0.6f) : UiTokens.Icon;
            UiStyle style = large ? UiStyle.Number : UiStyle.BodyStrong;
            UiFlags state = (row.Disabled ? UiFlags.Disabled : UiFlags.None) | (row.Primary ? UiFlags.Selected : UiFlags.None);
            int face = Box(s, at, x, y, w, h, row.Primary ? UiRole.Accent : UiRole.None, UiRole.None, 0f, state);
            UiAt inside = at.In(face);
            float pad = large ? UiTokens.S5 : UiTokens.S4;
            float tx = x + pad, ty = y + (h - tile) / 2f;
            int t = Tile(s, inside, tx, ty, tile, UiRole.None, row.Primary);
            UiRole tint = row.Primary ? UiRole.OnAccent : row.Danger ? UiRole.DangerText : row.Tint != UiRole.None ? row.Tint : UiIconTints.Of(row.Icon);
            Icon(s, inside.In(t), tx + (tile - g) / 2f, ty + (tile - g) / 2f, g, row.Icon, tint, UiFlags.Middle | UiFlags.Centre, true);
            bool cap = row.Key != UiAction.Count && !s.Held && HasKey(row.Key, keys);   // shown as a button shows its key, the mouse leading or not
            float capW = cap ? KeyCapWidth(row.Key, keys) : 0f;
            float lx = tx + tile + (large ? UiTokens.S4 : UiTokens.S3);
            float lw = x + w - pad - (cap ? capW + UiTokens.S3 : 0f) - lx;
            UiRole ink = row.Primary ? UiRole.OnAccent : row.Danger ? UiRole.DangerText : UiRole.Text;
            TextMiddle(s, inside, lx, y, h, lw, style, ink, row.Label, UiFlags.Label | UiFlags.Truncate | (row.Disabled ? UiFlags.Disabled : UiFlags.None));
            if (cap)
            {
                KeyCap(s, inside, x + w - pad - capW, y + (h - UiTokens.KeyCap) / 2f, row.Key, keys, row.Primary ? UiRole.OnAccent : UiRole.Track,
                    row.Primary ? UiRole.Accent : UiRole.Text);
            }
            return Node(s, at, x, y, w, h, command, arg, face, row.Disabled, -1, 0, row.Key);
        }
    }
}
