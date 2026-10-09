using System;
using Extensions;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>Texts of the Garage: a locale key in, the line out, with {key} filled in. Builds strings: for when something changes, never per frame.</summary>
    public static class GarageText
    {
        private static string _key;
        private static int _keyVersion = -1;

        public static string T(string key)
        {
            string text = key.GetLocalization();
            if (text.IndexOf("{key}", StringComparison.Ordinal) < 0)
            {
                return text;
            }
            if (_keyVersion != Keys.Version)
            {
                _keyVersion = Keys.Version;
                _key = Keys.Label(KeyAction.Garage);
            }
            return text.Replace("{key}", _key);
        }

        public static string T(string key, string name, string value)
        {
            return T(key).Replace("{" + name + "}", value);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return T(key).Replace("{" + name + "}", value).Replace("{" + name2 + "}", value2);
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        public static string Signed(float factor)
        {
            int percent = Mathf.RoundToInt((factor - 1f) * 100f);
            return (percent > 0 ? "+" : string.Empty) + percent;
        }
    }

    /// <summary>
    /// The Garage's own screen layer: one canvas in the style of the mod's HUD (dark plates, the game's
    /// font, laid out for 1080p), with a raycaster so its buttons take the mouse. It carries the custom
    /// shop's panel, the "My vehicles" summary, the two-wheeler stands' panel and the key prompt. Built on
    /// first use and destroyed with the city; nothing here runs per frame. While a panel that needs the
    /// mouse is up, <see cref="NeedsCursor"/> is true and the mod's cameras leave the cursor alone.
    /// </summary>
    public sealed class GarageUi : MonoBehaviour
    {
        public static readonly Color Plate = new Color(0.05f, 0.06f, 0.08f, 0.93f);
        public static readonly Color Row = new Color(0.13f, 0.15f, 0.19f, 0.95f);
        public static readonly Color Chosen = new Color(0.16f, 0.45f, 0.78f, 1f);
        public static readonly Color Go = new Color(0.14f, 0.55f, 0.27f, 1f);
        public static readonly Color Stop = new Color(0.55f, 0.18f, 0.16f, 1f);
        public static readonly Color Dim = new Color(0.72f, 0.78f, 0.86f);

        private static GarageUi _instance;
        private static int _cursorUsers;

        private TMP_FontAsset _font;
        private GameObject _prompt;
        private TextMeshProUGUI _promptText;
        private string _promptShown;

        public static GarageUi Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Garage UI").AddComponent<GarageUi>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        /// <summary>True while one of the Garage's panels needs the mouse cursor (read by Camera/ThirdPersonCamera.IsUiOpen).</summary>
        public static bool NeedsCursor => _cursorUsers > 0;

        public static void TakeCursor()
        {
            _cursorUsers++;
        }

        public static void GiveCursor()
        {
            _cursorUsers = Mathf.Max(0, _cursorUsers - 1);
        }

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -8;   // under the game's own menus, over the mod's HUD
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            _font = Hud.Font;

            RectTransform prompt = Box("Prompt", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(760f, 44f), Plate);
            prompt.GetComponent<Image>().raycastTarget = false;
            _promptText = Label("Text", prompt, 22f, Color.white, TextAlignmentOptions.Center);
            Stretch(_promptText.rectTransform, 12f);
            _prompt = prompt.gameObject;
            _prompt.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                _cursorUsers = 0;
            }
        }

        /// <summary>The line at the bottom of the screen that names a key; null hides it. Costs nothing when the text is the one already shown.</summary>
        public void Prompt(string text)
        {
            if (text == _promptShown)
            {
                return;
            }
            _promptShown = text;
            _prompt.SetActive(text != null);
            if (text != null)
            {
                _promptText.SetText(text);
            }
        }

        // ---------------------------------------------------------------- building blocks

        /// <summary>A plate. Anchor and pivot are the same point of the parent and of the plate.</summary>
        public RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = colour;
            return rect;
        }

        /// <summary>A plate placed by its top-left corner inside a parent, y measured downwards.</summary>
        public RectTransform At(string name, Transform parent, float x, float y, float width, float height, Color colour)
        {
            return Box(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height), colour);
        }

        public TextMeshProUGUI Label(string name, Transform parent, float size, Color colour, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = align;
            text.fontSize = size;
            text.color = colour;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (_font != null)
            {
                text.font = _font;
            }
            return text;
        }

        /// <summary>A line of text placed by its top-left corner, y measured downwards.</summary>
        public TextMeshProUGUI TextAt(Transform parent, float x, float y, float width, float height, float size, Color colour, string text,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            TextMeshProUGUI label = Label("Text", parent, size, colour, align);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            label.SetText(text ?? string.Empty);
            return label;
        }

        public Button ButtonAt(Transform parent, float x, float y, float width, float height, Color colour, string text, UnityAction onClick, float size = 18f)
        {
            RectTransform rect = At("Button", parent, x, y, width, height, colour);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colours.fadeDuration = 0.05f;
            button.colors = colours;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;   // the game's keys must not walk a selection across these
            button.navigation = navigation;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
            if (text != null)
            {
                TextMeshProUGUI label = Label("Label", rect, size, Color.white, TextAlignmentOptions.Center);
                Stretch(label.rectTransform, 4f);
                label.SetText(text);
            }
            return button;
        }

        /// <summary>A slider from 0 to 1 with a plain track and handle.</summary>
        public Slider SliderAt(Transform parent, float x, float y, float width, float value, UnityAction<float> onChange)
        {
            RectTransform track = At("Slider", parent, x, y, width, 18f, Row);
            var slider = track.gameObject.AddComponent<Slider>();
            var area = new GameObject("Handle Area", typeof(RectTransform));
            area.transform.SetParent(track, false);
            var areaRect = (RectTransform)area.transform;
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(8f, 0f);
            areaRect.offsetMax = new Vector2(-8f, 0f);
            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(area.transform, false);
            var handleRect = (RectTransform)handle.transform;
            handleRect.sizeDelta = new Vector2(16f, 8f);
            var knob = handle.AddComponent<Image>();
            knob.color = Color.white;
            slider.handleRect = handleRect;
            slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            var navigation = slider.navigation;
            navigation.mode = Navigation.Mode.None;
            slider.navigation = navigation;
            slider.SetValueWithoutNotify(value);
            if (onChange != null)
            {
                slider.onValueChanged.AddListener(onChange);
            }
            return slider;
        }

        /// <summary>Lets a text run over several lines inside its box.</summary>
        public static void Wrap(TextMeshProUGUI text)
        {
            // The SDK's TextMeshPro is older than the game's: this property is the one both versions have.
#pragma warning disable CS0618
            text.enableWordWrapping = true;
#pragma warning restore CS0618
            text.overflowMode = TextOverflowModes.Truncate;
        }

        public static void Stretch(RectTransform rect, float margin)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        public static void Empty(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }
    }

    /// <summary>
    /// The part of the screen the shop's panel leaves free: dragging on it turns the turntable view, the
    /// wheel moves it in and out. An invisible plate that takes the mouse, so clicks in the shop never
    /// reach the world behind it.
    /// </summary>
    public sealed class GarageDragArea : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public Action<Vector2> Dragged;
        public Action<float> Scrolled;

        public void OnDrag(PointerEventData data)
        {
            Dragged?.Invoke(data.delta);
        }

        public void OnScroll(PointerEventData data)
        {
            Scrolled?.Invoke(data.scrollDelta.y);
        }
    }
}
