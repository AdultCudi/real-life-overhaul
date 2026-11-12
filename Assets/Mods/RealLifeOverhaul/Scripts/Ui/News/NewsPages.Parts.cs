using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The parts the news pages share: the strip's note, a card with its label, the verdict's band and the ranked bars. Game-free.</summary>
    public static partial class NewsPages
    {
        private const float FigureSlot = 208f;       // px: a figure of the verdict's band, its label, its number and its change
        private const float BarRow = 36f;            // px: a sector's row of bars

        // The window's title strip keeps a note at its right, left of the close button: the day, the edition, the price.
        private static void StripNote(UiScene s, UiAt at, UiFrame frame, string note)
        {
            if (string.IsNullOrEmpty(note))
            {
                return;
            }
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            float right = wx + ww - UiTokens.S2 - UiTokens.MinTarget - UiTokens.S3;
            float room = Math.Min(UiDraw.SnapDown(ww * 0.4f), UiDraw.Snap(UiTextRules.Width(note, UiStyle.Small) + UiTokens.S1));
            UiDraw.TextMiddle(s, at, right - room, wy, UiFrame.Strip, room, UiStyle.Small, UiRole.Text2, note, UiFlags.Right | UiFlags.Truncate | UiFlags.Chrome);
        }

        /// <summary>
        /// A card on the page: a band with its label over its room, a note at its right. Returns the card to draw in and
        /// the y where its content begins.
        /// </summary>
        private static UiAt Card(UiScene s, UiAt at, float x, float y, float w, float h, string title, string note, out float contentY, float reserve = 0f)
        {
            int box = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(box);
            contentY = y + UiTokens.S2;
            if (string.IsNullOrEmpty(title))
            {
                return on;
            }
            float nw = string.IsNullOrEmpty(note) ? 0f : UiDraw.Snap(UiTextRules.Width(note, UiStyle.Small) + UiTokens.S1);
            UiDraw.TextMiddle(s, on, x + UiTokens.S4, y + UiTokens.S3, UiTokens.PairRow, w - UiTokens.S4 * 2f - (nw > 0f ? nw + UiTokens.S3 : 0f) - reserve,
                UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            if (nw > 0f)
            {
                UiDraw.TextMiddle(s, on, x + w - UiTokens.S4 - nw, y + UiTokens.S3, UiTokens.PairRow, nw, UiStyle.Small, UiRole.Muted, note, UiFlags.Right | UiFlags.Truncate);
            }
            contentY = y + UiTokens.S3 + UiTokens.PairRow + UiTokens.S2;
            return on;
        }

        // The width of a verdict's chip on the grid, as UiDraw.Chip sets it.
        private static float ChipWidth(string text)
        {
            return UiDraw.Snap(UiTokens.S2 + UiTokens.StatusDot + UiTokens.S2 + UiTextRules.Width(text, UiStyle.SmallStrong) + UiTokens.S1 + UiTokens.S2);
        }

        /// <summary>
        /// The verdict as a band: its chip at the left, the sentence large with a line under it, and up to three figures at the
        /// right, each with its change. Returns the band's height, 0 when the model has no sentence.
        /// </summary>
        private static float LeadBand(UiScene s, UiAt at, NewsModel m, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(m.Lead))
            {
                return 0f;
            }
            int figures = m.Figures == null ? 0 : Math.Min(3, m.Figures.Length);
            float figuresW = figures * FigureSlot;
            float pad = UiTokens.S4;
            float chipW = string.IsNullOrEmpty(m.Chip) ? 0f : ChipWidth(m.Chip);
            float tx = x + pad + (chipW > 0f ? chipW + pad : 0f);
            float tw = x + w - pad - figuresW - tx - (figures > 0 ? pad : 0f);
            float titleH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            float bodyH = string.IsNullOrEmpty(m.Body) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(m.Body, UiStyle.Body, tw, 2));
            float textH = titleH + (bodyH > 0f ? UiTokens.S1 + bodyH : 0f);
            float figH = figures == 0 ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + UiTokens.S6;
            float h = UiDraw.Snap(pad * 2f + Math.Max(textH, figH));
            int box = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(box);
            if (chipW > 0f)
            {
                UiDraw.Chip(s, on, x + pad, y + UiDraw.SnapDown((h - UiTokens.Badge - UiTokens.S1) / 2f), m.Chip, m.ChipTone);
            }
            float ty = y + UiDraw.SnapDown((h - textH) / 2f);
            UiDraw.Text(s, on, tx, ty, tw, UiStyle.Title, UiRole.Text, m.Lead, UiFlags.Truncate);
            if (bodyH > 0f)
            {
                UiDraw.Text(s, on, tx, ty + titleH + UiTokens.S1, tw, UiStyle.Body, UiRole.Text2, m.Body, UiFlags.Wrap | UiFlags.Truncate, 2);
            }
            float fx = x + w - pad - figuresW;
            float fy = y + UiDraw.SnapDown((h - figH) / 2f);
            for (int i = 0; i < figures; i++)
            {
                BandFigure(s, on, ref m.Figures[i], fx + i * FigureSlot, fy, FigureSlot - UiTokens.S3);
            }
            return h;
        }

        // A figure of the band: its label in capitals, its number large, its change with the arrow under it.
        private static void BandFigure(UiScene s, UiAt at, ref UiFigureModel f, float x, float y, float w)
        {
            float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float numberH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Text2, f.Label, UiFlags.Label | UiFlags.Truncate);
            UiDraw.Text(s, at, x, y + labelH + UiTokens.S1, w, UiStyle.Display, UiRole.Text, f.Value, UiFlags.Truncate);
            if (string.IsNullOrEmpty(f.Change))
            {
                return;
            }
            float cy = y + labelH + UiTokens.S1 + numberH;
            UiRole ink = f.Good ? UiRole.OkText : UiRole.DangerText;
            UiDraw.Icon(s, at, x, cy + (UiTokens.S6 - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiModels.Rises(ref f) ? UiIcon.TrendUp : UiIcon.TrendDown, ink, UiFlags.Middle);
            UiDraw.TextMiddle(s, at, x + UiTokens.IconSmall + UiTokens.S1, cy, UiTokens.S6, w - UiTokens.IconSmall - UiTokens.S1, UiStyle.SmallStrong, ink, f.Change, UiFlags.Truncate);
        }

        /// <summary>
        /// Ranked bars that run both ways from a centre line, a rise to the right in the good colour and a fall to the left in the bad:
        /// the label, the bar and the figure. As many rows as the card has room for.
        /// </summary>
        private static void BarRows(UiScene s, UiAt at, NewsBar[] bars, float x, float y, float w, float h)
        {
            int rows = Math.Min(bars.Length, (int)(h / BarRow));
            float labelW = UiDraw.SnapDown(w * 0.34f);
            float valueW = UiDraw.SnapDown(w * 0.2f);
            float trackX = x + labelW + UiTokens.S2;
            float trackW = UiDraw.SnapDown(w - labelW - valueW - UiTokens.S4);
            float half = trackW / 2f;
            for (int i = 0; i < rows; i++)
            {
                float ry = y + i * BarRow;
                UiDraw.TextMiddle(s, at, x, ry, BarRow, labelW, UiStyle.Body, UiRole.Text, bars[i].Label, UiFlags.Truncate);
                float by = ry + (BarRow - UiTokens.Segment * 2f) / 2f;
                UiDraw.Box(s, at, trackX, by, trackW, UiTokens.Segment * 2f, UiRole.Track, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor | UiFlags.Share);
                float share = Math.Max(-1f, Math.Min(1f, bars[i].Share));
                float len = Math.Abs(share) * half;
                if (len >= 1f)
                {
                    float bx = share >= 0f ? trackX + half : trackX + half - len;
                    UiDraw.Box(s, at, bx, by, len, UiTokens.Segment * 2f, share >= 0f ? UiRole.Ok : UiRole.Danger, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor | UiFlags.Share);
                }
                UiDraw.Box(s, at, trackX + half - UiTokens.Hairline, by - UiTokens.S1, UiTokens.Hairline * 2f, UiTokens.Segment * 2f + UiTokens.S2, UiRole.Line, UiRole.None, 0f,
                    UiFlags.Decor | UiFlags.Share);
                UiDraw.TextMiddle(s, at, x + w - valueW, ry, BarRow, valueW, UiStyle.BodyStrong, share >= 0f ? UiRole.OkText : UiRole.DangerText, bars[i].Value, UiFlags.Right | UiFlags.Truncate);
            }
        }
    }
}
