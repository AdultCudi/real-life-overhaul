using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Stocks desk: a window, the Markets, Yours and Other markets groups down the side (the sidebar has no group
    /// labels yet, so they are drawn here), and the page the player came for. Game-free. Cost: a layout a change.
    /// </summary>
    public static class StocksLay
    {
        public const int Choice = 30, Tool = 31, Chip = 32, Less = 33, More = 34, Place = 35, Instead = 36, Alert = 37, Empty = 38, Shares = 39,
            Act = 50, PriceLess = 51, PriceMore = 52, PricePlain = 53, Watch = 54, Second = 200;
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
            int shown = Nav(s, at, m, wx, top, wh - UiFrame.Strip);
            if (shown >= 0)
            {
                s.FocusFirst = shown;
            }
            int page = s.NodeCount;
            float x = wx + UiAppWindow.Sidebar + UiTokens.S5;
            float w = wx + ww - UiTokens.S5 - x;
            float y = top + UiTokens.S4;
            float bottom = wy + wh - UiTokens.S4;
            if (m.Page != StocksPage.Company)
            {
                y = Header(s, at, m, x, y, w, keys);
            }
            if (!string.IsNullOrEmpty(m.EmptyTitle) && m.Table == null && m.Statement == null && m.Desk == null)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, m.EmptyIcon, m.EmptyTitle, m.EmptyBody, null, Empty, keys);
            }
            else
            {
                switch (m.Page)
                {
                    case StocksPage.Company: y = Company(s, at, m, x, y, w, bottom, keys); break;
                    case StocksPage.Portfolio: y = Portfolio(s, at, m, x, y, w, bottom, keys); break;
                    case StocksPage.Orders:
                    case StocksPage.History: Statement(s, at, m, x, y, w, bottom, keys); break;
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
            if (m.Tape != null && m.Tape.Length > 0)
            {
                y = Tape(s, at, m, x, y, w);
            }
            Table(s, at, m, x, y, w, bottom, keys);
            return bottom;
        }

        private static float Tape(UiScene s, UiAt at, StocksModel m, float x, float y, float w)
        {
            int n = Math.Min(3, m.Tape.Length);
            float h = UiTokens.S2 + UiTokens.PairRow + n * UiTokens.PairRow;
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
            if (m.Head != null)
            {
                y = UiFirmHead.Lay(s, at, m.Head, x, y, w, keys, 90);
            }
            if (!string.IsNullOrEmpty(m.Note))
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.Body, UiRole.Text2, m.Note, UiFlags.Truncate);
                y += UiTokens.PairRow + UiTokens.S2;
            }
            if (m.Figures != null && m.Figures.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, m.Figures, x, y, w);
            }
            y = Facts(s, at, m, x, y, w);
            float gap = UiTokens.S4;
            float right = UiDraw.SnapDown(w * 0.34f);
            float left = w - right - gap;
            float tools = Tools(s, at, m, x, y, left);
            float chartTop = y + tools;
            if (m.Desk != null && bottom - chartTop > UiTokens.S16 * 4f)
            {
                UiChartDesk.Lay(s, at, m.Desk, x, chartTop, left, bottom - chartTop);
            }
            Ticket(s, at, m, x + left + gap, y, right, bottom, keys);
            return bottom;
        }

        private static float Facts(UiScene s, UiAt at, StocksModel m, float x, float y, float w)
        {
            int n = Math.Min(m.FactLabels.Length, m.FactValues.Length);
            if (n == 0)
            {
                return y;
            }
            int cols = n > 3 ? 2 : 1;
            int rows = (n + cols - 1) / cols;
            float col = UiDraw.SnapDown(w / cols);
            for (int i = 0; i < n; i++)
            {
                float rx = x + (i % cols) * col;
                float ry = y + (i / cols) * UiTokens.PairRow;
                UiDraw.InfoRow(s, at, rx, ry, col - UiTokens.S3, m.FactLabels[i], m.FactValues[i]);
            }
            return y + rows * UiTokens.PairRow + UiTokens.S3;
        }

        private static float Tools(UiScene s, UiAt at, StocksModel m, float x, float y, float w)
        {
            float h = 0f;
            if (m.Chips != null && m.Chips.Length > 0)
            {
                float cx = x;
                for (int i = 0; i < m.Chips.Length; i++)
                {
                    float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Chips[i], UiIcon.None, UiAction.Count, null);
                    if (cx + bw > x + w)
                    {
                        break;
                    }
                    UiDraw.Button(s, at, cx, y, bw, UiTokens.ControlSmall, UiButtonKind.Secondary, m.Chips[i], UiIcon.None, UiAction.Count, null, Chip, i);
                    cx += bw + UiTokens.S2;
                }
                h += UiTokens.ControlSmall + UiTokens.S2;
            }
            if (m.Tools != null && m.Tools.Length > 0)
            {
                float size = UiTokens.ControlSmall;
                for (int i = 0; i < m.Tools.Length; i++)
                {
                    float tx = x + i * (size + UiTokens.S1);
                    if (tx + size > x + w)
                    {
                        break;
                    }
                    UiIcon icon = i == m.Tools.Length - 1 ? UiIcon.Trash : UiIcon.Chart;
                    UiDraw.IconButton(s, at, tx, y + h, size, icon, UiRole.None, Tool, i, i == m.Tool);
                }
                h += size + UiTokens.S2;
            }
            return h;
        }

        private static void Ticket(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            StocksTicket t = m.Ticket;
            if (t == null)
            {
                return;
            }
            float h = UiTokens.Control, small = UiTokens.ControlSmall;
            // The buttons stand at the foot, so a short window never loses Place; what is above them gives way.
            float placeY = bottom - h;
            float watchY = placeY - UiTokens.S2 - small;
            float limit = watchY - UiTokens.S2;
            bool closing = string.IsNullOrEmpty(t.Instead);
            if (y + h <= limit)
            {
                UiDraw.Choice(s, at, x, y, w, t.Side, Choice, 0, closing);
                y += h + UiTokens.S2;
            }
            if (y + h <= limit)
            {
                Field(s, at, x, y, w, t.Kind, Choice, 1);
                y += h + UiTokens.S2;
            }
            if (y + h <= limit)
            {
                Field(s, at, x, y, w, t.Life, Choice, 2);
                y += h + UiTokens.S2;
            }
            if (y + h <= limit)
            {
                UiDraw.Stepper(s, at, x, y, w, t.Shares, t.CanLess, t.CanMore, Less, Shares, More, 0, false);
                y += h + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(t.Price) && y + h <= limit)
            {
                UiDraw.Stepper(s, at, x, y, w, t.Price, true, true, PriceLess, PricePlain, PriceMore, 0, false);
                y += h + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(t.PutBack) && y + h <= limit)
            {
                Field(s, at, x, y, w, t.PutBack, Choice, 3);
                y += h + UiTokens.S2;
            }
            if (string.IsNullOrEmpty(t.Price) && !string.IsNullOrEmpty(t.Limit) && y + UiTokens.PairRow <= limit)
            {
                UiDraw.InfoRow(s, at, x, y, w, t.Limit, t.Max);
                y += UiTokens.PairRow;
            }
            if (y + UiTokens.PairRow <= limit)
            {
                UiDraw.InfoRow(s, at, x, y, w, t.Cost, t.Fee);
                y += UiTokens.PairRow;
            }
            if (y + UiTokens.PairRow <= limit)
            {
                UiDraw.InfoRow(s, at, x, y, w, t.Total, t.CashAfter);
                y += UiTokens.PairRow;
            }
            if (!string.IsNullOrEmpty(t.After) && y + UiTokens.PairRow <= limit)
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.Small, UiRole.Text2, t.After, UiFlags.Truncate);
                y += UiTokens.PairRow;
            }
            if (!string.IsNullOrEmpty(t.Why) && y + UiTokens.PairRow * 2f <= limit)
            {
                float wh = UiDraw.Snap(UiTextRules.WrappedHeight(t.Why, UiStyle.Small, w, 2));
                UiDraw.Text(s, at, x, y, w, UiStyle.Small, t.PlaceDisabled ? UiRole.DangerText : UiRole.WarnText, t.Why, UiFlags.Wrap | UiFlags.Truncate, 2);
                y += wh + UiTokens.S1;
            }
            for (int i = 0; i < m.Signals.Length && y + UiTokens.PairRow <= limit; i++)
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.Small, UiRole.Text2, m.Signals[i], UiFlags.Truncate);
                y += UiTokens.PairRow;
            }
            for (int i = 0; i < m.Alerts.Length && i < 3 && y + small <= limit; i++)
            {
                UiDraw.Button(s, at, x, y, w, small, UiButtonKind.Quiet, m.Alerts[i], UiIcon.Bell, UiAction.Count, keys, Alert, i);
                y += small + UiTokens.S1;
            }
            float ww = UiDraw.ButtonWidth(UiButtonKind.Secondary, t.Watch, UiIcon.Eye, UiAction.Count, keys);
            UiDraw.Button(s, at, x, watchY, Math.Min(w, ww), small, UiButtonKind.Secondary, t.Watch, UiIcon.Eye, UiAction.Count, keys, Watch, 0);
            float bw = UiDraw.ButtonWidth(UiButtonKind.Primary, t.Place, UiIcon.None, UiAction.Alt, keys);
            UiDraw.Button(s, at, x + w - bw, placeY, bw, h, UiButtonKind.Primary, t.Place, UiIcon.None, UiAction.Alt, keys, Place, 0, t.PlaceDisabled);
            if (!string.IsNullOrEmpty(t.Instead))
            {
                float iw = UiDraw.ButtonWidth(UiButtonKind.Secondary, t.Instead, UiIcon.None, UiAction.Option, keys);
                if (iw < w - bw - UiTokens.S2)
                {
                    UiDraw.Button(s, at, x, placeY, iw, h, UiButtonKind.Secondary, t.Instead, UiIcon.None, UiAction.Option, keys, Instead, 0);
                }
            }
        }

        private static void Field(UiScene s, UiAt at, float x, float y, float w, string value, int command, int arg)
        {
            UiDraw.Choice(s, at, x, y, w, value, command, arg, false);
        }

        private static float Portfolio(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
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
