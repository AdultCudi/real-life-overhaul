using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A filter over a table: the game's own drop-down (UI.Elements.Dropdown), whole and working, as EconoView has one over
    /// its transactions ("All types", then each type); the copy is of MyEmployees' mass action box. The box, its list, its
    /// scroller, typing to find a choice and closing on a click elsewhere are all the game's scripts, left on the copy.
    /// Made when a table first needs it, under the app's panel while showing, so it wakes before it is given its choices.
    /// Everything that touches the game's class is here: a caller that gets an exception from it drops the filter and says
    /// so once. Costs nothing a frame of the mod's.
    /// </summary>
    public sealed class FilterDrop
    {
        private const float Width = 640f, Height = 80f, Gap = 20f;
        // Where MyEmployees has the box is level with the tabs; the mod's tables have up to seven tabs, so a filter stands a
        // row higher, in the band the game leaves empty.
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

        /// <summary>A copy of the template over the table (place 0: the right-hand one, 1: left of it). Null if it is no drop-down.</summary>
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
            // Shown and clickable, whatever state MyEmployees left its own box in.
            UiClone.Visible(copy.GetComponent<CanvasGroup>());
            copy.SetActive(true);
            return new FilterDrop(dropdown);
        }

        /// <summary>A drop-down of the game's already placed (a row of OptionRows), made awake under a showing page so its scripts start. Null if none.</summary>
        public static FilterDrop Of(GameObject placed)
        {
            var dropdown = placed != null ? placed.GetComponentInChildren<global::UI.Elements.Dropdown>(true) : null;
            if (dropdown == null)
            {
                return null;
            }
            UiClone.Visible(dropdown.GetComponent<CanvasGroup>());
            return new FilterDrop(dropdown);
        }

        /// <summary>Usable or not, in the game's own look for a control that cannot be used.</summary>
        public void SetInteractable(bool interactable)
        {
            _dropdown.SetInteractable(interactable);
        }

        /// <summary>Closes its list if it is open, as the game closes it.</summary>
        public void Close()
        {
            if (global::UI.Elements.Dropdown.currentDropdown == _dropdown)
            {
                _dropdown.HideOptions();
            }
        }

        /// <summary>
        /// The choices (final words, "all" first) and the one chosen. The game's drop-down is told only when something differs
        /// from what it was last given, so writing a table again costs it nothing.
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
                _dropdown.HideOptions();   // its list is open: closed as the game closes it, so it is no longer the current drop-down
            }
            o.SetActive(shown);
        }
    }
}
