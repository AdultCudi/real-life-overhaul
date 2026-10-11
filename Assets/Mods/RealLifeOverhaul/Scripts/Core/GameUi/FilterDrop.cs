using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A filter over a table: the game's own drop-down (UI.Elements.Dropdown), whole and working, as EconoView has
    /// one over its list of transactions ("All types" first, then each type) and MyEmployees one over its table
    /// (the "mass action" box, Right/MassActionDropdown, which is the object copied). Nothing of it is the mod's:
    /// the box, the list that drops down, its scroller, typing in the box to find a choice, and closing it by a click
    /// elsewhere (GameManager asks Dropdown.currentDropdown every click) are the game's scripts, left on the copy.
    ///
    /// The copy is made when a table first needs it, under the app's panel while that is showing, so the drop-down
    /// wakes up before it is given its choices. Everything that touches the game's class is in this file: a caller
    /// that gets an exception from it (a game update changed the class) drops the filter and says so once.
    /// </summary>
    public sealed class FilterDrop
    {
        private const float Width = 640f, Height = 80f, Gap = 20f;
        // Where MyEmployees has the box: level with the tabs, at the right. Tables of the mod have up to seven tabs
        // there, so a filter stands one row higher, over the tabs, in the band the game leaves empty.
        private static readonly Vector2 First = new Vector2(-127.5f, -134f);

        private readonly global::UI.Elements.Dropdown _dropdown;
        private readonly List<string> _given = new List<string>(16);
        private Action<int> _picked;

        public GameObject Object => _dropdown != null ? _dropdown.gameObject : null;

        private FilterDrop(global::UI.Elements.Dropdown dropdown)
        {
            _dropdown = dropdown;
            dropdown.onOptionSelected.AddListener(option => UiClone.Safe(() =>
            {
                if (_picked != null)
                {
                    _picked(option);
                }
            }));
        }

        /// <summary>Whether an object is the game's drop-down.</summary>
        public static bool Is(GameObject candidate)
        {
            return candidate != null && candidate.GetComponent<global::UI.Elements.Dropdown>() != null;
        }

        /// <summary>A copy of the template at a place over the table (0: the right-hand one, 1: left of it). Null if it is no drop-down.</summary>
        public static FilterDrop Make(GameObject template, Transform parent, int place, string name)
        {
            if (template == null || parent == null)
            {
                return null;
            }
            GameObject copy = UnityEngine.Object.Instantiate(template, parent, false);
            copy.name = name;
            var dropdown = copy.GetComponent<global::UI.Elements.Dropdown>();
            if (dropdown == null)
            {
                UnityEngine.Object.Destroy(copy);
                return null;
            }
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(Width, Height);
            rect.anchoredPosition = new Vector2(First.x - place * (Width + Gap), First.y);
            // (whatever state MyEmployees had left its own box in when it was copied: this one is shown and can be clicked)
            var group = copy.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            copy.SetActive(true);
            return new FilterDrop(dropdown);
        }

        /// <summary>
        /// The choices (final words, the first is "all") and which is chosen. The game's drop-down is only told when
        /// something differs from what it was last given, so writing a table again costs it nothing.
        /// </summary>
        public void Set(List<string> options, int chosen, Action<int> picked)
        {
            _picked = picked;
            bool same = options.Count == _given.Count && _dropdown.SelectedOptionIndex == chosen;
            for (int i = 0; same && i < options.Count; i++)
            {
                same = options[i] == _given[i];
            }
            if (same)
            {
                return;
            }
            _given.Clear();
            _given.AddRange(options);
            _dropdown.SetOptions(_given, false, chosen);
        }

        public void Show(bool shown)
        {
            GameObject o = _dropdown.gameObject;
            if (o.activeSelf == shown)
            {
                return;
            }
            if (!shown && global::UI.Elements.Dropdown.currentDropdown == _dropdown)
            {
                _dropdown.HideOptions();   // its list is open: closed as the game closes it, so it is no longer the "current drop-down"
            }
            o.SetActive(shown);
        }
    }
}
