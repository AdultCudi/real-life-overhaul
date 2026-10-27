using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The districts' edges in the game (<see cref="EdgeRules"/>): behind every stretch of edge a bank of the game's own border fog
    /// (one mesh, one draw call: four vertical layers that thicken over 12 m and a lid at the height of the game's own fog sheet,
    /// on a copy of the game's fog material whose depth test is the usual one, so the city stands in front of it), and every street
    /// end closed with a box on the walls' layer and a row of construction fencing, roadworks signs and cones, the mod's own props
    /// (<c>reallife_props</c>), one object each. The fog's material is the game's (MapBorder/BorderOverlayFog's: Shader Graphs/BorderFog,
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

        /// <summary>The barricade boxes standing, or 0.</summary>
        public static int EdgeWalls => _edgeWalls;

        /// <summary>The stretches of edge that have fog behind them, or 0.</summary>
        public static int EdgeFogSegments => _edgeSegments;

        /// <summary>The fog's vertical layers standing, or 0.</summary>
        public static int EdgeFogCurtains => _edgeCurtains;

        /// <summary>The barricades' pieces standing, or 0.</summary>
        public static int EdgePieces => _edgePieces;

        /// <summary>The fog's vertices as the mesh holds them, or 0 without it.</summary>
        public static int EdgeFogVertices => _edgeFogMesh != null ? _edgeFogMesh.vertexCount : 0;

        /// <summary>The fog's triangles as the mesh holds them, or 0 without it.</summary>
        public static int EdgeFogTriangles => _edgeFogMesh != null ? (int)_edgeFogMesh.GetIndexCount(0) / 3 : 0;

        /// <summary>
        /// Stands the barricades' boxes (the walls' layer, as the invisible walls round the districts) and the fog behind the edges.
        /// Null when both stand; else why the fog does not, the boxes standing all the same. Nothing stands without the walls.
        /// </summary>
        public static string LayEdges(List<Barricade> ends, EdgeFogData fog)
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
            int props = Layer(PropsLayer);
            for (int i = 0; i < ends.Count; i++)
            {
                PlanBox wall = ends[i].Wall;
                var made = new GameObject("barricade " + ends[i].Name);
                made.layer = props;
                made.transform.SetParent(_edges.transform, false);
                made.transform.position = new Vector3(wall.X, wall.Top * 0.5f, wall.Z);
                made.AddComponent<BoxCollider>().size = new Vector3(wall.HalfX * 2f, wall.Top, wall.HalfZ * 2f);
                _edgeWalls++;
            }
            return StandFog(fog);
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
        /// Stands the barricades' pieces from the props bundle: null when they all stand; else why not (the bundle not loaded, a prop missing),
        /// what stood before kept. Needs the edges laid first.
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
            if (!PropLibrary.Ready)
            {
                return "the props bundle is not loaded";
            }
            for (int k = 0; k < EdgeRules.PropIds.Length; k++)
            {
                if (!PropLibrary.Has(EdgeRules.PropIds[k]))
                {
                    return "the props bundle has no " + EdgeRules.PropIds[k];
                }
            }
            _pieces = new GameObject("barricade pieces");
            _pieces.transform.SetParent(_edges.transform, false);
            var places = new List<PiecePlace>(24);
            for (int i = 0; i < ends.Count; i++)
            {
                EdgeRules.Pieces(ends[i], places);
                for (int p = 0; p < places.Count; p++)
                {
                    string id = EdgeRules.PropOf(places[p].Kind);
                    if (!PropLibrary.TryGet(id, out Mesh mesh, out Material material))
                    {
                        return "the props bundle gave no " + id;
                    }
                    var piece = new GameObject(id);
                    piece.transform.SetParent(_pieces.transform, false);
                    piece.transform.SetPositionAndRotation(new Vector3(places[p].X, UptownLots.GroundY, places[p].Z), Quaternion.Euler(0f, places[p].Yaw, 0f));
                    piece.transform.localScale = new Vector3(places[p].Stretch, 1f, 1f);
                    piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var draw = piece.AddComponent<MeshRenderer>();
                    draw.sharedMaterial = material;
                    draw.lightProbeUsage = LightProbeUsage.Off;
                    draw.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    _edgePieces++;
                }
            }
            return null;
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
        }
    }
}
