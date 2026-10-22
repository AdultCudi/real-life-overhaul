using System;
using System.Collections.Generic;
using BAModAPI;
using Player.HUD.SmartphoneUI;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Makes the HUD phone look like a modern phone: a thin rounded body, an opaque wallpaper (or a Gallery photo), a status
    /// bar, a camera cut-out and a home bar. It goes in through the default entry of the game's SmartPhoneFrame, so bought
    /// phone accessories keep working. The artwork is drawn once a session; a frame costs one minute comparison for the clock.
    /// </summary>
    public class PhoneSkin : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("phone", "PhoneSkin", typeof(PhoneSkin));

        private const int TextureSize = 256;   // pixels
        private const int SliceBorder = 72;    // pixels
        private const float CornerRadius = 62f;   // pixels
        private const float BezelWidth = 18f;     // pixels

        public static IModLogger Logger;
        // Drawn once a session and shared by every city load.
        private static Sprite _frameSprite;
        private static Sprite _maskSprite;
        private static Sprite _wallpaperSprite;
        private static Sprite _circleSprite;
        private static Sprite _pillSprite;
        private static Sprite _statusSprite;

        private bool _applied;
        private SmartPhoneFrame _frame;
        private SmartPhoneFrame.FrameData _defaultFrame;
        private Sprite _vanillaFrameSprite;
        private Sprite _vanillaMaskSprite;
        private Image _screen;
        private Color _vanillaScreenColor;
        private Image _header;
        private Sprite _vanillaHeaderSprite;
        private Color _vanillaHeaderColor;
        private TMP_Text _title;
        private Color _vanillaTitleColor;
        private Image _arrow;
        private Color _vanillaArrowColor;
        private GameObject _background;
        private GameObject _chrome;
        private TextMeshProUGUI _clock;
        private int _clockMinute = -1;
        private RawImage _photo;            // a Gallery photo as the wallpaper, over the drawn one
        private Texture _photoTexture;      // the Gallery's own texture: never destroyed here

        private const float ScreenShape = 628f / 1230f;   // the HUD phone's width to its height (Smartphone/Container), until its screen is laid out

        /// <summary>The phone's look while it is applied (the option on and the game's phone found).</summary>
        public static PhoneSkin Instance { get; private set; }

        /// <summary>True while the modern look is on the phone: only then has it a wallpaper a photo can be.</summary>
        public bool Applied => _applied;

        /// <summary>A picture as the wallpaper, or null for the drawn one; false while the look is off. The caller keeps the picture alive.</summary>
        public bool Wallpaper(Texture picture)
        {
            if (picture != null && !_applied)
            {
                return false;
            }
            _photoTexture = picture;
            ShowPhoto();
            return true;
        }

        // The photo over the drawn wallpaper, its middle cut to the screen's shape; or taken away.
        private void ShowPhoto()
        {
            if (_photoTexture == null || !_applied || _background == null)
            {
                if (_photo != null)
                {
                    Destroy(_photo.gameObject);
                    _photo = null;
                }
                return;
            }
            if (_photo == null)
            {
                var made = new GameObject("RealLifeOverhaul Wallpaper Photo", typeof(RectTransform));
                made.transform.SetParent(_background.transform.parent, false);
                made.transform.SetSiblingIndex(_background.transform.GetSiblingIndex() + 1);
                Stretch((RectTransform)made.transform);
                _photo = made.AddComponent<RawImage>();
                _photo.raycastTarget = false;   // never block clicks meant for the phone itself
            }
            _photo.texture = _photoTexture;
            var screen = _background.transform.parent as RectTransform;
            float shape = screen != null && screen.rect.height > 1f ? screen.rect.width / screen.rect.height : ScreenShape;
            float picture = (float)_photoTexture.width / Mathf.Max(1, _photoTexture.height);
            _photo.uvRect = picture > shape ? new Rect((1f - shape / picture) / 2f, 0f, shape / picture, 1f) : new Rect(0f, (1f - picture / shape) / 2f, 1f, picture / shape);
        }

        private void Awake()
        {
            Instance = this;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Phone: PhoneSkin.Update");
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
            if (Settings.PhoneSkin != _applied)
            {
                if (Settings.PhoneSkin)
                {
                    TryApply();
                }
                else
                {
                    Remove();
                }
            }

            if (_applied && _clock != null)
            {
                int minute = TimeHelper.CurrentHour * 60 + (int)TimeHelper.CurrentMinute;
                if (minute != _clockMinute)
                {
                    _clockMinute = minute;
                    _clock.SetText("{0:00}:{1:00}", minute / 60, minute % 60);
                }
            }
        }

        private void OnDestroy()
        {
            Remove();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void TryApply()
        {
            var uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.smartphoneUI == null)
            {
                return;
            }

            try
            {
                _frame = uis.smartphoneUI.GetComponent<SmartPhoneFrame>();
                // The frame's private fields, resolved once (Core/GameContract): null when a game update took one away.
                var frames = _frame != null ? GameContract.FrameData.Field?.GetValue(_frame) as List<SmartPhoneFrame.FrameData> : null;
                var frameImage = _frame != null ? GameContract.FrameImage.Field?.GetValue(_frame) as Image : null;
                var maskImage = _frame != null ? GameContract.FrameMask.Field?.GetValue(_frame) as Image : null;
                if (_frame == null || frames == null || frames.Count == 0 || frameImage == null || maskImage == null)
                {
                    Logger?.Warn("Phone skin: the game's phone frame was not found; leaving the phone alone.");
                    Settings.PhoneSkin = false;
                    return;
                }

                CreateSprites();

                // The body: swap the default frame's artwork and let the game apply it.
                _defaultFrame = frames[0];
                _vanillaFrameSprite = _defaultFrame.frameSprite;
                _vanillaMaskSprite = _defaultFrame.maskSprite;
                _defaultFrame.frameSprite = _frameSprite;
                _defaultFrame.maskSprite = _maskSprite;
                _frame.UpdateFrame();

                Transform mask = maskImage.transform;
                _background = CreateImage("RealLifeOverhaul Wallpaper", mask, _wallpaperSprite, Color.white);
                Stretch((RectTransform)_background.transform);
                _background.transform.SetAsFirstSibling();

                // The vanilla screen is a see-through grey box; the wallpaper replaces it.
                _screen = mask.Find("OffsetReverser/Phone")?.GetComponent<Image>();
                if (_screen != null)
                {
                    _vanillaScreenColor = _screen.color;
                    _screen.color = Color.clear;
                }

                // The grey "BIZPHONE" bar becomes part of the screen: clear background, light text.
                Transform header = mask.Find("OffsetReverser/Headline");
                _header = header != null ? header.GetComponent<Image>() : null;
                if (_header != null)
                {
                    _vanillaHeaderSprite = _header.sprite;
                    _vanillaHeaderColor = _header.color;
                    _header.sprite = null;
                    _header.color = Color.clear;

                    _title = header.Find("Title")?.GetComponent<TMP_Text>();
                    if (_title != null)
                    {
                        _vanillaTitleColor = _title.color;
                        _title.color = new Color(1f, 1f, 1f, 0.92f);
                    }
                    _arrow = header.Find("Icon")?.GetComponent<Image>();
                    if (_arrow != null)
                    {
                        _vanillaArrowColor = _arrow.color;
                        _arrow.color = new Color(1f, 1f, 1f, 0.85f);
                    }
                }

                CreateChrome(mask);
                _applied = true;
                ShowPhoto();
                Logger?.Info("Phone skin applied.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Remove();
                Settings.PhoneSkin = false;
            }
        }

        // Status bar, camera cut-out and home bar, drawn above the screen content and below the body.
        private void CreateChrome(Transform mask)
        {
            _chrome = new GameObject("RealLifeOverhaul Phone Chrome", typeof(RectTransform));
            _chrome.transform.SetParent(mask, false);
            Stretch((RectTransform)_chrome.transform);
            _chrome.transform.SetAsLastSibling();

            var clockGo = new GameObject("Clock", typeof(RectTransform));
            clockGo.transform.SetParent(_chrome.transform, false);
            Place((RectTransform)clockGo.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -30f), new Vector2(170f, 44f));
            _clock = clockGo.AddComponent<TextMeshProUGUI>();
            _clock.raycastTarget = false;
            _clock.alignment = TextAlignmentOptions.Left;
            _clock.fontSize = 30f;
            _clock.fontStyle = FontStyles.Bold;
            _clock.color = Color.white;
            if (_title != null && _title.font != null)
            {
                _clock.font = _title.font;
            }
            _clockMinute = -1;

            GameObject status = CreateImage("Signal and Battery", _chrome.transform, _statusSprite, new Color(1f, 1f, 1f, 0.95f));
            Place((RectTransform)status.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -52f), new Vector2(108f, 36f));

            GameObject camera = CreateImage("Camera", _chrome.transform, _circleSprite, new Color(0.02f, 0.02f, 0.03f, 1f));
            Place((RectTransform)camera.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(24f, 24f));

            GameObject home = CreateImage("Home Bar", _chrome.transform, _pillSprite, new Color(1f, 1f, 1f, 0.45f));
            Place((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(190f, 9f));
            home.GetComponent<Image>().type = Image.Type.Sliced;
        }

        private void Remove()
        {
            if (_defaultFrame != null)
            {
                _defaultFrame.frameSprite = _vanillaFrameSprite;
                _defaultFrame.maskSprite = _vanillaMaskSprite;
                _defaultFrame = null;
                if (_frame != null)
                {
                    _frame.UpdateFrame();
                }
            }
            if (_screen != null)
            {
                _screen.color = _vanillaScreenColor;
                _screen = null;
            }
            if (_header != null)
            {
                _header.sprite = _vanillaHeaderSprite;
                _header.color = _vanillaHeaderColor;
                _header = null;
            }
            if (_title != null)
            {
                _title.color = _vanillaTitleColor;
                _title = null;
            }
            if (_arrow != null)
            {
                _arrow.color = _vanillaArrowColor;
                _arrow = null;
            }
            if (_photo != null)
            {
                Destroy(_photo.gameObject);
                _photo = null;
            }
            if (_background != null)
            {
                Destroy(_background);
            }
            if (_chrome != null)
            {
                Destroy(_chrome);
            }
            _clock = null;
            _applied = false;
        }

        private static GameObject CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;   // never block clicks meant for the phone itself
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void CreateSprites()
        {
            if (_frameSprite != null)
            {
                return;
            }
            var border = new Vector4(SliceBorder, SliceBorder, SliceBorder, SliceBorder);
            _frameSprite = MakeSprite("RealLifeOverhaul Phone Body", TextureSize, TextureSize, border, BodyPixel);
            _maskSprite = MakeSprite("RealLifeOverhaul Phone Screen", TextureSize, TextureSize, border, ScreenPixel);
            _wallpaperSprite = MakeSprite("RealLifeOverhaul Wallpaper", 4, 256, Vector4.zero, WallpaperPixel);
            _circleSprite = MakeSprite("RealLifeOverhaul Circle", 48, 48, Vector4.zero, CirclePixel);
            _pillSprite = MakeSprite("RealLifeOverhaul Pill", 48, 16, new Vector4(8f, 0f, 8f, 0f), PillPixel);
            _statusSprite = MakeSprite("RealLifeOverhaul Status", 108, 36, Vector4.zero, StatusPixel);
        }

        private static Sprite MakeSprite(string spriteName, int width, int height, Vector4 border, Func<float, float, int, int, Color> shade)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = spriteName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = shade(x + 0.5f, y + 0.5f, width, height);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);   // upload, then free the CPU copy
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = spriteName;
            return sprite;
        }

        // Signed distance to a rounded rectangle filling the texture: negative inside.
        private static float RoundedBox(float x, float y, int width, int height, float radius)
        {
            float qx = Mathf.Abs(x - width * 0.5f) - (width * 0.5f - radius);
            float qy = Mathf.Abs(y - height * 0.5f) - (height * 0.5f - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        // A ring: near-black body with a bright outer edge, like a metal phone rim.
        private static Color BodyPixel(float x, float y, int width, int height)
        {
            float d = RoundedBox(x, y, width, height, CornerRadius);
            float alpha = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + BezelWidth + 0.5f);
            Color body = new Color(0.035f, 0.038f, 0.048f);
            Color rim = new Color(0.42f, 0.44f, 0.50f);
            Color inner = new Color(0.11f, 0.12f, 0.15f);
            Color color = Color.Lerp(body, rim, Mathf.Clamp01((d + 3.5f) / 3f));
            color = Color.Lerp(color, inner, Mathf.Clamp01((-d - BezelWidth + 2.5f) / 2f));
            color.a = alpha;
            return color;
        }

        private static Color ScreenPixel(float x, float y, int width, int height)
        {
            float d = RoundedBox(x, y, width, height, CornerRadius);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
        }

        private static Color WallpaperPixel(float x, float y, int width, int height)
        {
            float t = y / height;
            Color bottom = new Color(0.035f, 0.050f, 0.090f);
            Color middle = new Color(0.075f, 0.105f, 0.200f);
            Color top = new Color(0.180f, 0.150f, 0.330f);
            Color color = t < 0.55f ? Color.Lerp(bottom, middle, t / 0.55f) : Color.Lerp(middle, top, (t - 0.55f) / 0.45f);
            color.a = 1f;
            return color;
        }

        private static Color CirclePixel(float x, float y, int width, int height)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(width * 0.5f, height * 0.5f)) - (width * 0.5f - 1f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
        }

        private static Color PillPixel(float x, float y, int width, int height)
        {
            float d = RoundedBox(x, y, width, height, height * 0.5f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
        }

        // Four signal bars on the left, a battery on the right.
        private static Color StatusPixel(float x, float y, int width, int height)
        {
            float alpha = 0f;
            for (int bar = 0; bar < 4; bar++)
            {
                float left = 4f + bar * 9f;
                float barHeight = 8f + bar * 6f;
                if (x >= left && x < left + 6f && y >= 6f && y < 6f + barHeight)
                {
                    alpha = 1f;
                }
            }

            const float batteryLeft = 52f, batteryRight = 98f, batteryBottom = 7f, batteryTop = 29f;
            float cx = (batteryLeft + batteryRight) * 0.5f, cy = (batteryBottom + batteryTop) * 0.5f;
            float hx = (batteryRight - batteryLeft) * 0.5f, hy = (batteryTop - batteryBottom) * 0.5f;
            float qx = Mathf.Abs(x - cx) - (hx - 5f), qy = Mathf.Abs(y - cy) - (hy - 5f);
            float shell = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - 5f;
            float outline = Mathf.Clamp01(0.5f - shell) * Mathf.Clamp01(shell + 3f);
            bool charge = x >= batteryLeft + 5f && x < batteryLeft + 5f + (batteryRight - batteryLeft - 10f) * 0.8f
                          && y >= batteryBottom + 5f && y < batteryTop - 5f;
            bool cap = x >= batteryRight + 1f && x < batteryRight + 5f && y >= cy - 5f && y < cy + 5f;
            alpha = Mathf.Max(alpha, Mathf.Max(outline, charge || cap ? 1f : 0f));
            return new Color(1f, 1f, 1f, alpha);
        }
    }
}
