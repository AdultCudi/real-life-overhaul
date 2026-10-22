using System;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// One of the game's own bars for a quantity (<see cref="Fill"/>) or a timing (<see cref="Zone"/>, <see cref="Mark"/>): a
    /// copy of the top bar's energy bar, with a canvas of its own. Fill and Mark may be called every frame: two comparisons,
    /// and a write only for a visible step (<see cref="BarRule"/>). Keep the game's size (412 x 56): the sprite has no
    /// border. <see cref="Make"/> returns null, with one log line, without the game's top bar.
    /// </summary>
    public sealed class GameBar
    {
        private const float MarkWidth = 12f;     // units of the game's interface: a marker seen on a bar 412 wide
        private const float MarkOver = 8f;       // units the marker stands out above and below the bar
        private const byte ZoneAlpha = 115;      // of 255: the zone lets the game's green through at 45 %, its better part at full strength

        private readonly GameObject _root;
        private readonly RectTransform _rect;
        private readonly Image _fill;
        private readonly RectTransform _zone, _best, _mark;
        private float _shown = -1f;              // the share last written to the filler; below zero for none
        private float _markShown = -1f;
        private float _zoneFrom = -1f, _zoneTo = -1f, _bestFrom = -1f, _bestTo = -1f;
        private bool _timing;
        private bool _on;

        private GameBar(GameObject root, Image fill, RectTransform zone, RectTransform best, RectTransform mark)
        {
            _root = root;
            _rect = (RectTransform)root.transform;
            _fill = fill;
            _zone = zone;
            _best = best;
            _mark = mark;
        }

        /// <summary>True while the copy exists; it goes with whatever it was hung on.</summary>
        public bool Alive => _root != null;

        /// <summary>The bar's rectangle for its owner to place: anchored and pivoted at its middle, the game's size.</summary>
        public RectTransform Rect => _rect;

        /// <summary>What the bar shows now, 0 to 1 (the marker while a timing, else the fill); below zero while nothing was written.</summary>
        public float Value => _timing ? _markShown : _shown;

        public bool Timing => _timing;

        /// <summary>A bar hung on <paramref name="parent"/>, off, in its middle. Null, with one log line, without the game's top bar to copy.</summary>
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

                // The filler's rectangle, empty: the zone and the marker are placed in it by shares of its width.
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

                // Looked at, never clicked: nothing of it takes the pointer from the world under it.
                foreach (Graphic graphic in bar.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
                bar.AddComponent<Canvas>();   // drawn by itself: a moving marker does not redraw its panel
                var made = new GameBar(bar, fill, zone, best, mark);
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

        // The filler's picture again, whole, in a colour: a stretch of the bar, or the marker.
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

        /// <summary>The bar as a quantity, filled to <paramref name="value"/> (0 to 1); written only when it moved a visible step.</summary>
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

        /// <summary>The bar as a timing: a zone about <paramref name="centre"/> with a better part inside (shares of the bar); written only when a figure differs.</summary>
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

        /// <summary>The marker at <paramref name="value"/> (0 to 1), anchored at that share of the bar; written only when it moved a visible step. Call <see cref="Zone"/> first.</summary>
        public void Mark(float value)
        {
            if (_root == null || !_timing || !BarRule.Due(_markShown, value))
            {
                return;
            }
            bool first = _markShown < 0f;
            _markShown = BarRule.Clamp(value);
            // Anchored at its share of the span, as the zone is: it stays in place at any screen size or interface scale.
            _mark.anchorMin = new Vector2(_markShown, 0f);
            _mark.anchorMax = new Vector2(_markShown, 1f);
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
