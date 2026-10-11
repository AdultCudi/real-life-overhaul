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
    /// game's own text field (the one of its text input window, TextInputUI.inputField), put beside the app's own
    /// "mark all as read" button in its row, and it is read by the game's own placeholder text.
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
        private const int Longest = 24;                 // characters
        private const float WidthOfButtons = 2.2f;      // the box is as wide as this many of the row's button

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
                Button mark = app != null ? Field<Button>(app, typeof(ContactsApp), "markAllAsReadButton") : null;
                TMP_InputField template = uis != null && uis.textInputUi != null ? Field<TMP_InputField>(uis.textInputUi, typeof(TextInputUI), "inputField") : null;
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

        private static T Field<T>(object owner, Type type, string name) where T : class
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field != null ? field.GetValue(owner) as T : null;
        }

        // The box in the row: before the game's button, as high as it, a little over two of it wide, with the game's placeholder words.
        private void Dress(Button mark, Transform row)
        {
            var markRect = (RectTransform)mark.transform;
            var rect = (RectTransform)transform;
            transform.SetSiblingIndex(mark.transform.GetSiblingIndex());
            float height = Mathf.Max(40f, markRect.rect.height), width = Mathf.Max(220f, markRect.rect.width * WidthOfButtons);
            bool laidOut = row.GetComponent<HorizontalOrVerticalLayoutGroup>() != null;
            LayoutElement layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.flexibleWidth = 0f;
            layout.minWidth = width * 0.6f;
            if (!laidOut)
            {
                // A row the game places by hand: the box stands just before the button, on the same line.
                rect.anchorMin = markRect.anchorMin;
                rect.anchorMax = markRect.anchorMax;
                rect.pivot = new Vector2(1f, markRect.pivot.y);
                rect.sizeDelta = new Vector2(width, height);
                rect.anchoredPosition = new Vector2(markRect.anchoredPosition.x - markRect.rect.width * markRect.pivot.x - 12f, markRect.anchoredPosition.y);
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
                Logger?.Info("Contacts check (search box): in '" + row.name + "' before '" + mark.name + "' (" + (laidOut ? "the row lays it out" : "placed by hand beside the button") + "), "
                             + width.ToString("0") + " x " + height.ToString("0") + ", copied from the game's text input window: PASS");
            }
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

        // Only while the Contacts app shows (the box is part of it): whether the box has the keyboard, and whether the game made its list again.
        private void Update()
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
            if (_typing)
            {
                Keys.Suspended = false;
            }
        }
    }
}
