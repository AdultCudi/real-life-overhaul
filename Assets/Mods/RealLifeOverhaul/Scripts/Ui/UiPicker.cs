using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>An entry of a picker: its glyph on a tile, its name and a line under it, its figure or state, and why it is greyed.</summary>
    public struct UiPickEntry
    {
        public UiIcon Icon;
        public string Name;        // "Hudson Street station"
        public string Detail;      // "4 min, line 2"; null for none
        public string Figure;      // "$2.75" or "Open"; null for none
        public bool Disabled;      // seen but not chosen: its reason says why
        public string Reason;      // "Closed until 06:00": a chip in place of its figure
        public bool Note;          // a line of the list that is no entry (the game's note rows): no tile, no target
        public string Mark;        // a short code on the tile in place of the glyph (a subway line's "2"), as a bullet; null for the glyph
        public UiRole MarkTone;    // the bullet's colour, the line's own: a chart hue, with the code in the dark ink on it
    }

    /// <summary>
    /// What a picker shows: its title, two rows of context, its entries a page at a time, and whether the player may leave
    /// it. One model for the mod's TravelList and DeskList, the game's subway station list and ItemsList.
    /// </summary>
    public sealed class UiPickerModel
    {
        public const int MostContext = 2;

        public string Title;                               // "Take the subway"
        public UiIcon Icon = UiIcon.MapTrifold;
        public UiInfoRow[] Context = new UiInfoRow[0];
        public UiPickEntry[] Entries = new UiPickEntry[0];
        public int PerPage = 8;
        public int Page;                                   // the page shown, from 0
        public string PageWords = "{0} of {1}";            // the page counter, {0} this page and {1} the pages
        public bool Locked;                                // custody: no close and no Back, the player must pick
        public string EmptyTitle, EmptyBody;
        public UiIcon EmptyIcon = UiIcon.MapTrifold;      // what would fill the list, on the empty state's tile
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The picker: a slate panel centred over the running game for choosing one of a list (where to travel, which desk,
    /// which station, which item): its title strip (no close while locked), two rows of context on a band, the entries as
    /// rows (a glyph on its tile, the name over its line, the figure at the right, or the reason it is greyed as a chip),
    /// and, when the list is long, the pages in a bar fixed at its foot with the dimmed arrow where no page lies. Commands:
    /// <see cref="EntryCommand"/> (an entry's index), <see cref="PageCommand"/> (-1 or 1) and the strip's close (Back).
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiPicker
    {
        public const int EntryCommand = 230, PageCommand = 231;
        public const float Width = 600f;          // px: two columns of words beside a tile, as the game's lists
        private const float Row = UiTokens.Row;   // px: an entry, its tile with room round it
        private const float Foot = UiTokens.Control + UiTokens.S2;   // px: the pages' bar
        private const byte ListRegion = 1, FootRegion = 2;

        public static int Pages(UiPickerModel m) => Math.Max(1, (m.Entries.Length + Math.Max(1, m.PerPage) - 1) / Math.Max(1, m.PerPage));

        /// <summary>The panel's height for its model: the strip, the context, a page of entries and the pages' bar when there are several.</summary>
        public static float Height(UiPickerModel m)
        {
            float h = UiFrame.Strip;
            int context = Math.Min(UiPickerModel.MostContext, m.Context.Length);
            if (context > 0)
            {
                h += UiTokens.S2 * 2f + context * UiTokens.PairRow;
            }
            int shown = Shown(m);
            h += shown == 0 ? EmptyRoom(m) : UiTokens.S2 + shown * Row + UiTokens.S2;
            if (Pages(m) > 1)
            {
                h += Foot;
            }
            return UiDraw.Snap(h);
        }

        // The empty state's room: its glyph's tile and its words with a step of room round them, so nothing floats in a hole.
        private static float EmptyRoom(UiPickerModel m) =>
            UiDraw.Snap(UiTokens.S12 + UiTokens.S4 + UiDraw.EmptyHeight(Width - UiTokens.S6 * 2f, m.EmptyTitle, m.EmptyBody) - UiTokens.S12 * 2f + UiTokens.S6 * 2f);

        // A tile says something only where the entries differ: a list of one kind of thing reads by its names.
        private static bool Differ(UiPickerModel m)
        {
            for (int i = 1; i < m.Entries.Length; i++)
            {
                if (!m.Entries[i].Note && (m.Entries[i].Icon != m.Entries[0].Icon || m.Entries[i].Mark != m.Entries[0].Mark))
                {
                    return true;
                }
            }
            return false;
        }

        private static int Shown(UiPickerModel m)
        {
            int first = Math.Max(0, Math.Min(m.Page, Pages(m) - 1)) * Math.Max(1, m.PerPage);
            return Math.Max(0, Math.Min(m.PerPage, m.Entries.Length - first));
        }

        /// <summary>The picker as a screen of its own: centred over the world above the status bar, with the bar and its prompts.</summary>
        public static void Lay(UiScene s, UiPickerModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("picker", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float h = Height(m);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f), y = UiDraw.SnapDown(Math.Max(UiTokens.S8, (frame.BandY - h) / 2f));
            Draw(s, UiAt.Screen, m, x, y);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The panel alone at a place (its top left); returns its height.</summary>
        public static float Draw(UiScene s, UiAt at, UiPickerModel m, float x, float y)
        {
            float h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, Width, m.Icon, m.Title, !m.Locked);
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            int context = Math.Min(UiPickerModel.MostContext, m.Context.Length);
            if (context > 0)
            {
                float bh = UiTokens.S2 * 2f + context * UiTokens.PairRow;
                UiDraw.Band(s, on, x, cy, Width, bh);
                float ry = cy + UiTokens.S2;
                for (int i = 0; i < context; i++)
                {
                    ry += UiDraw.InfoRow(s, on, ix, ry, iw, m.Context[i].Label, m.Context[i].Value, false, -1, 0, m.Context[i].Ink == UiRole.None ? UiRole.Text : m.Context[i].Ink);
                }
                cy += bh;
            }
            int pages = Pages(m), page = Math.Max(0, Math.Min(m.Page, pages - 1));
            int first = page * Math.Max(1, m.PerPage), shown = Shown(m);
            if (shown == 0)
            {
                UiDraw.EmptyState(s, on, x + UiTokens.S6, cy, Width - UiTokens.S6 * 2f, EmptyRoom(m), m.EmptyIcon, m.EmptyTitle, m.EmptyBody);
                return h;
            }
            float ey = cy + UiTokens.S2;
            UiAt list = on.InRegion(ListRegion);
            bool tiles = Differ(m);
            for (int i = 0; i < shown; i++)
            {
                Entry(s, list, ref m.Entries[first + i], first + i, x + UiTokens.S2, ey, Width - UiTokens.S2 * 2f, tiles);
                ey += Row;
            }
            if (pages > 1)
            {
                Pager(s, on.InRegion(FootRegion), m, page, pages, x, y + h - Foot);
            }
            return h;
        }

        // An entry: its glyph on a tile, its name over its line, its figure at the right, or the reason it is greyed as a chip.
        private static void Entry(UiScene s, UiAt at, ref UiPickEntry e, int index, float x, float y, float w, bool tiles)
        {
            float tile = UiTokens.IconTile;
            if (e.Note)
            {
                UiDraw.Text(s, at, x + UiTokens.S2, y + (Row - UiTextRules.WrappedHeight(e.Name, UiStyle.Body, w - UiTokens.S4, 2)) / 2f, w - UiTokens.S4, UiStyle.Body, UiRole.Text2, e.Name,
                    UiFlags.Wrap | UiFlags.Middle, 2);
                return;
            }
            int drawn = s.ItemCount;
            int face = UiDraw.Box(s, at, x, y, w, Row, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float tx = x + UiTokens.S2, nx = x + UiTokens.S3;
            if (tiles)
            {
                int t = UiDraw.Tile(s, inside, tx, y + (Row - tile) / 2f, tile, UiRole.None, false);
                if (!string.IsNullOrEmpty(e.Mark))
                {
                    // A line's bullet, as the subway signs it: its colour on a small disc, its code in the dark ink.
                    float d = UiTokens.ControlSmall, dy = y + (Row - d) / 2f;
                    int disc = UiDraw.Circle(s, inside.In(t), tx + (tile - d) / 2f, dy, d, d, e.MarkTone == UiRole.None ? UiRole.Accent : e.MarkTone, UiFlags.Middle);
                    UiDraw.TextMiddle(s, inside.In(disc), tx + (tile - d) / 2f, dy, d, d, UiStyle.BodyStrong, UiRole.OnAccent, e.Mark, UiFlags.Centre | UiFlags.Truncate);
                }
                else
                {
                    UiDraw.Icon(s, inside.In(t), tx + (tile - UiTokens.Icon) / 2f, y + (Row - UiTokens.Icon) / 2f, UiTokens.Icon, e.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                }
                nx = tx + tile + UiTokens.S3;
            }
            float right = x + w - UiTokens.S3;
            if (e.Disabled && !string.IsNullOrEmpty(e.Reason))
            {
                float most = Math.Min(UiDraw.SnapDown(w * 0.42f), 280f);
                float cw = UiDraw.Snap(UiTokens.S2 + UiTokens.StatusDot + UiTokens.S2 + UiTextRules.Width(e.Reason, UiStyle.SmallStrong) + UiTokens.S1 + UiTokens.S2);
                cw = Math.Min(cw, most);
                float chipH = UiTokens.Badge + UiTokens.S1;
                UiDraw.Chip(s, inside, right - cw, y + (Row - chipH) / 2f, e.Reason, UiRole.Warn, most);
                right -= cw + UiTokens.S3;
            }
            else if (!string.IsNullOrEmpty(e.Figure))
            {
                float fw = Math.Min(UiTextRules.Width(e.Figure, UiStyle.BodyStrong) + UiTokens.S1, w * 0.3f);
                UiDraw.TextMiddle(s, inside, right - fw, y, Row, fw, UiStyle.BodyStrong, UiRole.Text, e.Figure, UiFlags.Right | UiFlags.Truncate);
                right -= fw + UiTokens.S3;
            }
            float nw = right - nx;
            bool detail = !string.IsNullOrEmpty(e.Detail);
            float nh = UiTextRules.LineHeight(UiStyle.Title), dh = detail ? UiTextRules.LineHeight(UiStyle.Small) : 0f;
            float block = nh + (detail ? UiTokens.S1 + dh : 0f), ny = y + (Row - block) / 2f;
            UiDraw.Text(s, inside, nx, ny, nw, UiStyle.Title, UiRole.Text, e.Name, UiFlags.Truncate | UiFlags.Middle);
            if (detail)
            {
                UiDraw.Text(s, inside, nx, ny + nh + UiTokens.S1, nw, UiStyle.Small, UiRole.Text2, e.Detail, UiFlags.Truncate | UiFlags.Middle);
            }
            int node = UiDraw.Node(s, at, x, y, w, Row, EntryCommand, index, face, e.Disabled);
            if (e.Disabled)
            {
                // Greyed as the game greys it, the reason's chip kept whole so it still reads.
                for (int i = drawn; i < s.ItemCount; i++)
                {
                    if (s.Items[i].Kind != UiKind.Text || s.Items[i].Style != UiStyle.SmallStrong)
                    {
                        s.Items[i].Alpha = UiDraw.DimAlpha;
                    }
                    s.Items[i].Flags |= UiFlags.Disabled;
                }
                s.Nodes[node].Disabled = true;
            }
        }

        // The pages' bar fixed at the panel's foot: the arrows either side of the counter, the one with no page that way dimmed.
        private static void Pager(UiScene s, UiAt at, UiPickerModel m, int page, int pages, float x, float y)
        {
            UiDraw.Box(s, at, x, y, Width, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            float b = UiTokens.ControlSmall, by = y + (Foot - b) / 2f;
            float cx = x + Width / 2f;
            string words = string.Format(System.Globalization.CultureInfo.InvariantCulture, m.PageWords ?? "{0} of {1}", page + 1, pages);
            float tw = UiDraw.Snap(UiTextRules.Width(words, UiStyle.Small) + UiTokens.S1);
            int drawn = s.ItemCount;
            int back = UiDraw.IconButton(s, at, UiDraw.SnapDown(cx - tw / 2f - UiTokens.S3 - b), by, b, UiIcon.Back, UiRole.Text, PageCommand, -1, false, UiAction.Count, false, true);
            if (page == 0)
            {
                UiDraw.Dim(s, drawn, back);
            }
            UiDraw.TextMiddle(s, at, UiDraw.SnapDown(cx - tw / 2f), y, Foot, tw, UiStyle.Small, UiRole.Text2, words, UiFlags.Centre);
            drawn = s.ItemCount;
            int next = UiDraw.IconButton(s, at, UiDraw.Snap(cx + tw / 2f + UiTokens.S3), by, b, UiIcon.Next, UiRole.Text, PageCommand, 1, false, UiAction.Count, false, true);
            if (page >= pages - 1)
            {
                UiDraw.Dim(s, drawn, next);
            }
        }
    }
}
