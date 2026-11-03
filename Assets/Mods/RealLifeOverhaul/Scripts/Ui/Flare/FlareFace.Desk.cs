using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's working pages: a business's stars, reviews and visits; the creator studio's week, line, tier and results; the
    /// influencers for hire. Game-free.
    /// </summary>
    public static partial class FlareFace
    {
        public const float CardHeight = 176f, StatHeight = 104f, WarningHeight = 48f, BarRow = 20f;

        // ------------------------------------------------------------------------------------------------ Business

        private static void BusinessPage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            FlareBusinessModel b = m.Business;
            float bottom = y + h;
            float cy = y + PageHead(s, at, x, y, w, m.Head, m.HeadLine, false);
            if (b.Picker.Length == 0)
            {
                UiDraw.EmptyState(s, at, x + UiTokens.S8, cy + UiTokens.S8, w - UiTokens.S16, bottom - cy - UiTokens.S16, UiIcon.Store, m.List.EmptyTitle, m.List.EmptyBody);
                return;
            }
            cy += Picks(s, at, x + UiTokens.S4, cy, w - UiTokens.S8, b.Picker, b.Chosen) + UiTokens.S4;
            float cx = x + UiTokens.S4, inner = w - UiTokens.S8, gap = UiTokens.S4;
            float cw = UiDraw.SnapDown((inner - gap * 2f) / 3f);
            RatingCard(s, at, b, cx, cy, cw);
            BarsCard(s, at, b, cx + cw + gap, cy, cw);
            FiguresCard(s, at, b.VisitsTitle, b.Figures, b.FigureLabels, cx + (cw + gap) * 2f, cy, x + w - UiTokens.S4 - (cx + (cw + gap) * 2f), CardHeight);
            cy += CardHeight + UiTokens.S4;
            if (!string.IsNullOrEmpty(b.Warning))
            {
                int warn = UiDraw.Box(s, at, cx, cy, inner, WarningHeight, UiRole.WarnSoft, UiRole.None, UiTokens.RadiusLarge);
                UiAt inWarn = at.In(warn);
                UiDraw.Icon(s, inWarn, cx + UiTokens.S4, cy + (WarningHeight - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Warning, UiRole.WarnText, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, inWarn, cx + UiTokens.S4 + UiTokens.Icon + UiTokens.S3, cy, WarningHeight, inner - UiTokens.S12 - UiTokens.Icon, UiStyle.Body, UiRole.Text, b.Warning,
                    UiFlags.Truncate);
                cy += WarningHeight + UiTokens.S4;
            }
            UiDraw.Text(s, at, cx, cy, inner, UiStyle.SectionHeader, UiRole.Text2, b.ReviewsTitle, UiFlags.Label | UiFlags.Truncate);
            cy += L(UiStyle.SectionHeader) + UiTokens.S2;
            UiDraw.Box(s, at, x, cy - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            List(s, at, m.List, x, cy, w, bottom - cy, keys);
        }

        /// <summary>A row of choices as pills, the chosen one in the accent; those that do not fit wait off it. Returns its height.</summary>
        private static float Picks(UiScene s, UiAt at, float x, float y, float w, string[] picks, int chosen)
        {
            float px = x;
            for (int i = 0; i < picks.Length; i++)
            {
                float pw = PillWidth(picks[i]);
                if (px + pw > x + w)
                {
                    break;
                }
                Pill(s, at, px, y, pw, picks[i], i == chosen ? PillKind.Chosen : PillKind.Line, PickCommand, i);
                px += pw + UiTokens.S2;
            }
            return UiTokens.ControlSmall;
        }

        // The rating: its figure, its stars by halves and how many reviews it stands on.
        private static void RatingCard(UiScene s, UiAt at, FlareBusinessModel b, float x, float y, float w)
        {
            int card = Card(s, at, x, y, w, CardHeight);
            UiAt inCard = at.In(card);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            UiDraw.Text(s, inCard, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, b.RatingTitle, UiFlags.Label | UiFlags.Truncate);
            cy += L(UiStyle.SectionHeader) + UiTokens.S3;
            UiDraw.Text(s, inCard, ix, cy, iw, UiStyle.Hero, UiRole.Text, b.Rating, UiFlags.Truncate);
            cy += L(UiStyle.Hero) + UiTokens.S2;
            UiDraw.Stars(s, inCard, ix, cy, UiTokens.Icon, Math.Max(0, Math.Min(10, b.Halves)));
            cy += UiTokens.Icon + UiTokens.S3;
            UiDraw.Text(s, inCard, ix, cy, iw, UiStyle.Small, UiRole.Text2, b.RatingLine, UiFlags.Truncate);
        }

        // How the reviews fall: five stars down to one, a bar each by its share of the most.
        private static void BarsCard(UiScene s, UiAt at, FlareBusinessModel b, float x, float y, float w)
        {
            int card = Card(s, at, x, y, w, CardHeight);
            UiAt inCard = at.In(card);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            int most = 1;
            for (int i = 0; i < b.Bars.Length; i++)
            {
                most = Math.Max(most, b.Bars[i]);
            }
            float countW = UiTokens.S10, labelW = UiTokens.S6;
            float tx = ix + labelW + UiTokens.S2, tw = iw - labelW - UiTokens.S2 - countW - UiTokens.S2;
            for (int i = 0; i < 5; i++)
            {
                int n = i < b.Bars.Length ? b.Bars[i] : 0;
                float ry = cy + i * (BarRow + UiTokens.S2) + UiTokens.S2;
                int row = UiDraw.Box(s, inCard, ix, ry, iw, BarRow, UiRole.None, UiRole.None, 0f);
                UiAt inRow = inCard.In(row);
                UiDraw.TextMiddle(s, inRow, ix, ry, BarRow, UiTokens.S3, UiStyle.Small, UiRole.Text2, (5 - i).ToString(System.Globalization.CultureInfo.InvariantCulture));
                UiDraw.Icon(s, inRow, ix + UiTokens.S3, ry + (BarRow - UiTokens.S3) / 2f, UiTokens.S3, UiIcon.Star, UiRole.Gold, UiFlags.Middle, true);
                int track = UiDraw.Box(s, inRow, tx, ry + (BarRow - UiTokens.S2) / 2f, tw, UiTokens.S2, UiRole.Track, UiRole.None, UiTokens.S1, UiFlags.Middle);
                float share = n / (float)most;
                if (n > 0)
                {
                    UiDraw.Box(s, inRow.In(track), tx, ry + (BarRow - UiTokens.S2) / 2f, Math.Max(UiTokens.S2, tw * share), UiTokens.S2, UiRole.Gold, UiRole.None, UiTokens.S1,
                        UiFlags.Middle | UiFlags.Share);
                }
                UiDraw.TextMiddle(s, inRow, ix + iw - countW, ry, BarRow, countW, UiStyle.Small, UiRole.Text2, Num(n), UiFlags.Right);
            }
        }

        private static string Num(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A card of figures: its label, then the figures two to a row, each over its own label.</summary>
        private static void FiguresCard(UiScene s, UiAt at, string title, string[] figures, string[] labels, float x, float y, float w, float h)
        {
            int card = Card(s, at, x, y, w, h);
            UiAt inCard = at.In(card);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            UiDraw.Text(s, inCard, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate);
            cy += L(UiStyle.SectionHeader) + UiTokens.S3;
            int n = Math.Min(figures.Length, labels.Length);
            float colW = UiDraw.SnapDown((iw - UiTokens.S3) / 2f);
            float rowH = L(UiStyle.Number) + L(UiStyle.Small) + UiTokens.S3;
            for (int i = 0; i < n; i++)
            {
                float fx = ix + (i % 2) * (colW + UiTokens.S3), fy = cy + (i / 2) * rowH;
                if (fy + rowH > y + h)
                {
                    break;
                }
                UiDraw.Text(s, inCard, fx, fy, colW, UiStyle.Number, UiRole.Text, figures[i], UiFlags.Truncate);
                UiDraw.Text(s, inCard, fx, fy + L(UiStyle.Number), colW, UiStyle.Small, UiRole.Text2, labels[i], UiFlags.Truncate);
            }
        }

        // ------------------------------------------------------------------------------------------------ the creator studio

        public const float LineCardHeight = 168f;

        private static void StudioPage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            FlareStudioModel st = m.Studio;
            float bottom = y + h;
            float cy = y + PageHead(s, at, x, y, w, m.Head, m.HeadLine, false);
            float cx = x + UiTokens.S4, inner = w - UiTokens.S8;
            int n = Math.Min(4, st.Labels.Length);
            if (n > 0)
            {
                float tw = UiDraw.SnapDown((inner - UiTokens.S3 * (n - 1)) / n);
                for (int i = 0; i < n; i++)
                {
                    float tx = cx + i * (tw + UiTokens.S3);
                    StatCard(s, at, tx, cy, i == n - 1 ? cx + inner - tx : tw, StatHeight, st.Labels[i], At(st.Figures, i), At(st.Deltas, i), i < st.Tones.Length ? st.Tones[i] : 0);
                }
                cy += StatHeight + UiTokens.S4;
            }
            float rw = UiDraw.SnapDown(inner * 0.34f), lw = inner - rw - UiTokens.S4;
            float rx = cx + lw + UiTokens.S4;
            TierCard(s, at, st, rx, cy, rw, bottom - UiTokens.S4 - cy);
            int line = Card(s, at, cx, cy, lw, LineCardHeight);
            UiAt inLine = at.In(line);
            UiDraw.Text(s, inLine, cx + UiTokens.S4, cy + UiTokens.S4, lw - UiTokens.S8, UiStyle.SectionHeader, UiRole.Text2, st.LineTitle, UiFlags.Label | UiFlags.Truncate);
            float sy = cy + UiTokens.S4 + L(UiStyle.SectionHeader) + UiTokens.S3;
            if (Drawn(st.Line))
            {
                UiDraw.Spark(s, inLine, cx + UiTokens.S4, sy, lw - UiTokens.S8, cy + LineCardHeight - UiTokens.S4 - sy, st.Line, UiRole.Chart2);
            }
            cy += LineCardHeight + UiTokens.S3;
            List(s, at, m.List, cx - UiTokens.S4, cy, lw + UiTokens.S4, bottom - cy, keys);
        }

        /// <summary>A figure on its own card: its label in small capitals, the figure, and what changed in its tone.</summary>
        private static void StatCard(UiScene s, UiAt at, float x, float y, float w, float h, string label, string figure, string delta, int tone)
        {
            int tile = Card(s, at, x, y, w, h);
            UiAt inTile = at.In(tile);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            UiDraw.Text(s, inTile, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, label, UiFlags.Label | UiFlags.Truncate);
            cy += L(UiStyle.SectionHeader) + UiTokens.S2;
            UiDraw.Text(s, inTile, ix, cy, iw, UiStyle.Number, UiRole.Text, figure, UiFlags.Truncate);
            cy += L(UiStyle.Number) + UiTokens.S1;
            if (!string.IsNullOrEmpty(delta))
            {
                UiDraw.Text(s, inTile, ix, cy, iw, UiStyle.Small, tone > 0 ? UiRole.OkText : tone < 0 ? UiRole.DangerText : UiRole.Text2, delta, UiFlags.Truncate);
            }
        }

        // Sponsorship: the tier, how far to the next, and what each tier unlocks, ticked when it is open.
        private static void TierCard(UiScene s, UiAt at, FlareStudioModel st, float x, float y, float w, float h)
        {
            int card = Card(s, at, x, y, w, h);
            UiAt inCard = at.In(card);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            UiDraw.Text(s, inCard, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, st.TierTitle, UiFlags.Label | UiFlags.Truncate);
            cy += L(UiStyle.SectionHeader) + UiTokens.S2;
            UiDraw.Text(s, inCard, ix, cy, iw, UiStyle.Number, UiRole.Text, st.Tier, UiFlags.Truncate);
            cy += L(UiStyle.Number) + UiTokens.S3;
            int track = UiDraw.Box(s, inCard, ix, cy, iw, UiTokens.S2, UiRole.Track, UiRole.None, UiTokens.S1);
            float share = Math.Max(0f, Math.Min(1f, st.TierShare));
            if (share > 0f)
            {
                UiDraw.Box(s, inCard.In(track), ix, cy, Math.Max(UiTokens.S2, iw * share), UiTokens.S2, UiRole.Chart2, UiRole.None, UiTokens.S1, UiFlags.Share);
            }
            cy += UiTokens.S2 + UiTokens.S2;
            if (!string.IsNullOrEmpty(st.TierLine))
            {
                UiDraw.Text(s, inCard, ix, cy, iw, UiStyle.Small, UiRole.Text2, st.TierLine, UiFlags.Wrap | UiFlags.Truncate, 2);
                cy += UiDraw.Snap(UiTextRules.WrappedHeight(st.TierLine, UiStyle.Small, iw, 2)) + UiTokens.S3;
            }
            float rowH = UiTokens.S8;
            for (int i = 0; i < st.Perks.Length; i++)
            {
                if (cy + rowH > y + h - UiTokens.S4)
                {
                    break;
                }
                string perk = st.Perks[i];
                bool open = perk != null && perk.Length > 0 && perk[0] == '+';
                string words = open ? perk.Substring(1) : perk != null && perk.Length > 0 && perk[0] == '-' ? perk.Substring(1) : perk;
                int row = UiDraw.Box(s, inCard, ix, cy, iw, rowH, UiRole.None, UiRole.None, 0f);
                UiAt inRow = inCard.In(row);
                UiDraw.Icon(s, inRow, ix, cy + (rowH - UiTokens.Icon) / 2f, UiTokens.Icon, open ? UiIcon.Check : UiIcon.Lock, open ? UiRole.OkText : UiRole.Text2, UiFlags.Middle, open);
                UiDraw.TextMiddle(s, inRow, ix + UiTokens.Icon + UiTokens.S2, cy, rowH, iw - UiTokens.Icon - UiTokens.S2, UiStyle.Body, open ? UiRole.Text : UiRole.Text2, words, UiFlags.Truncate);
                cy += rowH;
            }
        }

        // ------------------------------------------------------------------------------------------------ Hire

        private static void HirePage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float bottom = y + h;
            float cy = y + PageHead(s, at, x, y, w, m.Head, m.HeadLine, false);
            if (m.Business.Picker.Length > 0)
            {
                cy += Picks(s, at, x + UiTokens.S4, cy, w - UiTokens.S8, m.Business.Picker, m.Business.Chosen) + UiTokens.S4;
            }
            List(s, at, m.List, x + UiTokens.S4, cy, w - UiTokens.S8, bottom - cy, keys);
        }
    }
}
