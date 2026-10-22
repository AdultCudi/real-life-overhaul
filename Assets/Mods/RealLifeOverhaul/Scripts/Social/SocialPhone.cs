using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using UI.Smartphone.Apps.Contacts;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The social life's contacts: everybody met, the "People" page and the City Ledger's desk. Made as Phone/PhoneContacts
    /// makes its own, so a save loads without the mod; the game does not save a call dialog, so it is set on every load.
    /// </summary>
    public static class SocialPhone
    {
        public const string PeopleId = "reallife-people";
        public const string LedgerId = "reallife-ledger";
        private const string MomId = "reallife-mom";
        private const string FriendId = "reallife-friend";

        // One call dialog type per person, "reallife_socialp<index>_calldialogtype": the game's factory makes a dialog by its
        // type, and each must know who it is. Hashed once a slot; 0 until then (the game's hash is never under 2001).
        private static readonly int[] DialogTypes = new int[SocialRules.MaxCast];

        /// <summary>The index of whoever is ringing the player now, or -1, for the dialog the call opens.</summary>
        public static int Incoming = -1;

        /// <summary>Set by Wiring/LedgerLinks; null while the desk is shut.</summary>
        public static Func<DialogEntry> LedgerDesk;

        /// <summary>Set by Wiring/FamilyLinks: whether this person's call offers "Family matters" (the partner's: time apart, the in-laws), and what it opens.</summary>
        public static Func<SocialPerson, bool> HasFamilyMatters;
        public static Func<DialogEntry> FamilyMatters;

        public static string IdOf(SocialPerson person)
        {
            return person.Origin == SocialOrigin.Family ? MomId : person.Origin == SocialOrigin.OldFriend ? FriendId : person.Name;
        }

        /// <summary>Once the cast is loaded, and when an option changes: every picture before the phone is first opened.</summary>
        public static void Setup(IModLogger logger)
        {
            Incoming = -1;
            if (!SocialState.Loaded || SaveGameManager.Current == null)
            {
                return;
            }
            try
            {
                if (!Settings.Social)
                {
                    Restore(PhoneContacts.Mom, "reallife_mom_calldialogtype");
                    Restore(PhoneContacts.Friend, "reallife_friend_calldialogtype");
                }
                else
                {
                    for (int i = 0; i < SocialState.Count; i++)
                    {
                        SocialPerson person = SocialState.Get(i);
                        if (person == null)
                        {
                            continue;
                        }
                        Picture(person);
                        if (person.Met)
                        {
                            Add(person, person.Origin > SocialOrigin.OldFriend);
                        }
                    }
                    ContactPictures.Add(PeopleId);
                    Page(PeopleId, "reallife_social_people_desc", "reallife_people_calldialogtype", () => new PeopleDialog(), "reallife_social_people_welcome");
                }
                if (Settings.SocialNewspaper)
                {
                    ContactPictures.Add(LedgerId);
                    Page(LedgerId, "reallife_social_ledger_desc", "reallife_ledger_calldialogtype", () => new LedgerDialog(), "reallife_social_ledger_welcome");
                }
                var scene = (CallDialogType)ModEnumHash.GetSafeHash("reallife_socialscene_calldialogtype");
                CallDialogFactory.RegisterDialog(scene, Guard.Maker(() => new SceneDialog()));
            }
            catch (Exception e)
            {
                logger?.Error(e);
                logger?.Warn("Social: the phone contacts could not be set up (see the error above); people can still be met and plans still happen.");
            }
        }

        /// <summary>Their own face on the contact, as SocialCast dresses them: for everybody of the cast, met or not.</summary>
        public static void Picture(SocialPerson person)
        {
            ContactPictures.Person(IdOf(person), person.Female, person.Age, person.Seed);
        }

        /// <summary>The contact of somebody who has been met, made again if the player deleted it. Null for a stranger.</summary>
        public static Contact ContactOf(SocialPerson person)
        {
            if (person == null || !person.Met || SaveGameManager.Current == null)
            {
                return null;
            }
            return Add(person, false);
        }

        /// <summary>
        /// A person's thread as the game keeps it (Contact.messagesQueue, its twenty newest), oldest first, as a call back reads
        /// it (SocialFollowRules): each message's written line's key (RLO Texts' words: the key they stand for), values, game
        /// minute and whether the player wrote it. Every text of theirs is here, whichever feature sent it. Never makes the
        /// contact: one the player deleted has no thread. COST: a pass over the save's contacts and one thread; the list's
        /// slots and nothing else (the queue's own enumerator is a struct).
        /// </summary>
        public static void Thread(SocialPerson person, List<SocialThreadLine> into)
        {
            into.Clear();
            Contact contact = Existing(person);
            Queue<TextMessage> messages = contact != null ? contact.messagesQueue : null;
            if (messages == null)
            {
                return;
            }
            foreach (TextMessage m in messages)
            {
                if (m != null && m.timestamp != null)
                {
                    into.Add(new SocialThreadLine(SocialFollowRules.KeyOf(m.messageKey, m.messageData), m.messageData, (int)m.timestamp.GetTotalMinutes(), m.isFromPlayer));
                }
            }
        }

        private static readonly List<SocialThreadLine> Scratch = new List<SocialThreadLine>(Contact.MaxMessages);

        /// <summary>
        /// The game day of the newest text a person sent, from any feature (the family's and life's texts are not the social
        /// life's own, so its LastTextDay does not have them); -1 for none. COST: as <see cref="Thread"/>.
        /// </summary>
        public static int LastTextDay(SocialPerson person)
        {
            Thread(person, Scratch);
            int day = -1;
            for (int i = Scratch.Count - 1; i >= 0; i--)
            {
                if (!Scratch[i].Player && Scratch[i].Key != null)
                {
                    day = SocialFollowRules.DayOf(Scratch[i].Minute);
                    break;
                }
            }
            Scratch.Clear();
            return day;
        }

        // The contact of somebody met as the save has it, never made anew (ContactOf makes one again): by its id and the
        // description the social life gives it, else the first of that id (the game keeps no description in a save).
        private static Contact Existing(SocialPerson person)
        {
            GameInstance save = SaveGameManager.Current;
            if (person == null || !person.Met || save == null || save.Contacts == null)
            {
                return null;
            }
            string id = IdOf(person), description = SocialCatalogue.OriginDesc[(int)person.Origin];
            Contact first = null;
            List<Contact> all = save.Contacts;
            for (int i = 0; i < all.Count; i++)
            {
                Contact c = all[i];
                if (c == null || c.id != id)
                {
                    continue;
                }
                if (c.description == description)
                {
                    return c;
                }
                first = first ?? c;
            }
            return first;
        }

        /// <summary>Callable, with the first message of their thread if it is empty.</summary>
        public static Contact Add(SocialPerson person, bool welcome)
        {
            Contact contact = Contact.GetContact(IdOf(person), ContactCategoryName.General, SocialCatalogue.OriginDesc[(int)person.Origin], null, false, true);
            var dialogType = (CallDialogType)DialogType(person.Index);
            contact.callDialogTypeOverride = dialogType;
            int index = person.Index;
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new PersonDialog(index)));
            if (welcome && (contact.messagesQueue == null || contact.messagesQueue.Count == 0))
            {
                contact.SendMessage(new TextMessage(SocialCatalogue.OriginWelcome[(int)person.Origin], SocialLife.Data(person)), notify: false);
            }
            return contact;
        }

        // The first eight's are the types every version has registered, so a contact's call opens the same dialog.
        private static int DialogType(int index)
        {
            if (DialogTypes[index] == 0)
            {
                DialogTypes[index] = ModEnumHash.GetSafeHash(SocialRules.DialogTypeName(index));
            }
            return DialogTypes[index];
        }

        /// <summary>A payment, a subscription stopped: with the game's notification.</summary>
        public static void LedgerMessage(string key, Dictionary<string, string> data)
        {
            try
            {
                if (SaveGameManager.Current == null || !Settings.SocialNewspaper || string.IsNullOrEmpty(key))
                {
                    return;
                }
                ContactPictures.Add(LedgerId);
                Contact ledger = Page(LedgerId, "reallife_social_ledger_desc", "reallife_ledger_calldialogtype", () => new LedgerDialog(), null);
                ledger.SendMessage(new TextMessage(key, data), notify: true, sendNotificationInstantly: true);
            }
            catch (Exception e)
            {
                SocialDirector.Logger?.Error(e);
            }
        }

        /// <summary>The game's own way: the phone open on their contact. False if it could not.</summary>
        public static bool Call(SocialPerson person)
        {
            Contact contact = ContactOf(person);
            if (contact == null)
            {
                return false;
            }
            ContactsApp.OpenAndStartCall(contact);
            return true;
        }

        /// <summary>In the game's own dialogue panel, as its counters and desks open one.</summary>
        public static void OpenInPerson(ThirdPersonCharacter with, Action onClosed)
        {
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash("reallife_socialscene_calldialogtype");
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new SceneDialog()));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog, null, Guard.Way(onClosed), with);
        }

        public static bool DialogOpen
        {
            get
            {
                var uis = InstanceBehavior<UI.UIs>.Instance;
                return uis != null && uis.playerHUD != null && uis.playerHUD.dialogUI != null && uis.playerHUD.dialogUI.isPanelOpen;
            }
        }

        private static Contact Page(string id, string description, string dialogTypeName, Func<Dialog> dialog, string welcomeKey)
        {
            Contact contact = Contact.GetContact(id, ContactCategoryName.General, description, null, false, true);
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(dialogTypeName);
            contact.callDialogTypeOverride = dialogType;
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(dialog));
            if (welcomeKey != null && (contact.messagesQueue == null || contact.messagesQueue.Count == 0))
            {
                contact.SendMessage(new TextMessage(welcomeKey), notify: false);
            }
            return contact;
        }

        private static void Restore(Contact contact, string dialogTypeName)
        {
            if (contact != null)
            {
                contact.callDialogTypeOverride = (CallDialogType)ModEnumHash.GetSafeHash(dialogTypeName);
            }
        }
    }

    /// <summary>A call to the City Ledger: its subscriptions desk (Scripts/Ledger, through <see cref="SocialPhone.LedgerDesk"/>).</summary>
    public sealed class LedgerDialog : Dialog
    {
        public LedgerDialog()
        {
            npcNameKey = SocialPhone.LedgerId;
            DialogEntry first = null;
            try
            {
                first = Settings.SocialNewspaper && SocialPhone.LedgerDesk != null ? SocialPhone.LedgerDesk() : null;
            }
            catch (Exception e)
            {
                SocialDirector.Logger?.Error(e);
            }
            DialogController.current.ShowEntry(Guard.Entry(first ?? PhoneContacts.Closing("reallife_ledger_desk_later")));
        }
    }
}
