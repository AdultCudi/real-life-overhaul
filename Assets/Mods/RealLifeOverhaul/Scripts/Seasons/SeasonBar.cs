using System;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// The season and its day under the game's date on the top bar: a copy of the date's own label (its font, size and
    /// colour), smaller, with the season's picture before it, so it goes wherever the top bar goes. The director calls
    /// <see cref="Show"/> once a second; the words are written only when the day changes. A fault removes it for the session.
    /// </summary>
    public static class SeasonBar
    {
        private const float Below = 4f;          // from the date's lower edge
        private const float Height = 34f;
        private const float Width = 420f;
        private const float Picture = 28f;
        private const float Shrink = 0.8f;       // of the date's own size

        private static GameObject _piece;
        private static Component _words;
        private static Image _picture;
        private static string _shown;
        private static int _season = -1;
        private static bool _failed;

        public static bool Shown => _piece != null && _piece.activeSelf;
        public static string Line => _shown;

        /// <summary>Shows <paramref name="line"/> with <paramref name="season"/>'s picture; pass the same string object while nothing changes.</summary>
        public static void Show(string line, int season)
        {
            if (_failed)
            {
                return;
            }
            try
            {
                if (_piece == null && !Build())
                {
                    _failed = true;
                    return;
                }
                if (!ReferenceEquals(line, _shown))
                {
                    _shown = line;
                    UiClone.Words(_words, line);
                }
                if (season != _season)
                {
                    _season = season;
                    Sprite sprite = SeasonIcons.Of(season);
                    _picture.sprite = sprite;
                    _picture.enabled = sprite != null;
                }
                if (!_piece.activeSelf)
                {
                    _piece.SetActive(true);
                }
            }
            catch (Exception e)
            {
                SeasonDirector.Logger?.Error(e);
                Remove();
                _failed = true;
            }
        }

        public static void Hide()
        {
            if (_piece != null && _piece.activeSelf)
            {
                _piece.SetActive(false);
            }
        }

        /// <summary>The city is unloading: the piece goes with the top bar's copy of it.</summary>
        public static void Remove()
        {
            if (_piece != null)
            {
                UnityEngine.Object.Destroy(_piece);
            }
            _piece = null;
            _words = null;
            _picture = null;
            SeasonIcons.Drop();
            _shown = null;
            _season = -1;
            _failed = false;
        }

        private static bool Build()
        {
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            Transform container = uis != null && uis.topBar != null && uis.topBar.container != null ? uis.topBar.container.transform : null;
            Transform bar = UiClone.Need(container, "Left Bar", "the season under the date");
            Transform date = UiClone.Need(bar, "Date/Label", "the season under the date");
            var dateText = date != null ? date.GetComponent<TMP_Text>() : null;
            if (dateText == null)
            {
                UiClone.Fallback("the game's top bar has no date label as expected, so the season is not shown under it (the Calendar still says it)");
                return false;
            }
            Transform holder = UiClone.Dormant(bar, "RealLifeOverhaul Season Holder");
            try
            {
                var piece = new GameObject("RealLifeOverhaul Season", typeof(RectTransform));
                piece.SetActive(false);
                piece.AddComponent<LayoutElement>().ignoreLayout = true;   // the date's own row keeps its layout
                var rect = (RectTransform)piece.transform;
                rect.SetParent(date, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(Width, Height);
                rect.anchoredPosition = new Vector2(0f, -Below);

                var pictureObject = new GameObject("Picture", typeof(RectTransform));
                var pictureRect = (RectTransform)pictureObject.transform;
                pictureRect.SetParent(rect, false);
                pictureRect.anchorMin = pictureRect.anchorMax = new Vector2(0f, 0.5f);
                pictureRect.pivot = new Vector2(0f, 0.5f);
                pictureRect.sizeDelta = new Vector2(Picture, Picture);
                pictureRect.anchoredPosition = Vector2.zero;
                _picture = pictureObject.AddComponent<Image>();
                _picture.raycastTarget = false;
                _picture.preserveAspect = true;
                _picture.color = dateText.color;

                GameObject words = UiClone.Asleep(date.gameObject, holder, "Words");
                for (int i = words.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(words.transform.GetChild(i).gameObject);
                }
                UiClone.StripGameScripts(words, null);
                var wordsRect = (RectTransform)words.transform;
                wordsRect.SetParent(rect, false);
                wordsRect.anchorMin = Vector2.zero;
                wordsRect.anchorMax = Vector2.one;
                wordsRect.pivot = new Vector2(0f, 0.5f);
                wordsRect.offsetMin = new Vector2(Picture + 8f, 0f);
                wordsRect.offsetMax = Vector2.zero;
                var text = words.GetComponent<TMP_Text>();
                text.fontSize = dateText.fontSize * Shrink;
                text.enableAutoSizing = false;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.raycastTarget = false;
                words.SetActive(true);
                _words = text;
                _piece = piece;
                UnityEngine.Object.DestroyImmediate(holder.gameObject);
                return true;
            }
            catch (Exception e)
            {
                SeasonDirector.Logger?.Error(e);
                if (holder != null)
                {
                    UnityEngine.Object.DestroyImmediate(holder.gameObject);
                }
                UiClone.Fallback("the season could not be put under the game's date");
                return false;
            }
        }
    }

    /// <summary>
    /// The seasons' four pictures, drawn once in code in the game's flat style (white shapes on nothing, tinted by the
    /// date's colour): a snowflake, a flower, a sun and a leaf. 64 pixels a side, made the first time each is asked for
    /// in a city and destroyed with it.
    /// </summary>
    internal static class SeasonIcons
    {
        private const int Size = 64;
        private static readonly Sprite[] Made = new Sprite[4];

        public static Sprite Of(int season)
        {
            if (season < 0 || season > 3)
            {
                return null;
            }
            if (Made[season] == null)
            {
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "RealLifeOverhaul Season " + season, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + 0.5f) / Size * 2f - 1f, v = (y + 0.5f) / Size * 2f - 1f;
                        float d = Distance(season, u, v);
                        byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(0.5f - d * Size * 0.5f));   // a pixel's worth of soft edge
                        pixels[y * Size + x] = new Color32(255, 255, 255, a);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
                Made[season] = sprite;
            }
            return Made[season];
        }

        /// <summary>The pictures and their textures destroyed: the city is unloading.</summary>
        public static void Drop()
        {
            for (int i = 0; i < Made.Length; i++)
            {
                Sprite sprite = Made[i];
                Made[i] = null;
                CodeAssets.DropSprite(sprite);
            }
        }

        // How far a point (-1 to 1 each way) is outside the season's shape; under 0 inside.
        private static float Distance(int season, float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            switch (season)
            {
                case CalendarRules.Winter:
                {
                    // Six arms with a short branch each, and a small hub.
                    float arm = float.MaxValue;
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * Mathf.PI / 3f;
                        arm = Mathf.Min(arm, Segment(u, v, 0f, 0f, 0.82f * Mathf.Cos(a), 0.82f * Mathf.Sin(a)) - 0.09f);
                        float bx = 0.5f * Mathf.Cos(a), by = 0.5f * Mathf.Sin(a);
                        arm = Mathf.Min(arm, Segment(u, v, bx, by, bx + 0.25f * Mathf.Cos(a + 0.9f), by + 0.25f * Mathf.Sin(a + 0.9f)) - 0.07f);
                        arm = Mathf.Min(arm, Segment(u, v, bx, by, bx + 0.25f * Mathf.Cos(a - 0.9f), by + 0.25f * Mathf.Sin(a - 0.9f)) - 0.07f);
                    }
                    return Mathf.Min(arm, r - 0.18f);
                }
                case CalendarRules.Spring:
                {
                    // Five round petals about a hole.
                    float petals = float.MaxValue;
                    for (int k = 0; k < 5; k++)
                    {
                        float a = Mathf.PI / 2f + k * 2f * Mathf.PI / 5f;
                        float px = u - 0.45f * Mathf.Cos(a), py = v - 0.45f * Mathf.Sin(a);
                        petals = Mathf.Min(petals, Mathf.Sqrt(px * px + py * py) - 0.36f);
                    }
                    return Mathf.Max(petals, 0.15f - r);
                }
                case CalendarRules.Summer:
                {
                    // A disc and eight rays.
                    float rays = float.MaxValue;
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        rays = Mathf.Min(rays, Segment(u, v, 0.62f * Mathf.Cos(a), 0.62f * Mathf.Sin(a), 0.9f * Mathf.Cos(a), 0.9f * Mathf.Sin(a)) - 0.08f);
                    }
                    return Mathf.Min(rays, r - 0.42f);
                }
                default:
                {
                    // A leaf: two arcs meeting at its tips (on the x axis), on a slant, with a stem and a vein.
                    float c = Mathf.Cos(0.785f), s = Mathf.Sin(0.785f);
                    float lx = (u * c + v * s) / 0.85f, ly = (-u * s + v * c) / 0.85f;
                    float leaf = Mathf.Max(Circle(lx, ly, 0f, -0.42f, 0.92f), Circle(lx, ly, 0f, 0.42f, 0.92f));
                    float stem = Segment(lx, ly, -0.78f, 0f, -1.05f, 0f) - 0.07f;
                    float vein = 0.04f - Segment(lx, ly, -0.6f, 0f, 0.55f, 0f);
                    return Mathf.Min(Mathf.Max(leaf, vein), stem) * 0.85f;
                }
            }
        }

        private static float Circle(float x, float y, float cx, float cy, float radius)
        {
            float dx = x - cx, dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        }

        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float px = x - ax, py = y - ay, qx = bx - ax, qy = by - ay;
            float t = Mathf.Clamp01((px * qx + py * qy) / Mathf.Max(0.0001f, qx * qx + qy * qy));
            float dx = px - qx * t, dy = py - qy * t;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
