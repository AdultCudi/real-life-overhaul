using System;
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
    /// own camera, and shown under the game's pins, the route and its panels; the game's own camera draws nothing meanwhile (its
    /// culling mask is set to none, and the game gives it its own mask back when the map shuts). The game picks, hovers and tints
    /// buildings through its real ones, so the picture reads each building's layer and tint colour in slices and writes them into the
    /// blocks. The camera's pitch follows its distance: flat from afar, the game's own tilt close. It stands down, and the game's map
    /// is as it was, in subway mode, taxi mode and when anything throws. Cost: nothing while the map is shut (the object is inactive,
    /// the texture released); open and still, a few comparisons and a slice of 128 buildings a frame; open and moving, one draw of
    /// the chunks in view into the texture.
    /// </summary>
    [DefaultExecutionOrder(30000)]   // after the camera's brain has put the camera where it goes this frame
    public sealed class NavMap25 : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("navigation", "NavMap25", typeof(NavMap25));

        public const string ObjectName = "RealLife Map 2.5D";
        private const float FadeSeconds = 0.4f;       // the blocks come in over this long once the camera has come to rest
        private const float MostSide = 2560f;         // pixels on the longer side of the picture: more is no clearer on a flat picture
        private const int Slice = 128;                // buildings read for a tint in a frame
        private const float RestMetres = 0.5f;        // the camera has come to rest when it is this near the game's map camera

        public static IModLogger Logger;

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
        private bool[] _lit;
        private int _readFrom;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private static readonly int SelectionColour = Shader.PropertyToID("_SelectionColor");
        private float _alpha;
        private bool _masked, _dirty = true, _failed;
        private float _px, _py, _pz, _heading;
        private bool _hasPlayer;
        private float _flatX, _flatZ = 1f;
        private float _lastWidth, _lastHeight;

        /// <summary>The game's map is not drawn over: another mod owns it, or this was switched off.</summary>
        public bool Standing { get; private set; }

        /// <summary>Makes the object in the game's pin layer, switched off. Null if the game's map is not there.</summary>
        public static NavMap25 Make(NavScene scene)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            Transform layer = city != null && city.cityMap != null && city.cityMap.poiTemplate != null ? city.cityMap.poiTemplate.transform.parent : null;
            if (layer == null || scene == null)
            {
                return null;
            }
            var made = new GameObject(ObjectName, typeof(RectTransform));
            made.SetActive(false);
            var rect = (RectTransform)made.transform;
            rect.SetParent(layer, false);
            rect.SetAsFirstSibling();   // under every pin and the route
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

        /// <summary>The map opened or shut; the object is switched on and off with it.</summary>
        public void Open(bool open)
        {
            if (gameObject.activeSelf != open)
            {
                if (open)
                {
                    _rect.SetAsFirstSibling();
                    _alpha = 0f;
                    _dirty = true;
                    _masked = false;
                    _readFrom = 0;
                    _failed = false;
                }
                else
                {
                    Release();
                }
                gameObject.SetActive(open);
            }
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
            if (_failed || map == null || !CityMap.IsOpen || !_scene.Ready)
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
            if (map.isSubwayMode || map.IsTaxiMode)
            {
                Tilt(map, Map25View.TiltPitch, false);   // the game's own map, as it is
                Hide(map);
                return;
            }
            bool rest = Resting();
            Tilt(map, Map25View.PitchFor(map.cityMapCam.distance), rest);
            if (!rest)
            {
                Hide(map);
                return;
            }
            Standing = true;
            Show(map);
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
            Mirror();
            Matrix4x4 view = _camera.worldToCameraMatrix, projection = _camera.projectionMatrix;
            for (int i = 0; i < 16; i++)
            {
                _viewMatrix[i] = view[i];
                _projection[i] = projection[i];
            }
            bool moved = _dirty || _scene.Scene.Pending > 0;
            for (int i = 0; i < 16 && !moved; i++)
            {
                moved = _viewMatrix[i] != _lastView[i] || _projection[i] != _lastProjection[i];
            }
            if (!moved)
            {
                return;
            }
            Array.Copy(_viewMatrix, _lastView, 16);
            Array.Copy(_projection, _lastProjection, 16);
            _dirty = false;
            float pitch = Mathf.Asin(Mathf.Clamp(-_camera.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            _view.Set(_viewMatrix, _projection, pitch);
            _scene.Scene.Refresh();
            var frame = new Map25Frame { Player = _hasPlayer, PlayerX = _px, PlayerY = _py - _scene.Y0, PlayerZ = _pz, Heading = _heading, ConeRadius = Mathf.Max(30f, map.cityMapCam.distance * 0.3f), Fog = pitch < 70f };
            _scene.Scene.Draw(_target, _view, frame, 0);
            PlacePlayer();
        }

        private void MakeTarget(int width, int height)
        {
            Release();
            float scale = Mathf.Min(1f, MostSide / Mathf.Max(width, height));
            _targetWidth = width;
            _targetHeight = height;
            int w = Mathf.Max(64, Mathf.RoundToInt(width * scale)), h = Mathf.Max(64, Mathf.RoundToInt(height * scale));
            _target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "RealLife Map 2.5D",
                antiAliasing = w * h <= 2300000 ? 2 : 1,
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

        // Reads the tint the game put on some of its buildings (the layer it moved a highlighted one to, and the colour in its property
        // block) for a slice of them a frame, and writes it into the blocks: hover, picked, the filters' tints, Going Public's players.
        private void Mirror()
        {
            if (_by == null)
            {
                Gather();
                if (_by == null)
                {
                    return;
                }
            }
            int outlined = LayerHelper.BuildingOutlinedLayerIndex;
            int end = Mathf.Min(_by.Length, _readFrom + Slice);
            for (int i = _readFrom; i < end; i++)
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
                if (on || _lit[i])
                {
                    if (_scene.Tint(i, on, colour))
                    {
                        _dirty = true;   // drawn again only when a tint really changed
                    }
                    _lit[i] = on;
                }
            }
            _readFrom = end >= _by.Length ? 0 : end;
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
            _lit = new bool[data.Hulls];
        }

        private void Hide(CityMap map)
        {
            if (!Standing && !_image.enabled)
            {
                return;
            }
            Standing = false;
            _alpha = 0f;
            _image.enabled = false;
            if (_player != null && _player.gameObject.activeSelf)
            {
                _player.gameObject.SetActive(false);
            }
            if (_masked && map != null && CityMap.IsOpen && _camera != null)
            {
                _camera.cullingMask = PlayerPrefSettings.LowDetailCityMap ? map.cityMapMaskLowDetail : map.cityMapMask;   // subway or taxi mode: the game's own map
            }
            _masked = false;
        }

        private void Release()
        {
            if (_image != null)
            {
                _image.enabled = false;
                _image.texture = null;
            }
            if (_target != null)
            {
                _target.Release();
                UnityEngine.Object.Destroy(_target);
                _target = null;
            }
            if (_masked && _camera != null && CityMap.IsOpen)
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                CityMap map = city != null ? city.cityMap : null;
                if (map != null)
                {
                    _camera.cullingMask = PlayerPrefSettings.LowDetailCityMap ? map.cityMapMaskLowDetail : map.cityMapMask;   // switched off with the map still open: the game's own map
                }
            }
            _masked = false;
            Standing = false;
        }

        private void OnDisable()
        {
            Release();
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}
