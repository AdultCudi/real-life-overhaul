using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class UtilitiesFace
    {
        private const float SheetWidth = 640f;   // px

        // The sheet over the page: a question, the districts to dig in, or the company's settings, on a card over the dimmed window.
        private static void Sheet(UiScene s, UtilitiesSheetModel k, UiFrame frame, IUiKeys keys)
        {
            float wx = 0f, wy = frame.ContentTop, ww = frame.Width, wh = frame.BandY - frame.ContentTop;
            if (k.Kind == UtilitiesSheetKind.Question)
            {
                UiDraw.Question(s, wx, wy, ww, wh, k.Title, k.Body, k.Answers, k.Answers.Length, keys);
                return;
            }
            UiDraw.Scrim(s, wx, wy, ww, wh, 1);
            float cw = Math.Min(SheetWidth, UiDraw.SnapDown(ww - UiTokens.S16 * 2f));
            float pad = UiTokens.S6, inner = cw - pad * 2f;
            var at = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1, Region = 15 };
            float ch = UiDraw.Snap(SheetBody(s, at, k, 0f, 0f, inner, keys, false) + pad * 2f);
            ch = Math.Min(ch, UiDraw.SnapDown(wh - UiTokens.S8));
            float cx = wx + UiDraw.SnapDown((ww - cw) / 2f), cy = wy + UiDraw.SnapDown(Math.Max(UiTokens.S4, (wh - ch) / 2f));
            int card = UiDraw.Box(s, at, cx, cy, cw, ch, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            SheetBody(s, at.In(card), k, cx + pad, cy + pad, inner, keys, true);
        }

        // The sheet's parts from the top: measured with draw false, laid with it true. Returns the height they take.
        private static float SheetBody(UiScene s, UiAt at, UtilitiesSheetModel k, float x, float y, float w, IUiKeys keys, bool draw)
        {
            float cy = y;
            if (draw)
            {
                UiDraw.TextMiddle(s, at, x, cy, UiTokens.Control, w - UiTokens.Control - UiTokens.S2, UiStyle.Number, UiRole.Text, k.Title, UiFlags.Truncate);
                int close = UiDraw.Box(s, at, x + w - UiTokens.Control, cy, UiTokens.Control, UiTokens.Control, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiDraw.Icon(s, at.In(close), x + w - UiTokens.Control + (UiTokens.Control - UiTokens.Icon) / 2f, cy + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Close,
                    UiRole.Text2, UiFlags.Middle | UiFlags.Centre);
                UiDraw.Node(s, at, x + w - UiTokens.Control, cy, UiTokens.Control, UiTokens.Control, SheetClose, 0, close);
            }
            cy += UiTokens.Control + UiTokens.S2;
            if (k.Kind == UtilitiesSheetKind.Dig)
            {
                cy = DigRows(s, at, k, x, cy, w, keys, draw);
            }
            else if (k.Card != null)
            {
                float h = Card(s, at, k.Card, x, cy, w, 4000f, -1, SheetLess, SheetMore, keys, false, false);
                if (draw)
                {
                    Card(s, at, k.Card, x, cy, w, h, -1, SheetLess, SheetMore, keys, false, true);
                }
                cy += h;
            }
            cy += UiTokens.S3;
            if (!string.IsNullOrEmpty(k.Close))
            {
                float bw = Math.Min(w, UiDraw.ButtonWidth(UiButtonKind.Primary, k.Close, UiIcon.None, UiAction.Count, keys));
                if (draw)
                {
                    UiDraw.Button(s, at, x + w - bw, cy, bw, UiTokens.Control, UiButtonKind.Primary, k.Close, UiIcon.None, UiAction.Count, keys, SheetClose, 1);
                }
                cy += UiTokens.Control;
            }
            return cy - y;
        }

        // The districts not started: a colour and the name, how many homes it would reach, what it costs, and the button that asks before it digs.
        private static float DigRows(UiScene s, UiAt at, UtilitiesSheetModel k, float x, float y, float w, IUiKeys keys, bool draw)
        {
            float row = DistrictH;
            for (int i = 0; i < k.Districts.Length; i++)
            {
                if (draw)
                {
                    UtilitiesDistrictRow d = k.Districts[i];
                    UiRole hue = (UiRole)((int)UiRole.Chart1 + Math.Max(0, Math.Min(7, d.Hue)));
                    float dot = UiTokens.Dot;
                    float bw = Math.Min(UiTokens.S16 * 1.5f, UiDraw.ButtonWidth(UiButtonKind.Secondary, d.Action, UiIcon.None, UiAction.Count, keys));
                    float costW = UiDraw.Snap(UiTextRules.Width(d.TakeUpWords, UiStyle.BodyStrong) + UiTokens.S1);
                    UiDraw.Box(s, at, x, y + (row - dot) / 2f, dot, dot, hue, UiRole.None, UiTokens.RadiusSmall, UiFlags.Middle);
                    float nx = x + dot + UiTokens.S3, nw = w - dot - UiTokens.S3 - bw - costW - UiTokens.S4 * 2f;
                    float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small), top = y + (row - lh - sh) / 2f;
                    UiDraw.Text(s, at, nx, top, nw, UiStyle.Title, UiRole.Text, d.Name, UiFlags.Truncate | UiFlags.Middle);
                    UiDraw.Text(s, at, nx, top + lh, nw, UiStyle.Small, UiRole.Text2, d.Homes, UiFlags.Truncate | UiFlags.Middle);
                    UiDraw.TextMiddle(s, at, x + w - bw - UiTokens.S4 - costW, y, row, costW, UiStyle.BodyStrong, UiRole.Text, d.TakeUpWords, UiFlags.Right | UiFlags.Truncate);
                    UiDraw.Button(s, at, x + w - bw, y + (row - UiTokens.ControlSmall) / 2f, bw, UiTokens.ControlSmall, UiButtonKind.Secondary, d.Action, UiIcon.None, UiAction.Count, keys,
                        SheetPick, d.ActionDistrict);
                }
                y += row;
            }
            return y;
        }
    }
}
