using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Entities;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The only place that touches the game for the Contacts app's groups (<see cref="ContactGroups"/>). The game's app already
    /// filters its list by a row of category buttons (ContactCategorySelection: one button for each category some contact is in,
    /// with the unread badge of its contacts and a tooltip), by the number in Contact.category. The mod adds seven categories
    /// beyond the game's seven (numbers 101 to 107 of ContactCategoryName, each a ContactCategory made once and added to the row's
    /// list) and puts each of its contacts in one: so its buttons, badges, "mark all as read" and filter are the game's own. The
    /// game's own contacts stay in their categories. With the option off every contact of the mod has the category it had before
    /// groups. Cost: a pass over the phone's contacts when the option changes or a contact is added, nothing a frame.
    /// </summary>
    public static class ContactGroupsGame
    {
        private static readonly FeatureGuard Fault = Guard.For("phone", "ContactGroupsGame", typeof(ContactGroupsGame));

        public static IModLogger Logger;

        private static ContactCategory[] _made;     // one for each group, made once for the session
        private static UnityAction _onAdded;        // made once: taken off and put on again at each load
        private static UnityAction _onRemoved;
        private static bool _busy;                  // the mod itself is raising the game's event
        private static bool _ready;                 // the app's row is as expected in this city
        private static ContactGroupsRow _row;

        /// <summary>Whether the contacts are grouped now: the option on and the row ready.</summary>
        public static bool On => _ready && Settings.PhoneContactGroups;

        /// <summary>
        /// Once per city load, after the mod's contacts are made: the groups as buttons of the game's row, then every contact in its
        /// group. Says in Player.log, and leaves the contacts where they were, if the app is not built as expected.
        /// </summary>
        public static void Install()
        {
            try
            {
                if (Fault.Off)
                {
                    return;
                }
                _ready = false;
                UIs uis = InstanceBehavior<UIs>.Instance;
                ContactsApp app = uis != null && uis.fullMenu != null ? uis.fullMenu.contactsApp : null;
                ContactCategorySelection selection = app != null ? app.GetComponentInChildren<ContactCategorySelection>(true) : null;
                FieldInfo field = GameContract.ContactCategories.Field;
                var list = selection != null && field != null ? field.GetValue(selection) as List<ContactCategory> : null;
                if (list == null || list.Count == 0 || ContactsApp.onContactAdded == null || ContactsApp.onContactRemoved == null)
                {
                    UiClone.Fallback("the Contacts app's row of category buttons is not built as expected, so the contacts have no groups");
                    Sweep(false);   // none left in a group the row would not show
                    return;
                }
                Categories(list);
                _row = selection.GetComponent<ContactGroupsRow>();
                if (_row == null)
                {
                    _row = selection.gameObject.AddComponent<ContactGroupsRow>();
                }
                _onAdded = _onAdded ?? Guard.Listener((UnityAction)OnAdded);
                _onRemoved = _onRemoved ?? Guard.Listener((UnityAction)OnRemoved);
                ContactsApp.onContactAdded.RemoveListener(_onAdded);
                ContactsApp.onContactAdded.AddListener(_onAdded);
                ContactsApp.onContactRemoved.RemoveListener(_onRemoved);
                ContactsApp.onContactRemoved.AddListener(_onRemoved);
                _ready = true;
                Apply();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Fault.Count(Time.realtimeSinceStartup);
            }
        }

        /// <summary>Puts every contact of the mod in its group, or back where it was with the option off; the game's list and row follow at once.</summary>
        public static void Apply()
        {
            try
            {
                if (Fault.Off)
                {
                    return;
                }
                int moved = Sweep(On);
                if (moved > 0)
                {
                    Refresh();
                }
                else if (_row != null)
                {
                    _row.Request();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The category each contact of the mod stands in now, from the registry; how many moved. Written for the game's two lists of it.</summary>
        private static int Sweep(bool grouped)
        {
            GameInstance save = SaveGameManager.Current;
            List<Contact> contacts = save != null ? save.Contacts : null;
            if (contacts == null)
            {
                return 0;
            }
            int ours = 0, moved = 0;
            for (int i = 0; i < contacts.Count; i++)
            {
                Contact contact = contacts[i];
                if (contact == null)
                {
                    continue;
                }
                int now = (int)contact.category;
                if (ContactGroups.GroupOf(contact.id, contact.description, out int plain) != ContactGroup.None)
                {
                    ours++;
                }
                int wanted = ContactGroups.CategoryFor(contact.id, contact.description, now, grouped);
                if (wanted == now)
                {
                    continue;
                }
                contact.category = (ContactCategoryName)wanted;
                moved++;
                if (save.PlayerDefaults != null && save.PlayerDefaults.contactsLastName == contact.id)
                {
                    // The app opens on the last contact only when the category kept with it is the contact's own, else on an empty list.
                    save.PlayerDefaults.contactsLastCategoryName = contact.category;
                }
            }
            if (moved > 0 && Meta.Dev.Verbose)
            {
                Logger?.Info("Contacts check (groups): " + moved + " of the mod's " + ours + " contact(s) moved " + (grouped ? "into their groups" : "back to General") + ": PASS");
            }
            return moved;
        }

        // The game's row is built again (its event), then the list for the category shown, which may be one that has gone.
        private static void Refresh()
        {
            _busy = true;
            try
            {
                ContactsApp.onContactAdded?.Invoke();
            }
            finally
            {
                _busy = false;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            ContactsApp app = uis != null && uis.fullMenu != null ? uis.fullMenu.contactsApp : null;
            if (app != null && app.isActiveAndEnabled)
            {
                ContactCategoryName? shown = ContactCategorySelection.SelectedCategory;
                if (shown.HasValue && !Holds(shown.Value))
                {
                    shown = null;
                }
                app.LoadContactsList(shown, false);
            }
            if (_row != null)
            {
                _row.Request();
            }
        }

        private static bool Holds(ContactCategoryName category)
        {
            List<Contact> contacts = SaveGameManager.Current != null ? SaveGameManager.Current.Contacts : null;
            for (int i = 0; contacts != null && i < contacts.Count; i++)
            {
                if (contacts[i] != null && contacts[i].category == category)
                {
                    return true;
                }
            }
            return false;
        }

        // The game raises it as a contact is made, before or after its own row is built: the contact is put in its group first.
        private static void OnAdded()
        {
            if (!_busy)
            {
                Apply();
            }
        }

        private static void OnRemoved()
        {
            if (!_busy && _row != null)
            {
                _row.Request();
            }
        }

        // The seven categories, made once, with the game's own pictures; added to the row's list once.
        private static void Categories(List<ContactCategory> list)
        {
            if (_made == null)
            {
                _made = new ContactCategory[ContactGroups.Count];
            }
            ContactCategory plain = null;
            for (int i = 0; i < list.Count && plain == null; i++)
            {
                plain = list[i] != null && ContactGroups.GroupOfCategory((int)list[i].categoryName) == ContactGroup.None ? list[i] : null;
            }
            for (int i = 0; i < ContactGroups.Count; i++)
            {
                if (_made[i] == null)
                {
                    ContactCategory made = ScriptableObject.CreateInstance<ContactCategory>();
                    made.name = "RealLife contact group " + (i + 1);
                    made.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    made.categoryName = (ContactCategoryName)ContactGroups.CategoryOf((ContactGroup)(i + 1));
                    _made[i] = made;
                }
                if (_made[i].icon == null)
                {
                    Sprite sprite = GameSprites.Get(ContactGroups.Glyphs[i]);
                    _made[i].icon = sprite != null ? sprite : plain != null ? plain.icon : null;
                    if (sprite == null && Meta.Dev.Verbose)
                    {
                        Logger?.Info("Contacts check (groups): the game has no picture '" + ContactGroups.Glyphs[i] + "' yet, so the button borrows a category's: WARN");
                    }
                }
                if (!list.Contains(_made[i]))
                {
                    list.Add(_made[i]);
                }
            }
        }
    }
}
