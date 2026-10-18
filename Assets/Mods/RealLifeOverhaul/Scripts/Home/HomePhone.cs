using System;
using BAModAPI;
using Dialogs;
using Entities;
using Localizor;
using UI.Smartphone.Apps.Contacts;

namespace RealLifeOverhaul
{
    /// <summary>The "Wellbeing" contact: the page of <see cref="HomeReport"/>, and from it the routines that need no furniture.</summary>
    public static class HomePhone
    {
        public const string ContactId = "reallife-health";
        public static CallDialogType DialogType => (CallDialogType)ModEnumHash.GetSafeHash("reallife_health_calldialogtype");

        public static void Setup()
        {
            if (!Settings.HomeLife || !Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                ContactPictures.Add(ContactId);   // the game's own logo shapes or portrait camera (Phone/Contacts)
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_health_desc", null, false, true);
                contact.callDialogTypeOverride = DialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(DialogType, Guard.Maker(() => new WellbeingDialog()));
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_health_welcome"), notify: false);
                }
            }
            catch (Exception e)
            {
                HomeDirector.Logger?.Error(e);
                HomeDirector.Logger?.Warn("Home: the Wellbeing contact could not be added; the page can still be opened from the options page.");
            }
        }

        public static void OpenInPerson()
        {
            CallDialogFactory.RegisterDialog(DialogType, Guard.Maker(() => new WellbeingDialog()));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(DialogType, NavigationBlocker.SpecialEmployeeDialog);
        }
    }

    /// <summary>The Wellbeing contact's call, or the same page face to face.</summary>
    public sealed class WellbeingDialog : Dialog
    {
        public WellbeingDialog()
        {
            npcNameKey = HomePhone.ContactId;
            if (!HomeState.Loaded || !Settings.HomeLife)
            {
                DialogController.current.ShowEntry(Guard.Entry(PhoneContacts.Closing("reallife_health_page_off")));
                return;
            }
            bool running = HomeDirector.Instance != null && HomeDirector.Instance.Jogging;
            DialogController.current.ShowEntry(Guard.Entry(new DialogEntry
            {
                messageData = HomeReport.Page(),
                ConfirmTextOverride = "reallife_health_menu_routines".Localize(),
                OnConfirm = Routines,
                SecondOptionTextOverride = running ? "reallife_health_menu_stop_run" : "reallife_health_menu_run",
                OnSecondOption = () => Ask(running ? HomeWish.StopRun : HomeWish.Run),
                OnCancel = PhoneContacts.HangUp
            }));
        }

        private static DialogEntry Routines()
        {
            return new DialogEntry
            {
                messageData = "reallife_health_menu_routines_text".Localize(),
                ConfirmTextOverride = "reallife_health_menu_tidy".Localize(),
                OnConfirm = () => Ask(HomeWish.Tidy),
                SecondOptionTextOverride = "reallife_health_menu_exercise",
                OnSecondOption = Exercises,
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static DialogEntry Exercises()
        {
            return new DialogEntry
            {
                messageData = "reallife_health_menu_exercise_text".Localize(),
                ConfirmTextOverride = "reallife_health_menu_stretch".Localize(),
                OnConfirm = () => Ask(HomeWish.Stretch),
                SecondOptionTextOverride = "reallife_health_menu_pushups",
                OnSecondOption = () => Ask(HomeWish.PushUps),
                CancelTextOverride = "reallife_health_menu_situps",
                OnCancel = () =>
                {
                    HomeDirector.Instance?.Wish(HomeWish.SitUps);
                    PhoneContacts.HangUp();
                }
            };
        }

        // The routine starts once the conversation and the phone are out of the way: the director sees to it.
        private static DialogEntry Ask(HomeWish wish)
        {
            HomeDirector.Instance?.Wish(wish);
            return PhoneContacts.Closing("reallife_health_menu_ok");
        }
    }
}
