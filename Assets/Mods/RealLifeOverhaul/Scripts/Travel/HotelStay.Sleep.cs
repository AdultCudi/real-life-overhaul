using System;
using System.Collections;
using BigAmbitions.DayNightCycle;
using Helpers;
using UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Sleep, rest and a beach walk, each through the game's time skip.</summary>
    public sealed partial class HotelStay
    {
        /// <summary>Puts the game's energy regeneration back as it was. Safe at any time; the watchdog calls it on every forced exit.</summary>
        public static void EndSleepEffects()
        {
            try
            {
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                EnergyHelper.energyRegenMultiplier = EnergyHelper.DefaultEnergyRegenMultiplier;
            }
            catch (Exception)
            {
                // the player is gone with the scene: there is nothing to put back
            }
        }

        // The next time the clock shows the wake-up hour, twelve hours at most, and never past checkout.
        private static Timestamp SleepGoal()
        {
            int day = TimeHelper.CurrentDay;
            int hour = TimeHelper.CurrentHour;
            int wakeDay = hour >= TravelState.WakeHour ? day + 1 : day;
            var goal = new Timestamp(wakeDay, TravelState.WakeHour, 0f);
            if (goal.GetMinutesFromNow() > 12f * 60f)
            {
                goal = TimeHelper.GetTimeInHours(12);
            }
            return Capped(goal);
        }

        private static Timestamp Capped(Timestamp goal)
        {
            var checkout = new Timestamp(TravelState.CheckoutDay, CheckoutHour, 0f);
            return goal.CompareTo(checkout) > 0 ? checkout : goal;
        }

        // True, with the reason said, when no time can be skipped to the goal.
        private bool CannotSkip(Timestamp goal)
        {
            // A Going Public session's clock runs faster only by everyone's vote, from a bed of the game's own.
            if (GoingPublic.ClockShared)
            {
                Say(TravelText.T("reallife_gp_hotel_no_skip"));
            }
            else if (goal.GetMinutesFromNow() < 10f)
            {
                Say(TravelText.T("reallife_travel_room_sleep_no_time"));
            }
            else
            {
                return false;
            }
            Show();
            return true;
        }

        private IEnumerator SleepRoutine(RoomType room, Timestamp goal, bool night)
        {
            if (CannotSkip(goal))
            {
                yield break;
            }
            float before = TimeHelper.NowInMinutes();
            _sleeping = true;
            Show();
            // As the game's own bed does (PlayerActivity.SleepActivity): no walking drain, the sleep rate on, time skipped.
            EnergyHelper.RemoveEnergySpender("move");
            EnergyHelper.SetCurrentEnergyRegen(room.Bed == 0 ? EnergyRegen.Bench : room.Bed == 1 ? EnergyRegen.Car : EnergyRegen.Bed);
            EnergyHelper.energyRegenMultiplier = Mathf.Max(0.5f, EnergyHelper.DefaultEnergyRegenMultiplier + room.BedBonus);
            yield return _director.Skip(goal, night ? "reallife_travel_tm_sleep" : "reallife_travel_tm_nap");
            EndSleepEffects();
            _sleeping = false;
            if (!TravelState.HasRoom)
            {
                yield break;   // the watchdog took the stay away meanwhile
            }
            float slept = TimeHelper.NowInMinutes() - before;
            Say(TravelText.T(TravelText.Pick(TravelCatalogue.SleptWell)));
            if (night && slept >= 240f)
            {
                TravelState.NightsSlept++;
                if (room.RoughNight > 0f && Random.value < room.RoughNight)
                {
                    TravelEvent rough = TravelState.Deal(TravelCatalogue.RoughNights, false, () => Random.value);
                    TravelEffects.Apply(rough);
                    if (rough != null && rough.Mood < 0)
                    {
                        ModState.AddModifier(TravelEffects.RoughNight);
                    }
                    if (rough != null)
                    {
                        Say(TravelText.T(rough.Key));
                    }
                }
                else if (room.Quality == 3)
                {
                    ModState.AddModifier(TravelEffects.Comfort);   // lasts into the next day
                    Say(TravelText.T("reallife_travel_room_slept_lux"));
                }
            }
            if (TravelState.InRoom)
            {
                Show();
            }
        }

        private IEnumerator RestRoutine(RoomType room)
        {
            Timestamp goal = Capped(TimeHelper.GetTimeInHours(2));
            if (CannotSkip(goal))
            {
                yield break;
            }
            _sleeping = true;
            Show();
            EnergyHelper.RemoveEnergySpender("move");
            EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Bench);
            EnergyHelper.energyRegenMultiplier = EnergyHelper.DefaultEnergyRegenMultiplier;
            yield return _director.Skip(goal, "reallife_travel_tm_rest");
            EndSleepEffects();
            _sleeping = false;
            if (!TravelState.HasRoom)
            {
                yield break;
            }
            int today = TimeHelper.CurrentDay;
            if (!TravelBridges.Home.Rest(2, room.Quality))
            {
                ModState.AddModifier(TravelEffects.Relaxed);
                if (TravelState.RestDay != today)
                {
                    TravelState.Streak = Mathf.Max(TravelState.Streak - 3, 0);   // an afternoon off undoes three days of the grind
                }
            }
            TravelState.RestDay = today;
            Say(TravelText.T("reallife_travel_room_rested"));
            if (TravelState.InRoom)
            {
                Show();
            }
        }

        // The cottages: two hours on the beach, with whatever the tide brings.
        private IEnumerator WalkRoutine()
        {
            int today = TimeHelper.CurrentDay;
            if (TravelState.WalkDay == today)
            {
                Say(TravelText.T("reallife_travel_room_walk_again"));
                Show();
                yield break;
            }
            Timestamp goal = Capped(TimeHelper.GetTimeInHours(2));
            if (CannotSkip(goal))
            {
                yield break;
            }
            _sleeping = true;
            Show();
            yield return _director.Skip(goal, "reallife_travel_tm_walk");
            _sleeping = false;
            if (!TravelState.HasRoom)
            {
                yield break;
            }
            TravelState.WalkDay = today;
            TravelEvent found = TravelState.Deal(TravelCatalogue.BeachWalks, false, () => Random.value);
            TravelEffects.Apply(found);
            if (found != null && found.Souvenir)
            {
                TravelState.Souvenirs++;
            }
            Say(found != null ? TravelText.T(found.Key) : null);
            if (TravelState.InRoom)
            {
                Show();
            }
        }

        private void StopSleep()
        {
            var uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
            {
                uis.timeMachine.StopTimeMachine(0f);
            }
        }
    }
}
