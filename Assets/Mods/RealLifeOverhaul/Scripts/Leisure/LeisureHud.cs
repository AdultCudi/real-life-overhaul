using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// What leisure puts on the screen: the objective panel (what is being played, what to do
    /// next, a score), the key prompt, a row of three choices, the timing meter (a cursor over
    /// a bar with a target zone), a list (the clothes rack, the wardrobe), a result card, the
    /// place marker and the one-line strip (radio station, photo mode help). Its own retained
    /// canvas, built the first time it is needed. Nothing here runs by itself and every text is
    /// rebuilt only when what it shows changes; the meter's cursor is one position per frame
    /// while a meter is up. <see cref="Suppress"/> switches the whole canvas off (photo mode).
    /// </summary>
    public sealed class LeisureHud : MonoBehaviour
    {
        public const int ListRows = 9;

        private const float MeterWidth = 460f;
        private static readonly Vector2 MarkerSize = new Vector2(300f, 42f);
        private static readonly Color Plate = new Color(0.05f, 0.07f, 0.10f, 0.86f);
        private static readonly Color Accent = new Color(0.36f, 0.78f, 0.52f, 0.97f);   // green: leisure, not work (blue) or an incident (orange)
        private static readonly Color Ink = new Color(0.03f, 0.07f, 0.05f, 0.97f);
        private static readonly Color Soft = new Color(0.76f, 0.84f, 0.80f);
        private static readonly Color Lit = new Color(0.10f, 0.36f, 0.22f, 0.94f);

        private static LeisureHud _instance;

        private Canvas _canvas;
        private TMP_FontAsset _font;

        private GameObject _panel;
        private TextMeshProUGUI _title, _line, _status;
        private string _titleShown, _lineShown, _statusShown;
        private bool _panelOn;

        private GameObject _prompt;
        private TextMeshProUGUI _promptText;
        private string _promptShown;
        private bool _promptOn;

        private GameObject _choices;
        private readonly TextMeshProUGUI[] _choiceText = new TextMeshProUGUI[JobKeys.Choices];
        private readonly GameObject[] _choicePlate = new GameObject[JobKeys.Choices];
        private string[] _choicesShown;
        private bool _choicesOn;

        private GameObject _meter;
        private RectTransform _meterZone, _meterSweet, _meterCursor;
        private TextMeshProUGUI _meterLabel;
        private string _meterLabelShown;
        private float _zoneAt = -1f, _zoneWidth = -1f, _sweetWidth = -1f;
        private bool _meterOn;

        private GameObject _list;
        private TextMeshProUGUI _listTitle, _listFoot;
        private readonly TextMeshProUGUI[] _rowText = new TextMeshProUGUI[ListRows];
        private readonly TextMeshProUGUI[] _rowRight = new TextMeshProUGUI[ListRows];
        private readonly Image[] _rowPlate = new Image[ListRows];
        private int _rowLit = -1;
        private bool _listOn;

        private GameObject _card;
        private TextMeshProUGUI _cardTitle, _cardBody;
        private bool _cardOn;

        private RectTransform _marker, _markerPointer;
        private TextMeshProUGUI _markerText;
        private bool _markerOn;
        private int _markerMetres = int.MinValue;
        private string _markerLabel;

        private GameObject _strip;
        private TextMeshProUGUI _stripText;
        private string _stripShown;
        private bool _stripOn;

        private GameObject _shade;
        private Image _shadeImage;
        private float _shadeShown = -1f;

        public static LeisureHud Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Leisure HUD").AddComponent<LeisureHud>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -8;   // under the game's own menus, over the mod's other HUDs

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            _font = Hud.Font;

            // Lights down (the cinema): a black sheet under everything else of ours.
            RectTransform shade = Box("Shade", transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8000f, 4000f), new Color(0f, 0f, 0f, 0f));
            _shadeImage = shade.GetComponent<Image>();
            _shade = shade.gameObject;
            _shade.SetActive(false);

            RectTransform panel = Box("Objective", transform, new Vector2(0.5f, 1f), new Vector2(0f, -142f), new Vector2(760f, 112f), Plate);
            Box("Stripe", panel, new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(6f, 112f), Accent);
            _title = Text("Title", panel, 17f, Soft, TextAlignmentOptions.Left);
            Place(_title.rectTransform, new Vector2(22f, -8f), new Vector2(420f, 24f), new Vector2(0f, 1f));
            _status = Text("Status", panel, 20f, Color.white, TextAlignmentOptions.Right);
            Place(_status.rectTransform, new Vector2(-16f, -6f), new Vector2(250f, 26f), new Vector2(1f, 1f));
            // What to do next, or what just happened: two lines of it, wrapped.
            _line = Text("Line", panel, 21f, Color.white, TextAlignmentOptions.TopLeft);
            Place(_line.rectTransform, new Vector2(22f, -38f), new Vector2(722f, 66f), new Vector2(0f, 1f));
#pragma warning disable CS0618
            _line.enableWordWrapping = true;
#pragma warning restore CS0618
            _panel = panel.gameObject;
            _panel.SetActive(false);

            RectTransform prompt = Box("Prompt", transform, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(640f, 48f), Plate);
            _promptText = Text("Text", prompt, 22f, Color.white, TextAlignmentOptions.Center);
            Fill(_promptText.rectTransform);
            _prompt = prompt.gameObject;
            _prompt.SetActive(false);

            RectTransform choices = Box("Choices", transform, new Vector2(0.5f, 0f), new Vector2(0f, 312f), new Vector2(1100f, 50f), new Color(0f, 0f, 0f, 0f));
            for (int i = 0; i < JobKeys.Choices; i++)
            {
                RectTransform choice = Box("Choice", choices, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 366f, 0f), new Vector2(358f, 46f), Plate);
                _choicePlate[i] = choice.gameObject;
                RectTransform keyTag = Box("Key", choice, new Vector2(0f, 0.5f), new Vector2(23f, 0f), new Vector2(34f, 34f), Accent);
                TextMeshProUGUI tagText = Text("Text", keyTag, 21f, Ink, TextAlignmentOptions.Center);
                Fill(tagText.rectTransform);
                tagText.SetText(JobKeys.ChoiceLabel(i));
                _choiceText[i] = Text("Label", choice, 20f, Color.white, TextAlignmentOptions.Left);
                Place(_choiceText[i].rectTransform, new Vector2(50f, 0f), new Vector2(300f, 40f), new Vector2(0f, 0.5f));
            }
            _choices = choices.gameObject;
            _choices.SetActive(false);

            // The meter: a bar, the zone to stop in, the better zone inside it, and the cursor that moves.
            RectTransform meter = Box("Meter", transform, new Vector2(0.5f, 0f), new Vector2(0f, 372f), new Vector2(MeterWidth + 12f, 34f), Plate);
            _meterZone = Box("Zone", meter, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(60f, 22f), new Color(0.36f, 0.78f, 0.52f, 0.55f));
            _meterSweet = Box("Sweet", meter, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(16f, 22f), new Color(0.98f, 0.86f, 0.30f, 0.9f));
            _meterCursor = Box("Cursor", meter, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, 30f), Color.white);
            _meterLabel = Text("Label", meter, 18f, Soft, TextAlignmentOptions.Center);
            Place(_meterLabel.rectTransform, new Vector2(0f, 26f), new Vector2(MeterWidth, 24f), new Vector2(0.5f, 1f));
            _meter = meter.gameObject;
            _meter.SetActive(false);

            // The list: right of centre, clear of the character it is about.
            RectTransform list = Box("List", transform, new Vector2(1f, 0.5f), new Vector2(-420f, 30f), new Vector2(520f, 470f), Plate);
            Box("Stripe", list, new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(520f, 6f), Accent);
            _listTitle = Text("Title", list, 24f, Color.white, TextAlignmentOptions.Left);
            Place(_listTitle.rectTransform, new Vector2(20f, -16f), new Vector2(480f, 32f), new Vector2(0f, 1f));
            for (int i = 0; i < ListRows; i++)
            {
                RectTransform row = Box("Row", list, new Vector2(0.5f, 1f), new Vector2(0f, -76f - i * 38f), new Vector2(496f, 34f), new Color(0f, 0f, 0f, 0f));
                _rowPlate[i] = row.GetComponent<Image>();
                _rowText[i] = Text("Name", row, 19f, Color.white, TextAlignmentOptions.Left);
                Place(_rowText[i].rectTransform, new Vector2(12f, 0f), new Vector2(340f, 30f), new Vector2(0f, 0.5f));
                _rowText[i].fontStyle = FontStyles.Normal;
                _rowRight[i] = Text("Right", row, 19f, Soft, TextAlignmentOptions.Right);
                Place(_rowRight[i].rectTransform, new Vector2(-12f, 0f), new Vector2(150f, 30f), new Vector2(1f, 0.5f));
                _rowRight[i].fontStyle = FontStyles.Normal;
            }
            _listFoot = Text("Foot", list, 17f, Soft, TextAlignmentOptions.Left);
            Place(_listFoot.rectTransform, new Vector2(20f, 12f), new Vector2(480f, 26f), new Vector2(0f, 0f));
            _list = list.gameObject;
            _list.SetActive(false);

            RectTransform card = Box("Card", transform, new Vector2(0f, 0.5f), new Vector2(300f, 40f), new Vector2(430f, 300f), Plate);
            Box("Stripe", card, new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(430f, 6f), Accent);
            _cardTitle = Text("Title", card, 26f, Color.white, TextAlignmentOptions.Left);
            Place(_cardTitle.rectTransform, new Vector2(22f, -18f), new Vector2(390f, 34f), new Vector2(0f, 1f));
            _cardBody = Text("Body", card, 20f, Soft, TextAlignmentOptions.TopLeft);
            Place(_cardBody.rectTransform, new Vector2(22f, -62f), new Vector2(390f, 226f), new Vector2(0f, 1f));
            _cardBody.fontStyle = FontStyles.Normal;
            _cardBody.lineSpacing = 12f;
#pragma warning disable CS0618
            _cardBody.enableWordWrapping = true;
#pragma warning restore CS0618
            _card = card.gameObject;
            _card.SetActive(false);

            _marker = Box("Leisure Marker", transform, Vector2.zero, Vector2.zero, MarkerSize + new Vector2(6f, 6f), Ink);
            _markerPointer = Box("Pointer", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f), Accent);
            _markerPointer.localRotation = Quaternion.Euler(0f, 0f, 45f);
            RectTransform plate = Box("Plate", _marker, new Vector2(0.5f, 0.5f), Vector2.zero, MarkerSize, Accent);
            _markerText = Text("Text", plate, 22f, Ink, TextAlignmentOptions.Center);
            Fill(_markerText.rectTransform);
            _marker.gameObject.SetActive(false);

            RectTransform strip = Box("Strip", transform, new Vector2(0.5f, 0f), new Vector2(0f, 196f), new Vector2(900f, 38f), Plate);
            _stripText = Text("Text", strip, 19f, Color.white, TextAlignmentOptions.Center);
            Fill(_stripText.rectTransform);
            _strip = strip.gameObject;
            _strip.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>The whole canvas off or on again (photo mode hides every HUD). Nothing shown is forgotten.</summary>
        public void Suppress(bool hidden)
        {
            if (_canvas.enabled == hidden)
            {
                _canvas.enabled = !hidden;
            }
        }

        // ---------------------------------------------------------------- the objective

        public void ShowObjective(string title, string line, string status)
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

        public void HideObjective()
        {
            if (_panelOn)
            {
                _panelOn = false;
                _panel.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- prompt, choices

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

        public void ShowChoices(string[] labels)
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
                    _choicePlate[i].SetActive(has);
                    if (has)
                    {
                        _choiceText[i].SetText(labels[i]);
                    }
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

        // ---------------------------------------------------------------- the meter

        /// <summary>
        /// The meter with its cursor at <paramref name="cursor"/> (0..1 along the bar). The zone (centre and width,
        /// 0..1) and the better zone inside it are laid out only when they change; the cursor is one position.
        /// Called per frame while a meter is being played, and allocates nothing.
        /// </summary>
        public void ShowMeter(float cursor, float zoneCentre, float zoneWidth, float sweetWidth, string label)
        {
            if (!_meterOn)
            {
                _meterOn = true;
                _meter.SetActive(true);
            }
            if (zoneCentre != _zoneAt || zoneWidth != _zoneWidth || sweetWidth != _sweetWidth)
            {
                _zoneAt = zoneCentre;
                _zoneWidth = zoneWidth;
                _sweetWidth = sweetWidth;
                _meterZone.anchoredPosition = new Vector2(6f + zoneCentre * MeterWidth, 0f);
                _meterZone.sizeDelta = new Vector2(zoneWidth > 0f ? Mathf.Max(2f, zoneWidth * MeterWidth) : 0f, 22f);
                _meterSweet.anchoredPosition = new Vector2(6f + zoneCentre * MeterWidth, 0f);
                _meterSweet.sizeDelta = new Vector2(Mathf.Max(0f, sweetWidth * MeterWidth), 22f);
            }
            if (!ReferenceEquals(label, _meterLabelShown))
            {
                _meterLabelShown = label;
                _meterLabel.SetText(label ?? string.Empty);
            }
            _meterCursor.anchoredPosition = new Vector2(6f + Mathf.Clamp01(cursor) * MeterWidth, 0f);
        }

        public void HideMeter()
        {
            if (_meterOn)
            {
                _meterOn = false;
                _meter.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- the list

        /// <summary>
        /// A list of up to <see cref="ListRows"/> rows with one lit. Builds texts: called when the list or the lit
        /// row changes, never per frame. <paramref name="names"/> and <paramref name="rights"/> are read from
        /// <paramref name="first"/> on.
        /// </summary>
        public void ShowList(string title, string[] names, string[] rights, int first, int count, int lit, string foot)
        {
            if (!_listOn)
            {
                _listOn = true;
                _list.SetActive(true);
            }
            _listTitle.SetText(title ?? string.Empty);
            _listFoot.SetText(foot ?? string.Empty);
            for (int i = 0; i < ListRows; i++)
            {
                int index = first + i;
                bool has = index < count;
                _rowText[i].SetText(has ? names[index] ?? string.Empty : string.Empty);
                _rowRight[i].SetText(has && rights != null ? rights[index] ?? string.Empty : string.Empty);
                _rowPlate[i].color = has && index == lit ? Lit : new Color(0f, 0f, 0f, 0f);
            }
            _rowLit = lit;
        }

        public bool ListShown => _listOn;
        public int ListLit => _rowLit;

        public void HideList()
        {
            if (_listOn)
            {
                _listOn = false;
                _list.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- the card

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

        // ---------------------------------------------------------------- the strip and the shade

        public void ShowStrip(string text)
        {
            if (!_stripOn)
            {
                _stripOn = true;
                _strip.SetActive(true);
            }
            if (!ReferenceEquals(text, _stripShown))
            {
                _stripShown = text;
                _stripText.SetText(text ?? string.Empty);
            }
        }

        public void HideStrip()
        {
            if (_stripOn)
            {
                _stripOn = false;
                _strip.SetActive(false);
            }
        }

        /// <summary>How dark the screen is under the mod's HUD, 0 (off) to 1. Pushed only when it changes by a step.</summary>
        public void Shade(float darkness)
        {
            darkness = Mathf.Round(Mathf.Clamp01(darkness) * 50f) / 50f;
            if (darkness == _shadeShown)
            {
                return;
            }
            _shadeShown = darkness;
            _shade.SetActive(darkness > 0f);
            _shadeImage.color = new Color(0f, 0f, 0f, darkness);
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

        /// <summary>Pins the marker over a place, or to the screen edge on its side when out of view. Per frame while there is somewhere to go; allocates nothing.</summary>
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
            _markerPointer.anchoredPosition = towards.normalized * Mathf.Min(reach, 170f);
            int metres = Mathf.RoundToInt(Vector3.Distance(from, place));
            if (metres != _markerMetres || !ReferenceEquals(label, _markerLabel))
            {
                _markerMetres = metres;
                _markerLabel = label;
                _markerText.SetText(label + "  {0} m", metres);
            }
        }

        /// <summary>Everything an activity shows, off. The strip (radio) is not an activity's and stays.</summary>
        public void HideActivity(bool cardToo)
        {
            HideObjective();
            HidePrompt();
            HideChoices();
            HideMeter();
            HideList();
            HideMarker();
            Shade(0f);
            if (cardToo)
            {
                HideCard();
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
