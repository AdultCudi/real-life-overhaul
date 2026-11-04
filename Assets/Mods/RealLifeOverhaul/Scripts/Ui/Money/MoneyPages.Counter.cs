using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A bank's counter (Banks, Your bank): the head of the bank, the strip of banks to pick from, the page's figures, then what the tab
    /// is about on the left (a statement, a table of rows, a chart, graded bars, check lines) and its cards of facts and buttons in a
    /// column at the right. Each tab fills only the parts it has, so no tab can show the page of another. Game-free. Cost: a layout when
    /// the model changes.
    /// </summary>
    public static partial class MoneyPages
    {
        private static void Counter(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            m.TableList = m.TableClip = m.Table2List = m.Table2Clip = m.StatementList = m.StatementClip = -1;
            if (m.Profile != null)
            {
                y = UiAppWindow.Profile(s, at, m.Profile, m.Actions, x, y, w, keys);
            }
            y = PickStrip(s, at, m, x, y, w);
            if (m.Hero != null)
            {
                y += UiAppWindow.Hero(s, at, m.Hero, x, y, w) + UiTokens.S5;
            }
            if (m.Figures.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, m.Figures, x, y, w) + UiTokens.S4;
            }
            if (!string.IsNullOrEmpty(m.Headline))
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w, UiStyle.BodyStrong, UiRole.Text2, m.Headline, UiFlags.Truncate);
                y += UiTokens.Control;
            }
            bool cards = m.Cards.Length > 0;
            bool left = m.Statement != null || m.Table != null || m.Table2 != null || m.Chart != null || m.Donut.Length > 0 || m.Lines.Length > 0 || m.Marks.Length > 0 || !string.IsNullOrEmpty(m.Note);
            float gap = UiTokens.S4;
            if (!cards)
            {
                LeftColumn(s, at, m, x, y, w, bottom, keys);
                return;
            }
            if (!left)
            {
                CardsAcross(s, at, m, x, y, w, bottom, keys);
                return;
            }
            float lw = UiDraw.SnapDown((w - gap) * 0.6f);
            LeftColumn(s, at, m, x, y, lw, bottom, keys);
            CardColumn(s, at, m, x + lw + gap, y, w - lw - gap, bottom, keys);
        }

        // The banks as a strip: each its own target, the chosen one lit, so a pointer and the keys both pick.
        private static float PickStrip(UiScene s, UiAt at, MoneyModel m, float x, float y, float w)
        {
            int n = Math.Min(m.PickNames.Length, UiSegments.Most);
            if (n < 2)
            {
                return y;
            }
            float h = UiTokens.Control;
            float tw = Math.Min(w, Math.Max(UiSegments.Width(m.PickNames, UiStyle.BodyStrong), UiTokens.S16 * 3f * n));
            if (m.PickEdges.Length != n)
            {
                m.PickEdges = new float[n];
            }
            int track = UiDraw.Box(s, at, x, y, tw, h, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
            UiAt on = at.In(track);
            float cx = x, total = 0f;
            var widths = new float[n];
            for (int i = 0; i < n; i++)
            {
                widths[i] = UiTextRules.Width(m.PickNames[i], UiStyle.BodyStrong) + UiTokens.S6;
                total += widths[i];
            }
            for (int i = 0; i < n; i++)
            {
                float sw = tw * widths[i] / total;
                bool lit = i == m.PickAt;
                int seg = UiDraw.Box(s, on, cx, y, sw, h, lit ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, UiFlags.Share | (lit ? UiFlags.Selected : UiFlags.None));
                UiDraw.TextMiddle(s, on.In(seg), cx + UiTokens.S1 / 2f, y, h, sw - UiTokens.S1, UiStyle.BodyStrong, lit ? UiRole.Text : UiRole.Text2, m.PickNames[i], UiFlags.Centre | UiFlags.Label | UiFlags.Truncate);
                UiDraw.Node(s, at, cx, y, sw, h, MoneyCommands.PickBank, i, seg);
                cx += sw;
                m.PickEdges[i] = cx;
            }
            return y + h + UiTokens.S4;
        }

        private static short ClipOf(UiScene s, short list) => list >= 0 && list < s.ListCount ? s.Lists[list].Clip : (short)-1;

        private static void LeftColumn(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Note(s, at, m.Note, x, y, w);
            y = m.Marks.Length > 0 ? Bars(s, at, m, x, y, w) : Lines(s, at, m, x, y, w);
            float chartH = UiTokens.S16 * 4f;
            if (m.Chart != null && bottom - y > chartH + UiTokens.S16 * 2f)
            {
                UiChart.Lay(s, at, m.Chart, x, y, w, chartH);
                y += chartH + UiTokens.S4;
            }
            float donutH = UiTokens.S16 * 3f;
            if (m.Donut.Length > 0 && bottom - y > donutH + UiTokens.S16)
            {
                y += Donut(s, at, m, x, y, w, donutH) + UiTokens.S4;
            }
            float h = bottom - y;
            if (h < UiTokens.S16)
            {
                return;
            }
            if (m.Statement != null)
            {
                m.StatementList = UiStatement.Lay(s, at.InRegion(1), m.Statement, x, y, w, h, keys);
                m.StatementClip = ClipOf(s, m.StatementList);
                return;
            }
            if (m.Table != null && m.Table2 != null)
            {
                // The second table takes what its rows need (a few), the first the rest, so neither hides a row the keys must reach.
                float need2 = m.Table2.Rows.Length * UiTokens.TableRow + UiTokens.S16 * 3f + (string.IsNullOrEmpty(m.Table2Title) ? 0f : UiTokens.Control) + UiTokens.S3;
                float h1 = UiDraw.SnapDown(Math.Max(UiTokens.S16 * 4f, Math.Min(m.Table.Rows.Length * UiTokens.TableRow + UiTokens.S16 * 2f, h - need2)));
                m.TableList = UiDataTable.Lay(s, at.InRegion(1), m.Table, x, y, w, h1, keys);
                m.TableClip = ClipOf(s, m.TableList);
                float y2 = y + h1 + UiTokens.S3;
                if (!string.IsNullOrEmpty(m.Table2Title))
                {
                    UiDraw.TextMiddle(s, at, x, y2, UiTokens.Control, w, UiStyle.BodyStrong, UiRole.Text2, m.Table2Title, UiFlags.Truncate);
                    y2 += UiTokens.Control;
                }
                if (bottom - y2 > UiTokens.S16)
                {
                    m.Table2List = UiDataTable.Lay(s, at.InRegion(1), m.Table2, x, y2, w, bottom - y2, keys, Table2Base);
                    m.Table2Clip = ClipOf(s, m.Table2List);
                }
                return;
            }
            if (m.Table != null)
            {
                m.TableList = UiDataTable.Lay(s, at.InRegion(1), m.Table, x, y, w, h, keys);
                m.TableClip = ClipOf(s, m.TableList);
                return;
            }
            if (m.Table2 != null)
            {
                if (!string.IsNullOrEmpty(m.Table2Title))
                {
                    UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w, UiStyle.BodyStrong, UiRole.Text2, m.Table2Title, UiFlags.Truncate);
                    y += UiTokens.Control;
                    h -= UiTokens.Control;
                }
                m.Table2List = UiDataTable.Lay(s, at.InRegion(1), m.Table2, x, y, w, h, keys, Table2Base);
                m.Table2Clip = ClipOf(s, m.Table2List);
            }
        }

        /// <summary>The second table's commands run from here, as the first's from <see cref="UiDataTable.First"/>.</summary>
        public const int Table2Base = 600;

        private static void CardColumn(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float cy = y;
            int n = Math.Min(m.Cards.Length, MoneyCommands.CardsMost);
            var need = new float[n];
            for (int k = 0; k < n; k++)
            {
                need[k] = UtilitiesFace.CardHeight(m.Cards[k], w, keys, true);
            }
            for (int k = 0; k < n; k++)
            {
                float room = bottom - cy;
                if (room < UiTokens.S16 * 2f)
                {
                    break;
                }
                // Each card gets what it needs while the cards after it keep a share of the room (a sixth each, or what they need if less).
                float later = 0f;
                for (int j = k + 1; j < n; j++)
                {
                    later += Math.Min(need[j], (bottom - y) * 0.15f) + UiTokens.S3;
                }
                float limit = k == n - 1 ? room : Math.Max(UiTokens.S16 * 2f, Math.Min(need[k], room - later));
                float used = UtilitiesFace.Card(s, at.InRegion(2), m.Cards[k], x, cy, w, limit, MoneyCommands.CardSwitch + k, MoneyCommands.CardLess + k, MoneyCommands.CardMore + k, keys, true, true);
                cy += used + UiTokens.S3;
            }
        }

        // Cards and nothing beside them: two columns, each card as tall as it needs.
        private static void CardsAcross(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            int n = Math.Min(m.Cards.Length, MoneyCommands.CardsMost);
            float gap = UiTokens.S4;
            int cols = n > 1 ? 2 : 1;
            float cw = cols == 2 ? UiDraw.SnapDown((w - gap) / 2f) : UiDraw.SnapDown(w * 0.62f);
            var top = new float[2] { y, y };
            for (int k = 0; k < n; k++)
            {
                int col = cols == 1 ? 0 : (top[0] <= top[1] ? 0 : 1);
                float cx = x + col * (cw + gap);
                float room = bottom - top[col];
                if (room < UiTokens.S16 * 2f)
                {
                    continue;
                }
                float used = UtilitiesFace.Card(s, at.InRegion(2), m.Cards[k], cx, top[col], cw, room, MoneyCommands.CardSwitch + k, MoneyCommands.CardLess + k, MoneyCommands.CardMore + k, keys, true, true);
                top[col] += used + UiTokens.S3;
            }
        }
    }
}
