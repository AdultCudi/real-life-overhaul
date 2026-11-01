using System;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The one icon family: Phosphor's icons (MIT), filled from the outlines the mod carries (UiIconRaster) into a white
    /// 96-texel texture tinted where shown, a duotone's back a lighter tone of the same tint. A duotone also comes filled,
    /// Phosphor's solid weight, for a toolbar's tile. The keyboard's arrows and the pad's buttons are no icon set's, so
    /// they stay drawn in code, bold strokes on a 24-unit grid. A pad's lettered buttons (A, B, LB) are a shape here and
    /// their letter in the kit's font (UiView). Each glyph is drawn the first time it is asked for, or ahead behind a
    /// loading screen (<see cref="Warm"/>), and kept for the session. Cost: well under a millisecond a glyph once, a field
    /// read after.
    /// </summary>
    public static class UiIcons
    {
        private const int Size = 96;            // texels: 2 to a pixel at the 48 px of a tile and 4 at the 24 px of a row, both exact
        private const float Unit = Size / 24f;  // texels in a grid unit of the glyphs drawn in code
        private const float Stroke = 2.25f;     // grid units: bold, as Phosphor's bold weight reads
        private const float MipBias = -0.5f;    // the finer of two mips, so a glyph is only ever shrunk, never a smaller one blown up
        private static readonly Sprite[] Made = new Sprite[(int)UiIcon.Count];
        private static readonly Sprite[] MadeFilled = new Sprite[(int)UiIcon.Count];
        private static float[] _a;

        /// <summary>
        /// A glyph's sprite, drawn on first use. Filled asks for a duotone's solid variant, and an icon without one (the
        /// bold chrome, the pad's glyphs) gives its main weight. Null for None.
        /// </summary>
        /// <summary>
        /// The game's own picture for an icon where it has one (DIRECTION_CS2: the game's icons first), set by the game file;
        /// null, or a null answer, draws the family's own. Never set offline, where the family stands in.
        /// </summary>
        public static Func<UiIcon, Sprite> Own;

        public static Sprite Get(UiIcon icon, bool filled = false)
        {
            if (icon == UiIcon.None || (int)icon >= Made.Length)
            {
                return null;
            }
            filled = filled && UiIconRaster.HasFilled(icon);
            Sprite s = filled ? MadeFilled[(int)icon] : Made[(int)icon];
            if (s != null)
            {
                return s;
            }
            _a = new float[Size * Size];
            if (!UiIconRaster.Fill(icon, _a, Size, filled))
            {
                Draw(icon);
            }
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                int from = y * Size, to = (Size - 1 - y) * Size;   // texture rows run bottom up, the glyph's top down
                for (int x = 0; x < Size; x++)
                {
                    px[to + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(_a[from + x]) * 255f));
                }
            }
            // Mips, as a row's 24 px shown straight from 96 texels skips three in four of them and breaks thin strokes up.
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "UiIcon" + icon + (filled ? "Filled" : ""), wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, mipMapBias = MipBias
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            if (filled)
            {
                MadeFilled[(int)icon] = s;
            }
            else
            {
                Made[(int)icon] = s;
            }
            _a = null;
            return s;
        }

        /// <summary>Draws the next glyph not yet drawn, filled variants too, for a warm-up spread a glyph at a time. False when all are drawn.</summary>
        public static bool Warm()
        {
            for (int i = 1; i < Made.Length; i++)
            {
                if (Made[i] == null)
                {
                    Get((UiIcon)i);
                    return true;
                }
                if (MadeFilled[i] == null && UiIconRaster.HasFilled((UiIcon)i))
                {
                    Get((UiIcon)i, true);
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

        // The glyph's grid and the map both run top down, the way UiIconRaster fills it.
        private static void Cover(int x, int y, float inside)
        {
            float a = Mathf.Clamp01(inside + 0.5f);
            int i = y * Size + x;
            if (a > _a[i])
            {
                _a[i] = a;
            }
        }
    }
}
