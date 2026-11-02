using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A tab down a catalogue panel's edge: the game's own picture for its category, or a glyph of the kit's.</summary>
    public struct UiEdgeTab
    {
        public string Picture;   // the game's sprite for the category (UiPictures), or null for the glyph
        public UiIcon Icon;
        public string Label;     // the category in words, for the strip's title when it is the one shown; null when the game names none
    }

    /// <summary>
    /// The catalogue's panel form with its categories as round tabs down its left edge, as the info panel joins its things:
    /// a narrow strip of tabs beside the panel, the shown one filled cyan, each a target of its own. The panel beside it is
    /// the screen's (a grid of goods, a colour row, its actions). Game-free. Cost: a box and a target a tab, on a layout.
    /// </summary>
    public static class UiCataloguePanel
    {
        public const float TabStrip = 56f;   // px: the tabs' column, a round tab with two grid steps either side for its ring
        public const float TabSize = 40f;    // px: a round tab
        private const float Glyph = 24f;     // px: a tab's picture or glyph

        /// <summary>The width the strip and its gap take before the panel.</summary>
        public static float Beside(int tabs) => tabs > 0 ? TabStrip + UiTokens.S2 : 0f;

        /// <summary>The strip's height for its tabs.</summary>
        public static float Height(int tabs) => tabs <= 0 ? 0f : tabs * TabSize + (tabs - 1) * UiTokens.S2 + UiTokens.S2 * 2f;

        /// <summary>
        /// The tabs down the edge from a top left, the shown one filled cyan; each a target of <paramref name="command"/>
        /// with its index. Returns the strip's height.
        /// </summary>
        public static float Tabs(UiScene s, float x, float y, UiEdgeTab[] tabs, int count, int shown, int command)
        {
            count = Math.Min(count, tabs?.Length ?? 0);
            if (count <= 0)
            {
                return 0f;
            }
            float h = Height(count);
            int strip = UiDraw.Box(s, UiAt.Screen, x, y, TabStrip, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(strip);
            int first = s.NodeCount;
            for (int i = 0; i < count; i++)
            {
                float tx = x + (TabStrip - TabSize) / 2f, ty = y + UiTokens.S2 + i * (TabSize + UiTokens.S2);
                bool on = i == shown;
                if (string.IsNullOrEmpty(tabs[i].Picture))
                {
                    int node = UiDraw.IconButton(s, at, tx, ty, TabSize, tabs[i].Icon, UiRole.None, command, i, on, UiAction.Count, true);
                    if (on)
                    {
                        s.FocusFirst = node;
                    }
                    continue;
                }
                int face = UiDraw.Circle(s, at, tx, ty, TabSize, TabSize, on ? UiRole.Accent : UiRole.Tile, (on ? UiFlags.Selected : UiFlags.None) | UiFlags.Middle);
                float g = Glyph, gx = tx + (TabSize - g) / 2f, gy = ty + (TabSize - g) / 2f;
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Text = tabs[i].Picture, Value = 1f, X = gx, Y = gy, W = g, H = g, Flags = UiFlags.Share | UiFlags.Middle,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = face, Alpha = 1f
                };
                s.Add(ref item);
                int target = UiDraw.Node(s, at, tx, ty, TabSize, TabSize, command, i, face);
                if (on)
                {
                    s.FocusFirst = target;
                }
            }
            if (s.NodeCount > first)
            {
                s.Nodes[first].Up = first;   // the strip's ends go nowhere up or down: it is a column of its own
                s.Nodes[s.NodeCount - 1].Down = s.NodeCount - 1;
            }
            return h;
        }
    }
}
