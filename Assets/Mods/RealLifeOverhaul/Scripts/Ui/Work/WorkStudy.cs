using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The campus pages whose shape is their own: the term's diary (a month with the classes, the homework and the exam on their days, and the
    /// courses under way above it), the transcript (a statement of grades by term with the average by quarter, what is coming up and the class), the
    /// skills as bars with where each is taught, and a profile for each child. Every part is the kit's own. Game-free.
    /// </summary>
    public static class WorkStudy
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------------------------------------- Studying

        /// <summary>The courses under way as cards across the top (their next class, attendance, homework and exam, their buttons) and the term's diary under them.</summary>
        public static void Diary(UiScene s, UiAt at, WorkTab t, UiActionModel[] actions, int[] cardActs, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float cy = y;
            int n = t.Rows.Count;
            if (n > 0)
            {
                float gap = UiTokens.S3, cw = UiDraw.SnapDown((w - gap * (Math.Min(n, 4) - 1)) / Math.Min(n, 4)), ch = CardHeight;
                for (int i = 0; i < n && i < 4; i++)
                {
                    CourseCard(s, at, t.Rows[i], actions, cardActs, i, x + i * (cw + gap), cy, cw, ch, keys);
                }
                cy += ch + UiTokens.S4;
            }
            if (t.Diary != null)
            {
                UiDiary.Lay(s, at, t.Diary.Month, x, cy, w, bottom - cy, keys);
            }
        }

        public const float CardHeight = 192f;

        private static void CourseCard(UiScene s, UiAt at, WorkRow r, UiActionModel[] actions, int[] cardActs, int index, float x, float y, float w, float h, IUiKeys keys)
        {
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float pad = UiTokens.S4, iw = w - pad * 2f, tile = UiTokens.IconTile;
            int t = UiDraw.Tile(s, at, x + pad, y + pad, tile, UiRole.None, false);
            UiDraw.Icon(s, at.In(t), x + pad + (tile - UiTokens.Icon) / 2f, y + pad + (tile - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon, r.Tint, UiFlags.Middle | UiFlags.Centre, true);
            float tx = x + pad + tile + UiTokens.S3, tw = iw - tile - UiTokens.S3;
            float chip = 0f;
            if (!string.IsNullOrEmpty(r.Status))
            {
                chip = Math.Min(tw * 0.5f, UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(r.Status, UiStyle.SmallStrong) + UiTokens.S1));
                UiDraw.Chip(s, at, x + w - pad - chip, y + pad, r.Status, r.Tone, chip);
            }
            UiDraw.Text(s, at, tx, y + pad, tw - chip - UiTokens.S2, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
            UiDraw.Text(s, at, tx, y + pad + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, r.Sub, UiFlags.Truncate);
            float ly = y + pad + tile + UiTokens.S2;
            for (int i = 0; i < r.Figures.Count && i < 3; i++)
            {
                UiDraw.Text(s, at, x + pad, ly, iw * 0.5f, UiStyle.Small, UiRole.Text2, r.Figures[i].Key, UiFlags.Truncate);
                UiDraw.Text(s, at, x + w - pad - iw * 0.5f, ly, iw * 0.5f, UiStyle.SmallStrong, UiRole.Text, r.Figures[i].Value, UiFlags.Right | UiFlags.Truncate);
                ly += UiTextRules.LineHeight(UiStyle.Small) + UiTokens.S1;
            }
            // the card's own buttons stand at its foot, drawn from the page's actions
            float by = y + h - pad - UiTokens.ControlSmall, right = x + w - pad;
            for (int k = 0; k < r.Acts.Count; k++)
            {
                int at2 = cardActs[index * 4 + k];
                if (at2 < 0 || at2 >= actions.Length)
                {
                    continue;
                }
                UiActionModel a = actions[at2];
                float bw = Math.Min(iw * 0.5f, UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, UiAction.Count, keys));
                right -= bw;
                UiDraw.Button(s, at, right, by, bw, UiTokens.ControlSmall, a.Kind, a.Label, a.Icon, UiAction.Count, keys, a.Command, a.Arg, a.Disabled);
                right -= UiTokens.S2;
            }
        }

        // ------------------------------------------------------------------------------------------------- Results

        public static void Statement(UiScene s, UiAt at, WorkStatement m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Head == null)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.ClipboardText, m.EmptyTitle, m.EmptyBody, null, -1, keys);
                return;
            }
            y = Head(s, at, m, x, y, w);
            float lw = UiDraw.SnapDown(w * 0.64f), gap = UiTokens.S8, rw = w - lw - gap, rx = x + lw + gap;
            float ly = Transcript(s, at, m, x, y, lw, bottom);
            if (m.Gains.Count > 0 && bottom - ly > UiTokens.S12 + UiTokens.PairRow * 2)
            {
                ly += UiTokens.S3;
                ly += WorkPanel.SectionHead(s, at, x, ly, lw, m.GainTitle, null);
                float colW = UiDraw.SnapDown((lw - UiTokens.S8) / 2f);
                for (int i = 0; i < m.Gains.Count; i++)
                {
                    float gx = x + (i % 2) * (colW + UiTokens.S8), gy = ly + (i / 2) * (UiTokens.PairRow + UiTokens.S1);
                    if (gy + UiTokens.PairRow > bottom)
                    {
                        break;
                    }
                    Bar(s, at, m.Gains[i], gx, gy, colW);
                }
            }
            float ry = y;
            if (m.Chart != null)
            {
                float ch = Math.Min(UiDraw.SnapDown((bottom - y) * 0.34f), UiTokens.S16 * 3.5f);
                UiChart.Lay(s, at, m.Chart, rx, ry, rw, ch);
                ry += ch + UiTokens.S3;
            }
            if (m.Coming.Count > 0)
            {
                ry += WorkPanel.SectionHead(s, at, rx, ry, rw, m.ComingTitle, null);
                for (int i = 0; i < m.Coming.Count && ry + UiTokens.PairRow < bottom; i++)
                {
                    UiDraw.Icon(s, at, rx, ry + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Calendar, UiRole.None, UiFlags.Middle, true);
                    float vw = UiTextRules.Width(m.Coming[i].Value, UiStyle.Body) + UiTokens.S1;
                    UiDraw.TextMiddle(s, at, rx + UiTokens.Icon + UiTokens.S2, ry, UiTokens.PairRow, rw - UiTokens.Icon - UiTokens.S2 - vw - UiTokens.S2, UiStyle.Body, UiRole.Text,
                        m.Coming[i].Key, UiFlags.Truncate);
                    UiDraw.TextMiddle(s, at, rx + rw - vw, ry, UiTokens.PairRow, vw, UiStyle.Body, UiRole.Text2, m.Coming[i].Value, UiFlags.Right);
                    ry += UiTokens.PairRow + UiTokens.S1;
                }
                ry += UiTokens.S3;
            }
            if (m.Class.Count > 0 && bottom - ry > UiTokens.PairRow + 40f)
            {
                ry += WorkPanel.SectionHead(s, at, rx, ry, rw, m.ClassTitle, m.ClassNote);
                for (int i = 0; i < m.Class.Count && ry + 40f <= bottom; i++)
                {
                    ry += ClassLine(s, at, m.Class[i], i + 1, rx, ry, rw);
                }
            }
        }

        private static float Head(UiScene s, UiAt at, WorkStatement m, float x, float y, float w)
        {
            UiProfileModel p = m.Head;
            float face = UiTokens.AvatarHero - UiTokens.S4 * 2f;
            UiDraw.Portrait(s, at, x, y, face, p.Name, p.Hue, p.Ring);
            float tx = x + face + UiTokens.S4;
            UiDraw.Text(s, at, tx, y + UiTokens.S2, w * 0.5f, UiStyle.Hero, UiRole.Text, p.Name, UiFlags.Truncate);
            UiDraw.Text(s, at, tx, y + UiTokens.S2 + UiTextRules.LineHeight(UiStyle.Hero) + UiTokens.S2, w * 0.5f, UiStyle.Body, UiRole.Text2, p.Role, UiFlags.Truncate);
            float right = x + w;
            for (int i = m.FigureLabels.Length - 1; i >= 0; i--)
            {
                bool credits = i == 1 && m.CreditShare >= 0f;
                float vw = UiTextRules.Width(m.FigureValues[i], UiStyle.Number) + UiTokens.S1;
                float cw = Math.Max(vw, UiTextRules.Width(m.FigureLabels[i], UiStyle.SectionHeader) + UiTokens.S1) + (credits ? UiTokens.S16 + UiTokens.S2 : 0f);
                right -= cw;
                UiDraw.Text(s, at, right, y + UiTokens.S2, cw, UiStyle.SectionHeader, UiRole.Text2, m.FigureLabels[i], UiFlags.Label | UiFlags.Truncate);
                UiDraw.Text(s, at, right, y + UiTokens.S2 + UiTextRules.LineHeight(UiStyle.SectionHeader) + UiTokens.S2, vw, UiStyle.Number, UiRole.Text, m.FigureValues[i], UiFlags.Truncate);
                if (credits)
                {
                    float by = y + UiTokens.S2 + UiTextRules.LineHeight(UiStyle.SectionHeader) + UiTokens.S2 + (UiTextRules.LineHeight(UiStyle.Number) - UiTokens.S1) / 2f;
                    UiDraw.Meter(s, at, right + vw + UiTokens.S2, by, UiTokens.S16, UiTokens.S1, (int)Math.Round(m.CreditShare * 100f), UiRole.Accent);
                }
                right -= UiTokens.S8;
            }
            return y + face + UiTokens.S4;
        }

        private const float MarkRow = 48f, TermRow = 32f, HeadRow = 32f;

        private static float Transcript(UiScene s, UiAt at, WorkStatement m, float x, float y, float w, float bottom)
        {
            float credits = 72f, attend = 104f, exam = 72f, grade = 120f, state = 132f, gap = UiTokens.S4;
            float courseW = w - credits - attend - exam - grade - state - gap * 5f - UiTokens.S4;
            float cx = x + UiTokens.S2;
            float c1 = cx + courseW + gap, c2 = c1 + credits + gap, c3 = c2 + attend + gap, c4 = c3 + exam + gap, c5 = c4 + grade + gap;
            y += WorkPanel.SectionHead(s, at, x, y, w, m.TranscriptTitle, null);
            string[] heads = { m.Heads.Length > 0 ? m.Heads[0] : string.Empty, m.Heads.Length > 1 ? m.Heads[1] : string.Empty, m.Heads.Length > 2 ? m.Heads[2] : string.Empty,
                m.Heads.Length > 3 ? m.Heads[3] : string.Empty, m.Heads.Length > 4 ? m.Heads[4] : string.Empty, m.Heads.Length > 5 ? m.Heads[5] : string.Empty };
            float[] xs = { cx, c1, c2, c3, c4, c5 }, ws = { courseW, credits, attend, exam, grade, state };
            for (int i = 0; i < 6; i++)
            {
                UiDraw.TextMiddle(s, at, xs[i], y, HeadRow, ws[i], UiStyle.SectionHeader, UiRole.Muted, heads[i], UiFlags.Label | UiFlags.Truncate | (i == 0 || i == 5 ? UiFlags.None : UiFlags.Right));
            }
            y += HeadRow;
            UiDraw.Box(s, at, x, y, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            y += UiTokens.S1;
            float room = bottom - y - (m.Gains.Count > 0 ? UiTokens.S12 + UiTokens.PairRow * 2f : 0f) - (m.TotalCredits != null ? MarkRow : 0f);
            // the latest terms that fit: a long transcript drops its oldest terms, so what is now is what is read
            int first = m.Terms.Count;
            float used = 0f;
            while (first > 0)
            {
                float need = TermRow + m.Terms[first - 1].Marks.Count * MarkRow;
                if (used + need > room && first < m.Terms.Count)
                {
                    break;
                }
                used += need;
                first--;
            }
            for (int ti = first; ti < m.Terms.Count; ti++)
            {
                WorkTerm term = m.Terms[ti];
                UiDraw.Box(s, at, x, y, w, TermRow, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                float nw = UiTextRules.Width(term.Note ?? string.Empty, UiStyle.Small) + UiTokens.S1;
                UiDraw.TextMiddle(s, at, cx + UiTokens.S2, y, TermRow, w - nw - UiTokens.S8, UiStyle.SectionHeader, UiRole.Text2, term.Title, UiFlags.Label | UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, x + w - nw - UiTokens.S2, y, TermRow, nw, UiStyle.Small, UiRole.Text2, term.Note, UiFlags.Right | UiFlags.Truncate);
                y += TermRow;
                for (int i = 0; i < term.Marks.Count; i++)
                {
                    Mark(s, at, term.Marks[i], xs, ws, x, y, w);
                    y += MarkRow;
                }
            }
            if (m.TotalCredits != null)
            {
                UiDraw.Box(s, at, x, y, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                UiDraw.TextMiddle(s, at, cx, y + UiTokens.S1, MarkRow - UiTokens.S2, courseW, UiStyle.BodyStrong, UiRole.Text, m.TotalLabel, UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, c1 - UiTokens.S8 * 3f, y + UiTokens.S1, MarkRow - UiTokens.S2, credits + UiTokens.S8 * 3f, UiStyle.BodyStrong, UiRole.Text, m.TotalCredits, UiFlags.Right);
                UiDraw.TextMiddle(s, at, c4, y + UiTokens.S1, MarkRow - UiTokens.S2, grade, UiStyle.BodyStrong, UiRole.Text, m.TotalGpa, UiFlags.Right);
                y += MarkRow;
            }
            return y;
        }

        private static void Mark(UiScene s, UiAt at, WorkMark k, float[] xs, float[] ws, float x, float y, float w)
        {
            float tile = UiTokens.ControlSmall;
            int t = UiDraw.Tile(s, at, xs[0], y + (MarkRow - tile) / 2f, tile, UiRole.None, false);
            UiDraw.Icon(s, at.In(t), xs[0] + (tile - UiTokens.Icon) / 2f, y + (MarkRow - UiTokens.Icon) / 2f, UiTokens.Icon, k.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float nx = xs[0] + tile + UiTokens.S3, nw = ws[0] - tile - UiTokens.S3;
            float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small), top = y + (MarkRow - lh - sh) / 2f;
            UiDraw.Text(s, at, nx, top, nw, UiStyle.Title, UiRole.Text, k.Course, UiFlags.Truncate | UiFlags.Middle);
            UiDraw.Text(s, at, nx, top + lh, nw, UiStyle.Small, UiRole.Text2, k.Place, UiFlags.Truncate | UiFlags.Middle);
            UiDraw.TextMiddle(s, at, xs[1], y, MarkRow, ws[1], UiStyle.Body, UiRole.Text, k.Credits, UiFlags.Right);
            UiDraw.TextMiddle(s, at, xs[2], y, MarkRow, ws[2], UiStyle.Body, k.AttendanceGrade >= 0 ? UiTokens.Grade(k.AttendanceGrade) : UiRole.Muted, k.Attendance, UiFlags.Right);
            UiDraw.TextMiddle(s, at, xs[3], y, MarkRow, ws[3], UiStyle.Body, k.ExamGrade >= 0 ? UiTokens.Grade(k.ExamGrade) : UiRole.Muted, k.Exam, UiFlags.Right);
            if (!string.IsNullOrEmpty(k.Letter))
            {
                float pw = UiTextRules.Width(k.Percent ?? string.Empty, UiStyle.Small) + UiTokens.S1;
                UiRole ink = k.LetterGrade >= 0 ? UiTokens.Grade(k.LetterGrade) : UiRole.Muted;
                UiDraw.TextMiddle(s, at, xs[4] + ws[4] - pw, y, MarkRow, pw, UiStyle.Small, UiRole.Text2, k.Percent, UiFlags.Right);
                UiDraw.TextMiddle(s, at, xs[4], y, MarkRow, ws[4] - pw - UiTokens.S1, UiStyle.BodyStrong, ink, k.Letter, UiFlags.Right);
            }
            else
            {
                UiDraw.TextMiddle(s, at, xs[4], y, MarkRow, ws[4], UiStyle.Body, UiRole.Muted, k.Percent, UiFlags.Right);
            }
            if (!string.IsNullOrEmpty(k.Status))
            {
                float chip = Math.Min(ws[5], UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(k.Status, UiStyle.SmallStrong) + UiTokens.S1));
                UiDraw.Chip(s, at, xs[5] + ws[5] - chip, y + (MarkRow - UiTokens.Badge - UiTokens.S1) / 2f, k.Status, k.Tone, chip);
            }
        }

        private static float ClassLine(UiScene s, UiAt at, WorkPerson p, int place, float x, float y, float w)
        {
            float h = 40f, face = UiTokens.AvatarSmall;
            bool you = p.Ring == UiRole.Accent;
            if (you)
            {
                UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S2 * 2f, h, UiRole.RowSel, UiRole.None, UiTokens.RadiusLarge, UiFlags.Selected);
            }
            UiDraw.TextMiddle(s, at, x, y, h, UiTokens.S6, UiStyle.Small, UiRole.Text2, place.ToString(Inv), UiFlags.Right);
            UiDraw.Portrait(s, at, x + UiTokens.S6 + UiTokens.S2, y + (h - face) / 2f, face, p.Name, UiColour.HueOf(p.Name), UiRole.None);
            float vw = UiTextRules.Width(p.Right ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1;
            UiDraw.TextMiddle(s, at, x + UiTokens.S6 + UiTokens.S2 + face + UiTokens.S3, y, h,
                w - UiTokens.S6 - UiTokens.S2 - face - UiTokens.S3 - vw - UiTokens.S2, UiStyle.Body, UiRole.Text, p.Name, UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, x + w - vw, y, h, vw, UiStyle.BodyStrong, UiRole.Text, p.Right, UiFlags.Right);
            return h + UiTokens.S1;
        }

        // ------------------------------------------------------------------------------------------------- Skills

        public static void Skills(UiScene s, UiAt at, WorkTab t, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (t.Skills.Count == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.Star, t.EmptyTitle, t.EmptyBody, null, -1, keys);
                return;
            }
            y += WorkPanel.SectionHead(s, at, x, y, w, t.Title, t.Lead) + UiTokens.S1;
            float colW = UiDraw.SnapDown((w - UiTokens.S8) / 2f), rowH = 56f;
            int perColumn = Math.Max(1, (int)((bottom - y) / rowH));
            if (t.Skills.Count <= perColumn * 2)
            {
                perColumn = Math.Max(1, (t.Skills.Count + 1) / 2);   // two balanced columns, not one full and one empty
            }
            for (int i = 0; i < t.Skills.Count && i < perColumn * 2; i++)
            {
                int col = i / perColumn, row = i % perColumn;
                float rx = x + col * (colW + UiTokens.S8), ry = y + row * rowH;
                WorkBar b = t.Skills[i];
                UiDraw.Box(s, at, rx, ry, colW, rowH - UiTokens.S1, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                float lw = UiDraw.SnapDown(colW * 0.42f), pad = UiTokens.S4;
                float face = UiTokens.AvatarRow, fx = rx + pad;
                int tile = UiDraw.Tile(s, at, fx, ry + (rowH - UiTokens.S1 - face) / 2f, face, UiRole.None, false);
                UiDraw.Icon(s, at.In(tile), fx + (face - UiTokens.Icon) / 2f, ry + (rowH - UiTokens.S1 - UiTokens.Icon) / 2f, UiTokens.Icon,
                    i < t.SkillIcons.Count ? t.SkillIcons[i] : UiIcon.Star, UiRole.None,
                    UiFlags.Middle | UiFlags.Centre, true);
                float lw0 = lw, tx = rx + face + UiTokens.S2;
                lw -= face + UiTokens.S2;
                UiDraw.Text(s, at, tx + pad, ry + UiTokens.S2, lw - pad, UiStyle.Title, UiRole.Text, b.Label, UiFlags.Truncate);
                string place = i < t.SkillPlaces.Count ? t.SkillPlaces[i] : null;
                UiDraw.Text(s, at, tx + pad, ry + UiTokens.S2 + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1, lw - pad, UiStyle.Small, UiRole.Text2, place, UiFlags.Truncate);
                float vw = UiTextRules.Width("100", UiStyle.BodyStrong) + UiTokens.S1;
                float bw = colW - lw0 - vw - pad - UiTokens.S3;
                UiDraw.Meter(s, at, rx + lw0, ry + (rowH - UiTokens.Hairline - UiTokens.S1) / 2f, bw, UiTokens.S1, b.Value, UiRole.Accent);
                UiDraw.TextMiddle(s, at, rx + colW - pad - vw, ry, rowH - UiTokens.Hairline, vw, UiStyle.BodyStrong, UiTokens.Grade(b.Value), b.Value.ToString(Inv), UiFlags.Right);
            }
        }

        private static void Bar(UiScene s, UiAt at, WorkBar b, float x, float y, float w)
        {
            float label = Math.Min(UiDraw.SnapDown(w * 0.34f), 150f), value = UiTextRules.Width("100", UiStyle.BodyStrong) + UiTextRules.Width("+00", UiStyle.Small) + UiTokens.S3;
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, label, UiStyle.Body, UiRole.Text, b.Label, UiFlags.Truncate);
            UiDraw.Meter(s, at, x + label, y + (UiTokens.PairRow - UiTokens.S1) / 2f, w - label - value - UiTokens.S2, UiTokens.S1, b.Value, UiTokens.Grade(b.Value));
            UiDraw.TextMiddle(s, at, x + w - value, y, UiTokens.PairRow, UiTextRules.Width("100", UiStyle.BodyStrong), UiStyle.BodyStrong,
                UiTokens.Grade(b.Value), b.Value.ToString(Inv), UiFlags.Right);
            if (!string.IsNullOrEmpty(b.Note))
            {
                float nw = UiTextRules.Width("+00", UiStyle.Small) + UiTokens.S1;
                UiDraw.TextMiddle(s, at, x + w - nw, y, UiTokens.PairRow, nw, UiStyle.Small, UiRole.OkText, b.Note, UiFlags.Right);
            }
        }

        // ------------------------------------------------------------------------------------------------- Family

        public static float Kids(UiScene s, UiAt at, WorkTab t, UiActionModel[] actions, int[] kidActs, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float colW = t.Kids.Count > 1 ? UiDraw.SnapDown((w - UiTokens.S6) / 2f) : w;
            float end = y;
            for (int i = 0; i < t.Kids.Count; i++)
            {
                WorkKid k = t.Kids[i];
                float kx = x + (i % 2) * (colW + UiTokens.S6), ky = y + (i / 2) * 260f;
                var profile = new UiProfileModel { Name = k.Name, Hue = UiColour.HueOf(k.Name), Ring = k.Ring, Role = k.Role, RoleIcon = UiIcon.Cap,
                    FactLabels = k.FactLabels, FactValues = k.FactValues };
                var buttons = new List<UiActionModel>(2);
                for (int a = 0; a < k.Acts.Count; a++)
                {
                    int at2 = kidActs[i * 4 + a];
                    if (at2 >= 0 && at2 < actions.Length)
                    {
                        buttons.Add(actions[at2]);
                    }
                }
                float yy = UiAppWindow.Profile(s, at, profile, buttons.ToArray(), kx, ky, colW, keys);
                if (k.Fund >= 0)
                {
                    yy += WorkPanel.SectionHead(s, at, kx, yy, colW, k.FundLabel, k.FundNote);
                    UiDraw.Meter(s, at, kx, yy + UiTokens.S1, colW, UiTokens.S2, k.Fund, UiRole.Accent);
                    yy += UiTokens.S4;
                }
                end = Math.Max(end, yy);
            }
            return end;
        }
    }
}
