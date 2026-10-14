using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UI.Components;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A search box in the game's Contacts app: what is typed narrows the list to the contacts whose name or description
    /// has every word of it, ignoring case and accents, within the category that is picked. The box is a copy of the
    /// game's own text field (the one of its text input window, TextInputUI.inputField), on a line of its own under the
    /// app's "mark all as read" and "delete all" buttons, as wide as the list of contacts, with the game's own
    /// placeholder text. The game places that row by hand and its two buttons fill it, so the box does not go in it; the
    /// list starts a line lower instead, and is put back where the game had it if the box goes.
    ///
    /// HOW THE LIST IS NARROWED. The game makes the list in ContactsApp.LoadContactsList: it fills the scroller's
    /// public list of rows (ContactScrollerController.data) and reloads it. A search asks the game to make the list as
    /// usual and then takes out the rows that do not match and reloads the scroller: the game's own rows, its own order,
    /// its own pictures. The game makes the list again by itself (a message comes in, a category is picked): while there is
    /// something in the box, the box's component sees that the list is not the one it narrowed (its count or first row,
    /// one comparison a frame, and only while the Contacts app is showing) and narrows it again.
    ///
    /// While the box has the keyboard the mod's own keys stand still (Keys.Suspended), so typing a letter does not also
    /// do what that key does. The box is emptied when the app closes. Cost: nothing while the box is empty but one bool
    /// a frame; a search is one pass over the contacts (a few dozen) with the words compared by the culture-free rules.
    /// </summary>
    public sealed class ContactSearch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("phone", "ContactSearch", typeof(ContactSearch));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const int Longest = 24;                 // characters
        private const float Gap = 16f;                  // between the buttons, the box and the list, on the game's canvas

        public static IModLogger Logger;

        private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;
        private static readonly Func<string, string, bool> Contains = (text, word) => Compare.IndexOf(text, word, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        private static ContactSearch _installed;

        private ContactsApp _app;
        private TMP_InputField _input;
        private string _query = string.Empty;
        private int _count = -1;
        private ContactModel _first;
        private bool _typing;
        private Predicate<ContactModel> _misses;
        private RectTransform _list;                    // the game's list of contacts, moved down a line for the box
        private Vector2 _listTopWas;
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>Puts the box in the game's Contacts app, once per city. Safe to call again; says in Player.log if the app is not built as expected.</summary>
        public static void Install()
        {
            try
            {
                if (_installed != null || !Settings.PhoneContacts)
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                ContactsApp app = uis != null && uis.fullMenu != null ? uis.fullMenu.contactsApp : null;
                Button mark = app != null ? Field<Button>(app, GameContract.ContactsMarkAll) : null;
                TMP_InputField template = uis != null && uis.textInputUi != null ? Field<TMP_InputField>(uis.textInputUi, GameContract.TextInputField) : null;
                if (app == null || mark == null || template == null || mark.transform.parent == null)
                {
                    UiClone.Fallback("the Contacts app or the game's text field is not built as expected, so the contacts have no search box");
                    return;
                }
                Transform row = mark.transform.parent;
                GameObject box = Instantiate(template.gameObject, row, false);
                box.name = "RealLife Contact Search";
                box.SetActive(true);
                foreach (MonoBehaviour extra in box.GetComponents<MonoBehaviour>())
                {
                    if (extra != null && !(extra is TMP_InputField) && !(extra is Graphic) && !(extra is LayoutElement))
                    {
                        Destroy(extra);   // the window's keyboard handler: it would take the focus and confirm the window
                    }
                }
                var search = box.AddComponent<ContactSearch>();
                search._app = app;
                search._input = box.GetComponent<TMP_InputField>();
                search._misses = search.Misses;
                search.Dress(mark, row);
                _installed = search;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("the contacts' search box could not be made");
            }
        }

        // A private field of the game's, through its line of the game contract (resolved once: Core/GameContract).
        private static T Field<T>(object owner, GameMember member) where T : class
        {
            FieldInfo field = member.Field;
            return field != null ? field.GetValue(owner) as T : null;
        }

        // The box on its own line: under the row of the game's buttons, as high as they are and as wide as the list, which
        // starts below it. Everything is measured from the game's own pieces in the panel they share, so it holds at any
        // screen size.
        private void Dress(Button mark, Transform row)
        {
            var markRect = (RectTransform)mark.transform;
            var rowRect = row as RectTransform;
            var panel = row.parent as RectTransform;
            var rect = (RectTransform)transform;
            RectTransform list = ListOf(panel);
            float height = Mathf.Max(40f, markRect.rect.height);
            string where;
            if (rowRect != null && panel != null && list != null)
            {
                rowRect.GetWorldCorners(Corners);
                float rowBottom = panel.InverseTransformPoint(Corners[0]).y;
                list.GetWorldCorners(Corners);
                float listLeft = panel.InverseTransformPoint(Corners[0]).x, listRight = panel.InverseTransformPoint(Corners[2]).x, listTop = panel.InverseTransformPoint(Corners[1]).y;
                float top = Mathf.Min(rowBottom, listTop + height + Gap) - Gap;
                rect.SetParent(panel, false);
                rect.SetSiblingIndex(list.GetSiblingIndex());
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(Mathf.Max(220f, listRight - listLeft), height);
                rect.anchoredPosition = new Vector2(listLeft - panel.rect.xMin, top - panel.rect.yMax);
                float shift = listTop - (top - height - Gap);
                if (shift > 0f)
                {
                    _list = list;
                    _listTopWas = list.offsetMax;
                    list.offsetMax = new Vector2(list.offsetMax.x, list.offsetMax.y - shift);   // (its top edge down, its bottom where it was)
                }
                where = "under '" + row.name + "', as wide as the list (" + rect.sizeDelta.x.ToString("0") + " x " + height.ToString("0") + "), the list " + Mathf.Max(0f, shift).ToString("0") + " lower";
            }
            else
            {
                // Not the panel the game has now: in the row after its last button, in what room is left.
                float right = 0f;
                for (int i = 0; i < row.childCount; i++)
                {
                    var child = row.GetChild(i) as RectTransform;
                    if (child != null && child != rect && child.gameObject.activeSelf)
                    {
                        right = Mathf.Max(right, child.anchoredPosition.x + child.rect.width * (1f - child.pivot.x));
                    }
                }
                float room = rowRect != null ? rowRect.rect.width - right - Gap : 0f;
                rect.anchorMin = rect.anchorMax = markRect.anchorMin;
                rect.pivot = new Vector2(0f, markRect.pivot.y);
                rect.sizeDelta = new Vector2(Mathf.Max(220f, room), height);
                rect.anchoredPosition = new Vector2(right + Gap, markRect.anchoredPosition.y);
                where = "in '" + row.name + "' after its buttons (" + rect.sizeDelta.x.ToString("0") + " x " + height.ToString("0") + ")";
                UiClone.Fallback("the Contacts app's list is not where it was, so the search box goes in the row of its buttons");
            }
            _input.onValueChanged.RemoveAllListeners();
            _input.onEndEdit.RemoveAllListeners();
            _input.onSubmit.RemoveAllListeners();
            _input.characterLimit = Longest;
            _input.contentType = TMP_InputField.ContentType.Standard;
            _input.lineType = TMP_InputField.LineType.SingleLine;
            _input.SetTextWithoutNotify(string.Empty);
            _input.onValueChanged.AddListener(text => UiClone.Safe(() => Changed(text)));
            if (_input.placeholder != null)
            {
                UiClone.Key(_input.placeholder, "reallife_contacts_search");
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Contacts check (search box): " + where + ", copied from the game's text input window: PASS");
            }
        }

        // The game's list of contacts as a piece of the panel the buttons are in: the scroller's own object, or the one it hangs under there.
        private RectTransform ListOf(RectTransform panel)
        {
            Transform t = _app != null && _app.contactScrollerController != null ? _app.contactScrollerController.transform : null;
            while (t != null && panel != null && t.parent != panel)
            {
                t = t.parent;
            }
            return t as RectTransform;
        }

        private void Changed(string text)
        {
            _query = (text ?? string.Empty).Trim();
            _app.LoadContactsList(ContactCategorySelection.SelectedCategory, false);   // the game's own list for the category, then narrowed
            Narrow();
        }

        private bool Misses(ContactModel model)
        {
            string name = model.nameLocalizationKey != null ? model.nameLocalizationKey.GetLocalization() : model.contactName;
            string about = model.descriptionLocalizationKey;
            if (!string.IsNullOrEmpty(about) && LocalizorManager.IsLocalizedKey(about))
            {
                about = about.GetLocalization();
            }
            return !ContactCatalogue.Matches(_query, name, about, Contains);
        }

        private void Narrow()
        {
            ContactScrollerController list = _app.contactScrollerController;
            if (list == null)
            {
                return;
            }
            int before = list.data.Count;
            if (_query.Length > 0)
            {
                list.data.RemoveAll(_misses);
                list.scroller.ReloadData();
            }
            _count = list.data.Count;
            _first = _count > 0 ? list.data[0] : null;
            if (Meta.Dev.Verbose && _query.Length > 0)
            {
                Logger?.Info("Contacts check (search): '" + _query + "' leaves " + _count + " of " + before + " contact(s): PASS");
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Phone: ContactSearch.Update");
        // Only while the Contacts app shows (the box is part of it): whether the box has the keyboard, and whether the game made its list again.
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            bool typing = _input != null && _input.isFocused;
            if (typing != _typing)
            {
                _typing = typing;
                Keys.Suspended = typing;
            }
            if (_query.Length == 0 || _app == null || _app.contactScrollerController == null)
            {
                return;
            }
            List<ContactModel> data = _app.contactScrollerController.data;
            if (data.Count != _count || (_count > 0 && !ReferenceEquals(data[0], _first)))
            {
                Narrow();
            }
        }

        // The app closes: the box is emptied, the keys are the player's again; the game makes its whole list when it next opens.
        private void OnDisable()
        {
            if (_typing)
            {
                _typing = false;
                Keys.Suspended = false;
            }
            _query = string.Empty;
            _count = -1;
            _first = null;
            if (_input != null)
            {
                _input.SetTextWithoutNotify(string.Empty);
            }
        }

        private void OnDestroy()
        {
            if (_installed == this)
            {
                _installed = null;
            }
            if (_list != null)
            {
                _list.offsetMax = _listTopWas;   // the list back where the game had it
            }
            if (_typing)
            {
                Keys.Suspended = false;
            }
        }
    }
}
