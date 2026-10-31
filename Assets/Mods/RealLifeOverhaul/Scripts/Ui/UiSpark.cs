using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A sparkline: values drawn as one line across the graphic's box, the lowest at its foot and the highest at its top,
    /// as a strip of quads built only when the values change. Cost a frame: nothing; a mesh of two triangles a step when
    /// its values are set.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiSpark : MaskableGraphic
    {
        private float[] _points;

        /// <summary>The line's width, in canvas units.</summary>
        public float Thickness = 4f;

        /// <summary>The values to draw, oldest first; the mesh is built again only if they are another array.</summary>
        public void SetPoints(float[] points)
        {
            if (!ReferenceEquals(points, _points))
            {
                _points = points;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float[] p = _points;
            if (p == null || p.Length < 2)
            {
                return;
            }
            Rect r = rectTransform.rect;
            float lo = p[0], hi = p[0];
            for (int i = 1; i < p.Length; i++)
            {
                lo = Mathf.Min(lo, p[i]);
                hi = Mathf.Max(hi, p[i]);
            }
            float span = Mathf.Max(1e-6f, hi - lo);
            float half = Thickness / 2f;
            float inset = half;
            Color32 c = color;
            Vector2 prev = Point(r, p, 0, lo, span, inset);
            for (int i = 1; i < p.Length; i++)
            {
                Vector2 next = Point(r, p, i, lo, span, inset);
                Vector2 d = (next - prev).normalized;
                var n = new Vector2(-d.y, d.x) * half;
                int at = vh.currentVertCount;
                vh.AddVert(prev - n, c, Vector4.zero);
                vh.AddVert(prev + n, c, Vector4.zero);
                vh.AddVert(next + n, c, Vector4.zero);
                vh.AddVert(next - n, c, Vector4.zero);
                vh.AddTriangle(at, at + 1, at + 2);
                vh.AddTriangle(at, at + 2, at + 3);
                prev = next;
            }
        }

        private static Vector2 Point(Rect r, float[] p, int i, float lo, float span, float inset)
        {
            float x = r.xMin + inset + (r.width - inset * 2f) * i / (p.Length - 1);
            float y = r.yMin + inset + (r.height - inset * 2f) * (p[i] - lo) / span;
            return new Vector2(x, y);
        }
    }
}
