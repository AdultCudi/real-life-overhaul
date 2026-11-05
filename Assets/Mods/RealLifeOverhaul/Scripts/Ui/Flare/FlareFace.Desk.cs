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
            float promoteW = string.IsNullOrEmpty(b.Promote) ? 0f : IconPillWidth(b.Promote);
            if (promoteW > 0f)
            {
                Pill(s, at, x + w - UiTokens.S4 - promoteW, cy, promoteW, b.Promote, PillKind.Orange, PromoteCommand, b.Chosen, UiIcon.Megaphone);
            }
            cy += Picks(s, at, x + UiTokens.S4, cy, w - UiTokens.S8 - (promoteW > 0f ? promoteW + UiTokens.S2 : 0f), b.Picker, b.Chosen) + UiTokens.S4;
            float cx = x + UiTokens.S4, inner = w - UiTokens.S8, gap = UiTokens.S4;
            float cw = UiDraw.SnapDown((inner - gap * 2f) / 3f);
            RatingCard(s, at, b, cx, cy, cw);
            BarsCard(s, at, b, cx + cw + gap, cy, cw);
            FiguresCard(s, at, b.VisitsTitle, b.Figures, b.FigureLabels, cx + (cw + gap) * 2f, cy, x + w - UiTokens.S4 - (cx + (cw + gap) * 2f), CardHeight);
            cy += CardHeight + UiTokens.S4;
            if (!string.IsNullOrEmpty(b.ReachTitle))
            {
                const float reachH = 96f;
                int reach = Card(s, at, cx, cy, inner, reachH);
                UiAt inReach = at.In(reach);
                Line(s, inReach, cx + UiTokens.S4, cy + UiTokens.S3, inner - UiTokens.S8, UiStyle.SectionHeader, UiRole.Text2, b.ReachTitle, UiFlags.Label | UiFlags.Truncate);
                float ry = cy + UiTokens.S3 + L(UiStyle.SectionHeader) + UiTokens.S2;
                if (Drawn(b.Reach))
                {
                    UiDraw.Spark(s, inReach, cx + UiTokens.S4, ry, inner - UiTokens.S8, cy + reachH - UiTokens.S3 - ry, b.Reach, UiRole.Chart2);
                }
                else
                {
                    Line(s, inReach, cx + UiTokens.S4, ry, inner - UiTokens.S8, UiStyle.Body, UiRole.Text2, b.ReachEmpty, UiFlags.Truncate);
                }
                cy += reachH + UiTokens.S4;
            }
            if (!string.IsNullOrEmpty(b.Warning))
            {
                // A slate row like the rival's under it, the state said by the amber glyph (a large fill of the warning's colour is a brown band on the dark panel).
                int warn = Card(s, at, cx, cy, inner, WarningHeight);
                UiAt inWarn = at.In(warn);
                UiDraw.Icon(s, inWarn, cx + UiTokens.S4, cy + (WarningHeight - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Warning, UiRole.WarnText, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, inWarn, cx + UiTokens.S4 + UiTokens.Icon + UiTokens.S3, cy, WarningHeight, inner - UiTokens.S12 - UiTokens.Icon, UiStyle.Body, UiRole.Text, b.Warning,
                    UiFlags.Truncate);
                cy += WarningHeight + UiTokens.S4;
            }
            if (!string.IsNullOrEmpty(b.Rival))
            {
                int rival = UiDraw.Box(s, at, cx, cy, inner, WarningHeight, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
                UiAt inRival = at.In(rival);
                UiDraw.Icon(s, inRival, cx + UiTokens.S4, cy + (WarningHeight - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Flag, UiRole.Text2, UiFlags.Middle);
                UiDraw.TextMiddle(s, inRival, cx + UiTokens.S4 + UiTokens.Icon + UiTokens.S3, cy, WarningHeight, inner - UiTokens.S12 - UiTokens.Icon, UiStyle.Body, UiRole.Text, b.Rival,
                    UiFlags.Truncate);
                cy += WarningHeight + UiTokens.S4;
            }
            Line(s, at, cx, cy, inner, UiStyle.SectionHeader, UiRole.Text2, b.ReviewsTitle, UiFlags.Label | UiFlags.Truncate);
            cy += L(UiStyle.SectionHeader) + UiTokens.S2;
            UiDraw.Box(s, at, x, cy - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            List(s, at, m.List, x, cy, w, bottom - cy, keys);
        }

        /// <summary>A row of choices as pills, the chosen one in the accent; those that do not fit wait off it. Returns its height.</summary>
        private static float Picks(UiScene s, UiAt at, float x, float y, float w, string[] picks, int chosen, int command = PickCommand)
        {
            float px = x;
            for (int i = 0; i < picks.Length; i++)
            {
                float pw = PillWidth(picks[i]);
                if (px + pw > x + w)
                {
                    break;
                }
                Pill(s, at, px, y, pw, picks[i], i == chosen ? PillKind.Chosen : PillKind.Line, command, i);
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
            Line(s, inCard, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, b.RatingTitle, UiFlags.Label | UiFlags.Truncate);
            cy += L(UiStyle.SectionHeader) + UiTokens.S3;
            Line(s, inCard, ix, cy, iw, UiStyle.Hero, UiRole.Text, b.Rating, UiFlags.Truncate);
            cy += L(UiStyle.Hero) + UiTokens.S2;
            UiDraw.Stars(s, inCard, ix, cy, UiTokens.Icon, Math.Max(0, Math.Min(10, b.Halves)));
            cy += UiTokens.Icon + UiTokens.S3;
            Line(s, inCard, ix, cy, iw, UiStyle.Small, UiRole.Text2, b.RatingLine, UiFlags.Truncate);
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
        private static void FiguresCard(UiScene s, UiAt at, string title, string[] figures, string[] labels, float x, float y, float w, float h, int columns = 2)
        {
            int card = Card(s, at, x, y, w, h);
            UiAt inCard = at.In(card);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            Line(s, inCard, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate);
            cy += L(UiStyle.SectionHeader) + UiTokens.S3;
            int n = Math.Min(figures.Length, labels.Length);
            float colW = UiDraw.SnapDown((iw - UiTokens.S3 * (columns - 1)) / columns);
            int labelLines = FigureLabelLines(labels, colW);
            float rowH = L(UiStyle.Number) + L(UiStyle.Small) * labelLines + UiTokens.S3;
            for (int i = 0; i < n; i++)
            {
                float fx = ix + (i % columns) * (colW + UiTokens.S3), fy = cy + (i / columns) * rowH;
                if (fy + rowH > y + h)
                {
                    break;
                }
                Line(s, inCard, fx, fy, colW, UiStyle.Number, UiRole.Text, figures[i], UiFlags.Truncate);
                Flow(s, inCard, fx, fy + L(UiStyle.Number), colW, UiStyle.Small, UiRole.Text2, labels[i], labelLines);
            }
        }

        /// <summary>The lines the labels under a card's figures take at a column's width: one, or two where a language needs it.</summary>
        public static int FigureLabelLines(string[] labels, float colW)
        {
            for (int i = 0; labels != null && i < labels.Length; i++)
            {
                if (UiTextRules.Width(labels[i] ?? string.Empty, UiStyle.Small) > colW)
                {
                    return 2;
                }
            }
            return 1;
        }

        // ------------------------------------------------------------------------------------------------ the creator studio

        public const float LineCardHeight = 168f;

        private static void StudioPage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            FlareStudioModel st = m.Studio;
            float bottom = y + h;
            float cy = y + PageHead(s, at, x, y, w, m.Head, m.HeadLine, false);
            float cx = x + UiTokens.S4, inner = w - UiTokens.S8, right = cx + inner;
            int n = Math.Min(4, st.Labels.Length);
            if (n > 0)
            {
                float tw = UiDraw.SnapDown((inner - UiTokens.S3 * (n - 1)) / n);
                for (int i = 0; i < n; i++)
                {
                    float tx = cx + i * (tw + UiTokens.S3);
                    // the last takes the rest, so the rows end together
                    StatCard(s, at, tx, cy, i == n - 1 ? right - tx : tw, StatHeight, st.Labels[i], At(st.Figures, i), At(st.Deltas, i), i < st.Tones.Length ? st.Tones[i] : 0);
                }
                cy += StatHeight + UiTokens.S4;
            }
            // The tier card is as tall as its words; the followers' line and the creator income stand beside it, to the same foot; the results run the page's whole width under them.
            float rw = UiDraw.SnapDown(inner * 0.34f), lw = inner - rw - UiTokens.S4;
            float rx = cx + lw + UiTokens.S4;
            float tierH = Math.Min(bottom - UiTokens.S4 - cy, TierCard(s, at, st, rx, cy, rw, 0f, false));
            TierCard(s, at, st, rx, cy, rw, tierH, true);
            const float creatorH = 76f;
            bool creator = !string.IsNullOrEmpty(st.CreatorTitle);
            float lineH = creator ? tierH - creatorH - UiTokens.S3 : tierH;
            int line = Card(s, at, cx, cy, lw, lineH);
            UiAt inLine = at.In(line);
            Line(s, inLine, cx + UiTokens.S4, cy + UiTokens.S4, lw - UiTokens.S8, UiStyle.SectionHeader, UiRole.Text2, st.LineTitle, UiFlags.Label | UiFlags.Truncate);
            float sy = cy + UiTokens.S4 + L(UiStyle.SectionHeader) + UiTokens.S3;
            if (Drawn(st.Line))
            {
                UiDraw.Spark(s, inLine, cx + UiTokens.S4, sy, lw - UiTokens.S8, cy + lineH - UiTokens.S4 - sy, st.Line, UiRole.Chart2);
            }
            if (creator)
            {
                float oy = cy + lineH + UiTokens.S3;
                int income = Card(s, at, cx, oy, lw, creatorH);
                UiAt inCreator = at.In(income);
                Line(s, inCreator, cx + UiTokens.S4, oy + UiTokens.S3, lw - UiTokens.S8, UiStyle.SectionHeader, UiRole.Text2, st.CreatorTitle, UiFlags.Label | UiFlags.Truncate);
                float figW = UiDraw.Snap(UiTextRules.Width(st.Creator, UiStyle.Number) + UiTokens.S3);
                Line(s, inCreator, cx + UiTokens.S4, oy + UiTokens.S3 + L(UiStyle.SectionHeader), figW, UiStyle.Number, UiRole.Text, st.Creator, UiFlags.Truncate);
                Line(s, inCreator, cx + UiTokens.S4 + figW, oy + UiTokens.S3 + L(UiStyle.SectionHeader) + (L(UiStyle.Number) - L(UiStyle.Small)), lw - UiTokens.S8 - figW, UiStyle.Small,
                    UiRole.Text2, st.CreatorLine, UiFlags.Truncate);
            }
            cy += tierH + UiTokens.S3;
            List(s, at, m.List, cx, cy, inner + UiTokens.S3, bottom - cy, keys);   // its rows end where the tiles and the card end
        }

        /// <summary>A figure on its own card: its label in small capitals, the figure, and what changed in its tone.</summary>
        private static void StatCard(UiScene s, UiAt at, float x, float y, float w, float h, string label, string figure, string delta, int tone)
        {
            int tile = Card(s, at, x, y, w, h);
            UiAt inTile = at.In(tile);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            Line(s, inTile, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, label, UiFlags.Label | UiFlags.Truncate);
            cy += L(UiStyle.SectionHeader) + UiTokens.S2;
            Line(s, inTile, ix, cy, iw, UiStyle.Number, UiRole.Text, figure, UiFlags.Truncate);
            cy += L(UiStyle.Number) + UiTokens.S1;
            if (!string.IsNullOrEmpty(delta))
            {
                Line(s, inTile, ix, cy, iw, UiStyle.Small, tone > 0 ? UiRole.OkText : tone < 0 ? UiRole.DangerText : UiRole.Text2, delta, UiFlags.Truncate);
            }
        }

        // Sponsorship: the tier, how far to the next, and what each tier unlocks, ticked when it is open. Measured with draw false (its height as its words take it),
        // laid with it true into the height given.
        private static float TierCard(UiScene s, UiAt at, FlareStudioModel st, float x, float y, float w, float h, bool draw)
        {
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            UiAt inCard = at;
            int track = -1;
            if (draw)
            {
                int card = Card(s, at, x, y, w, h);
                inCard = at.In(card);
                Line(s, inCard, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, st.TierTitle, UiFlags.Label | UiFlags.Truncate);
            }
            cy += L(UiStyle.SectionHeader) + UiTokens.S2;
            if (draw)
            {
                Line(s, inCard, ix, cy, iw, UiStyle.Number, UiRole.Text, st.Tier, UiFlags.Truncate);
            }
            cy += L(UiStyle.Number) + UiTokens.S3;
            if (draw)
            {
                track = UiDraw.Box(s, inCard, ix, cy, iw, UiTokens.S2, UiRole.Track, UiRole.None, UiTokens.S1);
                float share = Math.Max(0f, Math.Min(1f, st.TierShare));
                if (share > 0f)
                {
                    UiDraw.Box(s, inCard.In(track), ix, cy, Math.Max(UiTokens.S2, iw * share), UiTokens.S2, UiRole.Chart2, UiRole.None, UiTokens.S1, UiFlags.Share);
                }
            }
            cy += UiTokens.S2 + UiTokens.S2;
            if (!string.IsNullOrEmpty(st.TierLine))
            {
                if (draw)
                {
                    UiDraw.Text(s, inCard, ix, cy, iw, UiStyle.Small, UiRole.Text2, st.TierLine, UiFlags.Wrap | UiFlags.Truncate, 2);
                }
                cy += UiDraw.Snap(UiTextRules.WrappedHeight(st.TierLine, UiStyle.Small, iw, 2)) + UiTokens.S3;
            }
            float rowH = UiTokens.S8;
            for (int i = 0; i < st.Perks.Length; i++)
            {
                if (draw && cy + rowH > y + h - UiTokens.S4)
                {
                    break;
                }
                if (draw)
                {
                    string perk = st.Perks[i];
                    bool open = perk != null && perk.Length > 0 && perk[0] == '+';
                    string words = open ? perk.Substring(1) : perk != null && perk.Length > 0 && perk[0] == '-' ? perk.Substring(1) : perk;
                    int row = UiDraw.Box(s, inCard, ix, cy, iw, rowH, UiRole.None, UiRole.None, 0f);
                    UiAt inRow = inCard.In(row);
                    UiDraw.Icon(s, inRow, ix, cy + (rowH - UiTokens.Icon) / 2f, UiTokens.Icon, open ? UiIcon.Check : UiIcon.Lock, open ? UiRole.OkText : UiRole.Text2, UiFlags.Middle, open);
                    UiDraw.TextMiddle(s, inRow, ix + UiTokens.Icon + UiTokens.S2, cy, rowH, iw - UiTokens.Icon - UiTokens.S2, UiStyle.Body, open ? UiRole.Text : UiRole.Text2, words, UiFlags.Truncate);
                }
                cy += rowH;
            }
            return UiDraw.Snap(cy + UiTokens.S4 - y);
        }

        // ------------------------------------------------------------------------------------------------ In the city

        public const float CityBandHeight = 176f, CityBandNew = 112f;
        private const int EmptyLines = 5;

        private static void CityPage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            FlareCityModel c = m.City;
            float cy = y + PageHead(s, at, x, y, w, m.Head, m.HeadLine, false);
            UiDraw.Box(s, at, x, cy - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float cx = x + UiTokens.S4, inner = w - UiTokens.S8;
            cy += UiTokens.S3;
            if (c.Shown)
            {
                // A new account has no followers to count: its band is as short as its words, and the week's figures stand in a row beside them. An account the city has not
                // placed a follower of draws no donut and says nothing of one: the week's figures run across.
                bool followers = c.Names.Length > 0;
                bool donut = followers || !string.IsNullOrEmpty(c.DonutEmpty), week = c.WeekLabels.Length > 0;   // a new account has no week of zeros: its sentence stands alone
                float gap = UiTokens.S4, lw = donut ? (week ? UiDraw.SnapDown((inner - gap) * (followers ? 0.6f : 0.5f)) : inner) : 0f, rw = donut ? (week ? inner - lw - gap : 0f) : inner;
                int columns = followers ? 2 : 3;
                float band = followers ? CityBandHeight : CityBandNew;
                if (week && !followers && FigureLabelLines(c.WeekLabels, UiDraw.SnapDown((rw - UiTokens.S8 - UiTokens.S3 * (columns - 1)) / columns)) > 1)
                {
                    band += L(UiStyle.Small);   // a label of a longer language takes a second line
                }
                if (donut && !followers)
                {
                    // what the page says of its empty donut takes the lines a language needs (up to five)
                    float say = UiTokens.S4 + L(UiStyle.SectionHeader) + UiTokens.S3 + UiDraw.Snap(UiTextRules.WrappedHeight(c.DonutEmpty, UiStyle.Body, lw - UiTokens.S8, EmptyLines)) + UiTokens.S4;
                    band = Math.Max(band, UiDraw.Snap(say));
                }
                if (donut)
                {
                    DonutCard(s, at, c, cx, cy, lw, band);
                }
                if (week)
                {
                    FiguresCard(s, at, c.WeekTitle, c.WeekFigures, c.WeekLabels, donut ? cx + lw + gap : cx, cy, rw, band, columns);
                }
                cy += band + UiTokens.S3;
            }
            List(s, at, m.List, cx, cy, inner + UiTokens.S3, y + h - cy, keys);   // with nothing in it the list says so once, in its own words
        }

        // The followers by district: the donut, and beside it each district named with its followers; or why there is nothing to show.
        private static void DonutCard(UiScene s, UiAt at, FlareCityModel c, float x, float y, float w, float h)
        {
            int card = Card(s, at, x, y, w, h);
            UiAt inCard = at.In(card);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            Line(s, inCard, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, c.DonutTitle, UiFlags.Label | UiFlags.Truncate);
            cy += L(UiStyle.SectionHeader) + UiTokens.S3;
            int n = Math.Min(c.Names.Length, Math.Min(c.Values.Length, c.Shares.Length));
            if (n == 0)
            {
                Flow(s, inCard, ix, cy, iw, UiStyle.Body, UiRole.Text2, c.DonutEmpty, EmptyLines);
                return;
            }
            float size = h - (cy - y) - UiTokens.S4;
            UiDraw.Donut(s, inCard, ix, cy, size, c.Shares, n, c.Rest);
            float lx = ix + size + UiTokens.S6;
            float used = UiDraw.Legend(s, inCard, lx, cy, x + w - UiTokens.S4 - lx, c.Names, c.Values, n);
            if (c.Rest > 0f && !string.IsNullOrEmpty(c.DonutRest) && cy + used + L(UiStyle.Small) <= y + h)
            {
                Line(s, inCard, lx, cy + used + UiTokens.S1, x + w - UiTokens.S4 - lx, UiStyle.Small, UiRole.Text2, c.DonutRest, UiFlags.Truncate);
            }
        }
    }
}
