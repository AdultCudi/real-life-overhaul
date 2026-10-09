using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>What the picture on a card shows.</summary>
    public enum TravelScene { Dorm, Room, Suite, Cottage, Beach, Mountains, City, Ship, Fair, Terminal }

    /// <summary>One screen of the travel panel: a hotel room, a day of a trip, a summary.</summary>
    public sealed class TravelCard
    {
        public string Title;
        public string Subtitle;
        public string Body;
        public string Side;                 // the narrow column under the picture: amenities, figures
        public string Status;               // the line along the bottom: what the last press did
        public TravelScene Scene;
        public readonly List<string> Labels = new List<string>(14);
        public readonly List<int> Ids = new List<int>(14);
        public readonly List<bool> Enabled = new List<bool>(14);

        public void Clear()
        {
            Labels.Clear();
            Ids.Clear();
            Enabled.Clear();
        }

        public void Add(int id, string label, bool enabled = true)
        {
            if (Labels.Count < TravelUi.MostButtons)
            {
                Labels.Add(label);
                Ids.Add(id);
                Enabled.Add(enabled);
            }
        }
    }

    /// <summary>
    /// Travel's own screen elements, built once as a retained uGUI canvas and switched off while
    /// nothing shows: the full-screen panel a hotel stay and an abstract trip are presented in
    /// (a picture made of plain shapes, text, a column of buttons worked with the mouse), the
    /// key prompt at a hotel door, and a marker that points to a hotel. Nothing here runs per
    /// frame except the half-second fade of the panel and, while the marker is up, placing it.
    /// The game has no hotel interior and no destination to show, so the panel covers the whole
    /// screen: the player stays where they stood, hidden, and is never moved.
    /// </summary>
    public sealed class TravelUi : MonoBehaviour
    {
        public const int MostButtons = 14;
        private const float FadeSeconds = 0.35f;
        private static readonly Vector2 MarkerSize = new Vector2(360f, 44f);

        private static TravelUi _instance;

        private Canvas _canvas;
        private TMP_FontAsset _font;
        private Sprite _disc;
        private CanvasGroup _panelGroup;
        private GameObject _panel;
        private RectTransform _picture;
        private TextMeshProUGUI _title, _subtitle, _body, _side, _status;
        private readonly Button[] _buttons = new Button[MostButtons];
        private readonly TextMeshProUGUI[] _buttonTexts = new TextMeshProUGUI[MostButtons];
        private readonly Image[] _buttonFaces = new Image[MostButtons];
        private readonly int[] _buttonIds = new int[MostButtons];
        private readonly List<GameObject> _shapes = new List<GameObject>(40);
        private int _shapesUsed;
        private TravelScene _scene = (TravelScene)(-1);
        private Coroutine _fade;
        private bool _open;
        private bool _suspended;

        private GameObject _prompt;
        private TextMeshProUGUI _promptText;
        private string _promptShown;
        private bool _promptOn;

        private RectTransform _marker;
        private TextMeshProUGUI _markerText;
        private bool _markerOn;
        private int _markerMetres = int.MinValue;
        private string _markerLabel;

        /// <summary>Called with a button's id when it is pressed.</summary>
        public Action<int> Pressed;

        public static TravelUi Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Travel UI").AddComponent<TravelUi>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        /// <summary>True while the panel is up: the camera frees the mouse for it (the one line in ThirdPersonCamera.IsUiOpen).</summary>
        public static bool PanelOpen => _instance != null && _instance._open && !_instance._suspended;

        public bool IsOpen => _open;

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5;   // over the game's HUD; the director takes the panel away whenever one of the game's own menus opens
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            _font = Hud.Font;
            _disc = MakeDisc();
            BuildPanel();
            BuildPrompt();
            BuildMarker();
            _canvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (_disc != null)
            {
                Destroy(_disc.texture);
                Destroy(_disc);
            }
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ---------------------------------------------------------------- the panel

        /// <summary>Shows a card, fading the panel in if it was not up.</summary>
        public void Show(TravelCard card)
        {
            Fill(card);
            if (!_open)
            {
                _open = true;
                _panel.SetActive(true);
                Fade(1f);
            }
            UpdateCanvas();
        }

        public void SetStatus(string status)
        {
            _status.text = status ?? string.Empty;
        }

        /// <summary>Takes the panel off the screen (faded, or at once when something else needs the screen now).</summary>
        public void Hide(bool atOnce = false)
        {
            if (!_open)
            {
                return;
            }
            _open = false;
            if (atOnce || !isActiveAndEnabled)
            {
                StopFade();
                _panelGroup.alpha = 0f;
                _panel.SetActive(false);
                UpdateCanvas();
            }
            else
            {
                Fade(0f);
            }
        }

        /// <summary>One of the game's own menus is open: the panel steps aside until it closes.</summary>
        public void Suspend(bool suspended)
        {
            if (_suspended != suspended)
            {
                _suspended = suspended;
                _panel.SetActive(_open && !suspended);
                UpdateCanvas();
            }
        }

        private void Fill(TravelCard card)
        {
            _title.text = card.Title ?? string.Empty;
            _subtitle.text = card.Subtitle ?? string.Empty;
            _body.text = card.Body ?? string.Empty;
            _side.text = card.Side ?? string.Empty;
            _status.text = card.Status ?? string.Empty;
            for (int i = 0; i < MostButtons; i++)
            {
                bool used = i < card.Labels.Count;
                _buttons[i].gameObject.SetActive(used);
                if (used)
                {
                    _buttonTexts[i].text = card.Labels[i];
                    _buttonIds[i] = card.Ids[i];
                    _buttons[i].interactable = card.Enabled[i];
                    _buttonFaces[i].color = card.Enabled[i] ? new Color(0.16f, 0.30f, 0.46f, 1f) : new Color(0.17f, 0.19f, 0.22f, 1f);
                    _buttonTexts[i].color = card.Enabled[i] ? Color.white : new Color(0.55f, 0.58f, 0.62f);
                }
            }
            if (card.Scene != _scene)
            {
                _scene = card.Scene;
                Paint(card.Scene);
            }
        }

        private void Press(int index)
        {
            if (_open && !_suspended && _panelGroup.alpha > 0.9f && Pressed != null)
            {
                Pressed(_buttonIds[index]);
            }
        }

        private void Fade(float to)
        {
            StopFade();
            _fade = StartCoroutine(FadeTo(to));
        }

        private void StopFade()
        {
            if (_fade != null)
            {
                StopCoroutine(_fade);
                _fade = null;
            }
        }

        private IEnumerator FadeTo(float to)
        {
            float from = _panelGroup.alpha;
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                _panelGroup.alpha = Mathf.Lerp(from, to, t / FadeSeconds);
                yield return null;
            }
            _panelGroup.alpha = to;
            _fade = null;
            if (to <= 0f)
            {
                _panel.SetActive(false);
                UpdateCanvas();
            }
        }

        private void UpdateCanvas()
        {
            bool panel = _panel.activeSelf;
            _prompt.SetActive(_promptOn && !panel);
            _marker.gameObject.SetActive(_markerOn && !panel);
            _canvas.enabled = panel || _promptOn || _markerOn;
        }

        private void BuildPanel()
        {
            RectTransform panel = Box("Panel", transform, Vector2.zero, new Vector2(1f, 1f), new Color(0.045f, 0.055f, 0.075f, 1f));
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().raycastTarget = true;   // nothing behind the panel is clicked through it
            _panel = panel.gameObject;
            _panelGroup = _panel.AddComponent<CanvasGroup>();
            _panelGroup.alpha = 0f;

            // A fixed 1500 x 860 sheet in the middle of the screen, whatever its shape.
            RectTransform sheet = Box("Sheet", panel, Vector2.zero, new Vector2(1500f, 860f), new Color(0.085f, 0.10f, 0.13f, 1f));
            _title = Text("Title", sheet, new Vector2(-365f, 386f), new Vector2(730f, 50f), 34f, Color.white, TextAlignmentOptions.Left);
            _subtitle = Text("Subtitle", sheet, new Vector2(-365f, 346f), new Vector2(730f, 32f), 20f, new Color(0.72f, 0.80f, 0.90f), TextAlignmentOptions.Left);
            _picture = Box("Picture", sheet, new Vector2(-365f, 130f), new Vector2(730f, 380f), new Color(0.12f, 0.14f, 0.18f, 1f));
            _picture.gameObject.AddComponent<RectMask2D>();
            _body = Text("Body", sheet, new Vector2(-365f, -175f), new Vector2(730f, 210f), 21f, new Color(0.92f, 0.93f, 0.95f), TextAlignmentOptions.TopLeft);
            Wrap(_body);
            _side = Text("Side", sheet, new Vector2(-365f, -330f), new Vector2(730f, 90f), 17f, new Color(0.66f, 0.72f, 0.80f), TextAlignmentOptions.TopLeft);
            Wrap(_side);
            _status = Text("Status", sheet, new Vector2(0f, -402f), new Vector2(1460f, 40f), 20f, new Color(1f, 0.82f, 0.42f), TextAlignmentOptions.Center);
            Wrap(_status);

            for (int i = 0; i < MostButtons; i++)
            {
                int column = i / 7;
                int row = i % 7;
                RectTransform rect = Box("Button " + i, sheet, new Vector2(205f + column * 350f, 366f - row * 104f), new Vector2(336f, 92f), new Color(0.16f, 0.30f, 0.46f, 1f));
                Image face = rect.GetComponent<Image>();
                face.raycastTarget = true;
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                ColorBlock colours = button.colors;
                colours.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                colours.disabledColor = Color.white;   // the face itself is recoloured when a button is off
                colours.fadeDuration = 0.05f;
                button.colors = colours;
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;   // the game's keys must not walk a selection through these
                button.navigation = navigation;
                int index = i;
                button.onClick.AddListener(() => Press(index));
                TextMeshProUGUI label = Text("Label", rect, Vector2.zero, new Vector2(316f, 84f), 19f, Color.white, TextAlignmentOptions.Center);
                Wrap(label);
                _buttons[i] = button;
                _buttonFaces[i] = face;
                _buttonTexts[i] = label;
                rect.gameObject.SetActive(false);
            }
            _panel.SetActive(false);
        }

        // ---------------------------------------------------------------- the picture

        // Pictures are a dozen flat shapes each, laid out in the 730 x 380 frame (origin at its centre).
        private void Paint(TravelScene scene)
        {
            _shapesUsed = 0;
            var wall = new Color(0.80f, 0.76f, 0.68f);
            var floor = new Color(0.42f, 0.30f, 0.22f);
            var sky = new Color(0.45f, 0.70f, 0.92f);
            var night = new Color(0.10f, 0.14f, 0.26f);
            var white = new Color(0.95f, 0.95f, 0.93f);
            switch (scene)
            {
                case TravelScene.Dorm:
                    Shape(0f, 40f, 730f, 300f, new Color(0.62f, 0.66f, 0.62f));
                    Shape(0f, -150f, 730f, 80f, new Color(0.36f, 0.34f, 0.32f));
                    Shape(250f, 80f, 120f, 110f, night);
                    for (int bunk = 0; bunk < 2; bunk++)
                    {
                        float x = -230f + bunk * 250f;
                        Shape(x - 100f, -40f, 10f, 220f, new Color(0.30f, 0.32f, 0.36f));
                        Shape(x + 100f, -40f, 10f, 220f, new Color(0.30f, 0.32f, 0.36f));
                        Shape(x, 20f, 200f, 22f, new Color(0.30f, 0.44f, 0.62f));
                        Shape(x, -90f, 200f, 22f, new Color(0.62f, 0.34f, 0.30f));
                        Shape(x - 70f, 38f, 50f, 14f, white);
                        Shape(x - 70f, -72f, 50f, 14f, white);
                    }
                    Shape(250f, -100f, 90f, 60f, new Color(0.25f, 0.27f, 0.30f));   // lockers
                    break;
                case TravelScene.Room:
                case TravelScene.Suite:
                case TravelScene.Cottage:
                    bool suite = scene == TravelScene.Suite;
                    bool cottage = scene == TravelScene.Cottage;
                    Shape(0f, 40f, 730f, 300f, cottage ? new Color(0.86f, 0.88f, 0.84f) : suite ? new Color(0.30f, 0.24f, 0.30f) : wall);
                    Shape(0f, -150f, 730f, 80f, cottage ? new Color(0.72f, 0.60f, 0.42f) : floor);
                    Shape(230f, 70f, suite ? 220f : 150f, 170f, white);                                             // window frame
                    Shape(230f, 70f, suite ? 204f : 134f, 154f, cottage ? sky : night);
                    if (cottage)
                    {
                        Shape(230f, 20f, 134f, 54f, new Color(0.20f, 0.46f, 0.66f));                                // the sea
                        Disc(260f, 110f, 40f, new Color(1f, 0.90f, 0.50f));
                    }
                    else
                    {
                        Shape(200f, 30f, 26f, 70f, new Color(0.22f, 0.26f, 0.40f));                                 // lit towers across the street
                        Shape(245f, 45f, 30f, 100f, new Color(0.26f, 0.30f, 0.46f));
                        Disc(270f, 120f, 22f, new Color(0.95f, 0.95f, 0.80f));
                    }
                    Shape(-150f, 0f, suite ? 330f : 270f, 110f, suite ? new Color(0.36f, 0.20f, 0.16f) : new Color(0.46f, 0.32f, 0.22f));   // headboard
                    Shape(-150f, -70f, suite ? 330f : 270f, 80f, white);                                            // the bed
                    Shape(-150f, -95f, suite ? 330f : 270f, 46f, suite ? new Color(0.62f, 0.50f, 0.22f) : cottage ? new Color(0.36f, 0.56f, 0.70f) : new Color(0.58f, 0.24f, 0.22f));
                    Shape(-215f, -30f, 90f, 30f, white);
                    Shape(-85f, -30f, 90f, 30f, white);
                    Shape(80f, -80f, 60f, 60f, new Color(0.36f, 0.26f, 0.18f));                                     // bedside table
                    Shape(80f, -20f, 8f, 60f, new Color(0.25f, 0.25f, 0.25f));
                    Disc(80f, 20f, 46f, new Color(1f, 0.86f, 0.52f));                                               // the lamp
                    if (suite)
                    {
                        Shape(-320f, -90f, 60f, 90f, new Color(0.50f, 0.40f, 0.20f));                               // an armchair
                        Shape(0f, -170f, 500f, 24f, new Color(0.50f, 0.18f, 0.20f));                                // a rug
                    }
                    break;
                case TravelScene.Beach:
                    Shape(0f, 90f, 730f, 200f, sky);
                    Disc(240f, 120f, 80f, new Color(1f, 0.90f, 0.42f));
                    Shape(0f, -30f, 730f, 70f, new Color(0.16f, 0.50f, 0.72f));
                    Shape(0f, -62f, 730f, 10f, white);
                    Shape(0f, -130f, 730f, 130f, new Color(0.93f, 0.84f, 0.62f));
                    Shape(-170f, -70f, 8f, 150f, new Color(0.40f, 0.30f, 0.22f));
                    Shape(-170f, 10f, 170f, 26f, new Color(0.90f, 0.30f, 0.26f));
                    Shape(-170f, 30f, 110f, 18f, new Color(0.95f, 0.92f, 0.86f));
                    Shape(-110f, -140f, 130f, 16f, new Color(0.22f, 0.56f, 0.52f));
                    Disc(110f, -135f, 34f, new Color(0.95f, 0.50f, 0.20f));
                    break;
                case TravelScene.Mountains:
                    Shape(0f, 60f, 730f, 260f, new Color(0.62f, 0.78f, 0.92f));
                    Shape(-180f, -80f, 330f, 330f, new Color(0.52f, 0.58f, 0.68f), 45f);
                    Shape(120f, -120f, 380f, 380f, new Color(0.44f, 0.50f, 0.62f), 45f);
                    Shape(-180f, 110f, 80f, 80f, white, 45f);
                    Shape(120f, 105f, 96f, 96f, white, 45f);
                    Shape(0f, -150f, 730f, 110f, white);
                    Shape(250f, -100f, 110f, 70f, new Color(0.46f, 0.30f, 0.20f));                                  // a chalet
                    Shape(250f, -60f, 96f, 96f, new Color(0.60f, 0.20f, 0.18f), 45f);
                    Shape(250f, -122f, 30f, 28f, new Color(1f, 0.84f, 0.44f));
                    Shape(-80f, -110f, 10f, 70f, new Color(0.18f, 0.36f, 0.24f));
                    Shape(-80f, -80f, 50f, 50f, new Color(0.18f, 0.40f, 0.26f), 45f);
                    break;
                case TravelScene.City:
                    Shape(0f, 60f, 730f, 260f, new Color(0.95f, 0.76f, 0.56f));
                    Disc(-230f, 100f, 70f, new Color(1f, 0.92f, 0.70f));
                    Shape(-270f, -40f, 110f, 180f, new Color(0.70f, 0.58f, 0.48f));
                    Shape(-150f, -20f, 100f, 220f, new Color(0.78f, 0.66f, 0.52f));
                    Shape(-150f, 100f, 70f, 70f, new Color(0.40f, 0.52f, 0.50f), 45f);                              // a dome
                    Shape(-30f, -50f, 110f, 160f, new Color(0.66f, 0.52f, 0.44f));
                    Shape(120f, 0f, 16f, 260f, new Color(0.34f, 0.32f, 0.34f));                                     // a tower
                    Shape(120f, -90f, 90f, 80f, new Color(0.34f, 0.32f, 0.34f), 45f);
                    Shape(250f, -40f, 150f, 180f, new Color(0.74f, 0.60f, 0.50f));
                    Shape(0f, -150f, 730f, 80f, new Color(0.36f, 0.34f, 0.34f));
                    Shape(240f, -140f, 110f, 10f, new Color(0.80f, 0.24f, 0.22f));                                  // a cafe awning
                    break;
                case TravelScene.Ship:
                    Shape(0f, 70f, 730f, 240f, sky);
                    Disc(-250f, 120f, 60f, new Color(1f, 0.92f, 0.60f));
                    Shape(0f, -120f, 730f, 140f, new Color(0.12f, 0.36f, 0.58f));
                    Shape(20f, -50f, 460f, 70f, white);
                    Shape(-208f, -50f, 60f, 60f, white, 45f);                                                       // the bow
                    Shape(40f, 10f, 330f, 46f, new Color(0.90f, 0.90f, 0.88f));
                    Shape(60f, 50f, 230f, 30f, new Color(0.86f, 0.86f, 0.84f));
                    Shape(20f, 90f, 40f, 50f, new Color(0.80f, 0.22f, 0.20f));
                    Shape(110f, 90f, 40f, 50f, new Color(0.80f, 0.22f, 0.20f));
                    Shape(20f, -42f, 420f, 8f, new Color(0.14f, 0.20f, 0.40f));
                    Shape(0f, -92f, 730f, 8f, new Color(0.85f, 0.92f, 0.96f));
                    break;
                case TravelScene.Fair:
                    Shape(0f, 40f, 730f, 300f, new Color(0.78f, 0.80f, 0.84f));
                    Shape(0f, -150f, 730f, 80f, new Color(0.30f, 0.34f, 0.42f));
                    for (int booth = 0; booth < 3; booth++)
                    {
                        float x = -230f + booth * 230f;
                        Shape(x, -40f, 170f, 150f, white);
                        Shape(x, 50f, 190f, 34f, booth == 0 ? new Color(0.20f, 0.46f, 0.76f) : booth == 1 ? new Color(0.82f, 0.36f, 0.20f) : new Color(0.24f, 0.60f, 0.40f));
                        Shape(x, -95f, 150f, 36f, new Color(0.26f, 0.28f, 0.32f));
                        Disc(x, -30f, 46f, new Color(0.86f, 0.88f, 0.92f));
                    }
                    Shape(0f, 150f, 520f, 30f, new Color(0.16f, 0.20f, 0.30f));                                     // the banner
                    break;
                default:   // Terminal
                    Shape(0f, 60f, 730f, 260f, new Color(0.56f, 0.70f, 0.86f));
                    Shape(0f, -40f, 730f, 110f, new Color(0.84f, 0.86f, 0.88f));
                    Shape(0f, -150f, 730f, 110f, new Color(0.26f, 0.28f, 0.30f));
                    for (int pane = 0; pane < 6; pane++)
                    {
                        Shape(-300f + pane * 120f, -30f, 90f, 60f, new Color(0.40f, 0.56f, 0.72f));
                    }
                    Shape(0f, 40f, 300f, 34f, new Color(0.16f, 0.20f, 0.30f));
                    Shape(-150f, -150f, 150f, 50f, new Color(0.95f, 0.78f, 0.16f));                                 // a cab at the kerb
                    Shape(-150f, -118f, 90f, 30f, new Color(0.95f, 0.78f, 0.16f));
                    Disc(-200f, -178f, 28f, new Color(0.10f, 0.10f, 0.10f));
                    Disc(-100f, -178f, 28f, new Color(0.10f, 0.10f, 0.10f));
                    Shape(180f, 120f, 150f, 14f, white);                                                            // a plane climbing away
                    Shape(170f, 132f, 40f, 30f, white, 30f);
                    break;
            }
            for (int i = _shapesUsed; i < _shapes.Count; i++)
            {
                _shapes[i].SetActive(false);
            }
        }

        private void Shape(float x, float y, float width, float height, Color colour, float turn = 0f, Sprite sprite = null)
        {
            GameObject shape;
            if (_shapesUsed < _shapes.Count)
            {
                shape = _shapes[_shapesUsed];
            }
            else
            {
                shape = Box("Shape", _picture, Vector2.zero, Vector2.one, Color.white).gameObject;
                _shapes.Add(shape);
            }
            _shapesUsed++;
            shape.SetActive(true);
            var rect = (RectTransform)shape.transform;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localRotation = Quaternion.Euler(0f, 0f, turn);
            rect.SetSiblingIndex(_shapesUsed - 1);
            var image = shape.GetComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
        }

        private void Disc(float x, float y, float size, Color colour)
        {
            Shape(x, y, size, size, colour, 0f, _disc);
        }

        private static Sprite MakeDisc()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "RealLifeOverhaul Travel Disc" };
            var pixels = new Color32[size * size];
            float radius = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // ---------------------------------------------------------------- prompt and marker

        public void ShowPrompt(string text)
        {
            if (!_promptOn || text != _promptShown)
            {
                _promptOn = true;
                _promptShown = text;
                _promptText.text = text;
                UpdateCanvas();
            }
        }

        public void HidePrompt()
        {
            if (_promptOn)
            {
                _promptOn = false;
                UpdateCanvas();
            }
        }

        public void HideMarker()
        {
            if (_markerOn)
            {
                _markerOn = false;
                UpdateCanvas();
            }
        }

        /// <summary>
        /// Pins the marker over a place, or to the screen's edge when it is out of view, with the
        /// distance. Called per frame while a marker is wanted; allocates nothing, and the text
        /// is rebuilt only when the distance changes by a metre.
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
            if (!_markerOn)
            {
                _markerOn = true;
                UpdateCanvas();
            }
            float scale = Screen.height / 1080f;
            float half = MarkerSize.x * 0.5f * scale;
            Vector3 screen = camera.WorldToScreenPoint(place + Vector3.up * 3.5f);
            if (screen.z < 0f)
            {
                screen.x = Screen.width - screen.x;
                screen.y = 0f;
            }
            float left = half + 140f * scale;
            float right = Screen.width - half - 24f * scale;
            float x = Mathf.Clamp(screen.x, left, Mathf.Max(left, right));
            float bottom = (x > Screen.width - half - 330f * scale ? 170f : 120f) * scale;   // above the key prompt and clear of the phone
            float top = Screen.height - 205f * scale;                                          // under the incident marker's band
            _marker.anchoredPosition = new Vector2(x, Mathf.Clamp(screen.y, bottom, Mathf.Max(bottom, top))) / _canvas.scaleFactor;
            int metres = Mathf.RoundToInt(Vector3.Distance(player.transform.position, place));
            if (metres != _markerMetres || label != _markerLabel)
            {
                _markerMetres = metres;
                _markerLabel = label;
                _markerText.SetText(label + "  {0} m", metres);
            }
        }

        private void BuildPrompt()
        {
            RectTransform plate = Box("Prompt", transform, new Vector2(0f, 190f), new Vector2(760f, 46f), new Color(0.04f, 0.05f, 0.07f, 0.82f));
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0f);
            _promptText = Text("Text", plate, Vector2.zero, new Vector2(740f, 40f), 22f, Color.white, TextAlignmentOptions.Center);
            _prompt = plate.gameObject;
            _prompt.SetActive(false);
        }

        private void BuildMarker()
        {
            var blue = new Color(0.20f, 0.52f, 0.86f, 0.97f);
            _marker = Box("Hotel Marker", transform, Vector2.zero, MarkerSize + new Vector2(6f, 6f), new Color(0.04f, 0.06f, 0.10f, 0.95f));
            _marker.anchorMin = _marker.anchorMax = Vector2.zero;
            RectTransform plate = Box("Plate", _marker, Vector2.zero, MarkerSize, blue);
            _markerText = Text("Text", plate, Vector2.zero, MarkerSize, 22f, Color.white, TextAlignmentOptions.Center);
            _marker.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- pieces

        private static RectTransform Box(string objectName, Transform parent, Vector2 position, Vector2 size, Color colour)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return rect;
        }

        private TextMeshProUGUI Text(string objectName, Transform parent, Vector2 position, Vector2 size, float fontSize, Color colour, TextAlignmentOptions alignment)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.color = colour;
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

        private static void Wrap(TextMeshProUGUI text)
        {
#pragma warning disable CS0618
            text.enableWordWrapping = true;
#pragma warning restore CS0618
        }
    }
}
