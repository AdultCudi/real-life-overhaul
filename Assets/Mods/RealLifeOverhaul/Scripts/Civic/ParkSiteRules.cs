using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A car park's works site on ground given to it (docs/features/construction.md, "Parking lots"), game-free. The land is kept as
    /// text in the site's street field so the save's line and the host's line of sites keep their shape ("park:24x37:090": width and
    /// depth in whole metres, then the land's turn in degrees, its front looking out along -Z turned by it, as a plot's does), and
    /// what stands on it in each stage of the works: the hoarding along the street, the dig, the base, the surface, the lines. Any
    /// size a car park can have is allowed, up to seven columns wide.
    /// </summary>
    public static class ParkSiteRules
    {
        public const string Prefix = "park:";
        public const int Least = 10;            // metres a side at the least
        public const int Most = 80;             // ... and at the most (seven columns of nine metres and the rows' depth are inside it)
        public const float Margin = 1.2f;       // m of the land kept clear of the works' base round its edge: the kerb's place
        public const float BaseHeight = 0.1f;   // m: the base course the surface is laid on
        public const float SurfaceHeight = 0.14f;   // m: the surface laid on the base in the last stages
        public const float GateWidth = 6.5f;    // m: the gate in the front hoarding, at the low-x end (the mouth's width)

        /// <summary>The land as the site's street field: "park:24x37:090".</summary>
        public static string Encode(int width, int depth, int yaw)
        {
            yaw = ((yaw % 360) + 360) % 360;
            return Prefix + width.ToString(CultureInfo.InvariantCulture) + "x" + depth.ToString(CultureInfo.InvariantCulture) + ":" + yaw.ToString("000", CultureInfo.InvariantCulture);
        }

        public static bool IsPark(string street) => street != null && street.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>The land back from the street field; false for anything that is not a car park's or whose figures are outside what a site can be.</summary>
        public static bool TryRead(string street, out int width, out int depth, out int yaw)
        {
            width = depth = yaw = 0;
            if (!IsPark(street))
            {
                return false;
            }
            string[] parts = street.Substring(Prefix.Length).Split(':');
            if (parts.Length != 2)
            {
                return false;
            }
            string[] size = parts[0].Split('x');
            if (size.Length != 2 || !Int(size[0], out width) || !Int(size[1], out depth) || !Int(parts[1], out yaw))
            {
                return false;
            }
            yaw = ((yaw % 360) + 360) % 360;
            return Unfit(width, depth) == null;
        }

        private static bool Int(string text, out int value) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        /// <summary>Why land of a size cannot take a car park's works, or null if it can.</summary>
        public static string Unfit(int width, int depth)
        {
            if (width < Least || depth < Least || width > Most || depth > Most)
            {
                return "the land is " + width + " by " + depth + " m; a car park's works need " + Least + " to " + Most + " m each way";
            }
            return null;
        }

        /// <summary>The hoarding along the street's side, panel by panel, leaving the gate at the low-x end (the way the cars come in).</summary>
        public static void HoardingSpots(int width, int depth, List<PlotSpot> into)
        {
            float z = -depth * 0.5f;
            for (float x = -width * 0.5f + GateWidth + PlotRules.Hoarding * 0.5f; x + PlotRules.Hoarding * 0.5f <= width * 0.5f + 0.01f; x += PlotRules.Hoarding)
            {
                into.Add(new PlotSpot(x, z, 180f));
            }
        }

        /// <summary>The works' footprint: the land less the margin, as boxes of the base and the surface.</summary>
        public static ShellBox Base(int width, int depth, float top)
        {
            float x = width * 0.5f - Margin, z = depth * 0.5f - Margin;
            return new ShellBox(-x, x, 0f, top, -z, z);
        }

        /// <summary>What stands in a stage: the dig (1), the base (2), the surface (3 and 4); the surface's share of it at a stage's progress (0 to 1).</summary>
        public static bool BaseLaid(int phase) => phase >= 2;

        public static bool SurfaceLaid(int phase) => phase >= 3;

        /// <summary>The land's footprint in the world's axes as a box, to the height of the cones and the lamps: for the view test.</summary>
        public static void Corners(float originX, float originZ, int yaw, int width, int depth, List<float> xs, List<float> zs)
        {
            xs.Clear();
            zs.Clear();
            for (int corner = 0; corner < 4; corner++)
            {
                PlotRules.ToWorld(originX, originZ, yaw, (corner % 2 == 0 ? -0.5f : 0.5f) * width, (corner < 2 ? -0.5f : 0.5f) * depth, out float x, out float z);
                xs.Add(x);
                zs.Add(z);
            }
        }
    }
}
