using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The districts' edges in the game (<see cref="EdgeRules"/>): behind every stretch of edge a bank of the game's own border fog
    /// (one mesh, one draw call: four vertical layers that thicken over 12 m and a lid at the height of the game's own fog sheet,
    /// on a copy of the game's fog material whose depth test is the usual one, so the city stands in front of it), and every street
    /// end closed with a box on the walls' layer whose first face is the drawn fencing's face (a row of construction fencing, roadworks
    /// signs and cones, the mod's own props (<c>reallife_props</c>), one object each; the fencing drawn plain, from the mod's own
    /// shapes, where the bundle cannot give it, so no box ever stands without a drawn face). The fog's material is the game's (MapBorder/BorderOverlayFog's: Shader Graphs/BorderFog,
    /// HDRP unlit, premultiplied, alpha from uv0.x and its noise) and is matched first on its shader and the properties the copy
    /// changes; a fog that differs is left alone with one line. Everything hangs under the ground's object of the mod's own, so it goes
    /// with the city; the mesh and the material are destroyed with it. Called at the load and by the tests, never per frame.
    /// COST: one mesh of some 250 vertices made once a load, 27 boxes and about 330 small objects; the fog is one draw call, thin to
    /// draw (four layers of a shader that reads a noise map), and the pieces are the SRP batcher's.
    /// </summary>
    public static partial class GroundGame
    {
        private const string EdgeFogShader = "Shader Graphs/BorderFog";      // the game's fog shader, which the copy is matched on
        private const string EdgeFogName = "RealLifeOverhaul Uptown edge fog";
        private const string EdgesName = "RealLifeOverhaul Uptown edges";
        private const string ZTestForward = "_ZTestDepthEqualForOpaque";     // the fog pass's depth test (Always in the game's: it is drawn over everything from the map)
        private const string ZTestTransparent = "_ZTestTransparent";         // and the setting it comes from
        private static readonly string[] EdgeFogProperties = { ZTestForward, ZTestTransparent, "_Threshold", "_EdgeSmooth" };

        private static GameObject _edges;            // the edges' object, under the ground's
        private static GameObject _pieces;           // the barricades' pieces, under the edges'
        private static Mesh _edgeFogMesh;
        private static Material _edgeFogMaterial;
        private static int _edgeWalls;               // barricade boxes standing
        private static int _edgeSegments;            // stretches of edge with fog
        private static int _edgeCurtains;            // vertical layers in the fog
        private static int _edgePieces;              // pieces standing
        private static int _edgePlain;               // of those, fencing panels drawn plain (the bundle could not give the model)
        private static readonly List<Bounds> FenceBounds = new List<Bounds>(32);   // each barricade's drawn panels together (one a barricade, in the list's order; empty where none stands)

        /// <summary>The barricade boxes standing, or 0.</summary>
        public static int EdgeWalls => _edgeWalls;

        /// <summary>The stretches of edge that have fog behind them, or 0.</summary>
        public static int EdgeFogSegments => _edgeSegments;

        /// <summary>The fog's vertical layers standing, or 0.</summary>
        public static int EdgeFogCurtains => _edgeCurtains;

        /// <summary>The barricades' pieces standing, or 0.</summary>
        public static int EdgePieces => _edgePieces;

        /// <summary>Of those, the fencing panels drawn plain because the props bundle could not give the model, or 0.</summary>
        public static int EdgePlain => _edgePlain;

        /// <summary>The fog's vertices as the mesh holds them, or 0 without it.</summary>
        public static int EdgeFogVertices => _edgeFogMesh != null ? _edgeFogMesh.vertexCount : 0;

        /// <summary>The fog's triangles as the mesh holds them, or 0 without it.</summary>
        public static int EdgeFogTriangles => _edgeFogMesh != null ? (int)_edgeFogMesh.GetIndexCount(0) / 3 : 0;

        /// <summary>
        /// Stands the edges' object and the fog behind them. Null when the fog stands; else why it does not. The barricades' boxes are
        /// laid with their fencing (<see cref="LayPieces"/>), never before it: a box stands only with a drawn face to meet a car at.
        /// Nothing stands without the walls.
        /// </summary>
        public static string LayEdges(EdgeFogData fog)
        {
            if (_ground == null)
            {
                return "the walls and colliders were not built";
            }
            if (_edges != null)
            {
                return null;
            }
            _edges = new GameObject(EdgesName);
            _edges.transform.SetParent(_ground.transform, false);
            return StandFog(fog);
        }

        // A barricade's box on the walls' layer, as the invisible walls round the districts: its first face is its fencing's drawn face.
        private static void StandBox(Barricade end)
        {
            PlanBox wall = end.Wall;
            var made = new GameObject("barricade " + end.Name);
            made.layer = Layer(PropsLayer);
            made.transform.SetParent(_edges.transform, false);
            made.transform.position = new Vector3(wall.X, wall.Top * 0.5f, wall.Z);
            made.AddComponent<BoxCollider>().size = new Vector3(wall.HalfX * 2f, wall.Top, wall.HalfZ * 2f);
            _edgeWalls++;
        }

        // The fog: the game's fog material copied (matched first), the mesh made from the data, one renderer.
        private static string StandFog(EdgeFogData fog)
        {
            Transform game = _backdrop != null ? _backdrop.Find(FogName) : null;
            if (game == null || !game.TryGetComponent(out MeshRenderer renderer) || renderer.sharedMaterial == null)
            {
                return "the game's fog (" + BackdropName + "/" + FogName + ") has no material to copy";
            }
            Material source = renderer.sharedMaterial;
            string shader = source.shader != null ? source.shader.name : "none";
            if (shader != EdgeFogShader)
            {
                return "the game's fog material is on the shader " + shader + ", not " + EdgeFogShader;
            }
            for (int i = 0; i < EdgeFogProperties.Length; i++)
            {
                if (!source.HasProperty(EdgeFogProperties[i]))
                {
                    return "the game's fog material has no " + EdgeFogProperties[i];
                }
            }
            _edgeFogMaterial = new Material(source) { name = EdgeFogName };
            _edgeFogMaterial.SetFloat(ZTestForward, (float)CompareFunction.LessEqual);
            _edgeFogMaterial.SetFloat(ZTestTransparent, (float)CompareFunction.LessEqual);
            int count = fog.Vertices;
            var positions = new List<Vector3>(count);
            var normals = new List<Vector3>(count);
            var tangents = new List<Vector4>(count);
            var uv0 = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
            {
                positions.Add(new Vector3(fog.X[i], fog.Y[i], fog.Z[i]));
                normals.Add(new Vector3(fog.NormalX[i], fog.NormalY[i], fog.NormalZ[i]));
                tangents.Add(MadeTangent);
                uv0.Add(new Vector2(fog.U[i], LayoutFog.UvY(fog.U[i])));
            }
            _edgeFogMesh = new Mesh { name = EdgeFogName };
            _edgeFogMesh.SetVertices(positions);
            _edgeFogMesh.SetNormals(normals);
            _edgeFogMesh.SetTangents(tangents);
            _edgeFogMesh.SetUVs(0, uv0);
            _edgeFogMesh.SetTriangles(fog.Triangles, 0);
            _edgeFogMesh.RecalculateBounds();
            _edgeFogMesh.UploadMeshData(true);
            var made = new GameObject("fog");
            made.transform.SetParent(_edges.transform, false);
            made.AddComponent<MeshFilter>().sharedMesh = _edgeFogMesh;
            var draw = made.AddComponent<MeshRenderer>();
            draw.sharedMaterial = _edgeFogMaterial;
            draw.shadowCastingMode = ShadowCastingMode.Off;
            draw.receiveShadows = false;
            draw.lightProbeUsage = LightProbeUsage.Off;
            draw.reflectionProbeUsage = ReflectionProbeUsage.Off;
            draw.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            draw.allowOcclusionWhenDynamic = false;
            _edgeSegments = fog.Segments;
            _edgeCurtains = fog.Curtains;
            return null;
        }

        /// <summary>
        /// Stands the barricades' pieces from the props bundle: null when they all stand; else why not (the bundle not loaded, a prop
        /// missing). Where the bundle cannot give a panel it is drawn plain, a box in the colour of the fencing, so the box behind it is
        /// never without a drawn face; the signs and cones, which the boxes do not need, are left out then. Needs the edges laid first.
        /// </summary>
        public static string LayPieces(List<Barricade> ends)
        {
            if (_edges == null)
            {
                return "the edges were not laid";
            }
            if (_pieces != null)
            {
                return null;
            }
            string why = null;
            if (!PropLibrary.Ready)
            {
                why = "the props bundle is not loaded";
            }
            for (int k = 0; why == null && k < EdgeRules.PropIds.Length; k++)
            {
                why = PropLibrary.Has(EdgeRules.PropIds[k]) ? null : "the props bundle has no " + EdgeRules.PropIds[k];
            }
            _pieces = new GameObject("barricade pieces");
            _pieces.transform.SetParent(_edges.transform, false);
            var places = new List<PiecePlace>(24);
            FenceBounds.Clear();
            for (int i = 0; i < ends.Count; i++)
            {
                EdgeRules.Pieces(ends[i], places);
                bool any = false;
                Bounds fence = default;
                for (int p = 0; p < places.Count; p++)
                {
                    string id = EdgeRules.PropOf(places[p].Kind);
                    Mesh mesh = null;
                    Material material = null;
                    bool modelled = why == null && PropLibrary.TryGet(id, out mesh, out material) && mesh != null && material != null;
                    if (!modelled && why == null)
                    {
                        why = "the props bundle gave no " + id;
                    }
                    GameObject piece = modelled ? Modelled(id, mesh, material, places[p]) : places[p].Kind == PieceKind.Hoarding ? Plain(places[p]) : null;
                    if (piece == null)
                    {
                        continue;
                    }
                    _edgePieces++;
                    _edgePlain += modelled ? 0 : 1;
                    if (places[p].Kind == PieceKind.Hoarding)
                    {
                        Renderer drawn = piece.GetComponentInChildren<Renderer>();
                        if (drawn != null)
                        {
                            fence = any ? Union(fence, drawn.bounds) : drawn.bounds;
                            any = true;
                        }
                    }
                }
                FenceBounds.Add(any ? fence : default);
                if (any)
                {
                    StandBox(ends[i]);   // with its fencing, never before it: the box's first face is the drawn face
                }
            }
            return why;
        }

        private static Bounds Union(Bounds a, Bounds b)
        {
            a.Encapsulate(b);
            return a;
        }

        // A piece of the bundle at its place: the model's mesh and material, shared, no collider.
        private static GameObject Modelled(string id, Mesh mesh, Material material, PiecePlace place)
        {
            var piece = new GameObject(id);
            piece.transform.SetParent(_pieces.transform, false);
            piece.transform.SetPositionAndRotation(new Vector3(place.X, UptownLots.GroundY, place.Z), Quaternion.Euler(0f, place.Yaw, 0f));
            piece.transform.localScale = new Vector3(place.Stretch, 1f, 1f);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var draw = piece.AddComponent<MeshRenderer>();
            draw.sharedMaterial = material;
            draw.lightProbeUsage = LightProbeUsage.Off;
            draw.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return piece;
        }

        // A panel of fencing drawn plain where the bundle cannot give the model: a box as the prop is (2.4 m wide, 2.4 m high, 0.10 m deep from
        // 0.06 behind its origin to 0.04 before it), orange; null where the game has no shader to draw it with.
        private static GameObject Plain(PiecePlace place)
        {
            Material paint = Shapes.Paint(new Color(0.93f, 0.46f, 0.07f));
            if (paint == null)
            {
                return null;
            }
            var holder = new GameObject("site_hoarding (plain)");
            holder.transform.SetParent(_pieces.transform, false);
            holder.transform.SetPositionAndRotation(new Vector3(place.X, UptownLots.GroundY, place.Z), Quaternion.Euler(0f, place.Yaw, 0f));
            Shapes.Piece("panel", holder.transform, Shapes.Cube, paint, new Vector3(0f, EdgeRules.PanelWidth * 0.5f, (EdgeRules.FenceFace - EdgeRules.FenceBack) * 0.5f),
                         new Vector3(EdgeRules.PanelWidth * place.Stretch, EdgeRules.PanelWidth, EdgeRules.FenceFace + EdgeRules.FenceBack));
            return holder;
        }

        /// <summary>
        /// How far each barricade's box stands before the drawn face of its fencing, read back from what stands: the largest gap in
        /// metres (0 when the box's first face is the drawn face), the first end whose gap is outside 0 to <paramref name="most"/> or that has no
        /// fencing drawn, in words, or null when all are as wanted. A car meets the box's first face; a gap is the stretch of invisible
        /// wall before the fencing.
        /// </summary>
        public static string FenceMeetsBox(List<Barricade> ends, float most, out float worst)
        {
            worst = 0f;
            if (FenceBounds.Count != ends.Count)
            {
                return "the fencing was not laid";
            }
            for (int i = 0; i < ends.Count; i++)
            {
                Barricade b = ends[i];
                Bounds fence = FenceBounds[i];
                if (fence.size == Vector3.zero)
                {
                    return "no fencing is drawn at " + b.Name;
                }
                float drawn = Along(b, fence.min.x, fence.min.z, fence.max.x, fence.max.z);
                float box = Along(b, b.Wall.X - b.Wall.HalfX, b.Wall.Z - b.Wall.HalfZ, b.Wall.X + b.Wall.HalfX, b.Wall.Z + b.Wall.HalfZ);
                float gap = drawn - box;
                worst = Mathf.Max(worst, gap);
                if (gap < -0.02f || gap > most)
                {
                    return b.Name + ": the box's first face is " + gap.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m before the drawn face";
                }
            }
            return null;
        }

        // The nearest a rectangle comes to the end's edge along the way out (m past the edge).
        private static float Along(Barricade b, float x0, float z0, float x1, float z1)
        {
            float a = (x0 - b.Cx) * b.OutX + (z0 - b.Cz) * b.OutZ, c = (x1 - b.Cx) * b.OutX + (z1 - b.Cz) * b.OutZ;
            float d = (x0 - b.Cx) * b.OutX + (z1 - b.Cz) * b.OutZ, e = (x1 - b.Cx) * b.OutX + (z0 - b.Cz) * b.OutZ;
            return Mathf.Min(Mathf.Min(a, c), Mathf.Min(d, e));
        }

        /// <summary>The fog's mesh and material destroyed and the counts cleared; the objects go with the ground's.</summary>
        public static void DropEdges()
        {
            if (_edgeFogMesh != null)
            {
                Object.Destroy(_edgeFogMesh);
            }
            if (_edgeFogMaterial != null)
            {
                Object.Destroy(_edgeFogMaterial);
            }
            _edgeFogMesh = null;
            _edgeFogMaterial = null;
            _edges = null;
            _pieces = null;
            _edgeWalls = 0;
            _edgeSegments = 0;
            _edgeCurtains = 0;
            _edgePieces = 0;
            _edgePlain = 0;
            FenceBounds.Clear();
        }
    }
}
