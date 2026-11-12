using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The company page of the Stocks desk: the head (logo, name, ticker, sector, cap, the price with its day's move in money and
    /// per cent, one row of facts), the chart's toolbar (range, type, indicators), the chart with its tool rail, and the order
    /// panel beside it. The chart opens to the whole window (<see cref="StocksCompany.Big"/>) drawn by the same part, larger.
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public static class StocksCompanyLay
    {
        public const int Range = 56, Type = 57, Chip = 58, AddIndicator = 59, Pick = 60, Big = 62, Side = 63, Kind = 64, Life = 65, AlertAdd = 66, PutBack = 67,
            AlertLine = 68;   // AlertAdd opens the alert sheet; AlertLine stands an alert under the selected level line
        private const float Tall = UiTokens.ControlSmall;   // px: a toolbar's row
        private const float HeadH = 64f;                    // px: the head, the logo's tile and a little over it

        /// <summary>Lays the page in the room right of the sidebar. Returns where it ends.</summary>
        public static float Lay(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            StocksCompany c = m.Co;
            if (c.Big)
            {
                float top = Toolbar(s, at, c, x, y, w, keys, true);
                Chart(s, at, c, x, top, w, bottom, keys);
                PickerList(s, c);
                return bottom;
            }
            float gap = UiTokens.S4;
            float right = UiDraw.SnapDown(Math.Min(UiTokens.S16 * 5f + UiTokens.S4, w * 0.26f));
            float left = w - right - gap;
            float cy = Head(s, at, c, x, y, left);
            cy = Toolbar(s, at, c, x, cy, left, keys, false);
            Chart(s, at, c, x, cy, left, bottom, keys);
            StocksOrderLay.Lay(s, at, c, x + left + gap, y, right, bottom, keys);
            PickerList(s, c);
            return bottom;
        }

        private static void Chart(UiScene s, UiAt at, StocksCompany c, float x, float top, float w, float bottom, IUiKeys keys)
        {
            if (c.Chart != null && bottom - top > UiTokens.S16 * 4f)
            {
                UiChartDesk.Lay(s, at, c.Chart, x, top, w, bottom - top, keys);
            }
        }

        // The head: the logo on a tile, the name over its ticker, sector and cap, the price with its move, and the facts in one row.
        private static float Head(UiScene s, UiAt at, StocksCompany c, float x, float y, float w)
        {
            float logo = UiTokens.S8 + UiTokens.S4 + UiTokens.S4;   // 64 px
            int tile = UiDraw.Box(s, at, x, y, logo, logo, c.LogoTint, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiDraw.TextMiddle(s, at.In(tile), x, y, logo, logo, UiStyle.Title, UiRole.OnAccent, c.Ticker, UiFlags.Centre | UiFlags.Truncate | UiFlags.Decor);
            float nx = x + logo + UiTokens.S3;
            float nameW = UiDraw.Snap(UiTextRules.Width(c.Name, UiStyle.Hero) + UiTokens.S1);
            float lineH = UiTokens.Badge + UiTokens.S1;
            float meta = UiDraw.Snap(UiTextRules.Width(c.Ticker, UiStyle.Body) + UiTokens.S2);
            float chipW = UiDraw.Snap(UiTokens.S2 + UiTokens.StatusDot + UiTokens.S2 + UiTextRules.Width(c.Sector, UiStyle.SmallStrong) + UiTokens.S3);
            float metaW = UiDraw.Snap(UiTextRules.Width(c.Meta ?? string.Empty, UiStyle.Small) + UiTokens.S1);
            float watch = UiTokens.ControlSmall;
            float blockW = UiDraw.SnapDown(Math.Min(UiTokens.S16 * 6f, Math.Max(nameW + watch + UiTokens.S2, meta + chipW + UiTokens.S2 + metaW)));
            // The name's box is no wider than the name, so the watch button beside it never lies on the box of a longer one.
            float nameBox = Math.Min(nameW, Math.Max(UiTokens.S16, blockW - watch - UiTokens.S2));
            UiDraw.TextMiddle(s, at, nx, y, UiTokens.S8, nameBox, UiStyle.Hero, UiRole.Text, c.Name, UiFlags.Truncate);
            float wx = UiDraw.Snap(nx + nameBox + UiTokens.S2);
            UiDraw.IconButton(s, at, wx, y, watch, UiIcon.Eye, UiRole.None, StocksLay.Watch, 0, c.Watching);
            float ly = y + UiTokens.S8 + UiTokens.S1;
            UiDraw.TextMiddle(s, at, nx, ly, lineH, meta, UiStyle.Body, UiRole.Text2, c.Ticker, UiFlags.Truncate);
            float cx = nx + meta;
            if (!string.IsNullOrEmpty(c.Sector))
            {
                cx += UiDraw.Chip(s, at, cx, ly, c.Sector, UiTokens.Chart(0), blockW - meta - UiTokens.S4) + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(c.Meta) && cx < nx + blockW)
            {
                UiDraw.TextMiddle(s, at, cx, ly, lineH, nx + blockW - cx, UiStyle.Small, UiRole.Muted, c.Meta, UiFlags.Truncate);
            }
            // The price, big, with its day's move in money and per cent, in the colour of the move.
            float px = nx + blockW + UiTokens.S6;
            float priceW = UiDraw.Snap(UiTextRules.Width(c.Price, UiStyle.Display) + UiTokens.S1);
            UiRole tone = c.Way > 0 ? UiRole.OkText : c.Way < 0 ? UiRole.DangerText : UiRole.Text2;
            string move = c.ChangeMoney + " (" + c.ChangePercent + ")";
            float moveW = UiDraw.Snap(UiTextRules.Width(move, UiStyle.SmallStrong) + UiTokens.IconSmall + UiTokens.S2);
            float priceBlock = Math.Max(priceW, moveW + UiTokens.S2);
            UiDraw.TextMiddle(s, at, px, y, UiTokens.S8, priceBlock, UiStyle.Display, UiRole.Text, c.Price, UiFlags.Truncate);
            if (c.Way != 0)
            {
                UiDraw.Icon(s, at, px, ly + (lineH - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, c.Way > 0 ? UiIcon.TrendUp : UiIcon.TrendDown, UiRole.None, UiFlags.Middle);
            }
            float mx = px + (c.Way != 0 ? UiTokens.IconSmall + UiTokens.S2 : 0f);
            UiDraw.TextMiddle(s, at, mx, ly, lineH, priceBlock - (mx - px), UiStyle.SmallStrong, tone, move, UiFlags.Truncate);
            // The facts, one even row: a small label over its figure, each as wide as the wider of the two.
            int n = Math.Min(c.FactLabels.Length, c.FactValues.Length);
            float factsW = 0f;
            var cell = new float[n];
            for (int i = 0; i < n; i++)
            {
                cell[i] = UiDraw.Snap(Math.Max(UiTextRules.Width(c.FactLabels[i], UiStyle.Small), UiTextRules.Width(c.FactValues[i], UiStyle.BodyStrong)) + UiTokens.S4);
                factsW += cell[i];
            }
            float after = px + priceBlock + UiTokens.S6;
            float fy = y;
            float fx = x + w - factsW;
            float height = HeadH;
            if (fx < after)
            {
                fy = y + logo + UiTokens.S3;
                fx = x + w - factsW;
                if (fx < x)
                {
                    fx = x;
                }
                height = logo + UiTokens.S3 + UiTokens.S10;
            }
            for (int i = 0; i < n; i++)
            {
                UiDraw.TextMiddle(s, at, fx, fy, UiTokens.PairRow, cell[i] - UiTokens.S3, UiStyle.Small, UiRole.Muted, c.FactLabels[i], UiFlags.Label | UiFlags.Truncate | UiFlags.Right);
                UiDraw.TextMiddle(s, at, fx, fy + UiTokens.PairRow, UiTokens.PairRow, cell[i] - UiTokens.S3, UiStyle.BodyStrong, UiRole.Text, c.FactValues[i], UiFlags.Truncate | UiFlags.Right);
                fx += cell[i];
            }
            return y + height + UiTokens.S2;
        }

        // The toolbar: range and type as segmented choices, the indicator button, and the chart's size button at the right; then
        // one chip for each indicator on, each in its colour with an X. Returns where the chart can begin.
        private static float Toolbar(UiScene s, UiAt at, StocksCompany c, float x, float y, float w, IUiKeys keys, bool big)
        {
            float cx = x;
            float rangeW = UiSegments.Width(c.Ranges, UiStyle.Nav);
            UiSegments.Lay(s, at, cx, y, rangeW, Tall, c.Ranges, c.Range, Range, c.RangeEdges, false);
            cx += rangeW + UiTokens.S3;
            float typeW = UiSegments.Width(c.Types, UiStyle.Nav);
            UiSegments.Lay(s, at, cx, y, typeW, Tall, c.Types, c.Type, Type, c.TypeEdges, false);
            cx += typeW + UiTokens.S3;
            float addW = UiDraw.ButtonWidth(UiButtonKind.Chip, c.AddLabel, UiIcon.None, UiAction.Count, null);
            float right = x + w;
            float bigSize = Tall;
            float pxRoom = right - bigSize - UiTokens.S2;
            if (big)
            {
                string line = c.Ticker + "  " + c.Price + "  " + c.ChangePercent;
                float lw = UiTextRules.Width(line, UiStyle.BodyStrong) + UiTokens.S2;
                if (pxRoom - lw > cx + addW + UiTokens.S3)
                {
                    UiRole tone = c.Way > 0 ? UiRole.OkText : c.Way < 0 ? UiRole.DangerText : UiRole.Text;
                    UiDraw.TextMiddle(s, at, pxRoom - lw, y, Tall, lw, UiStyle.BodyStrong, tone, line, UiFlags.Right | UiFlags.Truncate);
                }
            }
            c.PickX = cx;
            c.PickY = y + Tall + UiTokens.S1;
            UiDraw.Button(s, at, cx, y, addW, Tall, UiButtonKind.Chip, c.AddLabel, UiIcon.None, UiAction.Count, keys, AddIndicator, 0);
            UiDraw.IconButton(s, at, right - bigSize, y, bigSize, c.Big ? UiIcon.Shrink : UiIcon.Expand, UiRole.None, Big, 0, false, UiAction.Option);
            float next = y + Tall + UiTokens.S2;
            if (c.Chips.Length > 0)
            {
                float chx = x;
                for (int i = 0; i < c.Chips.Length; i++)
                {
                    float cw = ChipWidth(c.Chips[i]);
                    if (chx + cw > right)
                    {
                        break;
                    }
                    IndicatorChip(s, at, c.Chips[i], chx, next, cw, i);
                    chx += cw + UiTokens.S2;
                }
                next += UiTokens.Chip + UiTokens.S2;
            }
            return next;
        }

        private static float ChipWidth(StocksChip chip) =>
            UiDraw.Snap(UiTokens.S3 + UiTokens.S4 + UiTokens.S2 + UiTextRules.Width(chip.Label, UiStyle.SmallStrong) + UiTokens.S2 + UiTokens.IconSmall + UiTokens.S3);

        // One indicator on the chart: its colour as a short bar, its name with its settings, and the X that takes it off.
        private static void IndicatorChip(UiScene s, UiAt at, StocksChip chip, float x, float y, float w, int index)
        {
            float h = UiTokens.Chip;
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.Field, UiRole.None, UiTokens.RadiusMedium);
            UiAt on = at.In(face);
            UiDraw.Box(s, on, x + UiTokens.S3, y + (h - UiTokens.S1) / 2f, UiTokens.S4, UiTokens.S1, chip.Tint, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor | UiFlags.Middle);
            float tx = x + UiTokens.S3 + UiTokens.S4 + UiTokens.S2;
            UiDraw.TextMiddle(s, on, tx, y, h, w - (tx - x) - UiTokens.IconSmall - UiTokens.S4, UiStyle.SmallStrong, UiRole.Text, chip.Label, UiFlags.Truncate);
            UiDraw.Icon(s, on, x + w - UiTokens.S3 - UiTokens.IconSmall, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Close, UiRole.Muted, UiFlags.Middle);
            UiDraw.Node(s, at, x, y, w, h, Chip, index, face);
        }

        // The picker of indicators, under its button: every indicator the chart has, the ones on ticked. It stands on the overlay layer.
        private static void PickerList(UiScene s, StocksCompany c)
        {
            if (!c.PickOpen || c.PickNames.Length == 0)
            {
                return;
            }
            var over = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 };
            int away = UiDraw.Box(s, over, 0f, 0f, s.Width, s.Height, UiRole.None, UiRole.None, 0f, UiFlags.Share | UiFlags.Decor);
            over = over.In(away);
            int n = c.PickNames.Length;
            float row = UiTokens.Control - UiTokens.S1;
            float most = 0f, hint = 0f;
            for (int i = 0; i < n; i++)
            {
                most = Math.Max(most, UiTextRules.Width(c.PickNames[i], UiStyle.BodyStrong));
                hint = Math.Max(hint, UiTextRules.Width(c.PickHints[i], UiStyle.Small));
            }
            float w = UiDraw.Snap(Math.Min(UiTokens.S16 * 7f, UiTokens.S4 + UiTokens.StatusDot + UiTokens.S3 + most + UiTokens.S4 + hint + UiTokens.S4 + UiTokens.Icon + UiTokens.S4));
            float h = n * row + UiTokens.S2 * 2f;
            float x = Math.Min(c.PickX, s.Width - w - UiTokens.S4), y = c.PickY;
            int panel = UiDraw.Box(s, over, x, y, w, h, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = over.In(panel);
            int first = -1;
            for (int i = 0; i < n; i++)
            {
                float ry = y + UiTokens.S2 + i * row;
                bool lit = i < c.PickOn.Length && c.PickOn[i];
                int face = UiDraw.Box(s, on, x + UiTokens.S1, ry, w - UiTokens.S2, row, lit ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusMedium, lit ? UiFlags.Selected : UiFlags.None);
                UiAt inside = on.In(face);
                UiDraw.Circle(s, inside, x + UiTokens.S4, ry + (row - UiTokens.StatusDot) / 2f, UiTokens.StatusDot, UiTokens.StatusDot, StocksIndicators.Tint((StocksInd)i), UiFlags.Middle);
                float tx = x + UiTokens.S4 + UiTokens.StatusDot + UiTokens.S3;
                UiDraw.TextMiddle(s, inside, tx, ry, row, UiDraw.Snap(most + UiTokens.S1), UiStyle.BodyStrong, UiRole.Text, c.PickNames[i], UiFlags.Truncate);
                float hx = UiDraw.Snap(tx + most + UiTokens.S4);
                UiDraw.TextMiddle(s, inside, hx, ry, row, x + w - UiTokens.S4 - UiTokens.Icon - UiTokens.S3 - hx, UiStyle.Small, lit ? UiRole.Text : UiRole.Text2, c.PickHints[i], UiFlags.Truncate);
                if (lit)
                {
                    UiDraw.Icon(s, inside, x + w - UiTokens.S4 - UiTokens.Icon, ry + (row - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Check, UiRole.AccentText, UiFlags.Middle);
                }
                int node = UiDraw.Node(s, on, x + UiTokens.S1, ry, w - UiTokens.S2, row, Pick, i, face);
                if (first < 0 || lit && first < 0)
                {
                    first = node;
                }
            }
            if (first >= 0)
            {
                s.FocusFirst = first;
            }
        }
    }
}
