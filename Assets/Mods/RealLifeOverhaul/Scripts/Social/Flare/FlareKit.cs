using System;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's own look, the one interface the mod draws itself (Flare must look like a social media app), kept inside the
    /// game's phone with its panel, font and text box: colours, type sizes, spacing and the parts of every screen. The
    /// rounded shapes are drawn once into soft-edged textures, nine-sliced at any size. Cost: those textures once a session.
    /// </summary>
    internal static class FlareKit
    {
        // Type sizes and spacing, in canvas units of the game's 3840 x 2160.
        public const float Body = 31f;           // a post's words
        public const float Name = 31f;           // who
        public const float Small = 26f;          // a handle, a time, a count
        public const float Title = 46f;          // a screen's title
        public const float Logo = 70f;
        public const float Nav = 36f;            // the rail's items
        public const float Pad = 34f;            // inside a card
        public const float Gap = 22f;            // between parts
        public const float Avatar = 84f;
        public const float Radius = 30f;         // a card's corners

        private const int TexSize = 64, TexRadius = 30;
        private static Sprite _round, _circle;

        /// <summary>A rounded square, nine-sliced: its corners are <see cref="Corner"/> wide at a multiplier of 1.</summary>
        public static Sprite Round => _round != null ? _round : (_round = Make(false));

        /// <summary>A circle (also a pill, nine-sliced along its length).</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = Make(true));

        public const float Corner = TexRadius;

        // A white shape with soft edges, as a sprite whose corners do not stretch.
        private static Sprite Make(bool circle)
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { name = circle ? "FlareCircle" : "FlareRound", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[TexSize * TexSize];
            float r = circle ? TexSize / 2f : TexRadius;
            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;
                    float dx = Mathf.Max(Mathf.Max(r - cx, cx - (TexSize - r)), 0f);
                    float dy = Mathf.Max(Mathf.Max(r - cy, cy - (TexSize - r)), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - r;   // the distance outside the shape, negative inside
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * TexSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float border = circle ? TexSize / 2f - 1f : TexRadius;
            return Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            return r;
        }

        /// <summary>An image of a colour: square, rounded (corners of <paramref name="radius"/> canvas units), or a circle or pill.</summary>
        public static Image Box(Transform parent, string name, Color color, float radius = 0f, bool circle = false, bool raycast = false)
        {
            RectTransform r = Rect(parent, name);
            var image = r.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            if (circle)
            {
                image.sprite = Circle;
                image.type = Image.Type.Sliced;
            }
            else if (radius > 0f)
            {
                image.sprite = Round;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = Corner / radius;
            }
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, float size, Color color, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            RectTransform r = Rect(parent, name);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GameFont.Asset;
            if (font != null)
            {
                t.font = font;
            }
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.richText = true;
            t.raycastTarget = false;
            t.enableAutoSizing = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.margin = Vector4.zero;
            return t;
        }

        public static RawImage Picture(Transform parent, string name)
        {
            RectTransform r = Rect(parent, name);
            var image = r.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A click on a graphic, made once with the part, so a pooled card keeps its button; run inside the guard (UiClone.Safe).</summary>
        public static Button Click(Graphic target, Action action)
        {
            target.raycastTarget = true;
            Button button = target.gameObject.GetComponent<Button>() ?? target.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(0.92f, 0.92f, 0.94f, 1f);
            colours.pressedColor = new Color(0.82f, 0.82f, 0.86f, 1f);
            colours.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colours.fadeDuration = 0.06f;
            button.colors = colours;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            UiClone.Rewire(button, action);
            return button;
        }

        public static void Place(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        public static void Stretch(RectTransform r, float left, float top, float right, float bottom)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(-right, -top);
        }

        public static void Show(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on)
            {
                c.gameObject.SetActive(on);
            }
        }

        /// <summary>A text set only when it changes, as TextMeshPro builds its mesh again on every set.</summary>
        public static void Set(TMP_Text t, string text)
        {
            text = text ?? string.Empty;
            if (!string.Equals(t.text, text, StringComparison.Ordinal))
            {
                t.text = text;
            }
        }

        /// <summary>A text's height at a width, as it would be set (measured, not drawn).</summary>
        public static float Height(TMP_Text t, string text, float width)
        {
            return string.IsNullOrEmpty(text) ? 0f : t.GetPreferredValues(text, width, 0f).y;
        }
    }

    /// <summary>Flare's colours, light or dark; the brand's own coral-to-rose (the app's icon) is its accent.</summary>
    internal sealed class FlareLook
    {
        public Color Back, Card, Raised, Text, Muted, Line, Accent, AccentSoft, Like, Share, Chip, ChipText, Shade, Good, Bad;
        public bool Dark;

        public static readonly FlareLook Light = new FlareLook
        {
            Back = C(244, 245, 247), Card = C(255, 255, 255), Raised = C(250, 250, 251), Text = C(15, 20, 25), Muted = C(83, 100, 113), Line = C(230, 233, 236),
            Accent = C(231, 72, 106), AccentSoft = new Color(231 / 255f, 72 / 255f, 106 / 255f, 0.12f), Like = C(249, 24, 128), Share = C(0, 186, 124), Chip = C(239, 241, 243),
            ChipText = C(40, 48, 56), Shade = new Color(0f, 0f, 0f, 0.42f), Good = C(0, 160, 100), Bad = C(220, 50, 60)
        };

        public static readonly FlareLook Night = new FlareLook
        {
            Back = C(13, 15, 18), Card = C(22, 24, 28), Raised = C(30, 33, 38), Text = C(231, 233, 234), Muted = C(139, 152, 165), Line = C(47, 51, 54),
            Accent = C(255, 107, 125), AccentSoft = new Color(1f, 107 / 255f, 125 / 255f, 0.16f), Like = C(249, 24, 128), Share = C(0, 186, 124), Chip = C(36, 39, 45),
            ChipText = C(220, 224, 228), Shade = new Color(0f, 0f, 0f, 0.6f), Good = C(60, 200, 140), Bad = C(255, 90, 95), Dark = true
        };

        /// <summary>The look for now: the option or, following the phone, dark from 19:00 to 07:00 of the game's clock.</summary>
        public static FlareLook Now(int hour)
        {
            switch (Settings.FlareTheme)
            {
                case 1: return Light;
                case 2: return Night;
                default: return hour >= 19 || hour < 7 ? Night : Light;
            }
        }

        private static readonly Color[] Avatars =
        {
            C(255, 150, 92), C(214, 62, 98), C(92, 124, 250), C(32, 178, 170), C(155, 89, 182), C(241, 196, 15), C(46, 204, 113), C(52, 152, 219),
            C(230, 126, 34), C(26, 188, 156)
        };

        /// <summary>An avatar's colour, always the same for the same name.</summary>
        public static Color AvatarOf(string key)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; key != null && i < key.Length; i++)
                {
                    h = h * 31 + key[i];
                }
                return Avatars[(h & 0x7FFFFFFF) % Avatars.Length];
            }
        }

        private static Color C(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
