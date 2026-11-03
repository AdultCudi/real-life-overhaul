using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One fact beside a photo: its label and its value, a link when it leads somewhere.</summary>
    public struct UiPhotoFact
    {
        public string Label;
        public string Value;
        public bool Link;
    }

    /// <summary>
    /// One photo opened: its picture, its title, the facts, the tags, where it went, the actions and the neighbours.
    /// The screen fills it from the photo and its sidecar. Game-free.
    /// </summary>
    public sealed class UiPhotoModel
    {
        public string Picture;
        public string Title;
        public string Caption;
        public string Position;
        public UiPhotoFact[] Facts = new UiPhotoFact[0];
        public string FactsTitle;
        public string TagsTitle;
        public string[] Tags = new string[0];
        public string WhereTitle;
        public string Where;
        public string[] Neighbours = new string[0];   // picture keys, the open one included
        public int NeighbourAt = -1;
        public UiActionModel[] Actions = new UiActionModel[0];
        public bool CanBack = true, CanOn = true;
    }

    /// <summary>
    /// A photo large in the page, letterboxed on slate, with its facts beside it, its actions in a row and its
    /// neighbours in a strip. Commands: <see cref="Action"/>, <see cref="Neighbour"/>, <see cref="Step"/> (-1 or 1)
    /// and <see cref="Fact"/> for a link. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiPhotoViewer
    {
        public const int Action = 90, Neighbour = 91, Step = 92, Fact = 93;
        private const float Side = 360f;
        private const float NeighbourSize = 56f;

        /// <summary>Lays the viewer into the room a window gave the page.</summary>
        public static void Lay(UiScene s, UiAt at, UiPhotoModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float side = Math.Min(Side, UiDraw.SnapDown(w * 0.34f));
            float gap = UiTokens.S6;
            float photoW = w - side - gap;
            float foot = UiTokens.S2 + UiTokens.PairRow + UiTokens.S2 + UiTokens.Control + UiTokens.S3 + NeighbourSize;
            float photoH = Math.Max(UiTokens.S16 * 2f, h - foot);
            int tile = UiDraw.Box(s, at, x, y, photoW, photoH, UiRole.Tile, UiRole.None, UiTokens.RadiusMedium, UiFlags.Decor);
            if (!string.IsNullOrEmpty(m.Picture))
            {
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = x, Y = y, W = photoW, H = photoH, Text = m.Picture, Value = 1f,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = tile, Alpha = 1f
                };
                s.Add(ref item);
            }
            else
            {
                float g = UiTokens.S16;
                UiDraw.Icon(s, at.In(tile), x + (photoW - g) / 2f, y + (photoH - g) / 2f, g, UiIcon.Image, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
            float by = y + photoH + UiTokens.S2;
            float cap = string.IsNullOrEmpty(m.Caption) ? 0f : UiDraw.Snap(Math.Min(photoW * 0.45f, UiTextRules.Width(m.Caption, UiStyle.Small) + UiTokens.S1));
            UiDraw.TextMiddle(s, at, x, by, UiTokens.PairRow, photoW - cap - (cap > 0f ? UiTokens.S2 : 0f), UiStyle.Title, UiRole.Text, m.Title, UiFlags.Truncate);
            if (cap > 0f)
            {
                UiDraw.TextMiddle(s, at, x + photoW - cap, by, UiTokens.PairRow, cap, UiStyle.Small, UiRole.Muted, m.Caption, UiFlags.Right | UiFlags.Truncate);
            }
            by += UiTokens.PairRow + UiTokens.S2;
            Actions(s, at, m, x, by, photoW, keys);
            by += UiTokens.Control + UiTokens.S3;
            Neighbours(s, at, m, x, by, photoW);
            Facts(s, at, m, x + photoW + gap, y, side, h);
        }

        private static void Actions(UiScene s, UiAt at, UiPhotoModel m, float x, float y, float w, IUiKeys keys)
        {
            float back = UiTokens.Control;
            int drawn = s.ItemCount;
            int b = UiDraw.IconButton(s, at, x, y, back, UiIcon.Back, UiRole.Text, Step, -1);
            if (!m.CanBack)
            {
                UiDraw.Dim(s, drawn, b);
            }
            float cx = x + back + UiTokens.S2;
            float limit = x + w - back - UiTokens.S2;
            if (m.Actions != null)
            {
                for (int i = 0; i < m.Actions.Length && cx < limit; i++)
                {
                    ref UiActionModel a = ref m.Actions[i];
                    bool iconOnly = string.IsNullOrEmpty(a.Label);
                    if (iconOnly)
                    {
                        if (cx + back > limit)
                        {
                            break;
                        }
                        UiDraw.IconButton(s, at, cx, y, back, a.Icon, UiRole.Text, Action, i, false, a.Key);
                        cx += back + UiTokens.S2;
                        continue;
                    }
                    float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                    bw = Math.Min(bw, Math.Max(back, limit - cx));
                    UiDraw.Button(s, at, cx, y, bw, back, a.Kind, a.Label, a.Icon, a.Key, keys, Action, i, a.Disabled);
                    cx += bw + UiTokens.S2;
                }
            }
            drawn = s.ItemCount;
            int n = UiDraw.IconButton(s, at, x + w - back, y, back, UiIcon.Next, UiRole.Text, Step, 1);
            if (!m.CanOn)
            {
                UiDraw.Dim(s, drawn, n);
            }
        }

        private static void Neighbours(UiScene s, UiAt at, UiPhotoModel m, float x, float y, float w)
        {
            int n = m.Neighbours != null ? m.Neighbours.Length : 0;
            if (n == 0)
            {
                if (!string.IsNullOrEmpty(m.Position))
                {
                    UiDraw.TextMiddle(s, at, x, y, NeighbourSize, w, UiStyle.Small, UiRole.Muted, m.Position, UiFlags.Right | UiFlags.Truncate);
                }
                return;
            }
            float gap = UiTokens.S2;
            float row = n * NeighbourSize + (n - 1) * gap;
            float posW = string.IsNullOrEmpty(m.Position) ? 0f : UiDraw.Snap(Math.Min(w * 0.35f, UiTextRules.Width(m.Position, UiStyle.Small) + UiTokens.S1));
            float room = w - posW - UiTokens.S3;
            float sx = x;
            if (row > room)
            {
                // The strip keeps the open photo in view and drops what does not fit, rather than shrinking a target under its least.
                int fit = Math.Max(1, (int)((room + gap) / (NeighbourSize + gap)));
                int first = Math.Max(0, Math.Min(n - fit, m.NeighbourAt - fit / 2));
                n = Math.Min(n - first, fit);
                for (int i = 0; i < n; i++)
                {
                    Thumb(s, at, m, first + i, sx, y);
                    sx += NeighbourSize + gap;
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    Thumb(s, at, m, i, sx, y);
                    sx += NeighbourSize + gap;
                }
            }
            if (posW > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - posW, y, NeighbourSize, posW, UiStyle.Small, UiRole.Muted, m.Position, UiFlags.Right | UiFlags.Truncate);
            }
        }

        private static void Thumb(UiScene s, UiAt at, UiPhotoModel m, int index, float x, float y)
        {
            bool on = index == m.NeighbourAt;
            int face = UiDraw.Box(s, at, x, y, NeighbourSize, NeighbourSize, on ? UiRole.Accent : UiRole.Tile, UiRole.None, UiTokens.RadiusMedium, on ? UiFlags.Selected : UiFlags.None);
            string key = m.Neighbours[index];
            if (!string.IsNullOrEmpty(key))
            {
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = x + UiTokens.S1, Y = y + UiTokens.S1,
                    W = NeighbourSize - UiTokens.S2, H = NeighbourSize - UiTokens.S2, Text = key, Value = 1f,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = face, Alpha = 1f
                };
                s.Add(ref item);
            }
            UiDraw.Node(s, at, x, y, NeighbourSize, NeighbourSize, Neighbour, index, face);
        }

        private static float ChipWidth(string text, float most)
        {
            float tw = UiTextRules.Width(text, UiStyle.SmallStrong) + UiTokens.S1;
            float width = UiDraw.Snap(UiTokens.S2 + UiTokens.StatusDot + UiTokens.S2 + tw + UiTokens.S2);
            return most > 0f && width > most ? UiDraw.SnapDown(most) : width;
        }

        private static void Facts(UiScene s, UiAt at, UiPhotoModel m, float x, float y, float w, float h)
        {
            float cy = y;
            float foot = y + h;
            if (!string.IsNullOrEmpty(m.FactsTitle))
            {
                UiDraw.Text(s, at, x, cy, w, UiStyle.SectionHeader, UiRole.Muted, m.FactsTitle, UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.PairRow;
            }
            if (m.Facts != null)
            {
                for (int i = 0; i < m.Facts.Length && cy + UiTokens.PairRow <= foot; i++)
                {
                    cy += UiDraw.InfoRow(s, at, x, cy, w, m.Facts[i].Label, m.Facts[i].Value, m.Facts[i].Link, Fact, i);
                }
            }
            cy += UiTokens.S3;
            if (!string.IsNullOrEmpty(m.TagsTitle) && m.Tags != null && m.Tags.Length > 0 && cy + UiTokens.Chip <= foot)
            {
                UiDraw.Text(s, at, x, cy, w, UiStyle.SectionHeader, UiRole.Muted, m.TagsTitle, UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.PairRow;
                float tx = x;
                for (int i = 0; i < m.Tags.Length && cy + UiTokens.Chip <= foot; i++)
                {
                    if (string.IsNullOrEmpty(m.Tags[i]))
                    {
                        continue;
                    }
                    float tw = ChipWidth(m.Tags[i], w);
                    if (tx > x && tx + tw > x + w)
                    {
                        cy += UiTokens.Chip + UiTokens.S2;
                        tx = x;
                        if (cy + UiTokens.Chip > foot)
                        {
                            break;
                        }
                    }
                    UiDraw.Chip(s, at, tx, cy, m.Tags[i], UiRole.AccentText, w);
                    tx += tw + UiTokens.S2;
                }
                cy += UiTokens.Chip + UiTokens.S4;
            }
            if (!string.IsNullOrEmpty(m.WhereTitle) && cy + UiTokens.PairRow <= foot)
            {
                UiDraw.Text(s, at, x, cy, w, UiStyle.SectionHeader, UiRole.Muted, m.WhereTitle, UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.PairRow;
                if (!string.IsNullOrEmpty(m.Where) && cy < foot)
                {
                    float room = Math.Max(UiTokens.PairRow, foot - cy);
                    int lines = room >= UiTokens.PairRow * 3f ? 3 : room >= UiTokens.PairRow * 2f ? 2 : 1;
                    UiDraw.Text(s, at, x, cy, w, UiStyle.Body, UiRole.Text2, m.Where, UiFlags.Label | UiFlags.Wrap, lines);
                }
            }
        }
    }
}
