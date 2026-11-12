using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The kit's shapes, drawn once a session into small white textures and nine-sliced at any size, so an edge stays one
    /// pixel and a corner keeps its radius however large the box: a filled rounded box and its one-pixel edge for each
    /// radius of the tokens, a circle and the focus ring's three-pixel stroke: flat shapes only, no glow or gradient. Sized in
    /// canvas units (a texel is a canvas unit), so they draw at their own scale on the game's 3840 x 2160 canvas.
    /// Cost: about a dozen textures of at most 160 texels once; a dictionary read a lookup.
    /// </summary>
    public static class UiSprites
    {
        private const float PixelsPerUnit = 100f;   // a canvas's reference pixels per unit, so a texel draws as one canvas unit
        private const int CircleRadius = 32;        // texels: the circle sprite's, scaled to each circle's own size

        private static readonly Dictionary<int, Sprite> Fills = new Dictionary<int, Sprite>(8);
        private static readonly Dictionary<int, Sprite> Tops = new Dictionary<int, Sprite>(4);
        private static readonly Dictionary<int, Sprite> Edges = new Dictionary<int, Sprite>(8);
        private static readonly Dictionary<int, Sprite> Rings = new Dictionary<int, Sprite>(8);
        private static Sprite _circle, _circleEdge, _donut, _ring;
        private static readonly Dictionary<int, Sprite> Faces = new Dictionary<int, Sprite>(32);
        private static readonly Queue<int> FaceOrder = new Queue<int>(32);
        private const int MostFaces = 160;   // faces kept at once, the oldest let go first: a long session's thousands of people never pile up

        /// <summary>A filled box with corners of a radius in px (0 square).</summary>
        public static Sprite Fill(float radiusPx)
        {
            int r = Texels(radiusPx);
            if (!Fills.TryGetValue(r, out Sprite s) || s == null)
            {
                s = Rounded(r, 0, "UiFill" + r);
                Fills[r] = s;
            }
            return s;
        }

        /// <summary>A filled box with its top corners rounded and its foot square: a title strip on its panel's head.</summary>
        public static Sprite FillTop(float radiusPx)
        {
            int r = Texels(radiusPx);
            if (!Tops.TryGetValue(r, out Sprite s) || s == null)
            {
                s = Rounded(r, 0, "UiFillTop" + r, true);
                Tops[r] = s;
            }
            return s;
        }

        /// <summary>A box's one-pixel edge with corners of a radius in px.</summary>
        public static Sprite Edge(float radiusPx)
        {
            int r = Texels(radiusPx);
            if (!Edges.TryGetValue(r, out Sprite s) || s == null)
            {
                s = Rounded(r, Texels(UiTokens.Hairline), "UiEdge" + r);
                Edges[r] = s;
            }
            return s;
        }

        /// <summary>The focus ring's stroke around a box whose corners have a radius in px (the ring's own corners are wider by its gap).</summary>
        public static Sprite Ring(float radiusPx)
        {
            int r = Texels(radiusPx + UiTokens.FocusGap + UiTokens.FocusStroke);
            if (!Rings.TryGetValue(r, out Sprite s) || s == null)
            {
                s = Rounded(r, Texels(UiTokens.FocusStroke), "UiRing" + r);
                Rings[r] = s;
            }
            return s;
        }

        /// <summary>A circle, or a pill when stretched long (nine-sliced at <see cref="CircleScale"/> of its height).</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = Rounded(CircleRadius, 0, "UiCircle"));

        /// <summary>A donut's ring, its hole 60% of its width as Cities: Skylines II draws its age donut, filled radially into slices.</summary>
        public static Sprite Donut
        {
            get
            {
                if (_donut != null)
                {
                    return _donut;
                }
                const int size = 256;
                const float outer = size / 2f - 1f, inner = outer * 0.6f;
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);   // a texel's share inside the ring, both edges soft by a texel
                        px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "UiDonut", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                tex.SetPixels32(px);
                tex.Apply(true, true);
                _donut = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
                return _donut;
            }
        }

        /// <summary>
        /// A thin ring, its stroke a fortieth of its width (4 px round a 340 px clock), filled radially as a donut's slice is:
        /// the time skip veil's way to its goal. A slice with a radius above 0 is drawn on it (UiDraw.Ring).
        /// </summary>
        public static Sprite ThinRing
        {
            get
            {
                if (_ring != null)
                {
                    return _ring;
                }
                const int size = 512;
                const float outer = size / 2f - 1f, inner = outer * 0.975f;
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "UiRing", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                tex.SetPixels32(px);
                tex.Apply(true, true);
                _ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
                return _ring;
            }
        }

        /// <summary>
        /// A painted face (UiPortraits) from a seed and a recorded age (0 for none), the same for the same ones, on a disc of a
        /// hue; made once a face. A screen never asks here for a person: it asks <see cref="UiFaces.Of"/>, the one rule.
        /// </summary>
        internal static Sprite Painted(int seed, int age, float hue)
        {
            int key = (seed ^ ((int)hue * 7919)) * 31 + age;
            if (Faces.TryGetValue(key, out Sprite s) && s != null)
            {
                return s;
            }
            if (Faces.Count >= MostFaces)
            {
                int oldest = FaceOrder.Dequeue();
                if (Faces.TryGetValue(oldest, out Sprite gone) && gone != null)
                {
                    UnityEngine.Object.Destroy(gone.texture);
                    UnityEngine.Object.Destroy(gone);
                }
                Faces.Remove(oldest);
            }
            const int size = 128;
            var rgba = new byte[size * size * 4];
            UiPortraits.Paint(seed, size, rgba, hue, age);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "UiPortrait", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixelData(rgba, 0);   // the top level only: Apply makes the smaller ones
            tex.Apply(true, true);   // mips for a face shown at 40 px, so it does not shimmer
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            Faces[key] = s;
            FaceOrder.Enqueue(key);
            return s;
        }

        /// <summary>The slicing scale that makes the circle's corners half a height of canvas units: a round end, never a rounded square.</summary>
        public static float CircleScale(float heightCanvas) => (CircleRadius + 1f) / Mathf.Max(1f, heightCanvas / 2f);

        /// <summary>A circle's one-pixel edge.</summary>
        public static Sprite CircleEdge => _circleEdge != null ? _circleEdge : (_circleEdge = Rounded(CircleRadius, Texels(UiTokens.Hairline), "UiCircleEdge"));

        private static int Texels(float px) => Mathf.Max(0, Mathf.RoundToInt(px * UiTokens.CanvasPerPx));

        // A white rounded box (stroke 0) or its outline, with soft edges, as a sprite whose corners do not stretch.
        private static Sprite Rounded(int radius, int stroke, string name, bool topOnly = false)
        {
            // The slicing border holds the whole corner and the whole stroke, so a square box's edge stays an edge and
            // its middle stays empty however large it is drawn.
            int border = Mathf.Max(radius, stroke) + 1;
            int size = Mathf.Max(4, border * 2 + 2);
            var px = new Color32[size * size];
            float r = Mathf.Max(0.5f, radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;
                    float dx = Mathf.Max(0f, Mathf.Max(r - cx, cx - (size - r)));
                    float dy = topOnly ? Mathf.Max(0f, cy - (size - r)) : Mathf.Max(0f, Mathf.Max(r - cy, cy - (size - r)));   // a texture's top is its high rows
                    float outside = (radius == 0 ? Mathf.Max(dx, dy) : Mathf.Sqrt(dx * dx + dy * dy)) - r;   // negative inside
                    if (radius == 0)
                    {
                        outside = -Mathf.Min(Mathf.Min(cx, size - cx), Mathf.Min(cy, size - cy));
                    }
                    float a = Mathf.Clamp01(0.5f - outside);
                    if (stroke > 0)
                    {
                        a *= Mathf.Clamp01(outside + stroke + 0.5f);   // only the band within the stroke of the edge
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            return Make(px, size, name, border);
        }

        private static Sprite Make(Color32[] px, int size, string name, float border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
