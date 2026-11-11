using System;
using Entities;
using Localizor;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Health
{
    /// <summary>
    /// What health says in the game's own conversations: at the doctor's desk (Home's consultation asks <see cref="Admission"/> first: a serious case is admitted, never
    /// prescribed for) and on the doctor's phone line (<see cref="Call"/>: an appointment booked, and no cure down the phone for what needs the hospital). Built on a press,
    /// never per frame.
    /// </summary>
    public static class HealthDoctor
    {
        /// <summary>The doctor's first words for a case that needs the hospital, with what the stay costs and what the plan pays; null for any other, so the consultation goes on as Home's.</summary>
        public static DialogEntry Admission()
        {
            HealthDirector d = HealthDirector.Instance;
            if (d == null || !d.NeedsAdmission)
            {
                return null;
            }
            d.AdmissionQuote(out Condition kind, out int days, out long bill, out long pays);
            return new DialogEntry
            {
                messageData = "reallife_hc_doctor_admit".Localize(new
                {
                    illness = HealthText.Name(kind), days, bill = HealthGame.Money(bill), plan = HealthGame.Money(pays), you = HealthGame.Money(bill - pays)
                }),
                ConfirmTextOverride = "reallife_hc_doctor_admit_yes".Localize(),
                OnConfirm = Admit,
                OnCancel = PhoneContacts.HangUp
            }.Says(DialogButton.Confirm, "reallife_hc_doctor_admit_cost", "amount", HealthGame.Money(bill - pays));
        }

        private static DialogEntry Admit()
        {
            HealthDirector d = HealthDirector.Instance;
            return PhoneContacts.Closing(d != null && d.Admit(false) ? "reallife_hc_doctor_admitted" : "reallife_hc_doctor_admit_failed");
        }

        /// <summary>
        /// The doctor's phone line with health on: what needs the hospital is sent there, and anybody may book an appointment; <paramref name="before"/> is the line as it was
        /// (a paid treatment while sick), offered beside the booking. Null while health is not running: the line as it was.
        /// </summary>
        public static DialogEntry Call(Func<DialogEntry> before)
        {
            HealthDirector d = HealthDirector.Instance;
            if (d == null || !d.Running)
            {
                return null;
            }
            if (d.NeedsAdmission)
            {
                return PhoneContacts.Closing("reallife_hc_call_serious");
            }
            HealthAppointment a = d.State.Appointment;
            if (a.Booked)
            {
                return new DialogEntry
                {
                    messageData = "reallife_hc_call_booked".Localize(new { day = a.Day, hour = a.Hour, place = a.Number + " " + a.Street }),
                    ConfirmTextOverride = "reallife_hc_call_other".Localize(),
                    OnConfirm = before,
                    OnCancel = PhoneContacts.HangUp
                };
            }
            return new DialogEntry
            {
                messageData = "reallife_hc_call_offer".Localize(),
                ConfirmTextOverride = "reallife_hc_book".Localize(),
                OnConfirm = Book,
                SecondOptionTextOverride = "reallife_hc_call_other",
                OnSecondOption = before,
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static DialogEntry Book()
        {
            HealthDirector d = HealthDirector.Instance;
            if (d == null || !d.Book())
            {
                return PhoneContacts.Closing("reallife_hc_call_no_clinic");
            }
            HealthAppointment a = d.State.Appointment;
            return PhoneContacts.Closing("reallife_hc_call_done", new { day = a.Day, hour = a.Hour, place = a.Number + " " + a.Street });
        }
    }
}
