using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The d-pad's walk over a card from its first target to every other, in reading order (a row left to right, the rows top to bottom), each leg the shortest run of presses
    /// that UiFocusRules itself would take. The previews draw it as the pad path, and the checks hold that it reaches every target of the card and stays short, so a stepper or a
    /// second button that only a long way round reaches is seen. Game-free. Cost: a few searches over a card's targets, offline only.
    /// </summary>
    public static class EstateTour
    {
        private const float RowTolerance = 20f;   // px: targets this close in height stand in one row of the reading
        private const float ColumnGap = 160f;     // px: the least gap between the middles of two columns of targets

        /// <summary>The presses as letters (U, D, L, R) that visit every enabled target of the top layer, or "" when there is nothing to walk. A list's rows count as one target.</summary>
        public static string Walk(UiScene s, int start)
        {
            if (start < 0 || start >= s.NodeCount)
            {
                return string.Empty;
            }
            int top = s.Nodes[start].Layer;
            var order = new List<int>(s.NodeCount);
            var listed = new HashSet<int>();
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if (n.Disabled || n.Layer != top || (n.List >= 0 && !listed.Add(n.List)))
                {
                    continue;
                }
                order.Add(i);
            }
            // A card of two columns is read down its left column and then down its right: the columns are told apart by the one wide gap between the targets' middles.
            var middles = new List<float>(order.Count);
            foreach (int i in order)
            {
                middles.Add(s.Nodes[i].X + s.Nodes[i].W / 2f);
            }
            middles.Sort();
            float split = float.MaxValue, widest = ColumnGap;
            for (int i = 3; i + 3 <= middles.Count; i++)   // a column holds at least three targets: a lone plus beside a stepper is no column
            {
                if (middles[i] - middles[i - 1] > widest)
                {
                    widest = middles[i] - middles[i - 1];
                    split = (middles[i] + middles[i - 1]) / 2f;
                }
            }
            order.Sort((a, b) =>
            {
                int ca = s.Nodes[a].X + s.Nodes[a].W / 2f < split ? 0 : 1, cb = s.Nodes[b].X + s.Nodes[b].W / 2f < split ? 0 : 1;
                if (ca != cb)
                {
                    return ca.CompareTo(cb);
                }
                float ya = s.Nodes[a].Y, yb = s.Nodes[b].Y;
                return Math.Abs(ya - yb) > RowTolerance ? ya.CompareTo(yb) : s.Nodes[a].X.CompareTo(s.Nodes[b].X);
            });
            var seen = new bool[s.NodeCount];
            seen[start] = true;
            var walk = new System.Text.StringBuilder(64);
            int at = start;
            for (int i = 0; i < order.Count; i++)
            {
                int target = order[i];
                if (seen[target])
                {
                    continue;
                }
                string leg = Path(s, at, target, seen);
                if (leg == null)
                {
                    continue;   // not reached by the pad at all: the checks name it
                }
                walk.Append(leg);
                at = target;
            }
            return walk.ToString();
        }

        // The shortest run of presses from a target to another (a search over what each press leads to), marking each target passed on the way as visited.
        private static string Path(UiScene s, int from, int to, bool[] seen)
        {
            var before = new int[s.NodeCount];
            var press = new char[s.NodeCount];
            for (int i = 0; i < before.Length; i++)
            {
                before[i] = -2;
            }
            before[from] = -1;
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0 && before[to] == -2)
            {
                int at = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int dx = d == 0 ? 1 : d == 1 ? -1 : 0, dy = d == 2 ? 1 : d == 3 ? -1 : 0;
                    int next = UiFocusRules.Next(s, at, dx, dy);
                    if (next >= 0 && next < s.NodeCount && before[next] == -2)
                    {
                        before[next] = at;
                        press[next] = d == 0 ? 'R' : d == 1 ? 'L' : d == 2 ? 'D' : 'U';
                        queue.Enqueue(next);
                    }
                }
            }
            if (before[to] == -2)
            {
                return null;
            }
            var leg = new List<char>(8);
            for (int n = to; n != from; n = before[n])
            {
                leg.Add(press[n]);
                seen[n] = true;
            }
            leg.Reverse();
            return new string(leg.ToArray());
        }
    }
}
