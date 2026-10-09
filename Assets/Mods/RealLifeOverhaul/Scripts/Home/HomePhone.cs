using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using UI.Notification;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>"How am I doing": the needs, health, fitness, food and sleep in words, and the one thing most worth doing about them.</summary>
    public static class HomeReport
    {
        public static string HygieneWord => HomeState.Hygiene >= 75f ? "reallife_health_word_fresh" : HomeState.Hygiene >= HomeBalance.HygieneLow ? "reallife_health_word_fine" : "reallife_health_word_unwashed";
        public static string StressWord => HomeState.Stress < HomeBalance.StressCalm ? "reallife_health_word_calm" : HomeState.Stress <= 50f ? "reallife_health_word_fine"
            : HomeState.Stress <= HomeBalance.StressHigh ? "reallife_health_word_tense" : "reallife_health_word_stressed";
        public static string TidyWord => !HomeTidy.HasHome ? "reallife_health_word_no_home" : HomeState.Tidy >= HomeBalance.TidyCosy ? "reallife_health_word_spotless"
            : HomeState.Tidy >= HomeBalance.MessLow ? "reallife_health_word_fine" : "reallife_health_word_messy";
        public static string DietWord => HomeState.DietScore >= HomeBalance.DietGood ? "reallife_health_word_good" : HomeState.DietScore > HomeBalance.DietPoor ? "reallife_health_word_fair" : "reallife_health_word_poor";
        public static string SleepWord => HomeState.SleepQuality == 3 ? "reallife_health_word_rested" : HomeState.SleepQuality == 1 ? "reallife_health_word_groggy" : "reallife_health_word_fair";

        /// <summary>The one thing most worth doing now, as a locale key.</summary>
        public static string Tip()
        {
            if (HomeHealth.Ill && !HomeState.IllTreated) return "reallife_health_tip_doctor";
            if (HomeState.SleepDebt > HomeBalance.DebtHigh) return "reallife_health_tip_sleep";
            if (SaveGameManager.Current != null && SaveGameManager.Current.Hunger < 20f) return "reallife_health_tip_eat";
            if (HomeState.Hygiene < HomeBalance.HygieneLow) return "reallife_health_tip_wash";
            if (HomeState.Stress > HomeBalance.StressHigh) return "reallife_health_tip_relax";
            if (HomeTidy.HasHome && HomeState.Tidy < HomeBalance.MessLow) return "reallife_health_tip_tidy";
            if (HomeState.DietScore <= HomeBalance.DietPoor) return "reallife_health_tip_cook";
            if (TimeHelper.CurrentDay - HomeState.LastWorkoutDay >= 3) return "reallife_health_tip_exercise";
            return "reallife_health_tip_none";
        }

        private static int Count(Meal meal)
        {
            int count = 0;
            for (int day = 0; day < HomeState.DietDays; day++)
            {
                count += HomeState.Meals(day, meal);
            }
            return count;
        }

        /// <summary>The whole page as the text of a conversation. Builds strings: for when the page is opened.</summary>
        public static LanguageChangeEventDataHolder Page()
        {
            HomeTidy.Read();
            float now = TimeHelper.NowInMinutes();
            string illness = HomeHealth.Ill
                ? JobText.T(HomeState.IllTreated ? "reallife_health_page_ill_treated" : "reallife_health_page_ill")
                    .Replace("{illness}", JobText.T(HomeHealth.NameKey(HomeState.Ill)))
                    .Replace("{symptom}", JobText.T(HomeHealth.SymptomKey(HomeState.Ill)))
                    .Replace("{days}", HomeHealth.Days(HomeState.IllUntil - now))
                : JobText.T("reallife_health_page_well");
            if (HomeHealth.SickNoteActive)
            {
                illness += " " + JobText.T("reallife_health_page_note", "days", HomeHealth.Days(HomeState.SickNoteUntil - now));
            }
            float change = HomeState.Fitness - HomeState.FitnessWeekAgo;
            string trend = change > 1f ? "reallife_health_word_improving" : change < -1f ? "reallife_health_word_slipping" : "reallife_health_word_steady";
            return "reallife_health_page".Localize(new
            {
                hygiene = Mathf.RoundToInt(HomeState.Hygiene),
                hygieneWord = JobText.T(HygieneWord),
                stress = Mathf.RoundToInt(HomeState.Stress),
                stressWord = JobText.T(StressWord),
                home = Mathf.RoundToInt(HomeState.Tidy),
                homeWord = JobText.T(TidyWord),
                illness,
                fitness = Mathf.RoundToInt(HomeState.Fitness),
                trend = JobText.T(trend),
                diet = JobText.T(DietWord),
                cooked = Count(Meal.Home),
                plain = Count(Meal.Plain),
                bought = Count(Meal.Junk),
                skipped = Count(Meal.Skipped),
                debt = HomeState.SleepDebt.ToString("F1"),
                slept = HomeState.LastSleepHours.ToString("F1"),
                sleepWord = JobText.T(SleepWord),
                tip = JobText.T(Tip())
            });
        }

        /// <summary>The morning's summary, once a day, after a real night's sleep.</summary>
        public static void Morning(float hours)
        {
            var data = new Dictionary<string, string>(3)
            {
                { "slept", hours.ToString("F1") }, { "quality", JobText.T(SleepWord) }, { "tip", JobText.T(Tip()) }
            };
            NotificationType type = HomeState.SleepQuality == 3 ? NotificationType.Success : HomeState.SleepQuality == 1 ? NotificationType.Warning : NotificationType.Info;
            Notifications.Show(type, "reallife_home_morning", data, 12f);
        }
    }

    /// <summary>The "Wellbeing" contact: the page above, and from it the routines that need no furniture.</summary>
    public static class HomePhone
    {
        public const string ContactId = "reallife-health";
        private static Sprite _icon;

        public static CallDialogType DialogType => (CallDialogType)ModEnumHash.GetSafeHash("reallife_health_calldialogtype");

        public static void Setup()
        {
            if (!Settings.HomeLife || !Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                AddIcon();
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_health_desc", null, false, true);
                contact.callDialogTypeOverride = DialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(DialogType, () => new WellbeingDialog());
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

        /// <summary>The same page face to face: for the options-page button.</summary>
        public static void OpenInPerson()
        {
            CallDialogFactory.RegisterDialog(DialogType, () => new WellbeingDialog());
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(DialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

        private static void AddIcon()
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (references == null || references.contactIcons == null)
            {
                return;
            }
            foreach (Sprite existing in references.contactIcons)
            {
                if (existing != null && existing.name == ContactId)
                {
                    return;
                }
            }
            if (_icon == null)
            {
                const int size = 96;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                var colour = new Color(0.36f, 0.72f, 0.36f);
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
                _icon = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                _icon.name = ContactId;
            }
            Sprite[] old = references.contactIcons;
            var icons = new Sprite[old.Length + 1];
            old.CopyTo(icons, 0);
            icons[old.Length] = _icon;
            references.contactIcons = icons;
        }
    }

    public class WellbeingDialog : Dialog
    {
        public WellbeingDialog()
        {
            npcNameKey = HomePhone.ContactId;
            if (!HomeState.Loaded || !Settings.HomeLife)
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_health_page_off"));
                return;
            }
            bool running = HomeDirector.Instance != null && HomeDirector.Instance.Jogging;
            DialogController.current.ShowEntry(new DialogEntry
            {
                messageData = HomeReport.Page(),
                ConfirmTextOverride = "reallife_health_menu_routines".Localize(),
                OnConfirm = Routines,
                SecondOptionTextOverride = running ? "reallife_health_menu_stop_run" : "reallife_health_menu_run",
                OnSecondOption = () => Ask(running ? HomeWish.StopRun : HomeWish.Run),
                OnCancel = PhoneContacts.HangUp
            });
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
                CallDialogFactory.RegisterDialog(DialogType, () => new ClinicDialog());
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

    public class ClinicDialog : Dialog
    {
        public ClinicDialog()
        {
            npcNameKey = "reallife-clinic";
            if (!DoctorVisit.Pay(HomeBalance.ConsultationFee, "reallife_transaction_consultation"))
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_doctor_no_money"));
                return;
            }
            bool hurt = Hurt();
            if (!HomeHealth.Ill && !hurt)
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_doctor_checkup", new
                {
                    fitness = Mathf.RoundToInt(HomeState.Fitness),
                    diet = JobText.T(HomeReport.DietWord),
                    debt = HomeState.SleepDebt.ToString("F1"),
                    tip = JobText.T(HomeReport.Tip())
                }));
                return;
            }
            if (HomeHealth.Ill && HomeState.IllTreated && !hurt)
            {
                DialogController.current.ShowEntry(new DialogEntry
                {
                    messageData = "reallife_doctor_already".Localize(new { illness = JobText.T(HomeHealth.NameKey(HomeState.Ill)), days = HomeHealth.Days(HomeState.IllUntil - TimeHelper.NowInMinutes()) }),
                    ConfirmTextOverride = "reallife_doctor_note_only".Localize(new { amount = JobText.Money(HomeBalance.SickNoteFee) }),
                    OnConfirm = () => Prescribe(false, true),
                    OnCancel = PhoneContacts.HangUp
                });
                return;
            }
            string key = HomeHealth.Ill ? "reallife_doctor_diagnosis" : "reallife_doctor_diagnosis_hurt";
            DialogController.current.ShowEntry(new DialogEntry
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
            });
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
