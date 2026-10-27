using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The window overlays on the plain side walls Uptown's buildings show (SideRules; the owner, 27 Oct 2026): one quad a few
    /// centimetres off each wall, wearing the game's own backside window material (M_BuildBack_V1, V2, V4 or V5, found by name once a
    /// load behind the loading screen), a child of its building's root so it stands, hides and goes with it. Not drawn while the city map
    /// is open (the map shows the building's box). A building with no material to wear is left as it is, said once.
    /// COST: nothing per frame; a few hundred quads and their meshes made once a load, behind the loading screen; the map's opening and
    /// closing switch them once.
    /// </summary>
    internal static class SideWalls
    {
        private const string PanelName = "RealLife Uptown windows";

        private static readonly List<GameObject> Laid = new List<GameObject>(512);
        private static readonly List<Renderer> Drawn = new List<Renderer>(512);
        private static readonly List<Mesh> Meshes = new List<Mesh>(512);
        private static readonly Material[] Windows = new Material[SideRules.Styles.Length];
        private static readonly float[] Positions = new float[12];
        private static readonly float[] Uvs = new float[8];
        private static readonly int[] Triangles = new int[6];
        private static Action<bool> _onMap;
        private static string _found = "not looked for";

        /// <summary>Panels standing at this load.</summary>
        public static int Count => Laid.Count;

        /// <summary>Which of the game's window materials were found, for the log.</summary>
        public static string Found => _found;

        /// <summary>
        /// Lays an overlay on every open end of the pieces whose design has a plain side there that can be dressed: <paramref name="ends"/>
        /// from the census, <paramref name="roots"/> the transform each piece stands on (null for a piece to leave alone). Returns how many.
        /// </summary>
        public static int Lay(IList<LookPiece> pieces, IList<Transform> roots, IList<LookEnd> ends)
        {
            Clear();
            FindMaterials();
            int laid = 0;
            for (int i = 0; ends != null && i < ends.Count; i++)
            {
                LookEnd e = ends[i];
                Transform root = e.Piece >= 0 && e.Piece < roots.Count ? roots[e.Piece] : null;
                LookPiece p = pieces[e.Piece];
                if (root == null || e.Kind != SideKind.Dressable || !SideRules.Panel(p.Design, p.Ground, e.High, p.Id, out SidePanel panel))
                {
                    continue;
                }
                Material wear = Windows[panel.Style];
                if (wear == null)
                {
                    wear = FirstFound();
                }
                if (wear == null)
                {
                    continue;
                }
                Make(root, panel, wear);
                laid++;
            }
            if (laid > 0 && _onMap == null)
            {
                _onMap = Guard.Wrap<bool>(OnMap);
                GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Combine(GlobalEvents.onCityMapToggle, _onMap);
                if (CityMap.IsOpen)
                {
                    OnMap(true);
                }
            }
            return laid;
        }

        // The game's window materials by name: one search of what is in memory, only behind the loading screen.
        private static void FindMaterials()
        {
            Material[] all = SceneFinds.InMemory<Material>("side walls: the game's backside window materials by name");
            var said = new System.Text.StringBuilder(96);
            for (int s = 0; s < SideRules.Styles.Length; s++)
            {
                Windows[s] = null;
                for (int i = 0; i < all.Length && Windows[s] == null; i++)
                {
                    if (all[i] != null && all[i].shader != null && string.Equals(all[i].name, SideRules.Styles[s], StringComparison.Ordinal))
                    {
                        Windows[s] = all[i];
                    }
                }
                said.Append(s > 0 ? ", " : string.Empty).Append(SideRules.Styles[s]).Append(Windows[s] != null ? " found" : " not found");
            }
            _found = said.ToString();
        }

        private static Material FirstFound()
        {
            for (int s = 0; s < Windows.Length; s++)
            {
                if (Windows[s] != null)
                {
                    return Windows[s];
                }
            }
            return null;
        }

        private static void Make(Transform root, SidePanel panel, Material wear)
        {
            SideRules.Quad(panel, Positions, Uvs, Triangles);
            var vertices = new Vector3[4];
            var uv = new Vector2[4];
            var normals = new Vector3[4];
            var tangents = new Vector4[4];
            for (int i = 0; i < 4; i++)
            {
                vertices[i] = new Vector3(Positions[i * 3], Positions[i * 3 + 1], Positions[i * 3 + 2]);
                uv[i] = new Vector2(Uvs[i * 2], Uvs[i * 2 + 1]);
                normals[i] = new Vector3(panel.NX, 0f, panel.NZ);
                tangents[i] = new Vector4(-panel.NZ, 0f, panel.NX, -1f);   // along the wall to the viewer's right
            }
            var mesh = new Mesh { name = PanelName, vertices = vertices, uv = uv, normals = normals, tangents = tangents, triangles = (int[])Triangles.Clone() };
            mesh.RecalculateBounds();
            var go = new GameObject(PanelName);
            go.layer = root.gameObject.layer;
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = wear;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            Laid.Add(go);
            Drawn.Add(r);
            Meshes.Add(mesh);
        }

        // The city map shows each building's box, not its walls: the windows stand aside while it is open.
        private static void OnMap(bool open)
        {
            for (int i = 0; i < Drawn.Count; i++)
            {
                if (Drawn[i] != null)
                {
                    Drawn[i].enabled = !open;
                }
            }
        }

        /// <summary>Every panel taken away (a new load, the city going, the feature closing).</summary>
        public static void Clear()
        {
            if (_onMap != null)
            {
                GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Remove(GlobalEvents.onCityMapToggle, _onMap);
                _onMap = null;
            }
            for (int i = 0; i < Laid.Count; i++)
            {
                if (Laid[i] != null)
                {
                    UnityEngine.Object.Destroy(Laid[i]);
                }
            }
            foreach (Mesh mesh in Meshes)
            {
                if (mesh != null)
                {
                    UnityEngine.Object.Destroy(mesh);
                }
            }
            Laid.Clear();
            Drawn.Clear();
            Meshes.Clear();
        }

        /// <summary>The panels of the buildings of this load: how many wear each of the game's window materials, for the test's line.</summary>
        public static string Describe()
        {
            var counts = new int[Windows.Length];
            for (int i = 0; i < Drawn.Count; i++)
            {
                for (int s = 0; s < Windows.Length; s++)
                {
                    counts[s] += Drawn[i] != null && Drawn[i].sharedMaterial == Windows[s] && Windows[s] != null ? 1 : 0;
                }
            }
            var text = new System.Text.StringBuilder(96);
            for (int s = 0; s < Windows.Length; s++)
            {
                text.Append(s > 0 ? ", " : string.Empty).Append(SideRules.Styles[s]).Append(' ').Append(counts[s].ToString(CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }
    }
}
