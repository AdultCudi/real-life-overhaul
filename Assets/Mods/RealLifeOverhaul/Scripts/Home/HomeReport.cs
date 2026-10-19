using System.Collections.Generic;
using System.Text;
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

        private const float MorningSeconds = 12f;   // seconds the morning line stays up, and each further part of its summary
        private const string HeadKey = "reallife_home_morning_news";   // "{morning} While you slept: {news}"
        private const string MoreKey = "reallife_home_morning_more";   // "Also while you slept: {news}"

        // The summary's working lists, kept: it is built once a morning at most.
        private static readonly List<MorningNews.Notice> News = new List<MorningNews.Notice>(MorningNewsRules.MostHeld);
        private static readonly List<string> NewsWords = new List<string>(MorningNewsRules.MostHeld);
        private static readonly List<int> NewsLengths = new List<int>(MorningNewsRules.MostHeld);
        private static readonly List<int> NewsWeights = new List<int>(MorningNewsRules.MostHeld);
        private static readonly List<NewsPart> Parts = new List<NewsPart>(4);
        private static readonly StringBuilder Joined = new StringBuilder(MorningNewsRules.Most);

        /// <summary>
        /// The line on waking, once a day after a real sleep (Home/WakeRules), greeted by the hour it ended at, with the city's
        /// news held while the player slept (Core/MorningNews) in the same notification, or as few more as it fits in
        /// (Core/MorningNewsRules.Pack). Builds strings: once a morning.
        /// </summary>
        public static void Morning(float hours, int hour)
        {
            var data = new Dictionary<string, string>(3)
            {
                { "slept", hours.ToString("F1") }, { "quality", JobText.T(SleepWord) }, { "tip", JobText.T(Tip()) }
            };
            NotificationType type = HomeState.SleepQuality == 3 ? NotificationType.Success : HomeState.SleepQuality == 1 ? NotificationType.Warning : NotificationType.Info;
            string key = WakeRules.Greets(hour) ? "reallife_home_morning" : "reallife_home_woke";
            News.Clear();
            int count = MorningNews.Take(News);
            if (count == 0)
            {
                Notifications.Show(type, key, data, MorningSeconds);
                return;
            }
            string morning = MorningNews.Words(new MorningNews.Notice { Key = key, Data = data });
            NewsWords.Clear();
            NewsLengths.Clear();
            NewsWeights.Clear();
            for (int i = 0; i < count; i++)
            {
                string words = MorningNews.Words(News[i]);
                NewsWords.Add(words);
                NewsLengths.Add(words.Length);
                NewsWeights.Add(MorningNews.Weight(News[i].Type));
            }
            int headExtra = MorningNewsRules.Extra(HeadKey.GetLocalization(), "{morning}", "{news}");
            int moreExtra = MorningNewsRules.Extra(MoreKey.GetLocalization(), "{news}", null);
            MorningNewsRules.Pack(morning.Length, headExtra, moreExtra, NewsLengths, count, Parts);
            int longest = 0;
            for (int p = 0; p < Parts.Count; p++)
            {
                NewsPart part = Parts[p];
                int length = MorningNewsRules.Length(part, morning.Length, headExtra, moreExtra, NewsLengths);
                longest = Mathf.Max(longest, part.Alone ? 0 : length);
                if (part.Alone)
                {
                    MorningNews.ShowAsCame(News[part.From]);
                }
                else if (part.Head && part.Count == 0)
                {
                    Notifications.Show(type, key, data, MorningSeconds);
                }
                else if (part.Head)
                {
                    int weight = MorningNewsRules.HeadWeight(MorningNews.Weight(type), MorningNewsRules.Strongest(NewsWeights, part.From, part.Count, -1));
                    Notifications.Show(MorningNews.OfWeight(weight), HeadKey, new Dictionary<string, string>(2) { { "morning", morning }, { "news", Join(part) } }, MorningSeconds);
                }
                else
                {
                    int weight = MorningNewsRules.Strongest(NewsWeights, part.From, part.Count, 0);
                    Notifications.Show(MorningNews.OfWeight(weight), MoreKey, new Dictionary<string, string>(1) { { "news", Join(part) } }, MorningSeconds);
                }
            }
            if (Meta.Dev.Verbose)
            {
                HomeDirector.Logger?.Info("Home check (morning news): " + count + " notice(s) held while asleep went out with the morning line in " + Parts.Count
                                          + " notification(s), the longest " + longest + " characters against " + MorningNewsRules.Most + ": "
                                          + (longest <= MorningNewsRules.Most ? "PASS" : "FAIL, a part is over the limit"));
            }
        }

        // The held notices' words a part carries, in the order they came, a space between.
        private static string Join(NewsPart part)
        {
            Joined.Length = 0;
            for (int i = part.From; i < part.From + part.Count && i < NewsWords.Count; i++)
            {
                if (i > part.From)
                {
                    Joined.Append(' ');
                }
                Joined.Append(NewsWords[i]);
            }
            return Joined.ToString();
        }
    }
}
