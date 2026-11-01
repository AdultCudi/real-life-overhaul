using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A face for a person without a picture: a flat painted bust on a disc (shoulders in a shirt, neck, head with ears,
    /// a hairstyle, two eyes), its skin tone, hair, hairstyle and shirt picked from the person's name, so the same person
    /// always looks the same. Painted by signed distances, soft at every edge, into an RGBA buffer; the view makes it a
    /// texture once a name. Game-free (EconomyCheck paints each style). Cost: about 1 ms a face once at 128 texels.
    /// </summary>
    public static class UiPortraits
    {
        // Skin tones light to deep, hair colours, and shirts in the chart's hues: soft, never a white or a neon.
        private static readonly uint[] Skins = { 0xF1C9A5FF, 0xE3B08AFF, 0xC98E5FFF, 0xA86B3CFF, 0x8D5524FF, 0x5E3B23FF };
        private static readonly uint[] Hairs = { 0x231C1AFF, 0x4A3426FF, 0x6F4E37FF, 0x9A4E2AFF, 0xC9A35EFF, 0x8E959CFF };
        private static readonly uint[] Shirts = { 0x4C86E0FF, 0x2FBF8FFF, 0xE69F2EFF, 0xD889BBFF, 0xA77BF0FF, 0x3FC7B6FF, 0x56B4E9FF, 0xF07A3AFF };

        /// <summary>The number of hairstyles a face may wear (short, long, bun, curly, cropped, side part).</summary>
        public const int Styles = 6;

        /// <summary>A stable seed from a name, the same in every session and in the offline checks.</summary>
        public static int Seed(string name)
        {
            unchecked
            {
                int h = (int)2166136261;
                for (int i = 0; name != null && i < name.Length; i++)
                {
                    h = (h ^ name[i]) * 16777619;
                }
                return h & 0x7FFFFFFF;
            }
        }

        /// <summary>Paints the face for a seed into a buffer of size x size RGBA texels, rows from the bottom as a texture holds them.</summary>
        public static void Paint(int seed, int size, byte[] rgba, float hue)
        {
            var rnd = new Random(seed);
            uint skin = Skins[rnd.Next(Skins.Length)], hair = Hairs[rnd.Next(Hairs.Length)], shirt = Shirts[rnd.Next(Shirts.Length)];
            int style = rnd.Next(Styles);
            UiColour g0 = UiColour.FromOklch(0.36f, 0.05f, hue);
            uint ground = ((uint)(g0.R * 255f) << 24) | ((uint)(g0.G * 255f) << 16) | ((uint)(g0.B * 255f) << 8) | 0xFF;
            float n = size;
            float cx = n / 2f, headY = n * 0.43f, headRx = n * 0.165f, headRy = n * 0.2f;
            Array.Clear(rgba, 0, rgba.Length);
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float x = px + 0.5f, y = size - py - 0.5f;   // y down from the top, as the shapes are laid out
                    float disc = Circle(x, y, cx, cx, cx - 0.5f);
                    if (disc > 1f)
                    {
                        continue;
                    }
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    Over(ref r, ref g, ref b, ref a, ground, Cover(disc));
                    if (style == 1)
                    {
                        // Long hair falls behind the head to the shoulders.
                        float back = RoundBox(x, y, cx - headRx * 1.25f, headY - headRy * 0.6f, cx + headRx * 1.25f, headY + headRy * 1.55f, headRx * 0.9f);
                        Over(ref r, ref g, ref b, ref a, hair, Cover(back));
                    }
                    float body = Ellipse(x, y, cx, n * 1.04f, n * 0.4f, n * 0.33f);
                    Over(ref r, ref g, ref b, ref a, shirt, Cover(body));
                    float neck = RoundBox(x, y, cx - n * 0.065f, headY + headRy * 0.6f, cx + n * 0.065f, n * 0.76f, n * 0.03f);
                    Over(ref r, ref g, ref b, ref a, Shade(skin, 0.86f), Cover(neck));
                    float ears = Math.Min(Circle(x, y, cx - headRx * 0.98f, headY + headRy * 0.08f, n * 0.035f), Circle(x, y, cx + headRx * 0.98f, headY + headRy * 0.08f, n * 0.035f));
                    Over(ref r, ref g, ref b, ref a, Shade(skin, 0.92f), Cover(ears));
                    float head = Ellipse(x, y, cx, headY, headRx, headRy);
                    Over(ref r, ref g, ref b, ref a, skin, Cover(head));
                    Over(ref r, ref g, ref b, ref a, hair, Cover(Hair(style, x, y, cx, headY, headRx, headRy, n)));
                    float eyes = Math.Min(Circle(x, y, cx - headRx * 0.38f, headY + headRy * 0.12f, n * 0.014f), Circle(x, y, cx + headRx * 0.38f, headY + headRy * 0.12f, n * 0.014f));
                    Over(ref r, ref g, ref b, ref a, 0x2A2420FFu, Cover(eyes));
                    a *= Cover(disc);
                    int at = (py * size + px) * 4;
                    rgba[at] = (byte)Math.Round(Math.Min(1f, r) * 255f);
                    rgba[at + 1] = (byte)Math.Round(Math.Min(1f, g) * 255f);
                    rgba[at + 2] = (byte)Math.Round(Math.Min(1f, b) * 255f);
                    rgba[at + 3] = (byte)Math.Round(Math.Min(1f, a) * 255f);
                }
            }
        }

        // A hairstyle over the head's top, as a signed distance (negative inside).
        private static float Hair(int style, float x, float y, float cx, float hy, float rx, float ry, float n)
        {
            float cap = Math.Max(Ellipse(x, y, cx, hy - ry * 0.08f, rx * 1.1f, ry * 1.0f), y - (hy - ry * 0.32f));   // the head's crown, cut at the brow
            switch (style)
            {
                case 1:   // long: the crown, and the sides framing the face
                    float sides = RoundBox(x, y, cx - rx * 1.18f, hy - ry * 0.4f, cx + rx * 1.18f, hy + ry * 0.7f, rx * 0.3f);
                    return Math.Min(cap, Math.Max(sides, -Ellipse(x, y, cx, hy + ry * 0.2f, rx * 0.86f, ry * 0.95f)));
                case 2:   // a bun on top
                    return Math.Min(cap, Circle(x, y, cx, hy - ry * 1.12f, n * 0.07f));
                case 3:   // curls round the crown
                    float c = cap;
                    for (int i = 0; i < 7; i++)
                    {
                        double t = Math.PI * (1.08 + i * 0.14);
                        c = Math.Min(c, Circle(x, y, cx + (float)Math.Cos(t) * rx * 1.02f, hy - ry * 0.12f + (float)Math.Sin(t) * ry * 0.92f, n * 0.05f));
                    }
                    return c;
                case 4:   // cropped close: a thinner crown
                    return Math.Max(Ellipse(x, y, cx, hy - ry * 0.12f, rx * 1.04f, ry * 0.94f), y - (hy - ry * 0.5f));
                case 5:   // a side part: the crown swept to one side
                    return Math.Min(cap, Math.Max(Ellipse(x, y, cx - rx * 0.35f, hy - ry * 0.45f, rx * 0.95f, ry * 0.42f), y - (hy - ry * 0.1f)));
                default:  // short
                    return cap;
            }
        }

        // Paints a colour over what is there by a coverage of 0 to 1.
        private static void Over(ref float r, ref float g, ref float b, ref float a, uint rgba, float cover)
        {
            if (cover <= 0f)
            {
                return;
            }
            float cr = ((rgba >> 24) & 0xFF) / 255f, cg = ((rgba >> 16) & 0xFF) / 255f, cb = ((rgba >> 8) & 0xFF) / 255f;
            r = r * (1f - cover) + cr * cover;
            g = g * (1f - cover) + cg * cover;
            b = b * (1f - cover) + cb * cover;
            a = a * (1f - cover) + cover;
        }

        private static uint Shade(uint rgba, float k) =>
            ((uint)(((rgba >> 24) & 0xFF) * k) << 24) | ((uint)(((rgba >> 16) & 0xFF) * k) << 16) | ((uint)(((rgba >> 8) & 0xFF) * k) << 8) | 0xFF;

        // A distance's coverage of a texel: soft over one texel, as the kit's other shapes are.
        private static float Cover(float d) => Math.Max(0f, Math.Min(1f, 0.5f - d));

        private static float Circle(float x, float y, float cx, float cy, float r) => (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        // An ellipse's distance, near enough at the edge for a soft texel: scaled to its smaller radius.
        private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return ((float)Math.Sqrt(dx * dx + dy * dy) - 1f) * Math.Min(rx, ry);
        }

        private static float RoundBox(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            float qx = Math.Abs(x - (x0 + x1) / 2f) - ((x1 - x0) / 2f - r), qy = Math.Abs(y - (y0 + y1) / 2f) - ((y1 - y0) / 2f - r);
            float ox = Math.Max(qx, 0f), oy = Math.Max(qy, 0f);
            return (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0f) - r;
        }
    }
}
