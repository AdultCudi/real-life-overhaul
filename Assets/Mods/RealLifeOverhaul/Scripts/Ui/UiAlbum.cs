using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One photo on the album: its picture key, the day it belongs to, and whether it is marked or picked.</summary>
    public struct UiAlbumPhoto
    {
        public string Picture;    // a picture key (UiPictures), or null for the glyph
        public int Day;
        public string DayLabel;   // the day's heading, the same for every photo of that day
        public string DayMeta;    // "8 photos", on every photo of that day
        public bool Favourite;
        public bool Picked;
    }

    /// <summary>
    /// An album of photos under a heading a day, newest or oldest as the screen sorted them. The screen owns the order,
    /// the favourites and which photos are picked. Game-free.
    /// </summary>
    public sealed class UiAlbumModel
    {
        public UiAlbumPhoto[] Photos = new UiAlbumPhoto[0];
        public int Count;
        public string SortLabel;
        public string FavLabel;
        public bool FavOn;
        public string SelectLabel;
        public bool Selecting;
        public string DeleteLabel;
        public int DeleteCount;
        public string EmptyTitle;
        public string EmptyBody;
        public string CountLabel;    // "12 photos" at the right of the tools
        public float Offset;
        public int Reveal = -1;
    }

    /// <summary>
    /// A catalogue of photos under a band a day: square thumbnails, a heart on a favourite, the picked ones filled.
    /// Only the rows in view are laid out. Commands: <see cref="Photo"/>, <see cref="Sort"/>, <see cref="Fav"/>,
    /// <see cref="Select"/> and <see cref="Delete"/>. Game-free. Cost: a layout a change, the rows in view only.
    /// </summary>
    public static class UiAlbum
    {
        public const int Photo = 80, Sort = 81, Fav = 82, Select = 83, Delete = 84;
        private const float Least = 148f;
        private const float Head = 32f;

        /// <summary>Lays the tools and the grid into the room a window gave the page.</summary>
        public static void Lay(UiScene s, UiAt at, UiAlbumModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float tools = Tools(s, at, m, x, y, w, keys);
            float gy = y + tools + UiTokens.S3;
            float gh = h - tools - UiTokens.S3;
            if (m.Count <= 0)
            {
                UiDraw.EmptyState(s, at, x, gy, w, Math.Max(UiTokens.S16, gh), UiIcon.Image, m.EmptyTitle, m.EmptyBody);
                m.Offset = 0f;
                return;
            }
            int columns = Math.Max(1, (int)((w + UiTokens.S3) / (Least + UiTokens.S3)));
            float cw = UiDraw.SnapDown((w - UiTokens.S3 * (columns - 1)) / columns);
            float content = Walk(m, columns, cw, 0f, 0f, false, s, at, 0f, 0f, -1);
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < m.Count)
            {
                float atY = Walk(m, columns, cw, 0f, 0f, false, s, at, 0f, 0f, m.Reveal);
                offset = Math.Min(offset, atY);
                offset = Math.Max(offset, atY + cw - gh);
            }
            offset = Math.Max(0f, Math.Min(offset, Math.Max(0f, content - gh)));
            m.Offset = offset;
            m.Reveal = -1;
            short clip = s.AddClip(x, y + tools + UiTokens.S3, w, gh, offset);
            s.Clips[clip].ContentHeight = content;
            var inside = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = at.Order, Region = at.Region };
            Walk(m, columns, cw, offset, offset + gh, true, s, inside, 0f, 0f, -1);
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S2, gy, gh, offset, content);
        }

        // The tools: sort, favourites, select, and delete once several are picked. Returns their height.
        private static float Tools(UiScene s, UiAt at, UiAlbumModel m, float x, float y, float w, IUiKeys keys)
        {
            float h = UiTokens.Chip;
            float right = x + w;
            if (m.Selecting && m.DeleteCount > 0 && !string.IsNullOrEmpty(m.DeleteLabel))
            {
                float dw = UiDraw.ButtonWidth(UiButtonKind.Destructive, m.DeleteLabel, UiIcon.Trash, UiAction.Count, keys);
                right -= dw;
                UiDraw.Button(s, at, right, y, dw, h, UiButtonKind.Destructive, m.DeleteLabel, UiIcon.Trash, UiAction.Count, keys, Delete, m.DeleteCount);
                right -= UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(m.CountLabel))
            {
                float cw = UiDraw.Snap(UiTextRules.Width(m.CountLabel, UiStyle.Small) + UiTokens.S2);
                right -= cw;
                UiDraw.TextMiddle(s, at, right, y, h, cw, UiStyle.Small, UiRole.Muted, m.CountLabel, UiFlags.Right | UiFlags.Truncate);
                right -= UiTokens.S3;
            }
            float cx = x;
            cx = Tool(s, at, cx, y, h, m.SortLabel, false, Sort, 0, keys, right);
            cx = Tool(s, at, cx, y, h, m.FavLabel, m.FavOn, Fav, 0, keys, right);
            Tool(s, at, cx, y, h, m.SelectLabel, m.Selecting, Select, 0, keys, right);
            return h;
        }

        private static float Tool(UiScene s, UiAt at, float x, float y, float h, string label, bool on, int command, int arg, IUiKeys keys, float limit)
        {
            if (string.IsNullOrEmpty(label) || x >= limit - UiTokens.S16)
            {
                return x;
            }
            float bw = UiDraw.ButtonWidth(on ? UiButtonKind.Primary : UiButtonKind.Chip, label, UiIcon.None, UiAction.Count, keys);
            bw = Math.Min(bw, Math.Max(UiTokens.S16, limit - x));
            if (on)
            {
                int face = UiDraw.Box(s, at, x, y, bw, h, UiRole.Accent, UiRole.None, UiTokens.RadiusMedium, UiFlags.Selected);
                UiDraw.TextMiddle(s, at.In(face), x, y, h, bw, UiStyle.Button, UiRole.OnAccent, label, UiFlags.Centre | UiFlags.Label | UiFlags.Truncate);
                UiDraw.Node(s, at, x, y, bw, h, command, arg, face);
            }
            else
            {
                UiDraw.Button(s, at, x, y, bw, h, UiButtonKind.Chip, label, UiIcon.None, UiAction.Count, keys, command, arg);
            }
            return x + bw + UiTokens.S2;
        }

        // Walks the days. With draw, lays what sits in view. Returns the content height, or the reveal photo's y when asked.
        private static float Walk(UiAlbumModel m, int columns, float cw, float viewTop, float viewBottom, bool draw, UiScene s, UiAt at, float x, float unused, int reveal)
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
                    if (draw && y + cw > viewTop && y < viewBottom)
                    {
                        for (int c = 0; c < columns; c++)
                        {
                            int index = i + r * columns + c;
                            if (index >= i + n)
                            {
                                break;
                            }
                            Cell(s, at, ref m.Photos[index], index, x + c * (cw + UiTokens.S3), y, cw);
                        }
                    }
                    y += rowH;
                }
                i += n;
            }
            return reveal >= 0 ? 0f : y;
        }

        private static void Cell(UiScene s, UiAt at, ref UiAlbumPhoto photo, int index, float x, float y, float size)
        {
            int face = UiDraw.Box(s, at, x, y, size, size, photo.Picked ? UiRole.RowSel : UiRole.Tile, UiRole.None, UiTokens.RadiusMedium, photo.Picked ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            if (!string.IsNullOrEmpty(photo.Picture))
            {
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = x, Y = y, W = size, H = size, Text = photo.Picture, Value = 1f,
                    Clip = inside.Clip, Node = -1, Layer = inside.Layer, Order = inside.Order, Parent = face, Alpha = 1f
                };
                s.Add(ref item);
            }
            else
            {
                float g = UiTokens.S12;
                UiDraw.Icon(s, inside, x + (size - g) / 2f, y + (size - g) / 2f, g, UiIcon.Image, UiRole.Text, UiFlags.Middle | UiFlags.Centre, true);
            }
            if (photo.Favourite)
            {
                float heart = UiTokens.IconSmall;
                UiDraw.Icon(s, inside, x + size - heart - UiTokens.S2, y + UiTokens.S2, heart, UiIcon.Heart, UiRole.Text, UiFlags.Decor, true);
            }
            UiDraw.Node(s, at, x, y, size, size, Photo, index, face);
        }
    }
}
