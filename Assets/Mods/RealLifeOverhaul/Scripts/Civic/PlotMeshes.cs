using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A plot's stage as one mesh: boxes in two sub-meshes, drawn in the game's own concrete and metal (SiteKit), and the wire
    /// fence as panels and posts, drawn in the game's fence wire. The concrete the game paints by world position (triplanar), so
    /// the boxes carry plain metre UVs for the rest. Cost: nothing per frame; one mesh built when a plot's stage changes.
    /// </summary>
    internal static class PlotMeshes
    {
        private const float MetalTile = 0.5f;      // UV a metre: the game's metal repeats every two metres
        private const float WireTile = 1f / PlotRules.FencePanel;   // UV a metre: one fence panel shows the wire texture once, as the game's own fences
        private const float PostSide = 0.05f;      // metres: a fence post
        private const float WireFoot = 0.1f;       // metres from the ground to the bottom of the wire

        private static readonly List<Vector3> Vertices = new List<Vector3>(4096);
        private static readonly List<Vector3> Normals = new List<Vector3>(4096);
        private static readonly List<Vector2> Uvs = new List<Vector2>(4096);
        private static readonly List<int> First = new List<int>(6144);
        private static readonly List<int> Second = new List<int>(6144);

        private static readonly Vector3[] FaceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        /// <summary>Boxes in the land's axes as one mesh: the first list's in sub-mesh 0, the second's in sub-mesh 1.</summary>
        public static Mesh Boxes(List<ShellBox> first, List<ShellBox> second, string name)
        {
            Begin();
            for (int i = 0; i < first.Count; i++)
            {
                Box(first[i], First);
            }
            for (int i = 0; i < second.Count; i++)
            {
                Box(second[i], Second);
            }
            return End(name);
        }

        /// <summary>The fence: wire panels (sub-mesh 0, seen from both sides) between posts every panel, and the posts (sub-mesh 1).</summary>
        public static Mesh Fence(List<FenceRun> runs, string name)
        {
            Begin();
            for (int r = 0; r < runs.Count; r++)
            {
                FenceRun run = runs[r];
                var from = new Vector3(run.X0, 0f, run.Z0);
                var to = new Vector3(run.X1, 0f, run.Z1);
                float length = Vector3.Distance(from, to);
                int panels = Mathf.Max(1, Mathf.CeilToInt(length / PlotRules.FencePanel - 0.01f));
                for (int p = 0; p < panels; p++)
                {
                    Vector3 a = Vector3.Lerp(from, to, (float)p / panels), b = Vector3.Lerp(from, to, (float)(p + 1) / panels);
                    Panel(a, b);
                    Post(a);
                }
                Post(to);
            }
            return End(name);
        }

        /// <summary>A mesh made here goes when its stage is taken down: destroying what shows it does not free it.</summary>
        public static void Drop(Mesh mesh)
        {
            if (mesh != null)
            {
                Object.Destroy(mesh);
            }
        }

        private static void Begin()
        {
            Vertices.Clear();
            Normals.Clear();
            Uvs.Clear();
            First.Clear();
            Second.Clear();
        }

        private static Mesh End(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, Uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(First, 0, false);
            mesh.SetTriangles(Second, 1, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(true);   // drawn only: no copy kept in main memory
            Begin();
            return mesh;
        }

        // A box's six faces, each with its own corners so that its edges stay sharp.
        private static void Box(ShellBox box, List<int> into)
        {
            var min = new Vector3(box.MinX, box.MinY, box.MinZ);
            var max = new Vector3(box.MaxX, box.MaxY, box.MaxZ);
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = FaceNormals[f];
                // The face's two directions across it, and its middle.
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(n, u);
                Vector3 middle = (min + max) * 0.5f + Vector3.Scale(n, (max - min) * 0.5f);
                float halfU = Mathf.Abs(Vector3.Dot(u, max - min)) * 0.5f, halfV = Mathf.Abs(Vector3.Dot(v, max - min)) * 0.5f;
                Quad(middle - u * halfU - v * halfV, middle + u * halfU - v * halfV, middle + u * halfU + v * halfV, middle - u * halfU + v * halfV, n, MetalTile, into);
            }
        }

        // A quad of four corners going round its face anticlockwise as seen from where its normal points.
        private static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float tile, List<int> into)
        {
            int at = Vertices.Count;
            Vector3 across = (b - a).normalized, up = (d - a).normalized;
            float width = Vector3.Distance(a, b), height = Vector3.Distance(a, d);
            float u0 = Vector3.Dot(a, across) * tile, v0 = Vector3.Dot(a, up) * tile;
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Vertices.Add(d);
            for (int i = 0; i < 4; i++)
            {
                Normals.Add(normal);
            }
            Uvs.Add(new Vector2(u0, v0));
            Uvs.Add(new Vector2(u0 + width * tile, v0));
            Uvs.Add(new Vector2(u0 + width * tile, v0 + height * tile));
            Uvs.Add(new Vector2(u0, v0 + height * tile));
            // Unity's front faces wind clockwise as seen from where the normal points. In its left-handed axes a corner order
            // whose cross product points along the normal goes round clockwise from that side.
            if (Vector3.Dot(Vector3.Cross(b - a, d - a), normal) > 0f)
            {
                into.Add(at);
                into.Add(at + 1);
                into.Add(at + 2);
                into.Add(at);
                into.Add(at + 2);
                into.Add(at + 3);
            }
            else
            {
                into.Add(at);
                into.Add(at + 2);
                into.Add(at + 1);
                into.Add(at);
                into.Add(at + 3);
                into.Add(at + 2);
            }
        }

        // One panel of wire, a face each way so that it is seen from inside the plot and from the street.
        private static void Panel(Vector3 a, Vector3 b)
        {
            Vector3 up = Vector3.up * (PlotRules.FenceHeight - WireFoot), foot = Vector3.up * WireFoot;
            Vector3 normal = Vector3.Cross(Vector3.up, b - a).normalized;
            Quad(a + foot, b + foot, b + foot + up, a + foot + up, normal, WireTile, First);
            Quad(b + foot, a + foot, a + foot + up, b + foot + up, -normal, WireTile, First);
        }

        private static void Post(Vector3 at)
        {
            Box(new ShellBox(at.x - PostSide, at.x + PostSide, 0f, PlotRules.FenceHeight + 0.05f, at.z - PostSide, at.z + PostSide), Second);
            Box(new ShellBox(at.x - 0.3f, at.x + 0.3f, 0f, 0.13f, at.z - 0.1f, at.z + 0.1f), Second);   // its weighted foot
        }
    }
}
