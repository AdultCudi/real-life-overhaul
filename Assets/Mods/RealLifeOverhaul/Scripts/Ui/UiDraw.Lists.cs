using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The kit's rows: a list row, a key and its value, a service card, a choice card and a table.</summary>
    public static partial class UiDraw
    {
        /// <summary>
        /// A list row: the avatar with its online dot, the name with its tick, the preview, the time and the unread badge.
        /// Selected, the row's fill turns to the accent's and the selection bar stands at its left. Returns the focus target,
        /// or -1 for a row that is words only (<paramref name="target"/> false: a press on it would open nothing). A
        /// <paramref name="plain"/> row has no fill of its own, for a list inside a panel, where a card in a card is clutter.
        /// </summary>
        public static int ListRow(UiScene s, UiAt at, float x, float y, float w, ref UiRowModel row, int command, int arg, short list, int index, bool target = true,
            bool plain = false)
        {
            float h = UiTokens.Row;
            bool sel = row.Selected;
            UiRole ground = plain ? UiRole.Panel : UiRole.Row;
            int face = Box(s, at, x, y, w, h, sel ? UiRole.RowSel : plain ? UiRole.None : UiRole.Row, UiRole.None, UiTokens.RadiusLarge,
                sel ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float ax = x + UiTokens.S3, ay = y + (h - UiTokens.AvatarRow) / 2f, size = UiTokens.AvatarRow;
            int avatar;
            if (row.Icon != UiIcon.None)
            {
                // A business or a place has no face: its icon on a tile of its family's colour says what it is.
                avatar = Tile(s, inside, ax, ay, size, row.Tint, false);
                float g = Snap(size * 0.6f);
                Icon(s, inside.In(avatar), ax + (size - g) / 2f, ay + (size - g) / 2f, g, row.Icon, row.Tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            else
            {
                avatar = Portrait(s, inside, ax, ay, size, row.Name, row.Hue, UiRole.None);
            }
            if (row.Online)
            {
                float ring = UiTokens.S4;
                int r = Circle(s, at.In(avatar), ax + size - UiTokens.S3, ay + size - UiTokens.S3, ring, ring, sel ? UiRole.RowSel : ground, UiFlags.Overflow);
                Circle(s, at.In(r), ax + size - UiTokens.S3 + (ring - UiTokens.Dot) / 2f, ay + size - UiTokens.S3 + (ring - UiTokens.Dot) / 2f, UiTokens.Dot, UiTokens.Dot, UiRole.Ok,
                    UiFlags.Middle | UiFlags.Centre | UiFlags.Overflow);
            }
            // The right column: the time on the name's line, the badge on the preview's.
            float nameH = UiTextRules.LineHeight(UiStyle.Title), prevH = UiTextRules.LineHeight(UiStyle.Small);
            float top = y + (h - nameH - UiTokens.S1 - prevH) / 2f;
            float rightW = 0f;
            if (!string.IsNullOrEmpty(row.Time))
            {
                rightW = UiTextRules.Width(row.Time, UiStyle.Small) + UiTokens.S1;
                Text(s, inside, x + w - UiTokens.S4 - rightW, top + (nameH - UiTextRules.LineHeight(UiStyle.Small)) / 2f, rightW, UiStyle.Small, sel ? UiRole.Text : UiRole.Muted, row.Time,
                    UiFlags.Right | UiFlags.Middle);
            }
            if (row.Unread > 0)
            {
                float bw = Badge(s, inside, x + w - UiTokens.S4, top + nameH + UiTokens.S1 + (prevH - UiTokens.Badge) / 2f, row.Unread, true);
                s.Items[s.ItemCount - 2].Flags |= UiFlags.Middle;
                s.Items[s.ItemCount - 3].Flags |= UiFlags.Middle;
                rightW = Math.Max(rightW, bw);
            }
            float tx = ax + size + UiTokens.S3;
            float tw = x + w - UiTokens.S4 - (rightW > 0f ? rightW + UiTokens.S3 : 0f) - tx;
            float tick = row.Verified ? UiTokens.IconSmall + UiTokens.S1 : 0f;
            float nameW = Math.Min(tw - tick, UiTextRules.Width(row.Name, UiStyle.Title) + UiTokens.S1);
            Text(s, inside, tx, top, nameW, UiStyle.Title, UiRole.Text, row.Name, UiFlags.Truncate | UiFlags.Middle);
            if (row.Verified)
            {
                Icon(s, inside, tx + nameW + UiTokens.S1, top + (nameH - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Check, UiRole.None,
                    UiFlags.Middle | UiFlags.Centre);
            }
            Text(s, inside, tx, top + nameH + UiTokens.S1, tw, row.Unread > 0 ? UiStyle.SmallStrong : UiStyle.Small, row.Unread > 0 || sel ? UiRole.Text : UiRole.Muted, row.Preview,
                UiFlags.Truncate | UiFlags.Middle);   // on the cyan of a chosen row only the main words keep 4.5:1
            return target ? Node(s, at, x, y, w, h, command, arg, face, false, list, index) : -1;
        }

        /// <summary>A key and its value on one line with a rule under them; returns the height.</summary>
        public static float KeyValue(UiScene s, UiAt at, float x, float y, float w, string key, string value)
        {
            float h = UiTokens.PairRow;
            // The value is player data and may end in an ellipsis past half the row; the key is a label and takes the rest,
            // on two lines when one is too few ("Wymagania dotyczące grafiku").
            float need = UiTextRules.Width(key, UiStyle.Small) + UiTokens.S1;
            float room = Math.Max(w * 0.55f, w - need - UiTokens.S3);
            UiStyle valueStyle = UiTextRules.Width(value, UiStyle.Body) + UiTokens.S1 <= room ? UiStyle.Body : UiStyle.Small;   // a long value shrinks a step rather than lose its end
            float vw = Math.Min(UiTextRules.Width(value, valueStyle) + UiTokens.S1, room);
            float kw = w - vw - UiTokens.S3;
            if (need <= kw)
            {
                kw = need;
                TextMiddle(s, at, x, y, h, kw, UiStyle.Small, UiRole.Text2, key, UiFlags.Label | UiFlags.Truncate);
            }
            else
            {
                float kh = UiTextRules.WrappedHeight(key, UiStyle.Small, kw, 2);
                Text(s, at, x, y + (h - kh) / 2f, kw, UiStyle.Small, UiRole.Muted, key, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 2);
            }
            // Overpass's figures are tabular, so a column of values lines up; no rule under, as a game's info panel packs its rows.
            TextMiddle(s, at, x + kw + UiTokens.S3, y, h, w - kw - UiTokens.S3, valueStyle, UiRole.Text, value, UiFlags.Right | UiFlags.Truncate);
            return h;
        }

        /// <summary>A service or action with its price ("Free", "$300", "Quote") and what it does. Returns the focus target; height in <paramref name="height"/>.</summary>
        public static int ServiceCard(UiScene s, UiAt at, float x, float y, float w, ref UiServiceModel service, int command, int arg, out float height)
        {
            float pw = UiTextRules.Width(service.Price, UiStyle.SmallStrong) + UiTokens.S1;
            float inner = w - UiTokens.S4 * 2f;
            float titleW = inner - pw - UiTokens.S3;
            float titleH = UiTextRules.WrappedHeight(service.Title, UiStyle.Title, titleW, 2);
            float descH = string.IsNullOrEmpty(service.About) ? 0f : UiTextRules.WrappedHeight(service.About, UiStyle.Small, inner, 2);
            height = Snap(UiTokens.S4 * 2f + titleH + (descH > 0f ? UiTokens.S1 + descH : 0f));
            int face = Box(s, at, x, y, w, height, UiRole.None, UiRole.None, UiTokens.RadiusLarge, service.Disabled ? UiFlags.Disabled : UiFlags.None);   // a row in its panel, not a card in a card
            UiAt inside = at.In(face);
            Box(s, inside, x + UiTokens.S4, y + height - UiTokens.Hairline, w - UiTokens.S4 * 2f, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            Text(s, inside, x + UiTokens.S4, y + UiTokens.S4, titleW, UiStyle.Title, UiRole.Text, service.Title, UiFlags.Wrap | UiFlags.Truncate, 2);
            float titleLine = UiTextRules.LineHeight(UiStyle.Title);
            Text(s, inside, x + w - UiTokens.S4 - pw, y + UiTokens.S4 + (titleLine - UiTextRules.LineHeight(UiStyle.SmallStrong)) / 2f, pw, UiStyle.SmallStrong,
                UiRole.Text, service.Price, UiFlags.Right | UiFlags.Middle);   // a price is a figure, not a link: the cyan is for links
            if (descH > 0f)
            {
                Text(s, inside, x + UiTokens.S4, y + UiTokens.S4 + titleH + UiTokens.S1, inner, UiStyle.Small, UiRole.Text2, service.About, UiFlags.Wrap | UiFlags.Truncate | UiFlags.Middle, 2);
            }
            return Node(s, at, x, y, w, height, command, arg, face, service.Disabled);
        }

        /// <summary>
        /// A choice for a desk session: its key's cap, its name and the trade-off it makes, and a note at the right (the
        /// time it takes), at least a given height (cards side by side stand level). Returns the focus target, and its
        /// height in <paramref name="height"/>.
        /// </summary>
        public static int ChoiceCard(UiScene s, UiAt at, float x, float y, float w, ref UiChoiceModel choice, IUiKeys keys, int command, int arg, out float height,
            float least = 0f)
        {
            bool sel = choice.Selected;
            float capW = HasKey(choice.Key, keys) ? KeyCapWidth(choice.Key, keys) : 0f;
            float nw = string.IsNullOrEmpty(choice.Note) ? 0f : UiTextRules.Width(choice.Note, UiStyle.Small) + UiTokens.S1;
            float tx = x + UiTokens.S5 + (capW > 0f ? capW + UiTokens.S4 : 0f);
            float tw = x + w - UiTokens.S5 - (nw > 0f ? nw + UiTokens.S4 : 0f) - tx;
            float nameH = UiTextRules.LineHeight(UiStyle.Title);
            float tradeH = UiTextRules.WrappedHeight(choice.Trade, UiStyle.Small, tw, 2);
            height = Math.Max(least, ChoiceCardHeight(ref choice, keys, w));
            int face = Box(s, at, x, y, w, height, sel ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge,
                (sel ? UiFlags.Selected : UiFlags.None) | (choice.Disabled ? UiFlags.Disabled : UiFlags.None));
            UiAt inside = at.In(face);
            if (!sel)
            {
                Box(s, inside, x + UiTokens.S5, y + height - UiTokens.Hairline, w - UiTokens.S5 * 2f, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            }
            if (capW > 0f)
            {
                KeyCap(s, inside, x + UiTokens.S5, y + (height - UiTokens.KeyCap) / 2f, choice.Key, keys, UiRole.Track, UiRole.Text);
                s.Items[s.ItemCount - 1].Flags |= UiFlags.Middle;
            }
            float top = y + (height - nameH - UiTokens.S1 - tradeH) / 2f;
            Text(s, inside, tx, top, tw, UiStyle.Title, UiRole.Text, choice.Name, UiFlags.Label | UiFlags.Middle);
            Text(s, inside, tx, top + nameH + UiTokens.S1, tw, UiStyle.Small, sel ? UiRole.Text : UiRole.Text2, choice.Trade, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 2);
            if (nw > 0f)
            {
                TextMiddle(s, inside, x + w - UiTokens.S5 - nw, y, height, nw, UiStyle.Small, sel ? UiRole.Text : UiRole.Muted, choice.Note, UiFlags.Right);
            }
            return Node(s, at, x, y, w, height, command, arg, face, choice.Disabled, -1, 0, choice.Key);
        }

        /// <summary>The height <see cref="ChoiceCard"/> takes at a width: its padding, the name and its trade on up to two lines.</summary>
        public static float ChoiceCardHeight(ref UiChoiceModel choice, IUiKeys keys, float w)
        {
            float capW = HasKey(choice.Key, keys) ? KeyCapWidth(choice.Key, keys) : 0f;
            float nw = string.IsNullOrEmpty(choice.Note) ? 0f : UiTextRules.Width(choice.Note, UiStyle.Small) + UiTokens.S1;
            float tw = w - UiTokens.S5 * 2f - (capW > 0f ? capW + UiTokens.S4 : 0f) - (nw > 0f ? nw + UiTokens.S4 : 0f);
            return Snap(UiTokens.S5 * 2f + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1 + UiTextRules.WrappedHeight(choice.Trade, UiStyle.Small, tw, 2));
        }

        /// <summary>
        /// The height a table takes in at most a height: its head and the rows that fit whole (all of them when they do), or
        /// room for its empty state, so its frame fits its rows and never shows a sliver of one.
        /// </summary>
        public static float TableHeight(UiTableModel table, float most)
        {
            float headH = UiTokens.ControlSmall, rowH = RowOf(table);
            int rows = table.Rows != null ? table.RowCount : 0;
            if (rows == 0)
            {
                return Math.Min(most, UiTokens.S16 * 3f);   // an empty table shows its words alone, with no head row over nothing
            }
            int fit = Math.Max(1, (int)((most - headH) / rowH));
            return headH + Math.Min(rows, fit) * rowH;
        }

        /// <summary>
        /// Lets a table's rows grow towards <see cref="UiTokens.TableRowMost"/> when all of them fit a room with height to
        /// spare, so a short table takes the page's height in calmer rows instead of stopping over an empty band. A table
        /// that scrolls keeps the dense row.
        /// </summary>
        public static void Breathe(UiTableModel table, float room)
        {
            int rows = table.Rows != null ? table.RowCount : 0;
            float rowsRoom = room - UiTokens.ControlSmall;
            table.RowHeight = rows == 0 || rows * UiTokens.TableRow > rowsRoom ? 0f : Math.Min(UiTokens.TableRowMost, Math.Max(UiTokens.TableRow, SnapDown(rowsRoom / rows)));
        }

        private static float RowOf(UiTableModel table) => table.RowHeight > UiTokens.TableRow ? table.RowHeight : UiTokens.TableRow;

        /// <summary>
        /// A table that stays readable: capitals over its columns, rows of a fixed height on alternate fills, figures to the
        /// right in the serif's even figures so their places line up, the selected row marked. Its rows scroll in their frame
        /// and only those in view are laid out. Each row a press opens is a focus target of one list, and a row that opens
        /// nothing is not one. An empty table is its empty state alone. Give it <see cref="TableHeight"/>. Returns the list,
        /// or -1 for an empty table.
        /// </summary>
        public static short Table(UiScene s, UiAt at, float x, float y, float w, float h, UiTableModel table, int command)
        {
            float headH = UiTokens.ControlSmall, rowH = RowOf(table);
            int cols = table.Columns.Length;
            if (table.Rows == null || table.RowCount == 0)
            {
                Empty(s, at, x, y, w, h, table.EmptyTitle, table.EmptyBody);
                table.Offset = 0f;
                return -1;
            }
            float fixedW = 0f, shares = 0f;
            for (int c = 0; c < cols; c++)
            {
                fixedW += table.Columns[c].Width;
                shares += table.Columns[c].Width <= 0f ? 1f : 0f;
            }
            float spare = Math.Max(0f, w - UiTokens.S4 * 2f - fixedW - UiTokens.S4 * (cols - 1));
            float cx = x + UiTokens.S4;
            for (int c = 0; c < cols; c++)
            {
                float cw = table.Columns[c].Width > 0f ? table.Columns[c].Width : SnapDown(spare / Math.Max(1f, shares));
                TextMiddle(s, at, cx, y, headH, cw, UiStyle.SectionHeader, UiRole.Muted, table.Columns[c].Title,
                    UiFlags.Label | UiFlags.Truncate | (table.Columns[c].Right ? UiFlags.Right : UiFlags.None));
                cx += cw + UiTokens.S4;
            }
            Box(s, at, x, y + headH - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float viewH = SnapDown(h - headH);
            int rows = table.RowCount;
            float offset = table.Offset;
            if (table.Reveal >= 0 && table.Reveal < rows)
            {
                offset = Math.Min(offset, table.Reveal * rowH);
                offset = Math.Max(offset, (table.Reveal + 1) * rowH - viewH);
            }
            offset = Math.Max(0f, Math.Min(offset, rows * rowH - viewH));
            table.Offset = offset;
            table.Reveal = -1;
            short clip = s.AddClip(x, y + headH, w, viewH, offset);
            int first = Math.Max(0, (int)(offset / rowH));
            int last = Math.Min(rows - 1, (int)((offset + viewH) / rowH));
            short list = s.AddList(rows, first, Math.Max(0, last - first + 1), clip, table.Selected);
            s.Clips[clip].ContentHeight = rows * rowH;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            for (int r = first; r <= last; r++)
            {
                bool sel = r == table.Selected;
                float ry = r * rowH;
                // Rows parted by a hairline, as a printed table's, rather than striped: stripes read as clutter on a calm page.
                int face = Box(s, inClip.Arriving(r - first), 0f, ry, w, rowH, sel ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusMedium,
                    sel ? UiFlags.Selected : UiFlags.None);
                UiAt inside = inClip.Arriving(r - first).In(face);
                Box(s, inside, UiTokens.S2, ry + rowH - UiTokens.Hairline, w - UiTokens.S4, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                float rx = UiTokens.S4;
                string[] cells = table.Rows[r];
                for (int c = 0; c < cols; c++)
                {
                    float cw = table.Columns[c].Width > 0f ? table.Columns[c].Width : SnapDown(spare / Math.Max(1f, shares));
                    string cell = cells != null && c < cells.Length ? cells[c] : string.Empty;
                    TextMiddle(s, inside, rx, ry, rowH, cw, c == 0 ? UiStyle.Title : UiStyle.Body, c == 0 || sel ? UiRole.Text : UiRole.Text2, cell,
                        UiFlags.Truncate | (table.Columns[c].Right ? UiFlags.Right : UiFlags.None));
                    rx += cw + UiTokens.S4;
                }
                if (table.RowLeads(r))
                {
                    Node(s, inClip, 0f, ry, w, rowH, command, r, face, false, list, r);
                }
            }
            return list;
        }
    }
}
