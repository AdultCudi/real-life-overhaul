using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the social life shows on screen: who the player is meeting and what to do next (top
    /// centre, under the game's bar), a marker for where, a key prompt, and a card with what an
    /// outing cost and what it did. A retained uGUI canvas of its own, built once on first use
    /// (a job and an outing are never on together, but the jobs' boards and this would otherwise
    /// take turns hiding each other's prompt). Nothing here runs by itself; every text is
    /// rebuilt only when the string handed in changes.
    /// </summary>
    public sealed class SocialHud : MonoBehaviour
    {
        private static readonly Vector2 MarkerSize = new Vector2(300f, 42f);
        private static readonly Color Plate = new Color(0.07f, 0.06f, 0.09f, 0.86f);
        private static readonly Color Accent = new Color(0.96f, 0.52f, 0.42f, 0.97f);
        private static readonly Color Ink = new Color(0.09f, 0.04f, 0.04f, 0.97f);
        private static readonly Color Soft = new Color(0.86f, 0.80f, 0.78f);

        private static SocialHud _instance;

        private Canvas _canvas;
        private TMP_FontAsset _font;

        private GameObject _panel;
        private TextMeshProUGUI _title, _line, _status;
        private string _titleShown, _lineShown, _statusShown;
        private bool _panelOn;

        private RectTransform _marker, _markerPointer;
        private TextMeshProUGUI _markerText;
        private bool _markerOn;
        private int _markerMetres = int.MinValue;
        private string _markerLabel;

        private GameObject _prompt;
        private TextMeshProUGUI _promptText;
        private string _promptShown;
        private bool _promptOn;

        private GameObject _card;
        private TextMeshProUGUI _cardTitle, _cardBody;
        private bool _cardOn;

        public static SocialHud Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Social HUD").AddComponent<SocialHud>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -9;   // under the game's own menus

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            _font = Hud.Font;

            RectTransform panel = Box("Plan", transform, new Vector2(0.5f, 1f), new Vector2(0f, -(UnderTopBar() + 54f)), new Vector2(660f, 84f), Plate);
            Box("Stripe", panel, new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(6f, 84f), Accent);
            _title = Text("Title", panel, 17f, Soft, TextAlignmentOptions.Left);
            Place(_title.rectTransform, new Vector2(22f, -8f), new Vector2(430f, 24f), new Vector2(0f, 1f));
            _status = Text("Status", panel, 20f, Color.white, TextAlignmentOptions.Right);
            Place(_status.rectTransform, new Vector2(-16f, -6f), new Vector2(220f, 26f), new Vector2(1f, 1f));
            _line = Text("Line", panel, 22f, Color.white, TextAlignmentOptions.Left);
            Place(_line.rectTransform, new Vector2(22f, -36f), new Vector2(626f, 40f), new Vector2(0f, 1f));
            _panel = panel.gameObject;
            _panel.SetActive(false);

            _marker = Box("Marker", transform, Vector2.zero, Vector2.zero, MarkerSize + new Vector2(6f, 6f), Ink);
            _markerPointer = Box("Pointer", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f), Accent);
            _markerPointer.localRotation = Quaternion.Euler(0f, 0f, 45f);
            RectTransform plate = Box("Plate", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, MarkerSize, Accent);
            _markerText = Text("Text", plate, 22f, Ink, TextAlignmentOptions.Center);
            Fill(_markerText.rectTransform);
            _marker.gameObject.SetActive(false);

            RectTransform prompt = Box("Prompt", transform, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(600f, 48f), Plate);
            _promptText = Text("Text", prompt, 23f, Color.white, TextAlignmentOptions.Center);
            Fill(_promptText.rectTransform);
            _prompt = prompt.gameObject;
            _prompt.SetActive(false);

            RectTransform card = Box("Card", transform, new Vector2(0f, 0.5f), new Vector2(300f, 40f), new Vector2(430f, 330f), Plate);
            Box("Stripe", card, new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(430f, 6f), Accent);
            _cardTitle = Text("Title", card, 25f, Color.white, TextAlignmentOptions.Left);
            Place(_cardTitle.rectTransform, new Vector2(22f, -18f), new Vector2(390f, 34f), new Vector2(0f, 1f));
            _cardBody = Text("Body", card, 20f, Soft, TextAlignmentOptions.TopLeft);
            Place(_cardBody.rectTransform, new Vector2(22f, -62f), new Vector2(390f, 256f), new Vector2(0f, 1f));
            _cardBody.fontStyle = FontStyles.Normal;
            _cardBody.lineSpacing = 12f;
            _card = card.gameObject;
            _card.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void ShowPlan(string title, string line, string status)
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
            if (!ReferenceEquals(status, _statusShown))
            {
                _statusShown = status;
                _status.SetText(status ?? string.Empty);
            }
        }

        public void HidePlan()
        {
            if (_panelOn)
            {
                _panelOn = false;
                _panel.SetActive(false);
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
        /// Pins the marker over a place, or to the screen edge on the side it lies when out of
        /// view, with the distance. Called per frame while there is somewhere to go; allocates
        /// nothing, and the text is rebuilt only when the distance changes by a metre.
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

        public void ShowCard(string title, string body)
        {
            _cardOn = true;
            _card.SetActive(true);
            _cardTitle.SetText(title ?? string.Empty);
            _cardBody.SetText(body ?? string.Empty);
        }

        public void HideCard()
        {
            if (_cardOn)
            {
                _cardOn = false;
                _card.SetActive(false);
            }
        }

        public bool CardShown => _cardOn;

        public void HideAll(bool cardToo)
        {
            HidePlan();
            HideMarker();
            HidePrompt();
            if (cardToo)
            {
                HideCard();
            }
        }

        // Where the game's own top bar ends, in the units of this canvas: measured once, the usual figure if it cannot be found.
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
                float under = (Screen.height - Corners[0].y) * 1080f / Screen.height;
                return under > 20f && under < 220f ? under : usual;
            }
            catch (System.Exception)
            {
                return usual;
            }
        }

        private static readonly Vector3[] Corners = new Vector3[4];

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
