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
        public string Name;        // "Brick wall"
        public string Figure;      // "$120"; null for none
        public bool Disabled;      // not to be had here: dimmed and inert
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
    /// with its name and its figure under it, the one in hand on the cyan. For the interior designer (w5-designer) and Build
    /// mode (w4-construction): their model provider leaves the bar's apps out while the row stands. Commands:
    /// <see cref="ToolCommand"/> (a tool's index) and <see cref="ChoiceCommand"/> (a choice's index). Game-free. Cost: a
    /// layout a change.
    /// </summary>
    public static class UiToolRow
    {
        public const int ToolCommand = 270, ChoiceCommand = 271;
        public const int MostAcross = 8;                    // choices a row of the panel: more wrap to a second
        private const float Gap = 8f;                       // px between two tool tiles, as between the app row's
        private const float Card = 104f;                    // px: a choice, its tile and two lines of its name
        private const float CardTile = 48f;                 // px: a choice's tile
        private const byte RowRegion = 242, PanelRegion = 243;

        /// <summary>Lays the row in the bar's upper row and, with a tool in hand, its panel above the bar.</summary>
        public static void Draw(UiScene s, UiAt at, UiToolRowModel m, UiFrame frame)
        {
            float tile = UiTokens.IconTile;
            int count = m.Tools.Length;
            if (count == 0)
            {
                return;
            }
            float total = count * tile + (count - 1) * Gap;
            float x0 = UiDraw.SnapDown((frame.Width - total) / 2f), ty = frame.BandY + (UiFrame.Toolbar - tile) / 2f;
            UiAt row = at.InRegion(RowRegion);
            int first = s.NodeCount;
            for (int i = 0; i < count; i++)
            {
                float tx = x0 + i * (tile + Gap);
                int face = UiDraw.Tile(s, row, tx, ty, tile, UiRole.None, i == m.Tool);
                s.Items[face].Flags |= UiFlags.Chrome;
                float g = UiTokens.S8;   // a tool's glyph, chunky as a game's, on the grid in its 40 px tile
                UiDraw.Icon(s, row.In(face), tx + (tile - g) / 2f, ty + (tile - g) / 2f, g, m.Tools[i].Icon, i == m.Tool ? UiRole.OnAccent : UiRole.None, UiFlags.Middle | UiFlags.Chrome, true);
                UiDraw.Node(s, row, tx, ty, tile, tile, ToolCommand, i, face);
            }
            for (int n = first; n < s.NodeCount; n++)
            {
                s.Nodes[n].Left = n > first ? n - 1 : -1;
                s.Nodes[n].Right = n < s.NodeCount - 1 ? n + 1 : -1;
            }
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
            h = UiFrame.Strip + UiTokens.S3 + rows * Card + (rows - 1) * UiTokens.S2 + UiTokens.S3;
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
            for (int i = 0; i < m.Choices.Length; i++)
            {
                float cx = x + UiTokens.S3 + (i % MostAcross) * (Card + UiTokens.S2);
                Choice(s, on, ref m.Choices[i], i, i == m.Choice, cx, cy + (i / MostAcross) * (Card + UiTokens.S2));
            }
        }

        // A choice: its glyph on its tile (the cyan when in hand), its name under it on two lines, its figure under that.
        private static void Choice(UiScene s, UiAt at, ref UiToolChoice c, int index, bool chosen, float x, float y)
        {
            int drawn = s.ItemCount;
            int face = UiDraw.Box(s, at, x, y, Card, Card, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float tx = x + (Card - CardTile) / 2f;
            int t = UiDraw.Tile(s, inside, tx, y + UiTokens.S2, CardTile, UiRole.None, chosen);
            UiDraw.Icon(s, inside.In(t), tx + (CardTile - UiTokens.S8) / 2f, y + UiTokens.S2 + (CardTile - UiTokens.S8) / 2f, UiTokens.S8, c.Icon, chosen ? UiRole.OnAccent : UiRole.None,
                UiFlags.Middle | UiFlags.Centre, true);
            float ny = y + UiTokens.S2 + CardTile + UiTokens.S1;
            float lh = UiTextRules.LineHeight(UiStyle.Small);
            bool figure = !string.IsNullOrEmpty(c.Figure);
            int lines = figure ? 1 : 2;
            UiDraw.Text(s, inside, x + UiTokens.S1, ny, Card - UiTokens.S2, UiStyle.Small, UiRole.Text, c.Name, UiFlags.Centre | UiFlags.Wrap | UiFlags.Truncate | UiFlags.Middle, lines);
            if (figure)
            {
                UiDraw.Text(s, inside, x + UiTokens.S1, ny + lh + UiTokens.S1, Card - UiTokens.S2, UiStyle.SmallStrong, UiRole.Text2, c.Figure, UiFlags.Centre | UiFlags.Middle);
            }
            int node = UiDraw.Node(s, at, x, y, Card, Card, ChoiceCommand, index, face, c.Disabled);
            if (c.Disabled)
            {
                UiDraw.Dim(s, drawn, node);
            }
        }
    }
}
