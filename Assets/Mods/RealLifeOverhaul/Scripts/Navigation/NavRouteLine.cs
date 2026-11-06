using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// A line through points, drawn as one ribbon by the game's interface, masked and batched like any picture of the HUD.
    /// Four vertices a point: the inner two carry the colour and the outer two fade out, a soft edge with no texture. The
    /// mesh is made only when <see cref="Set"/> is called, never per frame.
    /// </summary>
    public sealed class NavRouteLine : MaskableGraphic
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("navigation", "NavRouteLine", typeof(NavRouteLine));

        private const int MostPoints = 15000;   // four vertices each: under the 65,000 one mesh of the interface holds
        private const float MiterLimit = 2.5f;  // times the half width a corner may reach out

        private float[] _x;
        private float[] _y;
        private int _count;
        private float _half = 3f;
        private float _soft = 0.6f;

        /// <summary>The points, kept by reference (the caller calls again when they change), the width and the soft edge's width.</summary>
        public void Set(float[] x, float[] y, int count, float width, float soft)
        {
            _x = x;
            _y = y;
            _count = count > MostPoints ? MostPoints : count;
            _half = width * 0.5f;
            _soft = soft;
            SetVerticesDirty();
        }

        public void Clear()
        {
            if (_count != 0)
            {
                _count = 0;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (FrameGuard.Off)
            {
                Guard.NoMesh(vh);   // the feature is off: nothing drawn
                return;
            }
            try
            {
                vh.Clear();
                if (_count < 2 || _x == null || _y == null)
                {
                    return;
                }
                Color32 solid = color;
                Color32 clear = solid;
                clear.a = 0;
                float inner = _half - _soft * 0.5f;
                float outer = _half + _soft * 0.5f;
                if (inner < 0f)
                {
                    inner = 0f;
                }
                var uv = new Vector4(0.5f, 0.5f, 0f, 0f);
                float beforeX = 0f, beforeY = 0f;
                for (int i = 0; i < _count; i++)
                {
                    // The direction into the point and out of it; across the line is their average turned a quarter.
                    float afterX = 0f, afterY = 0f;
                    if (i + 1 < _count)
                    {
                        afterX = _x[i + 1] - _x[i];
                        afterY = _y[i + 1] - _y[i];
                        float length = Mathf.Sqrt(afterX * afterX + afterY * afterY);
                        if (length > 0.0001f)
                        {
                            afterX /= length;
                            afterY /= length;
                        }
                        else
                        {
                            afterX = beforeX;
                            afterY = beforeY;
                        }
                    }
                    else
                    {
                        afterX = beforeX;
                        afterY = beforeY;
                    }
                    if (i == 0)
                    {
                        beforeX = afterX;
                        beforeY = afterY;
                    }
                    float alongX = beforeX + afterX;
                    float alongY = beforeY + afterY;
                    float sum = Mathf.Sqrt(alongX * alongX + alongY * alongY);
                    float stretch = 1f;
                    if (sum > 0.0001f)
                    {
                        alongX /= sum;
                        alongY /= sum;
                        // At a bend the ribbon's corner lies further out than its half width, by one over the cosine of half the bend.
                        float cosine = alongX * afterX + alongY * afterY;
                        stretch = cosine > 1f / MiterLimit ? 1f / cosine : MiterLimit;
                    }
                    else
                    {
                        alongX = afterX;   // a hairpin: square across the way out
                        alongY = afterY;
                    }
                    float acrossX = -alongY * stretch;
                    float acrossY = alongX * stretch;
                    float x = _x[i];
                    float y = _y[i];
                    vh.AddVert(new Vector3(x + acrossX * outer, y + acrossY * outer), clear, uv);
                    vh.AddVert(new Vector3(x + acrossX * inner, y + acrossY * inner), solid, uv);
                    vh.AddVert(new Vector3(x - acrossX * inner, y - acrossY * inner), solid, uv);
                    vh.AddVert(new Vector3(x - acrossX * outer, y - acrossY * outer), clear, uv);
                    if (i > 0)
                    {
                        int a = (i - 1) * 4;
                        int b = i * 4;
                        for (int band = 0; band < 3; band++)
                        {
                            vh.AddTriangle(a + band, b + band, b + band + 1);
                            vh.AddTriangle(a + band, b + band + 1, a + band + 1);
                        }
                    }
                    beforeX = afterX;
                    beforeY = afterY;
                }
            }
            catch (System.Exception fault)
            {
                Guard.FailMesh(FrameGuard, fault, vh);   // half a mesh is not drawn
            }
        }
    }
}
