using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The whole map is loaded at once, and the engine tests every enabled renderer against the view (and against
    /// the sun's shadow volumes) every frame: about 186,000 of them, of which 3,000 to 4,500 are drawn. This lays
    /// a grid of 400 m squares over the scenery and takes the mesh renderers of every square that lies more than
    /// 1300 m from the camera out of the picture; a square comes back, whole and at once, as soon as the player's
    /// camera is within 1100 m of it (the far plane is 300 m), everything comes back when the city map opens
    /// (before the game's own map code runs), and a square comes back for any other camera that could see it,
    /// before that camera draws.
    ///
    /// There are two ways of taking a renderer out, and this class does either:
    ///
    /// SAFE (the option): Renderer.forceRenderingOff. The game never writes that flag (no occurrence in its code
    /// or its plug-ins), so nothing the game does while a square is held can be undone or overridden by giving
    /// the square back: the game's own enabled / active state of every object is untouched throughout. Every
    /// mesh renderer of the scenery can therefore be taken, with no exceptions. What is not known yet is whether
    /// the engine also skips its view test for such a renderer, which is where the saving comes from; the
    /// benchmark pair answers that.
    ///
    /// BY ENABLED (a benchmark trial only): Renderer.enabled. This is the version the third benchmark showed to
    /// save 0.61 ms with 35,986 renderers held. It cannot be made safe by construction: the game's own code
    /// writes Renderer.enabled in some forty places, and at least one of them (ViewBlockingEntity.renderersToHide,
    /// ViewBlockingEntity.cs line 248) reaches renderers through a list set up in the editor, anywhere in the
    /// scene, so no rule about where a renderer sits can prove the game will not switch it. If the game disabled
    /// a renderer while this held it, giving the square back would wrongly show it. It leaves alone everything on
    /// or under the components in <see cref="Switchers"/>, which is most of that risk, not all of it.
    ///
    /// WHAT MOVES IS NEVER TAKEN. A square is where a renderer stood when the scenery was looked through. The game
    /// keeps its parked cars in a pool (ParkingSimulator): a car parked in a far street is put away and parked again
    /// at a kerb near the player, the same object with the same renderers. Taken with its far square, it stood at the
    /// near kerb unseen, with its collider and its cut in the walking mesh: a block nobody could see. So nothing on
    /// or under an object of a layer of things that move (<see cref="MovingLayers"/>) is ever sorted into a square,
    /// and a renderer found outside its square when the square is taken is left alone (<see cref="Hold"/>).
    ///
    /// Either way only renderers are touched: objects stay active, so businesses, colliders, navigation, lights,
    /// sound and every script keep running. Level-of-detail groups are not affected (they choose among their
    /// renderers by a mask of their own and never write either flag), nor is static batching. Shadows: the sun's
    /// reach 100 m (150 m when driving), a held square is at least 1100 m away.
    /// </summary>
    public sealed class FarDistricts : PerfTweak
    {
        private const float CellSize = 400f;
        private const float Margin = 60f;            // a renderer reaches this far beyond the point it stands on, at most
        private const float HoldBeyond = 1300f;      // metres from the camera to the nearest point of a square
        private const float ReleaseWithin = 1100f;
        private const float Alarm = 500f;            // a held square this near means the camera jumped: worth a line in the log
        private const float Cooldown = 5f;           // after squares were given back for the map or a picture

        // Game components whose own code enables and disables renderers (by reading the game's code: file and line of one such write each).
        private static readonly string[] Switchers =
        {
            "CityBuildingController",        // CityBuildingController.cs 241, 718
            "CityHamptonsHouseController",   // CityHamptonsHouseController.cs 86-116
            "HamptonsHouse",                 // HamptonsHouse.cs 121-252
            "BuildingSignController", "BuildingLogoSignController",   // through CityBuildingController.cs 718
            "BillboardAd",                   // BillboardAd.cs 101, 106
            "DirtSpotObject",                // DirtSpotObject.cs 63, 79
            "GasStationTrigger",             // GasStationTrigger.cs 39-49
            "PrivateDriverGarageTrigger",    // PrivateDriverGarageTrigger.cs 68-82
            "AutoParkSpot",                  // AutoParkSpot.cs 14, 20; VehicleParkingHelper.cs 65-190
            "FenceDoor",                     // FenceDoor.cs 352, 357
            "EntityController",              // EntityController.cs 145, 160 (ViewBlockingEntity and its kin derive from it: ViewBlockingEntity.cs 248)
            "ItemController",                // ItemController.cs 461, 475
            "BaseHuman",                     // BaseHuman.cs 297; HumanHider.cs 117, 126
            "CarFeatures", "CarController",  // CarFeatures.cs 114, 130
            "TaxiDestinationData",           // Buildings/TaxiDestinationData.cs 49
            "CoatCheckController",           // Controllers/CoatCheckController.cs 143
            "CarnivalPedestrian",            // CarnivalPedestrian.cs 48; CarnivalPedestrianPool.cs 44
            "AttractionViewBlockingEntity", "FerrisWheelViewBlockingEntity",   // Controllers/*.cs 49, 38-58
            "InteriorElement",               // inside houses: switched with them
            "ParkingBuildingWithDifferentHeightsManager",   // ParkingBuildingWithDifferentHeightsManager.cs (22 writes)
            "GolfCourse",                    // PlayerActivity/GolfCourse.cs 232-498
            "SewerSteam",                    // SewerSteam.cs 54, 59
            "TennisPlayer", "TennisBall", "TennisScoreLine", "WorkoutActivity",
        };

        // Scenes that are not scenery standing in one place.
        // Layers of things the game moves about, pooled or driven: never scenery, whatever scene they stand in.
        private static readonly string[] MovingLayers = { "ParkedVehicles", "AiVehicles", "PlayerVehicles", "Vehicles", "Human" };

        private static readonly string[] NotScenery = { "MainScene", "UI", "Indoor", "LightsAndPostprocessing", "DontDestroyOnLoad" };

        private sealed class Cell
        {
            public Bounds Bounds;
            public bool Held;
            public int Cursor;       // how far through Scenery the holding has got
            public readonly List<MeshRenderer> Scenery = new List<MeshRenderer>(256);
            public readonly List<MeshRenderer> Taken = new List<MeshRenderer>(256);   // of those, the ones this has taken out
        }

        private readonly bool _byEnabled;
        private readonly string _id;
        private readonly int _slice;   // renderers looked at or switched per tick: about a millisecond

        private readonly Dictionary<long, Cell> _byKey = new Dictionary<long, Cell>(512);
        private readonly List<Cell> _cells = new List<Cell>(512);
        private readonly List<GameObject> _roots = new List<GameObject>(64);
        private readonly List<MeshRenderer> _gathered = new List<MeshRenderer>(32768);
        private readonly List<MeshRenderer> _survey = new List<MeshRenderer>(65536);
        private readonly HashSet<int> _switcherObjects = new HashSet<int>();
        private readonly HashSet<int> _seenScenes = new HashSet<int>();
        private readonly List<Scene> _sceneryScenes = new List<Scene>(8);   // the scenes that had scenery in them
        private Type[] _switcherTypes;

        private int _surveyCursor;
        private Cell _working;       // a square half-way through being held
        private bool _stale;
        private bool _settled;       // the last look found every square as it should be
        private float _quietUntil;
        private Camera _main;
        private Vector3 _lastChecked;
        private bool _hooked, _alarmed;
        private int _heldRenderers, _heldCells, _scenery, _leftAlone, _movingMask;

        /// <param name="byEnabled">False: the safe way (forceRenderingOff, every mesh renderer). True: the benchmark trial (enabled, minus what the game switches).</param>
        public FarDistricts(bool byEnabled)
        {
            _byEnabled = byEnabled;
            _id = byEnabled ? "trial_far_by_enabled" : "far_districts";
            _slice = byEnabled ? 2000 : 6000;
        }

        public override string Id => _id;
        public override string LabelKey => _byEnabled ? null : "reallife_perf_far_districts2";
        public override bool DefaultOn => false;   // the safe way has not been measured yet
        public override bool Trial => _byEnabled;
        public override float Interval => Busy ? 0.05f : 0.5f;
        public override bool Busy => _stale || _surveyCursor < _survey.Count || _working != null || !_settled;

        protected override void Apply()
        {
            if (_byEnabled)
            {
                _switcherTypes ??= FindSwitcherTypes();
            }
            // In front of the game's own map handlers: when they run, every renderer is as the game left it.
            GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Combine(new Action<bool>(OnMapToggled), GlobalEvents.onCityMapToggle);
            RenderPipelineManager.beginCameraRendering += OnCameraRendering;
            _hooked = true;
            _settled = false;
            _main = GameManager.GetMainCamera();
            _stale = true;   // the first tick looks for scenes not looked through yet
        }

        private static Type[] FindSwitcherTypes()
        {
            var names = new HashSet<string>(Switchers);
            var found = new List<Type>(Switchers.Length);
            foreach (Type type in typeof(GameManager).Assembly.GetTypes())
            {
                if (names.Contains(type.Name) && typeof(Component).IsAssignableFrom(type))
                {
                    found.Add(type);
                }
            }
            return found.ToArray();
        }

        internal override void WorldChanged(bool loaded) => _stale = true;   // (an unloaded scene matters here: its squares are forgotten)

        // The city's scenes gathered behind the loading screen (Refresh: every mesh renderer under every root object, some
        // 357,000, which was a frame of 127 ms in play on the first tick after the tweak came on). Sorting them into
        // squares is left to the timer, a slice a tick, as before; its first tick then finds no scene it has not seen.
        protected override void Prewarm()
        {
            if (_byEnabled)
            {
                return;   // the benchmark's trial: never wanted at a load
            }
            Refresh();
            if (_stale)
            {
                Refresh();   // the scenes of an earlier city were forgotten first: now the ones that are here
            }
        }

        // The timer only ever takes squares out. Giving them back is done the moment it is needed, in OnCameraRendering.
        protected override void Tick(float now)
        {
            if (_surveyCursor < _survey.Count)
            {
                Survey();
                return;
            }
            if (_stale)
            {
                Refresh();
                _settled = false;
                return;
            }
            _main = GameManager.GetMainCamera();
            if (_main == null)
            {
                _settled = true;
                return;
            }
            Vector3 eye = _main.transform.position;
            GiveBackNear(eye, ReleaseWithin, true);   // the safety net: OnCameraRendering has normally done this already
            if (CityMap.IsOpen || now < _quietUntil)
            {
                _settled = _working == null;
                return;
            }
            int budget = _slice;
            if (_working != null)
            {
                budget -= Hold(_working, budget);
            }
            for (int i = 0; i < _cells.Count && budget > 0 && _working == null; i++)
            {
                Cell cell = _cells[i];
                if (!cell.Held && cell.Scenery.Count > 0 && cell.Bounds.SqrDistance(eye) > HoldBeyond * HoldBeyond)
                {
                    cell.Held = true;
                    cell.Cursor = 0;
                    _heldCells++;
                    budget -= Hold(cell, budget);
                }
            }
            _settled = _working == null && budget > 0;
        }

        // Scenes were loaded or unloaded. A scene not seen before is queued to be looked through once; small scenes
        // that come and go (the game loads one around every save) cost one look at their root objects. If a scene
        // that had scenery has gone, everything starts again.
        private void Refresh()
        {
            _stale = false;
            foreach (Scene known in _sceneryScenes)
            {
                if (!known.IsValid() || !known.isLoaded)
                {
                    Forget();
                    _stale = true;
                    return;
                }
            }
            bool added = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || Array.IndexOf(NotScenery, scene.name) >= 0 || !_seenScenes.Add(scene.handle))
                {
                    continue;
                }
                int before = _survey.Count;
                using var piece = SessionWatch.Piece("performance: the scenery's renderers being gathered for the far districts (every object of a scene not looked through yet)");
                scene.GetRootGameObjects(_roots);
                foreach (GameObject root in _roots)
                {
                    // The safe way takes renderers on inactive objects too: one the game activates later is then already out.
                    if (root.activeSelf || !_byEnabled)
                    {
                        root.GetComponentsInChildren(!_byEnabled, _gathered);   // fills the list it is given
                        _survey.AddRange(_gathered);
                        _gathered.Clear();
                    }
                }
                _roots.Clear();
                if (_survey.Count > before)
                {
                    _sceneryScenes.Add(scene);
                    added = true;
                }
            }
            if (added && _byEnabled)
            {
                // Which objects the game switches itself, over everything loaded: once per round of new scenes.
                _switcherObjects.Clear();
                foreach (Type type in _switcherTypes)
                {
                    foreach (UnityEngine.Object found in UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        _switcherObjects.Add(((Component)found).transform.GetInstanceID());
                    }
                }
            }
        }

        // Sorts the gathered renderers into squares, a slice per tick.
        private void Survey()
        {
            int end = Mathf.Min(_surveyCursor + _slice, _survey.Count);
            for (; _surveyCursor < end; _surveyCursor++)
            {
                MeshRenderer renderer = _survey[_surveyCursor];
                if (renderer == null)
                {
                    continue;
                }
                Transform transform = renderer.transform;
                if (Moves(transform) || (_byEnabled && (!renderer.enabled || GameSwitches(transform))))
                {
                    _leftAlone++;
                    continue;
                }
                Vector3 at = transform.position;
                long key = ((long)Mathf.FloorToInt(at.x / CellSize) << 32) ^ (uint)Mathf.FloorToInt(at.z / CellSize);
                if (!_byKey.TryGetValue(key, out Cell cell))
                {
                    cell = new Cell { Bounds = new Bounds(at, Vector3.one * (Margin * 2f)) };
                    _byKey.Add(key, cell);
                    _cells.Add(cell);
                }
                else
                {
                    cell.Bounds.Encapsulate(new Bounds(at, Vector3.one * (Margin * 2f)));
                }
                cell.Scenery.Add(renderer);
                _scenery++;
            }
            if (_surveyCursor >= _survey.Count)
            {
                _survey.Clear();
                _survey.TrimExcess();
                _gathered.TrimExcess();
                _surveyCursor = 0;
                _settled = false;
                PerfDirector.Logger?.Info("Performance: " + _id + " looked through the scenery: " + _scenery + " mesh renderers in " + _cells.Count + " squares of " + CellSize + " m"
                                          + "; " + _leftAlone + " left alone (of things that move" + (_byEnabled ? ", disabled, or the game switches them itself" : string.Empty) + ").");
            }
        }

        // Takes out up to 'budget' of the square's renderers; returns how many it looked at.
        private int Hold(Cell cell, int budget)
        {
            int end = Mathf.Min(cell.Cursor + Mathf.Max(budget, 1), cell.Scenery.Count);
            int looked = end - cell.Cursor;
            for (; cell.Cursor < end; cell.Cursor++)
            {
                MeshRenderer renderer = cell.Scenery[cell.Cursor];
                if (renderer == null || !cell.Bounds.Contains(renderer.transform.position))
                {
                    continue;   // (gone, or no longer where it was sorted: something moved it, and it may stand in view)
                }
                if (_byEnabled)
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }
                    renderer.enabled = false;
                }
                else
                {
                    if (renderer.forceRenderingOff)
                    {
                        continue;   // somebody else's: not ours to give back
                    }
                    renderer.forceRenderingOff = true;
                }
                cell.Taken.Add(renderer);
                _heldRenderers++;
            }
            _working = cell.Cursor < cell.Scenery.Count ? cell : null;
            return looked;
        }

        // True if the game's own code may enable or disable this renderer: it sits on or under one of the switchers.
        // True if the renderer is on or under an object of a layer of things that move.
        private bool Moves(Transform transform)
        {
            if (_movingMask == 0)
            {
                _movingMask = LayerMask.GetMask(MovingLayers);
            }
            for (Transform t = transform; t != null; t = t.parent)
            {
                if (((_movingMask >> t.gameObject.layer) & 1) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private bool GameSwitches(Transform transform)
        {
            for (Transform t = transform; t != null; t = t.parent)
            {
                if (_switcherObjects.Contains(t.GetInstanceID()))
                {
                    return true;
                }
            }
            return false;
        }

        // Whole and at once: a square is never half back.
        private void GiveBack(Cell cell)
        {
            if (_working == cell)
            {
                _working = null;
            }
            foreach (MeshRenderer renderer in cell.Taken)
            {
                if (renderer == null)
                {
                    continue;
                }
                if (_byEnabled)
                {
                    if (!renderer.enabled)
                    {
                        renderer.enabled = true;
                    }
                }
                else
                {
                    renderer.forceRenderingOff = false;
                }
            }
            _heldRenderers -= cell.Taken.Count;
            cell.Taken.Clear();
            if (cell.Held)
            {
                cell.Held = false;
                _heldCells--;
            }
        }

        private void GiveBackNear(Vector3 eye, float reach, bool playersCamera)
        {
            if (_heldCells == 0)
            {
                return;
            }
            for (int i = 0; i < _cells.Count; i++)
            {
                Cell cell = _cells[i];
                if (!cell.Held)
                {
                    continue;
                }
                float distance = cell.Bounds.SqrDistance(eye);
                if (distance >= reach * reach)
                {
                    continue;
                }
                if (playersCamera && distance < Alarm * Alarm && !_alarmed)
                {
                    _alarmed = true;   // once per session: a jump (taxi, subway, hospital) is expected now and then
                    PerfDirector.Logger?.Info("Performance: " + _id + " found a held square " + Mathf.Sqrt(distance).ToString("F0") + " m from the camera (the camera jumped) and gave it back before the frame was drawn.");
                }
                GiveBack(cell);
                _settled = false;
            }
        }

        private void GiveBackAll()
        {
            foreach (Cell cell in _cells)
            {
                if (cell.Held || cell.Taken.Count > 0)
                {
                    GiveBack(cell);
                }
            }
            _working = null;
        }

        private void Forget()
        {
            GiveBackAll();
            _cells.Clear();
            _byKey.Clear();
            _seenScenes.Clear();
            _sceneryScenes.Clear();
            _survey.Clear();
            _surveyCursor = 0;
            _switcherObjects.Clear();
            _heldRenderers = _heldCells = _scenery = _leftAlone = 0;
        }

        // Runs before the game's own handlers (see Apply): the map shows the whole city.
        private void OnMapToggled(bool open)
        {
            if (open && _heldRenderers > 0)
            {
                GiveBackAll();
            }
            _quietUntil = Time.unscaledTime + Cooldown;
            _settled = false;
        }

        // Once per camera per frame while the tweak is on, just before the pipeline culls for that camera, so whatever
        // is given back here is in this very frame. The player's camera: when it has moved 5 m since the last look
        // (walking: about once a second; a jump across the map: on the frame of the jump; standing: never), every
        // held square within 1100 m comes back. Any other game camera (savegame picture, building picture, previews):
        // every held square within its far plane plus 100 m.
        private void OnCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_heldCells == 0 || camera.cameraType != CameraType.Game)
            {
                return;
            }
            try
            {
                Vector3 eye = camera.transform.position;
                if (camera == _main)
                {
                    if ((eye - _lastChecked).sqrMagnitude < 25f)
                    {
                        return;
                    }
                    _lastChecked = eye;
                    GiveBackNear(eye, ReleaseWithin, true);
                }
                else
                {
                    int before = _heldCells;
                    GiveBackNear(eye, camera.farClipPlane + 100f, false);
                    if (_heldCells != before)
                    {
                        _quietUntil = Time.unscaledTime + Cooldown;
                    }
                }
            }
            catch (Exception e)
            {
                Abort(e);   // everything is given back and the tweak stays off
            }
        }

        // The squares are kept (looking through the scenery is the expensive part); everything taken out goes back.
        protected override void Restore()
        {
            if (_hooked)
            {
                _hooked = false;
                GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Remove(GlobalEvents.onCityMapToggle, new Action<bool>(OnMapToggled));
                RenderPipelineManager.beginCameraRendering -= OnCameraRendering;
            }
            GiveBackAll();
            foreach (Scene known in _sceneryScenes)
            {
                if (!known.IsValid() || !known.isLoaded)
                {
                    Forget();   // the city is going: nothing to keep
                    break;
                }
            }
            _stale = false;
            _main = null;
        }

        public override string Describe() => _cells.Count == 0
            ? "scenery not looked through yet"
            : "renderers out of the picture (" + (_byEnabled ? "disabled" : "forceRenderingOff") + "): " + _heldRenderers + " in " + _heldCells + " of " + _cells.Count + " squares; scenery " + _scenery
              + ", left alone " + _leftAlone;
    }
}
