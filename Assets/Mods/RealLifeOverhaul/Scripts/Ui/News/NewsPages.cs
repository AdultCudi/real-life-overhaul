using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Economy app, the city's people, the Newsstand, the City Ledger and the ending. Each page is shaped by what
    /// it is for. Game-free. Cost: a layout when the model changes.
    /// </summary>
    public static partial class NewsPages
    {
        public static void Lay(UiScene s, NewsModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (m.Shape == NewsShape.People)
            {
                People(s, m, frame, dark, keys);
                return;
            }
            if (m.Shape == NewsShape.Ending)
            {
                UiDecision.Lay(s, m.Decision, frame, dark, keys);
                return;
            }
            if (m.Shape == NewsShape.Household)
            {
                UiInfoPanel.Lay(s, m.Household, frame, dark, keys);
                return;
            }
            s.Begin(m.Title ?? "news", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Nav, m.Tab, m.Across, keys, out float x, out float y, out float w, out float bottom);
            switch (m.Shape)
            {
                case NewsShape.Overview: Overview(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Prices: Prices(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Jobs: Jobs(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Districts: OneTable(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Markets: Markets(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Finances: Finances(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Insights: Insights(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Stand: Stand(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Reading: Read(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Paper: Paper(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Renters: OneTable(s, at, m, x, y, w, bottom, keys); break;
                default: Notice(s, at, m, x, y, w, bottom - y); break;
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        private static void Overview(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Lead(s, at, m, x, y, w);
            y = Ranges(s, at, m, x, y, keys);
            Dress(m);
            float gap = UiTokens.S4;
            float left = UiDraw.SnapDown(w * 0.62f);
            float rx = x + left + gap;
            float rw = UiDraw.SnapDown(x + w - rx);
            float adviceH = AdviceBlock(m);
            float chartH = Math.Max(UiTokens.S16 * 3f, adviceH);
            float room = bottom - y - UiTokens.S16 * 2f;
            if (room < chartH)
            {
                chartH = Math.Max(UiTokens.S16 * 2f, room);
            }
            float block = Math.Max(chartH, adviceH);
            if (m.Chart != null && bottom - y > chartH)
            {
                UiChart.Lay(s, at, m.Chart, x, y, left, chartH);
                Advice(s, at, m, rx, y, rw, keys);
                y += block + UiTokens.S2;
            }
            if (m.Marks == null || m.Marks.Length == 0)
            {
                y = Line(s, at, m.RiseLine, x, y, w);
            }
            if (m.Table != null && m.Table2 != null && bottom - y > UiTokens.S16 * 2f)
            {
                float half = UiDraw.SnapDown((w - gap) * 0.5f);
                float h = bottom - y;
                UiDataTable.Lay(s, at, m.Table, x, y, half, h, keys);
                UiDataTable.Lay(s, at, m.Table2, x + half + gap, y, w - half - gap, h, keys, NewsCommands.District);
                return;
            }
            Rest(s, at, m, x, y, w, bottom, keys);
        }

        private static float Ranges(UiScene s, UiAt at, NewsModel m, float x, float y, IUiKeys keys)
        {
            if (m.Actions == null || m.Actions.Length == 0)
            {
                return y;
            }
            float bw = UiTokens.S16 * 2f;
            for (int i = 0; i < m.Actions.Length && i < 3; i++)
            {
                UiDraw.Button(s, at, x + i * (bw + UiTokens.S2), y, bw, UiTokens.S8, m.Actions[i].Kind, m.Actions[i].Label, UiIcon.None,
                    UiAction.Count, keys, UiAppWindow.ActionCommand, i, false);
            }
            return y + UiTokens.S8 + UiTokens.S2;
        }

        private static void Prices(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Lead(s, at, m, x, y, w);
            float chartH = UiTokens.S16 * 3f;
            if (m.Chart != null && bottom - y > chartH + UiTokens.S16)
            {
                UiChart.Lay(s, at, m.Chart, x, y, w, chartH);
                y += chartH + UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(m.Note))
            {
                y = Note(s, at, m.Note, x, y, w);
            }
            Rest(s, at, m, x, y, w, bottom, keys);
        }

        private static void Jobs(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Lead(s, at, m, x, y, w);
            float chartH = UiTokens.S16 * 3f;
            if (m.Chart != null && bottom - y > chartH + UiTokens.S16)
            {
                UiChart.Lay(s, at, m.Chart, x, y, w, chartH);
                y += chartH + UiTokens.S4;
            }
            Rest(s, at, m, x, y, w, bottom, keys);
        }

        private static void Markets(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Lead(s, at, m, x, y, w);
            float chartH = UiTokens.S16 * 4f;
            if (m.Chart != null && bottom - y > chartH)
            {
                UiChart.Lay(s, at, m.Chart, x, y, w, chartH);
                y += chartH + UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(m.Note))
            {
                y = Note(s, at, m.Note, x, y, w);
            }
            Rest(s, at, m, x, y, w, bottom, keys);
        }

        private static void Finances(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Lead(s, at, m, x, y, w);
            float chartH = UiTokens.S16 * 4f;
            if (m.Chart != null && bottom - y > chartH)
            {
                UiChart.Lay(s, at, m.Chart, x, y, w, chartH);
            }
        }

        private static void Insights(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Lead(s, at, m, x, y, w);
            Advice(s, at, m, x, y, w, keys);
        }

        private static void Stand(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (!string.IsNullOrEmpty(m.Note))
            {
                y = Note(s, at, m.Note, x, y, w);
            }
            if (!string.IsNullOrEmpty(m.Note2))
            {
                y = Note(s, at, m.Note2, x, y, w);
            }
            if (m.Catalogue != null && m.Catalogue.CardCount > 0)
            {
                UiCatalogue.Lay(s, at, m.Catalogue, x, y, w, bottom - y, keys, out _);
                return;
            }
            Notice(s, at, m, x, y, w, bottom - y);
        }

        private static void Read(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            if (m.Reader != null)
            {
                UiReader.Lay(s, at, m.Reader, x, y, w, h - y, keys);
                return;
            }
            Notice(s, at, m, x, y, w, h - y);
        }

        private static void Paper(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Edition != null)
            {
                UiEdition.Lay(s, at, m.Edition, x, y, w, bottom - y, keys);
                return;
            }
            Notice(s, at, m, x, y, w, bottom - y);
        }

        private static void OneTable(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Lead(s, at, m, x, y, w);
            Rest(s, at, m, x, y, w, bottom, keys, m.Shape == NewsShape.Districts ? NewsCommands.District : UiDataTable.First);
        }

        private static void People(UiScene s, NewsModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (m.People == null)
            {
                s.Begin("people", frame.Width, frame.Height, dark);
                Notice(s, UiAt.Screen, m, UiTokens.S6, UiTokens.S6, frame.Width - UiTokens.S6 * 2f, frame.Height * 0.4f);
                return;
            }
            UiInfoView.Lay(s, m.People, frame, dark, keys);
            if (m.Actions == null || m.Actions.Length == 0)
            {
                return;
            }
            float x = UiTokens.S2 + 104f + UiTokens.S4;
            float y = UiTokens.S4 + UiInfoView.Height(m.People) + UiTokens.S4;
            int shown = m.Actions.Length < 2 ? m.Actions.Length : 2;
            float bh = UiTokens.S2 + shown * (UiTokens.S8 + UiTokens.S2);
            int box = UiDraw.Box(s, UiAt.Screen, x, y, 360f, bh, UiRole.Panel, UiRole.None, UiTokens.RadiusLarge, UiFlags.Panel);
            UiAt inside = UiAt.Screen.In(box);
            float ly = y + UiTokens.S2;
            for (int i = 0; i < shown; i++)
            {
                UiDraw.LinkRow(s, inside, x + UiTokens.S3, ly, 360f - UiTokens.S3 * 2f, UiTokens.S8, m.Actions[i].Label, m.Actions[i].Command, 0, false);
                ly += UiTokens.S8 + UiTokens.S2;
            }
        }

        private static float Lead(UiScene s, UiAt at, NewsModel m, float x, float y, float w)
        {
            if (!string.IsNullOrEmpty(m.Chip))
            {
                UiDraw.Chip(s, at, x, y, m.Chip, m.ChipTone);
                y += UiTokens.Badge + UiTokens.S1 + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(m.Lead))
            {
                UiDraw.Text(s, at, x, y, w, UiStyle.Title, UiRole.Text, m.Lead, UiFlags.Truncate);
                y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)) + UiTokens.S1;
            }
            y = Line(s, at, m.Body, x, y, w);
            if (m.Figures != null && m.Figures.Length > 0)
            {
                y = Figures(s, at, m.Figures, x, y, w) + UiTokens.S2;
            }
            return y;
        }

        // The three headline figures, each with the monthly change under the number. A spark stays on the chart and in the table.
        private static float Figures(UiScene s, UiAt at, UiFigureModel[] figures, float x, float y, float w)
        {
            int count = Math.Min(figures.Length, 4);
            bool change = false;
            for (int i = 0; i < count; i++)
            {
                change |= !string.IsNullOrEmpty(figures[i].Change);
            }
            float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float numberH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            float h = UiTokens.S3 * 2f + labelH + UiTokens.S1 + numberH + (change ? UiTokens.S6 : 0f);
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float slot = UiDraw.SnapDown(w / count);
            for (int i = 0; i < count; i++)
            {
                float fx = x + i * slot;
                if (i > 0)
                {
                    UiDraw.Box(s, at, fx, y + UiTokens.S3, UiTokens.Hairline, h - UiTokens.S3 * 2f, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                }
                float ix = fx + UiTokens.S4;
                float iw = slot - UiTokens.S4 * 2f;
                UiDraw.Text(s, at, ix, y + UiTokens.S3, iw, UiStyle.SectionHeader, UiRole.Text2, figures[i].Label, UiFlags.Label | UiFlags.Truncate);
                float ny = y + UiTokens.S3 + labelH + UiTokens.S1;
                UiDraw.Text(s, at, ix, ny, iw, UiStyle.Display, UiRole.Text, figures[i].Value, UiFlags.Truncate);
                if (string.IsNullOrEmpty(figures[i].Change))
                {
                    continue;
                }
                string words = figures[i].Change;
                UiRole ink = figures[i].Good ? UiRole.OkText : UiRole.DangerText;
                UiDraw.Icon(s, at, ix, ny + numberH, UiTokens.Icon, UiModels.Rises(ref figures[i]) ? UiIcon.TrendUp : UiIcon.TrendDown, ink, UiFlags.Middle);
                UiDraw.TextMiddle(s, at, ix + UiTokens.Icon + UiTokens.S1, ny + numberH, UiTokens.S6, iw - UiTokens.Icon - UiTokens.S1, UiStyle.SmallStrong, ink, words, UiFlags.Truncate);
            }
            return y + h;
        }

        // The cycle's phases and the rate rises travel with the chart, as washes and marks, not as a bar above it.
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

        private static float AdviceBlock(NewsModel m)
        {
            if (m.Advice == null || m.Advice.Length == 0)
            {
                return 0f;
            }
            float head = string.IsNullOrEmpty(m.AdviceTitle) ? 0f : UiTokens.S6;
            return head + m.Advice.Length * (UiTokens.S12 + UiTokens.S6);
        }

        private static void Bands(UiScene s, UiAt at, NewsModel m, float x, float y, float w)
        {
            if (m.Bands == null || m.Bands.Length == 0)
            {
                return;
            }
            float used = 0f;
            for (int i = 0; i < m.Bands.Length; i++)
            {
                float share = m.Bands[i].Share < 0f ? 0f : m.Bands[i].Share;
                float bw = i == m.Bands.Length - 1 ? w - used : UiDraw.SnapDown(w * share);
                if (bw < UiTokens.S2)
                {
                    continue;
                }
                UiDraw.Box(s, at, x + used, y, bw, UiTokens.S2, m.Bands[i].Tint, UiRole.None, 0f, UiFlags.Decor);
                used += bw;
            }
        }

        private static void Advice(UiScene s, UiAt at, NewsModel m, float x, float y, float w, IUiKeys keys)
        {
            if (m.Advice == null || m.Advice.Length == 0)
            {
                return;
            }
            if (!string.IsNullOrEmpty(m.AdviceTitle))
            {
                UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Muted, m.AdviceTitle, UiFlags.Label | UiFlags.Truncate);
                y += UiTokens.S6;
            }
            float row = UiTokens.S12 + UiTokens.S6;
            for (int i = 0; i < m.Advice.Length; i++)
            {
                float tw = w - UiTokens.S8;
                UiDraw.Icon(s, at, x, y + UiTokens.S1, UiTokens.Icon, m.Advice[i].Icon, UiRole.None, UiFlags.None, true);
                UiDraw.Text(s, at, x + UiTokens.S8, y, tw, UiStyle.BodyStrong, UiRole.Text, m.Advice[i].Title, UiFlags.Label | UiFlags.Truncate);
                UiDraw.Text(s, at, x + UiTokens.S8, y + UiTokens.S6, tw, UiStyle.Small, UiRole.Text2, m.Advice[i].Body, UiFlags.Truncate);
                if (!string.IsNullOrEmpty(m.Advice[i].Link))
                {
                    UiDraw.LinkRow(s, at, x + UiTokens.S8, y + UiTokens.S12, tw, UiTokens.S6, m.Advice[i].Link, NewsCommands.Link, m.Advice[i].Target, false);
                }
                y += row;
            }
        }

        private static void SplitTables(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            if (m.Table2 == null)
            {
                Rest(s, at, m, x, y, w, y + h, keys);
                return;
            }
            float gap = UiTokens.S4;
            float left = UiDraw.SnapDown((w - gap) * 0.5f);
            if (m.Table != null)
            {
                UiDataTable.Lay(s, at, m.Table, x, y, left, h, keys);
            }
            UiDataTable.Lay(s, at, m.Table2, x + left + gap, y, w - left - gap, h, keys);
        }

        private static void Rest(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys, int command = UiDataTable.First)
        {
            if (m.Table != null && bottom - y > UiTokens.S16)
            {
                UiDataTable.Lay(s, at, m.Table, x, y, w, bottom - y, keys, command);
                return;
            }
            Notice(s, at, m, x, y, w, bottom - y);
        }

        private static float Line(UiScene s, UiAt at, string text, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(text))
            {
                return y;
            }
            UiDraw.Text(s, at, x, y, w, UiStyle.Body, UiRole.Text, text, UiFlags.Truncate);
            return y + UiTokens.S6;
        }

        private static float Note(UiScene s, UiAt at, string text, float x, float y, float w)
        {
            return Line(s, at, text, x, y, w);
        }

        private static void Notice(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float h)
        {
            if (h < UiTokens.S16)
            {
                return;
            }
            UiDraw.Empty(s, at, x, y, w, h, m.EmptyTitle, m.EmptyBody);
        }
    }
}
