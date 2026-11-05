using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The director's part behind the options page's test buttons.</summary>
    public sealed partial class HomeDirector
    {
        public void TestRoutine(HomeWish wish)
        {
            try
            {
                // Every routine test is pressed on the options page, a menu with the game paused behind it, where the
                // game's own check for starting anything can never pass: the test waits for the menus to be closed.
                PlayerController player = PlayerHelper.PlayerController;
                if (wish != HomeWish.StopRun && HomeState.Loaded && Settings.HomeLife && !_broken
                    && player != null && !Free(player) && !JobDirector.OnShift && !PlayerHelper.IsUsingVehicle && !_run.Active)
                {
                    TestWait.Run(Logger, "Home " + wish, () => PlayerHelper.PlayerController != null && Free(PlayerHelper.PlayerController), () => Act(wish, true));
                    return;
                }
                Act(wish, true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        public void TestSick(Illness ill)
        {
            if (!Ready("sick"))
            {
                return;
            }
            HomeHealth.Catch(ill, "reallife_health_cause_test", true);
        }

        public void TestCure()
        {
            if (!Ready("cure"))
            {
                return;
            }
            bool was = HomeHealth.Ill || ModState.IsSick;
            HomeHealth.Over(false);
            LifeEvents.Instance?.Cure(null);
            ModState.RemoveModifier(ModState.Injured);
            HomeState.ImmuneUntilDay = 0;
            HomeState.SickNoteUntil = 0f;
            Notifications.Show(NotificationType.Success, was ? "reallife_health_test_cured" : "reallife_health_test_nothing_to_cure", null, 6f);
        }

        public void TestFitness(float change)
        {
            if (!Ready("fitness"))
            {
                return;
            }
            HomeState.Fitness = HomeState.Clamp(HomeState.Fitness + change, 0f, 100f);
            HomeState.ShapeDue += change * 0.002f;   // shown on the body the next time the player wakes
            HomeState.FatDue -= change * 0.002f;
            HomeHealth.Hourly(HomeTidy.AtHome(), false);
            JobText.Toast(NotificationType.Info, "reallife_health_test_fitness", "fitness", Mathf.RoundToInt(HomeState.Fitness).ToString());
        }

        public void TestNeeds(bool low)
        {
            if (!Ready("needs"))
            {
                return;
            }
            HomeState.Hygiene = low ? 8f : 90f;
            HomeState.Stress = low ? 88f : 10f;
            HomeState.SleepDebt = low ? 12f : 0f;
            HomeTidy.Change(low ? -(HomeTidy.Read() - 12f) : 100f);
            HomeTidy.Read();
            HomeHealth.Hourly(HomeTidy.AtHome(), false);
            Notifications.Show(NotificationType.Info, low ? "reallife_health_test_needs_low" : "reallife_health_test_needs_full", null, 6f);
        }

        public void TestPage()
        {
            if (!Ready("page"))
            {
                return;
            }
            if (PlayerHelper.PlayerController == null)
            {
                return;
            }
            // The page is a conversation, which cannot open over the menu this button is in.
            TestWait.Run(Logger, "Home page", () => PlayerHelper.PlayerController != null && Free(PlayerHelper.PlayerController), HomePhone.OpenInPerson);
        }

        /// <summary>
        /// "Wake up to the morning summary": the morning line of the last sleep, with three of the city's notices that come
        /// on waking (a storm warning, the forecast, a street market) held as a night holds them, through the summary's own
        /// path (Core/MorningNews, HomeReport.Morning). With its news switched off they come on their own, as they would.
        /// </summary>
        public void TestMorning()
        {
            if (!Ready("morning"))
            {
                return;
            }
            if (!Settings.HomeSleep || !Settings.HomeMorning)
            {
                TestRefusals.Tell(Logger, "Home morning", TestRefusal.FeatureOff);
                return;
            }
            MorningNews.Hold();
            MorningNews.Show(NotificationType.Warning, "reallife_weather_warning_storm", null, 8f, "rlo-weather");
            MorningNews.Show(NotificationType.Info, "reallife_weather_forecast_rain", null, 9f);
            MorningNews.Show(NotificationType.Info, "reallife_event_today_market", new Dictionary<string, string>(2) { { "from", 10.GetFormattedTime() }, { "to", 16.GetFormattedTime() } }, 10f);
            HomeReport.Morning(HomeState.LastSleepHours >= WakeRules.NightAtLeast ? HomeState.LastSleepHours : 8f, TimeHelper.CurrentHour);
        }

        private bool Ready(string test)
        {
            if (!HomeState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(Logger, "Home " + test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.HomeLife || _broken)
            {
                TestRefusals.Tell(Logger, "Home " + test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }
    }
}
