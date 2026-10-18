using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The chart's picture: boxes and lines in one UI mesh, so a whole chart is one draw however many candles it has. Built
    /// when the chart asks; nothing per frame.
    /// </summary>
    internal sealed class ChartMesh : MaskableGraphic
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("markets", "ChartMesh", typeof(ChartMesh));   // the game's calls of it

        private struct Shape
        {
            public Vector2 A, B, C, D;
            public Color32 Colour;
        }

        private readonly List<Shape> _shapes = new List<Shape>(1024);

        public void Begin()
        {
            _shapes.Clear();
        }

        public void Box(float x0, float y0, float x1, float y1, Color32 colour)
        {
            if (x1 < x0)
            {
                float t = x0;
                x0 = x1;
                x1 = t;
            }
            if (y1 < y0)
            {
                float t = y0;
                y0 = y1;
                y1 = t;
            }
            _shapes.Add(new Shape { A = new Vector2(x0, y0), B = new Vector2(x0, y1), C = new Vector2(x1, y1), D = new Vector2(x1, y0), Colour = colour });
        }

        public void Line(Vector2 from, Vector2 to, float width, Color32 colour)
        {
            Vector2 along = to - from;
            float length = along.magnitude;
            if (length < 0.01f)
            {
                return;
            }
            Vector2 side = new Vector2(-along.y, along.x) / length * (width * 0.5f);
            _shapes.Add(new Shape { A = from - side, B = from + side, C = to + side, D = to - side, Colour = colour });
        }

        public void End()
        {
            SetVerticesDirty();
        }

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
                for (int i = 0; i < _shapes.Count; i++)
                {
                    Shape s = _shapes[i];
                    int at = vh.currentVertCount;
                    v.color = s.Colour;
                    v.position = s.A;
                    vh.AddVert(v);
                    v.position = s.B;
                    vh.AddVert(v);
                    v.position = s.C;
                    vh.AddVert(v);
                    v.position = s.D;
                    vh.AddVert(v);
                    vh.AddTriangle(at, at + 1, at + 2);
                    vh.AddTriangle(at + 2, at + 3, at);
                }
            }
            catch (Exception fault)
            {
                Guard.FailMesh(FrameGuard, fault, vh);   // half a mesh is not drawn
            }
        }
    }
}
