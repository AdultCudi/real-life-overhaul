using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Stocks desk: a window, the Markets, Yours and Other markets groups down the side (the sidebar has no group
    /// labels yet, so they are drawn here), and the page the player came for. Game-free. Cost: a layout a change.
    /// </summary>
    public static class StocksLay
    {
        public const int Less = 33, More = 34, Place = 35, Alert = 37, Empty = 38, Shares = 39,
            Act = 50, PriceLess = 51, PriceMore = 52, PricePlain = 53, Watch = 54, Second = 200;
        public const int View = 70, OrderRow = 71;   // the Portfolio's views (Held, Pays, Account) and a row of the Market's open orders; 70 to 79 are the desk's own
        private static float Entry => UiTokens.Control + UiTokens.S2;   // the sidebar's row, on the grid

        public static void Lay(UiScene s, StocksModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (m.Page == StocksPage.Ticket && m.Decision != null)
            {
                UiDecision.Lay(s, m.Decision, frame, dark, keys);
                return;
            }
            s.Begin("stocks", frame.Width, frame.Height, dark);
            frame.Icon = UiIcon.ChartLineUp;
            int win = UiDraw.Window(s, frame, m.Title, keys);
            UiAt at = UiAt.Screen.In(win);
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            float top = wy + UiFrame.Strip;
            bool big = m.Co != null && m.Co.Big && m.Page == StocksPage.Company;   // the chart across the window: the sidebar stands aside
            int shown = big ? -1 : Nav(s, at, m, wx, top, wh - UiFrame.Strip);
            if (shown >= 0)
            {
                s.FocusFirst = shown;
            }
            int page = s.NodeCount;
            float x = wx + (big ? 0f : UiAppWindow.Sidebar) + UiTokens.S5;
            float w = wx + ww - UiTokens.S5 - x;
            float y = top + UiTokens.S4;
            float bottom = wy + wh - UiTokens.S4;
            if (m.Page != StocksPage.Company)
            {
                y = Header(s, at, m, x, y, w, keys);
            }
            if ((m.Page == StocksPage.Portfolio || m.Page == StocksPage.Market || m.Page == StocksPage.Index) && m.ViewLabels.Length > 1)
            {
                y = Views(s, at, m, x, y, w);
            }
            if (!string.IsNullOrEmpty(m.EmptyTitle) && m.Table == null && m.Statement == null && m.Co == null && m.Page != StocksPage.Index)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, m.EmptyIcon, m.EmptyTitle, m.EmptyBody, null, Empty, keys);
            }
            else
            {
                switch (m.Page)
                {
                    case StocksPage.Company: y = Company(s, at, m, x, y, w, bottom, keys); break;
                    case StocksPage.Portfolio: y = Portfolio(s, at, m, x, y, w, bottom, keys); break;
                    case StocksPage.Index: y = IndexPage(s, at, m, x, y, w, bottom, keys); break;
                    case StocksPage.Orders:
                    case StocksPage.History: NoteTable(s, at, m, x, y, w, bottom, keys); break;
                    case StocksPage.Listing: y = Listing(s, at, m, x, y, w, bottom, keys); break;
                    case StocksPage.Bots:
                    case StocksPage.Bonds:
                    case StocksPage.Futures: y = Stacked(s, at, m, x, y, w, bottom, keys); break;
                    default: y = Market(s, at, m, x, y, w, bottom, keys); break;
                }
            }
            UiPageFence.Lay(s, page, x, x + w);
            if (m.Sheet != null)
            {
                StocksSheetLay.Lay(s, m.Sheet, frame, keys);
            }
            else
            {
                Prompts(s, m);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // The market's state at the left and the page's own buttons at the right, one row; nothing when it has neither.
        private static float Header(UiScene s, UiAt at, StocksModel m, float x, float y, float w, IUiKeys keys)
        {
            bool status = !string.IsNullOrEmpty(m.Status);
            if (!status && (m.Actions == null || m.Actions.Length == 0))
            {
                return y;
            }
            float bx = x + w;
            for (int i = m.Actions.Length - 1; i >= 0; i--)
            {
                UiActionModel a = m.Actions[i];
                float bw = Math.Min(UiDraw.SnapDown(w / 3f), UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys));
                bx -= bw;
                UiDraw.Button(s, at, bx, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, Act, i, a.Disabled);
                bx -= UiTokens.S3;
            }
            if (status)
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, Math.Max(UiTokens.S16, bx - x), UiStyle.Body, UiRole.Text2, m.Status, UiFlags.Truncate);
            }
            return y + UiTokens.Control + UiTokens.S3;
        }

        // A page of two tables, what is on offer over what is held, each under its capitals. The upper takes the room left
        // after the lower's head and a row or two; both scroll on their own.
        private static float Stacked(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Figures != null && m.Figures.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, m.Figures, x, y, w);
            }
            float label = UiTokens.PairRow + UiTokens.S1;
            UiDataTableModel a = m.Table, b = m.Table2;
            if (a == null)
            {
                return bottom;
            }
            float h1 = bottom - y;
            float h2 = 0f;
            if (b != null)
            {
                int rows = b.RowCount == 0 ? 4 : Math.Min(b.RowCount, 3);
                h2 = label + UiDataTable.Chrome(b) + rows * UiDataTable.RowHeight(b) + UiTokens.S2;
                float least = label + UiDataTable.Chrome(a) + 2f * UiDataTable.RowHeight(a);
                h1 = Math.Max(least, bottom - y - h2 - UiTokens.S3);
                h2 = bottom - y - h1 - UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(m.TableTitle))
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.SectionHeader, UiRole.Text2, m.TableTitle, UiFlags.Label | UiFlags.Truncate);
            }
            short list = UiDataTable.Lay(s, at, a, x, y + label, w, h1 - label, keys);
            m.ListOne = list;
            m.ClipOne = list >= 0 ? s.Lists[list].Clip : -1;
            if (b != null && h2 > label + UiTokens.S16)
            {
                float y2 = y + h1 + UiTokens.S3;
                if (!string.IsNullOrEmpty(m.Table2Title))
                {
                    UiDraw.TextMiddle(s, at, x, y2, UiTokens.PairRow, w, UiStyle.SectionHeader, UiRole.Text2, m.Table2Title, UiFlags.Label | UiFlags.Truncate);
                }
                short two = UiDataTable.Lay(s, at, b, x, y2 + label, w, h2 - label, keys, Second);
                m.ListTwo = two;
                m.ClipTwo = two >= 0 ? s.Lists[two].Clip : -1;
            }
            return bottom;
        }

        private static int Nav(UiScene s, UiAt at, StocksModel m, float x, float y, float h)
        {
            UiDraw.Band(s, at, x, y, UiAppWindow.Sidebar, h);
            float ny = y + UiTokens.S2;
            float ex = x + UiTokens.S2, ew = UiAppWindow.Sidebar - UiTokens.S4;
            int shown = -1, group = 0;
            for (int i = 0; i < m.Nav.Length; i++)
            {
                if (group < m.GroupAt.Length && i == m.GroupAt[group])
                {
                    string label = group < m.Groups.Length ? m.Groups[group] : string.Empty;
                    UiDraw.TextMiddle(s, at, ex, ny, UiTokens.PairRow, ew, UiStyle.SectionHeader, UiRole.Muted, label, UiFlags.Label | UiFlags.Truncate);
                    ny += UiTokens.PairRow + UiTokens.S1;
                    group++;
                }
                bool on = i == m.NavAt;
                int face = UiDraw.Box(s, at, ex, ny, ew, Entry, on ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, on ? UiFlags.Selected : UiFlags.None);
                UiAt inside = at.In(face);
                float tile = UiTokens.ControlSmall;
                int t = UiDraw.Tile(s, inside, ex + UiTokens.S2, ny + (Entry - tile) / 2f, tile, UiRole.None, on);
                float g = UiTokens.Icon;
                UiDraw.Icon(s, inside.In(t), ex + UiTokens.S2 + (tile - g) / 2f, ny + (Entry - g) / 2f, g, m.Nav[i].Icon, on ? UiRole.OnAccent : UiRole.None,
                    UiFlags.Middle | UiFlags.Centre, true);
                float lx = ex + UiTokens.S2 + tile + UiTokens.S2;
                UiDraw.TextMiddle(s, inside, lx, ny, Entry, ex + ew - UiTokens.S2 - lx, UiStyle.Nav, UiRole.Text, m.Nav[i].Label, UiFlags.Label | UiFlags.Truncate);
                int node = UiDraw.Node(s, at, ex, ny, ew, Entry, UiAppWindow.NavCommand, i, face);
                if (on)
                {
                    shown = node;
                }
                ny += Entry + UiTokens.S1;
            }
            return shown;
        }

        private static float Market(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Figures != null && m.Figures.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, m.Figures, x, y, w);
            }
            if (!string.IsNullOrEmpty(m.RateLine))
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.Body, UiRole.Text2, m.RateLine, UiFlags.Truncate);
                y += UiTokens.PairRow + UiTokens.S2;
            }
            bool news = m.News != null && m.News.Length > 0, tape = m.Tape != null && m.Tape.Length > 0, orders = m.OrderLines != null && m.OrderLines.Length > 0;
            if (news || tape || orders)
            {
                // The news, the tape and the player's open orders side by side, one row of cards; the news is the wider, and each takes the width alone when it is the only one.
                float gap = UiTokens.S3;
                float wn = news ? 1.6f : 0f, wt = tape ? 1f : 0f, wo = orders ? 1f : 0f;
                float units = wn + wt + wo;
                float room = w - gap * ((news ? 1 : 0) + (tape ? 1 : 0) + (orders ? 1 : 0) - 1);
                int lines = Math.Max(1, Math.Max(news ? Math.Min(StocksModel.NewsShown, m.News.Length) : 0,
                    Math.Max(tape ? Math.Min(3, m.Tape.Length) : 0, orders ? Math.Min(3, m.OrderLines.Length) : 0)));
                float h = UiTokens.S2 + UiTokens.PairRow + lines * UiTokens.PairRow;
                float cx = x;
                if (news)
                {
                    float cw = UiDraw.SnapDown(room * wn / units);
                    NewsCard(s, at, m, cx, y, cw, h);
                    cx += cw + gap;
                }
                if (tape)
                {
                    float cw = UiDraw.SnapDown(room * wt / units);
                    Tape(s, at, m, cx, y, cw, h);
                    cx += cw + gap;
                }
                if (orders)
                {
                    Orders(s, at, m, cx, y, Math.Max(UiTokens.S16, x + w - cx), h);
                }
                y += h + UiTokens.S3;
            }
            Table(s, at, m, x, y, w, bottom, keys);
            return bottom;
        }

        // The portfolio's three views as one segmented choice over the page: what is held, what it pays, the account.
        private static float Views(UiScene s, UiAt at, StocksModel m, float x, float y, float w)
        {
            float width = Math.Min(w, UiSegments.Width(m.ViewLabels, UiStyle.Nav));
            UiSegments.Lay(s, at, x, y, width, UiTokens.ControlSmall, m.ViewLabels, m.View, View, m.ViewEdges, false);
            return y + UiTokens.ControlSmall + UiTokens.S3;
        }

        // The open orders: what each is and what it is worth, a row a press opens.
        private static void Orders(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float h)
        {
            int card = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(card);
            float count = string.IsNullOrEmpty(m.OrdersCount) ? 0f : Math.Min(w * 0.4f, UiTextRules.Width(m.OrdersCount, UiStyle.Small) + UiTokens.S1);
            UiDraw.TextMiddle(s, on, x + UiTokens.S3, y + UiTokens.S1, UiTokens.PairRow, w - UiTokens.S6 - count, UiStyle.SectionHeader, UiRole.Text, m.OrdersTitle, UiFlags.Label | UiFlags.Truncate);
            if (count > 0f)
            {
                UiDraw.TextMiddle(s, on, x + w - UiTokens.S3 - count, y + UiTokens.S1, UiTokens.PairRow, count, UiStyle.Small, UiRole.Text2, m.OrdersCount, UiFlags.Right | UiFlags.Truncate);
            }
            int n = Math.Min(3, m.OrderLines.Length);
            for (int i = 0; i < n; i++)
            {
                float ry = y + UiTokens.S1 + UiTokens.PairRow + i * UiTokens.PairRow;
                float vw = Math.Min(w * 0.4f, UiTextRules.Width(m.OrderValues[i], UiStyle.BodyStrong) + UiTokens.S1);
                int face = UiDraw.Box(s, on, x + UiTokens.S1, ry, w - UiTokens.S2, UiTokens.PairRow, UiRole.None, UiRole.None, UiTokens.RadiusMedium);
                UiAt row = on.In(face);
                UiDraw.TextMiddle(s, row, x + UiTokens.S3, ry, UiTokens.PairRow, w - UiTokens.S6 - vw - UiTokens.S2, UiStyle.Body, UiRole.Text2, m.OrderLines[i], UiFlags.Truncate);
                UiDraw.TextMiddle(s, row, x + w - UiTokens.S3 - vw, ry, UiTokens.PairRow, vw, UiStyle.BodyStrong, UiRole.Text, m.OrderValues[i], UiFlags.Right | UiFlags.Truncate);
                UiDraw.Node(s, on, x + UiTokens.S1, ry, w - UiTokens.S2, UiTokens.PairRow, OrderRow, i, face);
            }
        }

        // The news: the newest pieces in the old Market box's words, or its no-news note.
        private static void NewsCard(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float h)
        {
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiDraw.TextMiddle(s, at, x + UiTokens.S3, y + UiTokens.S1, UiTokens.PairRow, w - UiTokens.S6, UiStyle.SectionHeader, UiRole.Text, m.NewsTitle, UiFlags.Label | UiFlags.Truncate);
            int n = Math.Min(StocksModel.NewsShown, m.News.Length);
            for (int i = 0; i < n; i++)
            {
                UiDraw.TextMiddle(s, at, x + UiTokens.S3, y + UiTokens.S1 + UiTokens.PairRow + i * UiTokens.PairRow, UiTokens.PairRow, w - UiTokens.S6, UiStyle.Body,
                    m.NewsQuiet ? UiRole.Muted : UiRole.Text2, m.News[i], UiFlags.Truncate);
            }
        }

        // The index page: its figures, the index over the chosen period filling the room, and the rate line and what the index is under it.
        private static float IndexPage(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Figures != null && m.Figures.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, m.Figures, x, y, w);
            }
            float foot = (string.IsNullOrEmpty(m.RateLine) ? 0f : UiTokens.PairRow) + (string.IsNullOrEmpty(m.Note) ? 0f : UiTokens.PairRow);
            float ch = bottom - y - foot - (foot > 0f ? UiTokens.S3 : 0f);
            if (m.Chart != null && ch > UiTokens.S16 * 2f)
            {
                UiChart.Lay(s, at, m.Chart, x, y, w, ch);
            }
            else if (m.Chart == null && ch > UiTokens.S16 * 2f)
            {
                UiDraw.EmptyState(s, at, x, y, w, ch, m.EmptyIcon, m.EmptyTitle, m.EmptyBody, null, Empty, keys);
            }
            float fy = bottom - foot;
            if (!string.IsNullOrEmpty(m.RateLine))
            {
                UiDraw.TextMiddle(s, at, x, fy, UiTokens.PairRow, w, UiStyle.Body, UiRole.Text2, m.RateLine, UiFlags.Truncate);
                fy += UiTokens.PairRow;
            }
            if (!string.IsNullOrEmpty(m.Note))
            {
                UiDraw.TextMiddle(s, at, x, fy, UiTokens.PairRow, w, UiStyle.Small, UiRole.Muted, m.Note, UiFlags.Truncate);
            }
            return bottom;
        }

        private static float Tape(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float h)
        {
            int n = Math.Min(3, m.Tape.Length);
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiDraw.TextMiddle(s, at, x + UiTokens.S3, y + UiTokens.S1, UiTokens.PairRow, w - UiTokens.S6, UiStyle.SectionHeader, UiRole.Text, m.TapeTitle, UiFlags.Label | UiFlags.Truncate);
            for (int i = 0; i < n; i++)
            {
                UiDraw.TextMiddle(s, at, x + UiTokens.S3, y + UiTokens.S1 + UiTokens.PairRow + i * UiTokens.PairRow,
                    UiTokens.PairRow, w - UiTokens.S6, UiStyle.Body, UiRole.Text2, m.Tape[i], UiFlags.Truncate);
            }
            return y + h + UiTokens.S3;
        }

        private static float Company(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            return m.Co != null ? StocksCompanyLay.Lay(s, at, m, x, y, w, bottom, keys) : bottom;
        }

        // The Pays and the Account views: figures, the margin call over a card of labelled rows, and (Pays) the payments to come under it.
        private static float Rows(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Figures != null && m.Figures.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, m.Figures, x, y, w);
            }
            if (!string.IsNullOrEmpty(m.Banner))
            {
                float bh = UiTokens.Control;
                UiDraw.Box(s, at, x, y, w, bh, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                UiDraw.Icon(s, at, x + UiTokens.S3, y + (bh - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Warning, UiRole.None, UiFlags.Middle, true);
                float bx = x + UiTokens.S3 + UiTokens.Icon + UiTokens.S2;
                UiDraw.TextMiddle(s, at, bx, y, bh, x + w - UiTokens.S3 - bx, UiStyle.BodyStrong, m.BannerTone, m.Banner, UiFlags.Truncate);
                y += bh + UiTokens.S3;
            }
            if (m.Rows != null && m.Rows.Length > 0)
            {
                float ch = UiTokens.S2 + UiTokens.PairRow + m.Rows.Length * UiTokens.PairRow + UiTokens.S2;
                if (y + ch < bottom)
                {
                    int card = UiDraw.Box(s, at, x, y, w, ch, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                    UiAt on = at.In(card);
                    UiDraw.TextMiddle(s, on, x + UiTokens.S3, y + UiTokens.S1, UiTokens.PairRow, w - UiTokens.S6, UiStyle.SectionHeader, UiRole.Muted, m.RowsTitle, UiFlags.Label | UiFlags.Truncate);
                    for (int i = 0; i < m.Rows.Length; i++)
                    {
                        UiDraw.InfoRow(s, on, x + UiTokens.S3, y + UiTokens.S1 + UiTokens.PairRow + i * UiTokens.PairRow, w - UiTokens.S6, m.Rows[i].Label, m.Rows[i].Value, false, -1, 0,
                            m.Rows[i].Tone != UiRole.None ? m.Rows[i].Tone : UiRole.Text);
                    }
                    y += ch + UiTokens.S3;
                }
            }
            if (m.Table != null)
            {
                if (!string.IsNullOrEmpty(m.TableTitle) && bottom - y > UiTokens.S16 * 3f)
                {
                    UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.SectionHeader, UiRole.Text2, m.TableTitle, UiFlags.Label | UiFlags.Truncate);
                    y += UiTokens.PairRow + UiTokens.S1;
                }
                Table(s, at, m, x, y, w, bottom, keys);
            }
            return bottom;
        }

        // A line of words over a table: the History's totals over its trades; a statement when the page has one (the samples').
        private static void NoteTable(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Statement != null)
            {
                Statement(s, at, m, x, y, w, bottom, keys);
                return;
            }
            if (!string.IsNullOrEmpty(m.Note))
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.Body, UiRole.Text2, m.Note, UiFlags.Truncate);
                y += UiTokens.PairRow + UiTokens.S2;
            }
            Table(s, at, m, x, y, w, bottom, keys);
        }

        private static float Portfolio(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.View != 0)
            {
                return Rows(s, at, m, x, y, w, bottom, keys);
            }
            if (m.Figures != null && m.Figures.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, m.Figures, x, y, w);
            }
            if (m.Chart != null)
            {
                float ch = UiTokens.S16 * 3f;
                if (y + ch < bottom - UiTokens.S16 * 4f)
                {
                    UiChart.Lay(s, at, m.Chart, x, y, w, ch);
                    y += ch + UiTokens.S3;
                }
            }
            if (m.Donut != null && m.Donut.Length > 0 && y + UiTokens.S16 * 4f < bottom)
            {
                float size = UiTokens.S16 * 3f;
                UiDraw.Donut(s, at, x, y, size, m.Donut, m.Donut.Length);
                UiDraw.Legend(s, at, x + size + UiTokens.S4, y, w - size - UiTokens.S4, m.DonutLabels, m.DonutValues, m.DonutLabels.Length);
                y += size + UiTokens.S3;
            }
            Table(s, at, m, x, y, w, bottom, keys);
            return bottom;
        }

        private static void Statement(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Statement == null)
            {
                return;
            }
            UiStatement.Lay(s, at, m.Statement, x, y, w, bottom - y, keys);
        }

        private static float Listing(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Hero != null)
            {
                float hh = UiAppWindow.Hero(s, at, m.Hero, x, y, w);
                y += hh + UiTokens.S4;
            }
            if (m.Track != null)
            {
                y += UiTrack.Lay(s, at, m.Track, x, y, w) + UiTokens.S4;
            }
            if (!string.IsNullOrEmpty(m.Note))
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.Body, UiRole.Text2, m.Note, UiFlags.Truncate);
                y += UiTokens.PairRow + UiTokens.S2;
            }
            Table(s, at, m, x, y, w, bottom, keys);
            return bottom;
        }

        private static void Table(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Table == null || bottom - y < UiTokens.S16 * 2f)
            {
                return;
            }
            short list = UiDataTable.Lay(s, at, m.Table, x, y, w, bottom - y, keys);
            m.ListOne = list;
            m.ClipOne = list >= 0 ? s.Lists[list].Clip : -1;
        }

        private static void Prompts(UiScene s, StocksModel m)
        {
            if (!string.IsNullOrEmpty(m.PromptMove))
            {
                s.AddPrompt(UiAction.Move, m.PromptMove);
            }
            if (!string.IsNullOrEmpty(m.PromptSelect))
            {
                s.AddPrompt(UiAction.Confirm, m.PromptSelect);
            }
            if (!string.IsNullOrEmpty(m.PromptPlace))
            {
                s.AddPrompt(UiAction.Alt, m.PromptPlace);
            }
            if (!string.IsNullOrEmpty(m.PromptOther))
            {
                s.AddPrompt(UiAction.Option, m.PromptOther);
            }
            if (!string.IsNullOrEmpty(m.PromptBack))
            {
                s.AddPrompt(UiAction.Back, m.PromptBack);
            }
        }
    }
}
