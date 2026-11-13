using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One blueprint of the page the game has loaded: its picture, its name and who made it, its rating, its size and what it says of itself.</summary>
    public struct UiBlueprintCard
    {
        public string Picture;       // the thumbnail's key (UiPictures), or null while the game has not loaded it
        public string Name;
        public string Author;        // "by kettlecraft"; null for none
        public int Halves;           // the rating in stars by halves (0 to 10); -1 for a blueprint nobody has rated (a local one)
        public string Rating;        // "4.8", beside the stars; null for none
        public string Size;          // the building's size as the game names it ("Medium2")
        public string State;         // the chip in the picture's corner ("Yours", "In your library"); null for none
        public UiRole StateTone;
        public bool Old;             // made in an older game version: the warning glyph by its size
        public string Downloads;     // "1.2k downloads", as the game's card words them; null for a local blueprint (the game's card says "no Steam info" there)
    }

    /// <summary>A row of the filters' list: a group's caption, or one option of it with whether it is on.</summary>
    public struct UiBlueprintFilterRow
    {
        public string Label;
        public bool Group;           // a caption: not a target
        public bool On;
    }

    /// <summary>What the blueprints window shows: its two tabs, the search, the sort and filters, the page's cards, the chosen blueprint's detail and its actions.</summary>
    public sealed class UiBlueprintsModel
    {
        public string Title;
        public UiNavEntry[] Tabs = new UiNavEntry[0];
        public int Tab;
        public string Search;                  // the words typed, null for none
        public string SearchPlaceholder;
        public string SearchCount;             // "3 of 16" while the search narrows the page; null for none
        public string SortLabel;               // "Sort: Top rated"
        public string[] SortOptions = new string[0];
        public int SortChosen;
        public bool SortOpen;
        public string FiltersLabel;            // "Filters" or "Filters 2"
        public int FiltersOn;
        public bool FiltersOpen;
        public UiBlueprintFilterRow[] FilterRows = new UiBlueprintFilterRow[0];
        public float FilterOffset;
        public UiBlueprintCard[] Cards = new UiBlueprintCard[0];
        public int CardCount;
        public int Chosen = -1;
        public float Offset;
        public int Reveal = -1;
        public string PageLine;                // "Page 1 of 2"
        public string PrevLabel, NextLabel;
        public bool CanPrev, CanNext;
        public bool Loading;
        public string LoadingText;
        public string EmptyTitle, EmptyBody;
        public UiIcon EmptyIcon = UiIcon.Layout;
        public string EmptyAction;             // "Clear the search"; null for none

        // The chosen blueprint, when one is.
        public bool HasDetail;
        public string DetailPicture;
        public string DetailKicker;            // what kind of building it is, in capitals
        public string DetailName;
        public string DetailAuthor;
        public int DetailHalves = -1;
        public string DetailRating;
        public string DetailState;
        public UiRole DetailStateTone;
        public string FactsTitle;
        public string[] FactLabels = new string[0];
        public string[] FactValues = new string[0];
        public string InsideTitle;
        public string[] InsideLabels = new string[0];
        public string[] InsideValues = new string[0];
        public string InsideNote;              // the placeholder while the layout loads, or why there is no list
        public string[] Warnings = new string[0];
        public UiActionModel[] Actions = new UiActionModel[0];
        public string ActionNote;              // why an action is held ("This blueprint holds something the Workshop cannot take")
        public string DetailEmpty;             // the sentence in the detail's place while none is chosen
        public bool Confirm;                   // the Workshop's terms stand over the window before an upload
        public string ConfirmTitle, ConfirmBody, ConfirmYes, ConfirmNo, ConfirmTerms;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;

        // Written by Lay, for the screen: the regions that scroll.
        public int GridClip = -1, FilterClip = -1, GridColumns = 1;
        public short GridList = -1;
    }

    /// <summary>
    /// The blueprints window of the pause menu: an app window with the Library and the Workshop across its top, a search, a sort and
    /// the filters, the page's blueprints as cards (the thumbnail, the name, who made it, the stars, the size), the page's turner at
    /// the foot, and beside them the chosen blueprint as a detail with what it holds and its actions. Commands: <see cref="SearchCommand"/>,
    /// <see cref="SortCommand"/> and <see cref="SortOptionCommand"/>, <see cref="FiltersCommand"/> and <see cref="FilterRowCommand"/>,
    /// <see cref="CardCommand"/> (a card's index), <see cref="ActionCommand"/> (a detail action's index), <see cref="PageCommand"/> (-1
    /// or 1) and <see cref="ClearCommand"/>; the tabs answer <see cref="UiAppWindow.NavCommand"/>. Command block 9520 to 9539. Game-free.
    /// Cost: a layout a change; only the cards' rows in view are laid out.
    /// </summary>
    public static class UiBlueprints
    {
        public const int SearchCommand = 9520, SortCommand = 9521, SortOptionCommand = 9522, FiltersCommand = 9523, FilterRowCommand = 9524, CardCommand = 9525,
            ActionCommand = 9526, PageCommand = 9527, ClearCommand = 9528, ConfirmCommand = 9529;
        public static readonly int[] Commands =
        {
            UiAppWindow.NavCommand, SearchCommand, SortCommand, SortOptionCommand, FiltersCommand, FilterRowCommand, CardCommand, ActionCommand, PageCommand, ClearCommand, ConfirmCommand
        };

        private const float LeastCard = 248f;       // px: a card's narrowest before the grid holds one column fewer
        private const float TileShare = 0.5f;       // of a card's width, its picture's height
        private const float DetailShare = 0.28f;    // of the page's width, the detail's
        private const float LeastDetail = 340f, MostDetail = 440f;
        private const float DetailTile = 168f;      // px: the chosen blueprint's picture, at most
        private const float SearchWidth = 280f, SortWidth = 248f, FiltersWidth = 168f;
        private const float FilterListWidth = 320f;
        private const int FilterRowsInView = 9;
        private const byte RegionBar = 10, RegionGrid = 11, RegionDetail = 12;

        // A card's foot: its name, who made it, and its stars with its size, on one grid of lines.
        private static float TitleLine => UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
        private static float SmallLine => UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
        private static float RatingLine => UiDraw.Snap(Math.Max(UiTokens.IconSmall, UiTextRules.LineHeight(UiStyle.SmallStrong)));
        private static float FootHeight => UiTokens.S3 * 2f + TitleLine + UiTokens.S1 + SmallLine + UiTokens.S2 + RatingLine;

        /// <summary>The width of the grid in a page of a width, the detail taking its share beside it.</summary>
        public static float GridWidth(float w) => w - DetailWidth(w) - UiTokens.S6;

        private static float DetailWidth(float w) => Math.Max(LeastDetail, Math.Min(MostDetail, UiDraw.SnapDown(w * DetailShare)));

        /// <summary>The cards the grid holds across in a width: the card number a list and its row stand for is <see cref="UiCatalogue.CardOf"/>.</summary>
        public static int Columns(float gridWidth) => Math.Max(1, (int)((gridWidth + UiTokens.S3) / (LeastCard + UiTokens.S3)));

        public static void Lay(UiScene s, UiBlueprintsModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("blueprints", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, UiIcon.Layout, m.Tabs, m.Tab, true, keys, out float x, out float y, out float w, out float bottom);
            at = at.InRegion(RegionBar);
            float gw = GridWidth(w), dw = DetailWidth(w), dx = x + gw + UiTokens.S6;
            // The bar over the grid: the search, the sort and the filters, and how many the search leaves at the right.
            float sw = Math.Min(SearchWidth, UiDraw.SnapDown(gw * 0.34f));
            UiDraw.Search(s, at, x, y, sw, m.SearchPlaceholder, m.Search, null, SearchCommand);
            float fx = x + sw + UiTokens.S3;
            int sort = UiDraw.Choice(s, at, fx, y, SortWidth, m.SortLabel, SortCommand, 0, false);
            float bx = fx + SortWidth + UiTokens.S3;
            UiButtonKind filtersKind = m.FiltersOn > 0 || m.FiltersOpen ? UiButtonKind.Chosen : UiButtonKind.Secondary;
            UiDraw.Button(s, at, bx, y, FiltersWidth, UiTokens.Control, filtersKind, m.FiltersLabel, UiIcon.Funnel, UiAction.Count, keys, FiltersCommand, 0);
            if (!string.IsNullOrEmpty(m.SearchCount))
            {
                float cw = UiDraw.Snap(UiTextRules.Width(m.SearchCount, UiStyle.Small) + UiTokens.S1);
                UiDraw.TextMiddle(s, at, x + gw - cw, y, UiTokens.Control, cw, UiStyle.Small, UiRole.Muted, m.SearchCount, UiFlags.Right);
            }
            float top = y + UiTokens.Control + UiTokens.S4;
            bool paging = m.CanPrev || m.CanNext || !string.IsNullOrEmpty(m.PageLine) && m.CardCount > 0;
            float foot = paging ? UiTokens.S3 + UiTokens.ControlSmall : 0f;
            float gridH = UiDraw.SnapDown(bottom - top - foot);
            m.GridClip = -1;
            m.GridColumns = Columns(gw);
            if (m.Loading)
            {
                UiDraw.Busy(s, at.InRegion(RegionGrid), x, top, UiDraw.SnapDown(gw / 8f) * 8f, gridH, m.LoadingText);
            }
            else if (m.CardCount == 0)
            {
                UiDraw.EmptyState(s, at.InRegion(RegionGrid), x, top, gw, gridH, m.EmptyIcon, m.EmptyTitle, m.EmptyBody, m.EmptyAction, ClearCommand, keys);
                m.Offset = 0f;
            }
            else
            {
                m.GridList = Grid(s, at.InRegion(RegionGrid), m, x, top, gw, gridH, m.GridColumns);
            }
            if (paging)
            {
                int before = s.NodeCount;
                Paging(s, at.InRegion(RegionGrid), m, x, bottom - UiTokens.ControlSmall, gw, keys);
                JoinPaging(s, m, before);
            }
            Detail(s, at.InRegion(RegionDetail), m, dx, y, dw, bottom - y, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
            m.FilterClip = -1;
            if (m.Confirm)
            {
                ConfirmCard(s, m, frame, keys);
            }
            else if (m.SortOpen)
            {
                UiDraw.ChoiceList(s, UiAt.Screen, fx, y + UiTokens.Control + UiTokens.S1, SortWidth, m.SortOptions, m.SortOptions.Length, m.SortChosen, 0f, SortOptionCommand);
            }
            else if (m.FiltersOpen)
            {
                m.FilterClip = FilterList(s, m, Math.Min(bx, x + w - FilterListWidth), y + UiTokens.Control + UiTokens.S1);
            }
        }

        // ------------------------------------------------------------------------------------------------- the cards

        private static short Grid(UiScene s, UiAt at, UiBlueprintsModel m, float x, float y, float w, float h, int columns)
        {
            float cw = UiDraw.SnapDown((w - UiTokens.S3 * (columns - 1)) / columns);
            int rows = (m.CardCount + columns - 1) / columns;
            float th = UiDraw.SnapDown(cw * TileShare), ch = th + FootHeight, rowH = ch + UiTokens.S3;
            float content = rows * rowH - UiTokens.S3;
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < m.CardCount)
            {
                int r = m.Reveal / columns;
                offset = Math.Min(offset, r * rowH);
                offset = Math.Max(offset, r * rowH + ch - h);
            }
            offset = Math.Max(0f, Math.Min(offset, content - h));
            m.Offset = offset;
            m.Reveal = -1;
            short clip = s.AddClip(x, y, w, h, offset);
            s.Clips[clip].ContentHeight = content;
            m.GridClip = clip;
            int first = Math.Max(0, (int)(offset / rowH)), last = Math.Min(rows - 1, (int)((offset + h - 0.5f) / rowH));
            short firstList = -1;
            for (int c = 0; c < columns; c++)
            {
                int inColumn = (m.CardCount - c + columns - 1) / columns;
                int shownFirst = Math.Min(first, inColumn), shownLast = Math.Min(last, inColumn - 1);
                short list = s.AddList(inColumn, shownFirst, Math.Max(0, shownLast - shownFirst + 1), clip, m.Chosen >= 0 && m.Chosen % columns == c ? m.Chosen / columns : -1);
                firstList = c == 0 ? list : firstList;
            }
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            for (int r = first; r <= last; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int i = r * columns + c;
                    if (i >= m.CardCount)
                    {
                        break;
                    }
                    Card(s, inClip.Arriving(i - first * columns), ref m.Cards[i], i, i == m.Chosen, c * (cw + UiTokens.S3), r * rowH, cw, ch, th, (short)(firstList + c), r);
                }
            }
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S2, y, h, offset, content);
            return firstList;
        }

        // A card: its picture on its slate tile with the state chip in the corner, then the foot: the name, who made it, the stars and the size.
        private static void Card(UiScene s, UiAt at, ref UiBlueprintCard c, int index, bool chosen, float x, float y, float w, float h, float th, short list, int row)
        {
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            int tile = UiDraw.Box(s, inside, x, y, w, th, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
            Picture(s, inside.In(tile), c.Picture, x, y, w, th, UiTokens.S12);
            if (!string.IsNullOrEmpty(c.State))
            {
                float cw = Math.Min(UiDraw.SnapDown(w - UiTokens.S4 * 2f), UiDraw.ChipWidth(c.State));
                int over = s.ItemCount;
                UiDraw.Chip(s, inside.In(tile), x + UiTokens.S3, y + UiTokens.S3, c.State, c.StateTone, cw);
                Over(s, over);
            }
            float fy = y + th, fh = h - th;
            int foot = UiDraw.Box(s, inside, x, fy, w, fh, chosen ? UiRole.Accent : UiRole.None, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt onFoot = inside.In(foot);
            UiRole ink = chosen ? UiRole.OnAccent : UiRole.Text, soft = chosen ? UiRole.OnAccent : UiRole.Text2;
            float ix = x + UiTokens.S3, iw = w - UiTokens.S3 * 2f, ty = fy + UiTokens.S3;
            UiDraw.Text(s, onFoot, ix, ty, iw, UiStyle.Title, ink, c.Name, UiFlags.Truncate);
            ty += TitleLine + UiTokens.S1;
            float downW = string.IsNullOrEmpty(c.Downloads) ? 0f : Math.Min(UiDraw.SnapDown(iw * 0.6f), UiDraw.Snap(UiTextRules.Width(c.Downloads, UiStyle.Small) + UiTokens.S1));
            if (!string.IsNullOrEmpty(c.Author))
            {
                UiDraw.Text(s, onFoot, ix, ty, Math.Max(0f, iw - downW - (downW > 0f ? UiTokens.S2 : 0f)), UiStyle.Small, soft, c.Author, UiFlags.Truncate);
            }
            if (downW > 0f)
            {
                UiDraw.Text(s, onFoot, ix + iw - downW, ty, downW, UiStyle.Small, chosen ? UiRole.OnAccent : UiRole.Muted, c.Downloads, UiFlags.Right | UiFlags.Truncate);
            }
            ty += SmallLine + UiTokens.S2;
            float line = RatingLine;
            float sizeW = string.IsNullOrEmpty(c.Size) ? 0f : Math.Min(UiDraw.SnapDown(iw / 2f), UiDraw.Snap(UiTextRules.Width(c.Size, UiStyle.Small) + UiTokens.S1));
            float left = ix;
            if (c.Halves >= 0)
            {
                // The cyan of a chosen card would drown the gold of the stars: it keeps one star in the dark ink beside the figure.
                float star = chosen ? UiTokens.IconSmall : UiDraw.Stars(s, onFoot, left, ty + (line - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, c.Halves);
                if (chosen)
                {
                    UiDraw.Icon(s, onFoot, left, ty + (line - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Star, UiRole.OnAccent, UiFlags.Middle);
                }
                left += star + UiTokens.S2;
                if (!string.IsNullOrEmpty(c.Rating))
                {
                    float rw = UiDraw.Snap(UiTextRules.Width(c.Rating, UiStyle.SmallStrong) + UiTokens.S1);
                    UiDraw.TextMiddle(s, onFoot, left, ty, line, rw, UiStyle.SmallStrong, ink, c.Rating, UiFlags.None);
                }
            }
            if (sizeW > 0f)
            {
                UiDraw.TextMiddle(s, onFoot, ix + iw - sizeW, ty, line, sizeW, UiStyle.Small, chosen ? UiRole.OnAccent : UiRole.Muted, c.Size, UiFlags.Right | UiFlags.Truncate);
                if (c.Old)
                {
                    float gx = ix + iw - sizeW - UiTokens.S1 - UiTokens.IconSmall;
                    UiDraw.Icon(s, onFoot, gx, ty + (line - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Warning, chosen ? UiRole.OnAccent : UiRole.WarnText, UiFlags.Middle);
                }
            }
            UiDraw.Node(s, at, x, y, w, h, CardCommand, index, face, false, list, row);
        }

        // The last row of cards can stand partly out of its frame, where the d-pad finds nothing below it by place: Down from it goes to the page's turner.
        private static void JoinPaging(UiScene s, UiBlueprintsModel m, int from)
        {
            int turner = -1;
            for (int i = from; i < s.NodeCount && turner < 0; i++)
            {
                turner = s.Nodes[i].Command == PageCommand && !s.Nodes[i].Disabled ? i : -1;
            }
            if (turner < 0)
            {
                return;
            }
            int last = (m.CardCount - 1) / Math.Max(1, m.GridColumns);
            for (int i = 0; i < from; i++)
            {
                if (s.Nodes[i].Command == CardCommand && s.Nodes[i].Arg / Math.Max(1, m.GridColumns) == last)
                {
                    s.Nodes[i].Down = turner;
                }
            }
        }

        // A blueprint's picture filling its tile: cut to the tile's shape, never stretched; its own glyph while the game has not loaded it.
        private static void Picture(UiScene s, UiAt at, string key, float x, float y, float w, float h, float glyph)
        {
            if (!string.IsNullOrEmpty(key))
            {
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Text = key, Value = 1f, X = x, Y = y, W = w, H = h, Flags = UiFlags.Share | UiFlags.Middle | UiFlags.Centre | UiFlags.Cover,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
                };
                s.Add(ref item);
                return;
            }
            UiDraw.Icon(s, at, x + UiDraw.SnapDown((w - glyph) / 2f), y + UiDraw.SnapDown((h - glyph) / 2f), glyph, UiIcon.Layout, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
        }

        // A chip over a picture's corner lies across its tile on purpose.
        private static void Over(UiScene s, int from)
        {
            for (int k = from; k < s.ItemCount; k++)
            {
                s.Items[k].Flags |= UiFlags.Overflow;
            }
        }

        // The page's turner at the grid's foot: back, the page's words, next; a button the game's own page does not offer is drawn held.
        private static void Paging(UiScene s, UiAt at, UiBlueprintsModel m, float x, float y, float w, IUiKeys keys)
        {
            float bh = UiTokens.ControlSmall;
            float pw = UiDraw.Snap(UiTextRules.Width(m.PageLine ?? string.Empty, UiStyle.Body) + UiTokens.S2);
            float backW = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.PrevLabel, UiIcon.Back, UiAction.Count, keys);
            float nextW = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.NextLabel, UiIcon.Next, UiAction.Count, keys);
            float total = backW + UiTokens.S4 + pw + UiTokens.S4 + nextW;
            float px = x + UiDraw.SnapDown((w - total) / 2f);
            UiDraw.Button(s, at, px, y, backW, bh, UiButtonKind.Secondary, m.PrevLabel, UiIcon.Back, UiAction.Count, keys, PageCommand, -1, !m.CanPrev);
            UiDraw.TextMiddle(s, at, px + backW + UiTokens.S4, y, bh, pw, UiStyle.Body, UiRole.Text2, m.PageLine, UiFlags.Centre);
            UiDraw.Button(s, at, px + backW + UiTokens.S4 + pw + UiTokens.S4, y, nextW, bh, UiButtonKind.Secondary, m.NextLabel, UiIcon.Next, UiAction.Count, keys, PageCommand, 1, !m.CanNext);
        }

        // ------------------------------------------------------------------------------------------------- the Workshop's terms

        /// <summary>The Workshop's terms before an upload: a small card over the window dimmed, what the game says, and upload, cancel and read the terms.</summary>
        private static void ConfirmCard(UiScene s, UiBlueprintsModel m, UiFrame frame, IUiKeys keys)
        {
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            var at = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1, Region = 30 };
            UiDraw.Box(s, at, wx, wy, ww, wh, UiRole.Scrim, UiRole.None, UiTokens.RadiusPanel, UiFlags.Decor);
            float cw = UiDraw.SnapDown(Math.Min(ww - UiTokens.S8 * 2f, 520f)), inner = cw - UiTokens.S5 * 2f;
            float th = string.IsNullOrEmpty(m.ConfirmTitle) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(m.ConfirmTitle, UiStyle.Title, inner, 2));
            float bh = string.IsNullOrEmpty(m.ConfirmBody) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(m.ConfirmBody, UiStyle.Body, inner, 8));
            float h = UiTokens.S5 + th + (th > 0f ? UiTokens.S3 : 0f) + bh + UiTokens.S5 + UiTokens.Control + UiTokens.S5;
            float x = wx + UiDraw.SnapDown((ww - cw) / 2f), y = wy + UiDraw.SnapDown((wh - h) / 2f);
            int card = UiDraw.Box(s, at, x, y, cw, h, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(card);
            float ty = y + UiTokens.S5, ix = x + UiTokens.S5;
            if (th > 0f)
            {
                UiDraw.Text(s, on, ix, ty, inner, UiStyle.Title, UiRole.Text, m.ConfirmTitle, UiFlags.Wrap | UiFlags.Truncate, 2);
                ty += th + UiTokens.S3;
            }
            if (bh > 0f)
            {
                UiDraw.Text(s, on, ix, ty, inner, UiStyle.Body, UiRole.Text2, m.ConfirmBody, UiFlags.Wrap | UiFlags.Truncate, 8);
                ty += bh;
            }
            ty += UiTokens.S5;
            float yes = Math.Min(inner, UiDraw.ButtonWidth(UiButtonKind.Primary, m.ConfirmYes, UiIcon.Upload, UiAction.Alt, keys));
            float no = Math.Min(inner, UiDraw.ButtonWidth(UiButtonKind.Secondary, m.ConfirmNo, UiIcon.None, UiAction.Count, keys));
            float terms = Math.Min(inner, UiDraw.ButtonWidth(UiButtonKind.Quiet, m.ConfirmTerms, UiIcon.None, UiAction.Count, keys));
            float right = ix + inner;
            UiDraw.Button(s, on, right - yes, ty, yes, UiTokens.Control, UiButtonKind.Primary, m.ConfirmYes, UiIcon.Upload, UiAction.Alt, keys, ConfirmCommand, 0);
            right -= yes + UiTokens.S3;
            UiDraw.Button(s, on, right - no, ty, no, UiTokens.Control, UiButtonKind.Secondary, m.ConfirmNo, UiIcon.None, UiAction.Count, keys, ConfirmCommand, 1);
            if (ix + terms <= right - no - UiTokens.S3)
            {
                UiDraw.Button(s, on, ix, ty, terms, UiTokens.Control, UiButtonKind.Quiet, m.ConfirmTerms, UiIcon.None, UiAction.Count, keys, ConfirmCommand, 2);
            }
        }

        // ------------------------------------------------------------------------------------------------- the filters' list

        /// <summary>The filters' list: each group's caption over its options, each option a tick box and its words, nine rows in view and the rest scrolling.</summary>
        private static int FilterList(UiScene s, UiBlueprintsModel m, float x, float y)
        {
            var at = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1, Region = 20 };
            int count = m.FilterRows.Length;
            float rowH = UiTokens.ControlSmall, h = Math.Min(Math.Max(1, count), FilterRowsInView) * rowH + UiTokens.S1 * 2f, w = FilterListWidth;
            int card = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            float viewH = h - UiTokens.S1 * 2f;
            float offset = s.Choices.Open(UiChoiceScroll.Key(FilterRowCommand, null, count), m.FilterOffset, -1, rowH, viewH, count * rowH);   // the host scrolls it, as an open drop-down list
            m.FilterOffset = offset;
            short clip = s.AddClip(x + UiTokens.S1, y + UiTokens.S1, w - UiTokens.S2, viewH, offset);
            s.Clips[clip].Choice = true;
            s.Clips[clip].ContentHeight = count * rowH;
            int first = (int)(offset / rowH), last = Math.Min(count - 1, (int)((offset + viewH - 0.5f) / rowH));
            short list = s.AddList(count, first, Math.Max(0, last - first + 1), clip, -1);
            var rows = new UiAt { Parent = -1, Clip = clip, Layer = 1, Order = -1, Region = 20 };
            float rw = w - UiTokens.S2 - (count > FilterRowsInView ? UiTokens.S4 : 0f);
            for (int i = first; i <= last; i++)
            {
                ref UiBlueprintFilterRow row = ref m.FilterRows[i];
                float ry = i * rowH;
                if (row.Group)
                {
                    int caption = UiDraw.Box(s, rows, 0f, ry, rw, rowH, UiRole.Panel, UiRole.None, UiTokens.RadiusMedium);
                    UiDraw.TextMiddle(s, rows.In(caption), UiTokens.S3, ry, rowH, rw - UiTokens.S6, UiStyle.SectionHeader, UiRole.Muted, row.Label, UiFlags.Label | UiFlags.Truncate);
                    continue;
                }
                int face = UiDraw.Box(s, rows, 0f, ry, rw, rowH, row.On ? UiRole.RowSel : UiRole.Panel, UiRole.None, UiTokens.RadiusMedium, row.On ? UiFlags.Selected : UiFlags.None);
                UiAt inside = rows.In(face);
                float tick = UiTokens.Icon;
                if (row.On)
                {
                    UiDraw.Icon(s, inside, UiTokens.S3, ry + (rowH - tick) / 2f, tick, UiIcon.Check, UiRole.AccentText, UiFlags.Middle);
                }
                UiDraw.TextMiddle(s, inside, UiTokens.S3 + tick + UiTokens.S2, ry, rowH, rw - UiTokens.S3 * 2f - tick - UiTokens.S2, UiStyle.Body, UiRole.Text, row.Label, UiFlags.Truncate);
                UiDraw.Node(s, rows, 0f, ry, rw, rowH, FilterRowCommand, i, face, false, list, i);
            }
            UiDraw.ScrollThumb(s, at.In(card), x + w - UiTokens.S2, y + UiTokens.S1, viewH, offset, count * rowH);
            return clip;
        }

        // ------------------------------------------------------------------------------------------------- the detail

        private static void Detail(UiScene s, UiAt at, UiBlueprintsModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            if (!m.HasDetail)
            {
                int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                if (!string.IsNullOrEmpty(m.DetailEmpty) && m.CardCount > 0)
                {
                    UiDraw.EmptyState(s, at.In(band), x, y, w, h, UiIcon.Layout, null, m.DetailEmpty, null, -1, keys);
                }
                return;
            }
            // What the foot takes: the actions (the first across, the rest in pairs) and the note under them.
            int n = m.Actions.Length, pairRows = n > 1 ? (n - 1 + 1) / 2 : 0;
            float step = UiTokens.Control + UiTokens.S1;
            float actionsH = n == 0 ? 0f : UiTokens.Control + pairRows * step;
            float noteH = string.IsNullOrEmpty(m.ActionNote) ? 0f : UiTokens.S2 + UiDraw.Snap(UiTextRules.WrappedHeight(m.ActionNote, UiStyle.Small, w, 2));
            float footH = actionsH + noteH;
            float kickH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1;
            float headH = kickH + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S3;
            int facts = Math.Min(m.FactLabels.Length, m.FactValues.Length), inside = Math.Min(m.InsideLabels.Length, m.InsideValues.Length);
            float factsH = facts == 0 ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + facts * UiTokens.PairRow + UiTokens.S3;
            float warnH = m.Warnings.Length * (UiTokens.PairRow + UiTokens.S1);
            float room = h - footH - (footH > 0f ? UiTokens.S4 : 0f);
            // The picture takes what the words and the actions leave of the column, at most its own height.
            float rest = headH + factsH + warnH;
            float tileH = Math.Min(DetailTile, UiDraw.SnapDown(room - rest - UiTokens.S4));
            if (tileH < UiTokens.S12)
            {
                tileH = 0f;
            }
            else
            {
                int tile = UiDraw.Box(s, at, x, y, w, tileH, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
                Picture(s, at.In(tile), m.DetailPicture, x, y, w, tileH, UiTokens.S16);
                if (!string.IsNullOrEmpty(m.DetailState))
                {
                    int over = s.ItemCount;
                    UiDraw.Chip(s, at.In(tile), x + UiTokens.S3, y + UiTokens.S3, m.DetailState, m.DetailStateTone, w - UiTokens.S6);
                    Over(s, over);
                }
            }
            float ty = y + tileH + (tileH > 0f ? UiTokens.S4 : 0f);
            if (!string.IsNullOrEmpty(m.DetailKicker))
            {
                UiDraw.Text(s, at, x, ty, w, UiStyle.SectionHeader, UiRole.Text2, m.DetailKicker, UiFlags.Label | UiFlags.Truncate);
            }
            ty += kickH;
            UiDraw.Text(s, at, x, ty, w, UiStyle.Display, UiRole.Text, m.DetailName, UiFlags.Truncate);
            ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            float line = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)), lx = x;
            if (m.DetailHalves >= 0)
            {
                lx += UiDraw.Stars(s, at, x, ty + (line - UiTokens.Icon) / 2f, UiTokens.Icon, m.DetailHalves) + UiTokens.S2;
                if (!string.IsNullOrEmpty(m.DetailRating))
                {
                    float rw = UiDraw.Snap(UiTextRules.Width(m.DetailRating, UiStyle.BodyStrong) + UiTokens.S1);
                    UiDraw.TextMiddle(s, at, lx, ty, line, rw, UiStyle.BodyStrong, UiRole.Text, m.DetailRating, UiFlags.None);
                    lx += rw + UiTokens.S3;
                }
            }
            if (!string.IsNullOrEmpty(m.DetailAuthor))
            {
                UiDraw.TextMiddle(s, at, lx, ty, line, Math.Max(0f, x + w - lx), UiStyle.Body, UiRole.Text2, m.DetailAuthor, UiFlags.Truncate);
            }
            ty += line + UiTokens.S3;
            float limit = y + h - footH - (footH > 0f ? UiTokens.S4 : 0f);
            if (facts > 0)
            {
                UiDraw.Text(s, at, x, ty, w, UiStyle.SectionHeader, UiRole.Muted, m.FactsTitle, UiFlags.Label | UiFlags.Truncate);
                ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1;
                for (int i = 0; i < facts; i++)
                {
                    ty += UiDraw.InfoRow(s, at, x, ty, w, m.FactLabels[i], m.FactValues[i]);
                }
                ty += UiTokens.S3;
            }
            // Inside: only the lines that fit between the facts and the warnings are laid out.
            if (inside > 0 || !string.IsNullOrEmpty(m.InsideNote))
            {
                float head = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1;
                float left = limit - ty - warnH - head;
                int fit = Math.Max(0, Math.Min(inside, (int)(left / UiTokens.PairRow)));
                if (fit > 0 || (!string.IsNullOrEmpty(m.InsideNote) && left >= UiTokens.PairRow))
                {
                    UiDraw.Text(s, at, x, ty, w, UiStyle.SectionHeader, UiRole.Muted, m.InsideTitle, UiFlags.Label | UiFlags.Truncate);
                    ty += head;
                    for (int i = 0; i < fit; i++)
                    {
                        ty += UiDraw.InfoRow(s, at, x, ty, w, m.InsideLabels[i], m.InsideValues[i]);
                    }
                    if (fit == 0)
                    {
                        UiDraw.Text(s, at, x, ty, w, UiStyle.Body, UiRole.Muted, m.InsideNote, UiFlags.Truncate);
                        ty += UiTokens.PairRow;
                    }
                    ty += UiTokens.S3;
                }
            }
            for (int i = 0; i < m.Warnings.Length; i++)
            {
                UiDraw.Icon(s, at, x, ty + (UiTokens.PairRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Warning, UiRole.WarnText, UiFlags.Middle);
                float wx = x + UiTokens.IconSmall + UiTokens.S2;
                UiDraw.TextMiddle(s, at, wx, ty, UiTokens.PairRow, x + w - wx, UiStyle.Small, UiRole.WarnText, m.Warnings[i], UiFlags.Truncate);
                ty += UiTokens.PairRow + UiTokens.S1;
            }
            Actions(s, at, m, x, y + h - footH, w, actionsH, noteH, step, keys);
        }

        // The actions at the foot: the first across the width, the others in pairs below it, each its own press through the game's button; a note under them.
        private static void Actions(UiScene s, UiAt at, UiBlueprintsModel m, float x, float top, float w, float actionsH, float noteH, float step, IUiKeys keys)
        {
            int n = m.Actions.Length;
            if (n == 0)
            {
                return;
            }
            ref UiActionModel first = ref m.Actions[0];
            UiDraw.Button(s, at, x, top, w, UiTokens.Control, first.Kind, first.Label, first.Icon, first.Key, keys, ActionCommand, 0, first.Disabled);
            float cw = UiDraw.SnapDown((w - UiTokens.S3) / 2f);
            for (int i = 1; i < n; i++)
            {
                ref UiActionModel a = ref m.Actions[i];
                int slot = i - 1, row = slot / 2, col = slot % 2;
                bool alone = col == 0 && i == n - 1;   // an odd one out takes its row's width
                UiDraw.Button(s, at, alone ? x : x + col * (cw + UiTokens.S3), top + step * (row + 1), alone ? w : cw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys,
                    ActionCommand, i, a.Disabled);
            }
            if (noteH > 0f)
            {
                UiDraw.Text(s, at, x, top + actionsH + UiTokens.S2, w, UiStyle.Small, UiRole.Muted, m.ActionNote, UiFlags.Wrap | UiFlags.Label, 2);
            }
        }
    }
}
