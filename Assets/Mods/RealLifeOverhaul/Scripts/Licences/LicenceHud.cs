using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What driving licences put on the screen: an objective panel for a driving test, a
    /// marker over the next checkpoint, the key prompt at a desk, and a sheet (a test paper
    /// with three answers, a result sheet, a licence card). Its own retained canvas, built
    /// the first time anything is shown. Nothing runs by itself, and every text is rebuilt
    /// only when what it says changes.
    /// </summary>
    public sealed class LicenceHud : MonoBehaviour
    {
        public const int Options = 3;

        private static readonly Vector2 MarkerSize = new Vector2(300f, 42f);
        private static readonly Color Plate = new Color(0.05f, 0.07f, 0.10f, 0.90f);
        private static readonly Color Accent = new Color(0.12f, 0.72f, 0.62f, 0.97f);
        private static readonly Color Ink = new Color(0.03f, 0.07f, 0.07f, 0.97f);
        private static readonly Color Soft = new Color(0.74f, 0.82f, 0.86f);
        private static readonly string[] OptionKeys = { "1", "2", "3" };

        private static LicenceHud _instance;

        private Canvas _canvas;
        private TMP_FontAsset _font;

        private GameObject _panel;
        private TextMeshProUGUI _title, _line, _status;
        private string _titleShown, _lineShown, _statusShown;
        private int _clockShown = int.MinValue;
        private readonly char[] _clock = new char[8];
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

        private GameObject _sheet;
        private TextMeshProUGUI _sheetTitle, _sheetBody, _sheetFoot;
        private readonly GameObject[] _optionRow = new GameObject[Options];
        private readonly TextMeshProUGUI[] _optionText = new TextMeshProUGUI[Options];
        private readonly Image[] _optionPlate = new Image[Options];
        private bool _sheetOn;

        /// <summary>Created the first time it is needed and destroyed with the city scene.</summary>
        public static LicenceHud Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Licence HUD").AddComponent<LicenceHud>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -8;   // under the game's own menus, over the mod's other panels

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _font = Hud.Font;

            RectTransform panel = Box("Objective", transform, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(640f, 84f), Plate);
            Box("Stripe", panel, new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(6f, 84f), Accent);
            _title = Text("Title", panel, 17f, Soft, TextAlignmentOptions.Left, false);
            Place(_title.rectTransform, new Vector2(22f, -8f), new Vector2(400f, 24f), new Vector2(0f, 1f));
            _status = Text("Status", panel, 20f, Color.white, TextAlignmentOptions.Right, false);
            Place(_status.rectTransform, new Vector2(-16f, -6f), new Vector2(230f, 26f), new Vector2(1f, 1f));
            _line = Text("Line", panel, 22f, Color.white, TextAlignmentOptions.Left, false);
            Place(_line.rectTransform, new Vector2(22f, -36f), new Vector2(606f, 40f), new Vector2(0f, 1f));
            _panel = panel.gameObject;
            _panel.SetActive(false);

            _marker = Box("Marker", transform, Vector2.zero, Vector2.zero, MarkerSize + new Vector2(6f, 6f), Ink);
            _markerPointer = Box("Pointer", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f), Accent);
            _markerPointer.localRotation = Quaternion.Euler(0f, 0f, 45f);
            RectTransform plate = Box("Plate", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, MarkerSize, Accent);
            _markerText = Text("Text", plate, 22f, Ink, TextAlignmentOptions.Center, false);
            Fill(_markerText.rectTransform);
            _marker.gameObject.SetActive(false);

            RectTransform prompt = Box("Prompt", transform, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(620f, 48f), Plate);
            _promptText = Text("Text", prompt, 23f, Color.white, TextAlignmentOptions.Center, false);
            Fill(_promptText.rectTransform);
            _prompt = prompt.gameObject;
            _prompt.SetActive(false);

            // The sheet: a paper in the middle of the screen. Title, a wrapped body, up to three answers, a foot line.
            RectTransform sheet = Box("Sheet", transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(860f, 560f), Plate);
            Box("Stripe", sheet, new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(860f, 6f), Accent);
            _sheetTitle = Text("Title", sheet, 27f, Color.white, TextAlignmentOptions.Left, false);
            Place(_sheetTitle.rectTransform, new Vector2(28f, -20f), new Vector2(804f, 36f), new Vector2(0f, 1f));
            _sheetBody = Text("Body", sheet, 22f, Soft, TextAlignmentOptions.TopLeft, true);
            Place(_sheetBody.rectTransform, new Vector2(28f, -70f), new Vector2(804f, 250f), new Vector2(0f, 1f));
            _sheetBody.fontStyle = FontStyles.Normal;
            _sheetBody.lineSpacing = 8f;
            for (int i = 0; i < Options; i++)
            {
                RectTransform row = Box("Option", sheet, new Vector2(0.5f, 0f), new Vector2(0f, 204f - i * 62f), new Vector2(804f, 54f), new Color(0.10f, 0.14f, 0.18f, 0.95f));
                _optionPlate[i] = row.GetComponent<Image>();
                RectTransform tag = Box("Key", row, new Vector2(0f, 0.5f), new Vector2(27f, 0f), new Vector2(36f, 36f), Accent);
                TextMeshProUGUI key = Text("Text", tag, 22f, Ink, TextAlignmentOptions.Center, false);
                Fill(key.rectTransform);
                key.SetText(OptionKeys[i]);
                _optionText[i] = Text("Label", row, 20f, Color.white, TextAlignmentOptions.Left, true);
                Place(_optionText[i].rectTransform, new Vector2(58f, 0f), new Vector2(736f, 50f), new Vector2(0f, 0.5f));
                _optionRow[i] = row.gameObject;
            }
            _sheetFoot = Text("Foot", sheet, 18f, Soft, TextAlignmentOptions.Left, false);
            Place(_sheetFoot.rectTransform, new Vector2(28f, 14f), new Vector2(804f, 26f), new Vector2(0f, 0f));
            _sheet = sheet.gameObject;
            _sheet.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ---------------------------------------------------------------- the objective

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

        public void ShowStatus(string status)
        {
            if (_clockShown != int.MinValue || !ReferenceEquals(status, _statusShown))
            {
                _statusShown = status;
                _clockShown = int.MinValue;
                _status.color = Color.white;
                _status.SetText(status ?? string.Empty);
            }
        }

        /// <summary>A countdown, m:ss, in the status corner. Rebuilt once a second, without garbage.</summary>
        public void ShowClock(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            if (seconds == _clockShown)
            {
                return;
            }
            _clockShown = seconds;
            _statusShown = null;
            int minutes = Mathf.Min(seconds / 60, 99);
            int rest = seconds % 60;
            int length = 0;
            if (minutes >= 10)
            {
                _clock[length++] = (char)('0' + minutes / 10);
            }
            _clock[length++] = (char)('0' + minutes % 10);
            _clock[length++] = ':';
            _clock[length++] = (char)('0' + rest / 10);
            _clock[length++] = (char)('0' + rest % 10);
            _status.SetCharArray(_clock, 0, length);
            _status.color = seconds < 30 ? new Color(1f, 0.45f, 0.35f) : Color.white;
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

        /// <summary>Pins the marker over a place (or to the screen edge on its side), with the distance. Per frame while shown; allocates nothing.</summary>
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
            Vector3 screen = camera.WorldToScreenPoint(place + Vector3.up * 2.4f);
            if (screen.z < 0f)
            {
                screen.x = Screen.width - screen.x;
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

        // ---------------------------------------------------------------- the prompt

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

        // ---------------------------------------------------------------- the sheet

        /// <summary>A paper: title, body, up to three answers (null for none) and a foot line. Call only when something on it changes.</summary>
        public void ShowSheet(string title, string body, string[] options, string foot, int lit = -1, bool litGood = true)
        {
            _sheetOn = true;
            _sheet.SetActive(true);
            _sheetTitle.SetText(title ?? string.Empty);
            _sheetBody.SetText(body ?? string.Empty);
            _sheetFoot.SetText(foot ?? string.Empty);
            for (int i = 0; i < Options; i++)
            {
                bool has = options != null && i < options.Length && options[i] != null;
                _optionRow[i].SetActive(has);
                if (has)
                {
                    _optionText[i].SetText(options[i]);
                    _optionPlate[i].color = i != lit ? new Color(0.10f, 0.14f, 0.18f, 0.95f)
                        : litGood ? new Color(0.08f, 0.40f, 0.28f, 0.96f) : new Color(0.50f, 0.14f, 0.12f, 0.96f);
                }
            }
        }

        public void HideSheet()
        {
            if (_sheetOn)
            {
                _sheetOn = false;
                _sheet.SetActive(false);
            }
        }

        public bool SheetShown => _sheetOn;

        /// <summary>Everything of a test off: the objective, the marker and the sheet. The key prompt is its owner's to hide.</summary>
        public void HideActivity()
        {
            HideObjective();
            HideMarker();
            HideSheet();
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

        private TextMeshProUGUI Text(string objectName, Transform parent, float size, Color color, TextAlignmentOptions alignment, bool wrap)
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
            text.enableWordWrapping = wrap;
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
