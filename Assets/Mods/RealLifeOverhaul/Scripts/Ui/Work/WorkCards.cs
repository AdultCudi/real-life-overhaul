using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a catalogue page lays: its chips and search, the cards, the chosen card's detail. Game-free.</summary>
    public sealed class WorkCardsModel
    {
        public UiActionModel[] Chips = new UiActionModel[0];
        public int ChosenChip;
        public string SearchHint, Query;
        public WorkRow[] Cards = new WorkRow[0];
        public int Chosen = -1;
        public float Offset;
        public int Reveal = -1;
        public WorkRow Detail;
        public UiActionModel[] Actions = new UiActionModel[0];
        public WorkAmount[] Amounts = new WorkAmount[0];
        public string EmptyTitle, EmptyBody;
        public UiIcon EmptyIcon = UiIcon.Search;
        public string SortLabel;                       // "Newest first", at the right of the tool row
        public string Why;
        public int Columns = 1, FirstList = -1;        // written by the layout: the grid's columns and its first list, to find the card a focus stands for
    }

    /// <summary>
    /// A catalogue as the mockups draw it, in two cuts: a course card (its subject's glyph on a tile with its level as a tag, name, school, a line,
    /// the next class and, under a rule, its length and fee) and a listing card (the picture or glyph of the thing, its name, its price with how far it
    /// stands from its worth, and the seller's face and name). The chosen card takes the row tone. The grid scrolls inside its frame; only the rows in
    /// view are laid out. Game-free.
    /// </summary>
    public static class WorkCards
    {
        public const float Least = 232f, Course = 260f, Listing = 228f, Tile = 112f;

        public static void Lay(UiScene s, UiAt at, WorkCardsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float panel = m.Detail != null ? WorkPanel.WidthFor(w) : 0f;
            float gap = UiTokens.S6;
            float gw = w - panel - (panel > 0f ? gap : 0f);
            float ty = y + UiTokens.Control + UiTokens.S3;
            Tools(s, at, m, x, y, gw, keys);
            if (m.Cards.Length == 0)
            {
                UiDraw.EmptyState(s, at, x, ty, gw, bottom - ty, m.EmptyIcon, m.EmptyTitle, m.EmptyBody, null, -1, keys);
            }
            else
            {
                Grid(s, at, m, x, ty, gw - UiTokens.S4, bottom - ty);
            }
            if (m.Detail != null)
            {
                WorkPanel.Lay(s, at, m.Detail, m.Actions, x + w - panel, y, panel, bottom - y, keys, m.Amounts, m.Why);
            }
        }

        private static void Tools(UiScene s, UiAt at, WorkCardsModel m, float x, float y, float w, IUiKeys keys)
        {
            float h = UiTokens.ControlSmall + UiTokens.S1, cx = x, right = x + w;
            if (!string.IsNullOrEmpty(m.SortLabel))
            {
                float sw = UiDraw.SnapDown(Math.Min(220f, UiTextRules.Width(m.SortLabel, UiStyle.Body) + UiTokens.S10 + UiTokens.S6));
                UiDraw.Choice(s, at, right - sw, y, sw, m.SortLabel, WorkCmd.Sort, 0, false);
                right -= sw + UiTokens.S2;
            }
            for (int i = 0; i < m.Chips.Length; i++)
            {
                cx += WorkTable.Chip(s, at, m.Chips[i], i == m.ChosenChip, cx, y + UiDraw.SnapDown((UiTokens.Control - h) / 2f), h) + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(m.SearchHint))
            {
                float sw = UiDraw.SnapDown(Math.Min(260f, right - cx - UiTokens.S2));
                if (sw >= 160f)
                {
                    UiDraw.Search(s, at, right - sw, y, sw, m.SearchHint, m.Query, null, WorkCmd.Search);
                }
            }
        }

        private static float CardHeight(WorkRow r) => string.IsNullOrEmpty(r.Seller) ? Course : Listing;

        private static void Grid(UiScene s, UiAt at, WorkCardsModel m, float x, float y, float w, float h)
        {
            int n = m.Cards.Length;
            int columns = Math.Max(1, (int)((w + UiTokens.S3) / (Least + UiTokens.S3)));
            float cw = UiDraw.SnapDown((w - UiTokens.S3 * (columns - 1)) / columns);
            float ch = CardHeight(m.Cards[0]), rowH = ch + UiTokens.S3;
            int rows = (n + columns - 1) / columns;
            float content = rows * rowH - UiTokens.S3, offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < n)
            {
                int r = m.Reveal / columns;
                offset = Math.Min(offset, r * rowH);
                offset = Math.Max(offset, r * rowH + ch - h);
            }
            offset = Math.Max(0f, Math.Min(offset, content - h));
            m.Offset = offset;
            m.Reveal = -1;
            short clip = s.AddClip(x, y, w + UiTokens.S4, h, offset);
            s.Clips[clip].ContentHeight = content;
            int first = Math.Max(0, (int)(offset / rowH)), last = Math.Min(rows - 1, (int)((offset + h - 0.5f) / rowH));
            short firstList = -1;
            for (int c = 0; c < columns; c++)
            {
                int inColumn = (n - c + columns - 1) / columns;
                int shownFirst = Math.Min(first, inColumn), shownLast = Math.Min(last, inColumn - 1);
                short list = s.AddList(inColumn, shownFirst, Math.Max(0, shownLast - shownFirst + 1), clip, m.Chosen % columns == c ? m.Chosen / columns : -1);
                firstList = c == 0 ? list : firstList;
            }
            m.Columns = columns;
            m.FirstList = firstList;
            _vis = offset + h;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            for (int r = first; r <= last; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int i = r * columns + c;
                    if (i >= n)
                    {
                        break;
                    }
                    CardAt(s, inClip.Arriving(i - first * columns), m, i, c * (cw + UiTokens.S3), r * rowH, cw, ch, (short)(firstList + c), r);
                }
            }
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S2, y, h, offset, content);
        }

        private static float _vis = float.MaxValue;   // the content row the grid shows to: a card's words below it are not laid (they would stand over what is below the grid)

        private static void CardAt(UiScene s, UiAt at, WorkCardsModel m, int i, float x, float y, float w, float h, short list, int row)
        {
            WorkRow r = m.Cards[i];
            bool chosen = i == m.Chosen;
            // a Field face: the kit steps it ten per cent towards the words on a hover, where the few per cent it gives a row's fill could not be seen on a card
            int face = UiDraw.Box(s, at, x, y, w, h, chosen ? UiRole.RowSel : UiRole.Field, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            if (string.IsNullOrEmpty(r.Seller))
            {
                CourseCard(s, inside, r, x, y, w, h, chosen);
            }
            else
            {
                ListingCard(s, inside, r, x, y, w, h, chosen);
            }
            UiDraw.Node(s, at, x, y, w, h, WorkCmd.Card, i, face, false, list, row);
        }

        // A course: the subject's glyph on a tile at the left, its level as a tag at the right, then name, school, a line, the next class, a rule and the foot.
        private static void CourseCard(UiScene s, UiAt at, WorkRow r, float x, float y, float w, float h, bool chosen)
        {
            UiRole quiet = chosen ? UiRole.Text : UiRole.Text2;   // the chosen card's fill is lighter: its quiet words are the main ink
            float pad = UiTokens.S4, iw = w - pad * 2f, tile = UiTokens.IconTile;
            int t = UiDraw.Tile(s, at, x + pad, y + pad, tile, UiRole.None, false);
            UiDraw.Icon(s, at.In(t), x + pad + (tile - UiTokens.Icon) / 2f, y + pad + (tile - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon, r.Tint, UiFlags.Middle | UiFlags.Centre, true);
            if (!string.IsNullOrEmpty(r.Status))
            {
                float cw = Math.Min(iw - tile - UiTokens.S3, UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(r.Status, UiStyle.SmallStrong) + UiTokens.S1));
                UiDraw.Chip(s, at, x + w - pad - cw, y + pad + (tile - UiTokens.Badge - UiTokens.S1) / 2f, r.Status, r.Tone, cw);
            }
            float cy = y + pad + tile + UiTokens.S3;
            if (y + h - pad > _vis)
            {
                return;   // a card the grid's foot cuts shows its tile and tag only: a line of words is never cut through its middle, as a listing's are not
            }
            UiDraw.Text(s, at, x + pad, cy, iw, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)) + UiTokens.S1;
            UiDraw.Text(s, at, x + pad, cy, iw, UiStyle.Small, quiet, r.Sub, UiFlags.Truncate);
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2;
            if (!string.IsNullOrEmpty(r.Line))
            {
                UiDraw.Text(s, at, x + pad, cy, iw, UiStyle.Body, UiRole.Text, FitWords(r.Line, UiStyle.Body, iw, 2), UiFlags.Wrap, 2);
            }
            float foot = y + h - pad - UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong));
            float next = UiDraw.SnapDown(foot - UiTokens.S3 * 2f - UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong)) - UiTokens.S1);
            if (!string.IsNullOrEmpty(r.NextLabel))
            {
                UiDraw.Text(s, at, x + pad, next, iw * 0.5f, UiStyle.Body, quiet, r.NextLabel, UiFlags.Truncate);
                string when = r.NextValue ?? string.Empty;
                if (UiTextRules.Width(when, UiStyle.BodyStrong) > Math.Min(iw * 0.5f, iw - UiTextRules.Width(r.NextLabel, UiStyle.Body) - UiTokens.S3) && when.LastIndexOf(' ') > 0)
                {
                    when = when.Substring(when.LastIndexOf(' ') + 1);   // the days do not fit beside the label: the hour alone is kept, never cut
                }
                UiDraw.Text(s, at, x + w - pad - iw * 0.5f, next, iw * 0.5f, UiStyle.BodyStrong, UiRole.Text, when, UiFlags.Right | UiFlags.Truncate);
            }
            UiDraw.Box(s, at, x + pad, foot - UiTokens.S3, iw, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            UiDraw.Text(s, at, x + pad, foot, iw * 0.5f, UiStyle.Body, quiet, r.FootLeft, UiFlags.Truncate);
            UiDraw.Text(s, at, x + w - pad - iw * 0.5f, foot, iw * 0.5f, UiStyle.BodyStrong, UiRole.Text, r.FootRight, UiFlags.Right | UiFlags.Truncate);
        }

        /// <summary>Whole words that fit the lines, with "..." after the last when some are left out: a word is never cut in the middle.</summary>
        public static string FitWords(string text, UiStyle style, float width, int lines)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            float limit = lines * UiDraw.Snap(UiTextRules.LineHeight(style)) + 0.5f;
            if (UiTextRules.WrappedHeight(text, style, width, lines + 1) <= limit)
            {
                return text;
            }
            if (text.IndexOf(' ') < 0)
            {
                int keep = text.Length;   // a text with no spaces (Chinese, Japanese) is cut at a character
                while (keep > 1 && UiTextRules.WrappedHeight(text.Substring(0, keep) + "...", style, width, lines + 1) > limit)
                {
                    keep--;
                }
                return text.Substring(0, keep).TrimEnd() + "...";
            }
            string[] words = text.Split(' ');
            string best = words[0];
            for (int i = 1; i < words.Length; i++)
            {
                if (UiTextRules.WrappedHeight(best + " " + words[i] + "...", style, width, lines + 1) > limit)
                {
                    break;
                }
                best += " " + words[i];
            }
            return best.TrimEnd('.', ',', ';', ':') + "...";
        }

        // The colour of a thing's glyph where the kit's own is the industry grey that a card of goods would share between a van and a machine: a van is a vehicle, as the car beside
        // it is, and a machine has its own, so no two kinds of listing read the same on the grid.
        private static UiRole ThingTint(UiIcon icon) => icon == UiIcon.Package ? UiRole.Chart5 : icon == UiIcon.Factory ? UiRole.Chart4 : UiRole.None;

        // A listing: the picture or glyph of the thing on a tile over the card's width, name, price and how far it stands from its worth, the seller.
        private static void ListingCard(UiScene s, UiAt at, WorkRow r, float x, float y, float w, float h, bool chosen)
        {
            UiRole quiet = chosen ? UiRole.Text : UiRole.Text2;
            float pad = UiTokens.S3, iw = w - pad * 2f;
            int t = UiDraw.Box(s, at, x + pad, y + pad, iw, Tile, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
            if (!string.IsNullOrEmpty(r.Picture))
            {
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Share | UiFlags.Middle, X = x + pad + (iw - Tile * 1.6f) / 2f, Y = y + pad, W = Tile * 1.6f, H = Tile, Text = r.Picture, Value = 1.6f,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = t, Alpha = 1f
                };
                s.Add(ref item);
            }
            else
            {
                UiRole glyph = r.Tint != UiRole.None ? r.Tint : ThingTint(r.Icon);
                UiDraw.Icon(s, at.In(t), x + pad + (iw - UiTokens.S12) / 2f, y + pad + (Tile - UiTokens.S12) / 2f, UiTokens.S12, r.Icon, glyph, UiFlags.Middle | UiFlags.Centre, true);
            }
            if (!string.IsNullOrEmpty(r.Status))
            {
                float cw = Math.Min(iw - UiTokens.S6, UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(r.Status, UiStyle.SmallStrong) + UiTokens.S1));
                UiDraw.Chip(s, at.In(t), x + pad + UiTokens.S2, y + pad + UiTokens.S2, r.Status, r.Tone, cw);
            }
            float cy = y + pad + Tile + UiTokens.S3;
            if (y + h - pad > _vis)
            {
                return;   // the words of a card cut by the grid's foot are left out: only its tile is seen
            }
            UiDraw.Text(s, at, x + pad + UiTokens.S1, cy, iw - UiTokens.S2, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)) + UiTokens.S1;
            float under = string.IsNullOrEmpty(r.Under) ? 0f : UiTextRules.Width(r.Under, UiStyle.BodyStrong) + UiTokens.S2;
            UiDraw.Text(s, at, x + pad + UiTokens.S1, cy, iw - under - UiTokens.S3, UiStyle.BodyStrong, UiRole.Text, r.Price, UiFlags.Truncate);
            if (under > 0f)
            {
                UiRole tone = r.UnderValue <= 0 ? UiRole.OkText : UiRole.WarnText;
                UiDraw.Text(s, at, x + w - pad - under, cy, under, UiStyle.BodyStrong, tone, r.Under, UiFlags.Right | UiFlags.Truncate);
            }
            float face = UiTokens.AvatarSmall - UiTokens.S2, fy = y + h - pad - face;
            UiDraw.Portrait(s, at, x + pad + UiTokens.S1, fy, face, r.Seller, UiColour.HueOf(r.Seller), UiRole.None);
            UiDraw.TextMiddle(s, at, x + pad + UiTokens.S1 + face + UiTokens.S2, fy, face, iw - face - UiTokens.S3, UiStyle.Small, quiet, r.Seller, UiFlags.Truncate);
        }
    }
}
