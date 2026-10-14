using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The little chart of the city index on the paper's markets page: its closes over the last sessions as one line in the
    /// paper's ink, over three faint rules, with the last close marked. A graphic of the game's interface (a mesh the canvas
    /// draws with the rest of the page), filled once when the page is written: nothing per frame.
    /// </summary>
    public sealed class LedgerSpark : MaskableGraphic
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ledger", "LedgerSpark", typeof(LedgerSpark));   // the game's calls of it (Core/Guard)

        private readonly List<int> _closes = new List<int>(LedgerCatalogue.ChartDays);
        private Color _rule = new Color(0f, 0f, 0f, 0.15f);
        private float _thickness = 2f;

        public static LedgerSpark Make(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var spark = go.AddComponent<LedgerSpark>();
            spark.raycastTarget = false;
            return spark;
        }

        /// <summary>Draws these closes (oldest first) in the given ink, with rules in a fainter one.</summary>
        public void Show(List<int> closes, Color ink, Color rule, float thickness)
        {
            _closes.Clear();
            if (closes != null)
            {
                _closes.AddRange(closes);
            }
            color = ink;
            _rule = rule;
            _thickness = Mathf.Max(1f, thickness);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                vh.Clear();
                Rect r = GetPixelAdjustedRect();
                if (r.width < 4f || r.height < 4f)
                {
                    return;
                }
                for (int i = 0; i < 3; i++)
                {
                    float y = r.yMin + r.height * (0.1f + 0.4f * i);
                    Box(vh, new Rect(r.xMin, y - 0.5f, r.width, 1f), _rule);
                }
                if (_closes.Count < 2)
                {
                    return;
                }
                int low = int.MaxValue, high = int.MinValue;
                for (int i = 0; i < _closes.Count; i++)
                {
                    low = Mathf.Min(low, _closes[i]);
                    high = Mathf.Max(high, _closes[i]);
                }
                float span = Mathf.Max(1f, high - low);
                float step = r.width / (_closes.Count - 1);
                Vector2 last = Point(r, 0, step, low, span);
                for (int i = 1; i < _closes.Count; i++)
                {
                    Vector2 next = Point(r, i, step, low, span);
                    Segment(vh, last, next, _thickness, color);
                    last = next;
                }
                float dot = _thickness * 2.2f;
                Box(vh, new Rect(last.x - dot, last.y - dot, dot * 2f, dot * 2f), color);
            }
            catch (System.Exception fault)
            {
                Guard.FailMesh(FrameGuard, fault, vh);   // (half a mesh is not drawn)
            }
        }

        private Vector2 Point(Rect r, int i, float step, int low, float span)
        {
            return new Vector2(r.xMin + step * i, r.yMin + r.height * (0.1f + 0.8f * ((_closes[i] - low) / span)));
        }

        private static void Box(VertexHelper vh, Rect r, Color c)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color c)
        {
            Vector2 along = b - a;
            if (along.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Vector2 across = new Vector2(-along.y, along.x).normalized * (thickness * 0.5f);
            int start = vh.currentVertCount;
            vh.AddVert(a - across, c, Vector2.zero);
            vh.AddVert(a + across, c, Vector2.zero);
            vh.AddVert(b + across, c, Vector2.zero);
            vh.AddVert(b - across, c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
