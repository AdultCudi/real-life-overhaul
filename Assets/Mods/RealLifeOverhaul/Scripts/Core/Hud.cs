using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's own on-screen elements (crosshair, incident marker, speedometer), built once as
    /// a retained uGUI canvas. Nothing here runs per frame by itself, and text is only rebuilt
    /// when the number it shows changes.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        private static readonly Vector2 MarkerSize = new Vector2(330f, 46f);

        private static Hud _instance;

        private Canvas _canvas;
        private TMP_FontAsset _font;

        private GameObject _crosshair;
        private bool _crosshairOn;

        private RectTransform _marker;
        private RectTransform _markerPointer;
        private TextMeshProUGUI _markerText;
        private bool _markerOn;
        private int _markerMetres = int.MinValue;
        private string _markerLabel;

        private GameObject _speed;
        private TextMeshProUGUI _speedValue;
        private TextMeshProUGUI _speedUnit;
        private bool _speedOn;
        private int _speedShown = int.MinValue;
        private string _speedUnitShown;

        /// <summary>Created on first use and destroyed with the city scene.</summary>
        public static Hud Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul HUD").AddComponent<Hud>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        /// <summary>The game's own font, borrowed for anything the mod letters (the HUD, a uniform's patch); null if none was found.</summary>
        public static TMP_FontAsset Font => Instance._font;

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -10;   // under the game's own menus

            // Laid out for 1080p and scaled with the screen height, like the game's HUD.
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // Borrow the game's font so everything matches the rest of the HUD.
            var sample = FindObjectOfType<TextMeshProUGUI>();
            _font = sample != null ? sample.font : null;

            _crosshair = CreateBox("Crosshair", transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 4f), new Color(1f, 1f, 1f, 0.75f)).gameObject;
            _crosshair.SetActive(false);

            // A dark-edged orange plate with dark text reads against both road and sky; the diamond
            // behind it sticks out on the side the incident is on.
            var markerOrange = new Color(1f, 0.58f, 0.08f, 0.97f);
            var markerDark = new Color(0.07f, 0.05f, 0.03f, 0.95f);
            _marker = CreateBox("Incident Marker", transform, Vector2.zero, Vector2.zero, MarkerSize + new Vector2(6f, 6f), markerDark);
            _markerPointer = CreateBox("Pointer", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f), markerOrange);
            _markerPointer.localRotation = Quaternion.Euler(0f, 0f, 45f);
            RectTransform plate = CreateBox("Plate", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, MarkerSize, markerOrange);
            _markerText = CreateText("Text", plate, 24f, markerDark);
            Fill(_markerText.rectTransform);
            _marker.gameObject.SetActive(false);

            // Bottom centre, to the right of the game's vehicle panel.
            RectTransform speed = CreateBox("Speedometer", transform, new Vector2(0.5f, 0f), new Vector2(400f, 150f), new Vector2(150f, 84f), new Color(0.04f, 0.05f, 0.07f, 0.72f));
            _speedValue = CreateText("Value", speed, 44f, Color.white);
            Place(_speedValue.rectTransform, new Vector2(0f, 26f), new Vector2(150f, 52f));
            _speedUnit = CreateText("Unit", speed, 16f, new Color(0.72f, 0.78f, 0.86f));
            Place(_speedUnit.rectTransform, new Vector2(0f, 6f), new Vector2(150f, 22f));
            _speed = speed.gameObject;
            _speed.SetActive(false);
        }

        private static RectTransform CreateBox(string objectName, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private TextMeshProUGUI CreateText(string objectName, Transform parent, float size, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            // The SDK's TextMeshPro is older than the game's: this property is the one both versions have.
#pragma warning disable CS0618
            text.enableWordWrapping = false;
#pragma warning restore CS0618
            if (_font != null)
            {
                text.font = _font;
            }
            return text;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Anchored to the bottom centre of the parent.
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public void SetCrosshair(bool on)
        {
            if (on != _crosshairOn)
            {
                _crosshairOn = on;
                _crosshair.SetActive(on);
            }
        }

        public void HideMarker()
        {
            if (_markerOn)
            {
                _markerOn = false;
                _marker.gameObject.SetActive(false);
            }
        }

        /// <param name="screenPosition">Pixels from the bottom-left corner of the screen.</param>
        /// <param name="towards">Which way the incident lies from the marker, in screen space; zero means directly below it.</param>
        public void ShowMarker(string label, Vector2 screenPosition, int metres, Vector2 towards = default)
        {
            if (!_markerOn)
            {
                _markerOn = true;
                _marker.gameObject.SetActive(true);
            }
            _marker.anchoredPosition = screenPosition / _canvas.scaleFactor;

            // Slide the diamond to the plate's edge on the incident's side.
            if (towards.sqrMagnitude < 1f)
            {
                towards = Vector2.down;
            }
            float reach = Mathf.Min(
                MarkerSize.x * 0.5f / Mathf.Max(Mathf.Abs(towards.x), 0.0001f),
                MarkerSize.y * 0.5f / Mathf.Max(Mathf.Abs(towards.y), 0.0001f));
            _markerPointer.anchoredPosition = towards * reach;
            if (metres != _markerMetres || label != _markerLabel)
            {
                _markerMetres = metres;
                _markerLabel = label;
                _markerText.SetText("! " + label + "  {0} m", metres);
            }
        }

        /// <summary>
        /// Pins the marker over a place in the world, or to the screen edge when that place is
        /// out of view, with the distance from the player. Shared by every kind of incident
        /// (only one runs at a time). Called per frame while a scene is active; it allocates
        /// nothing, and the text is rebuilt only when the distance changes by a metre.
        /// </summary>
        public void ShowMarkerAt(string label, Vector3 place)
        {
            Camera camera = GameManager.GetMainCamera();
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            if (camera == null || player == null || ThirdPersonCamera.IsUiOpen())
            {
                HideMarker();
                return;
            }

            // The whole plate stays clear of the game's own HUD: the portrait at the top left, the bar along
            // the top and the phone at the bottom right. Laid out for 1080p and scaled with the screen height.
            float scale = Screen.height / 1080f;
            float half = MarkerSize.x * 0.5f * scale;
            float left = half + 140f * scale;
            float right = Screen.width - half - 24f * scale;
            float top = Screen.height - 155f * scale;
            Vector3 screen = camera.WorldToScreenPoint(place + Vector3.up * 3f);
            if (screen.z < 0f)
            {
                // Behind the camera: mirror and drop to the bottom edge, meaning "turn around".
                screen.x = Screen.width - screen.x;
                screen.y = 0f;
            }
            float x = Mathf.Clamp(screen.x, left, Mathf.Max(left, right));
            float bottom = (x > Screen.width - half - 330f * scale ? 120f : 70f) * scale;
            var position = new Vector2(x, Mathf.Clamp(screen.y, bottom, Mathf.Max(bottom, top)));
            ShowMarker(label, position, Mathf.RoundToInt(Vector3.Distance(player.transform.position, place)),
                new Vector2(screen.x - position.x, screen.y - position.y));
        }

        public void HideSpeed()
        {
            if (_speedOn)
            {
                _speedOn = false;
                _speed.SetActive(false);
            }
        }

        public void ShowSpeed(int value, string unit)
        {
            if (!_speedOn)
            {
                _speedOn = true;
                _speed.SetActive(true);
            }
            if (value != _speedShown)
            {
                _speedShown = value;
                _speedValue.SetText("{0}", value);
            }
            if (unit != _speedUnitShown)
            {
                _speedUnitShown = unit;
                _speedUnit.SetText(unit);
            }
        }
    }
}
