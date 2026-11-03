using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Whether a goods row offers to add the thing to an order.</summary>
    public enum UiGoodsAdd : byte
    {
        None = 0,   // nothing to add: a store's goods to look at, or a row already in the order (its amount shows)
        Add,        // the add button: one into the order
        Full        // the add button greyed: the order holds all it may
    }

    /// <summary>One thing of a list of goods: its picture, its name, its price, and in an order its amount between a minus and a plus.</summary>
    public struct UiGoodsRow
    {
        public string Picture;     // the game's own picture (UiPictures), null for the glyph
        public UiIcon Icon;        // the glyph on its tile when the game has no picture
        public string Name;        // "Flower stand"
        public string Sub;         // "$1,240 each"; null for none
        public string Figure;      // the row's sum ("$4,960") or its price; null for none
        public string Amount;      // the amount in the order between its minus and plus; null for none
        public bool CanLess, CanMore;
        public UiGoodsAdd Add;
    }

    /// <summary>What the items list shows: its title, the search, how many of how many show, the rows a screenful at a time, and an empty state.</summary>
    public sealed class UiItemsListModel
    {
        public string Title;                               // the game's own: "Fiorello Furniture Inventory (14/100 items selected)"
        public UiIcon Icon = UiIcon.ShoppingCart;
        public string Search;                              // the words typed, null for none
        public string Placeholder;                         // "Search"
        public string Count;                               // "12 of 48"; null for none
        public UiGoodsRow[] Rows = new UiGoodsRow[0];
        public int RowCount;
        public float Offset;                               // px the rows are scrolled
        public string EmptyTitle, EmptyBody;               // nothing found, or nothing to list
        public string ClearLabel;                          // the empty state's way out while a search hides everything; null for none
    }

    /// <summary>
    /// The items list (the game's ItemsList: a store's goods in BizMan, and the goods an order adds at the furniture store,
    /// the food supplier and the vehicle store), and the goods rows it is made of, which an order's form lays out too
    /// (<see cref="Rows"/>): each thing's picture on its tile, its name over its price, and in an order its amount between a
    /// minus and a plus with the row's sum at the right, or the add button while it is not in the order. As a panel it is
    /// centred over the world: its title strip with the close, the search with how many show, the rows scrolling inside it,
    /// an empty state in words. Commands: <see cref="AddCommand"/>, <see cref="LessCommand"/> and <see cref="MoreCommand"/> (a
    /// row's index), <see cref="TypeCommand"/> (the search with <see cref="SearchArg"/>, or a row's amount with its index: the
    /// screen's IUiTyping answers for whichever was focused last), <see cref="ClearCommand"/> and the strip's close (Back). Game-free. Cost: a layout a change; only the rows in view are laid out.
    /// </summary>
    public static class UiItemsList
    {
        public const int AddCommand = 430, LessCommand = 431, MoreCommand = 432, ClearCommand = 434;
        public const int TypeCommand = 433;                // typing: the search (arg -1) or a row's amount (its index), one command as the host types for one
        public const int SearchArg = -1;
        public const float Width = 600f;                   // px: a name, a price and a stepper side by side, as the delivery form's goods
        public const float RowHeight = UiTokens.Row;       // px: a thing's row, its 40 px picture with room round it
        public const float StepperWidth = 128f;            // px: the minus, the amount and the plus
        private const float Thumb = UiTokens.IconTile;
        private const float FigureMost = 112f;             // px: the row's sum at most
        private const int RowsInView = 8;                  // rows the panel shows before it scrolls
        private const int EmptyRows = 4;                   // rows' room the empty state stands in
        private const byte Region = 230;

        /// <summary>The height a block of rows takes in a form.</summary>
        public static float RowsHeight(int count) => Math.Max(0, count) * RowHeight;

        /// <summary>
        /// A block of goods rows from a place in a form (an order's chosen goods), each row's targets under the commands with
        /// the row's index as the argument (<paramref name="argBase"/> added, so a form with rows of its own keeps them apart).
        /// Returns the height.
        /// </summary>
        public static float Rows(UiScene s, UiAt at, UiGoodsRow[] rows, int count, float x, float y, float w, int argBase = 0)
        {
            count = Math.Min(count, rows?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                Row(s, at, ref rows[i], argBase + i, x, y + i * RowHeight, w, i < count - 1);
            }
            return RowsHeight(count);
        }

        /// <summary>
        /// One goods row: the picture on its tile, the name over its line, then at the right the add button, or the stepper
        /// and the sum. A hairline under it unless it is the last. Returns its height.
        /// </summary>
        public static float Row(UiScene s, UiAt at, ref UiGoodsRow r, int arg, float x, float y, float w, bool rule = false)
        {
            int row = UiDraw.Box(s, at, x, y, w, RowHeight, UiRole.None, UiRole.None, 0f);
            UiAt inside = at.In(row);
            float ty = y + (RowHeight - Thumb) / 2f;
            UiToolPanel.Thumbnail(s, inside, x, ty, r.Picture, r.Icon);
            float right = x + w;
            if (!string.IsNullOrEmpty(r.Figure))
            {
                float fw = Math.Min(FigureMost, UiDraw.Snap(UiTextRules.Width(r.Figure, UiStyle.BodyStrong) + UiTokens.S1));
                UiDraw.TextMiddle(s, inside, right - fw, y, RowHeight, fw, UiStyle.BodyStrong, UiRole.Text, r.Figure, UiFlags.Right | UiFlags.Truncate);
                right -= fw + UiTokens.S4;
            }
            if (r.Amount != null)
            {
                UiDraw.Stepper(s, inside, right - StepperWidth, y + (RowHeight - UiTokens.Control) / 2f, StepperWidth, r.Amount, r.CanLess, r.CanMore, LessCommand, TypeCommand,
                    MoreCommand, arg, false);
                right -= StepperWidth + UiTokens.S4;
            }
            else if (r.Add != UiGoodsAdd.None)
            {
                float b = UiTokens.ControlSmall;
                int drawn = s.ItemCount;
                int add = UiDraw.IconButton(s, inside, right - b, y + (RowHeight - b) / 2f, b, UiIcon.Plus, UiRole.AccentText, AddCommand, arg);
                if (r.Add == UiGoodsAdd.Full)
                {
                    UiDraw.Dim(s, drawn, add);
                }
                right -= b + UiTokens.S4;
            }
            float nx = x + Thumb + UiTokens.S3, nw = Math.Max(0f, right - nx);
            bool sub = !string.IsNullOrEmpty(r.Sub);
            float nh = UiTextRules.LineHeight(UiStyle.BodyStrong), sh = sub ? UiTextRules.LineHeight(UiStyle.Small) : 0f;
            float ny = y + (RowHeight - nh - sh) / 2f;
            UiDraw.Text(s, inside, nx, ny, nw, UiStyle.BodyStrong, UiRole.Text, r.Name, UiFlags.Truncate | UiFlags.Middle);
            if (sub)
            {
                UiDraw.Text(s, inside, nx, ny + nh, nw, UiStyle.Small, UiRole.Text2, r.Sub, UiFlags.Truncate | UiFlags.Middle);
            }
            if (rule)
            {
                UiDraw.Box(s, at, x, y + RowHeight - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            }
            return RowHeight;
        }

        /// <summary>The panel's height: the strip, the search, the rows in view (or the empty state's room), its padding.</summary>
        public static float Height(UiItemsListModel m) => UiFrame.Strip + UiTokens.S4 + UiTokens.Control + UiTokens.S3 + ViewHeight(m) + UiTokens.S3;

        /// <summary>The scroll the rows may take: none while they all show.</summary>
        public static float MostOffset(UiItemsListModel m) => Math.Max(0f, m.RowCount * RowHeight - ViewHeight(m));

        /// <summary>The height the rows have in the panel: a screenful of them, at least four.</summary>
        public static float ViewHeight(UiItemsListModel m) => m.RowCount == 0 ? EmptyRows * RowHeight : Math.Min(RowsInView, m.RowCount) * RowHeight;

        /// <summary>The panel as a screen of its own, centred over the world above the status bar, with the bar's prompts.</summary>
        public static void Lay(UiScene s, UiItemsListModel m, UiFrame frame, bool dark, IUiKeys keys, UiPrompt[] prompts, int promptCount)
        {
            s.Begin("items list", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float h = Height(m);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f), y = UiDraw.SnapDown(Math.Max(UiTokens.S8, (frame.BandY - h) / 2f));
            Draw(s, UiAt.Screen, m, x, y, keys);
            for (int i = 0; i < promptCount && prompts != null && i < prompts.Length; i++)
            {
                s.AddPrompt(prompts[i].Action, prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The panel alone at a place (its top left); returns its panel.</summary>
        public static int Draw(UiScene s, UiAt at, UiItemsListModel m, float x, float y, IUiKeys keys = null)
        {
            float h = Height(m);
            UiAt column = at.InRegion(Region);
            int panel = UiDraw.Box(s, column, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = column.In(panel);
            UiDraw.TitleStrip(s, on, x, y, Width, m.Icon, m.Title, true);
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f, cy = y + UiFrame.Strip + UiTokens.S4;
            int search = Search(s, on, m, ix, cy, iw);
            cy += UiTokens.Control + UiTokens.S3;
            float viewH = ViewHeight(m);
            if (m.RowCount == 0)
            {
                bool way = !string.IsNullOrEmpty(m.ClearLabel);
                UiDraw.EmptyState(s, on, ix, cy, iw, viewH, UiIcon.ShoppingCart, m.EmptyTitle, m.EmptyBody, way ? m.ClearLabel : null, way ? ClearCommand : -1, keys ?? UiDefaultKeys.Keyboard);
                s.FocusFirst = search;
                return panel;
            }
            float offset = Math.Max(0f, Math.Min(m.Offset, MostOffset(m)));
            short clip = s.AddClip(x, cy, Width, viewH, offset);
            int first = (int)(offset / RowHeight), last = Math.Min(m.RowCount - 1, (int)((offset + viewH - 0.5f) / RowHeight));
            short list = s.AddList(m.RowCount, first, Math.Max(0, last - first + 1), clip, -1);
            s.Clips[clip].ContentHeight = m.RowCount * RowHeight;
            var rows = new UiAt { Parent = -1, Clip = clip, Layer = on.Layer, Order = on.Order, Region = Region };
            for (int i = first; i <= last; i++)
            {
                int nodes = s.NodeCount;
                Row(s, rows, ref m.Rows[i], i, ix - x, i * RowHeight, iw, i < m.RowCount - 1);
                for (int n = nodes; n < s.NodeCount; n++)
                {
                    // The row's targets scroll with the list, so the keys reveal a row beyond those in view.
                    s.Nodes[n].List = list;
                    s.Nodes[n].Index = i;
                }
            }
            UiDraw.ScrollThumb(s, on, x + Width - UiTokens.S2, cy, viewH, offset, m.RowCount * RowHeight);
            return panel;
        }

        // The search: its glyph, the words typed or the placeholder, how many show; its target the typing command with the search's argument.
        private static int Search(UiScene s, UiAt at, UiItemsListModel m, float x, float y, float w)
        {
            return UiDraw.Search(s, at, x, y, w, m.Placeholder, m.Search, m.Count, TypeCommand, SearchArg);
        }
    }
}
