using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Map25
{
    /// <summary>What the player's marks add to a drawn picture: the player's view cone and halo on the ground (their radii in metres), and whether the distance fades.</summary>
    public struct Map25Frame
    {
        public bool Player;
        public float PlayerX, PlayerY, PlayerZ, Heading, ConeRadius, HaloRadius;
        public bool Fog;
    }

    /// <summary>
    /// The map's blocks, ground, roads and route in meshes, and the one way they are drawn: into a render texture the interface
    /// shows, with the engine's own colour shader (the vertex colour is the colour: no light, no exposure, no volume, so the
    /// picture is the mockup's at any hour). Meshes are made from <see cref="Map25Chunk"/>s a slice at a time (<see cref="Upload"/>),
    /// the CPU copy let go. A frame: the chunks in view, in a command buffer
    /// that is made once. Nothing here allocates once built. Uses only the engine, so the preview draws with it too.
    /// </summary>
    public sealed class Map25Scene : IDisposable
    {
        public const string ShaderName = "Hidden/Internal-Colored";
        private const float WaterY = -0.6f;
        private static readonly int DepthTestId = Shader.PropertyToID("_ZTest");

        /// <summary>Whether the projection is the one for a render texture (the platform may flip it).</summary>
        public static bool IntoTexture = true;

        /// <summary>
        /// The depth test the blocks are drawn with (a CompareFunction): a material's test given by a property is not turned round for a
        /// platform whose depth runs from far to near, as a literal one is, so it is chosen here, by <see cref="Calibrate"/>.
        /// </summary>
        public static int DepthTest = 4;

        // A run of chunks that become meshes: where they come from, where the meshes go.
        private sealed class Group
        {
            public Map25Chunk[] Chunks;
            public Mesh[] Meshes;
            public string Name;
        }

        private readonly Group[] _under = new Group[5];   // land, greens, roads, lanes, trees
        private readonly Group[] _kinds;              // by kind
        private readonly List<Group> _groups = new List<Group>(16);
        private Mesh _water, _cone, _halo, _fog, _route, _ghost;
        private readonly Map25Builder _routeBuilder = new Map25Builder(512);
        private bool _routeOn;
        private float[] _rx, _ry, _rz;   // the route's points, the director's own arrays
        private int _rcount;
        private float _ry0;
        private Material _flat, _solid, _blend;
        private CommandBuffer _cmd;
        private readonly Matrix4x4[] _matrices = new Matrix4x4[3];   // the view, the GPU projection, the blocks' own
        private int _cursor, _groupAt, _inGroup;
        private readonly int _total;

        /// <summary>True when the engine's colour shader is there to draw with; <paramref name="why"/> says what is missing otherwise.</summary>
        public static bool Available(out string why)
        {
            Shader shader = Shader.Find(ShaderName);
            why = shader == null ? "the engine's colour shader '" + ShaderName + "' was not found" : shader.isSupported ? null : "the colour shader is not supported here";
            return why == null;
        }

        /// <summary>
        /// Finds how this engine wants the projection of a command buffer given, by drawing two quads into a 16 pixel texture and reading
        /// it back once: the built-in pipeline turns a texture's picture over itself and a scriptable one does not, and the
        /// depth may run either way. Sets <see cref="IntoTexture"/> to the setting that puts a mark at the top left of the picture
        /// where it belongs and lets the nearer of two quads win; false, with <paramref name="report"/> saying why, when neither does.
        /// </summary>
        public static bool Calibrate(out string report)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                report = "the colour shader is missing";
                return false;
            }
            Material solid = Make(shader, "RealLife Map calibration", 1, 0, 1, 4);
            var b = new Map25Builder(16);
            Map25Colour white = new Map25Colour(1f, 1f, 1f), red = new Map25Colour(1f, 0f, 0f);
            // a near quad and a far one over the same spot at the top left, as a perspective camera sees them (the depth's remap is read for that, not for a flat box)
            b.Flat(-1.1f, 0.3f, -2f, -0.3f, 0.3f, -2f, -0.3f, 1.1f, -2f, -1.1f, 1.1f, -2f, white, 1f);
            b.Flat(-3f, 0.5f, -5f, -0.5f, 0.5f, -5f, -0.5f, 3f, -5f, -3f, 3f, -5f, red, 1f);
            Matrix4x4 lens = Matrix4x4.Perspective(60f, 1f, 0.3f, 100f);
            Mesh mesh = FromBuilder(b, "RealLife Map calibration");
            var target = new RenderTexture(16, 16, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var cmd = new CommandBuffer { name = "RealLife Map calibration" };
            var read = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            RenderTexture before = RenderTexture.active;
            bool found = false;
            bool wasFlipped = false, wasRed = false;
            try
            {
                target.Create();
                bool[] tries = { IntoTexture, !IntoTexture };
                int first = SystemInfo.usesReversedZBuffer ? 7 : 4;
                int[] depths = { first, first == 4 ? 7 : 4 };
                for (int t = 0; t < 4 && !found; t++)
                {
                    solid.SetInt("_ZTest", depths[t >> 1]);
                    cmd.Clear();
                    cmd.SetRenderTarget(target);
                    cmd.ClearRenderTarget(true, true, Color.black, SystemInfo.usesReversedZBuffer ? 0f : 1f);
                    cmd.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(lens, tries[t & 1]));
                    cmd.DrawMesh(mesh, Matrix4x4.identity, solid, 0, 0);
                    Graphics.ExecuteCommandBuffer(cmd);
                    RenderTexture.active = target;
                    read.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
                    Color top = read.GetPixel(2, 13), bottom = read.GetPixel(2, 2);
                    if (top.r > 0.6f && top.g > 0.6f && top.b > 0.6f)
                    {
                        IntoTexture = tries[t & 1];
                        DepthTest = depths[t >> 1];
                        found = true;
                    }
                    wasFlipped |= bottom.r > 0.6f && bottom.g > 0.6f;
                    wasRed |= top.r > 0.6f && top.g < 0.3f;
                }
            }
            finally
            {
                RenderTexture.active = before;
                cmd.Release();
                UnityEngine.Object.Destroy(read);
                UnityEngine.Object.Destroy(mesh);
                UnityEngine.Object.Destroy(solid);
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            report = found ? "the projection is given " + (IntoTexture ? "for a render texture" : "plain") + " here and the depth test is " + (DepthTest == 4 ? "less or equal" : "greater or equal")
                           : (wasRed ? "the depth runs the wrong way here" : wasFlipped ? "the picture comes out turned over either way" : "nothing was drawn");
            return found;
        }

        /// <summary>
        /// True when the middle of a drawn picture is black: nothing was drawn (the colour shader draws nothing here, or the camera's matrices put
        /// the whole scene out of view). One pixel is read back, once, after the first draw of each picture; no colour of the map is that dark.
        /// </summary>
        public static bool Blank(RenderTexture target)
        {
            RenderTexture before = RenderTexture.active;
            var one = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                one.ReadPixels(new Rect(target.width / 2, target.height / 2, 1, 1), 0, 0);
                Color c = one.GetPixel(0, 0);
                return c.r + c.g + c.b < 0.02f;
            }
            finally
            {
                RenderTexture.active = before;
                UnityEngine.Object.Destroy(one);
            }
        }

        public Map25Scene(Map25Set set, Map25Under under, float minX, float minZ, float maxX, float maxZ)
        {
            _kinds = new Group[Map25Builders.Kinds];
            string[] names = { "land", "greens", "roads", "lanes", "trees" };
            Map25Chunk[][] chunks = { under.Land, under.Greens, under.Roads, under.Lanes, under.Trees };
            for (int g = 0; g < _under.Length; g++)
            {
                _under[g] = Add(chunks[g], "RealLife Map " + names[g]);
            }
            string[] kinds = { "blocks", "crowns", "contacts" };
            for (int k = 0; k < Map25Builders.Kinds; k++)
            {
                _kinds[k] = Add(set.Layers[k].Chunks, "RealLife Map " + kinds[k]);
            }
            for (int g = 0; g < _groups.Count; g++)
            {
                _total += _groups[g].Chunks.Length;
            }
            Shader shader = Shader.Find(ShaderName);
            _flat = Make(shader, "RealLife Map flat", 1, 0, 0, 8);
            _solid = Make(shader, "RealLife Map solid", 1, 0, 1, DepthTest);
            _blend = Make(shader, "RealLife Map blend", 5, 10, 0, 8);
            _cmd = new CommandBuffer { name = "RealLife Map" };
            _water = Quad(minX - 20000f, WaterY, minZ - 20000f, maxX + 20000f, WaterY, maxZ + 20000f, Map25Palette.Water, 1f);
            Map25Colour blue = Map25Palette.Route;
            _cone = Fan(0f, 0.3f, 1f, Map25Marks.ConeHalfAngle, blue, Map25Marks.ConeAlpha);
            _halo = Fan(0f, 0.3f, 1f, 180f, blue, Map25Marks.HaloAlpha);
            _fog = FogQuad();
        }

        private Group Add(Map25Chunk[] chunks, string name)
        {
            var group = new Group { Chunks = chunks, Meshes = new Mesh[chunks.Length], Name = name };
            _groups.Add(group);
            return group;
        }

        /// <summary>Chunks still to turn into meshes.</summary>
        public int Pending => _total - _cursor;

        private static Material Make(Shader shader, string name, int source, int destination, int depthWrite, int depthTest)
        {
            var material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            material.SetInt("_SrcBlend", source);
            material.SetInt("_DstBlend", destination);
            material.SetInt("_ZWrite", depthWrite);
            material.SetInt("_ZTest", depthTest);
            material.SetInt("_Cull", 0);
            material.SetColor("_Color", Color.white);
            return material;
        }

        /// <summary>Makes meshes from the next chunks for at most <paramref name="milliseconds"/>; true while there are more.</summary>
        public bool Upload(double milliseconds)
        {
            long began = Stopwatch.GetTimestamp();
            while (_cursor < _total)
            {
                while (_inGroup >= _groups[_groupAt].Chunks.Length)
                {
                    _groupAt++;
                    _inGroup = 0;
                }
                Group group = _groups[_groupAt];
                group.Meshes[_inGroup] = FromChunk(group.Chunks[_inGroup], group.Name + "." + _inGroup);
                _inGroup++;
                _cursor++;
                if ((Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency >= milliseconds)
                {
                    break;
                }
            }
            return _cursor < _total;
        }

        // A mesh from a chunk's arrays, let go of on the CPU side.
        private static Mesh FromChunk(Map25Chunk chunk, string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
            Fill(mesh, chunk.Pos, chunk.Col, chunk.Idx, chunk.Vertices, chunk.Indices);
            mesh.bounds = new Bounds(new Vector3((chunk.MinX + chunk.MaxX) * 0.5f, (chunk.MinY + chunk.MaxY) * 0.5f, (chunk.MinZ + chunk.MaxZ) * 0.5f),
                                     new Vector3(chunk.MaxX - chunk.MinX, chunk.MaxY - chunk.MinY, chunk.MaxZ - chunk.MinZ));
            chunk.Pos = null;
            chunk.Idx = null;
            chunk.Col = null;
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static void Fill(Mesh mesh, float[] pos, ushort[] col, ushort[] idx, int vertices, int indices)
        {
            mesh.SetVertexBufferParams(vertices,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float16, 4, 1));
            mesh.SetVertexBufferData(pos, 0, 0, vertices * 3, 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetVertexBufferData(col, 0, 0, vertices * 4, 1, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetIndexBufferParams(indices, IndexFormat.UInt16);
            mesh.SetIndexBufferData(idx, 0, 0, indices, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices, MeshTopology.Triangles), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }

        private static Mesh FromBuilder(Map25Builder b, string name)
        {
            Map25Chunk chunk = b.ToChunk();
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
            Fill(mesh, chunk.Pos, chunk.Col, chunk.Idx, chunk.Vertices, chunk.Indices);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1e6f));
            return mesh;
        }

        private static Mesh Quad(float x0, float y, float z0, float x1, float y1, float z1, Map25Colour colour, float alpha)
        {
            var b = new Map25Builder(8);
            b.Flat(x0, y, z0, x1, y, z0, x1, y1, z1, x0, y1, z1, colour, alpha);
            return FromBuilder(b, "RealLife Map quad");
        }

        // A unit fan on the ground pointing north: scaled and turned by a matrix, so it is built once.
        private static Mesh Fan(float heading, float lift, float radius, float halfAngle, Map25Colour colour, float alpha)
        {
            var b = new Map25Builder(64);
            Map25Ground.Fan(b, 0f, lift, 0f, radius, heading, halfAngle, colour, alpha);
            return FromBuilder(b, "RealLife Map fan");
        }

        // The distance fades into the ground: a strip across the top of the picture, drawn in the picture's own coordinates.
        private static Mesh FogQuad()
        {
            var b = new Map25Builder(8);
            Map25Colour c = Map25Palette.Water;
            int a = b.Vertex(-1f, 1f, 0f, c, 0.92f);
            int d = b.Vertex(1f, 1f, 0f, c, 0.92f);
            int e = b.Vertex(1f, 0.55f, 0f, c, 0f);
            int f = b.Vertex(-1f, 0.55f, 0f, c, 0f);
            b.Quad(a, d, e, f);
            return FromBuilder(b, "RealLife Map fog");
        }

        /// <summary>
        /// The route, as the director keeps it (the arrays are held, not copied): written as two strips, a dark casing and the line over it, and the same
        /// line again, faint, to be drawn over the blocks. Its width is <see cref="RouteHalf"/>, set by <see cref="SetRouteHalf"/> for the picture that is drawn; null clears it.
        /// </summary>
        public void SetRoute(float[] x, float[] y, float[] z, int count, float y0)
        {
            if (x == null || count < 2)
            {
                _routeOn = false;
                _rx = null;
                return;
            }
            _rx = x;
            _ry = y;
            _rz = z;
            _rcount = count;
            _ry0 = y0;
            WriteRoute();
        }

        /// <summary>The route's half width in metres now; the one a picture draws it with is <see cref="Map25Marks.RouteHalf"/> where it looks (see <see cref="Map25Marks.Step"/>).</summary>
        public float RouteHalf { get; private set; } = 3.5f;

        /// <summary>
        /// Writes the route again when its half width is not <paramref name="half"/> (the picture wants a few pixels, so the metres follow the zoom);
        /// a compare while it is. Call it before a draw with <see cref="Map25Marks.Step"/> of the width wanted.
        /// </summary>
        public void SetRouteHalf(float half)
        {
            if (half > 0f && Math.Abs(half - RouteHalf) > RouteHalf * 0.001f)
            {
                RouteHalf = half;
                if (_rx != null)
                {
                    WriteRoute();
                }
            }
        }

        // The two strips of the route are written into the same two meshes each time it changes: no mesh and no array is made.
        private void WriteRoute()
        {
            if (_route == null)
            {
                _route = Empty("RealLife Map route");
                _ghost = Empty("RealLife Map route ghost");
            }
            _routeBuilder.Clear();
            Map25Ground.Ribbon(_routeBuilder, _rx, _ry, _rz, _rcount, RouteHalf * Map25Marks.CasingShare, Map25Palette.RouteCasing, 1f, 0.10f, _ry0);
            Map25Ground.Ribbon(_routeBuilder, _rx, _ry, _rz, _rcount, RouteHalf, Map25Palette.Route, 1f, 0.16f, _ry0);
            Fill(_route, _routeBuilder.PositionData, _routeBuilder.ColourData, _routeBuilder.IndexData, _routeBuilder.Vertices, _routeBuilder.Indices);
            _routeBuilder.Clear();
            Map25Ground.Ribbon(_routeBuilder, _rx, _ry, _rz, _rcount, RouteHalf, Map25Palette.Route, 0.38f, 0.20f, _ry0);
            Fill(_ghost, _routeBuilder.PositionData, _routeBuilder.ColourData, _routeBuilder.IndexData, _routeBuilder.Vertices, _routeBuilder.Indices);
            _routeOn = _routeBuilder.Indices > 0;
        }

        // The chunks of a group that are in view, as one draw each.
        private void DrawGroup(CommandBuffer cmd, Group group, Map25View view, bool tall, Matrix4x4 matrix, Material material)
        {
            for (int k = 0; k < group.Meshes.Length; k++)
            {
                if (group.Meshes[k] != null && view.Sees(group.Chunks[k], tall))
                {
                    cmd.DrawMesh(group.Meshes[k], matrix, material, 0, 0);
                }
            }
        }

        /// <summary>
        /// Draws the picture into <paramref name="target"/> as <paramref name="view"/> sees it. Everything out of view is left out whole
        /// chunks at a time. Order: water, land, green ground, roads and their lane marking, the player's cone
        /// and halo, the contact patches, the trees, the blocks (the only things that write depth) with their crowns when they
        /// stand tall enough, the route and its faint copy over the blocks,
        /// and the distance's fade. <see cref="Map25Rules.Draws"/> says which kinds a view has.
        /// </summary>
        public void Draw(RenderTexture target, Map25View view, in Map25Frame frame)
        {
            CommandBuffer cmd = _cmd;
            cmd.Clear();
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(true, true, Color.black, SystemInfo.usesReversedZBuffer ? 0f : 1f);
            for (int i = 0; i < 16; i++)
            {
                _matrices[0][i] = view.View[i];
            }
            for (int i = 0; i < 16; i++)
            {
                _matrices[2][i] = view.Projection[i];
            }
            _matrices[1] = GL.GetGPUProjectionMatrix(_matrices[2], IntoTexture);
            _solid.SetInt(DepthTestId, DepthTest);
            cmd.SetViewProjectionMatrices(_matrices[0], _matrices[1]);
            Matrix4x4 identity = Matrix4x4.identity;
            cmd.DrawMesh(_water, identity, _flat, 0, 0);
            DrawGroup(cmd, _under[0], view, false, identity, _flat);   // land
            DrawGroup(cmd, _under[1], view, false, identity, _flat);   // green ground
            DrawGroup(cmd, _under[2], view, false, identity, _flat);   // roads
            DrawGroup(cmd, _under[3], view, false, identity, _flat);   // their lane marking
            if (frame.Player)
            {
                // on the ground, under the blocks: a tall block hides the part of the cone behind it
                Vector3 at = new Vector3(frame.PlayerX, frame.PlayerY, frame.PlayerZ);
                cmd.DrawMesh(_cone, Matrix4x4.TRS(at, Quaternion.Euler(0f, frame.Heading, 0f), new Vector3(frame.ConeRadius, 1f, frame.ConeRadius)), _blend, 0, 0);
                cmd.DrawMesh(_halo, Matrix4x4.TRS(at, Quaternion.identity, new Vector3(frame.HaloRadius, 1f, frame.HaloRadius)), _blend, 0, 0);
            }
            Matrix4x4 rise = Matrix4x4.identity;
            rise.m11 = view.Rise;
            Group[] kinds = _kinds;
            if (Map25Rules.Draws(Map25Kind.Contacts, view.Rise))
            {
                DrawGroup(cmd, kinds[(int)Map25Kind.Contacts], view, false, identity, _blend);
            }
            DrawGroup(cmd, _under[4], view, true, rise, _solid);   // trees
            DrawGroup(cmd, kinds[(int)Map25Kind.Blocks], view, true, rise, _solid);
            if (Map25Rules.Draws(Map25Kind.Crowns, view.Rise))
            {
                DrawGroup(cmd, kinds[(int)Map25Kind.Crowns], view, true, rise, _solid);
            }
            if (_routeOn)
            {
                cmd.DrawMesh(_route, identity, _solid, 0, 0);
                cmd.DrawMesh(_ghost, identity, _blend, 0, 0);
            }
            if (frame.Fog)
            {
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.identity, IntoTexture));
                cmd.DrawMesh(_fog, Matrix4x4.identity, _blend, 0, 0);
            }
            Graphics.ExecuteCommandBuffer(cmd);
        }

        // A mesh to be written into again and again, never culled by its box.
        private static Mesh Empty(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1e6f));
            return mesh;
        }

        private static void DropMaterial(ref Material material)
        {
            if (material != null)
            {
                UnityEngine.Object.Destroy(material);
                material = null;
            }
        }

        private static void DestroyMesh(ref Mesh mesh)
        {
            if (mesh != null)
            {
                UnityEngine.Object.Destroy(mesh);
                mesh = null;
            }
        }

        public void Dispose()
        {
            for (int g = 0; g < _groups.Count; g++)
            {
                Mesh[] meshes = _groups[g].Meshes;
                for (int i = 0; i < meshes.Length; i++)
                {
                    DestroyMesh(ref meshes[i]);
                }
            }
            DestroyMesh(ref _water);
            DestroyMesh(ref _cone);
            DestroyMesh(ref _halo);
            DestroyMesh(ref _fog);
            DestroyMesh(ref _route);
            DestroyMesh(ref _ghost);
            DropMaterial(ref _flat);
            DropMaterial(ref _solid);
            DropMaterial(ref _blend);
            if (_cmd != null)
            {
                _cmd.Release();
                _cmd = null;
            }
        }
    }
}
