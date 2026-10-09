using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a job puts on the screen: the objective panel (job, what to do next, a timer or a
    /// count), a marker pinned over the next place with the distance to it, the key prompt, and
    /// the pay slip at the end. Its own retained canvas, built the first time a job starts and
    /// switched off when none is on. Nothing here runs by itself; the director calls it, and
    /// every text is rebuilt only when what it shows changes. The marker is the job's own (blue),
    /// so an incident's orange one can be on screen at the same time.
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
        private TextMeshProUGUI _status;
        private string _titleShown;
        private string _lineShown;
        private string _statusShown;
        private int _clockShown = int.MinValue;
        private readonly char[] _clock = new char[8];
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

        private RectTransform _target;
        private TextMeshProUGUI _targetKey;   // the key named on it...
        private int _keysVersion;             // ...as bound when it was last written (Keys.Version)
        private bool _targetOn;

        private GameObject _choices;
        private readonly TextMeshProUGUI[] _choiceText = new TextMeshProUGUI[JobKeys.Choices];
        private readonly Image[] _choicePlate = new Image[JobKeys.Choices];
        private string[] _choicesShown;
        private int _choiceLit = int.MinValue;
        private bool _choicesOn;

        private GameObject _slip;
        private TextMeshProUGUI _slipTitle;
        private TextMeshProUGUI _slipBody;
        private bool _slipOn;

        /// <summary>Created the first time a job needs it and destroyed with the city scene.</summary>
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
            _status = Text("Status", panel, 20f, Color.white, TextAlignmentOptions.Right);
            Place(_status.rectTransform, new Vector2(-16f, -6f), new Vector2(220f, 26f), new Vector2(1f, 1f));
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

            // Whoever the prompt is about: a small diamond with the key on it, over their head.
            _target = Box("Target", transform, Vector2.zero, Vector2.zero, new Vector2(34f, 34f), Ink);
            _target.localRotation = Quaternion.Euler(0f, 0f, 45f);
            RectTransform face = Box("Face", _target, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f), Accent);
            TextMeshProUGUI key = Text("Key", face, 20f, Ink, TextAlignmentOptions.Center);
            Fill(key.rectTransform);
            key.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            key.SetText(JobKeys.Label);
            _targetKey = key;
            _keysVersion = Keys.Version;
            _target.gameObject.SetActive(false);

            // The choices: a row of three just above the prompt, each with its key.
            RectTransform choices = Box("Choices", transform, new Vector2(0.5f, 0f), new Vector2(0f, 312f), new Vector2(800f, 50f), new Color(0f, 0f, 0f, 0f));
            for (int i = 0; i < JobKeys.Choices; i++)
            {
                RectTransform choice = Box("Choice", choices, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 266f, 0f), new Vector2(258f, 46f), Plate);
                _choicePlate[i] = choice.GetComponent<Image>();
                RectTransform keyTag = Box("Key", choice, new Vector2(0f, 0.5f), new Vector2(23f, 0f), new Vector2(34f, 34f), Accent);
                TextMeshProUGUI tagText = Text("Text", keyTag, 21f, Ink, TextAlignmentOptions.Center);
                Fill(tagText.rectTransform);
                tagText.SetText(JobKeys.ChoiceLabel(i));
                _choiceText[i] = Text("Label", choice, 21f, Color.white, TextAlignmentOptions.Left);
                Place(_choiceText[i].rectTransform, new Vector2(50f, 0f), new Vector2(200f, 40f), new Vector2(0f, 0.5f));
            }
            _choices = choices.gameObject;
            _choices.SetActive(false);

            // The pay slip: centre left, clear of the phone and of the middle of the road.
            RectTransform slip = Box("Pay Slip", transform, new Vector2(0f, 0.5f), new Vector2(300f, 40f), new Vector2(430f, 330f), Plate);
            Box("Stripe", slip, new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(430f, 6f), Accent);
            _slipTitle = Text("Title", slip, 26f, Color.white, TextAlignmentOptions.Left);
            Place(_slipTitle.rectTransform, new Vector2(22f, -18f), new Vector2(390f, 34f), new Vector2(0f, 1f));
            _slipBody = Text("Body", slip, 20f, Soft, TextAlignmentOptions.TopLeft);
            Place(_slipBody.rectTransform, new Vector2(22f, -62f), new Vector2(390f, 256f), new Vector2(0f, 1f));
            _slipBody.fontStyle = FontStyles.Normal;
            _slipBody.lineSpacing = 12f;
            _slip = slip.gameObject;
            _slip.SetActive(false);
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

        /// <summary>The figure at the top right of the panel as words (a count, a fare). Rebuilt only on change.</summary>
        public void ShowStatus(string status)
        {
            if (_clockShown != int.MinValue || !ReferenceEquals(status, _statusShown))   // a countdown was showing, or the words changed
            {
                _statusShown = status;
                _clockShown = int.MinValue;
                _status.color = Color.white;
                _status.SetText(status ?? string.Empty);
            }
        }

        /// <summary>The same place as a countdown, m:ss. Rebuilt once a second, without garbage.</summary>
        public void ShowClock(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            if (seconds != _clockShown)
            {
                _clockShown = seconds;
                _statusShown = null;
                // Written into a buffer by hand: no string is made, and no number format of the text component is relied on.
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
        }

        public void HideObjective()
        {
            if (_panelOn)
            {
                _panelOn = false;
                _panel.SetActive(false);
            }
            _status.color = Color.white;
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

        /// <summary>
        /// The small marker over the head of whoever the key prompt is about. Called per frame while
        /// a prompt about a person is up: one projection, no text, nothing allocated.
        /// </summary>
        public void ShowTarget(Vector3 overHead)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                HideTarget();
                return;
            }
            Vector3 screen = camera.WorldToScreenPoint(overHead);
            if (screen.z <= 0f)
            {
                HideTarget();   // behind the camera
                return;
            }
            if (!_targetOn)
            {
                _targetOn = true;
                if (_keysVersion != Keys.Version)
                {
                    // The interaction key was bound to something else since the diamond was made: it names the new one.
                    _keysVersion = Keys.Version;
                    _targetKey.SetText(JobKeys.Label);
                }
                _target.gameObject.SetActive(true);
            }
            _target.anchoredPosition = new Vector2(screen.x, screen.y) / _canvas.scaleFactor;
        }

        public void HideTarget()
        {
            if (_targetOn)
            {
                _targetOn = false;
                _target.gameObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- the choices

        /// <summary>Three things to pick from, each with its key; the one picked last is lit. Rebuilt only when the labels or the lit one change.</summary>
        public void ShowChoices(string[] labels, int lit)
        {
            if (!_choicesOn)
            {
                _choicesOn = true;
                _choices.SetActive(true);
            }
            if (!ReferenceEquals(labels, _choicesShown))
            {
                _choicesShown = labels;
                for (int i = 0; i < JobKeys.Choices; i++)
                {
                    bool has = labels != null && i < labels.Length && labels[i] != null;
                    _choicePlate[i].gameObject.SetActive(has);
                    if (has)
                    {
                        _choiceText[i].SetText(labels[i]);
                    }
                }
            }
            if (lit != _choiceLit)
            {
                _choiceLit = lit;
                for (int i = 0; i < JobKeys.Choices; i++)
                {
                    _choicePlate[i].color = i == lit ? new Color(0.10f, 0.30f, 0.48f, 0.94f) : Plate;
                }
            }
        }

        public void HideChoices()
        {
            if (_choicesOn)
            {
                _choicesOn = false;
                _choices.SetActive(false);
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

        // ---------------------------------------------------------------- the pay slip

        public void ShowSlip(string title, string body)
        {
            _slipOn = true;
            _slip.SetActive(true);
            _slipTitle.SetText(title ?? string.Empty);
            _slipBody.SetText(body ?? string.Empty);
        }

        public void HideSlip()
        {
            if (_slipOn)
            {
                _slipOn = false;
                _slip.SetActive(false);
            }
        }

        public bool SlipShown => _slipOn;

        /// <summary>Everything off: the job ended, or a screen that needs the whole display opened.</summary>
        public void HideAll(bool slipToo)
        {
            HideObjective();
            HideMarker();
            HidePrompt();
            HideTarget();
            HideChoices();
            if (slipToo)
            {
                HideSlip();
            }
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
