using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>A colour as it is written (sRGB, each channel 0 to 1); the shading is worked out in this space, as the approved mockups are, and turned to light when it is written.</summary>
    public struct Map25Colour
    {
        public float R, G, B;

        public Map25Colour(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static Map25Colour Hex(int rgb)
        {
            return new Map25Colour(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }

        /// <summary>Each channel times <paramref name="f"/>, held to 0 to 1 (the mockups' "lit": towards white a little, never past it).</summary>
        public Map25Colour Scale(float f)
        {
            return new Map25Colour(Clamp(R * f), Clamp(G * f), Clamp(B * f));
        }

        public Map25Colour Mix(Map25Colour other, float t)
        {
            return new Map25Colour(R + (other.R - R) * t, G + (other.G - G) * t, B + (other.B - B) * t);
        }

        private static float Clamp(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }

    /// <summary>
    /// The map's colours and the shading rules of its blocks, from the approved mockups (mockups/map/make_map25d.py): cool slate
    /// ground, streets a little lighter, five slate wall tones, the five tints, the light from the west and a little from the north.
    /// Nothing is pure white and no large area is light. Game-free.
    /// </summary>
    public static class Map25Palette
    {
        public static readonly Map25Colour Water = Map25Colour.Hex(0x0F2233);
        public static readonly Map25Colour Block = Map25Colour.Hex(0x2A363E);
        public static readonly Map25Colour Park = Map25Colour.Hex(0x1D3A35);
        public static readonly Map25Colour Avenue = Map25Colour.Hex(0x1B242B);
        public static readonly Map25Colour Street = Map25Colour.Hex(0x172027);
        public static readonly Map25Colour Window = Map25Colour.Hex(0xA9C0D3);
        public static readonly Map25Colour Lit = Map25Colour.Hex(0xF6D98A);
        public static readonly Map25Colour Ink = Map25Colour.Hex(0x050B11);
        public static readonly Map25Colour Route = Map25Colour.Hex(0x4FB8EA);
        public static readonly Map25Colour RouteCasing = Map25Colour.Hex(0x0B3C58);
        public static readonly Map25Colour Rooftop = Map25Colour.Hex(0x6F7D8A);
        public static readonly Map25Colour Tank = Map25Colour.Hex(0x5A4A3E);
        public static readonly Map25Colour TankTop = Map25Colour.Hex(0x76644F);
        public static readonly Map25Colour TankCone = Map25Colour.Hex(0x675645);
        public static readonly Map25Colour Leg = Map25Colour.Hex(0x2C3238);
        public static readonly Map25Colour TreeBase = Map25Colour.Hex(0x35574D);
        public static readonly Map25Colour TreeTop = Map25Colour.Hex(0x46705F);

        public static readonly Map25Colour[] Tones =
        {
            Map25Colour.Hex(0x6A7C88), Map25Colour.Hex(0x62747F), Map25Colour.Hex(0x74858F), Map25Colour.Hex(0x5A6B77), Map25Colour.Hex(0x66787E)
        };

        public static readonly Map25Colour[] Awnings =
        {
            Map25Colour.Hex(0xC0614F), Map25Colour.Hex(0xD1993F), Map25Colour.Hex(0x3E9C8A), Map25Colour.Hex(0x7A6BC4), Map25Colour.Hex(0x4A86B8), Map25Colour.Hex(0x79A04E)
        };

        /// <summary>The share of a tint that reaches a building the game has highlighted (mockups: 0.5 to 0.8 by kind; one for every kind the game has).</summary>
        public const float TintShare = 0.68f;

        /// <summary>The same for the white of the game's hover and picked outline.</summary>
        public const float HoverShare = 0.28f;

        /// <summary>
        /// How much of a window pane is the window colour, and how much of the wall under it shows through (the mockup's dashes: 24 per cent
        /// opacity of a colour 70 per cent window).
        /// </summary>
        public const float PaneAlpha = 0.24f, PaneShare = 0.70f, LitAlpha = 0.75f;

        /// <summary>The ground ring round a tinted building: the tint over the ground at this opacity, and its outline at the other.</summary>
        public const float RingFill = 0.20f, RingLine = 0.62f;

        /// <summary>The distance in metres from the ground at which the foot of a wall has lost its darkening (see <see cref="Foot"/>).</summary>
        public const float FootKnee = 12f, FootEnd = 28f;

        /// <summary>How light a wall is by the way it faces (the outward normal in the plan, x east, z north): west walls lightest, east darkest.</summary>
        public static float WallFactor(float nx, float nz)
        {
            float l = nx * -0.70f + nz * 0.55f;
            return 0.80f + 0.20f * l;
        }

        /// <summary>
        /// The darkening at the foot of a wall, as a share of its colour, by the metres above the ground it is at: 0.55 at the ground, 0.14 at 12 m, none from 28 m.
        /// (The mockups' gradient is a share of a 28 m wall; in metres a tall wall's foot is no taller than a low one's.)
        /// </summary>
        public static float Foot(float metres)
        {
            if (metres <= 0f)
            {
                return 0.55f;
            }
            if (metres < FootKnee)
            {
                return 0.55f + (0.14f - 0.55f) * (metres / FootKnee);
            }
            if (metres < FootEnd)
            {
                return 0.14f * (1f - (metres - FootKnee) / (FootEnd - FootKnee));
            }
            return 0f;
        }

        /// <summary>One of the five slate wall tones, by a number.</summary>
        public static Map25Colour Tone(int index)
        {
            return Tones[(index & 0x7FFFFFFF) % Tones.Length];
        }

        /// <summary>The colour a game tint has on a block (a mix, never the raw colour: nothing glares).</summary>
        public static Map25Colour Tinted(Map25Colour tone, Map25Colour tint, float share)
        {
            return tone.Mix(tint, share);
        }
    }
}
