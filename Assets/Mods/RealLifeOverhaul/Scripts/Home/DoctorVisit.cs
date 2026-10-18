using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The consultation at a hospital or clinic, after the wait: diagnosis, prescription, sick note.</summary>
    public static class DoctorVisit
    {
        /// <summary>Set by the test button: nothing is charged.</summary>
        public static bool Free;

        public static CallDialogType DialogType => (CallDialogType)ModEnumHash.GetSafeHash("reallife_clinic_calldialogtype");

        public static void Open()
        {
            try
            {
                CallDialogFactory.RegisterDialog(DialogType, Guard.Maker(() => new ClinicDialog()));
                InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(DialogType, NavigationBlocker.SpecialEmployeeDialog);
            }
            catch (Exception e)
            {
                HomeDirector.Logger?.Error(e);
            }
        }

        public static bool Pay(int amount, string transaction)
        {
            return Free || GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transaction), null, null, force: false, showNotification: true);
        }
    }

    /// <summary>The consultation itself: a check-up, or a diagnosis with a prescription and a sick note to buy.</summary>
    public sealed class ClinicDialog : Dialog
    {
        public ClinicDialog()
        {
            npcNameKey = "reallife-clinic";
            if (!DoctorVisit.Pay(HomeBalance.ConsultationFee, "reallife_transaction_consultation"))
            {
                DialogController.current.ShowEntry(Guard.Entry(PhoneContacts.Closing("reallife_doctor_no_money")));
                return;
            }
            bool hurt = Hurt();
            if (!HomeHealth.Ill && !hurt)
            {
                DialogController.current.ShowEntry(Guard.Entry(PhoneContacts.Closing("reallife_doctor_checkup", new
                {
                    fitness = Mathf.RoundToInt(HomeState.Fitness),
                    diet = JobText.T(HomeReport.DietWord),
                    debt = HomeState.SleepDebt.ToString("F1"),
                    tip = JobText.T(HomeReport.Tip())
                })));
                return;
            }
            if (HomeHealth.Ill && HomeState.IllTreated && !hurt)
            {
                DialogController.current.ShowEntry(Guard.Entry(new DialogEntry
                {
                    messageData = "reallife_doctor_already".Localize(new
                    {
                        illness = JobText.T(HomeHealth.NameKey(HomeState.Ill)), days = HomeHealth.Days(HomeState.IllUntil - TimeHelper.NowInMinutes())
                    }),
                    ConfirmTextOverride = "reallife_doctor_note_only".Localize(new { amount = JobText.Money(HomeBalance.SickNoteFee) }),
                    OnConfirm = () => Prescribe(false, true),
                    OnCancel = PhoneContacts.HangUp
                }));
                return;
            }
            string key = HomeHealth.Ill ? "reallife_doctor_diagnosis" : "reallife_doctor_diagnosis_hurt";
            DialogController.current.ShowEntry(Guard.Entry(new DialogEntry
            {
                messageData = key.Localize(new
                {
                    illness = JobText.T(HomeHealth.NameKey(HomeState.Ill)),
                    symptom = JobText.T(HomeHealth.SymptomKey(HomeState.Ill)),
                    days = HomeHealth.Days(HomeState.IllUntil - TimeHelper.NowInMinutes()),
                    fee = JobText.Money(HomeBalance.PrescriptionFee),
                    note = JobText.Money(HomeBalance.SickNoteFee)
                }),
                ConfirmTextOverride = "reallife_doctor_prescription".Localize(new { amount = JobText.Money(HomeBalance.PrescriptionFee) }),
                OnConfirm = () => Prescribe(true, false),
                SecondOptionTextOverride = "reallife_doctor_prescription_note",
                OnSecondOption = () => Prescribe(true, true),
                OnCancel = PhoneContacts.HangUp
            }));
        }

        private static bool Hurt()
        {
            List<HappinessModifierData> list = SaveGameManager.Current.happinessModifiers;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                if (list[i].type == ModState.Injured)
                {
                    return true;
                }
            }
            return false;
        }

        private static DialogEntry Prescribe(bool medicine, bool note)
        {
            int cost = (medicine ? HomeBalance.PrescriptionFee : 0) + (note ? HomeBalance.SickNoteFee : 0);
            if (!DoctorVisit.Pay(cost, "reallife_transaction_prescription"))
            {
                return null;
            }
            if (medicine)
            {
                HomeHealth.Treat();
                ModState.RemoveModifier(ModState.Injured);
            }
            if (note)
            {
                HomeState.SickNoteUntil = TimeHelper.NowInMinutes() + HomeBalance.SickNoteHours * 60f;
            }
            HomeDirector.Logger?.Info("Doctor: " + (medicine ? "prescription" : "no prescription") + (note ? ", sick note" : string.Empty) + ", " + cost + " paid.");
            string key = medicine && note ? "reallife_doctor_done_both" : medicine ? "reallife_doctor_done_medicine" : "reallife_doctor_done_note";
            return PhoneContacts.Closing(key, new { days = HomeHealth.Ill ? HomeHealth.Days(HomeState.IllUntil - TimeHelper.NowInMinutes()) : "0 h" });
        }
    }
}
