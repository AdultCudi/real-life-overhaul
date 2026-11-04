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
            UiDraw.TextMiddle(s, head, x, y, h, Math.Max(UiTokens.S16, w - figW), UiStyle.Title, UiRole.Text, m.HeadSub, UiFlags.Truncate);
            float fx = x + w - figW + UiTokens.S4;
            for (int i = 0; i < n; i++)
            {
                float lw = UiDraw.Snap(UiTextRules.Width(m.FigLabels[i] + " ", UiStyle.Body));
                UiDraw.TextMiddle(s, head, fx, y, h, lw, UiStyle.Body, UiRole.Text2, m.FigLabels[i], UiFlags.Truncate);
                UiDraw.TextMiddle(s, head, fx + lw, y, h, colW[i] - lw, UiStyle.BodyStrong, i < m.FigInks.Length && m.FigInks[i] != UiRole.None ? m.FigInks[i] : UiRole.Text, m.FigValues[i], UiFlags.Truncate);
                fx += colW[i] + UiTokens.S4;
            }
            y += h + UiTokens.S3;
            int columns;
            m.TableList = UiCatalogue.Lay(s, at.InRegion(RegionCatalogue), m.Catalogue, x, y, w, bottom - y, keys, out columns, UiCatalogue.First);
            m.TableClip = ClipOf(s, m.TableList);
        }

        // ------------------------------------------------------------------------------------------------- the reports

        private static void ChartPage(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y += Head(s, at.InRegion(RegionHead), m, x, y, w);
            float sw = m.Side != null ? Math.Min(SideWidth, UiDraw.SnapDown(w * 0.34f)) : 0f;
            float cw = w - (sw > 0f ? sw + UiTokens.S4 : 0f);
            UiAt controls = at.InRegion(RegionControls);
            float ch = ChipRows(m.ChartKinds, cw) + ChipRows(m.ChartPlaces, cw);
            float chartH = Math.Max(UiTokens.S16 * 4f, bottom - y - ch - UiTokens.S3);
            UiChart.Lay(s, at.InRegion(RegionChart), m.Chart, x, y, cw, chartH, UiChart.First);
            float cy = y + chartH + UiTokens.S3;
            cy = Chips(s, controls, m.ChartKinds, m.ChartKind, x, cy, cw, keys);
            Chips(s, controls, m.ChartPlaces, m.ChartPlace, x, cy, cw, keys);
            if (m.Side != null)
            {
                UiFactCard.Lay(s, at.InRegion(RegionRight), m.Side, x + cw + UiTokens.S4, y, sw, bottom - y, keys, true);
            }
        }

        // A row of choices as chips, the chosen one filled; they wrap under each other. Returns the y under them.
        private static float Chips(UiScene s, UiAt at, UiActionModel[] chips, int chosen, float x, float y, float w, IUiKeys keys)
        {
            if (chips == null || chips.Length == 0)
            {
                return y;
            }
            float cx = x, cy = y;
            for (int i = 0; i < chips.Length; i++)
            {
                ref UiActionModel a = ref chips[i];
                float bw = UiDraw.ButtonWidth(UiButtonKind.Chip, a.Label, UiIcon.None, UiAction.Count, keys);
                if (cx > x && cx + bw > x + w)
                {
                    cx = x;
                    cy += UiTokens.Control + UiTokens.S1;
                }
                UiDraw.Button(s, at, cx, cy, bw, UiTokens.Control, i == chosen ? UiButtonKind.Primary : UiButtonKind.Chip, a.Label, UiIcon.None, UiAction.Count, keys, a.Command, a.Arg, a.Disabled);
                cx += bw + UiTokens.S2;
            }
            return cy + UiTokens.Control + UiTokens.S2;
        }

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
                float bw = UiDraw.Snap(UiTextRules.Width(chips[i].Label ?? string.Empty, UiStyle.Button) + UiTokens.S4 * 2f + UiTokens.S3);
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

        // A lot of the trade auction over the dimmed world: the car and what it is worth at the left, the bidding at the right, the answer at its foot.
        private static void Auction(UiScene s, EstateAuction a, UiFrame frame, IUiKeys keys)
        {
            float fw = frame.Width, fh = frame.Height - UiFrame.Strip;
            UiDraw.Box(s, UiAt.Screen, 0f, 0f, fw, frame.Height, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);
            float pw = Math.Min(UiDraw.SnapDown(fw * 0.88f), UiTokens.S16 * 64f), pad = UiTokens.S5;
            float ph = Math.Min(UiDraw.SnapDown(fh * 0.8f), UiTokens.S16 * 38f);
            float px = UiDraw.SnapDown((fw - pw) / 2f), py = UiDraw.SnapDown((fh - ph) / 2f);
            int panel = UiDraw.Box(s, UiAt.Screen, px, py, pw, ph, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = UiAt.Screen.In(panel);
            float y = UiDraw.TitleStrip(s, on, px, py, pw, a.Icon, a.Kicker, true) + UiTokens.S4;
            float lw = UiDraw.SnapDown((pw - pad * 3f) * 0.42f), rw = pw - pad * 3f - lw;
            float lx = px + pad, rx = lx + lw + pad;
            float picH = UiDraw.SnapDown(lw / Math.Max(1f, a.Aspect));
            int pic = UiDraw.Box(s, on, lx, y, lw, picH, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            if (!string.IsNullOrEmpty(a.Picture))
            {
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = lx, Y = y, W = lw, H = picH, Text = a.Picture, Value = 1f, Clip = on.Clip, Node = -1, Layer = on.Layer, Order = on.Order,
                    Parent = pic, Alpha = 1f
                };
                s.Add(ref item);
            }
            else
            {
                float g = UiTokens.S16;
                UiDraw.Icon(s, on.In(pic), lx + (lw - g) / 2f, y + (picH - g) / 2f, g, a.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
            UiFactCard.Lay(s, on.InRegion(RegionLeft), a.Left, lx, y + picH + UiTokens.S3, lw, py + ph - pad - (y + picH + UiTokens.S3), keys, false);
            UiFactCard.Lay(s, on.InRegion(RegionRight), a.Right, rx, y, rw, py + ph - pad - y, keys, false);
        }
    }
}
