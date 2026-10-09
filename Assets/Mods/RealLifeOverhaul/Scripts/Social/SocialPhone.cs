using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The social life's side of the phone: a contact for everybody the player has met (Mom and
    /// Alex keep the contacts they always had and get the new conversation), a "People" contact
    /// that reads out how things stand with each of them, and the "City Ledger", which sends the
    /// morning paper as messages and reads it out when called.
    ///
    /// Contacts are made exactly as Phone/PhoneContacts makes its own: the game's own Contact,
    /// in the save's own contact list, with an id and a description key and nothing else. A
    /// save made with them loads without the mod (the names of people are their contact ids, so
    /// they still read as names). The call dialog is not saved by the game, so it is set again
    /// on every load.
    /// </summary>
    public static class SocialPhone
    {
        public const string PeopleId = "reallife-people";
        public const string LedgerId = "reallife-ledger";
        private const string MomId = "reallife-mom";
        private const string FriendId = "reallife-friend";

        // One call dialog type per person of the cast: the game's factory hands out a dialog per type, and each must know who it is.
        private static readonly string[] DialogTypes =
        {
            "reallife_socialp0_calldialogtype", "reallife_socialp1_calldialogtype", "reallife_socialp2_calldialogtype", "reallife_socialp3_calldialogtype",
            "reallife_socialp4_calldialogtype", "reallife_socialp5_calldialogtype", "reallife_socialp6_calldialogtype", "reallife_socialp7_calldialogtype"
        };

        // Kept alive for the whole session: the game caches contact icons by sprite name.
        private static readonly List<Sprite> Icons = new List<Sprite>();

        /// <summary>Who is ringing the player right now (their index), or -1. Read by the dialog the call opens.</summary>
        public static int Incoming = -1;

        /// <summary>True once this save's contacts have been set up with the social life on.</summary>
        public static bool On { get; private set; }

        public static string IdOf(SocialPerson person)
        {
            return person.Origin == SocialOrigin.Family ? MomId : person.Origin == SocialOrigin.OldFriend ? FriendId : person.Name;
        }

        /// <summary>
        /// Called once the cast is loaded, and again whenever the option changes: icons for the
        /// whole cast (the game reads its icon list once, so all of them go in before the phone
        /// is first opened), a contact for whoever has been met, the two pages.
        /// </summary>
        public static void Setup(IModLogger logger)
        {
            On = false;
            Incoming = -1;
            if (!SocialState.Loaded || SaveGameManager.Current == null)
            {
                return;
            }
            try
            {
                if (!Settings.Social)
                {
                    // Mom and Alex go back to the short calls they had before.
                    Restore(PhoneContacts.Mom, "reallife_mom_calldialogtype");
                    Restore(PhoneContacts.Friend, "reallife_friend_calldialogtype");
                }
                else
                {
                    for (int i = 0; i < SocialRules.CastSize; i++)
                    {
                        SocialPerson person = SocialState.Get(i);
                        if (person == null)
                        {
                            continue;
                        }
                        AddIcon(IdOf(person), Tint(person.Seed));
                        if (person.Met)
                        {
                            Add(person, person.Origin > SocialOrigin.OldFriend);
                        }
                    }
                    AddIcon(PeopleId, new Color(0.96f, 0.52f, 0.42f));
                    Page(PeopleId, "reallife_social_people_desc", "reallife_people_calldialogtype", () => new PeopleDialog(), "reallife_social_people_welcome");
                    On = true;
                }
                if (Settings.SocialNewspaper)
                {
                    AddIcon(LedgerId, new Color(0.25f, 0.27f, 0.32f));
                    Page(LedgerId, "reallife_social_ledger_desc", "reallife_ledger_calldialogtype", () => new LedgerDialog(), "reallife_social_ledger_welcome");
                }
                var scene = (CallDialogType)ModEnumHash.GetSafeHash("reallife_socialscene_calldialogtype");
                CallDialogFactory.RegisterDialog(scene, () => new SceneDialog());
            }
            catch (Exception e)
            {
                logger?.Error(e);
                logger?.Warn("Social: the phone contacts could not be set up (see the error above); people can still be met and plans still happen.");
            }
        }

        /// <summary>The contact of somebody who has been met, made if the player deleted it. Null for a stranger.</summary>
        public static Contact ContactOf(SocialPerson person)
        {
            if (person == null || !person.Met || SaveGameManager.Current == null)
            {
                return null;
            }
            return Add(person, false);
        }

        /// <summary>Puts somebody in the phone, callable, with the first message of their thread if it is empty.</summary>
        public static Contact Add(SocialPerson person, bool welcome)
        {
            Contact contact = Contact.GetContact(IdOf(person), ContactCategoryName.General, SocialCatalogue.OriginDesc[(int)person.Origin], null, false, true);
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypes[person.Index]);
            contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
            int index = person.Index;
            CallDialogFactory.RegisterDialog(dialogType, () => new PersonDialog(index));
            if (welcome && (contact.messagesQueue == null || contact.messagesQueue.Count == 0))
            {
                contact.SendMessage(new TextMessage(SocialCatalogue.OriginWelcome[(int)person.Origin], SocialLife.Data(person)), notify: false);
            }
            return contact;
        }

        /// <summary>The morning paper, as messages from the City Ledger: one notification for the lot.</summary>
        public static void DeliverPaper(IModLogger logger)
        {
            try
            {
                if (SaveGameManager.Current == null || News.Today.Count == 0)
                {
                    return;
                }
                AddIcon(LedgerId, new Color(0.25f, 0.27f, 0.32f));
                Contact ledger = Page(LedgerId, "reallife_social_ledger_desc", "reallife_ledger_calldialogtype", () => new LedgerDialog(), null);
                for (int i = 0; i < News.Today.Count; i++)
                {
                    NewsItem item = News.Today[i];
                    // (A copy of the data: the game keeps what a message is given, and the paper keeps its own.)
                    ledger.SendMessage(new TextMessage(item.Key, item.Data != null ? new Dictionary<string, string>(item.Data) : null), notify: i == 0, sendNotificationInstantly: i == 0);
                }
            }
            catch (Exception e)
            {
                logger?.Error(e);
            }
        }

        /// <summary>Opens the phone on somebody's contact and starts the call, the game's own way. False if it could not.</summary>
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

        /// <summary>The face-to-face conversation of a scene, in the game's own dialogue panel (the way its counters and desks open one).</summary>
        public static void OpenInPerson(ThirdPersonCharacter with, Action onClosed)
        {
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash("reallife_socialscene_calldialogtype");
            CallDialogFactory.RegisterDialog(dialogType, () => new SceneDialog());
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog, null, onClosed, with);
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
            CallDialogFactory.RegisterDialog(dialogType, dialog);
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

        // A colour of their own for each person, steady for the save: from the seed of their look.
        private static Color Tint(int seed)
        {
            float hue = (seed & 0xFFFF) / 65535f;
            return Color.HSVToRGB(hue, 0.55f, 0.82f);
        }

        /// <summary>The social life's badge: the People page's picture, and the icon of a meeting place on the city map.</summary>
        public static Sprite Badge => Disc(PeopleId, new Color(0.96f, 0.52f, 0.42f));

        // A plain coloured disc with a white middle, like the mod's other contacts; the game looks icons up by sprite name == contact id.
        private static void AddIcon(string id, Color colour)
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (references == null || references.contactIcons == null)
            {
                return;
            }
            foreach (Sprite existing in references.contactIcons)
            {
                if (existing != null && existing.name == id)
                {
                    return;
                }
            }
            Sprite sprite = Disc(id, colour);
            Sprite[] old = references.contactIcons;
            var icons = new Sprite[old.Length + 1];
            old.CopyTo(icons, 0);
            icons[old.Length] = sprite;
            references.contactIcons = icons;
        }

        private static Sprite Disc(string id, Color colour)
        {
            Sprite sprite = Icons.Find(s => s != null && s.name == id);
            if (sprite == null)
            {
                const int size = 96;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                float radius = size / 2f - 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                        Color pixel = distance < radius * 0.42f ? Color.white : colour;
                        pixel.a = Mathf.Clamp01(radius - distance);
                        pixels[y * size + x] = pixel;
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                sprite.name = id;
                Icons.Add(sprite);
            }
            return sprite;
        }
    }
}
