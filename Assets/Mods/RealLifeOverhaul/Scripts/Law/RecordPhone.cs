using System;
using BAModAPI;
using Dialogs;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>The Police Records Desk contact on the phone: it reads out both standings and the record, and points to a precinct.</summary>
    public static class RecordPhone
    {
        public const string ContactId = "reallife-records";

        public static void Setup(Sprite badge)
        {
            if (!Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                ContactPictures.Add(ContactId);   // the game's own logo shapes or portrait camera (Phone/Contacts)
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_records_desc", null, false, true);
                var dialogType = (CallDialogType)ModEnumHash.GetSafeHash("reallife_records_calldialogtype");
                contact.callDialogTypeOverride = dialogType;   // not saved by the game: set on every load
                CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new RecordsDialog()));
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_records_welcome"), notify: false);
                }
            }
            catch (Exception e)
            {
                LawDirector.Logger?.Error(e);
                LawDirector.Logger?.Warn("Law: the Police Records Desk contact could not be added; the record is still kept.");
            }
        }

        /// <summary>How a line of the record ended, in the player's language.</summary>
        internal static string OutcomeText(RecordEntry line)
        {
            switch (line.Outcome)
            {
                case Outcome.Held: return "reallife_records_outcome_held".GetLocalization();
                case Outcome.Served: return "reallife_records_outcome_served".GetLocalization().Replace("{hours}", line.Hours.ToString());
                case Outcome.Bailed: return "reallife_records_outcome_bailed".GetLocalization().Replace("{hours}", line.Hours.ToString());
                case Outcome.Escaped: return "reallife_records_outcome_escaped".GetLocalization();
                default: return "reallife_records_outcome_cited".GetLocalization();
            }
        }

        /// <summary>How long ago the last offence was, in the player's language.</summary>
        internal static string SinceText()
        {
            int since = Record.DaysSinceLastOffence;
            return since < 0 ? "reallife_records_since_never".GetLocalization()
                : since == 0 ? "reallife_records_since_today".GetLocalization()
                : "reallife_records_since_days".GetLocalization().Replace("{days}", since.ToString());
        }
    }

    /// <summary>The call to the records desk.</summary>
    public sealed class RecordsDialog : Dialog
    {
        public RecordsDialog()
        {
            npcNameKey = RecordPhone.ContactId;
            if (!LawState.Loaded)
            {
                DialogController.current.ShowEntry(Guard.Entry(PhoneContacts.Closing("reallife_records_closed")));
                return;
            }
            DialogController.current.ShowEntry(Guard.Entry(Summary()));
        }

        private static DialogEntry Summary()
        {
            string last = RecordPhone.SinceText();
            var entry = new DialogEntry
            {
                messageData = "reallife_records_summary".Localize(new
                {
                    police = Reputation.PoliceWord.GetLocalization(),
                    policeScore = Reputation.Police.ToString(),
                    people = Reputation.PublicWord.GetLocalization(),
                    peopleScore = Reputation.Public.ToString(),
                    count = Record.Count.ToString(),
                    since = last,
                    watch = (Reputation.NoWarnings ? "reallife_records_watch_high" : Reputation.ScrutinyLevel > 0 ? "reallife_records_watch_some"
                        : "reallife_records_watch_none").GetLocalization()
                }),
                ConfirmTextOverride = "reallife_records_read".Localize(),
                OnConfirm = () => Record.Count > 0 ? Line(Record.Count - 1) : PhoneContacts.Closing("reallife_records_clean"),
                SecondOptionTextOverride = "reallife_records_nearest",
                OnSecondOption = Nearest,
                OnCancel = PhoneContacts.HangUp
            };
            return entry;
        }

        private static DialogEntry Line(int index)
        {
            if (index < 0 || index >= Record.Count)
            {
                return PhoneContacts.Closing("reallife_records_end");
            }
            RecordEntry line = Record.Entry(index);
            string outcome = RecordPhone.OutcomeText(line);
            return new DialogEntry
            {
                messageData = "reallife_records_line".Localize(new
                {
                    number = (Record.Count - index).ToString(),
                    count = Record.Count.ToString(),
                    day = line.Day.ToString(),
                    charges = Custody.ChargeNames(line.Charges, ", "),
                    outcome,
                    amount = ((float)line.Fines).ToCurrencyFormat()
                }),
                ConfirmTextOverride = (index > 0 ? "reallife_records_next" : "reallife_records_done").Localize(),
                OnConfirm = () => index > 0 ? Line(index - 1) : PhoneContacts.Closing("reallife_records_end"),
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static DialogEntry Nearest()
        {
            LawDirector director = LawDirector.Instance;
            Precinct precinct = director != null && Settings.LawPrecincts ? director.GuideToNearest() : null;
            if (precinct == null)
            {
                return PhoneContacts.Closing("reallife_records_nearest_none");
            }
            Vector3 offset = precinct.Door - PlayerHelper.GetCityPosition();
            offset.y = 0f;
            return PhoneContacts.Closing("reallife_records_nearest_done", new
            {
                precinct = precinct.Name,
                metres = Mathf.RoundToInt(offset.magnitude).ToString()
            });
        }
    }
}
