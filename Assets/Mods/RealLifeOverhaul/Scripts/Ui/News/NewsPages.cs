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

        // Reading: the papers to pick, with the printed page of the Ledger's edition at the right, then the days of the paper open, a quiet line, and the
        // paper itself in the reader. A paper that is not held, or nothing held at all, shows what to do in the room of the reader, never a blank page.
        private static void Read(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float printed = string.IsNullOrEmpty(m.ReadPrinted.Label) ? 0f : UiDraw.ButtonWidth(m.ReadPrinted.Kind, m.ReadPrinted.Label, UiIcon.None, UiAction.Count, keys) + UiTokens.S1;
            y = ChipRow(s, at, m.ReadTitles, x, y, w - (printed > 0f ? printed + UiTokens.S3 : 0f), keys);
            if (printed > 0f)
            {
                UiDraw.Button(s, at, x + w - printed, y - UiTokens.PairRow - UiTokens.S2, printed, UiTokens.PairRow, m.ReadPrinted.Kind, m.ReadPrinted.Label, UiIcon.None, UiAction.Count, keys,
                    m.ReadPrinted.Command, m.ReadPrinted.Arg, false);
            }
            y = ChipRow(s, at, m.ReadDays, x, y, w, keys);
            if (!string.IsNullOrEmpty(m.ReadNote))
            {
                UiDraw.Text(s, at, x, y, w, UiStyle.Small, UiRole.Text2, m.ReadNote, UiFlags.Truncate);
                y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2;
            }
            if (m.Reader != null)
            {
                UiReader.Lay(s, at, m.Reader, x, y, w, h - y, keys);
                return;
            }
            if (h - y >= UiTokens.S16)
            {
                UiDraw.EmptyState(s, at, x, y, w, h - y, m.EmptyIcon, m.EmptyTitle, m.EmptyBody, m.EmptyAction, NewsCommands.ToTitles, keys);
            }
        }

        // A row of chips from the left, as many as the width holds. Returns the y under it, or the same y for none.
        private static float ChipRow(UiScene s, UiAt at, UiActionModel[] chips, float x, float y, float w, IUiKeys keys)
        {
            if (chips == null || chips.Length == 0)
            {
                return y;
            }
            float cx = x;
            for (int i = 0; i < chips.Length; i++)
            {
                float bw = UiDraw.ButtonWidth(chips[i].Kind, chips[i].Label, UiIcon.None, UiAction.Count, keys) + UiTokens.S1;   // a rounding down must not wrap the words
                if (cx + bw > x + w)
                {
                    break;
                }
                UiDraw.Button(s, at, cx, y, bw, UiTokens.PairRow, chips[i].Kind, chips[i].Label, UiIcon.None, UiAction.Count, keys, chips[i].Command, chips[i].Arg, false);
                cx += bw + UiTokens.S1;
            }
            return y + UiTokens.PairRow + UiTokens.S2;
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
                // Your own businesses and buildings stand under the districts, each in a table of its own; the districts keep what they need, the places the rest.
                float gap = UiTokens.S3;
                int places = m.Boards == null ? 0 : m.Boards.Length;
                float room = bottom - y;
                float placesH = 0f;
                for (int i = 0; i < places; i++)
                {
                    placesH = Math.Max(placesH, TableNeed(m.Boards[i], 3));
                }
                placesH = places > 0 ? Math.Max(Math.Min(placesH, UiDraw.SnapDown(room * 0.5f)), UiTokens.S16 * 3f) : 0f;
                float mainH = UiDraw.SnapDown(Math.Min(TableNeed(m.Table, 4), room - (places > 0 ? placesH + gap : 0f)));
                mainH = Math.Max(mainH, Math.Min(room, UiTokens.S16 * 3f));
                if (places > 0)
                {
                    placesH = room - mainH - gap;   // what the districts leave goes to your places
                }
                UiAt inside = Card(s, at, x, y, w, mainH, m.TableTitle, null, out float cy);
                UiDataTable.Lay(s, inside, m.Table, x + UiTokens.S2, cy, w - UiTokens.S4, y + mainH - UiTokens.S2 - cy, keys, NewsCommands.District);
                if (places > 0 && placesH > UiTokens.S16 * 2f)
                {
                    float py = y + mainH + gap;
                    float cw = UiDraw.SnapDown((w - gap * (places - 1)) / places);
                    for (int i = 0; i < places; i++)
                    {
                        float px = x + i * (cw + gap);
                        TableCard(s, at, m.Boards[i], m.BoardTitles != null && i < m.BoardTitles.Length ? m.BoardTitles[i] : null, px, py, i == places - 1 ? x + w - px : cw,
                            bottom - py, keys, UiDataTable.First);
                    }
                }
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
