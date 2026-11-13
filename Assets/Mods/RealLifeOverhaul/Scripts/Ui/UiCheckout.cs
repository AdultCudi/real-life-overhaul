using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A line of goods at a checkout, as the game's row shows it: the item, its pack, its price and the packs taken.</summary>
    public struct UiCheckoutLine
    {
        public string Name;        // the game's words for the row ("Cupcake")
        public string Pack;        // what one row's price buys ("1,500 pieces"); null for a ticket
        public string Picture;     // the item's own picture (UiPictures), or null for the glyph
        public float Aspect;
        public UiIcon Icon;
        public string Price;       // one pack's price as the game writes it
        public string Total;       // the row's price for the packs taken, as the game's row shows it
        public int Packs;          // packs taken; the row picks while the register lets the player choose
        public bool CanLess, CanMore;
    }

    /// <summary>A tax the player can pay at the tax office: what is owed and the amount to pay, typed in its field.</summary>
    public struct UiCheckoutTax
    {
        public string Name;        // "Current taxes"
        public string Owed;        // as the game writes it
        public string Amount;      // what is in the field to pay
    }

    /// <summary>What a checkout shows: the goods or the taxes, the total, the queue, and the game's own buttons as they stand.</summary>
    public sealed class UiCheckoutModel
    {
        public const int PerPage = 6;                           // rows in view at least; a taller screen shows more (Rows)

        public string Title;                                   // the game's title ("Purchase (3/10 items)", "Pay taxes")
        public UiCheckoutLine[] Lines = new UiCheckoutLine[0];
        public UiCheckoutTax[] Taxes = new UiCheckoutTax[0];
        public bool Choose;                                    // the register lets the player pick packs (up to the game's most)
        public int Rows = PerPage;                             // rows of goods in view; the rest scroll under them with the totals standing
        public float Offset;                                   // how far the goods have scrolled
        public int Reveal = -1;                                // a row the keys moved to, brought into view
        public string ColumnGoods, ColumnPrice, ColumnPacks, ColumnTotal;
        public string PriceEach;                               // "{price} a pack"
        public string TotalLabel, Total;                       // "To pay", "$4,980"; Total null when the game hides it
        public string Paused;                                  // "Time is paused" while the game holds time, else null
        public string Queue;                                   // the game's words for the queue once the order is in, else null
        public string QueueDetail;                             // a line under them, or null; while the goods are chosen, the place in the line alone
        public UiInfoRow[] Sums = new UiInfoRow[0];            // the goods and the tax in them on a band over the total, or none
        public string Order;                                   // the order button's words, null when the game hides it
        public bool OrderEnabled;
        public string Cancel;                                  // the cancel button's words, null when the game hides it
        public bool CancelEnabled;
        public string Pay, AmountHint, OwedWords;              // a tax row's button, its field's hint, "Owed {amount}"
        public string EmptyTitle, EmptyBody;
        public UiCheckoutHead Head;                            // the shop and where the goods go, or null
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>The shop a checkout is in, and where its goods go.</summary>
    public sealed class UiCheckoutHead
    {
        public UiIcon Mark;          // the shop's kind
        public string Name;          // the business's name
        public string Address;       // its address
        public string DeliveredLabel, DeliveredTo;   // "Delivered to", "Your hand truck"; null for none
    }

    /// <summary>
    /// The checkout (the game's PurchaseUI): a receipt centred over the paused store. Its title strip, the shop it is in,
    /// the goods as rows (the item's picture on a tile, its name and pack, a pack's price, the packs as a stepper while the
    /// register lets the player pick, the row's price), or at the tax office the taxes owed with the amount to pay; the
    /// total as the large figure, Cancel and Order with their keys; once the order is in, the queue as one row. A long
    /// list scrolls under its columns while the total and the buttons stay in view. Commands from <see cref="LessCommand"/>. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiCheckout
    {
        public const int LessCommand = 600, MoreCommand = 601, PackCommand = 602, TaxTypeCommand = 604, TaxPayCommand = 605,
            OrderCommand = 606, CancelCommand = 607;
        public static readonly int[] Commands = { LessCommand, MoreCommand, PackCommand, TaxTypeCommand, TaxPayCommand, OrderCommand, CancelCommand };

        public const float Width = 760f;                 // px: a receipt wide enough for a row's four columns
        private const float LineH = UiTokens.S16;         // px: a row of goods, its picture's tile with room round it
        private const float PicW = UiTokens.S16, PicH = UiTokens.S12;
        private const float PriceW = 136f, StepW = 136f, TotalW = 112f;
        private const float Foot = UiTokens.S16 + UiTokens.S6;   // px: the total and the buttons
        private const float HeadH = UiTokens.S16 + UiTokens.S2;  // px: the shop's line
        private const int MostRows = 14;                          // rows in view on the tallest screen

        // The rows of goods in view: all of them while they fit, else as many as the screen has room for, the rest scrolling.
        private static int Shown(UiCheckoutModel m) => Math.Max(0, Math.Min(m.Rows, m.Lines.Length));

        private static bool Queued(UiCheckoutModel m) => !string.IsNullOrEmpty(m.Queue);

        /// <summary>The panel's height for its model.</summary>
        public static float Height(UiCheckoutModel m)
        {
            float h = UiFrame.Strip + (m.Head != null ? HeadH : 0f);
            if (Queued(m))
            {
                return UiDraw.Snap(h + QueueH(m) + (m.Cancel != null ? Foot : UiTokens.S2));
            }
            if (m.Taxes.Length > 0)
            {
                h += UiTokens.S2 + m.Taxes.Length * LineH + UiTokens.S2;
            }
            else if (m.Lines.Length == 0)
            {
                h += UiDraw.EmptyStateHeight(Width - UiTokens.S6 * 2f, m.EmptyTitle, m.EmptyBody, null) + UiTokens.S6 * 2f;
            }
            else
            {
                h += UiTokens.TableRow + Shown(m) * LineH + UiTokens.S2 + SumsH(m) + PlaceH(m);
            }
            return UiDraw.Snap(h + Foot);
        }

        // The goods and the tax in them on a band, a row each.
        private static float SumsH(UiCheckoutModel m) => m.Sums.Length == 0 ? 0f : UiTokens.S2 * 2f + m.Sums.Length * UiTokens.PairRow;

        // While the goods are chosen, the player's place in the register's line as one row on a band; none outside a line.
        private static float PlaceH(UiCheckoutModel m) => Queued(m) || string.IsNullOrEmpty(m.QueueDetail) ? 0f : UiTokens.S3 * 2f + UiTokens.PairRow;

        private static float QueueH(UiCheckoutModel m) => UiTokens.S4 * 2f + UiTokens.PairRow + (string.IsNullOrEmpty(m.QueueDetail) ? 0f : UiTokens.PairRow);

        /// <summary>The checkout as a screen of its own: centred over the store above the status bar, with the bar and its prompts.</summary>
        public static void Lay(UiScene s, UiCheckoutModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("checkout", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            if (m.Lines.Length > 0 && !Queued(m) && m.Taxes.Length == 0)
            {
                // As many rows as the screen has room for between the margins, the totals and buttons beneath them kept in view.
                m.Rows = 0;
                float fixedH = Height(m);
                m.Rows = Math.Max(UiCheckoutModel.PerPage, Math.Min(MostRows, (int)((frame.BandY - UiTokens.S8 * 2f - fixedH) / LineH)));
            }
            float h = Height(m);
            // Once the order is in the game slides its panel down out of the way while the player waits: the queue row docks
            // low over the bar, the store in view above it.
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f);
            float y = Queued(m) ? UiDraw.SnapDown(frame.BandY - UiTokens.S6 - h) : UiDraw.SnapDown(Math.Max(UiTokens.S8, (frame.BandY - h) / 2f));
            Draw(s, m, x, y, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        public static float Draw(UiScene s, UiCheckoutModel m, float x, float y, IUiKeys keys)
        {
            float h = Height(m);
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, Width, UiIcon.ShoppingCart, m.Title, m.Cancel != null && m.CancelEnabled);
            if (m.Head != null)
            {
                cy = Head(s, on, m.Head, x, cy);
            }
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            if (Queued(m))
            {
                cy = Queue(s, on, m, x, cy);
                if (m.Cancel != null)
                {
                    Footer(s, on, m, x, cy, keys, false);
                }
                return h;
            }
            if (m.Taxes.Length > 0)
            {
                cy += UiTokens.S2;
                for (int i = 0; i < m.Taxes.Length; i++)
                {
                    TaxRow(s, on, ref m.Taxes[i], i, m, ix, cy, iw, keys);
                    cy += LineH;
                }
                cy += UiTokens.S2;
            }
            else if (m.Lines.Length == 0)
            {
                float eh = UiDraw.EmptyStateHeight(Width - UiTokens.S6 * 2f, m.EmptyTitle, m.EmptyBody, null) + UiTokens.S6 * 2f;
                UiDraw.EmptyState(s, on, x + UiTokens.S6, cy, Width - UiTokens.S6 * 2f, eh, UiIcon.ShoppingCart, m.EmptyTitle, m.EmptyBody);
                cy += eh;
            }
            else
            {
                cy = Columns(s, on, m, ix, cy, iw);
                cy = Goods(s, on, m, ix, cy, iw);
                cy += UiTokens.S2;
                cy = Sums(s, on, m, x, cy);
                cy = Place(s, on, m, x, cy);
            }
            Footer(s, on, m, x, cy, keys, true);
            return h;
        }

        // The goods in a scroll region of the rows that fit, every row of the cart in it: the wheel and the keys bring the rest into view.
        private static float Goods(UiScene s, UiAt at, UiCheckoutModel m, float x, float y, float w)
        {
            int shown = Shown(m);
            float viewH = shown * LineH, content = m.Lines.Length * LineH, offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < m.Lines.Length)
            {
                offset = Math.Min(offset, m.Reveal * LineH);
                offset = Math.Max(offset, (m.Reveal + 1) * LineH - viewH);
            }
            offset = Math.Max(0f, Math.Min(offset, content - viewH));
            m.Offset = offset;
            m.Reveal = -1;
            short clip = s.AddClip(x, y, w, viewH, offset);
            s.Clips[clip].ContentHeight = content;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            for (int i = 0; i < m.Lines.Length; i++)
            {
                Line(s, inClip, ref m.Lines[i], i, m, 0f, i * LineH, w);
            }
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S2, y, viewH, offset, content);
            return y + viewH;
        }

        // The shop's line: its kind's glyph on a tile, its name over its address, and where the goods go at the right.
        private static float Head(UiScene s, UiAt at, UiCheckoutHead head, float x, float y)
        {
            float tile = UiTokens.S12, ty = y + (HeadH - tile) / 2f, ix = x + UiTokens.S4;
            int t = UiDraw.Tile(s, at, ix, ty, tile, UiRole.None, false);
            UiDraw.Icon(s, at.In(t), ix + (tile - UiTokens.S6) / 2f, ty + (tile - UiTokens.S6) / 2f, UiTokens.S6, head.Mark, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float tx = ix + tile + UiTokens.S3, right = x + Width - UiTokens.S4;
            float rw = 0f;
            if (!string.IsNullOrEmpty(head.DeliveredTo))
            {
                float said = Math.Max(UiTextRules.Width(head.DeliveredTo, UiStyle.BodyStrong), UiTextRules.Width(head.DeliveredLabel, UiStyle.Small));
                rw = Math.Min(UiDraw.Snap(Width * 0.36f), UiDraw.Snap(said + UiTokens.S1));
                float block = UiTextRules.LineHeight(UiStyle.Small) + UiTokens.S1 + UiTextRules.LineHeight(UiStyle.BodyStrong), by = y + (HeadH - block) / 2f;
                UiDraw.Text(s, at, right - rw, by, rw, UiStyle.Small, UiRole.Text2, head.DeliveredLabel, UiFlags.Right | UiFlags.Truncate | UiFlags.Label | UiFlags.Middle);
                UiDraw.Text(s, at, right - rw, by + UiTextRules.LineHeight(UiStyle.Small) + UiTokens.S1, rw, UiStyle.BodyStrong, UiRole.Text, head.DeliveredTo,
                    UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
            }
            float tw = right - (rw > 0f ? rw + UiTokens.S4 : 0f) - tx;
            bool address = !string.IsNullOrEmpty(head.Address);
            float nb = UiTextRules.LineHeight(UiStyle.Title) + (address ? UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small) : 0f), ny = y + (HeadH - nb) / 2f;
            UiDraw.Text(s, at, tx, ny, tw, UiStyle.Title, UiRole.Text, head.Name, UiFlags.Truncate | UiFlags.Middle);   // the pair centred on the line
            if (address)
            {
                UiDraw.Text(s, at, tx, ny + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, head.Address, UiFlags.Truncate | UiFlags.Middle);
            }
            UiDraw.Box(s, at, x, y + HeadH - UiTokens.Hairline, Width, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            return y + HeadH;
        }

        // The columns' names in capitals over the rows: goods, a pack's price, the packs and the row's price.
        private static float Columns(UiScene s, UiAt at, UiCheckoutModel m, float x, float y, float w)
        {
            float h = UiTokens.TableRow, right = x + w;
            UiDraw.TextMiddle(s, at, right - TotalW, y, h, TotalW, UiStyle.SectionHeader, UiRole.Text2, m.ColumnTotal, UiFlags.Right | UiFlags.Truncate | UiFlags.Label);
            float px = right - TotalW - UiTokens.S4;
            if (m.Choose)
            {
                px -= StepW;
                UiDraw.TextMiddle(s, at, px, y, h, StepW, UiStyle.SectionHeader, UiRole.Text2, m.ColumnPacks, UiFlags.Centre | UiFlags.Truncate | UiFlags.Label);
                px -= UiTokens.S4;
            }
            UiDraw.TextMiddle(s, at, px - PriceW, y, h, PriceW, UiStyle.SectionHeader, UiRole.Text2, m.ColumnPrice, UiFlags.Right | UiFlags.Truncate | UiFlags.Label);
            UiDraw.TextMiddle(s, at, x, y, h, px - PriceW - UiTokens.S4 - x, UiStyle.SectionHeader, UiRole.Text2, m.ColumnGoods, UiFlags.Truncate | UiFlags.Label);
            return y + h;
        }

        // A row of goods: its picture on a tile, its name over its pack, a pack's price, the stepper, and the row's price.
        private static void Line(UiScene s, UiAt at, ref UiCheckoutLine l, int index, UiCheckoutModel m, float x, float y, float w)
        {
            float py = y + (LineH - PicH) / 2f;
            int tile = UiDraw.Box(s, at, x, py, PicW, PicH, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
            var card = new UiCatalogueCard { Picture = l.Picture, Aspect = l.Aspect, Icon = l.Icon };
            UiCatalogue.Picture(s, at.In(tile), ref card, x, py, PicW, PicH, UiTokens.S6);
            float right = x + w;
            UiDraw.TextMiddle(s, at, right - TotalW, y, LineH, TotalW, UiStyle.BodyStrong, UiRole.Text, l.Total, UiFlags.Right | UiFlags.Truncate);
            float px = right - TotalW - UiTokens.S4;
            if (m.Choose)
            {
                px -= StepW;
                UiDraw.Stepper(s, at, px, y + (LineH - UiTokens.Control) / 2f, StepW, l.Packs.ToString(System.Globalization.CultureInfo.InvariantCulture), l.CanLess, l.CanMore,
                    LessCommand, PackCommand, MoreCommand, index, false);
                px -= UiTokens.S4;
            }
            string each = string.IsNullOrEmpty(l.Price) ? null : (m.PriceEach ?? "{price}").Replace("{price}", l.Price);
            UiDraw.TextMiddle(s, at, px - PriceW, y, LineH, PriceW, UiStyle.Body, UiRole.Text2, each, UiFlags.Right | UiFlags.Truncate);
            float nx = x + PicW + UiTokens.S3, nw = px - PriceW - UiTokens.S4 - nx;
            bool pack = !string.IsNullOrEmpty(l.Pack);
            float block = UiTextRules.LineHeight(UiStyle.Title) + (pack ? UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small) : 0f), ny = y + (LineH - block) / 2f;
            UiDraw.Text(s, at, nx, ny, nw, UiStyle.Title, UiRole.Text, l.Name, UiFlags.Truncate | UiFlags.Middle);   // the pair centred on the row
            if (pack)
            {
                UiDraw.Text(s, at, nx, ny + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1, nw, UiStyle.Small, UiRole.Text2, l.Pack, UiFlags.Truncate | UiFlags.Middle);
            }
        }

        // A tax owed: its name over what is owed, the amount to pay in its field, and the button that pays it.
        private static void TaxRow(UiScene s, UiAt at, ref UiCheckoutTax t, int index, UiCheckoutModel m, float x, float y, float w, IUiKeys keys)
        {
            float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Pay, UiIcon.None, UiAction.Count, keys), by = y + (LineH - UiTokens.Control) / 2f;
            float right = x + w;
            UiDraw.Button(s, at, right - bw, by, bw, UiTokens.Control, UiButtonKind.Secondary, m.Pay, UiIcon.None, UiAction.Count, keys, TaxPayCommand, index);
            float fw = 200f, fx = right - bw - UiTokens.S3 - fw;
            UiDraw.TextField(s, at, fx, by, fw, t.Amount, m.AmountHint, TaxTypeCommand, index, false);
            float nw = fx - UiTokens.S4 - x;
            float block = UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small), ny = y + (LineH - block) / 2f;
            UiDraw.Text(s, at, x, ny, nw, UiStyle.Title, UiRole.Text, t.Name, UiFlags.Truncate | UiFlags.Middle);
            string owed = (m.OwedWords ?? "{amount}").Replace("{amount}", t.Owed ?? string.Empty);
            UiDraw.Text(s, at, x, ny + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1, nw, UiStyle.Small, UiRole.Text2, owed, UiFlags.Truncate | UiFlags.Middle);
        }

        // The goods and the tax in them, a row each on a band under the goods, their sum the total at the foot.
        private static float Sums(UiScene s, UiAt at, UiCheckoutModel m, float x, float y)
        {
            float h = SumsH(m);
            if (h <= 0f)
            {
                return y;
            }
            UiDraw.Band(s, at, x, y, Width, h);
            float ry = y + UiTokens.S2, ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            for (int i = 0; i < m.Sums.Length; i++)
            {
                ry += UiDraw.InfoRow(s, at, ix, ry, iw, m.Sums[i].Label, m.Sums[i].Value, false, -1, 0, m.Sums[i].Ink == UiRole.None ? UiRole.Text : m.Sums[i].Ink);
            }
            return y + h;
        }

        // The player's place in the register's line while the goods are still chosen: its clock and the words, on a hairline.
        private static float Place(UiScene s, UiAt at, UiCheckoutModel m, float x, float y)
        {
            float h = PlaceH(m);
            if (h <= 0f)
            {
                return y;
            }
            UiDraw.Box(s, at, x, y, Width, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float ix = x + UiTokens.S4, iy = y + UiTokens.S3;
            UiDraw.Icon(s, at, ix, iy + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Clock, UiRole.Warn, UiFlags.Middle, true);
            float tx = ix + UiTokens.Icon + UiTokens.S3;
            UiDraw.TextMiddle(s, at, tx, iy, UiTokens.PairRow, x + Width - UiTokens.S4 - tx, UiStyle.Body, UiRole.Text, m.QueueDetail, UiFlags.Truncate);
            return y + h;
        }

        // Once the order is in: the queue in the game's words, on a band, with its clock.
        private static float Queue(UiScene s, UiAt at, UiCheckoutModel m, float x, float y)
        {
            float h = QueueH(m);
            UiDraw.Band(s, at, x, y, Width, h);
            float ix = x + UiTokens.S4, iy = y + UiTokens.S4;
            UiDraw.Icon(s, at, ix, iy + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Clock, UiRole.Warn, UiFlags.Middle, true);
            float tx = ix + UiTokens.Icon + UiTokens.S3, tw = Width - UiTokens.S4 - (tx - x);
            UiDraw.TextMiddle(s, at, tx, iy, UiTokens.PairRow, tw, UiStyle.BodyStrong, UiRole.Text, m.Queue, UiFlags.Truncate);
            if (!string.IsNullOrEmpty(m.QueueDetail))
            {
                UiDraw.TextMiddle(s, at, tx, iy + UiTokens.PairRow, UiTokens.PairRow, tw, UiStyle.Body, UiRole.Text2, m.QueueDetail, UiFlags.Truncate);
            }
            return y + h;
        }

        // What it costs as the large figure, the clock held, and Cancel and Order with their keys, Order the one primary.
        private static void Footer(UiScene s, UiAt at, UiCheckoutModel m, float x, float y, IUiKeys keys, bool total)
        {
            UiDraw.Box(s, at, x, y, Width, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float ix = x + UiTokens.S4, right = x + Width - UiTokens.S4, by = y + (Foot - UiTokens.Control) / 2f;
            if (m.Order != null)
            {
                float ow = UiDraw.ButtonWidth(UiButtonKind.Primary, m.Order, UiIcon.None, UiAction.Alt, keys);
                right -= ow;
                UiDraw.Button(s, at, right, by, ow, UiTokens.Control, UiButtonKind.Primary, m.Order, UiIcon.None, UiAction.Alt, keys, OrderCommand, 0, !m.OrderEnabled);
                right -= UiTokens.S3;
            }
            if (m.Cancel != null)
            {
                float cw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Cancel, UiIcon.None, UiAction.Back, keys);
                right -= cw;
                UiDraw.Button(s, at, right, by, cw, UiTokens.Control, UiButtonKind.Secondary, m.Cancel, UiIcon.None, UiAction.Back, keys, CancelCommand, 0, !m.CancelEnabled);
                right -= UiTokens.S4;
            }
            float left = ix;
            if (total && !string.IsNullOrEmpty(m.Total))
            {
                float lh = UiTextRules.LineHeight(UiStyle.SectionHeader), fh = UiTextRules.LineHeight(UiStyle.Display), block = lh + UiTokens.S1 + fh, ty = y + (Foot - block) / 2f;
                float tw = Math.Min(UiDraw.Snap(Math.Max(UiTextRules.Width(m.Total, UiStyle.Display), UiTextRules.Width(m.TotalLabel, UiStyle.SectionHeader)) + UiTokens.S1), right - ix);
                UiDraw.Text(s, at, ix, ty, tw, UiStyle.SectionHeader, UiRole.Text2, m.TotalLabel, UiFlags.Truncate | UiFlags.Label | UiFlags.Middle);   // the pair centred on the foot
                UiDraw.Text(s, at, ix, ty + lh + UiTokens.S1, tw, UiStyle.Display, UiRole.Text, m.Total, UiFlags.Truncate | UiFlags.Middle);
                left = ix + tw + UiTokens.S6;
            }
            if (!string.IsNullOrEmpty(m.Paused) && right - left > UiTokens.S16)
            {
                float pw = Math.Min(right - left, UiDraw.Snap(UiTokens.Icon + UiTokens.S2 + UiTextRules.Width(m.Paused, UiStyle.Body) + UiTokens.S1));
                float py = y + (Foot - UiTokens.PairRow) / 2f;
                UiDraw.Icon(s, at, right - pw, py + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Pause, UiRole.Text2, UiFlags.Middle);
                UiDraw.TextMiddle(s, at, right - pw + UiTokens.Icon + UiTokens.S2, py, UiTokens.PairRow, pw - UiTokens.Icon - UiTokens.S2, UiStyle.Body, UiRole.Text2, m.Paused, UiFlags.Truncate);
            }
        }
    }
}
