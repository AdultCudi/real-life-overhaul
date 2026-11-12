using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiDesignerPanels
    {
        // ------------------------------------------------------------------ the walls and floors

        private static readonly float[] MaterialTabLeft = new float[2], MaterialTabRight = new float[2];

        // What the detail column needs: the name and what it is, its rows, its shades, and the button that lays it (with the word under it when it cannot be).
        private static float MaterialDetailNeed(UiDesignerMaterials c)
        {
            if (c.Chosen < 0 || c.Chosen >= c.CardCount)
            {
                return UiTokens.S3 * 2f + Lh(UiStyle.Body) * 5f;
            }
            float h = UiTokens.S3 + Lh(UiStyle.Hero) + Lh(UiStyle.Small) + UiTokens.S2 + Math.Max(1, c.DetailCount) * UiTokens.PairRow;
            if (c.ShadeCount > 1)
            {
                h += UiTokens.S2 + UiTokens.PairRow + UiTokens.Control;
            }
            if (!string.IsNullOrEmpty(c.LayWhy))
            {
                h += UiTokens.S2 + Lh(UiStyle.Small);
            }
            return h + UiTokens.S4 + UiTokens.Control + UiTokens.S3;
        }

        // The materials as the game lists them, cheapest first: Floors and Walls as tabs with their counts, the search, the tiles with the material's own
        // picture and its price, and the chosen one's detail with its shades and the button that lays it over the whole floor.
        private static void Materials(UiScene s, UiAt at, UiDesignerMaterials c, float x, float y, float w, float h, IUiKeys keys)
        {
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float hy = y + (HeaderHeight - UiTokens.Control) / 2f, chipY = y + (HeaderHeight - UiTokens.ControlSmall) / 2f;
            float searchW = 224f, sx = x + w - UiTokens.S4 - searchW;
            UiDraw.Search(s, on, sx, hy, searchW, c.SearchPlaceholder, c.Search, null, MaterialSearchCommand);
            Tabs(s, on, c.Tabs, c.TabCounts, c.Tabs.Length, c.Tab, x + UiTokens.S4, chipY, sx - UiTokens.S3 - x - UiTokens.S4, MaterialTabCommand, false, MaterialTabLeft, MaterialTabRight, -1);
            float body = y + HeaderHeight;
            UiDraw.Box(s, on, x, body - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            float detailX = x + w - DetailWidth;
            UiDraw.Box(s, on, detailX, body, UiTokens.Hairline, h - HeaderHeight, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            MaterialGrid(s, on, c, x + GridLeft, body + GridTop, detailX - x - GridLeft - UiTokens.S3, h - HeaderHeight - GridTop - UiTokens.S3, w);
            MaterialDetail(s, on.InRegion(DetailRegion), c, detailX, body, DetailWidth, h - HeaderHeight, keys);
        }

        private static void MaterialGrid(UiScene s, UiAt on, UiDesignerMaterials c, float x, float y, float w, float h, float panelWidth)
        {
            int cols = Columns(panelWidth);
            if (c.CardCount == 0)
            {
                c.Scroll = 0f;
                UiDraw.EmptyState(s, on, x, y, w, h, UiIcon.Grid, c.NoneTitle, c.NoneBody);
                return;
            }
            int rows = (c.CardCount + cols - 1) / cols;
            float step = RowStep, view = Rows * step - TileGap, content = rows * step - TileGap;
            float offset = Scrolled(c.CardCount, cols, c.Scroll);
            c.Scroll = offset;
            short clip = s.AddClip(x, y, w, Math.Min(view, content), offset);
            s.Clips[clip].ContentHeight = content;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = on.Layer, Order = -1, Region = on.Region };
            int top = (int)(offset / step + 0.5f), last = Math.Min(rows - 1, top + Rows - 1);
            short list = s.AddList(rows, top, last - top + 1, clip, c.Chosen >= 0 ? c.Chosen / cols : -1);
            for (int r = top; r <= last; r++)
            {
                for (int k = 0; k < cols; k++)
                {
                    int i = r * cols + k;
                    if (i >= c.CardCount)
                    {
                        break;
                    }
                    MaterialTile(s, inClip, ref c.Cards[i], i == c.Chosen, k * (TileW + TileGap), r * step, list, r);
                }
            }
            UiDraw.ScrollThumb(s, on, x + w + UiTokens.S1, y, Math.Min(view, content), offset, content);
        }

        // A material: its own picture across the tile, its name and what it costs a piece.
        private static void MaterialTile(UiScene s, UiAt at, ref UiDesignerMaterial d, bool chosen, float x, float y, short list, int row)
        {
            int face = UiDraw.Box(s, at, x, y, TileW, TileH, chosen ? UiRole.RowSel : UiRole.Row, chosen ? UiRole.Accent : UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt on = at.In(face);
            int pic = UiDraw.Box(s, on, x + UiTokens.S1, y + UiTokens.S1, TileW - UiTokens.S1 * 2f, TilePicture, UiRole.Tile, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor);
            if (!string.IsNullOrEmpty(d.Picture))
            {
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Text = d.Picture, Value = (TileW - UiTokens.S1 * 2f) / TilePicture, X = x + UiTokens.S1, Y = y + UiTokens.S1, W = TileW - UiTokens.S1 * 2f, H = TilePicture,
                    Flags = UiFlags.Share | UiFlags.Middle | UiFlags.Decor | UiFlags.Cover, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = pic, Alpha = 1f
                };
                s.Add(ref item);
            }
            float ty = y + UiTokens.S1 + TilePicture + UiTokens.S2, tw = TileW - UiTokens.S2 * 2f;
            UiDraw.Text(s, on, x + UiTokens.S2, ty, tw, UiStyle.BodyStrong, UiRole.Text, d.Name, UiFlags.Truncate);
            UiDraw.Text(s, on, x + UiTokens.S2, ty + Lh(UiStyle.BodyStrong), tw, UiStyle.Small, UiRole.Text, d.Price, UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, TileW, TileH, MaterialCardCommand, d.Index, face, false, list, row);
        }

        private static void MaterialDetail(UiScene s, UiAt at, UiDesignerMaterials c, float x, float y, float w, float h, IUiKeys keys)
        {
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f, cy = y + UiTokens.S3;
            if (c.Chosen < 0 || c.Chosen >= c.CardCount)
            {
                UiDraw.Text(s, at, ix, cy, iw, UiStyle.Body, UiRole.Text2, c.PickBody, UiFlags.Wrap | UiFlags.Truncate, 5);
                return;
            }
            UiDraw.Text(s, at, ix, cy, iw, UiStyle.Hero, UiRole.Text, c.DetailName, UiFlags.Truncate);
            cy += Lh(UiStyle.Hero);
            UiDraw.Text(s, at, ix, cy, iw, UiStyle.Small, UiRole.Text2, c.DetailKind, UiFlags.Truncate);
            cy += Lh(UiStyle.Small) + UiTokens.S2;
            for (int i = 0; i < c.DetailCount; i++)
            {
                cy += UiDraw.InfoRow(s, at, ix, cy, iw, c.DetailLabels[i], c.DetailValues[i], false, -1, 0, c.DetailInks[i] == UiRole.None ? UiRole.Text : c.DetailInks[i]);
            }
            if (c.ShadeCount > 1)
            {
                cy += UiTokens.S2;
                UiDraw.TextMiddle(s, at, ix, cy, UiTokens.PairRow, iw, UiStyle.Small, UiRole.Text2, c.ShadeLabel, UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.PairRow;
                UiDraw.Swatches(s, at, ix, cy, iw, c.Shades, c.ShadeCount, c.ShadeChosen, ShadeCommand);
            }
            float buttonsH = UiTokens.Control, by = y + h - UiTokens.S3 - buttonsH;
            if (!string.IsNullOrEmpty(c.LayLabel))
            {
                string label = string.IsNullOrEmpty(c.LayFigure) ? c.LayLabel : c.LayLabel + "  " + c.LayFigure;
                UiDraw.Button(s, at, ix, by, iw, buttonsH, UiButtonKind.Chosen, label, UiIcon.None, UiAction.Count, keys, LayCommand, 0, c.LayOff);
                if (!string.IsNullOrEmpty(c.LayWhy))
                {
                    UiDraw.Text(s, at, ix, by - UiTokens.S2 - Lh(UiStyle.Small), iw, UiStyle.Small, UiRole.WarnText, c.LayWhy, UiFlags.Truncate);
                }
            }
            else if (!string.IsNullOrEmpty(c.PickBody))
            {
                UiDraw.Text(s, at, ix, by - Lh(UiStyle.Small), iw, UiStyle.Small, UiRole.Text2, c.PickBody, UiFlags.Wrap | UiFlags.Truncate, 2);
            }
        }
    }
}
