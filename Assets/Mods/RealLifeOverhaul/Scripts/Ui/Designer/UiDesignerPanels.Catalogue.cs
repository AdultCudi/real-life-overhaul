using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiDesignerPanels
    {
        public const float CatalogueWidth = 1600f;      // px: the catalogue and the materials, a panel over the bar's middle
        public const float CatalogueHeight = 368f;
        public const float DetailWidth = 304f;          // px: the chosen item's column at the panel's right
        public const float HeaderHeight = 56f;          // px: the tabs, the favourites switch and the search
        private const float TileW = 160f, TileH = 140f, TilePicture = 88f, TileGap = UiTokens.S2;
        private const float GridTop = UiTokens.S3, GridLeft = UiTokens.S3;
        public const int Rows = 2;                      // rows of tiles in view; the wheel moves them

        // ------------------------------------------------------------------ the grid's geometry, shared with the face

        /// <summary>How many tiles a row of the grid holds in a panel this wide.</summary>
        public static int Columns(float panelWidth)
        {
            float room = panelWidth - DetailWidth - GridLeft - UiTokens.S4;
            return Math.Max(1, (int)((room + TileGap) / (TileW + TileGap)));
        }

        /// <summary>One row of tiles with the gap under it: how far the wheel moves the grid a notch.</summary>
        public static float RowStep => TileH + TileGap;

        /// <summary>The tiles in view for a scroll (the first and the last index), so the face has their pictures ready; false when there are none.</summary>
        public static bool Visible(int count, int columns, float scroll, out int first, out int last)
        {
            first = last = 0;
            if (count <= 0)
            {
                return false;
            }
            int rows = (count + columns - 1) / columns;
            int top = Math.Max(0, Math.Min((int)(scroll / RowStep + 0.5f), Math.Max(0, rows - Rows)));
            first = top * columns;
            last = Math.Min(count - 1, (top + Rows) * columns - 1);
            return true;
        }

        /// <summary>The scroll the grid takes: a whole row, never past the last one.</summary>
        public static float Scrolled(int count, int columns, float scroll)
        {
            int rows = (count + columns - 1) / columns;
            float most = Math.Max(0, rows - Rows) * RowStep;
            return Math.Max(0f, Math.Min((float)Math.Round(scroll / RowStep) * RowStep, most));
        }

        // The panel's height by what it holds: its head, then the taller of the rows of tiles in view and the chosen one's detail, so a short list leaves no empty panel.
        private static float GridPanelHeight(int count, float width, float detailNeed)
        {
            int cols = Columns(width);
            int rows = Math.Max(1, Math.Min(Rows, (count + cols - 1) / cols));
            float grid = GridTop + rows * TileH + (rows - 1) * TileGap + UiTokens.S3;
            return UiDraw.Snap(HeaderHeight + Math.Max(grid, detailNeed));
        }

        // What the detail column needs: the name and what it is, its rows, its colours and the two buttons at its foot.
        private static float CatalogueDetailNeed(UiDesignerCatalogue c)
        {
            if (c.Chosen < 0 || c.Chosen >= c.CardCount)
            {
                return UiTokens.S3 * 2f + Lh(UiStyle.Body) * 4f;
            }
            float h = UiTokens.S3 + Lh(UiStyle.Hero) + Lh(UiStyle.Small) + UiTokens.S2 + Math.Max(1, c.DetailCount) * UiTokens.PairRow;
            if (c.SwatchCount > 0)
            {
                h += UiTokens.S2 + UiTokens.PairRow + UiTokens.S6;
            }
            return h + UiTokens.S4 + UiTokens.Control + UiTokens.S3;
        }

        // ------------------------------------------------------------------ the furniture catalogue

        private static void Catalogue(UiScene s, UiAt at, UiDesignerCatalogue c, float x, float y, float w, float h, IUiKeys keys)
        {
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            Header(s, on, c, x, y, w, keys);
            float body = y + HeaderHeight;
            UiDraw.Box(s, on, x, body - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            float detailX = x + w - DetailWidth;
            UiDraw.Box(s, on, detailX, body, UiTokens.Hairline, h - HeaderHeight, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            Grid(s, on, c, x + GridLeft, body + GridTop, detailX - x - GridLeft - UiTokens.S3, h - HeaderHeight - GridTop - UiTokens.S3, w);
            Detail(s, on.InRegion(DetailRegion), c, detailX, body, DetailWidth, h - HeaderHeight, keys);
        }

        // The head: the categories as tabs with their counts (or the requirement the catalogue is narrowed to, with its way off), the favourites'
        // switch and the search.
        private static void Header(UiScene s, UiAt on, UiDesignerCatalogue c, float x, float y, float w, IUiKeys keys)
        {
            float hy = y + (HeaderHeight - UiTokens.Control) / 2f, chipY = y + (HeaderHeight - UiTokens.ControlSmall) / 2f;
            float searchW = 224f, sx = x + w - UiTokens.S4 - searchW;
            UiDraw.Search(s, on, sx, hy, searchW, c.SearchPlaceholder, c.Search, null, SearchCommand);
            float favW = UiDraw.Snap(UiDraw.Snap(UiTextRules.Width(c.FavouritesLabel ?? string.Empty, UiStyle.Body) + UiTokens.S1) + 44f + UiTokens.S3 + UiTokens.S2 * 2f);
            float fx = sx - UiTokens.S3 - favW;
            UiDraw.Switch(s, on, fx, hy, favW, c.FavouritesOnly, c.FavouritesLabel, FavouritesCommand, 0, false);
            float tx = x + UiTokens.S4, room = fx - UiTokens.S3 - tx;
            if (c.Filtered)
            {
                float cw = UiDraw.ButtonWidth(UiButtonKind.Chip, c.ClearLabel ?? string.Empty, UiIcon.Close, UiAction.Count, keys);
                float nw = UiDraw.Snap(Math.Min(UiTextRules.Width(c.FilterNote ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1, Math.Max(0f, room - cw - UiTokens.S3)));
                UiDraw.TextMiddle(s, on, tx, chipY, UiTokens.ControlSmall, nw, UiStyle.BodyStrong, UiRole.Text, c.FilterNote, UiFlags.Truncate);
                UiDraw.Button(s, on, tx + nw + UiTokens.S3, chipY, cw, UiTokens.ControlSmall, UiButtonKind.Chip, c.ClearLabel ?? string.Empty, UiIcon.Close, UiAction.Count, keys, ClearCommand, 0);
                return;
            }
            Tabs(s, on, c.Tabs, c.TabCounts, c.TabCount, c.Tab, tx, chipY, room, TabStripCommand, true, c.TabLeft, c.TabRight, c.TabHover);
        }

        /// <summary>The tab a place across the row stands in (px of the layout), or -1: the row is one target and the face tells which tab was meant.</summary>
        public static int TabAt(UiDesignerCatalogue c, float x)
        {
            for (int i = 0; i < c.TabCount; i++)
            {
                if (x >= c.TabLeft[i] && x < c.TabRight[i] + UiTokens.S1)
                {
                    return i;
                }
            }
            return -1;
        }

        // A row of tabs, the chosen on the cyan: each its name and its count. A row too wide for its room cuts each name to its share.
        private static void Tabs(UiScene s, UiAt on, string[] names, string[] counts, int count, int chosen, float x, float y, float room, int command, bool strip, float[] left, float[] right,
            int hover)
        {
            float pad = UiTokens.S2, gap = UiTokens.S1, h = UiTokens.ControlSmall;
            // The count after each name where the whole row has room for them; names alone where it has not; and where even those are too wide, each name cut to its share.
            float withCounts = 0f, namesOnly = 0f;
            for (int i = 0; i < count; i++)
            {
                float nw = UiDraw.Snap(UiTextRules.Width(names[i] ?? string.Empty, UiStyle.Nav) + UiTokens.S1), cw = CountWidth(counts[i]);
                withCounts += pad * 2f + nw + (cw > 0f ? UiTokens.S1 + cw : 0f) + (i > 0 ? gap : 0f);
                namesOnly += pad * 2f + nw + (i > 0 ? gap : 0f);
            }
            bool showCounts = withCounts <= room;
            float share = count > 0 ? UiDraw.SnapDown((room - gap * (count - 1)) / count) : 0f;
            // The row of many tabs is one target (the face reads where the pointer is), so a long row of categories never crowds the screen's targets; a pair of tabs is two.
            float rowW = 0f;
            for (int i = 0; i < count; i++)
            {
                float nw = UiDraw.Snap(UiTextRules.Width(names[i] ?? string.Empty, UiStyle.Nav) + UiTokens.S1), cw = showCounts ? CountWidth(counts[i]) : 0f;
                float w0 = pad * 2f + nw + (cw > 0f ? UiTokens.S1 + cw : 0f);
                rowW += UiDraw.Snap(!showCounts && namesOnly > room && w0 > share ? share : w0) + (i > 0 ? gap : 0f);
            }
            int back = strip && count > 0 ? UiDraw.Box(s, on, x, y, rowW, h, UiRole.None, UiRole.None, UiTokens.RadiusMedium) : -1;
            UiAt chips = back >= 0 ? on.In(back) : on;
            float x0 = x;
            for (int i = 0; i < count; i++)
            {
                float nameW = UiDraw.Snap(UiTextRules.Width(names[i] ?? string.Empty, UiStyle.Nav) + UiTokens.S1), countW = showCounts ? CountWidth(counts[i]) : 0f;
                float w = pad * 2f + nameW + (countW > 0f ? UiTokens.S1 + countW : 0f);
                if (!showCounts && namesOnly > room && w > share)
                {
                    nameW = Math.Max(UiTokens.S6, share - pad * 2f);
                    w = share;
                }
                w = UiDraw.Snap(w);
                bool on1 = i == chosen;
                left[i] = x;
                right[i] = x + w;
                int face = UiDraw.Box(s, chips, x, y, w, h, on1 ? UiRole.Accent : strip && i == hover ? UiRole.AccentSoft : UiRole.None, UiRole.None, UiTokens.RadiusMedium,
                    on1 ? UiFlags.Selected : strip ? UiFlags.Decor : UiFlags.None);
                UiAt inside = chips.In(face);
                string shown = UiTextRules.Cut(names[i] ?? string.Empty, UiStyle.Nav, nameW);
                UiDraw.TextMiddle(s, inside, x + pad, y, h, nameW, UiStyle.Nav, on1 ? UiRole.OnAccent : UiRole.Text, shown, UiFlags.Truncate);
                if (countW > 0f)
                {
                    UiDraw.TextMiddle(s, inside, x + pad + nameW + UiTokens.S1, y, h, countW, UiStyle.Small, on1 ? UiRole.OnAccent : UiRole.Text2, counts[i], UiFlags.Truncate);
                }
                if (!strip)
                {
                    UiDraw.Node(s, on, x, y, w, h, command, i, face);
                }
                x += w + gap;
            }
            if (strip && count > 0)
            {
                UiDraw.Node(s, on, x0, y, rowW, h, command, 0, back);
            }
        }

        private static float CountWidth(string count) => string.IsNullOrEmpty(count) ? 0f : UiDraw.Snap(UiTextRules.Width(count, UiStyle.Small) + UiTokens.S1);

        // The tiles, two rows in view, the wheel moving them a row; only the rows in view are laid out.
        private static void Grid(UiScene s, UiAt on, UiDesignerCatalogue c, float x, float y, float w, float h, float panelWidth)
        {
            int cols = Columns(panelWidth);
            if (c.CardCount == 0)
            {
                c.Scroll = 0f;
                UiDraw.EmptyState(s, on, x, y, w, h, UiIcon.Sofa, c.NoneTitle, c.NoneBody);
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
                    Tile(s, inClip, c, ref c.Cards[i], i == c.Chosen, k * (TileW + TileGap), r * step, list, r);
                }
            }
            UiDraw.ScrollThumb(s, on, x + w + UiTokens.S1, y, Math.Min(view, content), offset, content);
        }

        // A tile: the item's picture over its name, its price and what it is for the room (ceiling, wall, what it holds); NEEDED over its corner when
        // it fills a requirement not yet met, the star for a favourite.
        private static void Tile(UiScene s, UiAt at, UiDesignerCatalogue c, ref UiDesignerCard d, bool chosen, float x, float y, short list, int row)
        {
            int face = UiDraw.Box(s, at, x, y, TileW, TileH, chosen ? UiRole.RowSel : UiRole.Row, chosen ? UiRole.Accent : UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt on = at.In(face);
            int pic = UiDraw.Box(s, on, x + UiTokens.S1, y + UiTokens.S1, TileW - UiTokens.S1 * 2f, TilePicture, UiRole.Tile, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor);
            if (!string.IsNullOrEmpty(d.Picture))
            {
                float side = TilePicture - UiTokens.S2;
                Picture(s, on.In(pic), d.Picture, x + (TileW - side) / 2f, y + UiTokens.S1 + UiTokens.S1, side, side);
            }
            UiAt over = on.In(pic);
            if (d.Needed)
            {
                Badge(s, over, x + UiTokens.S2, y + UiTokens.S2, c.NeededLabel, TileW - UiTokens.S2 * 3f - UiTokens.IconSmall - UiTokens.S2);
            }
            UiDraw.Icon(s, over, x + TileW - UiTokens.S2 - UiTokens.Icon, y + UiTokens.S2, UiTokens.Icon, UiIcon.Star, d.Favourite ? UiRole.Gold : UiRole.Muted, UiFlags.Decor, d.Favourite);
            float ty = y + UiTokens.S1 + TilePicture + UiTokens.S2, tw = TileW - UiTokens.S2 * 2f;
            UiDraw.Text(s, on, x + UiTokens.S2, ty, tw, UiStyle.BodyStrong, UiRole.Text, d.Name, UiFlags.Truncate);
            ty += Lh(UiStyle.BodyStrong);
            float kw = string.IsNullOrEmpty(d.Kind) ? 0f : Math.Min(UiDraw.Snap(UiTextRules.Width(d.Kind, UiStyle.Small) + UiTokens.S1), tw * 0.5f);
            UiDraw.Text(s, on, x + UiTokens.S2, ty, tw - kw - (kw > 0f ? UiTokens.S2 : 0f), UiStyle.Small, UiRole.Text, d.Price, UiFlags.Truncate);
            if (kw > 0f)
            {
                UiDraw.Text(s, on, x + TileW - UiTokens.S2 - kw, ty, kw, UiStyle.Small, UiRole.Text, d.Kind, UiFlags.Right | UiFlags.Truncate);
            }
            UiDraw.Node(s, at, x, y, TileW, TileH, CardCommand, d.Index, face, false, list, row);
        }

        // A small amber label over a tile's corner: what it is for the business.
        private static void Badge(UiScene s, UiAt at, float x, float y, string words, float most)
        {
            if (string.IsNullOrEmpty(words))
            {
                return;
            }
            float tw = Math.Min(UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S1, Math.Max(UiTokens.S8, most - UiTokens.S3 * 2f));
            float w = UiDraw.Snap(tw + UiTokens.S3 * 2f), h = UiTokens.Badge + UiTokens.S1;
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusSmall, UiFlags.Overflow);
            UiDraw.TextMiddle(s, at.In(face), x + UiTokens.S3, y, h, tw, UiStyle.SmallStrong, UiRole.WarnText, words, UiFlags.Label | UiFlags.Truncate);
        }

        /// <summary>A picture of the game's in a box, kept to its own shape.</summary>
        internal static void Picture(UiScene s, UiAt at, string key, float x, float y, float w, float h)
        {
            var item = new UiItem
            {
                Kind = UiKind.Picture, Text = key, Value = h > 0f ? w / h : 1f, X = x, Y = y, W = w, H = h, Flags = UiFlags.Share | UiFlags.Middle | UiFlags.Decor, Clip = at.Clip, Node = -1,
                Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            s.Add(ref item);
        }

        // The chosen item at the panel's right: its name and what it is, its figures, its colours and the buttons under them.
        private static void Detail(UiScene s, UiAt at, UiDesignerCatalogue c, float x, float y, float w, float h, IUiKeys keys)
        {
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f, cy = y + UiTokens.S3;
            float buttonsH = UiTokens.Control;
            float by = y + h - UiTokens.S3 - buttonsH;
            if (c.Chosen < 0 || c.Chosen >= c.CardCount)
            {
                UiDraw.Text(s, at, ix, cy, iw, UiStyle.Body, UiRole.Text2, c.NoneBody, UiFlags.Wrap | UiFlags.Truncate, 4);
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
            if (c.SwatchCount > 0)
            {
                cy += UiTokens.S2;
                UiDraw.TextMiddle(s, at, ix, cy, UiTokens.PairRow, iw, UiStyle.Small, UiRole.Text2, c.ColoursLabel, UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.PairRow;
                float sw = UiTokens.S6;
                for (int i = 0; i < c.SwatchCount && ix + (i + 1) * (sw + UiTokens.S2) <= ix + iw + UiTokens.S2; i++)
                {
                    Picture(s, at, c.Swatches[i], ix + i * (sw + UiTokens.S2), cy, sw, sw);
                }
            }
            float half = UiDraw.SnapDown((iw - UiTokens.S2) / 2f);
            UiDraw.Button(s, at, ix, by, half, buttonsH, UiButtonKind.Secondary, c.FavouriteLabel ?? string.Empty, UiIcon.Star, UiAction.Count, keys, StarCommand,
                c.Cards[c.Chosen].Index);
            UiDraw.Button(s, at, ix + half + UiTokens.S2, by, iw - half - UiTokens.S2, buttonsH, UiButtonKind.Chosen, c.PlaceLabel ?? string.Empty, UiIcon.None, UiAction.Count, keys,
                PlaceCommand, c.Cards[c.Chosen].Index, c.PlaceOff);
        }
    }
}
