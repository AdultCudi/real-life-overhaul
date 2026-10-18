using System;
using System.Collections;
using System.Collections.Generic;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.Meta
{
    /// <summary>A panel on the mod's own canvas (the options page's messages): built once, then shown and hidden.</summary>
    public abstract class MetaPanel
    {
        public GameObject Root;
        public ScrollRect KeyScroll;       // what the arrow and page keys scroll
    }

    /// <summary>
    /// The canvas of the mod's own panels (uGUI built in code, in the HUD's colours), above the game's menus for the session.
    /// It costs nothing a frame while no panel is open: the canvas is off and this component disabled. While one is open its
    /// Update keeps the panel selected (so the game leaves its shortcuts alone), the mod's keys suspended and the player held.
    /// </summary>
    public sealed class MetaUi : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "MetaUi", typeof(MetaUi));   // its frame methods

        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color Window = new Color(0.05f, 0.06f, 0.085f, 0.985f);
        public static readonly Color Bar = new Color(0.09f, 0.11f, 0.15f, 1f);
        public static readonly Color Plate = new Color(0.17f, 0.21f, 0.28f, 1f);
        public static readonly Color Accent = new Color(1f, 0.58f, 0.08f, 1f);
        public static readonly Color Ink = new Color(0.93f, 0.95f, 0.97f, 1f);
        public static readonly Color Soft = new Color(0.72f, 0.78f, 0.86f, 1f);

        private const NavigationBlocker Blocker = (NavigationBlocker)7302;   // the mod's own, like PlayerKnockdown's 7301
        private const float LinePixels = 14f;                                // pixels the arrow keys scroll a line

        private static MetaUi _instance;

        private readonly List<MetaPanel> _open = new List<MetaPanel>();
        private readonly Dictionary<GameObject, Vector2> _sizes = new Dictionary<GameObject, Vector2>();
        private Canvas _canvas;
        private TMP_FontAsset _font;
        private int _layer;
        private PlayerController _held;
        private MessagePanel _message;
        private bool _faulted;

        /// <summary>Made on first use; kept for the session.</summary>
        public static MetaUi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("RealLifeOverhaul Meta UI");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<MetaUi>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        /// <summary>True while one of the mod's panels is on screen.</summary>
        public static bool AnyOpen => _instance != null && _instance._open.Count > 0;

        private void Awake()
        {
            _layer = LayerMask.NameToLayer("UI");
            if (_layer < 0)
            {
                _layer = 5;
            }
            gameObject.layer = _layer;
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30000;   // above the game's menus: the guide is opened from the options page
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            _canvas.enabled = false;
            enabled = false;
        }

        private void OnDestroy()
        {
            Hold(false);
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void Show(MetaPanel panel)
        {
            if (panel == null || panel.Root == null)
            {
                return;
            }
            FindFont();
            Fit(panel.Root);
            _open.Remove(panel);
            _open.Add(panel);
            panel.Root.SetActive(true);
            panel.Root.transform.SetAsLastSibling();
            _canvas.enabled = true;
            enabled = true;
            Hold(true);
        }

        public void Close(MetaPanel panel)
        {
            if (panel == null || !_open.Remove(panel))
            {
                return;
            }
            if (panel.Root != null)
            {
                panel.Root.SetActive(false);
            }
            try
            {
                EventSystem events = EventSystem.current;
                if (events != null && panel.Root != null && events.currentSelectedGameObject == panel.Root)
                {
                    events.SetSelectedGameObject(null);
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
            if (_open.Count == 0)
            {
                _canvas.enabled = false;
                enabled = false;
                Hold(false);
            }
        }

        public void CloseAll()
        {
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                Close(_open[i]);
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Meta: MetaUi.Update");
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
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            try
            {
                if (_open.Count == 0)
                {
                    enabled = false;
                    return;
                }
                MetaPanel top = _open[_open.Count - 1];
                Fit(top.Root);
                if (Keys.PanelClose())
                {
                    // On the next frame: until then the panel is still selected, so in the city the game takes this Esc as used
                    // (CancelButtonHandler) and does not act on it. In the main menu the options page closes on it too.
                    NextFrame(() => Close(top));
                    return;
                }
                int lines = Keys.PanelScroll();
                if (lines != 0 && top.KeyScroll != null)
                {
                    ScrollBy(top.KeyScroll, lines);
                }
                // The panel itself stays selected: the game then leaves its shortcuts alone (GameManager.HasInputSelected), takes Esc
                // as used, and the mod's camera gives the cursor back.
                EventSystem events = EventSystem.current;
                if (events != null && events.currentSelectedGameObject != top.Root)
                {
                    events.SetSelectedGameObject(top.Root);
                }
                if (_held == null)
                {
                    Hold(true);   // opened in the menu, and the city has come since
                }
            }
            catch (Exception e)
            {
                if (!_faulted)
                {
                    _faulted = true;
                    MetaLog.Error(e);
                    MetaLog.Warn("a panel failed and was closed.");
                }
                CloseAll();
            }
        }

        private static void ScrollBy(ScrollRect scroll, int lines)
        {
            if (scroll.content == null || scroll.viewport == null)
            {
                return;
            }
            float hidden = scroll.content.rect.height - scroll.viewport.rect.height;
            if (hidden <= 1f)
            {
                return;
            }
            scroll.velocity = Vector2.zero;
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - lines * LinePixels / hidden);
        }

        // The mod's keys are off and the player on foot stands still while a panel is open; both come back on every way out.
        private void Hold(bool on)
        {
            Keys.Suspended = on;
            try
            {
                if (on)
                {
                    var gm = InstanceBehavior<GameManager>.Instance;
                    PlayerController player = gm != null ? gm.playerController : null;
                    if (player != null && _held == null)
                    {
                        player.SetNavigationBlocker(Blocker);
                        _held = player;
                    }
                }
                else if (_held != null)
                {
                    PlayerController player = _held;
                    _held = null;
                    player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                _held = null;
                MetaLog.Error(e);
            }
        }

        /// <summary>Runs something on the next frame: for what must not happen inside a button's own click.</summary>
        public void NextFrame(Action action)
        {
            StartCoroutine(Guard.Routine(After(action)));
        }

        private static IEnumerator After(Action action)
        {
            yield return null;
            try
            {
                action();
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        public Coroutine Run(IEnumerator routine)
        {
            return StartCoroutine(Guard.Routine(routine));
        }

        /// <summary>A message with an OK button, or with the buttons given (label key, what it does; the panel closes first).</summary>
        public static void Message(string title, string body, params KeyValuePair<string, Action>[] buttons)
        {
            try
            {
                MetaUi ui = Instance;
                if (ui._message == null)
                {
                    ui._message = new MessagePanel(ui);
                }
                ui._message.Set(title, body, buttons);
                ui.Show(ui._message);
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                MetaLog.Warn("could not show: " + title + ": " + body);
            }
        }

        /// <summary>A question with a button that does it and one that does not.</summary>
        public static void Confirm(string title, string body, string yesKey, Action yes)
        {
            Message(title, body, new KeyValuePair<string, Action>(yesKey, yes), new KeyValuePair<string, Action>("reallife_meta_cancel", null));
        }

        // The game's font, found behind the loading screen (Core/GameUi/GameFont), asked for when a panel is shown until there is one.
        private void FindFont()
        {
            if (_font != null)
            {
                return;
            }
            _font = GameUi.GameFont.Asset;
            if (_font != null)
            {
                foreach (TextMeshProUGUI text in GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    text.font = _font;
                }
            }
        }

        public GameObject New(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = _layer;
            go.transform.SetParent(parent != null ? parent : transform, false);
            return go;
        }

        public RectTransform Box(string objectName, Transform parent, Color color, bool raycast)
        {
            GameObject go = New(objectName, parent);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return (RectTransform)go.transform;
        }

        public TextMeshProUGUI Text(string objectName, Transform parent, float size, Color color, TextAlignmentOptions alignment, bool wrap)
        {
            GameObject go = New(objectName, parent);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.fontSize = size;
            text.color = color;
            text.richText = true;
            // The SDK's TextMeshPro is older than the game's: this property is the one both versions have.
#pragma warning disable CS0618
            text.enableWordWrapping = wrap;
#pragma warning restore CS0618
            text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            if (_font != null)
            {
                text.font = _font;
            }
            return text;
        }

        /// <summary>A button with a text on it. In a layout group it asks for <paramref name="height"/>.</summary>
        public Button Button(string objectName, Transform parent, string label, Action onClick, float height = 46f, float fontSize = 20f)
        {
            RectTransform rect = Box(objectName, parent, Plate, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.66f, 0.66f, 0.66f, 1f);
            colors.selectedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            if (onClick != null)
            {
                button.onClick.AddListener(() =>
                {
                    try
                    {
                        onClick();
                    }
                    catch (Exception e)
                    {
                        MetaLog.Error(e);
                    }
                });
            }
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            TextMeshProUGUI text = Text("Label", rect, fontSize, Ink, TextAlignmentOptions.Center, false);
            Stretch(text.rectTransform, 10f, 2f, 10f, 2f);
            text.text = label;
            return button;
        }

        /// <summary>A vertical scroll view: the mouse wheel, a draggable bar, and the keys through MetaPanel.KeyScroll.</summary>
        public ScrollRect Scroll(string objectName, Transform parent, out RectTransform content, float spacing)
        {
            RectTransform root = Box(objectName, parent, new Color(0f, 0f, 0f, 0.18f), true);   // a target, so the wheel works over empty space
            var scroll = root.gameObject.AddComponent<ScrollRect>();

            GameObject viewport = New("Viewport", root);
            var viewRect = (RectTransform)viewport.transform;
            Stretch(viewRect, 0f, 0f, 14f, 0f);
            viewport.AddComponent<RectMask2D>();

            GameObject inner = New("Content", viewRect);
            content = (RectTransform)inner.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = inner.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = spacing;
            layout.padding = new RectOffset(14, 14, 12, 16);
            var fitter = inner.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            RectTransform barRect = Box("Scrollbar", root, new Color(1f, 1f, 1f, 0.06f), true);
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.offsetMin = new Vector2(-12f, 2f);
            barRect.offsetMax = new Vector2(-2f, -2f);
            var bar = barRect.gameObject.AddComponent<Scrollbar>();
            GameObject slide = New("Sliding Area", barRect);
            Stretch((RectTransform)slide.transform, 0f, 0f, 0f, 0f);
            RectTransform handle = Box("Handle", slide.transform, new Color(1f, 1f, 1f, 0.35f), true);
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            bar.handleRect = handle;
            bar.targetGraphic = handle.GetComponent<Image>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            var barNavigation = bar.navigation;
            barNavigation.mode = Navigation.Mode.None;
            bar.navigation = barNavigation;

            scroll.viewport = viewRect;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        /// <summary>Fills the parent, leaving these margins.</summary>
        public static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>A full-screen dimmer (which also takes every click meant for what is behind) with a window in its middle.</summary>
        public GameObject Frame(string objectName, Vector2 size, out RectTransform window)
        {
            RectTransform dimmer = Box(objectName, transform, Dim, true);
            Stretch(dimmer, 0f, 0f, 0f, 0f);
            RectTransform edge = Box("Edge", dimmer, Accent, false);
            edge.anchorMin = edge.anchorMax = edge.pivot = new Vector2(0.5f, 0.5f);
            edge.sizeDelta = size + new Vector2(4f, 4f);
            window = Box("Window", dimmer, Window, true);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            window.sizeDelta = size;
            // The dimmer is itself selectable, so a press on the panel outside its buttons leaves it selected: the event system is
            // never without a selection before the next Update, and the camera never takes the cursor in between.
            var selectable = dimmer.gameObject.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None;
            var navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
            _sizes[dimmer.gameObject] = size;
            dimmer.gameObject.SetActive(false);
            return dimmer.gameObject;
        }

        // A window made for a 16:9 screen is made to fit a narrower one each time it is shown.
        private void Fit(GameObject root)
        {
            if (!_sizes.TryGetValue(root, out Vector2 size))
            {
                return;
            }
            Vector2 room = ((RectTransform)transform).rect.size - new Vector2(24f, 24f);
            if (room.x < 200f || room.y < 200f)
            {
                return;   // the canvas has not been laid out yet
            }
            size = new Vector2(Mathf.Min(size.x, room.x), Mathf.Min(size.y, room.y));
            if (root.transform.Find("Window") is RectTransform window)
            {
                window.sizeDelta = size;
            }
            if (root.transform.Find("Edge") is RectTransform edge)
            {
                edge.sizeDelta = size + new Vector2(4f, 4f);
            }
        }

        /// <summary>A bar along the top of a window with a title in it; returns the bar for anything else that goes on it.</summary>
        private RectTransform TitleBar(RectTransform window, string title, out TextMeshProUGUI text)
        {
            RectTransform bar = Box("Title Bar", window, Bar, false);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -58f);
            bar.offsetMax = Vector2.zero;
            text = Text("Title", bar, 26f, Accent, TextAlignmentOptions.MidlineLeft, false);
            Stretch(text.rectTransform, 22f, 0f, 22f, 0f);
            text.fontStyle = FontStyles.Bold;
            text.text = title;
            return bar;
        }

        private sealed class MessagePanel : MetaPanel
        {
            private readonly MetaUi _ui;
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _body;
            private readonly RectTransform _buttons;

            public MessagePanel(MetaUi ui)
            {
                _ui = ui;
                Root = ui.Frame("Message", new Vector2(980f, 640f), out RectTransform window);
                ui.TitleBar(window, string.Empty, out _title);

                KeyScroll = ui.Scroll("Body", window, out RectTransform content, 8f);
                Stretch((RectTransform)KeyScroll.transform, 18f, 84f, 18f, 70f);
                _body = ui.Text("Text", content, 21f, Ink, TextAlignmentOptions.TopLeft, true);

                GameObject row = ui.New("Buttons", window);
                _buttons = (RectTransform)row.transform;
                _buttons.anchorMin = new Vector2(0f, 0f);
                _buttons.anchorMax = new Vector2(1f, 0f);
                _buttons.pivot = new Vector2(0.5f, 0f);
                _buttons.offsetMin = new Vector2(18f, 16f);
                _buttons.offsetMax = new Vector2(-18f, 68f);
                var layout = row.AddComponent<HorizontalLayoutGroup>();
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;
                layout.spacing = 14f;
            }

            public void Set(string title, string body, KeyValuePair<string, Action>[] buttons)
            {
                _title.text = title;
                _body.text = body;
                KeyScroll.verticalNormalizedPosition = 1f;
                for (int i = _buttons.childCount - 1; i >= 0; i--)
                {
                    GameObject old = _buttons.GetChild(i).gameObject;
                    old.SetActive(false);
                    UnityEngine.Object.Destroy(old);
                }
                if (buttons == null || buttons.Length == 0)
                {
                    buttons = new[] { new KeyValuePair<string, Action>("reallife_meta_ok", null) };
                }
                foreach (KeyValuePair<string, Action> button in buttons)
                {
                    Action action = button.Value;
                    _ui.Button("Button", _buttons, button.Key.GetLocalization(), () =>
                    {
                        _ui.Close(this);
                        action?.Invoke();
                    });
                }
            }
        }
    }
}
