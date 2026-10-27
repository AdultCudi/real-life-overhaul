using System;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>The Economy app's colours, as 0xRRGGBB, for one look. Game-free; every text colour on the surface it is set on holds WCAG AA contrast (EconomyCheck's OutlookChecks).</summary>
    public sealed class OutlookPalette
    {
        public string Name;
        public bool Dark;
        public int Back, Card, Raised, Border, Grid;                // surfaces and lines
        public int Text, Muted;                                     // lettering: the main, and the quiet
        public int Accent, AccentInk;                               // the one bright colour, and the lettering on a fill of it
        public int Good, Bad, Warn, Info;                           // news
        public double SoftAlpha;                                    // how much of a news colour a pill's fill takes of the card under it
        public double ShadowAlpha;                                  // the card's shadow

        public int GoodSoft => OutlookTheme.Blend(Good, Card, SoftAlpha);
        public int BadSoft => OutlookTheme.Blend(Bad, Card, SoftAlpha);
        public int WarnSoft => OutlookTheme.Blend(Warn, Card, SoftAlpha);
        public int InfoSoft => OutlookTheme.Blend(Info, Card, SoftAlpha);
        public int AccentSoft => OutlookTheme.Blend(Accent, Card, SoftAlpha);
        public int Flat => Raised;   // a pill for a change of nothing
    }

    /// <summary>One lettering on one surface: what it is, the two colours.</summary>
    public struct OutlookPair
    {
        public string Name;
        public int Ink, Surface;
    }

    /// <summary>The two looks of the Economy app (the option "Look of the Economy app": dark to begin with) and the contrast arithmetic that holds them to the standard.</summary>
    public static class OutlookTheme
    {
        public const int DarkLook = 0, LightLook = 1;
        public const double Least = 4.5;       // WCAG AA for text
        public const double LeastShape = 3.0;  // ...for a part of a picture (a line, a bar) against what it is drawn on

        public static readonly OutlookPalette Dark = new OutlookPalette
        {
            Name = "dark", Dark = true,
            Back = 0x0F1420, Card = 0x171E2E, Raised = 0x1F2940, Border = 0x2A3550, Grid = 0x273049,
            Text = 0xF2F5FA, Muted = 0x8A94A7, Accent = 0x2DD4BF, AccentInk = 0x04211E,
            Good = 0x2ECC71, Bad = 0xFF5C5C, Warn = 0xFFB020, Info = 0x5AA9FF,
            SoftAlpha = 0.14, ShadowAlpha = 0.35
        };

        public static readonly OutlookPalette Light = new OutlookPalette
        {
            Name = "light", Dark = false,
            Back = 0xF3F5F9, Card = 0xFFFFFF, Raised = 0xEBEFF5, Border = 0xD9DFE8, Grid = 0xE3E8F0,
            Text = 0x111827, Muted = 0x586275, Accent = 0x0A7468, AccentInk = 0xFFFFFF,
            Good = 0x166A3A, Bad = 0xBA2222, Warn = 0x8C5006, Info = 0x1856B8,
            SoftAlpha = 0.10, ShadowAlpha = 0.10
        };

        /// <summary>The palette of an option's choice: dark for 0 and anything unknown, light for 1.</summary>
        public static OutlookPalette Of(int look)
        {
            return look == LightLook ? Light : Dark;
        }

        // ------------------------------------------------------------------ arithmetic

        /// <summary>A colour laid over another at some opacity, opaque.</summary>
        public static int Blend(int over, int under, double alpha)
        {
            alpha = Math.Max(0.0, Math.Min(1.0, alpha));
            int r = Mix((over >> 16) & 255, (under >> 16) & 255, alpha);
            int g = Mix((over >> 8) & 255, (under >> 8) & 255, alpha);
            int b = Mix(over & 255, under & 255, alpha);
            return (r << 16) | (g << 8) | b;
        }

        private static int Mix(int a, int b, double alpha)
        {
            return (int)Math.Round(a * alpha + b * (1.0 - alpha));
        }

        private static double Linear(int channel)
        {
            double c = channel / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        /// <summary>The relative luminance of a colour (WCAG 2.x).</summary>
        public static double Luminance(int rgb)
        {
            return 0.2126 * Linear((rgb >> 16) & 255) + 0.7152 * Linear((rgb >> 8) & 255) + 0.0722 * Linear(rgb & 255);
        }

        /// <summary>The contrast ratio of two colours, 1 to 21.</summary>
        public static double Contrast(int a, int b)
        {
            double la = Luminance(a), lb = Luminance(b);
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        /// <summary>
        /// A colour made visible against a surface: itself if it is already <see cref="LeastShape"/> against it, else moved
        /// towards white (on a dark surface) or black a step at a time until it is. For the district colours of the city map,
        /// which the game chose for a map, not for this card.
        /// </summary>
        public static int Visible(int colour, int surface, bool dark)
        {
            int target = dark ? 0xFFFFFF : 0x000000;
            for (int step = 0; step < 10 && Contrast(colour, surface) < LeastShape; step++)
            {
                colour = Blend(target, colour, 0.12);
            }
            return colour;
        }

        /// <summary>
        /// A fill that lettering can stand on: itself, or darkened a step at a time, until near-black or white lettering on it holds
        /// <see cref="Least"/>; the lettering in <paramref name="ink"/>. For a label on a line's colour or a district's, which
        /// can be any colour at all.
        /// </summary>
        public static int Legible(int fill, out int ink)
        {
            const int Dark = 0x0B1220, White = 0xFFFFFF;
            for (int step = 0; step < 14; step++)
            {
                if (Contrast(Dark, fill) >= Least)
                {
                    ink = Dark;
                    return fill;
                }
                if (Contrast(White, fill) >= Least)
                {
                    ink = White;
                    return fill;
                }
                fill = Blend(0x000000, fill, 0.10);
            }
            ink = White;
            return fill;
        }

        // ------------------------------------------------------------------ the pairs

        /// <summary>Every lettering the app sets, on the surface it is set on, in a palette.</summary>
        public static OutlookPair[] Pairs(OutlookPalette p)
        {
            return new[]
            {
                Pair("text on the page", p.Text, p.Back), Pair("text on a card", p.Text, p.Card), Pair("text on a raised part", p.Text, p.Raised),
                Pair("quiet text on the page", p.Muted, p.Back), Pair("quiet text on a card", p.Muted, p.Card), Pair("quiet text on a raised part", p.Muted, p.Raised),
                Pair("accent on the page", p.Accent, p.Back), Pair("accent on a card", p.Accent, p.Card),
                Pair("lettering on an accent fill", p.AccentInk, p.Accent),
                Pair("good on a card", p.Good, p.Card), Pair("good on its pill", p.Good, p.GoodSoft),
                Pair("bad on a card", p.Bad, p.Card), Pair("bad on its pill", p.Bad, p.BadSoft),
                Pair("warning on a card", p.Warn, p.Card), Pair("warning on its pill", p.Warn, p.WarnSoft),
                Pair("information on a card", p.Info, p.Card), Pair("information on its pill", p.Info, p.InfoSoft),
                Pair("accent on its pill", p.Accent, p.AccentSoft), Pair("quiet text on its pill", p.Muted, p.Flat),
            };
        }

        private static OutlookPair Pair(string name, int ink, int surface)
        {
            return new OutlookPair { Name = name, Ink = ink, Surface = surface };
        }

        /// <summary>The weakest pair of a palette and its contrast.</summary>
        public static double Weakest(OutlookPalette p, out string name)
        {
            double worst = double.MaxValue;
            name = null;
            foreach (OutlookPair pair in Pairs(p))
            {
                double c = Contrast(pair.Ink, pair.Surface);
                if (c < worst)
                {
                    worst = c;
                    name = pair.Name;
                }
            }
            return worst;
        }
    }
}
