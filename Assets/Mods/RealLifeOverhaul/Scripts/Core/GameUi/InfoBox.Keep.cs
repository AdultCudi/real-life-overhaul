using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The box's parts kept from page to page: Clear lays them aside asleep, one list per kind, and Add takes one back before
    /// copying a template, set back first to what a new copy is (<see cref="Restore"/>). Costs nothing a frame.
    /// </summary>
    public sealed partial class InfoBox
    {
        private Transform _parked;   // asleep under the templates: the parts laid aside
        private readonly List<GameObject> _live = new List<GameObject>(48);       // the parts of the page showing that the box keeps
        private readonly List<GameObject> _liveFrom = new List<GameObject>(48);   // the template each is of
        private readonly Dictionary<GameObject, List<GameObject>> _spare = new Dictionary<GameObject, List<GameObject>>(16);

        // What Restore sets back, read from the templates once.
        private Color _lineColour, _checkColour, _checkMarkColour;
        private Vector4 _headerMargin;
        private Sprite _checkMarkSprite;
        private Vector2 _checkMarkSize, _checkMarkAt;
        private bool _switchOn;

        // Read once the templates are made (From).
        private void Keep(Transform templates)
        {
            var holder = new GameObject("Parked", typeof(RectTransform));
            holder.transform.SetParent(templates, false);
            _parked = holder.transform;
            _lineColour = _bodyT.GetComponent<TMP_Text>().color;
            _headerMargin = _headerT.GetComponent<TMP_Text>().margin;
            _checkColour = _checkT.GetComponent<TMP_Text>().color;
            var mark = _checkT.transform.Find("Mark").GetComponent<Image>();
            _checkMarkSprite = mark.sprite;
            _checkMarkColour = mark.color;
            var markRect = (RectTransform)mark.transform;
            _checkMarkSize = markRect.sizeDelta;
            _checkMarkAt = markRect.anchoredPosition;
            Toggle toggle = _switch != null ? _switch.GetComponentInChildren<Toggle>(true) : null;
            _switchOn = toggle != null && toggle.isOn;
        }

        // A template's copies written into the box are kept from page to page.
        private bool Kept(GameObject template)
        {
            return template != null && _parked != null
                   && (template == _bodyT || template == _subT || template == _noteT || template == _headerT || template == _headingT || template == _pairT
                       || template == _meterT || template == _checkT || template == _footT || template == _switch || template == _toggleT || template == _selector || template == _buttons);
        }

        // A part of the template's kind laid aside, back in the box as its last child; null if none is.
        private GameObject Reuse(GameObject template)
        {
            if (!_spare.TryGetValue(template, out List<GameObject> spare))
            {
                return null;
            }
            while (spare.Count > 0)
            {
                GameObject part = spare[spare.Count - 1];
                spare.RemoveAt(spare.Count - 1);
                if (part == null)
                {
                    continue;   // destroyed meanwhile with the box's whole panel
                }
                part.transform.SetParent(_box, false);   // the last of the box's children: the page's order
                if (!part.activeSelf)
                {
                    part.SetActive(true);
                }
                return part;
            }
            return null;
        }

        // Set back to its template while still awake, then laid aside asleep.
        private void Park(GameObject part, GameObject template)
        {
            Restore(part, template);
            part.transform.SetParent(_parked, false);
            if (!_spare.TryGetValue(template, out List<GameObject> spare))
            {
                spare = new List<GameObject>(16);
                _spare[template] = spare;
            }
            spare.Add(part);
        }

        /// <summary>Sets back what a page may change and its writer does not set; while still awake, so a switch returns to its template's state.</summary>
        private void Restore(GameObject part, GameObject template)
        {
            if (template == _bodyT)
            {
                part.GetComponent<TMP_Text>().color = _lineColour;   // a line in a tone
            }
            else if (template == _headerT)
            {
                part.GetComponent<TMP_Text>().margin = _headerMargin;   // the room the chips took
                Transform chips = part.transform.Find("Chips");
                if (chips != null)
                {
                    chips.gameObject.SetActive(false);   // gone at the end of the frame
                    chips.SetParent(null, false);
                    Object.Destroy(chips.gameObject);
                }
            }
            else if (template == _checkT)
            {
                part.GetComponent<TMP_Text>().color = _checkColour;   // a need not met is written in red
                var mark = part.transform.Find("Mark").GetComponent<Image>();
                mark.sprite = _checkMarkSprite;
                mark.color = _checkMarkColour;
                var rect = (RectTransform)mark.transform;
                rect.sizeDelta = _checkMarkSize;
                rect.anchoredPosition = _checkMarkAt;
            }
            else if (template == _switch || template == _toggleT)
            {
                Toggle toggle = part.GetComponentInChildren<Toggle>(true);
                if (toggle != null && template == _switch)
                {
                    toggle.SetIsOnWithoutNotify(_switchOn);
                }
                else if (toggle != null)
                {
                    toggle.SetIsOnWithoutNotify(_toggleOn);   // a settings switch (InfoBox.Toggle)
                }
                Prune(part.transform, template.transform);
            }
            else if (template == _pairT || template == _selector || template == _buttons)
            {
                Prune(part.transform, template.transform);
            }
        }

        // What was put under a part after it was copied: destroyed.
        private static void Prune(Transform part, Transform template)
        {
            for (int i = part.childCount - 1; i >= template.childCount; i--)
            {
                GameObject added = part.GetChild(i).gameObject;
                added.SetActive(false);
                added.transform.SetParent(null, false);
                Object.Destroy(added);
            }
        }
    }
}
