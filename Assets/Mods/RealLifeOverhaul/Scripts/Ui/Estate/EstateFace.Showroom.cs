using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class EstateFace
    {
        private const byte RegionCatalogue = 8, RegionChart = 9, RegionControls = 10;

        // ------------------------------------------------------------------------------------------------- the stock

        // The cars as a catalogue: a line over it that says how full the lot is and what the stock is worth, then the cards and the chosen car's detail.
        private static void CataloguePage(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            UiAt head = at.InRegion(RegionHead);
            float h = UiTokens.Control;
            int n = Math.Min(4, Math.Min(m.FigLabels.Length, m.FigValues.Length));
            float figW = 0f;
            var colW = new float[n];
            for (int i = 0; i < n; i++)
            {
                colW[i] = UiDraw.Snap(UiTextRules.Width(m.FigLabels[i] + " " + m.FigValues[i], UiStyle.Body) + UiTokens.S2);
                figW += colW[i] + UiTokens.S4;
            }
            float searchW = string.IsNullOrEmpty(m.SearchHint) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.3f), UiTokens.S16 * 5f);
            UiDraw.TextMiddle(s, head, x, y, h, Math.Max(UiTokens.S16, w - figW - (searchW > 0f ? searchW + UiTokens.S4 : 0f)), UiStyle.Title, UiRole.Text, m.HeadSub, UiFlags.Truncate);
            if (searchW > 0f)
            {
                UiDraw.Search(s, head, x + w - figW - searchW, y, searchW, m.SearchHint, m.Query, m.SearchCount, SearchCommand);
            }
            float fx = x + w - figW + UiTokens.S4;
            for (int i = 0; i < n; i++)
            {
                float lw = UiDraw.Snap(UiTextRules.Width(m.FigLabels[i] + " ", UiStyle.Body));
                UiDraw.TextMiddle(s, head, fx, y, h, lw, UiStyle.Body, UiRole.Text2, m.FigLabels[i], UiFlags.Truncate);
                UiDraw.TextMiddle(s, head, fx + lw, y, h, colW[i] - lw, UiStyle.BodyStrong, i < m.FigInks.Length && m.FigInks[i] != UiRole.None
                    ? m.FigInks[i] : UiRole.Text, m.FigValues[i], UiFlags.Truncate);
                fx += colW[i] + UiTokens.S4;
            }
            y += h + UiTokens.S3;
            // The filters and the order stand over the grid alone: the chosen car's detail beside it keeps the whole height.
            float inset = FilterRow(s, at.InRegion(RegionControls), m, x, y, UiCatalogue.GridWidth(w), keys) - y;
            int columns;
            m.TableList = UiCatalogue.Lay(s, at.InRegion(RegionCatalogue), m.Catalogue, x, y, w, bottom - y, keys, out columns, UiCatalogue.First, inset);
            m.TableClip = ClipOf(s, m.TableList);
        }

        // What shows at the left, in what order at the right, in one row where they fit and in two where they do not.
        private static float FilterRow(UiScene s, UiAt at, EstateModel m, float x, float y, float w, IUiKeys keys)
        {
            if (m.FilterChips.Length == 0 && m.SortChips.Length == 0)
            {
                return y;
            }
            float fw = ChipsWidth(m.FilterChips), sw = ChipsWidth(m.SortChips);
            if (fw + sw + UiTokens.S6 <= w)
            {
                ChipsAt(s, at, m.FilterChips, m.FilterAt, x, y, true, keys);
                ChipsAt(s, at, m.SortChips, m.SortAt, x + w - sw, y, false, keys);
                return y + UiTokens.Control + UiTokens.S3;
            }
            y = Chips(s, at, m.FilterChips, m.FilterAt, x, y, w, keys, true);
            return Chips(s, at, m.SortChips, m.SortAt, x, y, w, keys, false) + UiTokens.S1;
        }

        private static float ChipsWidth(UiActionModel[] chips)
        {
            float w = 0f;
            for (int i = 0; i < chips.Length; i++)
            {
                w += ChipWide(chips[i].Label, null) + (i > 0 ? UiTokens.S2 : 0f);
            }
            return w;
        }

        private static void ChipsAt(UiScene s, UiAt at, UiActionModel[] chips, int chosen, float x, float y, bool primary, IUiKeys keys)
        {
            for (int i = 0; i < chips.Length; i++)
            {
                ref UiActionModel a = ref chips[i];
                float bw = ChipWide(a.Label, keys);
                UiDraw.Button(s, at, x, y, bw, UiTokens.Control, i == chosen ? UiButtonKind.Chosen : UiButtonKind.Secondary, a.Label, UiIcon.None, UiAction.Count, keys, a.Command, a.Arg, a.Disabled);
                x += bw + UiTokens.S2;
            }
        }

        // ------------------------------------------------------------------------------------------------- the reports

        private static void ChartPage(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y += Head(s, at.InRegion(RegionHead), m, x, y, w);
            float sw = m.Side != null ? Math.Min(SideWidth, UiDraw.SnapDown(w * 0.34f)) : 0f;
            float cw = w - (sw > 0f ? sw + UiTokens.S4 : 0f);
            UiAt controls = at.InRegion(RegionControls);
            // The chart takes the whole height at the left; the column beside it holds the figures and the choices of what to plot and where.
            UiChart.Lay(s, at.InRegion(RegionChart), m.Chart, x, y, cw, UiDraw.SnapDown(bottom - y), UiChart.First);
            if (sw > 0f)
            {
                float rx = x + cw + UiTokens.S4, ry = y;
                // The choices come first (they are what the player acts on); the sums take the room they leave, a line left out rather than spilled.
                ry = ChipGroup(s, controls, m.ChartKindsTitle, m.ChartKinds, m.ChartKind, rx, ry, sw, keys);
                ry = ChipGroup(s, controls, m.ChartPlacesTitle, m.ChartPlaces, m.ChartPlace, rx, ry, sw, keys);
                LinkChips(s, m.ChartKinds, m.ChartPlaces);
                if (bottom - ry >= UiTokens.S16 * 3f)
                {
                    UiFactCard.Lay(s, at.InRegion(RegionRight), m.Side, rx, ry + UiTokens.S1, sw, bottom - ry - UiTokens.S1, keys, true);
                }
            }
            else
            {
                float cy = Chips(s, controls, m.ChartKinds, m.ChartKind, x, bottom - ChipRows(m.ChartKinds, w) - ChipRows(m.ChartPlaces, w), w, keys, true);
                Chips(s, controls, m.ChartPlaces, m.ChartPlace, x, cy, w, keys, false);
            }
        }

        // The choices of a stacked column in reading order, each the one under the one before, whatever the words' width wraps them into.
        private static void LinkChips(UiScene s, UiActionModel[] first, UiActionModel[] second)
        {
            int before = -1;
            for (int g = 0; g < 2; g++)
            {
                UiActionModel[] chips = g == 0 ? first : second;
                for (int i = 0; chips != null && i < chips.Length; i++)
                {
                    int node = s.FindNode(chips[i].Command, chips[i].Arg);
                    if (node < 0)
                    {
                        continue;
                    }
                    if (before >= 0)
                    {
                        s.Nodes[before].Down = node;
                        s.Nodes[node].Up = before;
                    }
                    before = node;
                }
            }
        }

        // A label in capitals over a group of choices as chips. Returns the y under it.
        private static float ChipGroup(UiScene s, UiAt at, string title, UiActionModel[] chips, int chosen, float x, float y, float w, IUiKeys keys)
        {
            if (chips == null || chips.Length == 0)
            {
                return y;
            }
            if (!string.IsNullOrEmpty(title))
            {
                UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
                y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            }
            return Chips(s, at, chips, chosen, x, y, w, keys, false) + UiTokens.S2;
        }

        // A row of choices as chips, the chosen one filled; they wrap under each other. Returns the y under them.
        private static float Chips(UiScene s, UiAt at, UiActionModel[] chips, int chosen, float x, float y, float w, IUiKeys keys, bool primary)
        {
            if (chips == null || chips.Length == 0)
            {
                return y;
            }
            float cx = x, cy = y;
            for (int i = 0; i < chips.Length; i++)
            {
                ref UiActionModel a = ref chips[i];
                float bw = ChipWide(a.Label, keys);
                if (cx > x && cx + bw > x + w)
                {
                    cx = x;
                    cy += UiTokens.Control + UiTokens.S1;
                }
                UiDraw.Button(s, at, cx, cy, bw, UiTokens.Control, i == chosen ? UiButtonKind.Chosen : UiButtonKind.Secondary, a.Label, UiIcon.None,
                    UiAction.Count, keys, a.Command, a.Arg, a.Disabled);
                cx += bw + UiTokens.S2;
            }
            return cy + UiTokens.Control + UiTokens.S2;
        }

        // One width for a chip chosen or not, so choosing one never changes how the rows break.
        private static float ChipWide(string label,
            IUiKeys keys) => Math.Max(UiDraw.ButtonWidth(UiButtonKind.Secondary, label, UiIcon.None, UiAction.Count, keys),
            UiDraw.ButtonWidth(UiButtonKind.Chosen, label, UiIcon.None, UiAction.Count, keys));

        private static float ChipRows(UiActionModel[] chips, float w)
        {
            if (chips == null || chips.Length == 0)
            {
                return 0f;
            }
            int rows = 1;
            float used = 0f;
            for (int i = 0; i < chips.Length; i++)
            {
                float bw = ChipWide(chips[i].Label, null);
                if (used > 0f && used + bw > w)
                {
                    rows++;
                    used = 0f;
                }
                used += bw + UiTokens.S2;
            }
            return rows * (UiTokens.Control + UiTokens.S1) + UiTokens.S2;
        }

        // ------------------------------------------------------------------------------------------------- the auction

        // A lot of the trade auction as a page of the window: the car and what it is worth at the left, the bidding at the right.
        private static void Auction(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            EstateAuction a = m.Auction;
            UiAt head = at.InRegion(RegionHead);
            if (!m.Card)
            {
                y = TopBar(s, head, a.Back, new UiActionModel[0], x, y, w, keys);
            }
            UiDraw.Text(s, head, x, y, w, UiStyle.SectionHeader, UiRole.Text2, a.Kicker, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            float lw = UiDraw.SnapDown((w - UiTokens.S6) * 0.42f), rw = w - UiTokens.S6 - lw;
            float lx = x, rx = x + lw + UiTokens.S6;
            // The picture takes what the bidding's column is taller by, so the two columns end together.
            float factsH = UiFactCard.Height(a.Left, lw, keys, true), rightH = UiFactCard.Height(a.Right, rw, keys, true);
            float picH = Math.Min(UiDraw.SnapDown(lw / Math.Max(1f, a.Aspect)), UiTokens.S16 * 3f);   // the car's picture is a tile, not the page
            UiAt on = at.InRegion(RegionLeft);
            int pic = UiDraw.Box(s, on, lx, y, lw, picH, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            if (!string.IsNullOrEmpty(a.Picture))
            {
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = lx, Y = y, W = lw, H = picH, Text = a.Picture,
                        Value = a.Aspect, Clip = on.Clip, Node = -1, Layer = on.Layer, Order = on.Order,
                    Parent = pic, Alpha = 1f
                };
                s.Add(ref item);
            }
            else
            {
                float g = UiTokens.S16;
                UiDraw.Icon(s, on.In(pic), lx + (lw - g) / 2f, y + (picH - g) / 2f, g, a.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
            float factsTop = y + picH + UiTokens.S3;
            float usedH = UiFactCard.Lay(s, on, a.Left, lx, factsTop, lw, bottom - factsTop, keys, true);
            // Under the car's facts, the other lots of this auction: the same short rows as the roll's lists, as many as the column has room for.
            float otherTop = UiDraw.Snap(factsTop + usedH + UiTokens.S5);
            if (m.Related.Length > 0 && bottom - otherTop >= UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + RowHeight)
            {
                Related(s, at, m, lx, otherTop, lw, bottom);
            }
            UiFactCard.Lay(s, at.InRegion(RegionRight), a.Right, rx, y, rw, bottom - y, keys, true);
        }
    }
}
