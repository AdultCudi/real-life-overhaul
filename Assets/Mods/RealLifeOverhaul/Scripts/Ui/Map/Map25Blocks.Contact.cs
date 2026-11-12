using System;

namespace RealLifeOverhaul.Map25
{
    // The patch of dark ground under a block, crisp and translucent (the kit's rule: no soft edge).
    public sealed partial class Map25Blocks
    {
        /// <summary>
        /// The contact patch of building <paramref name="i"/> on the ground: its foot grown a little and swept to the east and south,
        /// in two layers (a wide faint one and a tight stronger one). Written as translucent flat polygons at <paramref name="groundY"/>.
        /// </summary>
        public void Contact(Map25Builder b, Map25City city, int i, float groundY)
        {
            int start = city.HullStart[i];
            int n = city.HullStart[i + 1] - start;
            if (n < 3 || n > Most)
            {
                return;
            }
            float h = Math.Max(Map25Height.Least, city.Height[i]);
            float reach = 0.55f * Math.Min(h, 40f);
            for (int k = 0; k < n; k++)
            {
                _x[k] = city.HullX[start + k];
                _z[k] = city.HullZ[start + k];
            }
            Layer(b, n, 1.6f, reach * 1.0f, groundY, 0.14f);
            Layer(b, n, 0.4f, reach * 0.8f, groundY, 0.20f);
        }

        private void Layer(Map25Builder b, int n, float grow, float reach, float y, float alpha)
        {
            Inset(_x, _z, n, -grow, _qx, _qz);
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                _px[m] = _qx[k];
                _pz[m] = _qz[k];
                m++;
                _px[m] = _qx[k] + 0.78f * reach;
                _pz[m] = _qz[k] - 0.62f * reach;
                m++;
            }
            int hull = Hull(_px, _pz, m);
            if (hull < 3)
            {
                return;
            }
            Map25Colour ink = Map25Palette.Ink;
            int first = b.Vertex(_hx[0], y, _hz[0], ink, alpha);
            int prev = b.Vertex(_hx[1], y, _hz[1], ink, alpha);
            for (int k = 2; k < hull; k++)
            {
                int next = b.Vertex(_hx[k], y, _hz[k], ink, alpha);
                b.Triangle(first, prev, next);
                prev = next;
            }
        }

        // The convex hull of the points, anticlockwise, into _hx and _hz; its corner count.
        private int Hull(float[] px, float[] pz, int count)
        {
            for (int i = 1; i < count; i++)
            {
                float kx = px[i], kz = pz[i];
                int j = i - 1;
                while (j >= 0 && (px[j] > kx || (px[j] == kx && pz[j] > kz)))
                {
                    px[j + 1] = px[j];
                    pz[j + 1] = pz[j];
                    j--;
                }
                px[j + 1] = kx;
                pz[j + 1] = kz;
            }
            int m = 0;
            for (int i = 0; i < count; i++)
            {
                while (m >= 2 && Cross(_hx[m - 2], _hz[m - 2], _hx[m - 1], _hz[m - 1], px[i], pz[i]) <= 0f)
                {
                    m--;
                }
                _hx[m] = px[i];
                _hz[m] = pz[i];
                m++;
            }
            int lower = m + 1;
            for (int i = count - 2; i >= 0; i--)
            {
                while (m >= lower && Cross(_hx[m - 2], _hz[m - 2], _hx[m - 1], _hz[m - 1], px[i], pz[i]) <= 0f)
                {
                    m--;
                }
                _hx[m] = px[i];
                _hz[m] = pz[i];
                m++;
            }
            return m - 1;
        }

        private static float Cross(float ax, float az, float bx, float bz, float cx, float cz)
        {
            return (bx - ax) * (cz - az) - (bz - az) * (cx - ax);
        }
    }
}
