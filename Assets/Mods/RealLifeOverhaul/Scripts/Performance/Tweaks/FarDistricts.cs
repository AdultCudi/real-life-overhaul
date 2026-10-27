using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Takes the mesh renderers of every 400 m square beyond 1300 m out of the picture (the engine tests some 186,000 a
    /// frame to draw 4,000), and gives a square back whole before any camera within 1100 m draws. The option uses
    /// forceRenderingOff, which the game never writes; the trial, enabled, minus GameContract.Switchers. Nothing of a moving
    /// layer is sorted (pooled parked cars move to near kerbs). Work on the director's timer, a millisecond a tick; scenes
    /// gathered only behind a screen. Cost a frame: one comparison a camera, one more while the map is open.
    /// </summary>
    public sealed class FarDistricts : PerfTweak
    {
        private const float CellSize = 400f;   // metres
        private const float Margin = 60f;   // metres a renderer reaches beyond the point it stands on, at most
        internal const float HoldBeyond = 1300f;   // metres from the camera to the nearest point of a square
        private const float ReleaseWithin = 1100f;   // metres
        private const float Alarm = 500f;   // metres: a held square this near means the camera jumped, worth a log line
        private const float Cooldown = 5f;   // seconds after squares were given back for the map or a picture

        // layers of things the game moves about, pooled or driven: never scenery, whatever scene they stand in
        private static readonly string[] MovingLayers = { "ParkedVehicles", "AiVehicles", "PlayerVehicles", "Vehicles", "Human" };

        // scenes that are not scenery standing in one place
        private static readonly string[] NotScenery = { "MainScene", "UI", "Indoor", "LightsAndPostprocessing", "DontDestroyOnLoad" };

        private sealed class Cell
        {
            public Bounds Bounds;
            public bool Held;
            public int Cursor;   // how far through Scenery the holding has got
            public readonly List<MeshRenderer> Scenery = new List<MeshRenderer>(256);
            public readonly List<bool> OnMap = new List<bool>(256);   // for each of those: its layer is one the city map draws
            public readonly List<MeshRenderer> Taken = new List<MeshRenderer>(256);   // taken out, drawn by the city map
            public readonly List<MeshRenderer> TakenOff = new List<MeshRenderer>(64);   // taken out, not drawn by the map: kept out while it is open
        }

        private const double SliceMs = 1.0;   // milliseconds of a tick the sorting or holding may take
        private const int LookEvery = 64;              // renderers between two looks at the clock
        private const float HiddenEvery = 0.25f;   // seconds between looks at whether the screen is black, while a scene waits

        private readonly bool _byEnabled;
        private readonly string _id;
        private readonly Dictionary<int, bool> _moves = new Dictionary<int, bool>(4096);   // while sorting: a parent object (its id) on or under something that moves
        private int _mapMask = ~0;   // the layers both the map's masks draw; all where the map is not known
        private int _offMapHeld;       // renderers held that the city map does not draw
        private bool _waitHidden;   // a scene came or went in play: gathered behind the next black screen

        private readonly Dictionary<long, Cell> _byKey = new Dictionary<long, Cell>(512);
        private readonly List<Cell> _cells = new List<Cell>(512);
        // Squares of a city gone, kept with their lists' room: the survey is the one thing here that allocates (some 400,000
        // renderers sorted into 58 squares once a city, a few megabytes of lists in the first minute), and once a session.
        private readonly List<Cell> _spareCells = new List<Cell>(64);
        private readonly List<GameObject> _roots = new List<GameObject>(64);
        private readonly List<MeshRenderer> _gathered = new List<MeshRenderer>(32768);
        private readonly List<MeshRenderer> _survey = new List<MeshRenderer>(65536);
        private readonly HashSet<int> _switcherObjects = new HashSet<int>();
        private readonly HashSet<int> _seenScenes = new HashSet<int>();
        private readonly List<Scene> _sceneryScenes = new List<Scene>(8);   // the scenes that had scenery in them
        private Type[] _switcherTypes;

        private int _surveyCursor;
        private int _handedTaken;   // how many of Handed this instance has put in its survey
        private Cell _working;   // a square half-way through being held
        private bool _stale;
        private bool _settled;   // the last look found every square as it should be
        private float _quietUntil;
        private Camera _main;
        private Vector3 _lastChecked;
        private bool _hooked, _alarmed;
        private int _heldRenderers, _heldCells, _scenery, _leftAlone, _movingMask;

        // The buildings the mod stands in the city (Scripts/NewBuildings) live in no scene of scenery, so they are handed in;
        // each instance takes what was handed in since it last looked into its survey at its next tick.
        private static readonly List<MeshRenderer> Handed = new List<MeshRenderer>(512);
        private static readonly List<MeshRenderer> HandedNow = new List<MeshRenderer>(64);
        // Renderers whose drawing the mod holds itself (a building's look hidden under a copy): never taken or given back here.
        private static readonly HashSet<int> LeftAlone = new HashSet<int>();

        /// <summary>Hands in the mesh renderers of a building the mod stood in the city; each instance sorts them into its squares at its next tick.</summary>
        public static void Include(Transform root)
        {
            if (root == null)
            {
                return;
            }
            root.GetComponentsInChildren(true, HandedNow);
            Handed.AddRange(HandedNow);
            HandedNow.Clear();
        }

        /// <summary>
        /// Leaves a renderer's drawing to the mod (<paramref name="leave"/>), or gives it back; true when a square held now
        /// has it, so it is to be out of the picture. A pass over the held squares: for a look hidden or shown, not per frame.
        /// </summary>
        public static bool LeaveAlone(Renderer renderer, bool leave)
        {
            if (renderer == null)
            {
                return false;
            }
            if (leave)
            {
                LeftAlone.Add(renderer.GetInstanceID());
                return false;
            }
            LeftAlone.Remove(renderer.GetInstanceID());
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                if (tweak is FarDistricts far && far.Active && far.HoldsNow(renderer))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The city is going: what the mod handed in and left alone is forgotten.</summary>
        public static void Release()
        {
            Handed.Clear();
            LeftAlone.Clear();
        }

        private bool HoldsNow(Renderer renderer)
        {
            foreach (Cell cell in _cells)
            {
                if (cell.Held && (cell.Taken.Contains(renderer as MeshRenderer) || cell.TakenOff.Contains(renderer as MeshRenderer)))
                {
                    return true;
                }
            }
            return false;
        }

        // What the mod handed in since this instance last looked, into the survey (a city gone forgets it all).
        private void TakeHanded()
        {
            if (_handedTaken > Handed.Count)
            {
                _handedTaken = 0;
            }
            while (_handedTaken < Handed.Count)
            {
                _survey.Add(Handed[_handedTaken++]);
            }
        }

        private static bool Kept(Renderer renderer) => LeftAlone.Count > 0 && LeftAlone.Contains(renderer.GetInstanceID());

        /// <param name="byEnabled">False: forceRenderingOff on every mesh renderer. True: the benchmark trial, by enabled.</param>
        public FarDistricts(bool byEnabled)
        {
            _byEnabled = byEnabled;
            _id = byEnabled ? "trial_far_by_enabled" : "far_districts";
        }

        public override string Id => _id;
        public override string LabelKey => _byEnabled ? null : "reallife_perf_far_districts2";
        public override bool DefaultOn => false;   // not measured yet
        public override bool Trial => _byEnabled;
        public override float Interval => _waitHidden && _surveyCursor >= _survey.Count && _working == null ? HiddenEvery : Busy ? 0.05f : 0.5f;
        public override bool Busy => _stale || _surveyCursor < _survey.Count || _working != null || !_settled;

        protected override void Apply()
        {
            if (_byEnabled)
            {
                if (GameContract.FarDistricts.Off)
                {
                    // a listed component is gone after a game update: this trial cannot be safe without all of them
                    throw new InvalidOperationException("the game's components that switch renderers have changed: " + GameContract.FarDistricts.Why);
                }
                _switcherTypes ??= FindSwitcherTypes();
            }
            // in front of the game's own map handlers: when they run, every renderer is as the game left it
            _onMap = _onMap ?? Guard.Wrap<bool>(OnMapToggled);   // through the guard: a fault never reaches the game or the pipeline
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

        // both the map's masks (the player may switch); all layers where there is no map, so it gives everything back
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
            _stale = true;   // an unloaded scene matters too: its squares are forgotten
            _waitHidden = false;
        }

        // the scenes' renderers gathered behind the loading screen (some 357,000: 127 ms); the timer sorts them a slice a tick
        protected override void Prewarm()
        {
            if (_byEnabled)
            {
                return;   // the benchmark's trial: never wanted at a load
            }
            Refresh();
            if (_stale)
            {
                Refresh();   // an earlier city's scenes were forgotten first: now the ones here
            }
        }

        // the timer only takes squares out; OnCameraRendering gives them back the moment it is needed
        protected override void Tick(float now)
        {
            TakeHanded();
            if (_surveyCursor < _survey.Count)
            {
                Survey();
                return;
            }
            if (_stale && !_waitHidden)
            {
                if (!Changed())
                {
                    _stale = false;   // nothing with scenery came or went
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

        // a scene with scenery gone or one not looked through come: a look at the scenes' handles and names only
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

        // A new scene is queued to be looked through once; small ones that come and go (one around every save) cost a look
        // at their root objects. A scene with scenery gone starts everything again.
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
                using var piece = SessionWatch.Piece(
                    "performance: the scenery's renderers being gathered for the far districts (every object of a scene not looked through yet)");
                scene.GetRootGameObjects(_roots);
                foreach (GameObject root in _roots)
                {
                    // the safe way takes inactive objects' renderers too: one activated later is then already out
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
                // which objects the game switches itself, over everything loaded: once a round of new scenes
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

        // sorts the gathered renderers into squares, a slice a tick
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
                    cell = Fresh();
                    cell.Bounds = new Bounds(at, Vector3.one * (Margin * 2f));
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
                PerfDirector.Logger?.Info("Performance: " + _id + " looked through the scenery: " + _scenery + " mesh renderers in " + _cells.Count + " squares of " + CellSize
                                          + " m; " + _leftAlone + " left alone (of things that move" + (_byEnabled ? ", disabled, or the game switches them itself" : string.Empty) + ").");
            }
        }

        private Cell Fresh()
        {
            if (_spareCells.Count == 0)
            {
                return new Cell();
            }
            Cell cell = _spareCells[_spareCells.Count - 1];
            _spareCells.RemoveAt(_spareCells.Count - 1);
            return cell;
        }

        // takes out as many of the square's renderers as the slice has time for
        private void Hold(Cell cell, ref WorkSlice slice)
        {
            for (; cell.Cursor < cell.Scenery.Count && slice.More(); cell.Cursor++)
            {
                MeshRenderer renderer = cell.Scenery[cell.Cursor];
                if (renderer == null || !cell.Bounds.Contains(renderer.transform.position) || Kept(renderer))
                {
                    continue;   // gone, moved since it was sorted (it may stand in view), or its drawing is the mod's
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
                        continue;   // someone else's, or ours kept out while the map was open: not taken twice
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

        // on or under an object of a moving layer; asked once a parent while sorting, as siblings share their parents
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

        // the game's own code may switch this renderer: it sits on or under one of the switchers
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

        // whole and at once: a square is never half back, but for the map
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
                if (renderer == null || Kept(renderer))
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

        // The map opens: what it draws comes back; the rest stays out, the square counts as given back, and the timer takes
        // it out again after the map.
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
                    _alarmed = true;   // once a session: a jump (taxi, subway, hospital) is expected now and then
                    PerfDirector.Logger?.Info("Performance: " + _id + " found a held square " + Mathf.Sqrt(distance).ToString("F0")
                                              + " m from the camera (the camera jumped) and gave it back before the frame was drawn.");
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
            for (int i = 0; i < _cells.Count; i++)
            {
                Cell gone = _cells[i];
                gone.Held = false;
                gone.Cursor = 0;
                gone.Scenery.Clear();
                gone.OnMap.Clear();
                gone.Taken.Clear();
                gone.TakenOff.Clear();
                _spareCells.Add(gone);
            }
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

        // before the game's own handlers (see Apply): the map shows the whole city, of its own layers
        private void OnMapToggled(bool open)
        {
            if (open && _heldRenderers > 0)
            {
                GiveBackForMap();
            }
            _quietUntil = Time.unscaledTime + Cooldown;
            _settled = false;
        }

        // Once a camera a frame, just before the pipeline culls for it, so what comes back is in this frame. The player's
        // camera looks again after moving 5 m (on the frame of a jump).
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
                        GiveBackAll();   // the map's camera draws more than its layers: everything back before it draws
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

        // the squares are kept (looking through the scenery is the dear part); everything taken out goes back
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
            : "renderers out of the picture (" + (_byEnabled ? "disabled" : "forceRenderingOff") + "): " + _heldRenderers + " in " + _heldCells + " of " + _cells.Count
              + " squares (" + _offMapHeld + " of them of layers the city map does not draw); scenery " + _scenery + ", left alone " + _leftAlone;
    }
}
