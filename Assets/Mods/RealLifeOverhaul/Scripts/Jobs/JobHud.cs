using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A plate with a line of words, a marker pinned over a place with the distance to it, and a key prompt: the mod's
    /// own small HUD, kept for the features that still use it (the home's offers, a run for fitness). The jobs no longer
    /// do: they are shown in the game's own interface (a copy of its job panel, its panel over a place, its activity
    /// panel and its notifications). Its own retained canvas, built the first time something asks. Nothing here runs by
    /// itself, and every text is rebuilt only when what it shows changes.
    /// </summary>
    public sealed class JobHud : MonoBehaviour
    {
        private static readonly Vector2 MarkerSize = new Vector2(300f, 42f);
        private static readonly Color Plate = new Color(0.05f, 0.07f, 0.10f, 0.86f);
        private static readonly Color Accent = new Color(0.20f, 0.62f, 0.95f, 0.97f);
        private static readonly Color Ink = new Color(0.03f, 0.05f, 0.09f, 0.97f);
        private static readonly Color Soft = new Color(0.74f, 0.80f, 0.88f);

        private static JobHud _instance;

        private Canvas _canvas;
        private TMP_FontAsset _font;

        private GameObject _panel;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _line;
        private string _titleShown;
        private string _lineShown;
        private bool _panelOn;

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

        /// <summary>Created the first time something needs it and destroyed with the city scene.</summary>
        public static JobHud Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Job HUD").AddComponent<JobHud>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -9;   // under the game's own menus, over the mod's incident marker

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            _font = Hud.Font;

            // The objective: top centre, under the game's own bar.
            RectTransform panel = Box("Objective", transform, new Vector2(0.5f, 1f), new Vector2(0f, -(UnderTopBar() + 54f)), new Vector2(620f, 84f), Plate);
            Box("Stripe", panel, new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(6f, 84f), Accent);
            _title = Text("Title", panel, 17f, Soft, TextAlignmentOptions.Left);
            Place(_title.rectTransform, new Vector2(22f, -8f), new Vector2(400f, 24f), new Vector2(0f, 1f));
            _line = Text("Line", panel, 23f, Color.white, TextAlignmentOptions.Left);
            Place(_line.rectTransform, new Vector2(22f, -36f), new Vector2(586f, 40f), new Vector2(0f, 1f));
            _panel = panel.gameObject;
            _panel.SetActive(false);

            _marker = Box("Job Marker", transform, Vector2.zero, Vector2.zero, MarkerSize + new Vector2(6f, 6f), Ink);
            _markerPointer = Box("Pointer", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f), Accent);
            _markerPointer.localRotation = Quaternion.Euler(0f, 0f, 45f);
            RectTransform plate = Box("Plate", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, MarkerSize, Accent);
            _markerText = Text("Text", plate, 22f, Ink, TextAlignmentOptions.Center);
            Fill(_markerText.rectTransform);
            _marker.gameObject.SetActive(false);

            // The key prompt: low centre, above the game's vehicle panel.
            RectTransform prompt = Box("Prompt", transform, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(560f, 48f), Plate);
            _promptText = Text("Text", prompt, 23f, Color.white, TextAlignmentOptions.Center);
            Fill(_promptText.rectTransform);
            _prompt = prompt.gameObject;
            _prompt.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ---------------------------------------------------------------- the objective

        /// <summary>The job's name and what to do next. Rebuilt only when either changes.</summary>
        public void ShowObjective(string title, string line)
        {
            if (!_panelOn)
            {
                _panelOn = true;
                _panel.SetActive(true);
            }
            if (!ReferenceEquals(title, _titleShown))
            {
                _titleShown = title;
                _title.SetText(title ?? string.Empty);
            }
            if (!ReferenceEquals(line, _lineShown))
            {
                _lineShown = line;
                _line.SetText(line ?? string.Empty);
            }
        }

        public void HideObjective()
        {
            if (_panelOn)
            {
                _panelOn = false;
                _panel.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- the marker

        public void HideMarker()
        {
            if (_markerOn)
            {
                _markerOn = false;
                _marker.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Pins the marker over a place, or to the screen edge on the side it lies when out of
        /// view, with the distance from where the player is (on foot or in the car). Called per frame while a job has somewhere
        /// to go; allocates nothing, and the text is rebuilt only when the distance changes by a metre.
        /// </summary>
        public void ShowMarkerAt(string label, Vector3 place, Vector3 from)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null || ThirdPersonCamera.IsUiOpen())
            {
                HideMarker();
                return;
            }

            // Kept clear of the game's HUD (portrait, top bar, phone) and of the objective panel.
            float scale = Screen.height / 1080f;
            float half = MarkerSize.x * 0.5f * scale;
            float left = half + 140f * scale;
            float right = Screen.width - half - 24f * scale;
            float top = Screen.height - 240f * scale;
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

        // ---------------------------------------------------------------- the key prompt

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

        // Where the game's own top bar ends, in the units of this canvas (1080 high): the objective sits just under it,
        // whatever the interface scale. Measured once, from the bar itself; the usual figure if it cannot be found.
        private static float UnderTopBar()
        {
            const float usual = 74f;
            try
            {
                var uis = InstanceBehavior<UI.UIs>.Instance;
                if (uis == null || uis.topBar == null || Screen.height <= 0)
                {
                    return usual;
                }
                RectTransform bar = uis.topBar.transform.Find("Container") as RectTransform;
                if (bar == null)
                {
                    bar = uis.topBar.transform as RectTransform;
                }
                if (bar == null)
                {
                    return usual;
                }
                bar.GetWorldCorners(Corners);
                float under = (Screen.height - Corners[0].y) * 1080f / Screen.height;   // an overlay canvas: world corners are pixels
                return under > 20f && under < 220f ? under : usual;
            }
            catch (System.Exception)
            {
                return usual;
            }
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>Everything off: a screen that needs the whole display opened.</summary>
        public void HideAll()
        {
            HideObjective();
            HideMarker();
            HidePrompt();
        }

        // ---------------------------------------------------------------- building blocks

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

        private TextMeshProUGUI Text(string objectName, Transform parent, float size, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.overflowMode = TextOverflowModes.Ellipsis;
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

        // Anchored to a corner or edge of the parent, measured from it.
        private static void Place(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
