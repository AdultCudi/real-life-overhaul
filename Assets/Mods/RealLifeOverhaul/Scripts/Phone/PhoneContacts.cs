using System;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using Dialogs;
using Entities;
using Localizor;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Adds callable phone contacts. In the base game most contacts are text-only; these each
    /// get a call button and their own conversation.
    /// </summary>
    public static class PhoneContacts
    {
        private const string EmergencyId = "reallife-911";
        private const string DoctorId = "reallife-doctor";
        private const string FriendId = "reallife-friend";
        private const string MomId = "reallife-mom";

        public static IModLogger Logger;
        public static Contact Friend { get; private set; }
        public static Contact Mom { get; private set; }

        public static void Setup()
        {
            Friend = null;
            Mom = null;
            if (!Settings.PhoneContacts)
            {
                return;
            }

            ContactPictures.Add(EmergencyId);
            ContactPictures.Add(DoctorId);
            ContactPictures.Add(FriendId);
            ContactPictures.Add(MomId);

            Register(EmergencyId, "reallife_contact_911_desc", "reallife_911_calldialogtype", () => new EmergencyDialog(), "reallife_text_911_welcome");
            Register(DoctorId, "reallife_contact_doctor_desc", "reallife_doctor_calldialogtype", () => new DoctorCallDialog(), "reallife_text_doctor_welcome");
            Friend = Register(FriendId, "reallife_contact_friend_desc", "reallife_friend_calldialogtype", () => new FriendDialog(), "reallife_text_friend_welcome");
            Mom = Register(MomId, "reallife_contact_mom_desc", "reallife_mom_calldialogtype", () => new MomDialog(), "reallife_text_mom_welcome");
            ContactSearch.Install();   // a search box in the game's Contacts app (Phone/Contacts)
            Logger?.Info("Phone contacts registered.");
        }

        private static Contact Register(string id, string descriptionKey, string dialogTypeName, Func<Dialog> dialog, string welcomeKey)
        {
            Contact contact = Contact.GetContact(id, ContactCategoryName.General, descriptionKey, null, false, true);
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(dialogTypeName);
            contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
            CallDialogFactory.RegisterDialog(dialogType, dialog);
            if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
            {
                contact.SendMessage(new TextMessage(welcomeKey), notify: false);
            }
            return contact;
        }

        /// <summary>A closing line: on the phone the call ends by itself, in person there's a leave button.</summary>
        internal static DialogEntry Closing(string key, object arguments = null)
        {
            bool phone = DialogController.current.dialogType == DialogType.PhoneCall;
            return new DialogEntry
            {
                messageData = key.Localize(arguments),
                OnVisible = phone ? DialogController.current.FinishDialog : (Action)null,
                OnCancel = phone ? (Action)null : DialogController.current.FinishDialog
            };
        }

        internal static void HangUp()
        {
            DialogController.current.CancelDialog();
        }
    }

    public class EmergencyDialog : Dialog
    {
        public EmergencyDialog()
        {
            npcNameKey = "reallife-911";
            DialogController.current.ShowEntry(new DialogEntry
            {
                messageData = "reallife_911_greeting".Localize(),
                ConfirmTextOverride = (Events.EventDirector.FireBurning ? "reallife_911_report_fire" : ReportLabel(CrimeDirector.Reportable())).Localize(),
                OnConfirm = Report,
                SecondOptionTextOverride = "reallife_911_ambulance",
                OnSecondOption = AskAmbulance,
                OnCancel = PhoneContacts.HangUp
            });
        }

        // The first choice names whatever is actually happening near the caller.
        private static string ReportLabel(ReportKind kind)
        {
            switch (kind)
            {
                case ReportKind.Accident: return "reallife_911_report";
                case ReportKind.Theft: return ShopFloor.IsOwnShop ? "reallife_911_report_theft_store" : "reallife_911_report_theft";
                case ReportKind.Robbery: return "reallife_911_report_robbery";
                case ReportKind.Traffic: return "reallife_911_report_driver";
                case ReportKind.Crime: return "reallife_911_report_crime";
                default: return "reallife_911_report_incident";
            }
        }

        // Asked again when the choice is made: the scene may have ended while the phone rang.
        private static DialogEntry Report()
        {
            if (Events.EventDirector.FireBurning)
            {
                // A fire (Scripts/Events): the engine is sent at once; a second call is only noted.
                return PhoneContacts.Closing(Events.EventDirector.Report911() ? "reallife_911_dispatched_fire" : "reallife_911_noted");
            }
            ReportKind kind = CrimeDirector.Reportable();
            if (kind == ReportKind.Accident)
            {
                return ReportAccident();
            }
            if (kind != ReportKind.None && CrimeDirector.ReportScene())
            {
                return PhoneContacts.Closing("reallife_911_dispatched_crime");
            }
            if (CrimeDirector.IsActive || PoliceAttention.IsBusy)
            {
                // Something is going on, just nothing more that a call can do about it: never a wasted call.
                return PhoneContacts.Closing("reallife_911_noted");
            }
            // A call about nothing: a warning the first two times in a day, a fine after that.
            int fine = CrimeDirector.WastedCall();
            return fine > 0
                ? PhoneContacts.Closing("reallife_911_wasted_fine", new { amount = ((float)fine).ToCurrencyFormat() })
                : PhoneContacts.Closing("reallife_911_nothing");
        }

        private static DialogEntry ReportAccident()
        {
            if (TrafficAccidents.Instance != null && TrafficAccidents.Instance.ReportAccident())
            {
                ModState.AddModifier(ModState.Helped);
                return PhoneContacts.Closing("reallife_911_dispatched");
            }
            return PhoneContacts.Closing("reallife_911_nothing");
        }

        private static DialogEntry AskAmbulance()
        {
            return new DialogEntry
            {
                messageData = "reallife_911_ambulance_offer".Localize(new { amount = ((float)LifeEvents.AmbulanceFee).ToCurrencyFormat() }),
                ConfirmTextOverride = "reallife_911_ambulance_confirm".Localize(),
                OnConfirm = () =>
                {
                    LifeEvents.Instance?.SendToHospital();
                    return PhoneContacts.Closing("reallife_911_ambulance_sent");
                },
                OnCancel = PhoneContacts.HangUp
            };
        }
    }

    public class DoctorCallDialog : Dialog
    {
        public DoctorCallDialog()
        {
            npcNameKey = "reallife-doctor";
            if (!ModState.IsSick)
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_doctor_healthy"));
                return;
            }
            DialogController.current.ShowEntry(new DialogEntry
            {
                messageData = "reallife_doctor_offer".Localize(new { amount = ((float)LifeEvents.DoctorFee).ToCurrencyFormat() }),
                ConfirmTextOverride = "reallife_doctor_pay".Localize(),
                OnConfirm = Treat,
                OnCancel = PhoneContacts.HangUp
            });
        }

        private static DialogEntry Treat()
        {
            if (!GameManager.ChangeMoneySafe(-LifeEvents.DoctorFee, new TransactionInfo("reallife_transaction_doctor"), null, null, force: false, showNotification: true))
            {
                return null;
            }
            LifeEvents.Instance?.Cure("reallife_event_recovered");
            return PhoneContacts.Closing("reallife_doctor_treated");
        }
    }

    public class FriendDialog : Dialog
    {
        public FriendDialog()
        {
            npcNameKey = "reallife-friend";
            if (ModState.LastFriendCallDay == TimeHelper.CurrentDay)
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_friend_again"));
                return;
            }
            DialogController.current.ShowEntry(new DialogEntry
            {
                messageData = "reallife_friend_greeting".Localize(),
                ConfirmTextOverride = "reallife_friend_chat".Localize(),
                OnConfirm = () =>
                {
                    ModState.LastFriendCallDay = TimeHelper.CurrentDay;
                    ModState.AddModifier(ModState.Social);
                    return PhoneContacts.Closing("reallife_friend_bye");
                },
                OnCancel = PhoneContacts.HangUp
            });
        }
    }

    public class MomDialog : Dialog
    {
        public MomDialog()
        {
            npcNameKey = "reallife-mom";
            if (ModState.LastMomCallDay == TimeHelper.CurrentDay)
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_mom_again"));
                return;
            }
            DialogController.current.ShowEntry(new DialogEntry
            {
                messageData = "reallife_mom_greeting".Localize(),
                ConfirmTextOverride = "reallife_mom_chat".Localize(),
                OnConfirm = () =>
                {
                    ModState.LastMomCallDay = TimeHelper.CurrentDay;
                    ModState.AddModifier(ModState.Family);
                    return PhoneContacts.Closing(ModState.IsSick ? "reallife_mom_bye_sick" : "reallife_mom_bye");
                },
                OnCancel = PhoneContacts.HangUp
            });
        }
    }
}
