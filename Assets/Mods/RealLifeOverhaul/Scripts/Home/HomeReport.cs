using System.Collections.Generic;
using Localizor;
using Localizor.LanguageChangeEvent;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>"How am I doing": the needs, health, fitness, food and sleep in words, and the one thing most worth doing about them.</summary>
    public static class HomeReport
    {
        public static string HygieneWord => HomeState.Hygiene >= 75f ? "reallife_health_word_fresh"
            : HomeState.Hygiene >= HomeBalance.HygieneLow ? "reallife_health_word_fine" : "reallife_health_word_unwashed";
        public static string StressWord => HomeState.Stress < HomeBalance.StressCalm ? "reallife_health_word_calm" : HomeState.Stress <= 50f ? "reallife_health_word_fine"
            : HomeState.Stress <= HomeBalance.StressHigh ? "reallife_health_word_tense" : "reallife_health_word_stressed";
        public static string TidyWord => !HomeTidy.HasHome ? "reallife_health_word_no_home" : HomeState.Tidy >= HomeBalance.TidyCosy ? "reallife_health_word_spotless"
            : HomeState.Tidy >= HomeBalance.MessLow ? "reallife_health_word_fine" : "reallife_health_word_messy";
        public static string DietWord => HomeState.DietScore >= HomeBalance.DietGood ? "reallife_health_word_good"
            : HomeState.DietScore > HomeBalance.DietPoor ? "reallife_health_word_fair" : "reallife_health_word_poor";
        public static string SleepWord => HomeState.SleepQuality == 3 ? "reallife_health_word_rested" : HomeState.SleepQuality == 1 ? "reallife_health_word_groggy" : "reallife_health_word_fair";

        public static string Tip()
        {
            return HomeHealth.Ill && !HomeState.IllTreated ? "reallife_health_tip_doctor"
                : HomeState.SleepDebt > HomeBalance.DebtHigh ? "reallife_health_tip_sleep"
                : SaveGameManager.Current != null && SaveGameManager.Current.Hunger < 20f ? "reallife_health_tip_eat"
                : HomeState.Hygiene < HomeBalance.HygieneLow ? "reallife_health_tip_wash"
                : HomeState.Stress > HomeBalance.StressHigh ? "reallife_health_tip_relax"
                : HomeTidy.HasHome && HomeState.Tidy < HomeBalance.MessLow ? "reallife_health_tip_tidy"
                : HomeState.DietScore <= HomeBalance.DietPoor ? "reallife_health_tip_cook"
                : TimeHelper.CurrentDay - HomeState.LastWorkoutDay >= 3 ? "reallife_health_tip_exercise"
                : "reallife_health_tip_none";
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
}
