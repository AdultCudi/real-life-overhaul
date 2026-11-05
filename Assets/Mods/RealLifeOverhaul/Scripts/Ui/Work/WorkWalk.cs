using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// How a player with the pad reads a work page, as one walk of the real focus links: from where the focus starts, every target of the page in
    /// reading order (column by column, the rows of a column from the top, a row from the end nearest where the last one ended, so a grid is read
    /// as a serpentine), each reached from the one before by pressing the d-pad through <see cref="UiFocusRules.Next"/> and nothing else. The
    /// checks fail a page whose links cannot walk it that way (a target left out, a hop of more presses than <see cref="HopMost"/>, a walk that
    /// starts on a stepper or a close or ends on a destructive button), and the previews draw their pad path shots from this same walk, so the
    /// picture and the proof are one thing. Game-free. Cost: a pass over the scene's targets for each stop, once per check or preview.
    /// </summary>
    public static class WorkWalk
    {
        public const int HopMost = 4;     // d-pad presses from one stop to the next inside a column: a step along a row or down a column, or on to the next row
        public const int HopAcross = 6;   // into the next column: along to it at the height the last stop stood, then a few steps to its first target

        /// <summary>The walk: the stops in order, the presses each hop took (the first stop's is 0), and the targets the links could not reach.</summary>
        public sealed class Walked
        {
            public readonly List<int> Stops = new List<int>(32);
            public readonly List<int> Hops = new List<int>(32);
            public readonly List<int> Missed = new List<int>(4);
            public int Longest;   // the most presses of one hop inside a column
            public int Across;    // and of one into the next column
            public int Presses;
        }

        /// <summary>A target of the page itself: on the top layer, enabled, in view, and neither the window's close, the app's tray nor a section of the app (the shoulders step those).</summary>
        public static bool IsContent(UiScene s, int node, int top)
        {
            ref UiNode n = ref s.Nodes[node];
            if (n.Disabled || n.Layer != top || n.Command == UiDraw.CloseCommand || n.Command == UiAppWindow.NavCommand || UiDraw.IsChrome(n.Command))
            {
                return false;
            }
            if (n.Clip >= 0)
            {
                s.NodeBox(node, out float _, out float y, out float _, out float h);
                ref UiClip c = ref s.Clips[n.Clip];
                return y + h > c.Y + 0.5f && y < c.Y + c.H - 0.5f;
            }
            return true;
        }

        /// <summary>
        /// Walks the page from <paramref name="start"/> (the first target the keys land on). The page's main action, where it has one, is read last, so
        /// the walk ends on what the page is for; a walk that would end on a <paramref name="danger"/> button (one that ends or gives something up)
        /// reads it before the last safe target instead.
        /// </summary>
        public static Walked Walk(UiScene s, int start, Func<int, bool> danger = null, int primary = -1)
        {
            var w = new Walked();
            int top = UiFocusRules.TopLayer(s);
            var order = new List<int>(s.NodeCount);
            var column = new int[s.NodeCount];
            s.NodeBox(start, out float sx, out float _, out float sw, out float _);
            var block = new int[s.NodeCount];
            Reading(s, top, order, column, sx + sw / 2f, block);
            // Starting in a later block of the page (the main action stands in the right column): on down that column, then round to the ones before it.
            if (block[start] > 0)
            {
                var earlier = new List<int>(order.Count);
                order.RemoveAll(n => block[n] < block[start] && Take(earlier, n));
                order.AddRange(earlier);
            }
            order.Remove(start);   // the first stop is where the keys already are
            if (primary >= 0 && primary != start && order.Remove(primary))
            {
                order.Add(primary);
            }
            else if (danger != null)
            {
                int safe = order.Count - 1;
                while (safe >= 0 && danger(order[safe]))
                {
                    safe--;
                }
                if (safe >= 0 && safe < order.Count - 1)
                {
                    // the quiet targets that stand together (a stepper's less, its figure and its more) go to the end together, in their order
                    int first = safe;
                    while (first > 0 && !danger(order[first - 1]))
                    {
                        first--;
                    }
                    List<int> run = order.GetRange(first, safe - first + 1);
                    order.RemoveRange(first, run.Count);
                    order.AddRange(run);
                }
            }
            w.Stops.Add(start);
            w.Hops.Add(0);
            var seen = new bool[s.NodeCount];
            seen[start] = true;
            int at = start;
            for (int i = 0; i < order.Count; i++)
            {
                int to = order[i];
                if (seen[to])
                {
                    continue;
                }
                int presses = Distance(s, at, to);
                bool across = column[at] != column[to];   // a hop into the next column may take a few presses more: the same height, then along
                if (presses < 0)
                {
                    w.Missed.Add(to);
                    continue;
                }
                seen[to] = true;
                w.Stops.Add(to);
                w.Hops.Add(presses);
                w.Presses += presses;
                if (across)
                {
                    w.Across = Math.Max(w.Across, presses);
                }
                else
                {
                    w.Longest = Math.Max(w.Longest, presses);
                }
                at = to;
            }
            return w;
        }

        // Blocks stand for columns of a page only when each holds at least two targets: one button set apart at a corner (the third card of a row) belongs to the row it is in.
        private static bool Columns(List<List<int>> blocks)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].Count < 2)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Take(List<int> into, int node)
        {
            into.Add(node);
            return true;
        }

        private const float Gutter = 32f;   // a gap this wide between two blocks of targets is the gutter between two columns of panels: those are read a column at a time
        private const float TileHeight = 64f;
        private const float Gap = 8f;   // px of nothing between two groups of targets that reading treats as apart

        /// <summary>
        /// The page's targets in reading order, by the usual cut of a page: the bands (a gap that runs all across) from the top, and where there is no
        /// such gap the columns from the left, each cut again the same way. A row of single targets (a row of cards, a row of chips) is read from the end
        /// nearest the last stop, so a grid is read as a serpentine. <paramref name="column"/> gets each target's column, to tell a hop along a column from one
        /// into the next.
        /// </summary>
        public static void Reading(UiScene s, int top, List<int> into, int[] column = null, float fromX = 0f, int[] block = null)
        {
            var nodes = new List<int>(s.NodeCount);
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (IsContent(s, i, top))
                {
                    nodes.Add(i);
                }
            }
            var col = new int[s.NodeCount];
            int next = 0;
            float lastX = fromX;
            List<List<int>> blocks = nodes.Count > 1 ? Split(s, nodes, true, out float _, Gutter) : null;
            if (blocks != null && blocks.Count > 1 && Columns(blocks))
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    int id = ++next;
                    Cut(s, blocks[i], into, col, ref next, id, ref lastX);
                    for (int k = 0; block != null && k < blocks[i].Count; k++)
                    {
                        block[blocks[i][k]] = i;
                    }
                }
            }
            else
            {
                Cut(s, nodes, into, col, ref next, 0, ref lastX);
            }
            if (column != null)
            {
                Array.Copy(col, column, Math.Min(col.Length, column.Length));
            }
        }

        private static void Cut(UiScene s, List<int> nodes, List<int> into, int[] col, ref int next, int colId, ref float lastX)
        {
            if (nodes.Count > 1)
            {
                // the cut along the wider gap first: the column a page keeps beside its list (a gap of a gutter) before the rows of a grid (a gap of a few px)
                List<List<int>> major = Split(s, nodes, true, out float _, Gutter);
                if (major.Count > 1 && Columns(major))
                {
                    // the gutters between panels first: one column of the page after the other, each then read by its own bands
                    for (int i = 0; i < major.Count; i++)
                    {
                        int id = ++next;
                        Cut(s, major[i], into, col, ref next, id, ref lastX);
                    }
                    return;
                }
                List<List<int>> bands = Split(s, nodes, false, out float bandGap);
                List<List<int>> columns = Split(s, nodes, true, out float columnGap);
                bool aRow = columns.Count > 1;
                for (int i = 0; i < columns.Count; i++)
                {
                    aRow &= columns[i].Count == 1;
                }
                if (columns.Count > 1 && !aRow && !Columns(columns))
                {
                    columns.Clear();   // a lone button at a corner is no column: the page is read by its bands
                }
                if (bands.Count > 1 && (columns.Count < 2 || bandGap >= columnGap))
                {
                    for (int i = 0; i < bands.Count; i++)
                    {
                        Cut(s, bands[i], into, col, ref next, colId, ref lastX);
                    }
                    return;
                }
                if (columns.Count > 1)
                {
                    if (aRow && Tall(s, columns[0][0]) && Math.Abs(Centre(s, columns[columns.Count - 1][0], true) - lastX) < Math.Abs(Centre(s, columns[0][0], true) - lastX))
                    {
                        columns.Reverse();   // a row of single targets is read from its nearer end
                    }
                    for (int i = 0; i < columns.Count; i++)
                    {
                        int id = ++next;
                        Cut(s, columns[i], into, col, ref next, id, ref lastX);
                    }
                    return;
                }
                // no gap either way: rows of targets that stand level, from the top
                nodes.Sort((a, b) =>
                {
                    int byY = Centre(s, a, false).CompareTo(Centre(s, b, false));
                    return byY != 0 ? byY : Centre(s, a, true).CompareTo(Centre(s, b, true));
                });
                int from = 0;
                while (from < nodes.Count)
                {
                    int to = from + 1;
                    s.NodeBox(nodes[from], out float _, out float ry, out float _, out float rh);
                    float bottom = ry + rh, topY = ry;
                    while (to < nodes.Count)
                    {
                        float cy = Centre(s, nodes[to], false);
                        if (cy > bottom || cy < topY)
                        {
                            break;
                        }
                        s.NodeBox(nodes[to], out float _, out float ny, out float _, out float nh);
                        topY = Math.Min(topY, ny);
                        bottom = Math.Max(bottom, ny + nh);
                        to++;
                    }
                    var row = nodes.GetRange(from, to - from);
                    row.Sort((a, b) => Centre(s, a, true).CompareTo(Centre(s, b, true)));
                    if (row.Count > 1 && Tall(s, row[0]) && Math.Abs(Centre(s, row[row.Count - 1], true) - lastX) < Math.Abs(Centre(s, row[0], true) - lastX))
                    {
                        row.Reverse();
                    }
                    for (int k = 0; k < row.Count; k++)
                    {
                        into.Add(row[k]);
                        col[row[k]] = colId;
                    }
                    lastX = Centre(s, row[row.Count - 1], true);
                    from = to;
                }
                return;
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                into.Add(nodes[i]);
                col[nodes[i]] = colId;
                lastX = Centre(s, nodes[i], true);
            }
        }

        // The targets in groups along one axis (vertical: side by side, else one under another), cut where a gap of nothing runs clear across the other.
        private static List<List<int>> Split(UiScene s, List<int> nodes, bool sideBySide, out float widest, float apart = Gap)
        {
            var sorted = new List<int>(nodes);
            sorted.Sort((a, b) => Start(s, a, sideBySide).CompareTo(Start(s, b, sideBySide)));
            var groups = new List<List<int>>(4) { new List<int>(sorted.Count) };
            float reach = float.MinValue;
            widest = 0f;
            for (int i = 0; i < sorted.Count; i++)
            {
                float start = Start(s, sorted[i], sideBySide);
                if (i > 0 && start > reach + apart)
                {
                    groups.Add(new List<int>(sorted.Count - i));
                    widest = Math.Max(widest, start - reach);
                }
                groups[groups.Count - 1].Add(sorted[i]);
                reach = i == 0 ? start + Length(s, sorted[i], sideBySide) : Math.Max(reach, start + Length(s, sorted[i], sideBySide));
            }
            return groups;
        }

        // A card or a tile, not a chip or a button: only those are read in a snake (the end of a row, then the next row from that end); a strip of chips reads left to right.
        private static bool Tall(UiScene s, int node) => Length(s, node, false) >= TileHeight && Length(s, node, true) >= TileHeight * 2f;

        private static float Start(UiScene s, int node, bool x)
        {
            s.NodeBox(node, out float nx, out float ny, out float _, out float _);
            return x ? nx : ny;
        }

        private static float Length(UiScene s, int node, bool x)
        {
            s.NodeBox(node, out float _, out float _, out float nw, out float nh);
            return x ? nw : nh;
        }

        private static float Centre(UiScene s, int node, bool x)
        {
            s.NodeBox(node, out float nx, out float ny, out float nw, out float nh);
            return x ? nx + nw / 2f : ny + nh / 2f;
        }

        /// <summary>The fewest d-pad presses from one target to another through the real links, or -1 when more than <see cref="HopAcross"/> + 1 are needed.</summary>
        public static int Distance(UiScene s, int from, int to)
        {
            if (from == to)
            {
                return 0;
            }
            var seen = new bool[s.NodeCount];
            var frontier = new List<int>(8) { from };
            var next = new List<int>(8);
            seen[from] = true;
            for (int depth = 1; depth <= HopAcross + 1 && frontier.Count > 0; depth++)
            {
                next.Clear();
                for (int f = 0; f < frontier.Count; f++)
                {
                    for (int d = 0; d < 4; d++)
                    {
                        int m = UiFocusRules.Next(s, frontier[f], d == 0 ? 1 : d == 1 ? -1 : 0, d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (m < 0 || seen[m])
                        {
                            continue;
                        }
                        if (m == to)
                        {
                            return depth;
                        }
                        seen[m] = true;
                        next.Add(m);
                    }
                }
                List<int> swap = frontier;
                frontier = next;
                next = swap;
            }
            return -1;
        }
    }
}
