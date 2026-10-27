using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// A drawing surface of the Economy app: cards with a shadow and an outline, rounded pills, lettering, pictures made of one
    /// mesh each, and places to click, laid at positions the app works out. Every part is made once and used again: a page
    /// is written by asking for the next part of each kind in the order they are to be drawn, and the parts not asked for are
    /// hidden. The layers keep their order, shadows under shapes under pictures under lettering under clicks, so a card
    /// never covers its own words. It may scroll. Nothing runs per frame: a page is written when the app asks.
    /// </summary>
    internal sealed class OutlookPage
    {
        // The parts of a kind, handed out in order and used again.
        private sealed class Pool<T> where T : Component
        {
            private readonly List<T> _items = new List<T>(32);
            private readonly Func<T> _make;
            private int _used;

            public Pool(Func<T> make)
            {
                _make = make;
            }

            public int Used => _used;
            public int Made => _items.Count;
            public IReadOnlyList<T> Items => _items;

            public void Begin()
            {
                _used = 0;
            }

            public T Next()
            {
                if (_used == _items.Count)
                {
                    _items.Add(_make());
                }
                T item = _items[_used++];
                OutlookKit.Show(item, true);
                return item;
            }

            public void End()
            {
                for (int i = _used; i < _items.Count; i++)
                {
                    OutlookKit.Show(_items[i], false);
                }
            }
        }

        private readonly RectTransform _viewport, _content;
        private readonly ScrollRect _scroll;
        private readonly Pool<Image> _shadows, _shapes, _hits;
        private readonly Pool<OutlookMesh> _marks;
        private readonly Pool<TextMeshProUGUI> _words;
        private readonly TextMeshProUGUI _ruler;
        private readonly List<TMP_Text> _everyText = new List<TMP_Text>(128);
        private TMP_FontAsset _font;
        private OutlookPalette _palette = OutlookTheme.Dark;

        /// <summary>The surface's own rect, which the app places.</summary>
        public RectTransform Root { get; }

        public OutlookPalette Palette => _palette;

        /// <summary>The width of what is drawn on, canvas units.</summary>
        public float Width => _viewport.rect.width;

        /// <summary>The height of the window the page shows through.</summary>
        public float Height => _viewport.rect.height;

        public bool Scrolls => _scroll != null;

        /// <summary>Parts asked for in the last write, for the tests: a tab that drew has some of each.</summary>
        public int CardsDrawn { get; private set; }
        public int WordsDrawn => _words.Used;
        public int PicturesDrawn => _marks.Used;
        public int ClicksDrawn => _hits.Used;

        public OutlookPage(Transform parent, string name, bool scrolls)
        {
            Root = OutlookKit.Rect(parent, name);
            if (scrolls)
            {
                var back = Root.gameObject.AddComponent<Image>();   // takes the wheel and the drag over empty space
                back.color = new Color(0f, 0f, 0f, 0f);
                back.raycastTarget = true;
                _scroll = Root.gameObject.AddComponent<ScrollRect>();
                _viewport = OutlookKit.Rect(Root, "Viewport");
                OutlookKit.Stretch(_viewport, 0f, 0f, 0f, 0f);
                _viewport.gameObject.AddComponent<RectMask2D>();
                _content = OutlookKit.Rect(_viewport, "Content");
                _content.anchorMin = new Vector2(0f, 1f);
                _content.anchorMax = new Vector2(1f, 1f);
                _content.pivot = new Vector2(0f, 1f);
                _content.offsetMin = new Vector2(0f, 0f);
                _content.offsetMax = new Vector2(0f, 0f);
                _scroll.viewport = _viewport;
                _scroll.content = _content;
                _scroll.horizontal = false;
                _scroll.vertical = true;
                _scroll.movementType = ScrollRect.MovementType.Clamped;
                _scroll.inertia = false;
                _scroll.scrollSensitivity = 60f;   // the game's tables use 50 on this canvas
            }
            else
            {
                _viewport = Root;
                _content = OutlookKit.Rect(Root, "Content");
                OutlookKit.Stretch(_content, 0f, 0f, 0f, 0f);
            }
            RectTransform shadowLayer = Layer("Shadows"), shapeLayer = Layer("Shapes"), markLayer = Layer("Pictures"), wordLayer = Layer("Words"), hitLayer = Layer("Clicks");
            _shadows = new Pool<Image>(() => Made(OutlookKit.NewImage(shadowLayer, "Shadow"), OutlookKit.Soft));
            _shapes = new Pool<Image>(() => Made(OutlookKit.NewImage(shapeLayer, "Shape"), OutlookKit.Round));
            _marks = new Pool<OutlookMesh>(() => NewMesh(markLayer));
            _words = new Pool<TextMeshProUGUI>(() => NewWord(wordLayer));
            _hits = new Pool<Image>(() => NewHit(hitLayer));
            _ruler = OutlookKit.NewText(wordLayer, "Ruler");
            _ruler.text = string.Empty;
            _ruler.color = new Color(0f, 0f, 0f, 0f);
            _everyText.Add(_ruler);
            OutlookKit.Place(_ruler.rectTransform, 0f, 0f, 10f, 10f);
        }

        /// <summary>Gives the page a slim scroll bar down its right edge.</summary>
        public void AddScrollBar()
        {
            if (_scroll == null)
            {
                return;
            }
            RectTransform bar = OutlookKit.Rect(Root, "Scrollbar");
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 1f);
            bar.offsetMin = new Vector2(-18f, 8f);
            bar.offsetMax = new Vector2(-6f, -8f);
            RectTransform track = OutlookKit.Rect(bar, "Track");
            OutlookKit.Stretch(track, 0f, 0f, 0f, 0f);
            RectTransform handle = OutlookKit.Rect(track, "Handle");
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            Image grip = handle.gameObject.AddComponent<Image>();
            grip.sprite = OutlookKit.Circle;
            grip.type = Image.Type.Sliced;
            grip.color = OutlookKit.C(_palette.Border);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = grip;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            _grip = grip;
        }

        private Image _grip;

        private RectTransform Layer(string name)
        {
            RectTransform layer = OutlookKit.Rect(_content, name);
            OutlookKit.Stretch(layer, 0f, 0f, 0f, 0f);
            return layer;
        }

        private static Image Made(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            return image;
        }

        private static OutlookMesh NewMesh(Transform layer)
        {
            RectTransform r = OutlookKit.Rect(layer, "Picture");
            var mesh = r.gameObject.AddComponent<OutlookMesh>();
            mesh.raycastTarget = false;
            return mesh;
        }

        private TextMeshProUGUI NewWord(Transform layer)
        {
            TextMeshProUGUI t = OutlookKit.NewText(layer, "Words");
            if (_font != null)
            {
                t.font = _font;
            }
            _everyText.Add(t);
            return t;
        }

        private static Image NewHit(Transform layer)
        {
            Image image = OutlookKit.NewImage(layer, "Click");
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            return image;
        }

        // ------------------------------------------------------------------ writing a page

        /// <summary>Begins a page: every part is free to be asked for again, in the look given; the font is looked for if there was none.</summary>
        public void Begin(OutlookPalette palette)
        {
            _palette = palette ?? OutlookTheme.Dark;
            if (_font == null)
            {
                _font = GameFont.Asset;
                if (_font != null)
                {
                    for (int i = 0; i < _everyText.Count; i++)
                    {
                        _everyText[i].font = _font;
                    }
                }
            }
            _shadows.Begin();
            _shapes.Begin();
            _marks.Begin();
            _words.Begin();
            _hits.Begin();
            CardsDrawn = 0;
            if (_grip != null)
            {
                _grip.color = OutlookKit.C(_palette.Border);
            }
        }

        /// <summary>Ends a page: the parts not used are hidden, and the page is as tall as what was drawn on it.</summary>
        public void End(float height)
        {
            _shadows.End();
            _shapes.End();
            _marks.End();
            _words.End();
            _hits.End();
            if (_scroll != null)
            {
                _content.sizeDelta = new Vector2(0f, Mathf.Max(height, _viewport.rect.height));
            }
        }

        /// <summary>Back to the top, as a new tab starts.</summary>
        public void ScrollToTop()
        {
            if (_scroll != null)
            {
                _scroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>How far down the page is scrolled, canvas units.</summary>
        public float Scrolled => _scroll != null ? Mathf.Max(0f, _content.anchoredPosition.y) : 0f;

        // ------------------------------------------------------------------ parts

        /// <summary>A card: a rounded box in a surface colour with a one-pixel outline and, in a look that has them, a soft shadow.</summary>
        public void Card(float x, float y, float w, float h, int fill, float radius = 36f, bool outline = true, bool shadow = true)
        {
            if (shadow && _palette.ShadowAlpha > 0.0)
            {
                Image s = _shadows.Next();
                float reach = OutlookKit.Reach;
                OutlookKit.Place(s.rectTransform, x - reach, y - reach + 10f, w + reach * 2f, h + reach * 2f);
                s.color = new Color(0f, 0f, 0f, (float)_palette.ShadowAlpha);
                s.pixelsPerUnitMultiplier = 1f;
            }
            if (outline)
            {
                Image b = _shapes.Next();
                OutlookKit.Place(b.rectTransform, x, y, w, h);
                b.sprite = OutlookKit.Round;
                b.color = OutlookKit.C(_palette.Border);
                b.pixelsPerUnitMultiplier = OutlookKit.Corner / radius;
                float edge = 1.6f;
                Image f = _shapes.Next();
                OutlookKit.Place(f.rectTransform, x + edge, y + edge, w - edge * 2f, h - edge * 2f);
                f.sprite = OutlookKit.Round;
                f.color = OutlookKit.C(fill);
                f.pixelsPerUnitMultiplier = OutlookKit.Corner / Mathf.Max(1f, radius - edge);
            }
            else
            {
                Image f = _shapes.Next();
                OutlookKit.Place(f.rectTransform, x, y, w, h);
                f.sprite = OutlookKit.Round;
                f.color = OutlookKit.C(fill);
                f.pixelsPerUnitMultiplier = OutlookKit.Corner / radius;
            }
            CardsDrawn++;
        }

        /// <summary>A pill (or a round dot, when as wide as high) in a colour.</summary>
        public void Pill(float x, float y, float w, float h, int fill, float alpha = 1f)
        {
            Image p = _shapes.Next();
            OutlookKit.Place(p.rectTransform, x, y, w, h);
            p.sprite = OutlookKit.Circle;
            p.color = OutlookKit.C(fill, alpha);
            p.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>A rounded box in a colour, no outline.</summary>
        public void Box(float x, float y, float w, float h, int fill, float radius, float alpha = 1f)
        {
            Image f = _shapes.Next();
            OutlookKit.Place(f.rectTransform, x, y, w, h);
            f.sprite = OutlookKit.Round;
            f.color = OutlookKit.C(fill, alpha);
            f.pixelsPerUnitMultiplier = OutlookKit.Corner / Mathf.Max(1f, radius);
        }

        /// <summary>Lettering in a box. A wrapped text may be as high as the box; one line is cut with dots.</summary>
        public TextMeshProUGUI Text(string text, float x, float y, float w, float h, float size, int colour, TextAlignmentOptions align = TextAlignmentOptions.TopLeft,
            bool bold = false, bool wrap = false, bool caps = false, float spacing = 0f)
        {
            TextMeshProUGUI t = _words.Next();
            Style(t, size, colour, align, bold, wrap, caps, spacing);
            OutlookKit.Set(t, text);
            OutlookKit.Place(t.rectTransform, x, y, w, h);
            return t;
        }

        private static void Style(TMP_Text t, float size, int colour, TextAlignmentOptions align, bool bold, bool wrap, bool caps, float spacing)
        {
            t.fontSize = size;
            t.fontStyle = (bold ? FontStyles.Bold : FontStyles.Normal) | (caps ? FontStyles.UpperCase : FontStyles.Normal);
            t.color = OutlookKit.C(colour);
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            t.characterSpacing = spacing;
        }

        /// <summary>The width a line of lettering takes.</summary>
        public float Measure(string text, float size, bool bold = false, float spacing = 0f)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }
            Style(_ruler, size, _palette.Text, TextAlignmentOptions.TopLeft, bold, false, false, spacing);
            return _ruler.GetPreferredValues(text).x;
        }

        /// <summary>The height a wrapped text takes at a width.</summary>
        public float Fit(string text, float size, float width, bool bold = false)
        {
            if (string.IsNullOrEmpty(text) || width < 4f)
            {
                return 0f;
            }
            Style(_ruler, size, _palette.Text, TextAlignmentOptions.TopLeft, bold, true, false, 0f);
            return _ruler.GetPreferredValues(text, width, 0f).y;
        }

        /// <summary>A picture: one mesh in a box, ready to be filled (<see cref="OutlookMesh.Begin"/> already called).</summary>
        public OutlookMesh Picture(float x, float y, float w, float h)
        {
            OutlookMesh mesh = _marks.Next();
            OutlookKit.Place(mesh.rectTransform, x, y, w, h);
            mesh.color = Color.white;
            mesh.Begin();
            return mesh;
        }

        /// <summary>A place to click over a box: it runs <paramref name="click"/> inside the guard.</summary>
        public void Click(float x, float y, float w, float h, Action click)
        {
            Image hit = _hits.Next();
            OutlookKit.Place(hit.rectTransform, x, y, w, h);
            Button button = hit.GetComponent<Button>();
            UiClone.Rewire(button, click);
        }

        /// <summary>The next picture's mesh and the number of meshes made, for the tests.</summary>
        public IReadOnlyList<OutlookMesh> Pictures => _marks.Items;
    }
}
