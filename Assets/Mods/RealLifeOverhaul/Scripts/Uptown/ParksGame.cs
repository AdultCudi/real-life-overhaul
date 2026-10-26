using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The Game file of Uptown's parks, the only one of their files that touches the scene: the plan's two park squares laid
    /// from the game's own pieces, found by GamePieces as the campus's and the street furniture's are: free trees and
    /// bushes from the city's vegetation, the sittable bench, litter bin and street lamp of its pavements, and Madison
    /// Square Park's fountain and statue; the paths are a flat surface cut to ParksRules' rectangles and covered with the
    /// game's own pavement material (taken from Grand Central's ground), no model of the mod's. Everything is laid under one
    /// holder under the art's, made asleep and woken together, before the walking ground grows (so both meshes are grown
    /// round the trees, benches and fountain), and goes with the art when the city goes or the district stands down. A kind
    /// whose template is not found is left out with its name in the log. The bench is the game's OutsideBenchController
    /// whole, so the player sits on it and the game's pedestrians sit there as on Manhattan's. COST: nothing after the load.
    /// </summary>
    public static class ParksGame
    {
        public const string HolderName = "RealLifeOverhaul Uptown parks";   // under the art's holder: the ground's count of the game's colliders leaves it out

        private const float Reach = 40f;          // m from its fixed place a template must be: farther, and the game's scene has moved
        private const float PathRise = 0.09f;     // m above the ground the paving lies: over the block ground's plane, a hair under a walker's knee
        private const string PavementMaterial = "M_ConcretePavement_02";   // the game's pavement slabs on Grand Central's ground
        private const float FountainReach = 4.7f; // m: the fountain's basin, what keeps the walkers off it
        private const float FountainHeight = 2f;  // m: the height of the blocker that stands for it

        // Build 3682's pieces (the scene scan of 26 Oct 2026 over the game's level files): every one is drawn on its own, never in
        // a static batch, and each is looked for switched off or on. The trees are Vegetation's (the first four the lawns', the
        // maple a feature tree), the bench and bin those the streets use, the lamp the one of Street Lamp's, the fountain and the statue Madison
        // Square Park's, the last the ground the paving's material is taken from.
        private const int Bench = 0;
        private const int Tree = 1;       // five from here: the lawns' four, then the maple
        private const int Bush = 6;       // two from here
        private const int Bin = 8;
        private const int Lamp = 9;
        private const int Fountain = 10;
        private const int Statue = 11;
        private const int Pavement = 12;

        private static readonly PieceTemplate[] Templates =
        {
            new PieceTemplate
            {
                Root = GamePieces.BenchRoot, Name = GamePieces.BenchName, X = GamePieces.BenchX, Z = GamePieces.BenchZ, Y = 0.03f, Within = Reach,
                AltRoot = GamePieces.BenchAltRoot, AltX = GamePieces.BenchAltX, AltZ = GamePieces.BenchAltZ, Sittable = true
            },
            new PieceTemplate { Root = "Vegetation", Name = "Tree Broadleaf L1", X = -327.16f, Z = 313.58f, Y = 0f, Within = Reach },
            new PieceTemplate { Root = "Vegetation", Name = "Tree Birch", X = 4.05f, Z = -129.34f, Y = 0f, Within = Reach },
            new PieceTemplate { Root = "Vegetation", Name = "Tree Broadleaf M1", X = 440.06f, Z = -369.64f, Y = 0f, Within = Reach },
            new PieceTemplate { Root = "Vegetation", Name = "Tree Pine B M1", X = 315.32f, Z = -180.98f, Y = 0f, Within = Reach },
            new PieceTemplate { Root = "Vegetation", Name = "Tree Maple", X = -144.69f, Z = 441.56f, Y = 0f, Within = Reach },
            new PieceTemplate { Root = "Vegetation", Name = "Broadleaf Bush L1", X = -182.53f, Z = 313.33f, Y = 0f, Within = Reach },
            new PieceTemplate { Root = "Vegetation", Name = "Broadleaf Bush M1", X = -347.35f, Z = 313.26f, Y = 0f, Within = Reach },
            new PieceTemplate { Root = "Trash Cans", Name = "SM_Trash Can", X = -87.00f, Z = 453.90f, Y = 0.06f, Within = Reach },
            new PieceTemplate { Root = "Street Lamp", Name = "Prefab_StreetLamp", X = 200.40f, Z = -429.26f, Y = 0f, Within = Reach },
            new PieceTemplate { Root = "Madison Square Park", Name = "SM_Fountain", X = 262.11f, Z = 245.06f, Y = 0.03f, Within = Reach },
            new PieceTemplate { Root = "Madison Square Park", Name = "SM_Statue_A", X = 172.86f, Z = 238.58f, Y = 0f, Within = Reach },
            new PieceTemplate { Root = "AdditionalGroundPlanes", Name = "SM_GrandCentralStation GroundPlane 01", X = 47.53f, Z = 312.87f, Y = 0f, Within = Reach }
        };

        private static readonly GameObject[] Found = new GameObject[Templates.Length];
        private static readonly List<int> Blocks = new List<int>(4);
        private static readonly List<int> Standing = new List<int>(4);
        private static readonly List<ParkPiece> Pieces = new List<ParkPiece>(128);
        private static readonly List<PlanBox> Rects = new List<PlanBox>(10);
        private static readonly List<Mesh> Meshes = new List<Mesh>(4);
        private static readonly int[] LaidByKind = new int[7];
        private static readonly int[] PlannedByKind = new int[7];

        /// <summary>The pieces laid at this load, and those planned (a kind whose template was not found, or none at all, is the difference).</summary>
        public static int Laid { get; private set; }
        public static int Planned { get; private set; }

        /// <summary>The metres of paving laid, in squares.</summary>
        public static float Paved { get; private set; }

        /// <summary>The kinds whose template was not found at this load, in a word each, or null.</summary>
        public static string Missing { get; private set; }

        /// <summary>What was found and what was not at this load, for Player.log: "found: a, b; not found: c".</summary>
        public static string FoundWords { get; private set; }

        /// <summary>True when the paving's material was found and the paths laid.</summary>
        public static bool Pathed { get; private set; }

        /// <summary>The parks laid at this load (blocks, Row * 100 + Column).</summary>
        public static IReadOnlyList<int> Parks => Standing;

        /// <summary>True when the park on the block was laid at this load.</summary>
        public static bool Stands(int block) => Standing.Contains(block);

        /// <summary>The pieces of a kind laid in all the parks.</summary>
        public static int Count(ParkKind kind) => LaidByKind[(int)kind];

        /// <summary>The pieces of a kind the plan has in all the parks.</summary>
        public static int Plan(ParkKind kind) => PlannedByKind[(int)kind];

        /// <summary>
        /// Lays the parks under <paramref name="art"/>, the copied modules' holder: the templates found, every piece of the
        /// plan copied, the paving cut, all made asleep and woken together. Returns the pieces laid.
        /// </summary>
        public static int Lay(Transform art)
        {
            Forget();
            if (art == null)
            {
                Missing = "the art's holder";
                return 0;
            }
            Missing = GamePieces.Find(Templates, Found);
            FoundWords = GamePieces.Words(Templates, Found);
            ParksRules.Blocks(Blocks);
            var holder = new GameObject(HolderName);
            holder.SetActive(false);
            holder.transform.SetParent(art, false);
            Material paving = PavingOf();
            for (int b = 0; b < Blocks.Count; b++)
            {
                int pieces = LayPieces(Blocks[b], holder.transform);
                bool paths = paving != null && Pave(Blocks[b], holder.transform, paving);
                if (pieces > 0 || paths)
                {
                    Standing.Add(Blocks[b]);
                }
            }
            Pathed = paving != null;
            holder.SetActive(true);
            Physics.SyncTransforms();   // the trees and the fountain just woken stand where the ground is grown round them
            return Laid;
        }

        /// <summary>What was laid is forgotten (its pieces go with the art's holder): a new load begins.</summary>
        public static void Forget()
        {
            Laid = 0;
            Planned = 0;
            Paved = 0f;
            Pathed = false;
            Missing = null;
            FoundWords = null;
            Standing.Clear();
            Pieces.Clear();
            System.Array.Clear(LaidByKind, 0, LaidByKind.Length);
            System.Array.Clear(PlannedByKind, 0, PlannedByKind.Length);
            for (int i = 0; i < Meshes.Count; i++)
            {
                Mesh mesh = Meshes[i];
                if (mesh != null)
                {
                    Object.Destroy(mesh);
                }
            }
            Meshes.Clear();
            System.Array.Clear(Found, 0, Found.Length);
        }

        private static int TemplateOf(ParkPiece piece)
        {
            switch (piece.Kind)
            {
                case ParkKind.Tree: return Tree + piece.Variant % (ParksRules.TreeKinds + 1);
                case ParkKind.Bush: return Bush + piece.Variant % ParksRules.BushKinds;
                case ParkKind.Bench: return Bench;
                case ParkKind.Bin: return Bin;
                case ParkKind.Lamp: return Lamp;
                case ParkKind.Fountain: return Fountain;
                default: return Statue;
            }
        }

        // A park's pieces copied, each at its place on the ground.
        private static int LayPieces(int block, Transform parent)
        {
            ParksRules.Pieces(block, Pieces);
            int laid = 0;
            for (int i = 0; i < Pieces.Count; i++)
            {
                ParkPiece piece = Pieces[i];
                int t = TemplateOf(piece);
                PlannedByKind[(int)piece.Kind]++;
                Planned++;
                if (Found[t] == null)
                {
                    continue;
                }
                Vector3 at = new Vector3(piece.X, UptownLots.GroundY + Templates[t].Y, piece.Z);
                GameObject copy = GamePieces.Stand(Found[t], parent, HolderName + " " + block + " " + piece.Kind + " " + i, at, piece.Yaw);
                if (piece.Kind == ParkKind.Lamp)
                {
                    Unlit(copy);
                }
                else if (piece.Kind == ParkKind.Fountain)
                {
                    Basin(copy);
                }
                LaidByKind[(int)piece.Kind]++;
                Laid++;
                laid++;
            }
            return laid;
        }

        // A lamp copied from one that was lit stays dark until the game's own lamps are lit: the copy's own switch (which knows it
        // is off) turns its lights on at dusk, and off at dawn.
        private static void Unlit(GameObject lamp)
        {
            Light[] lights = lamp.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].enabled = false;
            }
        }

        // The fountain's basin is something solid the walking meshes are grown round and a walker stops at: the game's own blocker
        // stands in its copy, and one more of the mod's own on the default layer, in case the game's is on a layer the ground does not look at.
        private static void Basin(GameObject fountain)
        {
            var blocker = new GameObject(HolderName + " basin");
            blocker.transform.SetParent(fountain.transform, false);
            var capsule = blocker.AddComponent<CapsuleCollider>();
            capsule.radius = FountainReach;
            capsule.height = FountainHeight;
            capsule.center = new Vector3(0f, FountainHeight * 0.5f, 0f);
        }

        // The game's pavement material, from the ground it is found on; null where the ground or the material is not there.
        private static Material PavingOf()
        {
            return PavingFrom(Found[Pavement]);
        }

        // A park's paths laid as one flat surface, a quad to each rectangle, the pavement texture repeating every few metres
        // across the whole of it (by the ground's own x and z, so two rectangles meet without a seam).
        private static bool Pave(int block, Transform parent, Material material)
        {
            ParksRules.Paths(block, Rects);
            Mesh mesh = PavingMesh(Rects, HolderName + " paths " + block, out float area);
            Paved += area;
            Meshes.Add(mesh);
            var paving = new GameObject(HolderName + " paths " + block);
            paving.transform.SetParent(parent, false);
            paving.AddComponent<MeshFilter>().sharedMesh = mesh;
            var shown = paving.AddComponent<MeshRenderer>();
            shown.sharedMaterial = material;
            shown.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return true;
        }

        /// <summary>The game's pavement material, from the ground it is found on (the parks' and the campus's paths); null where the ground or the material is not there.</summary>
        public static Material PavingFrom(GameObject ground)
        {
            Renderer renderer = ground != null ? ground.GetComponent<Renderer>() : null;
            Material[] materials = renderer != null ? renderer.sharedMaterials : null;
            for (int i = 0; materials != null && i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].name == PavementMaterial)
                {
                    return materials[i];
                }
            }
            return null;
        }

        /// <summary>The ground the game's pavement material is taken from: the template a feature asks GamePieces for (Grand Central's ground plane).</summary>
        public static PieceTemplate PavingTemplate(int kind) => new PieceTemplate
        {
            Kind = kind, Root = "AdditionalGroundPlanes", Name = "SM_GrandCentralStation GroundPlane 01", X = 47.53f, Z = 312.87f, Y = 0f, Within = Reach
        };

        /// <summary>The height above the ground the game's pavement lies at: over the block ground's plane (its grass top is 0.0552 m, Build 3682), a hair under a walker's knee.</summary>
        public const float PavingRise = PathRise;

        /// <summary>
        /// A flat surface cut to rectangles on the ground, one quad each, the pavement texture repeating every few metres across the whole of it (by the
        /// ground's own x and z, so two rectangles meet without a seam). The mesh is the caller's to destroy; <paramref name="area"/> is the square metres.
        /// </summary>
        public static Mesh PavingMesh(List<PlanBox> rects, string name, out float area)
        {
            area = 0f;
            var vertices = new Vector3[rects.Count * 4];
            var uvs = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            var tangents = new Vector4[vertices.Length];
            var triangles = new int[rects.Count * 6];
            float y = UptownLots.GroundY + PathRise;
            for (int i = 0; i < rects.Count; i++)
            {
                PlanBox r = rects[i];
                float x0 = r.X - r.HalfX, z0 = r.Z - r.HalfZ, x1 = r.X + r.HalfX, z1 = r.Z + r.HalfZ;
                int v = i * 4;
                vertices[v] = new Vector3(x0, y, z0);
                vertices[v + 1] = new Vector3(x0, y, z1);
                vertices[v + 2] = new Vector3(x1, y, z1);
                vertices[v + 3] = new Vector3(x1, y, z0);
                for (int k = 0; k < 4; k++)
                {
                    uvs[v + k] = new Vector2(vertices[v + k].x / ParksRules.Tile, vertices[v + k].z / ParksRules.Tile);
                    normals[v + k] = Vector3.up;
                    tangents[v + k] = new Vector4(1f, 0f, 0f, 1f);
                }
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
                area += (x1 - x0) * (z1 - z0);
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
