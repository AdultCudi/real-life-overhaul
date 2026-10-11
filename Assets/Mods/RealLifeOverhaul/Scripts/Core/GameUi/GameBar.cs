using System;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// One of the game's own bars, for anything of the mod's that is a quantity or a timing: a copy of the energy bar
    /// of the game's top bar (Canvases/Topbar/Container/Left Bar/PlayerStats/Energy: the pill StatBarBackground at
    /// 30 % black with the same pill as a filled picture inside it), made while it sleeps (<see cref="UiClone"/>),
    /// with its button, its hover effect and its picture taken off. Nothing is drawn by the mod: the bar, the zone
    /// and the marker are all that one sprite of the game's.
    ///
    /// It shows one of two things:
    ///  - a quantity (<see cref="Fill"/>): the bar filled to a share, as the game's needs are (a clock running down);
    ///  - a timing (<see cref="Zone"/> and <see cref="Mark"/>): a zone marked on the empty bar in the game's green,
    ///    the better part of it in full green, and a marker that runs along the bar.
    ///
    /// The cost: nothing per frame of its own. <see cref="Fill"/> and <see cref="Mark"/> may be called every frame:
    /// each is two comparisons, and writes one number to one object only when the value has moved a visible step
    /// (<see cref="BarRule"/>, the game's own rule for its needs bars). No text and no layout is ever made again: a
    /// marker moving is one position written. The bar has a canvas of its own, so the panel it stands on is not
    /// drawn again for it.
    ///
    /// Keep the copy at the game's size (412 x 56): the sprite has no border, and a bar of another size is a
    /// squeezed pill. If the game's top bar is not built as expected, <see cref="Make"/> says so in one "Game UI:"
    /// line and returns null, and the owner does without a bar.
    /// </summary>
    public sealed class GameBar
    {
        private const float MarkWidth = 12f;     // units of the game's interface: a marker that is seen on a bar 412 wide
        private const float MarkOver = 8f;       // units the marker stands out above and below the bar
        private const byte ZoneAlpha = 115;      // of 255: the zone is the game's green let through at 45 %, its better part at full strength

        private readonly GameObject _root;
        private readonly RectTransform _rect;
        private readonly Image _fill;
        private readonly RectTransform _span;    // the filler's own rectangle: what "0 to 1" means on this bar
        private readonly RectTransform _zone, _best, _mark;
        private float _shown = -1f;              // the share last written to the filler (below zero: none)
        private float _markShown = -1f;
        private float _zoneFrom = -1f, _zoneTo = -1f, _bestFrom = -1f, _bestTo = -1f;
        private float _width = -1f;
        private bool _timing;
        private bool _on;

        private GameBar(GameObject root, Image fill, RectTransform span, RectTransform zone, RectTransform best, RectTransform mark)
        {
            _root = root;
            _rect = (RectTransform)root.transform;
            _fill = fill;
            _span = span;
            _zone = zone;
            _best = best;
            _mark = mark;
        }

        /// <summary>True while the copy exists (it goes with whatever it was hung on).</summary>
        public bool Alive => _root != null;

        /// <summary>The bar's own rectangle, for its owner to place: anchored and pivoted at its middle, the game's size.</summary>
        public RectTransform Rect => _rect;

        /// <summary>What the bar shows at this moment, 0 to 1: the marker's place while it is a timing, the share filled otherwise; below zero while nothing was written.</summary>
        public float Value => _timing ? _markShown : _shown;

        /// <summary>True while the bar shows a zone and a marker.</summary>
        public bool Timing => _timing;

        /// <summary>
        /// A bar hung on <paramref name="parent"/>, switched off, in the middle of it. Null, with one log line, if
        /// the game's top bar is not there to copy.
        /// </summary>
        public static GameBar Make(Transform parent, string name)
        {
            Transform holder = null;
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                Transform container = uis != null && uis.topBar != null && uis.topBar.container != null ? uis.topBar.container.transform : null;
                Transform stats = UiClone.Need(container, "Left Bar/PlayerStats", "a bar");
                if (parent == null || stats == null || stats.childCount < 1)
                {
                    UiClone.Fallback("the game's top bar is not built as expected, so '" + name + "' has no bar");
                    return null;
                }
                var source = (RectTransform)stats.GetChild(0);
                holder = UiClone.Dormant(parent, name + " (asleep)");
                GameObject bar = UiClone.Asleep(source.gameObject, holder, name);
                UiClone.StripGameScripts(bar, null);
                var button = bar.GetComponent<Button>();
                if (button != null)
                {
                    UnityEngine.Object.DestroyImmediate(button);
                }
                Image fill = bar.transform.childCount > 0 ? bar.transform.GetChild(0).GetComponent<Image>() : null;
                if (fill == null || fill.type != Image.Type.Filled)
                {
                    UiClone.Fallback("a bar of the game's top bar has no filled picture, so '" + name + "' has no bar");
                    UnityEngine.Object.DestroyImmediate(holder.gameObject);
                    return null;
                }
                for (int i = bar.transform.childCount - 1; i >= 1; i--)
                {
                    UnityEngine.Object.DestroyImmediate(bar.transform.GetChild(i).gameObject);   // the need's picture
                }

                var rect = (RectTransform)bar.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = source.rect.size;
                rect.anchoredPosition = Vector2.zero;

                // The filler's own rectangle, empty: the zone and the marker are placed in it by shares of its width.
                var fillRect = (RectTransform)fill.transform;
                var span = new GameObject("Span", typeof(RectTransform)).GetComponent<RectTransform>();
                span.SetParent(rect, false);
                span.anchorMin = fillRect.anchorMin;
                span.anchorMax = fillRect.anchorMax;
                span.pivot = fillRect.pivot;
                span.sizeDelta = fillRect.sizeDelta;
                span.anchoredPosition = fillRect.anchoredPosition;

                Color32 green = WorldPanel.Green();
                RectTransform zone = Piece(fill, span, "Zone", new Color32(green.r, green.g, green.b, ZoneAlpha));
                RectTransform best = Piece(fill, span, "Best", green);
                RectTransform mark = Piece(fill, span, "Marker", WorldPanel.White());
                mark.anchorMin = new Vector2(0f, 0f);
                mark.anchorMax = new Vector2(0f, 1f);
                mark.pivot = new Vector2(0.5f, 0.5f);
                mark.sizeDelta = new Vector2(MarkWidth, MarkOver * 2f);
                mark.anchoredPosition = Vector2.zero;

                // Looked at, never clicked: nothing of it may take the pointer from the world under it.
                foreach (Graphic graphic in bar.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
                bar.AddComponent<Canvas>();   // drawn by itself: a marker that moves does not have its panel drawn again
                var made = new GameBar(bar, fill, span, zone, best, mark);
                bar.SetActive(false);
                rect.SetParent(parent, false);
                UnityEngine.Object.Destroy(holder.gameObject);
                return made;
            }
            catch (Exception e)
            {
                UiClone.Logger?.Error(e);
                UiClone.Fallback("a bar could not be copied from the game's top bar, so '" + name + "' has none");
                if (holder != null)
                {
                    UnityEngine.Object.Destroy(holder.gameObject);
                }
                return null;
            }
        }

        // The filler's picture once more, whole, in a colour: a stretch of the bar, or the marker.
        private static RectTransform Piece(Image fill, RectTransform span, string name, Color32 colour)
        {
            GameObject piece = UnityEngine.Object.Instantiate(fill.gameObject, span, false);
            piece.name = name;
            var image = piece.GetComponent<Image>();
            image.type = Image.Type.Simple;
            image.color = colour;
            var rect = (RectTransform)piece.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            piece.SetActive(false);
            return rect;
        }

        /// <summary>Shows or hides the bar. One comparison when nothing changes.</summary>
        public void Show(bool on)
        {
            if (_root != null && on != _on)
            {
                _on = on;
                _root.SetActive(on);
            }
        }

        /// <summary>
        /// The bar as a quantity: filled to <paramref name="value"/> (0 to 1). Written only when it has moved a
        /// visible step; fit for every frame.
        /// </summary>
        public void Fill(float value)
        {
            if (_root == null)
            {
                return;
            }
            if (_timing || _shown < 0f)
            {
                _timing = false;
                _markShown = -1f;
                _zoneFrom = _zoneTo = _bestFrom = _bestTo = -1f;
                Switch(_zone, false);
                Switch(_best, false);
                Switch(_mark, false);
                Switch(_fill.rectTransform, true);
                _shown = -1f;
            }
            if (BarRule.Due(_shown, value))
            {
                _shown = BarRule.Clamp(value);
                _fill.fillAmount = _shown;
            }
        }

        /// <summary>
        /// The bar as a timing: a zone about <paramref name="centre"/>, <paramref name="width"/> wide (shares of the
        /// bar), and inside it a better part <paramref name="bestWidth"/> wide (0: none). Written only when a figure
        /// differs from what is marked.
        /// </summary>
        public void Zone(float centre, float width, float bestWidth)
        {
            if (_root == null)
            {
                return;
            }
            if (!_timing)
            {
                _timing = true;
                _shown = -1f;
                _markShown = -1f;
                Switch(_fill.rectTransform, false);
            }
            bool zone = BarRule.Span(centre, width, out float from, out float to);
            bool best = BarRule.Span(centre, bestWidth, out float bestFrom, out float bestTo);
            if (!zone)
            {
                from = to = -1f;
            }
            if (!best)
            {
                bestFrom = bestTo = -1f;
            }
            if (from != _zoneFrom || to != _zoneTo)
            {
                _zoneFrom = from;
                _zoneTo = to;
                Stretch(_zone, zone, from, to);
            }
            if (bestFrom != _bestFrom || bestTo != _bestTo)
            {
                _bestFrom = bestFrom;
                _bestTo = bestTo;
                Stretch(_best, best, bestFrom, bestTo);
            }
        }

        /// <summary>
        /// The marker at <paramref name="value"/> (0 to 1) along the bar. Written (one position) only when it has
        /// moved a visible step; fit for every frame. Call <see cref="Zone"/> first.
        /// </summary>
        public void Mark(float value)
        {
            if (_root == null || !_timing || !BarRule.Due(_markShown, value))
            {
                return;
            }
            if (_width < 0f)
            {
                _width = _span.rect.width;
            }
            bool first = _markShown < 0f;
            _markShown = BarRule.Clamp(value);
            _mark.anchoredPosition = new Vector2(_markShown * _width, 0f);
            if (first)
            {
                Switch(_mark, true);
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
        }

        private static void Stretch(RectTransform piece, bool on, float from, float to)
        {
            if (on)
            {
                piece.anchorMin = new Vector2(from, 0f);
                piece.anchorMax = new Vector2(to, 1f);
                piece.offsetMin = piece.offsetMax = Vector2.zero;
            }
            Switch(piece, on);
        }

        private static void Switch(RectTransform piece, bool on)
        {
            if (piece.gameObject.activeSelf != on)
            {
                piece.gameObject.SetActive(on);
            }
        }
    }
}
