using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The Economy app's pages: the overview, the prices, the jobs, the markets, the finances and the insights. Game-free.</summary>
    public static partial class NewsPages
    {
        private const float AdviceTile = UiTokens.IconTile;   // px: a piece of advice's glyph on its tile
        private const float MoveMark = UiTokens.IconTile;     // px: the initials of who moved a price

        // The overview: the verdict as a band; the chart with its ranges, then the prices and the districts, in the left column;
        // what it means for you, in a column of its own at the right. The cycle's phases and the rate rises travel with the chart.
        private static void Overview(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float band = LeadBand(s, at, m, x, y, w);
            y += band + UiTokens.S3;
            Dress(m);
            float gap = UiTokens.S3;
            float rw = UiDraw.SnapDown(Math.Max(320f, Math.Min(440f, w * 0.29f)));
            float lw = UiDraw.SnapDown(w - rw - gap);
            float room = bottom - y;
            AdviceCard(s, at, m, x + lw + gap, y, rw, room, keys);
            bool tables = m.Table != null && m.Table2 != null && room > UiTokens.S16 * 5f;
            float chartH = tables ? UiDraw.SnapDown(Math.Max(UiTokens.S16 * 3f, room * 0.5f)) : room;
            if (m.Chart != null)
            {
                UiChart.Lay(s, at, m.Chart, x, y, lw, chartH);
                Ranges(s, at, m, x, y, lw, keys);
            }
            if (!tables)
            {
                return;
            }
            float ty = y + chartH + gap;
            float half = UiDraw.SnapDown((lw - gap) * 0.5f);
            TableCard(s, at, m.Table, m.TableTitle, x, ty, half, bottom - ty, keys, UiDataTable.First);
            TableCard(s, at, m.Table2, m.Table2Title, x + half + gap, ty, lw - half - gap, bottom - ty, keys, NewsCommands.District);
        }

        // A table in a card with its label over it.
        private static void TableCard(UiScene s, UiAt at, UiDataTableModel table, string title, float x, float y, float w, float h, IUiKeys keys, int command)
        {
            UiAt on = Card(s, at, x, y, w, h, title, null, out float cy);
            if (table != null && y + h - cy > UiTokens.S8)
            {
                UiDataTable.Lay(s, on, table, x + UiTokens.S2, cy, w - UiTokens.S4, y + h - UiTokens.S2 - cy, keys, command);
            }
        }

        // The ranges of the chart, 30 days, 120 days and a year, as chips in the chart's head after its name.
        private static void Ranges(UiScene s, UiAt at, NewsModel m, float x, float y, float w, IUiKeys keys)
        {
            if (m.Actions == null || m.Actions.Length == 0)
            {
                return;
            }
            float title = UiDraw.Snap(UiTextRules.Width(m.Chart.Title ?? string.Empty, UiStyle.SectionHeader) * 1.16f);
            float cx = x + UiTokens.S4 + title + UiTokens.S4;
            for (int i = 0; i < m.Actions.Length && i < 3; i++)
            {
                float bw = UiDraw.ButtonWidth(m.Actions[i].Kind, m.Actions[i].Label, UiIcon.None, UiAction.Count, keys);
                UiDraw.Button(s, at, cx, y + UiTokens.S3, bw, UiTokens.PairRow, m.Actions[i].Kind, m.Actions[i].Label, UiIcon.None, UiAction.Count, keys,
                    UiAppWindow.ActionCommand, i, false);
                cx += bw + UiTokens.S1;
            }
        }

        // The cycle's phases and the rate rises travel with the chart, as quiet tints and marks.
        private static void Dress(NewsModel m)
        {
            if (m.Chart == null)
            {
                return;
            }
            if (m.Bands != null && m.Bands.Length > 0)
            {
                var plot = new UiPlotBand[m.Bands.Length];
                float from = 0f;
                for (int i = 0; i < m.Bands.Length; i++)
                {
                    float share = m.Bands[i].Share < 0f ? 0f : m.Bands[i].Share;
                    plot[i] = new UiPlotBand { From = from, To = from + share, Tint = m.Bands[i].Tint, Label = m.Bands[i].Label };
                    from += share;
                }
                m.Chart.PlotBands = plot;
            }
            if (m.Marks != null && m.Marks.Length > 0)
            {
                m.Chart.Marks = m.Marks;
            }
        }

        // What it means for you: a card of rows, each a glyph on its tile, its title, a line, and its link kept apart from the row after it.
        private static void AdviceCard(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            UiAt on = Card(s, at, x, y, w, h, m.AdviceTitle, null, out float cy);
            AdviceRows(s, on, m, x + UiTokens.S4, cy, w - UiTokens.S4 * 2f, y + h - UiTokens.S3);
        }

        private static void AdviceRows(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float limit)
        {
            if (m.Advice == null)
            {
                return;
            }
            float tx = x + AdviceTile + UiTokens.S3, tw = w - AdviceTile - UiTokens.S3;
            for (int i = 0; i < m.Advice.Length; i++)
            {
                NewsAdvice a = m.Advice[i];
                float titleH = UiDraw.Snap(UiTextRules.WrappedHeight(a.Title, UiStyle.BodyStrong, tw, 2));
                float bodyH = UiDraw.Snap(UiTextRules.WrappedHeight(a.Body, UiStyle.Small, tw, 3));
                bool link = !string.IsNullOrEmpty(a.Link);
                float rowH = titleH + UiTokens.S1 + bodyH + (link ? UiTokens.S1 + UiTokens.S6 : 0f);
                if (i > 0 && y + rowH > limit)
                {
                    break;
                }
                int tile = UiDraw.Tile(s, at, x, y, AdviceTile, UiRole.None, false);
                UiDraw.Icon(s, at.In(tile), x + (AdviceTile - UiTokens.Icon) / 2f, y + (AdviceTile - UiTokens.Icon) / 2f, UiTokens.Icon, a.Icon, UiRole.None,
                    UiFlags.Middle | UiFlags.Centre, true);
                UiDraw.Text(s, at, tx, y, tw, UiStyle.BodyStrong, UiRole.Text, a.Title, UiFlags.Wrap | UiFlags.Truncate, 2);
                float by = y + titleH + UiTokens.S1;
                UiDraw.Text(s, at, tx, by, tw, UiStyle.Small, UiRole.Text2, a.Body, UiFlags.Wrap | UiFlags.Truncate, 3);
                if (link)
                {
                    UiDraw.LinkRow(s, at, tx, by + bodyH + UiTokens.S1, tw, UiTokens.S6, a.Link, NewsCommands.Link, a.Target, false);
                }
                y += Math.Max(rowH, AdviceTile);
                if (i + 1 < m.Advice.Length)
                {
                    UiDraw.Box(s, at, x, y + UiTokens.S2, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                    y += UiTokens.S4 + UiTokens.S1;
                }
            }
        }

        // Prices: the basket as a band, the goods of the shops with the week, the month, the line and your own price; at the right
        // the index by kind and who moved prices this week. A block with nothing in it is left out, never drawn empty.
        private static void Prices(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float band = LeadBand(s, at, m, x, y, w);
            y += band + UiTokens.S3;
            float gap = UiTokens.S3;
            bool side = m.SideChart != null || (m.Moves != null && m.Moves.Length > 0);
            float rw = side ? UiDraw.SnapDown(Math.Max(360f, Math.Min(520f, w * 0.31f))) : 0f;
            float lw = side ? UiDraw.SnapDown(w - rw - gap) : w;
            float room = bottom - y;
            if (m.Table != null)
            {
                TableCard(s, at, m.Table, m.TableTitle, x, y, lw, room, keys, UiDataTable.First);
            }
            else
            {
                Notice(s, at, m, x, y, lw, room);
            }
            if (!side)
            {
                return;
            }
            bool moves = m.Moves != null && m.Moves.Length > 0;
            float chartH = moves && m.SideChart != null ? UiDraw.SnapDown(room * 0.44f) : room;
            float rx = x + lw + gap;
            if (m.SideChart != null)
            {
                UiChart.Lay(s, at, m.SideChart, rx, y, rw, chartH);
            }
            if (moves)
            {
                float my = m.SideChart != null ? y + chartH + gap : y;
                MovesCard(s, at, m, rx, my, rw, bottom - my, keys);
            }
        }

        // Named moves: the initials of who, what they did, and when, one to a row, as many as the card has room for.
        private static void MovesCard(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            UiAt on = Card(s, at, x, y, w, h, m.MovesTitle, null, out float cy);
            float ix = x + UiTokens.S4;
            float tx = ix + MoveMark + UiTokens.S3, tw = w - UiTokens.S4 * 2f - MoveMark - UiTokens.S3;
            float row = MoveMark + UiTokens.S3;
            float foot = string.IsNullOrEmpty(m.MovesLink) ? 0f : UiTokens.S6 + UiTokens.S2;
            for (int i = 0; i < m.Moves.Length; i++)
            {
                if (cy + MoveMark > y + h - UiTokens.S3 - foot)
                {
                    break;
                }
                int disc = UiDraw.Circle(s, on, ix, cy, MoveMark, MoveMark, UiRole.Tile, UiFlags.None);
                UiDraw.TextMiddle(s, on.In(disc), ix, cy, MoveMark, MoveMark, UiStyle.SmallStrong, UiRole.Text, m.Moves[i].Mark, UiFlags.Centre);
                float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong));
                float detailH = string.IsNullOrEmpty(m.Moves[i].Detail) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
                float ty = cy + UiDraw.SnapDown((MoveMark - nameH - detailH) / 2f);
                UiDraw.Text(s, on, tx, ty, tw, UiStyle.BodyStrong, UiRole.Text, m.Moves[i].Head, UiFlags.Truncate);
                if (detailH > 0f)
                {
                    UiDraw.Text(s, on, tx, ty + nameH, tw, UiStyle.Small, UiRole.Text2, m.Moves[i].Detail, UiFlags.Truncate);
                }
                cy += row;
            }
            if (foot > 0f)
            {
                UiDraw.LinkRow(s, on, x + UiTokens.S4, y + h - UiTokens.S3 - UiTokens.S6, w - UiTokens.S4 * 2f, UiTokens.S6, m.MovesLink, NewsCommands.Link, m.MovesTarget, false);
            }
        }

        // Jobs: the labour market as a band and a line chart with a short table under it.
        private static void Jobs(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float band = LeadBand(s, at, m, x, y, w);
            y += band + UiTokens.S3;
            float room = bottom - y;
            float chartH = m.Table != null ? UiDraw.SnapDown(Math.Max(UiTokens.S16 * 3f, room * 0.62f)) : room;
            if (m.Chart != null)
            {
                UiChart.Lay(s, at, m.Chart, x, y, w, chartH);
            }
            if (m.Table != null && room - chartH > UiTokens.S16)
            {
                TableCard(s, at, m.Table, m.TableTitle, x, y + chartH + UiTokens.S3, w, room - chartH - UiTokens.S3, keys, UiDataTable.First);
            }
        }

        private static void Finances(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float band = LeadBand(s, at, m, x, y, w);
            y += band + UiTokens.S3;
            if (m.Chart != null && bottom - y > UiTokens.S16 * 2f)
            {
                UiChart.Lay(s, at, m.Chart, x, y, w, bottom - y);
            }
        }

        // Insights: the findings as rows, each with its glyph, its sentence and the link to where you act.
        private static void Insights(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float band = LeadBand(s, at, m, x, y, w);
            y += band + UiTokens.S3;
            if (m.Advice == null || m.Advice.Length == 0)
            {
                Notice(s, at, m, x, y, w, bottom - y);
                return;
            }
            AdviceCard(s, at, m, x, y, w, bottom - y, keys);
        }

        // Markets: the city index large over its chart, the sectors' five sessions at the right, the risers, the fallers and the most
        // traded under them, and what you hold. A block with nothing in it is left out.
        private static void Markets(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float gap = UiTokens.S3;
            bool bars = m.Bars != null && m.Bars.Length > 0;
            float rw = bars ? UiDraw.SnapDown(Math.Max(320f, Math.Min(440f, w * 0.29f))) : 0f;
            float lw = bars ? UiDraw.SnapDown(w - rw - gap) : w;
            bool holds = m.Moves != null && m.Moves.Length > 0;
            bool boards = m.Boards != null && m.Boards.Length > 0;
            float holdH = holds ? UiTokens.S16 + UiTokens.S8 : 0f;
            float room = bottom - y;
            float topH = boards ? UiDraw.SnapDown(Math.Max(UiTokens.S16 * 3f, (room - holdH - gap) * 0.55f)) : room - (holds ? holdH + gap : 0f);
            float cy = y;
            if (!string.IsNullOrEmpty(m.BigValue))
            {
                cy = BigIndex(s, at, m, x, y, lw);
            }
            if (m.Chart != null)
            {
                UiChart.Lay(s, at, m.Chart, x, cy, lw, y + topH - cy);
            }
            if (bars)
            {
                UiAt on = Card(s, at, x + lw + gap, y, rw, topH, m.BarsTitle, m.BarsNote, out float by);
                BarRows(s, on, m.Bars, x + lw + gap + UiTokens.S4, by, rw - UiTokens.S4 * 2f, y + topH - UiTokens.S2 - by);
            }
            float ny = y + topH + gap;
            if (boards)
            {
                float bh = bottom - ny - (holds ? holdH + gap : 0f);
                int n = m.Boards.Length;
                float cw = UiDraw.SnapDown((w - gap * (n - 1)) / n);
                for (int i = 0; i < n; i++)
                {
                    float bx = x + i * (cw + gap);
                    TableCard(s, at, m.Boards[i], i < m.BoardTitles.Length ? m.BoardTitles[i] : null, bx, ny, i == n - 1 ? x + w - bx : cw, bh, keys,
                        UiDataTable.First);
                }
                ny += bh + gap;
            }
            if (holds)
            {
                HoldingsCard(s, at, m, x, ny, w, bottom - ny, keys);
            }
        }

        // The index's level large, with its changes on the day and over five sessions beside it.
        private static float BigIndex(UiScene s, UiAt at, NewsModel m, float x, float y, float w)
        {
            float h = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + UiTokens.S2;
            float vw = UiDraw.Snap(UiTextRules.Width(m.BigValue, UiStyle.Display) + UiTokens.S3);
            UiDraw.Text(s, at, x + UiTokens.S4, y + UiTokens.S3, vw, UiStyle.Display, UiRole.Text, m.BigValue, UiFlags.Truncate);
            float cx = x + UiTokens.S4 + vw + UiTokens.S3;
            for (int i = 0; m.BigChanges != null && i < m.BigChanges.Length; i++)
            {
                bool good = m.BigGood != null && i < m.BigGood.Length && m.BigGood[i];
                float cw = UiDraw.Snap(UiTextRules.Width(m.BigChanges[i], UiStyle.SmallStrong) + UiTokens.S1);
                UiDraw.Icon(s, at, cx, y + UiTokens.S3 + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, good ? UiIcon.TrendUp : UiIcon.TrendDown, good ? UiRole.OkText : UiRole.DangerText,
                    UiFlags.Middle);
                UiDraw.TextMiddle(s, at, cx + UiTokens.IconSmall + UiTokens.S1, y + UiTokens.S3, h, cw, UiStyle.SmallStrong, good ? UiRole.OkText : UiRole.DangerText, m.BigChanges[i],
                    UiFlags.Truncate);
                cx += UiTokens.IconSmall + UiTokens.S1 + cw + UiTokens.S3;
            }
            return y + h + UiTokens.S3 * 2f;
        }

        // What you hold in the market, beside what it is worth: a few named lines in a card, each with its initials.
        private static void HoldingsCard(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            UiAt on = Card(s, at, x, y, w, h, m.MovesTitle, null, out float cy);
            int n = Math.Min(3, m.Moves.Length);
            float slot = UiDraw.SnapDown((w - UiTokens.S4 * 2f) / n);
            for (int i = 0; i < n; i++)
            {
                float ix = x + UiTokens.S4 + i * slot;
                float tx = ix + MoveMark + UiTokens.S3, tw = slot - MoveMark - UiTokens.S3 - UiTokens.S3;
                int disc = UiDraw.Circle(s, on, ix, cy, MoveMark, MoveMark, UiRole.Tile, UiFlags.None);
                UiDraw.TextMiddle(s, on.In(disc), ix, cy, MoveMark, MoveMark, UiStyle.SmallStrong, UiRole.Text, m.Moves[i].Mark, UiFlags.Centre);
                float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong));
                UiDraw.Text(s, on, tx, cy, tw, UiStyle.BodyStrong, UiRole.Text, m.Moves[i].Head, UiFlags.Truncate);
                UiDraw.Text(s, on, tx, cy + nameH, tw, UiStyle.Small, UiRole.Text2, m.Moves[i].Detail, UiFlags.Truncate);
            }
            if (!string.IsNullOrEmpty(m.MovesLink))
            {
                float lw = UiDraw.Snap(UiTextRules.Width(m.MovesLink, UiStyle.BodyStrong) + UiTokens.IconSmall + UiTokens.S4);
                UiDraw.LinkRow(s, on, x + w - UiTokens.S4 - lw, y + UiTokens.S3, lw, UiTokens.PairRow, m.MovesLink, NewsCommands.Link, m.MovesTarget, false);
            }
        }
    }
}
