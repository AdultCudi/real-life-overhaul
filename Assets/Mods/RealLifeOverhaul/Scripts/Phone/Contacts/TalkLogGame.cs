using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Entities;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The only place that touches the game for the contacts' talk history (<see cref="TalkLog"/>). The game keeps a contact's
    /// texts and nothing of a call, so what the Contacts thread showed of a conversation was the line on the card now and then
    /// nothing. Every page one of the mod's dialogs shows (Guard.Entry) while a contact's call runs is kept as that contact's
    /// line, and the player's replies and typed words as the player's (PersonDialog); the kit's thread reads the lines back
    /// in order beside the game's texts. It makes no difference who the contact is: the people of the social life, family,
    /// work, services and the rest all talk through the same pages, and what an NPC texts the player is already the game's own
    /// text. The log is read from the save's line when first asked in a
    /// city, written with the save, and forgets a contact the player deleted. Cost: a reference comparison to see the save; a
    /// page or a reply is one short string kept on an event; nothing per frame.
    /// </summary>
    public static class TalkLogGame
    {
        private static readonly FeatureGuard Fault = Guard.For("phone", "TalkLogGame", typeof(TalkLogGame));

        private static readonly ConditionalWeakTable<DialogEntry, object> OwnPages = new ConditionalWeakTable<DialogEntry, object>();
        private static readonly object Mark = new object();
        private static TalkLog _log = new TalkLog();
        private static GameInstance _save;      // the save the log was read for
        private static Action _onSave;          // made once: the save hook keeps its reference

        /// <summary>The log's changes, 0 outside a city: the Contacts screen looks again when this moves.</summary>
        public static int Changes => Ready() ? _log.Changes : 0;

        /// <summary>
        /// A page one of the mod's dialogs is about to show (Guard.Entry): when a contact's call runs, its words are the contact's
        /// line. Forms, empty pages, pages with no contact (a desk, a street talk) and the player's own words are left out.
        /// </summary>
        public static void Page(DialogEntry page)
        {
            if (page == null || page.Template != DialogEntry.TemplateType.Text || string.IsNullOrEmpty(page.messageData.Key) || Fault.Off)
            {
                return;
            }
            try
            {
                DialogController dialog = DialogController.current;
                Contact contact = dialog != null ? dialog.contact : null;
                if (contact == null || OwnPages.TryGetValue(page, out _))
                {
                    return;
                }
                Keep(contact, false, ContactsRules.Plain(page.messageData.ToString()));
            }
            catch (Exception e)
            {
                Guard.Fail(Fault, e);
            }
        }

        /// <summary>The player's own words in the call that runs with this contact (a reply chosen, words typed).</summary>
        public static void Said(string words)
        {
            if (Fault.Off)
            {
                return;
            }
            try
            {
                DialogController dialog = DialogController.current;
                Keep(dialog != null ? dialog.contact : null, true, words);
            }
            catch (Exception e)
            {
                Guard.Fail(Fault, e);
            }
        }

        /// <summary>The page shows the player's own words, kept by <see cref="Said"/>: <see cref="Page"/> leaves it out.</summary>
        public static DialogEntry Own(DialogEntry page)
        {
            if (page != null)
            {
                OwnPages.Remove(page);
                OwnPages.Add(page, Mark);
            }
            return page;
        }

        /// <summary>A contact's history, oldest first, or null for none. The list is the log's own: read it, never change it.</summary>
        public static List<TalkLine> Lines(Contact contact)
        {
            return contact != null && Ready() ? _log.Of(contact.id) : null;
        }

        /// <summary>True when a contact's history is as long as it is kept, so the start of the conversation may be gone.</summary>
        public static bool Full(Contact contact) => contact != null && Ready() && _log.Full(contact.id);

        /// <summary>Forgets a contact's history (the chat was cleared).</summary>
        public static void Clear(Contact contact)
        {
            if (contact != null && Ready() && _log.Clear(contact.id))
            {
                Guard.Logger?.Info("Contacts: the talk history of " + contact.id + " was cleared with its chat.");
            }
        }

        private static void Keep(Contact contact, bool mine, string words)
        {
            if (contact != null && !string.IsNullOrEmpty(words) && Ready())
            {
                _log.Add(contact.id, (int)TimeHelper.NowInMinutes(), mine, words);
            }
        }

        // The log of the save loaded now, read the first time it is asked after a load. False outside a city.
        private static bool Ready()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return false;
            }
            if (!ReferenceEquals(save, _save))
            {
                Load(save);
            }
            return true;
        }

        private static void Load(GameInstance save)
        {
            _save = save;
            string line = null;
            TalkLog read = save.modData != null && save.modData.TryGetValue(TalkLogStore.Key, out line) ? TalkLogStore.Read(line) : new TalkLog();
            if (read == null)
            {
                SaveLines.Unreadable(save.modData, TalkLogStore.Key, "it could not be read");   // put back as read at every save
                Guard.Logger?.Warn("Contacts: the save's talk history could not be read (" + TalkLogStore.Key + "); it starts again and the line is kept in the save as it is.");
                read = new TalkLog();
            }
            _log = read;
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the game clears its save event on every load: kept attached here
        }

        private static void OnSaveGame()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || !ReferenceEquals(save, _save) || save.modData == null)
            {
                return;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            List<Contact> all = save.Contacts;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null)
                {
                    ids.Add(TalkLog.IdOf(all[i].id));
                }
            }
            _log.Keep(ids);   // a contact the player deleted takes its history with it
            if (_log.PeopleCount == 0)
            {
                save.modData.Remove(TalkLogStore.Key);
            }
            else
            {
                save.modData[TalkLogStore.Key] = TalkLogStore.Write(_log);
            }
        }
    }
}
