using System;
using System.Collections;
using System.Collections.Generic;
using AI.Employees.SalaryNegotiation;
using Entities;
using Helpers;
using UI;
using UI.Notification;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of Contacts (Options, Tests): a call on the kit (Uncle Fred, or the mod's own Mom or
    /// friend), a message with an offer to answer (from the mod's own friend), the tax table (a notice drawn in the tax
    /// office's thread from the figures of the period so far, kept in memory only) and a wage to negotiate (the first
    /// candidate waiting for one). Each opens the game's own app through the game's own methods, and says in a notice
    /// exactly why it cannot run when it cannot. Only the offer's message stays in the friend's thread, as any of the
    /// friend's messages would; nothing else is saved.
    /// </summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] Callers = { "uncle_fred", "reallife-mom", "reallife-friend" };   // contacts with a call of their own, in order

        /// <summary>Options, Tests: a call drawn as the call card, through the game's own OpenAndStartCall.</summary>
        public void TestContactsCall()
        {
            try
            {
                if (!ContactsReady())
                {
                    return;
                }
                Contact caller = FirstContact(Callers);
                if (caller == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_contacts_no_call", null, 8f);
                    return;
                }
                ContactsApp.OpenAndStartCall(caller);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: a message from the mod's own friend with two replies, opened in the thread.</summary>
        public void TestContactsOffer()
        {
            try
            {
                if (!ContactsReady())
                {
                    return;
                }
                Contact friend = PhoneContacts.Friend;
                if (friend == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_contacts_no_friend", null, 8f);
                    return;
                }
                var replies = new AdditionalMessageData
                {
                    contextButtonData = new List<TextMessage.ContextButtonData>
                    {
                        new TextMessage.ContextButtonData("reallife_ui_test_offer_yes", ContextButton.BackgroundColor.blue,
                            Guard.Listener(() => Answer(friend, "reallife_ui_test_offer_yes", "reallife_ui_test_offer_yes_reply"))),
                        new TextMessage.ContextButtonData("reallife_ui_test_offer_no", ContextButton.BackgroundColor.gray,
                            Guard.Listener(() => Answer(friend, "reallife_ui_test_offer_no", "reallife_ui_test_offer_no_reply"))),
                    }
                };
                friend.SendMessage(new TextMessage("reallife_ui_test_offer_text", null, false, false, false, replies), false);
                OpenContact(friend);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the tax office's thread with a notice drawn from the period so far, in memory only.</summary>
        public void TestContactsTax()
        {
            try
            {
                if (!ContactsReady())
                {
                    return;
                }
                Contact office = TaxHelper.GetIRSContact();
                int day = SaveGameManager.Current.Day, year = SaveGameManager.Current.gameVariables.daysPerYear;
                var taxes = new Taxes
                {
                    day = day, taxPercentage = SaveGameManager.Current.gameVariables.taxPercentage, dueDay = day + 20,
                    businessesIncome = TaxCalculationHelper.GetBusinessIncome(day - year + 1, day),
                    estateTaxes = TaxCalculationHelper.GetRealEstateTaxes(day - year + 1, day, year),
                    deductibleExpenses = new List<(string, float)>(), subtotalGamblingWinnings = (float)TaxCalculationHelper.GetCurrentTaxableGamblingWinnings()
                };
                TaxCalculationHelper.ApplyRoundedTotals(taxes);
                ContactsKit.Sample = new TextMessage("ba:messagetype_taxes", null, true, false, false, new AdditionalMessageData { taxes = taxes });
                ContactsKit.SampleFor = office;
                OpenContact(office);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the first candidate waiting to talk about their wage, their offer opened in the thread.</summary>
        public void TestContactsWage()
        {
            try
            {
                if (!ContactsReady())
                {
                    return;
                }
                Contact candidate = Negotiating();
                if (candidate == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_contacts_no_wage", null, 8f);
                    return;
                }
                OpenContact(candidate);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The interface and Contacts' own switch on, the phone built, the kit standing over it; or a notice that says why not.
        private bool ContactsReady()
        {
            if (!Ready())
            {
                return false;
            }
            if (!UiScreenSwitches.On("contacts"))
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_contacts_off", null, 8f);
                return false;
            }
            ContactsKit.Install();
            if (!ContactsKitReady() || SaveGameManager.Current == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                return false;
            }
            return true;
        }

        // The app opened on a contact as the game opens it from elsewhere (its last contact, then its list's own button).
        private void OpenContact(Contact contact)
        {
            SaveGameManager.Current.PlayerDefaults.contactsLastName = contact.id;
            SaveGameManager.Current.PlayerDefaults.contactsLastCategoryName = contact.category;
            InstanceBehavior<UIs>.Instance.fullMenu.ShowApp(AppName.Contacts);
            StartCoroutine(Guard.Routine(OpenSoon(contact)));
        }

        private static IEnumerator OpenSoon(Contact contact)
        {
            yield return new WaitForSecondsRealtime(MenuSettles);
            ContactsApp app = InstanceBehavior<UIs>.Instance != null ? InstanceBehavior<UIs>.Instance.fullMenu.contactsApp : null;
            if (app != null && app.selectedContact != contact)
            {
                app.OpenAppWithContact(contact);
            }
        }

        // A reply pressed in the test's message: said as the player, answered by the friend.
        private static void Answer(Contact friend, string said, string reply)
        {
            friend.ReceivePlayerMessage(new TextMessage(said, null, true));
            friend.SendMessage(new TextMessage(reply, null, true), false);
        }

        private static Contact FirstContact(string[] ids)
        {
            List<Contact> all = SaveGameManager.Current.Contacts;
            foreach (string id in ids)
            {
                Contact found = all.Find(c => c != null && c.id == id);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        // The contact of the first candidate whose wage talk is still open, as the game names a candidate's contact.
        private static Contact Negotiating()
        {
            foreach (CandidateSalaryNegotiation n in SaveGameManager.Current.candidateSalaryNegotiations)
            {
                if (n == null || n.completed || n.employeeInstance == null)
                {
                    continue;
                }
                string name = n.employeeInstance.characterData.name;
                Contact found = SaveGameManager.Current.Contacts.Find(c => c != null && c.IsEmployeeContact && c.id == name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
