using System;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// The Economy app's own look, the second interface the mod draws itself beside Flare's (the owner, 27 Oct 2026: "Keep the app
    /// theme dark as default. Also make it beautiful not the same old UI"), kept inside the game's phone with its panel, its font
    /// and its click. The rounded shapes, the circle and the soft shadow are drawn once into textures and nine-sliced at any size.
    /// Nothing here runs per frame; the sprites are made once a session.
    /// </summary>
    internal static class OutlookKit
    {
        public const float Corner = 30f;    // canvas units a rounded sprite's corner is wide at a multiplier of 1
        public const float Reach = 36f;     // canvas units a card's shadow spreads past its edge at a multiplier of 1

        private const int RoundSize = 64, RoundRadius = 30;
        private const int SoftSize = 128, SoftInset = 36, SoftRadius = 28;
        private static Sprite _round, _circle, _soft;

        /// <summary>A rounded square, nine-sliced: its corners are <see cref="Corner"/> wide at a multiplier of 1.</summary>
        public static Sprite Round => _round != null ? _round : (_round = Make(false));

        /// <summary>A circle, and a pill when stretched (its ends stay round).</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = Make(true));

        /// <summary>A soft shadow: a rounded box that fades out over <see cref="Reach"/>, nine-sliced.</summary>
        public static Sprite Soft => _soft != null ? _soft : (_soft = MakeSoft());

        // A white shape with soft edges, as a sprite whose corners do not stretch.
        private static Sprite Make(bool circle)
        {
            var tex = new Texture2D(RoundSize, RoundSize, TextureFormat.RGBA32, false, false)
            {
                name = circle ? "EconCircle" : "EconRound", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var px = new Color32[RoundSize * RoundSize];
            float r = circle ? RoundSize / 2f : RoundRadius;
            for (int y = 0; y < RoundSize; y++)
            {
                for (int x = 0; x < RoundSize; x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;
                    float dx = Mathf.Max(Mathf.Max(r - cx, cx - (RoundSize - r)), 0f);
                    float dy = Mathf.Max(Mathf.Max(r - cy, cy - (RoundSize - r)), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - r;   // the distance outside the shape, negative inside
                    px[y * RoundSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            float border = circle ? RoundSize / 2f - 1f : RoundRadius;
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, RoundSize, RoundSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        // A box inside the texture that is solid, and a fall-off round it: the shadow of a card.
        private static Sprite MakeSoft()
        {
            var tex = new Texture2D(SoftSize, SoftSize, TextureFormat.RGBA32, false, false) { name = "EconShadow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[SoftSize * SoftSize];
            float lo = SoftInset, hi = SoftSize - SoftInset;
            for (int y = 0; y < SoftSize; y++)
            {
                for (int x = 0; x < SoftSize; x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;
                    float dx = Mathf.Max(Mathf.Max(lo + SoftRadius - cx, cx - (hi - SoftRadius)), 0f);
                    float dy = Mathf.Max(Mathf.Max(lo + SoftRadius - cy, cy - (hi - SoftRadius)), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - SoftRadius;   // outside the box, negative inside
                    float fall = Mathf.Clamp01(1f - d / SoftInset);
                    float a = d <= 0f ? 1f : fall * fall * fall;
                    px[y * SoftSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            float border = SoftSize / 2f - 1f;
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, SoftSize, SoftSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        // ------------------------------------------------------------------ colours

        public static Color C(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);
        }

        public static Color32 C32(int rgb, float alpha = 1f)
        {
            return new Color32((byte)((rgb >> 16) & 255), (byte)((rgb >> 8) & 255), (byte)(rgb & 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
        }

        // ------------------------------------------------------------------ parts

        /// <summary>A rect under a parent, anchored and pivoted at its top left, so a position is a distance from the corner and y counts down.</summary>
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            return r;
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

        public static TextMeshProUGUI NewText(Transform parent, string name)
        {
            RectTransform r = Rect(parent, name);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GameFont.Asset;
            if (font != null)
            {
                t.font = font;
            }
            t.richText = true;
            t.raycastTarget = false;
            t.enableAutoSizing = false;
            t.margin = Vector4.zero;
            return t;
        }

        public static Image NewImage(Transform parent, string name)
        {
            RectTransform r = Rect(parent, name);
            var image = r.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
