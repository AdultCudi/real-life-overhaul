using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The logo editor of a business's settings (the "Shape and colours" page of the kit): the game's shapes as a scrolling grid of
    /// tiles at the left, and at the right the logo as it stands now, a row of the game's swatches for each of its three colours
    /// (the shape's, the name's, the background) with the add button leading each row, and Save logo with Done at the foot. Every
    /// tile and swatch is a target the game's side answers by pressing the game's own button, so the game's customizer takes the pick
    /// as a click on its own list would. Game-free. Cost: a layout a change; the grid lays only the rows in view.
    /// </summary>
    public static partial class BizManPages
    {
        private const float ShapeTile = UiTokens.S12;           // px: a shape's tile at least
        private const float ShapeTileMost = UiTokens.S12 * 2f;    // px: a shape's tile at most, when fewer columns give the grid more height
        private const float PreviewTile = UiTokens.S16 * 2f;     // px: the logo as it stands now
        private const int MostColumns = 6, LeastColumns = 3, ActionTargets = 3;   // a screen holds at most 48 targets, so the grid shows a window of the shapes
        private const float Frame = UiTokens.S1;                          // px: the chosen shape's cyan all round its tile

        private static void LogoPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float half = Half(w), rx = x + half + UiTokens.S8;
            // The logo as it stands stays at the left whichever tab is open, so every pick shows at once; the tab holds the picks.
            float ly = Title(s, at, x, y, half, m.PartTitle);
            Picture(s, at, m.PreviewPic, 1f, UiIcon.Store, x, ly, PreviewTile, PreviewTile, UiTokens.S8, true);
            if (!string.IsNullOrEmpty(m.LogoNote))
            {
                float nx = x + PreviewTile + UiTokens.S4;
                UiDraw.Text(s, at, nx, ly, x + half - nx, UiStyle.Small, UiRole.Text2, m.LogoNote, UiFlags.Label | UiFlags.Wrap, 6);
            }
            float ry = y;
            if (m.LogoTabs.Length > 1)
            {
                UiDraw.Tabs(s, at, rx, ry, PeriodWidth(m.LogoTabs), m.LogoTabs, m.LogoTab, null, LogoTab);
                ry += UiTokens.Control + UiTokens.S4;
            }
            float foot = bottom - UiTokens.Control - UiTokens.S4;
            m.ShapesClip = m.ShapesList = -1;
            m.ShapesColumns = 0;
            if (m.LogoTab == 0)
            {
                float gy = ry;
                if (!string.IsNullOrEmpty(m.ShapesOwn))
                {
                    UiDraw.LinkRow(s, at, rx, foot - UiTokens.Control, half, UiTokens.Control, m.ShapesOwn, LogoOwn, 0);
                    foot -= UiTokens.Control + UiTokens.S2;
                }
                if (m.ShapeCount == 0)
                {
                    Lead(s, at, m.ShapesNone, rx, gy, half);
                }
                else
                {
                    // What the page's other targets leave of the layer's budget is the grid's: the firm's head (laid after the page), the tabs,
                    // the link, and Done and Save logo (laid last) are all counted before a tile is.
                    int room = UiCheckRules.MostTargets - PageTargets(s) - HeadTargets(s, at, m, x, y, w, keys) - ActionTargets;
                    m.ShapesOffset = Shapes(s, at, m, rx, gy, half, foot - gy, room);
                }
            }
            else
            {
                for (int i = 0; i < m.Colours.Length; i++)
                {
                    ref BizManColours c = ref m.Colours[i];
                    UiDraw.TextMiddle(s, at, rx, ry, UiTokens.PairRow, half, UiStyle.Small, UiRole.Text2, c.Label, UiFlags.Label | UiFlags.Truncate);
                    ry += UiTokens.PairRow;
                    float cx = rx, cw = half;
                    int first = s.NodeCount;
                    if (c.CanAdd)
                    {
                        UiDraw.IconButton(s, at, rx, ry, UiDraw.Swatch, UiIcon.Plus, UiRole.AccentText, LogoColourAdd, i);
                        cx += UiDraw.Swatch + UiTokens.S2;
                        cw -= UiDraw.Swatch + UiTokens.S2;
                    }
                    float sh = UiDraw.Swatches(s, at, cx, ry, cw, c.Pics, c.Count, c.Chosen, LogoColour + i);
                    if (c.CanAdd && s.NodeCount > first + 1)
                    {
                        // The add and the colours are one row for the d-pad: Right from the add reaches the first colour.
                        s.Nodes[first].Left = first;
                        s.Nodes[first].Right = first + 1;
                        s.Nodes[first + 1].Left = first;
                    }
                    ry += Math.Max(sh, UiDraw.Swatch) + UiTokens.S8;
                }
            }
            int buttons = s.NodeCount;
            Buttons(s, at, m.Actions, rx + half, bottom - UiTokens.Control, keys, Action);
            if (m.ShapesColumns > 0 && buttons < s.NodeCount)
            {
                // The grid's last column leads right to the buttons, as the fence (which looks for a target level with a tile) finds none
                // under a tall grid, and the pad would not reach them or the link above them.
                for (int i = 0; i < buttons; i++)
                {
                    if (s.Nodes[i].Command == LogoShape && s.Nodes[i].Arg % m.ShapesColumns == m.ShapesColumns - 1)
                    {
                        Kept(i, -1, buttons);
                    }
                }
            }
        }

        // The shapes as tiles row by row in a region that scrolls inside its frame; only the rows in view are laid out. Each column
        // is a list, so up and down walk it and left and right cross to the next, as the catalogue's grid does. Returns the offset it took.
        private static float Shapes(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float h, int room)
        {
            float lane = w - UiTokens.S4;   // the thumb's lane
            int columns = 0;
            float tile = ShapeTile, rowH = 0f, vh = 0f;
            // The budget is laid as one more row than is in view (a scroll rests between rows), so a count of columns holds
            // room / columns - 1 rows in view. Fewer, larger tiles fill a tall page; the count that shows the most height wins.
            for (int c = MostColumns; c >= LeastColumns; c--)
            {
                float t = Math.Max(ShapeTile, Math.Min(ShapeTileMost, UiDraw.SnapDown((lane - (c - 1) * UiTokens.S2) / c)));
                float rh = t + UiTokens.S2;
                int rowsIn = Math.Max(1, Math.Min((int)((h + UiTokens.S2) / rh), room / c - 1));
                float seen = rowsIn * rh - UiTokens.S2;
                if (seen > vh + 0.5f)
                {
                    columns = c;
                    tile = t;
                    rowH = rh;
                    vh = seen;
                }
            }
            if (columns == 0)
            {
                columns = LeastColumns;
                tile = ShapeTile;
                rowH = tile + UiTokens.S2;
                vh = rowH - UiTokens.S2;
            }
            int rows = (m.ShapeCount + columns - 1) / columns;
            float content = rows * rowH - UiTokens.S2;
            vh = UiDraw.SnapDown(Math.Min(vh, Math.Max(h, tile)));
            float offset = m.ShapesOffset;
            if (m.ShapesReveal >= 0 && m.ShapesReveal < rows)
            {
                offset = Math.Min(offset, m.ShapesReveal * rowH);
                offset = Math.Max(offset, m.ShapesReveal * rowH + tile - vh);
            }
            m.ShapesReveal = -1;
            offset = Math.Max(0f, Math.Min(offset, content - vh));
            short clip = s.AddClip(x, y, lane, vh, offset);
            s.Clips[clip].ContentHeight = content;
            int first = Math.Max(0, (int)(offset / rowH)), last = Math.Min(rows - 1, (int)((offset + vh - 0.5f) / rowH));
            short firstList = -1;
            for (int c = 0; c < columns; c++)
            {
                int inColumn = (m.ShapeCount - c + columns - 1) / columns;
                int shownFirst = Math.Min(first, inColumn), shownLast = Math.Min(last, inColumn - 1);
                short list = s.AddList(inColumn, shownFirst, Math.Max(0, shownLast - shownFirst + 1), clip, m.ShapeChosen >= 0 && m.ShapeChosen % columns == c ? m.ShapeChosen / columns : -1);
                firstList = c == 0 ? list : firstList;
            }
            m.ShapesClip = clip;
            m.ShapesList = firstList;
            m.ShapesColumns = columns;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            for (int r = first; r <= last; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int i = r * columns + c;
                    if (i >= m.ShapeCount)
                    {
                        break;
                    }
                    float cx = c * (tile + UiTokens.S2), cy = r * rowH;
                    bool on = i == m.ShapeChosen;
                    int face;
                    if (on)
                    {
                        // The cyan all round the tile, never a mark down one side.
                        face = UiDraw.Box(s, inClip, cx, cy, tile, tile, UiRole.Accent, UiRole.None, UiTokens.RadiusLarge, UiFlags.Selected);
                        UiDraw.PictureFace(s, inClip.In(face), cx + Frame, cy + Frame, tile - Frame * 2f, m.ShapePics[i]);
                    }
                    else
                    {
                        face = UiDraw.PictureFace(s, inClip, cx, cy, tile, m.ShapePics[i]);
                    }
                    UiDraw.Node(s, inClip, cx, cy, tile, tile, LogoShape, i, face, false, (short)(firstList + c), r);
                }
            }
            UiDraw.ScrollThumb(s, at, x + w - UiTokens.S1, y, vh, offset, content);
            return offset;
        }
    }
}
