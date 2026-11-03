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
            if (m.Bands != null && m.Bands.Length > 0)
            {
                Bands(s, at, m, x, y, w);
                y += UiTokens.S4;
            }
            float gap = UiTokens.S4;
            float left = UiDraw.SnapDown(w * 0.62f);
            float rx = x + left + gap;
            float rw = UiDraw.SnapDown(x + w - rx);
            float chartH = UiTokens.S16 * 4f;
            float block = chartH;
            if (m.Advice != null && m.Advice.Length > 0)
            {
                block = Math.Max(chartH, UiTokens.S8 + m.Advice.Length * UiTokens.S16 * 2f);
            }
            if (m.Chart != null && bottom - y > block + UiTokens.S16)
            {
                UiChart.Lay(s, at, m.Chart, x, y, left, chartH);
                Advice(s, at, m, rx, y, rw, keys);
                y += block + UiTokens.S4;
            }
            y = Line(s, at, m.RiseLine, x, y, w);
            if (m.Table != null && m.Table2 != null && bottom - y > UiTokens.S16 * 2f)
            {
                float half = UiDraw.SnapDown((w - gap) * 0.5f);
                float h = bottom - y;
                UiDataTable.Lay(s, at, m.Table, x, y, half, h, keys);
                UiDataTable.Lay(s, at, m.Table2, x + half + gap, y, w - half - gap, h, keys);
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
            Rest(s, at, m, x, y, w, bottom, keys);
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
        }

        private static float Lead(UiScene s, UiAt at, NewsModel m, float x, float y, float w)
        {
            y = Line(s, at, m.Chip, x, y, w);
            y = Line(s, at, m.Lead, x, y, w);
            y = Line(s, at, m.Body, x, y, w);
            if (m.Figures != null && m.Figures.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, m.Figures, x, y, w) + UiTokens.S4;
            }
            return y;
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
            for (int i = 0; i < m.Advice.Length; i++)
            {
                UiDraw.Icon(s, at, x, y + UiTokens.S2, UiTokens.Icon, m.Advice[i].Icon, UiRole.None, UiFlags.None, true);
                float tx = x + UiTokens.S8;
                float tw = w - UiTokens.S8;
                UiDraw.Text(s, at, tx, y, tw, UiStyle.BodyStrong, UiRole.Text, m.Advice[i].Title, UiFlags.Label | UiFlags.Truncate);
                UiDraw.Text(s, at, tx, y + UiTokens.S6, tw, UiStyle.Small, UiRole.Text2, m.Advice[i].Body, UiFlags.Truncate);
                if (!string.IsNullOrEmpty(m.Advice[i].Link))
                {
                    UiDraw.Button(s, at, tx, y + UiTokens.S12, UiTokens.S16 * 3f, UiTokens.S8, UiButtonKind.Secondary, m.Advice[i].Link, UiIcon.None,
                        UiAction.Count, keys, NewsCommands.Link, m.Advice[i].Target, false);
                }
                y += UiTokens.S16 * 2f;
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

        private static void Rest(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Table != null && bottom - y > UiTokens.S16)
            {
                UiDataTable.Lay(s, at, m.Table, x, y, w, bottom - y, keys);
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
