using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The one icon family, drawn in code: bold strokes (2.25 of 24 units) with round ends and joins on a 24-unit grid, a
    /// few solid forms, each into a white 96-texel texture tinted where shown. A pad's lettered buttons (A, B, LB) are a
    /// shape here and their letter in the kit's font (UiView). Each glyph is drawn the first time it is asked for, or
    /// ahead behind a loading screen (<see cref="Warm"/>), and kept for the session. Cost: about 2 ms a glyph once; a
    /// field read after.
    /// </summary>
    public static class UiIcons
    {
        private const int Size = 96;            // texels
        private const float Unit = Size / 24f;  // texels in a grid unit
        private const float Stroke = 2.25f;     // grid units: bold, as a game's icons read at a glance
        private static readonly Sprite[] Made = new Sprite[(int)UiIcon.Count];
        private static float[] _a;

        /// <summary>A glyph's sprite, drawn on first use; null for None.</summary>
        public static Sprite Get(UiIcon icon)
        {
            if (icon == UiIcon.None || (int)icon >= Made.Length)
            {
                return null;
            }
            Sprite s = Made[(int)icon];
            if (s != null)
            {
                return s;
            }
            _a = new float[Size * Size];
            Draw(icon);
            var px = new Color32[Size * Size];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(_a[i]) * 255f));
            }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "UiIcon" + icon, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            Made[(int)icon] = s;
            _a = null;
            return s;
        }

        /// <summary>Draws the next glyph not yet drawn, for a warm-up spread a glyph at a time; false when all are drawn.</summary>
        public static bool Warm()
        {
            for (int i = 1; i < Made.Length; i++)
            {
                if (Made[i] == null)
                {
                    Get((UiIcon)i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether a pad glyph carries a letter drawn in the font over its shape, and which.</summary>
        public static string Letter(UiIcon icon)
        {
            switch (icon)
            {
                case UiIcon.PadA: return "A";
                case UiIcon.PadB: return "B";
                case UiIcon.PadX: return "X";
                case UiIcon.PadY: return "Y";
                case UiIcon.PadLb: return "LB";
                case UiIcon.PadRb: return "RB";
                case UiIcon.PadL1: return "L1";
                case UiIcon.PadR1: return "R1";
                default: return null;
            }
        }

        private static void Draw(UiIcon g)
        {
            switch (g)
            {
                case UiIcon.Search: Ring(10.5f, 10.5f, 6.5f); Line(15.5f, 15.5f, 20.5f, 20.5f); break;
                case UiIcon.Check: Poly(4.5f, 12.5f, 9.5f, 17.5f, 19.5f, 6.5f); break;
                case UiIcon.Close: Line(6f, 6f, 18f, 18f); Line(18f, 6f, 6f, 18f); break;
                case UiIcon.Back: Poly(15f, 5f, 8f, 12f, 15f, 19f); break;
                case UiIcon.Next: Poly(9f, 5f, 16f, 12f, 9f, 19f); break;
                case UiIcon.Up: Poly(5f, 15f, 12f, 8f, 19f, 15f); break;
                case UiIcon.Down: Poly(5f, 9f, 12f, 16f, 19f, 9f); break;
                case UiIcon.Phone:
                    Poly(6.5f, 3.5f, 9.5f, 3.5f, 11f, 8f, 8.8f, 9.6f);
                    Arc(17.5f, 6.5f, 11f, 90f, 180f);
                    Poly(14.4f, 15.2f, 16f, 13f, 20.5f, 14.5f, 20.5f, 17.5f);
                    Line(20.5f, 17.5f, 18f, 20.5f);
                    Line(6.5f, 3.5f, 3.5f, 6f);
                    break;
                case UiIcon.Message: RoundRect(3.5f, 4.5f, 17f, 12.5f, 3f, false); Poly(8f, 17f, 7f, 20.5f, 12f, 17f); break;
                case UiIcon.Send: Poly(3.5f, 11.5f, 20.5f, 3.5f, 13f, 20.5f, 10.5f, 13.5f, 3.5f, 11.5f); Line(10.5f, 13.5f, 20.5f, 3.5f); break;
                case UiIcon.Pin: Ring(12f, 8.5f, 4.5f); Line(12f, 13f, 12f, 20.5f); break;
                case UiIcon.Plus: Line(12f, 5f, 12f, 19f); Line(5f, 12f, 19f, 12f); break;
                case UiIcon.Minus: Line(5f, 12f, 19f, 12f); break;
                case UiIcon.Person: Ring(12f, 8f, 4f); Arc(12f, 21f, 7.5f, 180f, 360f); break;
                case UiIcon.People: Ring(9f, 8.5f, 3.5f); Arc(9f, 20.5f, 6f, 180f, 360f); Arc(16.5f, 8.5f, 3f, 290f, 430f); Arc(16.5f, 20.5f, 5f, 270f, 360f); break;
                case UiIcon.Briefcase: RoundRect(3.5f, 7.5f, 17f, 12.5f, 2.5f, false); Poly(9f, 7.5f, 9f, 4.5f, 15f, 4.5f, 15f, 7.5f); Line(3.5f, 13f, 20.5f, 13f); break;
                case UiIcon.Building:
                    RoundRect(5.5f, 3.5f, 13f, 17f, 1.5f, false);
                    Dot(9.5f, 8f, 1.2f); Dot(14.5f, 8f, 1.2f); Dot(9.5f, 12.5f, 1.2f); Dot(14.5f, 12.5f, 1.2f);
                    Line(12f, 16.5f, 12f, 20.5f);
                    break;
                case UiIcon.Coin: Ring(12f, 12f, 8.5f); Line(12f, 7f, 12f, 17f); Poly(14.5f, 9f, 10.5f, 9f, 9.5f, 11f, 14.5f, 13f, 13.5f, 15f, 9.5f, 15f); break;
                case UiIcon.Truck: RoundRect(2.5f, 6.5f, 11.5f, 10f, 1.5f, false); Poly(14f, 9.5f, 18f, 9.5f, 21f, 13f, 21f, 16.5f, 14f, 16.5f); Ring(7f, 17.5f, 2f); Ring(17f, 17.5f, 2f); break;
                case UiIcon.Sofa: Poly(5f, 11f, 5f, 7f, 19f, 7f, 19f, 11f); RoundRect(3f, 11f, 18f, 6f, 2f, false); Line(5f, 17f, 5f, 19.5f); Line(19f, 17f, 19f, 19.5f); break;
                case UiIcon.Flag: Line(5.5f, 3.5f, 5.5f, 20.5f); Poly(5.5f, 4.5f, 18.5f, 4.5f, 15.5f, 9f, 18.5f, 13.5f, 5.5f, 13.5f); break;
                case UiIcon.Star: Star(false); break;
                case UiIcon.Bell: Arc(12f, 11f, 6f, 180f, 360f); Poly(6f, 11f, 6f, 15.5f, 4f, 17.5f, 20f, 17.5f, 18f, 15.5f, 18f, 11f); Line(10.5f, 20.5f, 13.5f, 20.5f); break;
                case UiIcon.Clock: Ring(12f, 12f, 8.5f); Poly(12f, 7f, 12f, 12f, 15.5f, 14f); break;
                case UiIcon.Calendar: RoundRect(3.5f, 5f, 17f, 15.5f, 2.5f, false); Line(3.5f, 10f, 20.5f, 10f); Line(8f, 3f, 8f, 7f); Line(16f, 3f, 16f, 7f); break;
                case UiIcon.MapPin: Arc(12f, 10f, 6.5f, 150f, 390f); Poly(6.4f, 13.3f, 12f, 21f, 17.6f, 13.3f); Ring(12f, 10f, 2.2f); break;
                case UiIcon.Info: Ring(12f, 12f, 8.5f); Line(12f, 11f, 12f, 16.5f); Dot(12f, 7.75f, 1.4f); break;
                case UiIcon.Warning: Poly(12f, 3.5f, 21f, 19.5f, 3f, 19.5f, 12f, 3.5f); Line(12f, 9.5f, 12f, 13.5f); Dot(12f, 16.5f, 1.4f); break;
                case UiIcon.Trash: Line(4f, 6.5f, 20f, 6.5f); Poly(9f, 6.5f, 9f, 3.5f, 15f, 3.5f, 15f, 6.5f); Poly(6f, 6.5f, 7f, 20.5f, 17f, 20.5f, 18f, 6.5f); break;
                case UiIcon.Chart: Line(4f, 20f, 20f, 20f); Line(7f, 20f, 7f, 13f); Line(12f, 20f, 12f, 8f); Line(17f, 20f, 17f, 4.5f); break;
                case UiIcon.Heart: Heart(); break;
                case UiIcon.Home: Poly(3.5f, 11f, 12f, 3.5f, 20.5f, 11f); Poly(6f, 9f, 6f, 20f, 18f, 20f, 18f, 9f); Line(12f, 15f, 12f, 20f); break;
                case UiIcon.Store:
                    Poly(3.5f, 9f, 5f, 4f, 19f, 4f, 20.5f, 9f, 3.5f, 9f);
                    Poly(5f, 9f, 5f, 20f, 19f, 20f, 19f, 9f);
                    Poly(9.5f, 20f, 9.5f, 14f, 14.5f, 14f, 14.5f, 20f);
                    break;
                case UiIcon.Wrench: Arc(15.5f, 8.5f, 4.5f, 120f, 420f); Line(12.3f, 11.7f, 4.5f, 19.5f); break;
                case UiIcon.Cap:
                    Poly(2.5f, 9f, 12f, 4.5f, 21.5f, 9f, 12f, 13.5f, 2.5f, 9f);
                    Poly(6.5f, 11f, 6.5f, 16f, 12f, 18.5f, 17.5f, 16f, 17.5f, 11f);
                    Line(21.5f, 9f, 21.5f, 14.5f);
                    break;
                case UiIcon.Bank:
                    Poly(3f, 9f, 12f, 3.5f, 21f, 9f, 3f, 9f);
                    Line(6f, 11.5f, 6f, 17.5f);
                    Line(10f, 11.5f, 10f, 17.5f);
                    Line(14f, 11.5f, 14f, 17.5f);
                    Line(18f, 11.5f, 18f, 17.5f);
                    Line(3f, 20.5f, 21f, 20.5f);
                    break;
                case UiIcon.Gear: Gear(); break;
                case UiIcon.Sun: Ring(12f, 12f, 4f); Rays(12f, 12f, 7f, 9.5f, 8); break;
                case UiIcon.Moon: Arc(12f, 12f, 8.5f, 70f, 380f); Arc(16.5f, 8f, 6.5f, 110f, 340f); break;
                case UiIcon.Spark: Sparkle(); break;
                case UiIcon.TrendUp: Poly(3.5f, 17f, 9.5f, 11f, 13.5f, 15f, 20.5f, 7.5f); Poly(15f, 7.5f, 20.5f, 7.5f, 20.5f, 13f); break;
                case UiIcon.TrendDown: Poly(3.5f, 7f, 9.5f, 13f, 13.5f, 9f, 20.5f, 16.5f); Poly(15f, 16.5f, 20.5f, 16.5f, 20.5f, 11f); break;
                case UiIcon.Arrows: Tri(12f, 1.5f, 16.5f, 8f, 7.5f, 8f); Tri(12f, 22.5f, 7.5f, 16f, 16.5f, 16f); Tri(1.5f, 12f, 8f, 7.5f, 8f, 16.5f); Tri(22.5f, 12f, 16f, 16.5f, 16f, 7.5f); break;
                case UiIcon.PadDpad:
                    RoundRect(9f, 3f, 6f, 18f, 1.5f, true);
                    RoundRect(3f, 9f, 18f, 6f, 1.5f, true);
                    break;
                case UiIcon.PadCross: Line(7f, 7f, 17f, 17f); Line(17f, 7f, 7f, 17f); break;
                case UiIcon.PadCircle: Ring(12f, 12f, 6f); break;
                case UiIcon.PadSquare: RoundRect(6.5f, 6.5f, 11f, 11f, 0.5f, false); break;
                case UiIcon.PadTriangle: Poly(12f, 5.5f, 18.5f, 17f, 5.5f, 17f, 12f, 5.5f); break;
                default:
                    break;   // the lettered pad buttons: their shape is the key cap, their letter the font's
            }
        }

        // ------------------------------------------------------------------------------------------------- forms

        private static void Star(bool fill)
        {
            var pts = new float[22];
            for (int i = 0; i <= 10; i++)
            {
                float r = i % 2 == 0 ? 8.5f : 3.8f;
                float a = (-90f + i * 36f) * Mathf.Deg2Rad;
                pts[i * 2] = 12f + Mathf.Cos(a) * r;
                pts[i * 2 + 1] = 12.6f + Mathf.Sin(a) * r;
            }
            Poly(pts);
        }

        private static void Heart()
        {
            Arc(8.5f, 9f, 4.2f, 135f, 360f);
            Arc(15.5f, 9f, 4.2f, 180f, 405f);
            Poly(5.5f, 12f, 12f, 19.5f, 18.5f, 12f);
        }

        private static void Gear()
        {
            Ring(12f, 12f, 3f);
            Ring(12f, 12f, 7f);
            Rays(12f, 12f, 7.5f, 9.8f, 8);
        }

        private static void Sparkle()
        {
            Poly(12f, 3.5f, 13.6f, 10.4f, 20.5f, 12f, 13.6f, 13.6f, 12f, 20.5f, 10.4f, 13.6f, 3.5f, 12f, 10.4f, 10.4f, 12f, 3.5f);
        }

        private static void Rays(float cx, float cy, float r0, float r1, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                Line(cx + Mathf.Cos(a) * r0, cy + Mathf.Sin(a) * r0, cx + Mathf.Cos(a) * r1, cy + Mathf.Sin(a) * r1);
            }
        }

        // ------------------------------------------------------------------------------------------------- the painter

        private static void Poly(params float[] p)
        {
            for (int i = 0; i + 3 < p.Length; i += 2)
            {
                Line(p[i], p[i + 1], p[i + 2], p[i + 3]);
            }
        }

        private static void Line(float x1, float y1, float x2, float y2)
        {
            float half = Stroke * Unit / 2f;
            float ax = x1 * Unit, ay = y1 * Unit, bx = x2 * Unit, by = y2 * Unit;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, bx) - half - 1)), maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(ax, bx) + half + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, by) - half - 1)), maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(ay, by) + half + 1));
            float dx = bx - ax, dy = by - ay, len2 = Mathf.Max(1e-4f, dx * dx + dy * dy);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / len2);
                    float qx = ax + t * dx - px, qy = ay + t * dy - py;
                    Cover(x, y, half - Mathf.Sqrt(qx * qx + qy * qy));
                }
            }
        }

        private static void Ring(float cx, float cy, float r) => Arc(cx, cy, r, 0f, 360f);

        private static void Arc(float cx, float cy, float r, float from, float to)
        {
            int steps = Mathf.Max(8, Mathf.CeilToInt((to - from) / 8f));
            float px = 0f, py = 0f;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
                float x = cx + Mathf.Cos(a) * r, y = cy + Mathf.Sin(a) * r;
                if (i > 0)
                {
                    Line(px, py, x, y);
                }
                px = x;
                py = y;
            }
        }

        private static void Dot(float cx, float cy, float r)
        {
            float rr = r * Unit, x0 = cx * Unit, y0 = cy * Unit;
            for (int y = Mathf.Max(0, (int)(y0 - rr - 2)); y <= Mathf.Min(Size - 1, (int)(y0 + rr + 2)); y++)
            {
                for (int x = Mathf.Max(0, (int)(x0 - rr - 2)); x <= Mathf.Min(Size - 1, (int)(x0 + rr + 2)); x++)
                {
                    float dx = x + 0.5f - x0, dy = y + 0.5f - y0;
                    Cover(x, y, rr - Mathf.Sqrt(dx * dx + dy * dy));
                }
            }
        }

        private static void RoundRect(float x, float y, float w, float h, float r, bool fill)
        {
            if (!fill)
            {
                Line(x + r, y, x + w - r, y);
                Line(x + w, y + r, x + w, y + h - r);
                Line(x + w - r, y + h, x + r, y + h);
                Line(x, y + h - r, x, y + r);
                if (r > 0.01f)
                {
                    Arc(x + w - r, y + r, r, 270f, 360f);
                    Arc(x + w - r, y + h - r, r, 0f, 90f);
                    Arc(x + r, y + h - r, r, 90f, 180f);
                    Arc(x + r, y + r, r, 180f, 270f);
                }
                return;
            }
            float x0 = x * Unit, y0 = y * Unit, x1 = (x + w) * Unit, y1 = (y + h) * Unit, rr = r * Unit;
            for (int py = Mathf.Max(0, (int)y0 - 1); py <= Mathf.Min(Size - 1, (int)y1 + 1); py++)
            {
                for (int px = Mathf.Max(0, (int)x0 - 1); px <= Mathf.Min(Size - 1, (int)x1 + 1); px++)
                {
                    float cx = px + 0.5f, cy = py + 0.5f;
                    float dx = Mathf.Max(0f, Mathf.Max(x0 + rr - cx, cx - (x1 - rr)));
                    float dy = Mathf.Max(0f, Mathf.Max(y0 + rr - cy, cy - (y1 - rr)));
                    float inside = rr - Mathf.Sqrt(dx * dx + dy * dy);
                    if (dx <= 0f && dy <= 0f)
                    {
                        inside = Mathf.Min(Mathf.Min(cx - x0, x1 - cx), Mathf.Min(cy - y0, y1 - cy));
                    }
                    Cover(px, py, inside);
                }
            }
        }

        // A filled triangle with soft edges (the arrows of the keyboard's Move).
        private static void Tri(float x1, float y1, float x2, float y2, float x3, float y3)
        {
            float ax = x1 * Unit, ay = y1 * Unit, bx = x2 * Unit, by = y2 * Unit, cx = x3 * Unit, cy = y3 * Unit;
            int minX = Mathf.Max(0, (int)Mathf.Min(ax, Mathf.Min(bx, cx)) - 1), maxX = Mathf.Min(Size - 1, (int)Mathf.Max(ax, Mathf.Max(bx, cx)) + 1);
            int minY = Mathf.Max(0, (int)Mathf.Min(ay, Mathf.Min(by, cy)) - 1), maxY = Mathf.Min(Size - 1, (int)Mathf.Max(ay, Mathf.Max(by, cy)) + 1);
            float sign = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax) >= 0f ? 1f : -1f;   // either winding: inside is the area's side of every edge
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float d = Mathf.Min(sign * Edge(ax, ay, bx, by, px, py), Mathf.Min(sign * Edge(bx, by, cx, cy, px, py), sign * Edge(cx, cy, ax, ay, px, py)));
                    Cover(x, y, d);
                }
            }
        }

        // A point's signed distance from an edge's line: its sign tells the side.
        private static float Edge(float ax, float ay, float bx, float by, float px, float py)
        {
            float ex = bx - ax, ey = by - ay;
            float len = Mathf.Sqrt(ex * ex + ey * ey);
            return len < 1e-4f ? 0f : (ex * (py - ay) - ey * (px - ax)) / len;
        }

        // Texture rows run bottom up; the grid runs top down.
        private static void Cover(int x, int y, float inside)
        {
            float a = Mathf.Clamp01(inside + 0.5f);
            int i = (Size - 1 - y) * Size + x;
            if (a > _a[i])
            {
                _a[i] = a;
            }
        }
    }
}
