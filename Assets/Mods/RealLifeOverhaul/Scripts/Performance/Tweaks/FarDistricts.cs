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
    /// or under the components in GameContract.Switchers, which is most of that risk, not all of it.
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
    ///
    /// THE CITY MAP draws the city with the player's camera set to the map's own layers (CityMap.cityMapMask, or its
    /// low-detail mask: CityMap.Toggle). So when it opens only the renderers of those layers are given back; the rest
    /// stay out (the map's camera does not draw them), and all of them come back the moment the camera draws other
    /// layers while the map is open (CityMap.TemporarilyChangeState). That was every held renderer, some 10 to 30 ms.
    ///
    /// THE COST, all on the director's timer and budgeted by the clock (<see cref="WorkSlice"/>): sorting the gathered
    /// renderers into squares, and taking a square's renderers out, about a millisecond a tick (it was 6,000 renderers a
    /// tick whatever they cost: ticks of 3 to 9 ms); whether a renderer stands on something that moves is asked once per
    /// parent object, not once per renderer up its whole line of parents. Gathering a scene's renderers (every object of
    /// it: a frame of 127 ms for the city) is done behind the loading screen (Prewarm), and for a scene that arrives or
    /// goes in play behind the next black screen, pause or loading screen; until then the squares are held as they were.
    /// Per frame: one comparison a camera (OnCameraRendering), and one more while the map is open.
    /// </summary>
    public sealed class FarDistricts : PerfTweak
    {
        private const float CellSize = 400f;
        private const float Margin = 60f;            // a renderer reaches this far beyond the point it stands on, at most
        private const float HoldBeyond = 1300f;      // metres from the camera to the nearest point of a square
        private const float ReleaseWithin = 1100f;
        private const float Alarm = 500f;            // a held square this near means the camera jumped: worth a line in the log
        private const float Cooldown = 5f;           // after squares were given back for the map or a picture

        // Game components whose own code enables and disables renderers: the game contract's list (Core/GameContract,
        // Switchers, with the file and line of one such write each), resolved once when the mod starts.

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
            public readonly List<bool> OnMap = new List<bool>(256);                  // for each of those: drawn on the city map (its layer is one of the map's)
            public readonly List<MeshRenderer> Taken = new List<MeshRenderer>(256);   // the ones this has taken out that the city map draws
            public readonly List<MeshRenderer> TakenOff = new List<MeshRenderer>(64); // ...and that it does not: kept out while the map is open
        }

        private const double SliceMs = 1.0;            // milliseconds of a tick the sorting or the holding may take
        private const int LookEvery = 64;              // renderers between two looks at the clock
        private const float HiddenEvery = 0.25f;       // seconds between two looks at whether the screen is black, while a scene waits for it

        private readonly bool _byEnabled;
        private readonly string _id;
        private readonly Dictionary<int, bool> _moves = new Dictionary<int, bool>(4096);   // while sorting: a parent object (its id) on or under something that moves
        private int _mapMask = ~0;     // the layers the city map draws (both its masks); all of them where the map is not known
        private int _offMapHeld;       // renderers held that the city map does not draw
        private bool _waitHidden;      // a scene came or went in play: gathered behind the next black screen

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
        }

        public override string Id => _id;
        public override string LabelKey => _byEnabled ? null : "reallife_perf_far_districts2";
        public override bool DefaultOn => false;   // the safe way has not been measured yet
        public override bool Trial => _byEnabled;
        public override float Interval => _waitHidden && _surveyCursor >= _survey.Count && _working == null ? HiddenEvery : Busy ? 0.05f : 0.5f;
        public override bool Busy => _stale || _surveyCursor < _survey.Count || _working != null || !_settled;

        protected override void Apply()
        {
            if (_byEnabled)
            {
                if (GameContract.FarDistricts.Off)
                {
                    // A component of the list is gone after a game update: without all of them this trial cannot be safe (Core/GameContract).
                    throw new InvalidOperationException("the game's components that switch renderers have changed: " + GameContract.FarDistricts.Why);
                }
                _switcherTypes ??= FindSwitcherTypes();
            }
            // In front of the game's own map handlers: when they run, every renderer is as the game left it.
            _onMap = _onMap ?? Guard.Wrap<bool>(OnMapToggled);   // (a fault here never reaches the game's own handlers, or the pipeline: Core/Guard)
            _onCamera = _onCamera ?? Guard.Wrap<ScriptableRenderContext, Camera>(OnCameraRendering);
            GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Combine(_onMap, GlobalEvents.onCityMapToggle);
            RenderPipelineManager.beginCameraRendering += _onCamera;
            _hooked = true;
            _settled = false;
            _main = GameManager.GetMainCamera();
            _mapMask = MapLayers();
            _stale = true;   // the first tick looks for scenes not looked through yet
            _waitHidden = false;
        }

        // The layers the city map draws: both its masks (the player may switch between them). All layers where it is not there,
        // so that a map open gives everything back, as it always did.
        private static int MapLayers()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityMap map = city != null ? city.cityMap : null;
            return map != null ? map.cityMapMask.value | map.cityMapMaskLowDetail.value : ~0;
        }

        private static Type[] FindSwitcherTypes()
        {
            var found = new List<Type>(GameContract.Switchers.Length);
            foreach (GameMember switcher in GameContract.Switchers)
            {
                if (switcher.Type != null && typeof(Component).IsAssignableFrom(switcher.Type))
                {
                    found.Add(switcher.Type);
                }
            }
            return found.ToArray();
        }

        internal override void WorldChanged(bool loaded)
        {
            _stale = true;   // (an unloaded scene matters here: its squares are forgotten)
            _waitHidden = false;
        }

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
            if (_stale && !_waitHidden)
            {
                if (!Changed())
                {
                    _stale = false;   // nothing come or gone that has scenery: nothing to gather
                }
                else
                {
                    _waitHidden = true;
                }
            }
            if (_stale && SceneFinds.Hidden)
            {
                _waitHidden = false;
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
            WorkSlice slice = WorkSlice.Of(SliceMs, LookEvery);
            if (_working != null)
            {
                Hold(_working, ref slice);
            }
            for (int i = 0; i < _cells.Count && !slice.Over && _working == null; i++)
            {
                Cell cell = _cells[i];
                if (!cell.Held && cell.Scenery.Count > 0 && cell.Bounds.SqrDistance(eye) > HoldBeyond * HoldBeyond)
                {
                    cell.Held = true;
                    cell.Cursor = 0;
                    _heldCells++;
                    Hold(cell, ref slice);
                }
            }
            _settled = _working == null && !slice.Over;
        }

        // True if a scene with scenery has gone, or a scene not looked through has come: what Refresh would gather or forget.
        // A look at the scenes' handles and names (a few scenes; a name each), no object of them.
        private bool Changed()
        {
            foreach (Scene known in _sceneryScenes)
            {
                if (!known.IsValid() || !known.isLoaded)
                {
                    return true;
                }
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && !_seenScenes.Contains(scene.handle) && Array.IndexOf(NotScenery, scene.name) < 0)
                {
                    return true;
                }
            }
            return false;
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

        // Sorts the gathered renderers into squares, a slice per tick (about a millisecond).
        private void Survey()
        {
            WorkSlice slice = WorkSlice.Of(SliceMs, LookEvery);
            for (; _surveyCursor < _survey.Count && slice.More(); _surveyCursor++)
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
                cell.OnMap.Add(((_mapMask >> renderer.gameObject.layer) & 1) != 0);
                _scenery++;
            }
            if (_surveyCursor >= _survey.Count)
            {
                _survey.Clear();
                _survey.TrimExcess();
                _gathered.TrimExcess();
                _moves.Clear();
                _surveyCursor = 0;
                _settled = false;
                PerfDirector.Logger?.Info("Performance: " + _id + " looked through the scenery: " + _scenery + " mesh renderers in " + _cells.Count + " squares of " + CellSize + " m"
                                          + "; " + _leftAlone + " left alone (of things that move" + (_byEnabled ? ", disabled, or the game switches them itself" : string.Empty) + ").");
            }
        }

        // Takes out as many of the square's renderers as the slice has time for.
        private void Hold(Cell cell, ref WorkSlice slice)
        {
            for (; cell.Cursor < cell.Scenery.Count && slice.More(); cell.Cursor++)
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
                        continue;   // somebody else's (or ours already, kept out while the map was open): not taken twice
                    }
                    renderer.forceRenderingOff = true;
                }
                if (cell.OnMap[cell.Cursor])
                {
                    cell.Taken.Add(renderer);
                }
                else
                {
                    cell.TakenOff.Add(renderer);
                    _offMapHeld++;
                }
                _heldRenderers++;
            }
            _working = cell.Cursor < cell.Scenery.Count ? cell : null;
        }

        // True if the renderer is on or under an object of a layer of things that move. Asked once per parent object while
        // the renderers are sorted (the answer for a parent is kept until the sorting is done): siblings share their parents.
        private bool Moves(Transform transform)
        {
            if (_movingMask == 0)
            {
                _movingMask = LayerMask.GetMask(MovingLayers);
            }
            if (((_movingMask >> transform.gameObject.layer) & 1) != 0)
            {
                return true;
            }
            Transform parent = transform.parent;
            if (parent == null)
            {
                return false;
            }
            int id = parent.GetInstanceID();
            if (!_moves.TryGetValue(id, out bool moves))
            {
                moves = Moves(parent);
                _moves[id] = moves;
            }
            return moves;
        }

        // True if the game's own code may enable or disable this renderer: it sits on or under one of the switchers.
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

        // Whole and at once: a square is never half back (but for the map: GiveBackForMap).
        private void GiveBack(Cell cell)
        {
            if (_working == cell)
            {
                _working = null;
            }
            Show(cell.Taken);
            Show(cell.TakenOff);
            _heldRenderers -= cell.Taken.Count + cell.TakenOff.Count;
            _offMapHeld -= cell.TakenOff.Count;
            cell.Taken.Clear();
            cell.TakenOff.Clear();
            if (cell.Held)
            {
                cell.Held = false;
                _heldCells--;
            }
        }

        private void Show(List<MeshRenderer> taken)
        {
            foreach (MeshRenderer renderer in taken)
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
        }

        // The city map opens: what it draws comes back, every square; what it does not draw stays out (the square counts as
        // given back, and the timer takes the rest of it out again after the map, as it does a square that came back whole).
        private void GiveBackForMap()
        {
            foreach (Cell cell in _cells)
            {
                if (_working == cell)
                {
                    _working = null;
                }
                Show(cell.Taken);
                _heldRenderers -= cell.Taken.Count;
                cell.Taken.Clear();
                if (cell.Held)
                {
                    cell.Held = false;
                    _heldCells--;
                }
            }
        }

        private void GiveBackNear(Vector3 eye, float reach, bool playersCamera)
        {
            if (_heldRenderers == 0 && _heldCells == 0)
            {
                return;
            }
            for (int i = 0; i < _cells.Count; i++)
            {
                Cell cell = _cells[i];
                if (!cell.Held && cell.TakenOff.Count == 0)
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
                if (cell.Held || cell.Taken.Count > 0 || cell.TakenOff.Count > 0)
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
            _moves.Clear();
            _heldRenderers = _heldCells = _scenery = _leftAlone = _offMapHeld = 0;
        }

        private Action<bool> _onMap;
        private Action<ScriptableRenderContext, Camera> _onCamera;

        // Runs before the game's own handlers (see Apply): the map shows the whole city, of its own layers.
        private void OnMapToggled(bool open)
        {
            if (open && _heldRenderers > 0)
            {
                GiveBackForMap();
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
            if (_heldRenderers == 0 || camera.cameraType != CameraType.Game)
            {
                return;
            }
            try
            {
                Vector3 eye = camera.transform.position;
                if (camera == _main)
                {
                    if (_offMapHeld > 0 && CityMap.IsOpen && (camera.cullingMask & ~_mapMask) != 0)
                    {
                        GiveBackAll();   // the map's camera draws more than the map's layers now: everything, at once, before it draws
                        return;
                    }
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
                GlobalEvents.onCityMapToggle = (Action<bool>)Delegate.Remove(GlobalEvents.onCityMapToggle, _onMap);
                RenderPipelineManager.beginCameraRendering -= _onCamera;
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
            : "renderers out of the picture (" + (_byEnabled ? "disabled" : "forceRenderingOff") + "): " + _heldRenderers + " in " + _heldCells + " of " + _cells.Count + " squares ("
              + _offMapHeld + " of them of layers the city map does not draw); scenery " + _scenery + ", left alone " + _leftAlone;
    }
}
