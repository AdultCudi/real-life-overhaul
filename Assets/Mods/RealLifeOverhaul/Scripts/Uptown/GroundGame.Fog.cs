using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The city map's fog over the city's border, the game's own (MapBorder/BorderOverlayFog: a sheet 46 m up with the city
    /// cut out of it, faded in from the hole's edge by its shader), cut away over Uptown. The map draws it: its layer, Default,
    /// is in the map's mask. The street camera has it too but stands under it, and sees only its back faces, which its pass
    /// culls. Its mesh cannot be read at run time, so the cut was made offline from Build 3682's (<see cref="LayoutFog"/>).
    /// Once the map's bounds reach over the band, the fog under the backdrop is matched against what that was made from: its
    /// mesh's name, vertices, indices, parts and bounds, and where it stands. Only then does the made mesh go on its filter,
    /// the game's kept to give back; a fog that differs (a free update) is left as the game has it, with why. The game's comes
    /// back and the made one goes on every way out: the district given back or forgotten, the feature's clean-up and the city
    /// going. Unity alone, no type of the game's. COST: one mesh of some 400 vertices made once a load, behind the loading
    /// screen; nothing per frame.
    /// </summary>
    public static partial class GroundGame
    {
        private const string FogName = "BorderOverlayFog";              // the game's fog, a child of its backdrop
        private const string MadeFogName = "RealLifeOverhaul Uptown fog";   // the cut, named for the mod in the session's record
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The game's own most common tangent on its fog; the fog's pass never reads one, so the made vertices take it.
        private static readonly Vector4 MadeTangent = new Vector4(1f, 0f, 0f, -1f);

        private static MeshFilter _fog;      // the game's fog object while the cut is on it
        private static Mesh _fogGame;        // its own mesh, given back
        private static Mesh _fogMade;        // the cut, destroyed when it is given back

        /// <summary>True while the cut is the mesh the game's fog draws.</summary>
        public static bool FogCut => _fog != null && _fogMade != null && _fog.sharedMesh == _fogMade;

        /// <summary>
        /// Cuts the band out of the map's fog, once the backdrop is found: null when it is cut (or already was at this load),
        /// else why not, in words for the log, the game's fog left as it was.
        /// </summary>
        public static string CutFog()
        {
            if (FogCut)
            {
                return null;
            }
            FogMeasured = null;
            Transform fog = _backdrop != null ? _backdrop.Find(FogName) : null;
            if (fog == null || !fog.TryGetComponent(out MeshFilter filter))
            {
                return "the game's fog (" + BackdropName + "/" + FogName + ") was not found";
            }
            string differs = FogDiffers(filter);
            if (differs != null)
            {
                return differs;
            }
            _fog = filter;
            _fogGame = filter.sharedMesh;
            MakeFog();
            filter.sharedMesh = _fogMade;
            return null;
        }

        /// <summary>The game's own fog back on its object and the cut destroyed; nothing when the fog is not cut.</summary>
        public static void UncutFog()
        {
            // Only the cut is taken off: a mesh something else has put on the fog since is not the mod's to replace.
            if (_fog != null && _fogGame != null && _fog.sharedMesh == _fogMade)
            {
                _fog.sharedMesh = _fogGame;
            }
            if (_fogMade != null)
            {
                Object.Destroy(_fogMade);
            }
            _fog = null;
            _fogGame = null;
            _fogMade = null;
        }

        /// <summary>The fog as it was measured against Build 3682's at this load, for the load's check line.</summary>
        public static string FogMeasured { get; private set; }

        // Build 3682's fog as its mesh tells itself without read access, and where it stands: null when this is it. Each
        // figure is one the cut was made from, so a free update that changes the fog stands the cut down before it is drawn.
        private static string FogDiffers(MeshFilter filter)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                FogMeasured = "the game's fog has no mesh";
                return FogMeasured;
            }
            Bounds bounds = mesh.bounds;
            Transform at = filter.transform;
            Vector3 place = at.position, scale = at.lossyScale;
            float turn = Quaternion.Angle(at.rotation, Quaternion.identity);
            uint indices = mesh.subMeshCount > 0 ? mesh.GetIndexCount(0) : 0u;
            FogMeasured = "its mesh " + mesh.name + ", " + mesh.vertexCount + " vertices, " + indices + " indices in " + mesh.subMeshCount + " part(s), bounds centre ("
                          + Metres(bounds.center) + ") extent (" + Metres(bounds.extents) + "), standing at (" + Metres(place) + ") turned " + turn.ToString("0.###", Inv)
                          + " degrees, scaled (" + scale.x.ToString("0.###", Inv) + ", " + scale.y.ToString("0.###", Inv) + ", " + scale.z.ToString("0.###", Inv) + ")";
            bool same = mesh.name == LayoutFog.GameName && mesh.vertexCount == LayoutFog.GameVertexCount && mesh.subMeshCount == 1
                        && mesh.GetTopology(0) == MeshTopology.Triangles && indices == LayoutFog.GameIndexCount
                        && Near(bounds.center, LayoutFog.GameCentreX, LayoutFog.GameCentreY, LayoutFog.GameCentreZ)
                        && Near(bounds.extents, LayoutFog.GameExtentX, LayoutFog.GameExtentY, LayoutFog.GameExtentZ)
                        && Near(place, LayoutFog.PlaceX, LayoutFog.PlaceY, LayoutFog.PlaceZ) && turn <= LayoutFog.Near
                        && Mathf.Abs(scale.x - 1f) <= 0.0001f && Mathf.Abs(scale.y - 1f) <= 0.0001f && Mathf.Abs(scale.z - 1f) <= 0.0001f;
            return same ? null : "the game's fog is not the one the cut was made from (Build 3682's " + LayoutFog.GameName + ", " + LayoutFog.GameVertexCount + " vertices, "
                                 + LayoutFog.GameIndexCount + " indices in one part, at (" + LayoutFog.PlaceX.ToString("0.###", Inv) + ", " + LayoutFog.PlaceY.ToString("0.###", Inv)
                                 + ", " + LayoutFog.PlaceZ.ToString("0.###", Inv) + ")): " + FogMeasured;
        }

        private static bool Near(Vector3 v, float x, float y, float z)
        {
            return Mathf.Abs(v.x - x) <= LayoutFog.Near && Mathf.Abs(v.y - y) <= LayoutFog.Near && Mathf.Abs(v.z - z) <= LayoutFog.Near;
        }

        private static string Metres(Vector3 v)
        {
            return v.x.ToString("0.###", Inv) + ", " + v.y.ToString("0.###", Inv) + ", " + v.z.ToString("0.###", Inv);
        }

        // The cut as a mesh, made straight into the field UncutFog destroys: the game's vertices first at their own indices,
        // the made after them, every attribute the game's mesh has, and its triangles the game's kept and the made. Left
        // readable, so its test can read it back.
        private static void MakeFog()
        {
            int count = LayoutFog.VertexCount, gameCount = LayoutFog.GameVertexCount;
            var positions = new Vector3[count];
            var normals = new Vector3[count];
            var tangents = new Vector4[count];
            var uv0 = new Vector2[count];
            var uv1 = new Vector2[count];
            float[] game = LayoutFog.Game;
            for (int i = 0; i < gameCount; i++)
            {
                int k = i * LayoutFog.GameStride;
                positions[i] = new Vector3(game[k], game[k + 1], game[k + 2]);
                uv0[i] = new Vector2(game[k + 3], game[k + 4]);
                uv1[i] = new Vector2(game[k + 5], game[k + 6]);
                tangents[i] = new Vector4(game[k + 7], game[k + 8], game[k + 9], game[k + 10]);
                normals[i] = Vector3.up;
            }
            float[] made = LayoutFog.Made;
            for (int i = gameCount; i < count; i++)
            {
                int k = (i - gameCount) * LayoutFog.MadeStride;
                float x = made[k], z = made[k + 1], u = made[k + 2];
                positions[i] = new Vector3(x, LayoutFog.Height, z);
                uv0[i] = new Vector2(u, LayoutFog.UvY(u));
                uv1[i] = new Vector2(LayoutFog.PlanarU(x), LayoutFog.PlanarV(z));
                tangents[i] = MadeTangent;
                normals[i] = Vector3.up;
            }
            _fogMade = new Mesh { name = MadeFogName };
            _fogMade.SetVertices(positions);
            _fogMade.SetNormals(normals);
            _fogMade.SetTangents(tangents);
            _fogMade.SetUVs(0, uv0);
            _fogMade.SetUVs(1, uv1);
            _fogMade.SetIndices(LayoutFog.Triangles, MeshTopology.Triangles, 0);
            _fogMade.RecalculateBounds();
        }

        /// <summary>
        /// For the fog's test: how many of the rectangles' middles lie under a triangle of the cut as the fog draws it, read
        /// back from the mesh on the fog and placed where the fog stands; -1 while the cut is not on the fog. Its vertices and
        /// triangles are given out too.
        /// </summary>
        public static int FogOver(List<GroundRect> rects, out int vertices, out int triangles)
        {
            vertices = 0;
            triangles = 0;
            if (!FogCut)
            {
                return -1;
            }
            Mesh mesh = _fog.sharedMesh;
            Matrix4x4 world = _fog.transform.localToWorldMatrix;
            var points = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(points);
            int[] index = mesh.GetIndices(0);
            for (int i = 0; i < points.Count; i++)
            {
                points[i] = world.MultiplyPoint3x4(points[i]);
            }
            vertices = points.Count;
            triangles = index.Length / 3;
            int over = 0;
            for (int r = 0; r < rects.Count; r++)
            {
                float x = (rects[r].X1 + rects[r].X2) * 0.5f, z = (rects[r].Z1 + rects[r].Z2) * 0.5f;
                for (int t = 0; t + 2 < index.Length; t += 3)
                {
                    if (Under(points[index[t]], points[index[t + 1]], points[index[t + 2]], x, z))
                    {
                        over++;
                        break;
                    }
                }
            }
            return over;
        }

        // A point under a triangle seen from above, its edges included.
        private static bool Under(Vector3 a, Vector3 b, Vector3 c, float x, float z)
        {
            float d1 = (b.x - a.x) * (z - a.z) - (b.z - a.z) * (x - a.x);
            float d2 = (c.x - b.x) * (z - b.z) - (c.z - b.z) * (x - b.x);
            float d3 = (a.x - c.x) * (z - c.z) - (a.z - c.z) * (x - c.x);
            bool anyBelow = d1 < 0f || d2 < 0f || d3 < 0f, anyAbove = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(anyBelow && anyAbove);
        }
    }
}
