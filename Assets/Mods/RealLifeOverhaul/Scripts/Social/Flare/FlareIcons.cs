using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The glyphs Flare's screens use.</summary>
    internal enum FlareGlyph
    {
        Home, Explore, Bell, Mail, Person, Store, Plus, Heart, HeartFull, Reply, Repost, Camera, Picture, At, Back, Close, Flag, Star, StarFull,
        Pin, Calendar, Check, Selfie, City, Send, Mute, Hide,
        Trash, Prev, Next, Phone, Share   // (the Gallery's: Photos/GalleryApp)
    }

    /// <summary>
    /// Flare's glyphs, drawn once each in code into a small white texture with soft edges (as the phone's own skin draws
    /// its artwork, Phone/PhoneSkin): simple strokes and shapes, tinted by the image that shows them. A glyph is drawn the
    /// first time a screen asks for it and kept for the session. COST: one 96-pixel square each, once.
    /// </summary>
    internal static class FlareIcons
    {
        private const int Size = 96;
        private const float Stroke = 0.075f;      // a line's width, of the glyph's size
        private static readonly Dictionary<FlareGlyph, Sprite> Made = new Dictionary<FlareGlyph, Sprite>(32);
        private static float[] _a;

        public static Sprite Get(FlareGlyph glyph)
        {
            Sprite s;
            if (Made.TryGetValue(glyph, out s) && s != null)
            {
                return s;
            }
            _a = new float[Size * Size];
            Draw(glyph);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "FlareGlyph" + glyph, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[Size * Size];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(_a[i]) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            Made[glyph] = s;
            _a = null;
            return s;
        }

        private static void Draw(FlareGlyph g)
        {
            switch (g)
            {
                case FlareGlyph.Home:
                    Line(0.12f, 0.50f, 0.50f, 0.86f);
                    Line(0.50f, 0.86f, 0.88f, 0.50f);
                    BoxLine(0.24f, 0.12f, 0.76f, 0.56f);
                    Box(0.43f, 0.12f, 0.57f, 0.34f, 0.02f, true);
                    break;
                case FlareGlyph.Explore:
                    Line(0.40f, 0.12f, 0.46f, 0.88f);
                    Line(0.60f, 0.12f, 0.66f, 0.88f);
                    Line(0.18f, 0.36f, 0.84f, 0.36f);
                    Line(0.16f, 0.64f, 0.82f, 0.64f);
                    break;
                case FlareGlyph.Bell:
                    Circle(0.50f, 0.58f, 0.25f, true);
                    Box(0.25f, 0.30f, 0.75f, 0.58f, 0f, true);
                    Line(0.16f, 0.30f, 0.84f, 0.30f);
                    Circle(0.50f, 0.17f, 0.08f, true);
                    Circle(0.50f, 0.86f, 0.05f, true);
                    break;
                case FlareGlyph.Mail:
                    BoxLine(0.12f, 0.22f, 0.88f, 0.78f);
                    Line(0.14f, 0.76f, 0.50f, 0.46f);
                    Line(0.50f, 0.46f, 0.86f, 0.76f);
                    break;
                case FlareGlyph.Person:
                    Circle(0.50f, 0.68f, 0.17f, true);
                    Box(0.20f, 0.10f, 0.80f, 0.44f, 0.16f, true);
                    break;
                case FlareGlyph.Store:
                    Box(0.12f, 0.62f, 0.88f, 0.80f, 0.04f, true);
                    BoxLine(0.20f, 0.12f, 0.80f, 0.62f);
                    Box(0.42f, 0.12f, 0.58f, 0.38f, 0f, true);
                    break;
                case FlareGlyph.Plus:
                    Line(0.50f, 0.16f, 0.50f, 0.84f, 0.11f);
                    Line(0.16f, 0.50f, 0.84f, 0.50f, 0.11f);
                    break;
                case FlareGlyph.Heart:
                    Heart(true);
                    Heart(false, -1f);
                    break;
                case FlareGlyph.HeartFull:
                    Heart(true);
                    break;
                case FlareGlyph.Reply:
                    BoxLine(0.12f, 0.32f, 0.88f, 0.86f, 0.14f);
                    Line(0.26f, 0.34f, 0.18f, 0.14f);
                    Line(0.18f, 0.14f, 0.44f, 0.32f);
                    break;
                case FlareGlyph.Repost:
                    Line(0.20f, 0.38f, 0.20f, 0.66f);
                    Line(0.20f, 0.66f, 0.70f, 0.66f);
                    Line(0.62f, 0.76f, 0.74f, 0.66f);
                    Line(0.62f, 0.56f, 0.74f, 0.66f);
                    Line(0.80f, 0.62f, 0.80f, 0.34f);
                    Line(0.80f, 0.34f, 0.30f, 0.34f);
                    Line(0.38f, 0.44f, 0.26f, 0.34f);
                    Line(0.38f, 0.24f, 0.26f, 0.34f);
                    break;
                case FlareGlyph.Camera:
                    BoxLine(0.10f, 0.20f, 0.90f, 0.72f, 0.10f);
                    Ring(0.50f, 0.46f, 0.15f);
                    Box(0.34f, 0.70f, 0.58f, 0.82f, 0.03f, true);
                    break;
                case FlareGlyph.Picture:
                    BoxLine(0.10f, 0.16f, 0.90f, 0.84f, 0.08f);
                    Tri(0.18f, 0.24f, 0.44f, 0.58f, 0.66f, 0.24f);
                    Tri(0.50f, 0.24f, 0.66f, 0.46f, 0.82f, 0.24f);
                    Circle(0.68f, 0.66f, 0.08f, true);
                    break;
                case FlareGlyph.At:
                    Ring(0.50f, 0.50f, 0.34f);
                    Ring(0.48f, 0.50f, 0.12f);
                    Line(0.60f, 0.38f, 0.60f, 0.62f);
                    Line(0.60f, 0.38f, 0.78f, 0.34f);
                    break;
                case FlareGlyph.Back:
                    Line(0.80f, 0.50f, 0.22f, 0.50f, 0.09f);
                    Line(0.22f, 0.50f, 0.46f, 0.74f, 0.09f);
                    Line(0.22f, 0.50f, 0.46f, 0.26f, 0.09f);
                    break;
                case FlareGlyph.Close:
                    Line(0.24f, 0.24f, 0.76f, 0.76f, 0.09f);
                    Line(0.24f, 0.76f, 0.76f, 0.24f, 0.09f);
                    break;
                case FlareGlyph.Flag:
                    Line(0.26f, 0.10f, 0.26f, 0.88f);
                    Box(0.26f, 0.50f, 0.80f, 0.86f, 0.03f, true);
                    break;
                case FlareGlyph.Star:
                case FlareGlyph.StarFull:
                    StarShape(g == FlareGlyph.StarFull);
                    break;
                case FlareGlyph.Pin:
                    Circle(0.50f, 0.62f, 0.26f, true);
                    Tri(0.28f, 0.52f, 0.72f, 0.52f, 0.50f, 0.08f);
                    Circle(0.50f, 0.62f, 0.10f, false);
                    break;
                case FlareGlyph.Calendar:
                    BoxLine(0.14f, 0.12f, 0.86f, 0.78f, 0.06f);
                    Box(0.14f, 0.58f, 0.86f, 0.78f, 0.04f, true);
                    Line(0.34f, 0.70f, 0.34f, 0.88f);
                    Line(0.66f, 0.70f, 0.66f, 0.88f);
                    break;
                case FlareGlyph.Check:
                    Line(0.18f, 0.52f, 0.42f, 0.28f, 0.10f);
                    Line(0.42f, 0.28f, 0.84f, 0.72f, 0.10f);
                    break;
                case FlareGlyph.Selfie:
                    Ring(0.50f, 0.50f, 0.38f);
                    Circle(0.50f, 0.58f, 0.11f, true);
                    Box(0.32f, 0.24f, 0.68f, 0.42f, 0.09f, true);
                    break;
                case FlareGlyph.City:
                    Box(0.10f, 0.12f, 0.34f, 0.62f, 0.02f, true);
                    Box(0.38f, 0.12f, 0.62f, 0.86f, 0.02f, true);
                    Box(0.66f, 0.12f, 0.90f, 0.48f, 0.02f, true);
                    break;
                case FlareGlyph.Send:
                    Tri(0.12f, 0.20f, 0.12f, 0.80f, 0.90f, 0.50f);
                    Tri(0.12f, 0.42f, 0.12f, 0.58f, 0.46f, 0.50f, false);
                    break;
                case FlareGlyph.Mute:
                    Box(0.14f, 0.36f, 0.34f, 0.64f, 0.02f, true);
                    Tri(0.30f, 0.50f, 0.56f, 0.82f, 0.56f, 0.18f);
                    Line(0.66f, 0.34f, 0.88f, 0.66f);
                    Line(0.66f, 0.66f, 0.88f, 0.34f);
                    break;
                case FlareGlyph.Hide:
                    Ring(0.50f, 0.50f, 0.22f);
                    Line(0.14f, 0.50f, 0.30f, 0.66f);
                    Line(0.86f, 0.50f, 0.70f, 0.66f);
                    Line(0.16f, 0.16f, 0.84f, 0.84f, 0.08f);
                    break;
                case FlareGlyph.Trash:
                    Line(0.16f, 0.76f, 0.84f, 0.76f);
                    BoxLine(0.40f, 0.76f, 0.60f, 0.88f, 0.03f);
                    BoxLine(0.24f, 0.12f, 0.76f, 0.70f, 0.06f);
                    Line(0.41f, 0.24f, 0.41f, 0.58f);
                    Line(0.59f, 0.24f, 0.59f, 0.58f);
                    break;
                case FlareGlyph.Prev:
                    Line(0.64f, 0.84f, 0.32f, 0.50f, 0.10f);
                    Line(0.32f, 0.50f, 0.64f, 0.16f, 0.10f);
                    break;
                case FlareGlyph.Next:
                    Line(0.36f, 0.84f, 0.68f, 0.50f, 0.10f);
                    Line(0.68f, 0.50f, 0.36f, 0.16f, 0.10f);
                    break;
                case FlareGlyph.Phone:
                    BoxLine(0.28f, 0.08f, 0.72f, 0.92f, 0.10f);
                    Line(0.44f, 0.82f, 0.56f, 0.82f, 0.05f);
                    Circle(0.50f, 0.17f, 0.04f, true);
                    break;
                case FlareGlyph.Share:
                    Line(0.20f, 0.56f, 0.20f, 0.14f);
                    Line(0.20f, 0.14f, 0.80f, 0.14f);
                    Line(0.80f, 0.14f, 0.80f, 0.56f);
                    Line(0.50f, 0.32f, 0.50f, 0.88f);
                    Line(0.50f, 0.88f, 0.33f, 0.71f);
                    Line(0.50f, 0.88f, 0.67f, 0.71f);
                    break;
            }
        }

        // ---------------------------------------------------------------- shapes (0 to 1, y up; 'add' false takes away)

        private static void Put(int x, int y, float a, bool add)
        {
            int i = y * Size + x;
            _a[i] = add ? Mathf.Max(_a[i], a) : Mathf.Min(_a[i], 1f - a);
        }

        private static float Cover(float distance)
        {
            return Mathf.Clamp01(0.5f - distance * Size);
        }

        private static void Line(float ax, float ay, float bx, float by, float width = Stroke)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float px = (x + 0.5f) / Size, py = (y + 0.5f) / Size;
                    float dx = bx - ax, dy = by - ay;
                    float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / Mathf.Max(1e-6f, dx * dx + dy * dy));
                    float ex = px - (ax + t * dx), ey = py - (ay + t * dy);
                    Put(x, y, Cover(Mathf.Sqrt(ex * ex + ey * ey) - width / 2f), true);
                }
            }
        }

        private static void Circle(float cx, float cy, float r, bool add)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f) / Size - cx, dy = (y + 0.5f) / Size - cy;
                    Put(x, y, Cover(Mathf.Sqrt(dx * dx + dy * dy) - r), add);
                }
            }
        }

        private static void Ring(float cx, float cy, float r)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f) / Size - cx, dy = (y + 0.5f) / Size - cy;
                    Put(x, y, Cover(Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) - Stroke / 2f), true);
                }
            }
        }

        private static float BoxDistance(float px, float py, float x0, float y0, float x1, float y1, float r)
        {
            float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f, hx = (x1 - x0) / 2f - r, hy = (y1 - y0) / 2f - r;
            float qx = Mathf.Abs(px - cx) - hx, qy = Mathf.Abs(py - cy) - hy;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static void Box(float x0, float y0, float x1, float y1, float r, bool add)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Put(x, y, Cover(BoxDistance((x + 0.5f) / Size, (y + 0.5f) / Size, x0, y0, x1, y1, r)), add);
                }
            }
        }

        private static void BoxLine(float x0, float y0, float x1, float y1, float r = 0.04f)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = BoxDistance((x + 0.5f) / Size, (y + 0.5f) / Size, x0, y0, x1, y1, r);
                    Put(x, y, Cover(Mathf.Abs(d) - Stroke / 2f), true);
                }
            }
        }

        private static void Tri(float ax, float ay, float bx, float by, float cx, float cy, bool add = true)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float px = (x + 0.5f) / Size, py = (y + 0.5f) / Size;
                    float d = Mathf.Max(Edge(px, py, ax, ay, bx, by), Mathf.Max(Edge(px, py, bx, by, cx, cy), Edge(px, py, cx, cy, ax, ay)));
                    float sign = Area(ax, ay, bx, by, cx, cy) >= 0f ? 1f : -1f;
                    if (sign < 0f)
                    {
                        d = Mathf.Max(-Edge(px, py, ax, ay, bx, by), Mathf.Max(-Edge(px, py, bx, by, cx, cy), -Edge(px, py, cx, cy, ax, ay)));
                    }
                    Put(x, y, Cover(d), add);
                }
            }
        }

        // The signed distance to the line through a and b: positive to its right.
        private static float Edge(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, len = Mathf.Max(1e-6f, Mathf.Sqrt(dx * dx + dy * dy));
            return ((px - ax) * dy - (py - ay) * dx) / len;
        }

        private static float Area(float ax, float ay, float bx, float by, float cx, float cy)
        {
            return (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        }

        // A heart; with grow -1 a smaller one taken away (an outline).
        private static void Heart(bool add, float grow = 0f)
        {
            float shrink = grow < 0f ? Stroke : 0f;
            Circle(0.33f, 0.62f, 0.19f - shrink, add);
            Circle(0.67f, 0.62f, 0.19f - shrink, add);
            Tri(0.15f + shrink * 1.2f, 0.58f, 0.85f - shrink * 1.2f, 0.58f, 0.50f, 0.13f + shrink * 1.6f, add);
        }

        private static void StarShape(bool full)
        {
            var xs = new float[10];
            var ys = new float[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.42f : 0.17f;
                xs[i] = 0.5f + r * Mathf.Cos(angle);
                ys[i] = 0.5f + r * Mathf.Sin(angle);
            }
            for (int i = 0; i < 10; i += 2)
            {
                Tri(0.5f, 0.5f, xs[i], ys[i], xs[(i + 1) % 10], ys[(i + 1) % 10]);
                Tri(0.5f, 0.5f, xs[i], ys[i], xs[(i + 9) % 10], ys[(i + 9) % 10]);
            }
            if (!full)
            {
                for (int i = 0; i < 10; i++)
                {
                    xs[i] = 0.5f + (xs[i] - 0.5f) * 0.6f;
                    ys[i] = 0.5f + (ys[i] - 0.5f) * 0.6f;
                }
                for (int i = 0; i < 10; i += 2)
                {
                    Tri(0.5f, 0.5f, xs[i], ys[i], xs[(i + 1) % 10], ys[(i + 1) % 10], false);
                    Tri(0.5f, 0.5f, xs[i], ys[i], xs[(i + 9) % 10], ys[(i + 9) % 10], false);
                }
            }
        }
    }
}
