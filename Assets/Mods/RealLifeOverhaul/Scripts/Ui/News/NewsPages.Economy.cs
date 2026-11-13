using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The Economy app's pages: the overview, the prices, the jobs, the markets, the finances and the insights. Game-free.</summary>
    public static partial class NewsPages
    {
        private const float AdviceTile = UiTokens.IconTile;   // px: a piece of advice's glyph on its tile
        private const float MoveMark = UiTokens.IconTile;     // px: the initials of who moved a price

        // The overview: the verdict as a band and the facts that frame it; the headline tiles, the chart with its ranges, then the prices and the districts, in
        // the left column; what it means for you, in a column of its own at the right. The cycle's phases and the rate rises travel with the chart.
        private static void Overview(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float band = LeadBand(s, at, m, x, y, w);
            y += band + UiTokens.S3;
            Dress(m);
            y += FactsRow(s, at, m, x, y, w);
            float gap = UiTokens.S3;
            float rw = UiDraw.SnapDown(Math.Max(320f, Math.Min(440f, w * 0.29f)));
            float lw = UiDraw.SnapDown(w - rw - gap);
            float room = bottom - y;
            AdviceCard(s, at, m, x + lw + gap, y, rw, room, keys);
            float tileH = TilesHeight(m.Tiles, lw, gap);
            if (tileH > 0f && room > tileH + gap + UiTokens.S16 * 3f)
            {
                TilesLay(s, at, m.Tiles, x, y, lw, tileH, gap);
                y += tileH + gap;
                room -= tileH + gap;
            }
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

        // What it means for you: a card of rows, each a glyph on its tile, its title, a line, and its link kept apart from the row after it. When the room holds
        // fewer than there are, the link under them leads to the Insights tab, where all of them stand.
        private static void AdviceCard(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            UiAt on = Card(s, at, x, y, w, h, m.AdviceTitle, null, out float cy);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f, limit = y + h - UiTokens.S3;
            int total = m.Advice == null ? 0 : m.Advice.Length;
            bool link = !string.IsNullOrEmpty(m.AdviceMore) && AdviceRows(s, on, m, 0, ix, cy, iw, limit, false) < total;
            float foot = link ? UiTokens.S6 + UiTokens.S2 : 0f;
            AdviceRows(s, on, m, 0, ix, cy, iw, limit - foot, true);
            if (link)
            {
                UiDraw.LinkRow(s, on, ix, y + h - UiTokens.S3 - UiTokens.S6, iw, UiTokens.S6, m.AdviceMore, NewsCommands.Tab, 6, false);
            }
        }

        // Rows from the one at <paramref name="from"/> while they fit above <paramref name="limit"/> (the first always). Returns the index after the last drawn, or that
        // it would draw when <paramref name="draw"/> is false, so a column knows what is left for the next.
        private static int AdviceRows(UiScene s, UiAt at, NewsModel m, int from, float x, float y, float w, float limit, bool draw)
        {
            if (m.Advice == null)
            {
                return 0;
            }
            float tx = x + AdviceTile + UiTokens.S3, tw = w - AdviceTile - UiTokens.S3;
            int i = from;
            for (; i < m.Advice.Length; i++)
            {
                NewsAdvice a = m.Advice[i];
                float titleH = UiDraw.Snap(UiTextRules.WrappedHeight(a.Title, UiStyle.BodyStrong, tw, 2));
                float bodyH = UiDraw.Snap(UiTextRules.WrappedHeight(a.Body, UiStyle.Small, tw, 3));
                bool link = !string.IsNullOrEmpty(a.Link);
                float rowH = titleH + UiTokens.S1 + bodyH + (link ? UiTokens.S1 + UiTokens.S6 : 0f);
                if (i > from && y + rowH > limit)
                {
                    break;
                }
                if (draw)
                {
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
                }
                y += Math.Max(rowH, AdviceTile);
                if (i + 1 < m.Advice.Length)
                {
                    if (draw)
                    {
                        UiDraw.Box(s, at, x, y + UiTokens.S2, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                    }
                    y += UiTokens.S4 + UiTokens.S1;
                }
            }
            return i;
        }

        // Prices: the basket as a band; a row of charts (the price index, inflation, the bank rate, and the goods that moved most); under them the shops' goods with
        // the week, the month, the line and your own price, and who moved prices this week; then the costs and the bank, and the wholesale prices by kind of goods.
        // A block with nothing in it is left out, never drawn empty.
        private static void Prices(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float band = LeadBand(s, at, m, x, y, w);
            y += band + UiTokens.S3;
            float gap = UiTokens.S3;
            int charts = (m.Charts == null ? 0 : m.Charts.Length) + (m.SideChart != null ? 1 : 0);
            int panels = m.Panels == null ? 0 : m.Panels.Length;
            bool moves = m.Moves != null && m.Moves.Length > 0;
            float room = bottom - y;
            if (m.Table == null && charts == 0 && panels == 0)
            {
                Notice(s, at, m, x, y, w, room);
                return;
            }
            if (charts > 0)
            {
                float chartH = UiDraw.SnapDown(Math.Max(UiTokens.S16 * 2.5f, Math.Min(UiTokens.S16 * 5f, room * 0.34f)));
                float cw = UiDraw.SnapDown((w - gap * (charts - 1)) / charts);
                int k = 0;
                for (int i = 0; m.Charts != null && i < m.Charts.Length; i++)
                {
                    UiChart.Lay(s, at, m.Charts[i], x + k * (cw + gap), y, cw, chartH, UiChart.First);
                    k++;
                }
                if (m.SideChart != null)
                {
                    float cx = x + k * (cw + gap);
                    UiChart.Lay(s, at, m.SideChart, cx, y, x + w - cx, chartH, UiChart.First);
                }
                y += chartH + gap;
                room = bottom - y;
            }
            // The goods and the moves in the first column, the costs and the bank in the second, the wholesale prices in the third; with fewer blocks the rest widen.
            int mid = Math.Min(2, panels);
            float aw = panels == 0 ? w : UiDraw.SnapDown(w * 0.38f);
            float bw = panels == 0 ? 0f : panels > 2 ? UiDraw.SnapDown(w * 0.28f) : w - aw - gap;
            float cw3 = panels > 2 ? w - aw - bw - gap * 2f : 0f;
            float movesNeed = moves ? MovesNeed(m) : 0f;
            if (m.Table != null)
            {
                float tableH = Math.Min(room - (moves ? movesNeed + gap : 0f), TableNeed(m.Table, 6));
                tableH = UiDraw.SnapDown(Math.Max(tableH, Math.Min(room, UiTokens.S16 * 3f)));
                TableCard(s, at, m.Table, m.TableTitle, x, y, aw, tableH, keys, UiDataTable.First);
                if (moves)
                {
                    float my = y + tableH + gap;
                    MovesCard(s, at, m, x, my, aw, UiDraw.SnapDown(Math.Min(movesNeed, bottom - my)), keys);
                }
            }
            else
            {
                Notice(s, at, m, x, y, aw, room);
            }
            if (mid > 0)
            {
                StackPanels(s, at, m.Panels, 0, mid, x + aw + gap, y, bw, room);
            }
            if (panels > 2)
            {
                StackPanels(s, at, m.Panels, 2, panels, x + aw + gap + bw + gap, y, cw3, room);
            }
        }

        // The height of the moves card for its rows and its link.
        private static float MovesNeed(NewsModel m)
        {
            float foot = string.IsNullOrEmpty(m.MovesLink) ? 0f : UiTokens.S6 + UiTokens.S2;
            return UiDraw.Snap(CardHead + m.Moves.Length * (MoveMark + UiTokens.S3) + UiTokens.S3 + foot);
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

        // Jobs: the labour market as a band; the unemployment and the vacancies as charts beside what it means for someone who hires, over how well off the people
        // of each district are.
        private static void Jobs(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float band = LeadBand(s, at, m, x, y, w);
            y += band + UiTokens.S3;
            float gap = UiTokens.S3;
            float room = bottom - y;
            int charts = (m.Chart != null ? 1 : 0) + (m.Charts == null ? 0 : m.Charts.Length);
            int panels = m.Panels == null ? 0 : m.Panels.Length;
            bool top = charts > 0 || panels > 0;
            float tableH = m.Table != null ? Math.Min(TableNeed(m.Table, 4), UiDraw.SnapDown(top ? room * 0.5f : room - UiTokens.S16 * 3f)) : 0f;
            float topH = m.Table != null ? room - tableH - gap : room;
            if (!top)
            {
                // The jobs are not counted in this game: the notice says so over the districts' incomes, which are.
                Notice(s, at, m, x, y, w, topH);
            }
            else
            {
                float pw = panels > 0 ? UiDraw.SnapDown(Math.Max(300f, Math.Min(440f, w * 0.28f))) : 0f;
                float areaW = w - (panels > 0 ? pw + gap : 0f);
                float cw = charts > 0 ? UiDraw.SnapDown((areaW - gap * (charts - 1)) / charts) : 0f;
                int k = 0;
                if (m.Chart != null)
                {
                    UiChart.Lay(s, at, m.Chart, x, y, cw, topH);
                    k++;
                }
                for (int i = 0; m.Charts != null && i < m.Charts.Length; i++)
                {
                    float cx = x + k * (cw + gap);
                    UiChart.Lay(s, at, m.Charts[i], cx, y, i == m.Charts.Length - 1 ? x + areaW - cx : cw, topH, UiChart.First);
                    k++;
                }
                if (panels > 0)
                {
                    StackPanels(s, at, m.Panels, 0, panels, x + areaW + gap, y, pw, topH);
                }
            }
            if (m.Table != null && tableH > UiTokens.S16)
            {
                TableCard(s, at, m.Table, m.TableTitle, x, bottom - tableH, w, tableH, keys, UiDataTable.First);
            }
        }

        // Finance: the reserve's chart, and at its right the council's week, its departments and the tax; with the council not started, only the tax and what to do.
        private static void Finances(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float band = LeadBand(s, at, m, x, y, w);
            y += band + UiTokens.S3;
            float gap = UiTokens.S3;
            float room = bottom - y;
            int panels = m.Panels == null ? 0 : m.Panels.Length;
            float pw = panels > 0 ? UiDraw.SnapDown(Math.Max(340f, Math.Min(520f, w * 0.36f))) : 0f;
            float lw = panels > 0 ? w - pw - gap : w;
            if (m.Chart != null && room > UiTokens.S16 * 2f)
            {
                UiChart.Lay(s, at, m.Chart, x, y, lw, room);
            }
            else if (!string.IsNullOrEmpty(m.EmptyTitle))
            {
                Notice(s, at, m, x, y, lw, room);
            }
            if (panels > 0)
            {
                StackPanels(s, at, m.Panels, 0, panels, x + lw + gap, y, pw, room);
            }
        }

        // Insights: the findings as rows, each with its glyph, its sentence and the link to where you act, in as many columns as the width holds. What does not fit
        // is counted, never dropped without a word.
        private static void Insights(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float band = LeadBand(s, at, m, x, y, w);
            y += band + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Note))
            {
                float nh = UiDraw.Snap(UiTextRules.WrappedHeight(m.Note, UiStyle.Body, w, 2));
                UiDraw.Text(s, at, x, y, w, UiStyle.Body, UiRole.Text2, m.Note, UiFlags.Wrap | UiFlags.Truncate, 2);
                y += nh + UiTokens.S3;
            }
            if (m.Advice == null || m.Advice.Length == 0)
            {
                Notice(s, at, m, x, y, w, bottom - y);
                return;
            }
            UiAt on = Card(s, at, x, y, w, bottom - y, m.AdviceTitle, null, out float cy);
            float gap = UiTokens.S8;
            float inner = w - UiTokens.S4 * 2f;
            int cols = Math.Max(1, Math.Min(3, (int)((inner + gap) / (420f + gap))));
            float colW = UiDraw.SnapDown((inner - gap * (cols - 1)) / cols);
            float limit = bottom - UiTokens.S3;
            float moreH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2;
            int next = 0;
            for (int c = 0; c < cols && next < m.Advice.Length; c++)
            {
                next = AdviceRows(s, on, m, next, 0f, cy, colW, limit, false);
            }
            bool more = next < m.Advice.Length && !string.IsNullOrEmpty(m.AdviceMore);
            int drawn = 0;
            for (int c = 0; c < cols && drawn < m.Advice.Length; c++)
            {
                float cl = c == cols - 1 && more ? limit - moreH : limit;
                drawn = AdviceRows(s, on, m, drawn, x + UiTokens.S4 + c * (colW + gap), cy, colW, cl, true);
            }
            if (drawn < m.Advice.Length && !string.IsNullOrEmpty(m.AdviceMore))
            {
                UiDraw.Text(s, on, x + UiTokens.S4, bottom - UiTokens.S3 - moreH + UiTokens.S1, inner, UiStyle.Small, UiRole.Muted,
                    m.AdviceMore.Replace("{n}", (m.Advice.Length - drawn).ToString()), UiFlags.Truncate);
            }
        }

        // Markets: the city index large over its chart, the sectors' five sessions at the right, the risers, the fallers and the most
        // traded under them, and what you hold. A block with nothing in it is left out.
        private static void Markets(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float gap = UiTokens.S3;
            y += FactsRow(s, at, m, x, y, w);
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
                BarRows(s, on, m.Bars, x + lw + gap + UiTokens.S4, by, rw - UiTokens.S4 * 2f, y + topH - UiTokens.S2 - by, m.BarsMore);
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
            float linkW = string.IsNullOrEmpty(m.MovesLink) ? 0f : UiDraw.Snap(UiTextRules.Width(m.MovesLink, UiStyle.BodyStrong) + UiTokens.IconSmall + UiTokens.S4);
            UiAt on = Card(s, at, x, y, w, h, m.MovesTitle, null, out float cy, linkW + UiTokens.S3);
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
                UiDraw.LinkRow(s, on, x + w - UiTokens.S4 - linkW, y + UiTokens.S3, linkW, UiTokens.PairRow, m.MovesLink, NewsCommands.Link, m.MovesTarget, false);
            }
        }
    }
}
