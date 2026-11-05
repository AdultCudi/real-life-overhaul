using System;
using System.Runtime.CompilerServices;
using System.Text;
using BAModAPI;
using Cinemachine;
using Helpers;
using RealLifeOverhaul.Map25;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The full map in 2.5D (docs/features/navigation.md, "The city map in 2.5D"): while the game's city map is open and its camera
    /// has come to rest, the blocks of <see cref="NavScene"/> are drawn into a render texture the size of the screen from the game's
    /// own camera, and shown under the game's pins and its panels; the game's own camera draws nothing meanwhile (its culling mask is set to none,
    /// and the game gives it its own mask back when the map shuts). The game picks, hovers and tints buildings through its real ones, so the
    /// picture reads each building's layer and tint colour and writes them into the blocks, but only after something that changes them
    /// (<see cref="NavMapLights"/>: the map opening, the pointer on another building, a card, the filters' work), and the game's neighbourhood zones, which are planes over
    /// the city it no longer draws, are drawn into the picture in their colours. The camera has the game's own tilt at
    /// every distance (the map's 2D button turns it flat), and every way of standing down (the option off, another mod's
    /// route, a fault, a black picture, the map shut) gives it the game's own tilt back.
    /// Cost: nothing while the map is shut (the object is inactive, the texture released); open and still, three comparisons and the camera's
    /// matrices a frame; open and moving, one draw of the chunks in view into the texture.
    /// </summary>
    [DefaultExecutionOrder(30000)]   // after the camera's brain has put the camera where it goes this frame
    public sealed class NavMap25 : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("navigation", "NavMap25", typeof(NavMap25));

        public const string ObjectName = "RealLife Map 2.5D";
        private const float FadeSeconds = 0.4f;       // the blocks come in over this long once the camera has come to rest
        private const float MostSide = 2560f;         // pixels on the longer side of the picture: more is no clearer on a flat picture
        private const float RestMetres = 0.5f;        // the camera has come to rest when it is this near the game's map camera
        private const float ZoneY = 0.05f;            // metres over the ground the game's zones are drawn at

        public static IModLogger Logger;

        /// <summary>True while a picture stands over the game's own city: the mod's route line of the city map (NavCityMap) leaves the route to it.</summary>
        public static bool PictureStands { get; private set; }

        private NavScene _scene;
        private RectTransform _rect;
        private RawImage _image;
        private RectTransform _player;
        private RenderTexture _target;
        private int _targetWidth, _targetHeight;
        private readonly Map25View _view = new Map25View();
        private readonly float[] _viewMatrix = new float[16], _projection = new float[16], _lastView = new float[16], _lastProjection = new float[16];
        private Camera _camera;
        private Transform _virtual;
        private CityBuildingController[] _by;
        private readonly NavMapLights _lights = new NavMapLights();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private static readonly int SelectionColour = Shader.PropertyToID("_SelectionColor");
        private static readonly int ZoneColour = Shader.PropertyToID("_UnlitColor");
        private float _alpha;
        private bool _masked, _dirty = true, _failed, _said, _probed, _pitched, _zonesDirty = true, _zonesShown;
        private readonly NavMapNames _streets = new NavMapNames();
        private float _px, _py, _pz, _heading;
        private bool _hasPlayer;
        private float _flatX, _flatZ = 1f;
        private float _lastWidth, _lastHeight;
        private readonly float[] _zoneCorners = new float[Map25Zones.MostZones * 8], _zoneAlpha = new float[Map25Zones.MostZones], _matrix = new float[16];
        private readonly Map25Colour[] _zoneColour = new Map25Colour[Map25Zones.MostZones];
        private readonly Map25ZoneBook _zonesDrawn = new Map25ZoneBook();
        private MeshFilter[] _zoneMesh;
        private int _workSeen;

        /// <summary>The game's map is not drawn over: another mod owns it, or this was switched off.</summary>
        public bool Standing { get; private set; }

        /// <summary>The object, for the one place that decides where the marker layer's children stand (<see cref="NavMapLayers"/>).</summary>
        public RectTransform Rect => _rect;

        /// <summary>
        /// Makes the object in the game's pin layer, switched off. Null if the game's map is not there. Where it stands among the layer's
        /// children is <see cref="NavDirector"/>'s to say.
        /// </summary>
        public static NavMap25 Make(NavScene scene)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            Transform layer = city != null && city.cityMap != null && city.cityMap.poiTemplate != null ? city.cityMap.poiTemplate.transform.parent : null;
            if (layer == null || scene == null)
            {
                return null;
            }
            NavMapFollow.Install(Logger);   // once a session: the count of the buildings the game's filters work on, which says a toggle's work that ended in one frame
            var made = new GameObject(ObjectName, typeof(RectTransform));
            made.SetActive(false);
            var rect = (RectTransform)made.transform;
            rect.SetParent(layer, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            var map = made.AddComponent<NavMap25>();
            map._scene = scene;
            map._rect = rect;
            map._image = made.AddComponent<RawImage>();
            map._image.raycastTarget = false;
            map._image.enabled = false;
            return map;
        }

        /// <summary>
        /// The map opened or shut; the object is switched on and off with it. Shut, every tint it took into the blocks is taken out again and
        /// the game's map camera has its own tilt back.
        /// </summary>
        public void Open(bool open)
        {
            if (gameObject.activeSelf != open)
            {
                if (open)
                {
                    _alpha = 0f;
                    _dirty = true;
                    _masked = false;
                    _zonesDirty = true;
                    _zonesDrawn.Forget();   // the scene may hold the zones an earlier picture drew: the first look at the game's puts them right whatever they are
                    _lights.Reset(_scene != null && _scene.Ready ? _scene.City.Count : 0);
                    _lights.Poke();   // the game applies its filters as it opens: one pass to find them
                }
                else
                {
                    Release();
                    ClearTints();
                }
                gameObject.SetActive(open);
            }
        }

        /// <summary>The street names to write on the picture, once every road has its name.</summary>
        public void Names(Map25Names names)
        {
            _streets.Set(names);
            _dirty = true;
        }

        /// <summary>Where the player is, for the cone and the marker (on the slow tick).</summary>
        public void Player(float x, float y, float z, float heading)
        {
            _px = x;
            _py = y;
            _pz = z;
            _heading = heading;
            _hasPlayer = true;
            _dirty = true;
        }

        /// <summary>The route changed: the picture is drawn again at the next frame.</summary>
        public void RouteChanged()
        {
            _dirty = true;
        }

        /// <summary>Something on the open map changed its look that the game does not announce (the district view put a colour on a zone): the zones and the highlights are looked at again.</summary>
        public void Poke()
        {
            _zonesDirty = true;
            _lights.Poke();
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Navigation: NavMap25.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                Release();
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
                Standing = false;
                _failed = true;
                Release();
            }
        }

        private void OnLateUpdate()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityMap map = city != null ? city.cityMap : null;
            if (map == null || !CityMap.IsOpen)
            {
                Hide(map);
                return;
            }
            if (_camera == null)
            {
                _camera = GameManager.GetMainCamera();
                CinemachineVirtualCameraBase vcam = InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.citymapCamera : null;
                _virtual = vcam != null ? vcam.transform : null;
            }
            if (_camera == null || _virtual == null || map.cityMapCam == null)
            {
                Hide(map);
                return;
            }
            if (_failed || NavMapLatch.Drew(NavMapLatch.Full) || !_scene.Ready)
            {
                // the game's own map is showing: the 2D and 3D button still turns its camera, so the button works on both maps
                if (!map.isSubwayMode && !map.IsTaxiMode)
                {
                    try
                    {
                        Tilt(map, NavMapTilt.Pitch(NavTilt.Map, map.cityMapCam.distance), true);
                    }
                    catch (Exception)
                    {
                        _pitched = false;
                    }
                }
                Hide(map);
                return;
            }
            if (map.isSubwayMode || map.IsTaxiMode)
            {
                Tilt(map, Map25View.TiltPitch, false);   // the game's own map, as it is
                _pitched = false;
                Hide(map);
                return;
            }
            bool rest = Resting();
            Tilt(map, NavMapTilt.Pitch(NavTilt.Map, map.cityMapCam.distance), rest);   // the map's own button can fix it flat or tilted at any distance
            if (!rest)
            {
                Hide(map);
                return;
            }
            Standing = true;
            PictureStands = true;
            Show(map);
            if (!_said && Meta.Dev.Verbose && _target != null)
            {
                _said = true;
                SayShown(map);
            }
        }

        // One line, the first time the picture shows (with Developer diagnostics on).
        private void SayShown(CityMap map)
        {
            Logger?.Info("Navigation: the 2.5D city map shows: a picture of " + _target.width + " x " + _target.height + " pixels ("
                         + _target.antiAliasing + "x multisampled), the game's own camera drawing nothing behind it, pitch "
                         + Mathf.RoundToInt(_view.PitchDegrees) + " degrees at " + Mathf.RoundToInt(map.cityMapCam.distance)
                         + " m, the projection given " + (Map25Scene.IntoTexture ? "for a texture" : "plain") + ".");
        }

        // The game's own camera has come where the map's camera is.
        private bool Resting()
        {
            Transform eye = _camera.transform;
            return (eye.position - _virtual.position).sqrMagnitude < RestMetres * RestMetres && Quaternion.Angle(eye.rotation, _virtual.rotation) < 1.5f;
        }

        // The map camera's pitch eased to what its distance asks, by moving it along the arc round the pivot the game's own zoom keeps (it recomputes the
        // camera's place from its direction and its own distance every frame). Not eased while the camera is still flying in, so the fly goes to it.
        private void Tilt(CityMap map, float want, bool eased)
        {
            Vector3 pivot = map.cityMapCam.transform.position;
            Vector3 away = pivot - _virtual.position;
            float distance = away.magnitude;
            if (distance < 1f)
            {
                return;
            }
            float pitch = Mathf.Asin(Mathf.Clamp(-away.y / distance, -1f, 1f)) * Mathf.Rad2Deg;
            float flat = Mathf.Sqrt(away.x * away.x + away.z * away.z);
            if (flat > 0.01f)
            {
                _flatX = away.x / flat;
                _flatZ = away.z / flat;
            }
            if (Mathf.Abs(want - pitch) < 0.2f)
            {
                return;
            }
            float next = eased ? pitch + (want - pitch) * Mathf.Min(1f, Time.unscaledDeltaTime * 6f) : want;
            float radians = next * Mathf.Deg2Rad;
            var direction = new Vector3(_flatX * Mathf.Cos(radians), -Mathf.Sin(radians), _flatZ * Mathf.Cos(radians));
            _virtual.position = pivot - direction * distance;
            _virtual.rotation = Quaternion.LookRotation(direction);
            _pitched = want != Map25View.TiltPitch || next != Map25View.TiltPitch;
        }

        // The game's own tilt back on its map camera: whatever way the picture stands down, and when the map shuts (the game keeps the
        // direction it last had, and has no control of the pitch to undo ours).
        private void RestorePitch()
        {
            if (!_pitched || _virtual == null)
            {
                return;
            }
            _pitched = false;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityMap map = city != null ? city.cityMap : null;
            if (map != null && map.cityMapCam != null)
            {
                Tilt(map, Map25View.TiltPitch, false);
                _pitched = false;
            }
        }

        private void Show(CityMap map)
        {
            int width = Screen.width, height = Screen.height;
            if (width < 64 || height < 64)
            {
                return;
            }
            if (_target == null || _targetWidth != width || _targetHeight != height)
            {
                MakeTarget(width, height);
            }
            if (!_image.enabled)
            {
                _image.enabled = true;
                _image.texture = _target;
            }
            Place(width, height);
            _alpha = Mathf.Min(1f, _alpha + Time.unscaledDeltaTime / FadeSeconds);
            _image.color = new Color(1f, 1f, 1f, _alpha);
            if (_alpha >= 0.999f && !_masked)
            {
                _masked = true;
                _camera.cullingMask = 0;   // the game's own city is not drawn behind the picture; the game gives its mask back when the map shuts
            }
            Lights();
            if (_zonesDirty)
            {
                ReadZones();   // before the decision below: a zone the district view tints or a neighbourhood line the filters toggle moves no camera
            }
            Matrix4x4 view = _camera.worldToCameraMatrix, projection = _camera.projectionMatrix;
            for (int i = 0; i < 16; i++)
            {
                _viewMatrix[i] = view[i];
                _projection[i] = projection[i];
            }
            if (!NavMapMoves.Needed(_dirty, _scene.Scene.Pending, _viewMatrix, _lastView, _projection, _lastProjection))
            {
                return;
            }
            Array.Copy(_viewMatrix, _lastView, 16);
            Array.Copy(_projection, _lastProjection, 16);
            _dirty = false;
            float pitch = Mathf.Asin(Mathf.Clamp(-_camera.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            // the scene's ground is the median street: the game's view is moved the same, so the picture lies where the game's own city does
            _view.Set(_viewMatrix, _projection, pitch, _scene.Y0);
            _scene.Scene.Refresh();
            // the lines and marks are a few pixels, however far the camera is: their metres are what a pixel is where the middle of the picture lies
            float gx = _px, gz = _pz;
            if (!_view.GroundAhead(out gx, out gz) && _hasPlayer)
            {
                gx = _px;
                gz = _pz;
            }
            float mpp = _view.MetresPerPixel(gx, 0f, gz, _target.width);
            _scene.Scene.SetRouteHalf(Map25Marks.Step(Map25Marks.RouteHalf(false, _target.height, mpp)));
            var frame = new Map25Frame
            {
                Player = _hasPlayer, PlayerX = _px, PlayerY = _py - _scene.Y0, PlayerZ = _pz, Heading = _heading,
                ConeRadius = Map25Marks.ConeMetres(false, _target.height, mpp), HaloRadius = Map25Marks.HaloMetres(false, _target.height, mpp), Fog = pitch < 70f
            };
            _scene.Scene.Draw(_target, _view, frame, 0);
            if (!_probed)
            {
                _probed = true;
                if (Map25Scene.Blank(_target))
                {
                    if (NavMapLatch.Mark(NavMapLatch.Full))
                    {
                        Logger?.Warn("Navigation: the 2.5D city map drew nothing (its picture is black), so the city map stays the game's own for this session.");
                    }
                    Release();
                    return;
                }
            }
            float scale = _rect.lossyScale.x > 0.0001f ? _rect.lossyScale.x : 1f;
            try
            {
                _streets.Place(_rect, _view, width, height, 1f / scale, 0.3f);
            }
            catch (Exception e)
            {
                // the names are optional and the blocks are not: a fault in them takes the names away, never the map
                _streets.Set(null);
                _streets.Hide();
                Logger?.Warn("Navigation: the street names on the 2.5D city map are off for this session: " + e.GetType().Name + " " + e.Message);
            }
            PlacePlayer();
        }

        private void MakeTarget(int width, int height)
        {
            ReleaseTarget();
            float scale = Mathf.Min(1f, MostSide / Mathf.Max(width, height));
            _targetWidth = width;
            _targetHeight = height;
            int w = Mathf.Max(64, Mathf.RoundToInt(width * scale)), h = Mathf.Max(64, Mathf.RoundToInt(height * scale));
            _target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "RealLife Map 2.5D",
                antiAliasing = w * h <= 2300000 ? 4 : 2,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                hideFlags = HideFlags.HideAndDontSave
            };
            _target.Create();
            _dirty = true;
            if (_image != null)
            {
                _image.texture = _target;
            }
        }

        // As large as the screen, from its lower left corner, in whatever units the game's canvas has.
        private void Place(int width, int height)
        {
            float scale = _rect.lossyScale.x > 0.0001f ? _rect.lossyScale.x : 1f;
            if (width != _lastWidth || height != _lastHeight || _rect.position != Vector3.zero)
            {
                _lastWidth = width;
                _lastHeight = height;
                _rect.position = Vector3.zero;
                _rect.sizeDelta = new Vector2(width / scale, height / scale);
            }
        }

        private void PlacePlayer()
        {
            if (!_hasPlayer)
            {
                return;
            }
            if (_player == null)
            {
                _player = MakePlayer();
            }
            if (_player == null)
            {
                return;
            }
            Vector3 at = _camera.WorldToScreenPoint(new Vector3(_px, _py + 2f, _pz));
            bool visible = at.z > 0.1f;
            if (_player.gameObject.activeSelf != visible)
            {
                _player.gameObject.SetActive(visible);
            }
            if (visible)
            {
                at.z = 0f;
                _player.position = at;
            }
        }

        // The player's marker on the picture: the round base and the game's own player icon, in the colour the game's map gives them.
        private RectTransform MakePlayer()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.mapFilters == null || NavMarks.Blob == null)
            {
                return null;
            }
            var made = new GameObject("Player", typeof(RectTransform));
            var rect = (RectTransform)made.transform;
            rect.SetParent(_rect, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            float scale = _rect.lossyScale.x > 0.0001f ? _rect.lossyScale.x : 1f;
            float size = 64f * Screen.height / 1080f / scale;
            rect.sizeDelta = new Vector2(size, size);
            var blob = made.AddComponent<Image>();
            blob.sprite = NavMarks.Blob;
            blob.color = uis.mapFilters.playerFilterColor;
            blob.raycastTarget = false;
            var icon = new GameObject("Icon", typeof(RectTransform));
            var inner = (RectTransform)icon.transform;
            inner.SetParent(rect, false);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(size * 0.2f, size * 0.2f);
            inner.offsetMax = new Vector2(-size * 0.2f, -size * 0.2f);
            var image = icon.AddComponent<Image>();
            image.sprite = uis.mapFilters.playerIcon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }

        // Once a frame while the picture shows: the things that tell the game's highlights moved (the filters have work or worked on a building since the last look, the pointer
        // is on another building, another building's card is open), and, while a pass is under way, a slice of the buildings read. Nothing else is read while none changed.
        private void Lights()
        {
            if (_lights.Hulls != _scene.City.Count)
            {
                _lights.Reset(_scene.City.Count);   // the city came into being (or changed) while the map was open
                _lights.Poke();
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            // The game works its queue inside its own update, up to two milliseconds a frame: a toggle whose work fits in the frame it began in leaves the queue empty by the time
            // this runs. So the count of the buildings it worked on is asked every frame (NavMapFollow), and where the queue could not be counted a mouse button coming up is
            // taken for the click that toggled a filter (its work is done by now or by the next frame, and a pass follows both).
            bool counted = NavMapWork.Took(ref _workSeen);
            bool released = !NavMapWork.Following && Input.GetMouseButtonUp(0);
            bool working = counted || released || CityMapFilters.Work.HasPendingWork;
            int hover = RuntimeHelpers.GetHashCode(MouseController.currentTargetEntity);
            int selected = uis != null && uis.buildingResume != null ? RuntimeHelpers.GetHashCode(uis.buildingResume.CityBuildingController) : 0;
            if (!_lights.Step(working, hover, selected, out int from, out int to))
            {
                return;
            }
            if (from == 0)
            {
                _zonesDirty = true;   // a pass begins with the zones, whose changes the game does not announce either
            }
            if (_by == null)
            {
                Gather();
                if (_by == null)
                {
                    return;
                }
            }
            int outlined = LayerHelper.BuildingOutlinedLayerIndex;
            bool changed = false;
            for (int i = from; i < to && i < _by.Length; i++)
            {
                CityBuildingController building = _by[i];
                MeshRenderer lod = building != null ? building.lodVersion : null;
                if (lod == null)
                {
                    continue;
                }
                bool on = lod.gameObject.layer == outlined;
                Color32 colour = default;
                if (on)
                {
                    lod.GetPropertyBlock(_block);
                    on = _block.HasColor(SelectionColour);
                    if (on)
                    {
                        colour = _block.GetColor(SelectionColour);
                    }
                }
                if (on || _lights.IsLit(i))
                {
                    if (_scene.Tint(i, on, colour))
                    {
                        changed = true;   // drawn again only when a tint really changed
                    }
                    _lights.Set(i, on);
                }
            }
            if (changed)
            {
                _scene.RefreshMarks();
                _dirty = true;
            }
        }

        // Every tint the picture took into the blocks taken out again (the map shut, or the picture stood down): the blocks are as the game's city is.
        private void ClearTints()
        {
            if (_scene == null || !_scene.Ready)
            {
                _lights.Reset(0);
                return;
            }
            int cursor = 0;
            bool any = false;
            for (int hull = _lights.TakeNext(ref cursor); hull >= 0; hull = _lights.TakeNext(ref cursor))
            {
                any |= _scene.Tint(hull, false, default);
            }
            if (any)
            {
                _scene.RefreshMarks();
            }
            _lights.Reset(_scene.City.Count);
        }

        // The game's neighbourhood zones that show, drawn into the picture in the colour each has (the district view's tints and the game's
        // own filters' alike): the game's zones are planes in a city this picture covers.
        private void ReadZones()
        {
            _zonesDirty = false;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.cityMap == null || city.cityMap.cityMapNeighborhoodZones == null)
            {
                return;
            }
            CityMapNeighborhoodZones.CityMapNeighborhoodZone[] zones = CityMapNeighborhoodZones.Zones;
            if (zones == null)
            {
                return;
            }
            if (_zoneMesh == null || _zoneMesh.Length != zones.Length)
            {
                _zoneMesh = new MeshFilter[zones.Length];
                for (int i = 0; i < zones.Length; i++)
                {
                    _zoneMesh[i] = zones[i] != null && zones[i].zoneRenderer != null ? zones[i].zoneRenderer.GetComponent<MeshFilter>() : null;
                }
            }
            int n = 0;
            for (int i = 0; i < zones.Length && n < Map25Zones.MostZones; i++)
            {
                MeshRenderer zone = zones[i] != null ? zones[i].zoneRenderer : null;
                MeshFilter filter = _zoneMesh[i];
                if (zone == null || filter == null || filter.sharedMesh == null || !zone.enabled || !zone.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Matrix4x4 m = zone.transform.localToWorldMatrix;
                for (int k = 0; k < 16; k++)
                {
                    _matrix[k] = m[k];
                }
                Bounds b = filter.sharedMesh.bounds;
                Map25Zones.Rectangle(_matrix, b.center.x, b.center.y, b.center.z, b.extents.x, b.extents.y, b.extents.z, out float x0, out float z0, out float x1, out float z1);
                Map25Zones.Corners(x0, z0, x1, z1, _zoneCorners, n);
                zone.GetPropertyBlock(_block);
                Color c = new Color(1f, 0f, 0f, 0.47f);   // the zone material's own colour, when neither the block nor the material says
                if (_block.HasColor(ZoneColour))
                {
                    c = _block.GetColor(ZoneColour);
                }
                else if (zone.sharedMaterial != null && zone.sharedMaterial.HasProperty(ZoneColour))
                {
                    c = zone.sharedMaterial.GetColor(ZoneColour);
                }
                _zoneColour[n] = new Map25Colour(c.r, c.g, c.b);
                _zoneAlpha[n] = c.a;
                n++;
            }
            _zonesShown = n > 0;
            if (_zonesDrawn.Take(_zoneCorners, _zoneColour, _zoneAlpha, n))
            {
                _scene.Scene.SetZones(_zoneCorners, _zoneColour, _zoneAlpha, n, ZoneY);
                _dirty = true;   // a zone showed, went, changed its colour or moved: drawn again, whatever the camera does
            }
        }

        // Which of the game's buildings each outline is, by address (a pass over the game's list: once, as the map first shows).
        private void Gather()
        {
            NavBaseData data = _scene.Data;
            CityBuildingController[] buildings = NavBaseGame.Buildings();
            if (data == null || buildings == null)
            {
                return;
            }
            var found = new CityBuildingController[data.Hulls];
            NavBaseGame.Find(buildings, data, found);
            _by = found;
        }

        private void Hide(CityMap map)
        {
            PictureStands = false;
            HidePieces();
            if (!Standing && !_image.enabled)
            {
                return;
            }
            Standing = false;
            _alpha = 0f;
            _image.enabled = false;
            GiveCameraBack(map);
        }

        // Our own labels and marker go whenever the picture is not showing, on every path (a fault left the marker on the game's map once).
        private void HidePieces()
        {
            _streets.Hide();
            if (_player != null && _player.gameObject.activeSelf)
            {
                _player.gameObject.SetActive(false);
            }
        }

        // The game's own culling mask given back while the map is still open (subway or taxi mode, or the picture standing down): its own map.
        private void GiveCameraBack(CityMap map)
        {
            if (_masked && map != null && CityMap.IsOpen && _camera != null)
            {
                _camera.cullingMask = PlayerPrefSettings.LowDetailCityMap ? map.cityMapMaskLowDetail : map.cityMapMask;
            }
            _masked = false;
        }

        // Every way the picture stops standing: the texture let go, the game's culling mask and its map camera's tilt given back.
        private void Release()
        {
            PictureStands = false;
            HidePieces();
            if (_image != null)
            {
                _image.enabled = false;
                _image.texture = null;
            }
            ReleaseTarget();
            if (_masked && _camera != null && CityMap.IsOpen)
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                CityMap map = city != null ? city.cityMap : null;
                GiveCameraBack(map);
            }
            _masked = false;
            Standing = false;
            RestorePitch();
        }

        private void ReleaseTarget()
        {
            if (_target != null)
            {
                _target.Release();
                UnityEngine.Object.Destroy(_target);
                _target = null;
            }
        }

        /// <summary>
        /// For the map check after the map shut (<see cref="MapParts"/>, RLO-PERF-022): what the picture still holds changed or alive: its object, its texture, the game's
        /// camera masked or tilted, a tint in the blocks. 0 when all is as it was before the map opened.
        /// </summary>
        public int Held(StringBuilder line)
        {
            int held = 0;
            bool first = true;
            line?.Append("2.5D city map: ");
            if (gameObject.activeSelf)
            {
                held++;
                Note(line, ref first, "object on");
            }
            if (_target != null)
            {
                held++;
                Note(line, ref first, "texture alive");
            }
            if (_masked)
            {
                held++;
                Note(line, ref first, "camera masked");
            }
            if (_pitched)
            {
                held++;
                Note(line, ref first, "camera tilted");
            }
            if (_lights.Lit > 0)
            {
                held++;
                Note(line, ref first, "tints kept");
            }
            if (held == 0)
            {
                line?.Append("given back");
            }
            return held;
        }

        private static void Note(StringBuilder line, ref bool first, string what)
        {
            if (line != null)
            {
                line.Append(first ? string.Empty : ", ").Append(what);
            }
            first = false;
        }

        private void OnDisable()
        {
            Release();
        }

        private void OnDestroy()
        {
            Release();
            ClearTints();
        }
    }
}
