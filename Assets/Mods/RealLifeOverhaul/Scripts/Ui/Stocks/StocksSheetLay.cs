using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Lays the sheet over the Stocks window. Game-free. Cost: a layout when it changes.</summary>
    public static class StocksSheetLay
    {
        public const int Pick = 40, Switch = 41, Less = 42, More = 43, Type = 44, Ok = 45, Cancel = 46, Extra = 47, Plain = 48;
        public const float Width = 1040f, SideWidth = 296f;
        private const float Emblem = 72f;

        public static void Lay(UiScene s, StocksSheetModel m, UiFrame frame, IUiKeys keys)
        {
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            UiDraw.Scrim(s, wx, wy, ww, wh, 1);
            bool side = !string.IsNullOrEmpty(m.Name);
            float cw = Math.Min(Width, UiDraw.SnapDown(ww - UiTokens.S8 * 2f));
            float sideW = side ? Math.Min(SideWidth, UiDraw.SnapDown(cw * 0.3f)) : 0f;
            float inner = cw - sideW - UiTokens.S8 * 2f;
            var at = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1, Region = 15 };
            int n = m.Fields.Length;
            bool two = n > 8 && inner > UiTokens.S16 * 8f;
            float colW = two ? UiDraw.SnapDown((inner - UiTokens.S6) / 2f) : inner;
            int split = two ? Split(m, colW) : n;
            float head = (string.IsNullOrEmpty(m.Kicker) ? 0f : Line(UiStyle.SectionHeader) + UiTokens.S2) + Line(UiStyle.AppTitle) + UiTokens.S4;
            float left = Column(null, at, m, 0, split, 0f, 0f, colW);
            float right = two ? Column(null, at, m, split, n, 0f, 0f, colW) : 0f;
            float reasonH = string.IsNullOrEmpty(m.Reason) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(m.Reason, UiStyle.Body, inner, 2)) + UiTokens.S3;
            float foot = TwoRows(m, inner, keys) ? UiTokens.Control * 2f + UiTokens.S2 : UiTokens.Control;
            float body = head + Math.Max(left, right) + reasonH + UiTokens.S4 + foot;
            float sideH = side ? SideHeight(m, sideW) : 0f;
            float ch = UiDraw.Snap(Math.Max(body, sideH) + UiTokens.S8 * 2f);
            float cx = wx + UiDraw.SnapDown((ww - cw) / 2f), cy = wy + UiDraw.SnapDown(Math.Max(UiTokens.S2, (wh - ch) / 2f));
            int card = UiDraw.Box(s, at, cx, cy, cw, ch, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(card);
            if (side)
            {
                UiDraw.Box(s, on, cx, cy, sideW, ch, UiRole.Band, UiRole.None, -UiTokens.RadiusPanel, UiFlags.Decor);
                Side(s, on, m, cx, cy, sideW, ch);
            }
            float x = cx + sideW + UiTokens.S8, y = cy + UiTokens.S8;
            if (!string.IsNullOrEmpty(m.Kicker))
            {
                UiDraw.Text(s, on, x, y, inner, UiStyle.SectionHeader, UiRole.Text2, m.Kicker, UiFlags.Label | UiFlags.Truncate);
                y += Line(UiStyle.SectionHeader) + UiTokens.S2;
            }
            UiDraw.Text(s, on, x, y, inner, UiStyle.AppTitle, UiRole.AccentText, m.Title, UiFlags.Label | UiFlags.Truncate);
            y += Line(UiStyle.AppTitle) + UiTokens.S4;
            int first = s.NodeCount;
            Column(s, on, m, 0, split, x, y, colW);
            if (two)
            {
                Column(s, on, m, split, n, x + colW + UiTokens.S6, y, colW);
            }
            y += Math.Max(left, right);
            if (reasonH > 0f)
            {
                UiDraw.Text(s, on, x, y, inner, UiStyle.Body, m.ReasonGood ? UiRole.OkText : UiRole.DangerText, m.Reason, UiFlags.Wrap | UiFlags.Truncate, 2);
                y += reasonH;
            }
            Buttons(s, on, m, x, cy + ch - UiTokens.S8 - foot, inner, keys);
            if (first < s.NodeCount && s.FocusFirst < 0)
            {
                s.FocusFirst = first;
            }
            s.AddPrompt(UiAction.Back, m.Back);
        }

        private static float Line(UiStyle style) => UiDraw.Snap(UiTextRules.LineHeight(style));

        private static float FieldHeight(in StocksFieldModel f, float w)
        {
            switch (f.Kind)
            {
                case StocksFieldKind.Head: return UiTokens.PairRow + UiTokens.S2;
                case StocksFieldKind.Fact: return UiTokens.PairRow;
                case StocksFieldKind.Note: return UiDraw.Snap(UiTextRules.WrappedHeight(f.Value, UiStyle.Small, w, 3)) + UiTokens.S2;
                default: return UiTokens.Control + UiTokens.S2;
            }
        }

        // The first field of the right column: where the two columns stand nearest level.
        private static int Split(StocksSheetModel m, float w)
        {
            float total = 0f;
            for (int i = 0; i < m.Fields.Length; i++)
            {
                total += FieldHeight(m.Fields[i], w);
            }
            float run = 0f;
            for (int i = 0; i < m.Fields.Length; i++)
            {
                if (run * 2f >= total)
                {
                    return i;
                }
                run += FieldHeight(m.Fields[i], w);
            }
            return m.Fields.Length;
        }

        // The fields from one to another down a column: laid when s is given, else only measured. Returns the height.
        private static float Column(UiScene s, UiAt at, StocksSheetModel m, int from, int to, float x, float y, float w)
        {
            float cy = y;
            float labelW = UiDraw.SnapDown(w * 0.44f);
            for (int i = from; i < to; i++)
            {
                ref StocksFieldModel f = ref m.Fields[i];
                float h = FieldHeight(f, w);
                if (s != null)
                {
                    Field(s, at, ref f, i, x, cy, w, labelW);
                }
                cy += h;
            }
            return cy - y;
        }

        private static void Field(UiScene s, UiAt at, ref StocksFieldModel f, int index, float x, float y, float w, float labelW)
        {
            float h = UiTokens.Control;
            float cx = x + labelW + UiTokens.S2, cw = w - labelW - UiTokens.S2;
            switch (f.Kind)
            {
                case StocksFieldKind.Head:
                    UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.SectionHeader, UiRole.Text2, f.Label, UiFlags.Label | UiFlags.Truncate);
                    break;
                case StocksFieldKind.Fact:
                {
                    UiRole ink = f.Tone > 0 ? UiRole.OkText : f.Tone < 0 ? UiRole.DangerText : UiRole.Text;
                    float vw = Math.Min(w * 0.6f, UiDraw.Snap(UiTextRules.Width(f.Value ?? string.Empty, UiStyle.Body) + UiTokens.S1));
                    UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w - vw - UiTokens.S3, UiStyle.Body, UiRole.Text2, f.Label, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.TextMiddle(s, at, x + w - vw, y, UiTokens.PairRow, vw, UiStyle.Body, ink, f.Value, UiFlags.Right | UiFlags.Truncate);
                    break;
                }
                case StocksFieldKind.Note:
                    UiDraw.Text(s, at, x, y, w, UiStyle.Small, f.Tone < 0 ? UiRole.DangerText : f.Tone > 0 ? UiRole.OkText : UiRole.Text2, f.Value, UiFlags.Wrap | UiFlags.Truncate, 3);
                    break;
                case StocksFieldKind.Stepper:
                    UiDraw.TextMiddle(s, at, x, y, h, labelW, UiStyle.Body, UiRole.Text2, f.Label, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Stepper(s, at, cx, y, cw, f.Value, f.CanLess && !f.Off, f.CanMore && !f.Off, Less, f.Typed ? Type : Plain, More, index, f.Off);
                    break;
                case StocksFieldKind.Choice:
                    UiDraw.TextMiddle(s, at, x, y, h, labelW, UiStyle.Body, UiRole.Text2, f.Label, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Choice(s, at, cx, y, cw, f.Value, Pick, index, f.Off);
                    break;
                case StocksFieldKind.Text:
                {
                    UiDraw.TextMiddle(s, at, x, y, h, labelW, UiStyle.Body, UiRole.Text2, f.Label, UiFlags.Label | UiFlags.Truncate);
                    int field = UiDraw.Box(s, at, cx, y, cw, h, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
                    bool caret = UiDraw.Typing(s, Type, index);
                    float room = cw - UiTokens.S6;
                    string shown = caret ? UiDraw.Typed(f.Value ?? string.Empty, UiStyle.Body, room) : f.Value ?? string.Empty;
                    UiDraw.TextMiddle(s, at.In(field), cx + UiTokens.S3, y, h, room, UiStyle.Body, UiRole.Text, shown, UiFlags.Truncate);
                    if (caret)
                    {
                        UiDraw.CaretAfter(s, at.In(field), cx + UiTokens.S3, y, h, shown, UiStyle.Body, room);
                    }
                    UiDraw.Node(s, at, cx, y, cw, h, Type, index, field);
                    break;
                }
                default:
                    UiDraw.TextMiddle(s, at, x, y, h, labelW, UiStyle.Body, UiRole.Text2, f.Label, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Switch(s, at, cx, y, cw, f.On, f.Value, Switch, index, f.Off);
                    break;
            }
        }

        private static float SideHeight(StocksSheetModel m, float w)
        {
            float inner = w - UiTokens.S8 * 2f;
            float h = Emblem + UiTokens.S4 + UiDraw.Snap(UiTextRules.WrappedHeight(m.Name, UiStyle.Title, inner, 2)) + UiTokens.S2;
            if (!string.IsNullOrEmpty(m.Big))
            {
                h += Line(UiStyle.Number) + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(m.Sub))
            {
                h += Line(UiStyle.Small) + UiTokens.S2;
            }
            h += UiTokens.S4 + m.SideLabels.Length * UiTokens.PairRow;
            if (!string.IsNullOrEmpty(m.About))
            {
                h += UiTokens.S3 + UiDraw.Snap(UiTextRules.WrappedHeight(m.About, UiStyle.Small, inner, 5));
            }
            return h + UiTokens.S8 * 2f;
        }

        private static void Side(UiScene s, UiAt at, StocksSheetModel m, float x, float y, float w, float h)
        {
            float inner = w - UiTokens.S8 * 2f, sx = x + UiTokens.S8, sy = y + UiTokens.S8;
            int tile = UiDraw.Tile(s, at, sx, sy, Emblem, UiRole.None, false);
            UiDraw.Icon(s, at.In(tile), sx + (Emblem - UiTokens.Icon * 2f) / 2f, sy + (Emblem - UiTokens.Icon * 2f) / 2f, UiTokens.Icon * 2f, m.Emblem, UiRole.None,
                UiFlags.Middle | UiFlags.Centre, true);
            sy += Emblem + UiTokens.S4;
            float nh = UiDraw.Snap(UiTextRules.WrappedHeight(m.Name, UiStyle.Title, inner, 2));
            UiDraw.Text(s, at, sx, sy, inner, UiStyle.Title, UiRole.Text, m.Name, UiFlags.Wrap | UiFlags.Truncate, 2);
            sy += nh + UiTokens.S2;
            if (!string.IsNullOrEmpty(m.Big))
            {
                UiDraw.Text(s, at, sx, sy, inner, UiStyle.Number, UiRole.Text, m.Big, UiFlags.Truncate);
                sy += Line(UiStyle.Number) + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(m.Sub))
            {
                UiDraw.Text(s, at, sx, sy, inner, UiStyle.Small, UiRole.Text2, m.Sub, UiFlags.Truncate);
                sy += Line(UiStyle.Small) + UiTokens.S2;
            }
            sy += UiTokens.S4;
            for (int i = 0; i < m.SideLabels.Length; i++)
            {
                string value = i < m.SideValues.Length ? m.SideValues[i] : string.Empty;
                float vw = Math.Min(inner * 0.6f, UiDraw.Snap(UiTextRules.Width(value, UiStyle.Body) + UiTokens.S1));
                UiDraw.TextMiddle(s, at, sx, sy, UiTokens.PairRow, inner - vw - UiTokens.S3, UiStyle.Body, UiRole.Text2, m.SideLabels[i], UiFlags.Label | UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, sx + inner - vw, sy, UiTokens.PairRow, vw, UiStyle.Body, UiRole.Text, value, UiFlags.Right | UiFlags.Truncate);
                sy += UiTokens.PairRow;
            }
            if (!string.IsNullOrEmpty(m.About))
            {
                UiDraw.Text(s, at, sx, sy + UiTokens.S3, inner, UiStyle.Small, UiRole.Text2, m.About, UiFlags.Wrap | UiFlags.Truncate, 5);
            }
        }

        // Whether the buttons need a second row: the extras do not fit beside Back and the main button in the room the card has.
        private static bool TwoRows(StocksSheetModel m, float w, IUiKeys keys)
        {
            if (m.Extras.Length == 0)
            {
                return false;
            }
            float third = UiDraw.SnapDown(w / 3f);
            float ow = string.IsNullOrEmpty(m.Confirm) ? 0f : Math.Min(third, UiDraw.ButtonWidth(UiButtonKind.Primary, m.Confirm, UiIcon.None, UiAction.Alt, keys));
            float qw = Math.Min(third, UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Back, UiIcon.None, UiAction.Back, keys));
            float want = qw + ow + UiTokens.S3 * (m.Extras.Length + (ow > 0f ? 1 : 0));
            for (int i = 0; i < m.Extras.Length; i++)
            {
                want += UiDraw.ButtonWidth(m.Extras[i].Kind, m.Extras[i].Label, m.Extras[i].Icon, UiAction.Count, keys);
            }
            return want > w;
        }

        // Back at the left of the foot, the extras beside it and the main button at the right; with no room for them all the extras
        // stand on a row of their own over Back and the main button.
        private static void Buttons(UiScene s, UiAt at, StocksSheetModel m, float x, float y, float w, IUiKeys keys)
        {
            float h = UiTokens.Control;
            float third = UiDraw.SnapDown(w / 3f);
            bool two = TwoRows(m, w, keys);
            float ow = string.IsNullOrEmpty(m.Confirm) ? 0f : Math.Min(third, UiDraw.ButtonWidth(UiButtonKind.Primary, m.Confirm, UiIcon.None, UiAction.Alt, keys));
            float qw = Math.Min(third, UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Back, UiIcon.None, UiAction.Back, keys));
            int n = m.Extras.Length;
            float rowY = two ? y + h + UiTokens.S2 : y;   // the main row, where Back stands
            float bx = x + w;
            if (ow > 0f)
            {
                bx -= ow;
                UiDraw.Button(s, at, bx, rowY, ow, h, UiButtonKind.Primary, m.Confirm, UiIcon.None, UiAction.Alt, keys, Ok, 0, m.ConfirmOff);
                bx -= UiTokens.S3;
            }
            if (two)
            {
                float ex = x;
                float each = UiDraw.SnapDown((w - UiTokens.S3 * (n - 1)) / n);
                for (int i = 0; i < n; i++)
                {
                    UiActionModel e = m.Extras[i];
                    float ew = Math.Min(each, UiDraw.ButtonWidth(e.Kind, e.Label, e.Icon, UiAction.Count, keys));
                    UiDraw.Button(s, at, ex, y, ew, h, e.Kind, e.Label, e.Icon, UiAction.Count, keys, Extra, i, e.Disabled);
                    ex += ew + UiTokens.S3;
                }
            }
            else
            {
                for (int i = n - 1; i >= 0; i--)
                {
                    UiActionModel e = m.Extras[i];
                    float ew = UiDraw.ButtonWidth(e.Kind, e.Label, e.Icon, UiAction.Count, keys);
                    bx -= ew;
                    UiDraw.Button(s, at, bx, rowY, ew, h, e.Kind, e.Label, e.Icon, UiAction.Count, keys, Extra, i, e.Disabled);
                    bx -= UiTokens.S3;
                }
            }
            UiDraw.Button(s, at, x, rowY, qw, h, UiButtonKind.Secondary, m.Back, UiIcon.None, UiAction.Back, keys, Cancel, 0);
        }
    }
}
