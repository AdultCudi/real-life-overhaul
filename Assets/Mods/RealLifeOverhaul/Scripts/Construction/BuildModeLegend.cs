using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode's legend for the view shown, a small box of the designer's standing over the views at the foot (the owner, 26 Oct
    /// 2026: the eleven entries in every tool panel "dominate" it): the view's first entries (BuildModeRules.LegendShown, six at
    /// most), two to a row, each a square of the fill inside its edge's colour and its words; the rest of the view's entries (its
    /// lines) in the game's tooltip on the box. Parts kept and reused; written only when the view changes; nothing a frame.
    /// </summary>
    internal sealed class BuildModeLegend
    {
        private const float KeySize = 28f;     // a square, in the canvas's units (3840 by 2160)
        private const float WordsSize = 26f;
        private const float Column = 300f;     // an entry's width: two to a row

        private sealed class Key
        {
            public RectTransform Root;
            public Image Edge, Inside;
            public TMP_Text Name;
        }

        private readonly BuildModeKit _kit;
        private readonly RectTransform _rect;
        private readonly BuildModeTip _tip;
        private readonly List<Key> _keys = new List<Key>(BuildModeRules.LegendShort);
        private readonly List<RectTransform> _rows = new List<RectTransform>(3);
        private int _used;

        public RectTransform Rect => _rect;

        /// <summary>Entries shown now, for the tests.</summary>
        public int Shown => _used;

        public BuildModeLegend(BuildModeKit kit, Transform parent)
        {
            _kit = kit;
            _rect = kit.Panel(parent, "Legend", false, new RectOffset(28, 28, 18, 18), 10f);
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0f);
            _rect.pivot = Vector2.zero;
            var catcher = _rect.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);   // only so the pointer finds the box for its tooltip: it is looked at, never clicked
            _tip = _rect.gameObject.AddComponent<BuildModeTip>();
        }

        /// <summary>Starts the legend afresh.</summary>
        public void Begin() => _used = 0;

        /// <summary>An entry: a square of the fill inside its edge's colour (an edge alone: the box's own dark inside), and its words.</summary>
        public void Add(Color fill, Color edge, bool edgeOnly, string text)
        {
            if (_used >= BuildModeRules.LegendShort)
            {
                return;
            }
            Key k;
            if (_used < _keys.Count)
            {
                k = _keys[_used];
            }
            else
            {
                k = new Key { Root = BuildModeKit.Strip(_rect, "Key", true, new RectOffset(), 12f, TextAnchor.MiddleLeft) };
                BuildModeKit.Size(k.Root.gameObject, Column, -1f);
                Sprite box = _kit.White ?? _kit.Line;
                k.Edge = BuildModeKit.Picture(k.Root, "Edge", box, Color.white, KeySize, KeySize);
                k.Edge.pixelsPerUnitMultiplier = 3f;   // the box's rounding kept small on a small square
                k.Inside = BuildModeKit.Picture(k.Edge.transform, "Inside", box, Color.white, KeySize - 10f, KeySize - 10f);
                k.Inside.pixelsPerUnitMultiplier = 3f;
                var inside = (RectTransform)k.Inside.transform;
                inside.anchorMin = inside.anchorMax = inside.pivot = new Vector2(0.5f, 0.5f);
                inside.anchoredPosition = Vector2.zero;
                k.Inside.GetComponent<LayoutElement>().ignoreLayout = true;
                k.Name = _kit.Text(k.Root, "Name", WordsSize, BuildModeBar.Soft, false, true);
                k.Name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                _keys.Add(k);
            }
            int row = _used / 2;
            while (row >= _rows.Count)
            {
                RectTransform r = BuildModeKit.Strip(_rect, "Legend row", true, new RectOffset(), 20f, TextAnchor.MiddleLeft);
                ((HorizontalLayoutGroup)r.GetComponent<HorizontalLayoutGroup>()).childForceExpandWidth = false;
                _rows.Add(r);
            }
            if (!_rows[row].gameObject.activeSelf)
            {
                _rows[row].gameObject.SetActive(true);
            }
            k.Root.SetParent(_rows[row], false);
            k.Root.SetAsLastSibling();
            if (!k.Root.gameObject.activeSelf)
            {
                k.Root.gameObject.SetActive(true);
            }
            k.Edge.color = new Color(edge.r, edge.g, edge.b, 1f);
            k.Inside.color = edgeOnly ? new Color(0.12f, 0.13f, 0.15f, 1f) : new Color(fill.r, fill.g, fill.b, 1f);
            UiClone.Words(k.Name, text);
            _used++;
        }

        /// <summary>Ends the legend: the entries not written hidden, the rest of the view's entries in its tooltip, laid out at once.</summary>
        public void End(string title, string more)
        {
            for (int i = _used; i < _keys.Count; i++)
            {
                if (_keys[i].Root.gameObject.activeSelf)
                {
                    _keys[i].Root.gameObject.SetActive(false);
                }
            }
            for (int r = (_used + 1) / 2; r < _rows.Count; r++)
            {
                if (_rows[r].gameObject.activeSelf)
                {
                    _rows[r].gameObject.SetActive(false);
                }
            }
            _tip.Set(title, more, false);
            bool any = _used > 0;
            if (_rect.gameObject.activeSelf != any)
            {
                _rect.gameObject.SetActive(any);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
        }

        /// <summary>Stands the legend with its foot left corner at a point over the canvas's foot middle.</summary>
        public void Stand(Vector2 at) => _rect.anchoredPosition = at;

        /// <summary>The squares as drawn: each seen, of a picture with nothing see-through round it; null when so (the tests).</summary>
        public string Check()
        {
            if (_used > BuildModeRules.LegendShort)
            {
                return "the legend shows " + _used + " entries, more than " + BuildModeRules.LegendShort;
            }
            for (int i = 0; i < _used; i++)
            {
                Key k = _keys[i];
                if (!k.Edge.gameObject.activeInHierarchy || BuildModeKit.Padded(k.Edge.sprite) || k.Edge.rectTransform.rect.width < KeySize - 1f)
                {
                    return "the legend's square for '" + k.Name.text + "' is not drawn whole";
                }
            }
            return null;
        }
    }
}
