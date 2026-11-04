using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A card of a catalogue: its picture or glyph, its name, its price or key figure, a state chip, and whether it is the one chosen.</summary>
    public struct UiCatalogueCard
    {
        public string Picture;       // a picture's key the game resolves (UiPictures), or null for the glyph
        public float Aspect;         // the picture's width over its height, so its tile letterboxes it and never stretches it
        public UiIcon Icon;          // the glyph when there is no picture, in its own colour
        public string Name;
        public string Figure;        // the price as the game writes it, or the figure that matters
        public string State;         // "Owned", "Sold out", "Locked", or null
        public UiRole StateTone;
        public bool Disabled;        // cannot be had now (sold out, locked): still shown, never pressable
    }

    /// <summary>
    /// A catalogue: cards of goods or choices in a fixed grid that scrolls inside its frame (a store's shelves, the start
    /// of a business, furniture, vehicles, photos, buildings), and the chosen card's detail beside them.
    /// </summary>
    public sealed class UiCatalogueModel
    {
        public UiCatalogueCard[] Cards = new UiCatalogueCard[0];
        public int CardCount;
        public int Chosen = -1;
        public float Offset;
        public int Reveal = -1;
        public string DetailKicker;                 // what the chosen thing is ("Equipment")
        public string[] DetailLabels = new string[0];
        public string[] DetailValues = new string[0];
        public string DetailNote;                   // a sentence on the chosen thing
        public UiActionModel[] Actions = new UiActionModel[0];   // the detail's actions, the last the main one
        public string EmptyTitle;
        public string EmptyBody;
        public UiIcon EmptyIcon = UiIcon.ShoppingCart;
    }

    /// <summary>
    /// A catalogue, a block of an app window's page: cards of goods or choices in a fixed grid that scrolls inside its
    /// frame, each its picture on a slate tile (the game's own picture, letterboxed in its own shape and never stretched,
    /// or a glyph in its own colour), its name, its price or key figure and a state chip (owned, sold out, locked); the
    /// chosen card's foot filled cyan, and beside the grid the chosen card's detail: its picture large, what it is, its
    /// name, its facts and its actions. Each column of cards is a list, so up and down walk it and scroll it, and left and
    /// right cross to the next column. Commands from <see cref="First"/>: <see cref="Card"/> (the card's index) and
    /// <see cref="Action"/> (the detail's action). Game-free. Cost: a layout a change, the rows in view only.
    /// </summary>
    public static class UiCatalogue
    {
        public const int First = 130;
        public const int Card = 0, Action = 1;
        private const float LeastCard = 200f;    // px: a card's narrowest before the grid holds one column fewer
        private const float TileShare = 0.56f;   // of a card's width, its picture's height
        private const float DetailShare = 0.3f;  // of the width, the detail's
        private const float LeastDetail = 320f;  // px
        private const float MostDetail = 440f;   // px
        private const float DetailTile = 160f;   // px: the chosen card's picture in the detail
        private const float Glyph = 48f;         // px: a card's glyph when it has no picture

        /// <summary>The width of the grid in a catalogue of a width, so a screen can put something over it (a row of filters) that the detail does not share.</summary>
        public static float GridWidth(float w) => w - Math.Max(LeastDetail, Math.Min(MostDetail, UiDraw.SnapDown(w * DetailShare))) - UiTokens.S6;

        /// <summary>The columns of the last grid laid out and its first list, so a screen can find the card a list's row stands for.</summary>
        public static int CardOf(int columns, int firstList, int list, int index) => index * Math.Max(1, columns) + (list - firstList);

        /// <summary>
        /// Lays the catalogue out in a box: the grid at the left, the chosen card's detail at the right. Returns the first
        /// column's list (the next columns' follow it), and writes the columns into <paramref name="columns"/>.
        /// </summary>
        public static short Lay(UiScene s, UiAt at, UiCatalogueModel m, float x, float y, float w, float h, IUiKeys keys, out int columns, int command = First, float gridInset = 0f)
        {
            float gw = GridWidth(w), dw = w - gw - UiTokens.S6;
            columns = Math.Max(1, (int)((gw + UiTokens.S3) / (LeastCard + UiTokens.S3)));
            if (m.CardCount == 0)
            {
                UiDraw.EmptyState(s, at, x, y + gridInset, w, h - gridInset, m.EmptyIcon, m.EmptyTitle, m.EmptyBody, null, -1, keys);
                m.Offset = 0f;
                return -1;
            }
            short firstList = Grid(s, at, m, x, y + gridInset, gw, h - gridInset, columns, command);
            if (m.Chosen >= 0 && m.Chosen < m.CardCount)
            {
                Detail(s, at, m, x + gw + UiTokens.S6, y, dw, h, keys, command);
            }
            return firstList;
        }

        /// <summary>
        /// The grid alone, without the chosen card's detail: a catalogue panel's column beside a game's 3D preview, its
        /// cards no narrower than <paramref name="least"/>. Returns the first column's list, as <see cref="Lay"/> does.
        /// </summary>
        public static short LayGrid(UiScene s, UiAt at, UiCatalogueModel m, float x, float y, float w, float h, float least, IUiKeys keys, out int columns, int command = First)
        {
            columns = Math.Max(1, (int)((w + UiTokens.S3) / (Math.Max(UiTokens.S16, least) + UiTokens.S3)));
            if (m.CardCount == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, h, m.EmptyIcon, m.EmptyTitle, m.EmptyBody, null, -1, keys);
                m.Offset = 0f;
                return -1;
            }
            return Grid(s, at, m, x, y, w, h, columns, command);
        }

        /// <summary>
        /// The height a grid of <see cref="LayGrid"/> takes in a width with every row at its least, so a panel can end under
        /// its last row rather than stand the screen's height; the empty state's when there are no cards.
        /// </summary>
        public static float GridHeight(UiCatalogueModel m, float w, float least)
        {
            if (m.CardCount == 0)
            {
                return UiDraw.EmptyStateHeight(w, m.EmptyTitle, m.EmptyBody, null) + UiTokens.S6 * 2f;
            }
            int columns = Math.Max(1, (int)((w + UiTokens.S3) / (Math.Max(UiTokens.S16, least) + UiTokens.S3)));
            int rows = (m.CardCount + columns - 1) / columns;
            return rows * (TileHeight(CardWidth(w, columns)) + FootHeight + UiTokens.S3) - UiTokens.S3;
        }

        private static float CardWidth(float gw, int columns) => UiDraw.SnapDown((gw - UiTokens.S3 * (columns - 1)) / columns);

        private static float TileHeight(float cw) => UiDraw.SnapDown(cw * TileShare);

        private const float MostTile = 0.8f;   // of a card's width, the tallest its picture grows when few rows leave room

        // The picture's height: its share of the card's width, or taller when every row then fills the room whole.
        private static float TileFor(float cw, float h, int rows)
        {
            float least = TileHeight(cw);
            float fill = UiDraw.SnapDown((h + UiTokens.S3) / Math.Max(1, rows) - UiTokens.S3 - FootHeight);
            return Math.Max(least, Math.Min(UiDraw.SnapDown(cw * MostTile), fill));
        }

        private static float FootHeight => UiDraw.Snap(UiTokens.S3 * 2f + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1 + UiTextRules.LineHeight(UiStyle.BodyStrong));

        // The cards, row by row in a region that scrolls inside its frame; only the rows in view are laid out.
        private static short Grid(UiScene s, UiAt at, UiCatalogueModel m, float x, float y, float w, float h, int columns, int command)
        {
            float cw = CardWidth(w, columns);
            int rows = (m.CardCount + columns - 1) / columns;
            float th = TileFor(cw, h, rows), ch = th + FootHeight, rowH = ch + UiTokens.S3;
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
            int first = Math.Max(0, (int)(offset / rowH)), last = Math.Min(rows - 1, (int)((offset + h - 0.5f) / rowH));
            short firstList = -1;
            for (int c = 0; c < columns; c++)
            {
                int inColumn = (m.CardCount - c + columns - 1) / columns;
                int shownFirst = Math.Min(first, inColumn), shownLast = Math.Min(last, inColumn - 1);
                short list = s.AddList(inColumn, shownFirst, Math.Max(0, shownLast - shownFirst + 1), clip, m.Chosen % columns == c ? m.Chosen / columns : -1);
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
                    CardAt(s, inClip.Arriving(i - first * columns), m, i, c * (cw + UiTokens.S3), r * rowH, cw, ch, th, (short)(firstList + c), r, command);
                }
            }
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S2, y, h, offset, content);
            return firstList;
        }

        // A card: its picture or glyph on its slate tile with the state chip in the tile's corner, its name and its figure,
        // the foot filled cyan when it is the one chosen.
        private static void CardAt(UiScene s, UiAt at, UiCatalogueModel m, int i, float x, float y, float w, float h, float th, short list, int row, int command)
        {
            ref UiCatalogueCard c = ref m.Cards[i];
            bool chosen = i == m.Chosen;
            // The card's face is clear, so the hover's still fill shows on it as on a row; its picture's tile carries its shape.
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            int tile = UiDraw.Box(s, inside, x, y, w, th, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);   // the card's width, so tile and foot are one shape
            Picture(s, inside.In(tile), ref c, x, y, w, th, Glyph);
            if (!string.IsNullOrEmpty(c.State))
            {
                float most = w - UiTokens.S4 * 2f;
                float cw = Math.Min(UiDraw.SnapDown(most), UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(c.State, UiStyle.SmallStrong) + UiTokens.S1));
                int over = s.ItemCount;
                UiDraw.Chip(s, inside.In(tile), x + w - UiTokens.S3 - cw, y + UiTokens.S3, c.State, c.StateTone, cw);
                Over(s, over);
            }
            float fy = y + th, fh = h - th;
            int foot = UiDraw.Box(s, inside, x, fy, w, fh, chosen ? UiRole.Accent : UiRole.None, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt onFoot = inside.In(foot);
            float lh = UiTextRules.LineHeight(UiStyle.Title);
            UiDraw.Text(s, onFoot, x + UiTokens.S3, fy + UiTokens.S3, w - UiTokens.S3 * 2f, UiStyle.Title, chosen ? UiRole.OnAccent : c.Disabled ? UiRole.Text2 : UiRole.Text, c.Name,
                UiFlags.Truncate | (c.Disabled ? UiFlags.Disabled : UiFlags.None));
            UiRole price = chosen ? UiRole.OnAccent : c.Disabled ? UiRole.Muted : UiRole.Text;   // money in the words' white, the cyan kept for links and what is chosen
            UiDraw.Text(s, onFoot, x + UiTokens.S3, fy + UiTokens.S3 + lh + UiTokens.S1, Fit(c.Figure, w - UiTokens.S3 * 2f), UiStyle.BodyStrong, price,
                c.Figure, UiFlags.Truncate | UiFlags.Middle | (c.Disabled ? UiFlags.Disabled : UiFlags.None));
            UiDraw.Node(s, at, x, y, w, h, command + Card, i, face, false, list, row);   // a sold out card still opens its detail: only its actions are greyed
        }

        // A state's chip lies over the picture's corner on purpose, as the phone's badges lie over their tiles.
        private static void Over(UiScene s, int from)
        {
            for (int k = from; k < s.ItemCount; k++)
            {
                s.Items[k].Flags |= UiFlags.Overflow;
            }
        }

        // A lone figure's box is its own words, as it stands under a name rather than in a column of figures.
        private static float Fit(string figure, float most) => Math.Min(most, UiTextRules.Width(figure, UiStyle.BodyStrong) + UiTokens.S1);

        /// <summary>
        /// A card's picture in a tile: the game's picture in the largest box of its own shape the tile holds, centred (its
        /// width over its height kept in the item, so the checks can see it is never stretched), or its glyph in its own colour.
        /// </summary>
        public static int Picture(UiScene s, UiAt at, ref UiCatalogueCard c, float x, float y, float w, float h, float glyph)
        {
            if (!string.IsNullOrEmpty(c.Picture) && c.Aspect > 0f)
            {
                float room = w - UiTokens.S2 * 2f, roomH = h - UiTokens.S2 * 2f;
                float pw = room, ph = room / c.Aspect;
                if (ph > roomH)
                {
                    ph = roomH;
                    pw = roomH * c.Aspect;
                }
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Text = c.Picture, Value = c.Aspect, X = x + (w - pw) / 2f, Y = y + (h - ph) / 2f, W = pw, H = ph,
                    Flags = UiFlags.Share | UiFlags.Middle, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
                };
                return s.Add(ref item);
            }
            return UiDraw.Icon(s, at, x + UiDraw.SnapDown((w - glyph) / 2f), y + UiDraw.SnapDown((h - glyph) / 2f), glyph, c.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
        }

        // The chosen card's detail: its picture large, what it is in capitals, its name in the figures' size, its price,
        // its facts as compact rows, a sentence on it, and its actions at the foot, the main one last.
        private static void Detail(UiScene s, UiAt at, UiCatalogueModel m, float x, float y, float w, float h, IUiKeys keys, int command)
        {
            // Buttons that do not fit the width wrap to a row under (a detail with many things to do), the main one still first at the right.
            int rows = 1;
            float used = 0f;
            for (int i = m.Actions.Length - 1; i >= 0; i--)
            {
                float need = Math.Min(w, UiDraw.ButtonWidth(m.Actions[i].Kind, m.Actions[i].Label, m.Actions[i].Icon, m.Actions[i].Key, keys));
                if (used > 0f && used + need > w)
                {
                    rows++;
                    used = 0f;
                }
                used += need + UiTokens.S3;
            }
            float actionsH = rows * UiTokens.Control + (rows - 1) * UiTokens.S2;
            // The picture takes what the facts and the actions leave of the column, at most its own height: a short window gets a shorter picture, never a cut sentence.
            float restH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong))
                + UiTokens.S3 + Math.Min(m.DetailLabels.Length, m.DetailValues.Length) * UiTokens.PairRow + UiTokens.S4 + UiTokens.S5 + actionsH;
            if (!string.IsNullOrEmpty(m.DetailNote))
            {
                restH += UiTokens.S3 + UiDraw.Snap(UiTextRules.WrappedHeight(m.DetailNote, UiStyle.Body, w, 4));
            }
            float tileH = Math.Min(DetailTile, UiDraw.SnapDown(h - restH));
            ref UiCatalogueCard c = ref m.Cards[m.Chosen];
            if (tileH < UiTokens.S12)
            {
                tileH = 0f;   // too short a window for a picture that would be a sliver: the facts and the actions first
            }
            else
            {
                int tile = UiDraw.Box(s, at, x, y, w, tileH, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
                Picture(s, at.In(tile), ref c, x, y, w, tileH, UiTokens.S16);
                if (!string.IsNullOrEmpty(c.State))
                {
                    int over = s.ItemCount;
                    UiDraw.Chip(s, at.In(tile), x + UiTokens.S3, y + UiTokens.S3, c.State, c.StateTone, w - UiTokens.S6);
                    Over(s, over);
                }
            }
            float ty = y + tileH + (tileH > 0f ? UiTokens.S4 : 0f);
            UiDraw.Text(s, at, x, ty, w, UiStyle.SectionHeader, UiRole.Text2, m.DetailKicker, UiFlags.Label | UiFlags.Truncate);
            ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1;
            UiDraw.Text(s, at, x, ty, w, UiStyle.Display, UiRole.Text, c.Name, UiFlags.Truncate);
            ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            UiDraw.Text(s, at, x, ty, Fit(c.Figure, w), UiStyle.BodyStrong, UiRole.Text, c.Figure, UiFlags.Truncate);
            ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong)) + UiTokens.S3;
            for (int i = 0; i < m.DetailLabels.Length && i < m.DetailValues.Length; i++)
            {
                ty += UiDraw.InfoRow(s, at, x, ty, w, m.DetailLabels[i], m.DetailValues[i]);
            }
            if (!string.IsNullOrEmpty(m.DetailNote))
            {
                ty += UiTokens.S3;
                // The sentence takes the lines the actions leave it (at most four) and is cut rather than run under them.
                int lines = Math.Max(1, Math.Min(4, (int)((y + h - actionsH - UiTokens.S5 - ty) / UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)))));
                int note = UiDraw.Text(s, at, x, ty, w, UiStyle.Body, UiRole.Text2, m.DetailNote, UiFlags.Wrap | UiFlags.Label, lines);
                ty += s.Items[note].H;
            }
            // The actions under what they act on, the main one last at the right, as the hero's actions stand by it.
            float by = Math.Min(UiDraw.Snap(ty + UiTokens.S5), y + h - actionsH);
            float right = x + w;
            for (int i = m.Actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                float bw = Math.Min(w, UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys));
                if (right - bw < x)
                {
                    right = x + w;
                    by += UiTokens.Control + UiTokens.S2;
                }
                right -= bw;
                UiDraw.Button(s, at, right, by, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, command + Action, i, a.Disabled || c.Disabled);
                right -= UiTokens.S3;
            }
        }
    }
}
