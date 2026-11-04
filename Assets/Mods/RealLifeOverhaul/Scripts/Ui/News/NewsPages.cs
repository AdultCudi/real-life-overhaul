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
            if (m.Shape == NewsShape.Ending)
            {
                UiDecision.Lay(s, m.Decision, frame, dark, keys);
                return;
            }
            s.Begin(m.Title ?? "news", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Nav, m.Tab, m.Across, keys, out float x, out float y, out float w, out float bottom);
            StripNote(s, at, frame, m.StripNote);
            if (!string.IsNullOrEmpty(m.BackLabel))
            {
                y += BackRow(s, at, m, x, y, w, keys) + UiTokens.S3;
            }
            switch (m.Shape)
            {
                case NewsShape.Overview: Overview(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Prices: Prices(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Jobs: Jobs(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Districts: Districts(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Markets: Markets(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Finances: Finances(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Insights: Insights(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Stand: Stand(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Reading: Read(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Paper:
                case NewsShape.PaperMarkets: Paper(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.PaperPrices: Shares(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Renters: OneTable(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.People: People(s, at, m, x, y, w, bottom, keys); break;
                case NewsShape.Household: Household(s, at, m, x, y, w, bottom); break;
                default: Notice(s, at, m, x, y, w, bottom - y); break;
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
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

        private static void Shares(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (!string.IsNullOrEmpty(m.Note))
            {
                UiDraw.Text(s, at, x, y, w, UiStyle.Small, UiRole.Muted, m.Note, UiFlags.Truncate);
                y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2;
            }
            Rest(s, at, m, x, y, w, bottom, keys);
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

        private static void OneTable(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            Rest(s, at, m, x, y, w, bottom, keys);
        }

        private static void Districts(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float band = LeadBand(s, at, m, x, y, w);
            y += band + UiTokens.S3;
            if (m.Table != null && bottom - y > UiTokens.S16)
            {
                UiAt inside = Card(s, at, x, y, w, bottom - y, m.TableTitle, null, out float cy);
                UiDataTable.Lay(s, inside, m.Table, x + UiTokens.S2, cy, w - UiTokens.S4, bottom - UiTokens.S2 - cy, keys, NewsCommands.District);
                return;
            }
            Notice(s, at, m, x, y, w, bottom - y);
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
