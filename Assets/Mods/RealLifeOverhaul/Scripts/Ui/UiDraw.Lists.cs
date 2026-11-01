using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The kit's rows: a list row, a key and its value, a service card, a choice card and a table.</summary>
    public static partial class UiDraw
    {
        /// <summary>
        /// A list row: the avatar with its online dot, the name with its tick, the preview, the time and the unread badge;
        /// selected, the row's fill and edge turn to the accent and the selection bar stands at its left. Returns the focus target.
        /// </summary>
        public static int ListRow(UiScene s, UiAt at, float x, float y, float w, ref UiRowModel row, int command, int arg, short list, int index)
        {
            float h = UiTokens.Row;
            bool sel = row.Selected;
            int face = Box(s, at, x, y, w, h, sel ? UiRole.RowSel : UiRole.Row, sel ? UiRole.RowSelEdge : UiRole.RowEdge, UiTokens.RadiusLarge,
                sel ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            if (sel)
            {
                Box(s, inside, x, y + UiTokens.S4, UiTokens.SelectBar, h - UiTokens.S4 * 2f, UiRole.Accent, UiRole.None, UiTokens.SelectBar / 2f);
            }
            float ax = x + UiTokens.S4, ay = y + UiTokens.S4, size = UiTokens.AvatarRow;
            int avatar = Avatar(s, inside, ax, ay, size, row.Name, row.Hue);
            if (row.Online)
            {
                float ring = UiTokens.S4;
                int r = Circle(s, at.In(avatar), ax + size - UiTokens.S3, ay + size - UiTokens.S3, ring, ring, sel ? UiRole.RowSel : UiRole.Row, UiFlags.Overflow);
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
                Text(s, inside, x + w - UiTokens.S4 - rightW, top + (nameH - UiTextRules.LineHeight(UiStyle.Small)) / 2f, rightW, UiStyle.Small, UiRole.Muted, row.Time,
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
                Icon(s, inside, tx + nameW + UiTokens.S1, top + (nameH - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Check, UiRole.AccentText,
                    UiFlags.Middle | UiFlags.Centre);
            }
            Text(s, inside, tx, top + nameH + UiTokens.S1, tw, row.Unread > 0 ? UiStyle.SmallStrong : UiStyle.Small, row.Unread > 0 ? UiRole.Text : UiRole.Muted, row.Preview,
                UiFlags.Truncate | UiFlags.Middle);
            return Node(s, at, x, y, w, h, command, arg, face, false, list, index);
        }

        /// <summary>A key and its value on one line with a rule under them; returns the height.</summary>
        public static float KeyValue(UiScene s, UiAt at, float x, float y, float w, string key, string value)
        {
            float h = UiTokens.PairRow;
            // The value is player data and may end in an ellipsis past half the row; the key is a label and takes the rest,
            // on two lines when one is too few ("Wymagania dotyczące grafiku").
            float vw = Math.Min(UiTextRules.Width(value, UiStyle.Small) + UiTokens.S1, w * 0.55f);
            float kw = w - vw - UiTokens.S3;
            float need = UiTextRules.Width(key, UiStyle.Small) + UiTokens.S1;
            if (need <= kw)
            {
                kw = need;
                TextMiddle(s, at, x, y, h, kw, UiStyle.Small, UiRole.Muted, key, UiFlags.Label | UiFlags.Truncate);
            }
            else
            {
                float kh = UiTextRules.WrappedHeight(key, UiStyle.Small, kw, 2);
                Text(s, at, x, y + (h - kh) / 2f, kw, UiStyle.Small, UiRole.Muted, key, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 2);
            }
            TextMiddle(s, at, x + kw + UiTokens.S3, y, h, w - kw - UiTokens.S3, UiStyle.Small, UiRole.Text, value, UiFlags.Right | UiFlags.Truncate);
            Box(s, at, x, y + h - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
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
            int face = Box(s, at, x, y, w, height, UiRole.Field, UiRole.RowEdge, UiTokens.RadiusLarge, service.Disabled ? UiFlags.Disabled : UiFlags.None);
            UiAt inside = at.In(face);
            Text(s, inside, x + UiTokens.S4, y + UiTokens.S4, titleW, UiStyle.Title, UiRole.Text, service.Title, UiFlags.Wrap | UiFlags.Truncate, 2);
            float titleLine = UiTextRules.LineHeight(UiStyle.Title);
            Text(s, inside, x + w - UiTokens.S4 - pw, y + UiTokens.S4 + (titleLine - UiTextRules.LineHeight(UiStyle.SmallStrong)) / 2f, pw, UiStyle.SmallStrong,
                UiRole.AccentText, service.Price, UiFlags.Right | UiFlags.Middle);
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
            int face = Box(s, at, x, y, w, height, sel ? UiRole.RowSel : UiRole.Field, sel ? UiRole.RowSelEdge : UiRole.RowEdge, UiTokens.RadiusLarge,
                (sel ? UiFlags.Selected : UiFlags.None) | (choice.Disabled ? UiFlags.Disabled : UiFlags.None));
            UiAt inside = at.In(face);
            if (sel)
            {
                Box(s, inside, x, y + UiTokens.S4, UiTokens.SelectBar, height - UiTokens.S4 * 2f, UiRole.Accent, UiRole.None, UiTokens.SelectBar / 2f);
            }
            if (capW > 0f)
            {
                KeyCap(s, inside, x + UiTokens.S5, y + (height - UiTokens.KeyCap) / 2f, choice.Key, keys, UiRole.Track, UiRole.Text);
                s.Items[s.ItemCount - 1].Flags |= UiFlags.Middle;
            }
            float top = y + (height - nameH - UiTokens.S1 - tradeH) / 2f;
            Text(s, inside, tx, top, tw, UiStyle.Title, UiRole.Text, choice.Name, UiFlags.Label | UiFlags.Middle);
            Text(s, inside, tx, top + nameH + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, choice.Trade, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 2);
            if (nw > 0f)
            {
                TextMiddle(s, inside, x + w - UiTokens.S5 - nw, y, height, nw, UiStyle.Small, UiRole.Muted, choice.Note, UiFlags.Right);
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
        /// room for its empty state; so its frame fits its rows and never shows a sliver of one.
        /// </summary>
        public static float TableHeight(UiTableModel table, float most)
        {
            float headH = UiTokens.ControlSmall, rowH = RowOf(table);
            int rows = table.Rows != null ? table.RowCount : 0;
            if (rows == 0)
            {
                return Math.Min(most, headH + UiTokens.S16 * 3f);
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
        /// right, the selected row marked; its rows scroll in their frame and only those in view are laid out. Each row is a
        /// focus target of one list. Give it <see cref="TableHeight"/>. Returns the list.
        /// </summary>
        public static short Table(UiScene s, UiAt at, float x, float y, float w, float h, UiTableModel table, int command)
        {
            float headH = UiTokens.ControlSmall, rowH = RowOf(table);
            int cols = table.Columns.Length;
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
            int rows = table.Rows != null ? table.RowCount : 0;
            float offset = Math.Max(0f, Math.Min(table.Offset, rows * rowH - viewH));
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
                int face = Box(s, inClip.Arriving(r - first), 0f, ry, w, rowH, sel ? UiRole.RowSel : r % 2 == 0 ? UiRole.Row : UiRole.None, UiRole.None, UiTokens.RadiusMedium,
                    sel ? UiFlags.Selected : UiFlags.None);
                UiAt inside = inClip.Arriving(r - first).In(face);
                if (sel)
                {
                    Box(s, inside, 0f, ry + UiTokens.S3, UiTokens.SelectBar, rowH - UiTokens.S3 * 2f, UiRole.Accent, UiRole.None, UiTokens.SelectBar / 2f);
                }
                float rx = UiTokens.S4;
                string[] cells = table.Rows[r];
                for (int c = 0; c < cols; c++)
                {
                    float cw = table.Columns[c].Width > 0f ? table.Columns[c].Width : SnapDown(spare / Math.Max(1f, shares));
                    string cell = cells != null && c < cells.Length ? cells[c] : string.Empty;
                    TextMiddle(s, inside, rx, ry, rowH, cw, c == 0 ? UiStyle.Title : UiStyle.Body, c == 0 ? UiRole.Text : UiRole.Text2, cell,
                        UiFlags.Truncate | (table.Columns[c].Right ? UiFlags.Right : UiFlags.None));
                    rx += cw + UiTokens.S4;
                }
                Node(s, inClip, 0f, ry, w, rowH, command, r, face, false, list, r);
            }
            if (rows == 0 && !string.IsNullOrEmpty(table.EmptyTitle))
            {
                Empty(s, at, x, y + headH, w, viewH, table.EmptyTitle, table.EmptyBody);
            }
            return list;
        }
    }
}
