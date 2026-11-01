using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Fills an icon's coverage from its Phosphor outlines (UiIconPaths) into an n by n alpha map, top row first: its main
    /// weight, or for a duotone its solid fill weight when asked. Each layer is filled by the nonzero rule, as SVG fills it,
    /// on sixteen sample lines a texel row, each line's spans covering the texels they cross exactly, so an edge near level
    /// still steps in sixteenths. Then the layer is laid over the ones below at its opacity. Game-free, so EconomyCheck
    /// fills every icon the game shows. Cost: nothing a frame, a fraction of a millisecond an icon at 96 texels once a
    /// session, in scratch kept from icon to icon. One caller at a time.
    /// </summary>
    public static class UiIconRaster
    {
        /// <summary>
        /// A duotone's back layer. Phosphor's 0.2, made for white pages, all but fades out at 24 px on the slate panel, and 0.4
        /// turns an amber or red back into a muddy block that swamps the outline's details.
        /// </summary>
        public const float DuotoneBack = 0.32f;
        private const float PhosphorBack = 0.2f;   // the back layer's opacity in Phosphor's duotone files
        private const int Lines = 16;              // sample lines a texel row
        private const float Share = 1f / Lines;    // what one sample line gives a texel it covers whole

        private static float[] _x, _top, _bottom, _slope;   // a layer's edges, each kept from its top end down
        private static int[] _dir, _next, _active;          // +1 for an edge that runs down, edges by their first row, edges in the row
        private static int[] _firstInRow;
        private static float[] _hitX, _cover, _run;          // a line's crossings, a row's coverage and its runs of whole texels
        private static int[] _hitDir;

        /// <summary>Whether an icon is filled from outlines: every icon but None and the keys' and the pad's glyphs.</summary>
        public static bool Has(UiIcon icon) => UiIconPaths.Of(icon, false) != null;

        /// <summary>Whether an icon has a filled variant of its own: every duotone. The rest show their main weight when asked.</summary>
        public static bool HasFilled(UiIcon icon)
        {
            short[] main = UiIconPaths.Of(icon, false);
            return main != null && UiIconPaths.Of(icon, true) != main;
        }

        /// <summary>An icon's layers: two for a duotone (its back, then its outline), one for the rest, none without outlines.</summary>
        public static int Layers(UiIcon icon, bool filled = false)
        {
            short[] p = UiIconPaths.Of(icon, filled);
            return p == null ? 0 : p[0];
        }

        /// <summary>
        /// Fills alpha (n by n, top row first) with the icon's coverage, 0 to 1, its filled variant when asked and it has
        /// one. False, and alpha untouched, for an icon with no outlines or a map too small.
        /// </summary>
        public static bool Fill(UiIcon icon, float[] alpha, int n, bool filled = false) => Fill(UiIconPaths.Of(icon, filled), alpha, n);

        /// <summary>Fills alpha from outlines in UiIconPaths' form: an icon's own, or a shape a check builds to try the rules.</summary>
        internal static bool Fill(short[] p, float[] alpha, int n)
        {
            if (p == null || n <= 0 || alpha == null || alpha.Length < n * n)
            {
                return false;
            }
            Array.Clear(alpha, 0, n * n);
            int at = 1;
            for (int layer = 0; layer < p[0]; layer++)
            {
                float opacity = p[at] >= 100 ? 1f : Math.Min(1f, p[at] / 100f * (DuotoneBack / PhosphorBack));
                int contours = p[at + 1];
                at = Layer(p, at + 2, contours, opacity, alpha, n);
            }
            return true;
        }

        private static int Layer(short[] p, int at, int contours, float opacity, float[] alpha, int n)
        {
            int edges = 0;
            for (int c = 0, scan = at; c < contours; c++)
            {
                edges += p[scan];
                scan += 1 + 2 * p[scan];
            }
            Room(edges, n);
            float scale = n / (float)UiIconPaths.Box;
            int e = 0;
            for (int c = 0; c < contours; c++)
            {
                int count = p[at];
                int firstX = p[at + 1], firstY = p[at + 2];
                int x = firstX, y = firstY;
                for (int i = 1; i <= count; i++)
                {
                    int nextX = firstX, nextY = firstY;   // the last edge closes the contour
                    if (i < count)
                    {
                        nextX = x + p[at + 1 + 2 * i];
                        nextY = y + p[at + 2 + 2 * i];
                    }
                    if (nextY != y)   // a level edge meets no sample line
                    {
                        bool down = y < nextY;
                        int topX = down ? x : nextX, topY = down ? y : nextY, bottomY = down ? nextY : y;
                        _x[e] = topX * scale;
                        _top[e] = topY * scale;
                        _bottom[e] = bottomY * scale;
                        _slope[e] = ((down ? nextX : x) - topX) / (float)(bottomY - topY);
                        _dir[e] = down ? 1 : -1;
                        e++;
                    }
                    x = nextX;
                    y = nextY;
                }
                at += 1 + 2 * count;
            }
            for (int row = 0; row < n; row++)
            {
                _firstInRow[row] = -1;
            }
            for (int i = 0; i < e; i++)
            {
                if (_bottom[i] <= 0f || _top[i] >= n)
                {
                    continue;
                }
                int row = _top[i] <= 0f ? 0 : (int)_top[i];
                _next[i] = _firstInRow[row];
                _firstInRow[row] = i;
            }
            int active = 0;
            for (int row = 0; row < n; row++)
            {
                for (int i = _firstInRow[row]; i >= 0; i = _next[i])
                {
                    _active[active++] = i;
                }
                if (active == 0)
                {
                    continue;
                }
                for (int s = 0; s < Lines; s++)
                {
                    Line(row + (s + 0.5f) * Share, active, n);
                }
                Lay(alpha, row * n, n, opacity);
                int kept = 0;
                for (int k = 0; k < active; k++)
                {
                    if (_bottom[_active[k]] > row + 1)
                    {
                        _active[kept++] = _active[k];
                    }
                }
                active = kept;
            }
            return at;
        }

        // One sample line: the crossings in order along it, and the spans the winding count leaves at nonzero.
        private static void Line(float y, int active, int n)
        {
            int hits = 0;
            for (int k = 0; k < active; k++)
            {
                int i = _active[k];
                if (y < _top[i] || y >= _bottom[i])
                {
                    continue;   // half open, so a line through a shared end counts the corner once
                }
                float x = _x[i] + (y - _top[i]) * _slope[i];
                int h = hits++;
                while (h > 0 && _hitX[h - 1] > x)
                {
                    _hitX[h] = _hitX[h - 1];
                    _hitDir[h] = _hitDir[h - 1];
                    h--;
                }
                _hitX[h] = x;
                _hitDir[h] = _dir[i];
            }
            int winding = 0;
            float from = 0f;
            for (int h = 0; h < hits; h++)
            {
                int was = winding;
                winding += _hitDir[h];
                if (was == 0 && winding != 0)
                {
                    from = _hitX[h];
                }
                else if (was != 0 && winding == 0)
                {
                    Span(from, _hitX[h], n);
                }
            }
        }

        // A span's exact share of each texel it crosses: the two part texels directly, the whole ones between as a run.
        private static void Span(float from, float to, int n)
        {
            from = Math.Max(from, 0f);
            to = Math.Min(to, n);
            if (to <= from)
            {
                return;
            }
            int a = (int)from, b = (int)to;
            if (a == b)
            {
                _cover[a] += (to - from) * Share;
                return;
            }
            _cover[a] += (a + 1 - from) * Share;
            _run[a + 1] += Share;
            _run[b] -= Share;
            _cover[b] += (to - b) * Share;   // b may be n, the slot past the row
        }

        // The row's coverage at the layer's opacity, laid over what the layers below left, and the row's scratch cleared.
        private static void Lay(float[] alpha, int at, int n, float opacity)
        {
            float run = 0f;
            for (int x = 0; x < n; x++)
            {
                run += _run[x];
                float a = _cover[x] + run;
                _cover[x] = 0f;
                _run[x] = 0f;
                if (a > 0f)
                {
                    a = (a > 1f ? 1f : a) * opacity;
                    alpha[at + x] = a + alpha[at + x] * (1f - a);
                }
            }
            _cover[n] = 0f;
            _run[n] = 0f;
        }

        private static void Room(int edges, int n)
        {
            if (_x == null || _x.Length < edges)
            {
                int size = Math.Max(edges, 512);
                _x = new float[size];
                _top = new float[size];
                _bottom = new float[size];
                _slope = new float[size];
                _dir = new int[size];
                _next = new int[size];
                _active = new int[size];
                _hitX = new float[size];
                _hitDir = new int[size];
            }
            if (_firstInRow == null || _firstInRow.Length < n + 1)
            {
                _firstInRow = new int[n + 1];
                _cover = new float[n + 1];
                _run = new float[n + 1];
            }
        }
    }
}
