using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Where Uptown's ground meets the game's scene, its one Game file with its part for the street art (GroundGame.Art):
    /// the backdrop north of 1st Avenue (the game's MapBorder) found once a load and put away where the district stands,
    /// its block grounds kept as the lots' ground; the game's invisible walls across the old street ends switched off and
    /// new ones built round the district's edge as the game's own; the district's ground and road colliders, as the city's
    /// ground ends short of Uptown Avenue; the city map's bounds widened over it. The same load the district stands down,
    /// all is given back as it was; when the city goes, what is the mod's goes and what is the game's goes with its scene.
    /// Nothing is saved. Called at the load and by the tests, never per frame.
    /// </summary>
    public static partial class GroundGame
    {
        public const string GroundName = "RealLifeOverhaul Uptown ground";   // the new walls and colliders
        private const string BackdropName = "MapBorder";                      // the game's backdrop round the city
        private const string KeptName = "GroundPlanes";                       // its block grounds, the lots' ground: never put away
        private const string WallsName = "City Bounds Colliders";             // its invisible walls
        private const string PropsLayer = "StreetProps";                      // the layer of the game's invisible walls
        private const string GroundLayer = "Ground";                          // the layers the cars and the player stand on
        private const string RoadsLayer = "Roads";

        private static readonly List<GameObject> Hidden = new List<GameObject>(320);   // the backdrop's pieces put away, to be shown again
        private static readonly List<Collider> Opened = new List<Collider>(9);         // the game's walls switched off
        private static readonly List<GameObject> SceneRoots = new List<GameObject>(64);
        private static Transform _backdrop;
        private static Transform _walls;
        private static GameObject _ground;
        private static CityMapCam _map;
        private static Bounds _mapWas;
        private static bool _mapWidened;

        /// <summary>The backdrop's pieces put away at this load.</summary>
        public static int HiddenCount => Hidden.Count;

        /// <summary>The game's invisible walls switched off at this load.</summary>
        public static int OpenedCount => Opened.Count;

        /// <summary>The new walls and colliders standing, or 0.</summary>
        public static int Built => _ground != null ? _ground.transform.childCount : 0;

        /// <summary>
        /// The game's own pieces the district is laid against, found before anything changes: the backdrop, its walls, the
        /// city map's camera. Null when all are there; else why not.
        /// </summary>
        public static string FindBackdrop()
        {
            _backdrop = null;
            _walls = null;
            for (int s = 0; s < SceneManager.sceneCount && _backdrop == null; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                {
                    continue;
                }
                SceneRoots.Clear();
                scene.GetRootGameObjects(SceneRoots);
                for (int i = 0; i < SceneRoots.Count; i++)
                {
                    if (SceneRoots[i] != null && SceneRoots[i].name == BackdropName)
                    {
                        _backdrop = SceneRoots[i].transform;
                        break;
                    }
                }
            }
            SceneRoots.Clear();
            if (_backdrop == null)
            {
                return "the game's backdrop round the city (" + BackdropName + ") was not found";
            }
            _walls = _backdrop.Find(WallsName);
            if (_walls == null)
            {
                return "the game's invisible walls round the city (" + BackdropName + "/" + WallsName + ") were not found";
            }
            CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
            _map = city != null && city.cityMap != null ? city.cityMap.cityMapCam : null;
            return _map == null ? "the city map's camera was not found" : null;
        }

        /// <summary>
        /// Puts away every piece of the backdrop whose middle lies on the district's ground, its block grounds and walls
        /// apart: a piece is anything that shows or stands (a renderer, a collider, a level of detail), and the walk goes
        /// into what is neither. Returns how many.
        /// </summary>
        public static int HideBackdrop(List<GroundRect> rects)
        {
            if (_backdrop == null)
            {
                return 0;
            }
            for (int i = 0; i < _backdrop.childCount; i++)
            {
                Transform group = _backdrop.GetChild(i);
                if (group.name != KeptName && group.name != WallsName)
                {
                    HideUnder(group, rects);
                }
            }
            return Hidden.Count;
        }

        private static void HideUnder(Transform node, List<GroundRect> rects)
        {
            for (int i = 0; i < node.childCount; i++)
            {
                Transform child = node.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    continue;   // the game keeps it away itself
                }
                Vector3 at = child.position;
                if (Shows(child) && On(rects, at.x, at.z))
                {
                    child.gameObject.SetActive(false);
                    Hidden.Add(child.gameObject);
                    continue;
                }
                HideUnder(child, rects);
            }
        }

        private static bool Shows(Transform piece)
        {
            return piece.TryGetComponent(out Renderer _) || piece.TryGetComponent(out Collider _) || piece.TryGetComponent(out LODGroup _);
        }

        private static bool On(List<GroundRect> rects, float x, float z)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i].Holds(x, z))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Every piece of the backdrop put away at this load shown again.</summary>
        public static void ShowBackdrop()
        {
            for (int i = 0; i < Hidden.Count; i++)
            {
                if (Hidden[i] != null)
                {
                    Hidden[i].SetActive(true);
                }
            }
            Hidden.Clear();
        }

        /// <summary>
        /// Switches off the game's wall across each of the given street ends: the nearest collider of its walls within
        /// <paramref name="reach"/> of the end's middle. Returns how many were found.
        /// </summary>
        public static int OpenWalls(List<float> x, float z, float reach)
        {
            if (_walls == null)
            {
                return 0;
            }
            Collider[] walls = _walls.GetComponentsInChildren<Collider>(true);
            for (int k = 0; k < x.Count; k++)
            {
                Collider best = null;
                float nearest = reach * reach;
                for (int i = 0; i < walls.Length; i++)
                {
                    if (walls[i] == null || !walls[i].enabled || Opened.Contains(walls[i]))
                    {
                        continue;
                    }
                    Vector3 middle = walls[i].bounds.center;
                    float dx = middle.x - x[k], dz = middle.z - z;
                    if (dx * dx + dz * dz < nearest)
                    {
                        nearest = dx * dx + dz * dz;
                        best = walls[i];
                    }
                }
                if (best != null)
                {
                    best.enabled = false;
                    Opened.Add(best);
                }
            }
            return Opened.Count;
        }

        /// <summary>The game's walls switched off at this load switched on again.</summary>
        public static void CloseWalls()
        {
            for (int i = 0; i < Opened.Count; i++)
            {
                if (Opened[i] != null)
                {
                    Opened[i].enabled = true;
                }
            }
            Opened.Clear();
        }

        /// <summary>
        /// The new walls round the district's edge (as the game's own: boxes on its props layer, as high as its) and the
        /// district's ground (its top the city's ground) and road colliders, under one object of the mod's own in the
        /// backdrop's scene, so they go with the city.
        /// </summary>
        public static void Build(List<PlanBox> walls, PlanBox ground, List<PlanBox> roads, float groundThick, float roadThick)
        {
            if (_ground != null)
            {
                return;
            }
            _ground = new GameObject(GroundName);
            if (_backdrop != null)
            {
                SceneManager.MoveGameObjectToScene(_ground, _backdrop.gameObject.scene);
            }
            int props = Layer(PropsLayer), grounds = Layer(GroundLayer), roadsLayer = Layer(RoadsLayer);
            for (int i = 0; i < walls.Count; i++)
            {
                Box("wall", walls[i], walls[i].Top * 0.5f, walls[i].Top, props);
            }
            Box("ground", ground, ground.Top - groundThick * 0.5f, groundThick, grounds);
            for (int i = 0; i < roads.Count; i++)
            {
                Box("road", roads[i], roads[i].Top - roadThick * 0.5f, roadThick, roadsLayer);
            }
        }

        private static void Box(string name, PlanBox box, float middleY, float height, int layer)
        {
            var made = new GameObject(name);
            made.layer = layer;
            made.transform.SetParent(_ground.transform, false);
            made.transform.position = new Vector3(box.X, middleY, box.Z);
            var collider = made.AddComponent<BoxCollider>();
            collider.size = new Vector3(box.HalfX * 2f, height, box.HalfZ * 2f);
        }

        // A layer of the game's by name (Core/Guard names any that is gone at start); the default layer without it.
        private static int Layer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            return layer >= 0 ? layer : 0;
        }

        /// <summary>The city map's bounds widened to take in the district's ground; false without the map's camera.</summary>
        public static bool WidenMap(float west, float east, float north)
        {
            if (_map == null)
            {
                return false;
            }
            if (!_mapWidened)
            {
                _mapWas = _map.bounds;
                _mapWidened = true;
            }
            Bounds bounds = _map.bounds;
            bounds.Encapsulate(new Vector3(west, bounds.center.y, north));
            bounds.Encapsulate(new Vector3(east, bounds.center.y, north));
            _map.bounds = bounds;
            return true;
        }

        /// <summary>The city map's bounds as the game has them now, for the census; false without the map's camera.</summary>
        public static bool MapBounds(out Bounds bounds)
        {
            bounds = _map != null ? _map.bounds : default;
            return _map != null;
        }

        /// <summary>
        /// The district stands down the same load it was laid, before the traffic starts: the backdrop shown again, the
        /// game's walls switched on again, what the mod built taken away, the map as it was.
        /// </summary>
        public static void GiveBack()
        {
            ShowBackdrop();
            CloseWalls();
            Forget();
        }

        /// <summary>
        /// The city is going: what the mod made goes and the map's bounds are put back; the game's own pieces go with their
        /// scene, so none is woken again for a frame it would not be seen.
        /// </summary>
        public static void Forget()
        {
            DropArt();
            if (_ground != null)
            {
                Object.Destroy(_ground);
            }
            _ground = null;
            if (_mapWidened && _map != null)
            {
                _map.bounds = _mapWas;
            }
            _mapWidened = false;
            _map = null;
            _backdrop = null;
            _walls = null;
            Hidden.Clear();
            Opened.Clear();
        }
    }
}
