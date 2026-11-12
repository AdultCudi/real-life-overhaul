using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A sparkline: values drawn as one line across the graphic's box, the lowest at its foot and the highest at its top,
    /// as a strip of quads built only when the values change. A value that is no number has no point: the line runs on
    /// from the last value that has one, inside the box. Cost a frame: nothing; a mesh of two triangles a step when its
    /// values are set.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiSpark : MaskableGraphic
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiSpark", typeof(UiSpark));   // the engine's calls of its mesh (Core/GuardCalls)
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
            if (FrameGuard.Off)
            {
                Guard.NoMesh(vh);
                return;
            }
            try
            {
                vh.Clear();
                float[] p = _points;
                if (!UiDraw.SparkSpan(p, out float lo, out float hi))
                {
                    return;
                }
                Rect r = rectTransform.rect;
                float half = Thickness / 2f;
                float inset = half;
                Color32 c = color;
                Vector2 prev = Vector2.zero;
                bool begun = false;
                for (int i = 0; i < p.Length; i++)
                {
                    if (!UiChart.Real(p[i]))
                    {
                        continue;   // a vertex at NaN has no place on the canvas, and one NaN in the span would put every vertex there
                    }
                    Vector2 next = Point(r, p, i, lo, hi, inset);
                    if (begun)
                    {
                        Vector2 d = (next - prev).normalized;
                        var n = new Vector2(-d.y, d.x) * half;
                        int at = vh.currentVertCount;
                        vh.AddVert(prev - n, c, Vector4.zero);
                        vh.AddVert(prev + n, c, Vector4.zero);
                        vh.AddVert(next + n, c, Vector4.zero);
                        vh.AddVert(next - n, c, Vector4.zero);
                        vh.AddTriangle(at, at + 1, at + 2);
                        vh.AddTriangle(at, at + 2, at + 3);
                    }
                    prev = next;
                    begun = true;
                }

            }
            catch (System.Exception fault)
            {
                Guard.FailMesh(FrameGuard, fault, vh);
            }
        }

        private static Vector2 Point(Rect r, float[] p, int i, float lo, float hi, float inset)
        {
            float x = r.xMin + inset + (r.width - inset * 2f) * i / (p.Length - 1);
            float y = r.yMin + inset + (r.height - inset * 2f) * UiDraw.SparkShare(p[i], lo, hi);
            return new Vector2(x, y);
        }
    }
}
