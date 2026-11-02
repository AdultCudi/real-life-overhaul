using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A tool of the row: its glyph in its own colour and its name, which the rising panel's strip shows.</summary>
    public struct UiTool
    {
        public UiIcon Icon;
        public string Name;        // "Walls"
    }

    /// <summary>A choice of the chosen tool: its glyph on a tile, its name and its figure (a price, a size).</summary>
    public struct UiToolChoice
    {
        public UiIcon Icon;
        public string Picture;     // the game's own picture of it (a floor's swatch, an item's image) by its key (UiPictures), or null for the glyph
        public float Aspect;       // the picture's width over its height
        public string Name;        // "Brick wall"
        public string Figure;      // "$120"; null for none
        public bool Disabled;      // not to be had here: dimmed and inert
        public string Reason;      // why, in place of its figure ("Not sold here")
    }

    /// <summary>The tool row: its tools, the one in hand, and that tool's choices in the panel that rises above it.</summary>
    public sealed class UiToolRowModel
    {
        public UiTool[] Tools = new UiTool[0];
        public int Tool = -1;                              // the tool in hand, or -1 for none (no panel rises)
        public UiToolChoice[] Choices = new UiToolChoice[0];
        public int Choice = -1;                            // the choice in hand
    }

    /// <summary>
    /// The tool row, as Cities: Skylines II's build toolbar: the status bar's app row given over to a row of tool tiles (the
    /// one in hand filled cyan), and above the bar a panel rising from that tool for its choices, each a glyph on its tile
    /// with its name and its figure under it, the one in hand on the cyan. For the interior designer and Build mode: their
    /// model provider leaves the bar's apps out while the row stands and keeps its width clear (UiStatusModel.Middle).
    /// Commands: <see cref="ToolCommand"/> (a tool's index) and <see cref="ChoiceCommand"/> (a choice's index). Game-free.
    /// Cost: a layout a change.
    /// </summary>
    public static class UiToolRow
    {
        public const int ToolCommand = 270, ChoiceCommand = 271;
        public const int MostAcross = 8;                    // choices a row of the panel: more wrap to a second
        private const float Gap = 8f;                       // px between two tool tiles, as between the app row's
        private const float Card = 104f;                    // px: a choice's width, a tile with two short words beside it
        private const float CardH = 120f;                   // px: its height, the tile, two lines of its name and its figure
        private const float CardTile = 48f;                 // px: a choice's tile
        private const byte RowRegion = 242, PanelRegion = 243;

        /// <summary>The row's width on the bar, which the bar's pills keep clear of (UiStatusModel.Middle).</summary>
        public static float Width(UiToolRowModel m)
        {
            int count = m != null ? m.Tools.Length : 0;
            return count == 0 ? 0f : count * UiDraw.BarTile + (count - 1) * Gap;
        }

        /// <summary>Lays the row on the bar's centre line in the apps' place and, with a tool in hand, its panel above the bar: after the bar, which it stands on.</summary>
        public static void Draw(UiScene s, UiAt at, UiToolRowModel m, UiFrame frame)
        {
            float tile = UiDraw.BarTile;
            int count = m.Tools.Length;
            if (count == 0)
            {
                return;
            }
            float total = Width(m);
            float x0 = UiDraw.SnapDown((frame.Width - total) / 2f), ty = UiDraw.BarMiddle(frame) - tile / 2f;
            UiAt row = at.InRegion(RowRegion);
            int first = s.NodeCount;
            for (int i = 0; i < count; i++)
            {
                float tx = x0 + i * (tile + Gap);
                int face = UiDraw.Tile(s, row, tx, ty, tile, UiRole.None, i == m.Tool);
                s.Items[face].Flags |= UiFlags.Chrome | UiFlags.Middle;   // centred on the bar's line, as the app tiles
                float g = UiDraw.BarGlyph;
                UiDraw.Icon(s, row.In(face), tx + (tile - g) / 2f, ty + (tile - g) / 2f, g, m.Tools[i].Icon, i == m.Tool ? UiRole.OnAccent : UiRole.None, UiFlags.Middle | UiFlags.Chrome, true);
                int node = UiDraw.Node(s, row, tx, ty, tile, tile, ToolCommand, i, face);
                if (i == m.Tool || (m.Tool < 0 && i == 0))
                {
                    s.FocusFirst = node;   // the keys start on the tool in hand, where the player works
                }
            }
            for (int n = first; n < s.NodeCount; n++)
            {
                s.Nodes[n].Left = n > first ? n - 1 : -1;
                s.Nodes[n].Right = n < s.NodeCount - 1 ? n + 1 : -1;
            }
            UiDraw.JoinPills(s, first, s.NodeCount - 1);   // the bar's pills either side, one row for the d-pad
            if (m.Tool >= 0 && m.Tool < count && m.Choices.Length > 0)
            {
                Panel(s, at.InRegion(PanelRegion), m, frame, x0 + m.Tool * (tile + Gap) + tile / 2f);
            }
        }

        /// <summary>The panel's size for its choices.</summary>
        public static void PanelSize(UiToolRowModel m, out float w, out float h)
        {
            int across = Math.Min(MostAcross, Math.Max(1, m.Choices.Length));
            int rows = (m.Choices.Length + MostAcross - 1) / MostAcross;
            w = UiTokens.S3 * 2f + across * Card + (across - 1) * UiTokens.S2;
            h = UiFrame.Strip + UiTokens.S3 + rows * CardH + (rows - 1) * UiTokens.S2 + UiTokens.S3;
        }

        // The panel rising from the tool in hand: centred over it as far as the screen lets it, its choices in rows.
        private static void Panel(UiScene s, UiAt at, UiToolRowModel m, UiFrame frame, float over)
        {
            PanelSize(m, out float w, out float h);
            float x = UiDraw.SnapDown(Math.Max(UiTokens.S4, Math.Min(over - w / 2f, frame.Width - UiTokens.S4 - w)));
            float y = frame.BandY - UiTokens.S3 - h;
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            UiTool tool = m.Tools[m.Tool];
            float cy = UiDraw.TitleStrip(s, on, x, y, w, tool.Icon, tool.Name, false) + UiTokens.S3;
            bool tiles = Differ(m);
            int first = s.NodeCount;
            for (int i = 0; i < m.Choices.Length; i++)
            {
                float cx = x + UiTokens.S3 + (i % MostAcross) * (Card + UiTokens.S2);
                Choice(s, on, ref m.Choices[i], i, i == m.Choice, tiles, cx, cy + (i / MostAcross) * (CardH + UiTokens.S2));
            }
            Rows(s, first, s.NodeCount);
        }

        // Each row of choices as the d-pad's row: Left and Right to the next that can be had, a row's end going nowhere sideways.
        private static void Rows(UiScene s, int from, int to)
        {
            for (int n = from; n < to; n++)
            {
                int row = (n - from) / MostAcross;
                s.Nodes[n].Right = Along(s, from, to, n, 1, row);
                s.Nodes[n].Left = Along(s, from, to, n, -1, row);
            }
        }

        private static int Along(UiScene s, int from, int to, int n, int step, int row)
        {
            for (int m = n + step; m >= from && m < to && (m - from) / MostAcross == row; m += step)
            {
                if (!s.Nodes[m].Disabled)
                {
                    return m;
                }
            }
            return n;
        }

        // A tile says something only where the choices differ: by their pictures, or by their glyphs.
        private static bool Differ(UiToolRowModel m)
        {
            for (int i = 0; i < m.Choices.Length; i++)
            {
                if (!string.IsNullOrEmpty(m.Choices[i].Picture) || m.Choices[i].Icon != m.Choices[0].Icon)
                {
                    return true;
                }
            }
            return false;
        }

        // A choice: its picture or glyph on its tile (the cyan when in hand), or the cyan fill itself where no tile says
        // anything, its name under it on two lines, its figure under that, or why it is greyed.
        private static void Choice(UiScene s, UiAt at, ref UiToolChoice c, int index, bool chosen, bool tiles, float x, float y)
        {
            int drawn = s.ItemCount;
            int face = UiDraw.Box(s, at, x, y, Card, CardH, tiles || !chosen ? UiRole.None : UiRole.RowSel, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float ny = y + UiTokens.S3;
            if (tiles)
            {
                float tx = x + (Card - CardTile) / 2f, ty = y + UiTokens.S2;
                int t = UiDraw.Tile(s, inside, tx, ty, CardTile, UiRole.None, chosen);
                if (!string.IsNullOrEmpty(c.Picture))
                {
                    Picture(s, inside.In(t), c.Picture, c.Aspect, tx + UiTokens.S1, ty + UiTokens.S1, CardTile - UiTokens.S2);
                }
                else
                {
                    UiDraw.Icon(s, inside.In(t), tx + (CardTile - UiTokens.S8) / 2f, ty + (CardTile - UiTokens.S8) / 2f, UiTokens.S8, c.Icon, chosen ? UiRole.OnAccent : UiRole.None,
                        UiFlags.Middle | UiFlags.Centre, true);
                }
                ny = ty + CardTile + UiTokens.S1;
            }
            float lh = UiTextRules.LineHeight(UiStyle.Small);
            UiDraw.Text(s, inside, x + UiTokens.S1, ny, Card - UiTokens.S2, UiStyle.Small, UiRole.Text, c.Name, UiFlags.Centre | UiFlags.Wrap | UiFlags.Truncate | UiFlags.Middle, 2);
            string under = c.Disabled && !string.IsNullOrEmpty(c.Reason) ? c.Reason : c.Figure;
            if (!string.IsNullOrEmpty(under))
            {
                UiDraw.Text(s, inside, x + UiTokens.S1, ny + lh * 2f + UiTokens.S1, Card - UiTokens.S2, UiStyle.SmallStrong, c.Disabled ? UiRole.WarnText : UiRole.Text2, under,
                    UiFlags.Centre | UiFlags.Middle | UiFlags.Truncate);
            }
            int node = UiDraw.Node(s, at, x, y, Card, CardH, ChoiceCommand, index, face, c.Disabled);
            if (c.Disabled)
            {
                UiDraw.Dim(s, drawn, node);
                s.Items[s.ItemCount - 1].Alpha = 1f;   // the reason reads whole: it says why the rest is dim
            }
        }

        // The game's own picture of a choice, letterboxed in its tile at its own shape, as the catalogue shows goods.
        private static void Picture(UiScene s, UiAt at, string key, float aspect, float x, float y, float size)
        {
            float a = aspect > 0f ? aspect : 1f;
            float pw = a >= 1f ? size : size * a, ph = a >= 1f ? size / a : size;
            var item = new UiItem
            {
                Kind = UiKind.Picture, Text = key, Value = a, X = x + (size - pw) / 2f, Y = y + (size - ph) / 2f, W = pw, H = ph,
                Flags = UiFlags.Share | UiFlags.Middle, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            s.Add(ref item);
        }
    }
}
