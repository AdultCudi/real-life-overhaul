using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// One picture of the Economy app drawn as a single UI mesh: lines, filled areas with a fade, bars rounded at the top, discs
    /// and rounded boxes, with soft (feathered) edges. A whole chart is one draw however many days it has. Positions are
    /// in canvas units from the picture's top left, y counting down. Built when the page is written; nothing per frame.
    /// </summary>
    internal sealed class OutlookMesh : MaskableGraphic
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("economy_app", "OutlookMesh", typeof(OutlookMesh));   // the game's calls of it

        public const float Feather = 2.2f;    // canvas units the soft edge of a line or a disc spreads

        private readonly List<Vector3> _pos = new List<Vector3>(2048);
        private readonly List<Color32> _col = new List<Color32>(2048);
        private readonly List<int> _tri = new List<int>(6144);

        /// <summary>Shapes added since <see cref="Begin"/>, for the tests: a drawn chart has some.</summary>
        public int Shapes { get; private set; }

        public void Begin()
        {
            _pos.Clear();
            _col.Clear();
            _tri.Clear();
            Shapes = 0;
        }

        public void End()
        {
            SetVerticesDirty();
        }

        private int Vert(float x, float y, Color32 c)
        {
            _pos.Add(new Vector3(x, -y, 0f));
            _col.Add(c);
            return _pos.Count - 1;
        }

        private void Tri(int a, int b, int c)
        {
            _tri.Add(a);
            _tri.Add(b);
            _tri.Add(c);
        }

        private static Color32 Clear(Color32 c)
        {
            return new Color32(c.r, c.g, c.b, 0);
        }

        // ------------------------------------------------------------------ shapes

        public void Rect(float x0, float y0, float x1, float y1, Color32 c)
        {
            Fade(x0, y0, x1, y1, c, c);
        }

        /// <summary>A rectangle fading from one colour at its top to another at its foot.</summary>
        public void Fade(float x0, float y0, float x1, float y1, Color32 top, Color32 bottom)
        {
            if (x1 <= x0 || y1 <= y0)
            {
                return;
            }
            int a = Vert(x0, y0, top), b = Vert(x1, y0, top), c = Vert(x1, y1, bottom), d = Vert(x0, y1, bottom);
            Tri(a, b, c);
            Tri(c, d, a);
            Shapes++;
        }

        public void Triangle(float ax, float ay, float bx, float by, float cx, float cy, Color32 c)
        {
            int a = Vert(ax, ay, c), b = Vert(bx, by, c), d = Vert(cx, cy, c);
            Tri(a, b, d);
            Shapes++;
        }

        /// <summary>A disc with a soft edge.</summary>
        public void Disc(float cx, float cy, float r, Color32 c)
        {
            if (r <= 0f)
            {
                return;
            }
            int segments = Mathf.Clamp(Mathf.CeilToInt(r * 0.9f), 10, 28);
            int centre = Vert(cx, cy, c);
            int first = _pos.Count;
            for (int i = 0; i < segments; i++)
            {
                float t = i * Mathf.PI * 2f / segments;
                Vert(cx + Mathf.Cos(t) * r, cy + Mathf.Sin(t) * r, c);
            }
            int ring = _pos.Count;
            for (int i = 0; i < segments; i++)
            {
                float t = i * Mathf.PI * 2f / segments;
                Vert(cx + Mathf.Cos(t) * (r + Feather), cy + Mathf.Sin(t) * (r + Feather), Clear(c));
            }
            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                Tri(centre, first + i, first + n);
                Tri(first + i, ring + i, ring + n);
                Tri(first + i, ring + n, first + n);
            }
            Shapes++;
        }

        /// <summary>A rounded box: <paramref name="top"/> and <paramref name="bottom"/> are the radii of its upper and lower corners.</summary>
        public void Round(float x0, float y0, float x1, float y1, float top, float bottom, Color32 c)
        {
            if (x1 <= x0 || y1 <= y0)
            {
                return;
            }
            float limit = Mathf.Min(x1 - x0, y1 - y0) * 0.5f;
            top = Mathf.Clamp(top, 0f, limit);
            bottom = Mathf.Clamp(bottom, 0f, limit);
            int centre = Vert((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, c);
            int first = _pos.Count;
            Corner(x1 - top, y0 + top, top, -90f, c);        // top right
            Corner(x1 - bottom, y1 - bottom, bottom, 0f, c);   // bottom right
            Corner(x0 + bottom, y1 - bottom, bottom, 90f, c);   // bottom left
            Corner(x0 + top, y0 + top, top, 180f, c);          // top left
            int count = _pos.Count - first;
            for (int i = 0; i < count; i++)
            {
                Tri(centre, first + i, first + (i + 1) % count);
            }
            Shapes++;
        }

        // A quarter circle's points from an angle (degrees, y down), or the corner point itself when the radius is nothing.
        private void Corner(float cx, float cy, float r, float from, Color32 c)
        {
            if (r < 0.5f)
            {
                Vert(cx, cy, c);   // a square corner: the corner point itself
                return;
            }
            int steps = Mathf.Clamp(Mathf.CeilToInt(r / 5f), 3, 9);
            for (int i = 0; i <= steps; i++)
            {
                float a = (from + 90f * i / steps) * Mathf.Deg2Rad;
                Vert(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, c);
            }
        }

        /// <summary>A bar standing on a line, its top corners rounded.</summary>
        public void Bar(float x0, float x1, float yTop, float yBottom, float radius, Color32 c)
        {
            Round(x0, yTop, x1, yBottom, radius, 0f, c);
        }

        // ------------------------------------------------------------------ lines and areas

        /// <summary>
        /// A line through points, <paramref name="width"/> wide, joined at the points and soft at both edges. One point is a dot.
        /// </summary>
        public void Line(float[] xs, float[] ys, int n, float width, Color32 c)
        {
            if (xs == null || ys == null || n <= 0)
            {
                return;
            }
            if (n == 1)
            {
                Disc(xs[0], ys[0], width * 0.5f, c);
                return;
            }
            float half = Mathf.Max(0.5f, width * 0.5f);
            Color32 clear = Clear(c);
            // Each segment's unit normal; a vertex takes the mean of its two, longer where the line turns (held to 2.2 times).
            float px = 0f, py = 0f;
            int before = -1;
            var normals = n <= NormalsHeld ? _normals : new float[n * 2];
            for (int k = 0; k < n - 1; k++)
            {
                float dx = xs[k + 1] - xs[k], dy = ys[k + 1] - ys[k];
                float length = Mathf.Sqrt(dx * dx + dy * dy);
                if (length > 1e-4f)
                {
                    px = -dy / length;
                    py = dx / length;
                }
                else if (k == 0)
                {
                    px = 0f;
                    py = 1f;
                }
                normals[k * 2] = px;
                normals[k * 2 + 1] = py;
            }
            for (int k = 0; k < n; k++)
            {
                float ax = k > 0 ? normals[(k - 1) * 2] : normals[0], ay = k > 0 ? normals[(k - 1) * 2 + 1] : normals[1];
                float bx = k < n - 1 ? normals[k * 2] : ax, by = k < n - 1 ? normals[k * 2 + 1] : ay;
                float mx = ax + bx, my = ay + by;
                float ml = Mathf.Sqrt(mx * mx + my * my);
                if (ml < 1e-4f)
                {
                    mx = bx;
                    my = by;
                    ml = 1f;
                }
                mx /= ml;
                my /= ml;
                float cos = Mathf.Max(0.45f, mx * bx + my * by);   // 1 / cos is how much longer the join stands out
                float scale = 1f / cos;
                float inner = half * scale, outer = (half + Feather) * scale;
                int a = Vert(xs[k] - mx * outer, ys[k] - my * outer, clear);
                Vert(xs[k] - mx * inner, ys[k] - my * inner, c);
                Vert(xs[k] + mx * inner, ys[k] + my * inner, c);
                Vert(xs[k] + mx * outer, ys[k] + my * outer, clear);
                if (before >= 0)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        Tri(before + j, before + j + 1, a + j + 1);
                        Tri(before + j, a + j + 1, a + j);
                    }
                }
                before = a;
            }
            Shapes++;
        }

        private const int NormalsHeld = 1024;
        private readonly float[] _normals = new float[NormalsHeld * 2];

        /// <summary>The area under a line down to <paramref name="baseline"/>, fading from <paramref name="top"/> at the line to <paramref name="bottom"/> at the foot.</summary>
        public void Area(float[] xs, float[] ys, int n, float baseline, Color32 top, Color32 bottom)
        {
            if (xs == null || ys == null || n < 2)
            {
                return;
            }
            int before = -1;
            for (int k = 0; k < n; k++)
            {
                float y = Mathf.Min(ys[k], baseline);
                int a = Vert(xs[k], y, top);
                Vert(xs[k], baseline, bottom);
                if (before >= 0)
                {
                    Tri(before, before + 1, a + 1);
                    Tri(before, a + 1, a);
                }
                before = a;
            }
            Shapes++;
        }

        // ------------------------------------------------------------------ the graphic

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (FrameGuard.Off)
            {
                Guard.NoMesh(vh);   // its feature is off: nothing drawn
                return;
            }
            try
            {
                vh.Clear();
                UIVertex v = UIVertex.simpleVert;
                for (int i = 0; i < _pos.Count; i++)
                {
                    v.position = _pos[i];
                    v.color = _col[i];
                    vh.AddVert(v);
                }
                for (int t = 0; t + 2 < _tri.Count; t += 3)
                {
                    vh.AddTriangle(_tri[t], _tri[t + 1], _tri[t + 2]);
                }
            }
            catch (Exception fault)
            {
                Guard.FailMesh(FrameGuard, fault, vh);   // half a mesh is not drawn
            }
        }
    }
}
