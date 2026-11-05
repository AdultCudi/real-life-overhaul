using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The d-pad's walk over a card: from its first target to every other, each target once and none twice, as one run of presses that UiFocusRules itself would take, and of all such runs
    /// the shortest on the card, so the line the preview draws crosses it no more than it must and every stop shows. Where no run can avoid standing twice somewhere, the walk goes on
    /// from the longest run it found by the shortest ways to the rest, in reading order (a row left to right, the rows top to bottom, a two-column card down its left column and then its
    /// right), and the check names the card. Game-free. Cost: a search over a card's dozen targets, offline only.
    /// </summary>
    public static class EstateTour
    {
        private const float RowTolerance = 20f;   // px: targets this close in height stand in one row of the reading
        private const float ColumnGap = 160f;     // px: the least gap between the middles of two columns of targets
        private const int SearchMost = 400000;    // steps of the search before it gives the walk it has

        /// <summary>The presses as letters (U, D, L, R) that visit every enabled target of the top layer once, or "" when there is nothing to walk. A list's rows count as one target.</summary>
        public static string Walk(UiScene s, int start) => Walk(s, start, out int stands, out int targets);

        /// <summary>The same walk, with how many distinct targets it stands on and how many the card has: all of them, each once, when the two agree and the presses are one fewer.</summary>
        public static string Walk(UiScene s, int start, out int stands, out int targets)
        {
            stands = targets = 0;
            if (start < 0 || start >= s.NodeCount)
            {
                return string.Empty;
            }
            int top = s.Nodes[start].Layer;
            var order = new List<int>(s.NodeCount);
            var listed = new Dictionary<int, int>();   // a list's id to the first of its rows: the list is one target
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if (n.Disabled || n.Layer != top)
                {
                    continue;
                }
                if (n.List >= 0)
                {
                    if (listed.ContainsKey(n.List))
                    {
                        continue;
                    }
                    listed[n.List] = i;
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
            var rank = new int[s.NodeCount];
            for (int i = 0; i < rank.Length; i++)
            {
                rank[i] = int.MaxValue;
            }
            for (int i = 0; i < order.Count; i++)
            {
                rank[order[i]] = i;
            }
            int startRep = Rep(s, start, listed);
            if (rank[startRep] == int.MaxValue)
            {
                order.Insert(0, startRep);
                rank[startRep] = 0;
            }
            targets = order.Count;
            // What each press leads to, as a target of the walk (a row of a list is its list).
            var next = new int[s.NodeCount][];
            var letters = new[] { 'R', 'L', 'D', 'U' };
            foreach (int i in order)
            {
                next[i] = new int[4];
                for (int d = 0; d < 4; d++)
                {
                    int dx = d == 0 ? 1 : d == 1 ? -1 : 0, dy = d == 2 ? 1 : d == 3 ? -1 : 0;
                    int to = UiFocusRules.Next(s, i, dx, dy);
                    to = to >= 0 && to < s.NodeCount ? Rep(s, to, listed) : -1;
                    next[i][d] = to >= 0 && to != i && rank[to] != int.MaxValue ? to : -1;
                }
            }
            var visited = new bool[s.NodeCount];
            var path = new List<int>(targets) { startRep };
            var presses = new List<char>(targets);
            var best = new List<char>();
            int bestStands = 1, steps = 0;
            float bestLength = float.MaxValue;
            visited[startRep] = true;
            Search(s, next, visited, path, presses, letters, targets, best, ref bestStands, ref bestLength, 0f, ref steps);
            // Where the pad can stand on every target only by passing one twice, the walk goes on from the longest run it found by the shortest ways to the rest, standing again where it must.
            if (bestStands < targets)
            {
                best = Continue(s, startRep, next, best, rank, letters, order, out bestStands);
            }
            stands = bestStands;
            return new string(best.ToArray());
        }

        private static int Rep(UiScene s, int node, Dictionary<int, int> listed) => s.Nodes[node].List >= 0 && listed.TryGetValue(s.Nodes[node].List, out int first) ? first : node;

        // A depth first search over the pad's moves for the run through every target once that is shortest on the card (the straightest line for the eye), the nearer target tried first.
        private static void Search(UiScene s, int[][] next, bool[] visited, List<int> path, List<char> presses, char[] letters, int targets, List<char> best, ref int bestStands,
            ref float bestLength, float length, ref int steps)
        {
            if (path.Count > bestStands || path.Count == targets && path.Count == bestStands && length < bestLength)
            {
                bestStands = path.Count;
                bestLength = path.Count == targets ? length : float.MaxValue;
                best.Clear();
                best.AddRange(presses);
            }
            if (path.Count == targets || length >= bestLength || ++steps > SearchMost)
            {
                return;
            }
            int at = path[path.Count - 1];
            var tries = new List<int>(4);
            for (int d = 0; d < 4; d++)
            {
                if (next[at][d] >= 0 && !visited[next[at][d]])
                {
                    tries.Add(d);
                }
            }
            tries.Sort((a, b) => Distance(s, at, next[at][a]).CompareTo(Distance(s, at, next[at][b])));
            foreach (int d in tries)
            {
                int to = next[at][d];
                visited[to] = true;
                path.Add(to);
                presses.Add(letters[d]);
                Search(s, next, visited, path, presses, letters, targets, best, ref bestStands, ref bestLength, length + Distance(s, at, to), ref steps);
                presses.RemoveAt(presses.Count - 1);
                path.RemoveAt(path.Count - 1);
                visited[to] = false;
            }
        }

        private static float Distance(UiScene s, int a, int b)
        {
            float dx = s.Nodes[a].X + s.Nodes[a].W / 2f - s.Nodes[b].X - s.Nodes[b].W / 2f, dy = s.Nodes[a].Y + s.Nodes[a].H / 2f - s.Nodes[b].Y - s.Nodes[b].H / 2f;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // The best run found, continued by the shortest ways to what it left out, in reading order.
        private static List<char> Continue(UiScene s, int start, int[][] next, List<char> run, int[] rank, char[] letters, List<int> order, out int stands)
        {
            var seen = new bool[s.NodeCount];
            seen[start] = true;
            int at = start;
            foreach (char c in run)
            {
                at = next[at][Array.IndexOf(letters, c)];
                seen[at] = true;
            }
            var walk = new List<char>(run);
            foreach (int target in order)
            {
                if (seen[target])
                {
                    continue;
                }
                List<char> leg = Path(next, letters, at, target, seen, s.NodeCount);
                if (leg == null)
                {
                    continue;   // not reached by the pad at all: the checks name it
                }
                walk.AddRange(leg);
                at = target;
            }
            int count = 0;
            for (int i = 0; i < seen.Length; i++)
            {
                count += seen[i] ? 1 : 0;
            }
            stands = count;
            return walk;
        }

        // The shortest run of presses from a target to another (a search over what each press leads to), marking each target passed on the way as visited.
        private static List<char> Path(int[][] next, char[] letters, int from, int to, bool[] seen, int count)
        {
            var before = new int[count];
            var press = new char[count];
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
                if (next[at] == null)
                {
                    continue;
                }
                for (int d = 0; d < 4; d++)
                {
                    int n = next[at][d];
                    if (n >= 0 && before[n] == -2)
                    {
                        before[n] = at;
                        press[n] = letters[d];
                        queue.Enqueue(n);
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
            return leg;
        }
    }
}
