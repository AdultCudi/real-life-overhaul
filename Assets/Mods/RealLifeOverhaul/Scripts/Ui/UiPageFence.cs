using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Keeps the d-pad on an app window's page and to its rows and columns, for a page of many blocks (the parts' gallery
    /// does the same for its own pages). Sideways a move goes to the nearest target standing level with it that way (a
    /// row's next button, never a head under it), and from the page's left edge to the sidebar's chosen entry; up and down to the
    /// nearest sharing its width, or failing that the nearest below or above on the page, a list stepping its own rows. A
    /// move with nowhere to go stops where it is: never onto the window's close (it is Back, so a pad's press there would
    /// shut the app) while a sidebar leads there, and never up or down into the sidebar. A window with no sidebar reaches
    /// its close up from the page's top, its only way there. The sidebar's own moves never reach the close sideways.
    /// Game-free. Cost: a pass over the targets at a layout.
    /// </summary>
    public static class UiPageFence
    {
        /// <summary>
        /// Links the targets laid since <paramref name="page"/> (the page's first) as above; those before it are the sidebar's
        /// and the window's. Given the page's left and right edges, its middle is theirs (its two columns' gap); else the
        /// middle of its targets' spread.
        /// </summary>
        public static void Lay(UiScene s, int page, float left = float.NaN, float right = float.NaN)
        {
            _left = left;
            _right = right;
            int close = -1, chosen = -1;
            bool sidebar = false;
            for (int i = 0; i < page; i++)
            {
                ref UiNode node = ref s.Nodes[i];
                close = node.Command == UiDraw.CloseCommand ? i : close;
                sidebar |= node.Command != UiDraw.CloseCommand && node.Layer == 0;
                if (chosen < 0 && node.Layer == 0 && node.Face >= 0 && node.Face < s.ItemCount && (s.Items[node.Face].Flags & UiFlags.Selected) != 0)
                {
                    chosen = i;   // the sidebar's entry the page belongs to
                }
            }
            int n = s.NodeCount;
            var links = new int[n * 4];
            for (int i = 0; i < links.Length; i++)
            {
                links[i] = -2;   // left as the focus rules find it
            }
            for (int i = 0; i < n; i++)
            {
                ref UiNode node = ref s.Nodes[i];
                if (node.Layer != 0 || i == close)
                {
                    continue;
                }
                bool inPage = i >= page;
                for (int d = 0; d < 4; d++)
                {
                    int dx = d == 0 ? 1 : d == 1 ? -1 : 0, dy = d == 2 ? 1 : d == 3 ? -1 : 0;
                    int rules = UiFocusRules.Next(s, i, dx, dy);
                    if (!inPage)
                    {
                        links[i * 4 + d] = dx != 0 && rules == close ? i : -2;
                        continue;
                    }
                    int next = dx != 0 ? Level(s, i, page, dx)
                        : node.List >= 0 && (rules == UiFocusRules.ScrollAsked || (rules >= page && Overlap(s, i, rules))) ? -2 : Column(s, i, page, dy);
                    if (next == -1 && dx < 0 && chosen >= 0)
                    {
                        next = chosen;   // the page's left edge: the sidebar's entry it came from is a step away
                    }
                    else if (next == -1 && dx < 0 && rules >= 0 && rules < page && rules != close)
                    {
                        next = rules;
                    }
                    else if (next == -1 && dy < 0 && !sidebar && close >= 0)
                    {
                        next = close;
                    }
                    links[i * 4 + d] = next == -1 ? i : next;
                }
            }
            for (int i = 0; i < n; i++)
            {
                ref UiNode node = ref s.Nodes[i];
                node.Right = links[i * 4] != -2 ? links[i * 4] : node.Right;
                node.Left = links[i * 4 + 1] != -2 ? links[i * 4 + 1] : node.Left;
                node.Down = links[i * 4 + 2] != -2 ? links[i * 4 + 2] : node.Down;
                node.Up = links[i * 4 + 3] != -2 ? links[i * 4 + 3] : node.Up;
            }
        }

        // The nearest page target sideways that stands level with a target (their heights overlap), or -1.
        private static int Level(UiScene s, int from, int page, int dx)
        {
            s.NodeBox(from, out float fx, out float fy, out float fw, out float fh);
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = page; i < s.NodeCount; i++)
            {
                ref UiNode c = ref s.Nodes[i];
                if (i == from || c.Disabled || c.Layer != s.Nodes[from].Layer || !Shown(s, i))
                {
                    continue;
                }
                s.NodeBox(i, out float cx, out float cy, out float cw, out float ch);
                float along = dx > 0 ? cx - (fx + fw) : fx - (cx + cw);
                if (dx > 0 && Within(cx, cy, cw, ch, fx, fy, fw, fh) && cx > fx + fw / 2f)
                {
                    along = 0f;   // a button over a row's right end, its row's own action: right from the row reaches it
                }
                else if (dx < 0 && Within(fx, fy, fw, fh, cx, cy, cw, ch) && fx > cx + cw / 2f)
                {
                    along = 0f;   // and left from it goes back to the row
                }
                if (along < -UiTokens.S2 || Math.Min(fy + fh, cy + ch) - Math.Max(fy, cy) <= 0f)
                {
                    continue;
                }
                float score = along + Math.Abs(cy + ch / 2f - (fy + fh / 2f));
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        // The nearest page target up or down that shares some of a target's width; failing that the nearest that way on
        // the page, so a block under another one's side is still reached; or -1.
        // Whether the first box lies inside the second.
        private static bool Within(float x, float y, float w, float h, float ox, float oy, float ow, float oh) =>
            x >= ox && y >= oy && x + w <= ox + ow + 0.5f && y + h <= oy + oh + 0.5f;

        private static float _left = float.NaN, _right = float.NaN;   // the page's edges for this layout, or NaN

        private const float HeadBand = UiTokens.S16 * 1.5f;   // px: the page's top band, a firm's head or a title row with its tools

        private static int Column(UiScene s, int from, int page, int dy)
        {
            s.NodeBox(from, out float fx, out float fy, out float fw, out float fh);
            float left = float.MaxValue, right = float.MinValue, top = float.MaxValue;   // the page's spread, when its edges are not given
            for (int i = page; i < s.NodeCount; i++)
            {
                if (s.Nodes[i].Layer == s.Nodes[from].Layer)
                {
                    s.NodeBox(i, out float bx, out float by, out float bw, out float _);
                    left = Math.Min(left, bx);
                    right = Math.Max(right, bx + bw);
                    top = Math.Min(top, by);
                }
            }
            float middle = float.IsNaN(_left) || float.IsNaN(_right) ? (left + right) / 2f : (_left + _right) / 2f;
            bool rightSide = fx + fw / 2f > middle;
            bool head = fy < top + HeadBand;   // the page's head spans both columns: a step down from it may cross the middle
            int best = -1, near = -1;
            float bestScore = float.MaxValue, nearScore = float.MaxValue;
            for (int i = page; i < s.NodeCount; i++)
            {
                ref UiNode c = ref s.Nodes[i];
                if (i == from || c.Disabled || c.Layer != s.Nodes[from].Layer || !Shown(s, i) || (c.List >= 0 && c.List == s.Nodes[from].List))
                {
                    continue;
                }
                s.NodeBox(i, out float cx, out float cy, out float cw, out float ch);
                float along = dy > 0 ? cy - (fy + fh) : fy - (cy + ch);
                if (along < -UiTokens.S2)
                {
                    continue;
                }
                float across = Math.Abs(cx + cw / 2f - (fx + fw / 2f));
                if (Math.Min(fx + fw, cx + cw) - Math.Max(fx, cx) > 0f)
                {
                    float score = along + across * 0.5f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }
                else if (along + across < nearScore && (dy < 0 || head || (cx + cw / 2f > middle) == rightSide))
                {
                    nearScore = along + across;   // down, the nearest on its own side only (but from the head); up, the nearest anywhere
                    near = i;
                }
            }
            return best >= 0 ? best : near;
        }

        // A target is in view: outside a scroll region, or inside the part of one shown.
        private static bool Shown(UiScene s, int i)
        {
            ref UiNode c = ref s.Nodes[i];
            if (c.Clip < 0)
            {
                return true;
            }
            s.NodeBox(i, out float x, out float y, out float w, out float h);
            ref UiClip clip = ref s.Clips[c.Clip];
            return y + h > clip.Y + 0.5f && y < clip.Y + clip.H - 0.5f;
        }

        private static bool Overlap(UiScene s, int a, int b)
        {
            s.NodeBox(a, out float ax, out float ay, out float aw, out float ah);
            s.NodeBox(b, out float bx, out float by, out float bw, out float bh);
            return Math.Min(ax + aw, bx + bw) - Math.Max(ax, bx) > 0f;
        }
    }
}
