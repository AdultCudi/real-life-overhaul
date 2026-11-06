using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The ground of a car park as meshes in the site's own axes (x along the street, z from the street's side back, y up from the
    /// ground): the surface laid on the block's ground, the bays' dividers, the aisles' dashes and the squares for the disabled painted
    /// on it, the low kerb round it broken at the mouth, and the dropped kerb across the pavement and the road's kerb at the mouth, a
    /// ramp the cars cross. Flat colours only (CarParkGame gives each its paint). Made once when a car park stands, and destroyed with it.
    /// COST: a few hundred vertices once; nothing per frame.
    /// </summary>
    internal static class CarParkMesh
    {
        public const int Asphalt = 0, White = 1, Blue = 2, Concrete = 3, Yellow = 4, Submeshes = 5;

        public const float SurfaceY = 0.062f;      // m: the surface, over the block's ground (0.049) and the pavement (0.055) with a margin the depth buffer keeps apart
        public const float PaintLift = 0.004f;     // m: the paint over the surface
        public const float KerbHeight = 0.15f;     // m: the low kerb round the site
        public const float GameKerbTop = 0.105f;   // m: the top of the game's kerb strip along the street, which the dropped kerb carries the cars over
        public const float KerbFrom = 3.52f;       // m out from the site's front line: where the game's kerb strip begins (the street module's 5.13 m from its middle to its pavement's 8.65 m)
        public const float KerbTo = 3.68f;         // ... and where it ends
        public const float RampTo = 4.40f;         // m out: where the dropped kerb meets the road's asphalt
        public const float TactileFrom = 3.0f, TactileTo = 3.25f;   // m out: the strip of yellow paving at the road's edge of the pavement

        private sealed class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>(512);
            public readonly List<Vector3> Normals = new List<Vector3>(512);
            public readonly List<int>[] Triangles = new List<int>[Submeshes];

            public Builder()
            {
                for (int i = 0; i < Submeshes; i++)
                {
                    Triangles[i] = new List<int>(128);
                }
            }

            // A quad round its corners, facing the way <paramref name="toward"/> points.
            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 toward)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                bool flip = Vector3.Dot(n, toward) < 0f;
                if (flip)
                {
                    n = -n;
                }
                int i = Vertices.Count;
                Vertices.Add(a);
                Vertices.Add(b);
                Vertices.Add(c);
                Vertices.Add(d);
                for (int k = 0; k < 4; k++)
                {
                    Normals.Add(n);
                }
                List<int> t = Triangles[sub];
                if (!flip)
                {
                    t.Add(i);
                    t.Add(i + 1);
                    t.Add(i + 2);
                    t.Add(i);
                    t.Add(i + 2);
                    t.Add(i + 3);
                }
                else
                {
                    t.Add(i);
                    t.Add(i + 2);
                    t.Add(i + 1);
                    t.Add(i);
                    t.Add(i + 3);
                    t.Add(i + 2);
                }
            }

            public void Flat(int sub, ParkRect r, float y)
            {
                Quad(sub, new Vector3(r.X0, y, r.Z0), new Vector3(r.X0, y, r.Z1), new Vector3(r.X1, y, r.Z1), new Vector3(r.X1, y, r.Z0), Vector3.up);
            }

            public void Box(int sub, ParkRect r, float y0, float y1)
            {
                Flat(sub, r, y1);
                Quad(sub, new Vector3(r.X0, y0, r.Z0), new Vector3(r.X1, y0, r.Z0), new Vector3(r.X1, y1, r.Z0), new Vector3(r.X0, y1, r.Z0), Vector3.back);
                Quad(sub, new Vector3(r.X0, y0, r.Z1), new Vector3(r.X1, y0, r.Z1), new Vector3(r.X1, y1, r.Z1), new Vector3(r.X0, y1, r.Z1), Vector3.forward);
                Quad(sub, new Vector3(r.X0, y0, r.Z0), new Vector3(r.X0, y0, r.Z1), new Vector3(r.X0, y1, r.Z1), new Vector3(r.X0, y1, r.Z0), Vector3.left);
                Quad(sub, new Vector3(r.X1, y0, r.Z0), new Vector3(r.X1, y0, r.Z1), new Vector3(r.X1, y1, r.Z1), new Vector3(r.X1, y1, r.Z0), Vector3.right);
            }

            public Mesh Make(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(Vertices);
                mesh.SetNormals(Normals);
                mesh.subMeshCount = Submeshes;
                for (int i = 0; i < Submeshes; i++)
                {
                    mesh.SetTriangles(Triangles[i], i);
                }
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>A mesh of a car park's ground destroyed (it is made by code, so code destroys it). Safe on null.</summary>
        public static void Drop(Mesh mesh)
        {
            if (mesh != null)
            {
                UnityEngine.Object.Destroy(mesh);
            }
        }

        /// <summary>The surface, the paint and the low kerb of a plan, in the site's axes.</summary>
        public static Mesh Surface(CarParkLayout l, string name)
        {
            var b = new Builder();
            float hx = l.Width * 0.5f, hz = l.Depth * 0.5f;
            b.Flat(Asphalt, new ParkRect(-hx, -hz, hx, hz), SurfaceY);
            float paint = SurfaceY + PaintLift;
            for (int i = 0; i < l.Lines.Count; i++)
            {
                b.Flat(White, l.Lines[i], paint);
            }
            for (int i = 0; i < l.Dashes.Count; i++)
            {
                b.Flat(White, l.Dashes[i], paint);
            }
            for (int i = 0; i < l.Blue.Count; i++)
            {
                b.Flat(Blue, l.Blue[i], paint + 0.001f);
            }
            for (int i = 0; i < l.Kerbs.Count; i++)
            {
                b.Box(Concrete, l.Kerbs[i], SurfaceY, SurfaceY + KerbHeight);
            }
            return b.Make(name);
        }

        /// <summary>
        /// The dropped kerb across the pavement and the road's kerb at the mouth, in the site's axes, reaching out from its front line:
        /// the apron on the pavement, a rise to the game's kerb strip, its top, and the ramp down to the road. Also the cars' ground there.
        /// </summary>
        public static Mesh Ramp(CarParkLayout l, string name)
        {
            var b = new Builder();
            float x0 = l.Mouth.X0, x1 = l.Mouth.X1, front = -l.Depth * 0.5f;
            float[] d = { 0f, KerbFrom - 0.32f, KerbFrom, KerbTo, RampTo };
            float[] y = { SurfaceY, SurfaceY, GameKerbTop, GameKerbTop, 0f };
            for (int i = 0; i + 1 < d.Length; i++)
            {
                b.Quad(Concrete, new Vector3(x0, y[i], front - d[i]), new Vector3(x1, y[i], front - d[i]), new Vector3(x1, y[i + 1], front - d[i + 1]),
                    new Vector3(x0, y[i + 1], front - d[i + 1]), Vector3.up);
            }
            float lift = PaintLift;
            b.Quad(Yellow, new Vector3(x0, SurfaceY + lift, front - TactileFrom), new Vector3(x1, SurfaceY + lift, front - TactileFrom), new Vector3(x1, SurfaceY + lift, front - TactileTo),
                new Vector3(x0, SurfaceY + lift, front - TactileTo), Vector3.up);
            return b.Make(name);
        }
    }
}
