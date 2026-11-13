using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One photo on the album: its picture key, the day it belongs to, and whether it is marked, posted or picked.</summary>
    public struct UiAlbumPhoto
    {
        public string Picture;    // a picture key (UiPictures), or null for the glyph
        public int Day;
        public string DayLabel;   // the day's heading, the same for every photo of that day
        public string DayMeta;    // "8 photos", on every photo of that day
        public bool Favourite;
        public bool Posted;       // a post of the player's on Flare carries it
        public bool Picked;
    }

    /// <summary>One chip of the album's second row: a filter with its value, or the way to clear them.</summary>
    public struct UiAlbumChip
    {
        public string Label;
        public bool On;
        public int Command;
        public UiIcon Icon;
    }

    /// <summary>
    /// An album of photos under a heading a day, in the order the screen sorted them. The screen owns the order, the search, the
    /// filters, the favourites and which photos are picked. Game-free.
    /// </summary>
    public sealed class UiAlbumModel
    {
        public UiAlbumPhoto[] Photos = new UiAlbumPhoto[0];
        public int Count;
        public string Search;             // the words typed in the search box, or null
        public string SearchPlaceholder;
        public string SortLabel;
        public UiAlbumChip[] Chips = new UiAlbumChip[0];   // filters and "clear", after the sort
        public string SelectLabel, DoneLabel;
        public bool Selecting;
        public string PickedLabel;        // "3 selected"
        public int PickedCount;
        public string FavLabel, ShareLabel, DeleteLabel;
        public bool FavAll;               // every picked photo is a favourite already: the button takes the mark off
        public string CameraLabel;        // the camera's way from the Gallery; null for none
        public string EmptyTitle;
        public string EmptyBody;
        public string CountLabel;         // "12 photos" at the right of the second row
        public string StorageLabel;       // "12 of 300 photos kept for this save" under the grid, or null for none
        public bool StorageFull;          // the store is at its cap: the line is the accent's
        public float Offset;
        public int Reveal = -1;
        public int ViewFirst = -1, ViewLast = -1;   // out: the photos in view and a row either side, for the thumbnails to ask for
    }

    /// <summary>
    /// A catalogue of photos under a band a day: square thumbnails, a heart on a favourite, the picked ones filled. Above it a
    /// search box, the select button and the camera, and a row of sort and filter chips with the count; in select mode the
    /// favourite, share and delete buttons. Only the rows in view are laid out, and the model says which photos those are (and a
    /// row either side) for their thumbnails to be asked for. Commands: <see cref="Photo"/>, <see cref="Sort"/>, <see cref="Fav"/>,
    /// <see cref="Select"/>, <see cref="Delete"/>, <see cref="Search"/> (the typing target), <see cref="Share"/>, <see cref="Camera"/> and
    /// the chips' own. Game-free. Cost: a layout a change, the rows in view only.
    /// </summary>
    public static class UiAlbum
    {
        public const int Photo = 80, Sort = 81, Fav = 82, Select = 83, Delete = 84, Search = 85, Share = 86, Camera = 87;
        public const int FilterPlace = 88, FilterBusiness = 89, FilterDay = 94, FilterClear = 95;
        private const float Least = 224f;
        private const float Head = 32f;

        /// <summary>Lays the tools and the grid into the room a window gave the page.</summary>
        public static void Lay(UiScene s, UiAt at, UiAlbumModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float tools = Tools(s, at, m, x, y, w, keys);
            float foot = 0f;
            if (!string.IsNullOrEmpty(m.StorageLabel))
            {
                foot = UiTokens.Chip + UiTokens.S2;   // the storage line stands under the grid, as the old page's footer did
                UiDraw.TextMiddle(s, at, x, y + h - UiTokens.Chip, UiTokens.Chip, w, UiStyle.Small, m.StorageFull ? UiRole.Accent : UiRole.Muted, m.StorageLabel, UiFlags.Truncate);
            }
            Grid(s, at, m, x, y + tools + UiTokens.S3, w, h - tools - UiTokens.S3 - foot, Photo);
        }

        /// <summary>
        /// The grid alone, under its day headings: for the Gallery after its tools, and for Flare's photo picker after its own.
        /// <paramref name="photoCommand"/> is the command of a photo's target (its argument the photo's place in the album).
        /// </summary>
        public static void Grid(UiScene s, UiAt at, UiAlbumModel m, float x, float gy, float w, float gh, int photoCommand, bool onCard = false)
        {
            m.ViewFirst = m.ViewLast = -1;
            if (m.Count <= 0)
            {
                UiDraw.EmptyState(s, at, x, gy, w, Math.Max(UiTokens.S16, gh), UiIcon.Image, m.EmptyTitle, m.EmptyBody);
                m.Offset = 0f;
                return;
            }
            int columns = Math.Max(1, (int)((w + UiTokens.S3) / (Least + UiTokens.S3)));
            float cw = UiDraw.SnapDown((w - UiTokens.S3 * (columns - 1)) / columns);
            float content = Walk(m, columns, cw, 0f, 0f, false, s, at, 0f, -1, photoCommand);
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < m.Count)
            {
                float atY = Walk(m, columns, cw, 0f, 0f, false, s, at, 0f, m.Reveal, photoCommand);
                offset = Math.Min(offset, atY);
                offset = Math.Max(offset, atY + cw - gh);
            }
            offset = Math.Max(0f, Math.Min(offset, Math.Max(0f, content - gh)));
            m.Offset = offset;
            m.Reveal = -1;
            short clip = s.AddClip(x, gy, w, gh, offset);
            s.Clips[clip].ContentHeight = content;
            var inside = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = at.Order, Region = at.Region };
            if (onCard)
            {
                // On a card (an overlay's), the photos stand on a backing of the card's own colour inside the scroll region.
                int back = UiDraw.Box(s, inside, 0f, 0f, w, Math.Max(content, gh), UiRole.Panel, UiRole.None, 0f, UiFlags.Decor);
                inside = inside.In(back);
            }
            Walk(m, columns, cw, offset, offset + gh, true, s, inside, 0f, -1, photoCommand);
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S2, gy, gh, offset, content);
        }

        // The tools: the search box with the select and camera buttons, then the chips and the count; or, selecting, the picked
        // count with the favourite, share and delete buttons. Returns their height.
        private static float Tools(UiScene s, UiAt at, UiAlbumModel m, float x, float y, float w, IUiKeys keys)
        {
            float row = UiTokens.Control;
            float right = x + w;
            if (m.Selecting)
            {
                string done = string.IsNullOrEmpty(m.DoneLabel) ? m.SelectLabel : m.DoneLabel;
                float dw = UiDraw.ButtonWidth(UiButtonKind.Primary, done, UiIcon.Check, UiAction.Count, keys);
                right -= dw;
                UiDraw.Button(s, at, right, y, dw, row, UiButtonKind.Primary, done, UiIcon.Check, UiAction.Count, keys, Select, 0);
                right -= UiTokens.S2;
                bool any = m.PickedCount > 0;
                float lw = Math.Min(UiDraw.ButtonWidth(UiButtonKind.Destructive, m.DeleteLabel ?? string.Empty, UiIcon.Trash, UiAction.Count, keys), Math.Max(UiTokens.S16 * 2f, (right - x) * 0.3f));
                right -= lw;
                UiDraw.Button(s, at, right, y, lw, row, UiButtonKind.Destructive, m.DeleteLabel, UiIcon.Trash, UiAction.Count, keys, Delete, m.PickedCount, !any);
                right -= UiTokens.S2;
                float sw = Math.Min(UiDraw.ButtonWidth(UiButtonKind.Secondary, m.ShareLabel ?? string.Empty, UiIcon.Send, UiAction.Count, keys), Math.Max(UiTokens.S16 * 2f, (right - x) * 0.3f));
                right -= sw;
                UiDraw.Button(s, at, right, y, sw, row, UiButtonKind.Secondary, m.ShareLabel, UiIcon.Send, UiAction.Count, keys, Share, m.PickedCount, !any);
                right -= UiTokens.S2;
                float fw = Math.Min(UiDraw.ButtonWidth(UiButtonKind.Secondary, m.FavLabel ?? string.Empty, UiIcon.Heart, UiAction.Count, keys), Math.Max(UiTokens.S16 * 2f, (right - x) * 0.3f));
                right -= fw;
                UiDraw.Button(s, at, right, y, fw, row, UiButtonKind.Secondary, m.FavLabel, UiIcon.Heart, UiAction.Count, keys, Fav, m.FavAll ? 1 : 0, !any);
                right -= UiTokens.S3;
                if (!string.IsNullOrEmpty(m.PickedLabel))
                {
                    UiDraw.TextMiddle(s, at, x, y, row, Math.Max(UiTokens.S16, right - x), UiStyle.BodyStrong, UiRole.Text, m.PickedLabel, UiFlags.Truncate);
                }
                return row;
            }
            float cam = UiTokens.Control;
            if (!string.IsNullOrEmpty(m.CameraLabel))
            {
                right -= cam;
                UiDraw.IconButton(s, at, right, y, cam, UiIcon.Camera, UiRole.Text, Camera, 0);
                right -= UiTokens.S2;
            }
            float selW = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.SelectLabel ?? string.Empty, UiIcon.Check, UiAction.Count, keys);
            right -= selW;
            UiDraw.Button(s, at, right, y, selW, row, UiButtonKind.Secondary, m.SelectLabel, UiIcon.Check, UiAction.Count, keys, Select, 0);
            right -= UiTokens.S3;
            float searchW = Math.Max(UiTokens.S16 * 3f, right - x);
            UiDraw.Search(s, at, x, y, searchW, m.SearchPlaceholder, m.Search, null, Search);

            // The second row: sort, the filters, the count.
            float cy = y + row + UiTokens.S2;
            float chip = UiTokens.Chip;
            float rightEdge = x + w;
            if (!string.IsNullOrEmpty(m.CountLabel))
            {
                float cw = UiDraw.Snap(UiTextRules.Width(m.CountLabel, UiStyle.Small) + UiTokens.S2);
                rightEdge -= cw;
                UiDraw.TextMiddle(s, at, rightEdge, cy, chip, cw, UiStyle.Small, UiRole.Muted, m.CountLabel, UiFlags.Right | UiFlags.Truncate);
                rightEdge -= UiTokens.S3;
            }
            float cx = x;
            cx = Chip(s, at, cx, cy, chip, m.SortLabel, false, UiIcon.None, Sort, 0, keys, rightEdge);
            for (int i = 0; m.Chips != null && i < m.Chips.Length; i++)
            {
                cx = Chip(s, at, cx, cy, chip, m.Chips[i].Label, m.Chips[i].On, m.Chips[i].Icon, m.Chips[i].Command, 0, keys, rightEdge);
            }
            return row + UiTokens.S2 + chip;
        }

        private static float Chip(UiScene s, UiAt at, float x, float y, float h, string label, bool on, UiIcon icon, int command, int arg, IUiKeys keys, float limit)
        {
            if (string.IsNullOrEmpty(label) || x >= limit - UiTokens.S16)
            {
                return x;
            }
            float bw = UiDraw.ButtonWidth(on ? UiButtonKind.Primary : UiButtonKind.Chip, label, icon, UiAction.Count, keys);
            bw = Math.Min(bw, Math.Max(UiTokens.S16, limit - x));
            if (on)
            {
                int face = UiDraw.Box(s, at, x, y, bw, h, UiRole.Accent, UiRole.None, UiTokens.RadiusMedium, UiFlags.Selected);
                UiDraw.TextMiddle(s, at.In(face), x, y, h, bw, UiStyle.Button, UiRole.OnAccent, label, UiFlags.Centre | UiFlags.Label | UiFlags.Truncate);
                UiDraw.Node(s, at, x, y, bw, h, command, arg, face);
            }
            else
            {
                UiDraw.Button(s, at, x, y, bw, h, UiButtonKind.Chip, label, icon, UiAction.Count, keys, command, arg);
            }
            return x + bw + UiTokens.S2;
        }

        // Walks the days. With draw, lays what sits in view and notes which photos are in view and a row either side. Returns the
        // content height, or the reveal photo's y when asked.
        private static float Walk(UiAlbumModel m, int columns, float cw, float viewTop, float viewBottom, bool draw, UiScene s, UiAt at, float x, int reveal, int photoCommand)
        {
            float y = 0f;
            float rowH = cw + UiTokens.S3;
            int i = 0;
            while (i < m.Count)
            {
                int day = m.Photos[i].Day;
                int n = 0;
                while (i + n < m.Count && m.Photos[i + n].Day == day)
                {
                    n++;
                }
                if (reveal >= i && reveal < i + n)
                {
                    int row = (reveal - i) / columns;
                    return y + Head + UiTokens.S2 + row * rowH;
                }
                if (draw && y + Head > viewTop - Head && y < viewBottom)
                {
                    float gridW = columns * cw + (columns - 1) * UiTokens.S3;
                    string meta = m.Photos[i].DayMeta;
                    float metaW = string.IsNullOrEmpty(meta) ? 0f : UiDraw.Snap(Math.Min(gridW * 0.4f, UiTextRules.Width(meta, UiStyle.Small) + UiTokens.S2));
                    float labelW = Math.Max(UiTokens.S16, gridW - metaW - UiTokens.S2);
                    UiDraw.TextMiddle(s, at, x, y, Head, labelW, UiStyle.SectionHeader, UiRole.Text, m.Photos[i].DayLabel, UiFlags.Label | UiFlags.Truncate);
                    if (metaW > 0f)
                    {
                        UiDraw.TextMiddle(s, at, x + gridW - metaW, y, Head, metaW, UiStyle.Small, UiRole.Muted, meta, UiFlags.Right | UiFlags.Truncate);
                    }
                }
                y += Head + UiTokens.S2;
                int rows = (n + columns - 1) / columns;
                for (int r = 0; r < rows; r++)
                {
                    int firstOfRow = i + r * columns, lastOfRow = Math.Min(i + n - 1, firstOfRow + columns - 1);
                    if (draw && y + cw > viewTop - rowH && y < viewBottom + rowH)
                    {
                        m.ViewFirst = m.ViewFirst < 0 ? firstOfRow : Math.Min(m.ViewFirst, firstOfRow);
                        m.ViewLast = Math.Max(m.ViewLast, lastOfRow);
                    }
                    if (draw && y + cw > viewTop && y < viewBottom)
                    {
                        for (int c = 0; c < columns; c++)
                        {
                            int index = firstOfRow + c;
                            if (index >= i + n)
                            {
                                break;
                            }
                            Cell(s, at, ref m.Photos[index], index, x + c * (cw + UiTokens.S3), y, cw, photoCommand);
                        }
                    }
                    y += rowH;
                }
                i += n;
            }
            return reveal >= 0 ? 0f : y;
        }

        private static void Cell(UiScene s, UiAt at, ref UiAlbumPhoto photo, int index, float x, float y, float size, int photoCommand)
        {
            int face = UiDraw.Box(s, at, x, y, size, size, photo.Picked ? UiRole.RowSel : UiRole.Tile, UiRole.None, UiTokens.RadiusMedium, photo.Picked ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            if (!string.IsNullOrEmpty(photo.Picture))
            {
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre | UiFlags.Cover, X = x, Y = y, W = size, H = size, Text = photo.Picture, Value = 1f,
                    Clip = inside.Clip, Node = -1, Layer = inside.Layer, Order = inside.Order, Parent = face, Alpha = 1f
                };
                s.Add(ref item);
            }
            else
            {
                float g = UiTokens.S12;
                UiDraw.Icon(s, inside, x + (size - g) / 2f, y + (size - g) / 2f, g, UiIcon.Image, UiRole.Text, UiFlags.Middle | UiFlags.Centre, true);
            }
            float mark = UiTokens.IconSmall;
            if (photo.Favourite)
            {
                UiDraw.Icon(s, inside, x + size - mark - UiTokens.S2, y + UiTokens.S2, mark, UiIcon.Heart, UiRole.Text, UiFlags.Decor, true);
            }
            if (photo.Posted)
            {
                UiDraw.Icon(s, inside, x + UiTokens.S2, y + size - mark - UiTokens.S2, mark, UiIcon.Send, UiRole.Text, UiFlags.Decor, true);
            }
            if (photo.Picked)
            {
                UiDraw.Icon(s, inside, x + UiTokens.S2, y + UiTokens.S2, UiTokens.Icon, UiIcon.Check, UiRole.Text, UiFlags.Decor, true);
            }
            UiDraw.Node(s, at, x, y, size, size, photoCommand, index, face);
        }
    }
}
