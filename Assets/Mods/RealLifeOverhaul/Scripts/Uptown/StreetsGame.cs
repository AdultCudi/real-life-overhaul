using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The Game file of Uptown's street furniture, the only one of the streets' files that touches the scene: the game's own
    /// pavement bench, litter bin and three street trees in their holes from Manhattan's pavements, and its bicycle dock
    /// from Industry City's (Manhattan's docks and stands are in the scene's static batches, whose copies would draw the
    /// batch's mesh: Industry City's docks are not), each found once a load (the piece of its name nearest a fixed place of
    /// the game's scene, so every machine copies the same), copied at the places of StreetsRules.Furniture under the art's holder, so they go with the art when
    /// the city goes or the district stands down. They are laid behind the loading screen before the walking ground grows,
    /// so both meshes are grown round them as round the copied modules' lamps; a place where something already stands (a
    /// module's lamp or litter, a pole, an old scaffold) or where a car crosses into a big special place is left empty. The
    /// bench is the game's OutsideBenchController whole, so the game's own rest seats the player on it and its pedestrians
    /// sit there as on Manhattan's. COST: nothing after the load.
    /// </summary>
    public static class StreetsGame
    {
        public const string HolderName = "RealLifeOverhaul Uptown street furniture";   // under the art's holder: the ground's count of the game's colliders leaves it out

        private struct Template
        {
            public string Root;      // the scene root the game keeps the kind under
            public string Name;      // the piece's name, its copies' numbers aside
            public float X;          // the game's piece nearest here is taken
            public float Z;
            public Vector3 Half;     // the room it needs, half each way, turned with it: nothing of the game's may stand there
            public float Y;          // m: the height Manhattan's own pieces of the kind stand at on its pavements
        }

        // Build 3682's pieces on Manhattan's pavements (the navlab's scene scan, props_scan of 25 Oct 2026).
        private static readonly Template[] Templates =
        {
            new Template { Root = "SleepingBench", Name = "SleepingBench", X = 214.40f, Z = 99.39f, Half = new Vector3(0.95f, 0.6f, 0.4f), Y = 0.03f },
            new Template { Root = "Trash Cans", Name = "SM_Trash Can", X = -87.00f, Z = 453.90f, Half = new Vector3(0.35f, 0.6f, 0.35f), Y = 0.06f },
            new Template { Root = "Vegetation", Name = "SM_PavementHoleforTrees_Broadleaf M1", X = 148.67f, Z = 475.57f, Half = new Vector3(0.7f, 0.9f, 0.7f), Y = 0.05f },
            new Template { Root = "Vegetation", Name = "SM_PavementHoleforTrees_02 Birch", X = 346.53f, Z = 475.57f, Half = new Vector3(0.7f, 0.9f, 0.7f), Y = 0.05f },
            new Template { Root = "Vegetation", Name = "SM_PavementHoleforTrees_02 Broadleaf L1", X = 278.95f, Z = 328.38f, Half = new Vector3(0.7f, 0.9f, 0.7f), Y = 0.05f },
            new Template { Root = "Roads", Name = "Prefab_BikeRentalStand", X = -1852.53f, Z = -1283.52f, Half = new Vector3(0.3f, 0.5f, 0.3f), Y = 0.05f }
        };

        private const float TakeWithin = 40f;      // m from its fixed place the template must be: farther, and the game's scene has moved
        private const float ClearFrom = 0.15f;     // m above the pavement the room is looked at from: over its kerb, under nothing

        private static readonly GameObject[] Found = new GameObject[6];
        private static readonly List<GameObject> SceneRoots = new List<GameObject>(64);
        private static readonly List<StreetPlace> Plan = new List<StreetPlace>(1024);
        private static readonly List<TransitPlan.Route> Routes = new List<TransitPlan.Route>(4);
        private static int[,] _laid = new int[StreetsRules.FirstAvenue + 1, StreetsRules.Kinds];
        private static int _mask;

        /// <summary>The pieces laid at this load, of the plan's; those left as something stood there, off a big place's mouth, off a stop.</summary>
        public static int Laid { get; private set; }
        public static int Planned => Plan.Count;
        public static int Occupied { get; private set; }
        public static int Mouths { get; private set; }
        public static int OffStops { get; private set; }

        /// <summary>The kinds whose template was not found at this load, in a word each, or null.</summary>
        public static string Missing { get; private set; }

        /// <summary>The pieces of a kind laid on a street's pavements (StreetsRules.Counts' order).</summary>
        public static int Count(int street, StreetPiece kind)
        {
            return street >= 0 && street < _laid.GetLength(0) ? _laid[street, (int)kind] : 0;
        }

        /// <summary>
        /// Lays the band's furniture under <paramref name="art"/>, the copied modules' holder: the templates found, every
        /// place of the plan checked for room and copied, made asleep and woken together. Returns the pieces laid.
        /// </summary>
        public static int Furnish(Transform art)
        {
            Forget();
            if (art == null)
            {
                Missing = "the art's holder";
                return 0;
            }
            FindTemplates();
            StreetsRules.Routes(Routes);
            OffStops = StreetsRules.Furniture(Routes, Plan);
            _mask = LayerMask.GetMask("Default", "Buildings", "BuildingsOutlined", "BuildingWalls", "StreetProps", "InteractiveItems", "InteractiveItemsOutlined");
            var holder = new GameObject(HolderName);
            holder.SetActive(false);
            holder.transform.SetParent(art, false);
            Physics.SyncTransforms();   // the modules just woken stand where the room is looked for
            for (int i = 0; i < Plan.Count; i++)
            {
                StreetPlace place = Plan[i];
                int t = TemplateOf(place);
                GameObject template = Found[t];
                if (template == null)
                {
                    continue;
                }
                if (GroundGame.InMouth(place.X, place.Z))
                {
                    Mouths++;
                    continue;
                }
                Quaternion turn = Quaternion.Euler(0f, place.Yaw, 0f);
                float y = UptownLots.GroundY + Templates[t].Y;
                Vector3 half = Templates[t].Half;
                if (Physics.CheckBox(new Vector3(place.X, y + ClearFrom + half.y, place.Z), half, turn, _mask, QueryTriggerInteraction.Ignore))
                {
                    Occupied++;
                    continue;
                }
                GameObject copy = Object.Instantiate(template, holder.transform);
                copy.name = HolderName + " " + place.Kind + " " + i;
                copy.transform.SetPositionAndRotation(new Vector3(place.X, y, place.Z), turn);
                _laid[place.Street, (int)place.Kind]++;
                Laid++;
            }
            holder.SetActive(true);
            return Laid;
        }

        /// <summary>What was laid is forgotten (its pieces go with the art's holder): a new load begins.</summary>
        public static void Forget()
        {
            Laid = 0;
            Occupied = 0;
            Mouths = 0;
            OffStops = 0;
            Missing = null;
            Plan.Clear();
            _laid = new int[StreetsRules.FirstAvenue + 1, StreetsRules.Kinds];
            for (int i = 0; i < Found.Length; i++)
            {
                Found[i] = null;
            }
        }

        private static int TemplateOf(StreetPlace place)
        {
            switch (place.Kind)
            {
                case StreetPiece.Bench:
                    return 0;
                case StreetPiece.Bin:
                    return 1;
                case StreetPiece.Tree:
                    return 2 + place.Variant % StreetsRules.TreeKinds;
                default:
                    return 5;
            }
        }

        // Each template: the active piece of its name under its root nearest its fixed place, in the loaded scenes.
        private static void FindTemplates()
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                {
                    continue;
                }
                SceneRoots.Clear();
                scene.GetRootGameObjects(SceneRoots);
                for (int r = 0; r < SceneRoots.Count; r++)
                {
                    GameObject root = SceneRoots[r];
                    if (root == null)
                    {
                        continue;
                    }
                    for (int t = 0; t < Templates.Length; t++)
                    {
                        if (root.name == Templates[t].Root)
                        {
                            Take(root.transform, t);
                        }
                    }
                }
            }
            SceneRoots.Clear();
            string missing = null;
            for (int t = 0; t < Templates.Length; t++)
            {
                if (Found[t] == null)
                {
                    missing = (missing == null ? string.Empty : missing + ", ") + Templates[t].Name;
                }
            }
            Missing = missing;
        }

        private static void Take(Transform root, int t)
        {
            Template want = Templates[t];
            float best = Found[t] != null ? Flat(Found[t].transform.position, want) : TakeWithin * TakeWithin;
            Transform[] parts = root.GetComponentsInChildren<Transform>(false);
            for (int i = 0; i < parts.Length; i++)
            {
                Transform part = parts[i];
                string name = part.name;
                if (!name.StartsWith(want.Name, System.StringComparison.Ordinal) || (name.Length > want.Name.Length && name[want.Name.Length] != ' '))
                {
                    continue;   // a piece of another kind whose name begins the same ("SM_Trash Can_LOD1")
                }
                float d = Flat(part.position, want);
                if (d < best)
                {
                    best = d;
                    Found[t] = part.gameObject;
                }
            }
        }

        private static float Flat(Vector3 at, Template want)
        {
            float dx = at.x - want.X, dz = at.z - want.Z;
            return dx * dx + dz * dz;
        }
    }
}
