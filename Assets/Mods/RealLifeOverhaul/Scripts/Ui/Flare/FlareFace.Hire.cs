using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's hire page: the niches and districts as chips, the table of influencers (a bar behind the engagement and the fit), and under it a card of the one
    /// chosen: who they are, where their followers live, what their last campaigns did and what hiring them for this business should bring. Game-free.
    /// </summary>
    public static partial class FlareFace
    {
        public const float HireDetailHeight = 208f;

        private static void HirePage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float bottom = y + h;
            float cy = y + PageHead(s, at, x, y, w, m.Head, m.HeadLine, false);
            FlareBusinessModel b = m.Business;
            if (b.Picker.Length > 0)
            {
                cy += Picks(s, at, x + UiTokens.S4, cy, w - UiTokens.S8, b.Picker, b.Chosen, PickCommand) + UiTokens.S2;
            }
            if (b.Picker2.Length > 0 || b.Sort.Length > 0)
            {
                // The order at the right (by followers, by fit), the niches across the rest.
                float sortW = 0f;
                for (int i = 0; i < b.Sort.Length; i++)
                {
                    sortW += PillWidth(b.Sort[i]) + UiTokens.S2;
                }
                float sx = x + w - UiTokens.S4 - sortW + UiTokens.S2;
                for (int i = 0; i < b.Sort.Length; i++)
                {
                    float pw = PillWidth(b.Sort[i]);
                    Pill(s, at, sx, cy, pw, b.Sort[i], i == b.SortChosen ? PillKind.Chosen : PillKind.Line, SortCommand, i);
                    sx += pw + UiTokens.S2;
                }
                Picks(s, at, x + UiTokens.S4, cy, w - UiTokens.S8 - sortW - UiTokens.S2, b.Picker2, b.Chosen2, NicheCommand);
                cy += UiTokens.ControlSmall + UiTokens.S4;
            }
            else
            {
                cy += UiTokens.S2;
            }
            float rowW = w - UiTokens.S8 - UiTokens.S3;
            if (m.List.Count > 0 && m.List.Rows[0].Tags != null)
            {
                // One header for the columns: what each figure is, over its column.
                string[] labels = m.List.Rows[0].Tags;
                int cols = labels.Length / 2;
                float col = HireColumn(rowW, cols), fx = x + UiTokens.S4 + HireFigureX(rowW, cols);
                for (int i = 0; i < cols; i++)
                {
                    Line(s, at, fx + i * col, cy, col - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text2, labels[i * 2], UiFlags.Label | UiFlags.Right);
                }
                cy += UiTokens.S8;
            }
            FlareHireModel d = m.Hire;
            float listH = bottom - cy;
            if (d.Shown && m.List.Count > 0)
            {
                // The table takes the rows that fit above the card, whole rows, and no more than it has: the card stands right under it.
                Measure(m.List, rowW);
                float content = 0f;
                for (int i = 0; i < m.List.Count; i++)
                {
                    content += m.List.Rows[i].Height;
                }
                float one = m.List.Rows[0].Height, room = bottom - cy - HireDetailHeight - UiTokens.S3;
                listH = Math.Min(content, Math.Max(one, (float)Math.Floor(room / one) * one));
            }
            List(s, at, m.List, x + UiTokens.S4, cy, w - UiTokens.S8, listH, keys);
            if (d.Shown && m.List.Count > 0)
            {
                HireDetail(s, at, d, x + UiTokens.S4, cy + listH + UiTokens.S3, w - UiTokens.S8 - UiTokens.S3);
            }
        }

        // The chosen influencer: the face and who, what hiring them should bring and the button; where their followers live; what their last campaigns did.
        private static void HireDetail(UiScene s, UiAt at, FlareHireModel d, float x, float y, float w)
        {
            int card = Card(s, at, x, y, w, HireDetailHeight);
            UiAt inCard = at.In(card);
            float gap = UiTokens.S6, c1 = UiDraw.SnapDown(w * 0.31f), c2 = UiDraw.SnapDown(w * 0.33f);
            float x1 = x + UiTokens.S4, w1 = c1 - UiTokens.S4 - gap / 2f;
            float x2 = x + c1 + gap / 2f, w2 = c2 - gap;
            float x3 = x + c1 + c2 + gap / 2f, w3 = x + w - UiTokens.S4 - x3;
            UiDraw.Box(s, inCard, x + c1, y + UiTokens.S4, UiTokens.Hairline, HireDetailHeight - UiTokens.S8, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            UiDraw.Box(s, inCard, x + c1 + c2, y + UiTokens.S4, UiTokens.Hairline, HireDetailHeight - UiTokens.S8, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            // The first column: the face, the name and handle, the figures of hiring them, the button.
            float cy = y + UiTokens.S4;
            Face(s, inCard, x1, cy, UiTokens.AvatarRow, d.Name, d.Hue, true, d.Ring, false);
            float tx = x1 + UiTokens.AvatarRow + UiTokens.S3, tw = x1 + w1 - tx;
            float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(d.Name, UiStyle.Number) + UiTokens.S1), tw - (d.Verified ? UiTokens.S5 : 0f));
            float ny = cy + UiDraw.SnapDown((UiTokens.AvatarRow - L(UiStyle.Number)) / 2f);   // the row above has the handle: the card says the name once, beside the face
            Line(s, inCard, tx, ny, nameW, UiStyle.Number, UiRole.Text, d.Name, UiFlags.Truncate);
            if (d.Verified)
            {
                Verified(s, inCard, tx + nameW + UiTokens.S1, ny + UiTokens.S1);
            }
            cy += UiTokens.AvatarRow + UiTokens.S3;
            int figures = Math.Min(d.ForecastLabels.Length, d.ForecastFigures.Length);
            for (int i = 0; i < figures; i++)
            {
                UiDraw.TextMiddle(s, inCard, x1, cy, UiTokens.S6, w1 * 0.62f, UiStyle.Small, UiRole.Text2, d.ForecastLabels[i], UiFlags.Truncate);
                UiDraw.TextMiddle(s, inCard, x1 + w1 * 0.62f, cy, UiTokens.S6, w1 * 0.38f, UiStyle.BodyStrong, UiRole.Text, d.ForecastFigures[i], UiFlags.Right | UiFlags.Truncate);
                cy += UiTokens.S6;
            }
            if (!string.IsNullOrEmpty(d.Hire))
            {
                Orange(s, inCard, x1, y + HireDetailHeight - UiTokens.S4 - UiTokens.ControlSmall, w1, UiTokens.ControlSmall, d.Hire, ButtonCommand, d.Row, d.HireOff);
            }
            // The second: where their followers in the city live.
            float by = y + UiTokens.S4;
            Line(s, inCard, x2, by, w2, UiStyle.SectionHeader, UiRole.Text2, d.AudienceTitle, UiFlags.Label | UiFlags.Truncate);
            by += L(UiStyle.SectionHeader) + UiTokens.S2;
            int bars = Math.Min(d.AudienceLabels.Length, Math.Min(d.AudienceShares.Length, d.AudienceFigures.Length));
            if (bars == 0)
            {
                Flow(s, inCard, x2, by, w2, UiStyle.Small, UiRole.Text2, d.AudienceEmpty, 3);
            }
            float labelW = UiDraw.SnapDown(w2 * 0.34f), figW = UiTokens.S10;
            for (int i = 0; i < bars; i++)
            {
                UiDraw.TextMiddle(s, inCard, x2, by, UiTokens.S6, labelW - UiTokens.S2, UiStyle.Small, UiRole.Text, d.AudienceLabels[i], UiFlags.Truncate);
                float bx = x2 + labelW, bw = w2 - labelW - figW - UiTokens.S2;
                int track = UiDraw.Box(s, inCard, bx, by + (UiTokens.S6 - UiTokens.S1) / 2f, bw, UiTokens.S1, UiRole.Track, UiRole.None, UiTokens.S1 / 2f, UiFlags.Middle);
                if (d.AudienceShares[i] > 0f)
                {
                    UiDraw.Box(s, inCard.In(track), bx, by + (UiTokens.S6 - UiTokens.S1) / 2f, Math.Max(UiTokens.S1, bw * Math.Min(1f, d.AudienceShares[i])), UiTokens.S1, UiRole.Chart2,
                        UiRole.None, UiTokens.S1 / 2f, UiFlags.Middle | UiFlags.Share);
                }
                UiDraw.TextMiddle(s, inCard, x2 + w2 - figW, by, UiTokens.S6, figW, UiStyle.Small, UiRole.Text2, d.AudienceFigures[i], UiFlags.Right);
                by += UiTokens.S6;
            }
            // The third: what their last campaigns did, and what a post like this tends to do.
            float ly = y + UiTokens.S4;
            Line(s, inCard, x3, ly, w3, UiStyle.SectionHeader, UiRole.Text2, d.LastTitle, UiFlags.Label | UiFlags.Truncate);
            ly += L(UiStyle.SectionHeader) + UiTokens.S2;
            int lines = Math.Min(d.LastLines.Length, Math.Min(d.LastFigures.Length, 3));
            if (lines == 0)
            {
                Flow(s, inCard, x3, ly, w3, UiStyle.Small, UiRole.Text2, d.LastEmpty, 2);
                ly += UiDraw.Snap(UiTextRules.WrappedHeight(d.LastEmpty, UiStyle.Small, w3, 2)) + UiTokens.S2;
            }
            for (int i = 0; i < lines; i++)
            {
                int tone = i < d.LastTones.Length ? d.LastTones[i] : 0;
                float fw = Math.Min(UiDraw.Snap(UiTextRules.Width(d.LastFigures[i], UiStyle.SmallStrong) + UiTokens.S1), UiDraw.SnapDown(w3 * 0.4f));
                UiDraw.TextMiddle(s, inCard, x3, ly, UiTokens.S6, w3 - fw - UiTokens.S2, UiStyle.Small, UiRole.Text, d.LastLines[i], UiFlags.Truncate);
                UiDraw.TextMiddle(s, inCard, x3 + w3 - fw, ly, UiTokens.S6, fw, UiStyle.SmallStrong, tone > 0 ? UiRole.OkText : tone < 0 ? UiRole.DangerText : UiRole.Text2, d.LastFigures[i],
                    UiFlags.Right | UiFlags.Truncate);
                ly += UiTokens.S6;
            }
            if (!string.IsNullOrEmpty(d.Note))
            {
                float room = y + HireDetailHeight - UiTokens.S4 - ly - UiTokens.S2;
                int noteLines = Math.Max(1, Math.Min(3, (int)Math.Floor(room / L(UiStyle.Small))));
                int note = Flow(s, inCard, x3, ly + UiTokens.S2, w3, UiStyle.Small, UiRole.Text2, d.Note, noteLines);
                if ((s.Items[note].Flags & UiFlags.Wrap) != 0)
                {
                    s.Items[note].H = noteLines * L(UiStyle.Small);
                }   // the room it may take, so a longer language's second line is not cut by a box measured for one
            }
        }
    }
}
