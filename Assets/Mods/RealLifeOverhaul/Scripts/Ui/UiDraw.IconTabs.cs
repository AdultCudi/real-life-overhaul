namespace RealLifeOverhaul.Ui
{
    public static partial class UiDraw
    {
        /// <summary>
        /// Tabs inside a page (a district's market, its real estate, its events): each its glyph on a slate tile in the
        /// glyph's colour and its name, the shown one filled cyan with dark ink and its tile cyan, as the window's own tabs
        /// across are drawn, a row of one control's height. Each tab is a target (its command with its index). Returns the
        /// height.
        /// </summary>
        public static float IconTabs(UiScene s, UiAt at, float x, float y, UiIcon[] icons, string[] labels, int shown, int command)
        {
            float h = UiTokens.Control, tile = UiTokens.ControlSmall - UiTokens.S2, g = UiTokens.IconSmall;
            float tx = x;
            for (int i = 0; i < labels.Length; i++)
            {
                bool on = i == shown;
                UiIcon icon = icons != null && i < icons.Length ? icons[i] : UiIcon.None;
                float lw = Snap(UiTextRules.Width(labels[i], UiStyle.Nav) + UiTokens.S1);
                float tw = UiTokens.S2 + (icon != UiIcon.None ? tile + UiTokens.S2 : 0f) + lw + UiTokens.S4;
                int face = Box(s, at, tx, y, tw, h, on ? UiRole.Accent : UiRole.None, UiRole.None, UiTokens.RadiusLarge, on ? UiFlags.Selected : UiFlags.None);
                UiAt inside = at.In(face);
                float lx = tx + UiTokens.S2;
                if (icon != UiIcon.None)
                {
                    UiRole tint = UiIconTints.Of(icon);
                    int t = Tile(s, inside, lx, y + (h - tile) / 2f, tile, tint, on);
                    Icon(s, inside.In(t), lx + (tile - g) / 2f, y + (h - g) / 2f, g, icon, on ? UiRole.OnAccent : tint, UiFlags.Middle | UiFlags.Centre, true);
                    lx += tile + UiTokens.S2;
                }
                TextMiddle(s, inside, lx, y, h, lw, UiStyle.Nav, on ? UiRole.OnAccent : UiRole.Text, labels[i], UiFlags.Label);
                Node(s, at, tx, y, tw, h, command, i, face);
                tx += tw + UiTokens.S1;
            }
            return h;
        }
    }
}
