using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Stocks desk: a window, the Markets, Yours and Other markets groups down the side (the sidebar has no group
    /// labels yet, so they are drawn here), and the page the player came for. Game-free. Cost: a layout a change.
    /// </summary>
    public static class StocksLay
    {
        public const int Choice = 30, Tool = 31, Chip = 32, Less = 33, More = 34, Place = 35, Instead = 36, Alert = 37, Empty = 38, Shares = 39;
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
                    case StocksPage.Bots: y = Bots(s, at, m, x, y, w, bottom, keys); break;
                    default: y = Market(s, at, m, x, y, w, bottom, keys); break;
                }
            }
            UiPageFence.Lay(s, page, x, x + w);
            Prompts(s, m);
            UiDraw.PromptBar(s, frame, keys);
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
            float h = UiTokens.Control;
            Field(s, at, x, y, w, t.Side, Choice, 0);
            y += h + UiTokens.S2;
            Field(s, at, x, y, w, t.Kind, Choice, 1);
            y += h + UiTokens.S2;
            Field(s, at, x, y, w, t.Life, Choice, 2);
            y += h + UiTokens.S2;
            UiDraw.Stepper(s, at, x, y, w, t.Shares, t.CanLess, t.CanMore, Less, Shares, More, 0, false);
            y += h + UiTokens.S2;
            if (!string.IsNullOrEmpty(t.Limit))
            {
                UiDraw.InfoRow(s, at, x, y, w, t.Limit, t.Max);
                y += UiTokens.PairRow;
            }
            UiDraw.InfoRow(s, at, x, y, w, t.Cost, t.Fee);
            y += UiTokens.PairRow;
            UiDraw.InfoRow(s, at, x, y, w, t.Total, t.CashAfter);
            y += UiTokens.PairRow;
            if (!string.IsNullOrEmpty(t.After))
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.Small, UiRole.Text2, t.After, UiFlags.Truncate);
                y += UiTokens.PairRow;
            }
            for (int i = 0; i < m.Signals.Length && y + UiTokens.PairRow < bottom; i++)
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.Small, UiRole.Text2, m.Signals[i], UiFlags.Truncate);
                y += UiTokens.PairRow;
            }
            for (int i = 0; i < m.Alerts.Length && i < 3 && y + UiTokens.ControlSmall < bottom; i++)
            {
                UiDraw.Button(s, at, x, y, w, UiTokens.ControlSmall, UiButtonKind.Quiet, m.Alerts[i], UiIcon.Bell, UiAction.Count, keys, Alert, i);
                y += UiTokens.ControlSmall + UiTokens.S1;
            }
            if (y + h <= bottom)
            {
                float bw = UiDraw.ButtonWidth(UiButtonKind.Primary, t.Place, UiIcon.None, UiAction.Alt, keys);
                UiDraw.Button(s, at, x + w - bw, y, bw, h, UiButtonKind.Primary, t.Place, UiIcon.None, UiAction.Alt, keys, Place, 0, t.PlaceDisabled);
                if (!string.IsNullOrEmpty(t.Instead))
                {
                    float iw = UiDraw.ButtonWidth(UiButtonKind.Secondary, t.Instead, UiIcon.None, UiAction.Option, keys);
                    if (x + iw < x + w - bw - UiTokens.S2)
                    {
                        UiDraw.Button(s, at, x, y, iw, h, UiButtonKind.Secondary, t.Instead, UiIcon.None, UiAction.Option, keys, Instead, 0);
                    }
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

        private static float Bots(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Figures != null && m.Figures.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, m.Figures, x, y, w);
            }
            y = Facts(s, at, m, x, y, w);
            if (m.Statement != null)
            {
                UiStatement.Lay(s, at, m.Statement, x, y, w, bottom - y, keys);
            }
            else
            {
                Table(s, at, m, x, y, w, bottom, keys);
            }
            return bottom;
        }

        private static void Table(UiScene s, UiAt at, StocksModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Table == null || bottom - y < UiTokens.S16 * 2f)
            {
                return;
            }
            UiDataTable.Lay(s, at, m.Table, x, y, w, bottom - y, keys);
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
