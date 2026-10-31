using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Where the items being drawn go: the item they sit inside, the scroll region, the layer and the arrival order.</summary>
    public struct UiAt
    {
        public int Parent;
        public short Clip;
        public byte Layer;
        public short Order;
        public byte Region;   // the column a target stands in (groups, list, detail, side): up and down stay in it

        /// <summary>The screen itself: no parent, no scroll region, the base layer.</summary>
        public static UiAt Screen => new UiAt { Parent = -1, Clip = -1, Layer = 0, Order = -1 };

        /// <summary>The same place, inside an item.</summary>
        public UiAt In(int parent) => new UiAt { Parent = parent, Clip = Clip, Layer = Layer, Order = Order, Region = Region };

        /// <summary>The same place, arriving at a place in a list.</summary>
        public UiAt Arriving(int order) => new UiAt { Parent = Parent, Clip = Clip, Layer = Layer, Order = (short)Math.Min(order, short.MaxValue), Region = Region };

        /// <summary>The same place, in a region of its own for the d-pad.</summary>
        public UiAt InRegion(byte region) => new UiAt { Parent = Parent, Clip = Clip, Layer = Layer, Order = Order, Region = region };
    }

    /// <summary>
    /// The kit's components, laid out into a scene from tokens alone (UiTokens): every size, gap, colour role and style a
    /// screen shows comes from here, so no screen sets one of its own (docs/standards/ui.md). Each component writes its
    /// items at grid positions and returns its size; boxes, avatars and key caps land on the 4 px grid, text centred in a
    /// box says so (UiFlags.Middle). The parts: this file (shapes, text, avatars, badges, key caps, focus targets),
    /// UiDraw.Controls.cs, UiDraw.Lists.cs, UiDraw.Data.cs, UiDraw.Detail.cs and UiDraw.Overlays.cs. Game-free.
    /// Cost: a few item writes each; nothing allocated once a scene's arrays have grown.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>A length rounded up to the grid.</summary>
        public static float Snap(float px) => (float)Math.Ceiling(px / UiTokens.Grid - 0.001f) * UiTokens.Grid;

        /// <summary>A length rounded down to the grid.</summary>
        public static float SnapDown(float px) => (float)Math.Floor(px / UiTokens.Grid + 0.001f) * UiTokens.Grid;

        /// <summary>A filled shape with an optional clear edge.</summary>
        public static int Box(UiScene s, UiAt at, float x, float y, float w, float h, UiRole fill, UiRole edge, float radius, UiFlags flags = UiFlags.None)
        {
            var item = new UiItem
            {
                Kind = UiKind.Box, Flags = flags, Fill = fill, Edge = edge, Radius = radius, X = x, Y = y, W = w, H = h,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            return s.Add(ref item);
        }

        /// <summary>A circle, or a pill when wider than tall.</summary>
        public static int Circle(UiScene s, UiAt at, float x, float y, float w, float h, UiRole fill, UiFlags flags = UiFlags.None)
        {
            return Box(s, at, x, y, w, h, fill, UiRole.None, h / 2f, flags | UiFlags.Circle);
        }

        /// <summary>One line of words, or a wrapped block with <see cref="UiFlags.Wrap"/> up to <paramref name="lines"/> lines; returns its index.</summary>
        public static int Text(UiScene s, UiAt at, float x, float y, float w, UiStyle style, UiRole ink, string text, UiFlags flags = UiFlags.None, int lines = 1)
        {
            float h = (flags & UiFlags.Wrap) != 0 ? UiTextRules.WrappedHeight(text, style, w, lines) : UiTextRules.LineHeight(style);
            var item = new UiItem
            {
                Kind = UiKind.Text, Flags = flags, Ink = ink, Style = style, X = x, Y = y, W = w, H = h, Text = text ?? string.Empty,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f, Value = lines
            };
            return s.Add(ref item);
        }

        /// <summary>One line of words centred on a box's height (a button's label, a row's single line).</summary>
        public static int TextMiddle(UiScene s, UiAt at, float x, float boxY, float boxH, float w, UiStyle style, UiRole ink, string text, UiFlags flags = UiFlags.None)
        {
            float lh = UiTextRules.LineHeight(style);
            return Text(s, at, x, boxY + (boxH - lh) / 2f, w, style, ink, text, flags | UiFlags.Middle);
        }

        /// <summary>A glyph of the icon family in a square box.</summary>
        public static int Icon(UiScene s, UiAt at, float x, float y, float size, UiIcon icon, UiRole tint, UiFlags flags = UiFlags.None)
        {
            var item = new UiItem
            {
                Kind = UiKind.Icon, Flags = flags, Fill = tint, Icon = icon, X = x, Y = y, W = size, H = size,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            return s.Add(ref item);
        }

        /// <summary>Initials on a circle tinted by the name's hue.</summary>
        public static int Avatar(UiScene s, UiAt at, float x, float y, float size, string name, float hue)
        {
            var item = new UiItem
            {
                Kind = UiKind.Avatar, Text = Initials(name), X = x, Y = y, W = size, H = size, Value = hue, Radius = size / 2f,
                Flags = UiFlags.Circle, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            return s.Add(ref item);
        }

        /// <summary>A dot in a hue's own colour (a group's): brighter than an avatar's circle, so it reads small.</summary>
        public static int HueDot(UiScene s, UiAt at, float x, float y, float size, float hue)
        {
            var item = new UiItem
            {
                Kind = UiKind.HueDot, X = x, Y = y, W = size, H = size, Value = hue, Radius = size / 2f, Flags = UiFlags.Circle | UiFlags.Middle,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            return s.Add(ref item);
        }

        /// <summary>A soft accent shadow under a box (a primary button, a badge): decoration the layout checks pass over.</summary>
        public static int Glow(UiScene s, UiAt at, float x, float y, float w, float h, UiRole tint, float alpha)
        {
            var item = new UiItem
            {
                Kind = UiKind.Glow, Flags = UiFlags.Decor, Fill = tint, X = x, Y = y, W = w, H = h, Radius = UiTokens.RadiusLarge,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = alpha
            };
            return s.Add(ref item);
        }

        /// <summary>The soft accent wash in a panel's top left corner, as the wireframe lights its hero.</summary>
        public static int Wash(UiScene s, UiAt at, float x, float y, float w, float h)
        {
            var item = new UiItem
            {
                Kind = UiKind.Wash, Flags = UiFlags.Decor, Fill = UiRole.Accent, X = x, Y = y, W = w, H = h,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 0.13f
            };
            return s.Add(ref item);
        }

        /// <summary>A count in a badge, at a box's right edge from x; returns its width.</summary>
        public static float Badge(UiScene s, UiAt at, float right, float y, int count, bool loud)
        {
            string words = count > 99 ? "99+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            float w = Math.Max(UiTokens.Badge, Snap(UiTextRules.Width(words, UiStyle.Badge) + UiTokens.S2 * 2f));
            float x = right - w;
            if (loud)
            {
                Glow(s, at, x, y, w, UiTokens.Badge, UiRole.Accent, 0.5f);
            }
            int b = Box(s, at, x, y, w, UiTokens.Badge, loud ? UiRole.Accent : UiRole.Notice, UiRole.None, UiTokens.Badge / 2f, UiFlags.Circle | UiFlags.Right);
            TextMiddle(s, at.In(b), x, y, UiTokens.Badge, w, UiStyle.Badge, loud ? UiRole.OnAccent : UiRole.NoticeText, words, UiFlags.Centre);
            return w;
        }

        /// <summary>A key cap: the key's name, or the pad button's glyph; returns its width.</summary>
        public static float KeyCap(UiScene s, UiAt at, float x, float y, UiAction action, IUiKeys keys, UiRole face, UiRole ink)
        {
            UiIcon glyph = keys.Glyph(action);
            string name = glyph == UiIcon.None ? keys.Name(action) : null;
            float h = UiTokens.KeyCap;
            float w = glyph != UiIcon.None ? h : Math.Max(h, Snap(UiTextRules.Width(name, UiStyle.KeyCap) + UiTokens.S2 * 2f));
            var item = new UiItem
            {
                Kind = UiKind.KeyCap, Fill = face, Edge = ink, Ink = ink, Style = UiStyle.KeyCap, Icon = glyph, Text = name ?? string.Empty,
                X = x, Y = y, W = w, H = h, Radius = UiTokens.RadiusSmall, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order,
                Parent = at.Parent, Alpha = 1f
            };
            s.Add(ref item);
            return w;
        }

        /// <summary>A key cap's width before it is drawn, for a layout that must know it first.</summary>
        public static float KeyCapWidth(UiAction action, IUiKeys keys)
        {
            UiIcon glyph = keys.Glyph(action);
            return glyph != UiIcon.None ? UiTokens.KeyCap : Math.Max(UiTokens.KeyCap, Snap(UiTextRules.Width(keys.Name(action), UiStyle.KeyCap) + UiTokens.S2 * 2f));
        }

        /// <summary>A focus target over a box: what a press runs, the face that brightens and the list it is a row of.</summary>
        public static int Node(UiScene s, UiAt at, float x, float y, float w, float h, int command, int arg, int face, bool disabled = false, short list = -1, int index = 0)
        {
            var node = new UiNode
            {
                X = x, Y = y, W = w, H = h, Clip = at.Clip, Layer = at.Layer, Region = at.Region, Command = command, Arg = arg, Face = face, List = list,
                Index = index, Disabled = disabled, Up = -1, Down = -1, Left = -1, Right = -1
            };
            int n = s.AddNode(ref node);
            for (int i = face; i >= 0 && i < s.ItemCount; i++)
            {
                // An inner control drawn first keeps its own items: the outer target takes only what nothing has claimed.
                if (s.Items[i].Node < 0 && (i == face || Inside(s, i, face)))
                {
                    s.Items[i].Node = (short)n;
                }
            }
            return n;
        }

        /// <summary>Whether an item sits, through its parents, inside another.</summary>
        public static bool Inside(UiScene s, int item, int outer)
        {
            int p = s.Items[item].Parent;
            for (int guard = 0; p >= 0 && guard < 32; guard++)
            {
                if (p == outer)
                {
                    return true;
                }
                p = s.Items[p].Parent;
            }
            return false;
        }

        /// <summary>The app's mark (an accent tile with a dark dot) and its lower-case name; returns the height.</summary>
        public static float AppMark(UiScene s, UiAt at, float x, float y, string name)
        {
            const float mark = UiTokens.S8;
            int tile = Box(s, at, x, y + UiTokens.S1, mark, mark, UiRole.Accent, UiRole.None, UiTokens.RadiusMedium);
            Circle(s, at.In(tile), x + (mark - UiTokens.S3) / 2f, y + UiTokens.S1 + (mark - UiTokens.S3) / 2f, UiTokens.S3, UiTokens.S3, UiRole.OnAccent, UiFlags.Middle | UiFlags.Centre);
            float lh = UiTextRules.LineHeight(UiStyle.AppTitle);
            Text(s, at, x + mark + UiTokens.S3, y + UiTokens.S1 + (mark - lh) / 2f, UiTextRules.Width(name, UiStyle.AppTitle) + UiTokens.S1, UiStyle.AppTitle,
                UiRole.AccentText, name, UiFlags.Middle | UiFlags.Label);
            return mark + UiTokens.S2;
        }

        /// <summary>The player at the foot of a sidebar: an avatar, the name and a line of figures.</summary>
        public static void PlayerCard(UiScene s, UiAt at, float x, float y, float w, string name, float hue, string summary)
        {
            float size = UiTokens.AvatarSmall;
            Avatar(s, at, x, y, size, name, hue);
            float tx = x + size + UiTokens.S3, tw = w - size - UiTokens.S3;
            float block = UiTextRules.LineHeight(UiStyle.Title) + UiTextRules.LineHeight(UiStyle.Small);
            float ty = y + (size - block) / 2f;
            Text(s, at, tx, ty, tw, UiStyle.Title, UiRole.Text, name, UiFlags.Truncate | UiFlags.Middle);
            Text(s, at, tx, ty + UiTextRules.LineHeight(UiStyle.Title), tw, UiStyle.Small, UiRole.AccentText, summary, UiFlags.Truncate | UiFlags.Middle);
        }

        /// <summary>Capitals over a block of rows; returns the height it takes with the space above it.</summary>
        public static float SectionHeader(UiScene s, UiAt at, float x, float y, float w, string text)
        {
            Text(s, at, x + UiTokens.S3, y + UiTokens.S4, w - UiTokens.S3, UiStyle.SectionHeader, UiRole.Muted, text, UiFlags.Label);
            return Snap(UiTokens.S4 + UiTextRules.LineHeight(UiStyle.SectionHeader));
        }

        /// <summary>A panel's title in its header band; returns the band's height.</summary>
        public static float PanelTitle(UiScene s, UiAt at, float x, float y, float w, string title, string count)
        {
            float lh = UiTextRules.LineHeight(UiStyle.SectionTitle);
            float cw = string.IsNullOrEmpty(count) ? 0f : UiTextRules.Width(count, UiStyle.Small) + UiTokens.S1;
            Text(s, at, x, y, w - cw - UiTokens.S3, UiStyle.SectionTitle, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            if (cw > 0f)
            {
                Text(s, at, x + w - cw, y + lh - UiTextRules.LineHeight(UiStyle.Small), cw, UiStyle.Small, UiRole.Muted, count, UiFlags.Right | UiFlags.Middle);
            }
            return Snap(lh);
        }

        /// <summary>An empty state in words, centred in a box: never a blank panel.</summary>
        public static void Empty(UiScene s, UiAt at, float x, float y, float w, float h, string title, string body)
        {
            float bw = Math.Min(w - UiTokens.S10 * 2f, UiTokens.S16 * 6f);
            float th = UiTextRules.LineHeight(UiStyle.SectionTitle);
            float bh = UiTextRules.WrappedHeight(body, UiStyle.Body, bw, 4);
            float top = y + (h - th - UiTokens.S2 - bh) / 2f;
            float bx = x + (w - bw) / 2f;
            Text(s, at, bx, top, bw, UiStyle.SectionTitle, UiRole.Text2, title, UiFlags.Centre | UiFlags.Middle | UiFlags.Label);
            Text(s, at, bx, top + th + UiTokens.S2, bw, UiStyle.Body, UiRole.Muted, body, UiFlags.Centre | UiFlags.Middle | UiFlags.Wrap | UiFlags.Label, 4);
        }

        /// <summary>A loading state: three dots that beat while it shows, and what is loading.</summary>
        public static void Busy(UiScene s, UiAt at, float x, float y, float w, float h, string label)
        {
            float lh = UiTextRules.LineHeight(UiStyle.Small);
            float block = UiTokens.S3 + UiTokens.S3 + lh;
            float top = y + (h - block) / 2f;
            var dots = new UiItem
            {
                Kind = UiKind.Busy, Fill = UiRole.AccentText, X = x + (w - UiTokens.S12) / 2f, Y = top, W = UiTokens.S12, H = UiTokens.S3,
                Flags = UiFlags.Middle, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            s.Add(ref dots);
            Text(s, at, x, top + UiTokens.S3 * 2f, w, UiStyle.Small, UiRole.Muted, label, UiFlags.Centre | UiFlags.Middle | UiFlags.Label);
        }

        /// <summary>A name's initials for its avatar: "MO" for Maya Okafor; "?" for none.</summary>
        public static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "?";
            }
            char a = '\0', b = '\0';
            bool next = true;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsLetterOrDigit(c))
                {
                    if (next)
                    {
                        if (a == '\0')
                        {
                            a = c;
                        }
                        else if (b == '\0')
                        {
                            b = c;
                        }
                    }
                    next = false;
                }
                else
                {
                    next = true;
                }
            }
            if (a == '\0')
            {
                return "?";
            }
            if (UiTextRules.Wide(a))
            {
                return a.ToString();   // one ideograph or syllable fills a circle on its own
            }
            return b == '\0' ? char.ToUpperInvariant(a).ToString() : new string(new[] { char.ToUpperInvariant(a), char.ToUpperInvariant(b) });
        }
    }
}
