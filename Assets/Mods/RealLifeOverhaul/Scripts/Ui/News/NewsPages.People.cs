using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The city's people, a district and a household as pages of the Economy window, and the back button of a page opened from another. Game-free.</summary>
    public static partial class NewsPages
    {
        private const float DonutSize = 112f;   // px
        private const float FigureStep = UiTokens.TableRow;   // px: a figure's row in the card

        // The people: at the left a card of figures with the links under them, then the ages and the incomes as donuts; at the right the table of districts.
        private static void People(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            NewsPeople p = m.People;
            if (p == null)
            {
                Notice(s, at, m, x, y, w, bottom - y);
                return;
            }
            float gap = UiTokens.S3;
            float lw = UiDraw.SnapDown(Math.Max(380f, Math.Min(460f, w * 0.32f)));
            float rx = x + lw + gap, rw = w - lw - gap;
            float fh = FiguresHeight(m, p);
            FiguresCard(s, at, m, p, x, y, lw, fh);
            float dy = y + fh + gap;
            float dh = DonutCardHeight(p.ChartLabels, p.Chart2Labels);
            if (dy + dh <= bottom)
            {
                DonutCard(s, at, p.ChartTitle, p.ChartLabels, p.ChartValues, p.ChartShares, x, dy, lw, dh);
                dy += dh + gap;
            }
            if (dy + dh <= bottom)
            {
                DonutCard(s, at, p.Chart2Title, p.Chart2Labels, p.Chart2Values, p.Chart2Shares, x, dy, lw, dh);
            }
            if (p.Table != null && bottom - y > UiTokens.S16)
            {
                TableCard(s, at, p.Table, p.TableTitle, rx, y, rw, bottom - y, keys, NewsCommands.District);
            }
        }

        private static int LinkCount(NewsModel m) => m.Actions == null ? 0 : Math.Min(2, m.Actions.Length);

        // The card's height: its title, its rows and the links under them.
        private static float FiguresHeight(NewsModel m, NewsPeople p)
        {
            int rows = p.Rows.Length + (string.IsNullOrEmpty(p.HeadLabel) ? 0 : 1);
            int links = LinkCount(m);
            return UiDraw.Snap(UiTokens.S3 + UiTokens.PairRow + UiTokens.S2 + rows * FigureStep + (links > 0 ? UiTokens.S4 + links * UiTokens.Control : 0f) + UiTokens.S3);
        }

        // The headline figures, one to a row, and under them the links that open the page of who pays most rent and of the busiest district.
        private static void FiguresCard(UiScene s, UiAt at, NewsModel m, NewsPeople p, float x, float y, float w, float h)
        {
            UiAt on = Card(s, at, x, y, w, h, p.Title, null, out float cy);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            if (!string.IsNullOrEmpty(p.HeadLabel))
            {
                UiDraw.SectionRow(s, on, ix, cy, iw, p.HeadLabel, p.HeadValue, false, -1, 0);
                cy += FigureStep;
            }
            for (int i = 0; i < p.Rows.Length; i++)
            {
                UiDraw.InfoRow(s, on, ix, cy, iw, p.Rows[i].Label, p.Rows[i].Value, false, -1, 0, p.Rows[i].Ink == UiRole.None ? UiRole.Text : p.Rows[i].Ink);
                cy += FigureStep;
            }
            int links = LinkCount(m);
            if (links == 0)
            {
                return;
            }
            cy += UiTokens.S2;
            UiDraw.Box(s, on, ix, cy, iw, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            cy += UiTokens.S2;
            for (int i = 0; i < links; i++)
            {
                UiDraw.LinkRow(s, on, ix, cy + i * UiTokens.Control, iw, UiTokens.Control, m.Actions[i].Label, m.Actions[i].Command, m.Actions[i].Arg, false);
            }
        }

        private static float DonutCardHeight(string[] a, string[] b)
        {
            int n = Math.Max(a == null ? 0 : a.Length, b == null ? 0 : b.Length);
            return UiDraw.Snap(UiTokens.S3 + UiTokens.PairRow + UiTokens.S2 + Math.Max(DonutSize, n * 20f) + UiTokens.S4);
        }

        // A donut of shares with its legend: what each slice is and how much of the whole it is.
        private static void DonutCard(UiScene s, UiAt at, string title, string[] labels, string[] values, float[] shares, float x, float y, float w, float h)
        {
            UiAt on = Card(s, at, x, y, w, h, title, null, out float cy);
            if (shares == null || shares.Length == 0)
            {
                return;
            }
            UiDraw.Donut(s, on, x + UiTokens.S4, cy, DonutSize, shares, shares.Length);
            int n = labels == null ? 0 : labels.Length;
            float lx = x + UiTokens.S4 + DonutSize + UiTokens.S6, lw = x + w - UiTokens.S4 - lx;
            float lh = n * 20f;
            UiDraw.Legend(s, on, lx, cy + (DonutSize - lh) / 2f, lw, labels, values, n);
        }

        // A household: its rows in one card, as an info panel, in the window.
        private static void Household(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom)
        {
            UiInfoModel h = m.Household;
            if (h == null || h.Sections == null)
            {
                Notice(s, at, m, x, y, w, bottom - y);
                return;
            }
            bool captions = h.Sections.Length > 1;   // one section is what the card's title says already: its own label would be a second one over it
            float rows = 0f;
            for (int i = 0; i < h.Sections.Length; i++)
            {
                rows += (captions ? FigureStep : 0f) + h.Sections[i].Rows.Length * FigureStep + UiTokens.S2;
            }
            float cw = UiDraw.SnapDown(Math.Min(w, 480f));
            float ch = UiDraw.Snap(UiTokens.S3 + UiTokens.PairRow + UiTokens.S2 + rows + UiTokens.S3);
            UiAt on = Card(s, at, x, y, cw, Math.Min(ch, bottom - y), h.Title, null, out float cy);
            float ix = x + UiTokens.S4, iw = cw - UiTokens.S4 * 2f;
            for (int i = 0; i < h.Sections.Length; i++)
            {
                UiInfoSection section = h.Sections[i];
                if (captions)
                {
                    UiDraw.Text(s, on, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, section.Label, UiFlags.Label | UiFlags.Truncate);
                    cy += FigureStep;
                }
                for (int r = 0; r < section.Rows.Length; r++)
                {
                    UiDraw.InfoRow(s, on, ix, cy, iw, section.Rows[r].Label, section.Rows[r].Value, false, -1, 0, section.Rows[r].Ink == UiRole.None ? UiRole.Text : section.Rows[r].Ink);
                    cy += FigureStep;
                }
                cy += UiTokens.S2;
            }
        }

        // The way back from a page opened from another: a button naming the page under it, and this page's own name beside it. Returns its height.
        private static float BackRow(UiScene s, UiAt at, NewsModel m, float x, float y, float w, IUiKeys keys)
        {
            float h = UiTokens.ControlSmall;
            float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.BackLabel, UiIcon.Back, UiAction.Count, keys);
            UiDraw.Button(s, at, x, y, bw, h, UiButtonKind.Secondary, m.BackLabel, UiIcon.Back, UiAction.Count, keys, NewsCommands.Back, 0);
            if (!string.IsNullOrEmpty(m.PageTitle))
            {
                UiDraw.TextMiddle(s, at, x + bw + UiTokens.S4, y, h, w - bw - UiTokens.S4, UiStyle.Title, UiRole.Text, m.PageTitle, UiFlags.Truncate);
            }
            return h;
        }
    }
}
