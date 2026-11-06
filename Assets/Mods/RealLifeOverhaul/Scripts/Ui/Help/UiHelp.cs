using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the help window shows: the game's pages down the sidebar in their categories, the search and the history at the head of the page, and the page in the reader.</summary>
    public sealed class UiHelpModel
    {
        public string Title;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int Selected = -1;
        public float NavOffset;
        public string Search;                 // the words typed, null for none
        public string SearchPlaceholder;
        public string SearchCount;            // "12 pages", while a search narrows the list; null for none
        public string BackLabel, ForwardLabel;
        public bool CanBack, CanForward;
        public bool HasPage;
        public UiReaderModel Page = new UiReaderModel();
        public string EmptyTitle, EmptyBody;  // no page matches what is typed
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;

        // Written by Lay, for the screen: the sidebar's scroll region, so the wheel over it scrolls it.
        public int NavClip = -1;
    }

    /// <summary>
    /// The game's help (F1, the HUD's info glyph, a business's requirement links) as an app window: its pages down the sidebar in their
    /// categories with each category's glyph, at the head of the page the way back and forward through the pages seen and the search,
    /// and the page itself in the reader (UiReader): paragraphs in the body's face, names in bold, a page's links in the cyan, the
    /// pages that follow it in its category at its side. Commands: <see cref="SearchCommand"/>, <see cref="BackCommand"/> and
    /// <see cref="ForwardCommand"/>; the pages answer <see cref="UiAppWindow.NavCommand"/> and the reader's own commands. Command block 9530
    /// to 9539. Game-free. Cost: a layout a change; the reader lays out the blocks in view only.
    /// </summary>
    public static class UiHelp
    {
        public const int SearchCommand = 9530, BackCommand = 9531, ForwardCommand = 9532;
        public static readonly int[] Commands =
        {
            UiAppWindow.NavCommand, UiReader.AnchorCommand, UiReader.RelatedCommand, SearchCommand, BackCommand, ForwardCommand
        };

        private const float SearchWidth = 440f;

        public static void Lay(UiScene s, UiHelpModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("help", frame.Width, frame.Height, dark);
            float offset = m.NavOffset;
            m.NavClip = s.ClipCount;   // the sidebar's clip is the first the footed window adds
            UiAt at = UiAppWindow.ShellFooted(s, frame, m.Title, UiIcon.Info, m.Nav, m.Selected, ref offset, null, 0, keys, out float x, out float y, out float w, out float bottom);
            m.NavOffset = offset;
            float b = UiTokens.Control;
            float backW = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.BackLabel, UiIcon.Back, UiAction.Count, keys);
            float forwardW = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.ForwardLabel, UiIcon.Next, UiAction.Count, keys);
            UiDraw.Button(s, at, x, y, backW, b, UiButtonKind.Secondary, m.BackLabel, UiIcon.Back, UiAction.Count, keys, BackCommand, 0, !m.CanBack);   // nowhere to go: drawn held
            UiDraw.Button(s, at, x + backW + UiTokens.S2, y, forwardW, b, UiButtonKind.Secondary, m.ForwardLabel, UiIcon.Next, UiAction.Count, keys, ForwardCommand, 0, !m.CanForward);
            float sx = x + backW + forwardW + UiTokens.S2 + UiTokens.S4;
            UiDraw.Search(s, at, sx, y, Math.Min(SearchWidth, x + w - sx), m.SearchPlaceholder, m.Search, m.SearchCount, SearchCommand);
            float top = y + b + UiTokens.S4;
            if (!m.HasPage)
            {
                UiDraw.EmptyState(s, at, x, top, w, bottom - top, UiIcon.Search, m.EmptyTitle, m.EmptyBody, null, -1, keys);
            }
            else
            {
                UiReader.Lay(s, at, m.Page, x, top, w, bottom - top, keys);
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }
    }
}
