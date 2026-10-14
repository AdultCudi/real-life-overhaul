using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The box's parts kept from one page to the next (InfoBox.cs, Clear and Add). A page of an app is written again on
    /// every click, and it is mostly the same parts: Clear lays the last page's parts aside, asleep under the templates,
    /// one list for each kind, and Add takes one back for each part of that kind the new page writes, before it copies a
    /// template. A part laid aside goes back to what a new copy of its template is (<see cref="Restore"/>): every setting
    /// a page may have changed on it that the page writing it does not set itself is set again from the template, and
    /// anything put under it after it was made is destroyed, as the whole part was before. Then the page writes it exactly
    /// as it writes a new copy.
    ///
    /// What each part's writer sets, and so needs no setting back: a row's, a line's and a note's words and margin; a
    /// subtitle's words and margin; a header's words and bar; a section's heading (by key or in words); a pair's two
    /// halves and its indent; a meter's words, indent, bar and colour; a tick's words, indent and mark; a switch's label,
    /// its click and whether it is on; a selector's arrows, clicks and words; a row of buttons' buttons, each shown with
    /// its word and click by InfoBox.Button. What Restore sets back: a line's colour (a tone); a header's margin (its
    /// chips) and the chips themselves; a tick's colour and its mark's picture, colour, size and place; a switch's state,
    /// so that it wakes from the template's as a new copy does; and for the parts made of others (a pair, a switch, a
    /// selector, a row of buttons) everything under them that the template has not.
    ///
    /// Cost: nothing per frame. A page costs one move into the box for each part taken back, one copy for each new one;
    /// Clear one move for each part laid aside. The parts laid aside are at most the longest page written, kept asleep.
    /// </summary>
    public sealed partial class InfoBox
    {
        private Transform _parked;   // asleep under the templates: the parts laid aside
        private readonly List<GameObject> _live = new List<GameObject>(48);       // the parts of the page that shows that the box keeps
        private readonly List<GameObject> _liveFrom = new List<GameObject>(48);   // the template each is of
        private readonly Dictionary<GameObject, List<GameObject>> _spare = new Dictionary<GameObject, List<GameObject>>(16);

        // What Restore sets back, read from the templates once.
        private Color _lineColour, _checkColour, _checkMarkColour;
        private Vector4 _headerMargin;
        private Sprite _checkMarkSprite;
        private Vector2 _checkMarkSize, _checkMarkAt;
        private bool _switchOn;

        // The kinds kept, and what to set back on each: read once the templates are made (From).
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

        // Whether a template's copies written into the box are kept from page to page.
        private bool Kept(GameObject template)
        {
            return template != null && _parked != null
                   && (template == _bodyT || template == _subT || template == _noteT || template == _headerT || template == _headingT || template == _pairT
                       || template == _meterT || template == _checkT || template == _footT || template == _switch || template == _selector || template == _buttons);
        }

        // A part of the template's kind laid aside, back in the box at the end and as a new copy would be; null if none is.
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
                    continue;   // (destroyed meanwhile: the box's whole panel was)
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

        // A part of the page that showed, laid aside for the next: set back to its template (Restore) and asleep.
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

        /// <summary>
        /// A part as a new copy of its template is, for the settings a page may change and its writer does not set: see the
        /// class's summary. Run as the part is laid aside, while it is still awake, so that a switch goes back to the template's
        /// state as a copy's does when it first wakes.
        /// </summary>
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
                    chips.gameObject.SetActive(false);   // the chips go as the whole header once did: gone at the end of the frame
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
            else if (template == _switch)
            {
                Toggle toggle = part.GetComponentInChildren<Toggle>(true);
                if (toggle != null)
                {
                    toggle.SetIsOnWithoutNotify(_switchOn);
                }
                Prune(part.transform, template.transform);
            }
            else if (template == _pairT || template == _selector || template == _buttons)
            {
                Prune(part.transform, template.transform);
            }
        }

        // What was put under a part after it was copied: destroyed, as the whole part once was with its page.
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
