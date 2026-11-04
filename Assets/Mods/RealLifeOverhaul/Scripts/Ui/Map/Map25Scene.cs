using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Map25
{
    /// <summary>What the player's marks add to a drawn picture: the player's view cone and halo on the ground, and whether the distance fades.</summary>
    public struct Map25Frame
    {
        public bool Player;
        public float PlayerX, PlayerY, PlayerZ, Heading, ConeRadius;
        public bool Fog;
    }

    /// <summary>
    /// The map's blocks, ground, roads and route in meshes, and the one way they are drawn: into a render texture the interface
    /// shows, with the engine's own colour shader (the vertex colour is the colour: no light, no exposure, no volume, so the
    /// picture is the mockup's at any hour). Meshes are made from <see cref="Map25Chunk"/>s a slice at a time (<see cref="Upload"/>),
    /// the CPU copy of everything but the colours let go. A frame: the chunks in view, at most about fifty draws, in a command buffer
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

        private readonly Map25Set[] _sets;           // the detail sets: 0 is the full map's, 1 the minimap's (one when only one is wanted)
        private readonly Map25Chunk[] _land, _roads;
        private readonly Mesh[][] _blocks, _shadows;
        private readonly Mesh[] _landMesh, _roadMesh;
        private readonly int[][] _seen;
        private Mesh _water, _cone, _halo, _fog, _route, _ghost;
        private readonly Map25Builder _routeBuilder = new Map25Builder(512);
        private bool _routeOn;
        private Material _flat, _solid, _blend;
        private CommandBuffer _cmd;
        private readonly Matrix4x4[] _matrices = new Matrix4x4[3];   // the view, the GPU projection, the blocks' own
        private int _cursor;
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

        public Map25Scene(Map25Set[] sets, Map25Chunk[] land, Map25Chunk[] roads, float minX, float minZ, float maxX, float maxZ)
        {
            _sets = sets;
            _land = land;
            _roads = roads;
            _blocks = new Mesh[sets.Length][];
            _shadows = new Mesh[sets.Length][];
            _seen = new int[sets.Length][];
            _total = land.Length + roads.Length;
            for (int s = 0; s < sets.Length; s++)
            {
                _blocks[s] = new Mesh[sets[s].Blocks.Length];
                _shadows[s] = new Mesh[sets[s].Shadows.Length];
                _seen[s] = new int[sets[s].Blocks.Length];
                _total += _blocks[s].Length + _shadows[s].Length;
            }
            _landMesh = new Mesh[land.Length];
            _roadMesh = new Mesh[roads.Length];
            Shader shader = Shader.Find(ShaderName);
            _flat = Make(shader, "RealLife Map flat", 1, 0, 0, 8);
            _solid = Make(shader, "RealLife Map solid", 1, 0, 1, DepthTest);
            _blend = Make(shader, "RealLife Map blend", 5, 10, 0, 8);
            _cmd = new CommandBuffer { name = "RealLife Map" };
            _water = Quad(minX - 20000f, WaterY, minZ - 20000f, maxX + 20000f, WaterY, maxZ + 20000f, Map25Palette.Water, 1f);
            _cone = Fan(0f, 1.2f, 1f, 22f, new Map25Colour(0.31f, 0.72f, 0.92f), 0.17f);
            _halo = Fan(0f, 1.2f, 0.25f, 180f, new Map25Colour(0.31f, 0.72f, 0.92f), 0.26f);
            _fog = FogQuad();
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
                int k = _cursor;
                if (k < _landMesh.Length)
                {
                    _landMesh[k] = FromChunk(_land[k], "RealLife Map land " + k, false);
                }
                else if ((k -= _landMesh.Length) < _roadMesh.Length)
                {
                    _roadMesh[k] = FromChunk(_roads[k], "RealLife Map roads " + k, false);
                }
                else
                {
                    k -= _roadMesh.Length;
                    for (int s = 0; s < _sets.Length; s++)
                    {
                        if (k < _blocks[s].Length)
                        {
                            _blocks[s][k] = FromChunk(_sets[s].Blocks[k], "RealLife Map blocks " + s + "." + k, true);
                            _seen[s][k] = _sets[s].Blocks[k].ColourVersion;
                            break;
                        }
                        k -= _blocks[s].Length;
                        if (k < _shadows[s].Length)
                        {
                            _shadows[s][k] = FromChunk(_sets[s].Shadows[k], "RealLife Map shadows " + s + "." + k, false);
                            break;
                        }
                        k -= _shadows[s].Length;
                    }
                }
                _cursor++;
                if ((Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency >= milliseconds)
                {
                    break;
                }
            }
            return _cursor < _total;
        }

        // A mesh from a chunk's arrays. It stays readable so its colours can be written again; the chunk's own places and indices are let go.
        private static Mesh FromChunk(Map25Chunk chunk, string name, bool keepColours)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
            Fill(mesh, chunk.Pos, chunk.Col, chunk.Idx, chunk.Vertices, chunk.Indices);
            mesh.bounds = new Bounds(new Vector3((chunk.MinX + chunk.MaxX) * 0.5f, (chunk.MinY + chunk.MaxY) * 0.5f, (chunk.MinZ + chunk.MaxZ) * 0.5f),
                                     new Vector3(chunk.MaxX - chunk.MinX, chunk.MaxY - chunk.MinY, chunk.MaxZ - chunk.MinZ));
            chunk.Pos = null;
            chunk.Idx = null;
            if (!keepColours)
            {
                chunk.Col = null;
            }
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

        /// <summary>The route as two strips, a dark casing and the line over it (<paramref name="main"/>), and the same line again, faint, to be drawn over the blocks; null clears them.</summary>
        public void SetRoute(float[] x, float[] y, float[] z, int count, float half, float y0)
        {
            if (x == null || count < 2)
            {
                _routeOn = false;
                return;
            }
            if (_route == null)
            {
                _route = new Mesh { name = "RealLife Map route", indexFormat = IndexFormat.UInt16 };
                _ghost = new Mesh { name = "RealLife Map route ghost", indexFormat = IndexFormat.UInt16 };
                _route.bounds = _ghost.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1e6f));
            }
            // the two strips of the route are written into the same two meshes each time it changes: no mesh and no array is made
            _routeBuilder.Clear();
            Map25Ground.Ribbon(_routeBuilder, x, y, z, count, half * 1.45f, Map25Palette.RouteCasing, 1f, 0.10f, y0);
            Map25Ground.Ribbon(_routeBuilder, x, y, z, count, half, Map25Palette.Route, 1f, 0.16f, y0);
            Fill(_route, _routeBuilder.PositionData, _routeBuilder.ColourData, _routeBuilder.IndexData, _routeBuilder.Vertices, _routeBuilder.Indices);
            _routeBuilder.Clear();
            Map25Ground.Ribbon(_routeBuilder, x, y, z, count, half, Map25Palette.Route, 0.38f, 0.20f, y0);
            Fill(_ghost, _routeBuilder.PositionData, _routeBuilder.ColourData, _routeBuilder.IndexData, _routeBuilder.Vertices, _routeBuilder.Indices);
            _routeOn = _routeBuilder.Indices > 0;
        }

        /// <summary>Writes building <paramref name="hull"/>'s colours again in every detail set, as the city now tints it.</summary>
        public void Retint(Map25City city, int hull, Map25Blocks generator, Map25Builder scratch)
        {
            for (int s = 0; s < _sets.Length; s++)
            {
                _sets[s].Retint(city, hull, generator, scratch);
            }
        }

        /// <summary>Sends the colours of the chunks whose tint changed to their meshes; a few compares while none did.</summary>
        public void Refresh()
        {
            for (int s = 0; s < _sets.Length; s++)
            {
                for (int k = 0; k < _blocks[s].Length; k++)
                {
                    Map25Chunk chunk = _sets[s].Blocks[k];
                    if (chunk.ColourVersion != _seen[s][k] && _blocks[s][k] != null && chunk.Col != null)
                    {
                        _blocks[s][k].SetVertexBufferData(chunk.Col, 0, 0, chunk.Vertices * 4, 1, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                        _seen[s][k] = chunk.ColourVersion;
                        chunk.DirtyFrom = int.MaxValue;
                        chunk.DirtyTo = -1;
                    }
                }
            }
        }

        /// <summary>
        /// Draws the picture into <paramref name="target"/> as <paramref name="view"/> sees it. Everything out of view is left out whole
        /// chunks at a time. Order: water, land, roads, shadows, blocks (the only things that write depth), the route, its faint
        /// copy over the blocks, the player's cone and halo, and the distance's fade.
        /// </summary>
        public void Draw(RenderTexture target, Map25View view, in Map25Frame frame, int detail)
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
            cmd.DrawMesh(_water, Matrix4x4.identity, _flat, 0, 0);
            for (int k = 0; k < _landMesh.Length; k++)
            {
                if (_landMesh[k] != null && view.Sees(_land[k], false))
                {
                    cmd.DrawMesh(_landMesh[k], Matrix4x4.identity, _flat, 0, 0);
                }
            }
            for (int k = 0; k < _roadMesh.Length; k++)
            {
                if (_roadMesh[k] != null && view.Sees(_roads[k], false))
                {
                    cmd.DrawMesh(_roadMesh[k], Matrix4x4.identity, _flat, 0, 0);
                }
            }
            if (frame.Player)
            {
                // on the ground, under the blocks: a tall block hides the part of the cone behind it
                Matrix4x4 at = Matrix4x4.TRS(new Vector3(frame.PlayerX, frame.PlayerY, frame.PlayerZ), Quaternion.Euler(0f, frame.Heading, 0f), new Vector3(frame.ConeRadius, 1f, frame.ConeRadius));
                cmd.DrawMesh(_cone, at, _blend, 0, 0);
                cmd.DrawMesh(_halo, at, _blend, 0, 0);
            }
            Matrix4x4 rise = Matrix4x4.identity;
            rise.m11 = view.Rise;
            int which = detail < _sets.Length ? detail : 0;
            Map25Set set = _sets[which];
            Mesh[] shadows = _shadows[which];
            Mesh[] blocks = _blocks[which];
            if (view.Rise > 0.1f)
            {
                for (int k = 0; k < shadows.Length; k++)
                {
                    if (shadows[k] != null && view.Sees(set.Shadows[k], false))
                    {
                        cmd.DrawMesh(shadows[k], Matrix4x4.identity, _blend, 0, 0);
                    }
                }
            }
            for (int k = 0; k < blocks.Length; k++)
            {
                if (blocks[k] != null && view.Sees(set.Blocks[k], true))
                {
                    cmd.DrawMesh(blocks[k], rise, _solid, 0, 0);
                }
            }
            if (_routeOn)
            {
                cmd.DrawMesh(_route, Matrix4x4.identity, _solid, 0, 0);
                cmd.DrawMesh(_ghost, Matrix4x4.identity, _blend, 0, 0);
            }
            if (frame.Fog)
            {
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.identity, IntoTexture));
                cmd.DrawMesh(_fog, Matrix4x4.identity, _blend, 0, 0);
            }
            Graphics.ExecuteCommandBuffer(cmd);
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
            var groups = new System.Collections.Generic.List<Mesh[]> { _landMesh, _roadMesh };
            groups.AddRange(_blocks);
            groups.AddRange(_shadows);
            foreach (Mesh[] group in groups)
            {
                for (int i = 0; i < group.Length; i++)
                {
                    DestroyMesh(ref group[i]);
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
