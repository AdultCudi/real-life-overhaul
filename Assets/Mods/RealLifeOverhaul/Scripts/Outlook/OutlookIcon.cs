using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// The Economy app's own glyph for its icon: three bars of a chart rising under a trend line, drawn once in white into a
    /// texture and handed to the game's icon painter, which stamps it on the game's tile as it does the game's own glyphs.
    /// Made once a session; nothing a frame.
    /// </summary>
    internal static class OutlookIcon
    {
        private const int Size = 128;
        private static Sprite _glyph;

        public static Sprite Glyph()
        {
            if (_glyph != null)
            {
                return _glyph;
            }
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;
                    float d = Mathf.Min(Bar(cx, cy, 30f, 84f, 24f), Mathf.Min(Bar(cx, cy, 64f, 68f, 40f), Bar(cx, cy, 98f, 50f, 58f)));   // the bars, signed distance outside (negative inside)
                    d = Mathf.Min(d, Segment(cx, cy, 24f, 52f, 52f, 34f) - 4.5f);
                    d = Mathf.Min(d, Segment(cx, cy, 52f, 34f, 74f, 44f) - 4.5f);
                    d = Mathf.Min(d, Segment(cx, cy, 74f, 44f, 104f, 16f) - 4.5f);
                    d = Mathf.Min(d, Mathf.Sqrt((cx - 104f) * (cx - 104f) + (cy - 16f) * (cy - 16f)) - 8f);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            // The texture's rows run bottom to top: the picture was drawn top down.
            var flipped = new Color32[pixels.Length];
            for (int y = 0; y < Size; y++)
            {
                System.Array.Copy(pixels, (Size - 1 - y) * Size, flipped, y * Size, Size);
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false) { name = "EconomyAppGlyph", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(flipped);
            texture.Apply(false, false);
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _glyph = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            _glyph.name = "EconomyAppGlyph";
            _glyph.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return _glyph;
        }

        // A bar standing on the line y = 108, 22 wide, its corners round: the distance from the point to it.
        private static float Bar(float px, float py, float centre, float top, float height)
        {
            float hx = 11f, hy = height / 2f, r = 6f;
            float cy = top + hy;
            float qx = Mathf.Abs(px - centre) - (hx - r), qy = Mathf.Abs(py - cy) - (hy - r);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float sx = ax + dx * t - px, sy = ay + dy * t - py;
            return Mathf.Sqrt(sx * sx + sy * sy);
        }
    }
}
