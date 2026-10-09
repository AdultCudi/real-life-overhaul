using System.Collections.Generic;
using BAModAPI;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Plain shapes for the few props the mod builds itself (cordon posts and tape, the
    /// stretcher): a cube, a round post and one flat material per colour. Every mesh and
    /// material is made once and shared by all the pieces that use it, so a prop is a handful
    /// of renderers with nothing of their own; none has a collider or casts a shadow.
    /// Dispose drops them when the city unloads.
    /// </summary>
    public static class Shapes
    {
        private const string LitShader = "HDRP/Lit";
        private const int PostSides = 10;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private static readonly Dictionary<int, Material> Paints = new Dictionary<int, Material>();

        public static IModLogger Logger;

        private static Mesh _cube;
        private static Mesh _post;
        private static Mesh _drum;
        private static Shader _shader;
        private static bool _shaderSearched;

        /// <summary>False if the game has no shader these shapes can be drawn with; nothing is built then.</summary>
        public static bool Available
        {
            get
            {
                if (!_shaderSearched)
                {
                    _shaderSearched = true;
                    _shader = Shader.Find(LitShader);
                    if (_shader == null)
                    {
                        Logger?.Warn("Props: the game's lit shader was not found; cordons and the stretcher are left out.");
                    }
                }
                return _shader != null;
            }
        }

        /// <summary>A unit cube, centred.</summary>
        public static Mesh Cube
        {
            get
            {
                if (_cube == null)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _cube = cube.GetComponent<MeshFilter>().sharedMesh;
                    Object.Destroy(cube);
                }
                return _cube;
            }
        }

        /// <summary>A tapering round post one unit tall and one wide at the foot, standing on its origin.</summary>
        public static Mesh Post => _post != null ? _post : _post = BuildRound("RealLifeOverhaul Post", 0.5f, 0.18f);

        /// <summary>A straight round drum one unit tall and one wide, standing on its origin (a wheel, laid on its side).</summary>
        public static Mesh Drum => _drum != null ? _drum : _drum = BuildRound("RealLifeOverhaul Drum", 0.5f, 0.5f);

        /// <summary>The one material of a colour, made the first time it is asked for. Null if <see cref="Available"/> is false.</summary>
        /// <param name="glow">How strongly it shows in the dark, 0 for not at all (tape and stripes use a little).</param>
        public static Material Paint(Color colour, float glow = 0f)
        {
            if (!Available)
            {
                return null;
            }
            // Colour and glow packed into one number, so the same colour with and without a glow are two materials.
            Color32 packed = colour;
            int key = packed.r | packed.g << 8 | packed.b << 16 | Mathf.Clamp(Mathf.RoundToInt(glow * 4f), 0, 127) << 24;
            if (!Paints.TryGetValue(key, out Material paint) || paint == null)
            {
                paint = new Material(_shader) { name = "RealLifeOverhaul Paint" };
                paint.SetColor(BaseColorId, new Color(colour.r, colour.g, colour.b, 1f));
                paint.SetFloat(SmoothnessId, 0.25f);
                if (glow > 0f)
                {
                    paint.SetColor(EmissiveColorId, new Color(colour.r, colour.g, colour.b, 1f) * glow);
                }
                paint.enableInstancing = true;
                Paints[key] = paint;
            }
            return paint;
        }

        /// <summary>One piece of a prop: a mesh in a colour, placed and sized under a parent.</summary>
        public static Transform Piece(string name, Transform parent, Mesh mesh, Material paint, Vector3 localPosition, Vector3 size)
        {
            var piece = new GameObject(name);
            Transform place = piece.transform;
            place.SetParent(parent, false);
            place.localPosition = localPosition;
            place.localScale = size;
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = paint;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return place;
        }

        /// <summary>
        /// A modelled prop from the prop library, as one piece under a parent, at the parent's
        /// origin (the models stand on their own origin, in metres). False if the library does
        /// not have it (yet): the caller then builds its plain-shape stand-in, and should ask
        /// again the next time it builds, since the library loads in the background.
        /// </summary>
        public static bool Prop(string id, Transform parent)
        {
            if (!PropLibrary.TryGet(id, out Mesh mesh, out Material material) || mesh == null || material == null)
            {
                return false;
            }
            Piece(id, parent, mesh, material, Vector3.zero, Vector3.one);
            return true;
        }

        /// <summary>Called when the city unloads: the generated meshes and materials go.</summary>
        public static void Dispose()
        {
            foreach (Material paint in Paints.Values)
            {
                if (paint != null)
                {
                    Object.Destroy(paint);
                }
            }
            Paints.Clear();
            if (_post != null)
            {
                Object.Destroy(_post);
            }
            if (_drum != null)
            {
                Object.Destroy(_drum);
            }
            _post = null;
            _drum = null;
            _cube = null;   // the engine's own; only forgotten
        }

        // A round shape with a flat top, one unit tall: sides and a cap, no underside (it stands on the ground).
        private static Mesh BuildRound(string name, float footRadius, float topRadius)
        {
            int ring = PostSides + 1;   // the seam is doubled so the shading wraps cleanly
            var vertices = new Vector3[ring * 2 + PostSides + 1];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[PostSides * 6 + PostSides * 3];
            float slope = footRadius - topRadius;

            for (int i = 0; i < ring; i++)
            {
                float angle = i * Mathf.PI * 2f / PostSides;
                var spoke = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i] = spoke * footRadius;
                vertices[ring + i] = spoke * topRadius + Vector3.up;
                Vector3 normal = new Vector3(spoke.x, slope, spoke.z).normalized;
                normals[i] = normal;
                normals[ring + i] = normal;
            }
            int t = 0;
            for (int i = 0; i < PostSides; i++)
            {
                triangles[t++] = i;
                triangles[t++] = ring + i;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = ring + i;
                triangles[t++] = ring + i + 1;
            }

            int cap = ring * 2;
            for (int i = 0; i < PostSides; i++)
            {
                float angle = i * Mathf.PI * 2f / PostSides;
                vertices[cap + i] = new Vector3(Mathf.Cos(angle) * topRadius, 1f, Mathf.Sin(angle) * topRadius);
                normals[cap + i] = Vector3.up;
            }
            int centre = cap + PostSides;
            vertices[centre] = Vector3.up;
            normals[centre] = Vector3.up;
            for (int i = 0; i < PostSides; i++)
            {
                triangles[t++] = centre;
                triangles[t++] = cap + (i + 1) % PostSides;
                triangles[t++] = cap + i;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            // Kept readable (a few hundred bytes): the game reads the normals of every mesh it finds on a character when
            // it merges that character's meshes, kit pieces on the bones included, and warns twice for each it may not read.
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
