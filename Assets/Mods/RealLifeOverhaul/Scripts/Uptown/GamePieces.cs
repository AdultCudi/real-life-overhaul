using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// A piece of the game's scene the mod copies, and where to look for it: the scene root the game keeps the kind under,
    /// its name with the copies' numbers off, the place of the one taken (the nearest to it within a few metres, so every
    /// machine copies the same one and never one in a static batch), the height its pivot stands above the ground, and the
    /// room it needs (nothing of the game's may stand there; 0 for a flat one). <see cref="Kind"/> is the caller's own
    /// number for what the piece is.
    /// </summary>
    public struct PieceTemplate
    {
        public int Kind;
        public string Root, Name;
        public float X, Z, Y;
        public Vector3 Room;
        public float Within;   // m from its fixed place a piece may be: farther, and the game's scene has moved (0: GamePieces.DefaultWithin)
    }

    /// <summary>
    /// The one place that finds and copies the game's own pieces: the school campus's fence and court, Uptown's street
    /// furniture and its parks' trees, benches, lamps, fountain and statue all go through it, so a piece is found and laid
    /// the same way for each. A piece is looked for among ALL the children of its scene root, switched off or on: the game
    /// puts whole groups of its scene out (the far districts' tweak, a root with a city-map hider, Industry City's bicycle
    /// docks, which stand switched off), and a search of the switched-on alone found none of the pavement benches or the
    /// docks (Player.log, 26 Oct 2026: "not found: SleepingBench, Prefab_BikeRentalStand"). A copy is made switched on, its
    /// renderers drawn. COST: one pass through each scene root's children per load, behind the loading screen; nothing
    /// after it.
    /// </summary>
    public static class GamePieces
    {
        public const float DefaultWithin = 6f;   // m from its fixed place a template must be: farther, and the game's scene has moved

        private static readonly List<GameObject> SceneRoots = new List<GameObject>(64);
        private static readonly List<string> Names = new List<string>(16);
        private static readonly List<bool> Seen = new List<bool>(16);

        /// <summary>
        /// Fills <paramref name="found"/> with the piece of each template (null where there is none) and returns the names
        /// of the pieces missing, in a word each, or null. Switched-off pieces are looked at too.
        /// </summary>
        public static string Find(PieceTemplate[] templates, GameObject[] found)
        {
            System.Array.Clear(found, 0, found.Length);
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
                    for (int t = 0; root != null && t < templates.Length; t++)
                    {
                        if (root.name == templates[t].Root)
                        {
                            Take(root.transform, templates, t, found);
                        }
                    }
                }
            }
            SceneRoots.Clear();
            string missing = null;
            for (int t = 0; t < templates.Length; t++)
            {
                if (found[t] == null)
                {
                    missing = (missing == null ? string.Empty : missing + ", ") + templates[t].Name;
                }
            }
            return missing;
        }

        /// <summary>The line for Player.log about what was found and what was not: "found: a, b; not found: c".</summary>
        public static string Words(PieceTemplate[] templates, GameObject[] found)
        {
            Names.Clear();
            Seen.Clear();
            for (int t = 0; t < templates.Length; t++)
            {
                Names.Add(templates[t].Name);
                Seen.Add(found[t] != null);
            }
            return PieceRules.Words(Names, Seen);
        }

        // The nearest piece of the template's name under a root, within its reach; includeInactive is the point of this call.
        private static void Take(Transform root, PieceTemplate[] templates, int t, GameObject[] found)
        {
            PieceTemplate want = templates[t];
            float within = want.Within > 0f ? want.Within : DefaultWithin;
            float best = found[t] != null ? PieceRules.Flat(found[t].transform.position.x, found[t].transform.position.z, want.X, want.Z) : PieceRules.Start(within);
            Transform[] parts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                Transform part = parts[i];
                if (!PieceRules.Matches(part.name, want.Name))
                {
                    continue;
                }
                Vector3 at = part.position;
                float d = PieceRules.Flat(at.x, at.z, want.X, want.Z);
                if (PieceRules.Takes(d, best, within))
                {
                    best = d;
                    found[t] = part.gameObject;
                }
            }
        }

        /// <summary>
        /// A copy of a piece of the game's laid under <paramref name="parent"/>: switched on (the template may be one the game
        /// put out), at the place and turn given with its pivot at that height, its renderers drawn (the far districts' tweak
        /// may have put the template out of the picture, and a copy of it keeps that). The caller scales it if it wants.
        /// </summary>
        public static GameObject Stand(GameObject template, Transform parent, string name, Vector3 at, float yaw)
        {
            GameObject copy = Object.Instantiate(template, parent);
            copy.name = name;
            copy.SetActive(true);
            copy.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            Renderer[] renderers = copy.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].forceRenderingOff = false;
            }
            return copy;
        }

        /// <summary>What stands in the way: the walking meshes cut round a piece laid after they were grown, a little larger than its body.</summary>
        public static void Block(GameObject piece, Vector3 room)
        {
            var cut = piece.AddComponent<NavMeshObstacle>();
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = new Vector3(0f, room.y * 0.5f, 0f);
            cut.size = room;
            cut.carveOnlyStationary = true;
            cut.carving = true;
        }
    }
}
