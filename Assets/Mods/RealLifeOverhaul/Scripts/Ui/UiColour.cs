using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A colour in sRGB, 0 to 1 with its alpha, and the sums the interface does with colours: a role's colour composited
    /// over the surface beneath it, WCAG's relative luminance and contrast ratio, and an avatar's tint from a hue
    /// (OKLCH, as the wireframe writes them). Game-free, so EconomyCheck measures the very colours the game draws.
    /// Cost: a few multiplications; nothing allocated.
    /// </summary>
    public struct UiColour
    {
        public float R, G, B, A;

        public UiColour(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>From 0xRRGGBBAA, as the token tables hold colours.</summary>
        public static UiColour FromRgba(uint rgba)
        {
            return new UiColour(((rgba >> 24) & 0xFF) / 255f, ((rgba >> 16) & 0xFF) / 255f, ((rgba >> 8) & 0xFF) / 255f, (rgba & 0xFF) / 255f);
        }

        /// <summary>A role's colour in a theme.</summary>
        public static UiColour Of(UiRole role, bool dark) => FromRgba(UiTokens.Colour(role, dark));

        /// <summary>This colour laid over an opaque one, as the screen shows it; the result is opaque.</summary>
        public UiColour Over(UiColour below)
        {
            float a = A;
            return new UiColour(R * a + below.R * (1f - a), G * a + below.G * (1f - a), B * a + below.B * (1f - a), 1f);
        }

        /// <summary>The same colour with another alpha.</summary>
        public UiColour WithAlpha(float alpha) => new UiColour(R, G, B, alpha);

        /// <summary>WCAG 2's relative luminance of an opaque colour.</summary>
        public float Luminance() => 0.2126f * Linear(R) + 0.7152f * Linear(G) + 0.0722f * Linear(B);

        /// <summary>WCAG 2's contrast ratio of two opaque colours: 1 to 21.</summary>
        public static float Contrast(UiColour a, UiColour b)
        {
            float la = a.Luminance(), lb = b.Luminance();
            return la > lb ? (la + 0.05f) / (lb + 0.05f) : (lb + 0.05f) / (la + 0.05f);
        }

        /// <summary>The colour of an OKLCH lightness, chroma and hue (degrees), clipped into sRGB.</summary>
        public static UiColour FromOklch(float lightness, float chroma, float hue)
        {
            double h = hue * Math.PI / 180.0;
            double a = chroma * Math.Cos(h), b = chroma * Math.Sin(h);
            double l = lightness + 0.3963377774 * a + 0.2158037573 * b;
            double m = lightness - 0.1055613458 * a - 0.0638541728 * b;
            double s = lightness - 0.0894841775 * a - 1.2914855480 * b;
            l = l * l * l;
            m = m * m * m;
            s = s * s * s;
            double r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
            double g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
            double bl = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
            return new UiColour(Encode(r), Encode(g), Encode(bl), 1f);
        }

        /// <summary>An initials avatar's circle for a hue in a theme.</summary>
        public static UiColour AvatarFill(float hue, bool dark)
        {
            return dark ? FromOklch(UiTokens.AvatarDarkL, UiTokens.AvatarDarkC, hue) : FromOklch(UiTokens.AvatarLightL, UiTokens.AvatarLightC, hue);
        }

        /// <summary>An initials avatar's letters for a hue in a theme.</summary>
        public static UiColour AvatarInk(float hue, bool dark)
        {
            return dark ? FromOklch(UiTokens.AvatarDarkInkL, UiTokens.AvatarDarkInkC, hue) : FromOklch(UiTokens.AvatarLightInkL, UiTokens.AvatarLightInkC, hue);
        }

        /// <summary>A group's dot for a hue in a theme: the wireframe's oklch(0.7 0.12 h), deeper on light.</summary>
        public static UiColour HueDot(float hue, bool dark) => dark ? FromOklch(0.7f, 0.12f, hue) : FromOklch(0.58f, 0.14f, hue);

        /// <summary>A stable hue for a name, so a contact keeps its colour: degrees, 0 to 359.</summary>
        public static float HueOf(string key)
        {
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; key != null && i < key.Length; i++)
                {
                    h = (h ^ key[i]) * 16777619;
                }
                return h % 360;
            }
        }

        private static float Linear(float c) => c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055) / 1.055, 2.4);

        private static float Encode(double c)
        {
            c = c < 0 ? 0 : c > 1 ? 1 : c;
            return (float)(c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055);
        }
    }
}
