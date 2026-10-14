using System;
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
    /// The phone contact "City Cabs": a call asks for a taxi to where the caller is, or, while one is out, says
    /// how far off it is and cancels it. Registered the way the mod's other contacts are; the conversation is the
    /// game's own dialogue panel with the game's reply buttons, and every decision in it is the director's
    /// (<see cref="TaxiDirector"/>).
    /// </summary>
    public static class TaxiPhone
    {
        public const string ContactId = "reallife-taxi";
        private const string DialogTypeName = "reallife_taxi_calldialogtype";

        private static Contact _contact;

        /// <summary>True once the contact is on the phone in this city.</summary>
        public static bool Ready => _contact != null;

        public static void Setup()
        {
            _contact = null;
            if (!Settings.TaxiCall || !Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                ContactPictures.Add(ContactId);   // the game's own logo shapes (Phone/Contacts)
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_taxi_desc", null, false, true);
                contact.callDialogTypeOverride = Register();   // not saved by the game, so set on every load
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_taxi_welcome"), notify: false);
                }
                _contact = contact;
            }
            catch (Exception e)
            {
                TaxiDirector.Logger?.Error(e);
                TaxiDirector.Logger?.Warn("Taxi: the City Cabs contact could not be added; a taxi can still be called with the test button on the options page.");
            }
        }

        public static void Forget()
        {
            _contact = null;
        }

        /// <summary>The conversation without the phone: the game's own dialogue panel, as its counters open one.</summary>
        public static void Open()
        {
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(Register(), NavigationBlocker.SpecialEmployeeDialog);
        }

        private static CallDialogType Register()
        {
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypeName);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new TaxiDialog()));
            return dialogType;
        }
    }

    /// <summary>City Cabs on the line: the offer, the refusals, and the taxi that is out.</summary>
    public class TaxiDialog : Dialog
    {
        public TaxiDialog()
        {
            npcNameKey = TaxiPhone.ContactId;
            DialogController.current.ShowEntry(Guard.Entry(First()));
        }

        private static DialogEntry First()
        {
            TaxiDirector director = TaxiDirector.Instance;
            if (director == null)
            {
                return PhoneContacts.Closing("reallife_taxi_refuse_off");
            }
            if (TaxiDirector.Out)
            {
                return Status(director);
            }
            if (!director.Quote(out int minutes, out float fee, out TaxiRules.Refusal refusal, out bool failed))
            {
                return Refused(refusal, failed);
            }
            return new DialogEntry
            {
                messageData = "reallife_taxi_offer".Localize(new { minutes = minutes.ToString(), amount = TransitText.Money(fee) }),
                ConfirmTextOverride = "reallife_taxi_send".Localize(),
                OnConfirm = Send,
                CancelTextOverride = "reallife_taxi_no_thanks",
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static DialogEntry Send()
        {
            TaxiDirector director = TaxiDirector.Instance;
            return director != null && director.Dispatch(out int minutes)
                ? PhoneContacts.Closing("reallife_taxi_sent", new { minutes = minutes.ToString() })
                : PhoneContacts.Closing("reallife_taxi_refuse_none");
        }

        // A taxi is out: how far off it is, what a cancel costs now, and the cancel.
        private static DialogEntry Status(TaxiDirector director)
        {
            float due = director.CancelFee;
            string cancel = (due > 0f ? "reallife_taxi_cancel_costs" : "reallife_taxi_cancel_free").GetLocalization().Replace("{amount}", TransitText.Money(due));
            return new DialogEntry
            {
                messageData = director.Standing
                    ? "reallife_taxi_status_waiting".Localize(new { cancel })
                    : "reallife_taxi_status_coming".Localize(new { metres = Mathf.RoundToInt(director.Remaining).ToString(), cancel }),
                ConfirmTextOverride = "reallife_taxi_cancel".Localize(),
                OnConfirm = Cancel,
                CancelTextOverride = "reallife_taxi_keep",
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static DialogEntry Cancel()
        {
            TaxiDirector director = TaxiDirector.Instance;
            float paid = director != null ? director.Cancel() : 0f;
            return paid > 0f
                ? PhoneContacts.Closing("reallife_taxi_cancelled_fee", new { amount = TransitText.Money(paid) })
                : PhoneContacts.Closing("reallife_taxi_cancelled_free");
        }

        private static DialogEntry Refused(TaxiRules.Refusal refusal, bool failed)
        {
            switch (refusal)
            {
                case TaxiRules.Refusal.Riding: return PhoneContacts.Closing("reallife_taxi_refuse_riding");
                case TaxiRules.Refusal.Held: return PhoneContacts.Closing("reallife_taxi_refuse_held");
                case TaxiRules.Refusal.InVehicle: return PhoneContacts.Closing("reallife_taxi_refuse_vehicle");
                case TaxiRules.Refusal.Indoors: return PhoneContacts.Closing("reallife_taxi_refuse_indoors");
                case TaxiRules.Refusal.NoRoad: return PhoneContacts.Closing("reallife_taxi_refuse_road", new { metres = TaxiRules.RoadWithin.ToString("F0") });
                case TaxiRules.Refusal.NoTaxi: return PhoneContacts.Closing("reallife_taxi_refuse_none");
                default: return PhoneContacts.Closing(failed ? "reallife_taxi_refuse_failed" : "reallife_taxi_refuse_off");
            }
        }
    }
}
