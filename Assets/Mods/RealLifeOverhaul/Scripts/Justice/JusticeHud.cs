using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The little the justice system shows over the street: a key prompt (at the courthouse sign,
    /// at a lawyer's desk, over a piece of litter), a marker pinned over a place with the distance
    /// to it (the courthouse on a court day, the next piece of litter), and a one-line status
    /// (community service: pieces picked up). Its own retained canvas, built the first time it is
    /// needed; nothing here runs by itself, and every text is rebuilt only when it changes. The
    /// marker is its own (brass), so an incident's orange one and a job's blue one can be up too.
    /// </summary>
    public sealed class JusticeHud : MonoBehaviour
    {
        private static readonly Vector2 MarkerSize = new Vector2(300f, 42f);
        private static readonly Color Plate = new Color(0.05f, 0.07f, 0.10f, 0.86f);
        private static readonly Color Accent = new Color(0.86f, 0.70f, 0.32f, 0.97f);
        private static readonly Color Ink = new Color(0.05f, 0.05f, 0.07f, 0.97f);

        private static JusticeHud _instance;

        private Canvas _canvas;
        private TMP_FontAsset _font;

        private RectTransform _marker;
        private RectTransform _markerPointer;
        private TextMeshProUGUI _markerText;
        private bool _markerOn;
        private int _markerMetres = int.MinValue;
        private string _markerLabel;

        private GameObject _prompt;
        private TextMeshProUGUI _promptText;
        private string _promptShown;
        private bool _promptOn;

        private GameObject _status;
        private TextMeshProUGUI _statusText;
        private string _statusShown;
        private bool _statusOn;

        public static JusticeHud Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Justice HUD").AddComponent<JusticeHud>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -8;   // under the game's own menus, over the mod's other markers

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _font = Hud.Font;

            _marker = Box("Justice Marker", transform, Vector2.zero, Vector2.zero, MarkerSize + new Vector2(6f, 6f), Ink);
            _markerPointer = Box("Pointer", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f), Accent);
            _markerPointer.localRotation = Quaternion.Euler(0f, 0f, 45f);
            RectTransform plate = Box("Plate", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, MarkerSize, Accent);
            _markerText = Text("Text", plate, 22f, Ink);
            Fill(_markerText.rectTransform);
            _marker.gameObject.SetActive(false);

            // The key prompt: low centre, a row above where a job's own prompt sits.
            RectTransform prompt = Box("Prompt", transform, new Vector2(0.5f, 0f), new Vector2(0f, 372f), new Vector2(640f, 48f), Plate);
            _promptText = Text("Text", prompt, 23f, Color.white);
            Fill(_promptText.rectTransform);
            _prompt = prompt.gameObject;
            _prompt.SetActive(false);

            // The status line: top centre, under where a job's objective panel sits.
            RectTransform status = Box("Status", transform, new Vector2(0.5f, 1f), new Vector2(0f, -236f), new Vector2(620f, 44f), Plate);
            Box("Stripe", status, new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(6f, 44f), Accent);
            _statusText = Text("Text", status, 21f, Color.white);
            Fill(_statusText.rectTransform);
            _status = status.gameObject;
            _status.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void ShowPrompt(string text)
        {
            if (!_promptOn)
            {
                _promptOn = true;
                _prompt.SetActive(true);
            }
            if (!ReferenceEquals(text, _promptShown))
            {
                _promptShown = text;
                _promptText.SetText(text ?? string.Empty);
            }
        }

        public void HidePrompt()
        {
            if (_promptOn)
            {
                _promptOn = false;
                _prompt.SetActive(false);
            }
        }

        public void ShowStatus(string text)
        {
            if (!_statusOn)
            {
                _statusOn = true;
                _status.SetActive(true);
            }
            if (!ReferenceEquals(text, _statusShown))
            {
                _statusShown = text;
                _statusText.SetText(text ?? string.Empty);
            }
        }

        public void HideStatus()
        {
            if (_statusOn)
            {
                _statusOn = false;
                _status.SetActive(false);
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

        /// <summary>
        /// Pins the marker over a place, or to the screen edge on the side it lies when out of view, with the distance.
        /// Called per frame while there is somewhere to go; allocates nothing, and the text is rebuilt only when the
        /// distance changes by a metre.
        /// </summary>
        public void ShowMarkerAt(string label, Vector3 place, Vector3 from)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null || ThirdPersonCamera.IsUiOpen())
            {
                HideMarker();
                return;
            }
            float scale = Screen.height / 1080f;
            float half = MarkerSize.x * 0.5f * scale;
            float left = half + 140f * scale;
            float right = Screen.width - half - 24f * scale;
            float top = Screen.height - 290f * scale;
            Vector3 screen = camera.WorldToScreenPoint(place + Vector3.up * 2.6f);
            if (screen.z < 0f)
            {
                screen.x = Screen.width - screen.x;   // behind the camera: mirrored, at the bottom edge ("turn round")
                screen.y = 0f;
            }
            float x = Mathf.Clamp(screen.x, left, Mathf.Max(left, right));
            float bottom = (x > Screen.width - half - 330f * scale ? 120f : 70f) * scale;
            var position = new Vector2(x, Mathf.Clamp(screen.y, bottom, Mathf.Max(bottom, top)));
            if (!_markerOn)
            {
                _markerOn = true;
                _marker.gameObject.SetActive(true);
            }
            _marker.anchoredPosition = position / _canvas.scaleFactor;
            var towards = new Vector2(screen.x - position.x, screen.y - position.y);
            if (towards.sqrMagnitude < 1f)
            {
                towards = Vector2.down;
            }
            float reach = Mathf.Min(
                MarkerSize.x * 0.5f / Mathf.Max(Mathf.Abs(towards.x), 0.0001f),
                MarkerSize.y * 0.5f / Mathf.Max(Mathf.Abs(towards.y), 0.0001f));
            _markerPointer.anchoredPosition = towards * reach;
            int metres = Mathf.RoundToInt(Vector3.Distance(from, place));
            if (metres != _markerMetres || !ReferenceEquals(label, _markerLabel))
            {
                _markerMetres = metres;
                _markerLabel = label;
                _markerText.SetText(label + "  {0} m", metres);
            }
        }

        public void HideAll()
        {
            HidePrompt();
            HideStatus();
            HideMarker();
        }

        private static RectTransform Box(string objectName, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
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

        private TextMeshProUGUI Text(string objectName, Transform parent, float size, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.overflowMode = TextOverflowModes.Ellipsis;
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
    }
}
