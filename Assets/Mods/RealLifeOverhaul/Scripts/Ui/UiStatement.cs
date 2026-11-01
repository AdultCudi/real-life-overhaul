using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A dated line of a statement: when, its kind's glyph, what it was, money in or out, and the balance after it.</summary>
    public struct UiStatementLine
    {
        public string When;          // the time or the date as the game writes it
        public UiIcon Icon;
        public string What;          // "Wages, Bloom Café"
        public string Where;         // the place or the account it came from or went to, or null
        public string In;            // money in as the game writes it, or null
        public string Out;           // money out, or null
        public string Balance;       // the running balance after it
    }

    /// <summary>A group of a statement's lines: a day, or a heading of an income statement, with its total and whether it is folded.</summary>
    public struct UiStatementGroup
    {
        public string Label;         // "Tuesday, Day 412", "Wages"
        public string Total;         // the group's sum as the game writes it
        public bool TotalGood;       // the sum is money in (green) or out (red)
        public bool Folded;          // only its band shows
        public UiStatementLine[] Lines;
    }

    /// <summary>
    /// Money over time as a statement (EconoView, Finance, a bank, the bills, a share account): the account and its
    /// balance as the one large figure, the period, dated lines grouped by day under bands that fold, and the period's
    /// totals at the foot.
    /// </summary>
    public sealed class UiStatementModel
    {
        public string Account;       // "Everyday account"
        public UiIcon AccountIcon = UiIcon.Bank;
        public string Number;        // a quiet second line ("Midtown Savings Bank, ending 4417"), or null
        public string BalanceLabel;  // "Balance"
        public string Balance;
        public string PeriodLabel;   // the period's choice field's value ("This week")
        public string[] Columns = new string[0];   // the heads: when, what, in, out, balance
        public UiStatementGroup[] Groups = new UiStatementGroup[0];
        public string[] TotalLabels = new string[0];   // the foot: money in, money out, the change
        public string[] Totals = new string[0];
        public bool ChangeGood;      // the period's change is a gain
        public float Offset;
        public int Reveal = -1;
        public string EmptyTitle;
        public string EmptyBody;
    }

    /// <summary>
    /// A statement, a block of an app window's page for money over time (EconoView, Finance, a bank, the bills, a share
    /// account), shaped as a bank's statement rather than a dashboard: a head on its band with the account behind its
    /// tile, its balance as the one large figure and the period as a choice; the column heads; dated lines grouped by day
    /// (or by heading, as the game's income statement folds them) under bands that fold with a press, each line its
    /// time, its kind's glyph on a tile, what it was and where, money in, money out and the balance after it; and the
    /// period's totals at the foot. The bands and the lines are rows of one list, so up and down walk the whole statement
    /// and scroll it, and only the rows in view are laid out. Commands from <see cref="First"/>: <see cref="Period"/>,
    /// <see cref="Group"/> (the group's index) and <see cref="Line"/> (the row's index in the list). Game-free. Cost: a
    /// layout a change, the rows in view only.
    /// </summary>
    public static class UiStatement
    {
        public const int First = 140;
        public const int Period = 0, Group = 1, Line = 2;
        private const float BandRow = 40f;     // px: a group's band, as tall as a line, so the list shows whole rows
        private const float LineRow = 40f;     // px: a dated line with its kind's tile
        private const float Tile = 28f;        // px: a line's kind on its tile
        private const float When = 96f;        // px: the time or date column
        private const float Money = 128f;      // px: each of in, out and the balance
        private const float PeriodField = 200f;   // px

        /// <summary>The head's height: the account and the balance on their band.</summary>
        public static float HeadHeight => UiDraw.Snap(UiTokens.S4 * 2f + UiTextRules.LineHeight(UiStyle.SectionHeader) + UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Display));

        /// <summary>How many rows the statement's list holds: a band a group, and the lines of every group not folded.</summary>
        public static int RowCount(UiStatementModel m)
        {
            int n = 0;
            for (int g = 0; g < m.Groups.Length; g++)
            {
                n += 1 + (m.Groups[g].Folded || m.Groups[g].Lines == null ? 0 : m.Groups[g].Lines.Length);
            }
            return n;
        }

        /// <summary>The group a row of the list stands in, and its line there (-1 for the group's band).</summary>
        public static int GroupOf(UiStatementModel m, int row, out int line)
        {
            int at = 0;
            for (int g = 0; g < m.Groups.Length; g++)
            {
                int lines = m.Groups[g].Folded || m.Groups[g].Lines == null ? 0 : m.Groups[g].Lines.Length;
                if (row < at + 1 + lines)
                {
                    line = row - at - 1;
                    return g;
                }
                at += 1 + lines;
            }
            line = -1;
            return -1;
        }

        /// <summary>Lays the statement out in a box. Returns its rows' list, or -1 when no money has moved.</summary>
        public static short Lay(UiScene s, UiAt at, UiStatementModel m, float x, float y, float w, float h, IUiKeys keys, int command = First)
        {
            float hh = HeadHeight;
            Head(s, at, m, x, y, w, hh, command);
            float ty = y + hh + UiTokens.S4;
            // One right edge for the figures and the bands' sums, a step clear of the scroll thumb.
            float[] cx = Columns(x + UiTokens.S2, w - UiTokens.S2 - UiTokens.S4, out float[] cw);
            int rows = RowCount(m);
            float headH = UiTokens.ControlSmall;
            for (int c = 0; c < m.Columns.Length && c < cx.Length; c++)
            {
                bool right = c >= 2;
                UiDraw.TextMiddle(s, at, cx[c], ty, headH, cw[c], UiStyle.SectionHeader, UiRole.Muted, m.Columns[c], UiFlags.Label | UiFlags.Truncate | (right ? UiFlags.Right : UiFlags.None));
            }
            ty += headH;
            UiDraw.Box(s, at, x, ty, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            ty += UiTokens.S1;   // the rule in a grid step of its own
            float footH = m.Totals.Length > 0 ? UiTokens.Control + UiTokens.S1 : 0f;
            float viewH = rows > 0 ? Math.Max(LineRow, (float)Math.Floor((y + h - footH - ty) / LineRow) * LineRow) : UiDraw.SnapDown(y + h - footH - ty);
            short list = -1;
            if (rows == 0)
            {
                // The heads and the foot stay, so a new account reads as the same statement with nothing in it yet.
                UiDraw.EmptyState(s, at, x, ty, w, viewH, m.AccountIcon, m.EmptyTitle, m.EmptyBody, null, -1, keys);
                m.Offset = 0f;
            }
            else
            {
                list = Rows(s, at, m, x, ty, w, viewH, rows, cx, cw, command);
            }
            if (footH > 0f)
            {
                Foot(s, at, m, x, ty + viewH, w);
            }
            return list;
        }

        // The head on its band: the account's tile, its name and its number; the balance at the right with its label; the
        // period's choice at the far right.
        private static void Head(UiScene s, UiAt at, UiStatementModel m, float x, float y, float w, float h, int command)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            float tile = UiTokens.IconTile;
            int t = UiDraw.Tile(s, on, x + UiTokens.S4, y + (h - tile) / 2f, tile, UiRole.None, false);
            s.Items[t].Flags |= UiFlags.Middle;
            UiDraw.Icon(s, on.In(t), x + UiTokens.S4 + UiTokens.S2, y + (h - tile) / 2f + UiTokens.S2, tile - UiTokens.S4, m.AccountIcon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float right = x + w - UiTokens.S4;
            if (!string.IsNullOrEmpty(m.PeriodLabel))
            {
                right -= PeriodField;
                UiDraw.Choice(s, on, right, y + (h - UiTokens.Control) / 2f, PeriodField, m.PeriodLabel, command + Period, 0, false);
                s.Items[s.Nodes[s.NodeCount - 1].Face].Flags |= UiFlags.Middle;
                right -= UiTokens.S8;
            }
            float bw = UiDraw.Snap(Math.Max(UiTextRules.Width(m.Balance, UiStyle.Display), UiTextRules.Width(m.BalanceLabel, UiStyle.SectionHeader)) + UiTokens.S1);
            float by = y + UiTokens.S4;
            UiDraw.Text(s, on, right - bw, by, bw, UiStyle.SectionHeader, UiRole.Text2, m.BalanceLabel, UiFlags.Label | UiFlags.Right);
            UiDraw.Text(s, on, right - bw, by + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1, bw, UiStyle.Display, UiRole.Text, m.Balance, UiFlags.Right);
            float nx = x + UiTokens.S4 + tile + UiTokens.S4, nw = right - bw - UiTokens.S6 - nx;
            float block = UiTextRules.LineHeight(UiStyle.Title) + (string.IsNullOrEmpty(m.Number) ? 0f : UiTextRules.LineHeight(UiStyle.Small));
            float ny = y + (h - block) / 2f;
            UiDraw.Text(s, on, nx, ny, nw, UiStyle.Title, UiRole.Text, m.Account, UiFlags.Truncate | UiFlags.Middle);
            if (!string.IsNullOrEmpty(m.Number))
            {
                UiDraw.Text(s, on, nx, ny + UiTextRules.LineHeight(UiStyle.Title), nw, UiStyle.Small, UiRole.Text2, m.Number, UiFlags.Truncate | UiFlags.Middle);
            }
        }

        // The columns: when, what (the room left), in, out and the balance, a gutter between each.
        private static float[] Columns(float x, float w, out float[] widths)
        {
            widths = new[] { When, 0f, Money, Money, Money };
            widths[1] = w - When - Money * 3f - UiTokens.S4 * 4f;
            var lefts = new float[5];
            float cx = x;
            for (int c = 0; c < 5; c++)
            {
                lefts[c] = cx;
                cx += widths[c] + UiTokens.S4;
            }
            return lefts;
        }

        private static float RowH(UiStatementModel m, int row)
        {
            GroupOf(m, row, out int line);
            return line < 0 ? BandRow : LineRow;
        }

        // The bands and lines in view, in a region that scrolls inside its frame, each a row of one list.
        private static short Rows(UiScene s, UiAt at, UiStatementModel m, float x, float y, float w, float viewH, int rows, float[] cx, float[] cw, int command)
        {
            var tops = new float[rows + 1];
            for (int r = 0; r < rows; r++)
            {
                tops[r + 1] = tops[r] + RowH(m, r);
            }
            float content = tops[rows];
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < rows)
            {
                offset = Math.Min(offset, tops[m.Reveal]);
                offset = Math.Max(offset, tops[m.Reveal + 1] - viewH);
            }
            offset = Math.Max(0f, Math.Min((float)Math.Round(offset / LineRow) * LineRow, content - viewH));   // by whole rows, never a sliver of one
            m.Offset = offset;
            m.Reveal = -1;
            short clip = s.AddClip(x, y, w, viewH, offset);
            s.Clips[clip].ContentHeight = content;
            int first = 0;
            while (first < rows - 1 && tops[first + 1] <= offset + 0.5f)
            {
                first++;
            }
            int last = first;
            while (last < rows - 1 && tops[last + 1] < offset + viewH - 0.5f)
            {
                last++;
            }
            short list = s.AddList(rows, first, last - first + 1, clip, -1);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            for (int r = first; r <= last; r++)
            {
                int g = GroupOf(m, r, out int line);
                UiAt arriving = inClip.Arriving(r - first);
                if (line < 0)
                {
                    BandAt(s, arriving, m, g, tops[r], w, list, r, command);
                }
                else
                {
                    LineAt(s, arriving, ref m.Groups[g].Lines[line], tops[r], w, cx, cw, x, list, r, command);
                }
            }
            UiDraw.ScrollThumb(s, at, x + w - UiTokens.S1, y, viewH, offset, content);
            return list;
        }

        // A group's band: its chevron (down while open), its name (a date reads as a heading, not a label) and its sum at the right in good or bad's colour.
        private static void BandAt(UiScene s, UiAt at, UiStatementModel m, int g, float y, float w, short list, int row, int command)
        {
            ref UiStatementGroup grp = ref m.Groups[g];
            int face = UiDraw.Box(s, at, 0f, y, w, BandRow, UiRole.Band, UiRole.None, UiTokens.RadiusMedium);
            UiAt inside = at.In(face);
            UiDraw.Icon(s, inside, UiTokens.S2, y + (BandRow - UiTokens.Icon) / 2f, UiTokens.Icon, grp.Folded ? UiIcon.Next : UiIcon.Down, UiRole.AccentText, UiFlags.Middle);
            float tw = string.IsNullOrEmpty(grp.Total) ? 0f : UiDraw.Snap(UiTextRules.Width(grp.Total, UiStyle.BodyStrong) + UiTokens.S1);
            float lx = UiTokens.S2 + UiTokens.Icon + UiTokens.S2;
            UiDraw.TextMiddle(s, inside, lx, y, BandRow, w - lx - tw - UiTokens.S6, UiStyle.Title, UiRole.Text, grp.Label, UiFlags.Truncate);
            if (tw > 0f)
            {
                UiDraw.TextMiddle(s, inside, w - UiTokens.S4 - tw, y, BandRow, tw, UiStyle.BodyStrong, grp.TotalGood ? UiRole.OkText : UiRole.Text, grp.Total, UiFlags.Right);
            }
            UiDraw.Node(s, at, 0f, y, w, BandRow, command + Group, g, face, false, list, row);
        }

        // A dated line: its time, its kind on a tile, what and where, money in, money out and the balance after it.
        private static void LineAt(UiScene s, UiAt at, ref UiStatementLine l, float y, float w, float[] cx, float[] cw, float x0, short list, int row, int command)
        {
            int face = UiDraw.Box(s, at, 0f, y, w, LineRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            UiDraw.TextMiddle(s, inside, cx[0] - x0, y, LineRow, cw[0], UiStyle.Body, UiRole.Text2, l.When, UiFlags.Truncate);
            float tx = cx[1] - x0;
            int tile = UiDraw.Box(s, inside, tx, y + (LineRow - Tile) / 2f, Tile, Tile, UiRole.Tile, UiRole.None, UiTokens.RadiusMedium, UiFlags.Middle);
            UiDraw.Icon(s, inside.In(tile), tx + UiTokens.S1, y + (LineRow - Tile) / 2f + UiTokens.S1, Tile - UiTokens.S2, l.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float wx = tx + Tile + UiTokens.S3, ww = cw[1] - Tile - UiTokens.S3;
            float whatW = string.IsNullOrEmpty(l.Where) ? ww : Math.Min(UiDraw.Snap(UiTextRules.Width(l.What, UiStyle.Title) + UiTokens.S1), UiDraw.SnapDown(ww * 0.6f));
            UiDraw.TextMiddle(s, inside, wx, y, LineRow, whatW, UiStyle.Title, UiRole.Text, l.What, UiFlags.Truncate);
            if (!string.IsNullOrEmpty(l.Where))
            {
                UiDraw.TextMiddle(s, inside, wx + whatW + UiTokens.S3, y, LineRow, ww - whatW - UiTokens.S3, UiStyle.Body, UiRole.Text2, l.Where, UiFlags.Truncate);
            }
            UiDraw.TextMiddle(s, inside, cx[2] - x0, y, LineRow, cw[2], UiStyle.BodyStrong, UiRole.OkText, l.In, UiFlags.Right | UiFlags.Truncate);
            UiDraw.TextMiddle(s, inside, cx[3] - x0, y, LineRow, cw[3], UiStyle.Body, UiRole.Text, l.Out, UiFlags.Right | UiFlags.Truncate);
            UiDraw.TextMiddle(s, inside, cx[4] - x0, y, LineRow, cw[4], UiStyle.Body, UiRole.Text2, l.Balance, UiFlags.Right | UiFlags.Truncate);
            UiDraw.Node(s, at, 0f, y, w, LineRow, command + Line, row, face, false, list, row);
        }

        // The period's totals at the foot under a rule, each label over nothing but beside its figure, from the right.
        private static void Foot(UiScene s, UiAt at, UiStatementModel m, float x, float y, float w)
        {
            UiDraw.Box(s, at, x, y, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float fy = y + UiTokens.S1, h = UiTokens.Control, right = x + w - UiTokens.S4;   // on the figures' right edge
            int n = Math.Min(m.TotalLabels.Length, m.Totals.Length);
            for (int i = n - 1; i >= 0; i--)
            {
                bool change = i == n - 1;
                float vw = UiDraw.Snap(UiTextRules.Width(m.Totals[i], UiStyle.BodyStrong) + UiTokens.S1);
                right -= vw;
                UiRole ink = change && UiModels.Sign(m.Totals[i]) != 0 ? (m.ChangeGood ? UiRole.OkText : UiRole.DangerText) : UiRole.Text;   // no change says nothing good or bad
                UiDraw.TextMiddle(s, at, right, fy, h, vw, UiStyle.BodyStrong, ink, m.Totals[i], UiFlags.Right);
                float lw = UiDraw.Snap(UiTextRules.Width(m.TotalLabels[i], UiStyle.SectionHeader) + UiTokens.S1);
                right -= UiTokens.S2 + lw;
                UiDraw.TextMiddle(s, at, right, fy, h, lw, UiStyle.SectionHeader, UiRole.Text2, m.TotalLabels[i], UiFlags.Label);
                right -= UiTokens.S8;
            }
        }
    }
}
