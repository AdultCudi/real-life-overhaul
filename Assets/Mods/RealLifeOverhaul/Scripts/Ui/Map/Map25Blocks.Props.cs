using System;

namespace RealLifeOverhaul.Map25
{
    // What stands on a roof: a water tank on legs, a stair hut, a row of air conditioners (the same few shapes everywhere, as the mockups draw them).
    public sealed partial class Map25Blocks
    {
        private void Props(Map25Builder b, float[] x, float[] z, int n, float y, Map25Colour tone, float ux, float uz, float u0, float u1, float v0, float v1, float scale)
        {
            for (int s = 0; s < 2; s++)
            {
                if (!_slot[s])
                {
                    continue;
                }
                float u = u0 + _slotFx[s] * (u1 - u0), v = v0 + _slotFy[s] * (v1 - v0);
                float cx = x[0] + ux * u - uz * v, cz = z[0] + uz * u + ux * v;
                float kind = _slotKind[s];
                float room = (kind < 0.28f ? 2.0f : kind < 0.58f ? 1.8f : 1.2f + 1.7f * (_slotCount[s] - 1)) * scale;
                if (!Inside(x, z, n, cx, cz, room))
                {
                    continue;
                }
                if (kind < 0.28f)
                {
                    Tank(b, cx, cz, y, 1.7f * scale);
                }
                else if (kind < 0.58f)
                {
                    Box(b, cx, cz, y, 1.7f * scale, 1.4f * scale, 2.0f * scale, tone.Scale(1.02f), ux, uz);
                }
                else
                {
                    for (int q = 0; q < _slotCount[s]; q++)
                    {
                        float shift = (q - (_slotCount[s] - 1) * 0.5f) * 3.2f * scale;
                        Box(b, cx + ux * shift, cz + uz * shift, y, 1.1f * scale, 0.75f * scale, 1.0f * scale, Map25Palette.Rooftop, ux, uz);
                    }
                }
            }
        }

        // True if the point lies inside the convex outline by at least the margin.
        private static bool Inside(float[] x, float[] z, int n, float px, float pz, float margin)
        {
            for (int k = 0; k < n; k++)
            {
                int f = (k + 1) % n;
                float dx = x[f] - x[k], dz = z[f] - z[k];
                float l = (float)Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-5f)
                {
                    continue;
                }
                if ((dx * (pz - z[k]) - dz * (px - x[k])) / l < margin)
                {
                    return false;
                }
            }
            return true;
        }

        // A box with half sizes along (ux, uz) and across it, its four walls shaded by the way they face, and its top.
        private static void Box(Map25Builder b, float cx, float cz, float y, float halfU, float halfV, float height, Map25Colour colour, float ux, float uz)
        {
            float vx = -uz, vz = ux;
            float ax = cx - ux * halfU - vx * halfV, az = cz - uz * halfU - vz * halfV;
            float bx = cx + ux * halfU - vx * halfV, bz = cz + uz * halfU - vz * halfV;
            float dx = cx + ux * halfU + vx * halfV, dz = cz + uz * halfU + vz * halfV;
            float ex = cx - ux * halfU + vx * halfV, ez = cz - uz * halfU + vz * halfV;
            Face(b, ax, az, bx, bz, y, height, colour, -vx, -vz);
            Face(b, bx, bz, dx, dz, y, height, colour, ux, uz);
            Face(b, dx, dz, ex, ez, y, height, colour, vx, vz);
            Face(b, ex, ez, ax, az, y, height, colour, -ux, -uz);
            Map25Colour top = colour.Scale(1.14f);
            b.Flat(ax, y + height, az, bx, y + height, bz, dx, y + height, dz, ex, y + height, ez, top, 1f);
        }

        private static void Face(Map25Builder b, float ax, float az, float bx, float bz, float y, float height, Map25Colour colour, float nx, float nz)
        {
            Map25Colour c = colour.Scale(Map25Palette.WallFactor(nx, nz));
            b.Strip(ax, az, bx, bz, y, y + height, c.Scale(0.80f), c);
        }

        // A water tank: four thin legs (each two crossed strips, so it shows from any side), an eight sided drum on them, a flat lid and a cone over it.
        private static void Tank(Map25Builder b, float cx, float cz, float y, float r)
        {
            const int sides = 8;
            float legs = 1.45f * r, body = 1.6f * r, cone = 0.7f * r;
            float reach = 0.68f * r, thin = 0.11f;
            Map25Colour post = Map25Palette.Leg;
            for (int c = 0; c < 4; c++)
            {
                float lx = cx + ((c & 1) == 0 ? -reach : reach), lz = cz + ((c & 2) == 0 ? -reach : reach);
                b.Strip(lx - thin, lz, lx + thin, lz, y, y + legs, post.Scale(0.8f), post);
                b.Strip(lx, lz - thin, lx, lz + thin, y, y + legs, post.Scale(0.8f), post);
            }
            for (int s = 0; s < sides; s++)
            {
                double a0 = s * 2.0 * Math.PI / sides, a1 = (s + 1) * 2.0 * Math.PI / sides, am = (a0 + a1) * 0.5;
                float ax = cx + (float)Math.Cos(a0) * r, az = cz + (float)Math.Sin(a0) * r;
                float bx = cx + (float)Math.Cos(a1) * r, bz = cz + (float)Math.Sin(a1) * r;
                Face(b, ax, az, bx, bz, y + legs, body, Map25Palette.Tank, (float)Math.Cos(am), (float)Math.Sin(am));
            }
            float yt = y + legs + body;
            int lid = b.Vertex(cx, yt, cz, Map25Palette.TankTop);
            int apex = b.Vertex(cx, yt + cone, cz, Map25Palette.TankCone.Scale(0.88f));
            for (int s = 0; s < sides; s++)
            {
                double a0 = s * 2.0 * Math.PI / sides, a1 = (s + 1) * 2.0 * Math.PI / sides;
                int i0 = b.Vertex(cx + (float)Math.Cos(a0) * r, yt, cz + (float)Math.Sin(a0) * r, Map25Palette.TankTop);
                int i1 = b.Vertex(cx + (float)Math.Cos(a1) * r, yt, cz + (float)Math.Sin(a1) * r, Map25Palette.TankTop);
                b.Triangle(lid, i1, i0);
                b.Triangle(i0, apex, i1);
            }
        }
    }
}
