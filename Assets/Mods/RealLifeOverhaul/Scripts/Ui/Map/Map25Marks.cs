using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// The sizes of the lines and marks the player's own things are drawn with, as the approved mockups have them: a few pixels of
    /// route, a hint of a view cone at the player's feet, whatever the zoom. They are pixels of the picture, turned into metres at the
    /// place they lie by the metres a pixel is there, so a route is no wider than a few pixels on a road fifty pixels wide, and does not swell with the zoom
    /// (the first build drew them in metres or as a share of the view: a route twice the width of the road's quarter, a cone 650 pixels long).
    /// Game-free; EconomyCheck pins every number.
    /// </summary>
    public static class Map25Marks
    {
        public const float RouteMini = 4.2f;               // pixels of the route's line on a minimap 300 pixels wide (the mockup's)
        public const float CasingShare = 1.75f;            // the dark casing is this wide, times the line
        public const float ConeMini = 55f / 300f;          // the view cone's length, of the minimap's width
        public const float HaloMini = 11.6f / 300f;        // the halo's radius
        public const float ConeHalfAngle = 30f;

        /// <summary>
        /// What the eye gets of the view cone and the halo over an avenue, as shares of the route's colour: the mockups' own, 0.17 and 0.30 (their CSS blends the colours as they are
        /// written). The picture is blended in linear light (its texture is sRGB), where the same opacities would show 0.28 and 0.44, near twice as strong, so the opacities it
        /// blends with are worked out from these shares (<see cref="AlphaFor"/>), not taken as the mockups' numbers.
        /// </summary>
        public const float ConeShare = 0.17f, HaloShare = 0.30f;

        /// <summary>The opacities the cone and the halo are drawn with.</summary>
        public static readonly float ConeAlpha = AlphaFor(ConeShare), HaloAlpha = AlphaFor(HaloShare);

        /// <summary>
        /// The share of <paramref name="mark"/> that a mark of opacity <paramref name="alpha"/> shows over <paramref name="ground"/> in the colours the eye gets (the picture blends
        /// in linear light and is seen as written): the part of the change that lies along the mark's direction, the three channels together. Colours as written (sRGB, 0 to 1).
        /// </summary>
        public static float Seen(float alpha, Map25Colour ground, Map25Colour mark)
        {
            float along = 0f, whole = 0f;
            for (int c = 0; c < 3; c++)
            {
                float g = c == 0 ? ground.R : c == 1 ? ground.G : ground.B, m = c == 0 ? mark.R : c == 1 ? mark.G : mark.B;
                float lit = Map25Math.Encode(Map25Math.Linear(g) + alpha * (Map25Math.Linear(m) - Map25Math.Linear(g)));
                along += (lit - g) * (m - g);
                whole += (m - g) * (m - g);
            }
            return whole > 0f ? along / whole : 0f;
        }

        /// <summary>The colour the eye gets of a mark of opacity <paramref name="alpha"/> over a ground, as 8 bit numbers (what the preview's pixels hold).</summary>
        public static void SeenRgb(float alpha, Map25Colour ground, Map25Colour mark, out int r, out int g, out int b)
        {
            r = (int)(Map25Math.Encode(Map25Math.Linear(ground.R) + alpha * (Map25Math.Linear(mark.R) - Map25Math.Linear(ground.R))) * 255f + 0.5f);
            g = (int)(Map25Math.Encode(Map25Math.Linear(ground.G) + alpha * (Map25Math.Linear(mark.G) - Map25Math.Linear(ground.G))) * 255f + 0.5f);
            b = (int)(Map25Math.Encode(Map25Math.Linear(ground.B) + alpha * (Map25Math.Linear(mark.B) - Map25Math.Linear(ground.B))) * 255f + 0.5f);
        }

        /// <summary>The opacity that shows <paramref name="share"/> of the route's colour over an avenue (by halving: the share rises with the opacity).</summary>
        public static float AlphaFor(float share)
        {
            float low = 0f, high = 1f;
            for (int i = 0; i < 32; i++)
            {
                float middle = (low + high) * 0.5f;
                if (Seen(middle, Map25Palette.Avenue, Map25Palette.Route) < share)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            return (low + high) * 0.5f;
        }

        /// <summary>
        /// Half the width in metres of the route's line where a pixel spans <paramref name="metresPerPixel"/> m, on a minimap
        /// <paramref name="pixels"/> wide.
        /// </summary>
        public static float RouteHalf(float pixels, float metresPerPixel)
        {
            float line = RouteMini * pixels / 300f;
            return Math.Max(0.25f, line * 0.5f) * metresPerPixel;
        }

        /// <summary>The view cone's length in metres.</summary>
        public static float ConeMetres(float pixels, float metresPerPixel)
        {
            return ConeMini * pixels * metresPerPixel;
        }

        /// <summary>The halo's radius in metres.</summary>
        public static float HaloMetres(float pixels, float metresPerPixel)
        {
            return HaloMini * pixels * metresPerPixel;
        }

        /// <summary>
        /// The size in metres a mark is worth being drawn again at: the route's half width changes by steps of an eighth, so a zoom that moves
        /// it by a hair does not write the route's meshes again.
        /// </summary>
        public static float Step(float half)
        {
            if (half <= 0f)
            {
                return 0f;
            }
            double steps = Math.Round(Math.Log(half) / Math.Log(1.125));
            return (float)Math.Pow(1.125, steps);
        }
    }
}
