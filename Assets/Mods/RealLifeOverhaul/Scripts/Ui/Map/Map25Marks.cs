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
        public const float RouteFull = 7f;                 // pixels of the route's line on a full map 1080 pixels high (the mockup's 7.5)
        public const float RouteMini = 4.2f;               // on a minimap 300 pixels wide (the mockup's)
        public const float CasingShare = 1.75f;            // the dark casing is this wide, times the line
        public const float ConeFull = 142f / 1080f;        // the view cone's length, of the full map's height (the mockup's 142 pixels in 1080)
        public const float ConeMini = 55f / 300f;          // of the minimap's width
        public const float HaloFull = 30f / 1080f;         // the halo's radius
        public const float HaloMini = 11.6f / 300f;
        public const float ConeAlpha = 0.17f, HaloAlpha = 0.30f, ConeHalfAngle = 30f;

        /// <summary>
        /// Half the width in metres of the route's line where a pixel spans <paramref name="metresPerPixel"/> m, on a picture
        /// <paramref name="pixels"/> high (the full map) or wide (the minimap).
        /// </summary>
        public static float RouteHalf(bool minimap, float pixels, float metresPerPixel)
        {
            float line = minimap ? RouteMini * pixels / 300f : RouteFull * pixels / 1080f;
            return Math.Max(0.25f, line * 0.5f) * metresPerPixel;
        }

        /// <summary>The view cone's length in metres.</summary>
        public static float ConeMetres(bool minimap, float pixels, float metresPerPixel)
        {
            return (minimap ? ConeMini : ConeFull) * pixels * metresPerPixel;
        }

        /// <summary>The halo's radius in metres.</summary>
        public static float HaloMetres(bool minimap, float pixels, float metresPerPixel)
        {
            return (minimap ? HaloMini : HaloFull) * pixels * metresPerPixel;
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
