using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Where focus goes with the d-pad or the arrows, game-free: a node's own link first; in a list, up and down step
    /// through its rows (one beyond those in view asks the screen to scroll, <see cref="ScrollAsked"/>); otherwise the
    /// nearest target that way, its distance along the move plus twice its offset across it, ranges that overlap across
    /// counting as no offset. Up and down look in the target's own column (its region) first, as a game's columns do, and
    /// cross into another only when nothing lies that way in its own. Focus stays in the top layer that has targets (an
    /// open question or call). EconomyCheck proves
    /// with <see cref="Unreached"/> that every target of every registered screen is reached from its first. Cost: a pass
    /// over the scene's targets a press; nothing allocated.
    /// </summary>
    public static class UiFocusRules
    {
        public const int ScrollAsked = -2;   // the next row is beyond those in view: the screen scrolls, then focus goes to it

        /// <summary>The highest layer with a target that can take focus.</summary>
        public static int TopLayer(UiScene s)
        {
            int top = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (!s.Nodes[i].Disabled && s.Nodes[i].Layer > top)
                {
                    top = s.Nodes[i].Layer;
                }
            }
            return top;
        }

        /// <summary>The first target of the top layer: the scene's own first, or the top layer's first.</summary>
        public static int First(UiScene s)
        {
            int top = TopLayer(s);
            if (s.FocusFirst >= 0 && s.FocusFirst < s.NodeCount && s.Nodes[s.FocusFirst].Layer == top && !s.Nodes[s.FocusFirst].Disabled)
            {
                return s.FocusFirst;
            }
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (!s.Nodes[i].Disabled && s.Nodes[i].Layer == top)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// The target focus moves to from a node, one step along (dx, dy) with one of them 0; -1 when nothing lies that
        /// way, <see cref="ScrollAsked"/> when a list's next row is out of view.
        /// </summary>
        public static int Next(UiScene s, int from, int dx, int dy)
        {
            if (from < 0 || from >= s.NodeCount)
            {
                return First(s);
            }
            ref UiNode n = ref s.Nodes[from];
            int link = dx > 0 ? n.Right : dx < 0 ? n.Left : dy > 0 ? n.Down : n.Up;
            if (link >= 0 && link < s.NodeCount)
            {
                return link;
            }
            if (n.List >= 0 && dx == 0)
            {
                int step = InList(s, from, dy);
                if (step != -1)
                {
                    return step;
                }
            }
            if (dy != 0)
            {
                int inColumn = Nearest(s, from, dx, dy, true);
                if (inColumn >= 0)
                {
                    return inColumn;
                }
            }
            return Nearest(s, from, dx, dy, false);
        }

        // A list's next or previous row: in the scene, or out of view (the screen scrolls), or -1 at its first and last.
        private static int InList(UiScene s, int from, int dy)
        {
            ref UiNode n = ref s.Nodes[from];
            ref UiList list = ref s.Lists[n.List];
            int want = n.Index + (dy > 0 ? 1 : -1);
            if (want < 0 || want >= list.Count)
            {
                return -1;
            }
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (s.Nodes[i].List == n.List && s.Nodes[i].Index == want)
                {
                    return i;
                }
            }
            return list.First == 0 && list.Shown >= list.Count ? -1 : ScrollAsked;
        }

        private static int Nearest(UiScene s, int from, int dx, int dy, bool sameRegion)
        {
            s.NodeBox(from, out float fx, out float fy, out float fw, out float fh);
            int layer = s.Nodes[from].Layer;
            byte region = s.Nodes[from].Region;
            short list = s.Nodes[from].List;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode c = ref s.Nodes[i];
                if (i == from || c.Disabled || c.Layer != layer || (list >= 0 && c.List == list) || (sameRegion && c.Region != region))
                {
                    continue;   // leaving a list sideways or past its end never lands on another of its own rows
                }
                s.NodeBox(i, out float cx, out float cy, out float cw, out float ch);
                if (c.Clip >= 0 && !Visible(s, c.Clip, cy, ch))
                {
                    continue;
                }
                float along, across;
                if (dx != 0)
                {
                    along = dx > 0 ? cx - (fx + fw) : fx - (cx + cw);
                    across = Gap(fy, fh, cy, ch);
                }
                else
                {
                    along = dy > 0 ? cy - (fy + fh) : fy - (cy + ch);
                    across = Gap(fx, fw, cx, cw);
                }
                if (along < -UiTokens.S2)
                {
                    continue;   // behind or level: not that way
                }
                float score = Math.Max(0f, along) + across * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        // How far apart two ranges are across the move: 0 when they overlap.
        private static float Gap(float a, float aw, float b, float bw)
        {
            if (b + bw < a)
            {
                return a - (b + bw);
            }
            return b > a + aw ? b - (a + aw) : 0f;
        }

        private static bool Visible(UiScene s, short clip, float y, float h)
        {
            ref UiClip c = ref s.Clips[clip];
            return y + h > c.Y + 0.5f && y < c.Y + c.H - 0.5f;
        }

        /// <summary>
        /// The targets of the top layer that the d-pad never reaches from its first, counted; <paramref name="first"/> names
        /// the first one missed, or is null. A list reached counts all its rows reached (focus scrolls through them).
        /// </summary>
        public static int Unreached(UiScene s, out string first)
        {
            first = null;
            int start = First(s);
            if (start < 0)
            {
                return 0;
            }
            int top = s.Nodes[start].Layer;
            var seen = new bool[s.NodeCount];
            var queue = new int[s.NodeCount];
            int head = 0, tail = 0;
            seen[start] = true;
            queue[tail++] = start;
            while (head < tail)
            {
                int at = queue[head++];
                for (int d = 0; d < 4; d++)
                {
                    int dx = d == 0 ? 1 : d == 1 ? -1 : 0, dy = d == 2 ? 1 : d == 3 ? -1 : 0;
                    int next = Next(s, at, dx, dy);
                    if (next >= 0 && !seen[next])
                    {
                        seen[next] = true;
                        queue[tail++] = next;
                    }
                }
                if (s.Nodes[at].List >= 0)
                {
                    for (int i = 0; i < s.NodeCount; i++)
                    {
                        if (!seen[i] && s.Nodes[i].List == s.Nodes[at].List)
                        {
                            seen[i] = true;
                            queue[tail++] = i;
                        }
                    }
                }
            }
            int missed = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (!seen[i] && !s.Nodes[i].Disabled && s.Nodes[i].Layer == top)
                {
                    missed++;
                    if (first == null)
                    {
                        first = "target " + i + " (command " + s.Nodes[i].Command + ", " + s.Nodes[i].Arg + ") at " + (int)s.Nodes[i].X + "," + (int)s.Nodes[i].Y;
                    }
                }
            }
            return missed;
        }
    }
}
