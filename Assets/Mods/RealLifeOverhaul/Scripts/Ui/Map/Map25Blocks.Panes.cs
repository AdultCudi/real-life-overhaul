using System;

namespace RealLifeOverhaul.Map25
{
    // The windows and awnings of the full detail (make_map25d.py: a dash of window every 4.4 m on every floor, awnings on the north and
    // south walls of shops). A window is a translucent pane laid 8 cm out of its wall: the same colour whatever tints the wall, so
    // a tint never has to write them, and a few lit ones. A tall or long building would have more than PaneCap windows (four vertices
    // each), so its windows are larger in step, as many times as it takes (PaneScale): the same look at the same density, fewer quads.
    public sealed partial class Map25Blocks
    {
        private const float PaneWidth = 1.75f;       // metres of window in each 4.4
        private const float PanePitch = 4.4f;
        private const float PaneMargin = 1.9f;       // from either end of a wall
        private const float PaneHeight = 1.3f;
        private const float PaneFloor = 3.1f;        // metres from one floor's windows to the next's
        private const float PaneOut = 0.08f;         // out of the wall, far enough for the depth buffer to keep the pane in front
        private const float AwningOut = 1.25f;       // how far an awning stands out of its wall
        public const int PaneCap = 260;              // windows of one building at the most before they are drawn larger
        public const float PaneLargest = 2.6f;       // and the most they are drawn larger by

        /// <summary>The windows in a row of a wall of this length (0 for a wall too short to have any), the windows drawn <paramref name="scale"/> times their size.</summary>
        public static int PaneColumns(float length, float scale = 1f)
        {
            if (length < 8f)
            {
                return 0;
            }
            return Math.Max(0, (int)((length - 2f * PaneMargin + (PanePitch - PaneWidth) * scale) / (PanePitch * scale)));
        }

        /// <summary>The floors of windows a wall of this height has (a shop's start above its ground floor), the windows drawn <paramref name="scale"/> times their size.</summary>
        public static int PaneFloors(float height, bool shop, float scale = 1f)
        {
            if (height < 9f)
            {
                return 0;
            }
            float limit = height - (height >= 14f ? 1.9f : 1.0f);
            float bottom = shop ? 4.0f : 1.3f;
            float room = limit - bottom - PaneHeight * scale;
            return room < 0f ? 0 : (int)(room / (PaneFloor * scale)) + 1;
        }

        // How many times larger the building's windows are drawn so that there are no more than PaneCap: 1 when all fit.
        private float PaneScale(int corners, float height)
        {
            for (float scale = 1f; scale < PaneLargest; scale += 0.2f)
            {
                int floors = PaneFloors(height, _shop, scale);
                int windows = 0;
                for (int e = 0; e < corners; e++)
                {
                    int f = (e + 1) % corners;
                    float dx = _x[f] - _x[e], dz = _z[f] - _z[e];
                    windows += PaneColumns((float)Math.Sqrt(dx * dx + dz * dz), scale) * floors;
                }
                if (windows <= PaneCap)
                {
                    return scale;
                }
            }
            return PaneLargest;
        }

        private void Panes(Map25Builder p, float ax, float az, float ux, float uz, float nx, float nz, float length, float y0, float h, Map25Colour wall, int edge)
        {
            float s = _paneScale;
            int columns = PaneColumns(length, s), floors = PaneFloors(h, _shop, s);
            if (columns < 1 || floors < 1)
            {
                return;
            }
            float width = PaneWidth * s, pitch = PanePitch * s, tall = PaneHeight * s, step = PaneFloor * s;
            float span = columns * width + (columns - 1) * (pitch - width);
            float first = (length - span) * 0.5f;
            float bottom = _shop ? 4.0f : 1.3f;
            Map25Colour glass = wall.Mix(Map25Palette.Window, Map25Palette.PaneShare);
            float ox = nx * PaneOut, oz = nz * PaneOut;
            for (int f = 0; f < floors; f++)
            {
                float zb = bottom + f * step, zt = zb + tall;
                for (int k = 0; k < columns; k++)
                {
                    float s0 = first + k * pitch, s1 = s0 + width;
                    p.Flat(ax + ux * s0 + ox, y0 + zb, az + uz * s0 + oz, ax + ux * s1 + ox, y0 + zb, az + uz * s1 + oz,
                           ax + ux * s1 + ox, y0 + zt, az + uz * s1 + oz, ax + ux * s0 + ox, y0 + zt, az + uz * s0 + oz, glass, Map25Palette.PaneAlpha);
                }
            }
            if (h < 19f || length <= 15f)
            {
                return;
            }
            for (int k = 0; k < 4; k++)
            {
                if (_lit[k] != edge)
                {
                    continue;
                }
                float zb = bottom + (_litFloor[k] % floors) * step, zt = zb + tall;
                float s0 = PaneMargin + _litAt[k] * (length - 2f * PaneMargin - width), s1 = s0 + width;
                float lx = nx * (PaneOut + 0.03f), lz = nz * (PaneOut + 0.03f);
                p.Flat(ax + ux * s0 + lx, y0 + zb, az + uz * s0 + lz, ax + ux * s1 + lx, y0 + zb, az + uz * s1 + lz,
                       ax + ux * s1 + lx, y0 + zt, az + uz * s1 + lz, ax + ux * s0 + lx, y0 + zt, az + uz * s0 + lz, Map25Palette.Lit, Map25Palette.LitAlpha);
            }
        }

        // A shop's awnings: a slanted strip out of the wall at the top of the shop front, in the business's colour (lifted towards the tint by the building's tint share, as its walls
        // are: the game's hover is white at 0.28, so a hovered shop's awning is its own colour a little lighter, never white), with a dark valance hanging from its edge.
        private static void Awnings(Map25Builder f, float ax, float az, float ux, float uz, float nx, float nz, float length, float y0, Map25Colour tint, float share, Map25Colour plain,
                                    float factor, ref Map25Rng rng)
        {
            int segments = Math.Max(1, (int)(length / 12f));
            float seg = length / segments;
            for (int s = 0; s < segments; s++)
            {
                int pick = rng.Int(Map25Palette.Awnings.Length);
                Map25Colour a = Map25Palette.Awnings[pick].Mix(plain, 0.30f);
                if (share > 0f)
                {
                    a = Map25Palette.Tinted(a, tint, share);
                }
                a = a.Scale(0.80f + 0.35f * factor);
                float s0 = s * seg + 1.4f, s1 = (s + 1) * seg - 1.4f;
                float x0 = ax + ux * s0, z0 = az + uz * s0, x1 = ax + ux * s1, z1 = az + uz * s1;
                float ox = nx * AwningOut, oz = nz * AwningOut;
                f.Flat(x0, y0 + 3.25f, z0, x1, y0 + 3.25f, z1, x1 + ox, y0 + 2.25f, z1 + oz, x0 + ox, y0 + 2.25f, z0 + oz, a, 1f);
                Map25Colour dark = a.Mix(Map25Palette.Ink, 0.55f);
                f.Strip(x0 + ox, z0 + oz, x1 + ox, z1 + oz, y0 + 1.97f, y0 + 2.25f, dark, dark);
            }
        }
    }
}
