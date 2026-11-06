using System;
using System.Collections;
using System.Collections.Generic;
using Entities;
using Localizor;
using RealLifeOverhaul.Meta;
using UI;
using UI.Notification;
using UI.Smartphone;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Options > Tests > Check the Contacts app's groups. Opens the game's Contacts app and, in the game's own list, checks that
    /// every button lists exactly the contacts of its category (the save's count against the rows shown), that the lists of all
    /// the buttons together hold every contact (the count before against the count after: nothing lost), that every contact of the
    /// mod stands in the group the registry names, and that typing a contact's name in the search box finds it whatever its group.
    /// Each is one line in Player.log, "Contact groups check (...): PASS" or FAIL and why, and the last line says PASS only if every
    /// one did. Costs nothing unless it is run; the work is spread over frames, one list a frame.
    /// </summary>
    public static class ContactGroupsTest
    {
        private const int OpenFrames = 3;   // frames the app is given to open and write its list
        private static bool _running;

        /// <summary>Starts the test; a second press while it runs does nothing.</summary>
        public static void Run()
        {
            try
            {
                if (_running)
                {
                    return;
                }
                _running = true;
                MetaUi.Instance.Run(Routine());
            }
            catch (Exception e)
            {
                _running = false;
                ContactGroupsGame.Logger?.Error(e);
            }
        }

        private static IEnumerator Routine()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            ContactsApp app = uis != null && uis.fullMenu != null ? uis.fullMenu.contactsApp : null;
            GameInstance save = SaveGameManager.Current;
            ContactCategoryName? shownBefore = null;
            bool opened = false;
            try
            {
                if (app == null || save == null || save.Contacts == null)
                {
                    Say(NotificationType.Warning, "reallife_contacts_test_groups_none", null);
                    yield break;
                }
                if (!ContactGroupsGame.On)
                {
                    ContactGroupsGame.Logger?.Info("Contact groups check (all): SKIPPED, the groups are off or the Contacts app's row is not as expected.");
                    Say(NotificationType.Warning, "reallife_contacts_test_groups_off", null);
                    yield break;
                }
                shownBefore = ContactCategorySelection.SelectedCategory;
                uis.fullMenu.ShowApp(AppName.Contacts);
                opened = true;
                for (int f = 0; f < OpenFrames; f++)
                {
                    yield return null;
                }
                if (!app.isActiveAndEnabled)
                {
                    ContactGroupsGame.Logger?.Info("Contact groups check (all): FAIL, the Contacts app did not open.");
                    Say(NotificationType.Error, "reallife_contacts_test_groups_fail", Faults(1));
                    yield break;
                }

                int faults = 0;
                List<Contact> contacts = save.Contacts;
                int total = contacts.Count;

                // The count before: with no category chosen the list holds every contact.
                app.LoadContactsList(null, false);
                yield return null;
                int listed = app.contactScrollerController.data.Count;
                faults += Line("all contacts", "the save has " + total + ", the list shows " + listed, listed == total);

                // Each button lists its category, and no contact is in a category with no button.
                ContactCategorySelection selection = app.GetComponentInChildren<ContactCategorySelection>(true);
                ContactCategoryButton[] buttons = selection != null ? selection.GetComponentsInChildren<ContactCategoryButton>(false) : new ContactCategoryButton[0];
                int after = 0, groups = 0;
                for (int b = 0; b < buttons.Length; b++)
                {
                    ContactCategoryName category = buttons[b].categoryName;
                    ContactGroup group = ContactGroups.GroupOfCategory((int)category);
                    int expected = 0;
                    for (int c = 0; c < contacts.Count; c++)
                    {
                        expected += contacts[c] != null && contacts[c].category == category ? 1 : 0;
                    }
                    app.LoadContactsList(category, false);
                    yield return null;
                    List<ContactModel> rows = app.contactScrollerController.data;
                    bool members = true;
                    for (int r = 0; group != ContactGroup.None && r < rows.Count; r++)
                    {
                        members &= ContactGroups.GroupOf(rows[r].contact.id, rows[r].contact.description, out int plain) == group;
                    }
                    after += rows.Count;
                    groups += group != ContactGroup.None ? 1 : 0;
                    string mine = group == ContactGroup.None ? string.Empty : members ? ", all of this group" : ", one is not of this group";
                    faults += Line(group != ContactGroup.None ? "group " + group : "category " + category,
                                   "the button lists " + rows.Count + " of " + expected + " contact(s)" + mine,
                                   rows.Count == expected && expected > 0 && members);
                }
                faults += Line("nothing lost", "the buttons list " + after + " of the " + total + " contact(s) the list held before", after == total);

                // Every contact of the mod is in the group its registry entry names.
                int ours = 0, wrong = 0;
                for (int c = 0; c < contacts.Count; c++)
                {
                    Contact contact = contacts[c];
                    ContactGroup group = contact != null ? ContactGroups.GroupOf(contact.id, contact.description, out int plain) : ContactGroup.None;
                    if (group == ContactGroup.None)
                    {
                        continue;
                    }
                    ours++;
                    if ((int)contact.category != ContactGroups.CategoryOf(group))
                    {
                        wrong++;
                        ContactGroupsGame.Logger?.Info("Contact groups check (registry): '" + contact.id + "' is in category " + (int)contact.category
                                                       + ", not its group's " + ContactGroups.CategoryOf(group) + ": FAIL");
                    }
                }
                faults += Line("registry", ours + " contact(s) of the mod, " + wrong + " outside their group", wrong == 0);

                // Search finds each contact of the mod and a few of the game's, whichever group is shown.
                int searched = 0, missed = 0;
                bool boxed = true;
                for (int c = 0; c < contacts.Count && boxed; c++)
                {
                    Contact contact = contacts[c];
                    if (contact == null || contact.id == null)
                    {
                        continue;
                    }
                    bool mine = ContactGroups.GroupOf(contact.id, contact.description, out int plain) != ContactGroup.None;
                    if (!mine && c % 4 != 0)
                    {
                        continue;
                    }
                    string name = LocalizorManager.IsLocalizedKey(contact.id) ? contact.id.GetLocalization() : contact.id;
                    List<Contact> found = ContactSearch.Probe(name);
                    if (found == null)
                    {
                        boxed = false;
                        break;
                    }
                    searched++;
                    if (!found.Contains(contact))
                    {
                        missed++;
                        ContactGroupsGame.Logger?.Info("Contact groups check (search): '" + name + "' (category " + (int)contact.category + ") was not found: FAIL");
                    }
                    yield return null;
                }
                if (boxed)
                {
                    faults += Line("search", searched + " name(s) typed, " + missed + " contact(s) not found", missed == 0 && searched > 0);
                }
                else
                {
                    ContactGroupsGame.Logger?.Info("Contact groups check (search): SKIPPED, the search box is not in the app.");
                }

                bool ok = faults == 0;
                ContactGroupsGame.Logger?.Info("Contact groups check (all): " + total + " contact(s), " + groups + " group(s) with a button, " + faults + " fault(s): " + (ok ? "PASS" : "FAIL") + ".");
                if (ok)
                {
                    Say(NotificationType.Success, "reallife_contacts_test_groups_pass", new Dictionary<string, string>
                    {
                        { "contacts", total.ToString() }, { "groups", groups.ToString() }
                    });
                }
                else
                {
                    Say(NotificationType.Error, "reallife_contacts_test_groups_fail", Faults(faults));
                }
            }
            finally
            {
                _running = false;
                Restore(app, uis, shownBefore, opened);
            }
        }

        // The list as it was, and the phone closed again.
        private static void Restore(ContactsApp app, UIs uis, ContactCategoryName? shown, bool opened)
        {
            try
            {
                if (opened && app != null && app.isActiveAndEnabled)
                {
                    app.LoadContactsList(shown, false);
                }
                if (opened && uis != null && uis.fullMenu != null && FullMenu.IsOpen)
                {
                    uis.fullMenu.Toggle(false);
                }
            }
            catch (Exception e)
            {
                ContactGroupsGame.Logger?.Error(e);
            }
        }

        private static int Line(string what, string said, bool pass)
        {
            ContactGroupsGame.Logger?.Info("Contact groups check (" + what + "): " + said + ": " + (pass ? "PASS" : "FAIL"));
            return pass ? 0 : 1;
        }

        private static Dictionary<string, string> Faults(int faults)
        {
            return new Dictionary<string, string> { { "faults", faults.ToString() } };
        }

        private static void Say(NotificationType type, string key, Dictionary<string, string> data)
        {
            Notifications.Show(type, key, data, 8f, null, null, true, false);
        }
    }
}
