using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Dialogs;
using Entities;
using Extensions;
using Localizor;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The career on the phone: the Work contact, whose call carries the rota, clocking on, sick calls, notice, booked sets
    /// and the CV (<see cref="CareerDialog"/>), and whose texts carry reminders and letters. The same conversation face to
    /// face with a boss is where the player signs on and is reviewed.
    /// </summary>
    public static class CareerPhone
    {
        public const string ContactId = "reallife-career";
        private const string DialogName = "reallife_career_calldialogtype";

        private static Contact _contact;
        private static bool _tried;

        /// <summary>True once the contact is there, or adding it has been tried with the options as they are.</summary>
        public static bool Ready => _contact != null || _tried || !Settings.PhoneContacts;

        /// <summary>The employer whose boss the next dialogue is with; -1 for a phone call. Read once by the dialogue.</summary>
        internal static int At = -1;

        public static void Setup()
        {
            _contact = null;
            _tried = Settings.Jobs && Settings.Careers && Settings.PhoneContacts;
            if (!Settings.Jobs || !Settings.Careers || !Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                ContactPictures.Add(ContactId);   // the game's own logo shapes or portrait camera (Phone/Contacts)
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_career_desc", null, false, true);
                var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogName);
                contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new CareerDialog()));
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_career_welcome"), notify: false);
                }
                _contact = contact;
            }
            catch (Exception e)
            {
                JobDirector.Logger?.Error(e);
                JobDirector.Logger?.Warn("Careers: the Work contact could not be added; employers can still be spoken to at their boards.");
            }
        }

        /// <summary>
        /// RLO Texts (Wiring/TextsPhone): true when a text about to go out (from whom, the line's key, its values, how to send)
        /// is held for the model's words and sent later through the last argument. Null or false: sent now, as written.
        /// </summary>
        public static System.Func<string, string, Dictionary<string, string>, System.Action<string, Dictionary<string, string>>, bool> HoldText;

        /// <summary>A letter or reminder from work, as a text of the Work contact; nothing without the contact.</summary>
        public static void Text(string key, Dictionary<string, string> data)
        {
            if (_contact == null)
            {
                return;
            }
            try
            {
                // A copy: the game keeps the message's data for as long as the message is listed.
                var copy = data != null ? new Dictionary<string, string>(data) : null;
                Contact contact = _contact;
                if (HoldText == null || !HoldText("boss", key, copy, (k, d) => Send(contact, k, d)))
                {
                    _contact.SendMessage(new TextMessage(key, copy), notify: true);
                }
            }
            catch (Exception e)
            {
                JobDirector.Logger?.Error(e);
            }
        }

        // A text held for its words (RLO Texts), sent now.
        private static void Send(Contact contact, string key, Dictionary<string, string> data)
        {
            try
            {
                contact.SendMessage(new TextMessage(key, data), notify: true);
            }
            catch (Exception e)
            {
                JobDirector.Logger?.Error(e);
            }
        }

        /// <summary>Face to face with an employer's boss, in the game's own dialogue panel.</summary>
        public static void OpenInPerson(int employer)
        {
            At = employer;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogName);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new CareerDialog()));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }
    }
}
