using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The order panel beside the chart, as four cards: the order (side and type as segmented choices, how long it stands, the shares
    /// and a limit or stop price in labelled fields, then every figure with its label), the position, what the chart says
    /// and the price alerts. A card that does not fit the window's height gives way, the last first. Game-free. Cost: a layout a change.
    /// </summary>
    public static class StocksOrderLay
    {
        private const float Seg = UiTokens.ControlSmall + UiTokens.S1;   // px: a segmented choice's height
        private const float Pad = UiTokens.S3;                           // px: a card's padding
        private const float Row = UiTokens.PairRow;

        public static void Lay(UiScene s, UiAt at, StocksCompany c, float x, float y, float w, float bottom, IUiKeys keys)
        {
            StocksTicket t = c.Order;
            if (t == null)
            {
                return;
            }
            float h = OrderHeight(t, w, keys);
            Order(s, at, c, t, x, y, w, h, keys);
            y += h + UiTokens.S2;
            float ph = PositionHeight(c, t);
            if (y + ph <= bottom)
            {
                Position(s, at, c, t, x, y, w, ph);
                y += ph + UiTokens.S2;
            }
            float ah = AlertsHeight(c);
            int rows = c.Signals.Length;
            float sh = SignalsHeight(rows);
            while (rows > 1 && y + sh + UiTokens.S2 + ah > bottom)
            {
                rows--;
                sh = SignalsHeight(rows);
            }
            if (y + sh <= bottom)
            {
                Signals(s, at, c, x, y, w, sh, rows);
                y += sh + UiTokens.S2;
            }
            if (y + ah <= bottom)
            {
                Alerts(s, at, c, x, y, w, ah, keys);
            }
        }

        // The order card's height, from what it holds: the same sums the drawing makes.
        private static float OrderHeight(StocksTicket t, float w, IUiKeys keys)
        {
            float h = Pad + Row + UiTokens.S2 + Seg + UiTokens.S2 + Seg + UiTokens.S2 + UiTokens.ControlSmall + UiTokens.S2 + UiTokens.Control + UiTokens.S2;
            if (!string.IsNullOrEmpty(t.Price))
            {
                h += UiTokens.Control + UiTokens.S2;
            }
            h += t.Rows.Length * Row;
            if (!string.IsNullOrEmpty(t.Why))
            {
                h += UiDraw.Snap(UiTextRules.WrappedHeight(t.Why, UiStyle.Small, w - Pad * 2f, 2)) + UiTokens.S1;
            }
            return UiDraw.Snap(h + UiTokens.S2 + UiTokens.Control + Pad);
        }

        private static float PositionHeight(StocksCompany c, StocksTicket t)
        {
            float rows = c.Position.Length > 0 ? c.Position.Length * Row : Row;
            return Pad + Row + rows + (string.IsNullOrEmpty(t.PutBack) ? 0f : UiTokens.S2 + UiTokens.Control) + Pad;
        }

        private static float SignalsHeight(int rows) => Pad + Row + Math.Max(1, rows) * Row + Pad;

        private static float AlertsHeight(StocksCompany c) =>
            Pad + Row + (c.Alerts.Length == 0 ? Row : Math.Min(3, c.Alerts.Length) * UiTokens.Chip) + UiTokens.S2 + UiTokens.ControlSmall + Pad;

        private static int Card(UiScene s, UiAt at, float x, float y, float w, float h) => UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);

        private static void Title(UiScene s, UiAt on, float x, float y, float w, string title, string right)
        {
            float rw = string.IsNullOrEmpty(right) ? 0f : Math.Min(w * 0.6f, UiTextRules.Width(right, UiStyle.Small) + UiTokens.S1);
            UiDraw.TextMiddle(s, on, x, y, Row, w - rw - UiTokens.S2, UiStyle.SectionHeader, UiRole.Muted, title, UiFlags.Label | UiFlags.Truncate);
            if (rw > 0f)
            {
                UiDraw.TextMiddle(s, on, x + w - rw, y, Row, rw, UiStyle.Small, UiRole.Text2, right, UiFlags.Right | UiFlags.Truncate);
            }
        }

        private static void Order(UiScene s, UiAt at, StocksCompany c, StocksTicket t, float x, float y, float w, float h, IUiKeys keys)
        {
            int card = Card(s, at, x, y, w, h);
            UiAt on = at.In(card);
            float ix = x + Pad, iw = w - Pad * 2f;
            float cy = y + Pad;
            Title(s, on, ix, cy, iw, t.Title, t.CashWords);
            cy += Row + UiTokens.S2;
            UiSegments.Lay(s, on, ix, cy, iw, Seg, t.Sides, t.SideAt, StocksCompanyLay.Side, c.SideEdges, true, new[] { UiRole.Ok, UiRole.Danger });
            cy += Seg + UiTokens.S2;
            UiSegments.Lay(s, on, ix, cy, iw, Seg, t.Kinds, t.KindAt, StocksCompanyLay.Kind, c.KindEdges, true, null, UiStyle.Small);
            cy += Seg + UiTokens.S2;
            float label = UiDraw.LabelColumn(iw, t.LifeLabel, t.SharesLabel, t.PriceLabel);
            float fx = ix + label + UiTokens.S2, fw = iw - label - UiTokens.S2;
            UiDraw.TextMiddle(s, on, ix, cy, UiTokens.ControlSmall, label, UiStyle.Body, UiRole.Text2, t.LifeLabel, UiFlags.Label | UiFlags.Truncate);
            UiSegments.Lay(s, on, fx, cy, fw, UiTokens.ControlSmall, t.Lives, t.LifeAt, StocksCompanyLay.Life, c.LifeEdges, true, null, UiStyle.SmallStrong);
            cy += UiTokens.ControlSmall + UiTokens.S2;
            UiDraw.TextMiddle(s, on, ix, cy, UiTokens.Control, label, UiStyle.Body, UiRole.Text2, t.SharesLabel, UiFlags.Label | UiFlags.Truncate);
            UiDraw.Stepper(s, on, fx, cy, fw, t.Shares, t.CanLess, t.CanMore, StocksLay.Less, StocksLay.Shares, StocksLay.More, 0, false);
            cy += UiTokens.Control + UiTokens.S2;
            if (!string.IsNullOrEmpty(t.Price))
            {
                UiDraw.TextMiddle(s, on, ix, cy, UiTokens.Control, label, UiStyle.Body, UiRole.Text2, t.PriceLabel, UiFlags.Label | UiFlags.Truncate);
                UiDraw.Stepper(s, on, fx, cy, fw, t.Price, true, true, StocksLay.PriceLess, StocksLay.PricePlain, StocksLay.PriceMore, 0, false);
                cy += UiTokens.Control + UiTokens.S2;
            }
            for (int i = 0; i < t.Rows.Length; i++)
            {
                UiDraw.InfoRow(s, on, ix, cy, iw, t.Rows[i].Label, t.Rows[i].Value, false, -1, 0, t.Rows[i].Tone != UiRole.None ? t.Rows[i].Tone : UiRole.Text);
                cy += Row;
            }
            if (!string.IsNullOrEmpty(t.Why))
            {
                float wh = UiDraw.Snap(UiTextRules.WrappedHeight(t.Why, UiStyle.Small, iw, 2));
                UiDraw.Text(s, on, ix, cy, iw, UiStyle.Small, t.PlaceDisabled ? UiRole.DangerText : UiRole.WarnText, t.Why, UiFlags.Wrap | UiFlags.Truncate, 2);
                cy += wh + UiTokens.S1;
            }
            UiDraw.Button(s, on, ix, y + h - Pad - UiTokens.Control, iw, UiTokens.Control, UiButtonKind.Primary, t.Place, UiIcon.None, UiAction.Alt, keys, StocksLay.Place, 0, t.PlaceDisabled);
        }

        private static void Position(UiScene s, UiAt at, StocksCompany c, StocksTicket t, float x, float y, float w, float h)
        {
            int card = Card(s, at, x, y, w, h);
            UiAt on = at.In(card);
            float ix = x + Pad, iw = w - Pad * 2f, cy = y + Pad;
            Title(s, on, ix, cy, iw, c.PositionTitle, null);
            cy += Row;
            if (c.Position.Length == 0)
            {
                UiDraw.TextMiddle(s, on, ix, cy, Row, iw, UiStyle.Body, UiRole.Text2, c.PositionNone, UiFlags.Truncate);
                cy += Row;
            }
            for (int i = 0; i < c.Position.Length; i++)
            {
                UiDraw.InfoRow(s, on, ix, cy, iw, c.Position[i].Label, c.Position[i].Value, false, -1, 0, c.Position[i].Tone != UiRole.None ? c.Position[i].Tone : UiRole.Text);
                cy += Row;
            }
            if (!string.IsNullOrEmpty(t.PutBack))
            {
                float label = UiDraw.Snap(Math.Min(iw * 0.55f, UiTextRules.Width(t.PutBackLabel ?? string.Empty, UiStyle.Body) + UiTokens.S1));
                UiDraw.TextMiddle(s, on, ix, cy + UiTokens.S2, UiTokens.ControlSmall, label, UiStyle.Body, UiRole.Text2, t.PutBackLabel, UiFlags.Label | UiFlags.Truncate);
                UiDraw.Choice(s, on, ix + label + UiTokens.S2, cy + UiTokens.S2, iw - label - UiTokens.S2, t.PutBack, StocksCompanyLay.PutBack, 0, false);
            }
        }

        private static void Signals(UiScene s, UiAt at, StocksCompany c, float x, float y, float w, float h, int rows)
        {
            int card = Card(s, at, x, y, w, h);
            UiAt on = at.In(card);
            float ix = x + Pad, iw = w - Pad * 2f, cy = y + Pad;
            Title(s, on, ix, cy, iw, c.SignalsTitle, null);
            cy += Row;
            if (c.Signals.Length == 0)
            {
                UiDraw.TextMiddle(s, on, ix, cy, Row, iw, UiStyle.Small, UiRole.Muted, c.SignalsNone, UiFlags.Truncate);
                return;
            }
            for (int i = 0; i < rows && i < c.Signals.Length; i++)
            {
                UiRole tone = c.Signals[i].Tone != UiRole.None ? c.Signals[i].Tone : UiRole.Muted;
                UiDraw.Circle(s, on, ix + UiTokens.S1, cy + (Row - UiTokens.StatusDot) / 2f, UiTokens.StatusDot, UiTokens.StatusDot, tone, UiFlags.Middle);
                UiDraw.TextMiddle(s, on, ix + UiTokens.StatusDot + UiTokens.S3, cy, Row, iw - UiTokens.StatusDot - UiTokens.S3, UiStyle.Body, UiRole.Text, c.Signals[i].Words, UiFlags.Truncate);
                cy += Row;
            }
        }

        private static void Alerts(UiScene s, UiAt at, StocksCompany c, float x, float y, float w, float h, IUiKeys keys)
        {
            int card = Card(s, at, x, y, w, h);
            UiAt on = at.In(card);
            float ix = x + Pad, iw = w - Pad * 2f, cy = y + Pad;
            Title(s, on, ix, cy, iw, c.AlertsTitle, null);
            cy += Row;
            if (c.Alerts.Length == 0)
            {
                UiDraw.TextMiddle(s, on, ix, cy, Row, iw, UiStyle.Small, UiRole.Muted, c.AlertsNone, UiFlags.Truncate);
                cy += Row;
            }
            for (int i = 0; i < c.Alerts.Length && i < 3; i++)
            {
                int face = UiDraw.Box(s, on, ix, cy, iw, UiTokens.Chip, UiRole.None, UiRole.None, UiTokens.RadiusMedium);
                UiAt in2 = on.In(face);
                UiDraw.Icon(s, in2, ix, cy + (UiTokens.Chip - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Bell, UiRole.None, UiFlags.Middle, true);
                float state = UiTextRules.Width(c.Alerts[i].State ?? string.Empty, UiStyle.Small) + UiTokens.S1;
                float tx = ix + UiTokens.Icon + UiTokens.S2;
                UiDraw.TextMiddle(s, in2, tx, cy, UiTokens.Chip, iw - (tx - ix) - state - UiTokens.IconSmall - UiTokens.S4, UiStyle.Body, UiRole.Text, c.Alerts[i].Words, UiFlags.Truncate);
                UiDraw.TextMiddle(s, in2, ix + iw - UiTokens.IconSmall - UiTokens.S3 - state, cy, UiTokens.Chip, state, UiStyle.Small, UiRole.Muted, c.Alerts[i].State,
                    UiFlags.Right | UiFlags.Truncate);
                UiDraw.Icon(s, in2, ix + iw - UiTokens.IconSmall, cy + (UiTokens.Chip - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Close, UiRole.Muted, UiFlags.Middle);
                UiDraw.Node(s, on, ix, cy, iw, UiTokens.Chip, StocksLay.Alert, i, face);
                cy += UiTokens.Chip;
            }
            cy += UiTokens.S2;
            float half = UiDraw.SnapDown((iw - UiTokens.S2) / 2f);
            UiDraw.Button(s, on, ix, cy, half, UiTokens.ControlSmall, UiButtonKind.Secondary, c.AlertAbove, UiIcon.None, UiAction.Count, keys,
                StocksCompanyLay.AlertAdd, 0, c.AlertsFull);
            UiDraw.Button(s, on, ix + half + UiTokens.S2, cy, half, UiTokens.ControlSmall, UiButtonKind.Secondary, c.AlertBelow, UiIcon.None, UiAction.Count, keys,
                StocksCompanyLay.AlertAdd, 1, c.AlertsFull);
        }
    }
}
