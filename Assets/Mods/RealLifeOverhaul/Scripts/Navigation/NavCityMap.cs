using System;
using BAModAPI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The GPS's part of the game's city map: the route, in the markers' own layer under them, worked out again only in
    /// a frame the map's camera has moved, and a double click that sets a destination (one click drags the map or picks
    /// a building). Switched off, costing nothing, while the map is shut.
    /// </summary>
    public sealed class NavCityMap : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("navigation", "NavCityMap", typeof(NavCityMap));

        public const string ObjectName = "RealLife Map Route";
        private const float TapSeconds = 0.3f;     // seconds a click may be held
        private const float TapPixels = 6f;        // pixels it may move
        private const float DoubleSeconds = 0.4f;  // seconds between the two clicks
        private const float DoublePixels = 12f;    // pixels between them

        public static IModLogger Logger;

        private NavRouteLine _line;
        private RectTransform _rect;
        private float[] _wx, _wy, _wz;      // the route in the world: the director's arrays
        private float[] _sx = new float[0];
        private float[] _sy = new float[0];
        private int _count;
        private bool _stale = true;
        private Vector3 _cameraAt;
        private Quaternion _cameraTurn;
        private float _cameraFov;
        private int _screenWidth, _screenHeight;

        private float _downAt = -10f, _tapAt = -10f;
        private Vector2 _downPlace, _tapPlace;
        private bool _downOnInterface;
        private bool _failed;

        /// <summary>Whether a double click sets a destination.</summary>
        public bool Clicks;

        /// <summary>Called with the place on the screen that was clicked twice.</summary>
        public Action<Vector2> Picked;

        /// <summary>Makes the object in the map's marker layer, switched off. Null if the game's map is not there.</summary>
        public static NavCityMap Make(Color32 colour)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            Transform layer = city != null && city.cityMap != null && city.cityMap.poiTemplate != null ? city.cityMap.poiTemplate.transform.parent : null;
            if (layer == null)
            {
                return null;
            }
            var made = new GameObject(ObjectName, typeof(RectTransform));
            made.SetActive(false);
            var rect = (RectTransform)made.transform;
            rect.SetParent(layer, false);
            rect.SetAsFirstSibling();   // under every marker
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            var map = made.AddComponent<NavCityMap>();
            map._rect = rect;
            map._line = made.AddComponent<NavRouteLine>();
            map._line.color = colour;
            map._line.raycastTarget = false;
            return map;
        }

        public void Open(bool open)
        {
            if (gameObject.activeSelf != open)
            {
                _stale = true;
                _tapAt = -10f;
                gameObject.SetActive(open);
            }
        }

        /// <summary>The route in the world (x, height, z), or none.</summary>
        public void SetRoute(float[] x, float[] y, float[] z, int count)
        {
            _wx = x;
            _wy = y;
            _wz = z;
            _count = x != null && y != null && z != null ? count : 0;
            _stale = true;
            if (_count < 2 && _line != null)
            {
                _line.Clear();
            }
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Navigation: NavCityMap.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            if (_failed || _count < 2)
            {
                return;
            }
            try
            {
                Camera camera = GameManager.GetMainCamera();
                if (camera == null)
                {
                    return;
                }
                Transform eye = camera.transform;
                if (!_stale && eye.position == _cameraAt && eye.rotation == _cameraTurn && camera.fieldOfView == _cameraFov
                    && Screen.width == _screenWidth && Screen.height == _screenHeight)
                {
                    return;
                }
                _stale = false;
                _cameraAt = eye.position;
                _cameraTurn = eye.rotation;
                _cameraFov = camera.fieldOfView;
                _screenWidth = Screen.width;
                _screenHeight = Screen.height;
                if (_sx.Length < _count)
                {
                    _sx = new float[_wx.Length];
                    _sy = new float[_wx.Length];
                }
                int shown = 0;
                for (int i = 0; i < _count; i++)
                {
                    Vector3 onScreen = camera.WorldToScreenPoint(new Vector3(_wx[i], _wy[i] + 0.5f, _wz[i]));
                    if (onScreen.z <= 0.1f)
                    {
                        continue;   // behind the camera
                    }
                    onScreen.z = 0f;
                    Vector3 local = _rect.InverseTransformPoint(onScreen);
                    _sx[shown] = local.x;
                    _sy[shown] = local.y;
                    shown++;
                }
                float scale = _rect.lossyScale.x > 0.0001f ? _rect.lossyScale.x : 1f;
                float pixel = Screen.height / 1080f;
                _line.Set(_sx, _sy, shown, 8f * pixel / scale, 1.5f * pixel / scale);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Navigation: NavCityMap.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (!Clicks || _failed)
            {
                return;
            }
            try
            {
                if (Input.GetMouseButtonDown(0))
                {
                    _downAt = Time.unscaledTime;
                    _downPlace = Input.mousePosition;
                    _downOnInterface = OnInterface();
                    return;
                }
                if (!Input.GetMouseButtonUp(0))
                {
                    return;
                }
                float now = Time.unscaledTime;
                Vector2 at = Input.mousePosition;
                bool tap = !_downOnInterface && now - _downAt <= TapSeconds && (at - _downPlace).sqrMagnitude <= TapPixels * TapPixels && !OnInterface();
                if (!tap)
                {
                    _tapAt = -10f;
                    return;
                }
                if (now - _tapAt <= DoubleSeconds && (at - _tapPlace).sqrMagnitude <= DoublePixels * DoublePixels)
                {
                    _tapAt = -10f;
                    Picked?.Invoke(at);
                    return;
                }
                _tapAt = now;
                _tapPlace = at;
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private static bool OnInterface()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void Fail(Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Navigation: the route on the city map and the double click are switched off for this session after an error.");
            if (_line != null)
            {
                _line.Clear();
            }
        }
    }
}
