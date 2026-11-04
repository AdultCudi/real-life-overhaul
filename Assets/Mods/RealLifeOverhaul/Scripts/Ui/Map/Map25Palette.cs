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
    /// ground, streets a little lighter, five wall tones, the five tints, the light from the west and a little from the north.
    /// Nothing is pure white and no large area is light. Game-free.
    /// </summary>
    public static class Map25Palette
    {
        public static readonly Map25Colour Water = Map25Colour.Hex(0x0F2233);
        public static readonly Map25Colour Block = Map25Colour.Hex(0x1D2B38);
        public static readonly Map25Colour Avenue = Map25Colour.Hex(0x2F4357);
        public static readonly Map25Colour Street = Map25Colour.Hex(0x2A3D50);
        public static readonly Map25Colour Window = Map25Colour.Hex(0xA9C0D3);
        public static readonly Map25Colour Lit = Map25Colour.Hex(0xF6D98A);
        public static readonly Map25Colour Ink = Map25Colour.Hex(0x050B11);
        public static readonly Map25Colour Route = Map25Colour.Hex(0x4FB8EA);
        public static readonly Map25Colour RouteCasing = Map25Colour.Hex(0x0B3C58);
        public static readonly Map25Colour Rooftop = Map25Colour.Hex(0x6F7D8A);
        public static readonly Map25Colour Tank = Map25Colour.Hex(0x5A4A3E);
        public static readonly Map25Colour TankTop = Map25Colour.Hex(0x76644F);
        public static readonly Map25Colour Leg = Map25Colour.Hex(0x2C3238);

        public static readonly Map25Colour[] Tones =
        {
            Map25Colour.Hex(0x40566A), Map25Colour.Hex(0x4A5C6E), Map25Colour.Hex(0x665F56), Map25Colour.Hex(0x5C4F4B), Map25Colour.Hex(0x4D625B)
        };

        public static readonly Map25Colour[] Awnings =
        {
            Map25Colour.Hex(0xC0614F), Map25Colour.Hex(0xD1993F), Map25Colour.Hex(0x3E9C8A), Map25Colour.Hex(0x7A6BC4), Map25Colour.Hex(0x4A86B8), Map25Colour.Hex(0x79A04E)
        };

        /// <summary>The share of a tint that reaches a building the game has highlighted (mockups: 0.5 to 0.8 by kind; one for every kind the game has).</summary>
        public const float TintShare = 0.68f;

        /// <summary>The same for the white of the game's hover and picked outline.</summary>
        public const float HoverShare = 0.40f;

        /// <summary>How light a wall is by the way it faces (the outward normal in the plan, x east, z north): west walls lightest, east darkest.</summary>
        public static float WallFactor(float nx, float nz)
        {
            float l = nx * -0.70f + nz * 0.55f;
            return 0.72f + 0.20f * l;
        }

        /// <summary>The darkening at the foot of a wall, as a share of its colour: 0.55 at the ground, 0.14 at 42 per cent of its height, none at the top.</summary>
        public static float Foot(float t)
        {
            if (t <= 0f)
            {
                return 0.55f;
            }
            if (t < 0.42f)
            {
                return 0.55f + (0.14f - 0.55f) * (t / 0.42f);
            }
            if (t < 1f)
            {
                return 0.14f * (1f - (t - 0.42f) / 0.58f);
            }
            return 0f;
        }

        /// <summary>One of the five wall tones, by a number.</summary>
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
